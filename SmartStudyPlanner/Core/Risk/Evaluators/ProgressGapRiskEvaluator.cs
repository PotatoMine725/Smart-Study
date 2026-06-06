using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Risk.Evaluators
{
    public class ProgressGapRiskEvaluator : IRiskFactorEvaluator
    {
        private readonly IDecisionEngine _engine;

        public ProgressGapRiskEvaluator(IDecisionEngine engine)
        {
            _engine = engine;
        }

        public string Name => "ProgressGap";

        public double Evaluate(StudyTask task, MonHoc mon, IClock clock)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh) return 0.0;

            int suggested = _engine.CalculateRawSuggestedMinutes(task);
            if (suggested <= 0) return 0.0;

            double progress = System.Math.Min(1.0, task.ThoiGianDaHoc / (double)suggested);
            return 1.0 - progress;
        }
    }
}
