using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Fence;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 5 -- Phase 1 skeleton (tests first). Implemented in Phase 2.
    internal static class TaskEditorPlanner
    {
        public static TaskEditorPlan PlanUpsertNote(Guid taskId, TaskNote? liveNote, string? content) =>
            throw new NotImplementedException();

        public static TaskEditorPlan PlanAddLink(TaskReferenceLink link) =>
            throw new NotImplementedException();

        public static TaskEditorPlan PlanUpdateLink(TaskReferenceLink? liveTarget, TaskReferenceLink incoming) =>
            throw new NotImplementedException();

        public static TaskEditorPlan PlanDeleteLink(TaskReferenceLink? found) =>
            throw new NotImplementedException();

        public static TaskEditorPlan PlanSave(Guid taskId, TaskNote? liveNote, string? content,
            IReadOnlyList<TaskReferenceLink> liveLinks, IReadOnlyList<TaskReferenceLink> incoming) =>
            throw new NotImplementedException();
    }

    internal sealed record NoteContentEdit(TaskNote Tracked, string? Content);

    internal sealed record LinkFieldEdit(TaskReferenceLink Tracked, TaskReferenceLink Incoming);

    internal sealed class TaskEditorPlan
    {
        public TaskNote? NoteAdd { get; init; }
        public NoteContentEdit? NoteEdit { get; init; }
        public IReadOnlyList<TaskReferenceLink> LinkDeletes { get; init; } = Array.Empty<TaskReferenceLink>();
        public IReadOnlyList<LinkFieldEdit> LinkEdits { get; init; } = Array.Empty<LinkFieldEdit>();
        public IReadOnlyList<TaskReferenceLink> LinkAdds { get; init; } = Array.Empty<TaskReferenceLink>();
        public string? ValidationError { get; init; }
        public MutationRequest Request { get; init; } = new(MutationOrigin.LocalApplication, Array.Empty<MutationIntent>());
    }
}
