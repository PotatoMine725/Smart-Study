using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Fixtures
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (mission §4, §21). Wraps <see cref="SyncApplyFixture"/> with staging
    /// recipes for each unresolved conflict shape, reused verbatim from the existing PR-5/PR-6 test
    /// suites (mission §4.6 asset list) rather than re-derived. <see cref="SeedS2RecordAsync"/> and
    /// <see cref="SeedUnsupportedRecordAsync"/> are directly constructed -- the plan names them
    /// "Seed", not "Stage", because no production write path reaches these shapes today (S2: no local
    /// writer reassigns <c>TaskNote.MaTask</c>; Unsupported: fail-closed data by definition).
    /// </summary>
    internal sealed class FenceScenarioFixture : IDisposable
    {
        public readonly SyncApplyFixture Fx = new();

        public void Dispose() => Fx.Dispose();

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

        // ------------------------------------------------------------------ S1-CR

        /// <summary>
        /// Recipe from <c>SyncApplyParentHandlingTests.L_ConcurrentReparent_*</c>: both sides reparent
        /// the same StudyTask to different targets. Stages a StructuralConflict(ConcurrentReparent) on
        /// the task, held live at Base (parent A).
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, HocKy HocKy, MonHoc MonHocA, MonHoc MonHocB, MonHoc MonHocC, StudyTask Task)>
            StageS1CrAsync()
        {
            var (hocKy, monHocA, task) = await Fx.SeedTreeAsync();
            var monHocB = new MonHoc("MH B", 2) { MaHocKy = hocKy.MaHocKy };
            var monHocC = new MonHoc("MH C", 4) { MaHocKy = hocKy.MaHocKy };
            await Fx.AddLocalAsync(monHocB);
            await Fx.AddLocalAsync(monHocC);
            await Fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = Fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = monHocB.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var before = await ExistingConflictIdsAsync();
            var remote = CloneTask(task, t => t.MaMonHoc = monHocC.MaMonHoc);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await Fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = await NewlyStagedRecordAsync(before);
            return (record, hocKy, monHocA, monHocB, monHocC, task);
        }

        // ------------------------------------------------------------------ S1-PT

        /// <summary>
        /// Recipe from <c>SyncApplyParentHandlingTests.M_UpdateUnderTombstonedStructuralParent_*</c>:
        /// an incoming update to a task whose D4 parent is tombstoned locally. Stages a
        /// StructuralConflict(ParentTombstoned) with the local candidate present, held live at Base.
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, MonHoc MonHoc, StudyTask Task)> StageS1PtAsync()
        {
            var (_, monHoc, task) = await Fx.SeedTreeAsync();
            await Fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = Fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE"))
            {
                var live = await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == monHoc.MaMonHoc);
                ctx.MonHocs.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var before = await ExistingConflictIdsAsync();
            var remote = CloneTask(task, t => t.TenTask = "must not land live");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await Fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = await NewlyStagedRecordAsync(before);
            return (record, monHoc, task);
        }

        // ------------------------------------------------------------------ AL-PT

        /// <summary>
        /// Recipe from <c>SyncApplyParentHandlingTests.M_CreateUnderTombstonedStructuralParent_*</c>:
        /// an incoming create under a tombstoned D4 parent is never materialised. Stages a
        /// StructuralConflict(ParentTombstoned) with NO local candidate (D4/D9-T4 amendment) -- the
        /// AL-PT shape, on a MonHoc identity that never has a live row.
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, HocKy HocKy, Guid AbsentMonHocId)> StageAlPtAsync()
        {
            var (hocKy, _, _) = await Fx.SeedTreeAsync();

            using (var ctx = Fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE"))
            {
                var live = await ctx.HocKys.FirstAsync(h => h.MaHocKy == hocKy.MaHocKy);
                ctx.HocKys.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var before = await ExistingConflictIdsAsync();
            var newMonHocId = Guid.NewGuid();
            var remote = new MonHoc { MaMonHoc = newMonHocId, MaHocKy = hocKy.MaHocKy, TenMonHoc = "orphan?", SoTinChi = 3 };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await Fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = await NewlyStagedRecordAsync(before);
            return (record, hocKy, newMonHocId);
        }

        // ------------------------------------------------------------------ S3 (Constraint, Base null)

        /// <summary>
        /// Recipe from <c>SyncApplyConflictStagingTests.J_NullBaseTaskNoteCollision_*</c>: two
        /// competing TaskNote creates in the same scope, no Base. Stages a ConstraintConflict with
        /// Base = null and hard-deletes the withdrawn local candidate (M5) -- the scope has NO live
        /// row afterward, which is the S3 empty-scope case OD-4 governs.
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, StudyTask Task)> StageS3Async()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();

            var n1 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "local" };
            await Fx.AddLocalAsync(n1);

            var before = await ExistingConflictIdsAsync();
            var n2 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "remote" };
            SyncApplyFixture.Stamp(n2, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await Fx.Session().ApplyAsync(SyncApplyFixture.From(n2));

            var record = await NewlyStagedRecordAsync(before);
            return (record, task);
        }

        private async Task<HashSet<Guid>> ExistingConflictIdsAsync() =>
            (await Fx.ReadConflictsAsync()).Select(r => r.ConflictId).ToHashSet();

        private async Task<SyncConflictRecordRow> NewlyStagedRecordAsync(HashSet<Guid> before) =>
            Assert.Single(await Fx.ReadConflictsAsync(), r => !before.Contains(r.ConflictId));

        // ------------------------------------------------------------------ S2 (Constraint, Base present)

        /// <summary>
        /// Constructed (not staged: no production write path reassigns <c>TaskNote.MaTask</c>, and PR-6
        /// DoR §2.1 records S2 as unreachable in v1 by construction on the LOCAL path). Seeds a LIVE
        /// occupant note at the scope's Base identity, so the record has a real occupant for
        /// <see cref="ImpactResolver"/> to report as <c>Released</c>/<c>OccupantContentChanged</c> --
        /// a record with no live note in scope would silently only ever exercise the OD-4 empty-scope
        /// branch instead.
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, StudyTask Task, TaskNote OccupantNote)> SeedS2RecordAsync()
        {
            var (_, _, task) = await Fx.SeedTreeAsync();

            var occupant = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "base content" };
            await Fx.AddLocalAsync(occupant);

            var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", task.MaTask.ToString("D"));
            var scopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null, scope);

            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "s2-seed-" + Guid.NewGuid().ToString("N"),
                ScopeKey = scopeKey,
                Kind = ConflictKind.ConstraintConflict,
                EntityType = SyncEntityTypes.TaskNote,
                ConstraintKey = "MaTask",
                ConstraintValue = task.MaTask.ToString("D"),
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = occupant.Id,
                BaseSnapshotJson = "{}",
                BaseFingerprint = "seed-base-fp",
                LocalEntityId = occupant.Id,
                LocalSnapshotJson = "{}",
                LocalFingerprint = "seed-base-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "seed-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };

            using (var ctx = Fx.NewContext())
            {
                ctx.SyncConflictRecords.Add(row);
                await ctx.SaveChangesAsync();
            }

            return (row, task, occupant);
        }

        // ------------------------------------------------------------------ Unsupported

        /// <summary>
        /// Constructed: an Unresolved record whose column tuple <see cref="ConflictShapeClassifier"/>
        /// does not recognise (an out-of-range <see cref="StructuralReason"/>). Fail-closed data by
        /// construction -- there is no staging path that produces it.
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, StudyTask Task)> SeedUnsupportedRecordAsync()
        {
            var (_, monHoc, task) = await Fx.SeedTreeAsync();

            var scopeKey = ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", null);

            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "unsupported-seed-" + Guid.NewGuid().ToString("N"),
                ScopeKey = scopeKey,
                Kind = ConflictKind.StructuralConflict,
                EntityType = SyncEntityTypes.StudyTask,
                EntityId = task.MaTask,
                FieldName = "MaMonHoc",
                StructuralReason = (StructuralReason)99,
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = task.MaTask,
                BaseSnapshotJson = "{}",
                BaseFingerprint = "seed-base-fp",
                LocalEntityId = task.MaTask,
                LocalSnapshotJson = "{}",
                LocalFingerprint = "seed-local-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "seed-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };

            using (var ctx = Fx.NewContext())
            {
                ctx.SyncConflictRecords.Add(row);
                await ctx.SaveChangesAsync();
            }

            return (row, task);
        }

        // ------------------------------------------------------------------ Malformed evidence (N-3)

        /// <summary>
        /// Constructed: an otherwise well-formed S1-PT record whose <c>BaseSnapshotJson</c> is not
        /// parseable canonical JSON. Used to prove N-3 (unreadable evidence -&gt; <c>Unsupported</c>,
        /// never a thrown exception past the router).
        /// </summary>
        public async Task<(SyncConflictRecordRow Record, StudyTask Task)> SeedUnreadableEvidenceRecordAsync()
        {
            var (_, monHoc, task) = await Fx.SeedTreeAsync();

            var scopeKey = ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", null);

            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "unreadable-seed-" + Guid.NewGuid().ToString("N"),
                ScopeKey = scopeKey,
                Kind = ConflictKind.StructuralConflict,
                EntityType = SyncEntityTypes.StudyTask,
                EntityId = task.MaTask,
                FieldName = "MaMonHoc",
                StructuralReason = StructuralReason.ParentTombstoned,
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = task.MaTask,
                BaseSnapshotJson = "{not valid json",
                BaseFingerprint = "seed-base-fp",
                LocalEntityId = task.MaTask,
                LocalSnapshotJson = "{not valid json",
                LocalFingerprint = "seed-local-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "seed-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };

            using (var ctx = Fx.NewContext())
            {
                ctx.SyncConflictRecords.Add(row);
                await ctx.SaveChangesAsync();
            }

            return (row, task);
        }

        // ------------------------------------------------------------------ custom context types

        /// <summary>
        /// A <see cref="TestDoubles.SaveCountingDbContext"/> bound to the SAME shared connection every
        /// <see cref="Fx"/>-built context uses -- obtained via <c>Database.GetDbConnection()</c> rather
        /// than a new field, since <see cref="SyncApplyFixture"/>'s connection is private (mission §4:
        /// this fixture wraps it, it does not fork it).
        /// </summary>
        public TestDoubles.SaveCountingDbContext NewSaveCountingContext()
        {
            using var probe = Fx.NewContext();
            var conn = probe.Database.GetDbConnection();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;

            var ctx = new TestDoubles.SaveCountingDbContext(options);
            ctx.Clock = () => SyncApplyFixture.LocalNow;
            ctx.DeviceIdProvider = () => SyncApplyFixture.LocalDevice;
            return ctx;
        }

        // ------------------------------------------------------------------ read-only proofs

        /// <summary>
        /// A deterministic, order-independent text snapshot of every synced table plus the two
        /// bookkeeping tables, keyed by (Id, Rev, IsDeleted, ModifiedAtUtc). Used by the read-only
        /// tests (X-8) and the "blocked leaves tables byte-identical" assertions: two snapshots taken
        /// before/after a router evaluation must be equal.
        /// </summary>
        public async Task<string> SnapshotAllTablesAsync()
        {
            using var ctx = Fx.NewContext();
            var sb = new StringBuilder();

            async Task AppendAsync<T>(string label, IQueryable<T> query, Func<T, string> project) where T : class
            {
                var rows = await query.AsNoTracking().ToListAsync();
                sb.Append(label).Append(':').Append(rows.Count).Append('|');
                foreach (var line in rows.Select(project).OrderBy(s => s, StringComparer.Ordinal))
                    sb.Append(line).Append(';');
                sb.Append('\n');
            }

            await AppendAsync("HocKy", ctx.HocKys, h => $"{h.MaHocKy:D}:{h.Rev}:{h.IsDeleted}:{h.ModifiedAtUtc:O}");
            await AppendAsync("MonHoc", ctx.MonHocs, m => $"{m.MaMonHoc:D}:{m.Rev}:{m.IsDeleted}:{m.ModifiedAtUtc:O}");
            await AppendAsync("StudyTask", ctx.StudyTasks, t => $"{t.MaTask:D}:{t.Rev}:{t.IsDeleted}:{t.ModifiedAtUtc:O}");
            await AppendAsync("TaskNote", ctx.TaskNotes, n => $"{n.Id:D}:{n.Rev}:{n.IsDeleted}:{n.ModifiedAtUtc:O}");
            await AppendAsync("TaskReferenceLink", ctx.TaskReferenceLinks, l => $"{l.Id:D}:{l.Rev}:{l.IsDeleted}:{l.ModifiedAtUtc:O}");
            await AppendAsync("StudyLog", ctx.StudyLogs, l => $"{l.Id:D}:{l.Rev}:{l.IsDeleted}:{l.ModifiedAtUtc:O}");
            await AppendAsync("SyncConflictRecords", ctx.SyncConflictRecords,
                r => $"{r.ConflictId:D}:{r.Status}:{r.ResolvedAtUtc:O}");
            await AppendAsync("SyncBaseSnapshots", ctx.SyncBaseSnapshots,
                r => $"{r.PeerDeviceId}|{r.EntityType}|{r.EntityId:D}:{r.Rev}");

            return sb.ToString();
        }
    }
}
