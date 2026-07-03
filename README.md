# Smart-Study

> Intelligent Task Scheduling and Workload Management for College Students

Stop juggling deadlines. Let Smart-Study orchestrate your academic workflow.

## What is Smart-Study?

Smart-Study is a Windows desktop application built with C# and WPF on .NET 10, designed specifically for college students who struggle with scheduling and workload distribution. It is not just another to-do list — it is a personal study assistant that automatically converts your deadlines into actionable, balanced schedules using a multi-stage decision pipeline and an offline-first machine learning engine.

Instead of manually breaking down projects and managing competing deadlines, Smart-Study does the heavy lifting for you.

## Key Features

### Semester and Subject Management

- Organize your academic year into semesters with subjects and credit hours
- Attach tasks directly to subjects so heavier courses get higher priority
- Add tasks with a difficulty rating, deadline, and task type — Smart-Study handles the rest

### 5-Stage Planning Pipeline

The app runs every task through five automatic steps: Parse → Prioritize → Assess Risk → Balance Workload → Adapt.
Each step hands its result to the next, so by the end you have a balanced, risk-aware schedule — in one pass.
The final Adapt step even re-tunes itself based on your actual study history over time.

### Decision Engine

The Decision Engine scores every task by weighing four factors — time pressure, task type, credit load, and difficulty — and applies five urgency shortcuts (overdue, just-overdue, imminent, completed, or far in the future) to instantly classify it. If the scoring weights drift out of balance, the engine resets itself to safe defaults automatically.

### Risk Analyzer

The Risk Analyzer reads each task's deadline, difficulty, and workload context and assigns it a risk level from Low to Critical. The result appears as a dedicated column on the Dashboard so you can spot trouble before it becomes a crisis.

### Offline Machine Learning Suite

Smart-Study ships three ML models, all trained on-device — no data sent to the cloud or any third-party service:

- **Study Time Predictor** — built on Microsoft.ML and FastTree, estimates how many minutes a task will realistically take. When confidence drops below 60% it falls back to a formula-based estimate. You can retrain it from the Analytics page once you have at least 50 study log entries.
- **Task Type Classifier (TextClassifier)** — identifies the type of a task (exam, assignment, project, etc.) from its title text, wiring natural-language intent directly into the parsing pipeline without manual tagging.
- **Weight Optimizer** — tracks how your actual priority scores correlate with outcomes, then proposes updated scoring weights. You review the suggestion and apply it with one click; the change is persisted as JSON so it survives restarts.

### Study Analytics

- Weekly bar chart showing how many minutes you studied each day over the past week
- Per-subject completion rate chart so you can see which courses need more attention
- Productivity Score that combines your task completion rate, study streak length, and session efficiency into a single number

### Focus Mode

Click into Focus Mode for a distraction-free, timed study session. When you finish, the app automatically logs your study minutes, captures the ground-truth outcome for ML retraining, and updates your streak.

### Interface and Themes

- Sidebar navigation with icon labels for every section
- Dark mode toggle that remembers your preference across all pages
- Dashboard with native XAML charts and a mission-control layout — stat cards, risk badges, and urgency columns at a glance
- MonHoc/BaiTap workspace — tabbed editor that keeps subjects and their tasks side-by-side in one view
- Workload Balancer — a full-page schedule view showing day-by-day task distribution
- Weight Optimizer — review panel with before/after weight comparison and one-click apply
- Task Notes and Study Links — a three-zone editor where you can attach written notes, reference URLs, and study resources to any individual task

### Windows Notifications

The app sends native Windows toast notifications to remind you of upcoming deadlines and study sessions.

## Technology Stack

