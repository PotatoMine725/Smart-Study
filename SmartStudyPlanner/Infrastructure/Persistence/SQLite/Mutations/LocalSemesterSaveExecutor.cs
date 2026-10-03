using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Fence;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slices 3+4: owns the context and the transaction of a local semester save --
    // load the old graph, plan, route the plan's MutationRequest through the fence, write, one
    // SaveChanges, commit or roll back (plan §5, §12.2, §13).
    //
    // Gate order. BEGIN takes SQLite's write lock (P0-b), and the fence reads on this same context
    // inside this same transaction, so no write can land between the decision and the write it
    // authorizes (§14, TOCTOU closed by construction). A decision that is not RouteKnown ∧ FencePassed
    // throws MutationRejectedException before SemesterGraphWriter runs (N-6): the writer is the code
    // that heals FKs on, and rewrites collections of, the caller's graph, and that issues every tracker
    // write. A passed decision authorizes nothing more than "no unresolved conflict is crossed" --
    // business validation (the unknown-MonHoc check, still thrown by the writer at its Slice-3 stage
    // so the caller-visible effect 7 is unchanged) and SaveChanges can still fail, and roll back.
    //
    // Stateless: nothing about a rejected request is recorded, queued or retried (rulings §2.4).
    // Origin is LocalApplication on the request and is never handed to a policy (INV-6).
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
                var hocKyCu = await LoadLiveGraphAsync(db, hocKy.MaHocKy, ct);

                var plan = SemesterReconcilePlanner.Plan(hocKyCu, hocKy);
                var decision = await FenceRouter.EvaluateAsync(db, plan.Request, ct);
                await ApplyIfPassedAsync(db, hocKyCu, hocKy, plan, decision, ct);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        // D-2: the old graph is live-only (IsDeleted == false, the ruled cascade predicate),
        // the same view LayDanhSachHocKyAsync gives the caller. Loading tombstones here made
        // every dead row look "removed" again on each save, and made EF's cascade fixup on a
        // removed MonHoc reach its dead tasks -- both re-stamped rows that were already dead.
        internal static Task<HocKy?> LoadLiveGraphAsync(AppDbContext db, Guid maHocKy, CancellationToken ct) =>
            db.HocKys
                .Include(h => h.DanhSachMonHoc.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.DanhSachTask.Where(t => !t.IsDeleted))
                .FirstOrDefaultAsync(h => h.MaHocKy == maHocKy, ct);

        // The gate itself, as the one step ExecuteAsync runs (N-6 tests call it directly). Throwing
        // here, inside ExecuteAsync's try, makes its catch the single rollback site.
        internal static async Task ApplyIfPassedAsync(
            AppDbContext db, HocKy? hocKyCu, HocKy hocKy, SemesterReconcilePlan plan, FenceDecision decision, CancellationToken ct)
        {
            if (!decision.RouteKnown || !decision.FencePassed)
                throw RejectAndRestore(db, decision, hocKyCu, hocKy);

            await SemesterGraphWriter.ApplyAsync(db, hocKyCu, hocKy, plan, ct);
        }

        // OD-7 (rulings §2.4; mechanism R2, owner ruling 2026-10-03): on rejection nothing is committed
        // (the caller's catch rolls back), the caller's graph is brought back to persisted state here,
        // BEFORE the throw -- so no production code ever has to catch MutationRejectedException (N-10)
        // -- and the rejection carries its rule ids to the global handler, which shows them. Nothing is
        // retried. The restore mutates UI-bound collections; that is safe because nothing under
        // Infrastructure/, Sync/ or Data/ uses ConfigureAwait(false), so a VM's awaited save resumes
        // here on the Dispatcher (the writer relies on the same thing on success).
        //
        // If the restore itself fails, the user must still get the rejection with its rule ids (owner
        // requirement 2026-10-03): the restore failure is attached to it, never thrown instead of it.
        private static MutationRejectedException RejectAndRestore(AppDbContext db, FenceDecision decision, HocKy? hocKyCu, HocKy hocKy)
        {
            var rejection = new MutationRejectedException(decision);
            try
            {
                SemesterGraphRestorer.Restore(db.Model, hocKyCu, hocKy);
            }
            catch (Exception restoreFailure)
            {
                rejection.Data[LocalSaveRejection.RestoreFailureDataKey] = restoreFailure;
            }
            return rejection;
        }
    }
}
