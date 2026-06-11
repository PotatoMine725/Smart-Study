# Smart Study Planner — Roadmap

> Last updated: 2026-06-11
> Source: synthesized from `implementation_plan.md` (v1.6→v2.0), `2026-05-17-god-object-refactor` plan, and M8 specs.

## Snapshot

| Layer | State |
|---|---|
| Build | green (`dotnet build SmartStudyPlanner.slnx`) |
| Tests | **237 pass / 1 pre-existing fail** (`DecisionEngineTests.CalculatePriority_TaskToiHanHomNay`) |
| Branch model | `dev` is canonical |
| Index | GitNexus: 2,521 symbols / 5,935 relationships / 114 execution flows |

## Completed milestones

| ID | Name | Notes |
|---|---|---|
| M1 | DI Container (`Microsoft.Extensions.DependencyInjection` + `ServiceLocator`) | merged |
| M2 | DecisionEngine → instance + `IDecisionEngine` | merged |
| M3 | WorkloadService → instance + `IWorkloadService` | merged |
| M4 | Risk Analyzer engine | merged |
| M4.5 | Risk UI on Dashboard + commit split | merged |
| M4.6 | Migrate call sites + drop static facades | merged (`af673d2`) |
| M5 | Pipeline Orchestrator (5 stages) | merged (`865ca47`, PR #35) |
| M5-TD1–4 | Status constants, end-date wiring, pipeline reuse, adaptations UI | merged |
| M6 | Study Analytics & Insights (StudyLog, 3 charts) | merged (PR #37) |
| M6.1 | Task Notes & Study Links (`TaskNote`, `TaskReferenceLink`) | merged, 141 tests |
| M7 | ML Engine — Study Time Predictor (FastTree, offline-first) | merged |
| UI/UX A–F | Design system, navigation, dashboard, analytics, notes polish, quality gate + telemetry | shipped 2026-05-01 |
| Sidebar upgrade | Hover/active accent bar, ToggleButton template | shipped |
| Analytics heatmap | 52×7 GitHub-style heat grid | shipped |
| Dev reset | Opt-in via `DEV_RESET_DB=1`; DB persists by default | shipped |
| Semester end date | Default `+150 days`, manual override + restore | shipped |
| ML retrain post-reset | `DbSeedTests` seeds 180 logs for pipeline verification | shipped |
| Core/Risk extraction | `Core/Risk/Models` + adapter in `Services/RiskAnalyzer` | shipped 2026-05-12 |
| Refactor Slice 1 | Core contracts (Scheduling, Parsing, ML) | shipped (`5ece84c`) |
| Refactor Slice 2 | `DecisionEngineService` 92→42 lines, split into `Core/Scheduling/*` | shipped (`3b176fb`) |
| Refactor Slice 3 | `ParsingOrchestrator` + `SmartParser` instance facade | shipped 2026-05-18 |
| Refactor Slice 4 | Repository abstractions (`IStudyTaskRepository`, `IStudyLogRepository`, `IMonHocRepository`, `IUserStatsRepository`) | shipped 2026-05-18 |
| Refactor Slice 5 | M8-A — `TextClassifierService` + schema (standalone, no consumer wiring) | shipped 2026-06-05 |
| Refactor Slice 6 | M8-A — classifier wired into parser (`IntentClassifierAdapter`, `ServiceLocator`, `QuanLyTaskViewModel` ML hint) | shipped 2026-06-05 |
| M8-A seed v3 | 5-class 698-row seed (relabeled + synthetic); 96.2% held-out accuracy | shipped 2026-06-05 |
| Refactor Slice 7 | M8-B — `WeightOptimizerService` (rule-based) + `WeightConfigSuggestion` contract | shipped 2026-06-06 |
| Refactor Slice 8 | M8-B — `WeightOptimizerWindow` review/apply UI + `WeightConfigStore` JSON persistence | shipped 2026-06-06 |
| Test structure refactor | Test namespaces mirror prod 1:1; `TestDoubles/` + `Fixtures/` split | shipped 2026-06-09 |
| SmartParser facade retirement | Static `SmartParser` facade removed; `QuanLyTaskViewModel` requires `IParsingOrchestrator` | shipped 2026-06-09 |
| Core/Risk retirement | `RiskAnalyzerService` adapter deleted; `RiskOrchestrator` implements `IRiskAnalyzer` directly | shipped 2026-06-11 |
| M8 Telemetry Slice 0 | `DifficultyLabelLog` + `WeightChangeLog` persistence + repos | shipped 2026-06-11 |
| M8 Telemetry Slice 1A | `DefaultDifficultyKeywordParser` — TaskType prior fallback instead of hard-coded 3 | shipped 2026-06-11 |
| M8 Telemetry Slice 1B | Difficulty ground-truth capture on task save (`DifficultyLabelLog`) | shipped 2026-06-11 |
| M8 Telemetry Slice 2A | Weight-change ground-truth capture on apply (`WeightChangeLog` + cohort snapshot) | shipped 2026-06-11 |
| M8 Telemetry Slice 2B | `OutcomeMaturationService` — cohort outcome fill after 14-day window, idempotent | shipped 2026-06-11 |

## In progress / next up

Nothing active. All planned slices shipped.

The slice plan lives in [active/refactor-god-object.md](active/refactor-god-object.md).

## Out of scope (deferred)

- **Pipeline rehome** (`Services/Pipeline/*` → `Application/UseCases/*`) — independent plan after M8.
- **Core/Capacity** — only when a real need surfaces.
- **Core/Sync + PostgreSQL** — far future (Phase 4 of long-term plan).
- **Mobile / hybrid clients** — Phase 3 of long-term plan; preserves offline-first.
- **Cloud model storage** — opt-in via `IModelStorageProvider`; no work until users ask.
- **Async pipeline end-to-end** — current sync MVP is acceptable.
- **N6** `System.Drawing.Common` NU1904 vulnerability — ~30 min, independent.

## Guardrails for every change

1. `gitnexus_impact` before editing any symbol; report HIGH/CRITICAL to user.
2. `gitnexus_detect_changes` before commit.
3. `dotnet build SmartStudyPlanner.slnx` + `dotnet test --no-build` must stay green (≥237 pass).
4. Never silently mutate `WeightConfig` on low ML confidence.
5. Never let ML availability gate the app — formula fallback must remain.
6. Offline-first stays default; cloud is opt-in only.
