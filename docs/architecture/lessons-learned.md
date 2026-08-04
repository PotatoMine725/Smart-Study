# Lessons Learned — Architecture Review, 2026-06-30 → 2026-07-02

> **Status:** Engineering knowledge record (postmortem). This document preserves *why* the
> 2026-07 architectural decisions exist — the assumptions we started with, the evidence that
> changed them, and the principles that came out of the collision. It is **not** a
> specification: normative decisions live in
> [`../plans/2026-07-01-architecture-direction-decisions.md`](../plans/2026-07-01-architecture-direction-decisions.md) (D-A…D-F)
> and [`../plans/2026-07-02-architecture-freeze-decisions.md`](../plans/2026-07-02-architecture-freeze-decisions.md)
> (D-G…D-J — the "Accepted #1–#4" labels below map to D-G, D-H, D-I, D-J respectively), with the
> canonical roadmap at [`../specs/system_roadmap.md`](../specs/system_roadmap.md).
> Sections marked **OPEN** describe unresolved questions and must not be read as frozen.
>
> All `file:line` references were verified against the working tree at commit `5e54220`
> (no source changes since) on 2026-07-02.
>
> **Status addendum 2026-07-07:** Epic 1 M1.1 (merge `3193adf`) has since modified
> `FocusViewModel` and `AppDbContext` — the historical `file:line` references in L2 describe
> the pre-M1.1 tree. See the dated status notes inline; the lessons themselves are unchanged.

## Context

The review that produced these lessons ran across three sessions: the Study Optimization
Engine proposal (2026-06-30), the direction decisions D-A…D-F (2026-07-01), and the
deferred critical review of Decisions 1–4 (2026-07-02). The review's mandate was
explicitly adversarial — *challenge the accepted decisions before freezing them* — and its
method was code-normative (D-C): every claim had to survive contact with the source.

Several decisions survived unchanged. What follows is what had to change, and what we
learned from changing it.

---

## L1 — Architecture reviews must verify code, not documents about code

**Original assumption.** A specification review can be run against the documentation set:
compare docs to docs, find the contradictions, resolve them.

**Why it looked correct.** The docs were recent, had already been reconciled once, and
cross-reading them is much cheaper than tracing source. Contradictions *between documents*
are real findings, so the method does produce output — which makes it feel sufficient.

**What the evidence showed.** The method failed twice in one week, in both directions:

- The 2026-07-01 spec review asserted the parser was "heuristic-first." The source says the
  opposite: the heuristic is the always-on *baseline*, and the ML prediction **overrides** it
  when present (`prediction?.Loai ?? loaiHeuristic`,
  `SmartStudyPlanner/Core/Parsing/Orchestrators/ParsingOrchestrator.cs:50-51`), gated at
  confidence ≥ 0.60 (`SmartStudyPlanner/Services/ML/IntentClassifierAdapter.cs:33-36`).
  The claim was retracted via erratum banners; the docs had been *right* and the review wrong.
- The 2026-07-02 review's central question — "does the optimizer place tasks onto concrete
  dates, or only rearrange within upstream-fixed day-buckets?" — had been framed in the agenda
  as an open conceptual fork worth a debate. One method answered it in minutes:
  `WorkloadServiceImpl.GenerateSchedule` builds `ScheduleDay { Date = today.AddDays(i) }` and
  places task chunks onto those concrete dates
  (`SmartStudyPlanner/Services/WorkloadServiceImpl.cs:59-105`). No amount of document
  cross-reading could have settled it, because no document recorded it.

A corollary already encoded as D-C: doc/code drift is the *default state*, so the discriminating
question is never "do they differ?" but "is this **lag** (decision made, doc trails) or a
**fork** (two different intents, no decision ever made)?" Only forks are findings.

**Principle.** Code is normative. Every claim that enters an architecture document carries a
`file:line` verification. When a question discriminates between architectural options, assume
the source already contains the answer and read it before scheduling a debate.

**Impact on future development.** Review workflow is fixed: impact/graph tooling plus source
reading *precedes* any doc edit; claims that cannot be verified are labeled aspirational rather
than stated as fact; erratum banners (not silent edits) correct review documents so the audit
trail survives.

---

## L2 — A schema is not a feature: runtime can invalidate architecture assumptions

