# Review

> Code reviews. Answers: **is this change safe to ship**, **what are the risks**, **what should we follow up on**.

## When to add a file here

- Senior review of a finished slice or PR before merge.
- Architecture review of a god object before extraction.
- Security review of a new external surface.
- Independent second-opinion on a high-risk refactor.

## Naming

`YYYY-MM-DD-<short-kebab-slug>.md` — slug usually names the symbol or area reviewed.

## Required sections

1. **Scope reviewed** — files, commits, or symbols.
2. **Verdict** — `ship` / `ship-with-followups` / `block`.
3. **Strengths** — what worked.
4. **Risks / watchouts** — concrete issues with severity.
5. **Suggested follow-ups** — non-blocking improvements.
6. **Final notes** — overall judgment in one paragraph.

## Verdict vocabulary is per artifact type

Use the vocabulary of the thing being decided, and use it verbatim so a later reader can grep for it:

| Artifact | Verdict values |
|---|---|
| Review of a change (this folder) | `ship` · `ship-with-followups` · `block` |
| QA / gate report (`reports/`) | `PASS` · `PASS WITH FINDINGS` (list them) · `FAIL` (list blockers) |
| Closing note (`reports/`) | contract `met` / `met with accepted limitations` / `not met` |

A partially-executed run is never summarised as a pass — record which items were skipped and why.

## Evidence and judgement are separate

State what was independently re-derived (a suite you re-ran yourself, a file you read at its final
state) apart from what you concluded from it. "Independently verified" means the reviewer produced
the evidence on their own run, at the state actually being accepted — not that they read someone
else's evidence and found it plausible. Where a finding rests on reasoning rather than a measurement,
label it as reasoning; a claim that sets another package's severity gets measured first
(see [`../knowledge/review-methodology.md`](../knowledge/review-methodology.md)).

## Lifecycle

- A review is a snapshot in time — keep it dated and don't rewrite. Corrections are appended as a
  dated amendment, the same rule reports follow.
- If a follow-up gets addressed, link the addressing PR/commit at the bottom rather than editing the body.
- **When a later ruling closes a question the review raised, mark it at the finding.** *(Added
  2026-09-17, from the Slice-2 fence review: both of its owner questions were ruled within a day, and
  one engineering recommendation was ruled the **other way** — explicit registration only, where the
  review had suggested a fallback. The merged review carries no sign of it.)* Add a one-line dated
  pointer beside the finding (`> **Closed <date>** — ruled <outcome>; see <ruling record>.`), and
  have the ruling record link back. Without both halves, a rejected engineering recommendation reads
  as live advice to whoever finds the review next — and reviews are exactly what a later slice reads
  to understand why the code looks the way it does.
- After a review's findings are either fixed or accepted, the file can be deleted; the lessons should already be in `docs/knowledge/`.
