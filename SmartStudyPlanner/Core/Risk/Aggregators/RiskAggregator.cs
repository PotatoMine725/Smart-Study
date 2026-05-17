using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Core.Risk.Models;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Risk.Aggregators
{
    public sealed class RiskAggregator
    {
        private const double WeightDeadline = 0.5;
        private const double WeightProgress = 0.3;
        private const double WeightPerformance = 0.2;

        private readonly IRiskFactorEvaluator _deadline;
        private readonly IRiskFactorEvaluator _progress;
        private readonly IRiskFactorEvaluator _performance;
        private readonly IClock _clock;

        public RiskAggregator(
            IRiskFactorEvaluator deadline,
            IRiskFactorEvaluator progress,
            IRiskFactorEvaluator performance,
            IClock clock)
        {
            _deadline = deadline;
            _progress = progress;
            _performance = performance;
            _clock = clock;
        }

        public RiskAssessment Assess(StudyTask task, MonHoc mon)
        {
            double deadlineScore = _deadline.Evaluate(task, mon, _clock);
            double progressScore = _progress.Evaluate(task, mon, _clock);
            double performanceScore = _performance.Evaluate(task, mon, _clock);

            double total = deadlineScore * WeightDeadline
                         + progressScore * WeightProgress
                         + performanceScore * WeightPerformance;

            total = System.Math.Round(System.Math.Clamp(total, 0.0, 1.0), 3);

            return new RiskAssessment
            {
                Score = total,
                Level = RiskAssessment.FromScore(total),
                DeadlineUrgencyScore = System.Math.Round(deadlineScore, 3),
                ProgressGapScore = System.Math.Round(progressScore, 3),
                PerformanceDropScore = System.Math.Round(performanceScore, 3),
            };
        }
    }
}
