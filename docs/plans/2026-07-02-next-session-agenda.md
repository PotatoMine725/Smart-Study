# Next-Session Agenda — Architecture Freeze Follow-ups

> **✅ Outcome (2026-07-02) — this agenda has run.** The critical review executed against source.
> **2A and 2B are resolved** and frozen as **D-G/D-H/D-I/D-J** in
> [`2026-07-02-architecture-freeze-decisions.md`](2026-07-02-architecture-freeze-decisions.md);
> the §3 constraint-hard-filter question → **D-J**; the Decision-4 trilemma → conflict record (**D-I**).
> **2C remains OPEN by explicit decision** (both extremes' defects acknowledged; nothing frozen —
> SOE implementation blocked; freeze record §3). Still open: tombstone retention/purge, cascade policy,
> weight governance (B5). Discoveries: [`../architecture/lessons-learned.md`](../architecture/lessons-learned.md).
> Body preserved unchanged below.

**Date:** 2026-07-02
**Inputs:** `docs/plans/2026-07-01-forks-proposals.md` (your Decisions 1–4),
`docs/plans/2026-07-01-architecture-direction-decisions.md` (D-A..D-F).
**Scope of this doc:** summary of where the architecture stands + a prioritized list
of what to debate/plan next. **No code. No spec edits yet.**

---

## 1. Where we are (brief)

Six direction decisions are recorded (D-A..D-F). Your four latest answers resolve the
last open sub-decisions:

| # | Decision | Status |
|---|----------|--------|
| 1 | SOE = staged optimization; objective evaluates the **whole pass**, then accept/reject the pass (no per-optimizer greedy rollback). Deterministic, heuristic-first. | Locked in principle |
| 2 | `DeadlineUrgency` belongs **only** to the Decision Engine / `PriorityScore`; **removed** from the SOE objective. New objective = `w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty + w5·FragmentationPenalty`. | Locked in principle |
| 3 | LAN conflict clock = wall-clock timestamp + `DeviceId` tie-breaker + version/revision metadata. **No HLC** unless a concrete failure demands it. | Locked in principle |
| 4 | Soft-delete + tombstone + retention. On delete-vs-edit: preserve tombstone, preserve edit history if possible, no immediate permanent deletion. | Locked in principle |

"Locked in principle" = the *direction* is settled; the *edges below* are not, and each
one changes what the implementation must do. **The spec is not yet frozen** — freezing
was intentionally deferred until these edges are closed.

**Still owed from last session:** the critical review of Decisions 1–4 (challenge →
find hidden flaws → freeze if sound). This agenda is the precursor: it surfaces the flaws
so the freeze is informed rather than a rubber-stamp. The three edges in §2 are
load-bearing — and note that §2 has already *half-run* that critical review for
Decisions 1, 2 and 3, so next session starts those arguments already framed, not cold.

---

## 2. The three edges that must be debated first

These are not polish. Each is a case where a locked decision is only safe if a second thing
is also decided — and that second thing is currently unstated.

### 2A. Decision 2 is safe only if deadlines re-enter the SOE as a *constraint*
Removing `DeadlineUrgency` from the **objective** is correct — but the objective is not
the only place a deadline matters. If the SOE is free to reorder tasks to maximize
learning quality and it no longer sees deadlines at all, it can legally schedule a
high-priority, near-deadline task *late* because that arrangement scores better. Priority
would be silently violated by the very engine that runs after the Decision Engine.

**The discriminator that decides whether this is a must-fix or a non-issue:** *does the SOE
place tasks onto concrete dates/time-slots, or does it only rearrange within day-buckets
already fixed upstream by the Decision Engine?*
- If it **places tasks in time** → deadline feasibility is a **mandatory hard constraint**
  and the priority-inversion risk is real.
- If it **only arranges within fixed buckets** → the risk is contained upstream and 2A is
  largely a non-issue.

Answer this first. If placement-in-time, then deadlines/priority must enter the SOE as a
**hard constraint or a preserved ordering** (not a scored term): either (a) an
already-ordered list treated as inviolable, or (b) tasks + deadline feasibility windows
where any pass that pushes a task past its deadline / inverts priority is rejected.

### 2B. Decision 3 can't implement D-F — and it needs *two* distinct things, not one
D-F says "field-level merge, LWW **only** on concurrent same-field edits." A bare wall-clock
delivers neither half. Split the requirement so neither gets lost:

1. **Concurrency detection** → per-entity **revision/version metadata** (a monotonic counter
   or small version vector). Two timestamps tell you which is *larger*, not whether one edit
   *causally saw* the other — so a timestamp alone cannot tell a concurrent edit from a
   sequential one, which is exactly what "LWW *only* on concurrent" requires. This is what
   rules out HLC while still needing more than a timestamp — your no-HLC instinct holds.
