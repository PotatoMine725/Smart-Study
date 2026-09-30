using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 3: owns the context and the transaction of a local semester save --
    // load the old graph, plan, write, one SaveChanges, commit or roll back. No fence yet: Slice 4
    // puts it between the planner and the writer.
    internal sealed class LocalSemesterSaveExecutor
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public LocalSemesterSaveExecutor(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task ExecuteAsync(HocKy hocKy, CancellationToken ct = default)
        {
            if (hocKy == null) return;

            using var db = _ctxFactory();
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var hocKyCu = await db.HocKys
                    .Include(h => h.DanhSachMonHoc)
                    .ThenInclude(m => m.DanhSachTask)
                    .FirstOrDefaultAsync(h => h.MaHocKy == hocKy.MaHocKy, ct);

                var plan = SemesterReconcilePlanner.Plan(hocKyCu, hocKy);
                await SemesterGraphWriter.ApplyAsync(db, hocKyCu, hocKy, plan, ct);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}
