using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public class MLModelManager : IMLModelManager
    {
        private readonly MLContext _mlContext = new(seed: 42);
        private readonly IModelStorageProvider _storage;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private ModelMeta _meta = new();
        private ITransformer? _model;

        public bool IsReady { get; private set; }

        public MLModelManager(IModelStorageProvider storage)
        {
            _storage = storage;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (_storage.ModelExists())
                {
                    try
                    {
                        await using var stream = _storage.OpenReadModel();
                        _model = _mlContext.Model.Load(stream, out _);

                        if (_storage.MetaExists())
                        {
                            await using var ms = _storage.OpenReadMeta();
                            _meta = await JsonSerializer.DeserializeAsync<ModelMeta>(ms, cancellationToken: ct) ?? new ModelMeta();
                        }

                        IsReady = true;
                        return;
                    }
                    catch
                    {
                        // Corrupt/empty zip from old scaffold — fall through to retrain
                        _model = null;
                    }
                }

                // No valid model on disk — train from seed data
                await RetrainInternalAsync(SeedDataGenerator.Generate(), ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RetrainAsync(IReadOnlyList<StudyTimeInput> data, CancellationToken ct = default)
        {
            if (data == null || data.Count == 0) data = SeedDataGenerator.Generate();

            await _gate.WaitAsync(ct);
            try
            {
                await RetrainInternalAsync(data, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Core training logic. Must be called while the caller holds <see cref="_gate"/>.
        /// </summary>
        private async Task RetrainInternalAsync(IReadOnlyList<StudyTimeInput> data, CancellationToken ct)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(data);

            var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

            var pipeline = _mlContext.Transforms.Categorical
                .OneHotEncoding("TaskTypeEncoded", "TaskType")
                .Append(_mlContext.Transforms.Concatenate(
                    "Features",
                    "TaskTypeEncoded", "Difficulty", "Credits", "DaysLeft", "StudiedMinutesSoFar"))
                .Append(_mlContext.Regression.Trainers.FastTree(
                    numberOfLeaves: 20,
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 5));

            var trained = await Task.Run(() => pipeline.Fit(split.TrainSet), ct);

            var predictions = trained.Transform(split.TestSet);
            var metrics = _mlContext.Regression.Evaluate(predictions);
            var r2 = (float)metrics.RSquared;

            if (r2 >= 0.45f)
            {
                // Atomic swap: write to temp, then copy to final path
                var tempZip = Path.Combine(_storage.BaseDirectory, $"study_time_{Guid.NewGuid():N}.tmp.zip");
                var tempMeta = Path.Combine(_storage.BaseDirectory, $"meta_{Guid.NewGuid():N}.tmp.json");
                try
                {
                    await using (var fs = File.Create(tempZip))
                    {
                        _mlContext.Model.Save(trained, dataView.Schema, fs);
                    }

                    var newMeta = new ModelMeta
                    {
                        LastRetrainedAt = DateTime.UtcNow.ToString("O"),
                        LogsUsedCount = data.Count,
                        ModelVersion = _meta.ModelVersion + 1,
                        SeedOnly = false,
                        DeviceId = DeviceHelper.GetId(),
                        ModelHash = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(
                                System.Text.Encoding.UTF8.GetBytes(
                                    string.Join('|', data.Take(5).Select(x => x.Label.ToString("0.##"))))
                            )).ToLowerInvariant()
                    };

                    await using (var fs = File.Create(tempMeta))
                    {
                        await JsonSerializer.SerializeAsync(fs, newMeta, cancellationToken: ct);
                    }

                    File.Copy(tempZip, _storage.ModelZipPath, overwrite: true);
                    File.Copy(tempMeta, _storage.MetaPath, overwrite: true);

                    _model = trained;
                    _meta = newMeta;
                }
                finally
                {
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                    if (File.Exists(tempMeta)) File.Delete(tempMeta);
                }
            }
            // If R² < 0.45: keep existing _model (may be null); do NOT persist zip or meta

            IsReady = true;
        }

        public Task<float> EvaluateR2Async(CancellationToken ct = default)
        {
            if (_model == null) return Task.FromResult(0f);

            return Task.Run(() =>
            {
                var evalData = SeedDataGenerator.Generate().Take(50).ToList();
                var dataView = _mlContext.Data.LoadFromEnumerable(evalData);
                var predictions = _model.Transform(dataView);
                var metrics = _mlContext.Regression.Evaluate(predictions);
                return (float)metrics.RSquared;
            }, ct);
        }

        public int PredictMinutes(StudyTimeInput input)
        {
            if (_model == null) return -1;

            // PredictionEngine is not thread-safe; create one per call (acceptable for MVP)
            var engine = _mlContext.Model.CreatePredictionEngine<StudyTimeInput, StudyTimeOutput>(_model);
            var score = engine.Predict(input).Score;
            return Math.Clamp((int)Math.Round(score), 10, 600);
        }
    }
}
