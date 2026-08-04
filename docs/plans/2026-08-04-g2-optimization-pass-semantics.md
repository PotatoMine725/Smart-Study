# GATE G2 — SOE optimization-pass accept/commit semantics

**Date:** 2026-08-04 · **Gate:** G2 (master plan) · **Closes:** architecture-freeze record §3, lesson **L8** ·
**Status:** **Resolution proposed — pending owner ratification at CP-1.** Decisions **G2-1 … G2-6** below are
written to be implemented as-is; they are not yet ratified. Nothing here changes code.

**Reads with:**
[architecture freeze 2026-07-02](2026-07-02-architecture-freeze-decisions.md) §3 (the OPEN item this closes)
and **D-G / D-H / D-J**;
[architecture direction 2026-07-01](2026-07-01-architecture-direction-decisions.md) **D-E**;
[lessons-learned](../architecture/lessons-learned.md) **L3 / L4 / L5 / L8**;
[master plan](2026-07-03-master-plan.md) Epic 3 (gate table, M3.2, success metric #8);
[M3.0 baseline-vs-invariant classification](2026-08-04-m3.0-allocator-baseline-vs-invariant.md).

---

## 0. What was open, and what this note has to produce

The freeze of 2026-07-02 settled everything about the Study Optimization Engine except one question:
**when the pipeline has run, what does the engine keep?** Both ends of the answer were tried and both
were rejected, for reasons that are agreed and not reopened here (L8):

- **Per-step accept/reject** (roll back any optimizer that lowers the score) walls off recoverable
  valleys: a stage that dips so a later stage can climb higher never gets the chance.
- **Whole-pass accept/reject** (evaluate once at the end, keep all or nothing) introduces two new
  defects — the **all-or-nothing veto** (four stages improve, the fifth regresses, all five are
  discarded) and the **determinism paradox** (a deterministic rejected pass re-runs identically, so
  "iterate to convergence" is a no-op, and a single-pass reject means the engine did nothing).

L8's partial principle names the way out: *where you measure* and *where you commit* are independent
axes. Both extremes conflated them, and both therefore share one defect class — **discarding
recoverable work**.

This note must deliver four things, and the rest of the document is those four things plus their
consequences:

1. a commitment rule that does not discard recoverable work (the all-or-nothing veto);
2. either a precise statement of *what varies between iterations*, or an explicit adoption of
   single-shot semantics with its consequences accepted (the determinism paradox);
3. the **objective non-worsening threshold**, concrete enough that T3.3 writes one comparison and
   stops thinking about it — master plan v2 deliberately deferred this here;
4. confirmation that **D-E core, D-G, D-H, D-J, `w1…w5` and the `Optimize` seam** come out untouched.

---

## 1. Decisions

### G2-1 — Commitment unit: the best admissible checkpoint of the pass trajectory ("run-all, commit-best-prefix")

**Decision.** A pass runs **all** stages of the normative ordered pipeline **unconditionally**, in
order, with **no rollback during the run**. After each stage the engine records a **checkpoint** —
the schedule state at that point — and evaluates it (constraint validator, then objective evaluator,
per D-J). When the pass finishes, the engine **selects and commits exactly one checkpoint**: the best
admissible one, per the comparison in **G2-2**.

Write `N` for the number of stages and `C₀ … C_N` for the checkpoints, where `C₀` is the state that
entered the pass (zero stages applied) and `C_k` is the state after stages `1…k`. The committed
result of the pass is `C_k*` for the single selected `k*`. `k* = 0` is a legal, reportable outcome and
means *the pass committed nothing*.

This is the **best-feasible-prefix over per-stage checkpoints** candidate named in L8, adopted and
sharpened: admissibility is defined against D-H (G2-2), the comparison is lexicographic and
epsilon-guarded (G2-2, G2-3), ties are broken deterministically (G2-2), and the outer loop is a
stated fixed-point iteration (G2-4).

**How this resolves the all-or-nothing veto.** Take L8's own counterexample: stages 1–4 improve the
schedule, stage 5 regresses it enough to drag the aggregate below the entry state. The pass records
`C₀ … C₅`. `C₄` is admissible and best; `k* = 4`. The four gains are kept and stage 5's regression is
discarded, with reason code `Superseded_ByLaterCheckpoint` on nothing and `Rejected_LowerScore` on
`C₅`. Whole-pass semantics would have thrown away all four. **The veto is structurally impossible:
the engine never discards a gain it has already measured.**

**How this avoids re-importing the per-step defect.** Take the mirror case: stage 3 dips, stage 4
recovers past stage 2. Because no stage is rolled back *during* the run, `C₄` exists and is
selectable. Per-step rollback would have reverted stage 3 and stage 4 would have run on the wrong
state. **Both ends of L8's commitment axis are fixed by the same mechanism**, which is what makes it
the right resolution rather than a third point on the same spectrum.

**Why this is not a global search (D-E core).** The bright line, stated as a falsifiable assertion
T3.3 must encode as a test:

> The number of schedule states materialized per `Optimize` call is exactly `passes × N + 1`.
> The branching factor is **1**: every candidate is a state the normative pipeline produced anyway.

Selection ranges over the pipeline's **own trajectory**, not over the schedule space. Nothing is
generated in order to be evaluated; everything evaluated was going to be computed regardless. D-E
core prohibits searching the space of schedules — an argmax over six already-computed states on a
single deterministic path explores nothing. This is the one mechanical claim in the note the owner
should sanity-check against their reading of D-E core (§6).

---

### G2-2 — Admissibility and the accept comparison (one comparator, D-H first, quality second)

**Decision.** Two predicates, both defined on the evaluation triple
`Eval = (int ViolationCount, long OverdueMinutes, double Score)`, where `OverdueMinutes` is an
**integer** minute count so the D-H comparison is exact and never epsilon-dependent.

```csharp
// D-H's ordering: violation count first, then total overdue minutes.
static int CompareFeasibility(in Eval x, in Eval y)
    => x.ViolationCount != y.ViolationCount
        ? x.ViolationCount.CompareTo(y.ViolationCount)
        : x.OverdueMinutes.CompareTo(y.OverdueMinutes);

// D-H + D-J: a checkpoint that is less feasible than the state that entered the pass
// can NEVER be selected, whatever it scores. No score can purchase a violation.
static bool IsAdmissible(in Eval c, in Eval passEntry)
    => CompareFeasibility(c, passEntry) <= 0;

// The single accept comparison. Lexicographic: feasibility, then quality.
// See G2-3 for the epsilon.
const double RelEps = 1e-9;

static bool IsBetterThan(in Eval c, in Eval incumbent)
{
    int f = CompareFeasibility(c, incumbent);
    if (f != 0) return f < 0;
    return c.Score > incumbent.Score + RelEps * Math.Max(1.0, Math.Abs(incumbent.Score));
}
```

Selection within a pass:

```csharp
int best = 0;                                        // C0 == the pass entry state
for (int k = 1; k <= N; k++)
{
    if (!IsAdmissible(eval[k], eval[0])) continue;   // inadmissible: never selectable
    if (IsBetterThan(eval[k], eval[best])) best = k; // strict => ties keep the LOWER k
}
```

**Tie-break: the lowest `k` wins.** Because `IsBetterThan` is strict, equal-scoring checkpoints
resolve to the earliest one automatically. This is deliberate on two counts: it is deterministic
(required for byte-identical output), and among schedules of equal measured quality it prefers the one
that disturbs the user's existing schedule least.

**An inadmissible checkpoint stays in the trajectory.** It cannot be *selected*, but the run does not
stop there — later stages continue from it, and a later checkpoint may be both admissible and best.
This is the same "don't roll back mid-run" property as G2-1, applied to feasibility rather than
quality, and it is why a stage that temporarily worsens feasibility to enable a bigger repair is not
punished for it. The committed output is always admissible, so nothing leaks.

**Consequence — D-H becomes structural for the `Optimize` seam.** `C₀` is always a candidate, it is
by definition the pass entry state, and `IsAdmissible(C₀, C₀)` is trivially true. Therefore the
committed state of any pass is feasibility-`≤` its entry state. Across the outer loop (G2-4), pass
*n*'s entry state is pass *n−1*'s committed state, so by transitivity of `CompareFeasibility`:

> `violations(Optimize(s)) ≤ violations(s)` — **by construction, not by test.**

**Scope of that claim — read this before quoting it.** It holds for the `Optimize(schedule) →
(schedule, report)` seam, whose input is a schedule. It does **not** extend to the allocator. Verified
in code: `IWorkloadService.GenerateSchedule(HocKy hocKy, double capacityHours) → List<ScheduleDay>`
(`SmartStudyPlanner/Services/IWorkloadService.cs:19`) maps a *semester* to a schedule — there is no
input schedule to compare against, so D-H's relative invariant is not even well-formed there. **The
allocator's deadline-awareness is T3.3's separate obligation**, covered by the master plan's inversion
test and by G2-5 arm 3, and G2 does not discharge it. T3.4's property test should assert the invariant
on `Optimize` and assert the allocator's behaviour separately; a single end-to-end assertion would
silently attribute the allocator's correctness to this decision.

---

### G2-3 — The objective non-worsening threshold, inside the optimizer: strict, with a numerical-noise guard only

**Decision.** **Zero tolerance for objective regression.** At equal feasibility, a checkpoint is
accepted only if it scores **strictly higher** than the incumbent by more than a relative epsilon:

```csharp
c.Score > incumbent.Score + 1e-9 * Math.Max(1.0, Math.Abs(incumbent.Score))
```

That expression — not a bare constant — is the rule. `1e-9` is a **floating-point noise guard, not a
policy dial**: it exists so that two states differing only by summation order do not read as an
improvement, which would let the outer loop churn without progress.

**Why relative, and why this survives G3.** D-G fixes the objective's *shape*
(`w1·LoadBalance + … + w5·FragmentationPenalty`) but not its normalization, and the weight vector's
governance is still open (gate G3). The score's magnitude range is therefore **not knowable today**.
A relative epsilon is correct under any normalization G3 lands on; an absolute one would silently
become either a hard floor or a no-op if the weights are rescaled. The `max(1.0, …)` floor keeps it
well-behaved as the score approaches zero.

**Why zero tolerance is right here, and not a timid choice.** The usual reason to tolerate a
regression is to escape a local optimum — accept a dip now so a later move can climb higher. **G2-1
already provides that, for free and without tolerance:** every stage runs regardless, so a dip is
never rolled back and a later recovery is always reachable. Selection happens after the whole
trajectory is known. With the escape route already open, accepting a *final* regression buys the
engine nothing and costs it the one guarantee that makes the output defensible to a user — that the
schedule it hands back is, by its own published measure, no worse than the one it was given.

**What this rule is not.** It is not D-H. D-H is a hard invariant on constraint violations with no
threshold at all (strict `≤`, no epsilon, exact integer comparison). This is the *quality* rule that
sits underneath it in the lexicographic order and only ever runs when feasibility is tied. Keeping
them separate is D-J at the comparator level: `IsBetterThan` cannot reach the `Score` line while the
feasibility pair differs, so **no arrangement of weights can buy a violation.**

---

### G2-4 — Iteration: a deterministic fixed-point loop; what varies is the committed state, and nothing else

**Decision.** `Optimize` runs the pass of G2-1 in a loop:

```csharp
const int MaxPasses = 4;

var state = input;
for (int pass = 1; pass <= MaxPasses; pass++)
{
    var (next, kStar, passReport) = RunPassAndSelect(state);
    report.Passes.Add(passReport);

    if (kStar == 0) { report.Termination = Terminated_FixedPoint; break; }
    state = next;
    if (pass == MaxPasses) report.Termination = Terminated_PassLimit;
}
return (state, report);
```

**This is the answer to the determinism paradox, stated as L8 demands.** What varies between
iterations is **the committed schedule state, and nothing else**. No randomness, no perturbation, no
restarts, no varying weights, no varying stage order, no clock, no iteration counter fed into any
stage. Pass *n* runs on pass *n−1*'s committed output, which — whenever `k* ≥ 1` — is a *different
input*, so pass *n* is not "the same pass re-run on identical input" and its outcome is not forced to
repeat.

The paradox as L8 states it is a property of **whole-pass reject semantics specifically**: there,
rejection means `output = input` by construction, so the next pass provably repeats and iteration is
a genuine no-op. Under G2-1 that case has an exact name and an immediate response: `k* = 0` means the
pass changed nothing, therefore the next pass would be bit-identical, therefore the loop **stops
immediately** and reports `Terminated_FixedPoint`. The engine never spends a pass it can prove is a
repeat. Determinism is fully preserved: same input ⇒ same trajectory ⇒ same committed states ⇒
byte-identical output, which is exactly the master plan's determinism metric (three identical
full-corpus runs).

