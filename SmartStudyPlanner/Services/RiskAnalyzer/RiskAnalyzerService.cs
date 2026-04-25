using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>
    /// Triển khai IRiskAnalyzer theo công thức từ README §4.3:
    /// Risk = DeadlineUrgency * 0.5 + ProgressGap * 0.3 + PerformanceDrop * 0.2
    /// </summary>
    public class RiskAnalyzerService : IRiskAnalyzer
    {
        private const double WeightDeadline    = 0.5;
        private const double WeightProgress    = 0.3;
        private const double WeightPerformance = 0.2;

        private readonly IRiskComponent _deadlineComponent;
        private readonly IRiskComponent _progressComponent;
        private readonly IRiskComponent _performanceComponent;
        private readonly IClock _clock;

        public RiskAnalyzerService(IDecisionEngine decisionEngine, IClock clock)
        {
            _clock                = clock;
            _deadlineComponent    = new DeadlineUrgencyRisk();
            _progressComponent    = new ProgressGapRisk(decisionEngine);
            _performanceComponent = new PerformanceDropRisk();
        }

        // Constructor cho unit test — inject mock components
        public RiskAnalyzerService(
            IRiskComponent deadlineComponent,
            IRiskComponent progressComponent,
            IRiskComponent performanceComponent,
            IClock clock)
        {
            _deadlineComponent    = deadlineComponent;
            _progressComponent    = progressComponent;
            _performanceComponent = performanceComponent;
            _clock                = clock;
        }

        public RiskAssessment Assess(StudyTask task, MonHoc mon)
        {
            double deadlineScore    = _deadlineComponent.Score(task, mon, _clock);
            double progressScore    = _progressComponent.Score(task, mon, _clock);
            double performanceScore = _performanceComponent.Score(task, mon, _clock);

            double total = deadlineScore * WeightDeadline
                         + progressScore * WeightProgress
                         + performanceScore * WeightPerformance;

            total = System.Math.Round(System.Math.Clamp(total, 0.0, 1.0), 3);

            return new RiskAssessment
            {
                Score                = total,
                Level                = RiskAssessment.FromScore(total),
                DeadlineUrgencyScore = System.Math.Round(deadlineScore, 3),
                ProgressGapScore     = System.Math.Round(progressScore, 3),
                PerformanceDropScore = System.Math.Round(performanceScore, 3),
            };
        }
    }
}
