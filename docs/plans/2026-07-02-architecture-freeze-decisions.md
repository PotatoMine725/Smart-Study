# Architecture Freeze Decisions — 2026-07-02

> **Status:** Accepted (**D-G … D-J**); one item explicitly **OPEN** (§3).
> **Scope:** Documentation only. **No code changes in this pass.**
> **Continues:** [`2026-07-01-architecture-direction-decisions.md`](2026-07-01-architecture-direction-decisions.md)
> (D-A…D-F). Evidence & reasoning: [`../architecture/lessons-learned.md`](../architecture/lessons-learned.md)
> (L1–L9) and the 2026-07-02 critical review of [`2026-07-01-forks-proposals.md`](2026-07-01-forks-proposals.md)
> Decisions 1–4, run against the agenda [`2026-07-02-next-session-agenda.md`](2026-07-02-next-session-agenda.md).
> All code claims verified at commit `5e54220` (no source changes in the working tree).

---

## 0. Purpose

Record the outcome of the deferred critical review that gated the architecture freeze.
The review challenged the four "locked in principle" decisions from
`2026-07-01-forks-proposals.md` against the shipped source. Result: **four decisions
frozen** (user acceptance, 2026-07-02) and **one deliberately reopened** (§3).

Mapping from the acceptance message: Accepted #1 = **D-G**, #2 = **D-H**, #3 = **D-I**,
#4 = **D-J**. This file is the **single normative home** for these decisions; every other
document references it.

---

## 1. Decisions (ADR-style)

### D-G — Deadline re-enters the SOE as a hard constraint; the objective scores quality only

**Decision.** `DeadlineUrgency` belongs **exclusively** to the Decision Engine's priority
calculation (`PriorityScore`). It does **not** appear in the Study Optimization Engine's
objective. Instead, **deadline feasibility re-enters the SOE as a hard constraint** owned by
the Constraint Validator — alongside capacity limits, calendar constraints, and future hard
constraints. The SOE optimizes only among feasible schedules (feasibility read per D-H).
The objective is:

> `Score = w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty + w5·FragmentationPenalty`

The proposal's `w6·DeadlineUrgency` term is **dropped**.

**Rationale.** A priority score is a lossy scalar: it buys placement *order*, not placement
*legality* — once the deadline collapses into a number, downstream code cannot recover the
feasibility boundary. Verified: deadline reaches scheduling only through the priority scalar
(`Services/Strategies/PriorityCalculator.cs:35`) and placement never reads `HanChot`
(`Services/WorkloadServiceImpl.cs:77-91`) — a task due in 3 days can be placed on day 9
today. See lessons **L3** and **L4**.

**Resolves.** Agenda edge **2A** (placement-in-time confirmed → must-fix); reconciliation
**N4** (the `w6` double-count is gone by construction). The SOE weight vector everywhere is
now **`w1…w5`** (governance of the vector, B5, remains open — §4).

**Consequences.** The Constraint Validator and the objective are separate seams (D-J). The
current allocator's deadline-blind, least-loaded placement is a known violation source to be
fixed during SOE work. Tests must include the inversion scenario: a near-deadline task must
never be displaced past its deadline by a quality-improving rearrangement.

### D-H — Feasibility invariant: the SOE preserves or improves feasibility

**Decision.** Architectural **invariant**: for every input schedule,

> `violations(output) ≤ violations(input)` — compared first by hard-constraint violation
> count, then by total overdue minutes.

When the input is feasible this reduces to strict feasibility. Detecting and *reporting*
infeasibility remains the **Risk Analyzer's** responsibility; the optimizer never silently
absorbs it.

**Rationale.** Inputs can already be infeasible (workload > capacity × days-to-deadline),
and the current allocator produces such schedules via its unbounded overflow path
(`Services/WorkloadServiceImpl.cs:81-91`). An absolute "never violate" rule leaves the engine
undefined or inert exactly when the user is overloaded. See lesson **L5**.

**Resolves.** The infeasible-input edge surfaced in the 2026-07-02 review; refines the
proposal §5.6 wording ("constraints must never be violated") to a total, testable form.

**Consequences.** The SOE is defined on every input. The invariant is property-testable;
fixtures must include infeasible semesters, not only happy paths.

### D-I — Sync metadata & merge mechanics (no HLC)

**Decision.** Every synced entity carries a sync-metadata block:

| Field | Role |
|---|---|
| `Rev` (monotonic per-entity counter, local to each device) | change enumeration since a sync watermark + same-device ordering — **never compared across devices** |
| `ModifiedAtUtc` | LWW tie-break, first key |
| `ModifiedByDeviceId` | LWW tie-break, second key (lexicographic) |
| `IsDeleted` + `DeletedAtUtc` | tombstone (soft delete) |

Merge strategy: **3-way merge against the last-synced base snapshot per peer** (the
preferred strategy). A field changed on one side only → take it; changed on both sides →
concurrent same-field edit (concurrency detection falls out of the diff) → **LWW** with the
tie-break above. **Delete-vs-edit: the tombstone wins.** The losing side of *every* conflict
(same-field LWW and delete-vs-edit alike) is preserved in a **conflict record**; append-only
edit history is **out of v1 scope** — the conflict record is what delivers "no irreversible
loss." **No Hybrid Logical Clock** unless a concrete failure demands one; clock skew can only
bias which value is *current*, never destroy data.

