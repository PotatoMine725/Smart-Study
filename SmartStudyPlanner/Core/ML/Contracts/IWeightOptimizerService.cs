using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Services;

namespace SmartStudyPlanner.Core.ML.Contracts
{
    /// <summary>
    /// Adapter port cho M8-B WeightOptimizer. Implementation ở Services/ML/WeightOptimizer/ (Slice 7),
    /// đọc features qua IUserStatsRepository (Slice 4) và trả về suggestion kèm confidence.
    /// Async: snapshot được fetch bằng EF async thật — KHÔNG sync-over-async để tránh deadlock UI thread.
    /// </summary>
    public interface IWeightOptimizerService
    {
        Task<WeightConfigSuggestion?> SuggestAsync(WeightConfig current, CancellationToken ct = default);
    }

    public sealed class WeightConfigSuggestion
    {
        public required WeightConfig Suggested { get; init; }
        public double Confidence { get; init; }
        public string Rationale { get; init; } = string.Empty;
    }
}
