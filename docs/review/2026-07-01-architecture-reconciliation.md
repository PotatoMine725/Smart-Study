# Architecture Reconciliation Review — Smart Study Planner

> **⚠️ Correction (2026-07-01) — read first.** The parser premise behind the **C2** row and **D1** below
> is superseded. Verified against the shipped code: the parser runs a heuristic *baseline* but the ML
> classifier **overrides task type** at confidence ≥ 0.60 — **ML-first with a confidence-gated fallback,
> per field** (difficulty and deadline stay rule-based), **not** the "heuristic-first" stated here.
> Consequently **D1 is resolved, not an open fork**: the direction is ML-first-with-fallback (decision
> **D-D**). D2 is resolved in direction (decision **D-E** — a deterministic ordered pipeline, not a
> global search). Everything else in this reconciliation stands.
> See [`../plans/2026-07-01-architecture-direction-decisions.md`](../plans/2026-07-01-architecture-direction-decisions.md) §2 & D-D.
>
> **Amendment (2026-07-02)** — per [`../plans/2026-07-02-architecture-freeze-decisions.md`](../plans/2026-07-02-architecture-freeze-decisions.md):
> **N4 is resolved** (D-G drops `w6·DeadlineUrgency`; deadline is a hard constraint, so the double-count
> is gone) — the SOE weight vector in B5/N6/D5 now reads **`w1…w5`** (governance itself still open).
> D-E's per-step accept/reject sub-question was **reopened**: the pass accept/commit semantics are an
> explicitly open decision (freeze record §3), not settled per-step evaluation.

> Reviewer role: lead architect. Date: 2026-07-01. This is a **reconciliation** of three
> sources, produced under the explicit assumption that the Study Optimization Engine (SOE)
> proposal is **accepted**:
>
> 1. **Current spec** — `docs/architecture/*` + `docs/specs/system_roadmap.md` + `README.md`.
> 2. **Prior review** — `docs/review/2026-07-01-architecture-spec-review.md` (findings A1–C2 + appendix).
> 3. **Proposal (unmerged)** — `docs/plans/2026-06-30-workload-optimizer-proposal.md`.
>
> The prior review is preserved unchanged as a snapshot. **No code was modified.** This document
> supersedes the prior review's *status* only; its findings' identifiers (A1…C2) are carried over.

---

## 0. The one framing that governs everything below

**A design proposal cannot *resolve* implementation debt.** The SOE proposal is a direction, not
a built system, so it can (a) set a target that supersedes a *symptom*, and (b) change the *shape*
of future work — but it cannot retire any existing debt until code exists. Therefore:

> **No prior finding is Obsolete.** Several *symptoms* are superseded; every *root cause* either
> persists or is **reinforced** by the added surface area. That is the honest result of the
> reconciliation, not a refusal to use the category.

Two conflicts surfaced that this review **cannot resolve unilaterally** — they are decisions the
proposal's author must make before any code (see §3, D1 and D2). Everything else is downstream of
those two.

---

## 1. Reclassification of prior findings

