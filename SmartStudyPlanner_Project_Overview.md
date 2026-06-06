# Smart Study Planner — Project Overview

> Intelligent study planning, workload balancing, risk-aware scheduling, and edge ML for students.

## 1. Project summary

Smart Study Planner is a **student-focused planning system** that turns raw deadlines, subjects, and study logs into an actionable learning plan.  
The current codebase is designed as a **desktop-first, offline-first** application with:

- automatic task prioritization,
- schedule generation with workload balancing,
- risk assessment for overdue / near-deadline tasks,
- analytics for study habits,
- local machine learning support for study-time prediction,
- a modular architecture that is ready for future mobile and cloud expansion.

The current development direction is:

1. **Edge ML integration** for local, privacy-first predictions.
2. **UI overhaul** for a cleaner and more scalable experience.
3. Future evolution toward **mobile / hybrid apps**.
4. **Hybrid storage** with **SQLite local-first** and **cloud DB as opt-in** when cloud services are available.

---

## 2. Product goals

### Core goal
Help students reduce planning overhead by automatically transforming academic workload into a realistic study schedule.

### Design goals
- **Offline first**: the app must remain useful without internet access.
- **Privacy aware**: local data and local inference should be the default.
- **Adaptive**: scheduling and recommendations should react to user behavior.
- **Testable**: business logic is isolated from UI so it can be tested independently.
- **Extendable**: the architecture should support mobile, sync, and cloud later without rewrites.

---

## 3. Current tech stack

| Layer | Technology | Purpose |
|---|---|---|
| UI | **WPF** on **.NET 10** | Desktop interface for the current version |
| App type | `WinExe`, `UseWPF`, `UseWindowsForms` | Windows desktop application host |
| Language | **C#** | Main implementation language |
| Database | **SQLite** + **Entity Framework Core** | Local persistence, offline-first storage |
| ML | **Microsoft.ML** + **FastTree** | Local study-time prediction |
| Dependency Injection | **Microsoft.Extensions.DependencyInjection** | Service registration and composition root |
| MVVM utilities | **CommunityToolkit.Mvvm** | ViewModel support |
| Charts | **LiveChartsCore.SkiaSharpView.WPF** | Analytics and visualization |
| Notifications | **Microsoft.Toolkit.Uwp.Notifications** | Desktop notifications |
| Tests | **xUnit**, **Microsoft.NET.Test.Sdk**, **coverlet.collector** | Unit testing and coverage |

---

## 4. High-level architecture

The application is organized into clear layers:

```text
UI (Views + ViewModels)
        ↓
Application Services
        ↓
Domain / Scheduling Logic
        ↓
Data Access + Local Storage
```

### Layer responsibilities
- **Views**: render the interface and forward user actions.
- **ViewModels**: expose screen state and commands.
- **Services**: contain scheduling, analytics, risk, ML, and orchestration logic.
- **Data**: handle database access and repository operations.
- **Models**: define the core entities and transport objects used across the app.

---

## 5. Main modules

### 5.1 Study planning and scheduling
Responsible for converting semester data, subjects, and tasks into a 7-day study plan.

Key pieces:
- `WorkloadServiceImpl`
- `IDecisionEngine`
- `DecisionEngineService`
- `ScheduleDay`
- `ScheduledTask`

What it does:
- loads unfinished tasks,
- calculates priority for each task,
- estimates required study time,
- distributes tasks across available days,
- keeps daily load under a capacity limit.

---

### 5.2 Decision engine
The decision engine computes task priority using a weighted combination of rule-based components.

Inputs commonly considered:
- deadline proximity,
- overdue status,
- task type,
- difficulty,
- credit weight,
- completion state.

Key classes:
- `PriorityCalculator`
- `WeightConfig`
- `IUrgencyRule`
- `IPriorityComponent`
- `ITaskTypeWeightProvider`

This module is the core of task ranking logic.

---

### 5.3 Risk analyzer
This module assesses whether a task or subject is at risk.

Current risk model combines:
- **deadline urgency**
- **progress gap**
- **performance drop**

Output:
- risk score,
- risk level,
- component scores for explanation.

Key classes:
- `RiskAnalyzerService`
- `RiskAssessment`
- `IRiskAnalyzer`
- `IRiskComponent`