**Termination.** Every committed pass strictly improves the state under `IsBetterThan`. `ViolationCount`
and `OverdueMinutes` are non-negative integers that admissibility forbids from increasing, so they can
strictly decrease only finitely often; when they are unchanged, `Score` must rise by more than the
epsilon, and `Score` is bounded above on a finite schedule. The sequence of committed states is
therefore strictly monotone: **it cannot cycle and cannot revisit a state.**

**`MaxPasses = 4` is a runtime bound, not a cycle guard** — the monotonicity argument above already
rules out cycles, and nobody should read the ceiling as evidence that oscillation is possible. It
exists to keep the worst case inside the master plan's `< 2 s p95` budget. Hitting it is **reported**
(`Terminated_PassLimit`), never silent. It is an implementation constant: changing it does not reopen
G2.

**Permitted future optimization, explicitly not a reopening.** If T3.3's corpus telemetry shows pass 2
commits `k* = 0` on essentially every input, the loop may be pinned to a single pass. That is not a
change of semantics and needs no new gate — **at the fixed point the two behaviours coincide by
definition**, and the loop is only ever detecting a fixed point it has already reached. The
justification is the coincidence, not the percentage. The reverse move — raising `MaxPasses` because
`Terminated_PassLimit` is common — is equally in-scope for T3.3 and equally not a reopening.

