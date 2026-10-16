using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Fence;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 5: owns the context and the transaction of every task-editor write --
    // load the live rows, plan, route the plan's MutationRequest through the fence, write, one
    // SaveChanges, commit or roll back (plan §5, §13). Same gate order as LocalSemesterSaveExecutor:
    // BEGIN takes SQLite's write lock and the fence reads on this context inside this transaction, so
    // nothing lands between the decision and the write it authorizes. A decision that is not
    // RouteKnown ∧ FencePassed throws MutationRejectedException before TaskEditorWriter runs (N-6);
    // business validation (UpdateLinkAsync's missing target) runs after a passed fence, and SaveChanges
    // can still fail -- every failure rolls back.
    //
    // No in-memory state to restore: the editor port takes detached values, and the caller's form state
    // stays as typed on any failure (R-a, owner ruling 2026-10-16). Stateless: nothing about a rejected
    // request is recorded, queued or retried (rulings §2.4). Origin is LocalApplication on the request and
    // is never handed to a policy (INV-6). Each entry point returns its decision for tests (P-CR-5's
    // relevant-but-Passed entry); the repository discards it.
    internal sealed class LocalTaskEditorExecutor
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public LocalTaskEditorExecutor(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public Task<FenceDecision> UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default) =>
            RunAsync(async (db, c) => TaskEditorPlanner.PlanUpsertNote(taskId, await LiveNoteAsync(db, taskId, c), content), ct);

        public Task<FenceDecision> AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default) =>
            RunAsync((_, _) => Task.FromResult(TaskEditorPlanner.PlanAddLink(link)), ct);

        // Raw DbSet + explicit !IsDeleted: there is no global query filter, and a tombstoned row is not
        // an update target (resurrection is out of scope).
        public Task<FenceDecision> UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default) =>
            RunAsync(async (db, c) => TaskEditorPlanner.PlanUpdateLink(
                await db.TaskReferenceLinks.FirstOrDefaultAsync(l => l.Id == link.Id && !l.IsDeleted, c), link), ct);

        // Unfiltered, as before: a dead link is still found and re-removed (D-7, behaviour unchanged).
        public Task<FenceDecision> DeleteLinkAsync(Guid linkId, CancellationToken ct = default) =>
            RunAsync(async (db, c) => TaskEditorPlanner.PlanDeleteLink(
                await db.TaskReferenceLinks.FindAsync(new object[] { linkId }, c)), ct);

        // E2 (owner ruling 2026-10-16): one request, one decision, one transaction for the whole editor change.
        public Task<FenceDecision> SaveEditorAsync(Guid taskId, string? noteContent,
            IReadOnlyList<TaskReferenceLink> links, CancellationToken ct = default) =>
            RunAsync(async (db, c) => TaskEditorPlanner.PlanSave(taskId,
                await LiveNoteAsync(db, taskId, c), noteContent,
                await db.TaskReferenceLinks.Where(l => l.MaTask == taskId && !l.IsDeleted).OrderBy(l => l.SortOrder).ToListAsync(c),
                links), ct);

        private static Task<TaskNote?> LiveNoteAsync(AppDbContext db, Guid taskId, CancellationToken ct) =>
            db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId && !n.IsDeleted, ct);

        private async Task<FenceDecision> RunAsync(Func<AppDbContext, CancellationToken, Task<TaskEditorPlan>> loadAndPlan, CancellationToken ct)
        {
            using var db = _ctxFactory();
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var plan = await loadAndPlan(db, ct);
                var decision = await FenceRouter.EvaluateAsync(db, plan.Request, ct);
                ApplyIfPassed(db, plan, decision);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return decision;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        // The gate itself, as the one step RunAsync runs between the decision and the write (N-6 tests
        // call it directly). Throwing here, inside RunAsync's try, makes its catch the single rollback site.
        internal static void ApplyIfPassed(AppDbContext db, TaskEditorPlan plan, FenceDecision decision)
        {
            if (!(decision.RouteKnown && decision.FencePassed))
                throw new MutationRejectedException(decision);

            if (plan.ValidationError is { } message)
                throw new InvalidOperationException(message);

            TaskEditorWriter.Apply(db, plan);
        }
    }
}