---

### 5.4 Edge ML study-time prediction
This is the local ML subsystem used to predict how many minutes a task may require.

Key classes:
- `MLModelManager`
- `StudyTimePredictorService`
- `SeedDataGenerator`
- `StudyTimeInput`
- `StudyTimeOutput`
- `ModelMeta`

Behavior:
- loads a local model from disk when available,
- trains from seed data when no valid model exists,
- saves model and metadata locally,
- falls back to formula-based estimates when confidence is low or the model is unavailable.

Important design choice:
- ML is an **enhancement**, not a hard dependency.
- the app must still run normally when the model is missing, invalid, or not yet initialized.

---

### 5.5 Analytics
This module summarizes study behavior and progress.

Key outputs:
- weekly study minutes,
- subject-level insights,
- productivity score.

Key classes:
- `StudyAnalyticsService`
- `WeeklyReport`
- `SubjectInsight`
- `ProductivityScore`

---

### 5.6 Persistence layer
The app uses a local SQLite database managed by EF Core.

Key classes:
- `AppDbContext`
- `IStudyRepository`
- `StudyRepository`

Database entities:
- `HocKy`
- `MonHoc`
- `StudyTask`
- `StudyLog`
- `TaskNote`
- `TaskReferenceLink`

Cascade behavior is configured so that semester/subject/task relationships remain consistent.

---

### 5.7 UI and presentation layer
Current desktop UI is organized as WPF pages and windows.

Main screens:
- `MainWindow`
- `DashboardPage`
- `QuanLyMonHocPage`
- `QuanLyTaskPage`
- `AnalyticsPage`
- `SetupPage`
- `FocusWindow`
- `WorkloadBalancerWindow`

Supporting UI pieces:
- `Themes`
- `Converters`
- `ViewModels`

This layer is the main target of the ongoing UI redesign.

---

## 6. Pipeline design

The scheduling flow is implemented as a stage-based pipeline.

### Pipeline stages
1. **ParseInput**
2. **Prioritize**
3. **BalanceWorkload**
4. **AssessRisk**
5. **Adapt**

### Pipeline context
The pipeline passes a shared context object containing:
- semester data,
- raw input,
- parsed input,
- prioritized tasks,
- generated schedule,
- risk report,
- adaptation suggestions,
- warnings,
- errors,
- metadata,
- current status.

### Why this design matters
- stages are isolated,
- execution order is explicit,
- each step can be skipped when policy says so,
- errors are collected centrally,
- the architecture is easy to extend with new stages later.

### Execution result
The orchestrator returns:
- pipeline status,
- per-stage results,
- final schedule,
- risk report,
- adaptation suggestions,
- warnings and errors.

---

## 7. Core data structures

### Academic structure
- **HocKy**: semester container
- **MonHoc**: subject / course
- **StudyTask**: individual study task or assignment
- **StudyLog**: study session record

### Planning / display structure
- **ScheduleDay**: one day in the generated plan
- **ScheduledTask**: one planned task entry
- **TaskDashboardItem**: dashboard-friendly task summary
- **TaskEditorBundle**: grouped task editor data

### Notes and references
- **TaskNote**: task-level notes
- **TaskReferenceLink**: external references / attachments

### ML structures
- **StudyTimeInput**
- **StudyTimeOutput**
- **ModelMeta**

### Pipeline structures
- **PipelineContext**
- **PipelineStageResult**
- **PipelineExecutionResult**
- **AdaptationSuggestion**

---

## 8. Rules

These rules describe how the project is intended to behave.

### 8.1 Product rules
- The app must remain usable **without internet**.
- Local data is the default source of truth.
- Cloud features are optional and should be treated as **opt-in**.
- Predictions should never block the user flow.
- Scheduling should prefer practicality over aggressive optimization.
- The system should adapt from real study logs instead of assuming perfect behavior.

### 8.2 ML rules
- Use local inference first.
- Train / retrain locally when possible.
- Fall back to deterministic formulas if prediction is unavailable.
- Save model metadata for traceability.
- Do not let a bad model prevent the app from running.

### 8.3 Data rules
- Entities should preserve historical study logs.
- Deleting a semester should remove dependent subjects and tasks.
- Deleting a subject should remove dependent tasks.
- Notes and reference links should cascade with task deletion.

