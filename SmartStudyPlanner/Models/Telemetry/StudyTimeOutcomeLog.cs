using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models.Telemetry
{
    public class StudyTimeOutcomeLog
    {
        [Key] public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public Guid? MaTask { get; set; }

        // Features captured at study time
        public int TaskType { get; set; }
        public float Difficulty { get; set; }
        public float Credits { get; set; }
        public float DaysLeft { get; set; }
        public float StudiedMinutesSoFar { get; set; }

        // Label + eval columns
        public float ActualMinutes { get; set; }
        public float? PredictedMinutes { get; set; }
        public bool WasMlPrediction { get; set; }
        public float? Confidence { get; set; }
    }
}
