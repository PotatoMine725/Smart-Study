using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite
{
    // Epic 1 / M1.2 (G1, review fix M1.2-R1): TaskNote/TaskReferenceLink are FK-only
    // relationships (no navigation property from StudyTask), so EF's ChangeTracker fixup
    // can never reach them via .Include() -- every place that removes a StudyTask must call
    // this explicitly so SyncStamper tombstones the children too, instead of leaving them
    // live and orphaned pointing at a dead parent.
    //
    // D-2: live children only (IsDeleted == false, the ruled cascade predicate). An already-dead
    // note/link is left exactly as it is -- not re-stamped, and never hard-deleted: a dead TaskNote
    // still occupies UNIQUE(MaTask) (D9-T1).
    internal static class TaskCascadeHelper
    {
        public static async Task RemoveChildrenAsync(AppDbContext db, Guid maTask, CancellationToken ct = default)
        {
            var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == maTask && !n.IsDeleted, ct);
            if (note != null) db.TaskNotes.Remove(note);

            var links = await db.TaskReferenceLinks.Where(l => l.MaTask == maTask && !l.IsDeleted).ToListAsync(ct);
            if (links.Count > 0) db.TaskReferenceLinks.RemoveRange(links);
        }
    }
}
