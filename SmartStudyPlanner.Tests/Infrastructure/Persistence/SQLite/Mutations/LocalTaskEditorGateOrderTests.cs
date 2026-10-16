using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 5 — the executor's gate order (plan §5): load → plan → fence →
    /// gate (RouteKnown ∧ FencePassed, else throw) → business validation → writer → one SaveChanges →
    /// commit, and roll back on any failure. Also: no-op requests write nothing (N-9), the writer never
    /// runs on a Blocked decision (N-6), and the planner's intents name exactly the rows the writer
    /// touches (so the fence judges what is written, not an approximation of it).
    /// </summary>
    public class LocalTaskEditorGateOrderTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();

        public void Dispose() => _fence.Dispose();

        private SyncApplyFixture Fx => _fence.Fx;

        private ITaskEditorRepository Repo => new SqliteTaskEditorRepository(Fx.Factory);

        private static TaskReferenceLink NewLink(Guid taskId, string title, int sortOrder = 0) =>
            new() { Id = Guid.NewGuid(), MaTask = taskId, Title = title, Url = "https://example.test/" + title, SortOrder = sortOrder };

        private static FenceDecision Blocked(Guid conflictId) => new(new[]
        {
            new PolicyResult(conflictId, "scope", ConflictShape.ConcurrentReparent,
                new EntitySubject(SyncEntityTypes.StudyTask, Guid.NewGuid()), FenceOutcome.Blocked,
                RoutingStage.DirectSubject, "S1CR.NonStructuralFields", "evidence"),
        }, RouteKnown: true);

        private static FenceDecision Passed() => new(Array.Empty<PolicyResult>(), RouteKnown: true);

        private static readonly EntityState[] Writes = { EntityState.Added, EntityState.Modified, EntityState.Deleted };

        // ------------------------------------------------------------------ N-6 + gate before validation

        [Fact]
        public async Task N6_BlockedDecision_ThrowsBeforeTheWriter_TrackerHasNoWrites()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var live = NewLink(task.MaTask, "live");
            await Fx.AddLocalAsync(live);

            using var db = Fx.NewContext();
            var tracked = await db.TaskReferenceLinks.SingleAsync(l => l.Id == live.Id);
            var plan = TaskEditorPlanner.PlanSave(task.MaTask, null, "note",
                new[] { tracked }, new[] { NewLink(task.MaTask, "added") });
            var conflictId = Guid.NewGuid();

            var ex = Assert.Throws<MutationRejectedException>(() => LocalTaskEditorExecutor.ApplyIfPassed(db, plan, Blocked(conflictId)));

            Assert.Equal(conflictId, Assert.Single(ex.Decision.Results).ConflictId);
            Assert.DoesNotContain(db.ChangeTracker.Entries(), e => Writes.Contains(e.State));
        }

        [Fact]
        public async Task RouteUnknown_IsRejected_EvenWithNoBlockingResult()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            using var db = Fx.NewContext();
            var plan = TaskEditorPlanner.PlanUpsertNote(task.MaTask, null, "x");

            Assert.Throws<MutationRejectedException>(() =>
                LocalTaskEditorExecutor.ApplyIfPassed(db, plan, new FenceDecision(Array.Empty<PolicyResult>(), RouteKnown: false)));
            Assert.DoesNotContain(db.ChangeTracker.Entries(), e => Writes.Contains(e.State));
        }

        /// <summary>The fence is evaluated first: a plan that would ALSO fail validation reports the rejection.</summary>
        [Fact]
        public async Task GateRunsBeforeBusinessValidation_BlockedAndInvalid_ThrowsTheRejection()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            using var db = Fx.NewContext();
            var invalid = TaskEditorPlanner.PlanUpdateLink(null, NewLink(task.MaTask, "missing"));
            Assert.NotNull(invalid.ValidationError);

            Assert.Throws<MutationRejectedException>(() => LocalTaskEditorExecutor.ApplyIfPassed(db, invalid, Blocked(Guid.NewGuid())));
            var validation = Assert.Throws<InvalidOperationException>(() => LocalTaskEditorExecutor.ApplyIfPassed(db, invalid, Passed()));
            Assert.Equal(invalid.ValidationError, validation.Message);
        }

        /// <summary>
        /// UpdateLinkAsync on a missing or tombstoned link names no row (no intent), passes vacuously, and
        /// then fails business validation with today's message. Nothing is written.
        /// </summary>
        [Fact]
        public async Task UpdateLink_TombstonedTarget_PassedFence_ThenTodaysValidationError_NothingWritten()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var link = NewLink(task.MaTask, "dead");
            await Fx.AddLocalAsync(link);
            using (var ctx = Fx.NewContext())
            {
                ctx.TaskReferenceLinks.Remove(await ctx.TaskReferenceLinks.SingleAsync(l => l.Id == link.Id));
                await ctx.SaveChangesAsync();
            }
            var before = await _fence.SnapshotAllTablesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Repo.UpdateLinkAsync(new TaskReferenceLink { Id = link.Id, MaTask = task.MaTask, Title = "x", Url = link.Url }));

            Assert.Equal($"TaskReferenceLink {link.Id} không tồn tại (hoặc đã bị xoá) — không thể cập nhật.", ex.Message);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            Assert.Empty(TaskEditorPlanner.PlanUpdateLink(null, link).Request.Intents);
        }

        // ------------------------------------------------------------------ persistence failure ⇒ rollback

        /// <summary>
        /// A failure INSIDE the one SaveChanges, on a row the request writes after the note: the "new"
        /// link reuses the id of a tombstoned link (PK collision). Before E2 the note had already been
        /// committed by its own call when the link add failed; now the executor rolls the transaction
        /// back, so the note is not persisted either, and the original exception surfaces.
        /// </summary>
        [Fact]
        public async Task PersistenceFailureMidSave_LinkPkCollision_RollsBackTheNoteToo_TablesIdentical()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var dead = NewLink(task.MaTask, "dead");
            await Fx.AddLocalAsync(dead);
            await Repo.DeleteLinkAsync(dead.Id);
            var before = await _fence.SnapshotAllTablesAsync();
            var colliding = NewLink(task.MaTask, "reused id");
            colliding.Id = dead.Id;

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
                Repo.SaveEditorAsync(task.MaTask, "new note", new[] { colliding }));

            Assert.Contains("UNIQUE constraint failed: TaskReferenceLinks.Id", ex.InnerException?.Message ?? ex.Message, StringComparison.Ordinal);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
        }

        /// <summary>
        /// D-1 (the unfiltered UNIQUE(MaTask) on TaskNotes, R-9 — reported, not fixed): under the executor
        /// it stays a non-fence persistence failure — the fence passes, SaveChanges throws, the whole
        /// request (link add included) is rolled back. OBSERVED, not reachable from the UI (Phase 0 §3e).
        /// </summary>
        [Fact]
        public async Task D1_LiveTaskWithDeadNote_NewNote_IsAPersistenceFailure_RolledBack()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "dead note" };
            await Fx.AddLocalAsync(note);
            using (var ctx = Fx.NewContext())
            {
                ctx.TaskNotes.Remove(await ctx.TaskNotes.SingleAsync(n => n.Id == note.Id));
                await ctx.SaveChangesAsync();
            }
            var before = await _fence.SnapshotAllTablesAsync();

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
                Repo.SaveEditorAsync(task.MaTask, "new note", new[] { NewLink(task.MaTask, "added") }));

            Assert.Contains("UNIQUE constraint failed: TaskNotes.MaTask", ex.InnerException?.Message ?? ex.Message, StringComparison.Ordinal);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
        }

        // ------------------------------------------------------------------ N-9 no-op ⇒ no write

        [Fact]
        public async Task NoOp_SameNoteSameLinks_EmptyRequest_NothingWritten()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            await Fx.AddLocalAsync(new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "same" });
            var link = NewLink(task.MaTask, "same", 0);
            await Fx.AddLocalAsync(link);
            var before = await _fence.SnapshotAllTablesAsync();
            var unchanged = new TaskReferenceLink { Id = link.Id, MaTask = task.MaTask, Title = link.Title, Url = link.Url, Category = link.Category, SortOrder = 0 };

            await Repo.SaveEditorAsync(task.MaTask, "same", new[] { unchanged });
            await Repo.UpsertNoteAsync(task.MaTask, "same");
            await Repo.UpdateLinkAsync(unchanged);
            await Repo.DeleteLinkAsync(Guid.NewGuid());                            // missing id: no-op, as today

            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
        }

        [Fact]
        public async Task NoOp_Plans_CarryNoIntents()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "same" };
            var link = NewLink(task.MaTask, "same");
            var copy = new TaskReferenceLink { Id = link.Id, MaTask = link.MaTask, Title = link.Title, Url = link.Url, SortOrder = link.SortOrder };

            Assert.Empty(TaskEditorPlanner.PlanUpsertNote(task.MaTask, note, "same").Request.Intents);
            Assert.Empty(TaskEditorPlanner.PlanUpdateLink(link, copy).Request.Intents);
            Assert.Empty(TaskEditorPlanner.PlanDeleteLink(null).Request.Intents);
            Assert.Empty(TaskEditorPlanner.PlanSave(task.MaTask, note, "same", new[] { link }, new[] { copy }).Request.Intents);
        }

        // ------------------------------------------------------------------ D-7 (owner ruling 4, 2026-10-16)

        /// <summary>
        /// D-7 (filed, separate ticket): re-tombstoning an already-deleted link re-stamps it. Slice 5 keeps
        /// <c>DeleteLinkAsync</c> byte-identical (no intent for a dead target, write unchanged), so the
        /// re-stamp is still there on the single method — pinned here so the D-7 fix flips it.
        /// </summary>
        [Fact]
        public async Task D7_DeleteLinkAsync_OnAlreadyDeletedLink_StillRestamps_Unchanged()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var link = NewLink(task.MaTask, "dead");
            await Fx.AddLocalAsync(link);
            await Repo.DeleteLinkAsync(link.Id);
            var revAfterFirstDelete = await RevOfLinkAsync(link.Id);

            await Repo.DeleteLinkAsync(link.Id);

            Assert.True(await RevOfLinkAsync(link.Id) > revAfterFirstDelete);
            Assert.Empty(TaskEditorPlanner.PlanDeleteLink(new TaskReferenceLink { Id = link.Id, MaTask = task.MaTask, IsDeleted = true }).Request.Intents);
        }

        /// <summary>D-7 is unreachable through SaveEditorAsync: its diff is over LIVE links only.</summary>
        [Fact]
        public async Task D7_SaveEditorAsync_NeverTouchesADeadLink()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var link = NewLink(task.MaTask, "dead");
            await Fx.AddLocalAsync(link);
            await Repo.DeleteLinkAsync(link.Id);
            var rev = await RevOfLinkAsync(link.Id);

            await Repo.SaveEditorAsync(task.MaTask, "note", Array.Empty<TaskReferenceLink>());

            Assert.Equal(rev, await RevOfLinkAsync(link.Id));
        }

        private async Task<long> RevOfLinkAsync(Guid id)
        {
            using var ctx = Fx.NewContext();
            return (await ctx.TaskReferenceLinks.AsNoTracking().SingleAsync(l => l.Id == id)).Rev;
        }

        // ------------------------------------------------------------------ intents ⇔ writes

        /// <summary>
        /// Every row the writer adds, modifies or deletes is named by an intent of the matching operation,
        /// and every intent names a row the writer touches. Create ↔ Added, UpdateFields ↔ Modified,
        /// Tombstone ↔ Deleted (the stamper turns it into a soft delete at SaveChanges).
        /// </summary>
        [Fact]
        public async Task PlanSave_IntentsNameExactlyTheRowsTheWriterTouches()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();
            var kept = NewLink(task.MaTask, "kept", 0);
            var removed = NewLink(task.MaTask, "removed", 1);
            await Fx.AddLocalAsync(kept);
            await Fx.AddLocalAsync(removed);
            var incoming = new[]
            {
                new TaskReferenceLink { Id = kept.Id, MaTask = task.MaTask, Title = "kept, edited", Url = kept.Url, SortOrder = 0 },
                NewLink(task.MaTask, "added", 1),
            };

            using var db = Fx.NewContext();
            var liveLinks = await db.TaskReferenceLinks.Where(l => l.MaTask == task.MaTask && !l.IsDeleted).ToListAsync();
            var plan = TaskEditorPlanner.PlanSave(task.MaTask, null, "new note", liveLinks, incoming);
            TaskEditorWriter.Apply(db, plan);
            db.ChangeTracker.DetectChanges();

            var written = db.ChangeTracker.Entries()
                .Where(e => Writes.Contains(e.State))
                .Select(e => (Type: TypeOf(e.Entity), Id: (Guid)e.Property("Id").CurrentValue!, Op: OpOf(e.State)))
                .OrderBy(x => x.Type).ThenBy(x => x.Id).ToList();
            var predicted = plan.Request.Intents
                .Select(i => (Type: i.EntityType, Id: i.EntityId, Op: i.Operation))
                .OrderBy(x => x.Type).ThenBy(x => x.Id).ToList();

            Assert.Equal(predicted, written);
            Assert.Equal(4, predicted.Count);                                       // note create, edit, delete, add
            var noteCreate = Assert.Single(plan.Request.Intents, i => i.EntityType == SyncEntityTypes.TaskNote);
            Assert.Equal(new RelationChange("MaTask", null, task.MaTask), Assert.Single(noteCreate.Relations));
            var linkAdd = Assert.Single(plan.Request.Intents, i => i.Operation == MutationOperation.Create && i.EntityType == SyncEntityTypes.TaskReferenceLink);
            Assert.Equal(new RelationChange("MaTask", null, task.MaTask), Assert.Single(linkAdd.Relations));
            var edit = Assert.Single(plan.Request.Intents, i => i.Operation == MutationOperation.UpdateFields);
            Assert.Equal(new[] { "Title" }, edit.ChangedFields);
            Assert.Empty(edit.Relations);
            Assert.Equal(MutationOrigin.LocalApplication, plan.Request.Origin);
        }

        private static string TypeOf(object entity) => entity switch
        {
            TaskNote => SyncEntityTypes.TaskNote,
            TaskReferenceLink => SyncEntityTypes.TaskReferenceLink,
            _ => entity.GetType().Name,
        };

        private static MutationOperation OpOf(EntityState state) => state switch
        {
            EntityState.Added => MutationOperation.Create,
            EntityState.Modified => MutationOperation.UpdateFields,
            EntityState.Deleted => MutationOperation.Tombstone,
            _ => throw new InvalidOperationException(state.ToString()),
        };
    }
}
