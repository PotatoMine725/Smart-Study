# Smart Study Planner — Agent README

> **Behavior-aware planning system for college students**
> Version: 1.5.0 | Stack: .NET 10, C#, WPF, SQLite + EF Core
> **Docs status:** M5 ✅ | M6 ✅ | M6.1 ✅ | M7 ✅ | DEV-RESET ✅ | M8 planned

---

## 1. Project Vision

Smart Study Planner is a **closed-loop decision system** — not a CRUD app — that:

- Converts raw deadlines into balanced, actionable study schedules ✅
- Adapts based on user behavior and performance ✅
- Prevents overload, procrastination, and inefficient study patterns ✅

**Core loop:**
```
Plan → Execute → Measure → Adapt → Re-plan
```

---

## 2. Architecture

The system follows a **Layered + MVVM hybrid**:

```
Presentation Layer  (WPF Views + ViewModels — no business logic)
        ↓
Application Layer   (StudyPlanService, AssessmentService, ProgressService)
        ↓
Domain Layer        (DecisionEngine, PlannerEngine, BalancerEngine, RiskAnalyzer, AdaptiveEngine)
        ↓
Infrastructure      (SQLite via EF Core, Repository pattern, File storage, Notifications)
```

**Core engine pipeline:**
```
User Input → Parser Engine → Decision Engine → Planner Engine → Balancer Engine → Schedule Output
                                                                                        ↓
                                                               Re-planning ← Adaptive/ML Engine ← User Progress
```

### Layer responsibilities

| Layer | What it does | What it must NOT do |
|---|---|---|
| Presentation | Data binding, UI events | Contain business logic |
| Application | Orchestrate use cases | Access DB directly |
| Domain | All intelligence & scoring | Call UI or DB |
| Infrastructure | Persistence, notifications | Contain scheduling logic |

---

## 3. Key Design Principles

| Principle | Rule |
|---|---|
| **MVVM Strictness** | ViewModels own no UI logic; Views own no business logic |
| **Determinism** | All planner output must be predictable and reproducible |
| **Testability** | Every core feature needs unit tests; system time must be mockable via `IClock` |
| **Local-First** | All scheduling is processed locally before any cloud sync is considered ✅ |
| **Dependency Isolation** | All engines depend on interfaces only — `new DecisionEngine()` ❌ |
| **Pure Functions** | Planner logic: no DB calls, no `DateTime.Now`, no `Random()`, no side effects |

---

## 4. Algorithms

### 4.1 Priority Formula (Decision Engine)

```
Priority =
    w1 * DeadlineUrgency     +
    w2 * CompetencyGap       +
    w3 * ProgressGap         +
    w4 * Difficulty          +
    w5 * TaskWeight          +
    w6 * ConsistencyPenalty
```

**Supporting metrics:**
```
DeadlineUrgency = 1 / (DaysLeft + 1)
CompetencyGap   = 1 - (Score / 100)
ProgressGap     = 1 - ProgressPercent
Consistency     = StudyDays / 7
```

**Dynamic weight adjustment:**
```
If Deadline < 3 days   → increase urgency weight
If user procrastinates → increase penalty weight
If performance improves → decrease competency weight
```

**Current implementation** (`DecisionEngine.cs`):
```
WeightConfig: TimeWeight=0.40, TaskTypeWeight=0.30, CreditWeight=0.20, DifficultyWeight=0.10
```
Task type coefficients: ThiCuoiKy=1.0, DoAnCuoiKy=0.8, ThiGiuaKy=0.6, KiemTraThuongXuyen=0.3, BaiTapVeNha=0.1 ✅

### 4.2 Balancer Algorithm (WorkloadService)

Step 1 — Sort tasks `DESC` by priority  
Step 2 — Greedy allocation: assign each task to the day with lowest current load  
Step 3 — Constraints: max hours/day (user-configured), no excessive same-subject repetition  
Step 4 — Task splitting: if a task doesn't fit in one day's remaining capacity, split it across days

**Future (v2+):** `Minimize Σ(dayLoad − avgLoad)²`

### 4.3 Risk Detection

```
Risk = DeadlineUrgency * 0.5 + ProgressGap * 0.3 + PerformanceDrop * 0.2
```
✅

### 4.4 Adaptive Logic (Edge ML — MVP)

Rule-based, no deep learning:
```
If Progress < ExpectedProgress   → increase priority
If MilestoneScore > EntryScore   → reduce workload
If subject skipped multiple times → increase priority weight

ExpectedProgress = DaysPassed / TotalDays
```
✅

---

## 5. Current Implementation Status (v1.5.0)

**Working:**
- Basic CRUD for Subjects (`MonHoc`) and Tasks (`StudyTask`) ✅
- Rule-based priority scoring with WeightConfig ✅
- Workload balancing with task splitting across 7 days ✅
- SmartParser for natural-language deadline input ✅
- Pomodoro-based FocusMode with time tracking ✅
- Streak tracking, Toast notifications, System Tray background worker ✅
- Light/Dark theme toggle ✅
- Dashboard with LiveCharts visualizations ✅