| # | Finding (short) | Prior sev | **New status** | Why the proposal changes (or doesn't change) it |
|---|---|---|---|---|
| A1 | Pipeline is a leaky seam that masks failures | High | **Still Valid — reinforced** | Leak/fallback/masking untouched; proposal adds a *second* pipeline (N2). |
| A2 | Inter-stage data dependencies undefined | High | **Still Valid — root cause reinforced** | Stage-order arrow dispute *superseded*; same defect re-appears acutely inside SOE (N1). |
| A3 | Risk → Scheduling backward call | High | **Needs Revision** | "Suggested minutes" moves to SOE; call target relocates — must re-trace (N3, N5). |
| A4 | Two severity taxonomies, one UI vocabulary | Med-High | **Still Valid — reinforced** | Adds a third score (`LearningEfficiencyScore`) → proliferation (N4). |
| A5 | Domain invariants live in ViewModels | Med-High | **Still Valid** | Constraint Evaluator governs *schedule* constraints, not *entity* invariants — adjacent, not a fix. |
| A6 | Fire-and-forget study-log writes vs M8-C | Med-High | **Still Valid — reinforced** | Fatigue/Context/Session heuristics add more consumers of durable session history (N7). |
| B1 | Over-deep decision layering + inverted naming | Med | **Partially Resolved** | Naming/responsibility split addressed *in principle*; facade layers still open; Decision Engine simultaneously *gains* scope (N5). |
| B2 | ML confidence gate reimplemented | Med | **Still Valid** | Untouched; SOE may add further threshold/weight surfaces to unify. |
| B3 | Half-migrated repository layer | Med | **Still Valid — reinforced** | Data-hungry heuristics raise the cost of building on an unfinished persistence layer. |
| B4 | `ServiceLocator` anti-pattern | Med | **Still Valid — reinforced** | Proposal multiplies injectable components (6 optimizers + engines); clean DI becomes urgent. |
| B5 | Two advisory subsystems, no apply path | Low-Med | **Needs Revision** | A third tunable weight-set (`w1…w6`) enters; Adaptive Rule Engine is the intended unifier (N6). |
| C1 | Target vs actual structure unreconciled | Med | **Needs Revision** | Proposal is a *third* folder structure; supersedes roadmap's `Balancer` as a top engine — now a 3-way reconciliation. |
| C2 | Roadmap ML section contradicts shipped reality | Low-Med | **Still Valid — escalated to a required decision** | Proposal + user restate "parser is ML-first"; three docs now contradict the code (D1). |
| Appx | Spec-as-artifact hygiene | Low | **Still Valid — reinforced** | A fourth unreconciled doc, a third folder layout, and reuse of retired names ("Smart Parser", "Workload Balancer"). |
| Vfy | `HocKy.NgayKetThuc` `[NotMapped]` persistence | (verify) | **Still Valid** | Now feeds Constraint Evaluator's date constraints — persistence must be confirmed. |

Tally: **Still Valid 11 · Needs Revision 3 · Partially Resolved 1 · Obsolete 0.**

### Notes on the reclassified items

- **A2 — symptom superseded, root cause reinforced.** If the new Planner Engine pipeline (§8)
  replaces the 5-stage `PipelineOrchestrator`, the README-vs-arch stage-order dispute simply
  disappears. But the *root cause* — stages/heuristics not declaring their read/write sets — returns
  in a sharper form inside the SOE (see N1). Net: the finding is more important, not less.

- **A3 — Needs Revision.** Today `ProgressGapRiskEvaluator` calls `IDecisionEngine.CalculateRawSuggestedMinutes`.
  Under the proposal, allocation/minutes move to the SOE (Initial Allocation + Session Optimizer),
  while the Decision Engine keeps a *new* "progress evaluation" responsibility. So the backward call
  either (a) relocates to `Risk → SOE`, or (b) needs re-scoping if "progress" now lives in the
  Decision Engine. Which one Risk actually needs must be traced before the fix in the prior review
  is valid. The good news: if Risk runs *after* SOE, it can read allocated minutes from context and
  the backward call vanishes — but only if D4 (§3) is decided that way.

- **B1 — Partially Resolved, with a countervailing pull.** The proposal's taxonomy (Planner Engine
  orchestrates · Decision Engine scores · SOE allocates · Load Balancer balances) fixes the
  "`SchedulingOrchestrator` doesn't schedule" naming inversion *in principle*, and its "Decision
  Engine must remain deterministic, never allocate sessions" endorses my recommendation to split
  minute-prediction out of the priority path. **But** the same section *expands* the Decision Engine
  with "competency evaluation" and "progress evaluation" — neither exists today (grep-confirmed:
  `competency` has **0 occurrences** in the codebase). So the engine is renamed *and* enlarged at
  once; the facade-depth half of B1 is untouched. Treat B1 as "direction set, execution + scope
  still open."

