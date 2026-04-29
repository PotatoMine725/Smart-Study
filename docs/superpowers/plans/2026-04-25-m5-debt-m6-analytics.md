# M5 Technical Debt + M6 Study Analytics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repay 4 M5 pipeline technical debts (magic strings, duplicate computation, unused output), then implement M6 Study Analytics page with LiveCharts2 charts and productivity metrics, and add a lightweight note/link area inside task management for study notes, homework references, and lesson video links.

**Architecture:**
- M5 debt: extract `StudyTaskStatus` constants; add `HocKy.NgayKetThuc` [NotMapped]; add `TaskId` to `RiskAssessment` and wire pipeline output into `BuildDashboardSummary` to eliminate duplicate priority+risk computation; surface `Adaptations` on Dashboard UI.
- M6: add `StudyLog` entity for session tracking, `IStudyAnalytics` service for weekly/subject/productivity metrics, `AnalyticsViewModel`, `AnalyticsPage.xaml` with three LiveCharts2 charts, hooked into sidebar navigation.
- Task management enhancement: add a compact notes/reference area on the task/deadline screen so each task can store freeform notes plus multiple pasted URLs (exercise links, lesson videos, reference materials).

**Tech Stack:** C# 12, .NET 10, WPF, EF Core 9 / SQLite (EnsureCreated), LiveCharts2 (LiveChartsCore.SkiaSharpView.WPF), CommunityToolkit.Mvvm, xUnit

---

## File Map

### Part 1 — M5 Technical Debt

| Action | Path |
|--------|------|
| Create | `SmartStudyPlanner/Models/StudyTaskStatus.cs` |
| Modify | `SmartStudyPlanner/Models/StudyTask.cs` |
| Modify | `SmartStudyPlanner/Models/HocKy.cs` |
| Modify | `SmartStudyPlanner/Services/RiskAnalyzer/RiskAssessment.cs` |
| Modify | `SmartStudyPlanner/Services/Pipeline/Stages/PrioritizeStage.cs` |
| Modify | `SmartStudyPlanner/Services/Pipeline/Stages/AssessRiskStage.cs` |
| Modify | `SmartStudyPlanner/Services/Pipeline/Stages/AdaptStage.cs` |
| Modify | `SmartStudyPlanner/ViewModels/DashboardViewModel.cs` |
| Modify | `SmartStudyPlanner/ViewModels/FocusViewModel.cs` |
| Modify | `SmartStudyPlanner/Views/MainWindow.xaml.cs` |
| Modify | `SmartStudyPlanner/Views/DashboardPage.xaml` |
| Modify | `SmartStudyPlanner.Tests/PipelineStageTests.cs` |

### Part 2 — M6 Study Analytics

| Action | Path |
|--------|------|
| Create | `SmartStudyPlanner/Models/StudyLog.cs` |
| Modify | `SmartStudyPlanner/Models/StudyTask.cs` |
| Modify | `SmartStudyPlanner/Data/IStudyRepository.cs` |
| Modify | `SmartStudyPlanner/Data/StudyRepository.cs` |
| Modify | `SmartStudyPlanner/Data/AppDbContext.cs` |
| Create | `SmartStudyPlanner/Services/Analytics/IStudyAnalytics.cs` |
| Create | `SmartStudyPlanner/Services/Analytics/StudyAnalyticsService.cs` |
| Create | `SmartStudyPlanner/Services/Analytics/Models/WeeklyReport.cs` |
| Create | `SmartStudyPlanner/Services/Analytics/Models/SubjectInsight.cs` |
| Create | `SmartStudyPlanner/Services/Analytics/Models/ProductivityScore.cs` |
| Modify | `SmartStudyPlanner/ViewModels/FocusViewModel.cs` |
| Create | `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs` |
| Create | `SmartStudyPlanner/Views/AnalyticsPage.xaml` |
| Create | `SmartStudyPlanner/Views/AnalyticsPage.xaml.cs` |
| Modify | `SmartStudyPlanner/Views/MainWindow.xaml` |
| Modify | `SmartStudyPlanner/Views/MainWindow.xaml.cs` |
| Modify | `SmartStudyPlanner/App.xaml.cs` |
| Create | `SmartStudyPlanner.Tests/AnalyticsServiceTests.cs` |

### Part 3 — Task Notes & Reference Links

| Action | Path |
|--------|------|
| Modify | `SmartStudyPlanner/Models/StudyTask.cs` |
| Modify | `SmartStudyPlanner/Views/QuanLyTaskPage.xaml` |
| Modify | `SmartStudyPlanner/Views/QuanLyTaskPage.xaml.cs` |
| Modify | `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs` |
| Modify | `SmartStudyPlanner/Data/IStudyRepository.cs` |
| Modify | `SmartStudyPlanner/Data/StudyRepository.cs` |
| Modify | `SmartStudyPlanner/Data/AppDbContext.cs` |
| Create | `SmartStudyPlanner/Models/TaskReferenceLink.cs` |
| Create | `SmartStudyPlanner.Tests/TaskNotesTests.cs` |

---

## Task TD-1: Extract StudyTaskStatus Constants

**Context:** The string literal `"Hoàn thành"` appears in 7 places across 6 files (PrioritizeStage×2, AssessRiskStage, AdaptStage, DashboardViewModel×2, FocusViewModel, MainWindow.xaml.cs). `"Chưa làm"` appears in the StudyTask constructor. A typo in any of these silently breaks logic. Extracting to compile-time constants forces correctness and makes future status changes a one-line edit.

