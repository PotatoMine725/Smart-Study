# QA Gate Lessons

> Distilled 2026-08-19 from the Epic 3 (SOE) QA cycle: the automated gate (2026-08-10), the
> owner-led manual runbook and its two observation records, the stale-chart fix that the first
> manual run produced, and the gate closure. These are lessons about *what a gate must establish
> before it may call itself passed, and how manual evidence is recorded* — the mutation technique
> itself lives in [`review-methodology.md`](review-methodology.md), and how to classify what a gate
> finds lives in [`incident-investigation.md`](incident-investigation.md). The findings themselves
> live in the source documents at the bottom.

## A green suite over unreachable code is not release evidence

**Problem.** Epic 3 added 17 production files and the suite was green at 470 tests. The obvious way
to gate it is to measure coverage over the epic's code surface — tests per new seam.

**Why it was hard.** The epic's code surface and its *release* surface were wildly different sizes,
and nothing in the test output distinguishes them. Adding tests to `ScheduleOptimizer` and
`LoadRebalanceStage` would have produced real-looking coverage, a rising test count, and a gate
report full of green rows — over code no user can execute.

**Wrong assumption.** That "the suite is green" and "the change is covered" are the same statement.
They are different statements about different sets, and only a reachability analysis separates them.

**How it was solved.** Before writing a single test, a source search over `SmartStudyPlanner/`
(excluding `Services/Soe/` itself) established that `ScheduleOptimizer`, `LoadRebalanceStage`,
`ConstraintValidator`, `ObjectiveEvaluator`, `SoeWeights` and `OptimizerRunLogWriter` had **zero
production call sites** — every remaining mention was a doc comment. Consumers of the one interface
that *is* wired (`IWorkloadService`) were then enumerated, which reduced the whole epic to two
changes a running application can reach: the allocator placement rework and the startup schema
patch. Every subsequent probe was aimed at those two.

**Principle.** Scope a gate by reachability, not by diff size. The first question is *which of these
changes can a user execute?* — and a gate that measures itself in tests-added-per-seam will answer
it by accident, if at all.

**How to avoid it next time.** Start every gate by enumerating the production call sites of what
shipped, and write the reachable set down in the report. It costs one search, it is falsifiable by
the next reader, and it is what makes the rest of the gate's findings load-bearing. It also
independently reproduces (or contradicts) whatever scoping claim the closing note made — here it
reproduced G3-1 by a different route.

## Seam coverage is not call-site coverage

**Problem.** `TelemetrySchema.EnsureOptimizerRunLogTable` — the startup schema patch — was covered
thoroughly by `OptimizerRunLogSchemaTests`: a simulated pre-upgrade database, an idempotency check,
a round-trip with and without a null column. By any normal reading, that change was tested.

**Why it was hard.** Seam tests are the tests that get written, because the seam is the interesting
code. The call site is one line in a startup method and looks like plumbing. Nothing about a green
seam suite hints that the seam might never be invoked.

**Wrong assumption.** That covering the function covers the feature. The feature is "an upgrading
user's database gets the table", and that sentence contains a caller.

**How it was solved.** Mutation: the call was commented out of `AppStartup` and the suite re-run.
**The entire pre-existing suite stayed green.** Every user upgrading from a pre-Epic-3 database
would have received a database missing the table, with nothing going red anywhere. The gap was
closed by a test in the file-based suite — the only place the real startup sequence runs against a
real file — which downgrades a real database, runs the production entry point
`AppStartup.EnsureDatabaseReady`, and asserts the table came back.

**Principle.** For anything that must happen *automatically*, the guard belongs at the entry point
the user actually triggers, not only at the function that does the work. "Does it work when called?"
and "is it called?" are two tests, and the second is the one that is usually missing.

**How to avoid it next time.** For every side-effecting call added to a startup, shutdown, or
scheduled path, ask what turns red if the call line is deleted. If the answer is "nothing", that is
the test to write. The same gap was left one epic earlier for the M8 telemetry tables and is on
record as a cheap follow-up — the pattern recurs because the seam is always the interesting half.

## Observation, ruling, and inference are three different artifacts

**Problem.** Closing the Epic 3 manual gate required scenarios E1–E4 (Dashboard, Analytics, CRUD,
focus/streak) to have passed. They had no written observation. The owner, asked directly, stated in
session that the earlier run had covered them. That statement closes the condition — but recording
it as "E1–E4: pass" makes it indistinguishable, six months later, from the scenarios that have a
written record.

**Why it was hard.** All three kinds of support feel like the same thing at the moment you write the
row, because all three produce the same word: *pass*. And the weaker ones are not wrong — a ruling
from the person who sat in front of the application is the best artifact obtainable, and the
supporting inference (the session launched the app repeatedly and reported no error anywhere) is
genuinely reassuring. The cost of blurring them is invisible on the day and unrecoverable later.