**Missing (planned for v1.6+):**
- Machine Learning integration ⏳ (M7 done; M8 planned)
- Cloud sync and mobile platforms ⏳
- Pipeline Orchestrator ✅
- Study Analytics ✅
- Task notes & study links ✅
- Dev reset is opt-in via `DEV_RESET_DB=1` ✅
- Theme toggle now works from every page ✅
- Semester end date auto-suggests 150 days and is editable ✅

---

## 6. Technical Debt — Must fix before v1.6

### 6.1 Decoupling & Dependency Injection
- [x] Convert `DecisionEngine` and `WorkloadService` from `static` to instance-based classes ✅
- [x] Create `IDecisionEngine` and `IWorkloadService` interfaces ✅
- [x] Register services via DI in `App.xaml.cs` ✅

### 6.2 Strategy Pattern for Decision Engine
- [x] Isolate task-type weights (Exam vs. Homework) into separate strategy classes ✅
- [x] Separate Time Urgency, Credit Weight, and Difficulty into independent classes implementing a shared interface ✅
- [x] Replace `DateTime.Now` with an `IClock` interface for deterministic testing ✅

---

## 7. Agent Responsibility Split

| Agent | Responsibility | Scope |
|---|---|---|
| **Planner Agent** | Orchestration | Coordinates engine pipeline |
| **Decision Agent** | Priority scoring | `DecisionEngine.cs` ✅ |
| **Balancer Agent** | Time distribution | `WorkloadService.cs` ✅ |
| **Parser Agent** | Input interpretation | `SmartParser.cs` ✅ |
| **ML Agent** | Adaptive learning | `MLModelManager`, `StudyTimePredictorService` ✅ / M8 planned |

---

## 8. Agent Execution Rules (MANDATORY)

### Allowed
- Refactor code structure
- Extract interfaces and apply dependency injection
- Optimize algorithms
- Add non-invasive logging
- Add or update unit tests

### Forbidden
Agents **MUST NOT**:
- Modify the priority formula, risk calculation, or balancer logic without explicit instruction
- Rewrite entire modules without documented reasoning
- Merge unrelated responsibilities into a single class
- Introduce threading into core planner logic
- Add external libraries for core logic
- Inject UI logic into the domain layer
- Use `DateTime.Now`, `Random()`, or DB calls inside algorithm logic
- Use `new DecisionEngine()` or `new WorkloadService()` — depend on interfaces only

### Test-first enforcement
Every change to Planner, Decision Engine, or Balancer **must** include corresponding unit test updates and edge case coverage.

---

## 9. Reference Files

| File | Purpose |
|---|---|
| `Services/DecisionEngine.cs` | Priority calculation, WeightConfig, study time suggestion |
| `Services/WorkloadService.cs` | 7-day schedule generation with task splitting |
| `Services/SmartParser.cs` | NLP-style deadline parsing from free text |
| `Services/StreakManager.cs` | Daily study streak persistence |
| `Services/ThemeManager.cs` | Light/Dark theme switching |
| `Data/AppDbContext.cs` | EF Core SQLite context with cascade delete |
| `Data/StudyRepository.cs` | Async repository with transaction safety |
| `Data/IStudyRepository.cs` | Repository contract |
| `Models/HocKy.cs` | Semester model (root aggregate) |
| `Models/MonHoc.cs` | Subject model |
| `Models/StudyTask.cs` | Task model with priority, difficulty, status |
| `Models/TaskDashboardItem.cs` | Display-only DTO for dashboard |
| `ViewModels/DashboardViewModel.cs` | Main dashboard logic, charts, streak, schedule |
| `ViewModels/FocusViewModel.cs` | Pomodoro timer + time tracking |
| `ViewModels/QuanLyMonHocViewModel.cs` | Subject CRUD |
| `ViewModels/QuanLyTaskViewModel.cs` | Task CRUD + SmartParser integration |
| `ViewModels/WorkloadBalancerViewModel.cs` | Balancer UI |
| `ViewModels/SetupViewModel.cs` | Semester setup / load from DB |

---

## 10. Development Roadmap

| Phase | Feature |
|---|---|
| **v1.6** | DI refactor, Strategy pattern, IClock injection, full test coverage ✅ |
| **v2.0** | Pipeline Orchestrator ✅, Study Analytics ✅, Task notes & study links ✅, M7 StudyTimePredictor ✅, M8 ML suite planned |
| **v3.0** | Mobile (iOS/Android hybrid), Cloud sync ⏳ |
| **Future** | Study Analytics follow-ups, habit tracking, workload insights, M8 rollout |

**Status summary:**
- M5 Pipeline Orchestrator ✅
- M6 Study Analytics ✅
- M6.1 Task notes & study links ✅
- M7 StudyTimePredictor ✅
- M8 ML suite expansion 🟨 planned
