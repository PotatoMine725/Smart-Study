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

        /// <summary>
        /// SHA-256 of the embedded seed CSV the model was trained from (M8-A only).
        /// Lets <c>InitializeAsync</c> detect a stale seed-only cache and retrain when the
        /// embedded seed changes (e.g. v2 3-class → v3 5-class). Empty for user-trained / M7 models.
        /// </summary>
        public string SeedHash { get; set; } = string.Empty;
    }
}
