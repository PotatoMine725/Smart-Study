# Documentation Synchronization — Epic 1 Reopen → B4 Released + Analytics Fix

**Date:** 2026-07-24
**Agent:** Claude Opus 4.8 via Claude Code (fresh session, role = document auditor)
**Mission spec:** [`Prompt/Doc-sync.md`](../../Prompt/Doc-sync.md) — sync docs to current code + owner
decisions; preserve history; never infer unsupported facts.
**Venue:** main checkout, branch `ui_rf` — docs-only edits, plus tracking two already-referenced
untracked files (Concern 3). No worktree.

Prior sync anchor: [`2026-07-12-a2-doc-synchronization.md`](./2026-07-12-a2-doc-synchronization.md)
recorded *"code complete, release gate in progress"*; a later sync recorded the intermediate
`B4 = Reopen`. Since then the reopen fix shipped (merge `37f9678`), the owner signed off
**B4 = Released (2026-07-20)**, and a separate pre-existing Analytics stale-render bug was fixed
(`c4291c7`). This pass moves the docs from the *middle* of that cycle to its *end*.

## Scope

Three concerns, separate commits:

1. **Living/state docs → "Epic 1 Released (2026-07-20)"** — `system_roadmap.md` (canonical),
   `active/README.md`, `docs/README.md`, `CHANGELOG.md`, root `README.md`, `master-plan.md` banner.
2. **Architecture docs → fold in the crash-safety layer + R1 FK stamping** — `overview.md`,
   `dependency-flows.md`, `async-workflow.md`, `data-model.md`, `usecase-flows.md`. Three of these
   were **factually contradicted** by shipped code (claimed startup exceptions are "swallowed" when
   global handlers now catch them).
3. **Track two load-bearing untracked files** the committed gate record (`3e00ce0`) already links to
   — `docs/plans/2026-07-20-epic1-reopen-owner-reclosure-runbook.md` and
   `docs/reports/2026-07-20-fix-plan-observation.md` — so those links stop dangling.

Out of scope (unchanged): all code/architecture/feature behavior; `CLAUDE.md` / `AGENTS.md`
(cosmetic GitNexus stat bumps, owner-controlled — only *proposed* additions here); the Analytics
two-section redesign (design-only, backlog); next-cycle artifacts (`Assets/`, `Prompt/`, `tools/`,
`2026-07-24-smart-add-negation-fix-plan.md`).

## Ground-truth verification before writing

All crash-safety and R1 prose was derived from **shipped code**, not the fix-plan's intent (the
Doc-sync precision gate). Read directly this session:

- **`Services/CrashLogger.cs`** — `Log(context, ex)` appends to `%AppData%\SmartStudyPlanner\crash.log`
  and **must never throw** (the catch is deliberately empty — "swallowing here is the point" for a
  last-resort sink). `Observe(task, context)` attaches an **always-run** `ContinueWith`
  (`ExecuteSynchronously`) that logs only when `t.IsFaulted` — explicitly *not* `OnlyOnFaulted`,
  whose continuation cancels on success and would throw when awaited in tests.
- **`App.xaml.cs`** — 3 global handlers wired at the **top** of `OnStartup` (before DB/DI):
  `DispatcherUnhandledException` (Log + Vietnamese dialog + `args.Handled = true`),
  `AppDomain.UnhandledException` (Log), `TaskScheduler.UnobservedTaskException` (Log + `SetObserved()`).
  The three `Task.Run` warmups: the **two ML warmups still silent-catch** (empty catch, offline-first);
  **`MatureAsync` now logs** via inline `catch (Exception ex) { CrashLogger.Log(...); }`
  (`App.xaml.cs:96-100`).
- **`Observe` call sites** (grep-confirmed): `WeightOptimizerViewModel.cs:123`
  (`LogWeightChangeAsync`, "WeightChangeLog") and `QuanLyTaskViewModel.cs:219`
  (`LogDifficultyLabelAsync`, "DifficultyLabelLog"). These are the two telemetry writes the "F2
  nuance" wraps.
