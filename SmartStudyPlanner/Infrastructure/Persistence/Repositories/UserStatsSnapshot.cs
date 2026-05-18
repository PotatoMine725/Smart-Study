using System;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Aggregate features cho M8-B WeightOptimizer. Tính từ StudyTasks + StudyLogs tại một mốc thời gian.
    /// </summary>
    public sealed class UserStatsSnapshot
    {
        public DateTime ReferenceUtc { get; init; }
        public int TotalTaskCount { get; init; }
        public int CompletedTaskCount { get; init; }
        public int OverdueTaskCount { get; init; }
        /// <summary>Tỷ lệ task quá hạn / tổng task — 0..1. Trả về 0 nếu không có task.</summary>
        public double MissRate { get; init; }
        /// <summary>Trung bình số ngày completed task hoàn thành sau HanChot (chỉ tính task hoàn thành trễ).</summary>
        public double AverageDelayDays { get; init; }
        /// <summary>Số ngày liên tiếp gần nhất (tính tới ReferenceUtc) có ít nhất 1 StudyLog.</summary>
        public int FocusStreakDays { get; init; }
        public int TotalStudyMinutesLast30Days { get; init; }
    }
}