**Rationale.** Cross-device revision counters carry no ordering information (no shared
origin — lesson **L6** records the full argument, as requested); merge granularity is bounded
by tracking granularity, so field-level merge needs field-level change knowledge (**L7**);
the delete/edit trilemma is structural and the conflict record is the pick-two resolution
(**L9**).

**Resolves.** Both D-F open sub-decisions (LWW clock; delete-vs-edit), agenda edge **2B**,
and the Decision-4 trilemma.

**Code-reality note.** `StudyLog`'s M7 fields are a schema-only precedent: `DeviceId` is
never populated by the production write path (`ViewModels/FocusViewModel.cs:138-145`), and
that write is fire-and-forget (finding **A6**). Populating and backfilling metadata is part
of the sync epic; A6 is a hard prerequisite to two-way sync.

**Still open (§4).** Tombstone retention length & purge authority (retention ≥ max offline
window vs. seen-by-all-devices ack) and the cascade policy for soft-deleting a parent
(`MonHoc`) with live children.

### D-J — Constraint Validation and Objective Evaluation are independent stages

**Decision.** Constraint Validation is a **hard validation stage**: a predicate
(feasible / not feasible, under D-H's relative reading) applied as a filter. Objective
Evaluation is an **independent stage** that measures optimization quality only and ranks only
candidates the validator admits. **No score can purchase a violation.** Architecturally these
are two seams (indicatively `IConstraintValidator` / `IObjectiveEvaluator`; names are not
frozen API).

**Rationale.** A weighted penalty is negotiable by construction — it trades against every
other term, so a "must" expressed as a weight is a "may" with a price. See lesson **L4**.

**Resolves.** The agenda §3 question "is Constraint Validation a hard filter or soft?" —
hard filter, unconditionally.

**Consequences.** Independent tests per seam; explanations distinguish
"rejected: infeasible" from "rejected: lower score"; every future "important factor" must be
classified as boundary (constraint) or preference (score term) before it enters the SOE.

---

## 3. OPEN — optimization-pass accept/commit semantics (explicitly not decided)

> **Update 2026-08-04, ratified 2026-08-05 (CP-1):** closed as gate **G2** by
> [`2026-08-04-g2-optimization-pass-semantics.md`](2026-08-04-g2-optimization-pass-semantics.md)
> (**G2-1 … G2-6**) — *run-all, commit-best-prefix*, a deterministic fixed-point pass loop, and a
> strict objective non-worsening threshold. **Ratified as written**; the section body below is
> preserved unchanged for the historical record and no longer governs new implementation — the G2
> note does. The "frozen regardless" list came through untouched (see the note's §4).

By **explicit decision (2026-07-02)** this remains open; do not implement, do not treat any
candidate as chosen.

The agreed critique (both sides accepted): **per-step greedy rollback** causes local-optimum
walls; **whole-pass accept/reject** introduces the all-or-nothing veto and the determinism
paradox (a deterministic rejected pass re-runs identically, so iteration is a no-op and a
single-pass reject means the engine did nothing). See lesson **L8**.

This **supersedes in part** two earlier texts, which described the two ends of the spectrum
as settled at different times:

- **D-E**'s phrasing "objective evaluation after each step" — the *evaluation* points stand;
  the per-step accept/reject-vs-guardrail sub-decision is **reopened**, not resolved.
- **Forks-proposals Decision 1**'s "evaluate the whole pass, then accept/reject" — a
  candidate, no longer "locked in principle."

**Frozen regardless of the outcome:** deterministic ordered pipeline, never a global search
(D-E core); hard constraints and the D-H invariant (D-G/D-H/D-J); objective `w1…w5` (D-G);
the stable seam `Optimize(schedule) → (schedule, report)`.

**SOE implementation is blocked on this decision.**

---

## 4. Other open items (unchanged by this record)

- **B5 — weight-vector governance**, now reading `w1…w5`: ownership, guardrails, relation to
  `WeightOptimizer`.
- **Tombstone retention & purge authority; soft-delete cascade policy** (from D-I).
- **LAN transport / discovery / trigger model**, and the **max tolerable offline window**
  (feeds D-I retention).
- **Difficulty ML model** timing; **M9** natural-language deadline parsing.

---

## 5. Documents updated in this pass

- `docs/architecture/data-model.md` — §2 `StudyLog` runtime note; §8 change-tracking /
  conflict-policy items rewritten to D-I.
- `docs/specs/system_roadmap.md` — A.3 items 1–3; Part B §7.3 callout (D-G/D-H/D-J frozen,
  pass semantics OPEN).
- `docs/plans/2026-07-01-architecture-direction-decisions.md` — amendment banner; D-E/§5
  update markers (non-destructive).
- `docs/plans/2026-06-30-workload-optimizer-proposal.md` — supersession banner (w6 dropped;
  §5.6 refined by D-H; "search for highest score" is not the compute model).
- `docs/review/2026-07-01-architecture-reconciliation.md` — banner extended (N4 resolved;
  weight vector `w1…w5`; D-E amended).
- `docs/plans/2026-07-02-next-session-agenda.md` — outcome banner.
- `docs/architecture/lessons-learned.md` — decision references mapped to D-G…D-J.
- `docs/README.md` — index entries.

No source code was modified.
