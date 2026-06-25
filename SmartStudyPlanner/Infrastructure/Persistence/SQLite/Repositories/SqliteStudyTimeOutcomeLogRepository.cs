using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models.Telemetry;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteStudyTimeOutcomeLogRepository : IStudyTimeOutcomeLogRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteStudyTimeOutcomeLogRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task AddAsync(StudyTimeOutcomeLog entry, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.StudyTimeOutcomeLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<StudyTimeOutcomeLog>> GetAllAsync(CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyTimeOutcomeLogs.ToListAsync(ct);
        }

        public async Task<IReadOnlyList<StudyTimeOutcomeLog>> GetSinceAsync(DateTime since, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyTimeOutcomeLogs
                .Where(e => e.CreatedUtc >= since)
                .ToListAsync(ct);
        }

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.StudyTimeOutcomeLogs.CountAsync(ct);
        }
    }
}
