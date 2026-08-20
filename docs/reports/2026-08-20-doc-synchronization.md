# Documentation Synchronization — Epic 3 SOE: code complete → manual gate CLOSED

**Date:** 2026-08-20
**Agent:** Claude Opus 5 via Claude Code (role = document auditor)
**Mission spec:** [`Prompt/Doc-sync.md`](../../Prompt/Doc-sync.md) — sync docs to current code +
owner decisions; preserve history; never infer unsupported facts.
**Venue:** main checkout, branch `docs/epic3-knowledge-distillation` (PR #58). Docs-only edits; no
production file touched.

Prior sync anchor: [`2026-07-24-doc-synchronization.md`](./2026-07-24-doc-synchronization.md), which
moved the docs to *"Epic 1 Released 2026-07-20"*. Everything since — post-Epic-1 stabilization
(closed 2026-08-02), all of Epic 3 (code complete 2026-08-07), the automated and manual QA gates
(closed 2026-08-19), and the E6 follow-up (2026-08-20) — had reached `reports/` and `plans/` but
**never reached the canonical state documents**. `CHANGELOG.md` ended at 2026-08-02;
`system_roadmap.md` §A.2 had no Epic 3 row; three status surfaces still read *"Epic 1 Released;
next: Analytics redesign."*

This pass was queued as **follow-up 1** of the
[knowledge distillation](2026-08-19-epic3-knowledge-distillation.md) and deliberately not done
there, because writing canonical history from secondary summaries is the drift that cycle's lessons
are about.

---

## Scope

| In scope | Out of scope |
|---|---|
| `CHANGELOG.md`, `specs/system_roadmap.md`, `README.md` (docs + root), `active/README.md` | Any file under `SmartStudyPlanner/` or `SmartStudyPlanner.Tests/` |
| `architecture/*` — the code-normative set | `reports/`, `review/` — dated artifacts; amend or leave, never rewrite |
| Proposals for `CLAUDE.md` / `AGENTS.md` | Editing them (mission spec §5: propose only) |

---

## Ground-truth verification before writing

Every substantive claim below was checked against the code or the git object store, not against a
prior document. The three that changed what got written:

| # | Checked | Method | Result |
|---|---|---|---|
| 1 | Is placement still least-loaded? | Read `WorkloadServiceImpl.cs:204-211` | **No.** Earliest-feasible since T3.3. Three `architecture/` files still said otherwise |
| 2 | Does anything call `Optimize`? | `grep -rn 'IScheduleOptimizer\|ScheduleOptimizer\|SoeWeights' --include=*.cs SmartStudyPlanner/` + `ServiceLocator.cs` | **No production caller.** Two doc-comment mentions only (`AppDbContext.cs:42`, `TelemetrySchema.cs:82`); no DI registration. `BalanceWorkloadStage.cs:41` still calls `IWorkloadService.GenerateSchedule` |
| 3 | What is the suite count? | `dotnet test` on this branch | **487 passed**, matching [the E6 report](2026-08-20-e6-cascade-coverage-test.md). Not taken from any document |

Claim 2 mattered most. The roadmap's existing wording was scoped *"as of this HEAD"* — a claim that
silently expires. HEAD has moved 20+ commits since it was written, so it was **re-verified rather
than carried forward**, and the replacement text is now dated and pinned to a SHA (`8982af7`) so the
next reader knows exactly what the scope is.

All 40 commit SHAs cited in the new CHANGELOG section were checked to exist with
`git cat-file -e <sha>^{commit}`. One did not (`63a2e79`, a transposition of `63aa79d`) and was
corrected before commit.

---

## The finding that changed the shape of this pass

`architecture/` is described in [`../README.md`](../README.md) as *"current state of the code (single
source of truth)"*. Three files in it carried this claim, in three phrasings:

> placement is least-loaded-day, deadline-blind — **the Epic 3 SOE fixes this**

Both halves were wrong at HEAD, and the obvious repair would have introduced a third error.

- **"least-loaded" was wrong** — T3.3 shipped earliest-feasible placement on 2026-08-06 (`5197784`).
- **"the SOE fixes this" was wrong** — the SOE has no production caller, so at runtime it fixes
  nothing.