- **A5 — do not overcredit the Constraint Evaluator.** It is a genuinely good new home for *schedule*
  constraints (deadlines, max workload, unavailable dates, exams). It is **not** a home for *entity*
  invariants (`DoKho ∈ 1..5`, required `TenTask`/`HanChot`), which still leak in the ViewModel. A5
  stands; the Constraint Evaluator merely establishes the *pattern* that entity invariants should
  follow into the domain.

---

## 2. New architectural implications introduced by the proposal

Ordered by how much they should change the plan. **N1 is co-top with the two-pipeline problem and
arguably the single most consequential item in this document.**

### N1 — The SOE model is internally contradictory: greedy transform vs. objective search
- §6 defines the goal as **maximize** `LearningEfficiencyScore = Σ wᵢ·metricᵢ` and says the planner
  should "**search for the highest overall score**." That is an *argmax over candidate schedules*.
- §8 defines the SOE as a **sequential pipeline**: Load Balancer → Session Optimizer → Context →
  Fragmentation → Fatigue → Constraint Validation. That is an *order-dependent greedy transform*.
- **These are not the same computation.** A greedy sequence of heuristic mutations does not, in
  general, produce the argmax of a weighted-sum objective — and the heuristics provably conflict
  (load-balancing spreads work thin; deep-work continuity clumps it). You cannot implement §6 and §8
  both verbatim.
- **Discriminating test to hand the author:** *"Must the §8 pipeline output equal the
  max-`LearningEfficiencyScore` schedule of §6?"* If yes → you need search (evaluate candidates,
  pick best). If "good enough is fine" → you have a greedy transform and §6's objective is only a
  *diagnostic*, not the algorithm. Pick one explicitly.
- **Constraint from the project's own philosophy:** the roadmap (§6/§15) mandates *deterministic +
  explainable*. A search over a weighted objective introduces non-determinism (near-ties, tie-break
  order) and weakens explainability unless tie-breaking is pinned deterministically. The greedy
  transform is naturally deterministic and explainable but sub-optimal. This tension must be resolved
  *before* the objective function is written, not after.

### N2 — Two coexisting orchestration pipelines, relationship undefined
- Existing: `PipelineOrchestrator` — `ParseInput → Prioritize → BalanceWorkload → AssessRisk → Adapt`.
- Proposed (§8): Planner Engine — `Priority → Constraint → Initial Allocation → SOE → Validation`.
- The two overlap (both compute priority) and diverge (the new one has no explicit Prioritize-as-stage
  boundary, no Adapt, no Risk). Is the new pipeline **the internals of the old `BalanceWorkload`
  stage**, or a **replacement** for the whole 5-stage pipeline? Undecided. Until it is, A1's leaky-seam
  problem now has *two* seams to leak through.

### N3 — Risk Analyzer is orphaned from the new pipeline
§8 omits Risk entirely, yet the proposal lists Risk Analyzer as an affected component. Does Risk run
**before** optimization (feeding constraints/urgency) or **after** (scoring the final schedule)? This
placement decision *is* the resolution of A3 — decide it deliberately (D4), don't let it fall out.

### N4 — Score proliferation and double-counted deadline urgency
The system will carry `PriorityScore` (Decision), `RiskLevel` (Risk), and `LearningEfficiencyScore`
(SOE). Deadline urgency already lives in `PriorityScore` (via `TimeComponent`) **and** appears again
as `w6·DeadlineUrgency` in the SOE objective — a likely **double count**. Define, once, which score
owns ordering, which owns display (A4), and whether urgency is an input to the objective or already
baked into the initial allocation.

### N5 — Decision Engine scope silently expands (net-new domain concepts)
"Competency evaluation" (0 occurrences today) and "progress evaluation" are added to the Decision
Engine. "Competency" has **no data model, no source signal, and no persistence** behind it. Either
scope it out of this proposal or spec its inputs — otherwise it becomes a vague responsibility that
blocks the "Decision Engine is deterministic + stable" guarantee. Folds into N7.

