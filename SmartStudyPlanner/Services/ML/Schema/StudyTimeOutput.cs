using Microsoft.ML.Data;

namespace SmartStudyPlanner.Services.ML.Schema
{
    public class StudyTimeOutput
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
