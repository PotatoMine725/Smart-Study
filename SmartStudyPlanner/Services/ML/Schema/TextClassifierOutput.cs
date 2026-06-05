using Microsoft.ML.Data;

namespace SmartStudyPlanner.Services.ML.Schema
{
    /// <summary>
    /// ML.NET prediction-binding row for the multiclass TextClassifier.
    /// </summary>
    public class TextClassifierOutput
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        [ColumnName("Score")]
        public float[] Score { get; set; } = System.Array.Empty<float>();
    }
}
