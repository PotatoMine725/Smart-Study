using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 3: the diff half of what used to be SqliteHocKyRepository.LuuHocKyAsync.
    // Pure: no AppDbContext, no I/O, and it mutates neither graph -- it only reads the loaded old graph
    // and the incoming graph and says what SemesterGraphWriter is about to do. Slice 4 puts the fence
    // between this and the writer.
    internal static class SemesterReconcilePlanner
    {
        public static SemesterReconcilePlan Plan(HocKy? oldGraph, HocKy incoming)
        {
            if (oldGraph == null) return SemesterReconcilePlan.Create();

            var errors = new List<PlanValidationError>();

            var newMonIds = new HashSet<Guid>();
            foreach (var mon in incoming.DanhSachMonHoc)
            {
                if (newMonIds.Add(mon.MaMonHoc)) continue;

                errors.Add(new PlanValidationError(PlanValidationKind.DuplicateMonHocId, PlanStage.BeforeFkHeal,
                    null, mon.MaMonHoc, DuplicateKeyMessage(mon.MaMonHoc)));
                return new SemesterReconcilePlan { ValidationErrors = errors };
            }

            // Reopen fix 2026-07: a task can arrive having entered the graph only through a
            // navigation collection, with MaMonHoc never stamped (Guid.Empty). Its navigation
            // position is authoritative in that case -- the same semantics EF graph fixup gave the
            // pre-M1.2 save. The planner diffs on that effective FK and leaves writing it onto the
            // caller's object to the writer, so the incoming graph changes exactly where it did before.
            var fkHeals = new List<FkHeal>();
            var healedOwner = new Dictionary<StudyTask, Guid>(ReferenceEqualityComparer.Instance);
            foreach (var mon in incoming.DanhSachMonHoc)
                foreach (var t in mon.DanhSachTask)
                    if (t.MaMonHoc == Guid.Empty && healedOwner.TryAdd(t, mon.MaMonHoc))
                        fkHeals.Add(new FkHeal(t, mon.MaMonHoc));

            Guid EffectiveOwner(StudyTask t) => healedOwner.TryGetValue(t, out var owner) ? owner : t.MaMonHoc;

            var newTasks = new List<StudyTask>();
            var newTaskIds = new HashSet<Guid>();
            foreach (var t in incoming.DanhSachMonHoc.SelectMany(m => m.DanhSachTask))
            {
                if (newTaskIds.Add(t.MaTask))
                {
                    newTasks.Add(t);
                    continue;
                }

                errors.Add(new PlanValidationError(PlanValidationKind.DuplicateTaskId, PlanStage.AfterFkHeal,
                    t.MaTask, null, DuplicateKeyMessage(t.MaTask)));
                return new SemesterReconcilePlan { FkHeals = fkHeals, ValidationErrors = errors };
            }

            var oldMonList = oldGraph.DanhSachMonHoc.ToList();
            var oldMonIds = oldMonList.Select(m => m.MaMonHoc).ToHashSet();

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
            var oldTasks = oldMonList.SelectMany(m => m.DanhSachTask).ToList();
            var oldTaskIds = oldTasks.Select(t => t.MaTask).ToHashSet();
            var newTasksByMaTask = newTasks.ToDictionary(t => t.MaTask);

            var reparents = new List<TaskReparent>();
            var taskDeletes = new List<Guid>();
            foreach (var oldTask in oldTasks)
            {
                if (!newTasksByMaTask.TryGetValue(oldTask.MaTask, out var newTask))
                {
                    taskDeletes.Add(oldTask.MaTask);
                    continue;
                }

                var newOwner = EffectiveOwner(newTask);
                if (oldTask.MaMonHoc != newOwner)
                    reparents.Add(new TaskReparent(oldTask.MaTask, oldTask.MaMonHoc, newOwner));
            }

            var upserts = new List<TaskUpsert>();
            foreach (var newTask in newTasks)
            {
                var owner = EffectiveOwner(newTask);
                upserts.Add(new TaskUpsert(newTask.MaTask, owner, !oldTaskIds.Contains(newTask.MaTask)));

                if (!newMonIds.Contains(owner))
                    errors.Add(new PlanValidationError(PlanValidationKind.UnknownMonHoc, PlanStage.TaskUpsert,
                        newTask.MaTask, owner, UnknownMonHocMessage(newTask.TenTask, newTask.MaTask, owner, oldGraph.MaHocKy)));
            }

            return new SemesterReconcilePlan
            {
                FkHeals = fkHeals,
                TaskReparents = reparents,
                MonHocDeletes = oldMonList.Where(m => !newMonIds.Contains(m.MaMonHoc)).Select(m => m.MaMonHoc).ToList(),
                MonHocAdds = incoming.DanhSachMonHoc.Where(m => !oldMonIds.Contains(m.MaMonHoc)).Select(m => m.MaMonHoc).ToList(),
                MonHocUpdates = incoming.DanhSachMonHoc.Where(m => oldMonIds.Contains(m.MaMonHoc)).Select(m => m.MaMonHoc).ToList(),
                TaskDeletes = taskDeletes,
                TaskUpserts = upserts,
                ValidationErrors = errors,
            };
        }

        internal static string UnknownMonHocMessage(string tenTask, Guid maTask, Guid maMonHoc, Guid maHocKy) =>
            $"Reconcile: task '{tenTask}' ({maTask}) references MonHoc {maMonHoc} not present in HocKy {maHocKy}.";

        private static string DuplicateKeyMessage(Guid key) =>
            $"An item with the same key has already been added. Key: {key}";
    }

    internal sealed record FkHeal(StudyTask Task, Guid Owner);

    internal sealed record TaskReparent(Guid MaTask, Guid FromMon, Guid ToMon);

    internal sealed record TaskUpsert(Guid MaTask, Guid Owner, bool IsNew);

    internal enum PlanValidationKind { DuplicateMonHocId, DuplicateTaskId, UnknownMonHoc }

    // Where in the writer's statement order the failure surfaces. The pre-extraction save threw at
    // these points, after whatever it had already done to the caller's graph; the writer throws at
    // the same ones so that state is unchanged.
    internal enum PlanStage { BeforeFkHeal, AfterFkHeal, TaskUpsert }

    internal sealed record PlanValidationError(PlanValidationKind Kind, PlanStage Stage, Guid? MaTask, Guid? MaMonHoc, string Message);

    // Every list is in the order the writer walks it: old-graph order for reparents/deletes,
    // incoming-graph order for heals, MonHoc adds/updates and task upserts.
    internal sealed class SemesterReconcilePlan
    {
        // hocKyCu == null: the writer attaches the whole incoming graph and nothing else applies.
        public bool IsCreate { get; init; }
        public IReadOnlyList<FkHeal> FkHeals { get; init; } = Array.Empty<FkHeal>();
        public IReadOnlyList<TaskReparent> TaskReparents { get; init; } = Array.Empty<TaskReparent>();
        public IReadOnlyList<Guid> MonHocDeletes { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> MonHocAdds { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> MonHocUpdates { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<Guid> TaskDeletes { get; init; } = Array.Empty<Guid>();
        public IReadOnlyList<TaskUpsert> TaskUpserts { get; init; } = Array.Empty<TaskUpsert>();
        public IReadOnlyList<PlanValidationError> ValidationErrors { get; init; } = Array.Empty<PlanValidationError>();

        public static SemesterReconcilePlan Create() => new() { IsCreate = true };
    }
}
