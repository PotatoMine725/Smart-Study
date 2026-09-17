using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — apply-session integration tests A–F, I, V, W from the PR-5 brief.
    /// Real SQLite throughout: the behaviour under test is persistence behaviour.
    /// </summary>
    public class SyncApplySessionTests : IDisposable
    {
        private readonly SyncApplyFixture _fx = new();

        public void Dispose() => _fx.Dispose();

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

        // ------------------------------------------------------------------ A. ordinary merged update

        /// <summary>
        /// A — Base == Local, remote changed one field (D9 §12.1 case 3). The remote value and the
        /// remote PROVENANCE both land, and the local Rev advances exactly once. The provenance half is
        /// what distinguishes a real apply seam from an ordinary save: with local stamping the row would
        /// carry LOCAL-DEVICE and the session's clock instead.
        /// </summary>
        [Fact]
        public async Task A_OrdinaryMergedUpdate_KeepsRemoteProvenance_AndBumpsRevOnce()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "Task (remote)");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            Assert.Equal(SyncApplyOutcome.Applied, Assert.Single(report.Results).Outcome);

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.NotNull(row);
            Assert.Equal("Task (remote)", row!.TenTask);
            Assert.Equal(SyncApplyFixture.RemoteLater, DateTime.SpecifyKind(row.ModifiedAtUtc, DateTimeKind.Utc));
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);
            Assert.Equal(2, row.Rev);           // 1 -> 2, exactly once
            Assert.False(row.IsDeleted);
        }

        /// <summary>
        /// A (per-field independence) — Local changed one field, Remote changed a different one. Both
        /// survive; nothing is decided at row level (D9 §13).
        /// </summary>
        [Fact]
        public async Task A_DifferentFieldsOnBothSides_MergeIndependently()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            // Local edit after the baseline: DoKho.
            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.DoKho = 5;
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task, t => t.TenTask = "renamed remotely");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(5, row!.DoKho);                      // local-only change kept
            Assert.Equal("renamed remotely", row.TenTask);     // remote-only change applied
        }

        // ------------------------------------------------------------------ B. new synced entity

        /// <summary>B — a row this device has never seen is created, with remote provenance and Rev 1.</summary>
        [Fact]
        public async Task B_NewRemoteEntity_IsCreatedWithRemoteProvenance()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            var remote = new StudyLog
            {
                Id = Guid.NewGuid(),
                MaTask = task.MaTask,
                NgayHoc = new DateTime(2026, 5, 20),
                SoPhutHoc = 45,
                SoPhutDuKien = 60,
                DaHoanThanh = true,
                GhiChu = "từ thiết bị khác",
                CreatedAtUtc = SyncApplyFixture.RemoteEarlier,
                DeviceId = SyncApplyFixture.PeerDevice,
            };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            Assert.Equal(SyncApplyOutcome.Applied, Assert.Single(report.Results).Outcome);

            var row = await _fx.ReadLogAsync(remote.Id);
            Assert.NotNull(row);
            Assert.Equal(45, row!.SoPhutHoc);
            Assert.Equal("từ thiết bị khác", row.GhiChu);
            Assert.Equal(1, row.Rev);
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);

            // D9-T3 creation provenance is copied, not re-minted locally.
            Assert.Equal(SyncApplyFixture.RemoteEarlier, DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));
            Assert.Equal(SyncApplyFixture.PeerDevice, row.DeviceId);
        }

        // ------------------------------------------------------------------ C/D. remote tombstone

        /// <summary>
        /// C/D — a winning remote tombstone is applied as an ordinary tracked UPDATE (IsDeleted = true),
        /// never <c>Remove()</c>, and carries the winning tombstone's own timestamps and device. The row
        /// must still be physically present afterwards: Epic-1's no-hard-delete invariant.
        /// </summary>
        [Fact]
        public async Task CD_RemoteTombstone_SetsIsDeletedWithWinningProvenance_AndKeepsTheRow()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var deletedAt = new DateTime(2026, 6, 2, 3, 4, 5, DateTimeKind.Utc);
            var remote = CloneTask(task);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice,
                                   isDeleted: true, deletedAtUtc: deletedAt);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.NotNull(row);                                 // physically present: tombstone, not DELETE
            Assert.True(row!.IsDeleted);
            Assert.Equal(deletedAt, DateTime.SpecifyKind(row.DeletedAtUtc!.Value, DateTimeKind.Utc));
            Assert.Equal(SyncApplyFixture.RemoteLater, DateTime.SpecifyKind(row.ModifiedAtUtc, DateTimeKind.Utc));
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);
            Assert.Equal(2, row.Rev);
        }

        /// <summary>
        /// D — tombstone-wins beats a LATER local edit (DoR §7.3): the timestamp does not rescue the
        /// edit, and the resulting row keeps the tombstone's provenance, not the newer local one.
        /// </summary>
        [Fact]
        public async Task D_TombstoneWins_OverALaterLocalEdit()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var localEditAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);   // AFTER the remote delete
            using (var ctx = _fx.NewContext(localEditAt))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.TenTask = "edited locally, later";
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice, isDeleted: true);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.True(row!.IsDeleted);
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);
            Assert.Equal(SyncApplyFixture.RemoteLater, DateTime.SpecifyKind(row.ModifiedAtUtc, DateTimeKind.Utc));
        }

        // ------------------------------------------------------------------ E. tracked-instance guarantee

        /// <summary>
        /// E (R1) — the session marks the instance it WRITES, not the <c>AsNoTracking</c> instance it
        /// merged from. Marking the latter is a silent no-op: <c>MarkSyncApplied</c> is keyed by
        /// reference identity and the stamper only ever walks the ChangeTracker, so the apply would
        /// report success having written nothing, or (if the tracked instance were still saved) would
        /// stamp it with LOCAL provenance. Both failure shapes are excluded here: the content changed,
        /// the provenance is the remote's, and Rev advanced exactly one step.
        /// </summary>
        [Fact]
        public async Task E_MarkSyncApplied_TargetsTheTrackedInstanceThatIsSaved()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.ThoiGianDaHoc = 120);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(120, row!.ThoiGianDaHoc);                                    // the write happened
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);          // ...through the marked path
            Assert.NotEqual(SyncApplyFixture.LocalDevice, row.ModifiedByDeviceId);
            Assert.Equal(2, row.Rev);                                                   // ...exactly once
        }

        // ------------------------------------------------------------------ F. no global suppression

        /// <summary>
        /// F — the apply seam stays per-entry: after the session preserved remote provenance on a row,
        /// an ordinary local edit to that same row is still stamped with local provenance. A batch-wide
        /// or context-wide "sync mode" would leak into unrelated local writes, which is exactly what
        /// D9 §20 forbids.
        /// </summary>
        [Fact]
        public async Task F_OrdinaryLocalWrite_IsStillStampedLocally_AfterAnApply()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "remote name");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var afterApply = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(SyncApplyFixture.PeerDevice, afterApply!.ModifiedByDeviceId);

            var localEditAt = new DateTime(2026, 9, 12, 7, 0, 0, DateTimeKind.Utc);
            using (var ctx = _fx.NewContext(localEditAt, "ANOTHER-LOCAL-ID"))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.DoKho = 4;
                await ctx.SaveChangesAsync();
            }

            var afterLocal = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal("ANOTHER-LOCAL-ID", afterLocal!.ModifiedByDeviceId);
            Assert.Equal(localEditAt, DateTime.SpecifyKind(afterLocal.ModifiedAtUtc, DateTimeKind.Utc));
            Assert.Equal(3, afterLocal.Rev);
        }

        // ------------------------------------------------------------------ I. baseline POST-apply Rev

        /// <summary>
        /// I — the baseline records the POST-apply Rev and the POST-apply canonical content. Storing the
        /// pre-apply Rev would leave <c>row.Rev &gt; baseline.Rev</c>, which is exactly
        /// <c>SyncChangeEnumerator</c>'s predicate, so the row would be re-emitted to this peer forever.
        /// </summary>
        [Fact]
        public async Task I_Baseline_RecordsPostApplyRevAndContent()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "post-apply");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var row = await _fx.ReadTaskAsync(task.MaTask);
            var baseline = await _fx.ReadBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.StudyTask, task.MaTask);

            Assert.NotNull(baseline);
            Assert.Equal(row!.Rev, baseline!.Rev);
            Assert.Equal(2, baseline.Rev);
            Assert.Equal(CanonicalJson.Write(IncomingChanges.Of(row).Snapshot), baseline.SnapshotJson);
        }

        /// <summary>
        /// §11.2 no-op row — replaying a change already applied writes no domain row, leaves Rev alone,
        /// and advances the baseline only if it was missing or behind.
        /// </summary>
        [Fact]
        public async Task NoOpMerge_WritesNoDomainRow_AndLeavesRevAlone()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "once");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var afterFirst = await _fx.ReadTaskAsync(task.MaTask);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            Assert.Equal(SyncApplyOutcome.NoOp, Assert.Single(report.Results).Outcome);
            var afterSecond = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(afterFirst!.Rev, afterSecond!.Rev);
            Assert.Equal(afterFirst.ModifiedAtUtc, afterSecond.ModifiedAtUtc);
            Assert.Equal(afterFirst.ModifiedByDeviceId, afterSecond.ModifiedByDeviceId);
        }

        // ------------------------------------------------------------------ V. no re-emission

        /// <summary>
        /// V — after a successful apply the row is NOT enumerated back to the peer that sent it. This is
        /// the §11.2 "no re-emission proof" measured end-to-end through T2.2's real predicate rather
        /// than asserted about the baseline column alone.
        /// </summary>
        [Fact]
        public async Task V_AfterApply_RowIsNotReEmittedToTheSamePeer()
        {
            var (hocKy, monHoc, task) = await _fx.SeedTreeAsync();
            // Baselines for the rows we are NOT changing, so the assertion isolates the applied row.
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, hocKy);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, monHoc);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "applied");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            using var ctx = _fx.NewContext();
            var changes = await SyncChangeEnumerator.GetChangesForPeerAsync(ctx, SyncApplyFixture.PeerDevice);

            Assert.Empty(changes.StudyTasks);
            Assert.Empty(changes.HocKys);
            Assert.Empty(changes.MonHocs);
        }

        /// <summary>
        /// V (the other half) — the baseline is per peer, so a DIFFERENT peer still sees the applied row
        /// as a change. A single global watermark would wrongly hide it.
        /// </summary>
        [Fact]
        public async Task V_AppliedRow_IsStillEmittedToADifferentPeer()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "applied");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            using var ctx = _fx.NewContext();
            var changes = await SyncChangeEnumerator.GetChangesForPeerAsync(ctx, SyncApplyFixture.OtherPeerDevice);

            Assert.Contains(changes.StudyTasks, c => c.EntityId == task.MaTask);
        }

        // ------------------------------------------------------------------ W. independent operations

        /// <summary>
        /// W — D8-G: independent operations are independently transactional. One rejected operation in a
        /// change set does not roll back an unrelated one that succeeded.
        /// </summary>
        [Fact]
        public async Task W_OneRejectedOperation_DoesNotRollBackAnIndependentOne()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var goodRemote = CloneTask(task, t => t.TenTask = "committed anyway");
            SyncApplyFixture.Stamp(goodRemote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            // A log whose parent task does not exist on this device: deferred, then rejected.
            var orphanLog = new StudyLog
            {
                Id = Guid.NewGuid(),
                MaTask = Guid.NewGuid(),
                NgayHoc = new DateTime(2026, 6, 1),
                SoPhutHoc = 10,
                CreatedAtUtc = SyncApplyFixture.RemoteEarlier,
                DeviceId = SyncApplyFixture.PeerDevice,
            };
            SyncApplyFixture.Stamp(orphanLog, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(goodRemote, orphanLog));

            var applied = report.Results.Single(r => r.Operation.EntityId == task.MaTask);
            var rejected = report.Results.Single(r => r.Operation.EntityId == orphanLog.Id);

            Assert.Equal(SyncApplyOutcome.Applied, applied.Outcome);
            Assert.Equal(SyncApplyOutcome.Rejected, rejected.Outcome);
            Assert.Equal(SyncApplyReason.MissingParent, rejected.Reason);

            Assert.Equal("committed anyway", (await _fx.ReadTaskAsync(task.MaTask))!.TenTask);
            Assert.Null(await _fx.ReadLogAsync(orphanLog.Id));          // no live orphan, nothing half-written
        }

        /// <summary>
        /// DoR §12.5 step 3 — a child whose parent arrives in the SAME change set is applied, because
        /// the planner orders parents first and the retry pass re-runs anything deferred. An ordering
        /// bug here would turn a perfectly valid operation into a false MissingParent rejection.
        /// </summary>
        [Fact]
        public async Task ParentAndChildInOneChangeSet_BothApply_RegardlessOfInputOrder()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();

            var newMonHocId = Guid.NewGuid();
            var newTaskId = Guid.NewGuid();

            var remoteMonHoc = new MonHoc { MaMonHoc = newMonHocId, MaHocKy = hocKy.MaHocKy, TenMonHoc = "MH mới", SoTinChi = 2 };
            var remoteTask = new StudyTask
            {
                MaTask = newTaskId,
                MaMonHoc = newMonHocId,
                TenTask = "Task mới",
                HanChot = new DateTime(2026, 7, 1),
                TrangThai = StudyTaskStatus.ChuaLam,
                LoaiTask = LoaiCongViec.ThiGiuaKy,
                DoKho = 3,
            };
            SyncApplyFixture.Stamp(remoteMonHoc, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            SyncApplyFixture.Stamp(remoteTask, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            // Child listed BEFORE its parent on purpose.
            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remoteTask, remoteMonHoc));

            Assert.True(report.AllCommitted, string.Join(", ", report.Results.Select(r => $"{r.Operation.Describe()}={r.Outcome}/{r.Reason}")));
            Assert.NotNull(await _fx.ReadMonHocAsync(newMonHocId));
            Assert.NotNull(await _fx.ReadTaskAsync(newTaskId));
        }

        // ------------------------------------------------------------------ fail-closed inputs

        /// <summary>
        /// DoR §5.5 row 10 — an unreadable baseline rejects only its own operation and never degrades
        /// to "treat as null Base", which would silently re-apply the remote row over local edits.
        /// </summary>
        [Fact]
        public async Task UnreadableBaseline_RejectsOnlyThatOperation()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetRawBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.StudyTask, task.MaTask, 1,
                                          "{\"v\":1,\"entityType\":\"StudyTask\"");   // truncated JSON

            var remote = CloneTask(task, t => t.TenTask = "must not land");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var result = Assert.Single(report.Results);

            Assert.Equal(SyncApplyOutcome.Rejected, result.Outcome);
            Assert.Equal(SyncApplyReason.BaselineUnreadable, result.Reason);
            Assert.Equal("Task", (await _fx.ReadTaskAsync(task.MaTask))!.TenTask);
        }
    }
}
