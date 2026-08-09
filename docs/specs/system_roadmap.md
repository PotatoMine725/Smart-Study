# Smart Study Planner — System Roadmap & Architecture Direction

> **Canonical roadmap** (decision **D-C.1** — supersedes the retired `docs/ROADMAP.md` and the
> README "What's coming next"). Decisions: [`../plans/2026-07-01-architecture-direction-decisions.md`](../plans/2026-07-01-architecture-direction-decisions.md).
>
> - **Part A — Delivery Status** is **factual**: it reflects shipped state.
> - **Part B — Architecture Direction** is **aspirational**: target direction that may run ahead of code.

---

# PART A — Delivery Status *(factual)*

## A.1 Snapshot

| Layer | State |
|---|---|
| Build | green (`dotnet build SmartStudyPlanner.slnx`) |
| Tests | green — exact count lives in the README / CI (`dotnet test --no-build`); not hard-coded here |
| Version | `1.5.0` |
| GitNexus index | 4,254 symbols / 9,791 relationships / 120 execution flows (re-indexed at commit `5bf7342`, 2026-08-04) |

## A.2 Completed milestones

| ID | Name | Notes |
|---|---|---|
| M1 | DI Container (`Microsoft.Extensions.DependencyInjection` + `ServiceLocator`) | merged |
| M2 | DecisionEngine → instance + `IDecisionEngine` | merged |
| M3 | WorkloadService → instance + `IWorkloadService` | merged |
| M4 / M4.5 / M4.6 | Risk Analyzer engine + Dashboard risk UI + drop static facades | merged (`af673d2`) |
| M5 | Pipeline Orchestrator (5 stages) | merged (`865ca47`, PR #35) |
| M6 | Study Analytics & Insights (StudyLog, 3 charts) | merged (PR #37) |
| M6.1 | Task Notes & Study Links (`TaskNote`, `TaskReferenceLink`) | merged |
| M7 | ML Engine — Study Time Predictor (FastTree, offline-first) | merged |
| M8-A | TextClassifier wired into parser (seed v3, 5-class, 96.2% held-out); `IntentClassifierAdapter` | shipped 2026-06-05 |
| M8-B | WeightOptimizer (rule-based) + review/apply UI + JSON persistence | shipped 2026-06-06 |
| M8 (arch) | God-object refactor Slices 1–8: Core contracts, `DecisionEngineService`→42-line facade, `ParsingOrchestrator`, repo abstractions, `RiskOrchestrator` implements `IRiskAnalyzer`, injectable `StreakManager` | shipped 2026-06-11 |
| M8 Telemetry | `DifficultyLabelLog` + `WeightChangeLog` capture; `OutcomeMaturationService` (14-day cohort fill) | shipped 2026-06-11 |
| UI/UX | Design system, sidebar, dashboard, analytics heatmap, WorkloadBalancer page | shipped |
| M1.1 | Epic 1 — single stamping seam + A6 (`SyncStamper` in `AppDbContext.SaveChanges*` stamps `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId`; `StudyLog` write awaited, `DeviceId` populated) | merged `3193adf` (2026-07-05) |
| M1.2 | Epic 1 — schema upgrade + tombstones, gate G1 (`SyncSchema.EnsureColumns` versioned upgrade + backup + migration report; soft-delete tombstones replace hard cascades; `TaskCascadeHelper` cascade-tombstones FK-only children, M1.2-R1 remediation) | merged `e2f8268` (2026-07-10) |
| M1.3 | Epic 1 — MonHoc identity & dedup (`MonHocIdentity.Normalize` single dedup definition, 4 read-side sites + `ThemMon` prevent-at-source; folded fix for a pre-existing `LuuHocKyAsync` task-reconcile gap surfaced by the widened dedup key, Option A) | merged `a3a0a3d` (2026-07-11) — **Epic 1 code complete** |
| Epic 1 closeout | Post-close fix (`101aaa3` — duplicate-subject warning routed through `OnThongBao` seam) + A1 release-gate hardening (`DbBackup` WAL-checkpoint-before-copy, closes verdict finding F5) | fix `2d04be5`, merged `8740350` (2026-07-12/13) |
| Epic 1 released | B4 reopen fix — R1 (`QuanLyTaskViewModel.ThemTask` stamps `MaMonHoc`; reconcile heals an empty FK from navigation position and fails loud on an unknown FK, `3bb56c6`/`63b9611`) + R2 (`CrashLogger` last-resort sink + `Dispatcher`/`AppDomain`/`TaskScheduler` global handlers, `b0061e7`/`c18e1e7`); plus a separate pre-existing Analytics stale-render fix (`c4291c7`). **Epic 1 Released 2026-07-20** — owner sign-off, closure-gate release decision record | merged `37f9678`, 337 pass |

> Granular refactor-slice history: `refactor-god-object.md` (archived 2026-07-07 → `legacy/Archived plans/`, local-only) + git log. Epic 1 release gate (conditions C1–C3) tracked in [`../plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md); execution in `2026-07-12-epic1-closure-phase1-execution.md` (archived 2026-07-26 → `legacy/Archived plans/`, local-only).

## A.3 Next up

Ordered per decisions **D-B** (sync-ready data model first) and **D-A** (LAN sync target);
execution decomposition + order per the [2026-07-03 master plan](../plans/2026-07-03-master-plan.md)
(SOE precedes LAN transport for the desktop-first closed alpha — D-B's build queue is
*"data model → debt B3/A6 → SOE"*, and its consequences explicitly do not commit to LAN sync next):

1. **Sync-ready data model** *(foundation — shipped)* — **Epic 1 shipped in full and Released
   (2026-07-20).** The D-I metadata block (`Rev` + `ModifiedAtUtc` + `ModifiedByDeviceId`)
   + tombstones on every synced entity (M1.2, gate G1 closed) **and** identity semantics beyond
   Guid PKs (the dedup-cloned-`MonHoc` issue, M1.3) have both shipped — merge `a3a0a3d`,
   post-close fix `101aaa3`. **Release-gate history (preserved):** Phase 2's first supervised
   launch (2026-07-15) returned **B4 = Reopen** on one latent M1.2 regression (task creation
   never stamped `MaMonHoc`; the FK-based reconcile then crashed the app, with no global handler
   to contain it). Diagnosis accepted by the owner 2026-07-19
   ([QA investigation](../reports/2026-07-19-epic1-phase2-qa-investigation.md) ·
   [owner decisions](../specs/2026-07-19-owner-epic-1-decisions.md)); the
   the reopen fix plan (`2026-07-19-epic1-reopen-fix-plan.md`, archived 2026-07-26 →
   `legacy/Archived plans/`, local-only) was approved and shipped —
   **R1** (`ThemTask` stamps `MaMonHoc`; reconcile heals an empty FK from navigation position and
   fails loud on an unknown FK, `3bb56c6`/`63b9611`) + **R2** (`CrashLogger` + global exception
   handlers, `b0061e7`/`c18e1e7`), merged `37f9678`. A **separate, pre-existing** Analytics
   stale-render bug (surfaced during the B4 Step-2 re-run — *not* an Epic 1 regression) was fixed
   in `c4291c7` (337 pass; Part 2 XAML visibility shipped, visual toggle pending owner re-run).
   The owner re-ran the supervised launch and signed off **Epic 1 = Released (2026-07-20)** —
   release decision record in
   [`../plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md)
   (conditions C1–C3; Phase 1 execution plan `2026-07-12-epic1-closure-phase1-execution.md`,
   archived 2026-07-26 → `legacy/Archived plans/`, local-only),
   superseding the earlier "do not release yet" hold. **Post-release backlog:** the Analytics
   **two-section redesign** + subject-filter / range-vs-trend semantics
   ([design brief](../plans/2026-07-20-analytics-two-section-redesign.md), design-only). *(The
   latent `MucDoCanhBao` ctor gap listed here has since been closed — §A.4.)* The last-synced
   base-snapshot store for 3-way merge
   lands with the LAN-sync epic, co-designed with its consumer (master plan M2.1). See
   [`../architecture/data-model.md`](../architecture/data-model.md) §8.
2. **Study Optimization Engine** *(on top of the sync-ready data model — D-B)* — evolves the Balancer (Part B §7.3).
   **Guardrails frozen 2026-07-02 ([D-G/D-H/D-J](../plans/2026-07-02-architecture-freeze-decisions.md)):** deadline feasibility, capacity and calendar limits are **hard constraints** (Constraint Validator);
   objective = quality only (`w1…w5`); feasibility never worsens (`violations(out) ≤ violations(in)`).
   **Gates closed:** G2 (pass accept/commit semantics + non-worsening threshold) ratified 2026-08-05
   ([note](../plans/2026-08-04-g2-optimization-pass-semantics.md)); G3 (`w1…w5` weight-vector
   governance) ratified 2026-08-07 ([note](../plans/2026-08-07-g3-weight-vector-governance.md)).
   **Implementation shipped** (execution plan `2026-08-04-epic-3-execution-plan.md`, Cards A–H):
   corpus + baseline (T3.6), `IConstraintValidator` (T3.1), `IObjectiveEvaluator` (T3.2), schedule
   identity seam (T3.8), allocator **placement** rework (T3.3: least-loaded → earliest-feasible; the
   deadline clause is present but **provably output-inert** today — it cannot change any placement
   the chronological tier would not already have chosen, see the closing note), the
   `Optimize(schedule) → (schedule, report)` seam (T3.9), the D-H/inversion property suite +
   `OptimizerRunLog` telemetry (T3.4/T3.7).
   **`ScheduleOptimizer`/`SoeWeights` have zero production call sites as of this HEAD** —
   `BalanceWorkloadStage.cs` still calls the pre-Epic-3 `IWorkloadService.GenerateSchedule`
   path directly; wiring the seam into production is separate, unscheduled integration work, not
   part of any Epic 3 task card. Success metrics measured and reported in the
   [epic closing note](../reports/2026-08-07-epic3-closing-note.md) (DoD-7): D-H holds (0 breaches/230
   items); deadline inversions reduced from baseline (self-miss class eliminated by construction;
   residual pairwise inversions and 4 corpus items with a feasibility regression vs. baseline are
   disclosed, owner-accepted allocator limitations traced to a single root cause — priority-only
   task ordering — not implementation defects, see the G2 note's D7). See the SOE proposal
   `2026-06-30-workload-optimizer-proposal.md` (archived 2026-07-07 → `legacy/Archived plans/`,
   local-only; recoverable from git history — note it carries a supersession banner, the frozen
   contract is D-G/D-H/D-J).
3. **LAN sync epic** *(D-A)* — multi-device, two-way merge over LAN (not cloud). Merge policy **decided (D-F):** field-level merge, LWW only on concurrent same-field edits.
   **Mechanics frozen 2026-07-02 ([D-I](../plans/2026-07-02-architecture-freeze-decisions.md)):** 3-way merge vs. last-synced base; tie-break `ModifiedAtUtc` → `DeviceId`; delete-vs-edit → tombstone wins,
   losing side kept in a conflict record; no HLC. *Cascade policy decided + implemented (G1, cascade-tombstone —
   Epic 1 / M1.2). Still open: tombstone retention/purge authority (master plan gate G4).*

   > **Gate G4 is a planning-agenda item for this epic — it must be decided before implementation, not during.**
   > Added 2026-08-02 by WP-6 of the post-Epic-1 stabilization plan, which satisfies its Epic 2
   > entry criterion #10 (*"G4 is explicitly on the Epic 2 planning agenda … must not be silently
   > inherited"*). Stabilization deliberately did **not** decide it: WP-3.1 made tombstones invisible
   > to every read path, but **how long they live and who may purge them is a policy question Epic 2
   > owns**, and settling it from the stabilization side would have been guessing.
   > Three things make it load-bearing rather than housekeeping: a tombstone purged on one peer but
   > not another resurrects the row on the next merge; retention interacts with D-I's 3-way merge,
   > which needs the last-synced base to still exist; and unbounded retention means the tombstone
   > table grows without limit on a device that never syncs.
   > **Recording it here makes the criterion true; it does not make the decision.** The agenda item
   > is open until Epic 2 planning resolves it.
4. **M8-C** — retrain the Study Time Predictor on real Focus-session telemetry (replace synthetic seed).
5. **M9** — natural-language deadline parsing (Part B §9.1) and cross-semester analytics.

## A.4 Deferred / out of scope

- **Pipeline rehome** (`Services/Pipeline/*` → `Application/UseCases/*`) — independent plan.
- **Core/Capacity** — only when a real need surfaces.
- **Cloud model storage** — opt-in via `IModelStorageProvider`; no work until users ask.
- **Mobile / hybrid clients** — preserves offline-first; revisit after LAN sync lands.
- **Async pipeline end-to-end** — current sync MVP is acceptable.
- **`System.Drawing.Common` NU1904** vulnerability — ~30 min, independent.
- **`SQLitePCLRaw` NU1903** high-severity advisory — visible in every build; not Epic-1-caused,
  tracked but not yet scheduled (closure-verdict carry-forward ledger #8).
- *(Closed 2026-07-31, no longer deferred: the **`StudyTask.MucDoCanhBao` unstamped-by-constructor
  gap** — latent since 2026-07-19, the same shape as the `MaMonHoc` bug that caused B4 — was fixed by
  WP-3.3 of the post-Epic-1 stabilization (`78f16bb`). `StudyTask.MucDoCanhBao` now defaults to
  `"An toàn"` at declaration (`Models/StudyTask.cs:30`), so a task built from its constructor saves
  without `SQLite Error 19` and no longer depends on `QuanLyTaskViewModel.TinhDiemVaSapXep()` stamping
  it first. Pinned by `TaskDungTuCtor_LuuDuocMaKhongCanUIStampMucDoCanhBao`, which failed with exactly
  that error beforehand.)*
- *(Promoted out of "deferred": the old "Core/Sync + PostgreSQL — far-future Phase 4" item is now the
  planned **LAN-sync epic** in A.3, targeting LAN two-way merge rather than PostgreSQL/cloud.)*

## A.5 Guardrails for every change

1. `gitnexus_impact` before editing any symbol; report HIGH/CRITICAL to user.
2. `gitnexus_detect_changes` before commit.
3. `dotnet build SmartStudyPlanner.slnx` + `dotnet test --no-build` must stay green.
4. Never silently mutate `WeightConfig` on low ML confidence.
5. Never let ML availability gate the app — formula fallback must remain.
6. Offline-first stays the default; **LAN sync is a planned opt-in direction (D-A); cloud remains opt-in only**.

---

# PART B — Architecture Direction *(aspirational)*

---

# 1. Current Project State

The project has successfully transitioned away from a monolithic “god object” architecture into a more modular system.

Current strengths:

* pipeline-oriented structure
* strategy-based logic separation
* parser orchestration
* telemetry awareness
* testing structure
* adaptive planning direction

The project is no longer a CRUD-style student application.

It is evolving into:

> A modular local-first intelligent planning system.

---

# 2. Current Architectural Assessment

Current architecture style:

> MVVM + Layered Architecture + Domain-driven modularization

The project is currently in a:

> “Post-monolith refactor phase”

Meaning:

* responsibilities are being separated
* domain logic is emerging
* orchestration layers are forming
* engine boundaries are not yet fully stabilized

---

# 3. Current Architectural Risks

---

## 3.1 Semi-God Service Layer

The `Services/` folder still contains:

* orchestration
* business logic
* engine implementation
* adapters

This creates:

* unclear boundaries
* coupling
* refactor instability

---

## 3.2 Naming Inconsistency

Current naming:

```text
DecisionEngineService
WorkloadServiceImpl
SmartParser
```

These names suggest:

* generic services
* infrastructure-style components

While they are actually:

* domain engines
* orchestration modules

---

## 3.3 Service Locator Technical Debt

Current architecture still depends on:

```text
ServiceLocator.cs
```

This creates:

* hidden dependencies
* difficult testing
* weak DI boundaries

---

# 4. Recommended Architecture Direction

The system should evolve toward:

> Modular Domain-Driven MVVM Architecture

with:

* heuristic orchestration
* adaptive ML support
* pipeline-based processing
* strict engine isolation

---

# 5. Recommended Folder Structure

```text
SmartStudyPlanner
│
├── Core
│   ├── Engines
│   │   ├── Decision
│   │   ├── Planner
│   │   ├── Balancer
│   │   ├── Parser
│   │   └── Risk
│   │
│   ├── Rules
│   ├── Strategies
│   ├── Pipelines
│   ├── Algorithms
│   ├── Contracts
│   └── Models
│
├── Application
│   ├── Services
│   ├── DTOs
│   ├── UseCases
│   └── Interfaces
│
├── Infrastructure
│   ├── Persistence
│   ├── Repositories
│   ├── Logging
│   ├── Telemetry
│   ├── Notifications
│   └── Configuration
│
├── Presentation
│   ├── Views
│   ├── ViewModels
│   ├── Converters
│   └── Themes
│
└── Tests
```

---

# 6. Core System Philosophy

The planner system MUST remain:

* deterministic
* explainable
* testable
* heuristic-first

ML components should:

* support
* predict
* assist adaptation

ML components MUST NOT:

* autonomously generate schedules
* replace planner logic
* bypass heuristic validation

---

# 7. Stable Core Engines

These engines should become the long-term stable system backbone.

---

## 7.1 Decision Engine

Responsibility:

* priority scoring
* urgency evaluation
* competency gap calculation

> **Reconciliation (N5):** "competency gap calculation" is **net-new and undecided** — **zero
> occurrences** in the codebase today and no data model. Treat as aspirational until scoped; the
> shipped Decision Engine does priority scoring + urgency only.

Output:

```text
PriorityScore
```

Constraints:

* deterministic
* no ML dependency
* pure logic only

---

## 7.2 Planner Engine

Responsibility:

* orchestrate scheduling flow
* coordinate engines
* generate plans

This is the central orchestrator.

---

## 7.3 Balancer Engine

Responsibility:

* workload distribution
* overload prevention
* realistic scheduling

Constraints:

* max hours/day
* avoid burnout
* avoid repetition

> **Reconciliation (D-A/D-B/D2/N9):** the Balancer is slated to evolve into the **Study Optimization
> Engine** (see the SOE proposal `2026-06-30-workload-optimizer-proposal.md`, archived → `legacy/Archived plans/`),
> where balancing becomes one of several heuristics. Two constraints on that evolution:
> **(1) sequencing** — it sits on top of the sync-ready data model (Part A §A.3), not before it;
> **(2) scope** — the proposal's six sub-engines are in **direct tension with §13** below
> ("don't fragment engines / no unnecessary micro-engines"). Phase it behind an `IScheduleOptimizer`
> strategy seam (Load Balancer + Constraint Evaluator first). Compute model (**D-E**, amended
> 2026-07-02): a deterministic ordered pipeline — never a global search; `LearningEfficiencyScore` is an
> evaluation, not an argmax target. **Frozen guardrails
> ([D-G/D-H/D-J](../plans/2026-07-02-architecture-freeze-decisions.md)):** deadline feasibility, capacity
> and calendar limits are **hard constraints** owned by the Constraint Validator; the objective scores
> schedule quality only — `w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty
> + w5·FragmentationPenalty` (`w6·DeadlineUrgency` is **dropped**: deadline is a constraint, not a scored
> term); the SOE never worsens feasibility (`violations(out) ≤ violations(in)`). **Pass accept/commit
> granularity resolved** (G2, ratified 2026-08-05 — run-all/commit-best-prefix; see
> [G2 note](../plans/2026-08-04-g2-optimization-pass-semantics.md)); `w1…w5` governance resolved (G3,
> ratified 2026-08-07 — see [G3 note](../plans/2026-08-07-g3-weight-vector-governance.md)).
> **Implementation shipped** behind the `IScheduleOptimizer` strategy seam (execution plan
> `2026-08-04-epic-3-execution-plan.md`, Cards A–H) — not yet wired into the production pipeline
> (`BalanceWorkloadStage` still calls the pre-Epic-3 allocator path directly; that wiring is separate,
> unscheduled work). See §A.3 item 2 and the [epic closing note](../reports/2026-08-07-epic3-closing-note.md).

---

## 7.4 Risk Analyzer

Responsibility:

* detect procrastination
* detect overload
* detect deadline risk

Outputs:

```text
RiskLevel
RiskReason
```

---

## 7.5 Adaptive Rule Engine

Responsibility:

* adjust weights
* trigger re-planning
* apply adaptive heuristics

Uses:

* telemetry
* progress tracking
* ML advisory signals

BUT remains:

* deterministic
* rule-driven

---

# 8. Recommended ML Strategy

---

## IMPORTANT PRINCIPLE

The project is NOT an AI scheduling system.

It is:

> A heuristic scheduling system augmented by lightweight adaptive ML.

---

# 9. Recommended ML Submodels

Maximum:

* 1–2 ML models

Avoid overengineering.

---

## 9.1 Smart Parser (Primary ML Component)

This is the ONLY ML-first subsystem.

> **Reconciliation (D-D):** this is **consistent** with the "heuristic-first" philosophy (§6/§13)
> once scoped — the *system* is heuristic-first; the *parser* is the one place ML has precedence,
> applied **per output field with a confidence-gated fallback** (≥ 0.60, else heuristic).
> **Shipped today:** ML overrides **task type** only; difficulty and deadline are rule-based. The
> natural-language **deadline** parsing described below is the **M9 target**, not current behavior.
> See [`../architecture/pipeline.md`](../architecture/pipeline.md) §2.

Purpose:

* parse natural language deadlines
* infer scheduling intent
* resolve temporal expressions

Examples:

* “finish report before next Friday”
* “study OOP after midterm”
* “math exam in 2 weeks”

---

### Parser Pipeline

```text
Raw Input
↓
Tokenizer
↓
Intent Classification
↓
Entity Extraction
↓
Temporal Resolution
↓
Confidence Scoring
↓
Structured Output
```

---

### Output

```text
TaskName
Deadline
EstimatedUrgency
ConfidenceScore
```

---

### Parser Isolation Rule

The parser MUST NOT:

* schedule tasks
* allocate workload
* modify planner logic

Parser responsibilities end after:

* extraction
* inference
* confidence estimation

---

## 9.2 Performance Predictor (Optional)

Purpose:

* predict schedule success probability
* estimate workload tolerance
* detect failure likelihood

This model only provides:

* advisory signals
* adaptive hints

It MUST NOT:

* directly modify plans

---

# 10. ML Confidence & Fallback Policy

---

## ML Outputs Are Advisory

All ML outputs MUST include:

* confidence score
* uncertainty estimation

---

## Confidence Rules

### High Confidence

System MAY apply suggestion automatically.

---

### Medium Confidence

System SHOULD ask for user confirmation.

---

### Low Confidence

System MUST fallback to:

* heuristic logic
* safe defaults
* user input

---

## Fallback Pipeline

```text
ML Output
↓
Confidence Validation
↓
If valid:
    Apply Suggestion
Else:
    Fallback → Heuristic Engine
↓
If still ambiguous:
    Ask User
```

---

# 11. Telemetry & Analytics Direction

The system should track:

* study consistency
* completion rate
* parser correction frequency
* workload deviation
* user overrides

Telemetry should support:

* adaptive heuristics
* future ML improvements
* behavior analysis

---

# 12. Immediate Refactor Priorities

---

## PRIORITY 1 — Freeze Core Boundaries

Stabilize:

* Decision Engine
* Planner Engine
* Balancer Engine
* Risk Analyzer

Before adding more features.

---

## PRIORITY 2 — Split Orchestration vs Logic

Avoid:

```text
DecisionEngineService
    handles everything
```

Move toward:

```text
DecisionEngine
PriorityCalculator
UrgencyRule
```

---

## PRIORITY 3 — Replace Service Locator

Migrate to:

```csharp
Microsoft.Extensions.DependencyInjection
```

Use:

* constructor injection
* interface-based contracts

---

## PRIORITY 4 — Stabilize Parser Pipeline

Finalize:

```text
Tokenizer
↓
Intent Classifier
↓
Entity Extractor
↓
Temporal Resolver
↓
Confidence Validator
```

Before:

* ML retraining
* NLP optimization

---

# 13. Anti-Overengineering Rules

DO NOT:

* introduce deep learning
* create autonomous planners
* tightly couple ML to scheduling core
* fragment engines excessively
* create unnecessary micro-engines

The project should remain:

* local-first
* maintainable
* explainable
* deterministic

---

# 14. Recommended Development Roadmap

---

## v1.0

* stable heuristic planner
* engine separation
* balancing logic
* parser pipeline skeleton

---

## v1.2

* adaptive rule engine
* telemetry integration
* parser ML integration

---

## v1.5

* workload prediction
* performance estimation
* smarter adaptive weighting

---

## v2

* advanced analytics
* **multi-device two-way LAN sync** (D-A) — replaces the earlier "optional cloud sync" direction
* enhanced recommendation system

---

# 15. Final System Identity

This system should behave like:

> A deterministic planning engine assisted by adaptive intelligence.

NOT:

> An autonomous AI scheduler.

The planner remains:

* deterministic
* explainable
* stable

The ML layer improves:

* usability
* adaptability
* prediction quality

---

# 16. Final Engineering Principle

```text
Plan → Execute → Measure → Adapt → Re-plan
```

This feedback loop defines the entire system architecture.

---
