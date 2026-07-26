# Epic 1 Closure Gate — Task A3: Knowledge Distillation

**Date:** 2026-07-12
**Agent:** Claude Sonnet 5 via Claude Code (fresh session, gate Phase 1 Wave 1 — A3)
**Plan:** [`docs/plans/2026-07-11-epic-1-closure-gate.md`](../plans/2026-07-11-epic-1-closure-gate.md) §Task A3, [`docs/plans/2026-07-12-epic1-closure-phase1-execution.md`](../plans/2026-07-12-epic1-closure-phase1-execution.md) §D-P4
**Venue:** main checkout, `ui_rf` (docs-only, new files — no worktree needed; A1 runs concurrently in a separate worktree)

## Scope

Write 4 new engineering-knowledge articles in `docs/knowledge/` distilling the durable lessons of
Epic 1 (Sync-Ready Data Model), matching the existing distilled style (`debugging.md`,
`system-design.md`). Per D-P4, flat topic files — not the gate doc's suggested folder structure —
since the existing knowledge base is 4 flat files and 5 folders for ~8 files would fragment a small
knowledge base. Cross-link one-liners added to existing knowledge files where topics touch.
`docs/README.md` and `docs/CHANGELOG.md` are explicitly out of scope for this task (owned by A2).

## Findings

Source material was read from the committed gate/verdict docs, all four Epic 1 milestone reviews,
all Epic 1 milestone implementation reports, the architecture `lessons-learned.md` postmortem, the
G1 decision note, and `data-model.md`. Four articles were written:

### `docs/knowledge/release-engineering.md`
- **Problem:** `DbBackup.CreateBackup` was a bare `File.Copy`, missing any pending WAL content;
  discovered because a real pre-Epic-1 dev database (5,402 `StudyLog` rows) existed, contradicting
  a prior milestone's stale "no real database exists" claim.
- **Why hard:** every fixture in the test suite was cleanly closed before assertions ran, so no
  fixture could ever produce a pending-WAL state — the gap was invisible to every test that existed.
- **Wrong assumptions:** "no real pre-Epic-1 DB exists," "a file-copy of a `.db` is a backup,"
  "clean-fixture tests validate the backup path."
- **How solved:** verdict condition C3 (checkpoint-before-copy fix + live-WAL test + supervised
  first real upgrade with reference row counts), plus distilled the T1.8 upgrade-seam mechanics
  (idempotent `EnsureColumns`, row-count-primary `MigrationReporter`, independent-backfill lesson).
- **Principle:** "the first real run is a milestone, not a formality."
- **How to avoid:** ask whether any fixture ever leaves state open/dirty the way production can.

### `docs/knowledge/review-methodology.md`
- **Problem:** how do you catch defects that a plausible-sounding review, or a plausible-sounding
  code trace, would let through?
- **Why hard:** each anti-pattern *feels* sufficient in isolation — a correct-sounding prediction, a
  green self-report, a review that already blessed the exact line that later escaped, a fix that's
  "just" folded into someone else's milestone, a fix that "closes" the one reported case.
- **Wrong assumptions:** a prediction is evidence; a self-report is a reproducible result; "reviewed
  once" means "no downstream consequence anywhere"; a folded-in fix inherits its host milestone's
  review depth; the one call site you fixed is the whole invariant.
- **How solved:** six named disciplines distilled from the four milestone reviews — RED-first
  discriminating tests, independent re-verification, escape-rate framing (commit `101aaa3`),
  reproduce-before-escalating (the M1.3 protocol), folded-fix scrutiny (Option A), and completeness
  checks against `OnModelCreating` as ground truth (M1.2-R1).
- **Principle:** each section states its own — e.g. "a prediction is not a finding," "one minor
  escape across ~1,900 lines and four reviews is a healthy rate, not a failure."
- **How to avoid:** concrete per-section guidance (see article); all six sections follow the same
  problem/why-hard/wrong-assumption/fix/principle/avoid rubric.

