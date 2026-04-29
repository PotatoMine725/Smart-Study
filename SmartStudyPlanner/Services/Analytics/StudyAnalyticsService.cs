using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics.Models;

namespace SmartStudyPlanner.Services.Analytics
{
    public sealed class StudyAnalyticsService : IStudyAnalytics
    {
        public WeeklyReport ComputeWeeklyMinutes(IEnumerable<StudyLog> logs, DateTime referenceDate)
        {
            var logList = logs.ToList();
            var labels  = new List<string>();
            var minutes = new List<int>();

            for (int i = 6; i >= 0; i--)
            {
                var day = referenceDate.Date.AddDays(-i);
                labels.Add(day.ToString("ddd dd/MM"));
                minutes.Add(logList.Where(l => l.NgayHoc.Date == day).Sum(l => l.SoPhutHoc));
            }

            return new WeeklyReport { DayLabels = labels, MinutesPerDay = minutes };
        }

        public List<SubjectInsight> ComputeSubjectInsights(HocKy hocKy, IEnumerable<StudyLog> logs)
        {
            var logsByTask = logs
                .GroupBy(l => l.MaTask)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.SoPhutHoc));

            return hocKy.DanhSachMonHoc.Select(mon =>
            {
                var tasks     = mon.DanhSachTask.ToList();
                var completed = tasks.Count(t => t.TrangThai == StudyTaskStatus.HoanThanh);
                var studied   = tasks.Sum(t =>
                    logsByTask.TryGetValue(t.MaTask, out var m) ? m : t.ThoiGianDaHoc);

                return new SubjectInsight
                {
                    SubjectName        = mon.TenMonHoc,
                    TotalTaskCount     = tasks.Count,
                    CompletedTaskCount = completed,
                    CompletionRate     = tasks.Count == 0 ? 0.0 : (double)completed / tasks.Count,
                    TotalStudyMinutes  = studied
                };
            }).ToList();
        }

        public ProductivityScore ComputeProductivityScore(double completionRate, int streakDays, double timeEfficiency)
        {
            var streakFactor = Math.Min(streakDays, 30) / 30.0;
            var raw = completionRate * 50.0 + streakFactor * 30.0 + timeEfficiency * 20.0;
            return new ProductivityScore { Value = (int)Math.Round(raw) };
        }
    }
}