- **R1 MaMonHoc stamping** — `QuanLyTaskViewModel.ThemTask` (`cs:192-194`) sets
  `MaMonHoc = MonHocHienTai.MaMonHoc` on create. `SqliteHocKyRepository.LuuHocKyAsync` reconcile
  **heals** a `Guid.Empty` FK from navigation position (`cs:118-121`) then **fails loud** —
  `throw new InvalidOperationException("Reconcile: task '…' references MonHoc … not present …")`
  (`cs:191-195`) — on a genuinely unknown FK.
- **`Data/AppStartup.cs`** — the DB bootstrap (`EnsureCreated` `cs:16`, `ALTER TABLE IsSeeded` `cs:22`,
  dev-seed `UPDATE` `cs:31`, `TelemetrySchema.EnsureTables` `cs:36`, `SyncSchema.EnsureColumns` `cs:44`)
  moved out of `App.xaml.cs` into this helper as part of the R2 refactor — invalidating the old
  `App.xaml.cs:28` / `:31-39` citations in `overview.md §6`.

**Correction applied (plan text vs. code):** the approved plan said `CrashLogger.Observe` wraps
`MatureAsync`. The code shows `MatureAsync` uses an **inline** `try/catch → CrashLogger.Log`, while
`Observe` wraps the **two telemetry sites** (`WeightChangeLog` + `DifficultyLabelLog`). All prose
follows the code.

## Grep-sweep evidence

Swept `docs/` + root `README.md` for the stale phrases `B4 = Reopen`, `awaiting (owner) approval`,
`gated, not yet declared`, `code complete`, `289 tests`, `336 pass/tests`, `swallowed exceptions`,
`tombstones land with M1.2` — classifying `docs/reports/*`, `docs/review/*`, and dated
`docs/plans/*` as **exempt historical records**.

**Live hits found and fixed** (living/architecture docs):
- `overview.md:7` (intro) — "code complete, release gate in progress" → Released 2026-07-20. *(This
  is the exact line the 2026-07-12-a2 sync flagged as out-of-scope and deferred; resolved now.)*
- `overview.md:131-136` (§5.10) — "code complete … release is gated, not yet declared" → Released,
  with the gate arc preserved via cross-ref.
- All Concern-1 files (`system_roadmap.md`, `active/README.md`, `docs/README.md`, `CHANGELOG.md`,
  root `README.md`, `master-plan.md`) — already landed in the prior work of this task.
- All Concern-2 startup prose (`overview.md §6`, `dependency-flows.md §2`, `async-workflow.md §2/§6/§8`)
  — "swallowed exceptions" corrected to the precise split (2 ML silent-catch / `MatureAsync` logs);
  `dependency-flows.md:126` "tombstones land with M1.2" → shipped.

**Deliberately left (not stale, or exempt):**
- `CHANGELOG.md:17` "336 pass" — correct per the file's own convention (line 5: *"test count at the
  time of merge"*); R2 merged at 336, the very next row shows the Analytics fix at **337**.
- `system_roadmap.md:59` "B4 = Reopen" — inside "**Release-gate history (preserved)**", past-tense
  narrative; Released is stated at lines 54/70.
- `knowledge/system-design.md:115` "Exceptions are swallowed there" — narrowly **accurate**: it
  describes `MLModelManager.InitializeAsync`, which *is* still a silent-catch warmup. Left as-is
  (flagged below for an optional cross-ref).
- `plans/2026-07-05-ui-mobile-ready-polish.md:191` "289 tests" — a point-in-time count inside a
  *proposed* future plan's argument; out of scope, left (flagged below).
- All `docs/reports/*` and `docs/review/*` per-date records (e.g. `2026-07-19-epic1-reopen-execution.md`
  "336 pass") — correct for their date; preserve-history.

## Link-resolution check

- The committed gate record `3e00ce0` links to the two Concern-3 files — tracking them (Concern 3)
  turns those from dangling `??` references into resolved in-tree links.
