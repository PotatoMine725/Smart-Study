# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

## Current (2026-07-07)

| Work | Plan | State |
|---|---|---|
| **Epic 1 — Sync-Ready Data Model** | [`../plans/2026-07-03-epic-1-execution-plan.md`](../plans/2026-07-03-epic-1-execution-plan.md) | M1.1 **merged** (`3193adf`); M1.2 implemented in worktree `epic1-sync-ready-data-model`, **in review** (refine-before-accept — [M1.2-R1](../review/2026-07-06-epic1-m1.2-review.md)); M1.3 pending |
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
