# Review Methodology Lessons

> Distilled 2026-07-12 from the four Epic 1 milestone reviews (M1.1, M1.2, M1.2-R1, M1.3) and the
> closure verdict. These are lessons about *how review was run*, not what was reviewed — the
> engineering decisions the reviews produced live in [`sync-data-model.md`](sync-data-model.md) and
> the review documents themselves.

## RED-first discriminating tests: reproduce the prediction before fixing it

**Problem.** While implementing M1.3's `MonHoc` dedup widening, the implementer predicted — from
reading `LuuHocKyAsync`'s code and an impact-analysis report — that merging two subjects with
distinct tasks would crash with an EF "entity already tracked" exception. That prediction could
have been acted on directly: write the fix, ship it.

**Why it was hard.** A plausible, well-reasoned prediction *feels* like enough justification to
start editing, especially against code from a different, already-accepted milestone. Skipping
reproduction saves time in the common case where the prediction is right.

**Wrong assumption.** That a correct-sounding trace through the code is equivalent to evidence the
bug exists and is triggerable through the real code path, not just imagined by the reader.

**How it was solved.** Before writing any fix, the collision was reproduced against **unmodified**
M1.2 code, using two `MonHoc` rows with the exact same raw name (no normalization involved) — and
it threw the predicted `InvalidOperationException`. This distinguished two materially different
claims: "M1.3 introduces this bug" versus "M1.3 widens an already-latent trigger." Only with the
actual exception in hand was the finding brought back to the owner for an explicit scope decision.

**Principle.** A prediction is not a finding. Reproduce first, against the baseline the prediction
claims is already broken, and bring back the failing test and the real exception — not a
description of what should happen.

**How to avoid it next time.** Treat "I traced the code and I believe X breaks" as a hypothesis
requiring a red test, exactly the same way you would treat a bug report from a user. The
discriminating test is also the artifact that later lets a reviewer independently confirm the claim
(see the next section) instead of trusting the report.

## Independent verification: don't trust the report

**Problem.** A milestone self-report claims specific outcomes (build clean, N/N tests passed,
specific behavior verified) that a reviewer could simply accept.

**Why it was hard.** Re-running everything the implementer already ran feels redundant, and
milestone self-reports in this project are generally accurate — most of the time, trusting them
would cost nothing. The failure mode only shows up rarely, which is exactly what makes it easy to
stop checking.

**Wrong assumption.** That a green self-report is equivalent to a green, *reproducible* result, and
that reading a diff is equivalent to reading the changed file at its final, integrated state.

**How it was solved.** Every Epic 1 review independently: rebuilt and re-ran the full suite from a
clean `--no-build` test pass (not trusting the implementer's numbers); re-ran the specific touched
test namespace multiple times in isolation (3–5×) specifically because file- and async-based tests
in this codebase are known to flake under load; read every changed production file at its final
state, not just the diff; ran its own grep sweeps for known risk patterns (fire-and-forget writes,
raw-SQL bypasses of the stamping seam) instead of accepting the report's claim that none exist. The
closure verdict itself re-ran the full suite on the merged tree — a state none of the four
milestone reviews had actually seen, since each reviewed its own worktree.

**Principle.** "Independently verified" means the reviewer produced the evidence themselves, on
their own run, at the state actually being accepted — not that they read someone else's evidence
and found it plausible.

**How to avoid it next time.** Any acceptance claim in a report should be re-derivable by a second
party in under the time it takes to read the report. If it isn't (e.g. a manual GUI action, a
timing baseline captured on a now-gone commit), the report must say so explicitly rather than let
the number stand unqualified — see the M1.1 review's R3 finding on an unreproducible timing
baseline.

## Escape analysis: a healthy escape rate is evidence the process works

**Problem.** Accepted M1.3 code called `MessageBox.Show` directly inside a prevent-at-source
validation path. It popped a real modal dialog during headless test runs — an escape discovered
only after merge, in commit `101aaa3`, the same day.

**Why it was hard.** The M1.3 review had explicitly examined this exact call and blessed the
pattern as "consistent with the ViewModel's existing convention" — it was reviewed, not missed.
The miss was narrower and easier to overlook: the pattern's *test-runtime consequence* (a real
modal blocking a headless run), not the pattern itself.

**Wrong assumption.** That "reviewed and blessed" is the same guarantee as "has no downstream
consequence in every execution context (including CI/headless)."

