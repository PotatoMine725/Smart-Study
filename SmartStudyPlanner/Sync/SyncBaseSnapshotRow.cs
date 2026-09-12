using System;

namespace SmartStudyPlanner.Sync
{
    // Bookkeeping row, NOT a synced business entity. Must NOT implement ISyncMetadata —
    // that would make SyncStamper stamp/tombstone sync bookkeeping as if it were user
    // data, which is meaningless and would corrupt the very watermark it exists to hold.
    public class SyncBaseSnapshotRow
    {
        public string PeerDeviceId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public long Rev { get; set; }

        // Opaque, caller-supplied. NO code in this slice reads or interprets its
        // contents — T2.3 (not built yet) decides that shape. Tests round-trip
        // arbitrary fixture strings only; do not add parsing/validation logic here.
        public string? SnapshotJson { get; set; }
        public DateTime SyncedAtUtc { get; set; }
    }
}
