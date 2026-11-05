namespace SmartStudyPlanner.Models;

public class TaskNote : ISyncMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaTask { get; set; }
    public string? Content { get; set; }

    // D-I sync metadata (Epic 1 / M1.2, T1.1) — stamped by SyncStamper.
    // Reconciled from the pre-Epic-1 UpdatedAtUtc field (same meaning, one name).
    public long Rev { get; set; }
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public string ModifiedByDeviceId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