**Files:**
- Create: `SmartStudyPlanner/Models/StudyTaskStatus.cs`
- Modify: `SmartStudyPlanner/Models/StudyTask.cs:44`
- Modify: `SmartStudyPlanner/Services/Pipeline/Stages/PrioritizeStage.cs:48,54`
- Modify: `SmartStudyPlanner/Services/Pipeline/Stages/AssessRiskStage.cs:46`
- Modify: `SmartStudyPlanner/Services/Pipeline/Stages/AdaptStage.cs:43`
- Modify: `SmartStudyPlanner/ViewModels/DashboardViewModel.cs:133,138,255`
- Modify: `SmartStudyPlanner/ViewModels/FocusViewModel.cs:104`
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml.cs:94`

- [ ] **Step 1: Create StudyTaskStatus.cs**

```csharp
// SmartStudyPlanner/Models/StudyTaskStatus.cs
namespace SmartStudyPlanner.Models
{
    public static class StudyTaskStatus
    {
        public const string ChuaLam   = "Chưa làm";
        public const string HoanThanh = "Hoàn thành";
    }
}
```

- [ ] **Step 2: Build to verify file compiles**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Replace magic strings in StudyTask.cs constructor (line 44)**

Change:
```csharp
TrangThai = "Chưa làm";
```
To:
```csharp
TrangThai = StudyTaskStatus.ChuaLam;
```

- [ ] **Step 4: Replace magic strings in PrioritizeStage.cs (lines 48, 54)**

Change line 48:
```csharp
if (task.TrangThai == "Hoàn thành") continue;
```
To:
```csharp
if (task.TrangThai == StudyTaskStatus.HoanThanh) continue;
```

Change line 54:
```csharp
.Where(t => t.TrangThai != "Hoàn thành")
```
To:
```csharp
.Where(t => t.TrangThai != StudyTaskStatus.HoanThanh)
```

Add at top: `using SmartStudyPlanner.Models;`

- [ ] **Step 5: Replace magic strings in AssessRiskStage.cs (line 46)**

Change:
```csharp
if (task.TrangThai == "Hoàn thành") continue;
```
To:
```csharp
if (task.TrangThai == StudyTaskStatus.HoanThanh) continue;
```
Add using if missing: `using SmartStudyPlanner.Models;`

- [ ] **Step 6: Replace magic strings in AdaptStage.cs (line 43)**

Change:
```csharp
var completed = mon.DanhSachTask.Count(t => t.TrangThai == "Hoàn thành");
```
To:
```csharp
var completed = mon.DanhSachTask.Count(t => t.TrangThai == StudyTaskStatus.HoanThanh);
```

- [ ] **Step 7: Replace magic strings in DashboardViewModel.cs (lines 133, 138, 255)**

Line 133: `task.TrangThai == "Hoàn thành"` → `task.TrangThai == StudyTaskStatus.HoanThanh`
Line 138: `task.TrangThai != "Hoàn thành"` → `task.TrangThai != StudyTaskStatus.HoanThanh`
Line 255 (GetWarningLevel): `if (task.TrangThai == "Hoàn thành")` → `if (task.TrangThai == StudyTaskStatus.HoanThanh)`

- [ ] **Step 8: Replace magic string in FocusViewModel.cs (line 104)**

Change:
```csharp
TaskHienTai.TaskGoc.TrangThai = "Hoàn thành";
```
To:
```csharp
TaskHienTai.TaskGoc.TrangThai = StudyTaskStatus.HoanThanh;
```
Add using: `using SmartStudyPlanner.Models;`

- [ ] **Step 9: Replace magic string in MainWindow.xaml.cs (line 94)**

Change:
```csharp
if (task.TrangThai != "Hoàn thành")
```
To:
```csharp
if (task.TrangThai != StudyTaskStatus.HoanThanh)
```
Add using: `using SmartStudyPlanner.Models;`

- [ ] **Step 10: Build and run all tests**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5 && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `Build succeeded. 0 Error(s)`, `119 passed`

- [ ] **Step 11: Commit**

```bash
git add SmartStudyPlanner/Models/StudyTaskStatus.cs SmartStudyPlanner/Models/StudyTask.cs SmartStudyPlanner/Services/Pipeline/Stages/PrioritizeStage.cs SmartStudyPlanner/Services/Pipeline/Stages/AssessRiskStage.cs SmartStudyPlanner/Services/Pipeline/Stages/AdaptStage.cs SmartStudyPlanner/ViewModels/DashboardViewModel.cs SmartStudyPlanner/ViewModels/FocusViewModel.cs SmartStudyPlanner/Views/MainWindow.xaml.cs
git commit -m "refactor(M5-debt): extract StudyTaskStatus constants, replace all magic status strings"
```

---

## Task TD-2: Add HocKy.NgayKetThuc [NotMapped] + Fix AdaptStage Semester Duration

**Context:** `AdaptStage` uses `AssumedSemesterDays = 120` as a hardcoded constant because `HocKy` has no end date. This makes the expected-progress calculation inaccurate for semesters shorter or longer than 120 days. Adding `NgayKetThuc` as a `[NotMapped]` property avoids any DB schema change (EnsureCreated won't break existing databases) while letting callers set the real end date. The `HocKy(string, DateTime)` constructor will default it to `NgayBatDau + 120 days` for backward compatibility.

**Files:**
- Modify: `SmartStudyPlanner/Models/HocKy.cs` — add `NgayKetThuc` property
- Modify: `SmartStudyPlanner/Services/Pipeline/Stages/AdaptStage.cs` — use `semester.NgayKetThuc`
- Test: `SmartStudyPlanner.Tests/PipelineStageTests.cs` — add AdaptStage with real end date

- [ ] **Step 1: Write the failing test**

In `PipelineStageTests.cs`, add a new test after the existing ones:

```csharp
[Fact]
public void AdaptStage_Uses_NgayKetThuc_WhenSet()
{
    // 10-day semester; today is day 5 → expectedProgress = 50%
    // 0 tasks completed out of 4 → progress = 0% → below threshold → suggestion generated
    var start = new DateTime(2026, 1, 1);
    var hocKy = new HocKy("HK-Test", start);
    hocKy.NgayKetThuc = start.AddDays(10);

    var mon = new MonHoc("CNTT", 3) { MaHocKy = hocKy.MaHocKy };
    for (int i = 0; i < 4; i++)
        mon.DanhSachTask.Add(new StudyTask($"T{i}", start.AddDays(15), LoaiCongViec.BaiTapVeNha, 1));
    hocKy.DanhSachMonHoc.Add(mon);

    var ctx = new PipelineContext
    {
        Semester = hocKy,
        ReferenceTime = new DateTimeOffset(new DateTime(2026, 1, 6)) // day 5
    };
    var stage = new AdaptStage();

    var result = stage.Execute(ctx);

    Assert.True(result.Success);
    Assert.NotEmpty(ctx.Adaptations!);
    Assert.Contains(ctx.Adaptations!, a => a.RuleKey == "progress_below_expected");
}
```

Required usings already present in PipelineStageTests.cs; add `using SmartStudyPlanner.Services.Pipeline.Stages;` if missing.

- [ ] **Step 2: Run test to verify it fails**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "AdaptStage_Uses_NgayKetThuc" 2>&1 | tail -15
```
Expected: FAIL — `HocKy` has no `NgayKetThuc`

- [ ] **Step 3: Add NgayKetThuc to HocKy.cs**

Add using at top: `using System.ComponentModel.DataAnnotations.Schema;`

Add inside the class, after `public DateTime NgayBatDau { get; set; }`:

```csharp
[NotMapped]
public DateTime NgayKetThuc { get; set; }
```

In the parameterless constructor, do not set it (defaults to `default(DateTime)`, handled in AdaptStage).

In the parameterized constructor `HocKy(string ten, DateTime ngayBatDau)`, after `NgayBatDau = ngayBatDau;`, add:
```csharp
NgayKetThuc = ngayBatDau.AddDays(120);
```

- [ ] **Step 4: Update AdaptStage.cs to use NgayKetThuc**

Replace:
```csharp
private const int AssumedSemesterDays = 120;
// ...
var totalDays = Math.Max(1, AssumedSemesterDays);
```
With:
```csharp
private const int FallbackSemesterDays = 120;
// ...
var end = semester.NgayKetThuc != default
    ? semester.NgayKetThuc.Date
    : start.AddDays(FallbackSemesterDays);
var totalDays = Math.Max(1, (end - start).Days);
```

- [ ] **Step 5: Run failing test — verify it passes**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "AdaptStage_Uses_NgayKetThuc" 2>&1 | tail -10
```
Expected: PASS

- [ ] **Step 6: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `120 passed`

- [ ] **Step 7: Commit**

```bash
git add SmartStudyPlanner/Models/HocKy.cs SmartStudyPlanner/Services/Pipeline/Stages/AdaptStage.cs SmartStudyPlanner.Tests/PipelineStageTests.cs
git commit -m "refactor(M5-debt): add HocKy.NgayKetThuc [NotMapped], AdaptStage uses real semester end date with FallbackSemesterDays"
```

---

## Task TD-3: Eliminate Duplicate Pipeline Computation

**Context:** `PrioritizeStage.Execute()` already sets `task.DiemUuTien` for every non-completed task (line 49). `AssessRiskStage.Execute()` already calls `_riskAnalyzer.Assess()` for every task (result in `pipelineResult.RiskReport`). Yet `BuildDashboardSummary` re-runs both calls from scratch, ignoring the pipeline output. Fix:
1. Add `Guid TaskId` to `RiskAssessment` so it can be looked up by task.
2. `AssessRiskStage` wraps the RiskAnalyzer result and sets `TaskId = task.MaTask`.
3. `BuildDashboardSummary` accepts `PipelineExecutionResult`, removes `_decisionEngine.CalculatePriority()` call, looks up risk by task ID from `pipelineResult.RiskReport` with fallback to direct call if pipeline was skipped.

**Files:**
- Modify: `SmartStudyPlanner/Services/RiskAnalyzer/RiskAssessment.cs` — add `TaskId`
- Modify: `SmartStudyPlanner/Services/Pipeline/Stages/AssessRiskStage.cs` — set `TaskId`
- Modify: `SmartStudyPlanner/ViewModels/DashboardViewModel.cs` — refactor `BuildDashboardSummary`
- Test: `SmartStudyPlanner.Tests/PipelineStageTests.cs` — verify AssessRiskStage sets TaskId

- [ ] **Step 1: Write failing test**

In `PipelineStageTests.cs`, add:

```csharp
[Fact]
public void AssessRiskStage_SetsTaskId_OnEveryAssessment()
{
    var start = new DateTime(2026, 1, 1);
    var hocKy = new HocKy("HK-Test", start);
    var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
    var t1 = new StudyTask("Bài 1", start.AddDays(7), LoaiCongViec.BaiTapVeNha, 2);
    var t2 = new StudyTask("Bài 2", start.AddDays(3), LoaiCongViec.KiemTraThuongXuyen, 3);
    mon.DanhSachTask.Add(t1);
    mon.DanhSachTask.Add(t2);
    hocKy.DanhSachMonHoc.Add(mon);

    var riskAnalyzer = ServiceLocator.Get<IRiskAnalyzer>();
    var stage = new AssessRiskStage(riskAnalyzer);
    var ctx = new PipelineContext
    {
        Semester = hocKy,
        Settings = new PipelineUserSettings { EnableRiskAssessment = true },
        ReferenceTime = DateTimeOffset.Now
    };

    stage.Execute(ctx);

    Assert.NotNull(ctx.RiskReport);
    Assert.Equal(2, ctx.RiskReport!.Count);
    Assert.All(ctx.RiskReport, r => Assert.NotEqual(Guid.Empty, r.TaskId));
    Assert.Contains(ctx.RiskReport, r => r.TaskId == t1.MaTask);
    Assert.Contains(ctx.RiskReport, r => r.TaskId == t2.MaTask);
}
```

- [ ] **Step 2: Run test to verify it fails**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "AssessRiskStage_SetsTaskId" 2>&1 | tail -15
```
Expected: FAIL — `RiskAssessment` has no `TaskId`

