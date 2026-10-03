using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 4 — OD-7 restoration, mechanism R2 (owner ruling 2026-10-03): after a
    // fence rejection, bring the CALLER's in-memory graph back to persisted state, in place.
    //
    // Why in place: the VMs share one HocKy instance (MainWindow builds every sidebar page from it,
    // pages are navigated by instance, QuanLyTaskViewModel also holds a child MonHoc). Replacing the
    // graph in one VM would leave the others on the rejected graph, and their next save would re-derive
    // the rejected intent (sticky rejection, X-20). So every HocKy/MonHoc/StudyTask instance the caller
    // already holds is kept and re-synced; only rows the caller no longer has get a new instance.
    //
    // Source of truth: the executor's old graph, loaded live-only in the same transaction the fence
    // read, before the writer could touch it -- "persisted state" exactly, and with OQ-1 ruled (i) it is
    // the RAW live rows, not LayDanhSachHocKyAsync's de-duplicated view (a folded MonHoc clone reappears
    // until the next load; pinned as an OBSERVED test).
    //
    // What is copied: every EF-mapped scalar (read from the model, so [NotMapped] UI state such as
    // HocKy.NgayKetThuc survives), including sync metadata and the Derived columns, which therefore
    // equal the database again. Membership: unsaved additions dropped, removed rows re-added at their
    // persisted position, moved tasks put back; items the caller kept keep the caller's order.
    //
    // It never touches the persisted graph's instances (they are tracked by the executor's context, and
    // putting them into the caller's graph would make any later DetectChanges see two instances with one
    // key), nor the change tracker. Not repainted by this: scalar edits on the POCOs (no INotifyPropertyChanged)
    // until the VM refreshes; derived displays until the next navigation -- the status quo after any
    // failed save.
    //
    // OD-2 ("the fence must not become a repository-side hidden policy"): this decides nothing. It never
    // reads the decision, runs identically for every rejection, and only keeps a caller-owned cache
    // coherent with the database -- the same class of side effect as the writer's FK heal and its
    // collection rewrites on a successful save.
    internal static class SemesterGraphRestorer
    {
        public static void Restore(IModel model, HocKy? persisted, HocKy caller)
        {
            if (persisted is null)
            {
                // OQ-3 (engineering decision, not ruled): a never-persisted HocKy's persisted state has no
                // MonHoc. Its own scalars have no row to be copied from and are left as typed.
                Rebuild(caller.DanhSachMonHoc, Array.Empty<MonHoc>());
                return;
            }

            var hocKyScalars = MappedScalars(model, typeof(HocKy));
            var monHocScalars = MappedScalars(model, typeof(MonHoc));
            var taskScalars = MappedScalars(model, typeof(StudyTask));

            // First instance wins on a duplicate id: a save that is both rejected and carries duplicate
            // ids must restore, not crash; the duplicate is dropped by Rebuild as not wanted.
            var callerMons = new Dictionary<Guid, MonHoc>();
            var callerTasks = new Dictionary<Guid, StudyTask>();
            foreach (var mon in caller.DanhSachMonHoc)
            {
                callerMons.TryAdd(mon.MaMonHoc, mon);
                foreach (var task in mon.DanhSachTask)
                    callerTasks.TryAdd(task.MaTask, task);
            }

            Copy(hocKyScalars, persisted, caller);

            var wantedMons = new List<MonHoc>();
            var wantedTasks = new List<(MonHoc Owner, List<StudyTask> Tasks)>();
            foreach (var persistedMon in persisted.DanhSachMonHoc)
            {
                var mon = callerMons.TryGetValue(persistedMon.MaMonHoc, out var kept) ? kept : new MonHoc();
                Copy(monHocScalars, persistedMon, mon);

                var tasks = new List<StudyTask>();
                foreach (var persistedTask in persistedMon.DanhSachTask)
                {
                    var task = callerTasks.TryGetValue(persistedTask.MaTask, out var keptTask) ? keptTask : new StudyTask();
                    Copy(taskScalars, persistedTask, task);
                    tasks.Add(task);
                }

                wantedMons.Add(mon);
                wantedTasks.Add((mon, tasks));
            }

            foreach (var (owner, tasks) in wantedTasks)
                Rebuild(owner.DanhSachTask, tasks);
            Rebuild(caller.DanhSachMonHoc, wantedMons);
        }

        private static PropertyInfo[] MappedScalars(IModel model, Type clrType) =>
            (model.FindEntityType(clrType) ?? throw new InvalidOperationException($"{clrType.Name} is not in the EF model."))
                .GetProperties()
                .Where(p => p.PropertyInfo is not null && !p.IsShadowProperty())
                .Select(p => p.PropertyInfo!)
                .ToArray();

        private static void Copy(PropertyInfo[] scalars, object source, object target)
        {
            foreach (var property in scalars)
                property.SetValue(target, property.GetValue(source));
        }

        // Minimal edits on the (UI-bound) collection: remove what is not wanted or is a repeat, then
        // insert each missing wanted item at its wanted index. Kept items keep their relative order.
        private static void Rebuild<T>(ObservableCollection<T> target, IReadOnlyList<T> wanted) where T : class
        {
            var wantedSet = new HashSet<T>(wanted, ReferenceEqualityComparer.Instance);
            var present = new HashSet<T>(ReferenceEqualityComparer.Instance);

            for (var i = 0; i < target.Count;)
            {
                var item = target[i];
                if (wantedSet.Contains(item) && present.Add(item)) i++;
                else target.RemoveAt(i);
            }

            for (var i = 0; i < wanted.Count; i++)
            {
                if (present.Add(wanted[i]))
                    target.Insert(Math.Min(i, target.Count), wanted[i]);
            }
        }
    }
}
