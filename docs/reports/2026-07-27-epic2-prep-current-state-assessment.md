# Epic 2 Preparation — Current State Assessment (CSA)

**Date:** 2026-07-27
**Author:** Claude (Opus 5), dispatched by repo owner
**Baseline commit:** `d3deca7b5401aade47972ab9b5929c2b85352707` (branch `dev`)
**Task source:** `Prompt/epic_2_prep.md`

> **What this document is.** A descriptive technical baseline of the system exactly as
> it exists at `d3deca7`. It is **not** a code review, **not** a refactoring proposal,
> **not** an implementation plan, and contains **no** Epic 2 tasks, estimates, or
> roadmap changes. Every claim is anchored to `file:line` at the baseline commit or to
> a command whose output is recorded in §Verification.

---

## Scope

Entire codebase: application architecture, domain layer, services, infrastructure,
parsing, scheduling, analytics, machine learning, database, ViewModels, dependency
injection, background services, configuration, and tests. Documentation was consulted
for context only; **source code was treated as the sole source of truth** and every
inherited doc claim used in this report was re-verified against code at `d3deca7`
(two were found stale — see §12).

Method: a pinned baseline (SHA, build, warnings, test count, branch divergence)
established first, then six independent read-only assessments dispatched in parallel
(ML · Smart Add · Scheduling · Persistence · App Wiring · Tests), each required to
return `file:line` anchors. No file in the repository was modified by this assessment.

---

# 1. Executive Summary

The system is a single-user, offline-first WPF desktop application on .NET 10 with
SQLite persistence, a heuristic parsing front-end, a greedy workload allocator, two
trained ML models, and a 346-case xUnit suite that passes green in ~3 seconds. Epic 1
(Sync-Ready Data Model) is shipped and visible in code: all six non-telemetry entities
carry the full D-I sync metadata block, and all twelve write paths converge on a single
stamping seam.

Four facts dominate the current-state picture and are the ones most likely to
invalidate planning assumptions:

1. **`dev`, `origin/dev`, and `origin/main` are byte-identical at `d3deca7`.** There is
   no divergence in either direction, so there is exactly one trunk to plan against.
   Note the distinction that ancestry hides: the #46/#47 native-charts redesign commits
   (`ced372b`, `4122a42`) *are* reachable from HEAD, but the `d3deca7` sync was
   **ui_rf-authoritative**, so their tree content was **not adopted** — HEAD differs from
   `ced372b` by 141 files. That redesign remains parked in history, not shipped. The
   Dashboard's native `DonutChart` is unrelated to it: `Controls/DonutChart.xaml` was
   added by `7225dba`, which predates the split and is common to both lines.
2. **There is no CI.** `.github/workflows/` exists on disk but is empty and entirely
   untracked (`git ls-files .github` returns nothing). Every green build and green test
   run in this project's history is a local, manual result.
3. **The scheduler is deadline-blind.** The allocator picks the least-loaded day under
   capacity and never reads `HanChot`. There is no optimizer, no constraint validator,
   and no objective evaluator anywhere in the codebase — a repo-wide grep for those
   concepts returns zero matches. `WorkloadServiceImpl.GenerateSchedule` has no test
   coverage of any kind.
4. **"Smart Add" and `Services/Pipeline/**` are two disjoint systems.** They share zero
   code. Smart Add is a single straight-line method with no stages; the staged pipeline
   is a separate construct whose input stage is dead in production.

Maturity is uneven by design boundary rather than by feature: persistence and the sync
metadata layer are the most mature (single seam, full test coverage of the stamper's
callers, proven invariants); parsing and scheduling are heuristic and shallow-tested;
ML is trained-and-wired but gated by hand-rolled confidence arithmetic that in one case
bypasses the policy contract written for exactly that purpose. Presentation-layer code
(Views, Converters, Controls) has zero automated coverage.

A fifth point deserves emphasis because it cuts the other way: **the implementation has
essentially not deviated from the plan.** Eight candidate deviations were tested against
`docs/specs/system_roadmap.md`. Four had no plan side at all. Several others turned out to
be things the roadmap explicitly specifies (the `EnsureColumns` upgrade path, tombstones
replacing hard cascades, the thin decision-engine facade, the formula-fallback invariant
that keeps ML advisory). The remainder sit in the roadmap's **Part B**, which the document
itself labels "aspirational: target direction that may run ahead of code" (`:7`) — unbuilt
design, not drift. **Exactly one sourced deviation survives:** the roadmap assumes a CI
exists (`:18`) and none does. The gap between plan and code is far narrower than the
volume of findings in §8 suggests; most of §8 is *known* debt, much of it already on the
roadmap's own deferred ledger.

The project builds clean from a fresh clone — producing `SmartStudyPlanner.exe` — and the
test suite is green. No genuine blocker to Epic 2 *planning* was found. Three items are
flagged in §11 as things whose resolution changes what a plan can safely assume.

---

# 2. Architecture Snapshot

## 2.1 Solution shape

Four projects in `SmartStudyPlanner.slnx`. Target framework
`net10.0-windows10.0.19041.0`. UI is WPF; MVVM via CommunityToolkit.Mvvm.

| Layer | Location | Contents |
|---|---|---|
| Presentation | `Views/`, `Controls/`, `Converters/`, `MainWindow.xaml.cs` | 9 views, custom `DonutChart`, 11 converters |
| ViewModels | `ViewModels/` | Dashboard, QuanLyTask, Setup, WorkloadBalancer, WeightOptimizer, … |
| Services | `Services/` | Strategies, Pipeline, ML, Telemetry, Analytics |
| Core (domain logic) | `Core/` | Parsing, Scheduling, Risk, ML contracts |
| Infrastructure | `Infrastructure/Persistence/` | Repository interfaces + SQLite adapters |
| Data | `Data/` | `AppDbContext`, schema seams, `SyncStamper` |
| Models | `Models/` | Entities (`StudyTask`, `StudyLog`, …) |

Domain naming is Vietnamese throughout (`HanChot`, `DoKho`, `MucDoCanhBao`,
`TrangThai`, `LoaiTask`). This is consistent and is not treated as debt.

## 2.2 Dependency flow as it actually runs

```
App.xaml.cs ─ OnStartup ─► ServiceLocator.Configure()  ─► MS.DI ServiceProvider
                        └► AppStartup (EnsureCreated + 3 patch seams + 1 backup)
                        └► 1-minute notification timer (async void)

MainWindow.xaml.cs ──new──► ViewModels ──► Repositories (Infrastructure)
                                       └─► Services ──► Core
                                       └─► DbContext (via repos)
```

Two deviations from the nominal layering are structural rather than incidental:

- **ViewModels are never resolved from DI.** They depend on interfaces in their
  constructors, but every one is `new`-ed in code-behind. The container holds the
  services; the composition of ViewModels happens in the window.
- **ViewModels reach past the service layer to repositories directly**, and
  `MainWindow.xaml.cs:98-117` runs a full data-load + priority-scoring loop inside the
  window itself. `DashboardViewModel.cs:349-350` constructs a `Window` and calls
  `ShowDialog()` from a ViewModel. `DashboardViewModel.cs:89` executes the entire
  pipeline synchronously from the constructor.

## 2.3 Dependency injection

`ServiceLocator` is a static wrapper over `Microsoft.Extensions.DependencyInjection`.

- **Every registration is `AddSingleton`.** Zero transient, zero scoped, no scope is
  ever created.
- `Provider => _provider ??= BuildProvider()` (`ServiceLocator.cs:27`) means any
  `Get<T>()` reaching the locator before `Configure()` runs silently constructs a
  **second, independent provider**. Nothing currently triggers this, but the seam exists.
- `AddSingleton<AppDbContext>()` (`ServiceLocator.cs:40`) is a **dead registration** —
  no code path resolves `AppDbContext` from the container.
- 27 residual `ServiceLocator` call sites remain across `App.xaml.cs`, 8 ViewModels, and
  `MainWindow.xaml.cs`.

There is no Generic Host and no `IHostedService`. The only background work is a
`DispatcherTimer`-style 1-minute notification tick, implemented `async void` with no
`try`/`catch` and no de-duplication of toasts.

## 2.4 Extension points that exist today

These are seams already present in code, listed descriptively — not recommendations.

