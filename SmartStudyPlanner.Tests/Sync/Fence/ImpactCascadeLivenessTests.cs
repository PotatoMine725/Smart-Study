using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 — review finding <b>M-3/A-1, owner-ratified 2026-09-17</b>:
    /// <b>LIVE-ONLY EFFECTIVE CASCADE</b>.
    /// <para>
    /// The fence <see cref="ImpactSet"/> models the SEMANTIC domain effects of the requested mutation,
    /// not implementation-level re-stamping. The effective cascade predicate is therefore
    /// <c>child.IsDeleted == false</c>:
    /// </para>
    /// <list type="bullet">
    /// <item>a LIVE child is an effective cascade target — it is tombstoned, and it RELEASES the
    ///       constraint scope it occupied;</item>
    /// <item>an ALREADY-TOMBSTONED child is NOT a new cascade lifecycle target — no
    ///       <see cref="RowEffect.CascadeTombstoned"/>, no <see cref="LifecycleEffect.Tombstone"/>, and
    ///       critically no scope release, even though a production path
    ///       (<c>TaskCascadeHelper</c>) may re-stamp its <c>Rev</c>/provenance.</item>
    /// </list>
    /// <para>
    /// <b>D9-T1 is preserved and is asserted here.</b> The <c>UNIQUE(MaTask)</c> index on TaskNote is
    /// unfiltered, so a tombstoned note STILL OCCUPIES its scope. The dead-child case must therefore
    /// distinguish <i>"the scope remains occupied but this mutation does not release it"</i> from
    /// <i>"the scope does not exist"</i> — two different states that would produce the same fence
    /// output. Every dead-child assertion below is paired with a DIRECT database read proving the row
    /// is still present at <c>MaTask == T</c>, so the test cannot pass by having destroyed the
    /// occupancy it means to protect.
    /// </para>
    /// <para>
    /// Real SQLite throughout (<see cref="SyncApplyFixture"/>): the behaviour under test is relational
    /// — cascade enumeration, an unfiltered unique index, and soft-delete semantics.
    /// </para>
    /// </summary>
    public class ImpactCascadeLivenessTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static MutationRequest TombstoneTask(Guid taskId) =>
            new(MutationOrigin.LocalApplication, new[]
            {
                new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.StudyTask, taskId,
                    Array.Empty<RelationChange>(), Array.Empty<string>()),
            });

        private static string ScopeKeyOf(Guid taskId) =>
            ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null,
                new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", taskId.ToString("D")));

        /// <summary>
        /// Seeds HocKy/MonHoc/StudyTask + one TaskNote in T's <c>MaTask</c> scope, optionally
        /// tombstoning the note, plus an unresolved S2 ConstraintOccupancy record at exactly
        /// <c>K(T)</c>. The record's ScopeKey is built by the same <see cref="ConflictKeys"/> call the
        /// resolver uses, so the selector cannot miss it for a formatting reason.
        /// </summary>
        private async Task<(StudyTask Task, Guid NoteId, SyncConflictRecordRow Record)> SeedAsync(bool noteAlreadyDead)
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();

            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "occupant" };
            await _fx.Fx.AddLocalAsync(note);

            if (noteAlreadyDead)
            {
                using var kill = _fx.Fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE");
                var live = await kill.TaskNotes.FirstAsync(n => n.Id == note.Id);
                kill.TaskNotes.Remove(live); // soft delete: sets IsDeleted, keeps the row and its MaTask
                await kill.SaveChangesAsync();
            }

            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "m3-" + Guid.NewGuid().ToString("N"),
                ScopeKey = ScopeKeyOf(task.MaTask),
                Kind = ConflictKind.ConstraintConflict,
                EntityType = SyncEntityTypes.TaskNote,
                ConstraintKey = "MaTask",
                ConstraintValue = task.MaTask.ToString("D"),
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = note.Id,
                BaseSnapshotJson = "{}",
                BaseFingerprint = "m3-base-fp",
                LocalEntityId = note.Id,
                LocalSnapshotJson = "{}",
                LocalFingerprint = "m3-base-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "m3-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };

            using (var seed = _fx.Fx.NewContext())
            {
                seed.SyncConflictRecords.Add(row);
                await seed.SaveChangesAsync();
            }

            return (task, note.Id, row);
        }

        /// <summary>Reads the note straight from SQLite, bypassing the fence entirely — the independent
        /// channel that proves D9-T1 occupancy rather than inferring it from fence output.</summary>
        private async Task<TaskNote> ReadNoteDirectAsync(Guid noteId)
        {
            using var ctx = _fx.Fx.NewContext();
            return await ctx.TaskNotes.AsNoTracking().IgnoreQueryFilters().FirstAsync(n => n.Id == noteId);
        }

        // ==================================================================
        // TEST M3-LIVE — a live child IS an effective cascade target
        // ==================================================================

        /// <summary>
        /// M3-LIVE. <c>Tombstone(T)</c> with a LIVE TaskNote N at <c>K(T)</c>:
        /// N is included in the cascade impact, <c>K(T)</c> is RELEASED, the S2 record at that scope is
        /// selected, and the constraint policy blocks per the existing contract.
        /// </summary>
        [Fact]
        public async Task M3Live_LiveTaskNote_IsCascadeTarget_AndReleasesItsScope()
        {
            var (task, noteId, record) = await SeedAsync(noteAlreadyDead: false);

            // Precondition, read directly: the note is LIVE and occupies K(T).
            var before = await ReadNoteDirectAsync(noteId);
            Assert.False(before.IsDeleted);
            Assert.Equal(task.MaTask, before.MaTask);

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, TombstoneTask(task.MaTask));

            // (1) N is an effective cascade target.
            var noteRow = Assert.Single(impact.Rows,
                r => r.EntityType == SyncEntityTypes.TaskNote && r.EntityId == noteId);
            Assert.Equal(RowEffect.CascadeTombstoned, noteRow.Effect);
            Assert.True(noteRow.WasLive);
            Assert.Equal(task.MaTask, noteRow.CausedByEntityId);

            // (2) a new lifecycle tombstone effect is produced for it.
            Assert.Contains(impact.Lifecycle,
                l => l.EntityType == SyncEntityTypes.TaskNote && l.EntityId == noteId &&
                     l.Effect == LifecycleEffect.Tombstone);

            // (3) K(T) is RELEASED by this mutation.
            var released = Assert.Single(impact.Scopes,
                s => s.ScopeKey == ScopeKeyOf(task.MaTask) && s.Change == ScopeChange.Released);
            Assert.Equal(noteId, released.OccupantId);

            // (4) the record is selected and the policy blocks (existing contract, unchanged).
            using var routed = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(routed, TombstoneTask(task.MaTask));
            var result = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("CONS.ScopeReleased", result.RuleId);
            Assert.False(decision.FencePassed);
        }

        // ==================================================================
        // TEST M3-DEAD — an already-tombstoned child is NOT a new cascade target
        // ==================================================================

        /// <summary>
        /// M3-DEAD. Same shape, except N is ALREADY tombstoned before the request.
        /// <para>
        /// N must not appear as a new <see cref="RowEffect.CascadeTombstoned"/> effect and must not
        /// produce a scope release — an implementation path may re-stamp its <c>Rev</c>/provenance, but
        /// that is not a semantic lifecycle transition. Meanwhile <c>K(T)</c> is still OCCUPIED (D9-T1),
        /// which the direct DB read below establishes independently of anything the fence reports.
        /// </para>
        /// </summary>
        [Fact]
        public async Task M3Dead_AlreadyTombstonedTaskNote_IsNotANewCascadeTarget_AndReleasesNothing()
        {
            var (task, noteId, record) = await SeedAsync(noteAlreadyDead: true);

            // ---- D9-T1, established on an INDEPENDENT channel (direct SQLite read, no fence) ----
            // This is the difference between "the scope remains occupied" and "the scope does not
            // exist". The row is still there, still dead, and still at MaTask == T.
            var dead = await ReadNoteDirectAsync(noteId);
            Assert.True(dead.IsDeleted);
            Assert.Equal(task.MaTask, dead.MaTask);

            // ...and the unfiltered UNIQUE(MaTask) scope is genuinely still occupied by exactly this
            // row: a second note at the same scope is rejected by the database.
            using (var clash = _fx.Fx.NewContext())
            {
                clash.TaskNotes.Add(new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "intruder" });
                await Assert.ThrowsAnyAsync<DbUpdateException>(() => clash.SaveChangesAsync());
            }

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, TombstoneTask(task.MaTask));

            // (1) N is NOT a new cascade lifecycle target.
            Assert.DoesNotContain(impact.Rows,
                r => r.EntityType == SyncEntityTypes.TaskNote && r.EntityId == noteId);
            Assert.DoesNotContain(impact.Lifecycle,
                l => l.EntityType == SyncEntityTypes.TaskNote && l.EntityId == noteId);

            // (2) K(T) is NOT reported as released by this mutation. This is the assertion that an
            //     unfiltered cascade predicate turns RED (MUT-M3-1).
            Assert.DoesNotContain(impact.Scopes,
                s => s.ScopeKey == ScopeKeyOf(task.MaTask) && s.Change == ScopeChange.Released);
            Assert.DoesNotContain(impact.Scopes, s => s.OccupantId == noteId);

            // (3) the task itself IS still tombstoned — the mutation happened; only the dead child was
            //     excluded. Guards against passing because the whole impact set came back empty.
            var taskRow = Assert.Single(impact.Rows,
                r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task.MaTask);
            Assert.Equal(RowEffect.Tombstoned, taskRow.Effect);

            // (4) the record is still SELECTED (not silently dropped), and the applicable dead-parent
            //     branch returns the existing OD-4 result rather than a false scope-release violation.
            using var routed = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(routed, TombstoneTask(task.MaTask));
            var result = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Passed, result.Outcome);
            Assert.Equal("CONS.EmptyScopeParentTombstoned", result.RuleId);

            // (5) and the note is STILL in the database afterwards, untouched by the read-only fence.
            var after = await ReadNoteDirectAsync(noteId);
            Assert.True(after.IsDeleted);
            Assert.Equal(task.MaTask, after.MaTask);
            Assert.Equal(dead.Rev, after.Rev);
        }

        // ==================================================================
        // The two cases differ ONLY in the child's liveness
        // ==================================================================

        /// <summary>
        /// The discriminating pair, asserted side by side: identical seeds, identical request, and the
        /// single bit that differs is <c>N.IsDeleted</c>. Either mutation (unfiltered cascade, or
        /// excluding live children) collapses this difference and turns the test RED.
        /// </summary>
        [Fact]
        public async Task M3_LivenessIsTheOnlyDifference_BetweenReleaseAndNoRelease()
        {
            async Task<bool> ReleasesScopeAsync(bool dead)
            {
                var (task, _, _) = await SeedAsync(noteAlreadyDead: dead);
                using var ctx = _fx.Fx.NewContext();
                var impact = await ImpactResolver.ResolveAsync(ctx, TombstoneTask(task.MaTask));
                return impact.Scopes.Any(s => s.ScopeKey == ScopeKeyOf(task.MaTask) && s.Change == ScopeChange.Released);
            }

            Assert.True(await ReleasesScopeAsync(dead: false), "a LIVE occupant must release its scope");
            Assert.False(await ReleasesScopeAsync(dead: true), "an already-dead occupant must NOT release its scope");
        }
    }
}
