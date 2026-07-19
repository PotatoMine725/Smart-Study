# Smart Study Planner — Docs Index

> Last reorganized: 2026-07-07 — plans from 2026-06 and earlier + completed `active/` trackers were archived to `legacy/Archived plans/` (local-only, gitignored; content remains in git history).

This folder contains all living documentation for Smart Study Planner. Outdated/fulfilled docs are archived to `legacy/Archived plans/`; the unified history lives in `CHANGELOG.md`.

## Reading order

1. **[specs/system_roadmap.md](specs/system_roadmap.md)** — canonical roadmap: Part A (what's done / next), Part B (architecture direction). *(`ROADMAP.md` retired → pointer stub.)* Decision records: [plans/2026-07-01-architecture-direction-decisions.md](plans/2026-07-01-architecture-direction-decisions.md) (D-A…D-F) + [plans/2026-07-02-architecture-freeze-decisions.md](plans/2026-07-02-architecture-freeze-decisions.md) (D-G…D-J + open items). Execution decomposition: [plans/2026-07-03-master-plan.md](plans/2026-07-03-master-plan.md) (Epics 1–4 + gates).
2. **[CHANGELOG.md](CHANGELOG.md)** — synced history M1 → current.
3. **architecture/** — current state of the code (single source of truth).
   - [overview.md](architecture/overview.md) — layers, tech stack, runtime composition.
   - [data-model.md](architecture/data-model.md) — SQLite schema + data pipeline.
   - [dependency-flows.md](architecture/dependency-flows.md) — who calls who.
   - [async-workflow.md](architecture/async-workflow.md) — async posture.
   - [usecase-flows.md](architecture/usecase-flows.md) — UC-01..UC-12 step-by-step.
   - [lessons-learned.md](architecture/lessons-learned.md) — engineering postmortem of the 2026-07 architecture review (why the decisions exist).
4. **active/** — pointers to work in progress only (read [active/README.md](active/README.md) before editing).
   Current: Epic 1 **reopened** at the release gate (Phase 2 supervised launch, B4 = Reopen —
   one M1.2 regression; fix plan drafted, implementation awaiting owner approval — see
   [active/README.md](active/README.md)) + UI mobile-ready polish (proposed).
5. **knowledge/** — extracted lessons (programming, system design, ML, debugging, release
   engineering, review methodology, sync data model, architecture process, incident
   investigation).
6. **specs/**, **plans/**, **reports/**, **review/** — working areas for new work. Each has a README explaining when/how to add files.
7. **[ux_quality_gate_checklist.md](ux_quality_gate_checklist.md)** — regression checklist for UI work.

## Conventions

- New plans go under `plans/` (naming `YYYY-MM-DD-<kebab>.md`), with a pointer row in `active/README.md` while in progress; `architecture/` describes current state only.
- Once a plan ships → append a row to `CHANGELOG.md`, reflect the end state in `architecture/`, then move the plan to `legacy/Archived plans/` (local archive, gitignored — the repo keeps its history in git).
- Knowledge nuggets distilled from any work belong in `knowledge/`.