| Seam | Where | State |
|---|---|---|
| `IMlConfidencePolicy` | `Core/ML/` contracts | Implemented by `DefaultMlConfidencePolicy`; consumed by the text classifier path only |
| 5 scheduling strategy interfaces | `Core/Scheduling/` | Declared, but concrete types are `new`-ed inside `SchedulingOrchestrator`'s constructor — composition is closed |
| `ISchedulingOrchestrator` | `Core/Scheduling/` | Declared; never used as a type anywhere |
| Repository interfaces | `Infrastructure/Persistence/` | One implementation stack (SQLite); genuinely substitutable, used by tests |
| `IClock` / `SystemClock` | `Core/` | Substituted by `FakeClock` in tests; 184 direct wall-clock call sites remain across 27 files |
| `IStreakStore` / `IStreakManager` | Services | Injectable since `27ffd3e` |
| `SyncStamper` | `Data/` | Single choke point all 12 write sites pass through |
| Pipeline stages | `Services/Pipeline/` | Stage abstraction exists; `ParseInputStage` is dead in production |

## 2.5 Third-party surface

EF Core 10.0.5 + SQLite · Microsoft.ML 3.0.1 (+ FastTree) · CommunityToolkit.Mvvm 8.4.0
· LiveChartsCore.SkiaSharpView.WPF 2.0.0-rc6.1 · Microsoft.Toolkit.Uwp.Notifications
7.1.3 · MS.Extensions.DependencyInjection 10.0.7 · xUnit 2.9.3 + coverlet.

Two packages carry advisories surfaced by every build: `SQLitePCLRaw.lib.e_sqlite3`
2.1.11 (NU1903, high) and `System.Drawing.Common` 4.7.0 (NU1904, critical). Both arrive
transitively.

**Chart rendering is split across two technologies.** The Analytics page uses
LiveChartsCore; the Dashboard uses a hand-drawn native `Controls/DonutChart.xaml{,.cs}`
plus XAML rectangles.

`Verify.CommunityToolkit.Mvvm` is referenced by the **production** csproj and is used by
neither project.

---

# 3. Capability Inventory

Maturity is scored on evidence only: **Production** = implemented, reachable from the
UI, and covered by tests; **Working** = implemented and reachable, thin or no automated
coverage; **Partial** = implemented but not fully reachable or fully wired;
**Scaffolded** = code exists, no production consumer.

| Capability | Status | Maturity | Major dependencies |
|---|---|---|---|
| Task CRUD + semester (`HocKy`) management | Implemented | Production | EF Core, SQLite, repositories |
| **Sync-ready data model (Epic 1 / D-I)** | Implemented | Production | `SyncStamper`, `AppDbContext`, all repos |
| Smart Add (NL task entry) | Implemented | Working | `ParsingOrchestrator`, keyword parsers, `IntentClassifierAdapter` |
| Scheduling / workload balancing | Implemented | **Working, untested** | `WorkloadServiceImpl`, `SchedulingOrchestrator` |
| Priority scoring / decision engine | Implemented | Working | `DecisionEngineService` (pass-through facade), rules |
| Risk evaluation | Implemented | Working | 3 evaluators (1 untested), `RiskAggregator` (untested) |
| Analytics + charts | Implemented | Production (service layer) / Working (VM-side heatmap + narrative untested) | `IStudyAnalytics`, `IStudyLogRepository`, LiveChartsCore |
| Toast notifications | Implemented | Partial | Uwp.Notifications, 1-min `async void` timer, no de-dup |
| ML — study-time prediction (A1/M7) | Implemented | Partial | Microsoft.ML FastTree, telemetry, `MLModelManager` |
| ML — intent/text classification (A2/M8-A) | Implemented | Partial | SdcaMaximumEntropy, embedded `seed_intents.csv` |
| Weight optimizer (M8-B) | Implemented | Working | Rule-based; **no model**; UI-reachable |
| Streak tracking | Implemented | Production | `IStreakStore` (JSON file) |
| Backup | Implemented | **Narrow** | Runs **once**, only on schema upgrade |
| Offline operation | Implemented | Production | Entire app is local-only; no network code |
| Theme switching | Implemented | Partial | `ThemeManager`; **not persisted** across sessions |
| Database migrations | **Not implemented** | — | `EnsureCreated()` + 3 ad-hoc patch seams |
| LAN sync (Epic 2) | Not started | — | — |
| SOE (Epic 3) | Not started | — | — |

## 3.1 Analytics — detail

Delivered as roadmap **M6** ("Study Analytics & Insights (StudyLog, 3 charts)", `system_roadmap.md:31`).
Computation is **split across two layers**, which is the single most important structural
fact about this capability.

**Pure computation layer** — `Services/Analytics/StudyAnalyticsService.cs`, a `sealed`
stateless implementation of `IStudyAnalytics` with exactly three functions. It holds no
repository reference; all data arrives as arguments.

| Function | Anchor | Behaviour |
|---|---|---|
| `ComputeWeeklyMinutes` | `:11-25` | **Always exactly 7 day-buckets** ending at `referenceDate`, regardless of the caller's selected range |
| `ComputeSubjectInsights` | `:27-53` | Groups subjects by `MonHocIdentity.Normalize` (Epic 1 / M1.3, `:36`); falls back to `t.ThoiGianDaHoc` when a task has no logs (`:41-42`) |
| `ComputeProductivityScore` | `:55-60` | `completionRate*50 + min(streak,30)/30*30 + timeEfficiency*20`, rounded. Weights are hard-coded literals |

`ProductivityScore.Label` bands are `85 / 70 / 50 / 30` (`Models/ProductivityScore.cs`).

**ViewModel computation layer** — `ViewModels/AnalyticsViewModel.cs` (283 lines) computes
three further analytics products that never enter the service:

| Product | Anchor | Behaviour |
|---|---|---|
| Heatmap | `:219-248` | 7 × 52 = **364 cells**, Monday-aligned, level thresholds `0 / 30 / 60 / 120` minutes |
| Narrative | `:201-217` | Current-7-day vs. previous-7-day minute delta; weakest subject by completion rate |
| Productivity inputs | `:165-171` | `completionRate` and `timeEfficiency` are derived here, not in the service |

**Data flow.** `LoadAsync:87` → `IStudyLogRepository.GetForHocKyAsync(_hocKy)` → in-memory
filter by range (7/30/90) and subject (`ApplyFilters:111-183`) → the three service calls →
LiveChartsCore `ISeries`/`Axis` objects assigned directly to observable properties
(`:141-163`). The ViewModel therefore **holds chart-library types as its public surface**.

**Consequence of the persistence gap.** `SqliteStudyLogRepository.GetForHocKyAsync:45-48`
is missing `!l.IsDeleted`. That method is Analytics' *only* data source
(`AnalyticsViewModel.cs:87`), so **tombstoned study logs currently flow into every
analytics output** — weekly chart, subject insights, productivity score, heatmap, and
narrative. The parallel gap at `SqliteUserStatsRepository.cs:25` feeds `MissRate` and
`AverageDelayDays` the same way. This is filed under persistence in §8.1 but its
user-visible surface is Analytics.

**Other observations.** `HasEnoughData = _allLogs.Count >= 50` (`:88`) gates the retrain
button. `DateTime.Today` is read directly at `:121, :140, :203-204, :226` — Analytics does
not use `IClock`. `LoadAsync` wraps everything in a bare `catch` (`:96-101`) that sets
`HasError`. Telemetry emits `analytics_open` and `analytics_filter_changed`. The
production constructor (`:57-58`) resolves via `ServiceLocator` and passes
`mlModelManager: null`, with `:257` falling back to `ServiceLocator.Get<IMLModelManager>()`
at call time — consistent with §2.3 (ViewModels are not DI-resolved). The stale-render fix
is visible as `ResetAnalyticsOutputs` (`:187-199`), which clears all filter-driven outputs
so an empty filter renders empty rather than retaining the prior filter's charts.

**Coverage.** Analytics is among the better-covered areas: `AnalyticsServiceTests.cs`
(113 lines), `AnalyticsViewModelFilterTests.cs` (87), `AnalyticsViewModelRetrainTests.cs`
(127). Not covered: `BuildHeatmap` and `BuildNarrative` have no dedicated tests, and
`Views/AnalyticsPage.xaml.cs` is in the zero-coverage `Views/` set.

