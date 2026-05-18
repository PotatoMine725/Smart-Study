using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Port cho StudyTask aggregate. Slice 4 — chưa migrate consumer hiện tại.
    /// </summary>
    public interface IStudyTaskRepository
    {
        Task<StudyTask?> GetAsync(Guid maTask, CancellationToken ct = default);
        Task<List<StudyTask>> GetByMonHocAsync(Guid maMonHoc, CancellationToken ct = default);
        Task<List<StudyTask>> GetAllAsync(CancellationToken ct = default);
        Task AddAsync(StudyTask task, CancellationToken ct = default);
        Task UpdateAsync(StudyTask task, CancellationToken ct = default);
        Task DeleteAsync(Guid maTask, CancellationToken ct = default);
    }
}
