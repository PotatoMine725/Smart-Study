using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Tests.Helpers
{
    internal class FakeStudyRepository : IStudyRepository
    {
        public List<StudyLog> AddedLogs { get; } = new();
        public List<StudyLog> Logs { get; } = new();

        public void SeedLogs(IEnumerable<StudyLog> logs)
        {
            Logs.Clear();
            Logs.AddRange(logs);
        }

        public Task<HocKy> DocHocKyAsync() => Task.FromResult<HocKy>(null);
        public Task<List<HocKy>> LayDanhSachHocKyAsync() => Task.FromResult(new List<HocKy>());
        public Task LuuHocKyAsync(HocKy hocKy) => Task.CompletedTask;

        public Task AddStudyLogAsync(StudyLog log)
        {
            AddedLogs.Add(log);
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy) => Task.FromResult(new List<StudyLog>(Logs));
        public Task<List<StudyLog>> GetStudyLogsSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
            Task.FromResult(Logs.FindAll(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted));

        public Task<TaskEditorBundle?> GetTaskEditorBundleAsync(Guid taskId) => Task.FromResult<TaskEditorBundle?>(null);
        public Task UpsertTaskNoteAsync(Guid taskId, string? content) => Task.CompletedTask;
        public Task<List<TaskReferenceLink>> GetTaskReferenceLinksAsync(Guid taskId) => Task.FromResult(new List<TaskReferenceLink>());
        public Task AddTaskReferenceLinkAsync(TaskReferenceLink link) => Task.CompletedTask;
        public Task UpdateTaskReferenceLinkAsync(TaskReferenceLink link) => Task.CompletedTask;
        public Task DeleteTaskReferenceLinkAsync(Guid linkId) => Task.CompletedTask;
        public Task SaveTaskEditorBundleAsync(TaskEditorBundle bundle) => Task.CompletedTask;
    }
}
