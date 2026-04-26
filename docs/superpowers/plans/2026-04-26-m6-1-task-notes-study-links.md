# M6.1 — Task Notes & Study Links Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a DB-split `TaskNote` (1-1) and `TaskReferenceLink` (1-N) aggregate to the task management screen, with a clear 3-zone editor UI, strict parser isolation, and cascade delete from the parent task.

**Architecture:**
- Two new EF entities in separate DB tables, mapped via Fluent API FK + cascade delete.
- `TaskEditorBundle` DTO aggregates task + note + links to reduce ViewModel blast radius.
- New per-task repository methods; notes/links are NEVER routed through `LuuHocKyAsync` (delete-reinsert would wipe them).
- `QuanLyTaskViewModel` gains `NoteContent`, `ObservableCollection<TaskReferenceLinkItemVm>`, and 5 link commands.
- `QuanLyTaskPage.xaml` divided into 3 `GroupBox` zones: core fields / notes / study links.
- `PhanTichNhapNhanh` parser remains unchanged — hard invariant, touches only core fields.

**Tech Stack:** C# 12, .NET 10, WPF, EF Core 9 / SQLite (`EnsureCreated`), CommunityToolkit.Mvvm, xUnit

**EnsureCreated note:** New tables only appear on fresh DB files. For dev, delete `studyplanner.db` and relaunch after this feature lands. Existing data is preserved if new tables are added without making note/link fields required on `StudyTask`.

---

## File Map

| Action | Path |
|--------|------|
| Create | `SmartStudyPlanner/Models/TaskNote.cs` |
| Create | `SmartStudyPlanner/Models/TaskReferenceLink.cs` |
| Create | `SmartStudyPlanner/Models/TaskEditorBundle.cs` |
| Create | `SmartStudyPlanner/ViewModels/TaskReferenceLinkItemVm.cs` |
| Modify | `SmartStudyPlanner/Data/AppDbContext.cs` |
| Modify | `SmartStudyPlanner/Data/IStudyRepository.cs` |
| Modify | `SmartStudyPlanner/Data/StudyRepository.cs` |
| Modify | `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs` |
| Modify | `SmartStudyPlanner/Views/QuanLyTaskPage.xaml` |
| Create | `SmartStudyPlanner.Tests/TaskNotesTests.cs` |

---

## Task M6.1-1: Define `TaskNote` and `TaskReferenceLink` entities

**Context:** Two new model files. No nav props on `StudyTask` — the aggregate relationship is managed purely at the repository and EF mapping level. `TaskNote` is 1-1 (unique constraint on `MaTask`). `TaskReferenceLink` is 1-N.

**Files:**
- Create: `SmartStudyPlanner/Models/TaskNote.cs`
- Create: `SmartStudyPlanner/Models/TaskReferenceLink.cs`

**Steps:**
- [ ] Create `TaskNote.cs`:
  ```csharp
  public class TaskNote
  {
      public Guid Id { get; set; } = Guid.NewGuid();
      public Guid MaTask { get; set; }
      public string? Content { get; set; }
      public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
  }
  ```
- [ ] Create `TaskReferenceLink.cs`:
  ```csharp
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
  ```

**Verification:**
- [ ] Both files compile (`dotnet build`)

---

## Task M6.1-2: Define `TaskEditorBundle` DTO

**Context:** A lightweight aggregate DTO so `QuanLyTaskViewModel` loads all three concerns (task core + note + links) in one call without manually stitching repository results.

**Files:**
- Create: `SmartStudyPlanner/Models/TaskEditorBundle.cs`

**Steps:**
- [ ] Create `TaskEditorBundle.cs`:
  ```csharp
  public class TaskEditorBundle
  {
      public StudyTask Task { get; set; } = null!;
      public TaskNote? Note { get; set; }
      public List<TaskReferenceLink> Links { get; set; } = [];
  }
  ```

**Verification:**
- [ ] File compiles

---

## Task M6.1-3: Create `TaskReferenceLinkItemVm` (item ViewModel)

**Context:** `ObservableCollection<TaskReferenceLinkItemVm>` in the ViewModel. Wraps a `TaskReferenceLink` with observable properties so the DataGrid / ItemsControl can bind and reflect edits without exposing the model directly.

**Files:**
- Create: `SmartStudyPlanner/ViewModels/TaskReferenceLinkItemVm.cs`