**How it was solved.** Routed the warning through the `OnThongBao` seam already used by the
ViewModel's other callbacks — the same fix shape M1.1 used for `FocusViewModel` — and pinned the
new behavior with a test that asserts through the seam instead of expecting a real dialog.

**Principle.** One minor escape across roughly 1,900 inserted lines and four review passes is a
*healthy* escape rate, not a process failure. Reviews reduce escape rate; they do not drive it to
zero, and treating every single miss as proof the process failed produces the wrong incentive
(padding review time against diminishing returns instead of fixing escapes fast when they surface).

**How to avoid it next time.** Judge a review process by two numbers together: how much it caught
before merge, and how fast an escape gets fixed after merge — not by whether an escape happened at
all. When a fix pattern for one component (a callback seam) exists, apply it as the default answer
the next time the same UI-testability tension appears elsewhere, rather than re-deriving it.

## Reproduce-before-escalating: the protocol for a suspected pre-existing bug in accepted code

**Problem.** M1.3's discriminating test threatened to implicate `LuuHocKyAsync` — code from the
already-accepted M1.2 milestone — in a pre-existing correctness gap. Reworking accepted code from a
different milestone, mid-implementation of an unrelated feature, is exactly the kind of scope creep
a milestone boundary exists to prevent.

**Why it was hard.** The brief had actually anticipated this exact possibility and named the likely
remedy in advance ("the same Guid-diff treatment M1.2 gave `HocKy`") — which could easily be read
as pre-authorization to just go fix it. It is not: naming a possible gap is not the same as
approving a rework of someone else's accepted milestone.

**Wrong assumption.** That because the spec anticipated the shape of the fix, license to make the
change was implied rather than requiring an explicit decision.

**How it was solved.** The implementer reproduced the suspected gap against the **unmodified**
M1.2 code first — exact-duplicate subject names, no normalization involved — got a real, named
exception, and brought that evidence (not a description) back to the owner before touching the
code. This let the owner make an informed, explicit scope call (Option A: fold the fix into M1.3
and close Epic 1) rather than a call made on a prediction. The review then verified the
"pre-existing" claim independently by reading the M1.2-tip source directly, rather than accepting
the self-report's claim at face value.

**Principle.** When you suspect a defect in code someone else's milestone already shipped and
accepted: reproduce it against the *unmodified* prior state before proposing to touch it, bring the
owner a failing test and the real exception, and let them make the scope call explicitly. Treat a
brief's "flag it, don't paper over it" instruction as a mandate to surface the finding clearly — not
as license to silently rework unrelated, already-accepted code.

**How to avoid it next time.** Any time an implementation task touches a symbol last modified by a
different, closed milestone, pause and ask whether the change is "fixing this milestone's own new
code" or "reworking a prior milestone's accepted code" — the second case needs reproduction against
the prior baseline plus an explicit go/no-go, even when the fix is obviously correct.

## Folded-fix scrutiny: a fix riding in on unrelated feature work needs *more* review, not less

**Problem.** Once the owner chose to fold the `LuuHocKyAsync` reconcile fix into M1.3 (Option A),
there was a risk that the milestone's review would treat it as a footnote to the "real" dedup
feature, since it wasn't the thing the milestone was scoped to deliver.

**Why it was hard.** A fix that "just happens" to ride along with a different feature is easy to
under-scrutinize precisely because no one planned for it — there's no dedicated test plan or
acceptance checklist item for a fix that wasn't in the brief.

**Wrong assumption.** That a fix folded into an unrelated milestone can inherit that milestone's
review coverage by default, rather than needing its own explicit verification pass.

**How it was solved.** The M1.3 review gave the folded fix *more* scrutiny than a routine
remediation would get: it independently re-ran the touched `RepositoriesTests` namespace five times
in isolation (not the usual three), hand-traced the exact merge scenario the fix addresses
step-by-step (reparent-loop → `DetectChanges` → `MonHoc`-remove → task add/update), and
independently substantiated the implementer's "this was pre-existing, not new" claim by reading the
M1.2-tip source directly rather than accepting the claim.

**Principle.** A fix folded into an unrelated feature's delivery does not inherit that feature's
review depth automatically — it needs its own explicit verification, calibrated to what it touches
(here: a hub write path called by five UI save flows), not to the size of the diff that carries it.

