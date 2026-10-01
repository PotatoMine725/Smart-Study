using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 3: the tracker-write half of what used to be
    // SqliteHocKyRepository.LuuHocKyAsync, in the same statement order. It decides nothing -- what to
    // heal, reparent, remove and add comes from the plan -- and it does not SaveChanges (the executor
    // does). The two checks that depend on EF's fixup state (the owner lookup and the Contains guard
    // in the task loop) stay live here rather than being predicted by the planner.
    internal static class SemesterGraphWriter
    {
        public static async Task ApplyAsync(AppDbContext db, HocKy? hocKyCu, HocKy hocKy, SemesterReconcilePlan plan, CancellationToken ct)
        {
            // Epic 1 / M1.2 (G1): reconciled in place instead of remove-then-recreate.
            // Deletes in this app are implicit — XoaTask/XoaMon drop the item from the in-memory
            // graph and re-save the whole thing — so a genuine delete only shows up here as
            // "row present in the old DB graph, absent from the new one." Diffing by Guid (stable
            // across saves — only `new StudyTask(...)`/`new MonHoc(...)` mint fresh ones) lets
            // unchanged rows keep their identity and Rev history instead of being torn down and
            // recreated on every save, which would collide on the primary key of every unchanged
            // row the moment these entities became tombstoned instead of hard-deleted (SyncStamper
            // converts Remove() to a soft IsDeleted update, so the old row is never actually gone).
            if (plan.IsCreate || hocKyCu == null)
            {
                db.HocKys.Add(hocKy);
                return;
            }

            CopySyncSafeValues(db.Entry(hocKyCu), hocKy);

            ThrowIfInvalidAt(plan, PlanStage.BeforeFkHeal);
            var oldMonList = hocKyCu.DanhSachMonHoc.ToList();

            // The FK heal is written onto the caller's own task objects, before the FK-keyed steps
            // below treat MaMonHoc as identity.
            foreach (var heal in plan.FkHeals)
                heal.Task.MaMonHoc = heal.Owner;

            ThrowIfInvalidAt(plan, PlanStage.AfterFkHeal);
            var oldTasksByMaTask = oldMonList.SelectMany(m => m.DanhSachTask).ToDictionary(t => t.MaTask);
            var newTasksByMaTask = hocKy.DanhSachMonHoc.SelectMany(m => m.DanhSachTask).ToDictionary(t => t.MaTask);

            // Reparent surviving tasks onto their new owner's FK *before* any MonHoc is
            // removed. EF's cascade-on-remove fixup resolves a doomed parent's dependents
            // at the moment Remove() is called below, using its own tracked relationship
            // snapshot -- not the live ObservableCollection -- so a task must already have
            // moved off that FK (and DetectChanges must have run) or it gets swept into
            // the cascade and wrongly tombstoned despite surviving under a new parent.
            foreach (var reparent in plan.TaskReparents)
            {
                var oldTask = oldTasksByMaTask[reparent.MaTask];
                var oldOwner = oldMonList.First(m => m.MaMonHoc == oldTask.MaMonHoc);
                oldOwner.DanhSachTask.Remove(oldTask);
                oldTask.MaMonHoc = reparent.ToMon;
            }
            db.ChangeTracker.DetectChanges();

            foreach (var maMonHoc in plan.MonHocDeletes)
            {
                // MonHoc removed by the user (or merged away as a losing dedup clone).
                // Whatever remains in its tracked navigation at this point is genuinely
                // gone (reparented survivors were already moved off above), so EF's own
                // cascade fixup correctly tombstones it; hocKyCu.DanhSachMonHoc.Remove
                // keeps the in-memory graph consistent with that.
                var oldMon = oldMonList.First(m => m.MaMonHoc == maMonHoc);
                hocKyCu.DanhSachMonHoc.Remove(oldMon);
                db.MonHocs.Remove(oldMon);
            }

            foreach (var newMon in hocKy.DanhSachMonHoc)
            {
                if (plan.MonHocAdds.Contains(newMon.MaMonHoc))
                {
                    // Its tasks are handled by the flat task diff below, not by cascading
                    // through this Add -- clear the incoming navigation so EF doesn't
                    // double-track them.
                    newMon.DanhSachTask = new ObservableCollection<StudyTask>();
                    hocKyCu.DanhSachMonHoc.Add(newMon);
                    db.MonHocs.Add(newMon);
                    continue;
                }

                var oldMon = oldMonList.First(m => m.MaMonHoc == newMon.MaMonHoc);
                CopySyncSafeValues(db.Entry(oldMon), newMon);
            }

            foreach (var maTask in plan.TaskDeletes)
            {
                var oldTask = oldTasksByMaTask[maTask];
                await TaskCascadeHelper.RemoveChildrenAsync(db, oldTask.MaTask, ct);
                db.StudyTasks.Remove(oldTask);
            }

            foreach (var upsert in plan.TaskUpserts)
            {
                var newTask = newTasksByMaTask[upsert.MaTask];
                var owner = hocKyCu.DanhSachMonHoc.FirstOrDefault(m => m.MaMonHoc == newTask.MaMonHoc)
                    ?? throw new InvalidOperationException(
                        SemesterReconcilePlanner.UnknownMonHocMessage(newTask.TenTask, newTask.MaTask, newTask.MaMonHoc, hocKyCu.MaHocKy));

                if (!upsert.IsNew)
                {
                    var oldTask = oldTasksByMaTask[upsert.MaTask];
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

        // Duplicate ids used to surface as ToDictionary's ArgumentException at these two points.
        private static void ThrowIfInvalidAt(SemesterReconcilePlan plan, PlanStage stage)
        {
            var error = plan.ValidationErrors.FirstOrDefault(e => e.Stage == stage);
            if (error != null) throw new ArgumentException(error.Message);
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
