# Architecture Specification Review — Smart Study Planner

> **⚠️ Correction (2026-07-01) — read first.** Finding **C2** below asserts the *shipped parser is
> heuristic-first*. Verified against the code, that is **wrong**: the parser runs a heuristic
> *baseline*, but the ML classifier **overrides task type** when confidence ≥ 0.60 — i.e. **ML-first
> with a confidence-gated fallback, per field** (difficulty and deadline stay rule-based). The roadmap's
> "ML-first" framing was therefore closer to reality than this review credited. The corrected direction
> is recorded as decision **D-D** in
> [`../plans/2026-07-01-architecture-direction-decisions.md`](../plans/2026-07-01-architecture-direction-decisions.md)
> (§2 = verified reality). The rest of this snapshot is left unchanged.

> Reviewer role: lead architect. Date: 2026-07-01. Scope: the architecture
> specification under `docs/architecture/*` (`overview`, `pipeline`, `data-model`,
> `dependency-flows`, `async-workflow`, `usecase-flows`) plus `docs/specs/system_roadmap.md`
> and `README.md`. **No code was changed.** Findings are sourced from the spec text;
> two items are explicitly marked *verify* because they infer beyond what the spec states.

## How this was judged

The project states its own architectural philosophy (`system_roadmap.md` §6, §13, §15):
**deterministic, explainable, heuristic-first, ML strictly advisory, local-first, no
overengineering.** That is a good rubric, so I graded the architecture against *its own
stated principles* rather than an external ideal. Where the shipped design honours those
principles it is called out as a strength to preserve; where it drifts from them — or from
itself — it is a finding.

## Strengths worth protecting (don't refactor these away)

- **Strategy seams are clean and correctly grained.** `IUrgencyRule`, `IPriorityComponent`,
  `ITaskTypeKeywordParser`/`IDifficultyKeywordParser`/`IDeadlineKeywordParser`, and the risk
  `Evaluators/*` set are the right extensibility points. They are the good abstractions;
  simplification below must not touch them.
- **Offline-first ML with deterministic fallback everywhere** (parsing, minute prediction,
  weight suggestion) genuinely matches the stated philosophy. Atomic temp-swap model writes
  and `SemaphoreSlim` lifecycle serialization are solid.
- **Per-stage pipeline classes** give real unit-test seams even though the *orchestration*
  around them is leaky (see A1).
