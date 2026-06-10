using SmartStudyPlanner.Core.Risk;
using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Core.Risk.Models;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>
    /// Backward-compatible facade cho Risk Orchestrator.
    /// </summary>
    public class RiskAnalyzerService : IRiskAnalyzer
    {
        private readonly RiskOrchestrator _orchestrator;

        public RiskAnalyzerService(IDecisionEngine decisionEngine, IClock clock)
        {
            _orchestrator = new RiskOrchestrator(decisionEngine, clock);
        }

        public RiskAnalyzerService(
            IRiskComponent deadlineComponent,
            IRiskComponent progressComponent,
            IRiskComponent performanceComponent,
            IClock clock)
        {
            _orchestrator = new RiskOrchestrator(deadlineComponent, progressComponent, performanceComponent, clock);
        }

        public RiskAssessment Assess(StudyTask task, MonHoc mon)
            => _orchestrator.Assess(task, mon);
    }
}
