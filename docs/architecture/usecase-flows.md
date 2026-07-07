# Use-case Flows

> Consolidated 2026-05-21 from `2026-05-07-usecase-analysis.md` and `2026-05-07-usecase-system-flows.md`. Re-verified against source **2026-07-07 at commit `3c96978`** (branch `ui_rf`). Each flow shows: user action → entry point → service chain → output → fallback.

## 0. Use-case catalog

| UC | Name | Frequency |
|---|---|---|
| UC-01 | Open Dashboard | every session |
| UC-02 | Add new task (form) | high |
| UC-03 | Quick-input parser | high |
| UC-04 | Edit existing task | medium |
| UC-05 | Delete task | low |
| UC-06 | Mark task complete | high |
| UC-07 | Focus mode | high |
| UC-08 | Workload balancer | medium |
| UC-09 | Analytics view + retrain | medium |
| UC-10 | Task notes + reference links | medium |
| UC-11 | Toggle theme | low |
| UC-12 | Weight optimizer (Slice 8) | low |

Core flows = UC-01, UC-02, UC-06, UC-07, UC-08. Supporting = the rest.

## UC-01 — Open Dashboard
- **User**: clicks Dashboard in the sidebar (`MainWindow.NavDashboard_Click` → `MainFrame.Navigate(new DashboardPage(_currentHocKy))`).
- **Entry**: `DashboardViewModel(hocKy)` constructed.
- **Chain**:
  1. Production constructor resolves `IHocKyRepository`, `IDecisionEngine`, `IWorkloadService`, `IRiskAnalyzer`, `IPipelineOrchestrator`, `IStudyTelemetry`, `IStreakManager`.
  2. `LoadDuLieuDashboard()` runs.
  3. `IStudyTelemetry.Track("dashboard_open", ...)`.
  4. `IPipelineOrchestrator.Execute(new PipelineContext { ... })`.
  5. `BuildDashboardSummary(pipelineResult)`.
  6. Per task loop: `IDecisionEngine.CalculateRawSuggestedMinutes(task)` → `IRiskAnalyzer.Assess(task, mon)` (when pipeline didn't fill risk) → `PredictStudyMinutes(task, mon, out isMl)`.
  7. Apply summary → native XAML chart collections (`StatusSegment`, `SubjectTimeProgress`, `SubjectWorkload`) → today's schedule → adaptations → streak (`IStreakManager.GetCurrentStreak`).
  8. `RaiseNotification(topTasks)` if any urgent.
- **Output**: `ThongKe`, `Top5Task`, chart collections, `LichHocHomNay`, `AdaptationItems`, `ChuoiStreak`, optional toast.
- **Fallback**: empty state if no data; ViewModel can rebuild from `IDecisionEngine` + `IRiskAnalyzer` if pipeline partial.

## UC-02 — Add task (form)
- **User**: fills `TenTask`, `HanChot`, `LoaiTaskIndex`, `DoKho`, optionally Note + Links, then submits.
- **Entry**: `QuanLyTaskViewModel.ThemTask()`.
- **Chain**:
  1. Validate `TenTask != null`, `HanChot != null`.
  2. Parse `DoKho` → int, clamp 1..5; convert `LoaiTaskIndex` → `LoaiCongViec`.
  3. If creating: `new StudyTask(...)` added to `MonHocHienTai.DanhSachTask`; telemetry `task_add`. If editing: update `_taskDangSua.*`; telemetry `task_update`.
  4. `TinhDiemVaSapXep()` re-runs `IDecisionEngine.CalculatePriority(task, MonHocHienTai)` for every task.
  5. `OnRefreshGrid?.Invoke()`.
  6. `await _hocKyRepository.LuuHocKyAsync(HocKyHienTai)`.
  7. Notes / links sync via `ITaskEditorRepository` (`UpsertNoteAsync` + add/update/delete link).
  8. **Ground truth (M8)**: a `DifficultyLabelLog` row records suggested difficulty (`DefaultDifficultyKeywordParser.PriorForTaskType`) vs. the user's final `DoKho` + `WasOverride` (`QuanLyTaskViewModel.cs:328-341`).
  9. Reset form fields.
- **Output**: task persisted; notes/links synchronized; difficulty label logged; form cleared.
- **Fallback**: missing name/deadline → message box, stop.

## UC-03 — Quick-input parser
- **User**: pastes "Nộp báo cáo AI thứ 6 tuần sau" into the quick box.
- **Entry**: `QuanLyTaskViewModel.PhanTichNhapNhanh()`.
- **Chain**:
  1. Empty check.
  2. `IParsingOrchestrator.Parse(VanBanNhapNhanh)` — heuristic baseline; ML overrides **task type** at confidence ≥ 0.60.
  3. Assign `TenTask`, `HanChot`, `LoaiTaskIndex`, `DoKho` from the parse result.
  4. Refresh hint + save-button text; clear `VanBanNhapNhanh`.
- **Output**: form pre-filled with core fields only.
- **Invariant**: parser must never touch `NoteContent` or `StudyLinks` (covered by `PhanTichNhapNhanh_DoesNotModifyNoteOrLinks` test).

## UC-04 — Edit task
- **User**: picks a task, hits edit.
- **Entry**: `QuanLyTaskViewModel.SuaTask(taskCanSua)`.
- **Chain**:
  1. Save `_taskDangSua` + `_editingTaskId`; telemetry `task_click_edit`.
  2. Copy fields onto form; switch button label to "Cập nhật".
  3. `await _taskEditorRepository.GetBundleAsync(taskCanSua.MaTask)` returns `TaskEditorBundle`.
  4. Bind `NoteContent` + `StudyLinks` from the bundle.

## UC-05 — Delete task
- **User**: clicks delete.
- **Entry**: `QuanLyTaskViewModel.XoaTask(taskCanXoa)`.
- **Chain**: `MessageBox.YesNo` → remove from `DanhSachTask` → `LuuHocKyAsync` (delete-by-absence: the recreate transaction simply omits the removed task).
- **Cascade**: DB cascade rules delete the matching `TaskNote` + `TaskReferenceLink`s. *(M1.2, in review, converts this to cascade-tombstoning.)*

## UC-06 — Mark task complete
- **User**: ticks complete on a task.
- **Entry**: `QuanLyTaskViewModel.HoanThanhTask(taskDaXong)`.
- **Chain**: validate not already done → `task.TrangThai = StudyTaskStatus.HoanThanh` → `TinhDiemVaSapXep()` → `OnRefreshGrid` → `LuuHocKyAsync`.

## UC-07 — Focus mode
- **User**: launches focus mode on a task.
- **Entry**: `DashboardViewModel.MoFocusMode(taskDuocChon)` (`DashboardViewModel.cs:345-351`).
- **Chain**:
  1. Telemetry `focus_start`.
  2. `new Views.FocusWindow(taskDuocChon).ShowDialog()` — maximized, topmost, borderless focus-lock.
  3. After dialog closes: `await _hocKyRepository.LuuHocKyAsync(_hocKyHienTai)` → `LoadDuLieuDashboard()`.
- **Inside `FocusViewModel`**:
  - 1-second `DispatcherTimer`; `ThietLapPomodoro(true)` sets a 25-min session (5-min breaks alternate automatically).
  - Each study tick increments `_tongGiayDaHoc` and updates the progress text.
  - **Complete** (`HoanThanh`): `TryLuuThoiGianThucTe(true)` — on success: telemetry `focus_complete`, mark task done, `OnKetThuc`. **On save failure: task is NOT marked complete and the window stays open** (`autosave_failed` tracked, `NotifyUser` MessageBox).
  - **Emergency exit** (`ThoatKhanCap`): `TryLuuThoiGianThucTe(false)` — the window **always closes** regardless of save outcome (owner-ratified: a failed write must not trap the user); `focus_abort` tracked unconditionally.
- **Side effects** (all awaited since M1.1 — no fire-and-forget):
  - `StudyTimeOutcomeLog` appended with the features the predictor saw (pre-increment `StudiedMinutesSoFar`) + `ActualMinutes` — ground truth for retraining;
  - `StudyLog` written with **`DeviceId` stamped** via `DeviceHelper.GetId()`;
  - `StudyTask.ThoiGianDaHoc` accumulated; streak updated via `IStreakManager.UpdateStreak()`.

## UC-08 — Workload balancer
- **User**: clicks Workload in the sidebar.
- **Entry**: `MainWindow.NavWorkload_Click` → `MainFrame.Navigate(new WorkloadBalancerPage(_currentHocKy))` — a **page** since commit `6481fc8` (no longer a modal window; no dialog-close dashboard reload).
- **Inside**: `WorkloadBalancerViewModel` drives `IWorkloadService.GenerateSchedule(hocKy, capacityHours)` (capacity slider) → `IDecisionEngine.CalculatePriority` per task → emits `ScheduleDay` + `ScheduledTask`.
- **Known limitation**: placement is least-loaded-day, deadline-blind (`WorkloadServiceImpl.cs:77-91`) — the Epic 3 SOE fixes this.

## UC-09 — Analytics + retrain
- **User**: clicks Analytics in the sidebar.
- **Entry**: `AnalyticsViewModel.LoadAsync()`.
- **Chain**:
  1. `IStudyLogRepository` loads logs for the semester → `_allLogs`; `HasEnoughData = _allLogs.Count >= 50` (`AnalyticsViewModel.cs:88`).
  2. `IStudyAnalytics.ComputeWeeklyMinutes(_allLogs, today)` → 7-bar series.
  3. `ComputeSubjectInsights(hocKy, _allLogs)` → subject completion + minutes.
  4. `ComputeProductivityScore(completionRate, streakDays, timeEfficiency)` → 0-100 + label tier (streak via `IStreakManager`).
  5. `BuildHeatmap(_allLogs)` → 52×7 `HeatCell` grid (Monday-aligned).
  6. **Retrain** command (enabled by `HasEnoughData`): `StudyTimeTrainingDataSource.BuildAsync()` → real `StudyTimeOutcomeLog` rows if ≥ 50, else `SeedDataGenerator.Generate()` → `IMLModelManager.RetrainAsync` (R² ≥ 0.45 acceptance).

## UC-10 — Notes + reference links
- **Entry**: `QuanLyTaskViewModel` commands `AddLink`, `RemoveLink`, `OpenLink`, `CopyLink`, `ClearNote`.
- **Chain**:
  - `AddLink` validates with `Uri.TryCreate(..., UriKind.Absolute, out var uri)` + `Scheme is "http" or "https"`. Stores `uri.OriginalString` (not `ToString()`) to preserve the user-typed URL.
  - `TaskReferenceLinkItemVm` is added to `StudyLinks`; `RemoveLink` removes from collection.
  - `OpenLink` uses `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`; `CopyLink` uses `Clipboard.SetText`.
  - On task save: `ITaskEditorRepository.UpsertNoteAsync` + per-link add/update/delete.

## UC-11 — Toggle theme
- **Entry**: `MainWindow.BtnTheme_Click` (sidebar) or `DashboardViewModel.ToggleTheme()`.
- **Chain**: `Services.ThemeManager.ToggleTheme()` — swaps the Light/Dark merged dictionary; sidebar icon updated.
- **No** business data side effects.

## UC-12 — Weight optimizer (M8-B UI, Slice 8)
- **User**: clicks "Weight Optimizer" in the sidebar.
- **Entry**: `MainWindow.NavWeightOptimizer_Click` → opens the single non-modal `WeightOptimizerWindow` (re-activates if already open; an "open" badge shows while it lives — `MainWindow.xaml.cs:204-220`).
- **Chain**:
  1. `LoadSuggestion` → `IDecisionEngine.SuggestWeightConfigAsync()` → `WeightOptimizerService` + pure `WeightRuleEngine` over `UserStatsSnapshot`.
  2. `IMlConfidencePolicy.Decide(confidence)` gates the UI: Reject → "need more data"; Review/AutoApply → apply enabled (AutoApply highlighted).
  3. `ApplySuggestion` → snapshot before-state → mutate the shared `WeightConfig` → `Normalize()` → `WeightConfigStore.Save` (persists to `%LocalAppData%`).
  4. **Ground truth**: fire-and-forget `WeightChangeLog` (before/after weights, confidence, rationale, baseline stats, open-task cohort as JSON).
  5. On a later startup, `OutcomeMaturationService.MatureAsync` fills the log's outcome columns (miss rate / delay / completions within the 14-day window).
- **Output**: new weights take effect immediately in `SchedulingOrchestrator` (same `WeightConfig` singleton) and survive restart.

## Recurring participants

ViewModels: `DashboardViewModel`, `QuanLyMonHocViewModel`, `QuanLyTaskViewModel`, `FocusViewModel`, `AnalyticsViewModel`, `SetupViewModel`, `WorkloadBalancerViewModel`, `WeightOptimizerViewModel`.

Services: `IHocKyRepository`, `ITaskEditorRepository`, `IStudyLogRepository`, `IStudyTimeOutcomeLogRepository`, `IDifficultyLabelLogRepository`, `IWeightChangeLogRepository`, `IDecisionEngine`, `IWorkloadService`, `IRiskAnalyzer`, `IPipelineOrchestrator`, `IStudyTelemetry`, `IStudyAnalytics`, `IMLModelManager`, `IStudyTimePredictor`, `IStudyTimeTrainingDataSource`, `IParsingOrchestrator`, `IStreakManager`, `IOutcomeMaturationService`, `IWeightOptimizerService`.

Models / DTOs: `HocKy`, `MonHoc`, `StudyTask`, `StudyLog`, `TaskNote`, `TaskReferenceLink`, `TaskDashboardItem`, `TaskEditorBundle`, `ScheduledTask`, `ScheduleDay`, `AdaptationSuggestion`, `HeatCell`, `StatusSegment`/`SubjectTimeProgress`/`SubjectWorkload`, `DifficultyLabelLog`, `StudyTimeOutcomeLog`, `WeightChangeLog`, `WeightConfigSuggestion`, `UserStatsSnapshot`.
