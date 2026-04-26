namespace SmartStudyPlanner.Models;

public class TaskEditorBundle
{
    public StudyTask Task { get; set; } = null!;
    public TaskNote? Note { get; set; }
    public List<TaskReferenceLink> Links { get; set; } = [];
}
