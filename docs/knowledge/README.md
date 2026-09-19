# Knowledge

> Durable lessons. Answers: **what should a future engineer remember**, independent of the change
> that taught it.
>
> Everything here outlives the code it came from. A file that only makes sense while a particular
> branch is open belongs in `reports/` or `review/`, not here.

## Extend before you add

Articles are **topic-level and flat** — one file per subject area, not one per incident or per
milestone. Before creating a file, ask *"is this lesson's **home** new?"*, not *"is this lesson
new?"*. In the two distillation passes so far, most lessons belonged in an article that already
existed; a new file was written only when the concept had a clearly distinct scope (QA gates in
2026-08-19, decision governance in 2026-09-17).

The second occurrence of a lesson is valuable precisely because it sits *next to* the first — that
is what turns an anecdote into a pattern. Copying the same lesson into several files destroys that
and doubles the maintenance.

## Two shapes, and each file keeps the one it has

| Shape | Used by | Form |
|---|---|---|
| **Narrative** | `qa-gates.md`, `review-methodology.md`, `incident-investigation.md`, `architecture-process.md`, `sync-data-model.md`, `ml-experimentation.md`, `decision-governance.md`, `release-engineering.md` | `##` per lesson, each answering: **Problem** · **Why it was hard** · **Wrong assumption** · **How it was solved** · **Principle** · **How to avoid it next time**. Not every section is mandatory; the Principle is. |
| **Terse index** | `programming.md`, `system-design.md`, `debugging.md`, `machine-learning.md` | `###` entry under a topic heading, a few lines each, code-shaped. |

**Do not introduce a second shape into a file.** A lesson appended in the other form makes the whole
file harder to read than either form applied consistently, and it breaks file-wide checks ("does
every lesson state a principle?"). If a lesson genuinely needs the other shape, that is a signal it
belongs in a different file.

## What does not go here

- Execution narration ("added 13 tests to X") — that is a report.
- Anything already stated in `architecture/` (current state) or `CHANGELOG.md` (what shipped).
- A ruling or a spec. Those are normative and live in `specs/` / `plans/`; knowledge articles
  **link** them and explain the reusable reasoning, never restate them as fact.

## Narrative articles end with the same two sections

- **See also** — sibling articles, each with one line naming the *boundary* between them, so the next
  distiller knows where a new lesson belongs.
- **Sources** — the primary artifacts the lessons were drawn from, with a pointer into the specific
  section. A knowledge claim whose evidence cannot be re-found is not maintainable.

The terse index files carry inline links instead and mostly have no trailing sections; that is
deliberate, not an omission to fix. `machine-learning.md` is the one that carries a **See also**
without a **Sources** list — add one the next time it is extended, rather than as a standalone
tidy-up.

## Current articles

| File | Subject |
|---|---|
| [`architecture-process.md`](architecture-process.md) | Deciding and freezing architectural direction |
| [`decision-governance.md`](decision-governance.md) | Holding decisions: who may rule, how a ruling is recorded, what an agent does when sources disagree |
| [`debugging.md`](debugging.md) | Bugs that bit us; reading this codebase efficiently |
| [`incident-investigation.md`](incident-investigation.md) | Observation → diagnosis → classification of findings |
| [`machine-learning.md`](machine-learning.md) | ML.NET architecture, lifecycle, confidence policy |
| [`ml-experimentation.md`](ml-experimentation.md) | Running an ML experiment whose answer you can trust |
| [`programming.md`](programming.md) | C# / WPF / EF Core rules |
| [`qa-gates.md`](qa-gates.md) | What a gate must establish; manual evidence discipline |
| [`release-engineering.md`](release-engineering.md) | Backups, migrations, branch protection |
| [`review-methodology.md`](review-methodology.md) | How to review, and how to prove a check can fail |
| [`sync-data-model.md`](sync-data-model.md) | Sync metadata, tombstones, identity, mutation impact |
| [`system-design.md`](system-design.md) | Layering, patterns, composition seams, DI, testing strategy |
