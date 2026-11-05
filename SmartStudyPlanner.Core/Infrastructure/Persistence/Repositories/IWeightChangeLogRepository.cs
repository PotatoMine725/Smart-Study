using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models.Telemetry;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    public interface IWeightChangeLogRepository
    {
        Task AddAsync(WeightChangeLog entry, CancellationToken ct = default);

        /// <summary>Trả về các bản ghi chưa mature và đã qua cửa sổ outcome (applyUtc + windowDays &lt;= now).</summary>
        Task<IReadOnlyList<WeightChangeLog>> GetPendingMaturationAsync(DateTime nowUtc, CancellationToken ct = default);

        Task UpdateOutcomeAsync(WeightChangeLog entry, CancellationToken ct = default);
    }
}
