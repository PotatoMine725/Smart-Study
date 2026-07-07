# Plans

> Implementation plans. Answers: **how** to ship a spec, broken into shippable slices.

Differs from `active/` — `active/` holds the **current** plan in progress; `plans/` is the working area for any plan, including ones that are still being drafted or awaiting approval.

## When to add a file here

- A spec needs an execution path (file map, slice order, verification gate).
- A refactor needs blast-radius analysis + commit-by-commit breakdown.
- A multi-day effort needs explicit checkpoints.

## Naming

`YYYY-MM-DD-<short-kebab-slug>.md`.

## Required sections

1. **Goal** — what shipping this looks like.
2. **Status** — `draft` / `in-progress` / `done`.
3. **Slice list** — each slice = one shippable commit, with file map + exit criteria.
4. **Pre-edit checklist** — `gitnexus_impact` + risk classification.
5. **Acceptance gates** — `dotnet build`, `dotnet test`, `gitnexus_detect_changes`.
6. **Out of scope** — explicit deferrals.

## Lifecycle

- `draft` → `in-progress` → `done`.
- When a slice ships, record it in `docs/CHANGELOG.md`.
- When all slices ship, move the plan to `legacy/Archived plans/` (local archive, gitignored —
  the repo keeps the content in git history; the living state is CHANGELOG + architecture).
- Active in-progress plans must have a pointer row in `docs/active/README.md` for visibility.

> Archive sweep 2026-07-07: all `2026-06-*` plans were moved to `legacy/Archived plans/`.
> This folder now only holds plans that are in-flight or still normative (e.g. decision records).