- [ ] **Step 3: Add TaskId to RiskAssessment.cs**

Add before `public double Score { get; init; }`:
```csharp
public Guid TaskId { get; init; }
```

- [ ] **Step 4: Update AssessRiskStage.Execute() to set TaskId**

Replace:
```csharp
assessments.Add(_riskAnalyzer.Assess(task, mon));
```
With:
```csharp
var r = _riskAnalyzer.Assess(task, mon);
assessments.Add(new RiskAssessment
{
    TaskId                = task.MaTask,
    Score                 = r.Score,
    Level                 = r.Level,
    DeadlineUrgencyScore  = r.DeadlineUrgencyScore,
    ProgressGapScore      = r.ProgressGapScore,
    PerformanceDropScore  = r.PerformanceDropScore
});
```

- [ ] **Step 5: Run TaskId test — verify it passes**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "AssessRiskStage_SetsTaskId" 2>&1 | tail -10
```
Expected: PASS

- [ ] **Step 6: Refactor BuildDashboardSummary in DashboardViewModel.cs**

Change `LoadDuLieuDashboard` line 97 from:
```csharp
var summary = BuildDashboardSummary(pipelineResult.Schedule.FirstOrDefault());
```
To:
```csharp
var summary = BuildDashboardSummary(pipelineResult);
```

Change `BuildDashboardSummary` signature and first two lines:
```csharp
private DashboardSummary BuildDashboardSummary(PipelineExecutionResult pipelineResult)
{
    var todaySchedule = pipelineResult.Schedule.FirstOrDefault();
    var riskById = pipelineResult.RiskReport.ToDictionary(r => r.TaskId);
    int tongSoMon = _hocKyHienTai.DanhSachMonHoc.Count;
    // ... rest of method unchanged until the two lines below
```

Inside the task loop, remove:
```csharp
task.DiemUuTien = _decisionEngine.CalculatePriority(task, mon);
```
(PrioritizeStage already set it; remove this line entirely.)

Replace:
```csharp
var risk = _riskAnalyzer.Assess(task, mon);
```
With:
```csharp
var risk = riskById.TryGetValue(task.MaTask, out var cached)
    ? cached
    : _riskAnalyzer.Assess(task, mon); // fallback: pipeline was skipped
```

- [ ] **Step 7: Build — verify no errors**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `121 passed`

- [ ] **Step 9: Commit**

```bash
git add SmartStudyPlanner/Services/RiskAnalyzer/RiskAssessment.cs SmartStudyPlanner/Services/Pipeline/Stages/AssessRiskStage.cs SmartStudyPlanner/ViewModels/DashboardViewModel.cs SmartStudyPlanner.Tests/PipelineStageTests.cs
git commit -m "refactor(M5-debt): wire pipelineResult.RiskReport into BuildDashboardSummary, eliminate duplicate priority+risk computation"
```

---

## Task TD-4: Surface Adaptation Suggestions on Dashboard

**Context:** `pipelineResult.Adaptations` contains rule-based study suggestions (e.g., "CNTT: progress thấp hơn expected, nên tăng priority") from `AdaptStage` but is never displayed. Adding a collapsible "GỢI Ý THÍCH NGHI" section at the bottom of the Dashboard gives users actionable insights without cluttering the default view.

**Files:**
- Modify: `SmartStudyPlanner/ViewModels/DashboardViewModel.cs` — add `AdaptationItems` + `HasAdaptations`
- Modify: `SmartStudyPlanner/Views/DashboardPage.xaml` — add adaptations section at bottom

- [ ] **Step 1: Add AdaptationItems to DashboardViewModel**

After the existing `[ObservableProperty]` block, add:
```csharp
[ObservableProperty] private ObservableCollection<AdaptationSuggestion> adaptationItems = new();
public bool HasAdaptations => AdaptationItems.Count > 0;
```
Add using at top: `using SmartStudyPlanner.Services.Pipeline;`

Add a new private method:
```csharp
private void ApplyAdaptations(IReadOnlyList<AdaptationSuggestion> adaptations)
{
    AdaptationItems.Clear();
    foreach (var a in adaptations) AdaptationItems.Add(a);
}
```

In `LoadDuLieuDashboard()`, after `ApplySchedule(summary.ScheduleDay)`:
```csharp
ApplyAdaptations(pipelineResult.Adaptations);
OnPropertyChanged(nameof(HasAdaptations));
```

- [ ] **Step 2: Build to verify no errors**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Add adaptations section to DashboardPage.xaml**

After the closing `</Border>` of the "TIẾN ĐỘ THỜI GIAN HỌC" chart (the last `Height="250"` Border before `</StackPanel>`), add:

```xml
<!-- Adaptations — collapsed when no suggestions -->
<StackPanel Orientation="Horizontal" Margin="0,20,0,10">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Visible"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasAdaptations}" Value="False">
                    <Setter Property="Visibility" Value="Collapsed"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBlock Text="&#xE946;" FontFamily="Segoe MDL2 Assets" FontSize="15"
               Foreground="{DynamicResource AccentColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
    <TextBlock Text="GỢI Ý THÍCH NGHI" Style="{StaticResource SectionHeader}"
               Foreground="{DynamicResource AccentColor}" Margin="0"/>
</StackPanel>

<ItemsControl ItemsSource="{Binding AdaptationItems}" Margin="0,0,0,10">
    <ItemsControl.Style>
        <Style TargetType="ItemsControl">
            <Setter Property="Visibility" Value="Visible"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasAdaptations}" Value="False">
                    <Setter Property="Visibility" Value="Collapsed"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </ItemsControl.Style>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Background="{DynamicResource CardBackground}" CornerRadius="8" Padding="12,10"
                    Margin="0,0,0,6" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="&#xE8CA;" FontFamily="Segoe MDL2 Assets" FontSize="14"
                               Foreground="{DynamicResource AccentColor}" VerticalAlignment="Center" Margin="0,0,10,0"/>
                    <TextBlock Text="{Binding Message}" FontSize="13" Foreground="{DynamicResource PrimaryText}"
                               TextWrapping="Wrap"/>
                </StackPanel>
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

- [ ] **Step 4: Build and run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5 && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `Build succeeded`, `121 passed`

- [ ] **Step 5: Commit**

```bash
git add SmartStudyPlanner/ViewModels/DashboardViewModel.cs SmartStudyPlanner/Views/DashboardPage.xaml
git commit -m "feat(M5-debt): surface pipeline adaptation suggestions on Dashboard UI"
```

---

