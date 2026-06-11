using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services.Telemetry
{
    public interface IOutcomeMaturationService
    {
        /// <summary>
        /// Fills outcome fields on any WeightChangeLog entries whose window has elapsed.
        /// Returns the number of entries matured.
        /// Idempotent: already-matured entries are never touched.
        /// </summary>
        Task<int> MatureAsync(DateTime nowUtc, CancellationToken ct = default);
    }
}
