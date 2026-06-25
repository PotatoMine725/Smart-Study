# Architecture Overview

> Consolidated 2026-05-21 from `2026-05-07-project-architecture.md` and `2026-05-07-tech-stack.md`. Reflects current code state after refactor Slice 4.

## 1. What this app is

Smart Study Planner is a **WPF desktop app on .NET 10**, designed local-first / offline-first. It transforms semester / subject / task input into priority + schedule + risk + analytics, with ML used as a non-blocking enhancement.

## 2. Layered architecture

```text
Views (WPF pages/windows)
  → ViewModels (CommunityToolkit.Mvvm)
    → Services / Application orchestration
      → Core/*  (domain logic — Scheduling, Risk, Parsing, ML contracts)
        → Infrastructure/Persistence  (EF Core + SQLite)
        → Services/ML/*               (local model artifacts on filesystem)
```

Principles:
- UI does not own business logic.
- Business logic is split from view so it can be tested independently.
- Local data is the default source of truth.
- ML is an enhancement, not allowed to block the app.
- All services flow through DI (`ServiceLocator`) — no `static class` in domain.

## 3. Tech stack

| Layer | Technology | Notes |
|---|---|---|
| UI | WPF on **.NET 10** (`net10.0-windows10.0.19041.0`) | `WinExe`, `UseWPF=true`, `UseWindowsForms=true` |
| Language | C# (nullable reference types **enabled**, implicit usings **enabled**) | |
| MVVM | `CommunityToolkit.Mvvm` | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` |
| Charts | `LiveChartsCore.SkiaSharpView.WPF` | Used in Dashboard + Analytics |
| Notifications | `Microsoft.Toolkit.Uwp.Notifications` | Windows toasts |
| DI | `Microsoft.Extensions.DependencyInjection` | composed by `ServiceLocator` |
| DB | **SQLite** + `Microsoft.EntityFrameworkCore.Sqlite` | `SmartStudyData.db` next to the binary |
| ML | `Microsoft.ML` + `Microsoft.ML.FastTree` | local-only model in `%AppData%\SmartStudyPlanner\models\` |
| Tests | `xUnit` + `Microsoft.NET.Test.Sdk` + `coverlet.collector` + `Verify.CommunityToolkit.Mvvm` | 156 passing as of Slice 4 |
| Solution | `SmartStudyPlanner.slnx` (not `.sln`) | important when running `dotnet build` |

Project version: `1.5.0`.

## 4. Folder layout

```text
SmartStudyPlanner/
├── App.xaml(.cs)                # Startup, DB bootstrap, DI bootstrap
├── Models/                      # Entities + DTO-like models
├── Data/                        # AppDbContext, IStudyRepository, StudyRepository
├── Infrastructure/Persistence/  # New repo abstractions + SQLite impls (Slice 4)
│   ├── Repositories/            # IStudyTaskRepository, IStudyLogRepository,
│   │                            #   IMonHocRepository, IUserStatsRepository,
│   │                            #   UserStatsSnapshot
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
│   ├── DecisionEngineService.cs # 42-line facade over SchedulingOrchestrator
│   ├── WorkloadServiceImpl.cs   # IWorkloadService impl
│   ├── WeightConfig.cs          # POCO + IsValid()
│   ├── Analytics/               # IStudyAnalytics + StudyAnalyticsService
│   ├── ML/                      # IMLModelManager, MLModelManager, IModelStorageProvider,
│   │                            #   LocalModelStorageProvider, StudyTimePredictorService,
│   │                            #   SeedDataGenerator, DeviceHelper, Schema/*
│   ├── Pipeline/                # IPipelineOrchestrator + 5 stages + PipelineContext
│   ├── Strategies/              # IClock, IUrgencyRule, IPriorityComponent, parsers
│   └── Telemetry/               # IStudyTelemetry + DebugStudyTelemetry
├── Themes/                      # CommonStyles, Light/Dark, SidebarStyles
├── Converters/                  # HeatLevelToBrushConverter, etc.
├── ViewModels/                  # MVVM screen logic
└── Views/                       # XAML + code-behind
```

## 5. Major subsystems

### 5.1 Presentation
`MainWindow`, `DashboardPage`, `QuanLyMonHocPage`, `QuanLyTaskPage`, `AnalyticsPage`, `SetupPage`, `FocusWindow`, `WorkloadBalancerWindow`.

### 5.2 Planning / decision
`IDecisionEngine` → `DecisionEngineService` (facade) → `SchedulingOrchestrator` → `PriorityEvaluator` + `RawMinutesCalculator` + `StudyTimeSuggestionEngine` + `IStudyTimePredictor`. `WorkloadServiceImpl` consumes priorities to distribute across `ScheduleDay` / `ScheduledTask`.

> Full pipeline + classification/ranking detail with Mermaid diagrams: [pipeline.md](./pipeline.md).

### 5.3 Pipeline
`PipelineOrchestrator` runs 5 stages in `Order`: `ParseInput → Prioritize → BalanceWorkload → AssessRisk → Adapt`. Stages share a `PipelineContext` (Semester, Settings, ReferenceTime, RawInput, ParsedInput, PrioritizedTasks, Schedule, RiskReport, Adaptations, Warnings, Errors, Metadata, Status). Stages can be skipped by policy; errors collected centrally; stop-early on real failures. Note: `ParseInputStage` is a no-op normalizer (`.Trim()`) — real task classification lives in the separate `ParsingOrchestrator` flow (§5.5), invoked at task-creation time, not inside the pipeline.

### 5.4 Risk
`Core/Risk/RiskOrchestrator` (implements `IRiskAnalyzer`) + `RiskAggregator` over component evaluators (deadline urgency 0.5 + progress gap 0.3 + performance drop 0.2). Score → level via `RiskAssessment.FromScore` (≥0.8 Critical / ≥0.6 High / ≥0.3 Medium / else Low). The legacy `Services/RiskAnalyzer/*` folder (adapter + DTOs) was **fully retired** (commits `0346637` → `1b4c2ba` → `191dd17`); the risk subsystem now lives entirely under `Core/Risk/*`.

### 5.5 Parsing
`Core/Parsing/Orchestrators/ParsingOrchestrator` composes `RuleBasedTimeParsingEngine` + `TaskExtractionEngine`, then augments with `IIntentClassifier` (M8-A, **wired** via `IntentClassifierAdapter` → `TextClassifierService` → ML.NET `TextClassifierModelManager`). ML only sets task **type** (Loai), gated at confidence ≥ 0.60; difficulty/deadline stay rule-based; on model-absent/error it falls back byte-equal to heuristic. The old static `Services/SmartParser` facade was **retired** (commit `222cb5a`) — consumers inject `IParsingOrchestrator`.

### 5.6 Analytics
`StudyAnalyticsService` is a pure function over `IEnumerable<StudyLog>`. Outputs: `WeeklyReport` (7-day minutes), `SubjectInsight` (per-subject totals + completion), `ProductivityScore` (label tiers Xuất sắc / Tốt / Trung bình / Cần cải thiện).

### 5.7 ML
`MLModelManager` owns lifecycle. `StudyTimePredictorService` is the only insertion point into `SchedulingOrchestrator`. `LocalModelStorageProvider` reads/writes `%AppData%\SmartStudyPlanner\models\`. See [knowledge/machine-learning.md](../knowledge/machine-learning.md).

### 5.8 Persistence
`AppDbContext` (EF Core) + legacy `IStudyRepository` / `StudyRepository`. New repo abstractions (`IStudyTaskRepository`, `IStudyLogRepository`, `IMonHocRepository`, `IUserStatsRepository`) added in Slice 4 — implementations use `Func<AppDbContext>` factory to support in-memory SQLite tests.

## 6. Runtime composition (`App.xaml.cs`)

1. Open local DB; run `db.Database.Migrate()` (changed from `EnsureCreated()` after the `NgayHoanThanh` bug — see [knowledge/debugging.md](../knowledge/debugging.md)).
2. If `DEV_RESET_DB=1`, `EnsureDeleted()` first, then recreate.
3. Build DI container via `ServiceLocator.Configure()`.
4. Kick `IMLModelManager.InitializeAsync()` on `Task.Run(...)` — exceptions swallowed so app launches regardless.
5. UI shows even if ML fails.

## 7. Architectural strengths and constraints

Strengths
- Clear layer boundaries; no `static class` in domain.
- Central DI container + stage-based pipeline (not a monolith).
- Safe ML fallback wired everywhere.
- Offline-first as the default, explicit.

Constraints
- Windows desktop only for now.
- `ServiceLocator` is still a composition root, not a `HostBuilder`.
- Some ViewModels still resolve services via the static locator instead of constructor injection.
- DB schema is bootstrapped via `Migrate()`; migration story is light.

## 8. Suggested reading order

1. `App.xaml.cs`
2. `Services/ServiceLocator.cs`
3. `Data/AppDbContext.cs`
4. `Services/Pipeline/PipelineOrchestrator.cs`
5. `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs`
6. `Services/DecisionEngineService.cs` (notice how thin it is)
7. `ViewModels/DashboardViewModel.cs`
