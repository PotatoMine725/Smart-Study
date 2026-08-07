# Epic 3 (Study Optimization Engine) — Closing Note

**Date:** 2026-08-07 · **Satisfies:** master plan DoD-7 ("success metrics measured and reported in
the epic's closing note") · **Author:** orchestrator, independently re-derived — no number below is
taken from a subagent's self-report; every one was reproduced by running the cited test on this
session's HEAD and reading its own output, or by reading the cited artifact file directly.

**Reads with:** [master plan](../plans/2026-07-03-master-plan.md) §"Epic 3" (acceptance criteria,
success metrics, DoD); [execution plan](../plans/2026-08-04-epic-3-execution-plan.md) (cards A–H);
[G2 note](../plans/2026-08-04-g2-optimization-pass-semantics.md) (D7 — owner ruling on the two
findings this note reports); [G3 note](../plans/2026-08-07-g3-weight-vector-governance.md) (ratified
2026-08-07, establishes `Optimize()` has zero production callers as of this HEAD — the scoping fact
that governs how every metric below must be read).

**Format precedent:** [Epic 1 closure verdict](../review/2026-07-11-epic1-closure-verdict.md), whose
own F1 finding is the reason this note exists as a *separate* document rather than folded into a
milestone review.

---

## Evidence-scoping statement — read this before the tables below

`ScheduleOptimizer`/`SoeWeights` have **zero production call sites** as of this HEAD (established
independently during G3's ratification, `2026-08-07-g3-weight-vector-governance.md` §G3-1):
`BalanceWorkloadStage.cs:41` still calls `IWorkloadService.GenerateSchedule` directly, the pre-Epic-3
path. Every metric below was measured by the T3.4 test harness running `Optimize()` directly against
the T3.6 corpus (230 generated schedules) — **not** by observing real usage, because no real usage of
the `Optimize` seam exists yet. This is not a defect in the metrics; it is the correct reading of
what "measured" means for a seam that ships behind a strategy interface without a wired caller
(G3-1's own finding: wiring is separate, unscheduled integration work, not part of Epic 3's task
cards).

---

## Epic acceptance criteria (master plan) — state

| # | Criterion | State | Evidence |
|---|---|---|---|
| 1 | Inversion test: a near-deadline task is never displaced past its deadline by a **quality-improving rearrangement** (D-G) — scoped to the `Optimize` pass loop, not the initial allocator placement | ✅ | `DH_Optimize_ViolationsNeverWorsen_AcrossFullCorpus` — this is what D-H guarantees for the seam; 0 breaches (see metric #1 below). The separate, allocator-level residual-inversion finding (metric #1, second half) is a different scope — the *initial placement*, not a quality rearrangement — and does not bear on this criterion. |
| 2 | Property test: `violations(out) ≤ violations(in)` (count, then overdue minutes) on every input, including infeasible ones (D-H) | ✅ | `DH_Optimize_ViolationsNeverWorsen_AcrossFullCorpus`, run independently this session: **0 breaches across all 230 corpus items** (both feasible and infeasible designed subsets — the test does not filter). |
| 3 | No objective score can purchase a constraint violation (D-J); validator and evaluator tested independently | ✅ | Structural: `ScheduleOptimizer`'s admissibility gate (`CompareFeasibility`) reads only `ConstraintValidator`/`Eval` fields, never `SoeWeights`/`ObjectiveEvaluator` output (confirmed while tracing G3-2's all-zero-weight analysis — a zero objective can only ever affect the *quality* tie-break, never feasibility admission). `IConstraintValidator` (Card D) and `IObjectiveEvaluator` (Card E) have independent per-seam test suites, per D-J. |
| 4 | Same input ⇒ same output; every rejection carries a machine-readable reason | ✅ | Determinism + Explainability, both below (metrics #2, #3) — 0 mismatches across 3 runs, 0 missing/spurious reason codes across 558 checkpoints. |

---

## Success metrics (master plan) — measured state

The master plan bundles two distinct facts into metric #1's wording ("**0** D-H invariant breaches;
**0** deadline inversions"). They are reported separately below because Decision D5 (G2 note) requires
exactly this split: D-H is a claim about the `Optimize` seam; deadline inversions are a claim about
the allocator's initial placement. Conflating them would misreport which one is actually met.

| Metric (master plan wording) | State | Measured value | Source |
|---|---|---|---|
| **#1a** — 0 D-H invariant breaches | ✅ **Met** | 0 breaches / 230 items | `DH_Optimize_ViolationsNeverWorsen_AcrossFullCorpus`, run this session |
| **#1b** — 0 deadline inversions (baseline > 0; delta is the headline metric) | ⚠️ **Not met as worded; real, partial improvement** | **Self-miss:** 17 → 0 (**100% eliminated**, by construction — earliest-feasible chronological placement makes this class structurally impossible, per the test's own pinned comment). **Pairwise:** 233 → 220 (**5.6% reduction**). **Total:** 250 → 220 (**12% reduction**). Feasible-designed subset alone: 85 residual (all pairwise). | `Inversion_Allocator_TotalAcrossCorpus_ComparedAgainstFrozenBaseline`, run this session against `2026-08-05-soe-t36-baseline.json` (baseline: Self=17, Pairwise=233, Total=250) |
| **#2** — Determinism: byte-identical outputs across 3 repeated full-corpus runs | ✅ **Met** | 230 items × 3 fresh runs, **0 mismatches** (schedule sequence + report termination + pass count, all three pairwise-compared) | `Optimize_CorpusRun_IsDeterministic_AcrossThreeRepeatedRuns`, run this session |
| **#3** — Explainability: 100% of rejected candidates carry a reason code | ✅ **Met** (mapped onto checkpoints, not "candidates" — see note) | 279 C0 checkpoints (reason correctly null) + 279 non-C0 checkpoints (reason correctly non-null) = 558 total, **0 missing, 0 spurious** | `Explainability_AllNonC0CheckpointsCarryReasonCode_AcrossFullCorpus`, run this session. **Terminology note:** the master plan says "rejected candidates"; the report contract's unit is a *checkpoint* in the pass trajectory (a candidate schedule state). A non-C0 checkpoint is the operational meaning of "rejected candidate" in this seam's vocabulary — the mapping is direct, not approximate, but is stated here because the wording differs. |
| **#4** — Objective delta vs. current-allocator baseline, reported per corpus schedule | ❌ **Not evaluable for 90% of the corpus** | `Score(B)` (the pre-T3.3 baseline's objective score) is **not computable** for 207/230 items (90%): the frozen artifact `2026-08-05-soe-t36-baseline.json` predates `IObjectiveEvaluator` (T3.2) and stores only aggregated proxies, not the `ScheduledItem` list `Evaluate` requires. G2-5's arm-2 quality rule (`Score(S) ≥ Score(B) − ε`) is therefore **unevaluated**, not passed, for those 207 items. `Score(S)` (post-T3.3) *is* computable and is reported per item in the artifact below. | `2026-08-07-soe-t34-corpus-report.json`, `Aggregate.ScoreBComputable: false`, `ScoreBNote` field (independently re-read this session: `Arm1Count=19, Arm2Count=207, Arm3Count=4`) |
| **#5** — Runtime: full SOE run < 2 s p95 on the reference semester fixture (provisional) | ✅ **Met, with a substitution noted** | mean = 0.14 ms, **p95 = 0.146 ms**, max = 19.0 ms, against a 2000 ms budget — **~3 orders of magnitude of headroom** | `Runtime_OptimizeCall_LatencyAcrossCorpus_GatedAtProvisionalP95Budget`, run this session. **No "reference semester fixture" exists anywhere in the repo** (confirmed by search) — the metric was measured on the 230-item T3.6 corpus instead, the closest available substitute. Given the ~13,700× margin against budget, the substitution is very unlikely to change the verdict, but the fixture named in the master plan's wording does not exist. |

**Reading #1b and G2-5 arm 3 together (both owner-ruled, not re-litigated here):** both trace to the
same root cause — **A1**, `WorkloadServiceImpl.GenerateScheduleWithIdentity` using priority as the
sole task-ordering key, with deadline only selecting which day within a window a task's own chunk
lands on. Card G's 4 hard-fail G2-5 arm-3 items (`MIX-016/019/024/047`) are the same mechanism as
#1b's residual pairwise inversions, one architectural cause, two symptoms. **The owner ratified both
as disclosed, characterization-tested allocator limitations, not defects, on 2026-08-07** — see D7 in
the G2 note. This closing note does not reopen that ruling; it reports the numbers behind it in full.

---

## Definition of Done (master plan, 7 items — every epic)

| # | Item | State |
|---|---|---|
| 1 | `gitnexus_impact` before editing any symbol; HIGH/CRITICAL surfaced | ✅ Applied per-card through Cards A–H |
| 2 | `gitnexus_detect_changes`/reindex before commits | ✅ Reindexed after each of Card G's and Card H's commits this session |
| 3 | `dotnet build` + `dotnet test` green | ✅ Independently re-verified this session: build 0 errors; full suite 470 passed / 0 failed / 1 skipped / 471 total |
| 4 | New behavior covered by the epic's acceptance-criteria tests | ✅ `SoeT34InvariantTests.cs` (7 methods) + per-seam suites from Cards D/E/F |
| 5 | Architecture docs updated after code lands; roadmap A.3 status updated | ❌ **Stale.** `docs/specs/system_roadmap.md` (last touched 2026-08-04, before G2 was even ratified) still reads "Pass accept/commit semantics still OPEN — implementation blocked on it" and "SOE implementation is blocked on that decision." Both G2 and G3 are now ratified and Cards A–H are merged; the roadmap does not reflect any of it. **Open — see Findings.** |
| 6 | Open decisions closed in a `docs/plans/YYYY-MM-DD-*.md` note before dependent tasks start | ✅ G2 (`2026-08-04-g2-optimization-pass-semantics.md`, ratified CP-1) and G3 (`2026-08-07-g3-weight-vector-governance.md`, ratified CP-4) both merged and ratified |
| 7 | Success metrics measured and reported in the epic's closing note | ✅ This document |

---

## Findings

### F1 — MEDIUM (process/docs) — DoD-5 unmet: roadmap stale at the epic boundary

`docs/specs/system_roadmap.md` describes G2 as still open and SOE implementation as blocked on it.
Both are now false: G2 ratified 2026-08-05, G3 ratified 2026-08-07, and the full T3.0–T3.9 task set is
merged. This mirrors Epic 1's own F3 finding (same DoD item, same failure mode: docs updated
per-milestone but not swept at the epic boundary). **Not a code defect and not a ship blocker** — the
same judgment Epic 1's verdict applied to its F3 — but owed before Epic 3 is considered fully closed
out administratively.

### F2 — INFORMATIONAL — metric #1 is two facts, not one, and only one is fully met

Documented above (success metrics table, #1a/#1b) and already owner-ruled (D7). Restated here only to
make the DoD-7 report explicit that "0 D-H invariant breaches; 0 deadline inversions" as a single
master-plan bullet is **half met, half a disclosed 12%-improvement-not-elimination**, not a clean pass.

### F3 — INFORMATIONAL — metric #4 (objective delta) is structurally unevaluable for 90% of the corpus

Not a defect in Card G's work — the frozen baseline artifact was captured before `IObjectiveEvaluator`
existed, by design (Card A predates Card E). Re-deriving `Score(B)` would require resurrecting the
retired pre-T3.3 allocator, which the frozen-baseline discipline (PD-3/R8) forbids. Recorded here so a
future reader does not mistake metric #4's silence for a passed check.

---

## Decisions made (ADR-style)

### D1 — Report metric #1 as two separately-scoped facts, not one pass/fail line

- **Why:** the master plan's own wording bundles a claim about the `Optimize` seam (D-H, met) with a
  claim about the allocator's placement (deadline inversions, not eliminated) into one bullet.
  Reporting a single "partially met" verdict would hide which half is actually clean.
- **What for:** whoever reads this closing note to decide whether Epic 3 is ship-ready gets the
  precise fact — the seam behaves exactly as designed; the allocator's known limitation (A1) is
  unchanged from what the owner already ruled acceptable on 2026-08-07 (D7) — rather than a blended
  number that reads better than the situation.
- **Experience:** this is the same discipline Card G's own Decision D5 already established at the test
  level (two separate tests, not one). Bringing that split forward into the closing note keeps the
  report honest about what it's aggregating.

### D2 — Independently re-run every test cited, rather than trust prior subagent reports for this note's numbers

- **Why:** this session's standing discipline treats a subagent's self-report as unverified until
  independently checked. The closing note is the document an owner reads to judge ship-readiness; a
  synthesis error here (an invented-but-plausible number) would be far more consequential than in an
  intermediate card report.
- **What for:** every number in the success-metrics table above was produced by this session running
  the cited `dotnet test --filter` command directly and reading the console output — not copied from
  an agent's summary.
- **Experience:** the advisor's review before this note was drafted flagged exactly this risk
  ("a synthesis task is where invented-but-plausible numbers show up") and named the specific check
  that mattered most — whether the baseline-to-current inversion delta was genuinely zero (which would
  have contradicted the owner's "ship as known limitation" ruling's premise). It was not zero (250→220,
  and the self/pairwise split showed the self-miss class is genuinely eliminated) — but it was close
  enough on the pairwise component (233→220) that reporting only the bundled total would have
  overstated the improvement.
