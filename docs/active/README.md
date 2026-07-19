# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

## Current (2026-07-19)

| Work | Plan | State |
|---|---|---|
| **Epic 1 — release gate → REOPENED** | [`../plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md) + [reopen fix plan](../plans/2026-07-19-epic1-reopen-fix-plan.md) | Code merged (M1.1/M1.2/M1.3 `a3a0a3d`; post-close fix `101aaa3`). Phase 1 (A1–A4) **done**. Phase 2 (owner B1–B4, 2026-07-15) **done — B4 = Reopen**; driver: M1.2 FK regression (task create crash). [QA investigation](../reports/2026-07-19-epic1-phase2-qa-investigation.md) **accepted by owner** ([decisions](../specs/2026-07-19-owner-epic-1-decisions.md)); lessons → [`knowledge/incident-investigation.md`](../knowledge/incident-investigation.md). **Now:** reopen fix plan `draft` — implementation blocked on owner approval; gate Execution Rules still in force (no Epic 3/2 work until release) |
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
