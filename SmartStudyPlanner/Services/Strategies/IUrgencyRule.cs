using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface IUrgencyRule
    {
        bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score);
    }

    public class OverdueRule : IUrgencyRule
    {
        public bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score)
        {
            score = 0;
            if (daysLeft < -3) { score = 0.0; return true; }
            return false;
        }
    }

    public class JustOverdueRule : IUrgencyRule
    {
        public bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score)
        {
            score = 0;
            if (daysLeft < 0) { score = 100.0; return true; }
            return false;
        }
    }

    public class ImminentRule : IUrgencyRule
    {
        public bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score)
        {
            score = 0;
            if (daysLeft < 1) { score = 95.0; return true; }
            return false;
        }
    }

    public class CompletedRule : IUrgencyRule
    {
        public bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score)
        {
            score = 0;
            if (task.TrangThai == "Hoàn thành") { score = 0.0; return true; }
            return false;
        }
    }

    public class BeyondHorizonRule : IUrgencyRule
    {
        public bool TryApply(StudyTask task, double daysLeft, WeightConfig cfg, out double score)
        {
            score = 0;
            if (daysLeft > cfg.HorizonDays) { score = 1.0; return true; }
            return false;
        }
    }
}
