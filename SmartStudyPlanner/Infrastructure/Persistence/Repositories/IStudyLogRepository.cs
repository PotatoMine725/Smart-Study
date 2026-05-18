using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Port cho StudyLog. Replacement dần cho <see cref="Data.IStudyRepository"/> log methods.
    /// </summary>
    public interface IStudyLogRepository
    {
        Task AddAsync(StudyLog log, CancellationToken ct = default);
        Task<List<StudyLog>> GetByTaskAsync(Guid maTask, CancellationToken ct = default);
        Task<List<StudyLog>> GetForHocKyAsync(HocKy hocKy, CancellationToken ct = default);
        Task<List<StudyLog>> GetSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
    }
}
