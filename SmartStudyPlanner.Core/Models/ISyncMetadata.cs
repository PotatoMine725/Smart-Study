using System;

namespace SmartStudyPlanner.Models
{
    /// <summary>
    /// D-I sync metadata block. Stamped by <see cref="SmartStudyPlanner.Data.SyncStamper"/> on every
    /// local write. Rev is a local monotonic per-entity counter — never compared across devices (L6).
    /// </summary>
    public interface ISyncMetadata
    {
        long Rev { get; set; }
        DateTime ModifiedAtUtc { get; set; }
        string ModifiedByDeviceId { get; set; }
        bool IsDeleted { get; set; }
        DateTime? DeletedAtUtc { get; set; }
    }
}
