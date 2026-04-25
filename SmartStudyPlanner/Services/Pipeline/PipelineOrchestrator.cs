using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.RiskAnalyzer;

namespace SmartStudyPlanner.Services.Pipeline
{
    /// <summary>
    /// Orchestrator điều phối pipeline theo thứ tự stage cố định.
    /// Không chứa logic tính toán nghiệp vụ; chỉ gom state, order, error handling và diagnostics.
    /// </summary>
    public sealed class PipelineOrchestrator : IPipelineOrchestrator
    {
        private readonly IReadOnlyList<IPipelineStage> _stages;

        public PipelineOrchestrator(IEnumerable<IPipelineStage> stages)
        {
            _stages = stages.OrderBy(s => s.Order).ThenBy(s => s.StageType).ToArray();
        }

        public PipelineExecutionResult Execute(PipelineContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            var stageResults = new List<PipelineStageResult>();
            context.Status = PipelineStatus.Running;

            foreach (var stage in _stages)
            {
                if (!stage.CanRun(context))
                {
                    stageResults.Add(new PipelineStageResult
                    {
                        StageType = stage.StageType,
                        Success = true,
                        Skipped = true,
                        Message = $"Stage {stage.StageType} skipped by policy."
                    });
                    continue;
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    var result = stage.Execute(context);
                    sw.Stop();
                    stageResults.Add(new PipelineStageResult
                    {
                        StageType = result.StageType,
                        Success = result.Success,
                        Skipped = result.Skipped,
                        Message = result.Message,
                        Elapsed = sw.Elapsed
                    });

                    if (!result.Success && !result.Skipped)
                    {
                        context.Errors.Add(result.Message);
                        context.Status = PipelineStatus.Failed;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    context.Errors.Add($"{stage.StageType}: {ex.Message}");
                    stageResults.Add(new PipelineStageResult
                    {
                        StageType = stage.StageType,
                        Success = false,
                        Message = ex.Message,
                        Elapsed = sw.Elapsed
                    });
                    context.Status = PipelineStatus.Failed;
                    break;
                }
            }

            if (context.Status != PipelineStatus.Failed)
            {
                context.Status = context.Warnings.Count > 0 ? PipelineStatus.CompletedWithWarnings : PipelineStatus.Completed;
            }

            return new PipelineExecutionResult
            {
                Status = context.Status,
                StageResults = stageResults,
                Schedule = context.Schedule ?? Array.Empty<ScheduleDay>(),
                RiskReport = context.RiskReport ?? Array.Empty<RiskAssessment>(),
                Adaptations = context.Adaptations ?? Array.Empty<AdaptationSuggestion>(),
                Warnings = context.Warnings,
                Errors = context.Errors
            };
        }
    }
}