---

### G2-5 — The objective non-worsening threshold, at the corpus gate (master plan success metric #8)

**Decision.** Metric #8 says "objective delta vs the current-allocator baseline reported per corpus
schedule; the non-worsening threshold is finalized in the G2 decision note." That is a **different
comparison** from G2-3 — it compares the new engine's output against the *old allocator's* output on
the same corpus input, not a checkpoint against its predecessor — and it needs its own rule.

For each corpus item `I`, with `B = CurrentAllocator(I)` (the T3.6 baseline) and
`S = Optimize(NewAllocator(I))`, partition by the **feasibility** relation, then apply a quality rule
per arm. No global percentages, no invented tolerances.

| Arm | Condition | Quality rule | Disposition |
|---|---|---|---|
| **1** | `CompareFeasibility(S, B) < 0` — the SOE is strictly more feasible | **No quality floor.** | Report the delta. A negative delta here is **expected and accepted.** |
| **2** | `CompareFeasibility(S, B) == 0` — identical feasibility | `Score(S) ≥ Score(B) − 1e-9·max(1,\|Score(B)\|)` — the same epsilon-guarded strict non-worsening as G2-3 | Failures are **findings**: each is listed in the M3.2 report with a named cause and an explicit fix-or-waive decision. Not silently softened; not automatically blocking. |
| **3** | `CompareFeasibility(S, B) > 0` — the SOE is strictly less feasible | n/a | **Hard fail, no waiver.** |