## Task M6-1: StudyLog Entity + NgayHoanThanh on StudyTask + DB Registration

**Context:** Analytics needs time-series study data. `StudyLog` tracks each FocusMode session (task ID, date, minutes, completion). `StudyTask.NgayHoanThanh` (nullable DateTime) records when a task was marked complete — enables "time to completion" analytics. Both are added as nullable columns. Since we use `EnsureCreated()`, existing DB files won't have these columns but EF Core maps missing nullable columns to `null` without error.

**Files:**
- Create: `SmartStudyPlanner/Models/StudyLog.cs`
- Modify: `SmartStudyPlanner/Models/StudyTask.cs` — add `NgayHoanThanh`
- Modify: `SmartStudyPlanner/Data/AppDbContext.cs` — add `StudyLogs` DbSet
- Create: `SmartStudyPlanner.Tests/AnalyticsServiceTests.cs` — two model tests

- [ ] **Step 1: Write the failing tests**

Create new file `SmartStudyPlanner.Tests/AnalyticsServiceTests.cs`:

```csharp
using System;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests
{
    public class AnalyticsServiceTests
    {
        [Fact]
        public void StudyLog_HasRequiredProperties_AndGeneratesId()
        {
            var log = new StudyLog
            {
                MaTask       = Guid.NewGuid(),
                NgayHoc      = DateTime.Today,
                SoPhutHoc    = 25,
                SoPhutDuKien = 30,
                DaHoanThanh  = true
            };
            Assert.Equal(25, log.SoPhutHoc);
            Assert.True(log.DaHoanThanh);
            Assert.NotEqual(Guid.Empty, log.Id);
        }

        [Fact]
        public void StudyTask_NgayHoanThanh_IsNullByDefault()
        {
            var task = new StudyTask("Test", DateTime.Today, LoaiCongViec.BaiTapVeNha, 1);
            Assert.Null(task.NgayHoanThanh);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "StudyLog_HasRequired|StudyTask_NgayHoanThanh" 2>&1 | tail -15
```
Expected: FAIL — `StudyLog` type not found

- [ ] **Step 3: Create StudyLog.cs**

```csharp
// SmartStudyPlanner/Models/StudyLog.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Models
{
    public class StudyLog
    {
        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MaTask       { get; set; }
        public DateTime NgayHoc  { get; set; }
        public int SoPhutHoc     { get; set; }
        public int SoPhutDuKien  { get; set; }
        public bool DaHoanThanh  { get; set; }
        public string? GhiChu    { get; set; }
    }
}
```

- [ ] **Step 4: Add NgayHoanThanh to StudyTask.cs**

After `public int ThoiGianDaHoc { get; set; } = 0;`, add:
```csharp
public DateTime? NgayHoanThanh { get; set; }
```

- [ ] **Step 5: Add StudyLogs DbSet to AppDbContext.cs**

After `public DbSet<StudyTask> StudyTasks { get; set; }`, add:
```csharp
public DbSet<StudyLog> StudyLogs { get; set; }
```

- [ ] **Step 6: Run tests — verify they pass**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "StudyLog_HasRequired|StudyTask_NgayHoanThanh" 2>&1 | tail -10
```
Expected: PASS

- [ ] **Step 7: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `123 passed`

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner/Models/StudyLog.cs SmartStudyPlanner/Models/StudyTask.cs SmartStudyPlanner/Data/AppDbContext.cs SmartStudyPlanner.Tests/AnalyticsServiceTests.cs
git commit -m "feat(M6): add StudyLog entity, StudyTask.NgayHoanThanh, register DbSet<StudyLog>"
```

---

## Task M6-2: IStudyAnalytics Interface + StudyAnalyticsService

**Context:** The analytics service computes three metrics:
- **Weekly minutes** — sums `StudyLog.SoPhutHoc` for each of the last 7 days.
- **Subject insights** — completion rate and total study minutes per subject from tasks + logs.
- **Productivity score** — weighted formula: `completionRate×50 + streakFactor×30 + timeEfficiency×20`, capped 0–100.

All inputs are plain in-memory objects (no DB calls inside the service itself), making it fully unit-testable.

**Files:**
- Create: `SmartStudyPlanner/Services/Analytics/Models/WeeklyReport.cs`
- Create: `SmartStudyPlanner/Services/Analytics/Models/SubjectInsight.cs`
- Create: `SmartStudyPlanner/Services/Analytics/Models/ProductivityScore.cs`
- Create: `SmartStudyPlanner/Services/Analytics/IStudyAnalytics.cs`
- Create: `SmartStudyPlanner/Services/Analytics/StudyAnalyticsService.cs`
- Modify: `SmartStudyPlanner.Tests/AnalyticsServiceTests.cs` — add 4 computation tests

- [ ] **Step 1: Write failing tests**

Add to `AnalyticsServiceTests.cs` (after the two existing tests):

```csharp
[Fact]
public void ComputeWeeklyMinutes_Returns7Entries_WithCorrectTotals()
{
    var today = new DateTime(2026, 1, 7);
    var logs = new List<StudyLog>
    {
        new() { MaTask = Guid.NewGuid(), NgayHoc = today,               SoPhutHoc = 30 },
        new() { MaTask = Guid.NewGuid(), NgayHoc = today,               SoPhutHoc = 25 },
        new() { MaTask = Guid.NewGuid(), NgayHoc = today.AddDays(-1),   SoPhutHoc = 50 },
        new() { MaTask = Guid.NewGuid(), NgayHoc = today.AddDays(-8),   SoPhutHoc = 60 } // outside window
    };
    var service = new StudyAnalyticsService();

    var result = service.ComputeWeeklyMinutes(logs, today);

    Assert.Equal(7, result.DayLabels.Count);
    Assert.Equal(7, result.MinutesPerDay.Count);
    Assert.Equal(55, result.MinutesPerDay[6]); // today: 30+25
    Assert.Equal(50, result.MinutesPerDay[5]); // yesterday: 50
    Assert.Equal(0,  result.MinutesPerDay[0]); // 6 days ago: nothing
}

[Fact]
public void ComputeProductivityScore_IsZero_WhenNoData()
{
    var service = new StudyAnalyticsService();
    var score = service.ComputeProductivityScore(0, 0, 0);
    Assert.Equal(0, score.Value);
}

[Fact]
public void ComputeProductivityScore_IsHigh_WhenPerfect()
{
    var service = new StudyAnalyticsService();
    var score = service.ComputeProductivityScore(1.0, 30, 1.0);
    Assert.InRange(score.Value, 95, 100);
}

[Fact]
public void ComputeSubjectInsights_ReturnsCorrectCompletionRate()
{
    var hocKy = new HocKy("HK1", DateTime.Today);
    var mon   = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
    var t1 = new StudyTask("T1", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 1);
    t1.TrangThai = StudyTaskStatus.HoanThanh;
    var t2 = new StudyTask("T2", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 1);
    mon.DanhSachTask.Add(t1);
    mon.DanhSachTask.Add(t2);
    hocKy.DanhSachMonHoc.Add(mon);

    var service   = new StudyAnalyticsService();
    var insights  = service.ComputeSubjectInsights(hocKy, new List<StudyLog>());

    Assert.Single(insights);
    Assert.Equal(0.5, insights[0].CompletionRate, precision: 2);
    Assert.Equal("Toán", insights[0].SubjectName);
    Assert.Equal(1, insights[0].CompletedTaskCount);
}
```

Add using: `using System.Collections.Generic; using SmartStudyPlanner.Services.Analytics;`

- [ ] **Step 2: Run tests to verify they fail**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "ComputeWeekly|ComputeProductivity|ComputeSubject" 2>&1 | tail -15
```
Expected: FAIL — service not found

- [ ] **Step 3: Create the three DTO models**

```csharp
// SmartStudyPlanner/Services/Analytics/Models/WeeklyReport.cs
using System.Collections.Generic;
using System.Linq;
namespace SmartStudyPlanner.Services.Analytics.Models
{
    public sealed class WeeklyReport
    {
        public List<string> DayLabels    { get; init; } = new();
        public List<int>    MinutesPerDay { get; init; } = new();
        public int TotalMinutes => MinutesPerDay.Sum();
    }
}

