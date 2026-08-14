# Epic 3 (SOE) — Automated QA Session Report

**Date:** 2026-08-10 · **Branch:** `dev` · **HEAD at session start:** `dd41685` · **Author:**
orchestrator, single session, no subagents dispatched.

**Relationship to the other two documents from this session.** This is the *engineering record* —
what was investigated, in what order, what each probe actually returned, and why each call was made.
Its siblings are narrower by design:

| Document | Role |
|---|---|
| [`2026-08-10-epic3-automated-qa-gate.md`](2026-08-10-epic3-automated-qa-gate.md) | The **verdict** — gate criteria, evidence table, findings, pass/fail |
| [`../plans/2026-08-10-epic-3-manual-qa-runbook.md`](../plans/2026-08-10-epic-3-manual-qa-runbook.md) | The **handover** — 24 unexecuted manual scenarios for the owner |
| **This document** | The **record** — the reasoning and raw outcomes behind both |

Read the gate report if you want the answer. Read this one if you want to know whether to trust it.

---

## 1. Session context and mandate

The engineering convergence phase of Epic 3 closed on 2026-08-07 (4 docs-only commits, DoD 7/7).
The mandate for this session was to establish — **automatically, without touching the GUI** —
whether the converged implementation is ready to enter owner-led manual QA.

Standing constraints, all observed:

- Do not expand the Epic's scope; do not redesign already-ratified decisions.
- Do not start manual GUI testing; do not claim manual behaviour has been verified.
- Where a behaviour cannot be established reliably by automation, **record it as a manual-test
  requirement rather than guessing**.
- A finding outside approved scope gets fixed **only** if it is a necessary correctness/regression
  fix; otherwise classify it as accepted debt, deferred work, or proposal.

The gate's completion criteria required, among others, that required regression coverage be
*present* — which this session read, deliberately, as **present and demonstrably able to fail**, per
the project's standing rule that a passing test is not evidence.

---

## 2. Orientation — what Epic 3 actually shipped

Epic 3's execution ran in an isolated worktree (`worktree-epic-3-soe`); by this session its commits
were already on `dev`. `git status` at start showed a clean production tree with three pre-existing
stray files from earlier sessions (`.claude/*`, two handoff notes, a docs asset zip) — left untouched
throughout.

**Production surface introduced or modified since the epic's branch point (`1a5ad7d`):**

- 17 new files under `SmartStudyPlanner/Services/Soe/` — `IConstraintValidator`/`ConstraintValidator`,
  `IObjectiveEvaluator`/`ObjectiveEvaluator`, `IScheduleOptimizer`/`ScheduleOptimizer`,
  `IOptimizerStage`/`LoadRebalanceStage`, `OptimizerComparator`, `IDeadlinePolicy`, `ScheduledItem`,
  `SoeWeights`, `ObjectiveScore`, `Eval`, `OptimizeReport`, `OptimizerRunLogRow`,
  `OptimizerRunLogWriter`.
- `Services/WorkloadServiceImpl.cs` — the allocator, reworked (T3.3) and given an identity-carrying
  internal seam (T3.8).
- `Data/AppDbContext.cs`, `Data/AppStartup.cs`, `Data/TelemetrySchema.cs` — the `OptimizerRunLogs`
  telemetry table (T3.7), +51 lines, purely additive.

**Baseline measurement** (CI-equivalent, first action of the session):

```bash
dotnet build SmartStudyPlanner.slnx --configuration Release
dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --no-build --configuration Release
```

> build: 0 errors, 94 warnings (all pre-existing)
> **470 passed / 0 failed / 1 skipped / 471 total**

This reproduces the closing note's DoD-3 row exactly. The single skipped test
(`SoeBaselineCaptureTests.CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing`) is skipped
by design — it compares against a deliberately frozen pre-T3.3 baseline — and its skip is
compensated by a structural guard that does run. That compensating guard was itself verified later
(§5, M2); a skip with an unverified compensator is exactly the kind of thing a gate should not wave
through.

