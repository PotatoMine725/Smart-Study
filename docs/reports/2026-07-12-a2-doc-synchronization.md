# Epic 1 Closure Gate — Task A2: Documentation Synchronization

**Date:** 2026-07-12
**Agent:** Claude Sonnet 5 via Claude Code (fresh session, gate Phase 1 Wave 2 — A2)
**Plan:** [`docs/plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md)
§Task A2, [`docs/plans/2026-07-12-epic1-closure-phase1-execution.md`](../plans/2026-07-12-epic1-closure-phase1-execution.md)
§Agent W2 / §Task A2
**Venue:** main checkout, `ui_rf` (docs-only, no worktree — A1 and A3 already landed)

## Scope

Execute the A2 edit table exactly: bring six docs into agreement on Epic 1's true state — **code
complete (2026-07-11), release gate in progress**. Files touched:

- `docs/specs/system_roadmap.md` — §A.2 (4 new shipped rows), §A.3 item 1 (rewrite), §A.4 (NU1903
  advisory)
- `docs/CHANGELOG.md` — new Epic 1 entry
- `docs/plans/2026-07-03-master-plan.md` — dated status banner only, Epic 1 section (D-P6)
- `docs/active/README.md` — replaced the stale Epic 1 row
- `docs/README.md` — reading-order "Current:" line + knowledge-section line
- `docs/architecture/data-model.md` — **verify-only, no edit** (see below)

No other files were touched.

## Ground-truth verification before writing

Before drafting, I re-derived the actual commit graph rather than trusting the dispatch card's
hashes verbatim, because `git log` (even invoked without the `rtk` prefix) is silently rewritten by
the repo's rtk hook and returned a **stale** result during this session — it omitted the current
HEAD entirely and showed `b01bb9a` as the tip. `git rev-parse HEAD`, `git show -s HEAD`, and
`git branch --contains` all agreed on the true state and were used for every fact below:

- `HEAD` = `ui_rf` @ `8740350` — `merge: Epic 1 closure gate A1 — WAL-safe backup fix`, parents
  `b01bb9a` (A3's last commit) and `ac9ec79` (A1's report commit on `gate/a1-walfix`).
- A1 fix commit: `2d04be5` (2026-07-12), `fix(data): checkpoint WAL before DbBackup copy...` — 15
  lines in `DbBackup.cs`, 89/−3 in `DbBackupTests.cs`. Confirmed via `git show --stat`.
- M1.1 merge `3193adf` (2026-07-05); M1.2 merge `e2f8268` (2026-07-10); M1.3 merge `a3a0a3d`
  (2026-07-11, "Epic 1 closed" in the merge subject); post-close fix `101aaa3` (2026-07-11).
- **Flag:** the dispatch card's claimed merge hash `8740350` for A1 matched the real graph exactly
  once I bypassed the `git log` staleness — no discrepancy in the end, but the intermediate `git log`
  output was actively wrong and would have produced a wrong report if trusted. Worth a note to
  whoever maintains the rtk hook: its `git log` filter returned a different (older) tip than
  `git rev-parse HEAD` / `git show -s HEAD` in this session, on this exact repo state.

## Grep-sweep evidence (acceptance criterion)

Ran a byte-safe Python sweep (not PowerShell `Get/Set-Content`, per repo convention) over every
`.md` under `docs/`, classifying `docs/reports/` and `docs/review/` as exempt historical records:

```
LIVE   M1.2 in review -> docs\architecture\overview.md
LIVE   remain M1.3 -> docs\plans\2026-07-12-epic1-closure-phase1-execution.md
LIVE   M1.2 in review -> docs\plans\2026-07-12-epic1-closure-phase1-execution.md
LIVE   M1.3 pending -> docs\plans\2026-07-12-epic1-closure-phase1-execution.md
EXEMPT remain M1.3 -> docs\review\2026-07-11-epic1-closure-verdict.md
---total: 5
```

Run **after** all edits below landed. Two categories of remaining "LIVE" hits, both intentionally
left untouched — see "Findings flagged to PM" below:

1. `docs/architecture/overview.md:7` — a genuine stale claim ("M1.2 in review"), but **outside my
   write scope** (not in the A2 edit table). Not edited.
2. `docs/plans/2026-07-12-epic1-closure-phase1-execution.md` — this is the Phase 1 execution plan
   itself (my own task's spec), and all three hits are the spec's own quoted description of *what
   needed fixing elsewhere* (e.g. `the "remain M1.3" clause at line 51 is stale`), not a live status
   claim about the project. Not edited — it isn't in my write scope, and this plan is the
   orchestration document driving the whole gate, not a status doc.

`system_roadmap.md`, `CHANGELOG.md`, `master-plan.md`, `active/README.md`, and `docs/README.md` —
all five files I actually edited — are confirmed **clean of all three phrases** by the same sweep.

## Link-resolution check

Extracted every `[...](...)` relative link from the five edited files (excluding `http(s)://`) and
resolved each against its containing file's directory. **Zero broken links.**

## What changed, file by file

- **`docs/specs/system_roadmap.md`**
  - §A.2: appended 4 rows (M1.1, M1.2, M1.3, "Epic 1 closeout") summarizing the stamping seam/A6,
    schema-upgrade+tombstones/G1, MonHoc identity/dedup+folded fix, and the post-close/A1 fixes,
    each with its real merge hash and date.
  - §A.3 item 1: rewritten — "Epic 1 done in full" (both metadata/tombstones *and* identity
    semantics shipped), state = *code complete (2026-07-11), release gate in progress*, linking the
    gate doc and the Phase 1 execution plan. Kept the base-snapshot-store / M2.1 note verbatim as
    instructed.
  - §A.4: added the NU1903 (`SQLitePCLRaw`) high-severity advisory line beside NU1904, citing
    closure-verdict carry-forward ledger #8.
- **`docs/CHANGELOG.md`**: new top entry `## 2026-07-05 → 2026-07-13 — Epic 1 (Sync-Ready Data
  Model) — code complete, release gate in progress`, in the existing per-milestone table format
  (M1.1/M1.2/M1.3/post-close/A1), plus a closing paragraph noting the release gate is not yet signed
  and that NU1903 is now tracked.
- **`docs/plans/2026-07-03-master-plan.md`**: added a 6-line dated status blockquote immediately
  under the `## Epic 1 — Sync-Ready Data Model` heading. Nothing else in the file touched — the
  frozen body (Goal/Architecture Impact/Dependencies/Deliverables/Acceptance/Success
  Metrics/Risks) is untouched, per D-P6.
- **`docs/active/README.md`**: replaced the stale Epic 1 row (`"M1.2 in review"`, `M1.3 pending`)
  with an "Epic 1 — release gate" row pointing at the gate doc + Phase 1 execution plan, with
  explicit per-task state (A1 done / A3 done / A2 done-this-commit / A4 pending / Phase 2 not
  started). Updated the section's date header `## Current (2026-07-07)` → `(2026-07-13)`. Left the
  UI-polish row **byte-for-byte unchanged** — see flag below.
- **`docs/README.md`**: reading-order item 4's "Current:" line now says "Epic 1 release gate (code
  complete; Phase 1 agent tasks A1–A3 done, A4 pending)"; item 5's knowledge-section line now names
  all 8 files (added A3's `release-engineering.md`, `review-methodology.md`, `sync-data-model.md`,
  `architecture-process.md`). No other staleness found in this file.

