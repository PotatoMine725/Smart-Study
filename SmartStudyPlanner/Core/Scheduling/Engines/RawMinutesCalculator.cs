using System;
using SmartStudyPlanner.Core.Scheduling.Contracts;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Scheduling.Engines
{
    public sealed class RawMinutesCalculator : IRawMinutesCalculator
    {
        public int Calculate(StudyTask task)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh || task.DiemUuTien <= 0) return 0;

            double baseMinutes = (task.DiemUuTien / 100.0) * 120.0;
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;

            int totalMinutes = (int)(baseMinutes + difficultyBonus);
            return (int)Math.Round(totalMinutes / 15.0) * 15;
        }
    }
}
