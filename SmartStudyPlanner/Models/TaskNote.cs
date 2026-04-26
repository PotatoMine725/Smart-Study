namespace SmartStudyPlanner.Models;

public class TaskNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaTask { get; set; }
    public string? Content { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