- New/updated cross-refs added this pass all resolve: `overview.md` → `system_roadmap.md §A.3`,
  `dependency-flows.md:126` → `data-model.md §3–§4`, `data-model.md` FK note → `system_roadmap.md §A.4`,
  `async-workflow.md` §6 → §8. No dangling references introduced.

## What changed, file by file

### Concern 1 — living/state docs
- **`docs/specs/system_roadmap.md`** (canonical) — §A.2: new row *"Epic 1 released"* (R1/R2 +
  Analytics fix `c4291c7`, merge `37f9678`, 337 pass). §A.3 item 1: rewritten from "reopened, awaiting
  approval" to **"Epic 1 shipped in full and Released (2026-07-20)"** with the release-gate history
  preserved as a labelled block and the post-release backlog (Analytics redesign, `MucDoCanhBao`)
  appended. §A.4 `MucDoCanhBao` — **unchanged** (still accurate).
- **`docs/active/README.md`** — date header → `2026-07-20`; Epic 1 row → **Released** with R1/R2 +
  Analytics + owner sign-off, "Gate Execution Rules **lifted**"; added the Analytics-redesign QUEUED
  row; row marked for archival on the next sweep.
- **`docs/README.md`** — "Current:" → Epic 1 **Released (2026-07-20)**; Next = Analytics two-section
  redesign + UI mobile-ready polish.
- **`docs/CHANGELOG.md`** — header window extended to `→ 2026-07-20`, retitled **Released 2026-07-20**
  with the reopen/re-close arc; added rows for the Analytics B4-gate fix (`c4291c7`, **337 pass**) and
  B4 = Released (`3e00ce0`); closing paragraph rewritten to "Released 2026-07-20".
- **root `README.md`** — test count **289 → 337**; Epic 1 moved from "Coming Next" to a shipped row in
  "What Has Been Built" (+ "Released 2026-07-20"); "Coming Next" reworded to build on the now-shipped
  sync-ready data model. No invented version bump.
- **`docs/plans/2026-07-03-master-plan.md`** — dated status banner only → "**Status (2026-07-20):
  Released.**" with merge `37f9678` + owner sign-off; frozen planning body untouched.

