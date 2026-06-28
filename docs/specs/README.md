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
