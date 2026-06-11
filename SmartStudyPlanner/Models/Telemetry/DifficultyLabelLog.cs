using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models.Telemetry
{
    public class DifficultyLabelLog
    {
        [Key] public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string InputText { get; set; } = string.Empty;
        public LoaiCongViec TaskType { get; set; }
        public int? SuggestedDoKho { get; set; }
        public int FinalDoKho { get; set; }
        public bool WasOverride { get; set; }
        public string Source { get; set; } = string.Empty;
        public Guid? MaTask { get; set; }
    }
}
