using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models.Telemetry;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    public interface IDifficultyLabelLogRepository
    {
        Task AddAsync(DifficultyLabelLog entry, CancellationToken ct = default);
    }
}
