using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Risk.Evaluators
{
    public class PerformanceDropRiskEvaluator : IRiskFactorEvaluator
    {
        private const int MaxDifficulty = 5;

        public string Name => "PerformanceDrop";

        public double Evaluate(StudyTask task, MonHoc mon, IClock clock)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh) return 0.0;

            int clamped = System.Math.Max(1, System.Math.Min(MaxDifficulty, task.DoKho));
            return (clamped - 1) / (double)(MaxDifficulty - 1);
        }
    }
}
