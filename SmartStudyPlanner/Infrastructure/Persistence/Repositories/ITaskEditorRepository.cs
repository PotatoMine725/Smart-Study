using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Port cho M6.1 — TaskNote + TaskReferenceLink (di chuyển cùng nhau qua <see cref="TaskEditorBundle"/>).
    /// Thay thế các method editor của <see cref="Data.IStudyRepository"/>.
    /// </summary>
    public interface ITaskEditorRepository
    {
        Task<TaskEditorBundle?> GetBundleAsync(Guid taskId, CancellationToken ct = default);
        Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default);
        Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default);
        Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default);
        Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default);
        Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default);
    }
}
