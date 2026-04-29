using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models
{
    public class StudyLog
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MaTask { get; set; }
        public DateTime NgayHoc { get; set; }
        public int SoPhutHoc { get; set; }
        public int SoPhutDuKien { get; set; }
        public bool DaHoanThanh { get; set; }
        public string? GhiChu { get; set; }

        // Sync-ready fields
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string DeviceId { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
    }
}