---

## 3. Scope narrowing — why 470 green tests were not release evidence

This was the session's pivotal judgement, made before any test was written.

Epic 3's *code* surface is large. Its *release* surface is not. A source search over
`SmartStudyPlanner/`, excluding `Services/Soe/` itself, established that these types have **zero
production call sites** — every remaining mention is a doc comment:

`ScheduleOptimizer` · `LoadRebalanceStage` · `ConstraintValidator` · `ObjectiveEvaluator` ·
`SoeWeights` · `OptimizerRunLogWriter`

`BalanceWorkloadStage.cs:41` still calls `IWorkloadService.GenerateSchedule` — the pre-Epic-3 path.
This independently reproduces the closing note's evidence-scoping statement (G3-1) by a different
route: the note derived it during G3 ratification; this session derived it from the source tree.

Consumers of `IWorkloadService` were then enumerated to find the real blast radius:

| Consumer | Uses | Placement exposure |
|---|---|---|
| `BalanceWorkloadStage` | `GenerateSchedule` | yes (pipeline) |
| `WorkloadBalancerViewModel` | `GenerateSchedule` | yes (**the GUI surface**) |
| `DashboardViewModel` | `GetCapacity()` only | **none** |

**Conclusion that governed the rest of the session:** exactly two Epic 3 changes can affect a running
application — the allocator placement rework, and the startup schema patch. Everything else is
dormant code behind unwired seams.

The consequence is uncomfortable and worth stating plainly: **the 470-test baseline was green *and*
was mostly exercising code no user can reach.** Adding further tests to the optimizer seams would
have produced impressive coverage numbers while leaving the two shipping changes exactly as
examined as they were before. "The suite is green" and "the release is covered" were different
statements here, and only the narrowing separated them.

---

## 4. Investigation log

Five work items, executed in this order.

### 4.1 The `DiemUuTien` write-through — a decision that flip-flopped over persisted user data

**Why it was first.** CP-2 originally ratified *dropping* the allocator's write-through of
`DiemUuTien` onto the task model, and *deleting* its pinning test. The
[CP-2 amendment](../plans/2026-08-06-cp2-amended-diemuutien-writethrough-restored.md) (2026-08-06)
reversed that. A governing decision that reversed itself, over data that persists to the user's
database, is not something to take from the record.

**Verified from code, not from the note:**

- The write-through **is present**: `WorkloadServiceImpl.cs:143`,
  `task.DiemUuTien = _decisionEngine.CalculatePriority(task, mon);`
- The pinning test **was restored, not deleted**:
  `WorkloadServiceScheduleTests.GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh:86`.
- The coupling it protects is real and sits two layers away:
  `Core/Scheduling/Engines/RawMinutesCalculator.cs:11` reads `task.DiemUuTien` **off the model** —
  it takes no parameter — and returns 0 when the score is ≤ 0. `StudyTask.DiemUuTien` has no
  initializer, so it defaults to 0.0.

**Therefore:** remove the write-through and every task not scored elsewhere gets 0 suggested minutes
and silently vanishes from the schedule. No exception, no warning — just an emptier screen.
`WorkloadBalancerViewModel` calls `GenerateSchedule` directly in its constructor, with no scoring
step ahead of it, so this is reachable on a perfectly ordinary navigation.