### `docs/knowledge/sync-data-model.md`
- **Problem:** four related durable mechanics — `Rev` cannot order events across devices (L6); a
  single stamping seam; cascade-tombstone breaking silently in two ways once deletes became soft;
  EF's cascade fixup reading a stale tracked snapshot; semantic identity vs. Guid identity.
- **Why hard:** each looked correct in a narrower context (`Rev` on one device; the schema's cascade
  config; a `.Include()`-loaded collection) and only broke crossing that context (across devices;
  across delete mechanisms; across snapshot-vs-live-collection timing; across "has an ID" vs. "means
  the same thing").
- **Wrong assumptions:** enumerated per-subsection (see article) — "higher `Rev` wins," "the
  cascade config became irrelevant," "collection mutation is tracking-state mutation," "an ID fixes
  identity."
- **How solved:** the three-role metadata split (D-I); `SyncStamper` converting every
  `Deleted`-state entry via the single seam + `TaskCascadeHelper` for FK-only children; reparent +
  forced `DetectChanges()` before `Remove()`; `MonHocIdentity.Normalize` behind one comparer, applied
  at both prevent-at-source and read-side dedup sites.
- **Principle:** never let one scalar answer two questions; ground-truth completeness checks beat
  fixing the one reported site; snapshot tracking needs `DetectChanges()`, not just collection
  edits; prevent-at-source and read-side dedup are different defenses, need both.
- **How to avoid:** redirect future "simplify by comparing `Rev`" attempts here; re-derive the full
  child set from `OnModelCreating` for any new delete path; assert real row counts, not "no
  exception"; grep every consumer of a field before calling a dedup "centralized."

### `docs/knowledge/architecture-process.md`
- **Problem:** an SOE proposal's central mechanisms (deadline-as-score, absolute feasibility,
  row-level merge, `Rev`-ordering) were largely agreed before an adversarial review found them
  unsound.
- **Why hard:** each assumption looked correct in isolation, and doc-cross-checking as a review
  method produces real (if narrower) findings, which is what makes it feel sufficient even though it
  never touches source.
- **Wrong assumptions:** distilled as a general pattern — "true of the schema/doc/isolated
  requirement, false of the running system/composed requirement" — with L1–L9 as the concrete
  instances, linked rather than restated.
- **How solved:** the `file:line`-verification standard, the lag-vs-fork discriminator, the
  ADR-style decision records (D-G–D-J), and explicitly leaving L8 open rather than forcing it.
- **Principle:** four ownership rules (code normative; boundary→constraint, preference→score only;
  relative not absolute feasibility; decide tracking + merge granularity together).
- **How to avoid:** require `file:line` citations, run adversarial review before freezing, check
  conjunctions of guarantees explicitly, mark unresolved questions open, and re-verify old premises
  at each later milestone boundary (linking to F5 in `release-engineering.md` as a live example of a
  premise that quietly expired).

Cross-link one-liners were added (links only, no content duplication) to:
- `docs/knowledge/debugging.md` — `EnsureCreated` entry → `release-engineering.md`.
- `docs/knowledge/system-design.md` — the `OnModelCreating` cascade note → `sync-data-model.md`;
  the `gitnexus_detect_changes` note → `review-methodology.md`.
- `docs/knowledge/programming.md` — the Fluent API cascade note → `sync-data-model.md`.

