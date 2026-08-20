# Incident Investigation Lessons

> Distilled 2026-07-19 from the Epic 1 Phase-2 QA investigation — the first supervised real launch
> produced nine owner observations, and the investigation resolved every one to a root-cause class
> before any fix was planned. These are lessons about *how to investigate field observations*, not
> about the bugs themselves — the findings live in
> [`../reports/2026-07-19-epic1-phase2-qa-investigation.md`](../reports/2026-07-19-epic1-phase2-qa-investigation.md).

## Observation ≠ diagnosis

**Problem.** A release-gate GUI test produced nine observations that all *looked* like defects:
a crash, a "failed" duplicate warning, a blank page, a chart that "doesn't re-render", a missing
button, suspicious backup files. Treating that list as a bug list would have meant nine fixes.

**Why it was hard.** Observations from a trusted tester carry authority, and each one arrives
pre-framed as a verdict ("the warning failed", "the heatmap is broken"). Accepting the framing is
the path of least resistance — and each framing sounds actionable.

**Wrong assumption.** That a failed test step implies a code defect at the place the failure was
observed.

**How it was solved.** Each observation was investigated to a root cause independently before any
label stuck. The nine resolved to: one regression, one pre-existing design gap, two data/perception
artifacts, one expectation mismatch, one QA runbook error, and passes. Only *one* of nine was a
reopen-grade code defect.

**Principle.** An observation is testimony about a symptom, not a verdict about a cause. The
investigation's job is to convert each observation into a root-cause class; until then it has no
priority, no owner, and no fix.

**How to avoid it next time.** Ban fix-shaped language in triage ("fix the heatmap") until the
cause is established; track observations and findings as separate lists that only merge through
evidence.

## Diagnosis completes before planning begins

**Problem.** With a confirmed P0 in hand and plausible fixes visible from the causal chain, the
natural next step was to start writing the fix plan inside the investigation report.

**Why it was hard.** Once a plausible fix is visible, planning feels like progress, and combining
verdict + plan in one document saves a round-trip. Solution bias creeps in: findings get framed to
justify the fix already imagined.

**Wrong assumption.** That investigation and planning are one activity because the same person can
do both.

**How it was solved.** The report deliberately shipped a severity *ranking* but no plan — the
owner reviewed and approved the diagnosis first
([`../specs/2026-07-19-owner-epic-1-decisions.md`](../specs/2026-07-19-owner-epic-1-decisions.md)),
then separately authorized planning. The verdict document stayed decision-ready instead of
solution-biased.

**Principle.** Diagnosis and planning are separate artifacts with a decision gate between them.
The decision-maker approves *what is wrong* before anyone proposes *what to do about it*.

**How to avoid it next time.** End every investigation report at a ranked, classified finding list
plus open questions. If a fix idea is unavoidable during investigation, record it as one line in
the ranking — never as a plan section.

## Evidence-driven debugging: collect the artifacts the failure already left behind

**Problem.** The headline symptom was "app freezes ~30 s, then dies" — reported from memory, with
no stack trace, on a machine the investigating agent was forbidden to launch the app on (the gate
reserves GUI runs for the owner).

**Why it was hard.** Without a reproduction, the temptation is to reason from the code alone and
escalate an inference. Code-reading produces plausible stories, and plausible is not proven.

**Wrong assumption.** That no reproduction means no evidence.

**How it was solved.** The failure had already left artifacts: the Windows Application event log
held five identical .NET crash records with the exact exception type and throwing method; the
database and its sidecar files proved what was and wasn't persisted; git history dated the
behavioral change to a specific commit. The "~30 s freeze" turned out to be Windows Error
Reporting collecting a dump — the event log converted a vague hang report into a precise crash in
minutes. Every claim in the final report carries file:line or log evidence.

**Principle.** Before reasoning about code, harvest the evidence the failure already produced:
OS event logs, crash dumps, the database itself, sidecar files, git history. External evidence
turns "maybe" into "confirmed" without touching the app.

**How to avoid it next time.** Make the OS event log a standard first stop for any desktop-app
crash report. Treat "I traced the code and believe X" as a hypothesis until an artifact confirms
it (see also RED-first reproduction in
[`review-methodology.md`](review-methodology.md)).

## Reconstruct the causal chain, not just the failing line

**Problem.** The crash's throwing line was easy to find — a `First()` with no match in the
repository's reconcile step. Patching that line (e.g., `FirstOrDefault` + skip) would have made
the crash disappear.

