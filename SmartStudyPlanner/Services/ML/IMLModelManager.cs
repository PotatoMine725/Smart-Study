using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services.ML
{
    public interface IMLModelManager
    {
        bool IsReady { get; }
        Task InitializeAsync(CancellationToken ct = default);
        Task RetrainAsync(IReadOnlyList<Schema.StudyTimeInput> data, CancellationToken ct = default);
        Task<float> EvaluateR2Async(CancellationToken ct = default);

        /// <summary>
        /// Runs ML prediction for the given input.
        /// Returns -1 if the model is not loaded (signals caller to use formula fallback).
        /// </summary>
        int PredictMinutes(Schema.StudyTimeInput input);
    }
}
