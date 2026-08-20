# Analytics two-section redesign — ViewModel data contract

Target: `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs`
This note specifies every change the new `AnalyticsPage.xaml` requires. The XAML and
`RadialProgressRing` control in this package are ready to drop in; this file is the
implementation spec for the `.cs` side (intentionally left to the app's own PR, not
regenerated here, so it merges against the current branch cleanly).

## Why: the core semantic split

- **Section 1 (Focused)** = whatever `SelectedSubject` + `SelectedRangeDays` currently
  select. Weekly chart + completion ring only ever describe that slice.
- **Section 2 (Overall)** = the whole semester, always. Narrative, productivity score,
  heatmap, and the subject details table must stop being affected by either filter.

Today `ApplyFilters()` computes *everything* from the same `filtered` list (range +
subject scoped), then `ResetAnalyticsOutputs()` blanks *everything* — including Section
2's narrative/score/heatmap/table — the moment the focused slice is empty. That's the
bug the restructure fixes: Section 2 must render from `_allLogs`, independent of
`ApplyFilters` entirely.

## 1. New observable properties

Add to the `[ObservableProperty]` block:

```csharp
[ObservableProperty] private bool hasFocusedWeeklyData;
[ObservableProperty] private bool hasFocusedCompletionData;
[ObservableProperty] private int  focusedCompletionPercent;
[ObservableProperty] private string focusedCompletionSubjectLabel = string.Empty;
[ObservableProperty] private int  focusedCompletionCompletedCount;
[ObservableProperty] private int  focusedCompletionTotalCount;
[ObservableProperty] private int  focusedCompletionMinutes;
```

## 2. `ApplyFilters()` — scope to Section 1 only

Keep `filtered` (range + subject scoped) driving ONLY:
- `WeeklyChartSeries` / `WeeklyChartXAxes` (unchanged, already correctly scoped)
- The new focused-completion fields (below)

Remove from `ApplyFilters()`/`ResetAnalyticsOutputs()`: `BuildHeatmap(filtered)`,
`BuildNarrative(...)`, `ComputeProductivityScore(...)`, and the
`ComputeSubjectInsights(_hocKy, filtered)` call — these move to a new
`BuildOverallSection()` (step 4) that runs once per `LoadAsync()`, not on every
filter change.

```csharp
private void ApplyFilters()
{
    var from = DateTime.Today.AddDays(-Math.Max(1, SelectedRangeDays) + 1);
    var taskById = _hocKy.DanhSachMonHoc
        .SelectMany(m => m.DanhSachTask.Select(t => new { t.MaTask, Mon = m.TenMonHoc }))
        .ToDictionary(x => x.MaTask, x => x.Mon);
    var filtered = _allLogs
        .Where(l => l.NgayHoc.Date >= from)
        .Where(l => SelectedSubject == "Tất cả" || (taskById.TryGetValue(l.MaTask, out var mon)
            && MonHocIdentity.NameComparer.Instance.Equals(mon, SelectedSubject)))
        .ToList();

    HasFocusedWeeklyData = filtered.Count > 0;

    if (!HasFocusedWeeklyData)
    {
        WeeklyChartSeries = Array.Empty<ISeries>();
        WeeklyChartXAxes  = new[] { new Axis() };
    }
    else
    {
        var weekly = _analytics.ComputeWeeklyMinutes(filtered, DateTime.Today);
        WeeklyChartSeries = new ISeries[] { new ColumnSeries<int> {
            Values = weekly.MinutesPerDay.ToArray(), Name = "Phút học",
            Fill = new SolidColorPaint(SKColors.CornflowerBlue) } };
        WeeklyChartXAxes = new[] { new Axis { Labels = weekly.DayLabels.ToArray(), LabelsRotation = 15 } };
    }

    BuildFocusedCompletion(filtered);

    _telemetry.Track("analytics_filter_changed", new Dictionary<string, string> {
        ["range_days"] = SelectedRangeDays.ToString(), ["subject"] = SelectedSubject });
}
```

## 3. `BuildFocusedCompletion(filtered)` — new method, powers the ring

Completion must be driven by **task status** (existing `SubjectInsight` shape), not by
whether logs exist in range — a subject can have completed tasks with zero recent
study time. `HasFocusedCompletionData` = "this subject has tasks at all", independent
of `HasFocusedWeeklyData`.

