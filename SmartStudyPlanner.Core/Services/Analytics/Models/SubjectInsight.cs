namespace SmartStudyPlanner.Services.Analytics.Models
{
    public sealed class SubjectInsight
    {
        public string SubjectName       { get; init; } = string.Empty;
        public int    TotalTaskCount    { get; init; }
        public int    CompletedTaskCount { get; init; }
        public double CompletionRate    { get; init; }  // [0.0, 1.0]
        public int    TotalStudyMinutes { get; init; }
    }
}
