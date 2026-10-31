using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 5: the write half of what used to be SqliteTaskEditorRepository's four
    // mutation bodies -- the same tracker operations, no decisions, no SaveChanges. Called only by
    // LocalTaskEditorExecutor, after a passed fence and business validation (plan §5, §13; X-19
    // allowlists this file). The SyncStamper turns Remove into a soft delete at SaveChanges, as before.
    internal static class TaskEditorWriter
    {
        public static void Apply(AppDbContext db, TaskEditorPlan plan)
        {
            if (plan.NoteAdd is { } note)
                db.TaskNotes.Add(note);
            if (plan.NoteEdit is { } noteEdit)
                noteEdit.Tracked.Content = noteEdit.Content;

            foreach (var link in plan.LinkDeletes)
                db.TaskReferenceLinks.Remove(link);

            foreach (var edit in plan.LinkEdits)
            {
                edit.Tracked.Title = edit.Incoming.Title;
                edit.Tracked.Url = edit.Incoming.Url;
                edit.Tracked.Category = edit.Incoming.Category;
                edit.Tracked.SortOrder = edit.Incoming.SortOrder;
            }

            foreach (var link in plan.LinkAdds)
                db.TaskReferenceLinks.Add(link);
        }
    }
}
