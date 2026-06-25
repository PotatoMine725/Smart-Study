using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public interface IStudyTimeTrainingDataSource
    {
        Task<IReadOnlyList<StudyTimeInput>> BuildAsync(CancellationToken ct = default);
    }
}
