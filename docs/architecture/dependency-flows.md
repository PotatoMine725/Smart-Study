# Dependency Flows

> Consolidated 2026-05-21 from `2026-05-07-dependency-flows.md`. Re-verified against source **2026-07-07 at commit `3c96978`** (branch `ui_rf`) — reflects the call graph after the `StudyRepository` split, M8 telemetry, the `ui_rf` UI redesign, and Epic 1 M1.1.

## 1. Top-level direction

```text
Views → ViewModels → Services → Core / Infrastructure / Models
```

- `Views` know nothing about business logic; they bind and forward events. `MainWindow` is the navigation shell (sidebar + `MainFrame` + system tray).
- `ViewModels` own UI state and commands.
- `Services` host application orchestration + ML lifecycle + telemetry + streak/theme/weight stores.
- `Core/*` contains the pure domain logic (parsing, scheduling, risk, ML contracts).
- `Infrastructure/Persistence/*` and `Data/*` own persistence.
- `Models/*` are shared data contracts (incl. `Models/Telemetry/*` and `ISyncMetadata`).

## 2. Startup flow

1. `App.xaml.cs.OnStartup()`
2. `db.Database.EnsureCreated()` + idempotent patch seams: `IsSeeded` column `ALTER`, dev-seed marker `UPDATE`, `TelemetrySchema.EnsureTables` (**no EF migrations**).
3. `ServiceLocator.Configure()` registers DI.
4. Three background `Task.Run` warmups, exceptions swallowed: `IMLModelManager.InitializeAsync()`, `ITextClassifierModelManager.InitializeAsync()`, `IOutcomeMaturationService.MatureAsync(utcNow)`.
5. UI proceeds even if all warmups fail.

Why: DB + DI must be ready before any ViewModel resolves. ML and telemetry maturation are non-critical, isolated from the launch path.

## 3. Dashboard (`DashboardViewModel`)

Highest service density.

```text
DashboardViewModel
  ├── IHocKyRepository            → SqliteHocKyRepository (seed-filtered, clone-dedup read)
  ├── IDecisionEngine             → DecisionEngineService → SchedulingOrchestrator
  ├── IWorkloadService            → WorkloadServiceImpl
  ├── IRiskAnalyzer               → Core/Risk/RiskOrchestrator
  ├── IPipelineOrchestrator       → PipelineOrchestrator + 5 stages
  ├── IStudyTelemetry             → DebugStudyTelemetry
  └── IStreakManager              → StreakManager (JsonFileStreakStore + IClock)
```

`LoadDuLieuDashboard()` calls `Execute(PipelineContext)` then `BuildDashboardSummary(result)`. Fallback: if pipeline doesn't fill a slot, `IDecisionEngine` / `IRiskAnalyzer` are called directly. Charts are native XAML (`Controls/DonutChart` + `DashboardChartModels`) — no LiveCharts on this page.

## 4. Scheduling chain

```text
WorkloadServiceImpl.GenerateSchedule(hocKy, capacityHours)
  ├── IDecisionEngine.CalculatePriority(task, monHoc)
  │     → DecisionEngineService (facade)
  │       → SchedulingOrchestrator (owns WeightConfig — loaded from WeightConfigStore)
  │         ├── PriorityEvaluator → PriorityCalculator (rule chain + components)
  │         ├── RawMinutesCalculator
  │         ├── StudyTimeSuggestionEngine
  │         └── IStudyTimePredictor (optional ML augmentation)
  └── packs into ScheduleDay / ScheduledTask (least-loaded day — deadline-blind; Epic 3 fixes)
```

The decision engine is the priority source; the workload service is the distribution layer.

## 5. Pipeline (`PipelineOrchestrator`)

Stage order:
1. `ParseInputStage`
2. `PrioritizeStage`
3. `BalanceWorkloadStage`
4. `AssessRiskStage`
5. `AdaptStage`

Properties:
- Order is explicit via `IPipelineStage.Order`.
- Stages can be skipped by policy.
- Errors collected into `context.Errors`.
- Orchestrator stops early on real failures (not on missing inputs).

## 6. ML lifecycles

### Study-time predictor
```text
IModelStorageProvider   ←─ LocalModelStorageProvider (%AppData% filesystem)
        ↓
MLModelManager          ─ load / train / retrain / persist (R² ≥ 0.45 gate, atomic swap)
        ↓
StudyTimePredictorService  ─ agreement confidence (1 − |ml − formula|/formula); ≥0.6 → ML, else formula
        ↓
SchedulingOrchestrator     ─ uses predictor as optional input
```

Training data: `IStudyTimeOutcomeLogRepository → StudyTimeTrainingDataSource` (real outcome logs, ≥ 50 rows) with `SeedDataGenerator` fallback; retrain triggered by `AnalyticsViewModel.RetrainModel`.

