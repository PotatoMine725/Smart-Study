using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics.Models;

namespace SmartStudyPlanner.Services.Analytics
{
    public interface IStudyAnalytics
    {
        WeeklyReport       ComputeWeeklyMinutes(IEnumerable<StudyLog> logs, DateTime referenceDate);
        List<SubjectInsight> ComputeSubjectInsights(HocKy hocKy, IEnumerable<StudyLog> logs);
        ProductivityScore  ComputeProductivityScore(double completionRate, int streakDays, double timeEfficiency);
    }
}
