# Reports

> Reports + project context. Answers: **what happened**, **what we learned**, **what state things are in**.

## When to add a file here

- Completion report after a milestone / slice ships.
- Bug post-mortem (root cause + fix + lesson).
- Benchmark or measurement results.
- Project snapshot (test count, build status, GitNexus stats).
- Decision context — why a path was chosen over alternatives.

## Naming

`YYYY-MM-DD-<short-kebab-slug>.md`.

## Required sections

1. **Date** + **Author/agent** (if known).
2. **Scope** — what this report covers.
3. **Findings** — facts, numbers, observed behavior.
4. **Verification** — commands run, test counts, build status.
5. **Follow-ups** — non-blocking items to track.

## Lifecycle

- Reports are append-only historical artifacts; don't edit old reports, write new ones instead.
- Distill recurring lessons into `docs/knowledge/` so they don't get buried.
- Distill ship-events into `docs/CHANGELOG.md`.
- Once a report's content is in CHANGELOG + knowledge, it can be deleted unless it has standalone reference value (e.g. a benchmark).