### N6 — A second tunable weight vector (`w1…w6`) needs governance
The SOE objective introduces optimization weights parallel to the existing priority `WeightConfig`.
The current `WeightConfig` has `IsValid()`, `Normalize()`, and self-heal guardrails; `w1…w6` will need
the same, plus an owner. This directly extends B5: does `WeightOptimizer` now also tune these? If so,
the "two advisory subsystems" become three tunable surfaces — consolidate under the Adaptive Rule
Engine rather than growing another parallel loop.

### N7 — The domain model must grow, and B3/A6 gate it
Fatigue ("historical fatigue", "user behavior"), Context Switch ("subjects per day", "session
continuity"), Fragmentation ("task splitting into parts"), and Session Optimizer ("deep work sessions")
all require model concepts that don't exist today: intra-day session *ordering*, task *segmentation*,
per-session subject continuity, and a fatigue/competency signal. The current model
(`StudyLog`, `ScheduleDay`, `ScheduledTask`) does not represent these. Consequence: **the repository
migration (B3) should finish, and durable session logging (A6) should land, before these heuristics
are built** — they are the data foundation the heuristics stand on.

### N8 — "Soft balancing" changes the determinism/testability posture
Moving from "minimize variance" (one deterministic answer) to "accept 40–60 min if score improves"
(a ranged, score-driven decision) means schedules become harder to assert in tests and harder to
explain to users. Pair every heuristic with a deterministic tie-break rule and a one-line rationale
string (the parser already does this with `ParseSource`; mirror that pattern).

### N9 — Overengineering risk vs. the project's own anti-fragmentation rule
The roadmap §13 explicitly forbids "fragment engines excessively / create unnecessary micro-engines,"
and §9 caps ML at "1–2 models, avoid overengineering." Six optimizer sub-engines — several described
only as "future heuristics may include…" — for a solo v1.5 desktop app is in tension with that rule.
**Recommendation: phase it.** Ship `Load Balancer` (exists) + `Constraint Evaluator` behind the new
interface first; stub the other four as no-op strategies. Build them when a real scheduling complaint
demands each one.

### N10 — The proposal's real value is an extension point; design it like the existing strategy seams
The best thing here is SOE-as-plugin-host for optimization heuristics — the exact shape the codebase
already does well with `IUrgencyRule` / `IPriorityComponent`. Define a single
`IScheduleOptimizer { Schedule Apply(Schedule, Context) }` (or a scoring variant if D2 picks search),
compose them in the SOE, and the six sub-engines become ordered strategies rather than bespoke
classes. This preserves extensibility *and* keeps the Planner Engine stable — which is precisely the
proposal's stated §12 benefit. **Get this contract right; everything else is a strategy behind it.**

### N11 — Three constraint checkpoints (minor; a decision, not a defect)
The proposal checks constraints three times: "Constraint Evaluation" (early, §8), "Constraint
Validation" (inside SOE, §5.6/§8), and "Schedule Validation" (after). Decide whether that is intended
defense-in-depth (pre-filter the search space + post-validate the winner) or a muddled boundary.
Defensible either way — but say which.

---

## 3. Decisions this reconciliation surfaces (resolve before any code)

These are ranked. D1 and D2 are the ones the review cannot decide for the author.

| ID | Decision | Blocks | Ties to |
|----|----------|--------|---------|
| **D1** | **Parser: ML-first or heuristic-first?** The proposal + roadmap + user all say "ML-first"; the shipped code is heuristic-first with ML augmenting task-type only (verified). Either move the code to ML-first, or rewrite the docs to the (better) heuristic-first reality. Three docs vs. the code — pick one. | Parser roadmap, C2 | C2 |
| **D2** | **SOE computation: greedy sequential transform or search over the objective?** Determines determinism, explainability, testability, and whether §6's score is the algorithm or just a diagnostic. | The entire SOE | N1, N8 |
| **D3** | **Pipeline unification:** does the Planner Engine pipeline replace `PipelineOrchestrator`, or nest inside `BalanceWorkload`? Where do `AssessRisk` and `Adapt` live afterward? | SOE integration | A1, A2, N2 |
| **D4** | **Risk placement:** pre-optimization (constraint/urgency input) or post-optimization (evaluates final schedule)? | Removing A3's backward call | A3, N3 |
| **D5** | **Weight governance:** who owns/tunes `w1…w6`; do they get `WeightConfig`-style guardrails; does `WeightOptimizer` extend to them? | Adaptive loop | B5, N6 |
| **D6** | **Phasing:** build all six optimizers now, or Load Balancer + Constraint Evaluator first behind a stubbed `IScheduleOptimizer`? | Scope / overengineering | N9, N10 |
| **D7** | **Domain-model growth:** which new entities (session segments, subject continuity, competency signal), and does the B3 repo migration finish first? | Fatigue/Context/Session/Fragmentation | N5, N7, B3, A6 |
| **D8** | **Constraint checkpoints:** intentional pre-filter + post-validate, or collapse to one? | Constraint layer design | N11 |

---

## 4. Updated sequencing

The prior review's sequencing still holds for the debt items; the proposal inserts gating decisions
ahead of the SOE build.

1. **Resolve D1–D4 on paper first.** They determine the *shape* of the SOE and the pipeline; writing
   code before them guarantees rework.
2. **B4 (explicit DI) before SOE code.** The proposal multiplies injectable components; wiring six
   optimizers through the static `ServiceLocator` would multiply the anti-pattern.
3. **Fold A1 + A2 + A3 into the pipeline-unification change (D3/D4)** — decide the single orchestration
   seam, give stages/optimizers explicit read/write contracts, and place Risk so its backward call
   disappears. One coherent change, not three.
4. **Define `IScheduleOptimizer` (N10) + Constraint Evaluator; relocate the existing Load Balancer
   behind it; stub the other four (D6).** This delivers the proposal's structural value immediately at
   low risk.
5. **A6 (durable focus logging) before both M8-C and the Fatigue Evaluator** — both stand on session
   history.
6. **Finish B3 (repo migration) + grow the data model (N7) before Fatigue/Context/Session/Fragmentation
   heuristics** get real implementations.
7. **B1 facade-collapse, B2 confidence unification, A4/A5, B5/N6 weight consolidation, C1 structure
   reconciliation, appendix hygiene** — incremental, after the shape is fixed.

---

## 5. Preserve list (unchanged intent, now including the good part of the proposal)

- **Strategy seams** — `IUrgencyRule`, `IPriorityComponent`, keyword parsers, risk evaluators. Still
  the best pattern in the codebase.
- **Offline-first deterministic fallback everywhere.** The proposal's ML-advisory posture must inherit
  this, not weaken it.
- **The SOE-as-extension-point idea itself (N10).** This is the proposal's real contribution — a stable
  Planner Engine hosting composable optimization strategies. Keep it; just don't over-fragment it (N9)
  and decide its computational model first (N1/D2).
- **Deterministic priority scoring, ML-free** — now explicitly endorsed by the proposal's "Decision
  Engine must remain deterministic." Hold that line even as the engine gains "progress"/"competency"
  scope (N5).

---

### Bottom line

The SOE proposal is a **sound long-term direction** that correctly reframes workload balancing as one
heuristic among several and gives the system a real extension point. It **resolves no existing debt**
(it's a design), **reinforces most of it** by adding surface area, and **introduces two must-decide
forks** — the parser's ML-vs-heuristic identity (D1) and the SOE's transform-vs-search model (D2) —
that should be settled on paper before a single class is written. Adopt the direction; gate the build
on D1–D4; phase the six optimizers; and let the existing strategy-pattern discipline, not six bespoke
engines, carry the extensibility.