**Steps:**
- [ ] Create `TaskReferenceLinkItemVm.cs` using `ObservableObject`:
  ```csharp
  public partial class TaskReferenceLinkItemVm : ObservableObject
  {
      public Guid Id { get; } = Guid.NewGuid();
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
          MaTask = m.MaTask,
          Title = m.Title,
          Url = m.Url,
          Category = m.Category,
          SortOrder = m.SortOrder,
          _id = m.Id,  // use backing field or constructor
      };
  }
  ```
  > Note: `Id` must be preserved when round-tripping from an existing DB record. Adjust the `FromModel` helper to pass the existing `Guid` — e.g., add a private setter or a constructor overload.

**Verification:**
- [ ] File compiles; `ToModel()` / `FromModel()` round-trip correctly in a quick manual test

---

## Task M6.1-4: Register entities in `AppDbContext`

**Context:** Add `DbSet<TaskNote>` and `DbSet<TaskReferenceLink>`. Configure Fluent API: FK relationship, cascade delete from `StudyTask`, unique constraint on `TaskNote.MaTask`. No nav props are added to `StudyTask`.

**Files:**
- Modify: `SmartStudyPlanner/Data/AppDbContext.cs`

**Steps:**
- [ ] Add properties:
  ```csharp
  public DbSet<TaskNote> TaskNotes => Set<TaskNote>();
  public DbSet<TaskReferenceLink> TaskReferenceLinks => Set<TaskReferenceLink>();
  ```
- [ ] In `OnModelCreating`, add after existing task configuration:
  ```csharp
  // TaskNote: 1-1 with StudyTask, cascade delete
  modelBuilder.Entity<TaskNote>(b =>
  {
      b.HasKey(n => n.Id);
      b.HasIndex(n => n.MaTask).IsUnique();
      b.HasOne<StudyTask>()
       .WithOne()
       .HasForeignKey<TaskNote>(n => n.MaTask)
       .OnDelete(DeleteBehavior.Cascade);
  });

  // TaskReferenceLink: 1-N with StudyTask, cascade delete
  modelBuilder.Entity<TaskReferenceLink>(b =>
  {
      b.HasKey(l => l.Id);
      b.HasOne<StudyTask>()
       .WithMany()
       .HasForeignKey(l => l.MaTask)
       .OnDelete(DeleteBehavior.Cascade);
  });
  ```

**Verification:**
- [ ] `dotnet build` passes
- [ ] Delete `studyplanner.db`, launch app — two new tables visible in DB browser

---

## Task M6.1-5: Extend `IStudyRepository` with aggregate methods

**Context:** New methods for loading/saving the `TaskEditorBundle` and for individual CRUD on note/links. These are the only entry points for M6.1 data — no routing through `LuuHocKyAsync`.

**Files:**
- Modify: `SmartStudyPlanner/Data/IStudyRepository.cs`

**Steps:**
- [ ] Add to interface:
  ```csharp
  Task<TaskEditorBundle?> GetTaskEditorBundleAsync(Guid taskId);
  Task UpsertTaskNoteAsync(Guid taskId, string? content);
  Task<List<TaskReferenceLink>> GetTaskReferenceLinksAsync(Guid taskId);
  Task AddTaskReferenceLinkAsync(TaskReferenceLink link);
  Task UpdateTaskReferenceLinkAsync(TaskReferenceLink link);
  Task DeleteTaskReferenceLinkAsync(Guid linkId);
  Task SaveTaskEditorBundleAsync(TaskEditorBundle bundle);
  ```

**Verification:**
- [ ] `dotnet build` confirms no missing implementations yet (interface only here)

---

## Task M6.1-6: Implement aggregate methods in `StudyRepository`

**Context:** Implement each method added in M6.1-5. `SaveTaskEditorBundleAsync` handles the diff: update task core, upsert note, then reconcile links (add new, update changed, delete removed by comparing IDs). **Critical:** this must never touch the parent `HocKy` graph — only the task-level records.

**Files:**
- Modify: `SmartStudyPlanner/Data/StudyRepository.cs`

**Steps:**
- [ ] Implement `GetTaskEditorBundleAsync`:
  ```csharp
  public async Task<TaskEditorBundle?> GetTaskEditorBundleAsync(Guid taskId)
  {
      var task = await _ctx.Tasks.FindAsync(taskId);
      if (task is null) return null;
      var note = await _ctx.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId);
      var links = await _ctx.TaskReferenceLinks
          .Where(l => l.MaTask == taskId)
          .OrderBy(l => l.SortOrder)
          .ToListAsync();
      return new TaskEditorBundle { Task = task, Note = note, Links = links };
  }
  ```
