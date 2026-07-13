# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

## Current (2026-07-13)

| Work | Plan | State |
|---|---|---|
| **Epic 1 — release gate** | [`../plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md) + [Phase 1 execution plan](../plans/2026-07-12-epic1-closure-phase1-execution.md) | Code **complete** (M1.1/M1.2/M1.3 merged `a3a0a3d`; post-close fix `101aaa3`). Release-gate Phase 1: A1 **done** (WAL-safe backup, merged `8740350`) · A3 **done** (4 knowledge articles, `ca4f5ba`/`b01bb9a`) · A2 **done** (this docs sync) · A4 pending. Phase 2 (owner: supervised first launch, B1–B4) not started — no new Epic work until release per the gate's Execution Rules |
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
