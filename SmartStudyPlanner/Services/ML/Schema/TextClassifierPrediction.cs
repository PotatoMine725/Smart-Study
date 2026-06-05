using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.ML.Schema
{
    /// <summary>
    /// Lightweight domain DTO returned by <c>ITextClassifierModelManager</c>. The service maps
    /// this into the Core <c>IntentPrediction</c>.
    /// </summary>
    public sealed class TextClassifierPrediction
    {
        public LoaiCongViec? Loai { get; init; }
        public double Confidence { get; init; }
    }
}