**Why arm 1 has no floor.** D-G puts deadline feasibility in the constraint validator precisely so it
cannot be traded against quality, and D-J says no score can purchase a violation. The contrapositive
is the part people forget: **if quality cannot buy a violation, quality cannot veto a repair either.**
An item where the SOE fixes a deadline inversion and pays for it in fragmentation is the architecture
working, not a regression. Gating it on quality would reintroduce the negotiable-penalty defect that
L4 removed, through the back door of a metric.

**Why arm 2 is strict-with-findings rather than strict-with-blocking.** At identical feasibility the
new pipeline has no excuse a reader would accept — but "no excuse I can think of today" is not the
same as "impossible", and a deadline-aware placement can legitimately cost continuity even on an item
that ends up feasible either way. Metric #8's own wording is *reported* per corpus schedule. So: the
threshold is strict, every breach must be named and dispositioned in writing, and the owner decides at
M3.2 whether any of them blocks. What is ruled out is a breach passing unnoticed or the threshold
being quietly relaxed to make the corpus green.

**Why arm 3 exists separately.** It is already forbidden by the M3.2 acceptance criteria; it is broken
out so the corpus report has three buckets rather than folding a feasibility regression into a quality
discussion. Note this is a comparison between the **new and old allocators**, which is *not* what
D-H constrains (G2-2, scope paragraph) — different comparison, different bucket, deliberately.

