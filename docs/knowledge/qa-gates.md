# QA Gate Lessons

> Distilled 2026-08-19 from the Epic 3 (SOE) QA cycle: the automated gate (2026-08-10), the
> owner-led manual runbook and its two observation records, the stale-chart fix that the first
> manual run produced, and the gate closure. These are lessons about *what a gate must establish
> before it may call itself passed, and how manual evidence is recorded* — the mutation technique
> itself lives in [`review-methodology.md`](review-methodology.md), and how to classify what a gate
> finds lives in [`incident-investigation.md`](incident-investigation.md). The findings themselves
> live in the source documents at the bottom.
>
> **Extended 2026-08-27 from the DFD-9a instrumentation cycle** — a single-scenario gate that no
> automated test could close, because 492 green tests covered the three hops and their composition
> against stubs while none resolved the production DI wiring against a real database. It passed, and
> produced four lessons the Epic 3 cycle did not: that a check's discriminating power is *per claim*,
> that a procedure verified in the wrong shell is unverified, that a diagnostic must refuse a cause it
> cannot distinguish, and that closing a gate can turn a document's own recovery step into the way to
> destroy its evidence.

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

## Discriminating power is a property of each claim, not of the check

**Problem.** The DFD-9a end-to-end check asked four pre-registered questions of one output. Against
the tasks that happened to be in the database, three were decisively answerable and the fourth was
not — and nothing in the output said so.

**Why it was hard.** The check passed, correctly. The criterion that mattered — *is
`PredictedMinutes` recorded?* — was fully discriminating: the two pre-fix rows read `NULL` in the
same output where the new rows read `0.0`, so both outcomes were visibly available and the run proved
the reader could display a failure. Nothing about it felt degenerate. The blindness was confined to
one sub-question — *did the ML branch run, or the fallback?* — and the runbook's own table answered
that one anyway: `WasML = 0, Confidence = 0` → *"Fallback — no model was ready."*

**Wrong assumption.** That a check has *one* discriminating power. It has one per claim. The same
rows that decisively answered "is the value recorded?" could not answer "which branch produced it,"
and the reason was arithmetic rather than wording. Both tasks were 77 days overdue, so `OverdueRule`
zeroed `DiemUuTien`, `ComputeFormulaMinutes` returned `0`, and

```
confidence = 1 − clamp(|predicted − formula| / max(formula, 1), 0, 1)
```

collapsed to `1 − clamp(predicted)` — **exactly `0`** for any prediction of one minute or more. The
rejected-ML branch and the fallback therefore emit byte-identical `(0, false, 0f)` rows. No
observation of that row could separate them, and the table asserting otherwise would have put a false
statement into the evidence record. It also contradicted the runbook's own §1.5, which had already
declared `Confidence = 0` ambiguous.

**The input decided it, not the instrument.** The reader was sound. The *data* was degenerate: every
task in the database was past its deadline, so every field collapsed to the same value. Re-running
against tasks with runway left produced `PredictedMinutes` 132 and 88 with `Confidence` 0.90 and
0.7333 — and answered the fourth question immediately, because `WasMlPrediction = 1` paired with a
fractional confidence is reachable only through the ML branch.

**How it was solved — recompute the logged value from state the writer never saw.** Both confidences
reproduce exactly from each task's own stored `DiemUuTien`: 73.19 and 68.38 both give `formula = 120`,
hence `1 − 12/120 = 0.9` and `1 − 32/120 = 0.7333`, matching the logged `float32` to its last digit.
This is the strongest available proof that a logged value **travelled** rather than being written: a
hard-coded constant, a default-initialised field, and a mis-wired assignment are all incapable of
reproducing from inputs they never read. It costs one query against upstream state and it converts
"the column is populated" into "the column is populated *with the right number, from the right
place*."

**Principle.** Before recording a pass, ask of each claim separately: *what would this output look
like if this particular thing were false?* If the answer is "the same," that claim is unmeasured no
matter how green the rest is. Sound instrument plus degenerate input still yields no evidence.

**How to avoid it next time.** Choose the input so the expected value is **distinctive** — pick the
case where a right answer and a wrong answer look as different as possible, and avoid the input where
everything collapses to a default. Where a value is supposed to have travelled, recompute it from
upstream state as part of the check. And when a runbook table maps observations to conclusions, audit
each row for whether the observation can actually carry the conclusion; a table is a set of claims,
and it can be wrong the way any other claim can. The research-side counterpart of choosing before
measuring is [`ml-experimentation.md`](ml-experimentation.md), *Don't manufacture independence, and
choose the input distribution before measuring*.

## A check verified somewhere other than where it runs has not been verified

**Problem.** The DFD-9a runbook's read step was written as `cd … && python - <<'PY' … PY`. The
operator runs Windows PowerShell, where `&&` is a parser error and heredocs do not exist. Five of the
runbook's six commands were bash — `ls -la`, `$APPDATA`, `date +%Y%m%d` — and the run stopped dead at
the read step.