---

# 4. Machine Learning Assessment

Three components are commonly described as "ML". Only two are models.

## 4.1 A1 — Study-time regressor (M7)

| Aspect | Current state |
|---|---|
| Purpose | Predict minutes required for a task |
| Algorithm | FastTree regression — 20 leaves, 100 trees, min 5 examples per leaf |
| Determinism | `MLContext(seed: 42)` |
| Training source | **Synthetic by default** — `SeedDataGenerator.cs:12-40` emits a 180-row seed. A real-telemetry training path exists and is gated at `MinRows = 50` |
| Quality gate | R² ≥ 0.45 (`MLModelManager.cs:106`) |
| Inference flow | `StudyTimePredictorService` → model → confidence arithmetic → gate |
| Confidence | **Not a model probability.** Computed as `1 - clamp(abs(predicted - formula) / max(formula, 1), 0, 1)` — i.e. agreement with the heuristic formula. Gate ≥ 0.6, hard-coded (`StudyTimePredictorService.cs:41-45`) |
| Policy contract | **Never consulted.** `IMlConfidencePolicy` documents itself as the shared gate; this path does not call it |
| Readiness signal | `IsReady = true` is assigned **unconditionally** at `MLModelManager.cs:151`, including when the R² gate rejected the model. The effective guard is the `-1` sentinel returned at `:170` |
| Fallback | Formula-based estimate |
| Consumers | **Display only.** The scheduler does not consume the prediction (see §6) |
| Retraining | **UI-reachable** via `AnalyticsViewModel.RetrainModel` (`:250-268`), gated on `HasEnoughData` (≥ 50 logs, `:88`). It prefers real telemetry when `real.Count >= StudyTimeTrainingDataSource.MinRows`, else falls back to `SeedDataGenerator.Generate()` (`:259-261`) |
| Dead code | `EvaluateR2Async` has no production caller |

## 4.2 A2 — Text/intent classifier (M8-A)

| Aspect | Current state |
|---|---|
| Purpose | Classify raw Smart Add input into an intent / task type |
| Algorithm | SDCA Maximum Entropy (multiclass logistic) |
| Training source | Embedded `seed_intents.csv`, 903 rows |
| Quality gate | **None.** No accuracy gate and **no train/test split** — the model is persisted unconditionally. This is documented as intentional at `MLModelManager.cs:139-141` |
| Confidence | Raw uncalibrated `Score.Max()` |
| Thresholds | 0.75 / 0.60 (`DefaultMlConfidencePolicy.cs:13-14`) |
| Output constraint | `DoKho` is hard-pinned to `null` at `TextClassifierService.cs:35` — **difficulty is never model-predicted** |
| Retraining | The classifier's own retrain path has **no UI entry point**. (The retrain button on the Analytics page drives the *A1* study-time model, not this one.) |
| Consumers | Smart Add, via `IntentClassifierAdapter` |

## 4.3 Weight optimizer (M8-B) — not a model

Rule-based. `MaxShift = 0.15`. Its "confidence" is a data-sufficiency measure, not a
model output. UI-reachable, and it **never auto-applies** — the user confirms.

## 4.4 Cross-cutting ML facts

- **No model artifact is committed to the repository.** Both `.zip` models are generated
  per-machine at runtime. A fresh clone starts with no trained model.
- The two models use **two different confidence philosophies** (heuristic-agreement vs.
  raw score) and only one of them routes through `IMlConfidencePolicy`.
- Every ML path has a non-ML fallback; no user-facing feature fails when a model is
  absent. This satisfies the roadmap's hard invariant "*Never let ML availability gate
  the app — formula fallback must remain*" (`system_roadmap.md:122`).
- The weight optimizer never auto-applies, satisfying "*Never silently mutate
  `WeightConfig` on low ML confidence*" (`system_roadmap.md:121`).
- **Measured against the roadmap's ML Confidence & Fallback Policy** (`system_roadmap.md`
  §10, `:543-586`) — which sits in **Part B and is explicitly aspirational** (`:7`,
  "target direction that may run ahead of code"), so the gaps below are **unbuilt design,
  not broken contracts** — three requirements have no implementation:
  - §10 states "All ML outputs MUST include confidence score **and uncertainty
    estimation**." Neither model emits an uncertainty estimate.
  - §10 defines **three** confidence tiers (High → auto-apply, Medium → confirm,
    Low → fallback). The parser path collapses High and Medium into one branch
    (`IntentClassifierAdapter.cs:34-36`), leaving two tiers in practice.
  - §10's fallback pipeline has a single "Confidence Validation" node. The A1 path
    bypasses `IMlConfidencePolicy` and applies its own hard-coded 0.6
    (`StudyTimePredictorService.cs:41-45`).

## 4.5 Prediction capabilities currently served by heuristics

These are predictions the system makes **without** a model, listed for completeness:

- Task duration (the raw-minutes formula) — and it is this formula, not the model, that
  the scheduler actually consumes.
- Priority score (`DecisionEngineService` rules; weights 0.5 / 0.3 / 0.2 as hard-coded
  consts).
- Risk level (thresholds 0.8 / 0.6 / 0.3).
- `MucDoCanhBao` (warning level) — derived from the priority score.
- Deadline extraction, task-type detection, difficulty detection in Smart Add.

---

# 5. Smart Add Assessment

## 5.1 Headline structural fact

**`Services/Pipeline/**` is not Smart Add's pipeline.** The two systems share zero code.
Anything a plan assumes about "the pipeline" applies to whichever of the two is meant,
and they are not interchangeable.

## 5.2 The actual flow

```
QuanLyTaskViewModel.PhanTichNhapNhanh      (:257-279)
        └─► ParsingOrchestrator.Parse       (:37-67)
                ├─ deadline keyword parser        (substring matching)
                ├─ task-type keyword parser       (substring matching)
                ├─ difficulty keyword parser      (token-based, negation-aware)
                └─ IntentClassifierAdapter → TextClassifierService (ML)
```

`ParsingOrchestrator.Parse` is a **single straight-line method with no stages**.

**Extracted:** deadline, task type, difficulty.
**Not extracted:** subject, duration, priority, time-of-day.
**Task name:** the raw input, verbatim.

## 5.3 Difficulty parsing (post-`c163135`)

The negation fix touched exactly one file. Its algorithm: NFC-normalize → tokenize on
`\p{L}+` → ordinal token equality → a 2-token negation window → 10 negator terms →
**suppress to prior**, not invert. Deadline and task-type parsers were **not** migrated
and still use unbounded substring matching.

## 5.4 Confidence gates and fallback

ML runs, but its authority is narrow — and this narrowness is **documented as the
intended shipped state**, not a drift. `system_roadmap.md:457-459` states: "*the system
is heuristic-first; the parser is the one place ML has precedence, applied per output
field with a confidence-gated fallback (≥ 0.60, else heuristic). Shipped today: ML
overrides task type only; difficulty and deadline are rule-based.*" Code matches that
sentence exactly.

- ML can only ever change the **task type**. Nothing else it produces is applied.
- `AutoApplyThreshold = 0.75` is **dead on the parsing path**: the adapter collapses
  AutoApply and Review into the same branch (`IntentClassifierAdapter.cs:34-36`), so the
  0.75/0.60 distinction has no behavioural effect here. The effective contract is the
  roadmap's single ≥ 0.60 gate; the 0.75 tier exists in the policy type with no consumer.
- ML sees the **raw** input string; heuristics see the **lowered** string. The two
  layers do not see identical text.
- Fallback is heuristic-only output; no path fails closed.

## 5.5 Known limitations (16 confirmed, anchored)

The full anchored list was produced during assessment; the load-bearing ones:

| # | Limitation |
|---|---|
| L1 | ML can only ever change task type |
| L3 | `ParseSource.MlOverridden` is declared (`ParseResult.cs:31`) and **never emitted** — zero consumers |
| L4 | `AutoApplyThreshold = 0.75` is dead on the parsing path (`IntentClassifierAdapter.cs:34-36`) |
| L6 | ML and heuristics receive differently-normalized input |
| L7 | Deadline and task-type parsers use unbounded substring matching |
| L16 | `ParseInputStage` is dead in production — `DashboardViewModel` never sets `RawInput` |

