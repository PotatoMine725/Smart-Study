using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-6, DoR §13; owner rulings B-1..B-4) — <see cref="ConflictResolver"/> core
    /// behaviour: context lifetime (B-1), classification order (auto-rejection, replay, drift),
    /// and the positive resolution paths for the three reachable record shapes that do NOT involve
    /// a tombstoned/missing parent (S1-CR, AL-PT, S3). Parent-liveness (B-2), ManualMerge tombstone
    /// rejection (B-3), KeepLocal-on-AL-PT (B-4) and the E-3 identity rule have their own file,
    /// <see cref="ConflictResolverParentAndManualMergeTests"/>.
    /// <para>
    /// Three distinct device ids are used throughout (<see cref="SyncApplyFixture.LocalDevice"/>,
    /// <see cref="SyncApplyFixture.PeerDevice"/>, and a resolver-call-local device below) so that
    /// "kept the candidate's original provenance" and "stamped locally by the resolver" can never
    /// produce identical bytes by coincidence of shared identity (PR-5 §6 lesson).
    /// </para>
    /// </summary>
    public class ConflictResolverTests : IDisposable
    {
        private const string ResolverDevice = "RESOLVER-DEVICE";
        private static readonly DateTime ResolverNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
        private const string ParentDeleterDevice = "DELETER-DEVICE";
        private static readonly DateTime ParentDeletedAt = new(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc);

        private readonly SyncApplyFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private ConflictResolver Resolver() => new(() => _fx.NewContext(ResolverNow, ResolverDevice));

        private static StudyTask CloneTask(StudyTask src, Action<StudyTask>? edit = null)
        {
            var copy = new StudyTask
            {
                MaTask = src.MaTask,
                MaMonHoc = src.MaMonHoc,
                TenTask = src.TenTask,
                HanChot = src.HanChot,
                TrangThai = src.TrangThai,
                LoaiTask = src.LoaiTask,
                DoKho = src.DoKho,
                ThoiGianDaHoc = src.ThoiGianDaHoc,
                NgayHoanThanh = src.NgayHoanThanh,
            };
            edit?.Invoke(copy);
            return copy;
        }

        // ------------------------------------------------------------------ shape construction
        // Every shape is built by driving the REAL apply session (SyncApplyFixture.Session()),
        // exactly as SyncApplyParentHandlingTests / SyncApplyConflictStagingTests already do, so
        // the resolver is tested against records the production staging code actually produces.

        /// <summary>S1-CR: both sides reparent the same StudyTask to different MonHoc targets.</summary>
        private async Task<(Guid ConflictId, Guid TaskId, Guid MonHocAId, Guid MonHocBId, Guid MonHocCId)> StageConcurrentReparentAsync()
        {
            var (hocKy, monHocA, task) = await _fx.SeedTreeAsync();
            var monHocB = new MonHoc("MH B", 2) { MaHocKy = hocKy.MaHocKy };
            var monHocC = new MonHoc("MH C", 4) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(monHocB);
            await _fx.AddLocalAsync(monHocC);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = monHocB.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task, t => t.MaMonHoc = monHocC.MaMonHoc);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            // Filtered by EntityId rather than Assert.Single(...ReadConflictsAsync()): the fixture's
            // connection is shared across every helper call in a test, so a test exercising more than
            // one shape (or the same shape twice, e.g. ZL1's second "Failed" sub-case) has more than
            // one record in the whole table by the time this runs.
            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task.MaTask);
            return (record.ConflictId, task.MaTask, monHocA.MaMonHoc, monHocB.MaMonHoc, monHocC.MaMonHoc);
        }

        /// <summary>AL-PT: a remote pure create under a locally-tombstoned HocKy. No local candidate.</summary>
        private async Task<(Guid ConflictId, Guid NewMonHocId, Guid HocKyId)> StageAbsentLocalStructuralAsync()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            using (var ctx = _fx.NewContext(ParentDeletedAt, ParentDeleterDevice))
            {
                var live = await ctx.HocKys.FirstAsync(h => h.MaHocKy == hocKy.MaHocKy);
                ctx.HocKys.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var newMonHocId = Guid.NewGuid();
            var remote = new MonHoc { MaMonHoc = newMonHocId, MaHocKy = hocKy.MaHocKy, TenMonHoc = "orphan?", SoTinChi = 3 };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.EntityType == SyncEntityTypes.MonHoc && r.EntityId == newMonHocId);
            return (record.ConflictId, newMonHocId, hocKy.MaHocKy);
        }

        /// <summary>S3: Base = null, Local = N1, Remote = N2, same MaTask, different Ids (M5).</summary>
        private async Task<(Guid ConflictId, Guid TaskId, Guid N1Id, Guid N2Id, long N1Rev)> StageNullBaseConstraintAsync(
            bool remoteIsTombstone = false)
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var n1 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "local note" };
            await _fx.AddLocalAsync(n1);

            var n2 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "remote note" };
            SyncApplyFixture.Stamp(n2, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice,
                                   isDeleted: remoteIsTombstone, deletedAtUtc: remoteIsTombstone ? SyncApplyFixture.RemoteLater : null);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(n2));

            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.Kind == ConflictKind.ConstraintConflict && r.ConstraintValue == task.MaTask.ToString("D"));
            return (record.ConflictId, task.MaTask, n1.Id, n2.Id, record.LocalRowRev!.Value);
        }

        private static ResolutionRequest KeepLocal() => new(ResolutionKind.KeepLocal, null, null);
        private static ResolutionRequest KeepRemote() => new(ResolutionKind.KeepRemote, null, null);
        private static ResolutionRequest KeepBase() => new(ResolutionKind.KeepBase, null, null);

        // ================================================================= B-1: context lifetime

        /// <summary>
        /// Z-L1 — each <see cref="ConflictResolver.ResolveAsync"/> call uses exactly one context from
        /// the factory, disposed on every outcome kind (Applied, NoOpReplay, Rejected, Failed), and two
        /// sequential calls receive two distinct context instances.
        /// </summary>
        [Fact]
        public async Task ZL1_EachCall_UsesOneContext_DisposedOnEveryOutcomeKind()
        {
            var (conflictId, taskId, _, monHocBId, _) = await StageConcurrentReparentAsync();

            async Task<(ResolutionOutcome Outcome, FailingSaveDbContext Ctx)> CallAsync(Guid id, ResolutionRequest req, int failOnSave = int.MaxValue)
            {
                FailingSaveDbContext? created = null;
                var resolver = new ConflictResolver(() =>
                {
                    created = _fx.NewFailingContext(failOnSave);
                    return created;
                });
                var outcome = await resolver.ResolveAsync(id, req);
                return (outcome, created!);
            }

            // Applied
            var (applied, ctxApplied) = await CallAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Applied, applied.Kind);
            Assert.True(ctxApplied.WasDisposed);

            // NoOpReplay (same request again)
            var (replay, ctxReplay) = await CallAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.NoOpReplay, replay.Kind);
            Assert.True(ctxReplay.WasDisposed);
            Assert.NotSame(ctxApplied, ctxReplay);

            // Rejected (unknown conflict id)
            var (rejected, ctxRejected) = await CallAsync(Guid.NewGuid(), KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Rejected, rejected.Kind);
            Assert.Equal(ResolutionRejectReason.UnknownConflict, rejected.Reason);
            Assert.True(ctxRejected.WasDisposed);

            // Failed (inject a save failure on a fresh conflict)
            var (conflictId2, _, _, _, _) = await StageConcurrentReparentAsync();
            var (failed, ctxFailed) = await CallAsync(conflictId2, KeepLocal(), failOnSave: 1);
            Assert.Equal(ResolutionOutcomeKind.Failed, failed.Kind);
            Assert.True(ctxFailed.WasDisposed);
        }

        /// <summary>
        /// Z-R3 — a failed operation's context is never reused, and never reaches the retry: the
        /// original request is re-derived from scratch via a fresh <see cref="ConflictResolver"/> call
        /// built from a normal factory, which succeeds and bumps Rev exactly once.
        /// </summary>
        [Fact]
        public async Task ZR3_FailedOperation_LeavesRecordUnresolved_RetryWithFreshContextApplies()
        {
            var (conflictId, taskId, monHocAId, monHocBId, _) = await StageConcurrentReparentAsync();
            var before = await _fx.ReadTaskAsync(taskId);

            var failingResolver = new ConflictResolver(() => _fx.NewFailingContext(1));
            var failedOutcome = await failingResolver.ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Failed, failedOutcome.Kind);

            var afterFailure = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(before!.Rev, afterFailure!.Rev);                          // nothing committed
            Assert.Equal(monHocAId, afterFailure.MaMonHoc);                        // still held at Base

            var recordAfterFailure = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Unresolved, recordAfterFailure.Status);

            var retryOutcome = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Applied, retryOutcome.Kind);

            var afterRetry = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(monHocBId, afterRetry!.MaMonHoc);
            Assert.Equal(before.Rev + 1, afterRetry.Rev);                          // exactly one bump, not two
        }

        // ================================================================= atomicity (D8-E, I-4)

        /// <summary>
        /// Z-T1 — if the single save fails, NEITHER the domain row nor the ConflictRecord changed:
        /// the write and the resolution status share one save inside one transaction.
        /// </summary>
        [Fact]
        public async Task ZT1_FailedSave_LeavesBothTheDomainRowAndTheRecordUnchanged()
        {
            var (conflictId, taskId, monHocAId, _, _) = await StageConcurrentReparentAsync();
            var recordBefore = Assert.Single(await _fx.ReadConflictsAsync());
            var rowBefore = await _fx.ReadTaskAsync(taskId);

            var resolver = new ConflictResolver(() => _fx.NewFailingContext(1));
            var outcome = await resolver.ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Failed, outcome.Kind);

            var rowAfter = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(rowBefore!.Rev, rowAfter!.Rev);
            Assert.Equal(monHocAId, rowAfter.MaMonHoc);

            var recordAfter = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(recordBefore.Status, recordAfter.Status);
            Assert.Null(recordAfter.ResolutionKind);
            Assert.Null(recordAfter.ResolvedAtUtc);
        }

        // ================================================================= auto-resolution (D9-T5)

        /// <summary>
        /// Z-N1 — an AutoLww record can never be re-resolved, even by a request equal to the stored
        /// AutoLww outcome; and a request naming an auto kind directly is rejected as InvalidRequest
        /// before the record is even consulted for its Kind.
        /// </summary>
        [Fact]
        public async Task ZN1_AutoResolvedRecord_IsNeverResolvable()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.TenTask = "local name";
                await ctx.SaveChangesAsync();
            }
            var remote = CloneTask(task, t => t.TenTask = "remote name");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ResolutionKind.AutoLww, record.ResolutionKind);

            var outcome = await Resolver().ResolveAsync(conflictId: record.ConflictId, request: KeepRemote());
            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.NotResolvable, outcome.Reason);

            var stillAuto = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ResolutionKind.AutoLww, stillAuto.ResolutionKind);   // untouched
        }

        [Fact]
        public async Task ZN1_RequestNamingAnAutoKind_IsRejectedAsInvalidRequest()
        {
            var (conflictId, _, _, _, _) = await StageConcurrentReparentAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, new ResolutionRequest(ResolutionKind.AutoLww, null, null));

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.InvalidRequest, outcome.Reason);
        }

        [Fact]
        public async Task ZN2_UnknownConflictId_IsRejected()
        {
            var outcome = await Resolver().ResolveAsync(Guid.NewGuid(), KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.UnknownConflict, outcome.Reason);
        }

        // ================================================================= replay (D7-C/D7-D)

        /// <summary>
        /// Z-R1 — replaying the identical request after it was Applied is a semantic no-op: the row,
        /// its Rev and the record's ResolvedAtUtc are all unchanged. Also holds after a LATER, unrelated
        /// local edit to the now-resolved row (E-1: the stored fingerprint is the pre-stamp candidate,
        /// not the live row, so it never drifts out from under a legitimate replay).
        /// </summary>
        [Fact]
        public async Task ZR1_IdenticalReplay_IsNoOp_EvenAfterALaterLocalEdit()
        {
            var (conflictId, taskId, _, monHocBId, _) = await StageConcurrentReparentAsync();

            var applied = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Applied, applied.Kind);
            var afterFirst = await _fx.ReadTaskAsync(taskId);
            var recordAfterFirst = Assert.Single(await _fx.ReadConflictsAsync());

            // An unrelated later local edit to the resolved row.
            using (var ctx = _fx.NewContext(new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == taskId);
                live.DoKho = 9;
                await ctx.SaveChangesAsync();
            }

            var replay = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.NoOpReplay, replay.Kind);

            var recordAfterReplay = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(recordAfterFirst.ResolvedAtUtc, recordAfterReplay.ResolvedAtUtc);
            Assert.Equal(recordAfterFirst.ResultSnapshotJson, recordAfterReplay.ResultSnapshotJson);

            var afterReplay = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(9, afterReplay!.DoKho);            // the unrelated edit, not touched by the replay
            Assert.Equal(monHocBId, afterReplay.MaMonHoc);
        }

        [Fact]
        public async Task ZR2_DifferentReplayRequest_IsRejectedAsMismatched_AndWritesNothing()
        {
            var (conflictId, taskId, _, monHocBId, _) = await StageConcurrentReparentAsync();

            await Resolver().ResolveAsync(conflictId, KeepLocal());
            var afterFirst = await _fx.ReadTaskAsync(taskId);

            var mismatched = await Resolver().ResolveAsync(conflictId, KeepRemote());
            Assert.Equal(ResolutionOutcomeKind.Rejected, mismatched.Kind);
            Assert.Equal(ResolutionRejectReason.MismatchedReplay, mismatched.Reason);

            var afterMismatch = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(afterFirst!.Rev, afterMismatch!.Rev);
            Assert.Equal(monHocBId, afterMismatch.MaMonHoc);
        }

        // ================================================================= drift (D8-H)

        /// <summary>
        /// Z-D1 — an ordinary local edit to the row held at Base drifts the scope, and EVERY resolution
        /// kind (including KeepBase) is rejected while the drift stands; nothing is written.
        /// </summary>
        [Theory]
        [InlineData(ResolutionKind.KeepLocal)]
        [InlineData(ResolutionKind.KeepRemote)]
        [InlineData(ResolutionKind.KeepBase)]
        public async Task ZD1_LiveStateDrift_RejectsEveryResolutionKind(ResolutionKind kind)
        {
            var (conflictId, taskId, monHocAId, _, _) = await StageConcurrentReparentAsync();

            using (var ctx = _fx.NewContext(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == taskId);
                live.DoKho = 9;
                await ctx.SaveChangesAsync();
            }
            var beforeAttempt = await _fx.ReadTaskAsync(taskId);

            var outcome = await Resolver().ResolveAsync(conflictId, new ResolutionRequest(kind, null, null));

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.LiveStateDrift, outcome.Reason);

            var after = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(beforeAttempt!.Rev, after!.Rev);
            Assert.Equal(9, after.DoKho);
            Assert.Equal(monHocAId, after.MaMonHoc);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Unresolved, record.Status);
        }

        /// <summary>Z-D2 — drift on an empty-Base scope: a note created for the task drifts S3's scope.</summary>
        [Fact]
        public async Task ZD2_DriftOnAnEmptyBaseScope_RejectsResolution()
        {
            var (conflictId, taskId, _, _, _) = await StageNullBaseConstraintAsync();

            // Something else fills the (currently empty) scope while the conflict is Unresolved would
            // be rejected by the scope-lock at the APPLY layer, so drift the scope by resurrecting a
            // note through the ordinary local write path instead (a different mechanism, same effect:
            // the scope is no longer empty the way it was at staging time).
            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                ctx.TaskNotes.Add(new TaskNote { Id = Guid.NewGuid(), MaTask = taskId, Content = "drifted" });
                await ctx.SaveChangesAsync();
            }

            var outcome = await Resolver().ResolveAsync(conflictId, KeepBase());
            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.LiveStateDrift, outcome.Reason);
        }

        // ================================================================= positive resolutions

        /// <summary>Z-P1 — S1-CR KeepLocal/KeepRemote: accepted, correct fields, local stamp, Rev+1.</summary>
        [Theory]
        [InlineData(ResolutionKind.KeepLocal)]
        [InlineData(ResolutionKind.KeepRemote)]
        public async Task ZP1_ConcurrentReparent_KeepLocalOrKeepRemote_IsApplied_WithLocalStamp(ResolutionKind kind)
        {
            var (conflictId, taskId, _, monHocBId, monHocCId) = await StageConcurrentReparentAsync();
            var before = await _fx.ReadTaskAsync(taskId);

            var outcome = await Resolver().ResolveAsync(conflictId, new ResolutionRequest(kind, null, null));
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);

            var after = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(kind == ResolutionKind.KeepLocal ? monHocBId : monHocCId, after!.MaMonHoc);
            Assert.Equal(before!.Rev + 1, after.Rev);
            Assert.Equal(ResolverDevice, after.ModifiedByDeviceId);              // ordinary local stamp (A2-c)
            Assert.Equal(ResolverNow, DateTime.SpecifyKind(after.ModifiedAtUtc, DateTimeKind.Utc));

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Resolved, record.Status);
            Assert.Equal(kind, record.ResolutionKind);
            Assert.Equal(taskId, record.ResultEntityId);
            Assert.NotNull(record.ResultSnapshotJson);
            Assert.Equal(ResolverDevice, record.ResolvedByDeviceId);
            Assert.Equal(ResolverNow, DateTime.SpecifyKind(record.ResolvedAtUtc!.Value, DateTimeKind.Utc));

            // I-13: PR-6 never advances the sync baseline.
            var baseline = await _fx.ReadBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.StudyTask, taskId);
            Assert.NotNull(baseline);
            Assert.NotEqual(after.Rev, baseline!.Rev);
        }

        /// <summary>Z-P2 — S1-CR KeepBase: accepted, NO domain write, Rev unchanged, record Resolved.</summary>
        [Fact]
        public async Task ZP2_ConcurrentReparent_KeepBase_WritesNothing()
        {
            var (conflictId, taskId, monHocAId, _, _) = await StageConcurrentReparentAsync();
            var before = await _fx.ReadTaskAsync(taskId);

            var outcome = await Resolver().ResolveAsync(conflictId, KeepBase());
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);

            var after = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(before!.Rev, after!.Rev);                    // untouched
            Assert.Equal(before.ModifiedAtUtc, after.ModifiedAtUtc);
            Assert.Equal(monHocAId, after.MaMonHoc);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Resolved, record.Status);
            Assert.Equal(ResolutionKind.KeepBase, record.ResolutionKind);
        }

        /// <summary>
        /// Z-P3 — S3 KeepLocal: N1 is re-inserted with Rev seeded from the record's LocalRowRev (E-2),
        /// so a third peer already holding a baseline at that Rev still sees it as a genuine change.
        /// </summary>
        [Fact]
        public async Task ZP3_S3_KeepLocal_ReInsertsN1_WithRevSeededFromLocalRowRev()
        {
            var (conflictId, taskId, n1Id, _, n1Rev) = await StageNullBaseConstraintAsync();

            // A third peer already holds a baseline for N1 at its pre-withdrawal Rev.
            await _fx.SetRawBaselineAsync(SyncApplyFixture.OtherPeerDevice, SyncEntityTypes.TaskNote, n1Id,
                                          n1Rev, null);

            var outcome = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);

            var restored = await _fx.ReadNoteAsync(n1Id);
            Assert.NotNull(restored);
            Assert.Equal("local note", restored!.Content);
            Assert.Equal(n1Rev + 1, restored.Rev);                    // seeded, not reset to 1
            Assert.Equal(ResolverDevice, restored.ModifiedByDeviceId);

            var otherPeerBaseline = await _fx.ReadBaselineAsync(SyncApplyFixture.OtherPeerDevice, SyncEntityTypes.TaskNote, n1Id);
            Assert.True(restored.Rev > otherPeerBaseline!.Rev);        // still due for re-emission to that peer
        }

        /// <summary>Z-P4 — S3 KeepRemote: N2 inserted, exactly one row in scope; also for a tombstoned N2.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ZP4_S3_KeepRemote_InsertsN2_ExactlyOneRowInScope(bool remoteIsTombstone)
        {
            var (conflictId, taskId, n1Id, n2Id, _) = await StageNullBaseConstraintAsync(remoteIsTombstone);

            var outcome = await Resolver().ResolveAsync(conflictId, KeepRemote());
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);

            var n2 = await _fx.ReadNoteAsync(n2Id);
            Assert.NotNull(n2);
            Assert.Equal(1, n2!.Rev);
            Assert.Equal(remoteIsTombstone, n2.IsDeleted);
            Assert.Null(await _fx.ReadNoteAsync(n1Id));
            Assert.Equal(1, await _fx.CountNotesInScopeAsync(taskId));
        }

        /// <summary>
        /// Z-P5 — KeepBase(null) frees an empty scope with no write, null result columns, and the scope
        /// becomes available for a fresh conflict immediately (the partial unique index only guards
        /// Unresolved rows).
        /// </summary>
        [Fact]
        public async Task ZP5_KeepBaseNull_OnS3_WritesNothing_AndUnlocksTheScope()
        {
            var (conflictId, taskId, n1Id, n2Id, _) = await StageNullBaseConstraintAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, KeepBase());
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);

            Assert.Equal(0, await _fx.CountNotesInScopeAsync(taskId));

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Resolved, record.Status);
            Assert.Equal(ResolutionKind.KeepBase, record.ResolutionKind);
            Assert.Null(record.ResultEntityId);
            Assert.Null(record.ResultSnapshotJson);
            Assert.Null(record.ResultFingerprint);

            // The scope is free again: a later, unrelated note can be created without a scope-lock.
            using var ctx = _fx.NewContext();
            ctx.TaskNotes.Add(new TaskNote { Id = Guid.NewGuid(), MaTask = taskId, Content = "after resolve" });
            await ctx.SaveChangesAsync();
        }

        /// <summary>Z-P5 (AL-PT twin) — KeepBase(null) on AL-PT, where Base was already null.</summary>
        [Fact]
        public async Task ZP5_KeepBaseNull_OnAlPt_IsApplied_WithNoWrite()
        {
            var (conflictId, newMonHocId, _) = await StageAbsentLocalStructuralAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, KeepBase());
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);
            Assert.Null(await _fx.ReadMonHocAsync(newMonHocId));

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Resolved, record.Status);
            Assert.Null(record.ResultEntityId);
            Assert.Null(record.ResultSnapshotJson);
        }

        // ================================================================= evidence immutability

        /// <summary>Z-E1 — all evidence columns are byte-identical before and after resolution.</summary>
        [Fact]
        public async Task ZE1_EvidenceColumns_AreByteIdentical_AfterResolution()
        {
            var (conflictId, _, _, _, _) = await StageConcurrentReparentAsync();
            var before = Assert.Single(await _fx.ReadConflictsAsync());

            await Resolver().ResolveAsync(conflictId, KeepLocal());

            var after = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(before.ConflictKey, after.ConflictKey);
            Assert.Equal(before.ScopeKey, after.ScopeKey);
            Assert.Equal(before.BaseSnapshotJson, after.BaseSnapshotJson);
            Assert.Equal(before.BaseFingerprint, after.BaseFingerprint);
            Assert.Equal(before.LocalSnapshotJson, after.LocalSnapshotJson);
            Assert.Equal(before.LocalFingerprint, after.LocalFingerprint);
            Assert.Equal(before.LocalRowRev, after.LocalRowRev);
            Assert.Equal(before.LocalWithdrawal, after.LocalWithdrawal);
            Assert.Equal(before.RemoteSnapshotJson, after.RemoteSnapshotJson);
            Assert.Equal(before.RemoteFingerprint, after.RemoteFingerprint);
            Assert.Equal(before.CreatedAtUtc, after.CreatedAtUtc);
            Assert.Equal(before.CreatedByDeviceId, after.CreatedByDeviceId);
        }

        // ================================================================= R1 / no MarkSyncApplied

        /// <summary>
        /// Z-M1 — the resolver never calls <see cref="AppDbContext.MarkSyncApplied"/> (A2-c): a source
        /// scan of the one file, matched on the call shape rather than any delete-fence literal so this
        /// test cannot be mistaken for a second audit fence.
        /// </summary>
        [Fact]
        public void ZM1_ConflictResolverSource_NeverCallsMarkSyncApplied()
        {
            var text = File.ReadAllText(FindProductionFile("ConflictResolver.cs"));
            var callShape = new Regex(@"\.MarkSyncApplied\s*\(", RegexOptions.Compiled);
            Assert.DoesNotMatch(callShape, text);
        }

        private static string FindProductionFile(string fileName)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SmartStudyPlanner.slnx")))
                dir = dir.Parent;
            Assert.NotNull(dir);

            var hit = Directory.EnumerateFiles(Path.Combine(dir!.FullName, "SmartStudyPlanner"), fileName, SearchOption.AllDirectories).FirstOrDefault();
            Assert.NotNull(hit);
            return hit!;
        }
    }
}
