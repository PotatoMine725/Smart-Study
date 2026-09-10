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
    /// Epic 2 / T2.4 (PR-5) — tests L, M, N, O from the PR-5 brief: D4 structural conflicts, D9-T4
    /// tombstoned-parent handling, the owner-acknowledged FK-only cascade rule (A2-a) and cascade
    /// provenance inheritance (A2-b).
    /// </summary>
    public class SyncApplyParentHandlingTests : IDisposable
    {
        private const string DeleterDevice = "DELETER-DEVICE";
        private static readonly DateTime DeletedLocallyAt = new(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc);

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

        // ------------------------------------------------------------------ L. StructuralConflict

        /// <summary>
        /// L — D4 case C: both sides reparent the same task to DIFFERENT targets. No auto-winner. While
        /// the conflict is Unresolved the WHOLE live row equals Base (D9-T1 read literally), so the
        /// locally-changed ordinary field is rolled back to Base too — partially applying it would make
        /// live != Base.
        /// </summary>
        [Fact]
        public async Task L_ConcurrentReparent_StagesUnresolvedConflict_AndHoldsTheWholeRowAtBase()
        {
            var (hocKy, monHocA, task) = await _fx.SeedTreeAsync();

            var monHocB = new MonHoc("MH B", 2) { MaHocKy = hocKy.MaHocKy };
            var monHocC = new MonHoc("MH C", 4) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(monHocB);
            await _fx.AddLocalAsync(monHocC);

            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);   // Base: parent A, name "Task"

            // Local: reparent to B and also rename.
            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = monHocB.MaMonHoc;
                live.TenTask = "renamed locally";
                await ctx.SaveChangesAsync();
            }

            // Remote: reparent to C.
            var remote = CloneTask(task, t => t.MaMonHoc = monHocC.MaMonHoc);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var result = Assert.Single(report.Results);
            Assert.Equal(SyncApplyOutcome.ConflictStaged, result.Outcome);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictKind.StructuralConflict, record.Kind);
            Assert.Equal(StructuralReason.ConcurrentReparent, record.StructuralReason);
            Assert.Equal(ConflictRecordStatus.Unresolved, record.Status);
            Assert.Equal("MaMonHoc", record.FieldName);
            Assert.Equal(ConflictLocalWithdrawal.RewrittenToBase, record.LocalWithdrawal);
            Assert.Equal(2, record.LocalRowRev);                       // the withdrawn row's Rev at staging

            // Live state == Base, whole row.
            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(monHocA.MaMonHoc, row!.MaMonHoc);
            Assert.Equal("Task", row.TenTask);
            Assert.Equal(SyncApplyFixture.LocalDevice, row.ModifiedByDeviceId);       // Base's provenance
            Assert.Equal(SyncApplyFixture.LocalEarlier, DateTime.SpecifyKind(row.ModifiedAtUtc, DateTimeKind.Utc));
            Assert.Equal(3, row.Rev);                                                  // the rewrite is a real local write

            // Evidence preserves both candidates in full.
            Assert.NotNull(record.BaseSnapshotJson);
            Assert.Contains("renamed locally", record.LocalSnapshotJson);
            Assert.Contains(monHocC.MaMonHoc.ToString("D"), record.RemoteSnapshotJson);
        }

        // ------------------------------------------------------------------ M. tombstoned parent

        /// <summary>
        /// M — D9-T4: an incoming update to a child whose D4 parent is tombstoned locally becomes a
        /// StructuralConflict(ParentTombstoned). Never a live orphan, and never an LWW.
        /// </summary>
        [Fact]
        public async Task M_UpdateUnderTombstonedStructuralParent_StagesParentTombstonedConflict()
        {
            var (_, monHoc, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            // Tombstone the parent subject only. Loading it without its children means EF's cascade
            // fixup cannot reach the task, which is the documented Epic-1 shape.
            using (var ctx = _fx.NewContext(DeletedLocallyAt, DeleterDevice))
            {
                var live = await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == monHoc.MaMonHoc);
                ctx.MonHocs.Remove(live);
                await ctx.SaveChangesAsync();
            }
            Assert.True((await _fx.ReadMonHocAsync(monHoc.MaMonHoc))!.IsDeleted);

            var remote = CloneTask(task, t => t.TenTask = "must not land live");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            Assert.Equal(SyncApplyOutcome.ConflictStaged, Assert.Single(report.Results).Outcome);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictKind.StructuralConflict, record.Kind);
            Assert.Equal(StructuralReason.ParentTombstoned, record.StructuralReason);
            Assert.Equal(ConflictRecordStatus.Unresolved, record.Status);

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal("Task", row!.TenTask);                 // held at Base
            Assert.False(row.IsDeleted);
        }

        /// <summary>
        /// M (create variant) — an incoming CREATE under a tombstoned D4 parent is not materialised at
        /// all, so no live orphan is ever produced. It is rejected rather than staged: with neither a
        /// Base nor a local row there is no competing local candidate, and both
        /// <c>ConflictCandidate.Local</c> and the schema's <c>LocalSnapshotJson</c> are non-nullable, so
        /// staging would mean recording evidence that never existed. Reported as a semantic blocker in
        /// the PR notes; harmless in practice because the peer re-offers the row until its own cascade
        /// turns it into a tombstone, which applies cleanly.
        /// </summary>
        [Fact]
        public async Task M_CreateUnderTombstonedStructuralParent_IsNotMaterialised()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();

            using (var ctx = _fx.NewContext(DeletedLocallyAt, DeleterDevice))
            {
                var live = await ctx.HocKys.FirstAsync(h => h.MaHocKy == hocKy.MaHocKy);
                ctx.HocKys.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var newMonHocId = Guid.NewGuid();
            var remote = new MonHoc { MaMonHoc = newMonHocId, MaHocKy = hocKy.MaHocKy, TenMonHoc = "orphan?", SoTinChi = 3 };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var result = Assert.Single(report.Results);

            Assert.Equal(SyncApplyOutcome.Rejected, result.Outcome);
            Assert.Equal(SyncApplyReason.ParentTombstonedNoLocalRow, result.Reason);
            Assert.Null(await _fx.ReadMonHocAsync(newMonHocId));      // the invariant that matters
        }

        // ------------------------------------------------------------------ N. FK-only child cascade

        /// <summary>
        /// N (ACK A2-a) — an FK-only child (TaskNote / TaskReferenceLink) arriving LIVE under a
        /// tombstoned task is materialised as a TOMBSTONE carrying the parent's tombstone provenance,
        /// not as a StructuralConflict. A StructuralConflict here would offer KeepLocal and KeepRemote,
        /// both of which re-create a live child under a dead parent.
        /// </summary>
        [Fact]
        public async Task N_LiveFkOnlyChildUnderTombstonedTask_IsCascadeTombstonedWithParentProvenance()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            using (var ctx = _fx.NewContext(DeletedLocallyAt, DeleterDevice))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                ctx.StudyTasks.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var remoteNote = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "ghi chú từ peer" };
            SyncApplyFixture.Stamp(remoteNote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var remoteLink = new TaskReferenceLink
            {
                Id = Guid.NewGuid(),
                MaTask = task.MaTask,
                Title = "tài liệu",
                Url = "https://example.invalid/a",
                Category = "doc",
                SortOrder = 1,
                CreatedAtUtc = SyncApplyFixture.RemoteEarlier,
            };
            SyncApplyFixture.Stamp(remoteLink, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remoteNote, remoteLink));
            Assert.True(report.AllCommitted, string.Join(", ", report.Results.Select(r => $"{r.Operation.Describe()}={r.Outcome}/{r.Reason}")));

            var note = await _fx.ReadNoteAsync(remoteNote.Id);
            Assert.NotNull(note);
            Assert.True(note!.IsDeleted);
            Assert.Equal("ghi chú từ peer", note.Content);                           // content preserved
            Assert.Equal(DeleterDevice, note.ModifiedByDeviceId);                     // PARENT's provenance...
            Assert.Equal(DeletedLocallyAt, DateTime.SpecifyKind(note.ModifiedAtUtc, DateTimeKind.Utc));
            Assert.Equal(DeletedLocallyAt, DateTime.SpecifyKind(note.DeletedAtUtc!.Value, DateTimeKind.Utc));
            Assert.NotEqual(SyncApplyFixture.LocalDevice, note.ModifiedByDeviceId);   // ...not the local clock/device
            Assert.NotEqual(SyncApplyFixture.PeerDevice, note.ModifiedByDeviceId);

            var link = await _fx.ReadLinkAsync(remoteLink.Id);
            Assert.True(link!.IsDeleted);
            Assert.Equal(DeleterDevice, link.ModifiedByDeviceId);
        }

        /// <summary>
        /// N (StudyLog is NOT cascaded) — <c>StudyLog.MaTask</c> has no FK and Epic-1's cascade never
        /// touched logs, so a live log under a tombstoned task applies unchanged. Inventing a cascade
        /// here would silently delete the data analytics and the M8 models read.
        /// </summary>
        [Fact]
        public async Task N_StudyLogUnderTombstonedTask_AppliesLiveWithNoEvidence()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            using (var ctx = _fx.NewContext(DeletedLocallyAt, DeleterDevice))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                ctx.StudyTasks.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var remoteLog = new StudyLog
            {
                Id = Guid.NewGuid(),
                MaTask = task.MaTask,
                NgayHoc = new DateTime(2026, 5, 30),
                SoPhutHoc = 30,
                CreatedAtUtc = SyncApplyFixture.RemoteEarlier,
                DeviceId = SyncApplyFixture.PeerDevice,
            };
            SyncApplyFixture.Stamp(remoteLog, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remoteLog));

            var log = await _fx.ReadLogAsync(remoteLog.Id);
            Assert.NotNull(log);
            Assert.False(log!.IsDeleted);                               // stays live
            Assert.Equal(SyncApplyFixture.PeerDevice, log.ModifiedByDeviceId);
            Assert.Empty(await _fx.ReadConflictsAsync());                // and no evidence is invented
        }

        // ------------------------------------------------------------------ O. cascade provenance

        /// <summary>
        /// O (ACK A2-b) — applying a remote parent tombstone cascade-tombstones the parent's LIVE LOCAL
        /// children, which EF's own fixup can never reach because TaskNote/TaskReferenceLink have no
        /// navigation from StudyTask. Every descendant carries the ORIGINATING tombstone's provenance,
        /// which is what makes both peers write identical bytes for one causal deletion.
        /// <para>
        /// The session's own clock/device are deliberately different from the remote's, so "copied the
        /// parent's provenance" and "stamped locally" cannot produce the same bytes.
        /// </para>
        /// </summary>
        [Fact]
        public async Task O_RemoteTaskTombstone_CascadesToUnloadedChildren_WithParentProvenance()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "nội dung" };
            var link = new TaskReferenceLink
            {
                Id = Guid.NewGuid(), MaTask = task.MaTask, Title = "t", Url = "u", SortOrder = 0,
                CreatedAtUtc = SyncApplyFixture.LocalEarlier,
            };
            var log = new StudyLog
            {
                Id = Guid.NewGuid(), MaTask = task.MaTask, NgayHoc = new DateTime(2026, 4, 4),
                SoPhutHoc = 15, CreatedAtUtc = SyncApplyFixture.LocalEarlier, DeviceId = SyncApplyFixture.LocalDevice,
            };
            await _fx.AddLocalAsync(note);
            await _fx.AddLocalAsync(link);
            await _fx.AddLocalAsync(log);

            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var deletedAt = new DateTime(2026, 6, 1, 11, 22, 33, DateTimeKind.Utc);
            var remote = CloneTask(task);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice,
                                   isDeleted: true, deletedAtUtc: deletedAt);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            Assert.True((await _fx.ReadTaskAsync(task.MaTask))!.IsDeleted);

            var noteRow = await _fx.ReadNoteAsync(note.Id);
            Assert.True(noteRow!.IsDeleted);
            Assert.Equal(SyncApplyFixture.PeerDevice, noteRow.ModifiedByDeviceId);
            Assert.Equal(SyncApplyFixture.RemoteLater, DateTime.SpecifyKind(noteRow.ModifiedAtUtc, DateTimeKind.Utc));
            Assert.Equal(deletedAt, DateTime.SpecifyKind(noteRow.DeletedAtUtc!.Value, DateTimeKind.Utc));
            Assert.NotEqual(SyncApplyFixture.LocalDevice, noteRow.ModifiedByDeviceId);

            var linkRow = await _fx.ReadLinkAsync(link.Id);
            Assert.True(linkRow!.IsDeleted);
            Assert.Equal(SyncApplyFixture.PeerDevice, linkRow.ModifiedByDeviceId);
            Assert.Equal(deletedAt, DateTime.SpecifyKind(linkRow.DeletedAtUtc!.Value, DateTimeKind.Utc));

            // StudyLog has no FK and is not part of the Epic-1 cascade.
            Assert.False((await _fx.ReadLogAsync(log.Id))!.IsDeleted);

            // Cascade children are part of the same logical operation, so their baselines advance too —
            // otherwise this peer would be re-offered the child tombstones on every run.
            var noteBaseline = await _fx.ReadBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.TaskNote, note.Id);
            Assert.NotNull(noteBaseline);
            Assert.Equal(noteRow.Rev, noteBaseline!.Rev);
        }

        /// <summary>
        /// O (grandchildren) — a HocKy tombstone cascades HocKy → MonHoc → StudyTask →
        /// {TaskNote, TaskReferenceLink}, the exact edge set the model already has, and the originating
        /// provenance is propagated unchanged all the way down.
        /// </summary>
        [Fact]
        public async Task O_RemoteSemesterTombstone_CascadesThroughTheWholeTree()
        {
            var (hocKy, monHoc, task) = await _fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "x" };
            await _fx.AddLocalAsync(note);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, hocKy);

            var deletedAt = new DateTime(2026, 6, 3, 1, 2, 3, DateTimeKind.Utc);
            var remote = new HocKy { MaHocKy = hocKy.MaHocKy, Ten = hocKy.Ten, NgayBatDau = hocKy.NgayBatDau };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice,
                                   isDeleted: true, deletedAtUtc: deletedAt);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            foreach (var (label, isDeleted, device, deleted) in new[]
            {
                ("HocKy", (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!.IsDeleted, (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!.ModifiedByDeviceId, (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!.DeletedAtUtc),
                ("MonHoc", (await _fx.ReadMonHocAsync(monHoc.MaMonHoc))!.IsDeleted, (await _fx.ReadMonHocAsync(monHoc.MaMonHoc))!.ModifiedByDeviceId, (await _fx.ReadMonHocAsync(monHoc.MaMonHoc))!.DeletedAtUtc),
                ("StudyTask", (await _fx.ReadTaskAsync(task.MaTask))!.IsDeleted, (await _fx.ReadTaskAsync(task.MaTask))!.ModifiedByDeviceId, (await _fx.ReadTaskAsync(task.MaTask))!.DeletedAtUtc),
                ("TaskNote", (await _fx.ReadNoteAsync(note.Id))!.IsDeleted, (await _fx.ReadNoteAsync(note.Id))!.ModifiedByDeviceId, (await _fx.ReadNoteAsync(note.Id))!.DeletedAtUtc),
            })
            {
                Assert.True(isDeleted, $"{label} should be tombstoned");
                Assert.Equal(SyncApplyFixture.PeerDevice, device);
                Assert.Equal(deletedAt, DateTime.SpecifyKind(deleted!.Value, DateTimeKind.Utc));
            }
        }

        /// <summary>
        /// Replaying the same parent tombstone must not re-cascade: the children are already tombstoned,
        /// so nothing is re-stamped and no Rev moves. Without the live→dead transition guard, every
        /// replay would bump every descendant.
        /// </summary>
        [Fact]
        public async Task RepeatedTombstoneApply_DoesNotReCascade()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "x" };
            await _fx.AddLocalAsync(note);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice, isDeleted: true);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var afterFirst = await _fx.ReadNoteAsync(note.Id);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var afterSecond = await _fx.ReadNoteAsync(note.Id);

            Assert.Equal(afterFirst!.Rev, afterSecond!.Rev);
            Assert.Equal(afterFirst.ModifiedAtUtc, afterSecond.ModifiedAtUtc);
        }
    }
}
