using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Scheduling.Contracts;
using SmartStudyPlanner.Core.Scheduling.Engines;
using SmartStudyPlanner.Core.Scheduling.Evaluators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Scheduling.Orchestrators
{
    /// <summary>
    /// Compose toàn bộ luồng scheduling: priority -> raw minutes -> suggestion -> ML predict.
    /// Sở hữu WeightConfig + self-heal khi invalid; expose Config qua property cho facade.
    /// </summary>
    public sealed class SchedulingOrchestrator : ISchedulingOrchestrator
    {
        private readonly IPriorityEvaluator _priorityEvaluator;
        private readonly IRawMinutesCalculator _rawMinutesCalculator;
        private readonly IStudyTimeSuggestionEngine _suggestionEngine;
        private readonly IStudyTimePredictor _studyTimePredictor;
        private readonly IWeightOptimizerService? _weightOptimizer;
        private WeightConfig _config;

        public WeightConfig Config => _config;

        public SchedulingOrchestrator(
            ITaskTypeWeightProvider taskTypeProvider,
            IClock clock,
            IStudyTimePredictor studyTimePredictor,
            WeightConfig? initialConfig = null,
            IWeightOptimizerService? weightOptimizer = null)
        {
            _config = initialConfig ?? new WeightConfig();
            _studyTimePredictor = studyTimePredictor;
            _weightOptimizer = weightOptimizer;

            var calculator = new PriorityCalculator(
                cfgAccessor: () => _config,
                rules: new IUrgencyRule[]
                {
                    new OverdueRule(),
                    new JustOverdueRule(),
                    new ImminentRule(),
                    new CompletedRule(),
                    new BeyondHorizonRule(),
                },
                components: new IPriorityComponent[]
                {
                    new TimeComponent(),
                    new TaskTypeComponent(taskTypeProvider),
                    new CreditComponent(),
                    new DifficultyComponent(),
                },
                clock: clock);

            _priorityEvaluator = new PriorityEvaluator(calculator);
            _rawMinutesCalculator = new RawMinutesCalculator();
            _suggestionEngine = new StudyTimeSuggestionEngine(_rawMinutesCalculator);
        }

        public double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            if (!_config.IsValid())
                _config = new WeightConfig();

            return _priorityEvaluator.Evaluate(task, monHoc);
        }

        public int CalculateRawSuggestedMinutes(StudyTask task) => _rawMinutesCalculator.Calculate(task);

        public string SuggestStudyTime(StudyTask task) => _suggestionEngine.Suggest(task);

        public int PredictStudyMinutes(StudyTask task, MonHoc monHoc, out bool isMlPrediction)
        {
            var result = _studyTimePredictor.PredictAsync(task, monHoc).GetAwaiter().GetResult();
            isMlPrediction = result.IsMLPrediction;
            return result.Minutes;
        }

        public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
        {
            // READ-ONLY: chỉ đề xuất dựa trên _config hiện tại, KHÔNG ghi đè. Apply là việc của UI (Slice 8).
            if (_weightOptimizer == null)
                return Task.FromResult<WeightConfigSuggestion?>(null);

            return _weightOptimizer.SuggestAsync(_config, ct);
        }
    }
}
