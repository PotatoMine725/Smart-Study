using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models.Telemetry;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    public interface IStudyTimeOutcomeLogRepository
    {
        Task AddAsync(StudyTimeOutcomeLog entry, CancellationToken ct = default);
        Task<IReadOnlyList<StudyTimeOutcomeLog>> GetAllAsync(CancellationToken ct = default);
        Task<IReadOnlyList<StudyTimeOutcomeLog>> GetSinceAsync(DateTime since, CancellationToken ct = default);
        Task<int> CountAsync(CancellationToken ct = default);
    }
}