---

# 6. Scheduling Assessment

## 6.1 The allocator

```csharp
// SmartStudyPlanner/Services/WorkloadServiceImpl.cs:77-79
var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes)
                   .OrderBy(d => d.TotalMinutes)
                   .FirstOrDefault();
```

`:81-91` is the overflow-day append. **This is the entire allocation strategy: pick the
least-loaded day still under capacity; if none, append to an overflow day.**

- **Zero `HanChot` references exist in the allocator.** `ScheduledTask` carries no
  deadline field at all. The scheduler cannot express, let alone satisfy, a deadline
  constraint. *(Verified still true at `d3deca7` at the exact line anchors previously
  documented.)*
- **ML prediction is display-only.** The allocator consumes the heuristic formula.
- **Two divergent copies of the raw-minutes formula exist** in the codebase.

## 6.2 What does not exist

A repo-wide grep for
`IConstraintValidator|IObjectiveEvaluator|IOptimizer|ISolver|Objective|Constraint|Feasib|annealing|hill.?climb|backtrack`
returns **zero matches**. There is no optimizer, no constraint validator, no objective
function, and no feasibility check anywhere in the system.

This is **on-plan, not a deviation.** The roadmap places exactly these components in the
Study Optimization Engine (Epic 3): "*deadline feasibility, capacity and calendar limits
are hard constraints (Constraint Validator); objective = quality only (`w1…w5`);
feasibility never worsens (`violations(out) ≤ violations(in)`)*"
(`system_roadmap.md:83-84`), with "**Pass accept/commit semantics still OPEN —
implementation blocked on it**" (gate G2, `:85`). The roadmap also names the intended
seam — "*Phase it behind an `IScheduleOptimizer` strategy seam (Load Balancer +
Constraint Evaluator first)*" (`:376-377`). **`IScheduleOptimizer` does not exist in the
codebase.** The deadline-blindness described above is therefore the documented pre-SOE
state, not drift.

## 6.3 Decision engine

`DecisionEngineService` is a **pure 6-member pass-through facade** over the rule
components. It holds no logic of its own. This is the planned end-state, not erosion —
roadmap M8 (arch) records "`DecisionEngineService`→42-line facade" as shipped
(`system_roadmap.md:36`).

- Risk weights `0.5 / 0.3 / 0.2` are hard-coded consts.
- `RiskLevel` thresholds are `0.8 / 0.6 / 0.3`.
- `BeyondHorizonRule` uses `HorizonDays = 60`.

## 6.4 `MucDoCanhBao` — a parallel legacy channel

`MucDoCanhBao` is derived from the priority score and is **duplicated at three sites**.
It is a separate warning channel from `RiskLevel`, not a projection of it.

Its constructor gap is still latent, and is **already tracked** in
`system_roadmap.md:103-113` (§A.4, surfaced 2026-07-19 during the B4 reopen, explicitly
"**not surveyed**" — no audit of existing call sites was performed). Today every persisted
task is safe only because `QuanLyTaskViewModel.TinhDiemVaSapXep()` stamps the field before
each save; any future call site that saves a `StudyTask` without routing through that
ViewModel hits `SQLite Error 19: NOT NULL constraint failed`.

```csharp
public string MucDoCanhBao { get; set; }   // Models/StudyTask.cs:27 — NOT NULL in schema
...
public StudyTask(string tenTask, DateTime hanChot, LoaiCongViec loaiTask, int doKho)
{
    MaTask = Guid.NewGuid(); TenTask = tenTask; HanChot = hanChot;
    LoaiTask = loaiTask; DoKho = doKho; TrangThai = StudyTaskStatus.ChuaLam;
}   // :45-53 — MucDoCanhBao is never set
```

## 6.5 Composition

Five scheduling strategy interfaces exist, but their implementations are `new`-ed inside
`SchedulingOrchestrator`'s constructor — the composition is closed to substitution.
`ISchedulingOrchestrator` is declared and never used as a type.

## 6.6 Coverage

`WorkloadServiceImpl.GenerateSchedule` has **no test coverage whatsoever**. The only
test artefact is `StubWorkloadService`, which replaces it rather than exercising it.

---

# 7. Database & Persistence

## 7.1 Schema

9 `DbSet`s. All primary keys are `Guid`.

| Group | D-I sync block (`Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`) |
|---|---|
| 6 non-telemetry entities | **Present on all** |
| 3 telemetry tables | **Absent on all** |

## 7.2 Cascade configuration

Cascade config at `AppDbContext.cs:46-52` is **inert at the SQL level**. It exists to
drive EF `ChangeTracker` fixup so that `SyncStamper` observes the right entity set — not
to enforce referential action in SQLite.

## 7.3 Soft delete

**There is no global query filter.** `IsDeleted` filtering is applied manually, per
query. Two gaps are proven:

- `SqliteStudyLogRepository.GetForHocKyAsync:45-48` — missing `!l.IsDeleted`; returns
  tombstoned logs.
- `SqliteUserStatsRepository.cs:25` — missing `!IsDeleted`; tombstoned tasks feed
  `MissRate` and `AverageDelayDays`.

## 7.4 Repositories

One stack: interfaces in `Infrastructure/Persistence/` + SQLite adapters. Not two
parallel stacks. Substitutable and exercised by tests against **real SQLite** — the test
suite uses no EF InMemory provider.

## 7.5 Migrations and upgrade path

**No EF Migrations exist.** The upgrade path is:

```
AppStartup.cs:14-46
  ├─ EnsureCreated()
  ├─ patch seam 1
  ├─ patch seam 2
  └─ patch seam 3   (sequenced explicitly)
```

`SyncSchema.NeedsUpgrade(db)` gates the upgrade branch.

## 7.6 Backup

**Backup runs exactly once**, inside `if (SyncSchema.NeedsUpgrade(db))`. There is **no**
periodic backup, no pre-save backup, and no on-exit backup.

## 7.7 Write path

All 12 write sites converge on the single `SyncStamper` seam — the strongest invariant
in the system.

**Three fire-and-forget persistence writes remain**, all wrapped in `CrashLogger.Observe`
(so faults are logged) but none awaited:

- `QuanLyTaskViewModel.cs:219`
- `WeightOptimizerViewModel.cs:123`
- `App.xaml.cs:90-101`

## 7.8 Device identity

`DeviceId` = `"desktop-" + SHA256(MachineName)[..8]`. It is **recomputed on every call
and never persisted**, so it changes if the machine is renamed.

## 7.9 Configuration storage

**No `appsettings.json` exists.** Configuration lives in four hand-rolled stores plus one
environment variable:

| Store | Note |
|---|---|
| `weight_config.json` | `%LOCALAPPDATA%` |
| `capacity.txt` | Parsed **culture-sensitively**, without `InvariantCulture` |
| `streak_data.json` | — |
| `SmartStudyData.db` | SQLite |
| `DEV_RESET_DB` | Environment variable |

Theme selection is not persisted.

---

# 8. Technical Debt

Confirmed only — each item was observed in code at `d3deca7`. No speculation.

## 8.1 Architectural

| Item | Anchor |
|---|---|
| ViewModels bypass the service layer and call repositories directly | multiple VMs |
| Data-load + priority-scoring loop lives inside the window | `MainWindow.xaml.cs:98-117` |
| ViewModel constructs and `ShowDialog()`s a `Window` | `DashboardViewModel.cs:349-350` |
| Full pipeline executed synchronously in a VM constructor | `DashboardViewModel.cs:89` |
| Scheduling strategy seams exist but composition is closed | `SchedulingOrchestrator` ctor |
| `ISchedulingOrchestrator` declared, never used as a type | `Core/Scheduling/` |
| Lazy provider can build a second container | `ServiceLocator.cs:27` |
| Dead DI registration | `ServiceLocator.cs:40` |
| 27 residual `ServiceLocator` call sites | `App.xaml.cs`, 8 VMs, `MainWindow.xaml.cs` |
| No global soft-delete query filter; 2 proven leak sites | `SqliteStudyLogRepository.cs:45-48`, `SqliteUserStatsRepository.cs:25` |
| Two divergent copies of the raw-minutes formula | Scheduling |
| `MucDoCanhBao` derivation duplicated at 3 sites | Scheduling / VM |
| Chart rendering split across two technologies | Analytics (LiveCharts) vs Dashboard (`DonutChart`) |

