# 📚 Smart-Study

> **Intelligent Task Scheduling & Workload Management for College Students**

Stop juggling deadlines. Let Smart-Study orchestrate your academic workflow.

## 🎯 What is Smart-Study?

Smart-Study is a **Windows desktop application** built with WPF and .NET 10, designed specifically for college students who struggle with scheduling and workload distribution. It's not just another to-do list—it's a **personal study assistant** that automatically converts your deadlines into actionable, balanced schedules using a multi-stage decision pipeline and an offline-first machine learning engine.

Instead of manually breaking down projects and managing competing deadlines, Smart-Study does the heavy lifting for you.

## ✨ Key Features

### 🗂️ **Semester & Subject Management**
- Organize your academic year into semesters with subjects and credit hours
- Attach tasks directly to subjects for accurate priority weighting
- Full CRUD for tasks with difficulty ratings, deadlines, and task types

### 🔄 **5-Stage Planning Pipeline**
- **Parse** → **Prioritize** → **Assess Risk** → **Balance Workload** → **Adapt**
- Converts raw task data into a fully balanced, risk-aware schedule in one pass
- Adaptive stage re-tunes weights based on your actual study history

### ⚖️ **Decision Engine**
- Scores every task using four composable components: time pressure, task type, credit weight, and difficulty
- Five urgency rule shortcuts (overdue, just-overdue, imminent, completed, beyond-horizon) for fast classification
- Self-healing weight config: resets to safe defaults if weights drift out of range

### 🔬 **Risk Analyzer**
- Classifies tasks into risk levels (Low → Critical) using a strategy-based pipeline
- Risk column surfaced directly on the Dashboard for at-a-glance awareness

### 🧠 **Offline-First ML Engine**
- Trains a FastTree regression model locally on your own study logs
- Predicts required study minutes per task; falls back to the formula model when confidence < 60 %
- One-click retrain from the Analytics page once you have ≥ 50 study log entries

### 📊 **Study Analytics**
- Weekly study-minutes bar chart (last 7 days)
- Per-subject completion rate chart
- Productivity Score combining completion rate, streak length, and session efficiency

### ⏱️ **Focus Mode**
- Distraction-free focus window for timed study sessions
- Auto-logs study minutes and updates the streak on session complete

### 🎨 **UI / UX**
- Sidebar navigation with section icons
- Dark mode toggle (persists across pages via ThemeManager)
- Stat cards and badge columns on the Dashboard
- Task Notes & Study Links — a three-zone editor for notes, references, and resources attached to any task

### 🔔 **Windows Notifications**
- Native toast notifications for upcoming deadlines and session reminders

## 🛠️ Technology Stack

| Layer | Technology |
|---|---|
| Language | C# (.NET 10) |
| UI Framework | WPF (Windows 10 19041+) |
| Database | SQLite via Entity Framework Core 10 |
| ML Engine | Microsoft.ML 3 + FastTree |
| MVVM | CommunityToolkit.Mvvm 8 |
| Charts | LiveChartsCore / SkiaSharp |
| DI Container | Microsoft.Extensions.DependencyInjection 10 |
| Notifications | Microsoft.Toolkit.Uwp.Notifications 7 |

## 🚀 Project Status

**Version**: 1.5.0 — Active Development

All core milestones (M1–M7 + M6.1) are complete and merged into `dev`. The test suite currently has **119 tests, all passing**, and the build is clean (0 errors).

### ✅ What's Implemented

| Milestone | Feature |
|---|---|
| M1–M2 | ServiceLocator DI root, `IDecisionEngine` / `DecisionEngineService` |
| M3 | `IWorkloadService` / `WorkloadServiceImpl`, schedule models |
| M4 | Risk Analyzer strategy engine, Dashboard risk column |
| M4.6 | Removed static facades; full injectable service graph |
| M5 | 5-stage Pipeline Orchestrator, `PipelineContext`, stage interface |
| M6 | Study Analytics — `StudyLog`, analytics service, three charts, productivity score |
| M6.1 | Task Notes & Study Links — `TaskNote`, `TaskReferenceLink`, editor UI |
| M7 | Offline-first ML Study Time Predictor (FastTree, auto-fallback) |

### 🔲 What's Next (M8)

- **M8-A**: Text classifier for `SmartParser` (natural-language deadline/difficulty parsing)
- **M8-B**: ML-driven Weight Optimizer to replace the static `WeightConfig`

## 📋 Requirements

- **Windows 10** (build 19041) or later
- **.NET 10 Runtime**

## 📖 Getting Started

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

On first launch, complete the **Setup** page to create your first semester and add subjects. The app persists all data locally in a SQLite database alongside the executable.

## 🎓 Who Is This For?

Perfect for college students who:
- Have multiple competing deadlines across several subjects
- Want data-driven study time estimates instead of guesswork
- Prefer a local, privacy-first tool with no cloud dependency
- Like to track streaks and visualize their study habits

## 💡 What Makes Smart-Study Different

Unlike generic task managers, Smart-Study uses a **layered computational pipeline**:
- **Decision Engine**: Scores tasks with composable urgency rules and credit-weighted priority components
- **Risk Analyzer**: Classifies task risk levels using a pluggable strategy pattern
- **Adaptive Pipeline**: Each run re-balances the workload and adjusts weights from real study data
- **Local ML**: Learns your personal study patterns offline—no data ever leaves your machine

## 📝 License

To be determined.

## 👋 Contributing

This is currently a personal project. However, if you're interested in the concept or want to discuss the architecture, feel free to reach out!

---

**Smart-Study**: Because your GPA deserves more than a sticky note. 📚✨