**Reported per corpus schedule** (the metric's own words): the arm, the feasibility pair for `S` and
`B`, and `Score(S) − Score(B)`. Reported in aggregate: the count per arm, and the mean and minimum
delta over arm 2.

---

### G2-6 — The report contract: every checkpoint carries exactly one reason code

**Decision.** `Optimize`'s report is a per-pass, per-checkpoint record. Every checkpoint `C₁ … C_N` of
every pass carries **exactly one** code, which satisfies the master plan's explainability metric
("100% of rejected candidates carry a reason code") by construction rather than by audit.

Per checkpoint:

| Code | Meaning |
|---|---|
| `Selected` | This checkpoint became the pass's committed state (`k = k*`). |
| `Rejected_Infeasible` | Feasibility pair worse than the pass entry state. Never selectable, whatever it scores (D-H / D-J). |
| `Rejected_LowerScore` | Admissible; score strictly below the incumbent. |
| `Rejected_NoImprovement` | Admissible; score within the epsilon of the incumbent — a tie, and ties keep the lower `k`. |
| `Superseded_ByLaterCheckpoint` | Admissible and better than the entry state, but a later checkpoint was better still. |

Per pass: `k*`, and the feasibility pair and score of every checkpoint including `C₀`.
Per call: `Terminated_FixedPoint` or `Terminated_PassLimit`, and the pass count.

`Rejected_Infeasible` and `Rejected_LowerScore` are **kept distinct** — that is D-J's
"explanations distinguish *rejected: infeasible* from *rejected: lower score*" landing in a concrete
enum. The `Superseded_ByLaterCheckpoint` code matters more than it looks: it is the audit trail that
proves the all-or-nothing veto is gone, because it is exactly the record of a gain that was measured,
beaten, and *not* discarded through a veto. T3.7's `OptimizerRunLog` persists these rows.

---

## 2. The design space, and why the alternatives lose

| Mechanism | All-or-nothing veto | Determinism paradox | D-E core (no global search) | Verdict |
|---|---|---|---|---|
| **Per-step accept/reject** | avoided | unresolved — a rejected step re-runs identically | safe | **Rejected.** Local-optimum walls (L8). |
| **Whole-pass accept/reject** | **fails** — discards four gains for one regression | **fails** — reject ⇒ output = input ⇒ next pass repeats | safe | **Rejected.** The two named defects. |
| **Run-all, commit-best-prefix (G2-1)** | avoided — the best prefix is kept | resolved — the committed state varies; `k*=0` is the fixed point and stops the loop | safe — `N+1` states, branching factor 1 | **Adopted.** |
| **Skip-and-continue** (re-run later stages from an earlier checkpoint when a stage regresses) | avoided | resolved | **fails** | **Rejected**, below. |
| **Randomized restarts / annealing / perturbation between passes** | avoided | "resolved" by adding variation | **fails**, and breaks determinism | **Rejected**, below. |

**Skip-and-continue** is the tempting refinement of G2-1: if stage 2 regresses, drop it and re-run
stages 3–5 from `C₁`. It genuinely recovers value that prefix selection leaves on the table (a prefix
loses the tail's gains when an early stage regresses). It also turns `N+1` materialized states into up
to `2ᴺ` — a subset search over stage combinations, generating candidates the pipeline would not
otherwise have produced. That is a search over the schedule space wearing a pipeline costume, and it
breaches D-E core. **Rejected on the guardrail, not on cost.** The residual loss is accepted and named
here so nobody rediscovers it as a bug: *when an early stage regresses and later stages cannot recover
past the earlier prefix, the later stages' gains are lost.* It is bounded, reported (every affected
checkpoint carries `Rejected_LowerScore` or `Superseded_ByLaterCheckpoint`), and measurable on the
T3.6 corpus — which is the honest disposition for a known limitation under a hard guardrail.

**Randomized restarts and annealing** would resolve the determinism paradox by supplying the varying
quantity directly. They are excluded twice over: D-E core forbids the search, and the project's
determinism mandate (§6/§15, master plan metric: byte-identical outputs across three runs) forbids the
randomness. G2-4 supplies a varying quantity that costs neither — the committed state itself.

---

## 3. What this note does **not** decide

- **The allocator's placement strategy** (earliest-feasible vs. latest-feasible vs. least-loaded).
  That is T3.3's call, with the recommendation and the five-bucket test classification already staged
  in [`2026-08-04-m3.0-allocator-baseline-vs-invariant.md`](2026-08-04-m3.0-allocator-baseline-vs-invariant.md).
  G2 governs the pass loop *over* a schedule; it is deliberately silent on how the first schedule is built.
- **The weight vector's governance** (`w1…w5` values, ownership, relationship to `WeightConfig` and the
  WeightOptimizer). That is gate **G3**. G2-3's epsilon is relative specifically so that G3 can land
  any normalization without invalidating this note.
