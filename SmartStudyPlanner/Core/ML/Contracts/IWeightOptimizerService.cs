using SmartStudyPlanner.Services;

namespace SmartStudyPlanner.Core.ML.Contracts
{
    /// <summary>
    /// Adapter port cho M8-B WeightOptimizer. Implementation ở Services/ML/WeightOptimizer/ (Slice 7),
    /// đọc features qua IUserStatsRepository (Slice 4) và trả về suggestion kèm confidence.
    /// </summary>
    public interface IWeightOptimizerService
    {
        WeightConfigSuggestion? Suggest(WeightConfig current);
    }

    public sealed class WeightConfigSuggestion
    {
        public required WeightConfig Suggested { get; init; }
        public double Confidence { get; init; }
        public string Rationale { get; init; } = string.Empty;
    }
}
