# Smart Study Planner — Docs Index

> Last reorganized: 2026-05-21

This folder contains all living documentation for Smart Study Planner. Outdated/fulfilled docs were consolidated and removed; historical artifacts are in `_archive/` and the unified history lives in `CHANGELOG.md`.

## Reading order

1. **[specs/system_roadmap.md](specs/system_roadmap.md)** — canonical roadmap: Part A (what's done / next), Part B (architecture direction). *(`ROADMAP.md` retired → pointer stub.)* Decision records: [plans/2026-07-01-architecture-direction-decisions.md](plans/2026-07-01-architecture-direction-decisions.md) (D-A…D-F) + [plans/2026-07-02-architecture-freeze-decisions.md](plans/2026-07-02-architecture-freeze-decisions.md) (D-G…D-J + open items). Execution decomposition: [plans/2026-07-03-master-plan.md](plans/2026-07-03-master-plan.md) (Epics 1–4 + gates).
2. **[CHANGELOG.md](CHANGELOG.md)** — synced history M1 → current.
3. **architecture/** — current state of the code (single source of truth).
   - [overview.md](architecture/overview.md) — layers, tech stack, runtime composition.
   - [data-model.md](architecture/data-model.md) — SQLite schema + data pipeline.
   - [dependency-flows.md](architecture/dependency-flows.md) — who calls who.
   - [async-workflow.md](architecture/async-workflow.md) — async posture.
   - [usecase-flows.md](architecture/usecase-flows.md) — UC-01..UC-11 step-by-step.
   - [lessons-learned.md](architecture/lessons-learned.md) — engineering postmortem of the 2026-07 architecture review (why the decisions exist).
4. **active/** — work in progress / planned (read before editing).
   - [refactor-god-object.md](active/refactor-god-object.md) — remaining slices 5-8.
   - [m8-text-classifier.md](active/m8-text-classifier.md) — M8-A.
   - [m8-weight-optimizer.md](active/m8-weight-optimizer.md) — M8-B.
5. **knowledge/** — extracted lessons (programming, system design, ML, debugging).
6. **specs/**, **plans/**, **reports/**, **review/** — working areas for new work. Each has a README explaining when/how to add files.
7. **[ux_quality_gate_checklist.md](ux_quality_gate_checklist.md)** — regression checklist for UI work.

## Conventions

- All new plans / specs / reports go under `active/` (work) or `architecture/` (current state).
- Once a plan ships → append a row to `CHANGELOG.md`, then move the plan to `_archive/` only if it has lasting reference value, otherwise delete.
- Knowledge nuggets distilled from any work belong in `knowledge/`.