- **"placement is now deadline-aware" would also have been wrong.** The deadline filter T3.3 added
  is **provably output-inert**: tier-1 and tier-2 return the same day for every input
  ([proof](../plans/2026-08-06-deadline-tier-provably-inert.md)). The improvement came from ordering
  by *date*, not from consulting the deadline. The branch is retained only because it stops being
  inert once day-capacity becomes non-monotonic.

Every corrected site therefore says three things — earliest-feasible, filter present but inert
(citing the canonical proof rather than restating it, as the code comment at
`WorkloadServiceImpl.cs:200-203` explicitly asks), and the seam unwired.

---

## What changed, file by file

### Concern 1 — `architecture/` (commit `8982af7`)

| File | Why updated | Summary of changes |
|---|---|---|
| `usecase-flows.md` | UC-08 described the pre-T3.3 allocator and cited a line range (`77-91`) that no longer exists | Placement policy rewritten (now `204-211`); added the 2026-08-14 capacity-staleness badge (`RenderedCapacityHours` / `IsScheduleStale`); added the standing limitation. **SOE seams deliberately excluded** from the participants lists with the reason stated inline — listing them would claim a runtime participation they do not have |
| `dependency-flows.md` | §4's scheduling chain ended in "least-loaded day — deadline-blind; Epic 3 fixes" | Chain line corrected; **new §4b** draws the SOE box with *"no caller"* on its inbound edge, and explains why `OptimizerRunLogs` is expected empty (gate B2: 0 rows **is** the pass) |
| `data-model.md` | `OptimizerRunLogs` was **missing entirely** from the telemetry table, and the scheduler path was stale | Added the full `OptimizerRunLogRow` row (grain: one row per checkpoint per pass per call; `RunId` grouping; why `Reason` is null for C₀; entity lives in `Services/Soe/`, DbSet at `AppDbContext.cs:45`) + the empty-is-correct note. Scheduler path corrected. **Also fixed a pre-Epic-3 staleness:** the sync-metadata contract still read *"no production entity implements it at HEAD — M1.2 (in-flight)"*; M1.2 shipped 2026-07-10 on all six entities |
| `overview.md` | Folder tree omitted `Services/Soe/` — a 17-file subsystem was invisible | Tree entry added; **new §5.11** stating what the SOE is *and is not*, split into reachable (T3.3) vs. unreachable (T3.9 + seams). Also corrected the `ui_rf` branch scope on the UI-polish plan (merged PR #49, 2026-07-26) |
| `lessons-learned.md` | L4 and L8 made forward-looking claims that Epic 3 resolved | **Dated follow-ups appended, originals untouched.** L8's own named candidate (*best-feasible-prefix over per-stage checkpoints*) is what G2-1 ratified — recorded because a lesson whose prediction came true is worth more intact than tidied. L4's demanded inversion tests were written (T3.4) |

### Concern 2 — canonical ledgers (commit `ae0bbb3`)

| File | Why updated | Summary of changes |
|---|---|---|
| `CHANGELOG.md` | Newest entry was 2026-08-02; the epic that shipped after it was absent | New `2026-08-04 → 2026-08-19` section: one row per gate (G2, G3) and card (T3.8, T3.1, T3.2, T3.3, T3.9, T3.4, T3.7), plus convergence, the automated gate, the stale-chart fix, the manual gate closure, and the E6 test. Led by a scope line the table cannot carry. Suite chain **391 → 470 → 475 → 487**, each figure taken from the report that measured it |
| `system_roadmap.md` §A.1 | GitNexus counts were superseded and nobody noticed | `dd41685` re-measured at `c74962a` (5,166 / 11,794 / 133) and updated `CLAUDE.md` + `AGENTS.md` but **missed this file**, which still held 4,254 / 9,791 / 120. Corrected, with the supersession stated and the scope kept honest — not re-measured today, and the tooling currently reports the index stale against HEAD. Version row annotated: `1.5.0`, **not bumped** for Epic 3 |
| `system_roadmap.md` §A.2 | No Epic 3 row existed | Added stabilization, Epic 3, and Epic 3 QA rows + the zero-call-site caveat as a block quote, since a ledger cell cannot carry it |
| `system_roadmap.md` §A.3 | Headed *"Next up"* while items 1–2 were shipped; item 2's call-site claim was undated | State line added; item 2 marked SHIPPED and given what the gate closed on (including that **E1–E4 closed on an owner ruling, not a written observation**) and the E6 follow-up; item 3 marked as the first unstarted epic |
| `system_roadmap.md` §A.4 | Three pieces of deferred work existed in no durable list | Added **G3-1**, the **E6 surviving mutant**, and the un-integrated analytics redesign |

### Concern 3 — status surfaces (commit `b682852`)

| File | Why updated | Summary of changes |
|---|---|---|
| `README.md` (root) | *"391 tests"*; SOE listed under "coming next" while already merged | 487 tests; Epic 3 + stabilization rows in the milestone table; **a plain-language scope note** — the engine is merged but not wired, so it changes nothing a user sees. This file is read by people who will not open the roadmap, so the caveat is stated in words, not by link |
| `docs/README.md` §4 | *"Epic 1 Released; Next: Analytics redesign"* | Full current state, ending with *"Nothing is in progress"* |
| `active/README.md` | Banner read *"Epic 3 (SOE) is next"* | New banner; **old one marked superseded and kept** — its sequencing claim (E1 → E3 → E2 → E4, and that *"Epic 2 entry criteria"* names a gate set, not an order) is still correct. Table re-dated; deferred items enumerated so they are not mistaken for active work |

---

## Missing Documentation

Listed, **not authored** — each needs an owner decision or evidence this pass does not have.

### M-1 — A durable measurement of what `Optimize()` actually improves *(recommended)*

The largest gap on the board, and the one that gates the G3-1 decision.

Every recorded optimizer metric is a **safety** metric — D-H holds (0 breaches / 230 items),
determinism, latency — and closing-note finding **F3** states that success metric #4 (objective delta
vs. baseline) is **unevaluated for 90% of the corpus**. So the question *"what does wiring this buy a
user?"* has no filed answer, while the decision to wire it or not is pending.

