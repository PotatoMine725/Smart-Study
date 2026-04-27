namespace SmartStudyPlanner.Services.ML.Schema
{
    public class ModelMeta
    {
        public string LastRetrainedAt { get; set; } = string.Empty;
        public int LogsUsedCount { get; set; }
        public int ModelVersion { get; set; }
        public bool SeedOnly { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string ModelHash { get; set; } = string.Empty;
    }
}
