# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

## Current (2026-07-26)

| Work | Plan | State |
|---|---|---|
| **Analytics two-section redesign** | [`../plans/2026-07-20-analytics-two-section-redesign.md`](../plans/2026-07-20-analytics-two-section-redesign.md) | QUEUED — design brief only (owner post-release backlog); not implemented. Phase 3 unlocked, not started |
| **UI fidelity + mobile-ready polish** | [`../plans/2026-07-05-ui-mobile-ready-polish.md`](../plans/2026-07-05-ui-mobile-ready-polish.md) | PROPOSED (branch `ui_rf`) |

Deferred items that exit via *data*, not code (tracked in the roadmap, not here):
M8-B ML training (waits for matured `WeightChangeLog` rows with class balance);
M8-A `TextClassifierModelManager.RetrainAsync` consumer wiring.

## Rules

- One tracker file **or** one row in the table above per in-progress effort; the detailed plan
  lives in `plans/` (naming `YYYY-MM-DD-<kebab>.md`).
- When an effort ships: append to `CHANGELOG.md`, reflect the end state in `architecture/`,
  then move its tracker/plan to `legacy/Archived plans/`.
- Keep this folder near-empty on purpose — if something has been "active" for weeks with no
  commits, it is not active; archive it or re-plan it.

## Archived from here (2026-07-07 sweep)

`refactor-god-object.md` (Slices 1–8 shipped), `m8-text-classifier.md` (M8-A shipped),
`m8-weight-optimizer.md` (M8-B rule-based + Slice 8 UI shipped) → `legacy/Archived plans/`.

## Archived from here (2026-07-26 sweep)

Epic 1 shipped and Released (2026-07-20), so its execution/QA plans moved to
`legacy/Archived plans/` (local-only, gitignored; content stays in git history):
`2026-07-02-next-session-agenda.md`, `2026-07-03-epic-1-execution-plan.md`,
`2026-07-03-g1-soft-delete-cascade.md`, `2026-07-10-epic1-m1.3-monhoc-identity-brief.md`,
`2026-07-12-epic1-closure-phase1-execution.md`, `2026-07-15-epic1-phase2-owner-runbook.md`,
`2026-07-19-epic1-reopen-fix-plan.md`, `2026-07-20-epic1-reopen-owner-reclosure-runbook.md`,
`2026-07-20-analytics-stale-render-fix.md`. Kept in `plans/`: the closure-gate record
(`2026-07-11-epic-1-closure-gate.md`, holds the B4=Released decision) and the decision records.
