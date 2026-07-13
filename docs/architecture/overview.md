# Architecture Overview

> **Descriptive** — consolidated 2026-05-21 from `2026-05-07-project-architecture.md` and `2026-05-07-tech-stack.md`; last full re-verification against source **2026-07-07 at commit `3c96978`** (branch `ui_rf`, post-M1.1 merge). Per [../plans/2026-07-01-architecture-direction-decisions.md](../plans/2026-07-01-architecture-direction-decisions.md) (D-C), **code is normative and this file may lag it.** Canonical roadmap: [../specs/system_roadmap.md](../specs/system_roadmap.md); active execution plan: [../plans/2026-07-03-master-plan.md](../plans/2026-07-03-master-plan.md) (Epics 1–4, order E1 → E3 → E2 → E4).

## 1. What this app is

Smart Study Planner is a **WPF desktop app on .NET 10**, designed local-first / offline-first. It transforms semester / subject / task input into priority + schedule + risk + analytics, with ML used as a non-blocking enhancement. The current strategic direction (D-A) is **multi-device, two-way LAN sync** — Epic 1 (sync-ready data model) is **code complete, release gate in progress** (M1.1/M1.2/M1.3 merged `a3a0a3d`; see [system_roadmap.md §A.3](../specs/system_roadmap.md)).

## 2. Layered architecture

```text
Views (WPF pages/windows, MainFrame navigation)
  → ViewModels (CommunityToolkit.Mvvm)
    → Services / Application orchestration
      → Core/*  (domain logic — Scheduling, Risk, Parsing, ML contracts)
        → Infrastructure/Persistence  (EF Core + SQLite, 9 narrow repositories)
        → Services/ML/*               (local model artifacts on filesystem)
```

Principles:
- UI does not own business logic.
- Business logic is split from view so it can be tested independently.
- Local data is the default source of truth.
- ML is an enhancement, not allowed to block the app.
- All services flow through DI (`ServiceLocator`) — no `static class` in domain.
- Every DB write funnels through one stamping seam (`AppDbContext.SaveChanges*` → `SyncStamper`, Epic 1 M1.1).

## 3. Tech stack

