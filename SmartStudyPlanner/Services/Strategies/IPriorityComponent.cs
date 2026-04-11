using System;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface IPriorityComponent
    {
        double Score(StudyTask task, MonHoc mon, WeightConfig cfg);
        double Weight(WeightConfig cfg);
    }

    public class TimeComponent : IPriorityComponent
    {
        private readonly IClock _clock;

        public TimeComponent(IClock clock)
        {
            _clock = clock;
        }

        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg)
        {
            double daysLeft = (task.HanChot.Date - _clock.Now.Date).TotalDays;
            return Math.Max(0, 100.0 * (1.0 - daysLeft / cfg.HorizonDays));
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

        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg)
            => _provider.GetWeight(task.LoaiTask) * 100;

        public double Weight(WeightConfig cfg) => cfg.TaskTypeWeight;
    }

    public class CreditComponent : IPriorityComponent
    {
        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg)
        {
            int tinChiHopLe = Math.Max(1, mon.SoTinChi);
            double diem = (tinChiHopLe / (double)cfg.MaxCredits) * 100;
            return diem > 100 ? 100 : diem;
        }

        public double Weight(WeightConfig cfg) => cfg.CreditWeight;
    }

    public class DifficultyComponent : IPriorityComponent
    {
        public double Score(StudyTask task, MonHoc mon, WeightConfig cfg)
        {
            int doKhoHopLe = Math.Min(cfg.MaxDifficulty, Math.Max(1, task.DoKho));
            return (doKhoHopLe / (double)cfg.MaxDifficulty) * 100;
        }

        public double Weight(WeightConfig cfg) => cfg.DifficultyWeight;
    }
}
