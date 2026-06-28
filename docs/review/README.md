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

## Lifecycle

- A review is a snapshot in time — keep it dated and don't rewrite.
- If a follow-up gets addressed, link the addressing PR/commit at the bottom rather than editing the body.
- After a review's findings are either fixed or accepted, the file can be deleted; the lessons should already be in `docs/knowledge/`.
