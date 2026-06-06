using SmartStudyPlanner.Core.Risk.Aggregators;
using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Core.Risk.Models;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Risk
{
    public sealed class RiskOrchestrator
    {
        private readonly RiskAggregator _aggregator;

        public RiskOrchestrator(IDecisionEngine decisionEngine, IClock clock)
        {
            _aggregator = new RiskAggregator(
                new Evaluators.DeadlineUrgencyRiskEvaluator(),
                new Evaluators.ProgressGapRiskEvaluator(decisionEngine),
                new Evaluators.PerformanceDropRiskEvaluator(),
                clock);
        }

        public RiskOrchestrator(
            IRiskFactorEvaluator deadline,
            IRiskFactorEvaluator progress,
            IRiskFactorEvaluator performance,
            IClock clock)
        {
            _aggregator = new RiskAggregator(deadline, progress, performance, clock);
        }

        public RiskAssessment Assess(StudyTask task, MonHoc mon) => _aggregator.Assess(task, mon);
    }
}
