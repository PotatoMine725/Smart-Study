using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Lifecycle + inference port for the M8-A multiclass TextClassifier. Mirrors
    /// <see cref="IMLModelManager"/> so the service is unit-testable with a stub.
    /// </summary>
    public interface ITextClassifierModelManager
    {
        bool IsReady { get; }
        Task InitializeAsync(CancellationToken ct = default);
        Task RetrainAsync(IReadOnlyList<TextClassifierInput> data, CancellationToken ct = default);
        TextClassifierPrediction? Predict(TextClassifierInput input);
    }
}