A throwaway probe over the 230-item corpus was run on 2026-08-20 in the session that produced this
report. Recording it here **as an observation with its provenance stated, not as a filed result**:

```
ITEMS                   : 230
SCHEDULE CHANGED BY OPT : 36
VIOLATIONS  before/after: 862 / 862
OVERDUE MIN before/after: 50480 / 50480
SCORE SUM   before/after: 437.8589 / 450.0417
SCORE improved/worse    : 36 / 0
TERMINATION Terminated_FixedPoint: 229 · Terminated_PassLimit: 1
PASSES histogram        : 1p=194, 2p=24, 3p=11, 4p=1
```

> **Scope and uncertainty, stated plainly.** *Observation:* this is the recorded stdout of a run that
> happened. *What it appears to show:* the optimizer never repairs a deadline violation (862 → 862)
> and never reduces overdue minutes; its entire contribution is load-smoothing — a **+2.8% objective
> gain on 15.7% of scenarios**, and it never makes the objective worse. *Why it is not filed as a
> result:* **the instrument was deleted.** It is not in the repository, not reproducible by anyone
> reading this, and nothing here was independently re-verified. Treated as a **strong reason to
> measure properly**, not as the measurement. Anyone acting on G3-1 should re-run it as a committed
> test first.

**Recommended:** land the probe as a real corpus test (the harness it used,
`SoeOptimizeCorpusMetrics`, is already in the repo) and file the result as an execution report. That
converts the figures above from recollection into evidence and answers F3 at the same time.

### M-2 — ADR / decision record for G3-1

*"Wire the optimizer, or leave it unwired"* is a live architectural decision with no decision
record — it is currently only a bullet in roadmap §A.4. Given the freeze convention (D-A…D-J), it
warrants a note in `plans/`. **Blocked on M-1**: deciding it without effect-size data is guessing.

### M-3 — The missing analytics mockup

`docs/assets/analytics-ui-package/README.md` cites an interactive mockup
`Analytics Redesign Proposal.dc.html` as *"still in this project."* **It is not in the repository.**
The approved visual reference for the queued redesign may exist only outside version control. Flagged
in `active/README.md`; recovering it is an owner action.

### M-4 — No Epic 3 release/version decision

`1.5.0` is unchanged across `SmartStudyPlanner.csproj`, root `README.md` and roadmap §A.1. Whether an
epic that shipped a scheduling-behaviour change warrants a bump is an owner call. Annotated in §A.1
as *"not bumped"* so the silence is at least visible; **not decided here**.