**Why it was hard.** The failing line is where the evidence points, and a one-line patch there is
the fastest visible win. Everything upstream of it looks like context, not cause.

**Wrong assumption.** That the line that throws is the line that is wrong.

**How it was solved.** The investigation reconstructed the full chain, verifying each link: the
entity constructor never sets the foreign key (origin) → the ViewModel adds the entity without
stamping it (trigger) → a schema-milestone commit switched reconciliation from navigation-based
graph fixup to scalar-FK matching, removing the mechanism that had silently healed the empty FK
for months (the regression) → no global exception handler existed, so a recoverable exception
killed the process (the amplifier). A patch at the throwing line would have silently dropped user
data instead of crashing — strictly worse.

**Principle.** A regression investigation is complete when it can name (1) where the invalid state
originates, (2) which commit changed the invariant that had tolerated it, and (3) why the safety
nets didn't catch it. Fixes then target the origin, not the crash site.

**How to avoid it next time.** For any regression, diff the *behavioral contract* between the last
known-good version and the failing one — ask "what used to absorb this?" before "what now throws?".

## Fixture bias: a green suite only vouches for the paths the fixtures take

**Problem.** 331 tests were green through every milestone review, yet the very first real task
creation crashed the app.

**Why it was hard.** The suite genuinely covered the repository logic that crashed — with fixtures
that all set the foreign key explicitly in their initializers. The tests validated a contract
("callers stamp the FK") that the production caller never honored. Green-by-construction is
invisible: nothing marks a fixture as more disciplined than production.

**Wrong assumption.** That passing repository tests implies the repository is safe against real
callers; and that a milestone's test suite exercises production construction paths.

