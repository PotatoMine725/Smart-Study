using SmartStudyPlanner.Core.Risk;
using SmartStudyPlanner.Core.Risk.Models;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using LegacyRiskLevel = SmartStudyPlanner.Services.RiskAnalyzer.RiskLevel;
using LegacyRiskAssessment = SmartStudyPlanner.Services.RiskAnalyzer.RiskAssessment;

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

        public LegacyRiskAssessment Assess(StudyTask task, MonHoc mon)
            => Map(_orchestrator.Assess(task, mon));

        private static LegacyRiskAssessment Map(Core.Risk.Models.RiskAssessment assessment) => new()
        {
            TaskId = assessment.TaskId,
            Score = assessment.Score,
            Level = assessment.Level switch
            {
                Core.Risk.Models.RiskLevel.Low => Core.Risk.Models.RiskLevel.Low,
                Core.Risk.Models.RiskLevel.Medium => Core.Risk.Models.RiskLevel.Medium,
                Core.Risk.Models.RiskLevel.High => Core.Risk.Models.RiskLevel.High,
                Core.Risk.Models.RiskLevel.Critical => Core.Risk.Models.RiskLevel.Critical,
                _ => Core.Risk.Models.RiskLevel.Low,
            },
            DeadlineUrgencyScore = assessment.DeadlineUrgencyScore,
            ProgressGapScore = assessment.ProgressGapScore,
            PerformanceDropScore = assessment.PerformanceDropScore,
        };
    }
}
