using CommunityToolkit.Mvvm.ComponentModel;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.ViewModels;

public partial class TaskReferenceLinkItemVm : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MaTask { get; set; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string? _category;
    public int SortOrder { get; set; }

    public TaskReferenceLink ToModel() => new()
    {
        Id = Id,
        MaTask = MaTask,
        Title = Title,
        Url = Url,
        Category = Category,
        SortOrder = SortOrder,
        CreatedAtUtc = DateTime.UtcNow,
    };

    public static TaskReferenceLinkItemVm FromModel(TaskReferenceLink m) => new()
    {
        Id = m.Id,
        MaTask = m.MaTask,
        Title = m.Title,
        Url = m.Url,
        Category = m.Category,
        SortOrder = m.SortOrder,
    };
}
