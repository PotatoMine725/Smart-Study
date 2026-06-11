using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models.Telemetry
{
    public class WeightChangeLog
    {
        [Key] public Guid Id { get; set; }
        public DateTime AppliedUtc { get; set; }
        public double Confidence { get; set; }
        public string Rationale { get; set; } = string.Empty;

        // Before weights
        public double BeforeTime { get; set; }
        public double BeforeTaskType { get; set; }
        public double BeforeCredit { get; set; }
        public double BeforeDifficulty { get; set; }

        // After weights
        public double AfterTime { get; set; }
        public double AfterTaskType { get; set; }
        public double AfterCredit { get; set; }
        public double AfterDifficulty { get; set; }

        // Baseline snapshot at apply time
        public double BaselineMissRate { get; set; }
        public double BaselineAvgDelayDays { get; set; }
        public int BaselineFocusStreak { get; set; }
        public int BaselineStudyMinutes30 { get; set; }
        public int BaselineTaskCount { get; set; }
        public int BaselineCompletedCount { get; set; }

        // Cohort: task IDs open at apply time (JSON array of Guid strings)
        public string CohortTaskIdsJson { get; set; } = "[]";

        // Outcome (filled after OutcomeWindowDays)
        public int OutcomeWindowDays { get; set; } = 14;
        public DateTime? OutcomeMaturedUtc { get; set; }
        public double? OutcomeMissRate { get; set; }
        public double? OutcomeAvgDelayDays { get; set; }
        public int? OutcomeCompletedInWindow { get; set; }
    }
}