### Text classifier (M8-A)
```text
TextClassifierModelManager (ML.NET multiclass, seed CSV embedded)
        ↓
TextClassifierService → IntentClassifierAdapter (confidence gate ≥ 0.60)
        ↓
ParsingOrchestrator (task-type field only)
```

### Weight optimizer (M8-B, Slice 8)
```text
IUserStatsRepository → WeightOptimizerService → WeightRuleEngine (pure)
        ↓ WeightConfigSuggestion
WeightOptimizerViewModel (WeightOptimizerWindow)
        ├── apply → WeightConfig.Normalize() + WeightConfigStore.Save
        └── ground truth → IWeightChangeLogRepository (fire-and-forget)
                └── OutcomeMaturationService.MatureAsync (startup, 14d window)
```

Fallback rule everywhere: if a model is not ready or confidence is low, the deterministic path wins. ML never sits on the critical launch path.

## 7. Persistence

```text
ViewModels / Services / MainWindow background check
  ↓
I*Repository (9 narrow interfaces — legacy IStudyRepository RETIRED)
  ↓
Sqlite*Repository (Func<AppDbContext> factory)
  ↓
AppDbContext (EF Core)
  ↓  SaveChanges/SaveChangesAsync override → SyncStamper.Apply (M1.1 stamping seam)
SQLite (SmartStudyData.db)
```

`OnModelCreating` cascades: `HocKy → MonHoc → StudyTask → {TaskNote, TaskReferenceLink}` (hard deletes at HEAD; tombstones land with M1.2). Telemetry tables (`DifficultyLabelLogs`, `StudyTimeOutcomeLogs`, `WeightChangeLogs`) are standalone — no FK.

## 8. UI chain

```text
MainWindow (shell: sidebar, MainFrame, tray icon, 1-min deadline toast timer)
  ├── SetupPage               ─ SetupViewModel (start page; IHocKyRepository)
  ├── DashboardPage           ─ DashboardViewModel
  ├── QuanLyMonHocPage        ─ QuanLyMonHocViewModel
  ├── QuanLyTaskPage          ─ QuanLyTaskViewModel (IHocKyRepository + ITaskEditorRepository +
  │                              IParsingOrchestrator + IDifficultyLabelLogRepository)
  ├── AnalyticsPage           ─ AnalyticsViewModel (LiveCharts + heatmap + RetrainModel)
  ├── WorkloadBalancerPage    ─ WorkloadBalancerViewModel (Window → Page, commit 6481fc8)
  ├── FocusWindow (dialog)    ─ FocusViewModel (opened from Dashboard)
  └── WeightOptimizerWindow   ─ WeightOptimizerViewModel (non-modal, single instance, sidebar)
```

UI implication: production ViewModel constructors still resolve via `ServiceLocator.Get<T>()` (test constructors take injected dependencies). Data still flows up only from service → UI.

## 9. Observability

Two channels:

**Event stream** — `IStudyTelemetry.Track(event, props)` → `DebugStudyTelemetry` (`Debug.WriteLine`). Events emitted today include:
- `app_main_window_loaded`, `navigate_page`, `click_nav_dashboard|subjects|workload|analytics|weight_optimizer`, `click_save_sidebar`, `click_theme_toggle`
- `dashboard_open`, `dashboard_click_save`, `dashboard_click_goto`
- `analytics_open`, `analytics_filter_change` (with `range_days`, `subject`)
- `focus_start`, `focus_complete`, `focus_abort` (unconditional), `autosave_failed` (A6/R5)
- `task_add`, `task_update`, `task_click_edit`, `task_add_link`

**Ground-truth log tables** (SQLite, M8) — durable, analyzable offline: `DifficultyLabelLogs`, `StudyTimeOutcomeLogs`, `WeightChangeLogs` (+ outcome maturation). These feed retraining and weight-governance decisions; see [data-model.md §2](./data-model.md).

## 10. Known dependency risks

- `ServiceLocator` creates global coupling if overused; constructor injection is preferred (and exists on every ViewModel for tests).
- Schema evolution is `EnsureCreated()` + hand-rolled patch seams — every new table/column shipped to an existing DB needs its own idempotent patch (M1.2's `SyncSchema` formalizes this into a versioned upgrade with backup).
- `SqliteHocKyRepository.LuuHocKyAsync` is a Guid-diff reconcile over the whole semester graph (Epic 1 / M1.2, G1 — done) — replaced the old remove-then-recreate approach, which was incompatible with tombstones.
- `SqliteStudyTaskRepository.DeleteAsync` has zero production callers; M1.2 review flagged it (M1.2-R1) as a cascade-invariant trap for future callers.

## 11. Reading order

1. `App.xaml.cs`
2. `Services/ServiceLocator.cs`
3. `Views/MainWindow.xaml.cs`
4. `ViewModels/DashboardViewModel.cs`
5. `Services/Pipeline/PipelineOrchestrator.cs`
6. `Services/WorkloadServiceImpl.cs`
7. `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs`
8. `Data/AppDbContext.cs` + `Data/SyncStamper.cs`
