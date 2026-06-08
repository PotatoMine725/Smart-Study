using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Tests.TestDoubles
{
    /// <summary>In-memory fake cho <see cref="IHocKyRepository"/>. Ghi lại lệnh lưu để assert.</summary>
    internal sealed class FakeHocKyRepository : IHocKyRepository
    {
        public List<HocKy> SavedHocKys { get; } = new();
        private List<HocKy> _seeded = new();
        public void Seed(List<HocKy> list) => _seeded = list;

        public Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default)
            => Task.FromResult(_seeded);

        public Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
        {
            SavedHocKys.Add(hocKy);
            return Task.CompletedTask;
        }
    }

    /// <summary>In-memory fake cho <see cref="IStudyLogRepository"/>. Giữ <see cref="AddedLogs"/> để assert.</summary>
    internal sealed class FakeStudyLogRepository : IStudyLogRepository
    {
        public List<StudyLog> AddedLogs { get; } = new();
        private List<StudyLog> _seeded = new();
        public void SeedLogs(IEnumerable<StudyLog> logs) => _seeded = logs.ToList();

        public Task AddAsync(StudyLog log, CancellationToken ct = default)
        {
            AddedLogs.Add(log);
            return Task.CompletedTask;
        }

        public Task<List<StudyLog>> GetByTaskAsync(Guid maTask, CancellationToken ct = default)
            => Task.FromResult(_seeded.Where(l => l.MaTask == maTask).ToList());

        public Task<List<StudyLog>> GetForHocKyAsync(HocKy hocKy, CancellationToken ct = default)
            => Task.FromResult(new List<StudyLog>(_seeded));

        public Task<List<StudyLog>> GetSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
            => Task.FromResult(_seeded
                .Where(l => l.CreatedAtUtc >= sinceUtc && !l.IsDeleted)
                .OrderBy(l => l.CreatedAtUtc)
                .ToList());
    }

    /// <summary>No-op fake cho <see cref="ITaskEditorRepository"/> (đủ để dựng ViewModel test).</summary>
    internal sealed class FakeTaskEditorRepository : ITaskEditorRepository
    {
        public Task<TaskEditorBundle?> GetBundleAsync(Guid taskId, CancellationToken ct = default)
            => Task.FromResult<TaskEditorBundle?>(null);
        public Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default)
            => Task.FromResult(new List<TaskReferenceLink>());
        public Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
