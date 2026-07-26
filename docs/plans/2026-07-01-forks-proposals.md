> **Outcome (2026-07-02):** reviewed against source and frozen in
> [`2026-07-02-architecture-freeze-decisions.md`](2026-07-02-architecture-freeze-decisions.md) —
> Decisions 2–4 accepted with refinements (D-G/D-H/D-I/D-J); **Decision 1's whole-pass accept/reject
> was reopened** — the pass accept/commit semantics are an explicitly OPEN decision (freeze record §3).
> Original message preserved unchanged below.

Thank you for the reconciliation review. I agree that the remaining open items are genuine architectural trade-offs rather than implementation issues.

Below are my current architectural decisions. Please treat them as proposed design decisions, review them critically, identify any hidden flaws or unintended consequences, and then update the architecture documents accordingly if you agree they are internally consistent.

---

# Decision 1 — Study Optimization Engine execution model

I do not want the Study Optimization Engine to become a global search or optimization solver.

The project philosophy remains:

> deterministic, explainable, heuristic-first.

I also do not want every optimizer to immediately rollback when its local score decreases, because this creates a local optimum problem.

Instead, I prefer a staged optimization model.

Conceptually:

```text
Initial Schedule
        ↓
Optimization Pass
    ├── Load Balancer
    ├── Session Optimizer
    ├── Context Optimizer
    ├── Fragmentation Optimizer
    ├── Fatigue Evaluator
    └── Constraint Validation
        ↓
Evaluate Overall Objective
        ↓
Accept / Reject Optimization Pass
```

The objective function evaluates the result of an optimization pass rather than every individual optimizer.

The pipeline therefore remains deterministic while avoiding greedy local rollback after every step.

Please evaluate whether this approach introduces hidden issues.

---

# Decision 2 — Deadline ownership

I believe deadline urgency should belong exclusively to the Decision Engine.

The Decision Engine answers:

> Which tasks are more important?

The Study Optimization Engine answers:

> Given the prioritized tasks, how should they be arranged to maximize learning quality?

Therefore I do NOT want DeadlineUrgency to appear inside the Study Optimization objective function.

PriorityScore already includes deadline urgency.

The Study Optimization objective should instead optimize schedule quality only.

Current direction:

Score =
w1 * LoadBalance
+w2 * ContextContinuity
+w3 * SessionQuality
+w4 * FatiguePenalty
+w5 * FragmentationPenalty

Please review whether removing DeadlineUrgency completely from the optimization objective causes any unintended behavior.

---

# Decision 3 — LAN synchronization clocks

For the first public release I prefer simplicity over distributed-system sophistication.

Current proposal:

- Wall-clock timestamp
- DeviceId tie-breaker
- Version / revision metadata where appropriate

I do not want to introduce Hybrid Logical Clocks unless there is a concrete requirement that cannot be solved by the simpler design.

Please identify any critical failure cases that would justify HLC at this stage.

---

# Decision 4 — Delete vs Edit

I prefer soft deletion.

Deletion should create a tombstone rather than immediately removing the entity.

If one device deletes an entity while another edits it:

- preserve the tombstone
- preserve the edit history if possible
- avoid irreversible data loss

Automatic permanent deletion should not occur immediately.

Please evaluate whether tombstone + retention period is sufficient, or whether another conflict policy would better fit the project's local-first philosophy.

---

# Review Request

Please do NOT implement code.

Instead:

1. Challenge these decisions.
2. Point out architectural weaknesses.
3. Identify edge cases.
4. Suggest improvements only if they significantly improve consistency without increasing unnecessary complexity.
5. If the decisions remain sound, update the architecture specification so these become part of the project's architectural baseline.

The goal is to reach an architecture freeze before implementation.