using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Aggregate query port — feed cho M8-B WeightOptimizer.
    /// </summary>
    public interface IUserStatsRepository
    {
        Task<UserStatsSnapshot> GetSnapshotAsync(DateTime referenceUtc, CancellationToken ct = default);
    }
}