```csharp
private void BuildFocusedCompletion(List<StudyLog> filtered)
{
    // Full-semester insights (not range-filtered) so "task total/completed" reflects
    // real task status; only TotalStudyMinutes below uses the range+subject-scoped logs.
    var allInsights = _analytics.ComputeSubjectInsights(_hocKy, _allLogs);

    if (SelectedSubject == "Tất cả")
    {
        int total = allInsights.Sum(i => i.TotalTaskCount);
        int completed = allInsights.Sum(i => i.CompletedTaskCount);
        HasFocusedCompletionData = total > 0;
        FocusedCompletionTotalCount = total;
        FocusedCompletionCompletedCount = completed;
        FocusedCompletionPercent = total == 0 ? 0 : (int)Math.Round(100.0 * completed / total);
        FocusedCompletionSubjectLabel = "Trung bình tất cả môn";
    }
    else
    {
        var mine = allInsights.FirstOrDefault(i =>
            MonHocIdentity.NameComparer.Instance.Equals(i.SubjectName, SelectedSubject));
        HasFocusedCompletionData = mine != null && mine.TotalTaskCount > 0;
        FocusedCompletionTotalCount = mine?.TotalTaskCount ?? 0;
        FocusedCompletionCompletedCount = mine?.CompletedTaskCount ?? 0;
        FocusedCompletionPercent = mine == null || mine.TotalTaskCount == 0
            ? 0 : (int)Math.Round(100.0 * mine.CompletedTaskCount / mine.TotalTaskCount);
        FocusedCompletionSubjectLabel = SelectedSubject;
    }

    FocusedCompletionMinutes = filtered.Sum(l => l.SoPhutHoc);
}
```

## 4. `BuildOverallSection()` — new method, called once from `LoadAsync()`

Runs after `_allLogs` loads, **not** on `OnSelectedRangeDaysChanged`/`OnSelectedSubjectChanged`.

```csharp
public async Task LoadAsync()
{
    try
    {
        IsLoading = true;
        HasError = false;
        _allLogs = await _studyLogRepository.GetForHocKyAsync(_hocKy);
        HasEnoughData = _allLogs.Count >= 50;
        SubjectOptions = new ObservableCollection<string>(new[] { "Tất cả" }
            .Concat(_hocKy.DanhSachMonHoc.Select(m => m.TenMonHoc)
                .Distinct(MonHocIdentity.NameComparer.Instance).OrderBy(x => x)));

        BuildOverallSection();   // Section 2 — always full semester
        ApplyFilters();          // Section 1 — current filter slice

        _telemetry.Track("analytics_open", new Dictionary<string, string> { ["semester"] = _hocKy.Ten });
    }
    catch { HasError = true; EmptyStateMessage = "Không thể tải analytics. Hãy thử lại."; }
    finally { IsLoading = false; }
}

private void BuildOverallSection()
{
    var insights = _analytics.ComputeSubjectInsights(_hocKy, _allLogs);   // _allLogs, not filtered
    SubjectInsights = new ObservableCollection<SubjectInsight>(insights);

    int totalTasks = insights.Sum(x => x.TotalTaskCount);
    int completedTasks = insights.Sum(x => x.CompletedTaskCount);
    double completionRate = totalTasks == 0 ? 0.0 : (double)completedTasks / totalTasks;
    int streakDays = _streak.GetCurrentStreak().StreakCount;
    double timeEfficiency = _allLogs.Count == 0 ? 0.0
        : _allLogs.Count(l => l.DaHoanThanh) / (double)_allLogs.Count;

    var score = _analytics.ComputeProductivityScore(completionRate, streakDays, timeEfficiency);
    ProductivityValue = score.Value;
    ProductivityLabel = score.Label;

    BuildHeatmap(_allLogs);               // _allLogs, not filtered — fixes the existing
                                           // range/subject leak into the "52 weeks" heatmap
    BuildNarrative(_allLogs, insights);    // _allLogs, not filtered
}
```

`BuildNarrative` and `BuildHeatmap` bodies are unchanged — only their call sites and
input list change (`_allLogs` instead of `filtered`).

## 5. Properties that become unused by this view (leave in place)

`SubjectChartSeries` / `SubjectChartXAxes` (the old completion bar-chart-per-subject)
are no longer bound by `AnalyticsPage.xaml` — replaced by the ring. Leaving them
computed is harmless if anything else depends on them; if nothing does, they and their
`ComputeSubjectInsights`-driven bar-fill logic can be deleted in a follow-up cleanup PR.

`HasData` / `EmptyStateMessage`: no longer bound by the page (replaced by the two
granular flags above). Safe to keep for telemetry/tests; the blanket empty-state banner
Border is removed from the XAML.

## 6. Converters used (no new files needed)

- `HeatLevelToBrushConverter` — unchanged, already exists.
- `SubjectToBrushConverter` (`Converters/DashboardConverters.cs`) — already exists,
  now also referenced from `AnalyticsPage.xaml` (declared locally as `SubjectBrush` in
  `Page.Resources`, since it wasn't previously imported there).

## 7. New control

- `Controls/RadialProgressRing.xaml` + `.xaml.cs` (in this package) — percent-of-100
  ring, same 160×160/R=60/22px geometry as `DonutChart` so they read as one family.
  Drop into `SmartStudyPlanner/Controls/`.

## Checklist to wire this up

1. Copy `implementation-package/Controls/RadialProgressRing.xaml(.cs)` into
   `SmartStudyPlanner/Controls/`.
2. Replace `SmartStudyPlanner/Views/AnalyticsPage.xaml` with
   `implementation-package/Views/AnalyticsPage.xaml`.
3. Apply the `AnalyticsViewModel.cs` changes in sections 1-4 above.
4. Build — no Theme/Style/Converter files need changes.
