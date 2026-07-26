# Active

> Pointers to work **currently in progress** — nothing else lives here. Completed trackers
> are archived to `legacy/Archived plans/` (local-only, gitignored; content stays recoverable
> in git history). Canonical status lives in
> [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.3 and the
> [master plan](../plans/2026-07-03-master-plan.md) — this folder only answers
> *"what is being worked on right now, and where is its plan?"*.

## Current (2026-07-20)

| Work | Plan | State |
|---|---|---|
| **Epic 1 — Released (2026-07-20)** | [`../plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md) + [reopen fix plan](../plans/2026-07-19-epic1-reopen-fix-plan.md) | Code merged (M1.1/M1.2/M1.3 `a3a0a3d`; post-close fix `101aaa3`). Phase 1 (A1–A4) **done**. Phase 2 (owner B1–B4): B1–B3 passed; **B4 reopened** on a latent M1.2 FK regression (task-create crash), then **fixed** — R1 (`ThemTask` stamps `MaMonHoc`, `3bb56c6`/`63b9611`) + R2 (`CrashLogger` + global handlers, `b0061e7`/`c18e1e7`), merged `37f9678`. A separate pre-existing Analytics stale-render bug was fixed (`c4291c7`, 337 pass; Part 2 visual check pending owner re-run). **Owner signed off Epic 1 = Released (2026-07-20)** — closure-gate release decision record. Gate Execution Rules **lifted** (Epic 1 released; Epic 2/3 no longer blocked). QA lessons → [`knowledge/incident-investigation.md`](../knowledge/incident-investigation.md). *(Released — archive this row in the next sweep.)* |
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