- **The docs are unusually honest** — they self-flag their own smells ("đừng nhầm hai bộ
  nhãn", "notice how thin it is"). That culture is an asset; the recommendations below try to
  remove the *reasons* for those warnings rather than the warnings.

## Severity map

| # | Finding | Type | Severity |
|---|---------|------|----------|
| A1 | Pipeline is a partial/leaky seam that also masks stage failures | Boundary + correctness | High |
| A2 | Inter-stage data dependencies undefined (README vs arch stage-order is the symptom) | Boundary | High |
| A3 | Risk → Scheduling backward call breaks Core module independence | Coupling | High |
| A4 | Two parallel severity taxonomies share one UI vocabulary | Missing boundary | Med-High |
| A5 | Domain invariants live in ViewModels, not the domain | Missing boundary | Med-High |
| A6 | Fire-and-forget study-log writes collide with the next milestone (M8-C) | Correctness/reliability | Med-High |
| B1 | Over-deep decision/scheduling layering + inverted naming | Unnecessary abstraction | Medium |
| B2 | ML confidence gate reimplemented instead of shared (*confirmed*) | Duplication | Medium |
| B3 | Half-migrated repository layer is long-lived debt | Refactor debt | Medium |
| B4 | `ServiceLocator` anti-pattern alongside DI | Refactor debt | Medium |
| B5 | Two advisory-suggestion subsystems, neither with an apply path | Duplication | Low-Med |
| C1 | Target architecture (roadmap) never reconciled with actual structure | Direction gap | Medium |
| C2 | Roadmap's ML section contradicts the (better) shipped reality | Direction gap | Low-Med |
| Appx | Spec-as-artifact hygiene (drift, authority, volatile counts) | Documentation | Low |

---

## Tier 1 — Architecture: boundaries, coupling, correctness

### A1. The pipeline is a partial, leaky orchestration seam — and it masks failures

The 5-stage pipeline is marketed as the planning brain (`README` §5, `overview` §5.3,
`pipeline` §3), but it does not actually own the computation it claims to:

- **Stage 1 is a no-op.** `ParseInputStage` only does `RawInput.Trim()` and sets
  `ParsedTaskCount = 1`; real classification is a *separate* flow (`ParsingOrchestrator`).
  The docs repeat the warning "this is NOT the classifier" in three places. A stage that
  exists only to keep the count at five, and that every doc has to warn readers about, is a
  misleading abstraction, not a planning step.
- **Consumers bypass it.** `DashboardViewModel` falls back to calling `IDecisionEngine` /
  `IRiskAnalyzer` *directly* when the pipeline "doesn't fill a slot" (`dependency-flows` §3,
  `usecase-flows` UC-01 step 6). `WorkloadBalancer` calls `IDecisionEngine.CalculatePriority`
  per task directly plus an "optional pipeline run" (UC-08).
- **It masks failures.** `dependency-flows` §5 says the orchestrator stops early on a stage
  failure; §3 says the ViewModel then silently recomputes via direct calls. Net effect: a
  stage can throw, the pipeline aborts, and the dashboard still renders a plausible result.
  The failure is invisible — an observability and correctness cost on top of the consistency
  risk.
- **Priority is computed in at least four places** — `PrioritizeStage`,
  `WorkloadServiceImpl.GenerateSchedule`, `QuanLyTaskViewModel.TinhDiemVaSapXep`, and the
  `DashboardViewModel` per-task loop. There is no single source of computed truth for a
  task's score.

**Recommendation.** Decide the pipeline's role and commit:
- *Option A (keep it):* make it the single computation seam. All consumers go through it;
  `PipelineContext` carries every intermediate result; delete the direct-call fallbacks;
  surface stage failures instead of swallowing them.
- *Option B (drop it):* let ViewModels call the engines directly and delete the orchestrator.

The current halfway state pays the full abstraction cost and forfeits its only real benefit
(one authoritative computation). Either way, **keep the per-stage classes** — they are good
test seams and can back either option.

### A2. Inter-stage data dependencies are undefined — the stage-order disagreement is the symptom

`README` orders the stages *Prioritize → AssessRisk → BalanceWorkload*; the architecture docs
order them *Prioritize → BalanceWorkload → AssessRisk*. This is not a typo to fix — ask *why
two docs could disagree and both seem fine*. The answer: **AssessRisk does not consume
BalanceWorkload's output at all.** `ProgressGapRiskEvaluator` reaches back into
`IDecisionEngine.CalculateRawSuggestedMinutes` (`pipeline` §3.2) rather than reading the
schedule the previous stage produced. Because no real data flows between stages 3 and 4,
their order is arbitrary, and each doc author picked a different one.

The deeper problem is that stages neither declare their inputs/outputs nor read them from a
disciplined contract. `PipelineContext` is a 13-field mutable god-bag (`Semester, Settings,
ReferenceTime, RawInput, ParsedInput, PrioritizedTasks, Schedule, RiskReport, Adaptations,
Warnings, Errors, Metadata, Status`) that any stage may read or overwrite. As stages grow,
ordering bugs become silent.

**Recommendation.** Give each stage an explicit `consumes`/`produces` contract. Make
AssessRisk read suggested minutes/schedule *from context* (produced upstream) instead of
re-invoking the engine. Once data dependencies are real, correct ordering is enforced by the
data, not by a hand-maintained enum that two documents already contradict.

### A3. Risk → Scheduling backward call breaks Core module independence

`Core/Risk`'s `ProgressGapRiskEvaluator` calls back into `IDecisionEngine.CalculateRawSuggestedMinutes`
— i.e. into `Core/Scheduling` via the decision facade (`pipeline` §3.2, marked "gọi ngược").
So the risk subsystem is not the independent evaluator the layering implies; it is coupled to
scheduling's minute estimation. This also contradicts the project's own target, where the Risk
Analyzer is a stable, isolated engine (`system_roadmap` §7.4, §7).

**Recommendation.** Pass suggested minutes into the risk evaluator as an input value (via
`PipelineContext` or an explicit parameter), not by reaching into another engine. This both
restores Core-module independence and removes the hidden ordering dependency in A2 — the two
findings share one fix.

### A4. Two parallel severity taxonomies wear the same UI vocabulary

The dashboard renders severity from **two different, independent systems**:
- Risk-based: `RiskAssessment.FromScore` — ≥0.8 Critical / ≥0.6 High / ≥0.3 Medium / Low
  (`pipeline` §3.2).
- Priority-based: `DashboardViewModel.GetWarningLevel` — `DiemUuTien` ≥80 "Khẩn cấp" /
  ≥50 "Chú ý" / else "An toàn".

Both surface the same "Khẩn cấp / Chú ý / An toàn"-style language on the same screen, from
different inputs and thresholds. The spec itself has to warn "đừng nhầm hai bộ nhãn này."
A label vocabulary with two owners and no boundary is a latent source of user and developer
confusion.

**Recommendation.** One severity model owns the badge. Either derive the dashboard badge from
risk only, or namespace the two explicitly ("Ưu tiên: …" vs "Rủi ro: …") so identical words
never mean two things. Assign a single owner for severity/labeling.

### A5. Domain invariants are enforced in ViewModels, not the domain (missing boundary)

`usecase-flows` UC-02 shows `QuanLyTaskViewModel` clamping `DoKho` to 1..5 and null-checking
`TenTask`/`HanChot`. Nothing in the spec says `StudyTask` enforces these itself. Every *other*
creation path — `WorkloadService`, tests, the quick-input parser, and the roadmap's future
NL-deadline importer — bypasses those guards and can construct an invalid task.

**Recommendation.** Push invariants into the domain: a `StudyTask` factory/constructor that
rejects invalid difficulty and requires name/deadline, or a `Difficulty` value object. Leave
*presentation* validation (friendly messages) in the ViewModel. This boundary matters more,
not less, as task-creation entry points multiply.

### A6. Fire-and-forget study-log writes collide with the next milestone

`async-workflow` §8 (rule 5) and UC-07 state that `StudyLog` writes from Focus mode are
fire-and-forget and "accept the tradeoff that a crashed write loses the log." That was a
reasonable UX call — **until** M8-C. The roadmap's next milestone (`README`; `data-model` §8)
is explicitly "retrain the Study Time Predictor on real telemetry accumulated from completed
Focus sessions." The ground-truth training data M8-C depends on is exactly the data the async
design is willing to drop.

**Recommendation.** Reclassify focus-completion log durability from "perf tradeoff" to
"correctness boundary" before M8-C lands. Await the completion write (or use a small
retry/outbox, or at minimum log-on-failure). The timer can stay responsive for *tick* updates;
it is the terminal `focus_complete` write that must not be lost.

---

## Tier 2 — Simplification & refactor debt (while preserving extensibility)

### B1. Over-deep decision/scheduling layering, plus inverted naming

The priority path is `IDecisionEngine` (`DecisionEngineService`, a 42-line pass-through
facade — `overview` §4) → `SchedulingOrchestrator` → `PriorityEvaluator` → `PriorityCalculator`
→ (`IUrgencyRule` chain + `IPriorityComponent` set). That is four-to-five layers to produce a
single score; the overview itself says "notice how thin it is." Two of those layers are thin
forwarders.

Naming compounds it: **`SchedulingOrchestrator` does not schedule.** It computes priority,
owns `WeightConfig` + self-heal, and holds the ML minute predictor — while the *actual*
scheduling (distribution across days) lives in `WorkloadServiceImpl`. `system_roadmap` §3.2
independently flags `DecisionEngineService`/`WorkloadServiceImpl` naming as misleading.
Additionally, `SchedulingOrchestrator` conflates deterministic priority scoring with
ML-augmented minute prediction (`IStudyTimePredictor` injected), which violates the project's
own §7.1 constraint that the Decision Engine be "pure logic, no ML dependency."

**Recommendation.**
- Collapse one of the thin layers — keep a single public contract (`IDecisionEngine`) over a
  single implementation, not a facade *and* an evaluator *and* a calculator that mostly
  forward.
- Rename to a coherent ubiquitous language: prioritization (`Prioritizer`/`PriorityEngine`)
  vs distribution (`Balancer`) vs `StudyTimePredictor`.
- Split minute-prediction out of the priority engine so the priority score stays ML-free per
  the project's stated philosophy.
- **Preserve** the `IUrgencyRule` / `IPriorityComponent` strategy sets untouched — that is the
  extensibility worth keeping.

### B2. ML confidence gating is reimplemented instead of shared (*confirmed in code*)

`IMlConfidencePolicy`/`DefaultMlConfidencePolicy` is injected into `IntentClassifierAdapter`
(the text-classifier path), but `StudyTimePredictorService` hardcodes `if (confidence >= 0.6f)`
(`Services/ML/StudyTimePredictorService.cs:45`) rather than using the policy; `WeightRuleEngine`
also uses 0.60/0.75 bands independently. The same threshold and the same High/Review/Reject
concept live in three places — while `system_roadmap` §10 defines *one* confidence/fallback
policy for all ML.

**Recommendation.** Route all three ML surfaces through `IMlConfidencePolicy`. One place owns
the 0.60/0.75 bands. Cheap, and it makes the "ML is advisory with a uniform gate" principle
actually true instead of aspirational.

### B3. Half-migrated repository layer is long-lived debt

Two persistence abstractions coexist (`data-model` §5, `dependency-flows` §7): legacy
`IStudyRepository`/`StudyRepository` (wide surface, used by most ViewModels) and the new
per-aggregate `I*Repository` (Slice 4), consumed today by only `StudyAnalyticsService` plus
four tests. Migration is "intentionally deferred." Deferred half-migrations tend to become
permanent, and the app now carries two ways to do the same job. The `Func<AppDbContext>`
factory in the new repos exists mainly for test isolation — worth asking whether scoping the
`DbContext` would remove the need for the factory entirely.

**Recommendation.** Attach a definition-of-done and a boundary rule: **no new consumers on the
legacy repository.** Then either finish the migration or formally retire the new layer. Do not
keep two indefinitely.

### B4. `ServiceLocator` — service-locator anti-pattern alongside proper DI

Flagged in three docs (`overview` §7, `dependency-flows` §10, `system_roadmap` §3.3 / §12
Priority 3). Some ViewModels resolve via static `ServiceLocator.Get<T>()` instead of
constructor injection, creating hidden dependencies and harder testing.

**Recommendation.** Endorse the project's own Priority 3: introduce a ViewModel
factory/constructor injection, add a review/lint rule forbidding *new* `ServiceLocator.Get`
calls, and optionally migrate composition to `HostBuilder`. Sequence this early because B1/C1
are easier once dependencies are explicit.

### B5. Two advisory-suggestion subsystems, neither with an apply path

`AdaptStage` produces `AdaptationSuggestion` (rule-based, explicitly does not mutate the
domain) and `WeightOptimizer` produces `WeightConfigSuggestion` (read-only, apply deferred to
"Slice 8"). Two parallel half-built feedback loops that both emit advice nobody applies yet.
Separately, the `README` overstates this: "The Adaptive Pipeline re-balances your workload
every time it runs," while the docs say Adapt only *suggests* and never mutates.

**Recommendation.** Unify both under one "advisory suggestion + review/apply" surface (the
roadmap's §7.5 Adaptive Rule Engine is meant to be *one* engine, not two scattered halves).
Align the README wording with reality ("suggests re-balancing", not "re-balances").

---

## Tier 3 — Direction vs. reality (the roadmap itself)

### C1. A target architecture exists but is never reconciled with the actual structure

`system_roadmap` §5/§7 defines a Clean-Architecture target — `Core/Engines/{Decision, Planner,
Balancer, Parser, Risk}` + `Application` + `Infrastructure` + `Presentation` + named stable
engines. The actual layout (`overview` §4) is `Core/{Parsing, Scheduling, Risk, ML/Contracts}`
+ a **semi-god `Services/` grab-bag** (orchestration + ML + telemetry + strategies + pipeline
all mixed) + a legacy `Data/`. There is no `Application` layer; `Telemetry` and `Strategies`
sit under `Services/` rather than where the target puts them. Crucially, **none of the six
architecture docs reference the roadmap**, so the north star and the map of the territory are
maintained independently.

**Recommendation.** Treat the roadmap as the north star but **do not big-bang the folder
reorg** — a full four-layer reshuffle on a solo v1.5 project would violate the roadmap's own
§13 anti-overengineering rule. Adopt incrementally, in this order: (1) freeze engine
contracts + rename per B1; (2) carve `Application` vs `Services` by moving the semi-god
`Services/` pieces behind clear engine boundaries; (3) relocate `Telemetry`/`Strategies` last.
Sequence behind B4 (explicit DI) so moves are mechanical.

### C2. The roadmap's ML section contradicts the (better) shipped reality

- `system_roadmap` §9.1 calls the parser "the ONLY ML-first subsystem" / "Primary ML
  Component," yet §6 and §13 demand heuristic-first, and the *shipped* parser IS heuristic-first
  with ML augmenting only task *type* at confidence ≥0.60 (`pipeline` §2). **The implemented
  design is the correct one**; the roadmap's "ML-first parser" vision is stale and internally
  self-contradictory.
- §7.1 says the Decision Engine has "no ML dependency," but the shipped
  `SchedulingOrchestrator` injects `IStudyTimePredictor` (see B1's split recommendation).
- §3.2 and §9.1 still reference `SmartParser`, which was retired (commit `222cb5a`).

**Recommendation.** Here the spec should follow the code, because the code is better than the
plan: rewrite the roadmap's ML strategy to describe what shipped (heuristic-first; ML augments
task-type; a *separate* advisory minute predictor), and delete the `SmartParser` references.

---

## Appendix — Spec-as-artifact hygiene (documentation governance)

Lower severity, but it actively causes the confusion above:

- **No master index / competing authority claims.** `pipeline.md` declares itself "nguồn
  chuẩn … ưu tiên file này khi mâu thuẫn"; `data-model.md` claims "canonical source of truth";
  `system_roadmap.md` sets direction. Nothing says which doc owns which concern.
- **The drift-tracking section has itself drifted.** `pipeline.md`'s preamble says
  `overview` §5.4/§5.5 "describe dead types," but `pipeline.md` §6 says §5.4 was already
  updated, and `overview` §5.5 now correctly states `SmartParser` was retired. The warnings
  about staleness are stale.
- **Stale use-case step.** `usecase-flows` UC-03 step 2 still shows `SmartParser.Parse(...)`
  after `SmartParser` was retired and `IParsingOrchestrator` injection was made mandatory.
- **Volatile counts embedded in prose.** `overview` §3 "156 passing," `README` "289 tests" —
  different snapshots; specs also pin "as of Slice N."

**Recommendation.** (1) Add one master INDEX assigning exactly one normative owner per concern
(pipeline behavior / data model / dependency flow / direction); everything else links rather
than restates. (2) Remove test counts and "as of Slice N" from prose — those belong in CI
output, not documents. (3) Consider generating `dependency-flows` and route diagrams from the
GitNexus graph so they cannot drift by hand. (4) Delete the manual "known drift" sections —
they are drift generators; a single changelog does the job.

---

## Items to *verify* (inferences beyond the spec text — confirm before acting)

- **`HocKy.NgayKetThuc` is `[NotMapped]` with an auto/manual flag** (`data-model` §2). If a
  manual end-date override is a real feature, a `[NotMapped]` property is not persisted and
  would be recomputed to `NgayBatDau + 150 days` on reload — silently discarding the user's
  value. Verify against `Models/HocKy.cs` whether the manual override (and its flag) is
  actually persisted; if not, this is a data-loss bug, not just a modeling nit.

---

## Suggested sequencing (highest leverage first)

1. **B4 (explicit DI)** — unblocks clean refactoring of everything else.
2. **A1 + A2 + A3 together** — decide the pipeline's role, define stage I/O contracts, and
   remove the risk→scheduling backward call as one coherent change to the planning core.
3. **B1** — collapse a layer and fix naming once dependencies are explicit.
4. **A5, B2** — cheap, high-consistency correctness/boundary wins.
5. **A6** — before M8-C starts consuming focus telemetry.
6. **A4, B3, B5, C1, C2, Appendix** — consolidation and doc reconciliation, incremental.

Throughout: **preserve the strategy-based extensibility seams** (`IUrgencyRule`,
`IPriorityComponent`, keyword parsers, risk evaluators). The simplification target is the
orchestration/facade layering and the documentation sprawl — not the domain strategy sets,
which are the part of this architecture most worth keeping.
