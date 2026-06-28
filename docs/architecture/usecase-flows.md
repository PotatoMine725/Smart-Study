# Use-case Flows

> Consolidated 2026-05-21 from `2026-05-07-usecase-analysis.md` and `2026-05-07-usecase-system-flows.md`. Each flow shows: user action → entry point → service chain → output → fallback.

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
| UC-09 | Analytics view | medium |
| UC-10 | Task notes + reference links | medium |
| UC-11 | Toggle theme | low |

Core flows = UC-01, UC-02, UC-06, UC-07, UC-08. Supporting = the rest.

## UC-01 — Open Dashboard
- **User**: navigates to Dashboard.
- **Entry**: `DashboardViewModel(_currentHocKy)` constructed.
- **Chain**:
  1. Constructor resolves `IStudyRepository`, `IDecisionEngine`, `IWorkloadService`, `IRiskAnalyzer`, `IPipelineOrchestrator`, `IStudyTelemetry`.
  2. `LoadDuLieuDashboard()` runs.
  3. `IStudyTelemetry.Track("dashboard_open", ...)`.
  4. `IPipelineOrchestrator.Execute(new PipelineContext { ... })`.
  5. `BuildDashboardSummary(pipelineResult)`.
  6. Per task loop: `IDecisionEngine.CalculateRawSuggestedMinutes(task)` → `IRiskAnalyzer.Assess(task, mon)` (when pipeline didn't fill risk) → `PredictStudyMinutes(task, mon, out isMl)`.
  7. `ApplySummary` → `ApplyCharts` → `ApplySchedule` → `ApplyAdaptations` → `ApplyStreak`.
  8. `RaiseNotification(topTasks)` if any urgent.
- **Output**: `ThongKe`, `Top5Task`, chart series, `LichHocHomNay`, `AdaptationItems`, `ChuoiStreak`, optional toast.
- **Fallback**: empty state if no data; ViewModel can rebuild from `IDecisionEngine` + `IRiskAnalyzer` if pipeline partial.

## UC-02 — Add task (form)
- **User**: fills `TenTask`, `HanChot`, `LoaiTaskIndex`, `DoKho`, optionally Note + Links, then submits.
- **Entry**: `QuanLyTaskViewModel.ThemTask()`.
- **Chain**:
  1. Validate `TenTask != null`, `HanChot != null`.
  2. Parse `DoKho` → int, clamp 1..5.
  3. Convert `LoaiTaskIndex` → `LoaiCongViec`.
  4. If creating: `new StudyTask(...)` added to `MonHocHienTai.DanhSachTask`; telemetry `task_add`.
  5. If editing: update `_taskDangSua.*` fields; telemetry `task_update`.
  6. `TinhDiemVaSapXep()` re-runs `IDecisionEngine.CalculatePriority(task, MonHocHienTai)` for every task.
  7. `OnRefreshGrid?.Invoke()`.
  8. `await _repository.LuuHocKyAsync(HocKyHienTai)`.
  9. Notes / links sync via `UpsertTaskNoteAsync` + add/update/delete `TaskReferenceLink`.
  10. Reset form fields.
- **Output**: task persisted; notes/links synchronized; form cleared.
- **Fallback**: missing name/deadline → message box, stop.

## UC-03 — Quick-input parser
- **User**: pastes "Nộp báo cáo AI thứ 6 tuần sau" into the quick box.
- **Entry**: `QuanLyTaskViewModel.PhanTichNhapNhanh()`.
- **Chain**:
  1. Empty check.
  2. `SmartParser.Parse(VanBanNhapNhanh)` — delegates to default `ParsingOrchestrator(SystemClock())`.
  3. Assign `TenTask`, `HanChot`, `LoaiTaskIndex`, `DoKho` from the parse result.
  4. Refresh hint + save-button text.
  5. Clear `VanBanNhapNhanh`.
- **Output**: form pre-filled with core fields only.
- **Invariant**: parser must never touch `NoteContent` or `StudyLinks` (covered by `PhanTichNhapNhanh_DoesNotModifyNoteOrLinks` test).

## UC-04 — Edit task
- **User**: picks a task, hits edit.
- **Entry**: `QuanLyTaskViewModel.SuaTask(taskCanSua)`.
- **Chain**:
  1. Save `_taskDangSua` + `_editingTaskId`.
  2. Telemetry `task_click_edit`.
  3. Copy fields onto form.
  4. Switch button label to "Cập nhật".
  5. `await _repository.GetTaskEditorBundleAsync(taskCanSua.MaTask)` returns `TaskEditorBundle`.
  6. Bind `NoteContent` + `StudyLinks` from the bundle.

## UC-05 — Delete task
- **User**: clicks delete.
- **Entry**: `QuanLyTaskViewModel.XoaTask(taskCanXoa)`.
- **Chain**: `MessageBox.YesNo` → remove from `DanhSachTask` → `LuuHocKyAsync`.
- **Cascade**: cascade rules in `OnModelCreating` delete the matching `TaskNote` + `TaskReferenceLink`s.

## UC-06 — Mark task complete
- **User**: ticks complete on a task.
- **Entry**: `QuanLyTaskViewModel.HoanThanhTask(taskDaXong)`.
- **Chain**: validate not already done → `task.TrangThai = StudyTaskStatus.HoanThanh` → `TinhDiemVaSapXep()` → `OnRefreshGrid` → `LuuHocKyAsync`.

## UC-07 — Focus mode
- **User**: launches focus mode on a task.
- **Entry**: `DashboardViewModel.MoFocusMode(taskDuocChon)`.
- **Chain**:
  1. Telemetry `focus_start`.
  2. `new Views.FocusWindow(taskDuocChon).ShowDialog()`.
  3. After dialog closes: `await _repository.LuuHocKyAsync(_hocKyHienTai)` → `LoadDuLieuDashboard()`.
- **Inside `FocusViewModel`**:
  - 1-second `DispatcherTimer`.
  - `ThietLapPomodoro(true)` sets 25-min session.
  - Each tick decrements `_thoiGianConLai`, increments `_tongGiayDaHoc`.
  - On natural complete: `LuuThoiGianThucTe(true)` → telemetry `focus_complete` → mark task done → `OnKetThuc?.Invoke()`.
  - On early exit: `LuuThoiGianThucTe(false)` → telemetry `focus_abort` → `OnKetThuc?.Invoke()`.
- **Side effect**: `StudyLog` written fire-and-forget; `StudyTask.ThoiGianDaHoc` accumulated; streak updated.

## UC-08 — Workload balancer
- **User**: opens Workload from sidebar / dashboard.
- **Entry**: `DashboardViewModel.MoWorkloadBalancer()`.
- **Chain**: `new WorkloadBalancerWindow(_hocKyHienTai).ShowDialog()` → on close `LoadDuLieuDashboard()`.
- **Inside**: `IWorkloadService.GetCapacity` → `IDecisionEngine.CalculatePriority` per task → optional pipeline run → emits `ScheduleDay` + `ScheduledTask` + `AdaptationSuggestion`.

## UC-09 — Analytics
- **User**: navigates to Analytics.
- **Entry**: `AnalyticsViewModel.LoadAsync()`.
- **Chain**:
  1. `_repository.GetStudyLogsAsync(hocKy)` → store as `_allLogs`.
  2. `IStudyAnalytics.ComputeWeeklyMinutes(_allLogs, today)` → 7-bar series.
  3. `ComputeSubjectInsights(hocKy, _allLogs)` → subject completion + minutes.
  4. `ComputeProductivityScore(completionRate, streakDays, timeEfficiency)` → 0-100 + label tier.
  5. `BuildHeatmap(_allLogs)` → 52×7 `HeatCell` grid.
  6. `RetrainModel` command becomes enabled when `HasEnoughData` (≥20 logs).

## UC-10 — Notes + reference links
- **Entry**: `QuanLyTaskViewModel` commands `AddLink`, `RemoveLink`, `OpenLink`, `CopyLink`, `ClearNote`.
- **Chain**:
  - `AddLink` validates with `Uri.TryCreate(..., UriKind.Absolute, out var uri)` + `Scheme is "http" or "https"`. Stores `uri.OriginalString` (not `ToString()`) to preserve user-typed URL.
  - `TaskReferenceLinkItemVm` is added to `StudyLinks`.
  - `RemoveLink` removes from collection.
  - `OpenLink` uses `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`.
  - `CopyLink` uses `Clipboard.SetText`.
  - On task save: `UpsertTaskNoteAsync` + per-link add/update/delete.

## UC-11 — Toggle theme
- **Entry**: `DashboardViewModel.ToggleTheme()` or `MainWindow.ThemeToggle_Click`.
- **Chain**: `Services.ThemeManager.ToggleTheme()` — swaps merged dictionary.
- **No** business data side effects.

## Recurring participants

ViewModels: `DashboardViewModel`, `QuanLyTaskViewModel`, `FocusViewModel`, `AnalyticsViewModel`, `SetupViewModel`, `WorkloadBalancerViewModel`.

Services: `IStudyRepository`, `IDecisionEngine`, `IWorkloadService`, `IRiskAnalyzer`, `IPipelineOrchestrator`, `IStudyTelemetry`, `IStudyAnalytics`, `IMLModelManager`, `IStudyTimePredictor`, `IParsingOrchestrator`.

Models / DTOs: `HocKy`, `MonHoc`, `StudyTask`, `StudyLog`, `TaskNote`, `TaskReferenceLink`, `TaskDashboardItem`, `TaskEditorBundle`, `ScheduledTask`, `ScheduleDay`, `AdaptationSuggestion`, `HeatCell`.