**Original assumption.** `StudyLog` was "sync-ready" because M7 added `CreatedAtUtc`,
`DeviceId`, `IsDeleted` (`SmartStudyPlanner/Models/StudyLog.cs:16-19`). The D-F merge design
leaned on this: "`StudyLog` already carries both" a timestamp and a device identifier.

**Why it looked correct.** The columns exist. They are documented in `data-model.md` §2. A
device-identity provider exists (`DeviceHelper.GetId()`). Every static artifact agrees the
capability is there.

**What the evidence showed.** The claim was true of the *schema* and false of the *system*.
The only production write of a `StudyLog` never sets `DeviceId`
(`SmartStudyPlanner/ViewModels/FocusViewModel.cs:138-145`) — at runtime it is always `""`.
`DeviceHelper.GetId()` is consumed only by ML model metadata
(`Services/ML/MLModelManager.cs:124`, `Services/ML/TextClassifierModelManager.cs:171`), never
by the sync-facing fields it was presumed to feed. The same defect class had already surfaced
once: `ParseSource.MlOverridden` is a declared enum value the orchestrator never produces
(decision record §4, post-pass verification). And the write site itself is fire-and-forget
(`_ = _studyLogRepository.AddAsync(...)`, finding A6) — tolerable on one device, replica
divergence under two-way sync.

**Principle.** A capability claim is verified at the **write site**, not the type definition.
Declared-but-unused schema is not partial progress; it is *negative evidence* — it proves the
feature was designed and then not wired, which is exactly where assumptions rot.

**Impact on future development.** The sync epic includes populating and backfilling metadata,
not just adding columns. Every synced entity's metadata gets a write-site test ("saving X
stamps `ModifiedAtUtc`/`ModifiedByDeviceId`"). A6 is a hard prerequisite to LAN sync, not
cleanup.

> **Status 2026-07-07 — closed by Epic 1 M1.1** (commits `e968033` + `6e1c51f`, merged
> `3193adf`): the study-log write is awaited, `StudyLog.DeviceId` is stamped at the write site
> (`ViewModels/FocusViewModel.cs:151-159`), failures surface to the user
> (`autosave_failed` + `NotifyUser`), and write-site tests exist (`FocusViewModelA6Tests`).
> The full metadata block on every synced entity is M1.2 — implemented, in review.

---

## L3 — Deadline must not disappear after priority calculation: a score consumes information, a constraint preserves it

**Original assumption.** Folding deadline urgency into `PriorityScore` is sufficient
deadline handling. Downstream, the optimizer merely "arranges the prioritized tasks," so
`w6·DeadlineUrgency` could be deleted from the SOE objective with no replacement.

**Why it looked correct.** The ownership split is genuinely right: the Decision Engine
answers *which tasks matter*, the SOE answers *how to arrange them*. And keeping `w6` in the
objective double-counted a deadline signal already inside `PriorityScore.TimeComponent`
(finding N4). Removing the term was correct — the error was believing removal alone was safe.

**What the evidence showed.** Deadline reaches scheduling **only** through the priority
scalar (`daysLeft = (task.HanChot.Date - _clock.Now.Date).TotalDays`,
`Services/Strategies/PriorityCalculator.cs:35`). Placement then ignores it entirely: chunks go
to the least-loaded day under capacity (`WorkloadServiceImpl.cs:77-79`), and overflow appends
new days indefinitely (`WorkloadServiceImpl.cs:81-91`). A task due in three days can legally
have chunks placed on day nine — today, before the SOE even exists. The scalar is *lossy*:
once a deadline collapses into a number, no downstream consumer can recover the feasibility
boundary from it. High urgency buys a better *placement order*; it cannot express
*placement legality*.

