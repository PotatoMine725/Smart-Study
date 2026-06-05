namespace SmartStudyPlanner.Services.ML.Schema
{
    /// <summary>
    /// Represents one CSV row of the TextClassifier seed/training set, and doubles as the
    /// ML.NET input row. The pipeline maps <see cref="TaskType"/> → "Label" via MapValueToKey,
    /// so there is intentionally NO [ColumnName("Label")] attribute here.
    /// </summary>
    public class TextClassifierInput
    {
        /// <summary>The raw natural-language phrase used as the text feature.</summary>
        public string InputText { get; set; } = string.Empty;

        /// <summary>Optional friendly task name (not used as a feature this slice).</summary>
        public string? TaskName { get; set; }

        /// <summary>The classification label: exact <c>LoaiCongViec</c> enum name.</summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Parsed from the required "Difficulty" column. Slice 5 validates and parses this column
        /// but does NOT feed it to the model — difficulty prediction is a separate (deferred) model.
        /// </summary>
        public float Difficulty { get; set; }

        /// <summary>Optional deadline hint string; validated (column required) but not model-predicted this slice.</summary>
        public string? DeadlineHint { get; set; }

        /// <summary>Optional provenance of the row (e.g. "seed", "user").</summary>
        public string? Source { get; set; }

        /// <summary>Optional label-set version tag for future migrations.</summary>
        public string? LabelVersion { get; set; }
    }
}
