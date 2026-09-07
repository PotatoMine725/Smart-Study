# Specs

> Requirements + technical descriptions. Answers: **what** the feature is, **why** it exists, **what** the contract looks like.

## When to add a file here

- New feature or behavior change that needs an agreed-upon definition.
- Data contract (CSV schema, JSON shape, DB column).
- API / interface boundary that multiple modules will depend on.
- UX rule or invariant that must hold across releases (e.g. "quick parser never touches notes/links").

## Naming

`YYYY-MM-DD-<short-kebab-slug>.md` — date is the spec's effective date.

## Required sections

1. **Scope** — in-scope / out-of-scope.
2. **Goal** — one sentence.
3. **Contracts** — interfaces, schemas, invariants.
4. **Acceptance criteria** — checkable list.
5. **Non-goals**.

## Lifecycle

- A spec stays here while the feature is alive in the product.
- If a spec is superseded by a newer one, link the replacement at the top and delete this file once the change ships.
- Don't append "completed" to a spec — once it's shipped, the description belongs in `architecture/` instead.
- **A ratified spec whose initiative stopped before implementation is retained, not deleted, and not
  amended.** *(Added 2026-08-25, from the encoder spec: ratified, then stopped at the S0 research
  gate with nothing built.)* It never earns an `architecture/` description, because nothing shipped,
  so this folder is the only place the agreed contract survives. Add **closure metadata at the top** —
  a terminal lifecycle word, one line on why, and links to the deciding record — and change **no
  requirement text, no requirement ID, no threshold**. Requirements that were never reached are not
  withdrawn. If the banner leaves any related decision standing (a policy exception, a ratified
  constant), **say so explicitly**: a reader who assumes it died will reopen a settled decision, and
  one who assumes the feature shipped will go looking for code that does not exist.