// SmartStudyPlanner/Services/Analytics/Models/SubjectInsight.cs
namespace SmartStudyPlanner.Services.Analytics.Models
{
    public sealed class SubjectInsight
    {
        public string SubjectName       { get; init; } = string.Empty;
        public int    TotalTaskCount    { get; init; }
        public int    CompletedTaskCount { get; init; }
        public double CompletionRate    { get; init; }  // [0.0, 1.0]
        public int    TotalStudyMinutes { get; init; }
    }
}

// SmartStudyPlanner/Services/Analytics/Models/ProductivityScore.cs
namespace SmartStudyPlanner.Services.Analytics.Models
{
    public sealed class ProductivityScore
    {
        public int Value { get; init; }  // [0, 100]
        public string Label => Value switch
        {
            >= 85 => "Xuất sắc",
            >= 70 => "Tốt",
            >= 50 => "Trung bình",
            >= 30 => "Cần cải thiện",
            _     => "Chưa có dữ liệu"
        };
    }
}
```

- [ ] **Step 4: Create IStudyAnalytics.cs**

```csharp
// SmartStudyPlanner/Services/Analytics/IStudyAnalytics.cs
using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics.Models;

namespace SmartStudyPlanner.Services.Analytics
{
    public interface IStudyAnalytics
    {
        WeeklyReport       ComputeWeeklyMinutes(IEnumerable<StudyLog> logs, DateTime referenceDate);
        List<SubjectInsight> ComputeSubjectInsights(HocKy hocKy, IEnumerable<StudyLog> logs);
        ProductivityScore  ComputeProductivityScore(double completionRate, int streakDays, double timeEfficiency);
    }
}
```

- [ ] **Step 5: Create StudyAnalyticsService.cs**

```csharp
// SmartStudyPlanner/Services/Analytics/StudyAnalyticsService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics.Models;

namespace SmartStudyPlanner.Services.Analytics
{
    public sealed class StudyAnalyticsService : IStudyAnalytics
    {
        public WeeklyReport ComputeWeeklyMinutes(IEnumerable<StudyLog> logs, DateTime referenceDate)
        {
            var logList = logs.ToList();
            var labels  = new List<string>();
            var minutes = new List<int>();

            for (int i = 6; i >= 0; i--)
            {
                var day = referenceDate.Date.AddDays(-i);
                labels.Add(day.ToString("ddd dd/MM"));
                minutes.Add(logList.Where(l => l.NgayHoc.Date == day).Sum(l => l.SoPhutHoc));
            }

            return new WeeklyReport { DayLabels = labels, MinutesPerDay = minutes };
        }

        public List<SubjectInsight> ComputeSubjectInsights(HocKy hocKy, IEnumerable<StudyLog> logs)
        {
            var logsByTask = logs
                .GroupBy(l => l.MaTask)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.SoPhutHoc));

            return hocKy.DanhSachMonHoc.Select(mon =>
            {
                var tasks     = mon.DanhSachTask.ToList();
                var completed = tasks.Count(t => t.TrangThai == StudyTaskStatus.HoanThanh);
                var studied   = tasks.Sum(t =>
                    logsByTask.TryGetValue(t.MaTask, out var m) ? m : t.ThoiGianDaHoc);

                return new SubjectInsight
                {
                    SubjectName        = mon.TenMonHoc,
                    TotalTaskCount     = tasks.Count,
                    CompletedTaskCount = completed,
                    CompletionRate     = tasks.Count == 0 ? 0.0 : (double)completed / tasks.Count,
                    TotalStudyMinutes  = studied
                };
            }).ToList();
        }

        public ProductivityScore ComputeProductivityScore(double completionRate, int streakDays, double timeEfficiency)
        {
            var streakFactor = Math.Min(streakDays, 30) / 30.0;
            var raw = completionRate * 50.0 + streakFactor * 30.0 + timeEfficiency * 20.0;
            return new ProductivityScore { Value = (int)Math.Round(raw) };
        }
    }
}
```

- [ ] **Step 6: Run failing tests — verify all 4 pass**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "ComputeWeekly|ComputeProductivity|ComputeSubject" 2>&1 | tail -15
```
Expected: all 4 PASS

- [ ] **Step 7: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `127 passed`

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner/Services/Analytics/ SmartStudyPlanner.Tests/AnalyticsServiceTests.cs
git commit -m "feat(M6): add IStudyAnalytics, StudyAnalyticsService, WeeklyReport/SubjectInsight/ProductivityScore DTOs"
```

---

## Task M6-3: Hook StudyLog Recording in FocusViewModel

**Context:** `FocusViewModel.LuuThoiGianThucTe()` currently only updates `ThoiGianDaHoc` in-memory. We need to also write a `StudyLog` row to the database when a session ends. Requires: new `AddStudyLogAsync` on `IStudyRepository`; FocusViewModel gets an `IStudyRepository` injected via secondary constructor (primary delegate to it via ServiceLocator); `HoanThanh` also sets `NgayHoanThanh` on the task.

**Files:**
- Modify: `SmartStudyPlanner/Data/IStudyRepository.cs` — add `AddStudyLogAsync`
- Modify: `SmartStudyPlanner/Data/StudyRepository.cs` — implement it
- Modify: `SmartStudyPlanner/ViewModels/FocusViewModel.cs` — inject repository, write log
- Modify: `SmartStudyPlanner.Tests/AnalyticsServiceTests.cs` — add FocusViewModel log test

- [ ] **Step 1: Write failing test**

Add to `AnalyticsServiceTests.cs`:

```csharp
[Fact]
public void FocusViewModel_WritesStudyLog_OnHoanThanh()
{
    var task = new StudyTask("Test", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 2);
    var dashItem = new TaskDashboardItem
    {
        TenTask    = task.TenTask,
        TenMonHoc  = "CNTT",
        TaskGoc    = task,
        HanChot    = task.HanChot,
        DiemUuTien = 60
    };
    var mockRepo = new FakeStudyRepository();
    var vm = new FocusViewModel(dashItem, mockRepo);
    vm.SimulateStudySeconds(300); // 5 minutes

    vm.HoanThanhCommand.Execute(null);

    Assert.Single(mockRepo.Logs);
    Assert.Equal(5, mockRepo.Logs[0].SoPhutHoc);
    Assert.True(mockRepo.Logs[0].DaHoanThanh);
    Assert.Equal(task.MaTask, mockRepo.Logs[0].MaTask);
    Assert.NotNull(task.NgayHoanThanh);
}

private class FakeStudyRepository : IStudyRepository
{
    public List<StudyLog> Logs { get; } = new();
    public Task AddStudyLogAsync(StudyLog log) { Logs.Add(log); return Task.CompletedTask; }
    public Task<HocKy> DocHocKyAsync() => throw new NotImplementedException();
    public Task<List<HocKy>> LayDanhSachHocKyAsync() => throw new NotImplementedException();
    public Task LuuHocKyAsync(HocKy hocKy) => throw new NotImplementedException();
    public Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy) => throw new NotImplementedException();
}
```

Add usings: `using System.Threading.Tasks; using SmartStudyPlanner.Data; using SmartStudyPlanner.ViewModels;`

- [ ] **Step 2: Run test to verify it fails**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FocusViewModel_WritesStudyLog" 2>&1 | tail -15
```
Expected: FAIL — missing interface methods / constructor overload

- [ ] **Step 3: Add AddStudyLogAsync and GetStudyLogsAsync to IStudyRepository.cs**

```csharp
Task AddStudyLogAsync(StudyLog log);
Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy);
```

- [ ] **Step 4: Implement both methods in StudyRepository.cs**

```csharp
public async Task AddStudyLogAsync(StudyLog log)
{
    using var db = new AppDbContext();
    db.StudyLogs.Add(log);
    await db.SaveChangesAsync();
}

public async Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy)
{
    using var db = new AppDbContext();
    var taskIds = hocKy.DanhSachMonHoc
        .SelectMany(m => m.DanhSachTask)
        .Select(t => t.MaTask)
        .ToHashSet();
    return await db.StudyLogs
        .Where(l => taskIds.Contains(l.MaTask))
        .OrderBy(l => l.NgayHoc)
        .ToListAsync();
}
```
Add using: `using Microsoft.EntityFrameworkCore;`

