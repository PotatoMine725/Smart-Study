using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteUserStatsRepository : IUserStatsRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteUserStatsRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task<UserStatsSnapshot> GetSnapshotAsync(DateTime referenceUtc, CancellationToken ct = default)
        {
            using var db = _ctxFactory();

            var tasks = await db.StudyTasks.AsNoTracking().ToListAsync(ct);
            int total = tasks.Count;
            int completed = tasks.Count(t => t.TrangThai == StudyTaskStatus.HoanThanh);
            int overdue = tasks.Count(t =>
                t.TrangThai != StudyTaskStatus.HoanThanh && t.HanChot < referenceUtc);

            double missRate = total == 0 ? 0.0 : (double)overdue / total;

            var lateCompletions = tasks
                .Where(t => t.TrangThai == StudyTaskStatus.HoanThanh
                            && t.NgayHoanThanh.HasValue
                            && t.NgayHoanThanh.Value > t.HanChot)
                .Select(t => (t.NgayHoanThanh!.Value - t.HanChot).TotalDays)
                .ToList();
            double avgDelay = lateCompletions.Count == 0 ? 0.0 : lateCompletions.Average();

            DateTime since30 = referenceUtc.AddDays(-30);
            int totalMinutes30 = await db.StudyLogs
                .Where(l => !l.IsDeleted && l.NgayHoc >= since30 && l.NgayHoc <= referenceUtc)
                .SumAsync(l => l.SoPhutHoc, ct);

            var logDays = await db.StudyLogs
                .Where(l => !l.IsDeleted && l.NgayHoc <= referenceUtc)
                .Select(l => l.NgayHoc.Date)
                .Distinct()
                .ToListAsync(ct);
            int streak = ComputeStreak(logDays, referenceUtc.Date);

            return new UserStatsSnapshot
            {
                ReferenceUtc = referenceUtc,
                TotalTaskCount = total,
                CompletedTaskCount = completed,
                OverdueTaskCount = overdue,
                MissRate = missRate,
                AverageDelayDays = avgDelay,
                FocusStreakDays = streak,
                TotalStudyMinutesLast30Days = totalMinutes30,
            };
        }

        private static int ComputeStreak(System.Collections.Generic.List<DateTime> logDays, DateTime referenceDate)
        {
            if (logDays.Count == 0) return 0;
            var set = logDays.ToHashSet();
            int streak = 0;
            var cursor = referenceDate;
            while (set.Contains(cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }
            return streak;
        }
    }
}