**How to avoid it next time.** When accepting "fold the fix into this milestone" as a scope
decision, explicitly re-derive the fix's own acceptance criteria (what scenario must it cover, what
must the test assert) rather than letting it ride on the enclosing milestone's checklist.

## Completeness checks against ground truth, not against the one site you touched

**Problem.** M1.2-R1 fixed one specific cascade gap: `SqliteStudyTaskRepository.DeleteAsync`
tombstoned a task without cascading to its `TaskNote`/`TaskReferenceLink`. The fix could easily
have stopped at "this one call site now cascades correctly."

**Why it was hard.** Verifying that a fix is *complete* — that no second orphaned-child case
exists elsewhere — cannot be done by re-reading the fixed site harder; it requires an independent
enumeration of what the *complete* set of cascade children should be, from a source that isn't the
fix itself.

**Wrong assumption.** That fixing the one flagged call site closes the invariant the milestone
exists to establish (G1: every live descendant of a soft-deleted parent is tombstoned).

**How it was solved.** The review read `AppDbContext.OnModelCreating` — the actual schema
configuration that originally defined the pre-Epic-1 `ON DELETE CASCADE` relationships — and
enumerated every parent/child relationship against it: `HocKy→MonHoc`, `MonHoc→StudyTask`,
`StudyTask→TaskNote`, `StudyTask→TaskReferenceLink`, plus the three telemetry tables confirmed as
intentionally standalone (no FK). Only after checking each relationship against the fix's coverage
could the review conclude "`{TaskNote, TaskReferenceLink}` is the complete FK-only cascade set — the
helper is exhaustive; no second orphan hides here."

**Principle.** Ground truth for "is this fix complete" is the schema/config that defines the full
relationship set (here, `OnModelCreating`), not the bug report that motivated the fix. A fix is
only provably complete once you've checked it against an independent enumeration of everything it
was supposed to cover.

**How to avoid it next time.** For any "ensure X happens for every child of Y" fix, find the
authoritative source that lists all of Y's children (schema config, `OnModelCreating`, a type
registry) and check the fix's coverage against that list explicitly — don't infer completeness
from the fact that the one reported case now passes.

## A green check is evidence only after you've shown it can go red

**Problem.** WP-4's acceptance criteria were *"`GenerateSchedule` has characterization tests"*
and *"future behavioural regressions would cause meaningful test failures."* The obvious way to
report those met is to run the suite and show 13 passing. That demonstrates the first criterion
and says nothing whatsoever about the second.

**Why it was hard.** The failure mode is invisible from the passing side. A characterization
test that asserts something the method cannot violate — a tautology, an assertion on a value
the code derives rather than decides, a `Assert.NotNull` on a non-nullable — is green forever
and protects nothing. It looks identical in the output to a test that pins real behaviour. This
repo has already paid for this once: WP-2 removed a test that had been passing for two years on
a horizon sentinel rather than on the band it named.

**Wrong assumption.** That a suite written carefully, by someone who read the implementation
first, is therefore sensitive. Care correlates with sensitivity but does not establish it, and
the tests most likely to be vacuous are exactly the ones covering behaviour you understood
least well when you wrote them.

**How it was solved.** Mutation. Seven single-line changes were applied to
`WorkloadServiceImpl.GenerateSchedule` one at a time — reversing the least-loaded day sort,
adding one to the overflow day offset, dropping the completed-task filter, dropping the
`ThoiGianDaHoc` subtraction, reversing the priority sort, dropping the `DiemUuTien` write-back,
shrinking the 7-day window — each followed by a suite run, then reverted from git and the
production tree confirmed clean with `git diff`. All seven turned the suite red.

The sweep also produced information no green run could: it showed *which* test covers *which*
behaviour. The overflow off-by-one is caught by exactly one assertion — day-date contiguity —
because the day *count* is unchanged by that mutation, so the count assertion sails through.
That test had been added on a hunch; the sweep converted the hunch into a reason. Symmetrically,
it showed the single-task tests are insensitive to placement strategy, which is correct (with
one task, least-loaded and most-loaded agree) and worth knowing before someone deletes the
two-task test as redundant.

**Principle.** For any artifact whose purpose is to *detect a future change*, passing is not
evidence — the artifact must be shown to fail when the thing it watches changes. This
generalises past test suites: it is the same principle as *"'no exception was thrown' does not
confirm a fix"* elsewhere in this file, and as the guard test in `DecisionEngineTests` that had
to be checked against a `grep` because a self-scanning assertion can match itself. Whenever a
check's own sensitivity is untested, its green is decoration.

