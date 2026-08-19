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
   Current: Epic 1 **Released** (2026-07-20) — the B4 reopen (a latent M1.2 FK regression) was
   fixed (R1/R2) and, with a separate pre-existing Analytics stale-render bug also fixed, the
   owner signed off release — see [active/README.md](active/README.md). Next: Analytics
   two-section redesign (design brief queued) + UI mobile-ready polish (proposed).
5. **knowledge/** — extracted lessons (programming, system design, ML, debugging, release
   engineering, review methodology, sync data model, architecture process, incident
   investigation, QA gates).
6. **specs/**, **plans/**, **reports/**, **review/** — working areas for new work. Each has a README explaining when/how to add files.
7. **[ux_quality_gate_checklist.md](ux_quality_gate_checklist.md)** — regression checklist for UI work.

## Conventions

- New plans go under `plans/` (naming `YYYY-MM-DD-<kebab>.md`), with a pointer row in `active/README.md` while in progress; `architecture/` describes current state only.
- Once a plan ships → append a row to `CHANGELOG.md`, reflect the end state in `architecture/`, then move the plan to `legacy/Archived plans/` (local archive, gitignored — the repo keeps its history in git).
- Knowledge nuggets distilled from any work belong in `knowledge/`.

## Artifact types

> Added 2026-08-19 after the Epic 3 QA cycle, where one document was doing several jobs at once
> (a runbook accumulating results, a fix report carrying an amendment, an owner's raw evidence
> filed as a report). **Decide the type before writing**, then follow that type's README. One
> document, one purpose — when a document needs a second purpose, that is a second document.

| Type | Answers | Lives in | Naming |
|---|---|---|---|
| **Plan** | How do we ship this, in what slices? | `plans/` | `YYYY-MM-DD-<slug>.md` |
| **Decision record** | What was ratified, why, and what would reopen it? | `plans/` | `…-decision(s).md` / `…-governance.md` |
| **Runbook** | Exactly how does a human execute this, and what is pass vs. fail? | `plans/` | `…-runbook.md` |
| **Execution report** | What was implemented, what changed, what evidence shows completion, what is still open? | `reports/` | `YYYY-MM-DD-<slug>.md` |
| **QA / gate report** | What was tested, what passed, what could **not** be tested automatically, what is the gate verdict? | `reports/` | `…-qa-*.md` / `…-gate*.md` |
| **Investigation report** | What was observed, which hypotheses were considered, what evidence established the cause, what is still uncertain? | `reports/` | `…-investigation.md` / `…-diagnosis.md` |
| **Evidence record** | What did the person at the keyboard actually see? (raw, first-hand, usually owner-authored) | `reports/` | `…-observation.md` |
| **Review / verdict** | Independently assessed: what is confirmed, what findings remain, ship or not? | `review/` | `YYYY-MM-DD-<slug>.md` |
| **Closing note** | Did the milestone satisfy its contract, on what evidence, with what accepted limitations? | `reports/` | `…-closing-note.md` |
| **Knowledge article** | What should a future engineer remember, independent of this change? | `knowledge/` | `<topic>.md` (flat, topic-level — extend before adding) |

Two rules cut across every type:

- **Evidence-scoped claims.** State every substantive claim as *claim → evidence → scope →
  remaining uncertainty*. A class of defect is only "closed" if the document names the evidence
  that establishes that scope. Keep fact, inference, decision and recommendation visibly distinct —
  and where a verdict came from a person rather than a file, say which (**observation** = written
  down while looking; **ruling** = an authorised person's statement, no written record;
  **inference** = supporting circumstance, not evidence). Never upgrade a ruling into an
  observation.
- **Amendments, not rewrites.** Dated artifacts (reports, reviews, evidence records) are corrected
  by *appending* a dated amendment section and marking the superseded passage in place — never by
  editing the original text into a cleaner story. Live artifacts (plans, runbooks, `architecture/`,
  `active/`) are edited in place, because their job is to be current.
