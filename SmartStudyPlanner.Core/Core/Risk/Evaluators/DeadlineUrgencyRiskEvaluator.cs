using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Risk.Evaluators
{
    public class DeadlineUrgencyRiskEvaluator : IRiskFactorEvaluator
    {
        public string Name => "DeadlineUrgency";

        public double Evaluate(StudyTask task, MonHoc mon, IClock clock)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh) return 0.0;

            double daysLeft = (task.HanChot.Date - clock.Now.Date).TotalDays;
            if (daysLeft < 0) return 1.0;

            return 1.0 / (daysLeft + 1);
        }
    }
}