**How it was solved.** The investigation identified the divergence exactly: every test constructed
the entity with the FK set; the single production creation path (one ViewModel method serving both
manual and smart-add flows) never set it. Compounding it, a process rule ("do not launch the app
until the backup fix lands") created a ten-day blind window between the schema change and the
first GUI run — no layer between fixtures and the owner's supervised launch existed.

**Principle.** Fixtures must be *derived from production call paths*, not written to satisfy the
unit under test. A regression test that constructs its data the way the UI does is worth more than
ten that construct it correctly. And when process forbids live runs, that window's risk must be
named explicitly, not discovered later.

**How to avoid it next time.** When a repository/service contract depends on caller discipline,
add at least one test that goes through the real caller (ViewModel-level, file-based DB). At
review time, ask of each fixture: "which production code constructs this object, and does it do
what the fixture does?"

## Classify before you fix: every finding class has a different venue

**Problem.** The investigation surfaced, in one pass: a regression, a pre-existing parser gap, a
product-behavior question (are long-overdue tasks *supposed* to disappear?), UX gaps (silent
no-ops, unexplained empty states), perception artifacts from stale seeded data, and a QA process
error. An undifferentiated "issues found" list would have dumped all of it into the reopen.

**Why it was hard.** During a release gate everything found *feels* release-blocking — urgency is
contagious across a finding list.

**Wrong assumption.** That everything discovered during a release gate belongs to the release.

**How it was solved.** Each finding was classified — regression / pre-existing design gap /
product decision / UX improvement / observation artifact / test-process error — and each class was
routed differently: only the regression reopens the epic; design gaps and UX go to the ranked
backlog; product questions go to the owner as *decisions*, not bugs; artifacts close with a
discriminating retest (a filter selection predicted to visibly change the output) instead of a
code change; the process error goes back to QA.

**Principle.** Classification is what turns an investigation into decisions. Each class has a
different owner, urgency, and fix venue; conflating them inflates scope and buries the one finding
that actually matters.

**How to avoid it next time.** Give every finding a class in the verdict table, and let the class
— not the discovery date — decide where the work goes. For suspected artifacts, design the
discriminating retest that would falsify the "it's broken" reading before touching code.

## QA owns its own errors

**Problem.** One "failed" runbook step instructed the owner to find a save button that has never
existed in the application — the save is implicit. The owner dutifully reported the button missing.

**Why it was hard.** The investigating party wrote the runbook. Recording the failure as an app
bug (or quietly dropping it) would have been less embarrassing than the true classification:
QA induced the owner's confusion.

**Wrong assumption.** That test instructions are neutral ground truth and only the product can be
wrong.

**How it was solved.** The finding was recorded as a QA runbook error, attributed as such in the
report, with the corrected retest supplied. The mislabeled step still surfaced a real (smaller) UX
finding — which was recorded separately under its own class rather than used to launder the
runbook mistake.

**Second instance (Epic 3, 2026-08-14).** The same failure, one epic later and one step worse: the
manual runbook's E6 asked the tester to **delete a semester** in order to exercise the EF
cascade-fixup path. No `XoaHocKy`, `DeleteHocKy` or `HocKys.Remove` exists anywhere in production
code — semesters can be created but never renamed or deleted, so the scenario had been written
against a capability that was never built. It was fixed at its source: retargeted at *subject*
deletion, which reaches the same cascade path and is reachable from the UI, while the missing
semester-management UI was recorded separately as a proposal rather than absorbed into the bug-fix
package that found it.

**Principle.** An investigation that cannot indict its own instructions will systematically
misattribute process failures to the product. Runbooks are code: they can have bugs, and their
bugs get named and fixed like code bugs. A scenario that *cannot be executed at all* is the extreme
case — it is a documentation defect, and it gets corrected where it was written, not filed against
the application.

**How to avoid it next time.** Write runbook steps against the actual UI (walk the screens, name
the controls verbatim), not against repository semantics; review runbooks like code before handing
them to a tester. Cheapest possible check for the E6 class: for every step whose verb is a
destructive or state-changing action, grep production for the method that performs it before the
runbook is handed over. An unexecutable scenario also costs twice — E6 stayed a blocking gate
condition long after the rest of the group had run, purely because the step was wrong.

## Keep the reopen scope minimal — deferral is a written decision

**Problem.** Nine observations, four latent hardening findings, and three feature requests were
all on the table when the reopen verdict was written. The gravitational pull was toward a broad
"quality push" reopen.

**Why it was hard.** Every extra item has an advocate and a rationale ("while we're in there…").
Scope creep during a reopen doesn't feel like creep — it feels like thoroughness.

**Wrong assumption.** That a reopen is an opportunity to fix everything known.

**How it was solved.** The verdict names exactly one reopen driver. Hardening findings adjacent to
the crash were recorded and *ranked* but not acted on; feature requests stayed parked in their
proposed row; product questions were left with the owner. The deferred items are all written down
with their class and venue — deferral is explicit, not forgetting.

**Principle.** The output of an investigation is a ranked, classified list with a minimal
mandatory core. Everything outside the core is deferred *in writing* — visible, owned, and
schedulable — which is what makes the minimal core defensible.

**How to avoid it next time.** For each candidate item ask: "does the release verdict flip if this
is not done?" If no, it defers. Record the deferral and its venue in the same document that
defines the mandatory scope, so nothing silently disappears.

## See also

- [`qa-gates.md`](qa-gates.md) — the Epic 3 gate that produced this article's second runbook-defect
  instance; classification happens here, gate scoping and manual-evidence provenance happen there.
- [`review-methodology.md`](review-methodology.md) — RED-first discriminating tests and
  reproduce-before-escalating: the sibling disciplines this investigation applied to *review*
  rather than field incidents.
- [`release-engineering.md`](release-engineering.md) — "the first real run is a milestone, not a
  formality"; the WAL backup lesson that shaped this gate's no-launch window.
- [`debugging.md`](debugging.md) — the codebase-level triage tree these investigations start from.

## Sources

- [`docs/reports/2026-07-19-epic1-phase2-qa-investigation.md`](../reports/2026-07-19-epic1-phase2-qa-investigation.md) — the full investigation: verdict table, causal chain, evidence.
- [`docs/reports/2026-07-15-GUI-test-observations.md`](../reports/2026-07-15-GUI-test-observations.md) — the owner's raw B1–B4 observations.
- `docs/plans/2026-07-15-epic1-phase2-owner-runbook.md` (archived 2026-07-26 → `legacy/Archived plans/`, local-only) — the Phase-2 runbook, including the mis-specified step.
- [`docs/specs/2026-07-19-owner-epic-1-decisions.md`](../specs/2026-07-19-owner-epic-1-decisions.md) — owner acceptance of the diagnosis and the decision gate between investigation and planning.
- [`docs/reports/2026-08-14-workload-balancer-stale-chart-fix-report.md`](../reports/2026-08-14-workload-balancer-stale-chart-fix-report.md) — D7, the E6 retarget: a runbook scenario written against a capability that was never built.
