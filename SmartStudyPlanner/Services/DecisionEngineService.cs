using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Scheduling.Orchestrators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Backward-compatible facade cho <see cref="SchedulingOrchestrator"/>.
    /// Giữ nguyên chữ ký <see cref="IDecisionEngine"/> để ViewModels/Pipeline không phải đổi.
    /// </summary>
    public class DecisionEngineService : IDecisionEngine
    {
        private readonly SchedulingOrchestrator _orchestrator;

        public WeightConfig Config => _orchestrator.Config;

        public DecisionEngineService(
            ITaskTypeWeightProvider taskTypeProvider,
            IClock clock,
            IStudyTimePredictor studyTimePredictor,
            WeightConfig? initialConfig = null,
            IWeightOptimizerService? weightOptimizer = null)
        {
            _orchestrator = new SchedulingOrchestrator(taskTypeProvider, clock, studyTimePredictor, initialConfig, weightOptimizer);
        }

        public double CalculatePriority(StudyTask task, MonHoc monHoc)
            => _orchestrator.CalculatePriority(task, monHoc);

        public int CalculateRawSuggestedMinutes(StudyTask task)
            => _orchestrator.CalculateRawSuggestedMinutes(task);

        public string SuggestStudyTime(StudyTask task)
            => _orchestrator.SuggestStudyTime(task);

        public int PredictStudyMinutes(StudyTask task, MonHoc monHoc, out bool isMlPrediction)
            => _orchestrator.PredictStudyMinutes(task, monHoc, out isMlPrediction);

        public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
            => _orchestrator.SuggestWeightConfigAsync(ct);
    }
}