- **The stage list and its order.** D-E froze that the order is normative and must be specified; *what*
  the five stages are is M3.1/M3.2 work. G2 is stated for arbitrary `N` and does not assume the stages
  map one-to-one onto `w1…w5`.
- **Seam names.** `IConstraintValidator` / `IObjectiveEvaluator` / `IScheduleOptimizer` remain
  indicative, not frozen API (D-J).

---

## 4. Frozen guardrails — confirmation, not relitigation

| Frozen item | Status after G2 | Why it is untouched |
|---|---|---|
| **D-E core** — deterministic ordered pipeline, never a global search | **Preserved** | `passes × N + 1` states, branching factor 1; candidates are the pipeline's own trajectory. Encoded as a test (G2-1). |
| **D-G** — deadline is a hard constraint; objective is `w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty + w5·FragmentationPenalty`, quality only | **Preserved** | G2 adds no term, changes no term, and never reads a deadline into the score. |
| **D-H** — `violations(output) ≤ violations(input)`, count then overdue minutes | **Preserved and strengthened to structural** for the `Optimize` seam (G2-2); the allocator remains T3.3's obligation |  `C₀` is always an admissible candidate; transitivity carries it across passes. |
| **D-J** — validator and evaluator are independent seams; no score can purchase a violation | **Preserved** | The comparator cannot reach its `Score` line while the feasibility pair differs (G2-2); the reason codes keep the two rejection kinds distinct (G2-6). |
| **`w1…w5`** | **Untouched** | G2 consumes `Score` as an opaque double. |
| **`Optimize(schedule) → (schedule, report)`** | **Unchanged** | Every decision here is about what happens *inside* it. `report` gains a defined shape (G2-6); the signature does not change. |

