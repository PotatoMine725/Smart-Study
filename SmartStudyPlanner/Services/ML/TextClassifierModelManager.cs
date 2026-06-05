using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Manages the lifecycle of the M8-A multiclass TextClassifier model.
    /// Mirrors <see cref="MLModelManager"/> (SemaphoreSlim gate, atomic temp-file swap, meta json)
    /// but owns its OWN paths (text_classifier.zip / text_classifier_meta.json) so it never clobbers
    /// the M7 StudyTime model.
    /// </summary>
    public class TextClassifierModelManager : ITextClassifierModelManager
    {
        private const string ModelZipName = "text_classifier.zip";
        private const string MetaJsonName = "text_classifier_meta.json";
        private const string SeedResourceSuffix = "seed_intents.csv";

        private readonly MLContext _ml = new(seed: 42);
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly string _baseDirectory;
        private readonly string _modelZipPath;
        private readonly string _metaPath;

        private ModelMeta _meta = new();
        private ITransformer? _model;

        public bool IsReady { get; private set; }

        public TextClassifierModelManager(string? baseDirectory = null)
        {
            _baseDirectory = baseDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner", "models");
            Directory.CreateDirectory(_baseDirectory);

            _modelZipPath = Path.Combine(_baseDirectory, ModelZipName);
            _metaPath = Path.Combine(_baseDirectory, MetaJsonName);
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (File.Exists(_modelZipPath))
                {
                    try
                    {
                        await using (var stream = File.OpenRead(_modelZipPath))
                        {
                            _model = _ml.Model.Load(stream, out _);
                        }

                        if (File.Exists(_metaPath))
                        {
                            await using var ms = File.OpenRead(_metaPath);
                            _meta = await JsonSerializer.DeserializeAsync<ModelMeta>(ms, cancellationToken: ct) ?? new ModelMeta();
                        }

                        // Seed-version gate: a cached SEED-ONLY model trained from an older embedded
                        // seed is stale (e.g. v2 3-class cache vs v3 5-class seed) → retrain from the
                        // current seed. User-trained models (SeedOnly == false) are preserved.
                        bool staleSeed = _meta.SeedOnly &&
                            !string.Equals(_meta.SeedHash, ComputeSeedHash(), StringComparison.OrdinalIgnoreCase);
                        if (!staleSeed)
                        {
                            IsReady = true;
                            return;
                        }
                        // else fall through to retrain from the new seed below.
                    }
                    catch
                    {
                        // Corrupt/empty zip — fall through to seed-train.
                        _model = null;
                    }
                }

                // No valid model on disk — train from embedded seed CSV and persist (SeedOnly).
                var seed = LoadSeedData();
                await TrainAndSaveAsync(seed, seedOnly: true, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RetrainAsync(IReadOnlyList<TextClassifierInput> data, CancellationToken ct = default)
        {
            if (data == null || data.Count == 0) data = LoadSeedData();

            await _gate.WaitAsync(ct);
            try
            {
                await TrainAndSaveAsync(data, seedOnly: false, ct);
            }
            finally
            {
                _gate.Release();
            }
        }

        public TextClassifierPrediction? Predict(TextClassifierInput input)
        {
            if (_model == null || input == null) return null;

            // PredictionEngine is not thread-safe; create one per call (acceptable for MVP, matches M7).
            var engine = _ml.Model.CreatePredictionEngine<TextClassifierInput, TextClassifierOutput>(_model);
            var output = engine.Predict(input);

            LoaiCongViec? loai = null;
            if (!string.IsNullOrWhiteSpace(output.PredictedLabel) &&
                Enum.TryParse<LoaiCongViec>(output.PredictedLabel, out var parsed))
            {
                loai = parsed;
            }

            double confidence = output.Score is { Length: > 0 } ? output.Score.Max() : 0d;

            return new TextClassifierPrediction
            {
                Loai = loai,
                Confidence = confidence,
            };
        }

        /// <summary>
        /// Trains the multiclass pipeline and atomically persists the model + meta.
        /// For the seed model we persist unconditionally (no accuracy gate, no TrainTestSplit —
        /// the seed set is tiny and would degenerate). User retrains may be evaluated by callers
        /// via MulticlassClassification.Evaluate / MicroAccuracy if needed.
        /// </summary>
        private async Task TrainAndSaveAsync(IReadOnlyList<TextClassifierInput> data, bool seedOnly, CancellationToken ct)
        {
            var dataView = _ml.Data.LoadFromEnumerable(data);

            var pipeline = _ml.Transforms.Conversion.MapValueToKey("Label", "TaskType")
                .Append(_ml.Transforms.Text.FeaturizeText("Features", "InputText"))
                .Append(_ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var trained = await Task.Run(() => pipeline.Fit(dataView), ct);

            // Atomic swap: write to temp files, then copy to the final paths.
            var tempZip = Path.Combine(_baseDirectory, $"text_classifier_{Guid.NewGuid():N}.tmp.zip");
            var tempMeta = Path.Combine(_baseDirectory, $"text_classifier_meta_{Guid.NewGuid():N}.tmp.json");
            try
            {
                await using (var fs = File.Create(tempZip))
                {
                    _ml.Model.Save(trained, dataView.Schema, fs);
                }

                var newMeta = new ModelMeta
                {
                    LastRetrainedAt = DateTime.UtcNow.ToString("O"),
                    LogsUsedCount = data.Count,
                    ModelVersion = _meta.ModelVersion + 1,
                    SeedOnly = seedOnly,
                    SeedHash = ComputeSeedHash(),
                    DeviceId = DeviceHelper.GetId(),
                    ModelHash = Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(
                                string.Join('|', data.Take(5).Select(x => x.TaskType))))
                        ).ToLowerInvariant()
                };

                await using (var fs = File.Create(tempMeta))
                {
                    await JsonSerializer.SerializeAsync(fs, newMeta, cancellationToken: ct);
                }

                File.Copy(tempZip, _modelZipPath, overwrite: true);
                File.Copy(tempMeta, _metaPath, overwrite: true);

                _model = trained;
                _meta = newMeta;
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
                if (File.Exists(tempMeta)) File.Delete(tempMeta);
            }

            IsReady = true;
        }

        /// <summary>
        /// Loads and parses the embedded seed CSV. Resolves the manifest name robustly by suffix
        /// so a namespace/folder rename can't silently break it.
        /// </summary>
        private static List<TextClassifierInput> LoadSeedData()
        {
            var asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(SeedResourceSuffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                throw new InvalidOperationException(
                    $"Embedded seed resource ending in '{SeedResourceSuffix}' was not found. " +
                    "Verify the EmbeddedResource entry in SmartStudyPlanner.csproj.");
            }

            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Could not open embedded resource '{resourceName}'.");

            return TextClassifierDatasetImporter.Parse(stream);
        }

        /// <summary>
        /// SHA-256 (hex) of the raw embedded seed bytes. Used to detect when the seed shipped in the
        /// assembly differs from the seed a cached seed-only model was trained on. Returns empty if
        /// the resource is missing (callers then treat the cache as non-stale to avoid a crash loop).
        /// </summary>
        private static string ComputeSeedHash()
        {
            var asm = Assembly.GetExecutingAssembly();
            string? resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(SeedResourceSuffix, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return string.Empty;

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return string.Empty;

            using var sha = System.Security.Cryptography.SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
    }
}
