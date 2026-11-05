using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Port cho M6.1 — TaskNote + TaskReferenceLink (di chuyển cùng nhau qua <see cref="TaskEditorBundle"/>).
    /// Thay thế các method editor của StudyRepository cũ.
    /// </summary>
    public interface ITaskEditorRepository
    {
        Task<TaskEditorBundle?> GetBundleAsync(Guid taskId, CancellationToken ct = default);
        Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default);
        Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default);
        Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default);
        Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default);
        Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default);

        /// <summary>
        /// Epic 2 / T2.4 fence Slice 5, E2 (owner ruling 2026-10-16, L1 amendment: additive port method
        /// allowed, no existing signature changes): the editor's whole note + links change as ONE
        /// operation. <paramref name="links"/> is the complete live set the task should end up with, in
        /// display order; a live link of <paramref name="taskId"/> that it does not name is deleted.
        /// <para>
        /// This default body is the reference sequence <c>QuanLyTaskViewModel.ThemTask</c> ran before
        /// Slice 5 -- the four calls above, one after another, NOT atomic. It exists so that
        /// implementations written before this method keep compiling and keep their observable calls
        /// (the test doubles). <c>SqliteTaskEditorRepository</c> overrides it with one fenced
        /// transaction: all of it persists or none of it does.
        /// </para>
        /// </summary>
        async Task SaveEditorAsync(Guid taskId, string? noteContent, IReadOnlyList<TaskReferenceLink> links, CancellationToken ct = default)
        {
            await UpsertNoteAsync(taskId, noteContent, ct);
            var existingLinks = await GetLinksAsync(taskId, ct);
            var incomingIds = links.Select(l => l.Id).ToHashSet();
            foreach (var dead in existingLinks.Where(l => !incomingIds.Contains(l.Id)))
                await DeleteLinkAsync(dead.Id, ct);
            foreach (var link in links)
            {
                if (existingLinks.Any(l => l.Id == link.Id))
                    await UpdateLinkAsync(link, ct);
                else
                    await AddLinkAsync(link, ct);
            }
        }
    }
}
