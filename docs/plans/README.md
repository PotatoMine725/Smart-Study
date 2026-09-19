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
2. **Status** — `draft` / `in-progress` / `done`, or a terminal non-shipping state
   (`stopped_at_<gate>` / `superseded` / `abandoned`) — see *Closing an initiative* below.
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
- When a slice ships, record it in `docs/CHANGELOG.md`, and move the plan's **Status** on
  (`draft` → `in-progress`) with the slice row naming its PR and date. A plan still reading
  *"Status: draft. Planning only"* after two slices have merged is the project's only in-repo answer
  to "what shipped?", and it is answering wrong.
- **A PR description is not an in-repo artifact.** *(Added 2026-09-17: the fence slices' mutation
  tables — the only evidence that those tests can fail — existed solely in the PR bodies on GitHub.)*
  Whatever verification a slice produced beyond the suite count (mutants applied and observed RED,
  probes, measurements) must be reachable from the repository: either mirrored into a short execution
  report under `reports/`, or linked from the plan's slice row with the PR number. What is not in the
  repo cannot be grepped, cannot be read offline, and does not survive the branch.
- When all slices ship, move the plan to `legacy/Archived plans/` (local archive, gitignored —
  the repo keeps the content in git history; the living state is CHANGELOG + architecture).
- Active in-progress plans must have a pointer row in `docs/active/README.md` for visibility.
- A plan can also end **without shipping** — stopped at a gate, superseded, or abandoned. That is a
  lifecycle state too, and it needs the same treatment as `done`: see below.

## Closing an initiative updates every artifact in its set, not just the plan

> Added 2026-08-25. Evidence: after the edge-AI encoder initiative was stopped at its S0 gate, the
> execution plan, `active/README.md`, the roadmap and the CHANGELOG were updated — while the
> **proposal still read `SCOPE-FROZEN and ACTIVE`**, the **specification still read `S0 is
> dispatchable now`**, and the plan's own executive summary still read `draft`. The same shape
> recurs: the repeated doc-synchronization reports, and `active/README.md`'s own *"Superseded
> 2026-08-19"* banner. A partly-closed document set is worse than an un-closed one, because the
> stale half now looks authoritative next to the fresh half.

A non-trivial initiative owns **more than one document** — typically a proposal, a ratified spec, an
execution plan, a decision/handoff record, and a report. When it ends, walk the whole set:

1. **List the set first**, then edit. `grep` the initiative's slug across `docs/` — a document nobody
   remembered is exactly the one that will mislead someone in three months.
2. **State the terminal lifecycle in the header of each**, in a word a grep will find
   (`stopped_at_s0`, `superseded`, `closed`, `abandoned`) plus one line saying **why** and linking to
   the record that decided it.
3. **Sweep the bodies, not just the headers.** A fixed header over a body that still says the next
   phase is dispatchable is worse than either alone. Grep each document for present-tense activation
   language — *active*, *dispatchable*, *scope-frozen*, *next action*, *pending*, *authorised* — and
   for each hit decide: **current-state claim** (fix it) or **historical design** (leave it, covered
   by the header banner).
4. **Mark superseded passages in place; never rewrite them into a cleaner story.** The reasoning that
   led somewhere is worth keeping even when the destination changed. A dated *"Superseded YYYY-MM-DD"*
   line under the original preserves both.
5. **Say plainly which phases never ran.** A plan whose later phases were designed but never executed
   must be readable as such **from the top** — otherwise a fresh reader takes a detailed
   never-executed file map for a record of something built.
6. **Do not amend normative text as a side effect of closing.** A ratified spec's requirements are
   not withdrawn by the initiative stopping; they were never exercised. Add closure *metadata*, touch
   no requirement, and say so in the banner — and name explicitly anything that **remains in force**,
   because a reader who assumes a ratified exception died will reopen a settled decision.
7. **Lift anything that outlives the initiative before closing it.** Findings, deferred defects and
   durable lessons must land in `specs/system_roadmap.md` §A.4, `docs/knowledge/`, or `CHANGELOG.md`
   — a live item filed only inside a closed initiative's documents is lost.
8. **Record it in `CHANGELOG.md` even when nothing shipped**, with the heading saying so. A decision
   *not* to build is a change to the project's state, and the alternative is a repository holding a
   ratified spec with no record of what became of it.

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
> Retention 2026-08-25 (edge-AI encoder, `stopped_at_s0`): the **execution plan**, the **proposal**
> and the **owner decision handoff** all stay in `plans/` rather than being archived. Nothing shipped,
> so there is no `architecture/` description for them to hand off to, and for an initiative stopped by
> evidence the record of *why* it stopped is the artifact worth keeping beside the report that closed
> it — the same basis on which the Epic 1 closure-gate record is kept here. A future sweep should
> leave all three. Outcome: [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md).
>
> Two `dev`-only drafts remain here deliberately, not archived: `2026-06-25-dashboard-redesign-native-charts.md`
> (a competing, untested native-XAML chart redesign — never merged, would overwrite the shipped
> LiveCharts-based Dashboard) and `2026-06-25-m8c-study-time-predictor-retrain.md` (the real M8-C
> retrain work shipped on `ui_rf`, not `dev` — this draft was never executed here). Both are owner-
> known; left as-is per prior direction rather than touched during this sweep.
>
> Archive sweep 2026-09-18 (documentation-lifecycle housekeeping — Epic 3 closed a month earlier,
> 2026-08-19, with no sweep yet run): 5 fully-executed Epic 3 execution/design plans moved to
> `legacy/Archived plans/` — `2026-08-04-epic-3-execution-plan.md` (header stale "AWAITING OWNER
> APPROVAL", but Cards A–H shipped, code complete 2026-08-07 per the closing note and
> `system_roadmap.md`'s ledger), `2026-08-07-epic-3-convergence-plan.md` (self-marked `EXECUTED
> 2026-08-07`), `2026-08-10-workload-balancer-stale-chart-fix-design.md` and
> `2026-08-14-workload-balancer-stale-chart-fix-plan.md` (self-marked `implemented` / `done`, shipped
> PR #54; already flagged as an archive candidate a month earlier in
> `docs/reports/2026-08-19-epic3-knowledge-distillation.md:132`, never actioned until now),
> `2026-08-19-e6-cascade-coverage-test.md` (self-marked `EXECUTED 2026-08-20`). Their two
> living-doc citations in `system_roadmap.md`, three in `docs/knowledge/` and the `Reads with` lines of
> six retained Epic 3 decision records were converted to the textual archived pointer used elsewhere
> in this file; dated citations in `CHANGELOG.md` and `docs/reports/` were left untouched, so some
> dated CHANGELOG and report links now point at archived plans (per `docs/README.md` "Amendments, not rewrites":
> dated artifacts are not edited into a cleaner story). **Retained in `plans/`, not archived:** the Epic 3 gate/seam decision records (G2, G3,
> T3.1, T3.8, T3.2, the CP-2 amendment, both T3.3 scope amendments, the T3.9 design/DoR, the M3.0
> allocator classification, the deadline-tier proof) — same class as the Epic 1 decision records kept
> in the 2026-07-26/08-02 sweeps — and `2026-08-10-epic-3-manual-qa-runbook.md`, exempt by the
> explicit runbook rule above. `g3-weight-vector-governance.md` additionally stays because it is the
> load-bearing origin of the still-open roadmap deferred item G3-1.
