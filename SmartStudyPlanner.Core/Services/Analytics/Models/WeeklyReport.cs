using System.Collections.Generic;
using System.Linq;
namespace SmartStudyPlanner.Services.Analytics.Models
{
    public sealed class WeeklyReport
    {
        public List<string> DayLabels    { get; init; } = new();
        public List<int>    MinutesPerDay { get; init; } = new();
        public int TotalMinutes => MinutesPerDay.Sum();
    }
}