---

## 5. Consequences for downstream work

- **T3.3** (allocator rework + pass loop) — implements G2-1/G2-2/G2-4 verbatim; the comparator and the
  loop are copyable from this note. Must add the branching-factor test (`states == passes × N + 1`).
  Must report the observed pass-count distribution, which is the input to the permitted single-pass
  pin (G2-4).
- **T3.4** (invariant + inversion tests) — asserts D-H **on the `Optimize` seam** and the inversion
  property **on the allocator**, as two tests, per the scope paragraph in G2-2. The `Optimize` test is
  a structural check that should be near-impossible to fail; if it ever fails, the comparator has been
  edited, and that is the interesting signal.
- **T3.6** (corpus + baseline) — already captures what G2-5 needs. Its report gains the three-arm
  partition and the per-item `(arm, feasibility pair, score delta)` row.
- **T3.7** (`OptimizerRunLog`) — persists the G2-6 rows: per pass `k*` and termination, per checkpoint
  the reason code and evaluation triple.
- **G3** — unaffected and unblocked; G2-3's relative epsilon is normalization-agnostic by design.
- **On ratification**, these lines stop being true and must be updated in the same commit:
  freeze record §3 ("SOE implementation is blocked on this decision"), the master plan gate table's
  G2 row, `system_roadmap.md` §A.3 item 2 ("Pass accept/commit semantics still OPEN"), and L8's
  pending-ratification marker.

---

## 6. Open sub-questions for CP-1

Two, both narrow. Neither blocks writing the implementation against this note; both are places where
the owner's ruling would change a line rather than the design.

1. **Does prefix selection read as "global search" to you?** G2-1 argues it does not — the candidate
   set is the pipeline's own trajectory, branching factor 1, nothing generated for the purpose of being
   evaluated. This is the mechanical claim the whole note rests on. If the owner reads D-E core as
   prohibiting *any* argmax, including over already-computed states, the fallback is per-stage
   sequential commitment (accept each stage against its predecessor, `IsBetterThan` unchanged) — which
   keeps the all-or-nothing veto fixed and the determinism resolution intact, but reintroduces L8's
   local-optimum wall. That is a real loss and is why it is not the recommendation.
2. **Arm 2 of G2-5: findings, or blocking?** The note says a quality regression at identical
   feasibility is a finding that must be named and dispositioned, not an automatic M3.2 blocker,
   because metric #8 is worded as a *reporting* requirement. If the owner wants it to block outright,
   change one word in the arm-2 disposition cell; nothing else in the note moves.

Not open, and deliberately so: the epsilon's form and value (G2-3), `MaxPasses` (G2-4), the tie-break
direction (G2-2), and the reason-code set (G2-6). Those are implementation constants T3.3 owns.

---

## 7. Decisions made (ADR-style)

### D1 — Resolve both L8 defects with one mechanism, rather than one fix each

- **Why:** the two defects look independent — a commitment-granularity bug and an iteration bug — and
  the obvious plan is to patch each. But L8's partial principle already says they share a root:
  conflating *where you measure* with *where you commit*. Separating those two axes once fixes the veto
  (commit at the best measured point, not at the end) and dissolves the paradox (the committed state is
  what varies) in the same stroke. Two separate patches would have left the conflation in place and
  invited a third defect from the same root.
- **What for:** T3.3 implements one comparator and one loop, not a commitment policy plus an iteration
  policy that have to be kept consistent with each other.
- **Experience:** the giveaway was that per-step and whole-pass fail on *opposite* defects while sharing
  the class "discards recoverable work". A mechanism that discards nothing recoverable is under-determined
  by neither — which is a strong hint that the axis, not the point on it, was the problem.

### D2 — Adopt the on-record candidate and sharpen it, rather than invent