**Gap identified:** the suite's `StubDecisionEngine` returns minutes from a **name-keyed lookup
table**, ignoring `DiemUuTien` entirely. So no test reproduced the coupling. The guard that existed
asserted the *mechanism* (does the write happen?), never the *reason* (what breaks if it doesn't?).
That distinction mattered — see §6.1.

### 4.2 Consumer-level regression coverage for the placement change

- `BalanceWorkloadStage` is tested (`PipelineStageTests.BalanceWorkloadStage_UsesWorkloadService`)
  but against a `StubWorkloadService` — it covers stage wiring, not allocation. Correct for a stage
  unit test, and it means the placement change cannot regress *through* it.
- `WorkloadBalancerViewModel` has **no automated tests at all**. It is the primary GUI surface for
  the change. `BuildSchedule(notify: true)` calls `System.Windows.MessageBox.Show` directly, which
  is what makes it awkward to drive headlessly.
- `DashboardViewModel` touches only `GetCapacity()` — no exposure.

**Consequence:** the user-visible shape of the placement change has no automated consumer-level
guard. This is pre-existing debt (the ViewModel was untested before Epic 3 too), so it was not
"fixed" by building a WPF harness — that would have been scope expansion. Instead it became
(a) a service-layer invariant test (§6.2) and (b) runbook Group C.

### 4.3 The deadline-tier inertness claim

`WorkloadServiceImpl` selects a day with a two-tier expression: tier 1 takes the earliest day with
room *at or before* the task's deadline; tier 2 falls back to the earliest day with room anywhere.
The closing note, the production call site, and the characterization suite's class doc all assert
that tier 1 is **provably output-inert** — it cannot change any placement tier 2 would not already
have chosen ([proof](../plans/2026-08-06-deadline-tier-provably-inert.md)).

Two things were true of that claim: it rested on prose plus a one-off empirical confirmation, and
the ratified decision **explicitly declines to write a discriminating test for the branch**, on the
grounds that any such test is vacuous.

Both were respected. The claim was verified by mutation instead of by a new test (§5, M6) — which
tests the assertion without contradicting the decision that governs it.

### 4.4 Guard integrity — the two guards that could have failed silently

Two guards carry unusual weight and could plausibly pass for the wrong reason:

- `SoeBaselineCaptureTests.BaselineArtifact_TonTaiVaCoCauTrucHopLe:247` — the compensator for the
  skipped frozen-baseline test. If this were vacuous, the skip would silently cost Card G its
  "before" measurement.
- `ObjectiveEvaluatorTests.SourceFiles_ContainNoHanChotOrDeadlineToken:398` — a literal source-text
  scan enforcing that no deadline vocabulary enters the objective seam (D-G/D-J).

Both locate files by repo-relative path (`RepoLocator.FindRepoRoot()`) and read them with
`File.ReadAllText`. A path that fails to resolve therefore **throws** rather than passing over an
empty set — so neither is structurally vacuous. Both were then mutation-proven anyway (§5, M2/M3).

### 4.5 The legacy-database startup path

`TelemetrySchema.EnsureOptimizerRunLogTable` runs on **every** launch, and in `AppStartup` it is
ordered *before* the Epic 1 backup + D-I column patch. Since it only issues
`CREATE TABLE IF NOT EXISTS` for a table that is empty on every existing DB, running before the
backup is safe and is documented as such.

`OptimizerRunLogSchemaTests` covers the seam thoroughly: a pre-T3.7 DB simulated by `DROP TABLE`,
confirmation that `EnsureCreated()` is a no-op on an existing DB, the patch, and a round-trip
including both `Reason = null` (C₀) and a non-null reason on the same trip.

**What it does not cover:** that `AppStartup.EnsureDatabaseReady` actually **calls** it.
`AppStartupFileBasedTests` — the only test that drives the real startup sequence against a real file
DB — asserts no-throw, the HocKy upgrade, and backup behaviour, but never checks the table. This
became the session's one genuine coverage gap; §5 M5 proved it was real rather than theoretical.

---

## 5. Mutation testing — six probes

Every probe modified **production** code, was measured, and was reverted. Reverts were done with
`git checkout --` and confirmed with `git status --porcelain SmartStudyPlanner/` returning empty.

### M1 — Remove the `DiemUuTien` write-through

```diff
- task.DiemUuTien = _decisionEngine.CalculatePriority(task, mon);
+ _ = _decisionEngine.CalculatePriority(task, mon);
```

Simulates precisely the "make it pure" refactor Card F round 1 performed at `5197784`.

**Run A (full suite, before any test was added):** `Failed: 3, Passed: 467, Skipped: 1, Total: 471`

- `WorkloadServiceScheduleTests.GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh`
- `WorkloadServiceScheduleTests.GenerateSchedule_UuTienCaoDuocXepTruoc` *(all priorities collapse to
  0, so the sort's tie-break exposes it)*
- `SoeT34CorpusReportTests.G25_ThreeArmPartition_ProducesReportArtifact_AndPersistsOptimizerRunLog`

**Run B (after the new test, filtered):** `Failed: 3, Passed: 22, Total: 25` — the two above plus the
newly added `GenerateSchedule_TaskChuaTungDuocChamDiem_VanDuocXepLich`.

**Reading:** the CP-2 regression *cannot* return silently — that was the important answer, and it was
reassuring. But all three Run-A failures assert the mechanism. A future purity refactor would read
them as "tests of the impurity" and update them in the same commit, which is exactly the historical
failure mode. Hence §6.1.

### M2 — Hide the frozen baseline artifact

`docs/reports/data/2026-08-05-soe-t36-baseline.json` renamed away, single test run, file restored.

**Result:** `Failed: 1` — `BaselineArtifact_TonTaiVaCoCauTrucHopLe`. Compensator is real.

### M3 — Inject forbidden vocabulary into the objective seam

Appended `// MUTATION PROBE: HanChot` to `Services/Soe/ObjectiveEvaluator.cs`.

**Result:** `Failed: 1` — `SourceFiles_ContainNoHanChotOrDeadlineToken`. The scan scans.

### M4 + M5 — Two independent, disjoint mutations applied together

**M4** — restore the pre-T3.3 least-loaded rule:

```diff
- var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes && d.Date <= hanChotDate)
-                    .OrderBy(d => d.Date).FirstOrDefault()
-                ?? days.Where(d => d.TotalMinutes < capacityMinutes)
-                    .OrderBy(d => d.Date).FirstOrDefault();
+ var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes)
+                    .OrderBy(d => d.TotalMinutes).FirstOrDefault();
```

**M5** — comment out `TelemetrySchema.EnsureOptimizerRunLogTable(db);` in `AppStartup`.

**Result:** `Failed: 5, Passed: 23, Total: 28`

Attributable to M4 (placement):
- all three Theory cases of the new `…NgayDungLaTienToLienTuc_ChiNgayCuoiConCho` (capacity 1, 2, 3)
- the pre-existing `GenerateSchedule_ChonNgaySomNhatConCho_ChuKhongPhaiNgayItTaiNhat`

Attributable to M5 (startup):
- `EnsureDatabaseReady_OnPreT37Db_TaoLaiBangOptimizerRunLogs` — **and nothing else**

**Reading — this is the session's most consequential single result.** M5 removed a production
startup call and the *entire pre-existing suite stayed green*. Every user upgrading from a
pre-Epic-3 database would have got a DB missing the table, with no test anywhere going red. The seam
tests covered the seam; nothing covered the call site. The gap was real, not theoretical, and is now
closed.

### M6 — Delete the tier-1 deadline filter entirely

```diff
- var targetDay = days.Where(d => … && d.Date <= hanChotDate).OrderBy(d => d.Date).FirstOrDefault()
-                ?? days.Where(d => d.TotalMinutes < capacityMinutes).OrderBy(d => d.Date).FirstOrDefault();
+ var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes).OrderBy(d => d.Date).FirstOrDefault();
```

**Result:** `Passed: 475, Failed: 0, Skipped: 1, Total: 476` — **the entire suite stayed green.**

**Reading:** a surviving mutant is normally a coverage gap. Here it is the *predicted* outcome of a
ratified proof, and reproducing it is the point. The claim "tier 1 cannot disagree with tier 2 on
any input" now has an independent empirical confirmation on this HEAD, obtained without writing the
vacuous test the ratified decision declines. **No test was added.**

This is the probe most easily misread by a tool that reports mutation score without reading the
decision behind the code. Acting on it — "coverage gap, write a test" — would have produced a test
that cannot fail, plus a claim of improved coverage.

---

## 6. Coverage gaps found and closed

Three test methods / five cases added. **No production code was modified.** Each was mutation-proven
red *before* being accepted — a new test that has never been seen to fail is not evidence either.

### 6.1 `GenerateSchedule_TaskChuaTungDuocChamDiem_VanDuocXepLich`

`SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs`

Closes the mechanism-vs-reason gap from §4.1. Introduces a second test double,
`PriorityCoupledDecisionEngine`, whose `CalculateRawSuggestedMinutes` reproduces the real gate —
`task.DiemUuTien <= 0 ? 0 : …` — instead of the name-keyed lookup. The test asserts its own premise
(the task enters with the default score of 0.0), then asserts the task is nonetheless **scheduled**.

Its comment states the consequence in full, so a future reader deleting it has to first disagree in
writing with "unscored tasks silently vanish from the schedule". Proven red by M1 Run B.

One supporting change: the private helper `Sut(…)` was widened from `StubDecisionEngine` to
`IDecisionEngine` so a second double can be injected.

### 6.2 `GenerateSchedule_DonVeNgaySomNhat_NgayDungLaTienToLienTuc_ChiNgayCuoiConCho` (Theory ×3)

Same file. Pins earliest-feasible as a **structural invariant** at capacities 1, 2 and 3 h/day,
rather than by a single 3-day worked example:

1. days with work form a **contiguous prefix** starting today — no empty day between two used days;
2. every used day **except the last** is filled exactly to the capacity ceiling;
3. total minutes are preserved.

The invariant follows from the algorithm — each chunk goes to the earliest day with any room, and
chunks are cut to exactly the room available (`chunk = min(remaining, spaceLeft)`), so a day can only
retain room once no work remains. Least-loaded placement violates (1) and (2) simultaneously, which
is what makes the test discriminating rather than descriptive. Proven red by M4, all three cases.

This is also the closest automated proxy for what the owner will see on screen: dense, contiguous day
cards rather than thin load spread across seven days.

### 6.3 `EnsureDatabaseReady_OnPreT37Db_TaoLaiBangOptimizerRunLogs`

`SmartStudyPlanner.Tests/Data/AppStartupFileBasedTests.cs`

Closes the M5 gap. Creates a real **file-based** DB, downgrades it to pre-T3.7 by dropping
`OptimizerRunLogs`, asserts the table is genuinely absent, runs the production entry point
`AppStartup.EnsureDatabaseReady`, and asserts the table is back. Filed in the file-based suite
specifically because that is the only place the real startup sequence runs against a real file.

**Final suite state:** `475 passed / 0 failed / 1 skipped / 476 total`, build 0 errors.

---

## 7. Findings and classification rationale

**No Critical finding. No release blocker. No unresolved defect.**

### QA-1 — MEDIUM — deferred to owner

`Views/WorkloadBalancerPage.xaml:39`:

> "Thuật toán **rải các bài tập chưa hoàn thành đều khắp** những ngày tới — theo điểm ưu tiên và sức
> học mỗi ngày của bạn."

After T3.3 the algorithm does not spread work evenly; it packs it into the earliest days with room.
The sentence describes the **pre-Epic-3 least-loaded rule**. The rest of the page was checked and is
still accurate — the capacity-ceiling text, and "sắp theo Điểm ưu tiên" (priority remains the
ordering key; only day selection changed).

**Why it was not fixed, despite being a one-line change.** The question that settled it: *is the
correct output determined by the code, or by a preference?* Here it is a preference. "Rải đều" is
wrong, but a naive "dồn vào các ngày sớm nhất" is also incomplete — priority still orders tasks,
capacity still caps each day, and the feature is still named "CÂN BẰNG TẢI". It is user-facing
Vietnamese product copy on a page with an active design track. A QA gate reports that; it does not
decide it. Routed to runbook **C7** with proposed wording so the owner rules with the screen in view.

**Why MEDIUM rather than a nit.** This is F1's failure mode with a different blast radius. F1 was a
*document* asserting what the code never did; this is *UI* asserting what the code used to do.
Neither self-corrects on a sweep — a later reader finds a fluent, confident sentence and no reason to
doubt it. The stale-but-plausible claim is the dangerous kind.

### Accepted as ratified — deliberately not reopened

Listed so the manual gate does not rediscover them as defects. All carry owner rulings from
2026-08-07 (D7) or the closing note:

- **Metric #1b** — deadline inversions 250 → 220 (12% reduction); the self-miss class eliminated
  (17 → 0); pairwise residual accepted. Root cause **A1**: priority is the sole task-ordering key.
- **Metric #4** — `Score(B)` not computable for 207/230 corpus items; unevaluated, recoverable.
- **F4** — the A6 cross-check's `>=` vs `>` boundary offset (113 chunks / 6 440 minutes).
- **The optimizer ships unwired** — zero production callers; integration is separate work (G3-1).

### Deferred / proposals — outside scope, not acted on

- **P1 (proposal)** — `AppStartupFileBasedTests` still does not assert that the **M8** telemetry
  tables are created through `EnsureDatabaseReady`. That is the same silent-call-site-removal gap M5
  exposed, one epic earlier. Pre-existing; fixing it for M8 in this gate would have been scope
  expansion. Recommended as a cheap follow-up — the pattern is now written down in §6.3.
- **P2 (accepted debt)** — `WorkloadBalancerViewModel` has no automated tests; `MessageBox.Show`
  inside `BuildSchedule(notify: true)` is the obstacle. Pre-existing, not introduced by Epic 3. It is
  the reason runbook Group C is manual.
- **P3 (observation)** — the feature name "CÂN BẰNG TẢI" (load balancing) now describes the algorithm
  less well than it did. Renaming is a product decision well outside this gate.

---

## 8. What manual QA must cover, and why automation could not

Recorded as requirements rather than guessed at. **No manual behaviour was verified in this session**
and every result cell in the runbook is blank.

| Requirement | Runbook | Why automation could not settle it |
|---|---|---|
| A real long-lived user DB upgrades without data loss | A1 | The suite builds synthetic DBs; only a genuine pre-Epic-3 file proves migration against real accumulated data |
| The placement change **as rendered** | C1–C2 | `WorkloadBalancerViewModel` is untested (P2) and filters empty days before display; the invariant is pinned at the service layer, not at the pixel |
| Header copy vs. observed behaviour | C7 | A wording judgement, not an assertion |
| No collateral regression on other screens | E1–E6 | WPF navigation and rendering are outside the headless suite |
| `OptimizerRunLogs` stays empty in real use | B2 | Requires exercising the real app, not a fixture |

Three notes were written into the runbook specifically to stop the owner logging non-defects: the
optimizer is not GUI-reachable; the telemetry table will exist but stay **permanently empty**; and a
task placed past its deadline is ratified limitation A1/D7.

---

## 9. Final state

- **Suite:** 475 passed / 0 failed / 1 skipped / 476 total. Build 0 errors, 94 pre-existing warnings.
- **Production code:** unchanged by this session. All six mutation probes reverted and verified.
- **Working tree (uncommitted):** two modified test files
  (`Services/WorkloadServiceScheduleTests.cs`, `Data/AppStartupFileBasedTests.cs`) and three new docs
  (this report, the gate report, the runbook). Pre-existing stray files left untouched.
- **Not committed.** `dev` has been PR-only since 2026-08-09; the branch/PR decision is the owner's.
- **Test-count reconciliation:** the closing note's 470/471 was accurate at its SHA and was **not**
  amended; execution-plan criterion 13 ("≥ 391") still holds at 475. The +5 delta is itemised in §6.

**Verdict: the repository is ready for owner-led manual QA**, with QA-1 deferred to the owner at
runbook C7.

---

## Decisions made (ADR-style)

### D1 — Narrow the gate to the two production-reachable changes before writing anything

- **Why:** Epic 3's optimizer seams have zero production call sites. A gate that measured itself in
  tests-added-per-seam would have generated real-looking coverage over unreachable code while the
  allocator rework and the startup patch — the only changes a user can execute — went unexamined.
- **What for:** every finding in §5–§7 sits on a code path that actually runs. M5, the one real gap,
  is on a startup call site; no amount of optimizer testing would have surfaced it.
- **Experience:** the baseline was green *and* silently missing a guard on production startup. That
  combination is the argument for narrowing: a green suite answers "did anything I tested break?",
  not "is the change covered?" — and only after separating the two does the second question become
  answerable.

### D2 — Verify the CP-2 amendment from the code, not from the decision record

- **Why:** CP-2 ratified removing the write-through and deleting its test; the amendment reversed
  both. When the governing decision for persisted user data has flip-flopped, the record tells you
  what was *decided*, not what is *there*.
- **What for:** confirmed the write-through is present at `WorkloadServiceImpl.cs:143`, the pinning
  test restored, and — the part neither document spelled out — that the `StubDecisionEngine` cannot
  reproduce the coupling, which is what turned "guarded" into "guarded at the mechanism only".
- **Experience:** the reflex to accept "amended, restored, fine" was strong; the amendment note is
  clear and correct. Reading the two files it cites cost minutes and produced §6.1, the one addition
  that defends the *reason* rather than the mechanism.

### D3 — Mutate every guard relied on, including ones expected to survive

- **Why:** the project's standing rule is that a passing test is not evidence. Applied to six guards,
  including two whose failure mode would be silent vacuity rather than a wrong answer, and one
  expected to survive.
- **What for:** M5 converted an assumption ("the seam tests cover T3.7") into a measured fact ("they
  cover the seam; nothing covered the call site"). M6 converted a prose proof into a reproduced
  result without writing a test the ratified design declines.
- **Experience:** M6 is the instructive one and the easiest to mishandle. A surviving mutant reads as
  a coverage gap; acting on that reading here would have produced a vacuous test *plus* a claim of
  improved coverage — worse than leaving it alone. Mutation results are evidence, not instructions;
  they have to be read against the decision that governs the code.

### D4 — Classify the stale UI copy rather than edit it

- **Why:** QA-1 is a genuine mismatch introduced by this epic, but the correct replacement sentence
  is a product-copy decision — not mechanically derivable from the code.
- **What for:** the owner decides the wording at runbook C7 with the screen in front of them; the
  finding is on record either way, so nothing is lost by not editing.
- **Experience:** the pull toward "it's one line, just fix it" was strong and would have been scope
  expansion dressed as tidiness. The test that settled it — *is the correct output determined by the
  code or by a preference?* — is reusable, and it is the same test that would have blocked the
  original F1 propagation.

### D5 — Reconcile the test-count delta in a new document rather than amend ratified text

- **Why:** the closing note's 470/471 was accurate at its SHA, and criterion 13's "≥ 391" still holds
  at 475. Editing a ratified closure document to track a later, unrelated addition trades a correct
  historical record for one that must be maintained forever.
- **What for:** a reader who runs the suite today and sees 476 finds the +5 itemised with its cause,
  and neither document has to misstate anything.
- **Experience:** F1's lesson is about documents asserting things the code never did — not about
  documents recording a measurement that was true when taken. Conflating the two would have produced
  an unnecessary edit to ratified text in F1's own name.