- [ ] **Step 5: Update FocusViewModel.cs**

Add field after existing fields:
```csharp
private readonly IStudyRepository _repository;
```

Change the existing single-parameter constructor to a delegating constructor:
```csharp
public FocusViewModel(TaskDashboardItem task)
    : this(task, ServiceLocator.Get<IStudyRepository>()) { }
```

Add a new two-parameter constructor that contains the original initialization logic:
```csharp
public FocusViewModel(TaskDashboardItem task, IStudyRepository repository)
{
    TaskHienTai = task;
    _repository = repository;
    TieuDeTask = $"Đang Focus: {task.TenTask} ({task.TenMonHoc})";
    ThietLapPomodoro(true);
    _timer = new DispatcherTimer();
    _timer.Interval = TimeSpan.FromSeconds(1);
    _timer.Tick += Timer_Tick;
}
```

Add `internal` test hook after the two constructors:
```csharp
internal void SimulateStudySeconds(int seconds) => _tongGiayDaHoc += seconds;
```

Update `LuuThoiGianThucTe` to accept a `daHoanThanh` parameter and write the log:
```csharp
private void LuuThoiGianThucTe(bool daHoanThanh = false)
{
    int phutDaHoc = _tongGiayDaHoc / 60;
    if (phutDaHoc > 0)
    {
        TaskHienTai.TaskGoc.ThoiGianDaHoc += phutDaHoc;
        Services.StreakManager.UpdateStreak();
        _ = _repository.AddStudyLogAsync(new StudyLog
        {
            MaTask      = TaskHienTai.TaskGoc.MaTask,
            NgayHoc     = DateTime.Today,
            SoPhutHoc   = phutDaHoc,
            SoPhutDuKien = 0,
            DaHoanThanh = daHoanThanh
        });
    }
}
```

Update `HoanThanh` to pass `daHoanThanh: true` and set `NgayHoanThanh`:
```csharp
[RelayCommand]
private void HoanThanh()
{
    _timer.Stop();
    LuuThoiGianThucTe(daHoanThanh: true);
    TaskHienTai.TaskGoc.TrangThai    = StudyTaskStatus.HoanThanh;
    TaskHienTai.TaskGoc.NgayHoanThanh = DateTime.Today;
    OnKetThuc?.Invoke();
}
```

Update `ThoatKhanCap` to pass `daHoanThanh: false`:
```csharp
[RelayCommand]
private void ThoatKhanCap()
{
    _timer.Stop();
    LuuThoiGianThucTe(daHoanThanh: false);
    OnKetThuc?.Invoke();
}
```

- [ ] **Step 6: Run failing test — verify it passes**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FocusViewModel_WritesStudyLog" 2>&1 | tail -10
```
Expected: PASS

- [ ] **Step 7: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `128 passed`

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner/Data/IStudyRepository.cs SmartStudyPlanner/Data/StudyRepository.cs SmartStudyPlanner/ViewModels/FocusViewModel.cs SmartStudyPlanner.Tests/AnalyticsServiceTests.cs
git commit -m "feat(M6): hook StudyLog recording in FocusViewModel, set NgayHoanThanh on completion"
```

---

## Task M6-4: AnalyticsViewModel + Register IStudyAnalytics

**Context:** `AnalyticsViewModel` loads study logs via `IStudyRepository.GetStudyLogsAsync`, computes metrics via `IStudyAnalytics`, and exposes chart series + summary properties for data binding in `AnalyticsPage`. Follows same constructor pattern as `DashboardViewModel`: primary constructor delegates to a testable overload.

**Files:**
- Modify: `SmartStudyPlanner/App.xaml.cs` — register `IStudyAnalytics`
- Create: `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs`

- [ ] **Step 1: Register IStudyAnalytics in App.xaml.cs**

Find the `ServiceLocator.Configure` call in `OnStartup`. Add after existing registrations:
```csharp
services.AddSingleton<IStudyAnalytics, StudyAnalyticsService>();
```
Add usings:
```csharp
using SmartStudyPlanner.Services.Analytics;
```

- [ ] **Step 2: Build to verify registration compiles**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Create AnalyticsViewModel.cs**

```csharp
// SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.Analytics.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly HocKy _hocKy;
        private readonly IStudyRepository _repository;
        private readonly IStudyAnalytics _analytics;

        public HocKy HocKy => _hocKy;

        [ObservableProperty] private ISeries[] weeklyChartSeries   = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[]     weeklyChartXAxes   = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] subjectChartSeries  = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[]     subjectChartXAxes  = Array.Empty<Axis>();
        [ObservableProperty] private int        productivityValue;
        [ObservableProperty] private string     productivityLabel  = "Chưa có dữ liệu";
        [ObservableProperty] private string     trangThai          = "Đang tải...";
        [ObservableProperty] private ObservableCollection<SubjectInsight> subjectInsights = new();

        public AnalyticsViewModel(HocKy hocKy)
            : this(hocKy,
                ServiceLocator.Get<IStudyRepository>(),
                ServiceLocator.Get<IStudyAnalytics>()) { }

        public AnalyticsViewModel(HocKy hocKy, IStudyRepository repository, IStudyAnalytics analytics)
        {
            _hocKy      = hocKy;
            _repository = repository;
            _analytics  = analytics;
        }

        public async Task LoadAsync()
        {
            var logs = await _repository.GetStudyLogsAsync(_hocKy);

            // Weekly minutes bar chart
            var weekly = _analytics.ComputeWeeklyMinutes(logs, DateTime.Today);
            WeeklyChartSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = weekly.MinutesPerDay.ToArray(),
                    Name   = "Phút học",
                    Fill   = new SolidColorPaint(SKColors.CornflowerBlue)
                }
            };
            WeeklyChartXAxes = new[] { new Axis { Labels = weekly.DayLabels.ToArray(), LabelsRotation = 25 } };

            // Subject completion chart
            var insights = _analytics.ComputeSubjectInsights(_hocKy, logs);
            SubjectInsights = new ObservableCollection<SubjectInsight>(insights);
            SubjectChartSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = insights.Select(i => Math.Round(i.CompletionRate * 100, 1)).ToArray(),
                    Name   = "% Hoàn thành",
                    Fill   = new SolidColorPaint(SKColors.MediumSeaGreen)
                }
            };
            SubjectChartXAxes = new[]
            {
                new Axis { Labels = insights.Select(i => Truncate(i.SubjectName)).ToArray(), LabelsRotation = 15 }
            };

            // Productivity score
            var allTasks       = _hocKy.DanhSachMonHoc.SelectMany(m => m.DanhSachTask).ToList();
            var completionRate = allTasks.Count == 0 ? 0.0
                : (double)allTasks.Count(t => t.TrangThai == StudyTaskStatus.HoanThanh) / allTasks.Count;
            var streakData     = StreakManager.GetCurrentStreak();
            var totalActual    = logs.Sum(l => l.SoPhutHoc);
            var totalExpected  = allTasks.Count * 30; // 30 min baseline per task
            var timeEfficiency = totalExpected == 0 ? 0.0 : Math.Min(1.0, (double)totalActual / totalExpected);

            var score         = _analytics.ComputeProductivityScore(completionRate, streakData.StreakCount, timeEfficiency);
            ProductivityValue = score.Value;
            ProductivityLabel = score.Label;
            TrangThai         = $"Cập nhật: {DateTime.Now:HH:mm dd/MM/yyyy}";
        }

        private static string Truncate(string s) => s.Length > 12 ? s[..9] + "..." : s;
    }
}
```

- [ ] **Step 4: Build**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `128 passed`

- [ ] **Step 6: Commit**

```bash
git add SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs SmartStudyPlanner/App.xaml.cs
git commit -m "feat(M6): add AnalyticsViewModel, register IStudyAnalytics in ServiceLocator"
```