**Principle** *(frozen 2026-07-02, Accepted #1)*. Information that acts as a **boundary**
must flow as a **constraint**; only information that acts as a **preference** may flow as a
score term. Removing a term from an objective is safe only if the same information re-enters
as a constraint. Concretely: `DeadlineUrgency` belongs exclusively to the Decision Engine's
priority calculation, **and** deadline feasibility re-enters the SOE as a hard constraint
owned by the Constraint Validator (alongside capacity limits, calendar constraints, and future
hard constraints). The objective evaluates schedule quality only, among feasible schedules.

**Impact on future development.** The SOE contract is two-part by construction (validator +
objective). The initial allocator eventually needs deadline-aware placement — its current
least-loaded fill is a known deadline-violation source. Tests must include the inversion
scenario: a near-deadline task must never be displaced past its deadline by a
quality-improving rearrangement.

---

## L4 — Hard constraints and objective functions are different concepts

**Original assumption.** One weighted sum (`w1…w6`, including `DeadlineUrgency`) can steer
the entire optimization: make the deadline weight big enough and deadlines will be respected.

**Why it looked correct.** A single scalar is simple to compute, compare, and tune, and
weighted scoring is the established idiom in this codebase (the priority components and
`WeightConfig` work exactly that way).

**What the evidence showed.** A weighted penalty is **negotiable by construction**: it trades
against every other term, so whenever accumulated quality gains exceed the deadline penalty,
violating the deadline is the *winning* move. Tuning `w6` changes where that break-even sits;
it cannot remove it. A requirement that must never lose cannot be a bidder in an auction.
(The same analysis exposed N4's double-counting: the deadline was bidding twice, once inside
`PriorityScore.TimeComponent` and once as `w6`.)

**Principle** *(frozen 2026-07-02, Accepted #4)*. Inviolable requirements are **predicates**
(feasible / not feasible) applied as filters; objectives **rank** only what the predicates
admit. Constraint Validation is a hard validation stage; Objective Evaluation is an
independent stage that measures optimization quality only. The two never mix: no score can
purchase a violation.

**Impact on future development.** Two separate seams (constraint validator vs. objective
evaluator) with independent tests. Explanations distinguish "rejected: infeasible" from
"rejected: lower score." Anyone proposing a new "very important" factor must first answer:
boundary or preference? — the answer decides which seam it enters.

---

## L5 — Relative feasibility is more robust than absolute feasibility

**Original assumption.** "Constraints must never be violated" (SOE proposal §5.6) — hard
constraints mean *absolute* feasibility of every output.

**Why it looked correct.** It is the strongest-sounding guarantee, it matches the intuitive
meaning of "hard," and in a world where inputs are always feasible it is even implementable.

**What the evidence showed.** The input is not always feasible. A student can owe more
minutes before a deadline than capacity allows, and the current allocator already produces
such schedules — its overflow path extends the horizon day by day with no deadline check
(`WorkloadServiceImpl.cs:81-91`). Under an absolute rule, an optimizer facing an infeasible
input must either be undefined or reject every candidate — i.e., the engine goes inert
**exactly when the user is overloaded, which is when they most need it**.

**Principle** *(frozen 2026-07-02 as an architectural invariant, Accepted #2)*. The SOE
preserves or improves feasibility; it never worsens it:

> `violations(output) ≤ violations(input)`, compared first by violation count, then by total
> overdue minutes.

When the input is feasible, this reduces to strict feasibility, so nothing is lost in the
normal case. Detecting and *reporting* infeasibility remains the Risk Analyzer's
responsibility — the optimizer must not silently absorb it.

**Impact on future development.** The SOE is total: defined on every input, degraded on none.
Test fixtures must include infeasible semesters, not just happy paths. The invariant is
property-testable (generate schedules, assert monotone non-worsening), which is cheap
insurance for every future optimizer added to the pipeline.

---

## L6 — Revision counters are local clocks: they cannot order events across devices

**Original assumption.** (An attractive shortcut, caught before it shipped.) A per-entity
monotonic revision counter could arbitrate sync conflicts: higher `Rev` = newer edit — and
being skew-free, counters even feel *safer* than timestamps.

**Why it looked correct.** On a single device it is literally true — the counter is a perfect
edit clock. It also mirrors the familiar EF Core `RowVersion` optimistic-concurrency idiom,
which lends it false authority in a .NET codebase.

**What the evidence showed.** Each device increments **its own** counter with no shared
origin and no communication. Device A at `Rev 40` versus device B at `Rev 3` proves only that
A *edits more often* — it says nothing about which edit happened *later*. Cross-device
comparison of local counters is not weak evidence; it is **no** evidence. The dual mistake is
also worth recording: two wall-clock timestamps tell you which is *larger*, not whether one
edit causally *saw* the other — recency and concurrency are different questions, and no single
scalar answers both. That was the trap in "timestamp + tiebreaker is enough."

**Principle** *(frozen 2026-07-02, Accepted #3)*. Separate the three roles the metadata must
play — no single mechanism covers them:

| Role | Mechanism | Explicitly not |
|---|---|---|
| Change enumeration ("what changed since the last sync watermark") + same-device ordering | `Rev` (monotonic per-entity counter) | never compared across devices |
| Concurrency detection ("did this edit see that one") | 3-way diff against the last-synced base — a field differing from base **on both sides** *is* a concurrent same-field edit | not derivable from `Rev` or timestamps |
| Tie-break for genuine same-field conflicts only | `ModifiedAtUtc`, then `ModifiedByDeviceId` (lexicographic) | `Rev` is excluded from this ordering |

Clock skew can therefore bias only *which value wins* a genuine conflict — never destroy
data — provided losing values are preserved in a conflict record. No Hybrid Logical Clock
unless a concrete failure demands one; the added machinery buys nothing the base snapshot
does not already provide here.

**Impact on future development.** Every synced entity gains the metadata block: `Rev`,
`ModifiedAtUtc`, `ModifiedByDeviceId`, and tombstone fields (`IsDeleted`, `DeletedAtUtc`).
Merge tests exercise concurrency detection and tie-breaking as *separate* concerns. Anyone
tempted to "simplify" by ordering on `Rev` across devices should be pointed at this section.

> **Status 2026-07-07:** the metadata block + tombstones are implemented in Epic 1 M1.2
> (worktree `epic1-sync-ready-data-model`, verdict refine-before-accept — one blocker
> M1.2-R1, [`../review/2026-07-06-epic1-m1.2-review.md`](../review/2026-07-06-epic1-m1.2-review.md)),
> stamped exclusively through the single `SyncStamper` seam merged in M1.1.

---

## L7 — Merge granularity is bounded by tracking granularity

**Original assumption.** "Field-level merge, LWW only on concurrent same-field edits" (D-F)
is a merge-*algorithm* choice that per-row metadata (one timestamp per entity) could support.

**Why it looked correct.** Row-level metadata is the standard shape (`UpdatedAt` column), and
the merge policy is executed at sync time — so it reads like a runtime behavior you can pick
independently of the schema.

**What the evidence showed.** With row-level metadata the only computable question is "which
*row* is newer" — which is entity-level LWW, a materially different (and lossier) policy than
what D-F promises: two devices editing *different* fields of the same task would still clobber
each other. You cannot merge changes you cannot detect. Field-level merge requires field-level
change knowledge — either per-field version columns or a retained base snapshot to diff
against. None of this exists today: `StudyTask` and `MonHoc` carry no change metadata at all
(`Models/StudyTask.cs`, `Models/MonHoc.cs`).

**Principle** *(frozen 2026-07-02, Accepted #3)*. The merge policy you can offer is bounded
by the change information you record — decide them together, never independently. Chosen
mechanism: **three-way merge** against a last-synced base snapshot per peer. It is preferred
over per-field version columns because concurrency detection falls out of the diff itself
(see L6) and the schema does not grow a column per field.

**Impact on future development.** The data model gains base-snapshot storage per peer;
base-snapshot retention interacts with tombstone garbage collection (retention window /
seen-by-all acknowledgment — still open in the Decision-4 sweep). Any future "just add a
field" change to a synced entity is also a merge-surface change and gets reviewed as one.

---

## L8 — **RESOLVED (pending CP-1 ratification)** — Granularity of evaluation ≠ granularity of commitment (optimization-pass semantics)

> **Status 2026-08-04 — resolution recorded, awaiting owner ratification at CP-1.** Gate **G2**
> is closed as a decision note:
> [`../plans/2026-08-04-g2-optimization-pass-semantics.md`](../plans/2026-08-04-g2-optimization-pass-semantics.md)
> (**G2-1 … G2-6**). The mechanism is *run-all, commit-best-prefix*: every stage runs unconditionally,
> each produces a checkpoint, and the pass commits the best **admissible** checkpoint (D-H first, then
> quality). The **all-or-nothing veto** is structurally impossible — four gains are never discarded for
> a fifth regression. The **determinism paradox** is dissolved by naming what varies: the committed
> schedule state, and nothing else; a pass that commits nothing is the fixed point and stops the loop
> immediately. The objective non-worsening threshold is strict, with a relative numerical-noise guard
> only. The defect analysis below is unchanged and still correct — it is *why* the resolution takes the
> shape it does. **Until CP-1 ratifies the note, the historical status directly below still governs
> what may be implemented.** On ratification, drop the "pending" qualifier here and in the note.

> **Status: OPEN by explicit decision (2026-07-02).** *(Historical — superseded 2026-08-04 by the
> note linked above; preserved per "Using this document".)* The defect analysis below is agreed;
> the *resolution* is not. Do not implement, and do not treat any candidate mechanism here as
> chosen.

**Original assumption.** Whole-pass accept/reject escapes the local-optimum trap of per-step
greedy rollback: evaluate the objective once per pass instead of once per optimizer, and
coarser granularity avoids greedy myopia.

**Why it looked correct.** It genuinely fixes one defect: within a pass, score dips can
propagate, so a later optimizer can recover through a valley that per-step rollback would have
walled off.

**What the review showed.** Coarsening the granularity moved the defect instead of removing
it, and added a second one:

- **All-or-nothing veto.** If four optimizers improve the schedule and the fifth regresses
  enough to drag the aggregate down, rejecting the pass discards all four gains. Coarser
  commitment can be *strictly worse* than finer commitment on the same input.
- **Determinism paradox.** A deterministic pass that was rejected will, re-run on identical
  input, be rejected identically. "Iterate to convergence" is a no-op unless something varies
  deterministically between passes — and a single-pass rejection means the engine did nothing
  at all. Determinism (which this project wants) and iteration (which hill-climbing needs)
  interact; neither the per-step nor the whole-pass extreme resolves that interaction.

**Partial principle (the lesson, not the decision).** *Where you measure* and *where you
commit* are independent design axes — conflating them is what made both extremes look like the
only options. Both ends of the commitment axis share one defect class: discarding recoverable
work. Any resolution must either state explicitly what varies between iterations, or
explicitly claim single-shot semantics and accept the consequences.

**Impact on future development.** SOE implementation is **blocked** on this decision — it is
the remaining gate in the architecture freeze. Candidate mechanisms (e.g., best-feasible-prefix
selection over per-stage checkpoints) live in the review discussion, not in any spec. The
interface seam is stable under every candidate: `Optimize(schedule) → (schedule, report)`,
with the Constraint Validator and Objective Evaluator seams from L4 unchanged.

---

## L9 — Guarantees can be pairwise consistent and jointly impossible: the delete/edit trilemma

**Original assumption.** The delete-vs-edit conflict policy could satisfy all three stated
goals at once: the tombstone wins; no irreversible data loss; no edit-history subsystem in v1.

**Why it looked correct.** Every *pair* is comfortable: delete-wins + history = recoverable;
no-loss + no-history = just don't let delete win; delete-wins + no-history = simple. Each goal
read in isolation sounds like a modest requirement, so the conjunction was never checked.

**What the evidence showed.** The conjunction is structurally impossible: if the tombstone
wins a delete-vs-edit race and there is no history, the concurrent edit **is** the
irreversible loss. This is not an implementation difficulty — no amount of engineering
delivers all three.

**Principle.** When guarantees conflict, name the trade explicitly and pick — shipping the
contradiction means the code picks silently later. Resolution direction adopted with the sync
design: preserve the *losing side* of every conflict (delete-vs-edit and same-field LWW alike)
in a conflict/audit record. That buys "no irreversible loss" without a full edit-history
subsystem; append-only edit history is then explicitly out of v1 scope rather than vaguely
"preserved if possible."

**Impact on future development.** The conflict record becomes a first-class schema element of
the sync design. Still open in the Decision-4 sweep: tombstone retention length and purge
authority (retention ≥ maximum offline window vs. seen-by-all-devices acknowledgment), and the
cascade policy for soft-deleting a parent (`MonHoc`) with live children.

---

## Using this document

- Read L3–L5 before touching the SOE; read L6–L7 and L9 before touching persistence or sync;
  read L1–L2 before trusting any document — including this one.
- New lessons follow the same rubric: original assumption → why it looked correct → what the
  evidence showed → principle → impact. A lesson without evidence is an opinion; put the
  `file:line` in.
- Decisions do not live here. When a lesson's principle is superseded, mark it superseded and
  link the decision record — do not rewrite history.