2. **Field-level merge granularity** → **per-field** change tracking, so non-overlapping
   field edits merge and only genuine same-field overlaps fall to LWW. Entity-level revision
   only buys entity-level LWW, which is **not** field-level merge at all.

So per-field tracking is **mandatory, not "ideally"** — without it there is no field-level
merge. (This connects cleanly: the data-model doc already lists per-field change tracking as
an open gap.) Net: the metadata gap is **smaller than HLC, larger than a lone timestamp**.

**Secondary risk to note:** wall-clock LWW is vulnerable to **clock skew** — a device with
a fast clock always "wins." Decide a tolerance / whether the per-field revision counter
should break ties *before* the timestamp does.

### 2C. Decision 1's pass-level accept/reject still has the defect it was meant to escape
You rejected per-optimizer rollback to avoid local optima. Whole-pass accept/reject is
still hill-climbing — just coarser — and introduces two new problems:
- **All-or-nothing veto:** if four optimizers improve the schedule and one regresses enough
  to drag the aggregate down, the pass is rejected and *all four gains are discarded*.
  Coarser granularity can be strictly worse than per-step.
- **Determinism paradox:** a deterministic rejected pass, re-run on identical input, rejects
  identically. So "iterate to convergence" is a **no-op** unless something varies
  deterministically between passes (optimizer order, weight schedule, an annealing-like
  temperature) — which is exactly the added complexity you're trying to avoid. And a
  single-pass reject means the engine does **nothing** at all.

**To debate:** is it single-pass (accept the whole pass or ship the initial schedule
untouched), or iterated? If iterated, what varies between passes to make re-running
meaningful *without* turning the SOE into the search solver you ruled out? This is the
same weight as 2A/2B — it decides whether the staging model actually does work.

---

## 3. Remaining open questions, by decision (lower urgency)

**Decision 1 — SOE staging** (beyond the single-pass-vs-iterate edge in 2C)
- Acceptance criterion for a pass: strictly-better? not-worse? better-by-tolerance?
- `Constraint Validation` appears in the pass but not in the objective — is it a **hard
  filter** (reject pass on violation) or soft? Define hard vs. soft constraints.
- Weights w1..w5: static constants for v1? How are they justified/tuned with no telemetry
  yet? How does a deterministic pass surface an **explanation** ("why this schedule")?
- Reconcile the optimizer inventory with roadmap §13's "six engines" framing (tension
  already flagged in `system_roadmap.md §7.3`).

**Decision 2 — deadline ownership** — mostly covered by 2A. Also: confirm the SOE never
*reads* `PriorityScore` internals, only consumes its output ordering (keeps the boundary clean).

**Decision 3 — LAN clocks** — mostly covered by 2B. Also out-of-scope-but-needed-for-LAN-planning:
transport/discovery/trigger model, and max tolerable offline window (feeds retention below).

**Decision 4 — delete vs edit**
- Retention length + **who purges**: in a sync mesh, a tombstone can't be GC'd until every
  known peer has seen it, or a purge-on-A row resurrects from an offline B. Decide:
  retention ≥ max offline window, or a "seen-by-all-known-devices" ack. This ties to
  Decision 3's device registry.
- Cascade: soft-deleting a parent (`MonHoc`) — do children tombstone too? Orphan policy.
  (Note: cascade deletes are already called out as hard in the current data model.)
- Delete-vs-edit **UX rule**: "preserve tombstone" implies delete-wins — so the user sees
  the entity gone, with the edit recoverable? Confirm the visible outcome, not just storage.
- "Preserve edit history" — is real history (append-only / change log) **in scope for v1**,
  or aspirational? This is a meaningful data-model commitment; scope it explicitly.
- **Latent trilemma (pick two):** "preserve tombstone / delete-wins" + "avoid irreversible
  loss" + "no real edit history" cannot all hold — if delete wins and there's no history,
  the concurrent edit *is* the irreversible loss. Deciding the edit-history scope above
  resolves it.

---

## 4. Suggested sequencing for next session

1. **Close 2A and 2B** (they gate correctness of two locked decisions).
2. Sweep the Decision-1 staging parameters and the Decision-4 retention/cascade rules.
3. **Then** run the deferred critical review → if sound, freeze into
   `docs/architecture/*` + `system_roadmap.md` (D-E per-pass semantics, drop w6·Deadline,
   record the clock model, record the tombstone/retention policy).
4. Respect **D-B ordering**: sync-ready data model (identity semantics, per-field revision,
   tombstones) lands **before** SOE implementation — so the Decision-3/4 data-model work is
   the first thing that becomes code, even though SOE decisions were made in parallel.

**Guardrail:** the deliverables through the freeze remain documentation only. Code starts
after the freeze, data-model-first.