---

## Stale Documentation

| Document | Verdict | Action |
|---|---|---|
| `architecture/usecase-flows.md`, `dependency-flows.md`, `data-model.md`, `overview.md` | **Partially outdated** — contradicted the code | **Updated** (Concern 1) |
| `architecture/lessons-learned.md` L4, L8 | **Partially outdated** — forward-looking claims now resolved | **Appended**, dated follow-ups; originals preserved |
| `CHANGELOG.md`, `system_roadmap.md`, both `README.md`s, `active/README.md` | **Partially outdated** | **Updated** (Concerns 2–3) |
| `docs/ROADMAP.md` | **Obsolete by design** — retired 2026-07-01, pointer stub | **Leave.** Working as intended |
| `knowledge/review-methodology.md` §"a green check must be shown to go red" | **Not stale — correctly historical.** Describes the WP-4 mutation campaign in past tense, including *"reversing the least-loaded day sort"*, a sort that no longer exists | **Leave.** It is a dated account of a campaign that happened, and never claims to describe current code. Rewriting it would destroy history to fix nothing |
| `SmartStudyPlanner_Project_Overview.md` | **Already synchronized** | **Leave.** A structural narrative that makes no version, count, or epic-status claim — nothing in it contradicts HEAD |
| `reports/README.md`, `plans/README.md`, `review/README.md` | **Already synchronized** — updated by PR #56 | **Leave.** Convention documents, not indexes; no per-file listing to drift |
| All files under `reports/` and `review/` | **Dated artifacts** | **Leave.** Correct by dated amendment only; none required one this pass |
| `CLAUDE.md`, `AGENTS.md` | See Convention Updates | **Propose only** — mission spec §5 |

---

## Knowledge Distillation

**Already done, and this pass deliberately adds nothing.** The Epic 3 cycle was distilled on
2026-08-19 into `knowledge/qa-gates.md` and `knowledge/review-methodology.md`
([report](2026-08-19-epic3-knowledge-distillation.md)), and amended on 2026-08-20 when the E6
follow-up **falsified the most citable sentence in the original distillation** — the claim that
writing the E6 test would convert a passing manual observation into a standing guard. It did not; the
acceptance bar went unmet, and the articles were corrected in place rather than left to harden.

Re-distilling here would duplicate those articles at a lower fidelity, written from summaries instead
of from the campaign. Per the mission spec's allowance to state why a document is already
synchronized: **the knowledge base is current, and the freshest thing in it.**

One observation from *this* pass is worth recording, and belongs in an existing article rather than a
new one — offered as a proposal, not written (see C-3):

> **Problem.** `architecture/` claimed *"placement is deadline-blind — Epic 3 fixes this."* Both
> halves were false, and the obvious repair — *"placement is now deadline-aware"* — would have been a
> third false claim.
> **Root cause.** A document that describes present behaviour *and* promises a future fix has two
> failure modes with one shape. When the fix lands, the promise reads as satisfied, and the natural
> edit is to flip the tense. But shipping the named work does not establish that the named problem
> was solved by it: T3.3 improved placement by ordering chronologically, while the deadline filter it
> added is provably inert, and the SOE that was to "fix this" has no caller at all.
> **Evidence.** `WorkloadServiceImpl.cs:204-211` + the inertness proof + a zero-hit grep for
> production call sites.
> **Decision.** State the mechanism, not the intent; cite the proof rather than restating it; keep
> the "not wired" fact adjacent to the "shipped" fact wherever either appears.
> **Convention.** A forward-looking claim in a code-normative document must name **what would make it
> true**, so a later reader can check *that* rather than infer satisfaction from a changelog entry.
> Where a doc says "X will fix this", closing X requires re-verifying the original claim — not
> deleting it.

---

## Convention Updates (PROPOSED only — not modified)

### C-1 — `CLAUDE.md` / `AGENTS.md` GitNexus counts drift silently *(recommended)*

`dd41685` re-measured the index and updated both files, but missed `system_roadmap.md` §A.1, which
then carried superseded figures for 11 days. The same number lives in three files with no mechanism
tying them together.