## `docs/architecture/data-model.md` — verify-only, no edit needed

Read the full Epic 1 content: the entity table (line 26, `StudyLog`'s D-I metadata since M1.2),
the cascade-rule sync note (§3, M1.2/G1/M1.2-R1), the repository-layer notes (`LuuHocKyAsync`
Guid-diff reconcile, `TaskCascadeHelper`), and §8 "Future-proofing & sync-readiness" (D-I metadata
block "done", tombstones "done", and — already present — **`MonHoc` identity semantics (Epic 1 /
M1.3) — done, bounded to `MonHoc`**, including the folded `LuuHocKyAsync` fix trace). Every Epic-1
claim in this file matches the shipped code and is already at the "done" state I'd otherwise be
writing in. **No fix applied** — the file was already re-verified against source through M1.3 by
its own prior maintenance (header says "Re-verified against source 2026-07-10... Epic 1 M1.1 and
M1.2"; the M1.3 §8 paragraph was added afterward and is accurate). It does not mention A1's
`DbBackup` WAL fix, but that's correctly out of this document's scope — `data-model.md` documents
entity/schema/pipeline shape, not the backup utility.

## `gitnexus_detect_changes` (before staging)

```
scope: unstaged, repo: Smart-Study
risk_level: low, affected_count: 0, changed_files: 10, changed_symbols: 8
```

The 8 listed symbols are section-header touches in `docs/README.md`, `docs/active/README.md`, and
`docs/plans/2026-07-03-master-plan.md`, plus (unrelated to me) `AGENTS.md`/`CLAUDE.md` sections from
the owner's own untracked edits. `system_roadmap.md`/`CHANGELOG.md` edits landed inside existing
table rows rather than under new headers, so they didn't surface as separate "Section" symbols —
consistent with `changed_files: 10` (my 5 + the owner's 5). `affected_processes: []` — as expected,
docs carry no execution flows. Docs are out of the graph's structural-code scope, matching the
dispatch card's expectation.

## `git status --short` proof — no owner files staged

Before commit, staged exactly:

```
M  docs/CHANGELOG.md
M  docs/README.md
M  docs/active/README.md
M  docs/plans/2026-07-03-master-plan.md
M  docs/specs/system_roadmap.md
```

Unstaged (owner's, confirmed untouched):

```
 M .claude/settings.json
 M .claude/settings.local.json
 M .claude/skills/gitnexus/gitnexus-cli/SKILL.md
 M AGENTS.md
 M CLAUDE.md
?? Assets/
```

Staged with `git add <path>` per file — never `-A`/`.`/`-u`.

## Findings flagged to PM

1. **`docs/architecture/overview.md:7`** still reads "Epic 1 (sync-ready data model) is executing
   now (M1.1 merged; M1.2 in review)." This is a genuine stale claim caught by the grep sweep, but
   `overview.md` is **not** in the A2 edit table (my write scope is locked to 6 files) — leaving it
   for A4's consistency audit, which explicitly covers "no outdated architecture wording" across all
   of `docs/`.
2. **UI-polish row in `docs/active/README.md`** — per the dispatch card, "flag the UI-polish row's
   status to PM if unclear." I checked: `git log` (bypassing the rtk staleness issue via
   `git log -- docs/plans/2026-07-05-ui-mobile-ready-polish.md`) shows exactly one commit touching
   that plan file since it was created — the 2026-07-07 archive-sweep commit that added the row
   itself. No later commits, reports, or reviews reference "ui-mobile-ready-polish" or "UI fidelity"
   anywhere in `docs/` (checked via grep). The current branch is named `ui_rf`, which is suggestive
   but not evidence of active work on *this specific* plan. **Left the row untouched** as instructed
   (byte-for-byte identical to before). PM should confirm whether this effort is still active,
   stalled, or should be archived — I did not have enough signal to decide either way, and the gate's
   Execution Rules prohibit non-release work right now regardless.
3. The rtk hook's `git log` filter returned stale output in this session (see "Ground-truth
   verification" above) — not a docs issue, but worth a maintenance look since it silently rewrites
   plain `git log` invocations per the repo's CLAUDE.md hook contract, and here it disagreed with
   `git rev-parse`/`git show` on the actual branch tip.

## Decisions made (ADR-style)

**D-A2.1 — Keep §A.2's new rows at milestone granularity (M1.1/M1.2/M1.3/closeout), not one
Epic-1-wide row.**
*Why:* Every other multi-part shipment in §A.2 (M8-A/M8-B/M8 arch/M8 Telemetry) is broken out by
milestone, not collapsed into one "M8" row. A single Epic-1 row would bury the per-milestone commit
hashes the acceptance criteria explicitly asked for (merge `a3a0a3d`, post-close `101aaa3`, A1's
fix) behind a wall of text.
*What for:* Future readers (Epic 2/3 kickoff, or the C1 closing note) can jump straight to the
milestone that shipped a given piece of sync metadata without re-deriving it from git log.
*Experience:* Matches the table's existing precedent exactly — no new convention introduced.

**D-A2.2 — Ground-truth every fact from `git show`/`rev-parse`, not from the dispatch card or
`git log`.**
*Why:* The dispatch card's commit hashes (`8740350`, `2d04be5`) turned out to be correct, but I only
know that because I independently re-derived them — `git log`, even called plainly, silently routes
through the rtk hook in this repo and returned a state one merge behind the real `HEAD` during this
session. Trusting either the card or `git log` uncritically risked writing dates/hashes into four
permanent docs that didn't match the actual repository.
*What for:* A2's whole job is to make docs agree with reality; getting the "reality" input wrong
would have shipped confidently-wrong docs, which is worse than the staleness being fixed.
*Experience:* `git rev-parse HEAD` + `git show -s <ref> --format=...` + `git branch --contains`
proved reliable where `git log` (via the hook) did not; worth remembering for the next docs-sync
agent in this repo.

**D-A2.3 — Leave the UI-polish row and `overview.md` untouched; flag both instead of guessing.**
*Why:* The dispatch card explicitly scoped my writes to 6 files and told me to flag the UI-polish
row "if unclear" rather than resolve it myself; `overview.md` isn't in that list at all, and A4's
mandate is exactly "no outdated architecture wording... across `docs/`" — editing it here would
duplicate/pre-empt that pass and risk drifting from A4's own verification method.
*What for:* Keeps the Wave 2/Wave 3 boundary clean (A4 audits the post-A1/A2/A3 state, including
catching what A2 didn't touch) and avoids a docs-sync agent making an unverified judgment call about
whether a UI effort is still active.
*Experience:* Cheaper to leave one accurate "I don't know, here's my evidence" note than to guess
and be wrong in a doc other agents will read as ground truth.

## Verification performed (superpowers:verification-before-completion)

- Ran the stale-phrase grep sweep **after** all edits (not before) and confirmed the 5 edited-scope
  files are clean; pasted the actual command output above, not a paraphrase.
- Ran the link-resolution script over the actual staged files and confirmed zero broken links with
  real output, not an assumption.
- Compared `data-model.md`'s prose against the actual M1.2/M1.3 shipped-code claims already
  documented in it before deciding "no edit needed," rather than skimming and assuming it was fine.
- Confirmed the commit graph via `git rev-parse`/`git show -s`/`git branch --contains`, catching the
  `git log` staleness rather than trusting first output.
- Confirmed staged/unstaged split via `git status --short` immediately before committing, with the
  actual output pasted above.

## Commit

One commit, this task's scope only (5 doc files + this report). No `Co-Authored-By` trailer per
repo convention.
