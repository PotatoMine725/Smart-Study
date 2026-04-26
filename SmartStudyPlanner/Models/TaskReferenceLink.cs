namespace SmartStudyPlanner.Models;

public class TaskReferenceLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaTask { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
