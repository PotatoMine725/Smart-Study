using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Fence;
using Xunit;

namespace SmartStudyPlanner.Tests.Fixtures
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4. Drives a local save the way the VMs do: load through
    /// <c>LayDanhSachHocKyAsync</c>, change the in-memory graph, <c>LuuHocKyAsync</c> the whole HocKy.
    /// Only public entry points are used for the save itself, so a test written against this driver
    /// observes exactly what a VM command observes.
    /// </summary>
    internal sealed class LocalSaveDriver
    {
        private readonly SyncApplyFixture _fx;

        public LocalSaveDriver(SyncApplyFixture fx)
        {
            _fx = fx;
            Repo = new SqliteHocKyRepository(fx.Factory);
        }

        public SqliteHocKyRepository Repo { get; }

        public async Task<HocKy> LoadAsync(Guid hocKyId) =>
            (await Repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKyId);

        public Task SaveAsync(HocKy graph) => Repo.LuuHocKyAsync(graph);

        public Task<MutationRejectedException> SaveExpectingRejectionAsync(HocKy graph) =>
            Assert.ThrowsAsync<MutationRejectedException>(() => Repo.LuuHocKyAsync(graph));

        // ------------------------------------------------------------------ graph edits (VM-equivalent)

        public static MonHoc Mon(HocKy graph, Guid id) => graph.DanhSachMonHoc.Single(m => m.MaMonHoc == id);

        public static StudyTask Task(HocKy graph, Guid id) =>
            graph.DanhSachMonHoc.SelectMany(m => m.DanhSachTask).Single(t => t.MaTask == id);

        public static bool Contains(HocKy graph, Guid taskId) =>
            graph.DanhSachMonHoc.SelectMany(m => m.DanhSachTask).Any(t => t.MaTask == taskId);

        /// <summary>XoaTask: drop the task from its owner's collection.</summary>
        public static void RemoveTask(HocKy graph, Guid taskId)
        {
            var owner = graph.DanhSachMonHoc.Single(m => m.DanhSachTask.Any(t => t.MaTask == taskId));
            owner.DanhSachTask.Remove(owner.DanhSachTask.Single(t => t.MaTask == taskId));
        }

        /// <summary>XoaMon: drop the MonHoc from the HocKy's collection.</summary>
        public static void RemoveMon(HocKy graph, Guid monHocId) => graph.DanhSachMonHoc.Remove(Mon(graph, monHocId));

        public static void MoveTask(HocKy graph, Guid taskId, Guid toMonHocId)
        {
            var task = Task(graph, taskId);
            RemoveTask(graph, taskId);
            task.MaMonHoc = toMonHocId;
            Mon(graph, toMonHocId).DanhSachTask.Add(task);
        }

        // ------------------------------------------------------------------ assertions

        public static PolicyResult SingleBlocking(MutationRejectedException ex)
        {
            Assert.True(ex.Decision.RouteKnown);
            Assert.False(ex.Decision.FencePassed);
            return Assert.Single(ex.Decision.Results, r => r.Outcome is FenceOutcome.Blocked or FenceOutcome.Unsupported);
        }

        /// <summary>Base-drift primitive (D8-H) over the task's CURRENT live row.</summary>
        public async Task<bool> BaseStillMatchesAsync(SyncConflictRecordRow record, Guid taskId)
        {
            var stored = (await _fx.ReadConflictsAsync()).Single(r => r.ConflictId == record.ConflictId);
            var live = (await _fx.ReadTaskAsync(taskId))!;
            return SyncBaseFingerprint.Matches(stored.BaseFingerprint, IncomingChanges.Of(live).Snapshot);
        }

        public async Task<ConflictRecordStatus> StatusOfAsync(SyncConflictRecordRow record) =>
            (await _fx.ReadConflictsAsync()).Single(r => r.ConflictId == record.ConflictId).Status;

        /// <summary>
        /// OD-7 "the in-memory graph matches persisted state": the live rows of the HocKy, read back
        /// fresh, against the caller's graph -- membership (which task under which MonHoc) and every
        /// mapped scalar that the save path reads or writes.
        /// </summary>
        public async Task AssertGraphMatchesPersistedAsync(HocKy graph)
        {
            using var ctx = _fx.NewContext();
            var persisted = await ctx.HocKys.AsNoTracking()
                .Include(h => h.DanhSachMonHoc.Where(m => !m.IsDeleted))
                .ThenInclude(m => m.DanhSachTask.Where(t => !t.IsDeleted))
                .SingleAsync(h => h.MaHocKy == graph.MaHocKy);

            Assert.Equal(Describe(persisted), Describe(graph));
        }

        public static IReadOnlyList<string> Describe(HocKy h)
        {
            var lines = new List<string> { $"HocKy {h.MaHocKy:D} {h.Ten} {h.NgayBatDau:O} rev={h.Rev} mod={h.ModifiedAtUtc:O}" };
            foreach (var m in h.DanhSachMonHoc.OrderBy(m => m.MaMonHoc))
            {
                lines.Add($" MonHoc {m.MaMonHoc:D} hk={m.MaHocKy:D} {m.TenMonHoc} {m.SoTinChi} rev={m.Rev} mod={m.ModifiedAtUtc:O} dead={m.IsDeleted}");
                foreach (var t in m.DanhSachTask.OrderBy(t => t.MaTask))
                {
                    lines.Add($"  Task {t.MaTask:D} mh={t.MaMonHoc:D} {t.TenTask} {t.HanChot:O} {t.TrangThai} {t.LoaiTask} {t.DoKho} " +
                              $"{t.ThoiGianDaHoc} {t.NgayHoanThanh:O} prio={t.DiemUuTien} warn={t.MucDoCanhBao} rev={t.Rev} mod={t.ModifiedAtUtc:O} dead={t.IsDeleted}");
                }
            }
            return lines;
        }
    }
}
