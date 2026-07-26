using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models
{
    public class StudyLog : ISyncMetadata
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MaTask { get; set; }
        public DateTime NgayHoc { get; set; }
        public int SoPhutHoc { get; set; }
        public int SoPhutDuKien { get; set; }
        public bool DaHoanThanh { get; set; }
        public string? GhiChu { get; set; }

        // Sync-ready fields. CreatedAtUtc/DeviceId predate D-I and stay distinct from
        // ISyncMetadata's ModifiedAtUtc/ModifiedByDeviceId (creation != last write).
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string DeviceId { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;

        // D-I sync metadata (Epic 1 / M1.2, T1.1) — stamped by SyncStamper.
        public long Rev { get; set; }
        public DateTime ModifiedAtUtc { get; set; }
        public string ModifiedByDeviceId { get; set; } = string.Empty;
        public DateTime? DeletedAtUtc { get; set; }
    }
}
