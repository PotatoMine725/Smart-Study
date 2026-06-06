using SmartStudyPlanner.Core.Scheduling.Contracts;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Scheduling.Engines
{
    public sealed class StudyTimeSuggestionEngine : IStudyTimeSuggestionEngine
    {
        private readonly IRawMinutesCalculator _rawCalculator;

        public StudyTimeSuggestionEngine(IRawMinutesCalculator rawCalculator)
        {
            _rawCalculator = rawCalculator;
        }

        public string Suggest(StudyTask task)
        {
            int totalMinutes = _rawCalculator.Calculate(task);
            if (totalMinutes == 0) return "0 phút";

            int remainingMinutes = totalMinutes - task.ThoiGianDaHoc;

            if (remainingMinutes <= 0) return "Đã đạt mục tiêu 🎉";
            if (remainingMinutes < 60) return $"{remainingMinutes} phút";

            int hours = remainingMinutes / 60;
            int mins = remainingMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}p" : $"{hours}h";
        }
    }
}
