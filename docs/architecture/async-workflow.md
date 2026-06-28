# Async Workflow

> Consolidated 2026-05-21 from `2026-05-07-async-workflow.md`. Reflects current behavior after M7 + Slice 4.

## 1. Posture

The app is **not async-heavy**. It is sync by default with async carved out exactly where the UI would otherwise feel slow or where state correctness needs gating:

- ML warm-up at startup
- ML retrain
- Repository save/load on user commands
- Dashboard refresh after a dialog closes

Anything not on that list is sync — local data is small, so reading it on the UI thread is fine.

## 2. Startup (`App.xaml.cs`)

1. DB init: **sync** — `db.Database.Migrate()` runs on the UI thread.
2. DI container build: **sync** via `ServiceLocator.Configure()`.
3. ML warm-up: **async** via `Task.Run(async () => await _mlManager.InitializeAsync())`.
4. Warm-up exceptions are swallowed so the app never fails to launch because of ML.

Intent: ML must not slow startup, must not block launch, must be allowed to be ready late.

## 3. ML lifecycle (`MLModelManager`)

### `InitializeAsync()`
- Holds a `SemaphoreSlim` gate that serializes all lifecycle operations.
- Tries `IModelStorageProvider.ReadAsync("study_time")`. If a zip exists, load it.
- If the model is invalid or absent, retrain from `SeedDataGenerator.Generate(180)`.
- After load, checks `GetStudyLogsSinceAsync(meta.LastRetrainedAt).Count >= 50` → fires `Task.Run(RetrainAsync)` in background (non-blocking).

### `RetrainAsync(logs)`
- Acquires the lifecycle gate.
- Merges 70% real logs + 30% seed (to avoid catastrophic forgetting when real logs are scarce).
- Trains on the thread pool via `Task.Run` so the UI doesn't freeze.
- Writes zip + meta to a `.tmp` file first, then `File.Move` over the canonical name (atomic swap).
- Cleans up the temp file.

### Properties of this design
- No race condition between concurrent `Initialize` and `Retrain` calls.
- `Save` never blocks the UI thread.
- A bad train (R² too low) leaves the previous good model on disk.

## 4. Dashboard

Mostly sync inside the ViewModel, with two async escape hatches:

- `await _repository.LuuHocKyAsync(...)` on save commands.
- After `FocusWindow` / `WorkloadBalancerWindow` close, `LoadDuLieuDashboard()` re-runs.

UX effect: saves feel snappy because the UI thread doesn't block on the database round-trip.

## 5. Command-level patterns

### Save command (`SaveCommand` / `ThemTask` / `HoanThanhTask`)
1. `IStudyTelemetry.Track(...)`.
2. `await _repository.LuuHocKyAsync(hocKy)`.
3. Show confirmation dialog.

### Focus command (`MoFocusMode`)
1. Open `FocusWindow.ShowDialog()` — modal.
2. After dialog closes, `await _repository.LuuHocKyAsync(...)`.
3. Reload dashboard.

### Other commands
Many are still sync because they only open a window or toggle theme — no I/O.

## 6. Telemetry

`DebugStudyTelemetry.Track(...)` is sync because it only writes to `Debug.WriteLine`. No network, no I/O. Safe to call inline from command handlers.

## 7. What is intentionally NOT async yet

- Pipeline execution (`PipelineOrchestrator.Execute`) — sync.
- `WorkloadServiceImpl.GenerateSchedule` — sync.
- Dashboard load — sync.
- Analytics refresh — no background timer; user-triggered.

This matches the current data scale. If pipeline or analytics balloon, the seams are already there (each stage is its own class).

## 8. Async safety rules

1. ML warm-up never blocks startup.
2. `RetrainAsync` never blocks the UI thread.
3. Lifecycle operations are serialized by a `SemaphoreSlim` gate.
4. Fail-soft: if any async task throws, the UI must remain usable.
5. Fire-and-forget is used for `FocusViewModel.LuuThoiGianThucTe` (study-log writes) to keep the focus timer responsive — accept the tradeoff that a crashed write loses the log.

## 9. Reading order

1. `App.xaml.cs`
2. `Services/ML/MLModelManager.cs`
3. `ViewModels/DashboardViewModel.cs`
4. `Data/StudyRepository.cs`
