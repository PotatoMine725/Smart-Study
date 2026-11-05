using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Parsing.Contracts
{
    /// <summary>
    /// Augmentation port: ParsingOrchestrator có thể chạy mà không có implementer này (null = pure heuristic).
    /// Slice 5 (M8-A) sẽ cung cấp implementation qua Services/ML/TextClassifier.
    /// </summary>
    public interface IIntentClassifier
    {
        IntentPrediction? Classify(string rawInput);
    }

    public sealed class IntentPrediction
    {
        public LoaiCongViec? Loai { get; init; }
        public int? DoKho { get; init; }
        public double Confidence { get; init; }
    }
}