**Proposed:** keep the count in **one** place (§A.1) and have `CLAUDE.md`/`AGENTS.md` link to it
rather than restate it. If the `gitnexus analyze` marker block must stay in those two files, add a
line to §A.1 saying it is the mirror, so the next re-measure has an obvious third target. *(Related
known behaviour: consecutive analyses of an unchanged tree differ by ~0.1% on symbols/relationships,
so exact equality across files is not the goal — a shared, dated source is.)*

### C-2 — Line-number citations in `architecture/` go stale invisibly

`usecase-flows.md` cited `WorkloadServiceImpl.cs:77-91` for a block that now lives at `204-211`. The
citation still parsed as plausible and pointed at unrelated code. **Proposed:** cite a symbol or a
named comment marker (e.g. *"the T3.3 placement block"*) with the line range as a hint, not the
anchor.

### C-3 — Add the distilled lesson above to `knowledge/architecture-process.md`

Not written — it is a convention proposal, and §5 of the mission spec says propose, do not modify.

### C-4 — `docs/README.md`'s "Artifact types" table has no row for a doc-sync report

This report is an execution report by naming convention but a recurring audit by function. Minor;
noted so the next pass does not have to re-derive it.

---

## Verification performed

| Check | Result |
|---|---|
| `dotnet test` on this branch | **487 passed / 0 failed / 1 skipped**, 6 projects |
| Production tree untouched | `git diff --name-only origin/dev...HEAD` → every path under `docs/` or root `README.md`; **no `.cs`/`.xaml`** |
| Every cited commit SHA exists | 40/40 after correcting `63a2e79` → `63aa79d` |
| Every new relative link resolves | All targets present on this branch — including `knowledge/qa-gates.md` and the distillation report, which exist **only on this branch**, not on `dev` (see D-1) |
| Zero-call-site claim | Re-verified by grep + `ServiceLocator.cs` read at `8982af7`, not carried forward from the roadmap |
| Stale-claim sweep | `grep -rn "least-loaded\|deadline-blind"` over `docs/` — remaining hits are the corrected text, the roadmap's own "least-loaded → earliest-feasible" description, and the two historical passages in `review-methodology.md` deliberately left alone |
| `gitnexus_detect_changes` | **Not run.** Every change is Markdown; no symbol, call edge or execution flow is touched. The index also reports itself stale against HEAD independently of this work. Recorded as skipped rather than silently omitted |

---

## Follow-ups

1. **M-1** — land the corpus probe as a committed test and file the effect-size result. Gates M-2.
2. **M-2** — write the G3-1 decision record once M-1 exists.
3. **M-3** — locate or re-create `Analytics Redesign Proposal.dc.html`.
4. **M-4** — owner call on a version bump for Epic 3.
5. **C-1** — de-duplicate the GitNexus count across three files.
6. Carried forward, unchanged: the **E6 surviving mutant** (`DetectChanges()` ordering — pin it or
   prove it redundant; **do not delete the call**).

---

## Decisions made (ADR-style)

### D-1 — Sync on the PR #58 branch rather than a fresh branch off `dev`

**Why it had to be made.** The natural instinct for a new concern is a new branch, and this work is a
different concern from the knowledge distillation already open in PR #58.

**What it's for.** Two facts made a `dev`-based branch the wrong venue. `docs/README.md` is already
modified by #58, so a second branch would conflict with my own unmerged work. More decisively,
`knowledge/qa-gates.md` and the distillation report **do not exist on `dev`** — every cross-reference
this pass writes to them would have been a dead link until #58 merged, and dead links in canonical
state documents are exactly the defect class this pass exists to remove.

**Experience for future development.** Branch by *dependency*, not by concern, when the concerns are
documentation. Separation of concerns is preserved where it costs nothing — in the commits (`8982af7`
/ `ae0bbb3` / `b682852`), each independently revertible. **Consequence the owner should know: PR #58
is now broader than its title, and its description needs updating before merge.**

### D-2 — Re-verify the zero-call-site claim instead of copying it

**Why it had to be made.** The roadmap already said it. Copying it would have been faster and looked
identical in the diff.

**What it's for.** The existing wording was scoped *"as of this HEAD"* — a scope that silently
expires. HEAD had moved 20+ commits. A claim carried forward on trust and re-dated is
indistinguishable, in the finished document, from one that was checked — which is precisely how a
correct statement becomes a false one without anybody editing it.