## 8.2 Maintainability

| Item | Anchor |
|---|---|
| `MucDoCanhBao` NOT NULL but never set by the ctor — *already tracked, `system_roadmap.md:103-113`* | `Models/StudyTask.cs:27, 45-53` |
| `ParseSource.MlOverridden` declared, zero consumers | `Core/Parsing/Models/ParseResult.cs:31` |
| `ParseInputStage` dead in production | `Services/Pipeline/` |
| `EvaluateR2Async` has no production caller | `MLModelManager` |
| `RetrainAsync` has no UI entry point | `TextClassifierService` / manager |
| `IsReady = true` set unconditionally past a failed gate | `MLModelManager.cs:151` |
| `IMlConfidencePolicy` bypassed by the A1 path | `StudyTimePredictorService.cs:41-45` |
| `AutoApplyThreshold` dead on the parsing path | `IntentClassifierAdapter.cs:34-36` |
| `capacity.txt` parsed culture-sensitively | Config |
| `DeviceId` recomputed, never persisted | Sync |
| Notification timer is `async void`, no `try`/`catch`, no toast de-dup | `App.xaml.cs` |
| `Verify.CommunityToolkit.Mvvm` referenced by the production csproj, unused by both projects | csproj |
| 3 fire-and-forget persistence writes | `:219`, `:123`, `:90-101` |

## 8.3 Performance

No measured performance debt. No benchmarks exist, so no performance claim — positive or
negative — is currently evidence-backed.

## 8.4 Testing

| Item | Detail |
|---|---|
| **No CI** | `.github/workflows/` is empty and untracked |
| 11 of 346 cases are pure duplicates | three byte-identical file pairs |
| Zero-coverage directories | `Views/` (9), `Converters/` (11), `Controls/` (DonutChart), `Core/Parsing/Engines/` (2), `Core/Scheduling/Evaluators/` (PriorityEvaluator), `Core/Risk/Aggregators/` (RiskAggregator) |
| Additional untested types | `ProgressGapRiskEvaluator`, `WorkloadServiceImpl`, `SystemClock`, `DashboardViewModel`, `SetupViewModel`, `WorkloadBalancerViewModel`, `MLModelManager`, `SeedDataGenerator`, `TaskCascadeHelper`, `SyncStamper`, `ThemeManager` |
| **A false green exists** | see below |
| Tests touch the real user profile | `LocalModelStorageTests.cs:13-14` writes to real `%APPDATA%\SmartStudyPlanner\models`, no temp dir, no cleanup |
| A test builds the production composition root | `PipelineStageTests.cs:200` — real app DB, real streak JSON, real `%LOCALAPPDATA%` weight config, real model paths |
| No parallelism control | no `[Collection]`, no `xunit.runner.json`; all 57 classes run in parallel |
| `await Task.Delay(50)` used as a synchronisation primitive | 5 places |
| No assertion library, no mocking library | all doubles hand-written |
| coverlet present with no coverage gate | csproj |
| 184 wall-clock call sites across 27 files | repo-wide |

**The false green.** The DecisionEngine date-fragility is **partially** fixed. Priority
ordering tests now use `FixedNow` for both clock and deadlines
(`DecisionEngineTests.cs:22-24, 49-50, 64-65`), but six `DateTime.Now` deadline
constructions remain (`:37, 78, 92, 110, 121, 134, 149`). One has already drifted:

> `DecisionEngineTests.cs:87-96` — `CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0`
> builds `DateTime.Now.AddDays(45)` against a clock frozen at 2026-04-11. At today's
> wall clock the deadline is ≈152 days out, so `BeyondHorizonRule` fires
> (`HorizonDays = 60`) and returns `1.0`. `Assert.True(score > 0.0)` **passes for the
> wrong reason**, and the 31–60-day band is now unasserted anywhere in the suite.

## 8.5 Documentation

| Item | Detail |
|---|---|
| README test count stale | claims "337 tests"; actual is **346** |
| Roadmap §A.1 GitNexus stats stale | cites 3,333 symbols @ `5e54220`; `CLAUDE.md` says 4,001; both stale vs `d3deca7` |

## 8.6 Build hygiene

**192 distinct warning messages** at the working tree: CS8618 ×156, CS8625 ×10,
NU1903 ×10, CS8622 ×8, NU1904 ×4, CS8602 ×4. Zero errors. The nullable-reference warnings
(CS8618 dominating) indicate nullable annotations are enabled but not satisfied.

*Counting note:* the clean-clone build reported "96 warnings" — that is MSBuild's own
per-project tally, whereas 192 is a count of distinct warning messages across the
solution. Different methods, neither wrong; do not read the two numbers as a discrepancy.

---

# 9. Deferred Items

## 9.1 Intentional roadmap decisions

| Item | Status |
|---|---|
| Epic 2 — LAN sync | Not started. Epic 1 laid the data model for it |
| Epic 3 — SOE | Not started. Blocked by open gate **G2** (SOE pass semantics) |
| Epic 4 — ML maturation | Not started |
| Gate **G4** — tombstone retention policy | Open |
| No accuracy gate on the A2 classifier | Documented as intentional at `MLModelManager.cs:139-141` |
| Weight optimizer never auto-applies | Deliberate; user confirms every change |
| Analytics 2-section restructure | Design brief written (`docs/plans/2026-07-20-analytics-two-section-redesign.md`); **not coded** |
| **#46/#47 native-charts redesign — "integrate nothing"** | The most concrete intentional deferral in the repo. The redesign of Analytics / QuanLyTask / QuanLyMonHoc exists in history (`ced372b`, `4122a42`) and was deliberately **not** taken into the tree by the ui_rf-authoritative `d3deca7` sync, because it would overwrite manually-tested UI rather than add a missing feature. See §10.3 |

