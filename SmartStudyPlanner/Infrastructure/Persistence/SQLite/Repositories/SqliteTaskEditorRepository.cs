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
    public sealed class SqliteTaskEditorRepository : ITaskEditorRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteTaskEditorRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task<TaskEditorBundle?> GetBundleAsync(Guid taskId, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            var task = await db.StudyTasks.FirstOrDefaultAsync(t => t.MaTask == taskId && !t.IsDeleted, ct);
            if (task is null) return null;
            var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId && !n.IsDeleted, ct);
            var links = await db.TaskReferenceLinks
                .Where(l => l.MaTask == taskId && !l.IsDeleted)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(ct);
            return new TaskEditorBundle { Task = task, Note = note, Links = links };
        }

        public async Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            var note = await db.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId && !n.IsDeleted, ct);
            if (note is null)
                db.TaskNotes.Add(new TaskNote { MaTask = taskId, Content = content });
            else
            {
                note.Content = content;
            }
            await db.SaveChangesAsync(ct);
        }

        public async Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.TaskReferenceLinks
                .Where(l => l.MaTask == taskId && !l.IsDeleted)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(ct);
        }

        public async Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.TaskReferenceLinks.Add(link);
            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            db.TaskReferenceLinks.Update(link);
            await db.SaveChangesAsync(ct);
        }

        public async Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            var link = await db.TaskReferenceLinks.FindAsync(new object[] { linkId }, ct);
            if (link is not null)
            {
                db.TaskReferenceLinks.Remove(link);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
