using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML.Schema;

namespace SmartStudyPlanner.Services.ML
{
    public sealed class StudyTimeTrainingDataSource : IStudyTimeTrainingDataSource
    {
        public const int MinRows = 50;

        private readonly IStudyTimeOutcomeLogRepository _repo;

        public StudyTimeTrainingDataSource(IStudyTimeOutcomeLogRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<StudyTimeInput>> BuildAsync(CancellationToken ct = default)
        {
            var rows = await _repo.GetAllAsync(ct);
            if (rows.Count < MinRows)
                return System.Array.Empty<StudyTimeInput>();

            return rows.Select(r => new StudyTimeInput
            {
                TaskType            = ((LoaiCongViec)r.TaskType).ToString(),
                Difficulty          = r.Difficulty,
                Credits             = r.Credits,
                DaysLeft            = r.DaysLeft,
                StudiedMinutesSoFar = r.StudiedMinutesSoFar,
                Label               = r.ActualMinutes,
            }).ToList();
        }
    }
}