**Wrong assumption.** That the ledger's job is to record verdicts. Its job is to record verdicts
*and where each one comes from*, because that is the part a later reader cannot reconstruct.

**How it was solved.** The closure ledger carries the provenance in the row: *pass* (written
observation), *pass by ruling* (owner statement, no written observation, labelled as such), and
*observed; ratified limitation* for the scenario that cannot fail. A terser record was flagged as
terser — the owner's E5/E6 result came back as "every test pass" without the per-step figures the
steps asked for, and the report says exactly that instead of writing the missing figures in. A
supporting circumstance was labelled "supporting circumstance, not evidence". Provenance of the
*binary* was checked the same way: the Release `.exe`'s mtime was compared against the build, and
`git diff` against merged `dev` for `*.cs`/`*.xaml` established it was code-identical to what
shipped.

**Principle.** An observation is what someone wrote down while looking. A ruling is what an
authorised person asserts without a written record. An inference is what the surrounding evidence
makes likely. All three can close a gate; only labelling them keeps the gate honest. Never upgrade a
ruling into an observation — reconstructing per-screen detail after the fact is manufacturing, not
recording.

**How to avoid it next time.** Give the result table a provenance column, not just a P/F column, and
make "who recorded this, and in what form" answerable from the row. When a verdict arrives in
conversation rather than in a file, write down that it did. Ask the question that produces the
ruling early — it costs one line and is unfalsifiable if you assume the answer instead.

## A pass read through a faulty instrument is withdrawn, not defended

