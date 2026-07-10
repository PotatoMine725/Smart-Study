using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            // Khử trùng môn học clone: DB có thể chứa nhiều MonHoc cùng identity chuẩn hóa
            // (do seed/lưu lặp, hoặc chỉ khác hoa/thường/khoảng trắng) → giữ 1 bản đại diện
            // mỗi identity để UI + mọi consumer thấy danh sách sạch. Epic 1 / M1.3: key qua
            // MonHocIdentity.Normalize thay vì raw TenMonHoc — nhất quán pattern dedup toàn dự án.
            // GỘP task từ MỌI clone (SelectMany, distinct theo MaTask) vào bản đại diện trước
            // khi loại clone → KHÔNG mất task nằm ở clone không-đại-diện. Mỗi task gộp được
            // reparent (MaMonHoc = daiDien.MaMonHoc) để đồ thị trả về nhất quán FK-với-navigation —
            // LuuHocKyAsync's reconcile dựa vào đó để nhận diện đúng chủ sở hữu mới của task.
            // Lần LuuHocKyAsync kế tiếp ghi đè danh sách đã khử trùng → prune clone khỏi DB.
            foreach (var hocKy in danhSach)
            {
                var monDuyNhat = hocKy.DanhSachMonHoc
                    .GroupBy(mon => MonHocIdentity.Normalize(mon.TenMonHoc))
                    .Select(nhom =>
                    {
                        var daiDien = nhom.First();

                        var taskGop = nhom
                            .SelectMany(mon => mon.DanhSachTask)
                            .GroupBy(task => task.MaTask)
                            .Select(nhomTask => nhomTask.First())
                            .ToList();

                        foreach (var task in taskGop)
                            task.MaMonHoc = daiDien.MaMonHoc;

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

                    // Epic 1 / M1.3: task reconcile is scoped to the whole HocKy, not nested
                    // per-MonHoc parent. LayDanhSachHocKyAsync's identity-based dedup can merge
                    // two MonHoc clones and move a task from the losing clone into the surviving
                    // representative between load and save. A per-parent diff would see that
                    // task as "removed under its old parent" AND "new under the representative"
                    // -- two conflicting operations on the same StudyTask row in one
                    // SaveChanges, which EF rejects ("already being tracked"). Diffing every task
                    // under this HocKy by Guid up front -- before touching any MonHoc -- mirrors
                    // this same HocKy-level MonHoc diff and lets a reparented task reconcile onto
                    // its single existing tracked row instead of colliding with it.
                    var oldTasksByMaTask = oldMonList.SelectMany(m => m.DanhSachTask).ToDictionary(t => t.MaTask);
                    var newTasksByMaTask = hocKy.DanhSachMonHoc.SelectMany(m => m.DanhSachTask).ToDictionary(t => t.MaTask);

                    // Reparent surviving tasks onto their new owner's FK *before* any MonHoc is
                    // removed. EF's cascade-on-remove fixup resolves a doomed parent's dependents
                    // at the moment Remove() is called below, using its own tracked relationship
                    // snapshot -- not the live ObservableCollection -- so a task must already have
                    // moved off that FK (and DetectChanges must have run) or it gets swept into
                    // the cascade and wrongly tombstoned despite surviving under a new parent.
                    foreach (var oldTask in oldTasksByMaTask.Values)
                    {
                        if (!newTasksByMaTask.TryGetValue(oldTask.MaTask, out var newTask)) continue;
                        if (oldTask.MaMonHoc == newTask.MaMonHoc) continue;

                        var oldOwner = oldMonList.First(m => m.MaMonHoc == oldTask.MaMonHoc);
                        oldOwner.DanhSachTask.Remove(oldTask);
                        oldTask.MaMonHoc = newTask.MaMonHoc;
                    }
                    db.ChangeTracker.DetectChanges();

                    foreach (var oldMon in oldMonList)
                    {
                        if (newMonById.ContainsKey(oldMon.MaMonHoc)) continue;

                        // MonHoc removed by the user (or merged away as a losing dedup clone).
                        // Whatever remains in its tracked navigation at this point is genuinely
                        // gone (reparented survivors were already moved off above), so EF's own
                        // cascade fixup correctly tombstones it; hocKyCu.DanhSachMonHoc.Remove
                        // keeps the in-memory graph consistent with that.
                        hocKyCu.DanhSachMonHoc.Remove(oldMon);
                        db.MonHocs.Remove(oldMon);
                    }

                    foreach (var newMon in hocKy.DanhSachMonHoc)
                    {
                        var oldMon = oldMonList.FirstOrDefault(m => m.MaMonHoc == newMon.MaMonHoc);
                        if (oldMon == null)
                        {
                            // Its tasks are handled by the flat task diff below, not by cascading
                            // through this Add -- clear the incoming navigation so EF doesn't
                            // double-track them.
                            newMon.DanhSachTask = new ObservableCollection<StudyTask>();
                            hocKyCu.DanhSachMonHoc.Add(newMon);
                            db.MonHocs.Add(newMon);
                            continue;
                        }

                        CopySyncSafeValues(db.Entry(oldMon), newMon);
                    }

                    foreach (var oldTask in oldTasksByMaTask.Values)
                    {
                        if (newTasksByMaTask.ContainsKey(oldTask.MaTask)) continue;

                        await TaskCascadeHelper.RemoveChildrenAsync(db, oldTask.MaTask, ct);
                        db.StudyTasks.Remove(oldTask);
                    }

                    foreach (var newTask in newTasksByMaTask.Values)
                    {
                        var owner = hocKyCu.DanhSachMonHoc.First(m => m.MaMonHoc == newTask.MaMonHoc);

                        if (oldTasksByMaTask.TryGetValue(newTask.MaTask, out var oldTask))
                        {
                            CopySyncSafeValues(db.Entry(oldTask), newTask);
                            if (!owner.DanhSachTask.Contains(oldTask))
                                owner.DanhSachTask.Add(oldTask);
                        }
                        else
                        {
                            owner.DanhSachTask.Add(newTask);
                            db.StudyTasks.Add(newTask);
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
