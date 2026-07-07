# Async Workflow

> Consolidated 2026-05-21 from `2026-05-07-async-workflow.md`. Re-verified against source **2026-07-07 at commit `3c96978`** (branch `ui_rf`) — reflects behavior after M7 + Slice 4 + M8 + **Epic 1 M1.1** (fire-and-forget study-log write removed).

## 1. Posture

The app is **not async-heavy**. It is sync by default with async carved out exactly where the UI would otherwise feel slow or where state correctness needs gating:

- ML / text-classifier warm-up and outcome maturation at startup (3 background tasks)
- ML retrain (user-triggered from Analytics)
- Repository save/load on user commands
- Focus-session autosave (awaited since M1.1 — no longer fire-and-forget)
- Dashboard refresh after the focus dialog closes
- MainWindow's 1-minute background deadline check

Anything not on that list is sync — local data is small, so reading it on the UI thread is fine.

## 2. Startup (`App.xaml.cs`)

1. DB init: **sync** on the UI thread — `EnsureCreated()` plus the idempotent patch seams (`IsSeeded` column, `TelemetrySchema.EnsureTables`).
2. DI container build: **sync** via `ServiceLocator.Configure()`.
3. Three **async** fire-and-forget warmups, each on its own `Task.Run` with exceptions swallowed:
   - `IMLModelManager.InitializeAsync()` — study-time model;
   - `ITextClassifierModelManager.InitializeAsync()` — M8-A intent classifier (loads `text_classifier.zip` or trains from embedded seed CSV);
   - `IOutcomeMaturationService.MatureAsync(utcNow)` — M8-B: fills `WeightChangeLog` outcome columns whose 14-day window elapsed.

Intent: ML and telemetry maturation must not slow startup, must not block launch, and are allowed to be ready late (or never).

## 3. ML lifecycle (`MLModelManager`)

### `InitializeAsync()`
- Holds a `SemaphoreSlim` gate that serializes all lifecycle operations.
- If a model zip exists on disk, load it (+ `ModelMeta`); a corrupt zip falls through to retrain.
- If no valid model, train from `SeedDataGenerator.Generate()`.
- **No automatic retrain check anymore** — the old "≥ 50 new logs since `LastRetrainedAt` → background retrain" behavior was replaced by the explicit, user-triggered retrain below.

### `RetrainAsync(data)`
- Called from `AnalyticsViewModel.RetrainModel` (`[RelayCommand]`, guarded by `IsRetraining` + `HasEnoughData` = ≥ 50 study logs).
- Training data is **real-data-first** (commit `2f0e51e`): `StudyTimeTrainingDataSource.BuildAsync()` projects `StudyTimeOutcomeLog` rows into `StudyTimeInput` (needs ≥ `MinRows = 50`, else returns empty) and the caller falls back to seed data; `RetrainAsync` itself also falls back to seed on a null/empty list.
- Acquires the lifecycle gate; trains on the thread pool via `Task.Run` so the UI doesn't freeze.
- Accepts the new model only if test-split **R² ≥ 0.45**; writes zip + meta to temp files first, then copies over the canonical names and deletes the temps (atomic-swap pattern).
- A rejected train leaves the previous good model in memory and on disk.

### Properties of this design
- No race between concurrent `Initialize` and `Retrain` calls (single gate).
- Saving never blocks the UI thread.
- A bad train (R² < 0.45) can never replace a good model.

## 4. Dashboard

Mostly sync inside the ViewModel, with async escape hatches:

- `await _hocKyRepository.LuuHocKyAsync(...)` on save commands.
- `MoFocusMode` opens `FocusWindow.ShowDialog()`; after it closes, the semester is saved and the dashboard reloads.
- Workload Balancer is now a **page** (`WorkloadBalancerPage`, navigated via `MainFrame`) — no dialog-close hook anymore; it recomputes on its own load.

UX effect: saves feel snappy because the UI thread doesn't block on the database round-trip.

## 5. Command-level patterns

### Save command (`ThemTask` / `HoanThanhTask` / dashboard save)
1. `IStudyTelemetry.Track(...)`.
2. `await _hocKyRepository.LuuHocKyAsync(hocKy)` (transactional).
3. Notes / links sync through `ITaskEditorRepository`; a `DifficultyLabelLog` row records suggested-vs-final difficulty.

### Focus-session autosave (A6 — reworked in M1.1, reviewed R1/R5)
`FocusViewModel.LuuThoiGianThucTe` is `async Task` and **awaits** both writes (`StudyTimeOutcomeLog`, then `StudyLog` with `DeviceId` stamped). `HoanThanh()` / `ThoatKhanCap()` are `[RelayCommand(FlowExceptionsToTaskScheduler = true)] async Task` and route through `TryLuuThoiGianThucTe`, which catches failures, tracks `autosave_failed`, and calls the `NotifyUser` seam (production = `MessageBox.Show`; tests substitute a lambda):

- `HoanThanh` **blocks on failure** — the task is not marked complete and the window stays open so the user can retry.
- `ThoatKhanCap` **always exits** (owner-ratified): it notifies on failure but still invokes `OnKetThuc` — a failed write must never trap the user inside the maximized/topmost focus-lock window. `focus_abort` is tracked unconditionally (independent fact from `autosave_failed`).

### Background deadline check (`MainWindow`)
A 1-minute `DispatcherTimer` with an `async void` tick: reads all semesters via `IHocKyRepository`, recomputes priorities, and fires a Windows toast when tasks score ≥ 80 (`Views/MainWindow.xaml.cs:96-129`). Runs even while the window is hidden in the system tray.

### Other commands
Many are still sync because they only navigate, open a window, or toggle theme — no I/O.

## 6. Telemetry

`DebugStudyTelemetry.Track(...)` is sync because it only writes to `Debug.WriteLine`. No network, no file I/O — safe inline from command handlers. The **ground-truth telemetry writes** (SQLite log tables) are async: awaited inside the focus autosave path; fire-and-forget only for `WeightChangeLog` (`WeightOptimizerViewModel.ApplySuggestion` — logging is an enhancement and must never block or fail the apply path).

## 7. What is intentionally NOT async yet

- Pipeline execution (`PipelineOrchestrator.Execute`) — sync.
- `WorkloadServiceImpl.GenerateSchedule` — sync.
- Analytics refresh — no background timer; user-triggered.

This matches the current data scale. If pipeline or analytics balloon, the seams are already there (each stage is its own class).

## 8. Async safety rules

1. Warm-ups (ML, classifier, maturation) never block startup.
2. `RetrainAsync` never blocks the UI thread; the lifecycle gate serializes it against `InitializeAsync`.
3. Fail-soft: if any background task throws, the UI must remain usable.
4. **No fire-and-forget on data the user can lose.** The former fire-and-forget `StudyLog` write (rule 5 of the old version of this doc) was eliminated by M1.1/A6 — persisted-state writes are awaited and their failures surfaced. The only remaining fire-and-forget writes are pure enhancements (`WeightChangeLog`, startup warmups).
5. An emergency exit must never be blockable by I/O (`ThoatKhanCap` invariant above).

## 9. Reading order

1. `App.xaml.cs`
2. `Services/ML/MLModelManager.cs`
3. `ViewModels/FocusViewModel.cs` (A6 pattern: awaited writes + `TryLuuThoiGianThucTe` + `NotifyUser`)
4. `ViewModels/AnalyticsViewModel.cs` (`RetrainModel`)
5. `Views/MainWindow.xaml.cs` (background timer + tray)