| Layer | Technology | Notes |
|---|---|---|
| UI | WPF on **.NET 10** (`net10.0-windows10.0.19041.0`) | `WinExe`, `UseWPF=true`, `UseWindowsForms=true` (tray icon) |
| Language | C# (nullable reference types **enabled**, implicit usings **enabled**) | |
| MVVM | `CommunityToolkit.Mvvm` | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` |
| Charts | `LiveChartsCore.SkiaSharpView.WPF` — **Analytics only** | Dashboard was redesigned to **native XAML charts** (`Controls/DonutChart`, `DashboardChartModels`) in commit `7225dba` |
| Notifications | `Microsoft.Toolkit.Uwp.Notifications` | Windows toasts (deadline warnings, minimize-to-tray notice) |
| DI | `Microsoft.Extensions.DependencyInjection` | composed by `ServiceLocator` |
| DB | **SQLite** + `Microsoft.EntityFrameworkCore.Sqlite` | `SmartStudyData.db` next to the binary |
| ML | `Microsoft.ML` + `Microsoft.ML.FastTree` | study-time model in `%AppData%\SmartStudyPlanner\models\`; text classifier `text_classifier.zip` |
| Tests | `xUnit` + `Microsoft.NET.Test.Sdk` + `coverlet.collector` + `Verify.CommunityToolkit.Mvvm` | 291/291 green at M1.1 acceptance |
| Solution | `SmartStudyPlanner.slnx` (not `.sln`) | important when running `dotnet build` |

Project version: `1.5.0`.

## 4. Folder layout

```text
SmartStudyPlanner/
├── App.xaml(.cs)                # Startup: DB bootstrap (EnsureCreated + patch seams),
│                                #   DI bootstrap, 3 background warmups
├── Assets/                      # icon.ico (app + window icon)
├── Controls/                    # DonutChart (native XAML chart control)
├── Models/                      # Entities + DTO-like models + ISyncMetadata (M1.1)
│   └── Telemetry/               # DifficultyLabelLog, StudyTimeOutcomeLog, WeightChangeLog
├── Data/                        # AppDbContext (+ SaveChanges stamping overrides),
│                                #   SyncStamper (M1.1), TelemetrySchema (runtime table patch)
├── Infrastructure/Persistence/  # THE persistence layer (legacy StudyRepository retired)
│   ├── Repositories/            # IHocKyRepository, IStudyTaskRepository, IStudyLogRepository,
│   │                            #   IMonHocRepository, IUserStatsRepository, ITaskEditorRepository,
│   │                            #   IDifficultyLabelLogRepository, IWeightChangeLogRepository,
│   │                            #   IStudyTimeOutcomeLogRepository, UserStatsSnapshot
│   └── SQLite/Repositories/     # Sqlite* implementations (Func<AppDbContext> factory)
├── Core/                        # Domain — pure logic, no UI/storage references
│   ├── Parsing/                 # ParsingOrchestrator + engines + contracts + ParseResult
│   ├── Scheduling/              # SchedulingOrchestrator + RawMinutesCalculator +
│   │                            #   StudyTimeSuggestionEngine + PriorityEvaluator
│   ├── Risk/                    # RiskOrchestrator + RiskAggregator + Models (Core)
│   └── ML/Contracts/            # IMlConfidencePolicy, IIntentClassifierService,
│                                #   IWeightOptimizerService, WeightConfigSuggestion
├── Services/                    # Application services + adapters
│   ├── ServiceLocator.cs        # Composition root (singletons)
│   ├── DecisionEngineService.cs # thin facade over SchedulingOrchestrator
│   ├── WorkloadServiceImpl.cs   # IWorkloadService impl
│   ├── WeightConfig.cs          # POCO + IsValid()/Normalize()
│   ├── WeightConfigStore.cs     # persists weights to %LocalAppData%\SmartStudyPlanner\weight_config.json
│   ├── StreakManager.cs         # IStreakManager/IStreakStore + JsonFileStreakStore (streak_data.json)
│   ├── ThemeManager.cs          # Light/Dark merged-dictionary swap
│   ├── Analytics/               # IStudyAnalytics + StudyAnalyticsService
│   ├── ML/                      # IMLModelManager, MLModelManager, StudyTimePredictorService,
│   │                            #   StudyTimeTrainingDataSource, SeedDataGenerator, DeviceHelper,
│   │                            #   TextClassifierModelManager/-Service, IntentClassifierAdapter,
│   │                            #   TextClassifierDatasetImporter, Schema/*, WeightOptimizer/*
│   ├── Pipeline/                # IPipelineOrchestrator + 5 stages + PipelineContext
│   ├── Strategies/              # IClock, IUrgencyRule, IPriorityComponent, keyword parsers
│   └── Telemetry/               # IStudyTelemetry + DebugStudyTelemetry,
│                                #   IOutcomeMaturationService + OutcomeMaturationService (M8-B)
├── Themes/                      # CommonStyles, Light/Dark, SidebarStyles, StudyWorkspaceStyles
├── Converters/                  # HeatLevelToBrushConverter, etc.
├── ViewModels/                  # MVVM screen logic (incl. WeightOptimizerViewModel — Slice 8)
└── Views/                       # XAML + code-behind (pages + FocusWindow + WeightOptimizerWindow)
```

## 5. Major subsystems

### 5.1 Presentation
`MainWindow` is a shell: sidebar navigation + `MainFrame` (page navigation), a system-tray icon (close button hides to tray; explicit "Thoát hoàn toàn" quits), and a 1-minute background `DispatcherTimer` that recomputes priorities and fires a toast when urgent tasks (score ≥ 80) exist (`Views/MainWindow.xaml.cs:96-129`).

- **Pages** (navigated in `MainFrame`): `SetupPage` (start page), `DashboardPage`, `QuanLyMonHocPage`, `QuanLyTaskPage`, `AnalyticsPage`, `WorkloadBalancerPage` (converted Window → Page in commit `6481fc8`).
- **Windows**: `FocusWindow` (modal, maximized/topmost focus-lock) and `WeightOptimizerWindow` (non-modal, opened from the sidebar; Slice 8 UI, `Views/MainWindow.xaml.cs:204-220`).

An in-flight UI plan ([../plans/2026-07-05-ui-mobile-ready-polish.md](../plans/2026-07-05-ui-mobile-ready-polish.md), status PROPOSED) targets fidelity closure + responsive/touch-friendly polish on branch `ui_rf`.

### 5.2 Planning / decision
`IDecisionEngine` → `DecisionEngineService` (facade) → `SchedulingOrchestrator` → `PriorityEvaluator` + `RawMinutesCalculator` + `StudyTimeSuggestionEngine` + `IStudyTimePredictor`. `WorkloadServiceImpl` consumes priorities to distribute across `ScheduleDay` / `ScheduledTask`. The `WeightConfig` singleton is now **loaded from disk** at composition time (`WeightConfigStore.Load()`, `Services/ServiceLocator.cs:67`) and persisted when the user applies a Weight Optimizer suggestion.

> Full pipeline + classification/ranking detail with Mermaid diagrams: [pipeline.md](./pipeline.md).

### 5.3 Pipeline
`PipelineOrchestrator` runs 5 stages in `Order`: `ParseInput → Prioritize → BalanceWorkload → AssessRisk → Adapt`. Stages share a `PipelineContext` (Semester, Settings, ReferenceTime, RawInput, ParsedInput, PrioritizedTasks, Schedule, RiskReport, Adaptations, Warnings, Errors, Metadata, Status). Stages can be skipped by policy; errors collected centrally; stop-early on real failures. Note: `ParseInputStage` is a no-op normalizer (`.Trim()`) — real task classification lives in the separate `ParsingOrchestrator` flow (§5.5), invoked at task-creation time, not inside the pipeline.

### 5.4 Risk
`Core/Risk/RiskOrchestrator` (implements `IRiskAnalyzer`) + `RiskAggregator` over component evaluators (deadline urgency 0.5 + progress gap 0.3 + performance drop 0.2). Score → level via `RiskAssessment.FromScore` (≥0.8 Critical / ≥0.6 High / ≥0.3 Medium / else Low). The legacy `Services/RiskAnalyzer/*` folder (adapter + DTOs) was **fully retired** (commits `0346637` → `1b4c2ba` → `191dd17`); the risk subsystem now lives entirely under `Core/Risk/*`.

### 5.5 Parsing
`Core/Parsing/Orchestrators/ParsingOrchestrator` composes `RuleBasedTimeParsingEngine` + `TaskExtractionEngine`, then augments with `IIntentClassifier` (M8-A, **wired** via `IntentClassifierAdapter` → `TextClassifierService` → ML.NET `TextClassifierModelManager`). ML only sets task **type** (Loai), gated at confidence ≥ 0.60; difficulty/deadline stay rule-based; on model-absent/error it falls back byte-equal to heuristic. The old static `Services/SmartParser` facade was **retired** (commit `222cb5a`) — consumers inject `IParsingOrchestrator`.

### 5.6 Analytics
`StudyAnalyticsService` is a pure function over `IEnumerable<StudyLog>`. Outputs: `WeeklyReport` (7-day minutes), `SubjectInsight` (per-subject totals + completion), `ProductivityScore` (label tiers Xuất sắc / Tốt / Trung bình / Cần cải thiện). `AnalyticsViewModel` adds a 52×7 heatmap and hosts the user-triggered **Retrain** command (enabled at ≥ 50 study logs, `ViewModels/AnalyticsViewModel.cs:88`).

### 5.7 ML (study-time predictor)
`MLModelManager` owns lifecycle (SemaphoreSlim-gated load/train, atomic temp-file swap, R² ≥ 0.45 acceptance gate — `Services/ML/MLModelManager.cs:106`). `StudyTimePredictorService` is the only insertion point into `SchedulingOrchestrator`; its confidence is agreement-based (`1 − |predicted − formula| / max(formula, 1)`, ML wins at ≥ 0.6 — `Services/ML/StudyTimePredictorService.cs:41-48`). Retraining is **real-data-first**: `StudyTimeTrainingDataSource` builds the training set from `StudyTimeOutcomeLog` rows (requires ≥ 50, else empty) and `AnalyticsViewModel.RetrainModel` falls back to `SeedDataGenerator.Generate()` when real data is insufficient (`ViewModels/AnalyticsViewModel.cs:234-239`). See [knowledge/machine-learning.md](../knowledge/machine-learning.md).

### 5.8 Persistence
`AppDbContext` (EF Core) + **nine narrow repositories** under `Infrastructure/Persistence/` (the legacy `Data/IStudyRepository` + `StudyRepository` pair is **fully retired** — zero references remain; all ViewModels consume the new layer). Implementations take a `Func<AppDbContext>` factory to support in-memory SQLite tests. Since M1.1, `AppDbContext` overrides `SaveChanges(bool)` / `SaveChangesAsync(bool, ct)` to run `SyncStamper.Apply` (the single sync-metadata stamping seam) before every write (`Data/AppDbContext.cs:93-103`). See [data-model.md](./data-model.md).

### 5.9 Telemetry & ground truth (M8)
Two channels:
- **Debug event stream** — `IStudyTelemetry` → `DebugStudyTelemetry` (`Debug.WriteLine` only, no I/O).
- **Ground-truth SQLite tables** (no FK to domain tables): `DifficultyLabelLogs` (suggested-vs-final difficulty at task creation), `StudyTimeOutcomeLogs` (features + actual minutes per focus session — the predictor's real training data), `WeightChangeLogs` (before/after weights + baseline stats + task cohort when a weight suggestion is applied). `OutcomeMaturationService.MatureAsync` runs opportunistically at startup and fills each `WeightChangeLog`'s outcome columns once its 14-day window has elapsed (`Services/Telemetry/OutcomeMaturationService.cs`).

### 5.10 Sync-readiness (Epic 1 — code complete, release gate in progress)

> **Docs-audit note (2026-07-13):** the three bullets below still read as of the M1.1-only merge
> point (M1.2 "in review"/"NOT merged", M1.3 "Pending") and are stale — M1.2 and M1.3 have both
> since shipped (merge `a3a0a3d`). Canonical current status:
> [system_roadmap.md §A.2/§A.3](../specs/system_roadmap.md); shipped-behavior detail:
> [data-model.md §8](./data-model.md). Left for PM to rewrite (needs fresh per-entity code-state
> verification, out of this audit's docs-only wording-fix scope).

- **Merged (M1.1, commits `e968033` + `6e1c51f`, merge `3193adf`)**: `ISyncMetadata` contract, `SyncStamper` seam in `AppDbContext`, A6 closed (the focus-session write is awaited, `StudyLog.DeviceId` stamped at the write site, save failures surface to the user via `NotifyUser`/MessageBox + `autosave_failed` telemetry).
- **In review (M1.2, worktree `epic1-sync-ready-data-model`, NOT merged)**: `ISyncMetadata` on all six entities, delete → tombstone + G1 cascade, `SyncSchema.EnsureColumns` upgrade seam + backup. Verdict 2026-07-06: refine-before-accept ([../review/2026-07-06-epic1-m1.2-review.md](../review/2026-07-06-epic1-m1.2-review.md), one blocker M1.2-R1). At `ui_rf` HEAD, **no production entity implements `ISyncMetadata` yet** and deletes are still hard cascades.
- **Pending**: M1.3 (bounded `MonHoc` identity/dedup).

## 6. Runtime composition (`App.xaml.cs`)

1. If `DEV_RESET_DB=1`, `EnsureDeleted()` first.
2. `db.Database.EnsureCreated()` (`App.xaml.cs:28`) — **no EF migrations**. Existing DBs are patched by ad-hoc idempotent seams: an `ALTER TABLE HocKys ADD COLUMN IsSeeded` guarded by try/catch (`App.xaml.cs:31-39`), a dev-seed marker `UPDATE`, and `TelemetrySchema.EnsureTables(db)` (`CREATE TABLE IF NOT EXISTS` for the 3 telemetry tables). M1.2 extends this pattern into a proper `SyncSchema.EnsureColumns` upgrade seam with backup-before-upgrade.
3. Build DI container via `ServiceLocator.Configure()`.
4. Three fire-and-forget background warmups on `Task.Run`, each with swallowed exceptions so the app launches regardless: `IMLModelManager.InitializeAsync()`, `ITextClassifierModelManager.InitializeAsync()` (M8-A), `IOutcomeMaturationService.MatureAsync(utcNow)` (M8-B).
5. UI shows even if all three fail.

## 7. Architectural strengths and constraints

Strengths
- Clear layer boundaries; no `static class` in domain (`ServiceLocator`, `WeightConfigStore`, `ThemeManager` are composition/infra shells).
- Central DI container + stage-based pipeline (not a monolith).
- Safe ML fallback wired everywhere; ML never on the launch path.
- Offline-first as the default, explicit.
- Single write path: all EF writes flow through `SaveChanges*` → stamping seam (verified precondition — no `ExecuteUpdate`/`ExecuteDelete` bypasses).

Constraints
- Windows desktop only for now (a MAUI/Avalonia companion is aspirational — see the mobile-ready UI plan).
- `ServiceLocator` is still a composition root, not a `HostBuilder`; several ViewModels resolve services via the static locator in their production constructors (constructor injection exists for tests).
- Schema evolution is `EnsureCreated()` + hand-rolled idempotent patch seams, **not** EF migrations — every schema change to shipped DBs needs its own upgrade step (M1.2's T1.8 formalizes this).
- `SqliteHocKyRepository.LuuHocKyAsync` persists via a **Guid-diff reconcile** of the whole semester graph (Epic 1 / M1.2, G1 — done; replaced the old remove-then-recreate approach, which tombstones would break — `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs:81-93`).

## 8. Suggested reading order

1. `App.xaml.cs`
2. `Services/ServiceLocator.cs`
3. `Data/AppDbContext.cs` + `Data/SyncStamper.cs`
4. `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs`
5. `Services/Pipeline/PipelineOrchestrator.cs`
6. `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs`
7. `Services/DecisionEngineService.cs` (notice how thin it is)
8. `ViewModels/DashboardViewModel.cs`
9. `ViewModels/FocusViewModel.cs` (A6 write path + ground-truth logging)