---

## Task M6-5: AnalyticsPage.xaml — LiveCharts2 Charts

**Context:** Three-section analytics page: (1) productivity score card at top; (2) weekly study time bar chart; (3) subject completion bar chart + summary DataGrid. Styled with existing theme tokens. Uses `Loaded` event to call `vm.LoadAsync()`. Follows the same `Page` pattern as `DashboardPage`.

**Files:**
- Create: `SmartStudyPlanner/Views/AnalyticsPage.xaml`
- Create: `SmartStudyPlanner/Views/AnalyticsPage.xaml.cs`

- [ ] **Step 1: Create AnalyticsPage.xaml.cs**

```csharp
// SmartStudyPlanner/Views/AnalyticsPage.xaml.cs
using SmartStudyPlanner.Models;
using SmartStudyPlanner.ViewModels;
using System.Windows.Controls;

namespace SmartStudyPlanner
{
    public partial class AnalyticsPage : Page
    {
        private readonly AnalyticsViewModel _vm;

        public AnalyticsPage(HocKy hocKy)
        {
            InitializeComponent();
            _vm = new AnalyticsViewModel(hocKy);
            DataContext = _vm;
            Loaded += async (_, _) => await _vm.LoadAsync();
        }

        public HocKy HocKy => _vm.HocKy;
    }
}
```

- [ ] **Step 2: Create AnalyticsPage.xaml**

```xml
<Page x:Class="SmartStudyPlanner.AnalyticsPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF"
      Title="Analytics"
      Background="{DynamicResource AppBackground}">
    <Page.Resources>
        <Style x:Key="SectionHeader" TargetType="TextBlock">
            <Setter Property="FontSize"   Value="14"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Foreground" Value="{DynamicResource PrimaryText}"/>
            <Setter Property="Margin"     Value="0,20,0,10"/>
        </Style>
    </Page.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="0,0,10,0">
        <StackPanel Margin="20">

            <!-- Page header -->
            <TextBlock Text="PHÂN TÍCH HỌC TẬP" FontWeight="Bold" FontSize="22"
                       Foreground="{DynamicResource PrimaryText}" HorizontalAlignment="Center" Margin="0,0,0,4"/>
            <TextBlock Text="{Binding TrangThai}" FontSize="12" Foreground="{DynamicResource SecondaryText}"
                       HorizontalAlignment="Center" Margin="0,0,0,20"/>

            <!-- Productivity Score Card -->
            <Border Background="{DynamicResource StatCardBackground}" CornerRadius="12" Padding="24,18"
                    BorderBrush="{DynamicResource BorderColor}" BorderThickness="1" Margin="0,0,0,20"
                    HorizontalAlignment="Center" MinWidth="240">
                <StackPanel HorizontalAlignment="Center">
                    <TextBlock Text="&#xE9D9;" FontFamily="Segoe MDL2 Assets" FontSize="30"
                               Foreground="{DynamicResource AccentColor}" HorizontalAlignment="Center" Margin="0,0,0,8"/>
                    <TextBlock Text="{Binding ProductivityValue, StringFormat={}{0} điểm}"
                               FontSize="38" FontWeight="Bold" Foreground="{DynamicResource PrimaryText}"
                               HorizontalAlignment="Center"/>
                    <TextBlock Text="{Binding ProductivityLabel}" FontSize="14" FontWeight="SemiBold"
                               Foreground="{DynamicResource AccentColor}" HorizontalAlignment="Center" Margin="0,6,0,0"/>
                    <TextBlock Text="Điểm năng suất học tập" FontSize="12"
                               Foreground="{DynamicResource SecondaryText}" HorizontalAlignment="Center"/>
                </StackPanel>
            </Border>

            <!-- Weekly minutes chart -->
            <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                <TextBlock Text="&#xE787;" FontFamily="Segoe MDL2 Assets" FontSize="15"
                           Foreground="{DynamicResource AccentColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBlock Text="THỜI GIAN HỌC 7 NGÀY QUA" Style="{StaticResource SectionHeader}"
                           Foreground="{DynamicResource AccentColor}" Margin="0"/>
            </StackPanel>
            <Border Background="{DynamicResource CardBackground}" CornerRadius="8" Padding="10"
                    Margin="0,0,0,20" Height="220">
                <lvc:CartesianChart Series="{Binding WeeklyChartSeries}" XAxes="{Binding WeeklyChartXAxes}"
                                    LegendPosition="Right"/>
            </Border>

            <!-- Subject completion chart -->
            <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                <TextBlock Text="&#xE930;" FontFamily="Segoe MDL2 Assets" FontSize="15"
                           Foreground="{DynamicResource SuccessColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBlock Text="TỈ LỆ HOÀN THÀNH THEO MÔN HỌC" Style="{StaticResource SectionHeader}"
                           Foreground="{DynamicResource SuccessColor}" Margin="0"/>
            </StackPanel>
            <Border Background="{DynamicResource CardBackground}" CornerRadius="8" Padding="10"
                    Margin="0,0,0,20" Height="220">
                <lvc:CartesianChart Series="{Binding SubjectChartSeries}" XAxes="{Binding SubjectChartXAxes}"
                                    LegendPosition="Right"/>
            </Border>

            <!-- Subject details table -->
            <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                <TextBlock Text="&#xE8BF;" FontFamily="Segoe MDL2 Assets" FontSize="15"
                           Foreground="{DynamicResource WarningColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBlock Text="CHI TIẾT THEO MÔN HỌC" Style="{StaticResource SectionHeader}"
                           Foreground="{DynamicResource WarningColor}" Margin="0"/>
            </StackPanel>
            <DataGrid AutoGenerateColumns="False" IsReadOnly="True" CanUserAddRows="False"
                      ItemsSource="{Binding SubjectInsights}" Margin="0,0,0,20"
                      Background="{DynamicResource CardBackground}" Foreground="{DynamicResource PrimaryText}">
                <DataGrid.Resources>
                    <Style TargetType="DataGridColumnHeader">
                        <Setter Property="Background" Value="{DynamicResource CardBackground}"/>
                        <Setter Property="Foreground" Value="{DynamicResource PrimaryText}"/>
                        <Setter Property="FontWeight" Value="Bold"/>
                        <Setter Property="Padding"    Value="8,5"/>
                        <Setter Property="BorderBrush" Value="{DynamicResource BorderColor}"/>
                        <Setter Property="BorderThickness" Value="0,0,0,1"/>
                    </Style>
                </DataGrid.Resources>
                <DataGrid.RowStyle>
                    <Style TargetType="DataGridRow">
                        <Setter Property="Background" Value="{DynamicResource CardBackground}"/>
                        <Setter Property="Foreground" Value="{DynamicResource PrimaryText}"/>
                    </Style>
                </DataGrid.RowStyle>
                <DataGrid.Columns>
                    <DataGridTextColumn Header="Môn Học"    Binding="{Binding SubjectName}"                                      Width="*"   FontWeight="Bold"/>
                    <DataGridTextColumn Header="Tổng Task"  Binding="{Binding TotalTaskCount}"                                   Width="80"/>
                    <DataGridTextColumn Header="Đã xong"    Binding="{Binding CompletedTaskCount}"                               Width="80"/>
                    <DataGridTextColumn Header="Tỉ lệ HT"  Binding="{Binding CompletionRate, StringFormat={}{0:P0}}"            Width="80"/>
                    <DataGridTextColumn Header="Giờ đã học" Binding="{Binding TotalStudyMinutes, StringFormat={}{0} phút}"      Width="100"/>
                </DataGrid.Columns>
            </DataGrid>

        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 3: Build**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/Views/AnalyticsPage.xaml SmartStudyPlanner/Views/AnalyticsPage.xaml.cs
git commit -m "feat(M6): add AnalyticsPage.xaml with productivity score card, weekly/subject LiveCharts2 charts"
```

---

## Task M6-6: Sidebar Navigation Entry

**Context:** Add "Analytics" button to the sidebar in `MainWindow`, wire click handler, update `SetActiveNav` to include the new button, and update `MainFrame_Navigated` to track `_currentHocKy` from `AnalyticsPage`.

