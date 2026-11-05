using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Pipeline.Stages
{
    /// <summary>
    /// Stage cân bằng workload bằng IWorkloadService. Không thay đổi thuật toán cân bằng lõi.
    /// </summary>
    public sealed class BalanceWorkloadStage : IPipelineStage
    {
        private readonly IWorkloadService _workloadService;

        public BalanceWorkloadStage(IWorkloadService workloadService)
        {
            _workloadService = workloadService;
        }

        public PipelineStageType StageType => PipelineStageType.BalanceWorkload;
        public int Order => (int)StageType;

        public bool CanRun(PipelineContext context)
        {
            return context is not null && context.Semester is not null;
        }

        public PipelineStageResult Execute(PipelineContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (context.Semester is null)
            {
                return new PipelineStageResult
                {
                    StageType = StageType,
                    Success = false,
                    Message = "No semester available for workload balancing."
                };
            }

            double capacity = context.Settings.CapacityHours ?? _workloadService.GetCapacity();
            List<ScheduleDay> schedule = _workloadService.GenerateSchedule(context.Semester, capacity);
            context.Schedule = schedule;

            return new PipelineStageResult
            {
                StageType = StageType,
                Success = true,
                Message = $"Generated schedule for {schedule.Count} days."
            };
        }
    }
}
