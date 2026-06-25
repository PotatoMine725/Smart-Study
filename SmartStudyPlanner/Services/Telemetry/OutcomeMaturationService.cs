using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services.Telemetry
{
    public class OutcomeMaturationService : IOutcomeMaturationService
    {
        private readonly IWeightChangeLogRepository _weightChangeLogRepo;
        private readonly IStudyTaskRepository _studyTaskRepo;

        public OutcomeMaturationService(
            IWeightChangeLogRepository weightChangeLogRepo,
            IStudyTaskRepository studyTaskRepo)
        {
            _weightChangeLogRepo = weightChangeLogRepo;
            _studyTaskRepo = studyTaskRepo;
        }

        public async Task<int> MatureAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var pending = await _weightChangeLogRepo.GetPendingMaturationAsync(nowUtc, ct);
            if (pending.Count == 0) return 0;

            var allTasks = await _studyTaskRepo.GetAllAsync(ct);
            var taskById = allTasks.ToDictionary(t => t.MaTask);

            int maturedCount = 0;
            foreach (var log in pending)
            {
                var maturationDate = log.AppliedUtc.AddDays(log.OutcomeWindowDays);

                List<Guid> cohortIds;
                try { cohortIds = JsonSerializer.Deserialize<List<Guid>>(log.CohortTaskIdsJson) ?? new(); }
                catch { cohortIds = new(); }

                var cohortTasks = cohortIds
                    .Where(id => taskById.ContainsKey(id))
                    .Select(id => taskById[id])
                    .ToList();

                int completedInWindow = 0;
                int missedCount = 0;
                double totalDelayDays = 0;
                int lateCount = 0;

                foreach (var task in cohortTasks)
                {
                    bool doneInWindow = task.TrangThai == StudyTaskStatus.HoanThanh
                        && task.NgayHoanThanh.HasValue
                        && task.NgayHoanThanh.Value <= maturationDate;

                    if (doneInWindow)
                    {
                        completedInWindow++;
                        if (task.NgayHoanThanh!.Value > task.HanChot)
                        {
                            totalDelayDays += (task.NgayHoanThanh.Value - task.HanChot).TotalDays;
                            lateCount++;
                        }
                    }
                    else
                    {
                        missedCount++;
                    }
                }

                int n = cohortTasks.Count;
                log.OutcomeMaturedUtc = nowUtc;
                log.OutcomeCompletedInWindow = completedInWindow;
                log.OutcomeMissRate = n > 0 ? (double)missedCount / n : 0.0;
                log.OutcomeAvgDelayDays = lateCount > 0 ? totalDelayDays / lateCount : 0.0;

                await _weightChangeLogRepo.UpdateOutcomeAsync(log, ct);
                maturedCount++;
            }

            return maturedCount;
        }
    }
}
