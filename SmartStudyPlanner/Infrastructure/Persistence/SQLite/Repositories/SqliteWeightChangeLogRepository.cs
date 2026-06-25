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
    public sealed class SqliteWeightChangeLogRepository : IWeightChangeLogRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteWeightChangeLogRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task AddAsync(WeightChangeLog entry, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.WeightChangeLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<WeightChangeLog>> GetPendingMaturationAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.WeightChangeLogs
                .AsNoTracking()
                .Where(e => e.OutcomeMaturedUtc == null
                         && e.AppliedUtc.AddDays(e.OutcomeWindowDays) <= nowUtc)
                .ToListAsync(ct);
        }

        public async Task UpdateOutcomeAsync(WeightChangeLog entry, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.WeightChangeLogs.Update(entry);
            await db.SaveChangesAsync(ct);
        }
    }
}