**Experience for future development.** **A relative scope marker is a claim with an expiry date.**
When syncing, treat *"as of this HEAD" / "currently" / "at present"* as expired by default and
re-verify, then replace with an absolute anchor. Every such claim in this pass now names a date and a
SHA.

### D-3 — Correct the placement text to the *mechanism*, not to the intent

**Why it had to be made.** *"Placement is now deadline-aware"* is the obvious fix, reads well, and is
false.

**What it's for.** T3.3's deadline filter is provably output-inert; the gain came from chronological
ordering. Writing "deadline-aware" would have replaced a stale false claim with a fresh one and made
the document *harder* to correct later, because it would read as recently maintained.

**Experience for future development.** **Shipping the work named in a promise does not discharge the
promise.** When a document says *"X will fix this"* and X ships, re-verify the original claim rather
than inferring satisfaction. The tell is that the natural edit is a tense change: if flipping the
tense is all that seems required, the claim was probably never checked.

### D-4 — Record the probe figures as an observation with the instrument named as deleted

**Why it had to be made.** The measurement answers the open F3 question and is the strongest input to
the G3-1 decision. Three options: write it as a result, omit it entirely, or record it with its
provenance.

**What it's for.** Writing it as a result would launder an unreproducible one-off into project
record — the exact failure the QA-gate lessons warn about. Omitting it would delete the most decision-
relevant finding available, leaving G3-1 to be decided on nothing. Recording it *with the instrument
named as deleted* keeps the signal and prevents the citation. The figures were recovered from the
session's recorded tool output rather than restated from memory, so the transcription is faithful even
though the run is not repeatable.

**Experience for future development.** **A measurement without a retained instrument is a lead, not a
result** — and the difference must be visible in the document, not just in the author's head. When a
throwaway probe finds something decision-relevant, the finding is that *the real measurement is worth
funding*.

### D-5 — Append dated follow-ups to `lessons-learned.md` instead of correcting L4 and L8

**Why it had to be made.** Both lessons made claims Epic 3 resolved — L8 said *"SOE implementation is
blocked"* (it is not) and L4 said *"the allocator eventually needs deadline-aware placement"* (T3.3
came, in an unexpected form). Editing them to current fact would have been cleaner and shorter.

**What it's for.** L8 named *best-feasible-prefix over per-stage checkpoints* as a candidate
mechanism, and that is exactly what G2-1 ratified. **A lesson whose prediction came true is evidence
that the reasoning method works**, and that evidence exists only while the original prediction and
its date are still legible. Rewriting it into a description of the outcome would destroy the only
proof the lesson earned.

**Experience for future development.** Preserving history is not only about not losing facts — it is
about not destroying the *record of a prediction* — and it applies with most force where a document
turned out **right**, because that is where the temptation to merge prediction into outcome is
strongest.

### D-6 — Leave `review-methodology.md`'s least-loaded passages alone

**Why it had to be made.** A sweep for stale placement claims flagged them, and they describe a sort
that no longer exists.

**What it's for.** They are a past-tense account of the WP-4 mutation campaign — a record of what was
mutated, when, and what it showed. The campaign happened against that code. "Correcting" it would
falsify a historical record to remove a non-contradiction, since the article never claims to describe
current code.

**Experience for future development.** A stale-claim sweep produces candidates, not verdicts. Before
editing a hit, ask what the document is *for*: code-normative documents must track the code; dated
accounts must track what happened. **The same sentence is stale in one and correct in the other.**

### D-7 — State the next epic without choosing it

**Why it had to be made.** With E1 and E3 closed, the master plan's order (E1 → E3 → E2 → E4) points
at the LAN-sync epic. Writing *"next: Epic 2"* would be a faithful reading of the plan of record.

**What it's for.** It would also have functioned as a decision. G3-1 is unscheduled and could
reasonably be sequenced first, and the owner has not made that call. Both `active/README.md` and
roadmap §A.3 now state the order, name G3-1 as a live alternative, and say explicitly that the choice
is open and belongs to the owner.

**Experience for future development.** **Documentation that records a plan can silently make a
decision.** When a sync arrives at a fork the owner has not resolved, write the fork — not the
default branch of it.