No verbatim copying from reports or reviews: every named decision (D-A…D-J, G1, the M1.1/M1.2/M1.3
review verdicts, the closure verdict's F1–F5/C1–C3) is linked to its authoritative source document
rather than restated, per the gate's single-source-of-truth requirement for decisions.

## Verification

- Confirmed `docs/knowledge/*.md` now contains 8 files (4 pre-existing + 4 new); no other directory
  touched except this report.
- Re-read each new article after writing to confirm it independently answers all six gate
  questions (problem / why hard / wrong assumptions / how solved / principle / how to avoid) — each
  article's subsections follow that explicit six-part rubric.
- Confirmed every named decision/finding (D-A…D-J, G1, F1–F5, C1–C3, L1–L9, 101aaa3) resolves to a
  markdown link to its source document rather than being restated as fact.
- Confirmed `docs/README.md` and `docs/CHANGELOG.md` were not opened or edited.
- `git show --stat HEAD` after commit confirms only the 4 new knowledge files, the 3 cross-linked
  existing knowledge files, and this report are in the commit — no owner-local files
  (`.claude/settings*.json`, `AGENTS.md`, `CLAUDE.md`, `Assets/`) leaked in.

This is a docs-only task; no code symbols were touched, so `gitnexus_impact`/`detect_changes` do
not apply and were not run (per this task's explicit instruction, to avoid disturbing the index the
concurrent A1 worktree agent shares).

## Follow-ups

- `docs/README.md`'s knowledge-base index (Task A2) should add entries for the 4 new files.
- Task A4 (audit) should verify these articles' six-question coverage and source-link integrity
  independently, per the gate's review-then-audit sequencing.
- `docs/knowledge/machine-learning.md` was reviewed for cross-link opportunities and found to have
  no natural topical overlap with Epic 1's sync/release/review/architecture-process material; no
  cross-link was added there.

## Decisions made (ADR-style)

**D1 — Flat files in `docs/knowledge/`, not the gate doc's suggested folder structure.**
*Why:* the phase-1 execution plan's D-P4 already locked this (owner-confirmed 2026-07-12): the
existing knowledge base is 4 flat files, and 5 folders (`architecture/`, `implementation/`,
`reviews/`, `releases/`, `engineering/`) for what would be ~8 total files fragments a small,
easily-grep-able knowledge base for no navigational benefit.
*What for:* keeps `docs/knowledge/` scanning cheaply (one `ls`, one grep) as it grows, and matches
the convention new contributors already see in the 4 existing files.
*Experience:* the gate doc's structure was explicitly marked "suggested," which turned out to be
exactly the right amount of authority — a later, more-informed decision (D-P4) overrode it cleanly
without any conflict, because it was never stated as binding.

**D2 — Distill principles and link decisions/sources; never restate a decision as new prose.**
*Why:* the task brief explicitly warned that an auditor will check each decision has exactly one
authoritative source — restating a decision in a knowledge article would create a second,
driftable copy of the same fact.
*What for:* keeps `docs/knowledge/` genuinely distilled (durable *principles*, not a second history)
while keeping the milestone reports/reviews as the sole historical record of *what was decided and
when*.
*Experience:* this made several articles read more like an index-with-commentary than free
narrative in places (especially `architecture-process.md`, which leans almost entirely on links to
`lessons-learned.md` and the two decision files) — the right trade-off given the audit requirement,
even though it costs some standalone readability. A reader who wants the full derivation must follow
the link; the article gives them the reason to.

**D3 — Six-part rubric (problem / why hard / wrong assumption / how solved / principle / how to
avoid) applied per named lesson, not once per article.**
*Why:* several articles (`review-methodology.md`, `sync-data-model.md`) bundle multiple genuinely
distinct lessons under one topic umbrella; answering the six gate questions only once at the
article level would have flattened lessons that have different problems, different wrong
assumptions, and different fixes into one blurred narrative.
*What for:* lets a future reader (or the A4 auditor) verify six-question coverage per lesson, not
just per file, and mirrors the rubric `lessons-learned.md` itself already uses
(assumption → why it looked correct → evidence → principle → impact) — reusing a rubric the
codebase already trusts rather than inventing a new one.
*Experience:* this made the articles longer than a single flowing narrative would have been, which
is the deliberate cost of verifiability — a six-question compliance check should not require
inferring intent from prose.