- [ ] Implement `UpsertTaskNoteAsync`:
  ```csharp
  public async Task UpsertTaskNoteAsync(Guid taskId, string? content)
  {
      var note = await _ctx.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == taskId);
      if (note is null)
      {
          _ctx.TaskNotes.Add(new TaskNote { MaTask = taskId, Content = content });
      }
      else
      {
          note.Content = content;
          note.UpdatedAtUtc = DateTime.UtcNow;
      }
      await _ctx.SaveChangesAsync();
  }
  ```
- [ ] Implement `GetTaskReferenceLinksAsync`:
  ```csharp
  public Task<List<TaskReferenceLink>> GetTaskReferenceLinksAsync(Guid taskId) =>
      _ctx.TaskReferenceLinks
          .Where(l => l.MaTask == taskId)
          .OrderBy(l => l.SortOrder)
          .ToListAsync();
  ```
- [ ] Implement `AddTaskReferenceLinkAsync`:
  ```csharp
  public async Task AddTaskReferenceLinkAsync(TaskReferenceLink link)
  {
      _ctx.TaskReferenceLinks.Add(link);
      await _ctx.SaveChangesAsync();
  }
  ```
- [ ] Implement `UpdateTaskReferenceLinkAsync`:
  ```csharp
  public async Task UpdateTaskReferenceLinkAsync(TaskReferenceLink link)
  {
      _ctx.TaskReferenceLinks.Update(link);
      await _ctx.SaveChangesAsync();
  }
  ```
- [ ] Implement `DeleteTaskReferenceLinkAsync`:
  ```csharp
  public async Task DeleteTaskReferenceLinkAsync(Guid linkId)
  {
      var link = await _ctx.TaskReferenceLinks.FindAsync(linkId);
      if (link is not null)
      {
          _ctx.TaskReferenceLinks.Remove(link);
          await _ctx.SaveChangesAsync();
      }
  }
  ```
- [ ] Implement `SaveTaskEditorBundleAsync` (full diff reconcile):
  ```csharp
  public async Task SaveTaskEditorBundleAsync(TaskEditorBundle bundle)
  {
      // Update task core
      _ctx.Tasks.Update(bundle.Task);

      // Upsert note
      if (bundle.Note is not null)
          await UpsertTaskNoteAsync(bundle.Task.MaTask, bundle.Note.Content);

      // Reconcile links
      var existing = await _ctx.TaskReferenceLinks
          .Where(l => l.MaTask == bundle.Task.MaTask)
          .ToListAsync();
      var incomingIds = bundle.Links.Select(l => l.Id).ToHashSet();
      var existingIds = existing.Select(l => l.Id).ToHashSet();

      // Delete removed
      var toDelete = existing.Where(l => !incomingIds.Contains(l.Id)).ToList();
      _ctx.TaskReferenceLinks.RemoveRange(toDelete);

      // Add new / update changed
      foreach (var link in bundle.Links)
      {
          if (existingIds.Contains(link.Id))
              _ctx.TaskReferenceLinks.Update(link);
          else
              _ctx.TaskReferenceLinks.Add(link);
      }

      await _ctx.SaveChangesAsync();
  }
  ```

**Verification:**
- [ ] `dotnet build` passes
- [ ] Existing 128 tests still pass (`dotnet test`)

---

## Task M6.1-7: Extend `QuanLyTaskViewModel`

**Context:** Add `NoteContent`, `StudyLinks` collection, input fields for new link, and 5 commands. Load note/links via `GetTaskEditorBundleAsync` when editing an existing task. Save via `SaveTaskEditorBundleAsync`. Parser `PhanTichNhapNhanh` must remain unchanged (never set `NoteContent` or `StudyLinks`).

**Files:**
- Modify: `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs`

**Steps:**
- [ ] Add observable properties after existing core fields:
  ```csharp
  [ObservableProperty] private string? _noteContent;
  [ObservableProperty] private string _newLinkTitle = string.Empty;
  [ObservableProperty] private string _newLinkUrl = string.Empty;
  public ObservableCollection<TaskReferenceLinkItemVm> StudyLinks { get; } = [];
  ```
- [ ] Add `AddLinkCommand`:
  ```csharp
  [RelayCommand]
  private void AddLink()
  {
      if (string.IsNullOrWhiteSpace(NewLinkUrl)) return;
      StudyLinks.Add(new TaskReferenceLinkItemVm
      {
          MaTask = _editingTaskId ?? Guid.Empty,
          Title = string.IsNullOrWhiteSpace(NewLinkTitle) ? NewLinkUrl : NewLinkTitle,
          Url = NewLinkUrl,
          SortOrder = StudyLinks.Count,
      });
      NewLinkTitle = string.Empty;
      NewLinkUrl = string.Empty;
  }
  ```
