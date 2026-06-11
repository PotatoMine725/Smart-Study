using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;

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

    /// <summary>In-memory fake cho <see cref="IDifficultyLabelLogRepository"/>. Ghi lại entries để assert.</summary>
    internal sealed class FakeDifficultyLabelLogRepository : IDifficultyLabelLogRepository
    {
        public List<DifficultyLabelLog> Added { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task AddAsync(DifficultyLabelLog entry, CancellationToken ct = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("simulated repo failure");
            Added.Add(entry);
            return Task.CompletedTask;
        }
    }

    /// <summary>In-memory fake cho <see cref="IWeightChangeLogRepository"/>.</summary>
    internal sealed class FakeWeightChangeLogRepository : IWeightChangeLogRepository
    {
        public List<WeightChangeLog> Added { get; } = new();
        public List<WeightChangeLog> All { get; } = new();
        public bool ShouldThrow { get; set; }

        public void Seed(IEnumerable<WeightChangeLog> logs) => All.AddRange(logs);

        public Task AddAsync(WeightChangeLog entry, CancellationToken ct = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("simulated failure");
            Added.Add(entry);
            All.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WeightChangeLog>> GetPendingMaturationAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var pending = All
                .Where(e => e.OutcomeMaturedUtc == null && e.AppliedUtc.AddDays(e.OutcomeWindowDays) <= nowUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<WeightChangeLog>>(pending);
        }

        // Entry is the same object reference held in All, so caller mutations are reflected.
        public Task UpdateOutcomeAsync(WeightChangeLog entry, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>In-memory fake cho <see cref="IUserStatsRepository"/>.</summary>
    internal sealed class FakeUserStatsRepository : IUserStatsRepository
    {
        public UserStatsSnapshot Snapshot { get; set; } = new UserStatsSnapshot { ReferenceUtc = DateTime.UtcNow };

        public Task<UserStatsSnapshot> GetSnapshotAsync(DateTime referenceUtc, CancellationToken ct = default)
            => Task.FromResult(Snapshot);
    }

    /// <summary>In-memory fake cho <see cref="IStudyTaskRepository"/>.</summary>
    internal sealed class FakeStudyTaskRepository : IStudyTaskRepository
    {
        private List<StudyTask> _tasks = new();
        public void Seed(IEnumerable<StudyTask> tasks) => _tasks = tasks.ToList();

        public Task<StudyTask?> GetAsync(Guid maTask, CancellationToken ct = default)
            => Task.FromResult(_tasks.FirstOrDefault(t => t.MaTask == maTask));

        public Task<List<StudyTask>> GetByMonHocAsync(Guid maMonHoc, CancellationToken ct = default)
            => Task.FromResult(_tasks.Where(t => t.MaMonHoc == maMonHoc).ToList());

        public Task<List<StudyTask>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(new List<StudyTask>(_tasks));

        public Task AddAsync(StudyTask task, CancellationToken ct = default) { _tasks.Add(task); return Task.CompletedTask; }
        public Task UpdateAsync(StudyTask task, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid maTask, CancellationToken ct = default) => Task.CompletedTask;
    }
}
