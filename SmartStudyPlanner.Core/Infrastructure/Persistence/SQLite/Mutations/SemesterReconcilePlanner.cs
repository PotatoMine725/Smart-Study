using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 3: the diff half of what used to be SqliteHocKyRepository.LuuHocKyAsync.
    // Pure: no AppDbContext, no I/O, and it mutates neither graph -- it only reads the loaded old graph
    // and the incoming graph and says what SemesterGraphWriter is about to do. Slice 4: it also says
    // it as a MutationRequest (plan §6), which the executor routes through the fence before the writer.
    internal static class SemesterReconcilePlanner
    {
        public static SemesterReconcilePlan Plan(HocKy? oldGraph, HocKy incoming)
        {
            if (oldGraph == null)
                return new SemesterReconcilePlan { IsCreate = true, Request = LocalRequest(CreateIntents(incoming)) };

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

            var monHocDeletes = oldMonList.Where(m => !newMonIds.Contains(m.MaMonHoc)).Select(m => m.MaMonHoc).ToList();

            return new SemesterReconcilePlan
            {
                FkHeals = fkHeals,
                TaskReparents = reparents,
                MonHocDeletes = monHocDeletes,
                MonHocAdds = incoming.DanhSachMonHoc.Where(m => !oldMonIds.Contains(m.MaMonHoc)).Select(m => m.MaMonHoc).ToList(),
                MonHocUpdates = incoming.DanhSachMonHoc.Where(m => oldMonIds.Contains(m.MaMonHoc)).Select(m => m.MaMonHoc).ToList(),
                TaskDeletes = taskDeletes,
                TaskUpserts = upserts,
                ValidationErrors = errors,
                Request = LocalRequest(ReconcileIntents(oldGraph, oldMonList, oldTasks, incoming, monHocDeletes, reparents, taskDeletes, upserts, newTasksByMaTask)),
            };
        }

        // ---------------------------------------------------------------- fence request (Slice 4)

        // Plan §6 intent table, read off the same reconcile data the writer walks. Every structural
        // write the writer will make is in the request (Reparent of a MonHoc's MaHocKy included, which
        // the writer copies verbatim though no VM does it today); field edits are UpdateFields over
        // MergeSurfaceRegistry Merge-class fields ONLY, so a Derived (DiemUuTien, MucDoCanhBao,
        // IsSeeded) or NotMapped change emits nothing. Early-return validation errors emit an empty
        // request: the writer throws at its original stage before touching anything that matters.
        private static List<MutationIntent> ReconcileIntents(
            HocKy oldGraph, List<MonHoc> oldMonList, List<StudyTask> oldTasks, HocKy incoming,
            List<Guid> monHocDeletes, List<TaskReparent> reparents, List<Guid> taskDeletes,
            List<TaskUpsert> upserts, Dictionary<Guid, StudyTask> newTasksByMaTask)
        {
            var intents = new List<MutationIntent>();
            var oldMonById = oldMonList.ToDictionary(m => m.MaMonHoc);
            var oldTaskById = oldTasks.ToDictionary(t => t.MaTask);

            AddFieldUpdate(intents, SyncEntityTypes.HocKy, oldGraph.MaHocKy, HocKyMergeFields, oldGraph, incoming);

            foreach (var maMonHoc in monHocDeletes)
                intents.Add(Intent(MutationOperation.Tombstone, SyncEntityTypes.MonHoc, maMonHoc));

            foreach (var newMon in incoming.DanhSachMonHoc)
            {
                if (!oldMonById.TryGetValue(newMon.MaMonHoc, out var oldMon))
                {
                    // The writer adds it to the loaded HocKy's collection; EF fixup sets MaHocKy to it.
                    intents.Add(Intent(MutationOperation.Create, SyncEntityTypes.MonHoc, newMon.MaMonHoc,
                        new RelationChange(MaHocKy, null, oldGraph.MaHocKy)));
                    continue;
                }

                if (oldMon.MaHocKy != newMon.MaHocKy)
                    intents.Add(Intent(MutationOperation.Reparent, SyncEntityTypes.MonHoc, newMon.MaMonHoc,
                        new RelationChange(MaHocKy, oldMon.MaHocKy, newMon.MaHocKy)));

                AddFieldUpdate(intents, SyncEntityTypes.MonHoc, newMon.MaMonHoc, MonHocMergeFields, oldMon, newMon);
            }

            foreach (var reparent in reparents)
                intents.Add(Intent(MutationOperation.Reparent, SyncEntityTypes.StudyTask, reparent.MaTask,
                    new RelationChange(MaMonHoc, reparent.FromMon, reparent.ToMon)));

            // A task removed together with its MonHoc gets no intent of its own: Tombstone(MonHoc)
            // reaches it through ImpactResolver's live-only cascade. A direct Tombstone would outrank
            // that (RowEffect precedence) and report the row @DirectSubject instead of @CascadeReached.
            var deletedMonIds = monHocDeletes.ToHashSet();
            foreach (var maTask in taskDeletes)
            {
                if (!deletedMonIds.Contains(oldTaskById[maTask].MaMonHoc))
                    intents.Add(Intent(MutationOperation.Tombstone, SyncEntityTypes.StudyTask, maTask));
            }

            foreach (var upsert in upserts)
            {
                if (upsert.IsNew)
                {
                    intents.Add(Intent(MutationOperation.Create, SyncEntityTypes.StudyTask, upsert.MaTask,
                        new RelationChange(MaMonHoc, null, upsert.Owner)));
                    continue;
                }

                AddFieldUpdate(intents, SyncEntityTypes.StudyTask, upsert.MaTask, StudyTaskMergeFields,
                    oldTaskById[upsert.MaTask], newTasksByMaTask[upsert.MaTask]);
            }

            return intents;
        }

        // hocKyCu == null: the writer attaches the whole incoming graph, and EF's fixup gives every
        // MonHoc/task its navigation owner's key, whatever FK the caller stamped.
        private static List<MutationIntent> CreateIntents(HocKy incoming)
        {
            var intents = new List<MutationIntent> { Intent(MutationOperation.Create, SyncEntityTypes.HocKy, incoming.MaHocKy) };
            foreach (var mon in incoming.DanhSachMonHoc)
            {
                intents.Add(Intent(MutationOperation.Create, SyncEntityTypes.MonHoc, mon.MaMonHoc,
                    new RelationChange(MaHocKy, null, incoming.MaHocKy)));
                foreach (var task in mon.DanhSachTask)
                    intents.Add(Intent(MutationOperation.Create, SyncEntityTypes.StudyTask, task.MaTask,
                        new RelationChange(MaMonHoc, null, mon.MaMonHoc)));
            }
            return intents;
        }

        private const string MaHocKy = "MaHocKy";
        private const string MaMonHoc = "MaMonHoc";

        private static readonly PropertyInfo[] HocKyMergeFields = MergeFieldsOf(SyncEntityTypes.HocKy, typeof(HocKy));
        private static readonly PropertyInfo[] MonHocMergeFields = MergeFieldsOf(SyncEntityTypes.MonHoc, typeof(MonHoc));
        private static readonly PropertyInfo[] StudyTaskMergeFields = MergeFieldsOf(SyncEntityTypes.StudyTask, typeof(StudyTask));

        // Ordinal by name, so ChangedFields is deterministic whatever the registry's canonical order.
        private static PropertyInfo[] MergeFieldsOf(string entityType, Type clrType) =>
            MergeSurfaceRegistry.Get(entityType).Fields
                .Where(f => f.Class == FieldClass.Merge)
                .Select(f => clrType.GetProperty(f.Name)
                    ?? throw new InvalidOperationException($"MergeSurfaceRegistry names {entityType}.{f.Name}, which {clrType.Name} does not have."))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

        // Value equality on the boxed values, the same notion EF's default change detection uses for
        // these scalar types (DateTime by ticks, string ordinal, enum by value).
        private static void AddFieldUpdate(List<MutationIntent> intents, string entityType, Guid id,
            PropertyInfo[] mergeFields, object oldRow, object newRow)
        {
            var changed = mergeFields
                .Where(p => !Equals(p.GetValue(oldRow), p.GetValue(newRow)))
                .Select(p => p.Name)
                .ToArray();

            if (changed.Length > 0)
                intents.Add(new MutationIntent(MutationOperation.UpdateFields, entityType, id, Array.Empty<RelationChange>(), changed));
        }

        private static MutationIntent Intent(MutationOperation operation, string entityType, Guid id, params RelationChange[] relations) =>
            new(operation, entityType, id, relations, Array.Empty<string>());

        private static MutationRequest LocalRequest(IReadOnlyList<MutationIntent> intents) =>
            new(MutationOrigin.LocalApplication, intents);

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

        public MutationRequest Request { get; init; } = new(MutationOrigin.LocalApplication, Array.Empty<MutationIntent>());

        public static SemesterReconcilePlan Create() => new() { IsCreate = true };
    }
}
