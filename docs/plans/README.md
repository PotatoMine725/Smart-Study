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

## Runbooks live here too, and have a different shape

A **runbook** (`…-runbook.md`) tells a human exactly how to execute a manual procedure. It is not a
plan and is exempt from the six sections above — kept in this folder because there have only ever
been a handful of them and moving them would break the reports that cite them; a `runbooks/` folder
becomes worth it if the count grows. Its required shape is:

1. **Preconditions** — build to use (with a provenance check: does the binary's mtime match the
   build you think you're testing?), test data to prepare, what is explicitly *not* in scope.
2. **Numbered scenarios** — each with the exact steps, the expected result, and **what a failure
   looks like**, written so the check is capable of failing.
3. **Pass / fail criteria, stated in advance** — before anyone runs it, not after.
4. **A blank result table** — one row per scenario, left empty until the run happens.

Two rules keep a runbook usable more than once:

- **Results do not live in the runbook.** The run's outcome belongs in an evidence record
  (`reports/…-observation.md`, in the tester's own words) and its interpretation in a QA report or
  closing note. The runbook may carry a pointer row to those; it must not become the record.
- **A scenario that cannot be executed is a defect in the runbook**, fixed where it was written —
  not filed against the application. Before handing one over, check that every destructive or
  state-changing step names an action production code actually implements.

## Lifecycle

- `draft` → `in-progress` → `done`.
- When a slice ships, record it in `docs/CHANGELOG.md`.
- When all slices ship, move the plan to `legacy/Archived plans/` (local archive, gitignored —
  the repo keeps the content in git history; the living state is CHANGELOG + architecture).
- Active in-progress plans must have a pointer row in `docs/active/README.md` for visibility.

> Archive sweep 2026-07-07: all `2026-06-*` plans were moved to `legacy/Archived plans/`.
> This folder now only holds plans that are in-flight or still normative (e.g. decision records).
>
> Archive sweep 2026-07-26: with Epic 1 Released (2026-07-20), its shipped execution/QA plans
> moved to `legacy/Archived plans/` (9 files — the Epic 1 execution/closure/reopen plans + the
> analytics stale-render fix plan). Retained here: the closure-gate record
> (`2026-07-11-epic-1-closure-gate.md`, holds the B4=Released decision), the architecture
> decision/freeze records, the forks-proposals record (open SOE Decision 1), and the master plan.
>
> Archive sweep 2026-08-02 (post-stabilization consolidation): `2026-07-24-smart-add-negation-fix-plan.md`
> shipped 2026-07-26 (346 pass, see `CHANGELOG.md`) → moved to `legacy/Archived plans/`. Also
> removed 4 stale `docs/plans/` drafts left over on `dev` whose content had already shipped and
> already had byte-identical copies in `legacy/Archived plans/` from an earlier sweep on another
> branch (`2026-06-11-m8-ground-truth-instrumentation.md`, `2026-06-16-m8a-textclassifier-retrain.md`,
> `2026-06-27-analytics-ui-redesign.md`, `2026-06-27-monhoc-baitap-ui-redesign.md`). The post-Epic-1
> stabilization plan (`2026-07-27-post-epic1-stabilization.md`) is superseded and closed (all six
> packages landed, Epic 2 entry criteria 12/12) but stays here rather than archived — its Progress
> table is still the single-table summary of the phase; see its own Lifecycle line.
>
> Two `dev`-only drafts remain here deliberately, not archived: `2026-06-25-dashboard-redesign-native-charts.md`
> (a competing, untested native-XAML chart redesign — never merged, would overwrite the shipped
> LiveCharts-based Dashboard) and `2026-06-25-m8c-study-time-predictor-retrain.md` (the real M8-C
> retrain work shipped on `ui_rf`, not `dev` — this draft was never executed here). Both are owner-
> known; left as-is per prior direction rather than touched during this sweep.
