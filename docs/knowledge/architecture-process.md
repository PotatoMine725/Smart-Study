# Architecture Process Lessons

> Distilled 2026-07-12 from the 2026-06-30 → 2026-07-02 architecture freeze review. This article is
> about the *process* that produced decisions D-A…D-J, and the meta-principles that generalize
> beyond any one of them. The decisions themselves are normative and live in
> [`../plans/2026-07-01-architecture-direction-decisions.md`](../plans/2026-07-01-architecture-direction-decisions.md)
> (D-A…D-F) and [`../plans/2026-07-02-architecture-freeze-decisions.md`](../plans/2026-07-02-architecture-freeze-decisions.md)
> (D-G…D-J); the full derivation of each — original assumption, why it looked correct, what the
> evidence showed — lives in [`../architecture/lessons-learned.md`](../architecture/lessons-learned.md)
> (L1–L9). Read this article for the reusable *process*, not the individual decisions.

## What was the problem

A proposal for the Study Optimization Engine (SOE) — how deadlines, hard constraints, feasibility,
merge granularity, and sync ordering should work — had been drafted and mostly agreed to before a
deliberately adversarial review pass. That pass, run across three sessions (the SOE proposal, the
direction decisions D-A–D-F, and a deferred critical review of decisions 1–4), found that several
of the proposal's central mechanisms were unsound: a scalar that was supposed to guarantee a
deadline could be violated by construction; a "constraints must never be violated" guarantee was
unimplementable against realistic input; a merge policy assumed change-tracking granularity the
schema didn't have; a revision counter was assumed to order events across devices when it
structurally cannot (see [`sync-data-model.md`](sync-data-model.md)).

## Why it was hard

Architecture-level mistakes are subtle in a specific way: each wrong assumption *looked correct in
isolation*. A single weighted scalar is simple to compute, tune, and reason about, and it's the
established idiom already used elsewhere in this codebase — so "just weight the deadline heavily
enough" looked like a natural extension, not a red flag. "Constraints must never be violated" is
the strongest-sounding guarantee available, and it's trivially implementable *when inputs are
always feasible* — which they usually are, until the one case (an overloaded student) that matters
most. Cross-checking documents against each other also *looks* like a legitimate review method — it
produces real findings (genuine doc-vs-doc contradictions exist) — which is exactly what makes it
feel sufficient even when it never touches the one thing that actually decides the question: the
source. These mistakes only break under contact with code or under composition with other
requirements, not under isolated inspection — which is why catching them requires deliberately
adversarial review, scheduled *before* freezing, not an audit after the fact.

## What wrong assumptions were made

The freeze review's central finding, stated generally: several assumptions were each *true of the
schema, the docs, or the isolated requirement*, and *false of the running system, the composed
requirement, or the code path that actually decides it*. Concretely (full derivations in
`lessons-learned.md`):

- Doc-cross-checking substitutes for reading the source (L1) — false; code is normative, and a
  question framed as an "open conceptual fork" can have already been answered by one grep.
  Declared-but-unwired schema (a column that exists but no write site populates) looks like partial
  progress and is actually negative evidence (L2).
- A score term can safely replace a hard boundary once "double-counting" is removed (L3, L4) —
  false; a weighted penalty is negotiable by construction, so *any* requirement that must never lose
  cannot be expressed as a bidder in a weighted sum, no matter how the weight is tuned.
- "Never violate a constraint" is the correct feasibility guarantee (L5) — false when inputs
  themselves can already be infeasible; an absolute rule goes inert exactly when the system is
  needed most.
- Field-level merge is a runtime policy choice, independent of what per-row metadata already tracks
  (L7) — false; the merge granularity you can offer is bounded by the change information you
  actually record, and the two must be decided together.
- Three individually-reasonable guarantees are jointly satisfiable just because each pair is
  comfortable (L9) — false in general; conjunctions of guarantees must be checked as a conjunction,
  not approved pairwise.

## How it was solved

The review's method was made explicit and repeatable rather than ad hoc: **every claim entering an
architecture document must carry a `file:line` verification**, and the discriminating question for
any doc/code mismatch is never "do they differ" but "is this **lag** (a decision was made, the doc
hasn't caught up) or a **fork** (two different intents, no decision was ever actually made)" — only
forks are findings. Applying this standard to the SOE proposal produced the freeze decisions
(D-G–D-J), each recorded ADR-style with *why* / *what for* / *experience*, alongside the direction
decisions (D-A–D-F) that had already been settled the same way. Where the evidence didn't yet
support a decision, the review explicitly left the question **open** (L8 — optimization-pass
granularity) rather than force a choice, and marked it so no later reader mistakes an unresolved
question for a frozen one.

## What's the reusable principle

Four ownership rules generalize past this specific freeze, independent of the SOE's specific
mechanics:

1. **Code is normative; docs are narrative** (D-C). A document claim without a `file:line` is
   aspirational, not fact, until verified — including this article's own claims about other
   documents.
2. **Boundary information must flow as a constraint; only preference information may flow as a
   score.** Removing a term from an objective is safe only if the same information re-enters
   somewhere as a hard constraint owned by an independent validation stage — never folded back into
   the same weighted sum it left.
3. **Feasibility guarantees should be relative and monotonic, not absolute**, when the input itself
   isn't guaranteed feasible: "never worsens" is implementable everywhere; "never violates" is
   implementable only when you can also afford to go inert on the hardest inputs.
4. **Decide tracking granularity and merge/processing granularity together, never independently** —
   whatever policy you want to offer downstream is bounded by the information you chose to record
   upstream, and that coupling has to be reasoned about as one decision.

## How to avoid it next time

- Require `file:line` citation for any claim that enters an architecture document; treat
  doc-vs-doc agreement as necessary but not sufficient evidence.
- Run the adversarial, code-normative pass **before** freezing a design, not as a post-hoc audit —
  by the freeze point, sunk cost makes findings expensive to act on.
- When a decision reduces several guarantees into one mechanism, write out the conjunction
  explicitly and check it as a whole; each guarantee "sounding modest" in isolation is exactly the
  trap (L9).
- Where the evidence runs out, mark the question **open** rather than let silence default to
  "decided" — an unresolved question that looks frozen is a worse state than one honestly labeled
  unresolved.
- Revisit whether an old decision's premise still holds at each later milestone boundary, rather
  than treating a frozen decision as permanently self-verifying — see how `lessons-learned.md`'s
  entries carry dated "Status" addenda as later epics close over them, and
  [`release-engineering.md`](release-engineering.md) for a case (F5) where an environment fact
  ("no real database exists yet") quietly expired between milestones.

## See also

- [`sync-data-model.md`](sync-data-model.md) — L6/L7/L9 applied concretely to the sync metadata
  schema that shipped from these decisions.
- [`review-methodology.md`](review-methodology.md) — the same "verify independently, don't trust
  the document" discipline, applied to code review rather than architecture review.

## Sources

- [`../architecture/lessons-learned.md`](../architecture/lessons-learned.md) — L1–L9, full derivations
- [`../plans/2026-07-01-architecture-direction-decisions.md`](../plans/2026-07-01-architecture-direction-decisions.md) — D-A…D-F
- [`../plans/2026-07-02-architecture-freeze-decisions.md`](../plans/2026-07-02-architecture-freeze-decisions.md) — D-G…D-J