**How to avoid it next time.** Budget the mutation sweep into the package, not after it — it
cost about ten minutes here via a scripted edit/run/revert loop, and it is the only artifact
that lets you write "regressions would be caught" without hedging. Pick mutations that are
*plausible future edits* (a sort direction, an off-by-one, a dropped filter), not absurd ones;
the point is to model the regression you fear, not to prove the compiler works. Always restore
from version control rather than by re-editing, and verify the restore.

## Verify a claim before it sets someone else's severity

**Problem.** WP-4's report handed two defects to WP-5.2. One of them — that
`double.TryParse("4,5", out v)` silently yields `45` on `en-US` rather than failing — was
written from reasoning about which `NumberStyles` the overload implies, and had already been
committed and pushed when the reasoning was challenged.

**Why it was hard.** The reasoning was *correct about the mechanism* (the overload does imply
`AllowThousands`) and that is the part that feels like the hard part. The unverified step was
the mundane one: whether .NET's group-position validation actually accepts a one-digit trailing
group. Had it rejected `"4,5"`, the bug would have been a benign fall-through to the default
instead of a silent 45-hour capacity — the same mechanism, a different severity, and a
different priority for the package receiving it.

**How it was solved.** A scratch xUnit fact that pinned `CurrentCulture` and printed the parse
outcomes through `Assert.Fail`, run once, read, deleted. Under a minute. It confirmed the claim
(`"4,5"` → `45`, `"4,500"` → `4500`) and incidentally surfaced something the reasoning had
missed: WP-5.2's planned reader recovers `"4,5"` only where `CurrentCulture` is `vi-VN`, and
falls to the default on `en-US` — a safe outcome and still an improvement, but a *safe fallback*
rather than the *cross-locale recovery* the plan's wording implies. That correction went into
the report so the next commit message would not over-claim.

**Principle.** A claim that assigns severity to another package's work is a handoff, and a
handoff is load-bearing in a way that an observation is not. The bar for it is measurement, not
derivation — especially when measuring is cheap and the deliverable is already written.

**How to avoid it next time.** Before writing a runtime behaviour into a report, a commit
message, or a plan, ask which parts were *executed* and which were *reasoned*. Reasoned ones in
a handoff get a scratch probe. The existing discipline of *"reproduce before escalating"* covers
suspected bugs you are raising; this is its counterpart for facts you are asserting.

## See also

- [`sync-data-model.md`](sync-data-model.md) — the cascade-tombstone and reconcile mechanics these
  reviews were verifying.
- [`../knowledge/debugging.md`](debugging.md) — `Random(seed)` and reproducible test data, the same
  discipline this article's "reproduce before escalating" section relies on.

## Sources

- [`docs/review/2026-07-11-epic1-m1.3-review.md`](../review/2026-07-11-epic1-m1.3-review.md) — RED-first reproduction, folded-fix scrutiny, Option A/B/C decision
- [`docs/review/2026-07-10-epic1-m1.2-r1-remediation-review.md`](../review/2026-07-10-epic1-m1.2-r1-remediation-review.md) — completeness check against `OnModelCreating`
- [`docs/review/2026-07-05-epic1-m1.1-review.md`](../review/2026-07-05-epic1-m1.1-review.md) — independent flake reproduction (R1), user-visibility decision (R5)
- [`docs/review/2026-07-06-epic1-m1.2-review.md`](../review/2026-07-06-epic1-m1.2-review.md) — refine-before-accept verdict shape
- [`docs/review/2026-07-11-epic1-closure-verdict.md`](../review/2026-07-11-epic1-closure-verdict.md) — F4 escape analysis (commit `101aaa3`), independent re-verification on the merged tree
- [`docs/reports/2026-07-31-wp4-scheduling-characterization.md`](../reports/2026-07-31-wp4-scheduling-characterization.md) — the seven-mutation sweep, and the parse claim that was published before it was measured
- [`docs/reports/2026-07-10-epic1-m1.3-monhoc-identity-dedup.md`](../reports/2026-07-10-epic1-m1.3-monhoc-identity-dedup.md) — D2 (reproduce-before-escalating), D3 (the underlying fix)
