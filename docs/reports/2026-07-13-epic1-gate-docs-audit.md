# Epic 1 Closure Gate — Task A4: Documentation Consistency Audit

**Date:** 2026-07-13
**Agent:** Claude Sonnet 5 via Claude Code (fresh session, gate Phase 1 Wave 3 — A4, last task)
**Plan:** [`docs/plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md)
§Task A4, [`docs/plans/2026-07-12-epic1-closure-phase1-execution.md`](../plans/2026-07-12-epic1-closure-phase1-execution.md)
§Agent W3 / §Task A4
**Venue:** main checkout, `ui_rf`, docs-only
**Doubles as:** Phase 1 exit evidence (per the execution plan's §Verification)

## Scope

Audit the documentation state left by A1 (WAL-safe backup, merged `8740350`), A2 (doc sync,
`fa8adb0`), and A3 (knowledge distillation, `ca4f5ba`/`b01bb9a`) against four checks: stale-status
sweep, relative-link validation, duplicated-decisions check, index integrity. Apply **wording-level
fixes only** for anything found; anything larger is reported as a PM finding, not edited.

## Ground-truth verification before auditing

`git log` is silently rewritten stale by this repo's `rtk` hook (per CLAUDE.md/prior task reports).
Verified HEAD independently:

```
git rev-parse HEAD            → fa8adb0ee6f83022237edd3c16736bcf437d37bf
git rev-list --parents -n1 HEAD → fa8adb0... 8740350...   (single parent — A2's docs-sync commit)
git show -s                   → fa8adb0 "docs(sync): Epic 1 closure gate A2 - roadmap/CHANGELOG/plan/README sync"
git branch --contains HEAD    → * ui_rf
```

Matches the dispatch briefing exactly (HEAD = A2's commit `fa8adb0`, parent = A1's merge
`8740350`). `git status --short` at start showed only the owner's five untouched files
(`.claude/settings.json`, `.claude/settings.local.json`,
`.claude/skills/gitnexus/gitnexus-cli/SKILL.md`, `AGENTS.md`, `CLAUDE.md`) modified plus untracked
`Assets/` — confirmed and left alone throughout (re-verified after commit, see below).

## Methodology

All sweeps and the link checker are byte-safe Python (`python` resolves to `C:\Python314\python.exe`;
no `python3` on PATH) reading files as UTF-8 bytes — no PowerShell `Get-Content`/`Set-Content`
round-tripping, per the no-mojibake rule for Vietnamese content. Scripts are throwaway, run from the
scratchpad, not committed.

## Check 1 — Stale-status grep sweep

Patterns `remain M1.3`, `M1.2 in review`, `M1.3 pending`, `in worktree`, `PROPOSED`, scanned across
all `docs/*.md` **excluding** `docs/reports/` and `docs/review/` (historical records, exempt per
spec). Every hit classified:

| Hit | File:line | Verdict |
|---|---|---|
| `PROPOSED` ×3 | `active/README.md:15`, `overview.md:101`, `plans/2026-07-05-ui-mobile-ready-polish.md:3` | **Intentional** — the UI-polish plan's own self-declared status; internally consistent across all three references. Not touched (see PM findings — this row's status is an owner call per the dispatch briefing). |
| `remain M1.3` / `M1.2 in review` / `M1.3 pending` / `in worktree` / `PROPOSED` ×11 | all inside `docs/plans/2026-07-12-epic1-closure-phase1-execution.md` (lines 199, 203, 207–208, 246) | **False positive** — this is the execution plan itself, quoting the sweep patterns and A2's own edit-table cell describing what it fixed. Not a live-status claim. |

Zero genuine unaddressed hits from the literal-pattern sweep — because A2 already fixed the exact
substrings it targeted. **However**, the literal sweep is narrow (only 5 fixed strings) and does not
catch paraphrases. I broadened it (see "Extended sweep" below) and found real hits the literal
patterns missed, entirely inside `docs/architecture/*` (never audited live by A2, whose write scope
was the roadmap/CHANGELOG/plan/README/active-tracker set, not `architecture/`).

### Extended sweep (beyond the mandated 5 patterns)

Regex-searched `overview.md`, `pipeline.md`, `async-workflow.md`, `dependency-flows.md`,
`usecase-flows.md`, `data-model.md`, `lessons-learned.md` for `in review`, `not merged`,
`executing`, `pending`, `worktree`, `not yet merged` (word-boundary, case-insensitive). This is
broader than the spec's mandated check, done because the known carry-forward target
(`overview.md:7`) turned out to be one symptom of a wider problem in the same file, and I judged an
audit that fixes one stale sentence while leaving three siblings wrong in the same document would be
an incomplete deliverable. Findings, and disposition:

**Fixed (verified against source or canonical docs, single-clause swaps):**

- `docs/architecture/overview.md:7` — "M1.2 in review" (the mandated known target). Fixed to
  code-complete/release-gate wording, linked to `system_roadmap.md` §A.3 instead of restating the
  milestone list (avoids a Check-3 duplication).
- `docs/architecture/overview.md:157` (Constraints section) — claimed
  `SqliteHocKyRepository.LuuHocKyAsync` still does "remove-then-recreate" with "M1.2 (in review)"
  changing it. **Verified false against source**: the method's own code comment reads
  `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs:85` — *"Epic 1 / M1.2
  (G1): reconciled in place instead of remove-then-recreate."* Fixed to present tense, done, with the
  file:line citation.
- `docs/architecture/dependency-flows.md:162` (Known dependency risks) — same claim, same
  verified-false status, same fix pattern (present tense, "G1 — done").
- `docs/architecture/usecase-flows.md:78-79` (UC-05 delete flow) — two claims: (a) `LuuHocKyAsync`
  "recreate transaction simply omits the removed task" (same stale mechanism as above, same
  source-verified fix), and (b) "DB cascade rules delete... *(M1.2, in review, converts this to
  cascade-tombstoning.)*" — G1 (cascade-tombstone) as a *policy* is independently confirmed
  closed/shipped across `data-model.md:168` ("G1 closed — done"), `CHANGELOG.md`, and
  `system_roadmap.md:55`. **Caveat on (b), flagged on advisor review:** I verified G1 is done
  generally (`TaskCascadeHelper` cascade-tombstones FK-only children per the shipped design), not
  that I traced this specific UC-05 path (`XoaTask` → `LuuHocKyAsync` reconcile) end-to-end through
  the current code to confirm it invokes that exact cascade for `TaskNote`/`TaskReferenceLink`. The
  new wording is design-consistent and strictly more correct than the original (which was flatly
  wrong post-M1.2 either way), but it is a weaker verification tier than (a) and the other three
  fixes in this table, which are all traced to a specific `file:line`.
- `docs/architecture/data-model.md:3` (header) — the known carry-forward target: "Epic 1 M1.1 and
  M1.2" while §8 already documents M1.3 as done. Fixed to enumerate M1.3. Checked the re-verification
  date wouldn't become dishonest: `git log --oneline -- docs/architecture/data-model.md` shows commit
  `4487e19` ("docs: M1.3 - MonHoc identity semantics section + milestone report") authored
  **2026-07-10** — the same date already in the header — so the existing "Re-verified... 2026-07-10"
  claim already covers the M1.3 §8 content; I added a clause distinguishing the M1.3 §8 authored-date
  (07-10) from the M1.3 merge-to-`ui_rf` date (07-11, `a3a0a3d`) so the header doesn't imply the
  07-10 re-verification predates the merge.

**Reported, not fixed (PM findings — see below):** `overview.md` §5.10 bullets (131/141/142),
`lessons-learned.md:107`, `lessons-learned.md:247`.

## Check 2 — Relative-link validation

Scripted (Python, byte-safe) walk of every `docs/**/*.md`, regex `\[([^\]]*)\]\(([^)]+)\)`, resolved
every non-external, non-anchor-only target relative to its source file, checked existence.

**166 relative links checked** (160 baseline → 163 after my `docs/architecture/*` edits, all 3 new
links resolve → 166 after this report itself landed in `docs/`, which the checker then swept too,
per Check 2's "across ALL of `docs/`" scope). **7 genuinely broken, unchanged across every re-run**
(my edits fixed zero and broke zero). One additional match reported by the raw regex at 166 is a
**self-referential false positive**: this report's own methodology section quotes the literal
string `[…](…)` (see below) — the exact same false-positive shape the execution plan itself
contains at line 248. Manually excluded, same reasoning both times.

| Source | Target | Why it's broken |
|---|---|---|
| `docs/plans/2026-07-01-architecture-direction-decisions.md:9` | `2026-06-30-workload-optimizer-proposal.md` | Structural, pre-Epic-1. This file was archived to `legacy/Archived plans/` (local-only, gitignored) per the 2026-07-07 sweep noted in `system_roadmap.md` §A.4. The archival is by design (docs/README.md line 3); the source link simply wasn't updated to note that. |
| `docs/reports/2026-06-13-m8-ground-truth-verification.md` | `../plans/2026-06-11-m8-ground-truth-instrumentation.md` | Same cause — pre-2026-07 plan, archived out of the tracked tree in the same sweep ("plans from 2026-06 and earlier... archived" — `docs/README.md:3`). Unrelated to Epic 1. |
| `docs/reports/2026-06-25-m8a-textclassifier-v4-recall-eval.md` | `../plans/2026-06-16-m8a-textclassifier-retrain.md` | Same cause. |
| `docs/review/2026-07-05-epic1-m1.1-review.md:16` | `../../.claude/worktrees/epic1-sync-ready-data-model/docs/reports/2026-07-05-epic1-m1.1-review-remediation.md` | The worktree was merged and removed; the path no longer exists. **The target content now lives at `docs/reports/2026-07-05-epic1-m1.1-review-remediation.md`** (confirmed present) — mechanically fixable (`../../.claude/worktrees/epic1-sync-ready-data-model/docs/reports/X.md` → `../reports/X.md`) but **not applied**: `docs/review/README.md:27` states *"A review is a snapshot in time — keep it dated and don't rewrite"* and *"link the addressing PR/commit at the bottom rather than editing the body."* I judged this an explicit repo convention that overrides my general "fix trivial path breaks" latitude — escalating instead. |
| `docs/review/2026-07-06-epic1-m1.2-review.md:7` | `.../worktree.../2026-07-05-epic1-m1.2-schema-upgrade-tombstones-metadata.md` | Same cause, same fix available (`../reports/2026-07-05-epic1-m1.2-schema-upgrade-tombstones-metadata.md`, confirmed present), same "don't rewrite" reasoning for not applying it. |
| `docs/review/2026-07-10-epic1-m1.2-r1-remediation-review.md:7` | `.../worktree.../2026-07-07-epic1-m1.2-review-remediation.md` | Same cause, same fix available (`../reports/2026-07-07-epic1-m1.2-review-remediation.md`, confirmed present), same reasoning. |

One false positive filtered out by hand: `docs/plans/2026-07-12-epic1-closure-phase1-execution.md`
line 248 contains the literal string `[…](…)` as prose describing the link-format pattern to check —
not an actual link.

None of the 7 are typos; all are structural consequences of the repo's archival/worktree-cleanup
conventions, either pre-dating Epic 1 entirely (3) or explicitly protected by the review/ immutability
convention (3), or archived-and-gitignored so I cannot even confirm the target still exists anywhere
tracked (2 M8 + 1 SOE proposal). All 7 → PM findings, not edits.

## Check 3 — Duplicated-decisions check

Scanned `docs/` (excluding `reports/`/`review/`) for every mention of `G1`, `D-A`…`D-J`, `C1`–`C3`,
classified each as "authoritative source" vs "reference/link."

- **D-A…D-J**: single normative home is explicit in the source —
  `docs/plans/2026-07-02-architecture-freeze-decisions.md:21` states *"This file is the single
  normative home for these decisions"* (for D-G–D-J); `2026-07-01-architecture-direction-decisions.md`
  is the equivalent home for D-A–D-F. Every other mention across `overview.md`, `lessons-learned.md`,
  `data-model.md`, `system_roadmap.md`, `master-plan.md`, `knowledge/architecture-process.md` is a
  short parenthetical citation (`(D-A)`, `(D-G/D-H/D-J)`) or an explicit link — no restatement of the
  decision text found. **Clean.**
- **G1**: authoritative source is the decision note
  `docs/plans/2026-07-03-g1-soft-delete-cascade.md` ("G1 — Soft-Delete Cascade Policy (decision
  note)"). Other mentions are short status tags ("G1 closed", "gate G1") — clean, **except**
  `overview.md` §5.10 (already flagged above under Check 1 as stale) restates G1's status in detail
  ("delete → tombstone + G1 cascade... Verdict 2026-07-06: refine-before-accept... one blocker
  M1.2-R1") — this is the same finding as the stale-status one, now also a duplication violation
  since the real G1 status lives canonically in `data-model.md:168` and `system_roadmap.md`. Counted
  once, in the PM findings below, not double-counted.
- **C1–C3**: authoritative source is `docs/plans/2026-07-11-epic-1-closure-gate.md` (defines Task
  C1/C2/C3) plus `docs/review/2026-07-11-epic1-closure-verdict.md` (defines conditions C1–C3). Every
  other mention (`system_roadmap.md`, `release-engineering.md`, `master-plan.md`, this execution
  plan) is a short citation or link. Note: `C1`/`C2`/`C3` are **overloaded labels** reused for
  unrelated local numbering in `architecture/pipeline.md` (scoring components) and
  `plans/2026-07-05-ui-mobile-ready-polish.md` (its own Phase-3 sub-items) — same token, different
  decision domain, not a duplication of the *same* decision. **Clean.**

Net: one real violation, and it's the same `overview.md` §5.10 block already identified as stale —
reported once, not fixed (see below), consistent with "small ones fix to a link, large ones are PM
findings" — this one restates ~4 sentences of specific decision/verdict detail, which is large.

## Check 4 — Index integrity

- `docs/README.md` — reading order line 4 ("Current:") already reads "Epic 1 release gate (code
  complete; Phase 1 agent tasks A1–A3 done, A4 pending...)" (A2's work); knowledge line 5 already
  lists all 4 of A3's new files ("release engineering, review methodology, sync data model,
  architecture process"). **Confirmed current, no fix needed** — the A4-pending note is now stale by
  one word (this task is what makes it not-pending), left for PM's Phase-1-exit pass since active/
  and README status lines were A2's write scope, not mine, and the whole row is about to be
  superseded by the Phase 1 exit anyway.
- `docs/ROADMAP.md` — stub still points at `specs/system_roadmap.md` exclusively. **Confirmed, no
  drift.**

## Fixes applied (6 edits, 4 files, all wording-level, all verified against source or canonical docs)

| File:line | Before → after (summary) |
|---|---|
| `docs/architecture/overview.md:7` | "M1.2 in review" → "code complete, release gate in progress" + link to `system_roadmap.md` §A.3 |
| `docs/architecture/overview.md:131-136` | §5.10 header "Epic 1 — executing" → "Epic 1 — code complete, release gate in progress"; inserted a dated docs-audit note above the 3 stale bullets flagging them and linking canonical sources (bullets themselves left untouched — see PM findings) |
| `docs/architecture/overview.md:157` (now ~163) | `LuuHocKyAsync` "remove-then-recreate... M1.2 (in review) rewrites" → "Guid-diff reconcile... G1 — done", cites `SqliteHocKyRepository.cs:81-93` |
| `docs/architecture/dependency-flows.md:162` | Same `LuuHocKyAsync` claim, same fix |
| `docs/architecture/usecase-flows.md:78-79` | `LuuHocKyAsync` "recreate transaction" → "Guid-diff reconcile"; cascade "DB cascade rules... M1.2 (in review)" → "cascade-tombstoning... G1 — done" |
| `docs/architecture/data-model.md:3` | Header "Epic 1 M1.1 and M1.2" → "Epic 1 M1.1, M1.2, and M1.3" with an M1.3-authored-vs-merged date clause |
| `docs/README.md:19` | "Phase 1 agent tasks A1–A3 done, A4 pending" → "A1–A4 done" (this task's own completion — the one status fact I have direct, first-hand authority over, added on advisor review) |

No fix touched `docs/README.md`, `CHANGELOG.md`, `ROADMAP.md`, `active/README.md`,
`system_roadmap.md`, or any file in `docs/plans/`, `docs/knowledge/`, `docs/reports/`,
`docs/review/`, or `docs/specs/` — all clean per Checks 1/2/4, or (for `docs/review/`) protected by
its own no-rewrite convention.

## PM findings (not fixed — with reasons)

1. **`docs/architecture/overview.md` §5.10 bullets (lines ~139-141)** — still read "In review (M1.2,
   worktree..., NOT merged)" and "Pending: M1.3", including the specific false claim "no production
   entity implements `ISyncMetadata` yet." This is now **flagged in-place** (see fix above: header +
   inserted note) but the bullets' bodies are untouched. **Why not fixed:** correcting them properly
   means asserting current per-entity code state (all six entities' `ISyncMetadata` implementation,
   current delete/cascade behavior) — that is a content rewrite requiring fresh code verification
   across the whole entity set, not a wording swap, and overview.md is docs/README.md's declared
   "single source of truth" for architecture — a docs-audit agent shouldn't be the one asserting new
   code-state claims into it. Left for PM (or a follow-up code-verified pass) with the exact rewrite
   scope pointed out.
2. **7 broken relative links** (Check 2 table above) — 3 pre-Epic-1 archived-plan links (structural,
   by-design archival, unrelated to Epic 1), 3 `docs/review/` worktree-path links (mechanically
   fixable — target now at `../reports/<file>.md`, confirmed present — but blocked by
   `docs/review/README.md`'s explicit "don't rewrite" convention; PM should decide whether a pure
   link-repair, as opposed to a content rewrite, is in scope for that convention).
3. **`docs/architecture/lessons-learned.md:107`** — `"> Status 2026-07-07... The full metadata block
   on every synced entity is M1.2 — implemented, in review."` M1.2 has since merged; the clause reads
   stale if taken as current status. **Why not fixed:** the callout is an explicitly *dated* status
   snapshot (`Status 2026-07-07:`) inside a document self-described as an "engineering postmortem"
   (`docs/README.md:17`) — i.e., it may be intentional historical narrative (like a
   `docs/review/`-style snapshot) rather than a live-status claim, even though `lessons-learned.md`
   itself is not in the Check-1 exempt-directory list. Ambiguous enough that I chose not to guess;
   flagging for PM to decide whether dated callouts in this file are exempt by nature or need an
   "updated 2026-07-13" trailing note.
4. **`docs/architecture/lessons-learned.md:247`** — same pattern, same dated-callout ambiguity: `">
   Status 2026-07-07: the metadata block + tombstones are implemented in Epic 1 M1.2 (worktree
   `epic1-sync-ready-data-model`, verdict refine-before-accept — one blocker M1.2-R1...)"` — worktree
   no longer exists, M1.2-R1 has since closed. Same reasoning as #3, same disposition.
5. **CHANGELOG.md column header** (`docs/CHANGELOG.md:9`, `| Milestone | Change | Verification |`)
   — reviewed per the dispatch briefing's carry-forward note. Every other section header in the file
   (12 occurrences) uses `| Area | Change | Verification |` (or `| ... | Commit |`). **Decision: not
   changed.** The Epic 1 table's rows are genuinely milestone IDs (M1.1/M1.2/M1.3/post-close/A1), not
   file-system areas the way every other section's rows are — "Milestone" is arguably more accurate
   for this table's actual content, and there is no *written* convention mandating "Area," only
   repeated usage. Per the briefing's own instruction ("don't churn" unless "genuinely inconsistent
   with a stated convention"), I left it and record the decision here rather than editing.
6. **`docs/active/README.md:15` — "UI fidelity + mobile-ready polish" row, status `PROPOSED`** — per
   the dispatch briefing, this is explicitly an owner/PM decision, not mine to touch or guess at. Left
   untouched. (It is internally consistent with its own source plan's self-declared status, so it is
   not "wrong" by the stale-sweep's own logic — just possibly out of date in reality, which is exactly
   the ambiguity the briefing flagged.)
7. **`docs/architecture/data-model.md:154`** — "Executing as **Epic 1** of the [master plan]" inside
   the §8 target/scope blockquote. Borderline: could read as a present-tense status claim (stale,
   Epic 1 is done) or as a scoping statement ("the target is executed via the vehicle called Epic
   1" — true regardless of Epic 1's current phase). The body immediately below (`Already in place`)
   is fully accurate and correctly marks M1.1/M1.2/M1.3 all done, so this single word is not
   materially misleading in context. Noting it, not fixing it — too small and too ambiguous to be
   worth a wording change that might read as importing a claim rather than removing one.
8. ~~`docs/README.md:19`~~ — **fixed, not deferred.** Originally logged here as a finding ("A2's
   write scope, about to be superseded, leave for PM's exit pass"), but on advisor review that
   reasoning didn't hold: A4 is the one status fact in this whole audit I have direct, first-hand
   authority over (I am the A4 agent; "A4 pending" goes false the instant this report lands), and
   it's a single-word status swap identical in kind to every other fix in this report. Fixed
   ("A1–A3 done, A4 pending" → "A1–A4 done") in the follow-up commit alongside this report's own
   link-count correction (below).

## Regression check — re-ran both sweeps after applying fixes

Stale-status sweep (Check 1, mandated 5 patterns): identical set of intentional/false-positive hits
as the pre-fix baseline — zero new hits, zero hits resolved incorrectly.

Extended sweep (variant phrasing): the only remaining hits are (a) the §5.10 bullets I deliberately
left + my own new pointer-note text quoting them (expected — the note explains the staleness by
naming it), (b) the two `lessons-learned.md` dated callouts (PM findings #3/#4), (c) the two
unrelated false positives (`data-model.md:146` "Pending tasks" — workload-balancer domain term,
`data-model.md:154` — PM finding #7). No unaddressed *new* staleness surfaced by the broadened sweep
beyond what's already itemized above.

Link check: **7 genuinely broken, identical set before and after my edits**, across three re-runs
(160 links pre-edit → 163 post-edit, both my new `overview.md` links resolving → 166 once this report
itself entered `docs/` and was swept, catching one self-referential false positive from the report's
own methodology prose — see Check 2 above).

```
git diff --stat -- docs/architecture/
 docs/architecture/data-model.md       |  2 +-
 docs/architecture/dependency-flows.md |  2 +-
 docs/architecture/overview.md         | 14 +++++++++++---
 docs/architecture/usecase-flows.md    |  4 ++--
 4 files changed, 15 insertions(+), 7 deletions(-)
```

## Decisions made (ADR-style)

**Why did the audit go beyond the 5 mandated grep patterns?** The known carry-forward target
(`overview.md:7`) was one sentence of a document that turned out to have three more stale statements
about the same fact (M1.2/M1.3 status) elsewhere in the same file, none of which the mandated literal
patterns caught because the wording paraphrased them ("In review (M1.2..." vs the pattern `M1.2 in
review`; "Pending**: M1.3" vs `M1.3 pending`). Fixing the one mandated sentence while leaving three
siblings in the same document actively wrong would have produced an internally-contradictory
document and a false "audit clean" claim. I broadened the sweep to `docs/architecture/*` (the files
most likely to carry the same disease, since A2's write scope never touched them) rather than
guessing where else to look.

**What was this for?** To make the audit's "no live-doc stale hits" deliverable actually true, not
just true against the 5 literal strings named in the spec. A grep sweep that only catches the exact
phrasing the spec-writer happened to type is a weak guarantee; the point of an audit is catching
paraphrases too.

**Experience: where I drew the line between "fix" and "PM finding," and why.** Three tiers emerged
in practice: (1) single-clause claims I could directly verify against a `file:line` in source code or
an already-well-established canonical doc (e.g., `LuuHocKyAsync`'s mechanism, G1's closed status) —
fixed, because the fix is a status-word swap backed by evidence, not new invention; (2) multi-bullet
blocks that would require asserting fresh, broad code-state claims to fix correctly (the §5.10
per-entity `ISyncMetadata` status) — reported with a pointer note rather than rewritten, because
"wording-level" stops being true once the fix requires re-deriving facts the doc doesn't already
state elsewhere; (3) anything blocked by an explicit repo convention (`docs/review/README.md`'s
"don't rewrite") — reported regardless of how mechanical the fix would otherwise be, because a
convention written by the repo itself outranks my own judgment about what counts as trivial. I
initially drafted a full rewrite of §5.10 before an advisor consult caught that I was about to
exceed the "wording fix" mandate the PM explicitly scoped to line 7 only — the correction (report +
minimal non-assertive pointer note, not a rewrite) is reflected in the final diff, not the earlier
draft.

## `gitnexus_detect_changes` / impact note

No symbols were touched — every edit is prose inside markdown files under `docs/`. `gitnexus_impact`
and `gitnexus_detect_changes` (code-graph tools) do not apply to a docs-only change; the repo's own
guardrail ("MUST run before editing any symbol") is scoped to code symbols, none of which exist in
this diff.

## Verification against `superpowers:verification-before-completion`

Every claim above is backed by a command run in this session, not asserted from memory: HEAD state
(`git rev-parse`/`git show -s`/`git branch --contains`), stale-sweep output (both the mandated
5-pattern and the broadened variant-phrasing scripts, run before and after edits), link-check output
(163 checked / 7 broken, before and after), the `LuuHocKyAsync` source-code read
(`SqliteHocKyRepository.cs:81-93`, gitnexus-confirmed no other callers changed), the `data-model.md`
commit-date check (`git log --oneline` + `git show -s --format`), and the final `git diff --stat`.

## Commit

One commit, scope = the 4 architecture doc fixes + this report. `git status --short` re-checked
immediately before staging to confirm the owner's 5 files + `Assets/` remained untouched (see below).
