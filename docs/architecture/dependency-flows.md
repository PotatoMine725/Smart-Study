# Dependency Flows

> Consolidated 2026-05-21 from `2026-05-07-dependency-flows.md`. Reflects current call graph after Slice 4.

## 1. Top-level direction

```text
Views → ViewModels → Services → Core / Infrastructure / Models
```

- `Views` know nothing about business logic; they bind and forward events.
- `ViewModels` own UI state and commands.
- `Services` host application orchestration + ML lifecycle + telemetry.
- `Core/*` contains the pure domain logic (parsing, scheduling, risk, ML contracts).
- `Infrastructure/*` and `Data/*` own persistence.
- `Models/*` are shared data contracts.

## 2. Startup flow

1. `App.xaml.cs.OnStartup()`
2. `db.Database.Migrate()` creates/upgrades SQLite schema.
3. `ServiceLocator.Configure()` registers DI.
4. `IMLModelManager.InitializeAsync()` warmed up on a background `Task.Run` — exceptions swallowed.
5. UI proceeds even if ML is unavailable.

Why: DB + DI must be ready before any ViewModel resolves. ML is non-critical, isolated from the launch path.

## 3. Dashboard (`DashboardViewModel`)

Highest service density.

```text
DashboardViewModel
  ├── IStudyRepository
  ├── IDecisionEngine            → SchedulingOrchestrator
  ├── IWorkloadService           → WorkloadServiceImpl
  ├── IRiskAnalyzer              → Core/Risk/RiskOrchestrator
  ├── IPipelineOrchestrator      → PipelineOrchestrator + 5 stages
  └── IStudyTelemetry            → DebugStudyTelemetry
```

`LoadDuLieuDashboard()` calls `Execute(PipelineContext)` then `BuildDashboardSummary(result)`. Fallback: if pipeline doesn't fill a slot, `IDecisionEngine` / `IRiskAnalyzer` are called directly.

## 4. Scheduling chain

```text
WorkloadServiceImpl.GenerateSchedule(hocKy, capacityHours)
  ├── IDecisionEngine.CalculatePriority(task, monHoc)
  │     → DecisionEngineService (facade)
  │       → SchedulingOrchestrator
  │         ├── PriorityEvaluator → PriorityCalculator (rule chain + components)
  │         ├── RawMinutesCalculator
  │         ├── StudyTimeSuggestionEngine
  │         └── IStudyTimePredictor (optional ML augmentation)
  └── packs into ScheduleDay / ScheduledTask
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

## 6. ML lifecycle

```text
IModelStorageProvider   ←─ LocalModelStorageProvider (filesystem only)
        ↓
MLModelManager          ─ load / train / retrain / persist
        ↓
StudyTimePredictorService  ─ predict + confidence gate (≥0.6 ML, else formula)
        ↓
SchedulingOrchestrator     ─ uses predictor as optional input
```

Fallback rule: if model not ready or confidence low, deterministic formula wins.

Startup note: ML never sits on the critical launch path.

## 7. Persistence

```text
ViewModels / Services
  ↓
IStudyRepository (legacy)         OR    I*Repository (Slice 4)
  ↓                                       ↓
StudyRepository                          Sqlite*Repository (Func<AppDbContext> factory)
  ↓                                       ↓
AppDbContext (EF Core)
  ↓
SQLite (SmartStudyData.db)
```

`OnModelCreating` cascades: `HocKy → MonHoc → StudyTask → {TaskNote, TaskReferenceLink}`.

## 8. UI chain

```text
MainWindow
  ├── DashboardPage           ─ binds DashboardViewModel
  ├── QuanLyMonHocPage        ─ QuanLyMonHocViewModel
  ├── QuanLyTaskPage          ─ QuanLyTaskViewModel
  ├── AnalyticsPage           ─ AnalyticsViewModel
  ├── SetupPage               ─ SetupViewModel
  ├── FocusWindow (dialog)    ─ FocusViewModel
  └── WorkloadBalancerWindow  ─ WorkloadBalancerViewModel
```

UI implication: a few ViewModels still call `ServiceLocator.Get<T>()` (semi-static composition). Data still flows up only from service → UI.

## 9. Observability

`IStudyTelemetry.Track(event, props)` is the abstraction; `DebugStudyTelemetry` writes to `Debug.WriteLine`.

Events emitted today:
- `dashboard_open`, `dashboard_click_save`, `dashboard_click_goto`
- `analytics_open`, `analytics_filter_change` (with `range_days`, `subject`)
- `focus_start`, `focus_complete`, `focus_abort`
- `task_add`, `task_update`, `task_click_edit`, `task_add_link`
- Sidebar navigation events

## 10. Known dependency risks

- `ServiceLocator` creates global coupling if overused; constructor injection is preferred.
- A few ViewModels still own model objects + service calls together — keep an eye on this when extending.
- `EnsureCreated` was the source of a production bug; the codebase has switched to `Migrate()` but the migration story is still light.

## 11. Reading order

1. `App.xaml.cs`
2. `Services/ServiceLocator.cs`
3. `ViewModels/DashboardViewModel.cs`
4. `Services/Pipeline/PipelineOrchestrator.cs`
5. `Services/WorkloadServiceImpl.cs`
6. `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs`
7. `Data/AppDbContext.cs`