**Files:**
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml` — add `NavAnalytics` button
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml.cs` — add click handler + update `SetActiveNav` + update `Navigated` hook

- [ ] **Step 1: Add NavAnalytics button in MainWindow.xaml**

Find the nav StackPanel that contains `NavDashboard`, `NavMonHoc`, `NavWorkload`. After the `NavWorkload` Button closing tag, add:

```xml
<Button x:Name="NavAnalytics" Click="NavAnalytics_Click"
        HorizontalAlignment="Stretch" HorizontalContentAlignment="Left"
        Background="Transparent" Foreground="{DynamicResource SidebarText}"
        Padding="16,12" Margin="0,2,0,0"
        ToolTip="Phân tích học tập">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="&#xE9D9;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                   VerticalAlignment="Center" Margin="0,0,10,0"/>
        <TextBlock Text="Analytics" VerticalAlignment="Center" FontSize="13"/>
    </StackPanel>
</Button>
```

- [ ] **Step 2: Update SetActiveNav in MainWindow.xaml.cs to include NavAnalytics**

Change line:
```csharp
foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload })
```
To:
```csharp
foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload, NavAnalytics })
```

- [ ] **Step 3: Add NavAnalytics_Click handler**

After `NavWorkload_Click`, add:
```csharp
private void NavAnalytics_Click(object sender, RoutedEventArgs e)
{
    if (_currentHocKy == null) return;
    SetActiveNav(NavAnalytics);
    MainFrame.Navigate(new AnalyticsPage(_currentHocKy));
}
```

- [ ] **Step 4: Update MainFrame_Navigated to track AnalyticsPage**

Change:
```csharp
private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
{
    if (e.Content is DashboardPage dp)
        _currentHocKy = dp.HocKy;
}
```
To:
```csharp
private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
{
    if (e.Content is DashboardPage dp)
        _currentHocKy = dp.HocKy;
    else if (e.Content is AnalyticsPage ap)
        _currentHocKy = ap.HocKy;
}
```

- [ ] **Step 5: Build**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet build SmartStudyPlanner.sln 2>&1 | tail -5
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Run full test suite**

```
cd D:\Code\C#\SmartStudyPlanner && dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj 2>&1 | tail -10
```
Expected: `128 passed`

- [ ] **Step 7: Commit**

```bash
git add SmartStudyPlanner/Views/MainWindow.xaml SmartStudyPlanner/Views/MainWindow.xaml.cs
git commit -m "feat(M6): add Analytics sidebar navigation, update SetActiveNav and Navigated hook"
```

---

## Self-Review

**Spec coverage check:**

| Requirement | Task |
|-------------|------|
| Magic string `"Hoàn thành"` → constant | TD-1 |
| Magic number `AssumedSemesterDays` → use real end date | TD-2 |
| Duplicate `CalculatePriority` + `Assess` in BuildDashboardSummary | TD-3 |
| `pipelineResult.Adaptations` never shown | TD-4 |
| `StudyLog` tracking entity + DB | M6-1 |
| `StudyTask.NgayHoanThanh` | M6-1, M6-3 |
| `IStudyAnalytics` + weekly/subject/productivity metrics | M6-2 |
| `FocusViewModel` writes `StudyLog` on session end | M6-3 |
| `AnalyticsViewModel` with LiveCharts2 series | M6-4 |
| `AnalyticsPage.xaml` with 3 charts | M6-5 |
| Sidebar "Analytics" nav + `SetActiveNav` update | M6-6 |

**Placeholder scan:** None found — all steps include full code.

**Type consistency check:**
- `StudyTaskStatus.HoanThanh` used in TD-1, M6-2 (`StudyAnalyticsService`), M6-4 (`AnalyticsViewModel`) — consistent.
- `IStudyRepository.AddStudyLogAsync` / `GetStudyLogsAsync` defined in M6-3, used in M6-3 (`FocusViewModel`) and M6-4 (`AnalyticsViewModel`) — consistent.
- `WeeklyReport.MinutesPerDay` is `List<int>` in DTO, used as `.ToArray()` in `AnalyticsViewModel` for `ColumnSeries<int>` — consistent.
- `SubjectInsight.CompletionRate` is `double` [0.0, 1.0], multiplied ×100 for chart axis, formatted `{0:P0}` in DataGrid — consistent.
- `AnalyticsPage.HocKy` exposes `_vm.HocKy` which is `AnalyticsViewModel.HocKy => _hocKy` — consistent chain.

---

## Task M6.1: Task notes & study links

**Context:** Người dùng cần một khu vực ngay trong phần quản lý task/deadline để ghi chú tự do và dán các link học tập như bài tập, video bài học, tài liệu tham khảo. **Bản M6.1 này sẽ đi theo phương án B: tách bảng DB**, UI/UX phân vùng rõ ràng, parser nhập nhanh không fill notes/links, và ưu tiên trải nghiệm nhập liệu cho người dùng tự quản lý nội dung note/link.

**Naming convention:** dùng thống nhất cụm `Task notes & study links` trong tất cả docs, task titles, và commit/PR summaries.

**Files:**
- Modify: `SmartStudyPlanner/Models/StudyTask.cs` — giữ core task fields, thêm navigation/summary fields nếu cần
- Create: `SmartStudyPlanner/Models/TaskNote.cs` — note entity riêng, 1 task có 1 note chính
- Create: `SmartStudyPlanner/Models/TaskReferenceLink.cs` — link entity riêng, 1 task có nhiều link
- Modify: `SmartStudyPlanner/Data/IStudyRepository.cs` — CRUD cho notes/links theo task
- Modify: `SmartStudyPlanner/Data/StudyRepository.cs` — persistence + load/include graph
- Modify: `SmartStudyPlanner/Data/AppDbContext.cs` — DbSet/mapping quan hệ task-note-links
- Modify: `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs` — expose note panel + links list + commands; parser quick-fill không đụng notes/links
- Modify: `SmartStudyPlanner/Views/QuanLyTaskPage.xaml` — khu vực notes tách riêng, links tách riêng, layout phân vùng rõ; fallback rich-text document nếu cần
- Modify: `SmartStudyPlanner/Views/QuanLyTaskPage.xaml.cs` — wiring UI events nếu cần
- Create: `SmartStudyPlanner.Tests/TaskNotesTests.cs` — coverage cho lưu/đọc notes và links

- [ ] **Step 1: Xác định mô hình DB tách bảng**
  - `TaskNote` là 1-1 với `StudyTask`.
  - `TaskReferenceLink` là 1-n với `StudyTask`.
  - Task core giữ nguyên, notes/links thành aggregate phụ.

- [ ] **Step 2: Bổ sung note/link UI trong màn quản lý task**
  - UI phân vùng rõ ràng: task core / notes / study links.
  - Note và link là 2 vùng riêng nếu khả thi.
  - Nếu cần fallback, dùng rich-text document layout nhưng vẫn chia theo block/dòng như bảng để tối ưu UX.

- [ ] **Step 3: Cập nhật ViewModel và persistence**
  - Parser nhập nhanh chỉ fill core task fields.
  - Notes/links do người dùng tự nhập, không auto-fill.
  - Load/save notes/links qua repository include graph.

- [ ] **Step 4: Viết test hành vi**
  - Lưu note, load lại task, giữ nguyên links.
  - Link rỗng/invalid không làm hỏng form.
  - Parser nhanh không đụng notes/links.

- [ ] **Step 5: Xác nhận scope hoàn thành**
  - Task/deadline screen có 2 khu vực riêng cho note và study links nếu UI fit.
  - Nếu không fit, dùng rich-text document với layout phân dòng giống bảng.
  - Không ảnh hưởng flow tạo/sửa/xóa task hiện tại.

**M6.1 acceptance criteria:**
- Parser nhập nhanh không fill notes/links.
- Note area và link area được tách riêng nếu khả thi.
- DB lưu theo mô hình tách bảng.
- UX rõ ràng, không làm form task bị rối.