- **Why:** "best-feasible-prefix selection over per-stage checkpoints" was already named in L8 as living
  in review discussion. Everything that made it un-implementable was under-specification, not a flaw:
  what "feasible" means (D-H against the pass entry state), what "best" means (the lexicographic
  comparator), what happens on ties (lowest `k`), and whether the prefix idea survives iteration (it
  does, and iteration is what dissolves the paradox). Inventing a sixth mechanism would have discarded
  the review's thinking and required the owner to re-audit a novel design.
- **What for:** the owner ratifies a candidate they have already reasoned about, with four gaps closed,
  instead of evaluating something new against L8 from scratch.
- **Experience:** the four missing pieces were all *comparison* semantics — which is the same shape as
  D-H and D-J, where the architectural content also turned out to be "what exactly is compared, in what
  order". Under-specified comparisons are where these decisions actually live.

### D3 — Zero tolerance on the quality threshold, and say why it is not timidity

- **Why:** a non-zero tolerance exists to buy escape from local optima. G2-1 hands that escape over for
  free — every stage runs, no dip is rolled back — so a tolerance would be paid for and unused. The
  alternative framing, "be generous so the engine can be clever", describes a search, which D-E core
  forbids anyway.
- **What for:** T3.3 writes one strict comparison with a noise guard and never revisits it; the user
  gets a guarantee that survives being stated out loud — the schedule handed back is, by the engine's
  own measure, no worse than the one it was given.
- **Experience:** the threshold was deferred to this note "because it depends on the accept semantics",
  and that turned out to be exactly right, but not in the expected direction. It is not that the accept
  semantics tell you *how much* slack to allow — it is that the right accept semantics make slack
  pointless. A deferral that dissolves the question is a better outcome than a deferral that answers it.

### D4 — Make the epsilon relative, and say that G3 is the reason

- **Why:** D-G froze the objective's shape but not its normalization, and G3 may change the weight
  vector's scale entirely. An absolute epsilon would silently become a hard quality floor or a dead
  no-op depending on which way G3 goes, and nobody would notice until a corpus run looked strange.
- **What for:** G2 and G3 stay genuinely independent; ratifying this note does not constrain G3, and
  closing G3 does not reopen this one.
- **Experience:** the first draft used a bare `1e-9`, which is *codeable* and therefore passes the
  "concrete rule, not a range" bar while still being wrong. Concreteness and correctness are separate
  properties, and a threshold note is precisely where they get confused.

### D5 — Bound the D-H structural claim to the `Optimize` seam, and verify it in code

- **Why:** "D-H holds by construction" is the most quotable sentence in this note and the one most
  likely to be used later as licence to skip a test. It is true for a `schedule → schedule` transform,
  where `C₀` is the input. It is not even well-formed for the allocator, and
  `IWorkloadService.GenerateSchedule(HocKy, double)` (`SmartStudyPlanner/Services/IWorkloadService.cs:19`)
  settles that empirically: allocation maps a *semester* to a schedule, so there is no input schedule to
  be no-worse-than.
- **What for:** T3.4 writes two tests instead of one, and no future reader gets to attribute the
  allocator's deadline-awareness to a decision that never covered it.
- **Experience:** L1's rule — verify code, not documents about code — paid out here. The prose in the
  freeze record, the master plan and the roadmap all leave the seam's scope ambiguous; the interface
  signature does not. One `grep` turned an argued position into a settled one.

### D6 — Name the residual loss instead of designing it away

- **Why:** prefix commitment loses the tail's gains when an early stage regresses and nothing recovers
  past the earlier prefix. The fix (skip-and-continue) is a subset search and breaches D-E core, so it
  cannot be taken. The temptation is then to not mention it.
- **What for:** whoever finds it during T3.3 finds it already written down, with the guardrail that
  forbids the fix, instead of filing it as a bug and "fixing" it into a `2ᴺ` search.
- **Experience:** this is the same failure mode the M3.0 note guards against on the test side — a red
  test getting "made green" by reflex. The document-level version is an accepted limitation that was
  never written down becoming a defect report six weeks later.