### Concern 2 — architecture docs
- **`docs/architecture/overview.md`** — intro line 7 → Released; §5.10 heading + intro → Released
  (gate arc cross-ref'd); **§6 runtime composition** rewritten: new item 1 = the crash-safety layer
  (3 handlers + `CrashLogger`), citations repointed to `Data/AppStartup.cs`, warmup nuance corrected
  (2 ML silent-catch / `MatureAsync` logs).
- **`docs/architecture/dependency-flows.md`** — §2 startup: new step 1 = global handlers + `CrashLogger`;
  warmup "exceptions swallowed" → precise split; `SyncSchema.EnsureColumns` added. Line 126:
  "tombstones land with M1.2" → **shipped**, cross-ref to `data-model.md`.
- **`docs/architecture/async-workflow.md`** — header re-verification note (2026-07-24, reopen layer);
  §2 new step 0 = crash-safety handlers + warmup correction; §6 telemetry → **observed fire-and-forget**
  (`WeightChangeLog` + `DifficultyLabelLog` via `Observe`); §8 rule 4 → documents the observable
  fire-and-forget pattern + the "never a bare `_ = SomethingAsync()`" convention.
- **`docs/architecture/data-model.md`** — §4: new bullet for R1 FK integrity (ThemTask stamping +
  reconcile heal-from-navigation + fail-loud), plus a blockquote noting the **latent `MucDoCanhBao`
  ctor gap** cross-ref'd to `system_roadmap §A.4`.
- **`docs/architecture/usecase-flows.md`** — UC-02 step 3 records the `MaMonHoc` stamping at task
  creation.

### Concern 3 — track two files (pending, see Commit)
`git add` (per-file) only `docs/plans/2026-07-20-epic1-reopen-owner-reclosure-runbook.md` and
`docs/reports/2026-07-20-fix-plan-observation.md`.

## Findings flagged to PM

### Missing Documentation (listed, NOT authored — owner decision)
1. **Standalone Analytics stale-render fix execution report.** Evidence is currently folded into the
   [diagnosis report](./2026-07-20-analytics-step2-diagnosis.md) Status + the
   [fix plan](../plans/2026-07-20-analytics-stale-render-fix.md). No dedicated execution report exists
   (Part 1 337-pass RED→GREEN; Part 2 XAML visibility shipped, **visual toggle pending owner re-run**).
2. **Dedicated Epic 1 reclosure/release report.** `B4 = Released` currently lives **only** as an
   addendum inside `docs/plans/2026-07-11-epic-1-closure-gate.md`; the `2026-07-20-owner-epic-1-redecision.md`
   spec the reclosure runbook proposed was **never created**. The reopen *execution* is reported
   ([`2026-07-19-epic1-reopen-execution.md`](./2026-07-19-epic1-reopen-execution.md)), but the release
   sign-off itself has no standalone report.

### Owner-authored inputs lacking the mandated ADR section (flag, leave as-is)
`docs/reports/2026-07-15-GUI-test-observations.md` and `docs/reports/2026-07-20-fix-plan-observation.md`
lack the "Decisions made" ADR section (a post-2026-07-07 report convention), but they are **raw
owner-authored evidence**, not agent reports. Retrofitting ADR structure onto owner observations
would distort the record — **left as-is**.

### Minor, left untouched
- `knowledge/system-design.md:115` — accurate for the ML warmup, but could gain a one-line cross-ref
  to the new global handlers for completeness. Not edited (avoids expanding scope into the knowledge
  base).
- `plans/2026-07-05-ui-mobile-ready-polish.md:191` "289 tests" — stale count inside a *proposed*
  plan's argument; left (out of the Concern-1 file set; changing a count inside a historical argument
  risks distorting it).

## Stale Documentation (summary)

Every file in Concerns 1–2 → action = **update** (all applied). **None archived.** The earlier
"do not release yet" hold → **marked superseded** (not deleted) in `CHANGELOG.md`, `system_roadmap §A.3`,
and `active/README.md`. The release-gate arc is preserved in every doc via labelled history +
cross-reference.

## Knowledge Distillation — the observable fire-and-forget pattern

- **Problem.** A telemetry write launched fire-and-forget (`_ = SomethingAsync()`) that faults
  disappears — no await, no continuation, nothing logged. During B4 this class of silence let a real
  crash (the unstamped-`MaMonHoc` reconcile throw) kill the process with no trace.
- **Root cause.** `App.xaml.cs` had **no global exception handler**, and the waived telemetry writes
  (`MatureAsync`, `LogDifficultyLabelAsync`, `LogWeightChangeAsync`) were bare unawaited tasks — a
  fault on any of them was unobservable by design.
- **Evidence.** `App.xaml.cs:23-38` (handlers now wired first); `CrashLogger.Observe`
  (`Services/CrashLogger.cs:39-42`, always-run `ContinueWith`); the two `.Observe(...)`-wrapped call
  sites (`WeightOptimizerViewModel.cs:123`, `QuanLyTaskViewModel.cs:219`); `MatureAsync`'s inline
  `catch → CrashLogger.Log` (`App.xaml.cs:96-100`). Reopen commits `b0061e7` + `c18e1e7` (R2).
- **Decision.** Keep fire-and-forget for pure enhancements (they must never block/fail the user's
  action), but make faults **observable**: wrap in `CrashLogger.Observe`, or use an inline
  `try/catch → CrashLogger.Log`. Global handlers backstop everything else.
- **Convention.** *An unawaited task must either be `.Observe(...)`-wrapped or carry an inline fault
  log — never a bare `_ = SomethingAsync()` that swallows.* Now documented in `async-workflow.md §8
  rule 4`.

## Convention Updates (PROPOSED only — not modified)

1. **`async-workflow.md §8`** — the observable-fire-and-forget rule is now written there (part of
   Concern 2). *This is the recommended canonical home.*
