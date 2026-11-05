namespace SmartStudyPlanner.Models;

public class TaskReferenceLink : ISyncMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaTask { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // D-I sync metadata (Epic 1 / M1.2, T1.1) — stamped by SyncStamper. Distinct from
    // CreatedAtUtc above (creation != last write).
    public long Rev { get; set; }
    public DateTime ModifiedAtUtc { get; set; }
    public string ModifiedByDeviceId { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