**Why it was hard.** The command was not sloppy and it had been *run*. Its SQL was correct, its null
handling was the entire point of the check, and it had been executed verbatim to prove it worked —
in a bash tool. Every property anyone thought to verify about it was true.

**Wrong assumption.** That "I ran it and it worked" is a property of the command. It is a property of
the command *and the shell*. This is not the [faulty-instrument](#a-pass-read-through-a-faulty-instrument-is-withdrawn-not-defended)
case above, where the instrument was broken: here the instrument was in perfect order and was simply
**a different instrument than the operator's**. Reasoning about a command is not running it, and
running it somewhere else is not running it here.

**How it was solved.** Every command was rewritten in PowerShell and executed in PowerShell before
being written down, with the real output pasted in beside it. The read step became a committed
script, [`tools/qa/read_outcome_logs.py`](../../tools/qa/read_outcome_logs.py), so that what is
verified and what is run are the same bytes and no shell syntax sits on the critical path at all.

**Principle.** A runbook is a claim about a machine, and the only place to check a claim about a
machine is on that machine, in the environment its reader will use. A procedure that its own operator
cannot execute has not been verified, however carefully its logic was reasoned.

**How to avoid it next time.** State the target shell at the top of any runbook. Prefer a committed
script over a pasted snippet for anything longer than a line — it removes shell dialect from the
procedure and makes the verified artifact and the executed artifact identical. And when a runbook is
written by someone whose environment differs from the operator's, that difference is a risk to be
named in the document, not an implementation detail.

## A diagnostic must refuse to name a cause it cannot distinguish

**Problem.** The replacement read script opened the database with
`sqlite3.connect("file:" + path, uri=True)` and reported *"table `StudyTimeOutcomeLogs` is not in this
database"* — about a database that contains it. Two Windows-specific faults, both silent:
`file:D:/x.db` is not a valid URI, so SQLite opens an **empty** database rather than failing; and the
`#` in this repository's path (`C#`) starts a URI fragment, truncating the path at `D:/Code/C`.

**Why it was hard.** Neither fault raises. Both produce an empty result, and an empty result from a
telemetry table reads exactly like a real finding — *the migration never ran*, *the app never wrote a
row*. The output was well-formed and confident. Worse, the error message named a cause: *"Wrong file,
or a schema older than the outcome-log migration."* Neither had been established. The tool was
handing the operator a conclusion to write into an evidence record.

**Wrong assumption.** That an error path is exempt from the standards applied to the success path.
A diagnostic reporting *absent* is making a claim, and it can be wrong in all the ways any claim can.
The general epistemics — check the instrument before believing a null — is
[`ml-experimentation.md`](ml-experimentation.md), *Verify the instrument before you believe a null
result*. What is new here is narrower and is a **design rule for tools**: the refusal has to be built
into the diagnostic, because the operator reads what it prints.

**How it was solved.** The URI is now built with `pathlib.Path(p).as_uri()`, and the tool distinguishes
two states it previously conflated:

- **zero tables** → exit 3, *"opened, but the database reports zero tables. Treat this as a broken
  instrument, NOT as an observation about the app. Do not record a verdict from this run."*
- **the table missing while other tables exist** → exit 2, *"wrong file, or a schema older than the
  migration"* — a diagnosis it has now actually earned, because a populated `sqlite_master` rules out
  the broken-handle case.

**Principle.** An error path that confidently names a cause is more dangerous than one that admits it
does not know, because a named cause gets recorded. A diagnostic should distinguish *"I measured
absence"* from *"I could not measure"*, and say the second one plainly when it applies.

**How to avoid it next time.** Give every tool that can report a negative a self-check that runs
first and can veto the reading — here, counting tables before looking for one. Make "instrument
broken" a distinct exit path with distinct wording, and have that wording tell the operator what
*not* to do. Then prove the veto fires: point the tool at a path you know is wrong and confirm it
says *broken*, not *absent*.

## Closing a gate obsoletes the documents that described the state before it

**Problem.** With the gate passed and the verdict recorded, the runbook's §5 read PASS while its §1.3
still said *"Expected baseline: **2 rows**"* and its §2 still embedded the two-row output as *"your
baseline."* The database now held six.

**Why it was hard.** Nothing was wrong when written, and the closure edits all landed in the places
that obviously needed them — the defect record, the active README, the changelog, the result table.
The stale statements were in the *preconditions*, which nobody re-reads when finishing a run.

**Wrong assumption.** That closing a gate is an additive edit. It also invalidates every `[measured]`
figure describing the pre-run world, and those figures usually live in the setup section of the same
document. **The failure mode is not confusion, it is a plausible destructive recovery:** a future
operator reads "expect 2," sees 6, and takes the documented remedy — restore the backup from §1.3.
That backup predates the run, so restoring it deletes the four rows the closure rests on, and the two
pre-fix control rows can never be recreated because nothing backfills them. The runbook's own recovery
step had become the way to destroy its own evidence.

**A supporting hazard, found on the way.** The database runs `journal_mode = wal`. Mid-run the new
commits sat in a 104 KB `-wal` sidecar while the main `.db` mtime still read an hour earlier — so
**file mtime is not evidence of data recency**. The restore step compounded it: copying a `.bak` over
the `.db` while a stale `-wal` is present lets SQLite replay that sidecar onto the file just restored.
Restore now runs a preflight that *reports* the sidecars before anything is deleted, because deleting
one that still holds uncheckpointed commits destroys them.

**How it was solved.** §2's output was relabelled a historical capture, §1.3 gained the post-run state
and an explicit *do not restore to "get back to a clean baseline"*, and §7 gained a do-not-run banner
while keeping the procedure for a future run that would take its own backup first. The superseded
statements were struck through rather than deleted — a struck line records that the situation
changed, which a deleted one does not.

**Principle.** A document that records a measured baseline carries a maintenance obligation that a
document of opinion does not. When a gate closes, sweep the same document for what its *setup* claimed
about the world, and treat the recovery instructions as part of the sweep: the most dangerous stale
statement is the one that tells someone how to undo the thing you just proved.

**How to avoid it next time.** When writing a `[measured]` figure, mark it with its capture date at
the point of use, so a later reader can see it is a snapshot rather than an invariant. When closing
anything, re-read the whole document rather than the sections you are editing. And ask specifically:
*if someone followed the recovery path in here tomorrow, what would they lose?*

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
  sanity check nearly turned a sound null result into a reported harness failure. Its *choose the
  input distribution before measuring* is the research-side twin of *discriminating power is a
  property of each claim*, and its *verify the instrument before you believe a null result* is the
  epistemics that *a diagnostic must refuse to name a cause it cannot distinguish* turns into a tool
  design rule.

## Sources

- [`docs/reports/2026-08-10-epic3-automated-qa-gate.md`](../reports/2026-08-10-epic3-automated-qa-gate.md) — the verdict: reachability narrowing, six mutations, findings and classification.
- [`docs/reports/2026-08-10-epic3-qa-session-report.md`](../reports/2026-08-10-epic3-qa-session-report.md) — the engineering record behind it: investigation order, raw probe outcomes, why each call was made.
- [`docs/plans/2026-08-10-epic-3-manual-qa-runbook.md`](../plans/2026-08-10-epic-3-manual-qa-runbook.md) — the runbook: 24 scenarios with pass/fail criteria stated in advance.
- [`docs/reports/2026-08-10-epic3-soe-manual-observation.md`](../reports/2026-08-10-epic3-soe-manual-observation.md) and [`docs/reports/2026-08-19-epic3-manual-observation-updated.md`](../reports/2026-08-19-epic3-manual-observation-updated.md) — the owner's two observation records, in the owner's own words.
- [`docs/reports/2026-08-14-workload-balancer-stale-chart-fix-report.md`](../reports/2026-08-14-workload-balancer-stale-chart-fix-report.md) — the fix the first manual run produced; §3/§3.1 is the withdrawn-instrument case and the criteria-stated-in-advance table.
- [`docs/plans/2026-08-19-e6-cascade-coverage-test.md`](../plans/2026-08-19-e6-cascade-coverage-test.md) and [`docs/reports/2026-08-20-e6-cascade-coverage-test.md`](../reports/2026-08-20-e6-cascade-coverage-test.md) — the E6 follow-up end to end: the design that set the acceptance bar and its fallback in advance, and the campaign that missed the bar and said so.
- [`docs/reports/2026-08-19-epic3-manual-gate-closure.md`](../reports/2026-08-19-epic3-manual-gate-closure.md) — the closure: scenario ledger with provenance, the E1–E4 ruling, the E6 coverage finding.

**DFD-9a instrumentation cycle (2026-08-27):**

- [`docs/plans/2026-08-26-dfd9a-instrumentation-runbook.md`](../plans/2026-08-26-dfd9a-instrumentation-runbook.md) — the runbook, with criteria fixed in advance. §8 is its own correction record: the bash-in-PowerShell failure (§8.1–8.2), the two silent URI faults (§8.3), and the two corrections the run itself produced (§8.5).
- [`docs/reports/2026-08-27-dfd9a-instrumentation-observation.md`](../reports/2026-08-27-dfd9a-instrumentation-observation.md) — the evidence record. §2.5 is the confidence-reproduction check; §5 is the undetermined-then-determined branch question; §3 shows an owner attestation quoted verbatim with its scope stated rather than paraphrased.
- [`docs/plans/2026-08-26-prediction-instrumentation-defect.md`](../plans/2026-08-26-prediction-instrumentation-defect.md) — the defect record. §9.4 is the *"what is still true after the fix"* list, with the one closed gate struck through rather than deleted.
- [`tools/qa/read_outcome_logs.py`](../../tools/qa/read_outcome_logs.py) — the reader, committed so that the verified artifact and the executed artifact are the same bytes. Its comments carry the two Windows URI traps and its exit codes separate *broken instrument* from *wrong file*.
