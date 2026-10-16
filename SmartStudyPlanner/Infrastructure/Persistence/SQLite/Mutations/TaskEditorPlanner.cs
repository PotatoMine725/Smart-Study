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
    // Epic 2 / T2.4 fence Slice 5: the diff half of the task editor's writes (plan §6 intent mapping).
    // Pure: no AppDbContext, no I/O. It reads the live rows the executor loaded (tracked) and the caller's
    // input, and says what TaskEditorWriter is about to do -- as a plan for the writer and as a
    // MutationRequest for the fence -- so the fence judges exactly the rows that are written.
    //
    // Live-only (M-3/A-1): a re-stamp of a dead row is not a semantic effect, so a dead target gets no
    // intent. DeleteLinkAsync keeps writing it anyway (D-7, filed 2026-10-16, not fixed here); PlanSave's
    // diff never sees dead rows at all.
    internal static class TaskEditorPlanner
    {
        /// <summary>
        /// No live note: Create (with the K(T) scope relation), whatever the content -- a null-content
        /// note too, as the pre-Slice-5 UpsertNoteAsync did. Live note with other content: UpdateFields.
        /// Same content: nothing.
        /// </summary>
        public static TaskEditorPlan PlanUpsertNote(Guid taskId, TaskNote? liveNote, string? content)
        {
            var plan = new Builder();
            plan.Note(taskId, liveNote, content);
            return plan.Build();
        }

        public static TaskEditorPlan PlanAddLink(TaskReferenceLink link)
        {
            var plan = new Builder();
            plan.Add(link);
            return plan.Build();
        }

        /// <summary>
        /// <paramref name="liveTarget"/> null (missing or tombstoned): no intent, and a business-validation
        /// error the executor raises after a passed fence, with UpdateLinkAsync's message.
        /// </summary>
        public static TaskEditorPlan PlanUpdateLink(TaskReferenceLink? liveTarget, TaskReferenceLink incoming)
        {
            if (liveTarget is null)
                return new TaskEditorPlan { ValidationError = MissingLinkMessage(incoming.Id) };

            var plan = new Builder();
            plan.Edit(liveTarget, incoming);
            return plan.Build();
        }

        /// <summary>
        /// <paramref name="found"/> is the row with that id, live or not (DeleteLinkAsync's unfiltered
        /// lookup). Missing: nothing. Dead: no intent, but the write stays (D-7, behaviour unchanged).
        /// </summary>
        public static TaskEditorPlan PlanDeleteLink(TaskReferenceLink? found)
        {
            if (found is null) return new TaskEditorPlan();

            var plan = new Builder();
            plan.Delete(found);
            return plan.Build();
        }

        /// <summary>
        /// E2 (owner ruling 2026-10-16): the note plus the whole link set as ONE request, in the order of
        /// the port's reference sequence -- note, deletes of live links <paramref name="incoming"/> does
        /// not name, then per incoming link an edit (live link of this task) or an add.
        /// <para>
        /// Unlike <see cref="PlanUpsertNote"/>: no live note and null/empty content ⇒ no note at all, so a
        /// links-only edit does not acquire K(T) (owner ruling 2026-10-16, Q2). A live note edited to
        /// empty is still an UpdateFields.
        /// </para>
        /// </summary>
        public static TaskEditorPlan PlanSave(Guid taskId, TaskNote? liveNote, string? content,
            IReadOnlyList<TaskReferenceLink> liveLinks, IReadOnlyList<TaskReferenceLink> incoming)
        {
            var plan = new Builder();
            if (liveNote is not null || !string.IsNullOrEmpty(content))
                plan.Note(taskId, liveNote, content);

            var incomingIds = incoming.Select(l => l.Id).ToHashSet();
            foreach (var dead in liveLinks.Where(l => !incomingIds.Contains(l.Id)))
                plan.Delete(dead);

            var liveById = liveLinks.ToDictionary(l => l.Id);
            foreach (var link in incoming)
            {
                if (liveById.TryGetValue(link.Id, out var live))
                    plan.Edit(live, link);
                else
                    plan.Add(link);
            }

            return plan.Build();
        }

        internal static string MissingLinkMessage(Guid linkId) =>
            $"TaskReferenceLink {linkId} không tồn tại (hoặc đã bị xoá) — không thể cập nhật.";

        private const string MaTask = "MaTask";

        private static readonly PropertyInfo[] TaskNoteMergeFields = MergeFieldsOf(SyncEntityTypes.TaskNote, typeof(TaskNote));
        private static readonly PropertyInfo[] LinkMergeFields = MergeFieldsOf(SyncEntityTypes.TaskReferenceLink, typeof(TaskReferenceLink));

        // Ordinal by name, so ChangedFields is deterministic whatever the registry's canonical order.
        // For a link these are exactly the fields UpdateLinkAsync has always copied (Title, Url,
        // Category, SortOrder); for a note, Content.
        private static PropertyInfo[] MergeFieldsOf(string entityType, Type clrType) =>
            MergeSurfaceRegistry.Get(entityType).Fields
                .Where(f => f.Class == FieldClass.Merge)
                .Select(f => clrType.GetProperty(f.Name)
                    ?? throw new InvalidOperationException($"MergeSurfaceRegistry names {entityType}.{f.Name}, which {clrType.Name} does not have."))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

        private static string[] ChangedFields(PropertyInfo[] fields, object current, object incoming) =>
            fields.Where(p => !Equals(p.GetValue(current), p.GetValue(incoming))).Select(p => p.Name).ToArray();

        private static MutationIntent Intent(MutationOperation operation, string entityType, Guid id,
            IReadOnlyList<RelationChange> relations, IReadOnlyList<string> changedFields) =>
            new(operation, entityType, id, relations, changedFields);

        private sealed class Builder
        {
            private TaskNote? _noteAdd;
            private NoteContentEdit? _noteEdit;
            private readonly List<TaskReferenceLink> _deletes = new();
            private readonly List<LinkFieldEdit> _edits = new();
            private readonly List<TaskReferenceLink> _adds = new();
            private readonly List<MutationIntent> _intents = new();

            public void Note(Guid taskId, TaskNote? liveNote, string? content)
            {
                if (liveNote is null)
                {
                    // The id is fixed here, so the intent names the row the writer inserts.
                    _noteAdd = new TaskNote { Id = Guid.NewGuid(), MaTask = taskId, Content = content };
                    _intents.Add(Intent(MutationOperation.Create, SyncEntityTypes.TaskNote, _noteAdd.Id,
                        new[] { new RelationChange(MaTask, null, taskId) }, Array.Empty<string>()));
                    return;
                }

                var changed = ChangedFields(TaskNoteMergeFields, liveNote, new TaskNote { Content = content });
                if (changed.Length == 0) return;

                _noteEdit = new NoteContentEdit(liveNote, content);
                _intents.Add(Intent(MutationOperation.UpdateFields, SyncEntityTypes.TaskNote, liveNote.Id,
                    Array.Empty<RelationChange>(), changed));
            }

            public void Add(TaskReferenceLink link)
            {
                _adds.Add(link);
                _intents.Add(Intent(MutationOperation.Create, SyncEntityTypes.TaskReferenceLink, link.Id,
                    new[] { new RelationChange(MaTask, null, link.MaTask) }, Array.Empty<string>()));
            }

            // Only the editable fields count; the incoming POCO's Id/MaTask/Rev/CreatedAtUtc are not
            // authoritative (UpdateLinkAsync's contract), so a link edit never moves the link.
            public void Edit(TaskReferenceLink live, TaskReferenceLink incoming)
            {
                var changed = ChangedFields(LinkMergeFields, live, incoming);
                if (changed.Length == 0) return;

                _edits.Add(new LinkFieldEdit(live, incoming));
                _intents.Add(Intent(MutationOperation.UpdateFields, SyncEntityTypes.TaskReferenceLink, live.Id,
                    Array.Empty<RelationChange>(), changed));
            }

            public void Delete(TaskReferenceLink found)
            {
                _deletes.Add(found);
                if (!found.IsDeleted)
                    _intents.Add(Intent(MutationOperation.Tombstone, SyncEntityTypes.TaskReferenceLink, found.Id,
                        Array.Empty<RelationChange>(), Array.Empty<string>()));
            }

            public TaskEditorPlan Build() => new()
            {
                NoteAdd = _noteAdd,
                NoteEdit = _noteEdit,
                LinkDeletes = _deletes,
                LinkEdits = _edits,
                LinkAdds = _adds,
                Request = new MutationRequest(MutationOrigin.LocalApplication, _intents),
            };
        }
    }

    internal sealed record NoteContentEdit(TaskNote Tracked, string? Content);

    internal sealed record LinkFieldEdit(TaskReferenceLink Tracked, TaskReferenceLink Incoming);

    // Every list is in the order the writer walks it.
    internal sealed class TaskEditorPlan
    {
        public TaskNote? NoteAdd { get; init; }
        public NoteContentEdit? NoteEdit { get; init; }
        public IReadOnlyList<TaskReferenceLink> LinkDeletes { get; init; } = Array.Empty<TaskReferenceLink>();
        public IReadOnlyList<LinkFieldEdit> LinkEdits { get; init; } = Array.Empty<LinkFieldEdit>();
        public IReadOnlyList<TaskReferenceLink> LinkAdds { get; init; } = Array.Empty<TaskReferenceLink>();

        // Raised by the executor AFTER a passed fence (plan §5 gate order); the writer never sees it.
        public string? ValidationError { get; init; }

        public MutationRequest Request { get; init; } = new(MutationOrigin.LocalApplication, Array.Empty<MutationIntent>());
    }
}
