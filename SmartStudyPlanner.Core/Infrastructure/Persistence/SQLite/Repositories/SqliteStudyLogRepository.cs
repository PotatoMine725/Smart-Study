using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteStudyLogRepository : IStudyLogRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteStudyLogRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task AddAsync(StudyLog log, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.StudyLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<StudyLog>> GetByTaskAsync(Guid maTask, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyLogs
                .Where(l => l.MaTask == maTask && !l.IsDeleted)
                .OrderBy(l => l.NgayHoc)
                .ToListAsync(ct);
        }

        public async Task<List<StudyLog>> GetForHocKyAsync(HocKy hocKy, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            var taskIds = hocKy.DanhSachMonHoc
                .SelectMany(m => m.DanhSachTask)
                .Select(t => t.MaTask)
                .ToHashSet();
            return await db.StudyLogs
                .Where(l => taskIds.Contains(l.MaTask) && !l.IsDeleted)
                .OrderBy(l => l.NgayHoc)
                .ToListAsync(ct);
        }

        public async Task<List<StudyLog>> GetSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyLogs
                .Where(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted)
                .OrderBy(l => l.CreatedAtUtc)
                .ToListAsync(ct);
        }
    }
}
