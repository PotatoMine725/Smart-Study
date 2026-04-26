using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Pipeline.Stages
{
    /// <summary>
    /// Stage rule-based adaptation MVP theo README §4.4.
    /// Không mutate dữ liệu domain; chỉ sinh suggestion để re-plan.
    /// </summary>
    public sealed class AdaptStage : IPipelineStage
    {
        private const int AssumedSemesterDays = 120;

        public PipelineStageType StageType => PipelineStageType.Adapt;
        public int Order => (int)StageType;

        public bool CanRun(PipelineContext context)
        {
            return context is not null && context.Settings.EnableAdaptation;
        }

        public PipelineStageResult Execute(PipelineContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            var suggestions = new List<AdaptationSuggestion>();
            var semester = context.Semester;
            if (semester is not null)
            {
                var start = semester.NgayBatDau.Date;
                var today = context.ReferenceTime.Date;
                var daysPassed = Math.Max(0, (today - start).Days);
                var totalDays = Math.Max(1, AssumedSemesterDays);
                var expectedProgress = (double)daysPassed / totalDays;

                foreach (var mon in semester.DanhSachMonHoc)
                {
                    var taskCount = mon.DanhSachTask.Count;
                    if (taskCount == 0) continue;

                    var completed = mon.DanhSachTask.Count(t => t.TrangThai == StudyTaskStatus.HoanThanh);
                    var progress = (double)completed / taskCount;

                    if (progress + 0.05 < expectedProgress)
                    {
                        suggestions.Add(new AdaptationSuggestion
                        {
                            RuleKey = "progress_below_expected",
                            Message = $"{mon.TenMonHoc}: progress thấp hơn expected progress, nên tăng priority cho tasks còn lại.",
                            SuggestedPriorityDelta = 0.1,
                            SuggestedWorkloadDelta = 0
                        });
                    }

                    if (completed > 0 && completed == taskCount)
                    {
                        suggestions.Add(new AdaptationSuggestion
                        {
                            RuleKey = "milestone_exceeded",
                            Message = $"{mon.TenMonHoc}: đã hoàn thành milestone, có thể giảm workload kế tiếp.",
                            SuggestedPriorityDelta = 0,
                            SuggestedWorkloadDelta = -0.1
                        });
                    }
                }
            }

            context.Adaptations = suggestions;
            context.Metadata["adapt.count"] = suggestions.Count;
            return new PipelineStageResult
            {
                StageType = StageType,
                Success = true,
                Message = $"Generated {suggestions.Count} adaptation suggestion(s)."
            };
        }
    }
}
