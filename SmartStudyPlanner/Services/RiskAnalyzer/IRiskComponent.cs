using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Core.Risk.Evaluators;

namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>
    /// Backward-compatible bridge để không phá code cũ trong lúc tách Core/Risk.
    /// </summary>
    public interface IRiskComponent : IRiskFactorEvaluator { }

    public sealed class DeadlineUrgencyRisk : DeadlineUrgencyRiskEvaluator, IRiskComponent { }
    public sealed class ProgressGapRisk : ProgressGapRiskEvaluator, IRiskComponent
    {
        public ProgressGapRisk(SmartStudyPlanner.Services.IDecisionEngine engine) : base(engine) { }
    }
    public sealed class PerformanceDropRisk : PerformanceDropRiskEvaluator, IRiskComponent { }
}
