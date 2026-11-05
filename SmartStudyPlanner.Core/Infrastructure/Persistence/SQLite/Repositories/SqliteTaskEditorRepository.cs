using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    // Epic 2 / T2.4 fence Slice 5: reads stay here; every mutation is delegated to
    // LocalTaskEditorExecutor, which routes it through the structural conflict fence (T-11: the
    // repository is not the semantic owner of a write).
    public sealed class SqliteTaskEditorRepository : ITaskEditorRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;
        private readonly LocalTaskEditorExecutor _executor;

        public SqliteTaskEditorRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
            _executor = new LocalTaskEditorExecutor(ctxFactory);
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

        public Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default) =>
            _executor.UpsertNoteAsync(taskId, content, ct);

        public async Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.TaskReferenceLinks
                .Where(l => l.MaTask == taskId && !l.IsDeleted)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(ct);
        }

        public Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default) =>
            _executor.AddLinkAsync(link, ct);

        /// <summary>
        /// Applies an edit to an existing link. The caller hands us a *detached* POCO (the editor VM
        /// builds one via <c>TaskReferenceLinkItemVm.ToModel()</c>) that carries only the editable
        /// fields; its Id/MaTask/Rev/CreatedAtUtc/sync metadata are not authoritative. Loading the
        /// tracked row and copying just the editable fields keeps identity (Id, MaTask), creation
        /// provenance (CreatedAtUtc) and the Rev watermark intact, and lets SaveChanges stamp this as
        /// the ordinary local modification it is. A missing or tombstoned target is rejected, not a
        /// no-op, so the bug surfaces at the call site.
        /// </summary>
        public Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default) =>
            _executor.UpdateLinkAsync(link, ct);

        public Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default) =>
            _executor.DeleteLinkAsync(linkId, ct);

        /// <summary>E2 (owner ruling 2026-10-16): the whole editor change in one fenced transaction.</summary>
        public Task SaveEditorAsync(Guid taskId, string? noteContent, IReadOnlyList<TaskReferenceLink> links, CancellationToken ct = default) =>
            _executor.SaveEditorAsync(taskId, noteContent, links, ct);
    }
}