**Problem.** Manual scenario C2 (the schedule's shape holds at 1/3/8 hours per day) was recorded
"Met" on 2026-08-10. Four days later, the fix for a defect found in the *same* run established that
the Workload Balancer had been drawing the previous allocation against the new capacity ceiling
whenever the slider moved — the screen C2 was read from was showing a schedule the algorithm never
produced.

**Why it was hard.** C2 had passed, honestly, and re-running it costs the owner real time. The
recorded verdict was also probably right: the underlying algorithm was never in doubt. Defending the
existing pass would have been easy and would very likely have reached the same answer.

**Wrong assumption.** That an instrument fault degrades a reading randomly, so a prior pass is still
weak evidence. Here the error had a *direction*: a stale render systematically resembles the
shape-violation C2 exists to detect. Biased error cannot be averaged away or discounted — it has to
be re-measured.

**How it was solved.** C2's result was withdrawn and the runbook row returned to unverified, with
the reason recorded, and the scenario was re-run on the fixed build with a corrected procedure
(press the rebuild button after every slider move). The re-run passed — which is the point: the
withdrawal cost one scenario and bought a result that means something.

**Principle.** When a measuring instrument is found faulty, readings taken through it are withdrawn,
not defended. This is the manual counterpart of *a green check is evidence only after you've shown
it can go red* ([`review-methodology.md`](review-methodology.md)): a manual pass is evidence only if
the channel it was read through was sound at the time.

**How to avoid it next time.** When a defect is found in a display, list every prior result that was
read off that display before deciding what to do about the defect — the blast radius of a rendering
bug is the set of observations taken through it. And when the re-run needs a different procedure
than the original, write the procedure change into the runbook row, or the re-run reproduces the
original error.

## A passing manual observation is not a standing guard

**Problem.** Scenario E6 — delete a subject that has two or more tasks, confirm its own tasks go and
a sibling subject's tasks do not — passed when the owner ran it. The behaviour is right today.

**Why it was hard.** A pass closes the scenario, and closing scenarios is what a gate is for. The
question the pass does *not* answer only matters later: every existing removal test gives the deleted
subject exactly **one** task, and none asserts that a *sibling* subject's tasks survive — so a
cascade that reached only the first child, or that reached too far, would pass the suite in E6's
shape. The manual pass and the automated gap are perfectly compatible.

**Wrong assumption.** That a scenario which passed is a scenario which is protected. A manual run
tells you about one moment; nothing carries that forward to the next change.

**How it was solved.** The closure recorded the pass *and* the coverage gap as a finding in its own
right — non-blocking, correctly classified as an automation gap rather than a defect — with a
recommended repository-level test named, and deliberately did **not** write that test inside the
closure note. A verdict document that also changes code stops being auditable as a verdict, and the
test deserves its own red-before-green evidence.

**And the estimate of the gap was itself a hypothesis.** Designing the follow-up test re-derived it
and produced two corrections, appended to the closure as a dated amendment: the gap was *smaller*
than stated (existing clone-merge tests already cover "a subject is removed and a task under it
survives"), and it was attributed to the wrong regression class — the cascade-fixup ordering only
bites when a task changes parent, which no GUI path does, so the user-reachable failure mode is
over-cascade alone. The finding survived at its corrected size; a mutation campaign written against
the original attribution would have run green and proved nothing.

**And then the test, once written, did not become a guard.** The design pre-committed to an
acceptance bar — at least one mutant the new test kills *and* the pre-existing suite survives — and
to a named fallback if nothing cleared it. Nothing did. Of five mutants, three took both sets red,
one took only the pre-existing suite red, and one survived the entire suite. The test reproduces
E6's shape faithfully and passes, but it catches nothing the suite would have missed, because the
production cascade has no branch keyed on how many children a subject has or on whether a sibling
exists — the cascade is EF's, and uniform. It was filed as **scenario-fidelity coverage, not
regression protection**, in those words.

**Principle.** Every manual scenario that passes is a candidate for conversion into a test. Ask of
each pass: *would the suite notice if this stopped being true?* Where the answer is no and the
regression class is real, the gate's output is a recommended test, not a closed line. But note what
that recommendation can and cannot promise: **closing a scenario gap and buying regression
protection are two different outcomes, and only the first is in the gate's gift.** Whether the
second follows depends on the shape of the production code, which the gate does not know when it
writes the recommendation. A test that documents a scenario nobody had exercised is worth having on
those terms — stated on those terms, and not counted as coverage it did not buy.

**How to avoid it next time.** Record, next to each manual result, which automated test covers the
same class — and blank means blank. The scenarios where that column is empty are the manual gate's
permanent cost, and they are the ones worth paying down first. Keep the writing of those tests
outside the closure document so the verdict stays a verdict. And state a coverage gap at the size
you can defend: "no test asserts X" is checkable, "this class is not covered at all" is a claim about
a whole suite. Expect the size to move when someone finally writes the test — that is the follow-up
doing its job, and it is corrected by amendment, not by rewriting the closure. Expect its *value* to
move too: decide in advance what would make the new test worth its maintenance, and what you will
say if it turns out not to clear that line (see
[`review-methodology.md`](review-methodology.md), *Set the bar before you measure*).

## See also

- [`review-methodology.md`](review-methodology.md) — mutation as the technique this gate ran on
  (including what a *surviving* mutant means), and independent verification of a self-report.
- [`incident-investigation.md`](incident-investigation.md) — classifying what a gate finds, and
  "QA owns its own errors": the runbook scenario in this cycle that was written against a capability
  the product never had.
- [`release-engineering.md`](release-engineering.md) — the migration-safety mechanics the A-group
  scenarios exercise, and "the first real run is a milestone, not a formality".
- [`system-design.md`](system-design.md) — the rendered-vs-target split that came out of the defect
  this gate's first manual run found.
- [`ml-experimentation.md`](ml-experimentation.md) — the research-gate counterpart: a gate whose
  pass/fail criterion was pre-registered, fired against the initiative, and was obeyed. Also the
  clearest instance of *"a pass read through a faulty instrument is withdrawn"* — there, a wrong
  sanity check nearly turned a sound null result into a reported harness failure.

## Sources

- [`docs/reports/2026-08-10-epic3-automated-qa-gate.md`](../reports/2026-08-10-epic3-automated-qa-gate.md) — the verdict: reachability narrowing, six mutations, findings and classification.
- [`docs/reports/2026-08-10-epic3-qa-session-report.md`](../reports/2026-08-10-epic3-qa-session-report.md) — the engineering record behind it: investigation order, raw probe outcomes, why each call was made.
- [`docs/plans/2026-08-10-epic-3-manual-qa-runbook.md`](../plans/2026-08-10-epic-3-manual-qa-runbook.md) — the runbook: 24 scenarios with pass/fail criteria stated in advance.
- [`docs/reports/2026-08-10-epic3-soe-manual-observation.md`](../reports/2026-08-10-epic3-soe-manual-observation.md) and [`docs/reports/2026-08-19-epic3-manual-observation-updated.md`](../reports/2026-08-19-epic3-manual-observation-updated.md) — the owner's two observation records, in the owner's own words.
- [`docs/reports/2026-08-14-workload-balancer-stale-chart-fix-report.md`](../reports/2026-08-14-workload-balancer-stale-chart-fix-report.md) — the fix the first manual run produced; §3/§3.1 is the withdrawn-instrument case and the criteria-stated-in-advance table.
- [`docs/plans/2026-08-19-e6-cascade-coverage-test.md`](../plans/2026-08-19-e6-cascade-coverage-test.md) and [`docs/reports/2026-08-20-e6-cascade-coverage-test.md`](../reports/2026-08-20-e6-cascade-coverage-test.md) — the E6 follow-up end to end: the design that set the acceptance bar and its fallback in advance, and the campaign that missed the bar and said so.
- [`docs/reports/2026-08-19-epic3-manual-gate-closure.md`](../reports/2026-08-19-epic3-manual-gate-closure.md) — the closure: scenario ledger with provenance, the E1–E4 ruling, the E6 coverage finding.
