using System;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface IPriorityComponent
    {
        // daysLeft được PriorityCalculator tính 1 lần rồi truyền xuống để tránh
        // tính trùng và đảm bảo mọi component nhìn thấy cùng 1 mốc thời gian.
        double Score(StudyTask task, MonHoc mon, WeightConfig cfg, double daysLeft);
        double Weight(WeightConfig cfg);
    }

    public class TimeComponent : IPriorityComponent
    {
        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg, double daysLeft)
        {
            // Defensive: HorizonDays <= 0 không hợp lệ; WeightConfig.IsValid không
            // check field này nên guard tại đây để component dùng độc lập cũng an toàn.
            int horizon = cfg.HorizonDays;
            if (horizon <= 0) return 0;

            return Math.Max(0, 100.0 * (1.0 - daysLeft / horizon));
        }

        public double Weight(WeightConfig cfg) => cfg.TimeWeight;
    }

    public class TaskTypeComponent : IPriorityComponent
    {
        private readonly ITaskTypeWeightProvider _provider;

        public TaskTypeComponent(ITaskTypeWeightProvider provider)
        {
            _provider = provider;
        }

        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg, double daysLeft)
            => _provider.GetWeight(task.LoaiTask) * 100;

        public double Weight(WeightConfig cfg) => cfg.TaskTypeWeight;
    }

    public class CreditComponent : IPriorityComponent
    {
        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg, double daysLeft)
        {
            int tinChiHopLe = Math.Max(1, mon.SoTinChi);
            double diem = (tinChiHopLe / (double)cfg.MaxCredits) * 100;
            return diem > 100 ? 100 : diem;
        }

        public double Weight(WeightConfig cfg) => cfg.CreditWeight;
    }

    public class DifficultyComponent : IPriorityComponent
    {
        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg, double daysLeft)
        {
            int doKhoHopLe = Math.Min(cfg.MaxDifficulty, Math.Max(1, task.DoKho));
            return (doKhoHopLe / (double)cfg.MaxDifficulty) * 100;
        }

        public double Weight(WeightConfig cfg) => cfg.DifficultyWeight;
    }
}