| What it does | Technology used |
|---|---|
| Programming language | C# on .NET 10 |
| Desktop UI framework | WPF — Windows Presentation Foundation (Windows 10 build 19041 or later) |
| Local database | SQLite, accessed through Entity Framework Core 10 |
| Machine learning | Microsoft.ML 3 with FastTree regression and SdcaMaximumEntropy classification |
| UI data binding | CommunityToolkit.Mvvm 8 — keeps the interface and logic cleanly separated |
| Charts and graphs | LiveChartsCore with SkiaSharp rendering |
| Dependency injection | Microsoft.Extensions.DependencyInjection 10 |
| Windows notifications | Microsoft.Toolkit.Uwp.Notifications 7 |

## Project Status

Version 1.5.0 — Active Development

All core milestones (M1 through M8) are complete and merged. The test suite has 289 tests, all passing, and the build is clean with zero errors.

### What Has Been Built

| Milestone | What was delivered |
|---|---|
| M1–M2 | Dependency injection foundation and the core Decision Engine service |
| M3 | Workload balancing service and schedule data models |
| M4 | Risk Analyzer engine and the risk column on the Dashboard |
| M4.6 | Removed legacy static shortcuts; all services are now fully injectable and testable |
| M5 | Five-stage Pipeline Orchestrator that sequences all planning steps |
| M6 | Study Analytics — study logs, analytics service, three charts, and productivity score |
| M6.1 | Task Notes and Study Links — notes editor and reference link manager per task |
| M7 | Offline-first ML Study Time Predictor with automatic formula fallback |
| M8-A | TextClassifier — SdcaMaximumEntropy model that identifies task type from title text; wired into the parsing pipeline; retrains from a real-data-first seed (903 rows, 5 classes) |
| M8-B | WeightOptimizer — rule-based optimizer that reads WeightChangeLog ground truth and proposes updated scoring weights; review/apply UI with JSON persistence |
| M8 (arch) | God-object refactor: IDecisionEngine decomposed into strategy components; RiskAnalyzerService retired in favour of RiskOrchestrator; StudyRepository split into focused repos; StreakManager made fully injectable |

### What Is Coming Next

Near-term: a **sync-ready data model** and **multi-device two-way LAN sync**, the **Study Optimization Engine** (evolving the Workload Balancer), **M8-C** telemetry-based ML retraining, and **M9** natural-language deadline parsing.

The full, canonical roadmap lives in [`docs/specs/system_roadmap.md`](docs/specs/system_roadmap.md).

## Requirements

- Windows 10 (build 19041) or later
- .NET 10 Runtime

## Getting Started

```bash
# Clone and build
git clone https://github.com/PotatoMine725/Smart-Study.git
cd Smart-Study
dotnet build SmartStudyPlanner.slnx

# Run the app
dotnet run --project SmartStudyPlanner

# Run the test suite
dotnet test SmartStudyPlanner.Tests
```

On first launch, complete the Setup page to create your first semester and add subjects. All data is saved locally in a SQLite database file stored next to the application — no cloud, no account, and no third-party servers. Planned multi-device sync will keep data on your own devices over your local network (still no cloud).

## Who Is This For?

Smart-Study is a great fit for college students who:

- Are juggling multiple deadlines across several subjects at the same time
- Want data-driven estimates of how long a task will actually take, not just guesswork
- Prefer a local, privacy-first tool that works entirely offline
- Like to track study streaks and see their habits visualized over time

## What Makes Smart-Study Different

Most task managers let you write a list and set a reminder. Smart-Study goes further by running your tasks through a layered computational pipeline:

- The Decision Engine scores and ranks every task objectively using urgency rules and credit-weighted components
- The Risk Analyzer flags tasks that are heading toward trouble before they become emergencies
- The Adaptive Pipeline re-balances your workload every time it runs, using your real study data to get more accurate over time
- Three on-device ML models — Study Time Predictor, Task Type Classifier, and Weight Optimizer — learn your personal patterns without an account, cloud, or data sharing

## License

To be determined.

## Contributing

This is currently a personal project. However, if you are interested in the concept or want to discuss the architecture, feel free to reach out!

---

Smart-Study — because your GPA deserves more than a sticky note.
