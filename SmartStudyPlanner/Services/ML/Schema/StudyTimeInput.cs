using Microsoft.ML.Data;

namespace SmartStudyPlanner.Services.ML.Schema
{
    public class StudyTimeInput
    {
        public string TaskType { get; set; } = string.Empty;
        public float Difficulty { get; set; }
        public float Credits { get; set; }
        public float DaysLeft { get; set; }
        public float StudiedMinutesSoFar { get; set; }

        [ColumnName("Label")]
        public float Label { get; set; }
    }
}
