using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services.ML.WeightOptimizer
{
    /// <summary>
    /// M8-B WeightOptimizer (rule-based MVP). Wrapper async mỏng quanh <see cref="WeightRuleEngine"/>:
    /// fetch <see cref="UserStatsSnapshot"/> qua EF async thật rồi chạy rule thuần. Không tự apply —
    /// chỉ trả về <see cref="WeightConfigSuggestion"/> tách rời cho UI (Slice 8) quyết định.
    /// </summary>
    public sealed class WeightOptimizerService : IWeightOptimizerService
    {
        private readonly IUserStatsRepository _statsRepository;
        private readonly IClock _clock;

        public WeightOptimizerService(IUserStatsRepository statsRepository, IClock clock)
        {
            _statsRepository = statsRepository;
            _clock = clock;
        }

        public async Task<WeightConfigSuggestion?> SuggestAsync(WeightConfig current, CancellationToken ct = default)
        {
            if (current == null)
                return null;

            var stats = await _statsRepository.GetSnapshotAsync(_clock.Now, ct);
            return WeightRuleEngine.Compute(current, stats);
        }
    }
}
