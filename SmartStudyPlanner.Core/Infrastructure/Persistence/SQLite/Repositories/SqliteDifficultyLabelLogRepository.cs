using System;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models.Telemetry;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteDifficultyLabelLogRepository : IDifficultyLabelLogRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteDifficultyLabelLogRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task AddAsync(DifficultyLabelLog entry, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.DifficultyLabelLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
    }
}
