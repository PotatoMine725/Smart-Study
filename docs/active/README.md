# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

**Epic 3 (Study Optimization Engine) closed 2026-08-19** — code complete 2026-08-07, manual QA gate
**CLOSED, PASS WITH FINDINGS** (every scenario passed; no scenario produced a defect). Suite 391 →
**487**. See `docs/CHANGELOG.md`, the [gate closure](../reports/2026-08-19-epic3-manual-gate-closure.md),
and the [closing note](../reports/2026-08-07-epic3-closing-note.md).

**Nothing is in progress right now.** Both rows below are queued/proposed, not being worked.

*Superseded 2026-08-19 (kept for history):* the previous banner said *"Epic 3 (SOE) is next"*, which
was true when written on 2026-08-02. The order it cited still holds — the
[master plan](../plans/2026-07-03-master-plan.md) sequences **E1 → E3 → E2 → E4**, and *"Epic 2 entry
criteria"* remains the stabilization plan's name for a set of gates, **not** an execution order.
With E1 and E3 both closed, the next epic in that sequence is the **LAN-sync epic (Epic 2)**, which
**has not been started**. Naming the order is not the same as choosing it: **G3-1** — wiring the
Epic 3 optimizer into production, still unscheduled — could reasonably come first. That call is the
owner's and has not been made.

## Current (2026-08-20)

| Work | Plan | State |
|---|---|---|
| **Analytics two-section redesign** | [`../plans/2026-07-20-analytics-two-section-redesign.md`](../plans/2026-07-20-analytics-two-section-redesign.md) | QUEUED — design brief, **plus a delivered implementation package** (2026-08-02) now under version control at [`../assets/analytics-ui-package/`](../assets/analytics-ui-package/). **Not integrated**; no code merged. Phase 3 unlocked, not started. *Known gap: the package README cites an interactive mockup `Analytics Redesign Proposal.dc.html` that is not in the repository.* |
| **UI fidelity + mobile-ready polish** | [`../plans/2026-07-05-ui-mobile-ready-polish.md`](../plans/2026-07-05-ui-mobile-ready-polish.md) | PROPOSED, on `dev` — `ui_rf` was adopted as the tested trunk and merged (PR #49, 2026-07-26), so the plan is no longer branch-scoped; it remains unimplemented |

Deferred items tracked in the roadmap (§A.4), not here — listed so they are not mistaken for active
work: **G3-1** (wire `IScheduleOptimizer.Optimize` into production — the engine has no production
call site); the **E6 surviving mutant** (`DetectChanges()` ordering — pin it or prove it redundant,
do **not** delete the call); M8-B ML training (waits for matured `WeightChangeLog` rows with class
balance); M8-A `TextClassifierModelManager.RetrainAsync` consumer wiring. The last two exit via
*data*, not code.

## Rules

- One tracker file **or** one row in the table above per in-progress effort; the detailed plan
  lives in `plans/` (naming `YYYY-MM-DD-<kebab>.md`).
- When an effort ships: append to `CHANGELOG.md`, reflect the end state in `architecture/`,
  then move its tracker/plan to `legacy/Archived plans/`.
- Keep this folder near-empty on purpose — if something has been "active" for weeks with no
  commits, it is not active; archive it or re-plan it.

## Archived from here (2026-07-07 sweep)

`refactor-god-object.md` (Slices 1–8 shipped), `m8-text-classifier.md` (M8-A shipped) →
`legacy/Archived plans/`. `m8-weight-optimizer.md` was copied there too but **stays tracked
here as well** — it is still the live tracker for the one M8-B item that hasn't shipped (ML
training, gated on `WeightChangeLog` data volume; see Deferred items above).

## Archived from here (2026-07-26 sweep)

Epic 1 shipped and Released (2026-07-20), so its execution/QA plans moved to
`legacy/Archived plans/` (local-only, gitignored; content stays in git history):
`2026-07-02-next-session-agenda.md`, `2026-07-03-epic-1-execution-plan.md`,
`2026-07-03-g1-soft-delete-cascade.md`, `2026-07-10-epic1-m1.3-monhoc-identity-brief.md`,
`2026-07-12-epic1-closure-phase1-execution.md`, `2026-07-15-epic1-phase2-owner-runbook.md`,
`2026-07-19-epic1-reopen-fix-plan.md`, `2026-07-20-epic1-reopen-owner-reclosure-runbook.md`,
`2026-07-20-analytics-stale-render-fix.md`. Kept in `plans/`: the closure-gate record
(`2026-07-11-epic-1-closure-gate.md`, holds the B4=Released decision) and the decision records.
