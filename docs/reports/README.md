# Reports

> Reports + project context. Answers: **what happened**, **what we learned**, **what state things are in**.
>
> Pick the sub-type first — see the artifact-type table in [`../README.md`](../README.md). This
> folder holds four of them: **execution report**, **QA / gate report**, **investigation report**,
> and **closing note**, plus the **evidence records** they cite.

## When to add a file here

- Completion report after a milestone / slice ships.
- QA or gate report — what was tested, what passed, what was not testable automatically.
- Bug post-mortem / investigation (observation → hypotheses → root cause → what remains uncertain).
- Benchmark or measurement results.
- Project snapshot (test count, build status, GitNexus stats).
- Decision context — why a path was chosen over alternatives.

## Naming

`YYYY-MM-DD-<short-kebab-slug>.md`.

## Required sections

1. **Date** + **Author/agent** (if known).
2. **Scope** — what this report covers.
3. **Findings** — facts, numbers, observed behavior.
4. **Verification** — commands run, test counts, build status.
5. **Follow-ups** — non-blocking items to track.
6. **Decisions made** — ADR-style, one sub-section per non-trivial decision, each with *why it had
   to be made* / *what it's for* / *experience for future development*. Standing owner requirement
   since 2026-07-07. Skip the obvious ("ran the build"); if a round genuinely produced none, say so
   in one line. **Applies to agent-authored reports** — see the evidence-record exemption below.

## Evidence records are a different thing — do not reformat them

A file that records what the person at the keyboard actually saw (`…-observation.md`, usually
owner-authored) is **primary evidence, not a report**. It is exempt from the required sections
above: forcing Scope/Verification/Decisions onto someone's raw record corrupts the evidence it
exists to preserve. Link to it from the report that interprets it; never transcribe it into a
document authored by someone else, and never "tidy" its wording. A cross-reference only works if the
target is tracked — commit the evidence record before you cite it.

## Claims must be scoped by their evidence

- Write *claim → evidence → scope → remaining uncertainty*. Do not report a class of defect as
  closed unless the report names the evidence establishing that scope.
- Distinguish fact, inference, decision and recommendation in the text; a plausible inference
  labelled as one is useful, and the same sentence unlabelled is a future error.
- For manual results, record **how** the verdict was obtained — *observation* (written down while
  looking), *ruling* (an authorised person's statement, no written record), or *inference*
  (supporting circumstance). A terser record stays visibly terser: never write in figures the
  procedure asked for but nobody reported.
- Say what was *not* run. "NOT RUN" in a criteria table is a result; a blank that reads as a pass is
  not.

## A report that asks for a decision: verdict first, ruling appended

> Added 2026-08-25 from the S0 encoder pilot report
> ([`2026-08-25-encoder-pilot.md`](2026-08-25-encoder-pilot.md)), which carried a stop/continue
> ruling on a whole initiative. Three things it did are worth making the default; one is a trap it
> avoided by construction.

- **Put the verdict in §0, before the evidence.** A 50 KB report whose conclusion emerges on page
  nine is a report whose conclusion gets missed. State it in the first section, in one paragraph,
  then spend the rest earning it. **A negative or null verdict especially** — it is the one a reader
  skimming for a headline number will otherwise fail to notice at all.
- **Answer each criterion individually before the overall conclusion.** Where a gate has several
  dimensions, give the per-dimension verdict table first — including the dimensions that *passed*
  while the overall result failed. Compressing a multi-dimensional result into one word destroys the
  part a future reader needs: the encoder pilot's arms **failed** on classification quality and
  **passed** on confidence calibration, and only the split makes the result reusable.
- **Separate "findings that outlive this decision" into their own section.** A report that stops an
  initiative will be skipped by everyone who knows the initiative stopped — so anything inside it
  that stands regardless has to be liftable, and then actually lifted somewhere permanent
  (`knowledge/`, the roadmap's deferred list). Filing a live finding only inside a closure document
  is how it gets lost.
- **The owner's ruling may live in the same file — appended verbatim, under its own heading, after
  the report as submitted.** This is the one place where "one document, one purpose" bends, and it
  bends the right way: evidence and the decision taken on it belong next to each other. The rule that
  makes it safe is that the ruling is **appended, never folded in** — the submitted report stays
  distinguishable from the decision, so nobody later mistakes an owner's call for a measurement.
  Keep the two kinds of "decision" visibly distinct: the author's process decisions (the **Decisions
  made** section) are not the owner's ruling, and a reader searching for "the decision" must not land
  on the wrong one.
- **Do not let proposed work read as committed work.** Follow-up tables need an owner column and a
  *where it belongs* column, and each row needs its status in words — *deferred*, *defect candidate*,
  *knowledge only*, *needs a new owner decision*. "Recommendation: track this" is not a commitment,
  and a bare bullet list makes it look like one.

## Lifecycle

- Reports are dated snapshots. **Correct them by appending a dated amendment section** (e.g.
  `### Amendment, YYYY-MM-DD`) and marking the superseded passage in place — for example, leaving a
  criteria table as written and heading it *"as written on <date>; superseded — see §X"*. Never
  rewrite the original text into a cleaner story: a report that hides its own uncertainty is worth
  less than one that shows where it changed. Superseding a whole report is done by a new report that
  links back.
- Distill recurring lessons into `docs/knowledge/` so they don't get buried.
- Distill ship-events into `docs/CHANGELOG.md`.
- Once a report's content is in CHANGELOG + knowledge, it can be deleted unless it has standalone
  reference value (e.g. a benchmark) or is cited as evidence by a live document.