2. **`CLAUDE.md` / `AGENTS.md`** — *proposed* one-line pointer: "Unawaited tasks: `CrashLogger.Observe`
   or inline fault-log; never bare `_ = …Async()`. See `docs/architecture/async-workflow.md §8`."
   Presented as a proposal — these files are owner-controlled and were **not** modified.

## Decisions made (ADR-style)

**D1 — Follow shipped code over the plan text where they disagreed (`Observe` scope).**
*Why:* The plan said `Observe` wraps `MatureAsync`; the code shows `MatureAsync` uses inline
`CrashLogger.Log` while `Observe` wraps the two telemetry writes. Doc-sync's whole job is to match
reality. *What for:* Readers get the true seam (inline vs. `Observe`) instead of an approximation.
*Experience:* Verifying the three sites individually — not trusting "three observed sites" as a
uniform claim — is what caught the split.

**D2 — Fix the two prior-deferred live contradictions in `overview.md` (line 7 + §5.10), though the
plan named only §6.**
*Why:* Both are genuine live "code complete / gated" claims in the same architecture doc I was already
editing, directly contradicted by the shipped Released state; the 2026-07-12-a2 sync explicitly
deferred line 7 to "a later consistency pass." This is that pass. *What for:* Leaves no
self-contradicting Epic-1 status inside one file. *Experience:* A within-file grep after the §6 edit
is what surfaced them — section-scoped edits can miss same-file intro/summary claims.

**D3 — Repoint the `App.xaml.cs:28`/`:31-39` citations to `Data/AppStartup.cs`.**
*Why:* The R2 refactor moved DB bootstrap into `AppStartup.EnsureDatabaseReady`; those line numbers now
hold the global handlers, so the citations were doubly wrong. *What for:* A reader following the
citation lands on the real code. *Experience:* The same commit that added the crash layer moved the
bootstrap — fixing both together keeps §6 internally consistent.

**D4 — Flag, don't fix, the knowledge-base line and the proposed-plan test count.**
*Why:* `system-design.md:115` is narrowly accurate (ML warmup does swallow), and the "289 tests" line
sits inside a proposed plan's historical argument — neither is a live status contradiction, and both
sit outside the Concern-1/2 file set. *What for:* Avoids scope creep and preserves the plans-area
convention that dated arguments aren't retro-edited. *Experience:* Cheaper to leave an evidenced note
than to silently alter a historical argument.

## Verification performed

- Re-read `CrashLogger.cs`, `App.xaml.cs`, both `Observe` call sites, `ThemTask`, the
  `LuuHocKyAsync` reconcile, and `AppStartup.cs` **before** writing any crash-safety/R1 prose.
- Ran the stale-phrase grep sweep over `docs/` + root `README.md` **after** the edits; classified live
  vs. exempt hits (output summarized above), fixing the two live `overview.md` hits and leaving the
  exempt/accurate ones with a documented reason.
- Checked link resolution for the gate record's two now-tracked files and every cross-ref added this
  pass — zero dangling.
- Confirmed the "missing" reports genuinely don't exist via a `docs/{reports,plans}/2026-07-*` glob
  (only diagnosis + fix-plan for Analytics; no reclosure/release report; no `…redecision.md` spec).
- **Pending before "done":** `git status --short` + `gitnexus_detect_changes` to prove **docs-only**
  scope immediately before each commit (see Commit).

## Commit

Three concern-separated commits, no `Co-Authored-By` trailer (repo convention):

1. `docs(sync): Epic 1 Released — roadmap/CHANGELOG/README/active/master-plan → B4=Released`
   (Concern 1).
2. `docs(architecture): fold crash-safety layer + MaMonHoc stamping into architecture docs`
   (Concern 2).
3. `docs(track): reopen reclosure-runbook + fix-plan-observation + this sync report`
   (Concern 3 — the two untracked gate-linked files, plus this report so it lands after every
   artifact it references).

Owner confirmation requested between concerns per the step-by-step working convention.
