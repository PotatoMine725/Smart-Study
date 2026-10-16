using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Fence;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations
{
    // Epic 2 / T2.4 fence Slice 5 -- Phase 1 skeleton (tests first). Implemented in Phase 2.
    internal sealed class LocalTaskEditorExecutor
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public LocalTaskEditorExecutor(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public Task<FenceDecision> UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<FenceDecision> AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<FenceDecision> UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<FenceDecision> DeleteLinkAsync(Guid linkId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<FenceDecision> SaveEditorAsync(Guid taskId, string? noteContent,
            IReadOnlyList<TaskReferenceLink> links, CancellationToken ct = default) =>
            throw new NotImplementedException();

        internal static void ApplyIfPassed(AppDbContext db, TaskEditorPlan plan, FenceDecision decision) =>
            throw new NotImplementedException();
    }
}