- [ ] Add `RemoveLinkCommand`:
  ```csharp
  [RelayCommand]
  private void RemoveLink(TaskReferenceLinkItemVm item) => StudyLinks.Remove(item);
  ```
- [ ] Add `OpenLinkCommand`:
  ```csharp
  [RelayCommand]
  private void OpenLink(TaskReferenceLinkItemVm item)
  {
      if (!string.IsNullOrWhiteSpace(item.Url))
          Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
  }
  ```
- [ ] Add `CopyLinkCommand`:
  ```csharp
  [RelayCommand]
  private void CopyLink(TaskReferenceLinkItemVm item) => Clipboard.SetText(item.Url);
  ```
- [ ] Add `ClearNoteCommand`:
  ```csharp
  [RelayCommand]
  private void ClearNote() => NoteContent = null;
  ```
- [ ] Add `_editingTaskId` field (`Guid? _editingTaskId`) and populate it when user selects a task for editing (in the existing `ChinhSuaTask` or equivalent method).
- [ ] When loading an existing task for edit, call:
  ```csharp
  var bundle = await _repo.GetTaskEditorBundleAsync(task.MaTask);
  NoteContent = bundle?.Note?.Content;
  StudyLinks.Clear();
  if (bundle?.Links is { Count: > 0 } links)
      foreach (var l in links)
          StudyLinks.Add(TaskReferenceLinkItemVm.FromModel(l));
  ```
- [ ] In the save path, build and save the bundle:
  ```csharp
  var bundle = new TaskEditorBundle
  {
      Task = taskBeingSaved,
      Note = new TaskNote { MaTask = taskBeingSaved.MaTask, Content = NoteContent },
      Links = StudyLinks.Select((vm, i) => { vm.SortOrder = i; return vm.ToModel(); }).ToList(),
  };
  await _repo.SaveTaskEditorBundleAsync(bundle);
  ```
- [ ] Confirm `PhanTichNhapNhanh` does not set `NoteContent` or `StudyLinks` (read-verify, no edits needed if already isolated)

**Verification:**
- [ ] `dotnet build` passes
- [ ] Add a link, save task, re-open — link persists
- [ ] Quick-fill parser — note/links remain empty

---

## Task M6.1-8: Build 3-zone UI in `QuanLyTaskPage.xaml`

**Context:** Wrap existing flat StackPanel content in a `ScrollViewer`. Add two new `GroupBox` sections below the existing core form: one for notes (multiline `TextBox`), one for study links (input row + `ItemsControl`). Use consistent section headers matching the existing page style.

**Files:**
- Modify: `SmartStudyPlanner/Views/QuanLyTaskPage.xaml`

**Steps:**
- [ ] Wrap top-level content in a `ScrollViewer` if not already present.
- [ ] Add **Notes section** `GroupBox` below the existing submit button area:
  ```xml
  <GroupBox Header="Ghi Chú" Margin="0,12,0,0">
      <Grid>
          <Grid.RowDefinitions>
              <RowDefinition Height="*"/>
              <RowDefinition Height="Auto"/>
          </Grid.RowDefinitions>
          <TextBox Grid.Row="0"
                   Text="{Binding NoteContent, UpdateSourceTrigger=PropertyChanged}"
                   AcceptsReturn="True"
                   TextWrapping="Wrap"
                   MinHeight="80"
                   MaxHeight="200"
                   VerticalScrollBarVisibility="Auto"
                   Padding="6"/>
          <Button Grid.Row="1"
                  Content="Xóa ghi chú"
                  Command="{Binding ClearNoteCommand}"
                  HorizontalAlignment="Left"
                  Margin="0,4,0,0"/>
      </Grid>
  </GroupBox>
  ```
