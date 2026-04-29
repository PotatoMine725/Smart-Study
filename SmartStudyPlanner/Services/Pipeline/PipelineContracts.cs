using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.RiskAnalyzer;

namespace SmartStudyPlanner.Services.Pipeline
{
    public enum PipelineStageType
    {
        ParseInput = 0,
        Prioritize = 1,
        BalanceWorkload = 2,
        AssessRisk = 3,
        Adapt = 4
    }

    public enum PipelineStatus
    {
        NotStarted = 0,
        Running = 1,
        Completed = 2,
        CompletedWithWarnings = 3,
        Failed = 4
    }

    public sealed class PipelineUserSettings
    {
        public bool EnableRiskAssessment { get; init; } = true;
        public bool EnableAdaptation { get; init; } = true;
        public double? CapacityHours { get; init; }
        public string? RawInput { get; init; }
    }

    public sealed class ParsedInputModel
    {
        public string? NormalizedInput { get; init; }
        public int ParsedTaskCount { get; init; }
    }

    public sealed class AdaptationSuggestion
    {
        public string RuleKey { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public double SuggestedPriorityDelta { get; init; }
        public double SuggestedWorkloadDelta { get; init; }
    }

    public sealed class PipelineContext
    {
        public HocKy? Semester { get; init; }
        public PipelineUserSettings Settings { get; init; } = new();
        public DateTimeOffset ReferenceTime { get; init; } = DateTimeOffset.MinValue;
        public string? RawInput { get; set; }
        public ParsedInputModel? ParsedInput { get; set; }
        public IReadOnlyList<StudyTask>? PrioritizedTasks { get; set; }
        public IReadOnlyList<ScheduleDay>? Schedule { get; set; }
        public IReadOnlyList<RiskAssessment>? RiskReport { get; set; }
        public IReadOnlyList<AdaptationSuggestion>? Adaptations { get; set; }
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public Dictionary<string, object> Metadata { get; } = new();
        public PipelineStatus Status { get; set; } = PipelineStatus.NotStarted;
    }

    public sealed class PipelineStageResult
    {
        public PipelineStageType StageType { get; init; }
        public bool Success { get; init; }
        public bool Skipped { get; init; }
        public string Message { get; init; } = string.Empty;
        public TimeSpan Elapsed { get; init; }
    }

    public sealed class PipelineExecutionResult
    {
        public PipelineStatus Status { get; init; }
        public IReadOnlyList<PipelineStageResult> StageResults { get; init; } = Array.Empty<PipelineStageResult>();
        public IReadOnlyList<ScheduleDay> Schedule { get; init; } = Array.Empty<ScheduleDay>();
        public IReadOnlyList<RiskAssessment> RiskReport { get; init; } = Array.Empty<RiskAssessment>();
        public IReadOnlyList<AdaptationSuggestion> Adaptations { get; init; } = Array.Empty<AdaptationSuggestion>();
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    }

    public interface IPipelineStage
    {
        PipelineStageType StageType { get; }
        int Order { get; }
        bool CanRun(PipelineContext context);
        PipelineStageResult Execute(PipelineContext context);
    }

    public interface IPipelineOrchestrator
    {
        PipelineExecutionResult Execute(PipelineContext context);
    }
}
