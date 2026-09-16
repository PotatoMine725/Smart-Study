using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 — <b>P0-a measurement</b> (plan §17 row P0-a, §7.4 line "Slice 0 measurement P0-a
    /// decides whether re-stamped already-dead rows are excluded from the oracle (and reported as D-2)
    /// or modelled"). Characterization only: these tests pin the OBSERVED behaviour of the two
    /// independent cascade implementations, they do not assert a desired semantic.
    /// <para>
    /// The question under measurement (review finding M-3/A-1): when a <c>StudyTask</c> is tombstoned,
    /// is an <b>already-tombstoned</b> <c>TaskNote</c> occupying that task's <c>MaTask</c> constraint
    /// scope included in the actual cascade? The answer decides whether
    /// <see cref="SmartStudyPlanner.Sync.Fence.ImpactResolver"/>'s cascade predicate is live-only or
    /// unfiltered. Plan §7.3 requires "the same predicate the executing path uses" — which is only
    /// well-defined if the executing paths agree.
    /// </para>
    /// <para>
    /// Note that the <c>UNIQUE(MaTask)</c> index on TaskNote is unfiltered
    /// (<c>AppDbContext.cs</c> <c>b.HasIndex(n =&gt; n.MaTask).IsUnique()</c>), so a tombstoned note
    /// still occupies its constraint scope (D9-T1). A cascade that reaches it therefore touches a
    /// row inside a protected <see cref="SmartStudyPlanner.Sync.Fence.ConflictShape.ConstraintOccupancy"/>
    /// scope; a cascade that skips it does not.
    /// </para>
    /// </summary>
    public class CascadePredicateProbeTests : IDisposable
    {
        private readonly SyncApplyFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private sealed record NoteState(bool IsDeleted, long Rev, DateTime ModifiedAtUtc, DateTime? DeletedAtUtc);

        private async Task<NoteState> ReadNoteStateAsync(Guid noteId)
        {
            using var ctx = _fx.NewContext();
            var n = await ctx.TaskNotes.AsNoTracking().FirstAsync(x => x.Id == noteId);
            return new NoteState(n.IsDeleted, n.Rev, n.ModifiedAtUtc, n.DeletedAtUtc);
        }

        /// <summary>Seeds a live tree plus a TaskNote in T's scope, then tombstones ONLY the note.</summary>
        private async Task<(StudyTask Task, Guid NoteId, NoteState AfterFirstTombstone)> SeedTaskWithTombstonedNoteAsync()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "dead occupant" };
            await _fx.AddLocalAsync(note);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE"))
            {
                var live = await ctx.TaskNotes.FirstAsync(n => n.Id == note.Id);
                ctx.TaskNotes.Remove(live); // SyncStamper converts Remove -> soft tombstone
                await ctx.SaveChangesAsync();
            }

            var state = await ReadNoteStateAsync(note.Id);
            Assert.True(state.IsDeleted); // precondition: the note is dead but still occupies UNIQUE(MaTask)
            return (task, note.Id, state);
        }

        // ------------------------------------------------------------------ Leg 1: LOCAL path

        /// <summary>
        /// P0-a leg 1 — the LOCAL cascade (<c>TaskCascadeHelper.RemoveChildrenAsync</c>, reached from
        /// <c>SqliteStudyTaskRepository.DeleteAsync</c> and <c>SqliteHocKyRepository.LuuHocKyAsync</c>
        /// delete-by-absence). Its query has no <c>IsDeleted</c> filter and <c>AppDbContext</c> declares
        /// no global query filter, so it is expected to re-reach the already-dead note.
        /// </summary>
        [Fact]
        public async Task P0a_Leg1_LocalTaskCascade_OverAlreadyTombstonedNote_ObservedBehaviour()
        {
            var (task, noteId, before) = await SeedTaskWithTombstonedNoteAsync();

            using (var db = _fx.NewContext(SyncApplyFixture.LocalNow.AddHours(1), "LATER-DEVICE"))
            {
                await TaskCascadeHelper.RemoveChildrenAsync(db, task.MaTask);
                var live = await db.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                db.StudyTasks.Remove(live);
                await db.SaveChangesAsync();
            }

            var after = await ReadNoteStateAsync(noteId);

            // OBSERVATION (not a desired semantic): the local cascade re-stamps the already-dead note.
            Assert.True(after.IsDeleted);
            Assert.True(after.Rev > before.Rev,
                $"local cascade re-stamped the dead note: Rev {before.Rev} -> {after.Rev}");
            Assert.NotEqual(before.ModifiedAtUtc, after.ModifiedAtUtc);
        }

        // ------------------------------------------------------------------ Leg 2: SYNC path

        /// <summary>
        /// P0-a leg 2 — the SYNC cascade (<c>SyncApplySession.CascadeTombstoneAsync</c>), whose child
        /// queries all carry <c>&amp;&amp; !IsDeleted</c>. Expected to leave the already-dead note untouched.
        /// </summary>
        [Fact]
        public async Task P0a_Leg2_SyncApplyTaskCascade_OverAlreadyTombstonedNote_ObservedBehaviour()
        {
            var (task, noteId, before) = await SeedTaskWithTombstonedNoteAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = new StudyTask
            {
                MaTask = task.MaTask,
                MaMonHoc = task.MaMonHoc,
                TenTask = task.TenTask,
                HanChot = task.HanChot,
                TrangThai = task.TrangThai,
                LoaiTask = task.LoaiTask,
                DoKho = task.DoKho,
                ThoiGianDaHoc = task.ThoiGianDaHoc,
                NgayHoanThanh = task.NgayHoanThanh,
            };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice, isDeleted: true);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var taskAfter = await _fx.ReadTaskAsync(task.MaTask);
            Assert.True(taskAfter!.IsDeleted); // precondition: the tombstone really landed (live -> dead)

            var after = await ReadNoteStateAsync(noteId);

            // OBSERVATION: the sync cascade's live-only predicate skips the already-dead note entirely.
            Assert.Equal(before.Rev, after.Rev);
            Assert.Equal(before.ModifiedAtUtc, after.ModifiedAtUtc);
            Assert.Equal(before.DeletedAtUtc, after.DeletedAtUtc);
        }

        // ------------------------------------------------------------------ Control: LIVE note

        /// <summary>
        /// Control leg — proves the probe's observation channel can go the other way: a LIVE note in the
        /// same scope is re-stamped by BOTH paths. Without this, "sync left the row alone" would be
        /// indistinguishable from "the cascade never ran at all".
        /// </summary>
        [Fact]
        public async Task P0a_Control_SyncApplyTaskCascade_OverLiveNote_TombstonesIt()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "live occupant" };
            await _fx.AddLocalAsync(note);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var before = await ReadNoteStateAsync(note.Id);
            Assert.False(before.IsDeleted);

            var remote = new StudyTask
            {
                MaTask = task.MaTask,
                MaMonHoc = task.MaMonHoc,
                TenTask = task.TenTask,
                HanChot = task.HanChot,
                TrangThai = task.TrangThai,
                LoaiTask = task.LoaiTask,
                DoKho = task.DoKho,
                ThoiGianDaHoc = task.ThoiGianDaHoc,
                NgayHoanThanh = task.NgayHoanThanh,
            };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice, isDeleted: true);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var after = await ReadNoteStateAsync(note.Id);
            Assert.True(after.IsDeleted, "the sync cascade must reach a LIVE note in scope");
            Assert.True(after.Rev > before.Rev);
        }

        // ------------------------------------------------------------------ what the choice costs

        /// <summary>
        /// P0-a consequence, MEASURED at router level — this is the outcome the owner's M-3/A-1 ruling
        /// actually decides, so it is observed here rather than reasoned about.
        /// <para>
        /// Two S2 ConstraintOccupancy records at <c>K(T)</c>, identical except that one scope's Base
        /// occupant is LIVE and the other's is ALREADY TOMBSTONED. Both notes occupy their unfiltered
        /// <c>UNIQUE(MaTask)</c> scope (D9-T1). Tombstoning the owning task gives:
        /// </para>
        /// <list type="bullet">
        /// <item>live occupant   =&gt; <c>Released(K)</c> in the impact =&gt; <c>Blocked CONS.ScopeReleased</c>;</item>
        /// <item>dead occupant   =&gt; nothing in the impact =&gt; the OD-4 branch,
        ///       <c>Passed CONS.EmptyScopeParentTombstoned</c>.</item>
        /// </list>
        /// <para>
        /// The second row is what would flip to <c>Blocked</c> under an unfiltered cascade predicate.
        /// This test pins CURRENT behaviour; it does not assert that either answer is correct.
        /// </para>
        /// </summary>
        [Fact]
        public async Task P0a_Consequence_S2WithADeadOccupant_CurrentlyPasses_WhereALiveOccupantBlocks()
        {
            async Task<PolicyResult> EvaluateAsync(bool occupantAlreadyDead)
            {
                var (_, _, task) = await _fx.SeedTreeAsync();
                var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "occupant" };
                await _fx.AddLocalAsync(note);

                if (occupantAlreadyDead)
                {
                    using var kill = _fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE");
                    var live = await kill.TaskNotes.FirstAsync(n => n.Id == note.Id);
                    kill.TaskNotes.Remove(live);
                    await kill.SaveChangesAsync();
                }

                var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", task.MaTask.ToString("D"));
                var row = new SyncConflictRecordRow
                {
                    ConflictId = Guid.NewGuid(),
                    ConflictKey = "p0a-" + Guid.NewGuid().ToString("N"),
                    ScopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null, scope),
                    Kind = ConflictKind.ConstraintConflict,
                    EntityType = SyncEntityTypes.TaskNote,
                    ConstraintKey = "MaTask",
                    ConstraintValue = task.MaTask.ToString("D"),
                    PeerDeviceId = SyncApplyFixture.PeerDevice,
                    BaseEntityId = note.Id,
                    BaseSnapshotJson = "{}",
                    BaseFingerprint = "p0a-base-fp",
                    LocalEntityId = note.Id,
                    LocalSnapshotJson = "{}",
                    LocalFingerprint = "p0a-base-fp",
                    LocalRowRev = 1,
                    RemoteEntityId = Guid.NewGuid(),
                    RemoteSnapshotJson = "{}",
                    RemoteFingerprint = "p0a-remote-fp",
                    Status = ConflictRecordStatus.Unresolved,
                    CreatedAtUtc = SyncApplyFixture.LocalNow,
                    CreatedByDeviceId = SyncApplyFixture.LocalDevice,
                };

                using (var seed = _fx.NewContext())
                {
                    seed.SyncConflictRecords.Add(row);
                    await seed.SaveChangesAsync();
                }

                using var ctx = _fx.NewContext();
                var decision = await FenceRouter.EvaluateAsync(ctx, new MutationRequest(
                    MutationOrigin.LocalApplication,
                    new[]
                    {
                        new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.StudyTask, task.MaTask,
                            Array.Empty<RelationChange>(), Array.Empty<string>()),
                    }));

                return Assert.Single(decision.Results, r => r.ConflictId == row.ConflictId);
            }

            var liveOccupant = await EvaluateAsync(occupantAlreadyDead: false);
            Assert.Equal(FenceOutcome.Blocked, liveOccupant.Outcome);
            Assert.Equal("CONS.ScopeReleased", liveOccupant.RuleId);

            var deadOccupant = await EvaluateAsync(occupantAlreadyDead: true);
            Assert.Equal(FenceOutcome.Passed, deadOccupant.Outcome);
            Assert.Equal("CONS.EmptyScopeParentTombstoned", deadOccupant.RuleId);
        }
    }
}
