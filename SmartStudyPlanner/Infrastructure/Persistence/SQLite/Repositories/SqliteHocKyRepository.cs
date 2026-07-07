using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteHocKyRepository : IHocKyRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteHocKyRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            // Dùng ToListAsync() để lôi TOÀN BỘ học kỳ có trong DB lên
            var danhSach = await db.HocKys
                     .Where(hk => !hk.IsSeeded && !hk.IsDeleted)
                     .Include(hk => hk.DanhSachMonHoc.Where(mon => !mon.IsDeleted))
                        .ThenInclude(mon => mon.DanhSachTask.Where(t => !t.IsDeleted))
                     .ToListAsync(ct);

            // Khử trùng môn học clone: DB có thể chứa nhiều MonHoc cùng TenMonHoc
            // (do seed/lưu lặp) → giữ 1 bản đại diện mỗi tên để UI + mọi consumer
            // thấy danh sách sạch. Nhất quán pattern GroupBy(TenMonHoc) toàn dự án.
            // GỘP task từ MỌI clone (SelectMany, distinct theo MaTask) vào bản đại
            // diện trước khi loại clone → KHÔNG mất task nằm ở clone không-đại-diện.
            // Lần LuuHocKyAsync kế tiếp ghi đè danh sách đã khử trùng → prune clone khỏi DB.
            foreach (var hocKy in danhSach)
            {
                var monDuyNhat = hocKy.DanhSachMonHoc
                    .GroupBy(mon => mon.TenMonHoc)
                    .Select(nhom =>
                    {
                        var daiDien = nhom.First();

                        var taskGop = nhom
                            .SelectMany(mon => mon.DanhSachTask)
                            .GroupBy(task => task.MaTask)
                            .Select(nhomTask => nhomTask.First())
                            .ToList();

                        if (taskGop.Count != daiDien.DanhSachTask.Count)
                        {
                            daiDien.DanhSachTask.Clear();
                            foreach (var task in taskGop)
                                daiDien.DanhSachTask.Add(task);
                        }

                        return daiDien;
                    })
                    .ToList();

                if (monDuyNhat.Count != hocKy.DanhSachMonHoc.Count)
                {
                    hocKy.DanhSachMonHoc.Clear();
                    foreach (var mon in monDuyNhat)
                        hocKy.DanhSachMonHoc.Add(mon);
                }
            }

            return danhSach;
        }

        public async Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
        {
            if (hocKy == null) return;

            // Epic 1 / M1.2 (G1): reconciled in place instead of remove-then-recreate.
            // Deletes in this app are implicit — XoaTask/XoaMon drop the item from the in-memory
            // graph and re-save the whole thing — so a genuine delete only shows up here as
            // "row present in the old DB graph, absent from the new one." Diffing by Guid (stable
            // across saves — only `new StudyTask(...)`/`new MonHoc(...)` mint fresh ones) lets
            // unchanged rows keep their identity and Rev history instead of being torn down and
            // recreated on every save, which would collide on the primary key of every unchanged
            // row the moment these entities became tombstoned instead of hard-deleted (SyncStamper
            // converts Remove() to a soft IsDeleted update, so the old row is never actually gone).
            using var db = _ctxFactory();
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var hocKyCu = await db.HocKys
                    .Include(h => h.DanhSachMonHoc)
                    .ThenInclude(m => m.DanhSachTask)
                    .FirstOrDefaultAsync(h => h.MaHocKy == hocKy.MaHocKy, ct);

                if (hocKyCu == null)
                {
                    db.HocKys.Add(hocKy);
                }
                else
                {
                    CopySyncSafeValues(db.Entry(hocKyCu), hocKy);

                    var newMonById = hocKy.DanhSachMonHoc.ToDictionary(m => m.MaMonHoc);
                    var oldMonList = hocKyCu.DanhSachMonHoc.ToList();

                    foreach (var oldMon in oldMonList)
                    {
                        if (newMonById.ContainsKey(oldMon.MaMonHoc)) continue;

                        // MonHoc removed by the user -> cascade its tasks' Note/Links explicitly
                        // (FK-only relationship, not reachable via Include -> EF's own cascade
                        // fixup can't see them), then remove the MonHoc itself (EF fixup already
                        // tombstones its loaded StudyTask children via the Include chain above).
                        foreach (var oldTask in oldMon.DanhSachTask.ToList())
                            await TaskCascadeHelper.RemoveChildrenAsync(db, oldTask.MaTask, ct);
                        hocKyCu.DanhSachMonHoc.Remove(oldMon);
                        db.MonHocs.Remove(oldMon);
                    }

                    foreach (var newMon in hocKy.DanhSachMonHoc)
                    {
                        var oldMon = oldMonList.FirstOrDefault(m => m.MaMonHoc == newMon.MaMonHoc);
                        if (oldMon == null)
                        {
                            hocKyCu.DanhSachMonHoc.Add(newMon);
                            db.MonHocs.Add(newMon);
                            continue;
                        }

                        CopySyncSafeValues(db.Entry(oldMon), newMon);

                        var newTaskById = newMon.DanhSachTask.ToDictionary(t => t.MaTask);
                        var oldTaskList = oldMon.DanhSachTask.ToList();

                        foreach (var oldTask in oldTaskList)
                        {
                            if (newTaskById.ContainsKey(oldTask.MaTask)) continue;

                            await TaskCascadeHelper.RemoveChildrenAsync(db, oldTask.MaTask, ct);
                            oldMon.DanhSachTask.Remove(oldTask);
                            db.StudyTasks.Remove(oldTask);
                        }

                        foreach (var newTask in newMon.DanhSachTask)
                        {
                            var oldTask = oldTaskList.FirstOrDefault(t => t.MaTask == newTask.MaTask);
                            if (oldTask == null)
                            {
                                oldMon.DanhSachTask.Add(newTask);
                                db.StudyTasks.Add(newTask);
                            }
                            else
                            {
                                CopySyncSafeValues(db.Entry(oldTask), newTask);
                            }
                        }
                    }
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        // Copies scalar fields from a detached "new" POCO onto a tracked entity, without letting
        // the incoming object's stale/default ISyncMetadata values (it never touches Rev etc. --
        // that's seam-owned) stomp the tracked entity's real sync state.
        private static void CopySyncSafeValues<T>(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> entry, T source) where T : class
        {
            ISyncMetadata? previous = entry.Entity as ISyncMetadata;
            var snapshot = previous is null
                ? default
                : (previous.Rev, previous.ModifiedAtUtc, previous.ModifiedByDeviceId, previous.IsDeleted, previous.DeletedAtUtc);

            entry.CurrentValues.SetValues(source);

            if (previous is not null)
            {
                previous.Rev = snapshot.Rev;
                previous.ModifiedAtUtc = snapshot.ModifiedAtUtc;
                previous.ModifiedByDeviceId = snapshot.ModifiedByDeviceId;
                previous.IsDeleted = snapshot.IsDeleted;
                previous.DeletedAtUtc = snapshot.DeletedAtUtc;
            }
        }
    }
}