- [ ] Add **Study Links section** `GroupBox` below notes:
  ```xml
  <GroupBox Header="Liên Kết Học Tập" Margin="0,12,0,0">
      <StackPanel>
          <!-- Input row -->
          <Grid Margin="0,0,0,8">
              <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="Auto"/>
              </Grid.ColumnDefinitions>
              <TextBox Grid.Column="0"
                       Text="{Binding NewLinkTitle, UpdateSourceTrigger=PropertyChanged}"
                       Margin="0,0,4,0"
                       Tag="Tiêu đề (tùy chọn)"/>
              <TextBox Grid.Column="1"
                       Text="{Binding NewLinkUrl, UpdateSourceTrigger=PropertyChanged}"
                       Margin="0,0,4,0"
                       Tag="URL"/>
              <Button Grid.Column="2"
                      Content="Thêm"
                      Command="{Binding AddLinkCommand}"/>
          </Grid>
          <!-- Link list -->
          <ItemsControl ItemsSource="{Binding StudyLinks}">
              <ItemsControl.ItemTemplate>
                  <DataTemplate>
                      <Border BorderBrush="#DDDDDD" BorderThickness="0,0,0,1" Padding="4,6">
                          <Grid>
                              <Grid.ColumnDefinitions>
                                  <ColumnDefinition Width="*"/>
                                  <ColumnDefinition Width="Auto"/>
                                  <ColumnDefinition Width="Auto"/>
                                  <ColumnDefinition Width="Auto"/>
                              </Grid.ColumnDefinitions>
                              <StackPanel Grid.Column="0">
                                  <TextBlock Text="{Binding Title}" FontWeight="SemiBold"/>
                                  <TextBlock Text="{Binding Url}" Foreground="#0078D4"
                                             TextTrimming="CharacterEllipsis" FontSize="11"/>
                              </StackPanel>
                              <Button Grid.Column="1" Content="Mở"
                                      Command="{Binding DataContext.OpenLinkCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                      CommandParameter="{Binding}"
                                      Margin="4,0"/>
                              <Button Grid.Column="2" Content="Sao chép"
                                      Command="{Binding DataContext.CopyLinkCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                      CommandParameter="{Binding}"
                                      Margin="4,0"/>
                              <Button Grid.Column="3" Content="Xóa"
                                      Command="{Binding DataContext.RemoveLinkCommand,
                                          RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                      CommandParameter="{Binding}"
                                      Margin="4,0"/>
                          </Grid>
                      </Border>
                  </DataTemplate>
              </ItemsControl.ItemTemplate>
          </ItemsControl>
      </StackPanel>
  </GroupBox>
  ```

**Verification:**
- [ ] App builds and launches
- [ ] Task editor shows 3 clear zones: core form / notes / links
- [ ] Notes `TextBox` accepts multiline input
- [ ] Add link → appears in list; Open / Copy / Remove all function
- [ ] Parser quick-fill → notes and links remain empty

---

## Task M6.1-9: Write tests

**Context:** Add xUnit tests covering the new repository methods and ViewModel command behavior. Use an in-memory SQLite context (`:memory:`) for repo tests. Verify cascade delete.

**Files:**
- Create: `SmartStudyPlanner.Tests/TaskNotesTests.cs`

**Steps:**
- [ ] Add test class with in-memory `AppDbContext` fixture (same pattern as existing tests)
- [ ] Test: save task with note → load bundle → note persists
  ```csharp
  [Fact]
  public async Task UpsertTaskNote_ThenLoad_ReturnsNote()
  ```
- [ ] Test: save task with 3 links → load bundle → 3 links in order
  ```csharp
  [Fact]
  public async Task AddLinks_ThenLoadBundle_ReturnsAllLinks()
  ```
- [ ] Test: delete task → `TaskNote` and `TaskReferenceLink` rows also deleted (cascade)
  ```csharp
  [Fact]
  public async Task DeleteTask_CascadesToNoteAndLinks()
  ```
- [ ] Test: `SaveTaskEditorBundleAsync` diff — remove one link from bundle, save → that link gone from DB
  ```csharp
  [Fact]
  public async Task SaveBundle_RemovesDeletedLinks()
  ```
- [ ] Test: `AddLinkCommand` on ViewModel — `StudyLinks.Count` increases; parser (`PhanTichNhapNhanh`) does not change `StudyLinks`
- [ ] Test: empty note (null) → `UpsertTaskNoteAsync` does not throw

**Verification:**
- [ ] `dotnet test` — all new tests pass; existing 128 tests still pass

---

## Done Criteria

M6.1 is complete when:
- [ ] `TaskNote` and `TaskReferenceLink` tables exist in the DB with cascade delete
- [ ] `GetTaskEditorBundleAsync` / `SaveTaskEditorBundleAsync` work correctly
- [ ] `QuanLyTaskViewModel` has `NoteContent`, `StudyLinks`, and 5 link commands
- [ ] Task editor page shows 3 visually separated zones
- [ ] Quick-fill parser (`PhanTichNhapNhanh`) does not touch note/link state
- [ ] Existing task CRUD continues to work
- [ ] All tests pass (`dotnet test`)
