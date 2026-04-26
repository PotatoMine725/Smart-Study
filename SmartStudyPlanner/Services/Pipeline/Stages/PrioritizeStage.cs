using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Pipeline.Stages
{
    /// <summary>
    /// Stage tính priority thông qua IDecisionEngine. Không thay đổi công thức score.
    /// </summary>
    public sealed class PrioritizeStage : IPipelineStage
    {
        private readonly IDecisionEngine _decisionEngine;

        public PrioritizeStage(IDecisionEngine decisionEngine)
        {
            _decisionEngine = decisionEngine;
        }

        public PipelineStageType StageType => PipelineStageType.Prioritize;
        public int Order => (int)StageType;

        public bool CanRun(PipelineContext context)
        {
            return context is not null && GetTasks(context).Any();
        }

        public PipelineStageResult Execute(PipelineContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            var semester = context.Semester;
            var tasks = GetTasks(context).ToList();
            if (semester is null || tasks.Count == 0)
            {
                return new PipelineStageResult
                {
                    StageType = StageType,
                    Success = false,
                    Message = "No semester or tasks available for prioritization."
                };
            }

            foreach (var mon in semester.DanhSachMonHoc)
            {
                foreach (var task in mon.DanhSachTask)
                {
                    if (task.TrangThai == StudyTaskStatus.HoanThanh) continue;
                    task.DiemUuTien = _decisionEngine.CalculatePriority(task, mon);
                }
            }

            var prioritizedTasks = semester.DanhSachMonHoc
                .SelectMany(m => m.DanhSachTask.Where(t => t.TrangThai != StudyTaskStatus.HoanThanh))
                .OrderByDescending(t => t.DiemUuTien)
                .ToArray();

            context.PrioritizedTasks = prioritizedTasks;
            context.Metadata["prioritized.count"] = prioritizedTasks.Length;

            return new PipelineStageResult
            {
                StageType = StageType,
                Success = true,
                Message = $"Prioritized {prioritizedTasks.Length} tasks."
            };
        }

        private static IEnumerable<StudyTask> GetTasks(PipelineContext context)
        {
            return context.Semester?.DanhSachMonHoc?.SelectMany(m => m.DanhSachTask) ?? Array.Empty<StudyTask>();
        }
    }
}