The roadmap's own deferred ledger (`system_roadmap.md` §A.4, `:95-114`) additionally
records as *deliberately* deferred: the pipeline rehome (`Services/Pipeline/*` →
`Application/UseCases/*`), `Core/Capacity` ("only when a real need surfaces"), cloud model
storage (opt-in via `IModelStorageProvider`), mobile/hybrid clients ("revisit after LAN
sync lands"), end-to-end async pipeline ("current sync MVP is acceptable"), the
`System.Drawing.Common` NU1904 advisory (~30 min, independent), the `SQLitePCLRaw` NU1903
advisory (carry-forward ledger #8), and the `MucDoCanhBao` constructor gap. **None of
these are new findings** — the build advisories and the constructor gap in §8 are restated
here so they are not double-counted as untracked debt.

## 9.2 Technical limitations accepted for now

| Item | Nature |
|---|---|
| No EF Migrations | `EnsureCreated()` + patch seams chosen instead |
| Telemetry tables carry no D-I block | Telemetry is excluded from the sync model |
| `DoKho` never model-predicted | Hard-pinned `null` at `TextClassifierService.cs:35` |
| No model artifacts committed | Both models are trained per-machine at runtime |
| ML prediction display-only | Allocator consumes the formula |
| Backup on schema upgrade only | No periodic/pre-save/on-exit backup |
| Theme not persisted | — |

## 9.3 Future extension points already in code

Listed in §2.4. The ones with no current second implementation but a working seam:
`IMlConfidencePolicy`, the five scheduling strategy interfaces, `ISchedulingOrchestrator`,
the pipeline stage abstraction, and `IClock`.

---

# 10. Master Plan Alignment

Compared against the Master Plan / `docs/specs/system_roadmap.md` (canonical, D-C.1).
Reported without judgement of whether any deviation is good or bad.

## 10.1 Already completed

- **Epic 1 — Sync-Ready Data Model.** Full D-I metadata block on all six non-telemetry
  entities; a single stamping seam (`SyncStamper`) that all 12 write sites pass through;
  `DeviceId` generation; tombstone columns present.
- **Frozen decisions D-A … D-J** are executed as recorded; the freeze itself is
  reflected in code and docs.
- **M7 (study-time model)** and **M8-A (text classifier)** are trained, persisted, and
  wired to consumers.
- **M8-B (weight optimizer)** is implemented and UI-reachable.
- **Dashboard native XAML charts** (`Controls/DonutChart.xaml{,.cs}`, added by `7225dba`)
  are on the trunk. This is *not* the #46/#47 redesign — see §10.3 for that.
- Cascade/reparent correctness (snapshot-before-`Remove` FK reassignment) is in place.
- `IStreakStore`/`IStreakManager` injectability landed (`27ffd3e`), removing the streak
  test file-contention flake.

## 10.2 Partially implemented

- **Soft delete.** Columns and stamping are complete; **query-side enforcement is not** —
  no global filter, and two proven leak sites.
- **ML confidence policy.** `IMlConfidencePolicy` exists and is honoured by the A2 path;
  the A1 path computes its own gate and never calls it.
- **Model readiness gating.** The R² gate is computed but `IsReady` is set
  unconditionally; the real guard is a sentinel return value.
- **Pipeline abstraction.** Stage infrastructure exists; the input stage is dead in
  production and the abstraction is not what Smart Add uses.
- **DI adoption.** Services are registered and resolved; ViewModels are not.
- **Test discipline / clock injection.** `IClock` + `FakeClock` exist and are used, but
  184 wall-clock call sites remain and one date-fragile test has drifted into a false
  green.
- **Analytics redesign.** Designed, not built.
- **M8-C** ("retrain the Study Time Predictor on real Focus-session telemetry — replace
  synthetic seed", `system_roadmap.md:90`). The *mechanism* exists and is UI-reachable:
  `AnalyticsViewModel.RetrainModel:250-268` prefers real telemetry when the row count
  clears `StudyTimeTrainingDataSource.MinRows` (50). The *replacement* has not happened —
  the synthetic seed remains the default and the shipped model is seed-trained.

## 10.3 Not started

- Epic 2 (LAN sync) — no networking, discovery, transport, conflict resolution, or peer
  code exists anywhere in the repository.
- Epic 3 (SOE) — blocked by open gate G2.
- Epic 4 (ML maturation).
- Tombstone retention (gate G4).
- **M9** — "natural-language deadline parsing (Part B §9.1) and cross-semester analytics"
  (`system_roadmap.md:91`). This is why deadline extraction is still keyword/substring
  based and why analytics is scoped to a single `HocKy`.
- `IScheduleOptimizer` — the roadmap's named seam for the SOE (`:376-377`) does not exist.
- Any CI pipeline.
- **The roadmap's Part B ML Confidence & Fallback Policy** (§10, `:543-586`). Three of its
  requirements have no implementation: uncertainty estimation on ML outputs (`:549-552`);
  a three-tier High/Medium/Low gate (`:556-575`) — the parser collapses High and Medium
  (`IntentClassifierAdapter.cs:34-36`); and a single "Confidence Validation" node
  (`:577-586`) — A1 bypasses `IMlConfidencePolicy` with its own hard-coded 0.6. Part B is
  labelled aspirational, so this is unscheduled design rather than drift.
- **Part B §9.2 Performance Predictor** (`:524`) — optional by its own heading; no code.
- **The #46/#47 native-charts "mission-control" redesign of Analytics / QuanLyTask /
  QuanLyMonHoc.** Its commits (`ced372b`, `4122a42`) are reachable from HEAD, but the
  `d3deca7` sync was ui_rf-authoritative and did not take their tree; HEAD differs from
  `ced372b` by 141 files. The redesign is parked in history, unintegrated.

## 10.4 Implemented differently than originally planned

Every candidate deviation was checked against `docs/specs/system_roadmap.md` before being
listed here. **Most did not survive that check** — the roadmap turned out to have planned
what was built. Only deviations whose plan side can be quoted are listed.

**Which roadmap text counts as "the plan".** `system_roadmap.md` splits itself at `:6-7`:
"**Part A — Delivery Status** is **factual**: it reflects shipped state" (`:11-126`);
"**Part B — Architecture Direction** is **aspirational**: target direction that may run
ahead of code" (`:127`–end). Only Part A statements — plus explicit "Shipped today"
annotations inside Part B — can support a deviation claim. Part B text that code does not
implement is **not-yet-built design**, and belongs in §10.3 / §9, not here.

Applying that rule leaves **one** sourced deviation:

| Plan text (sourced, Part A) | As built |
|---|---|
| `:18` — "exact count lives in the README / **CI** (`dotnet test --no-build`)" | **No CI exists** (`git ls-files .github` → empty). The count lives only in the README, and it is stale (337 vs. 346). Part A invariant `:120` also requires build + test to "stay green" with no mechanism enforcing it |

### Checked and found *not* to be deviations

Recording these explicitly, because each is a plausible-sounding deviation that the
roadmap in fact specifies:

| Apparent deviation | Roadmap says |
|---|---|
| No EF Migrations; `EnsureCreated()` + patch seams | M1.2 planned exactly this — "`SyncSchema.EnsureColumns` versioned upgrade + backup + migration report" (`:40`) |
| Cascade config inert; tombstones instead | M1.2 — "soft-delete tombstones **replace hard cascades**"; cascade policy "decided + implemented (G1)" (`:40, :88-89`) |
| Smart Add is unstaged while a 5-stage pipeline exists elsewhere | M5 delivered "Pipeline Orchestrator (5 stages)" (`:30`) as its own component. The roadmap never states that Smart Add consumes it |
| ML is display-only, not driving scheduling | Hard invariant: "Never let ML availability gate the app — formula fallback must remain" (`:122`) |
| ML overrides task type only | "Shipped today: ML overrides **task type** only; difficulty and deadline are rule-based" (`:459` — a factual "shipped today" annotation embedded in Part B) |
| ML confidence handling doesn't match §10's policy | §10 (`:543-586`) is **Part B / aspirational**. It is unbuilt design, not a broken contract — recorded in §10.3 |
| `DecisionEngineService` is a thin facade | M8 (arch) shipped "`DecisionEngineService`→42-line facade" (`:36`) |
| No optimizer / constraint validator | Both belong to Epic 3's SOE and are blocked on gate G2 (`:83-85`) |

### Not sourced either way

Two observations have no roadmap counterpart and are therefore reported as plain current
behaviour rather than as deviations: the split charting stack (LiveChartsCore on
Analytics vs. native `DonutChart` on Dashboard), and the absence of `appsettings.json` in
favour of four hand-rolled stores. `MucDoCanhBao` being a separate channel from
`RiskLevel` is likewise unsourced as a *plan* claim, though its constructor gap **is**
tracked at `:103-113`.

## 10.5 Master Plan assumptions no longer true

1. **"There are two competing lines to reconcile."** No longer true at `d3deca7` —
   `dev == origin/dev == origin/main`, zero commits of divergence either way. What
   remains un-integrated is not a *branch* but a *parked tree*: the #46/#47 redesign,
   reachable from HEAD yet not present in it.
2. **"The DecisionEngine date-fragility is fixed."** Partially. Six `DateTime.Now`
   deadlines remain and one test now passes for the wrong reason.
3. **"337 tests."** 346.
4. **"GitNexus index at 3,333 / 4,001 symbols."** Both figures predate `d3deca7`.
5. **"Green builds are verified."** They are verified *locally and manually*. No CI has
   ever run in this repository. The roadmap itself assumes otherwise: `:18` defers the
   test count to "the README / **CI**", and invariant `:120` requires build + test to
   "stay green" with no mechanism that enforces it.

---

# 11. Epic 2 Readiness

Assessed on the six axes named in the brief. Only genuine blockers are called blockers.

| Axis | State |
|---|---|
| **Architecture stability** | Stable. The sync data model — the layer Epic 2 builds on — is the most mature part of the system: one write seam, one repository stack, full D-I coverage on the entities that matter |
| **Code quality** | Adequate. Zero errors; **zero TODO/HACK/FIXME comments anywhere in production code** (verified repo-wide); consistent Vietnamese domain naming. 192 build warnings, dominated by unsatisfied nullable annotations |
| **Extensibility** | Mixed. Persistence and ML have real, substitutable seams. Scheduling's seams are declared but closed by constructor-`new`. ViewModels are outside DI |
| **Testing** | 346/346 green in ~3s, real SQLite, no mocking framework. But: no CI, one false green, zero presentation-layer coverage, `WorkloadServiceImpl` untested, and tests that touch the real user profile and build the production composition root |
| **Documentation** | Good and recently curated; two numeric claims stale |
| **Deployment readiness** | Builds clean from a **fresh clone** (verified: 4 projects, 0 errors) despite `Assets/` being untracked. No installer, no packaging, no release pipeline exists |

## Genuine blockers to Epic 2 *planning*

**None.** Planning can proceed on the current baseline.

## Three items that change what a plan may safely assume

These are stated as facts, not as tasks:

1. **No CI exists.** Any planning assumption of the form "the suite protects us from
   regression on merge" is currently unfounded — the suite protects only whoever runs it
   locally.
2. **The test suite contains a demonstrated false green** and has no clock discipline
   over 184 wall-clock call sites. Green is a weaker signal than the count suggests.
3. **Soft delete is not enforced at the query layer.** Epic 2 is a sync epic; tombstone
   semantics are its raw material, and two read paths currently return or aggregate
   tombstoned rows (`SqliteStudyLogRepository.cs:45-48`,
   `SqliteUserStatsRepository.cs:25`). Gate **G4** (tombstone retention) is also still
   open.

Additionally, **gate G2 (SOE pass semantics) remains open**, which per the frozen
decisions blocks Epic 3, not Epic 2 — but the Master Plan's execution order is
E1 → E3 → E2, so the ordering assumption and the gate state are worth surfacing to
whoever sequences the work.

---

# 12. Key Findings

1. **`dev`, `origin/dev`, and `origin/main` are identical at `d3deca7`** — one trunk, no
   divergence. But the #46/#47 redesign is **reachable without being adopted**: its
   commits are ancestors of HEAD while HEAD's tree differs from `ced372b` by 141 files.
   Ancestry is a misleading signal on this repo's history; content diffs are the reliable
   test.
2. **There is no CI.** `.github/workflows/` is empty and untracked. All verification to
   date has been local and manual.
3. **The scheduler cannot see deadlines.** `WorkloadServiceImpl.cs:77-91` allocates by
   least-loaded-day-under-capacity; `HanChot` appears nowhere in it, and `ScheduledTask`
   has no deadline field. No optimizer, constraint validator, or objective evaluator
   exists in the codebase.
4. **`WorkloadServiceImpl.GenerateSchedule` has zero test coverage.** The most
   behaviour-defining method in scheduling is exercised by nothing.
5. **Smart Add and `Services/Pipeline/**` are disjoint systems** sharing zero code.
   `ParsingOrchestrator.Parse` is one straight-line method with no stages;
   `ParseInputStage` is dead in production.
6. **ML output is display-only.** Neither model influences scheduling; the allocator uses
   the heuristic formula.
7. **The two models gate confidence differently, and one bypasses the policy contract
   written for it.** A1 uses heuristic-agreement with a hard-coded 0.6 and never calls
   `IMlConfidencePolicy`; A2 uses a raw uncalibrated score through the policy. A2 has no
   accuracy gate and no train/test split (documented as intentional).
8. **No model artifacts are committed** — both are trained per-machine at runtime.
9. **Epic 1 is genuinely shipped.** Full D-I block on all six non-telemetry entities,
   all 12 write sites through one `SyncStamper` seam.
10. **Soft delete is stamped but not enforced on read.** No global query filter; two
    proven leak sites.
11. **No EF Migrations.** `EnsureCreated()` plus three sequenced patch seams; backup runs
    exactly once, only on schema upgrade.
12. **A false green exists in the test suite** —
    `DecisionEngineTests.cs:87-96` passes because `BeyondHorizonRule` fires, not because
    the 31–60-day band works. That band is now unasserted anywhere.
13. **Tests reach into the real user profile and build the production composition root**
    (`LocalModelStorageTests.cs:13-14`, `PipelineStageTests.cs:200`) with no parallelism
    control across 57 classes.
14. **Everything is a singleton.** Zero transient, zero scoped registrations; ViewModels
    are never resolved from DI; `ServiceLocator.cs:27` can build a second provider.
15. **A fresh clone builds and links without `Assets/`.** The directory is untracked and
    not gitignored, and `SmartStudyPlanner.csproj` references it twice
    (`<ApplicationIcon>`, `<Resource Include>`) — yet a clean clone at `d3deca7` compiled
    with 0 errors **and produced `SmartStudyPlanner.exe`** under
    `bin/Debug/net10.0-windows10.0.19041.0/`. The missing icon does not break the build;
    it simply is not in the repository.
16. **Two doc claims are stale:** README says 337 tests (actual 346); roadmap §A.1 and
    `CLAUDE.md` disagree on the GitNexus symbol count and both predate `d3deca7`.
17. **Analytics computation is split between the service and the ViewModel.**
    `IStudyAnalytics` holds three pure functions; the heatmap (`AnalyticsViewModel.cs:219-248`),
    the narrative (`:201-217`), and the productivity-score *inputs* (`:165-171`) are
    computed in the ViewModel. `ComputeWeeklyMinutes` always emits exactly 7 buckets
    (`StudyAnalyticsService.cs:11-25`) regardless of the 7/30/90-day range selection.
18. **The tombstone leak surfaces in Analytics.** `GetForHocKyAsync:45-48` is Analytics'
    only data source (`AnalyticsViewModel.cs:87`), so deleted study logs currently feed
    every analytics output on the page.
19. **Apparent "plan deviations" almost all dissolve on inspection.** The roadmap
    explicitly specifies the `EnsureColumns` upgrade path, tombstones replacing hard
    cascades, the thin `DecisionEngineService` facade, the ML-overrides-task-type-only
    scope, and the formula-fallback invariant. The ML-confidence gaps sit in **Part B**,
    which the document labels aspirational (`:7`) — unbuilt design, not drift.
    **One sourced deviation remains:** the roadmap assumes CI exists (`:18`); it does not.
    Part A vs. Part B is the distinction that makes this section meaningful, and any
    future comparison against this roadmap should apply it.

---

# Verification

All results below were obtained at `d3deca7` during this assessment.

| Check | Command / method | Result |
|---|---|---|
| Baseline SHA | `git rev-parse HEAD` | `d3deca7b5401aade47972ab9b5929c2b85352707` |
| Merge shape | `git show --stat d3deca7` | merge of `9111d86` + `0bdc5b5`; diff vs first parent = 1 doc file, 7 insertions |
| Branch divergence | `git log origin/main..HEAD`, `git log HEAD..origin/dev` | **zero commits both directions**; local `main` does not exist |
| #46/#47 adoption (content, not ancestry) | `git merge-base --is-ancestor ced372b HEAD`; `git diff --stat ced372b HEAD` | ancestor: **YES**, but tree differs by **141 files, +15260/−1588** → reachable, **not adopted** |
| `DonutChart` provenance | `git log --diff-filter=A -- Controls/DonutChart.xaml` | added by `7225dba` (pre-split merge-base), **not** by #46/#47 |
| Build (working tree) | `dotnet build` | 4 projects, **0 errors**, 192 distinct warnings |
| Warning taxonomy | `Select-String -AllMatches` + `Group-Object` | CS8618 ×156, CS8625 ×10, NU1903 ×10, CS8622 ×8, NU1904 ×4, CS8602 ×4 |
| Tests | `dotnet test` | **346 passed / 0 failed / 0 skipped**, ~3 s |
| Test-count reconciliation | 51 files / 57 classes / 242 Facts + 21 Theories (104 InlineData) = 263 declarations | reconciles to 346 exactly |
| CI presence | `git ls-files .github` | **empty** — `.github/workflows/` on disk is untracked |
| Clean-clone build | fresh `git clone` at `d3deca7`, `ls Assets` + `dotnet build` | `Assets`: **No such file or directory**; build: **4 projects, 0 errors** |
| Clean-clone output artifact | `find` for `SmartStudyPlanner.exe` in the clone | **produced** at `SmartStudyPlanner/bin/Debug/net10.0-windows10.0.19041.0/SmartStudyPlanner.exe` |
| Roadmap Part A / Part B boundary | `grep -n "^# " docs/specs/system_roadmap.md` | Part A `:11-126` (*factual*); Part B `:127`–end (*aspirational*, per `:6-7`). §10 ML policy is at `:543` → **Part B** |
| §10.4 plan-side sourcing | grep each claimed plan assumption against `system_roadmap.md`, then filter to Part A | 8 candidates → 4 unsourced (removed), 3 Part B (→ §10.3), **1 sourced deviation**; 7 apparent deviations reclassified as on-plan |
| Analytics assessment | direct read of `StudyAnalyticsService.cs`, `IStudyAnalytics.cs`, `Models/*.cs`, `AnalyticsViewModel.cs` | 3 pure service functions + 3 VM-side computations; 3 test files / 327 lines |
| Commits since Master Plan (2026-07-03) | `git log --since` | 50 total — 34 docs, 8 fix, 6 feat, 1 test, 1 merge |
| Deadline-blindness claim | direct read of `WorkloadServiceImpl.cs:77-91` | **still true**, exact prior line anchors hold |
| Optimizer absence | repo-wide grep for optimizer/constraint/objective/solver terms | **zero matches** |
| `MlOverridden` consumers | repo-wide grep | declared at `ParseResult.cs:31`, **zero consumers** |
| `ServiceLocator` residual usage | repo-wide grep | **27 call sites** |
| TODO/HACK/FIXME | repo-wide grep, production code | **zero** |
| Date-fragility status | direct read of `DecisionEngineTests.cs` | partially fixed; 6 `DateTime.Now` deadlines remain; `:87-96` drifted |

**Working tree at assessment time** (pre-existing, untouched by this assessment):
modified `.claude/settings.json`, `.claude/settings.local.json`,
`.claude/skills/gitnexus/gitnexus-cli/SKILL.md`, `AGENTS.md`, `CLAUDE.md`; untracked
`Assets/`, `Prompt/`, `tools/epic1_b2_verify.py`.

**No file in the repository was modified by this assessment** other than this report.
All six dispatched agents were read-only and each confirmed compliance.

---

# Decisions made

**Why this assessment ran at all.** Epic 1 shipped and docs were curated, but the last
comprehensive picture of the system predates 50 commits of work. Epic 2 planning needs a
baseline that is *measured*, not remembered — and two of the assumptions carried in
project memory turned out to be wrong (§10.5). Establishing that before planning is
cheaper than discovering it mid-epic.

**What it is for.** This is the primary input to Epic 2 planning and nothing else. It
deliberately contains no tasks, no estimates, no refactor proposals, and no roadmap
edits, per the constraints in `Prompt/epic_2_prep.md`. Anyone planning Epic 2 should be
able to answer the six success-criteria questions from this document without opening the
codebase.

**Decision: source code over documentation, without exception.** Every inherited claim
that mattered was re-verified against code at `d3deca7`. This caught the stale test
count, the stale GitNexus figures, the partially-fixed date fragility, and the
already-drifted false green — none of which would have surfaced from reading docs.

**Decision: pin the baseline before assessing.** SHA, build, warning taxonomy, test
count, and branch divergence were captured first so that every later finding is anchored
to a single reproducible state. This is what made the `dev`/`origin/main` identity a fact
rather than an inference.

**Decision: judge branch state by content, never by ancestry.** A first pass concluded
the #46/#47 redesign was "on the trunk" because its commits are ancestors of HEAD and
`Controls/DonutChart.xaml` exists. Both signals are traps here. The `d3deca7` sync was
ui_rf-authoritative, so reachability says nothing about adoption — HEAD's tree differs
from `ced372b` by 141 files — and `DonutChart` was added by `7225dba`, the pre-split
merge-base common to both lines. The conclusion was reversed on content evidence. Prior
project notes warned that earlier `-X ours` syncs poisoned ancestry checks on this repo;
that warning proved correct and is worth honouring in any future branch reasoning here.

**Decision: parallel read-only agents over sequential reading.** Six independent domain
assessments (ML, Smart Add, Scheduling, Persistence, App Wiring, Tests) ran concurrently,
each required to return `file:line` anchors and forbidden to write. This kept raw file
bytes out of the synthesising context while preserving traceability. The cost is that
findings arrive as claims, which is why every load-bearing one was independently
re-checked here.

**Decision: report the clean-clone/`Assets/` result as measured, in both directions.**
The obvious inference — a csproj referencing an untracked directory must break a fresh
clone — is wrong. The clone lacks `Assets/` *and* builds with 0 errors. Both halves are
stated rather than the inference, because a plan built on "fresh clones are broken"
would waste effort on a non-problem.

**Decision: every "implemented differently than planned" row must quote the plan — and
the quote must come from Part A.** The first draft of §10.4 carried eight rows. Grepping
each claimed assumption against `docs/specs/system_roadmap.md` showed that **four had no
plan side at all** — the roadmap is silent on charting unification and on
`appsettings.json` — and that several others were backwards: the roadmap *specifies* the
`EnsureColumns` upgrade path (`:40`), *specifies* tombstones replacing hard cascades
(`:40, :88-89`), *specifies* the thin `DecisionEngineService` facade (`:36`), and
*mandates* the formula fallback that makes ML display-only (`:122`).

A second pass caught a subtler version of the same error. Three surviving rows cited
roadmap §10 (ML Confidence & Fallback Policy) as the plan side — but §10 begins at `:543`,
inside `PART B — Architecture Direction`, which the document's own preamble labels
"**aspirational**: target direction that may run ahead of code" (`:7`). Comparing code
against Part B and calling the difference a *deviation* is the same failure as inventing
the plan side, just better disguised: it converts unbuilt design into apparent drift.
Those rows moved to §10.3 Not started. **The Part A / Part B split is the load-bearing
distinction in this comparison** — Part A is the milestone table (`:18-41`), the frozen
decisions (`:79-91`), the invariants (`:120-122`), and §A.4's deferred ledger
(`:95-114`); everything from `:127` on is direction, not contract. Exactly one deviation
survives both filters, and it is quoted rather than paraphrased.

**Decision: assess Analytics as a computation layer, not as charts.** The brief names
Analytics in Scope and in the Capability Inventory examples. The initial six-agent fan-out
covered chart *rendering* under App Wiring but never touched `Services/Analytics/**`, which
left the capability as a single unanchored inventory row. A targeted pass closed it and
surfaced two things nothing else would have: the computation is split between the service
and the ViewModel, and the soft-delete leak at `SqliteStudyLogRepository.cs:45-48` has its
user-visible consequence here — that repository method is Analytics' only data source.

**Experience worth carrying forward.** Two things repeated. First, a passing test suite
is a weaker signal than its count implies when the suite reads wall-clock time in 184
places — the false green at `DecisionEngineTests.cs:87-96` was not visible from the green
bar. Second, several "seams" in this codebase are declarative rather than functional:
interfaces exist, but composition is closed (`SchedulingOrchestrator`), the type is never
used (`ISchedulingOrchestrator`), the contract is bypassed (`IMlConfidencePolicy` on the
A1 path), or the value is never emitted (`ParseSource.MlOverridden`). Counting interfaces
overstates extensibility here; only checking consumers gives the true number.

---

# Follow-ups

Listed as observations for the owner to triage. **None are proposed work** — this report
creates no tasks and sequences nothing.

- The false green at `DecisionEngineTests.cs:87-96` leaves the 31–60-day priority band
  unasserted; six `DateTime.Now` deadlines remain in that file.
- Two soft-delete read paths return or aggregate tombstoned rows
  (`SqliteStudyLogRepository.cs:45-48`, `SqliteUserStatsRepository.cs:25`).
- README's "337 tests" and the roadmap/`CLAUDE.md` GitNexus symbol counts are stale.
- `.github/workflows/` is untracked and empty.
- `Assets/` is untracked and not gitignored while being referenced twice by the
  production csproj.
- Gates **G2** (SOE pass semantics) and **G4** (tombstone retention) remain open.
- `docs/plans/2026-07-20-analytics-two-section-redesign.md` is designed and uncoded.
- Two dependency advisories (NU1903 high, NU1904 critical) surface on every build — both
  already on the roadmap's deferred ledger (`system_roadmap.md:100-102`).
- The #46/#47 redesign remains parked in history and un-integrated; the owner has a
  standing ask to be reminded it exists.