### 8.4 Architecture rules
- UI should not contain business logic.
- Services should be injectable and testable.
- New features should fit into the existing layer structure.
- The pipeline should stay stage-based, not become a monolith.
- Local-first behavior should remain the default even after cloud support is added.

---

## 9. Constraints

### Current constraints
- **Desktop only** for the current implementation.
- **Windows-targeted** application host.
- **Local SQLite** is the primary persistence layer.
- **Offline-first** behavior is mandatory.
- **Cloud sync is not required yet** and should not be assumed.
- **ML model quality is limited by local seed data** unless better data becomes available.
- **Model inference must stay lightweight** because it runs on the client device.
- The app currently depends on the Windows desktop ecosystem, so cross-platform work is a future step.

### Product constraints for the future
- Mobile support should not break offline usage.
- Cloud DB should remain optional and not become a hard dependency.
- Synchronization must handle conflict resolution cleanly.
- Hybrid storage must avoid duplicating business logic across local and cloud layers.

---

## 10. Development roadmap

### Phase 1 — Current focus
- stabilize the core desktop app,
- improve the overall UI/UX,
- complete edge ML integration,
- make prediction and scheduling more visible to the user,
- improve test coverage around planning and data flows.

### Phase 2 — Local intelligence hardening
- refine the model training pipeline,
- improve fallback behavior,
- increase confidence reporting,
- add better model metadata and version tracking,
- improve analytics and explainability.

### Phase 3 — Mobile direction
- move toward a mobile-friendly client,
- redesign key flows for smaller screens,
- preserve offline-first usage patterns,
- prepare shared business logic for multiple clients.

### Phase 4 — Hybrid storage
- keep **SQLite as local cache / offline store**,
- add **cloud DB as opt-in** when infrastructure is available,
- design sync for tasks, logs, notes, and metadata,
- implement conflict handling and incremental sync.

### Phase 5 — Ecosystem growth
- calendar integration,
- collaboration / study group support,
- richer recommendations,
- habit insights,
- notification automation,
- cross-device continuity.

---

## 11. Repository structure

```text
SmartStudyPlanner/
├── Data/                  # DbContext and repository layer
├── Models/                # Entities and DTO-like models
├── Services/              # Business logic, ML, pipeline, analytics
│   ├── Analytics/
│   ├── ML/
│   ├── Pipeline/
│   ├── RiskAnalyzer/
│   └── Strategies/
├── Themes/                # Light/Dark styles and shared WPF styles
├── Converters/            # UI converters
├── ViewModels/            # Screen logic
├── Views/                 # WPF pages/windows
└── App.xaml.cs            # Startup, DB bootstrap, DI bootstrap
```

Tests are separated into:

```text
SmartStudyPlanner.Tests/
├── MLTests/
├── Pipeline/
├── RiskAnalyzer/
├── Strategies/
├── DevTools/
└── Helpers/
```

---

## 12. Startup and runtime behavior

At startup the application:

1. initializes the local database,
2. optionally resets the DB in development when requested,
3. configures the dependency injection container,
4. warms up the ML model manager in the background,
5. keeps the UI responsive even if ML loading fails.

This is an important design point: **the app should launch even if the ML layer is unavailable**.

---

## 13. Quality and testing focus

The codebase already has unit tests for:
- analytics,
- decision engine,
- pipeline stages,
- risk analyzer,
- ML storage and prediction behavior,
- strategy rules,
- task notes and development utilities.

This suggests the project is being built with a strong emphasis on:
- deterministic business rules,
- isolated service testing,
- protecting core scheduling logic from UI changes.

---

## 14. Summary

Smart Study Planner is evolving into a **local-first intelligent academic assistant**.

Today it is already structured around:
- modular services,
- a stage-based scheduling pipeline,
- local persistence,
- rule-based decision logic,
- edge ML with safe fallback,
- desktop UI layers ready for redesign.

Next, it is being prepared for:
- a cleaner UI,
- stronger on-device intelligence,
- mobile clients,
- and optional cloud-backed hybrid storage.

The architecture is already aligned with that direction: **offline first, cloud optional, ML local when possible, and business logic separated from presentation**.
