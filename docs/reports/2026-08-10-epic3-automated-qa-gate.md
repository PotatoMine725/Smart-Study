# Epic 3 (SOE) — Automated QA Gate Report

**Date:** 2026-08-10 · **Branch:** `dev` · **Baseline HEAD:** `dd41685` · **Purpose:** establish
whether the converged Epic 3 implementation is ready to enter owner-led manual QA.

**Produces:** [`docs/plans/2026-08-10-epic-3-manual-qa-runbook.md`](../plans/2026-08-10-epic-3-manual-qa-runbook.md)
— the manual runbook this gate hands over to.

**Reads with:** [Epic 3 closing note](2026-08-07-epic3-closing-note.md) (ratified state and
limitations — **not reopened here**) · [convergence plan](../plans/2026-08-07-epic-3-convergence-plan.md).

**Scope discipline.** This gate verified; it did not redesign. No ratified decision was reopened, no
epic scope was widened. Where a finding fell outside the approved scope it was classified (§4), not
fixed.

---

## 1. The QA surface is much narrower than the epic's code surface

Epic 3 touched 17 new production files, but **only two of its changes can affect a running
application.** This was established independently at the start of the gate by source search over
`SmartStudyPlanner/` (excluding `Services/Soe/` itself):

`ScheduleOptimizer`, `LoadRebalanceStage`, `ConstraintValidator`, `ObjectiveEvaluator`, `SoeWeights`
and `OptimizerRunLogWriter` have **zero production call sites** — every remaining mention is a doc
comment. `BalanceWorkloadStage.cs:41` still calls `IWorkloadService.GenerateSchedule`, the
pre-Epic-3 path. This reproduces the closing note's evidence-scoping statement (G3-1) by an
independent route.

The two production-reachable changes are:

| # | Change | Reachable via |
|---|---|---|
| 1 | `WorkloadServiceImpl.GenerateScheduleWithIdentity` — placement rework, least-loaded → earliest-feasible (T3.3) | `GenerateSchedule` ← `BalanceWorkloadStage`, `WorkloadBalancerViewModel` |
| 2 | `TelemetrySchema.EnsureOptimizerRunLogTable` — runs at every startup (T3.7) | `AppStartup.EnsureDatabaseReady` |

A third consumer, `DashboardViewModel`, holds `IWorkloadService` but calls only `GetCapacity()` — no
placement exposure. That narrowing is what the rest of this gate was spent on: **a green suite over
dormant seams is not release evidence.**

---

## 2. Automated QA performed

### 2.1 Full suite — before and after

CI-equivalent commands (`dotnet build SmartStudyPlanner.slnx -c Release`, then
`dotnet test SmartStudyPlanner.Tests/... --no-build -c Release`):

| Run | Build | Result |
|---|---|---|
| Baseline, at `dd41685` | 0 errors (94 warnings, all pre-existing) | **470 passed / 0 failed / 1 skipped / 471 total** |
| Final, after this gate's additions | 0 errors | **475 passed / 0 failed / 1 skipped / 476 total** |

The baseline reproduces the closing note's DoD-3 row exactly (470/0/1/471). The skipped test is
`SoeBaselineCaptureTests.CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing` — skipped by
design (it compares against a deliberately frozen pre-T3.3 baseline), with a compensating
structural guard that does run (verified in §3.2).

**Reconciliation with the closing note.** The closing note cites 470/471 for DoD-3 and "470 ≥ 391"
for execution-plan criterion 13. Both were accurate at that SHA and are **not** amended: criterion
13's threshold still holds at 475. The delta is entirely this gate's three added test methods
(5 cases: 3 Theory + 2 Fact), itemised in §3.4. Recorded here rather than by editing a ratified
closure document.

### 2.2 Targeted SOE and regression runs

Epic 3 test inventory confirmed present and green: `ConstraintValidatorTests` (11),
`LoadRebalanceStageTests` (10), `ObjectiveEvaluatorTests` (20), `ScheduleOptimizerTests` (14),
`SoeT34InvariantTests` (7), `SoeBaselineCaptureTests` (7), `WorkloadServiceIdentityTests` (5),
`OptimizerRunLogWriterTests` (3), `SoeT34CorpusReportTests` (1), `OptimizerRunLogSchemaTests` (2),
plus `WorkloadServiceScheduleTests` (16 → 18 methods after this gate).

### 2.3 Acceptance criteria and invariants

The epic's four master-plan acceptance criteria are carried by named tests that are green in the
runs above (D-H non-worsening across the 230-item corpus; determinism across 3 repeated runs;
explainability across 558 checkpoints; the structural D-J separation of validator and evaluator).
These were **re-verified as passing**, not re-measured — the closing note already re-derived every
figure independently on 2026-08-07 and this gate found no reason to dispute it.

---

## 3. Verification evidence — mutation testing

**A passing test is not evidence.** Per the standing project rule, every guard this gate relied on
was mutated to prove it can go red. Six mutations were run; all production files were reverted
afterwards (`git status` clean on `SmartStudyPlanner/` — confirmed after each).

| # | Mutation applied to production | Result | Reads as |
|---|---|---|---|
| M1 | Remove the `DiemUuTien` write-through (`task.DiemUuTien = …` → discard) | **3 red** — `GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh`, `UuTienCaoDuocXepTruoc`, and the new `TaskChuaTungDuocChamDiem_VanDuocXepLich` | CP-2 regression is genuinely guarded |
| M2 | Hide the frozen baseline artifact JSON | **1 red** — `BaselineArtifact_TonTaiVaCoCauTrucHopLe` | the compensating guard for the skipped test is real, not vacuous |
| M3 | Insert a `HanChot` token into `ObjectiveEvaluator.cs` | **1 red** — `SourceFiles_ContainNoHanChotOrDeadlineToken` | the D-G/D-J source-text scan actually scans |
| M4 | Restore the pre-T3.3 least-loaded rule | **4 red** — all 3 cases of the new prefix-packing Theory, plus `ChonNgaySomNhatConCho_ChuKhongPhaiNgayItTaiNhat` | the placement change is pinned, generally and by example |
| M5 | Delete the `EnsureOptimizerRunLogTable` call from `AppStartup` | **1 red** — *only* the new `EnsureDatabaseReady_OnPreT37Db_…` | **gap confirmed real**: nothing caught this before §3.4 |
| M6 | Delete the tier-1 deadline filter entirely | **suite fully green, 475/476** | inertness claim independently reproduced (see §3.3) |

### 3.1 The `DiemUuTien` write-through (M1)

CP-2 originally ratified *dropping* the write-through and deleting its pinning test; the
[CP-2 amendment](../plans/2026-08-06-cp2-amended-diemuutien-writethrough-restored.md) reversed that.
Because the governing decision flip-flopped over persisted user data, it was verified from the code
rather than the record: the write-through **is present** (`WorkloadServiceImpl.cs:143`) and the
pinning test **was restored**, not deleted.

The coupling it protects was confirmed at source: `RawMinutesCalculator.Calculate` reads
`task.DiemUuTien` **off the model** and returns 0 when it is ≤ 0, and `StudyTask.DiemUuTien`
defaults to 0. Removing the write-through therefore makes every task that was not scored elsewhere
silently drop out of the schedule — no exception, just an emptier screen.

M1 proves the regression cannot return silently. But all three tests it trips assert the
*mechanism*, and the existing stub decision engine (`StubDecisionEngine`, table lookup by task name)
deliberately does not reproduce the coupling — so nothing stated the *reason* the mechanism must
stay. A "make it pure" refactor would naturally delete those tests along with the write-through,
which is exactly what Card F round 1 did (`5197784`). §3.4 closes that with a consequence-level test.

### 3.2 Guard integrity (M2, M3)

Both guards locate files by repo-relative path and read them directly, so a path that fails to
resolve throws rather than passing vacuously — checked specifically, because a silently-vacuous
guard is worse than no guard.

### 3.3 The deadline-tier inertness claim (M6)

The closing note and two doc comments assert that the allocator's tier-1 deadline filter is
**provably output-inert** — it cannot change any placement the chronological tier would not already
have chosen ([proof](../plans/2026-08-06-deadline-tier-provably-inert.md)). That claim rested on
prose plus a one-off empirical confirmation.

M6 reproduces it: with tier-1 deleted outright, the **entire suite stays green (475/476)**. A
surviving mutant normally signals a coverage gap; here it is the predicted and correct result. No
test was added — the ratified design decision explicitly declines one as vacuous, and this gate did
not reopen it. Recorded as: *claim independently reproduced by mutation; no executable pin exists by
ratified design.*

### 3.4 Tests added (3 methods / 5 cases)

Each was mutation-proven red before being accepted (M1, M4, M5 above).

| Test | File | Closes |
|---|---|---|
| `GenerateSchedule_TaskChuaTungDuocChamDiem_VanDuocXepLich` | `Services/WorkloadServiceScheduleTests.cs` | states *why* the write-through must stay, using a new double (`PriorityCoupledDecisionEngine`) that reproduces the real `DiemUuTien ≤ 0 ⇒ 0 minutes` gate |
| `GenerateSchedule_DonVeNgaySomNhat_NgayDungLaTienToLienTuc_ChiNgayCuoiConCho` (Theory ×3) | `Services/WorkloadServiceScheduleTests.cs` | pins earliest-feasible as a **structural invariant** across capacities 1/2/3h — used days form a contiguous prefix from today, all but the last at the capacity ceiling, minutes preserved — rather than by a single 3-day example |
| `EnsureDatabaseReady_OnPreT37Db_TaoLaiBangOptimizerRunLogs` | `Data/AppStartupFileBasedTests.cs` | the seam tests proved `EnsureOptimizerRunLogTable` patches correctly but nothing proved `AppStartup` **calls** it; M5 confirms this was a real silent-removal gap |

One supporting change: `WorkloadServiceScheduleTests.Sut(…)` widened from `StubDecisionEngine` to
`IDecisionEngine` so a second double can be injected. No production code was modified by this gate.

---

## 4. Findings and classification

**No Critical finding. No release-blocking finding. Zero unresolved defects.**

### QA-1 — MEDIUM — *deferred to owner* — Workload Balancer header describes the old algorithm

`Views/WorkloadBalancerPage.xaml:39` reads:

> "Thuật toán **rải các bài tập chưa hoàn thành đều khắp** những ngày tới …"

After T3.3 the algorithm does not spread work evenly — it packs it into the earliest days with room.
The sentence describes the **pre-Epic-3 least-loaded rule**. Every other string on the page checked
and still accurate (the capacity ceiling text; "sắp theo Điểm ưu tiên" — priority remains the
ordering key).

**Deliberately not fixed here.** It is user-facing Vietnamese product copy; the feature is still
named "CÂN BẰNG TẢI"; and the accurate replacement is a small product-copy decision, not a
mechanical correction — a QA gate is the wrong place to make it unilaterally. Routed to the owner as
runbook scenario **C7**, with proposed wording, so the decision is made with the screen in view.

**Worth naming: this is F1's failure mode with a different blast radius.** F1 was a *document*
asserting what the code never did; this is *UI* asserting what the code used to do. Neither
self-corrects on a documentation sweep — a later reader finds a confident, fluent sentence and no
reason to doubt it. That is what makes a stale-but-plausible claim worth a MEDIUM rather than a nit.

### Accepted as ratified — not reopened

Carried forward from the closing note and the owner's D7 ruling (2026-08-07). Listed so the manual
gate does not rediscover them as defects:

- **Metric #1b** — deadline inversions reduced 250 → 220 (12%), self-miss class eliminated (17 → 0),
  pairwise residual accepted. Root cause **A1**.
- **Metric #4** — `Score(B)` not computable for 207/230 corpus items; unevaluated, recoverable.
- **F4** — the A6 cross-check's `>=` vs `>` boundary offset (113 chunks / 6 440 min).
- **The optimizer ships unwired** — zero production callers; integration is separate work (G3-1).

### Deferred / proposals — outside Epic 3 scope, not acted on

- **P1 (proposal)** — `AppStartupFileBasedTests` still does not assert that the **M8** telemetry
  tables are created through `EnsureDatabaseReady`. That is the same silent-call-site-removal gap
  M5 exposed, one epic earlier. Pre-existing; fixing it for M8 would have been scope expansion.
- **P2 (accepted debt)** — `WorkloadBalancerViewModel` has **no automated tests at all**. It is the
  primary GUI surface for the placement change, and `BuildSchedule(notify: true)` calls
  `MessageBox.Show` directly, which makes it awkward to drive headlessly. Pre-existing debt, not
  introduced by Epic 3; it is why Group C of the runbook is manual.
- **P3 (observation)** — the feature name "CÂN BẰNG TẢI" (load balancing) now describes the
  algorithm less well than it did. Renaming is a product decision well outside this gate.

---

## 5. Manual QA coverage and limitations

Recorded as manual-test requirements rather than guessed at. The following **cannot** be established
by the automated suite and are carried into the runbook:

| Requirement | Runbook | Why not automatable here |
|---|---|---|
| A real long-lived user DB upgrades without data loss | A1 | The suite builds synthetic DBs; only a real pre-Epic-3 file proves the migration against genuine accumulated data |
| The placement change as *rendered* | C1–C2 | `WorkloadBalancerViewModel` is untested (P2) and filters empty days before display; the invariant is pinned at the service layer, not at the pixel |
| Header copy vs. observed behaviour | C7 | A wording judgement, not an assertion |
| No collateral regression across other screens | E1–E6 | WPF navigation and rendering are outside the headless suite |
| `OptimizerRunLogs` stays empty in real use | B2 | Requires exercising the real app, not a fixture |

**No manual behaviour was verified by this gate.** No GUI test was run, and every result cell in the
runbook is deliberately blank.

---

## 6. Verdict

**The repository is ready for owner-led manual QA.**

- Full suite green: 475 passed / 0 failed / 1 skipped / 476 total; build 0 errors.
- Epic 3 / SOE targeted tests present and green.
- Required regression coverage present, and **proven capable of failing** (six mutations).
- One real coverage gap found (M5) and closed; two evidence gaps found and closed (§3.4).
- No Critical finding; no release blocker; QA-1 classified and routed, remaining findings ratified,
  deferred or recorded as proposals.
- No production code changed by this gate.

---

## Decisions made (ADR-style)

### D1 — Scope the gate to the two production-reachable changes, not the epic's whole code surface

- **Why:** Epic 3's optimizer seams have zero production call sites. Spending the gate adding tests
  to `ScheduleOptimizer`/`LoadRebalanceStage` would have produced impressive-looking coverage over
  code no user can reach, while the two changes that *do* run on launch — the allocator placement
  rework and the startup schema patch — went unexamined.
- **What for:** every finding in §3 and §4 is on a code path a user actually executes. M5 (the
  `AppStartup` call site that nothing guarded) would not have been found by a gate that measured
  itself in tests-added-per-seam.
- **Experience:** the 470-test baseline was green *and* silently missing a guard on a production
  startup call. "The suite is green" and "the release is covered" were two different statements here,
  and only the narrowing separated them.

### D2 — Mutate every guard relied on, including the ones that were expected to survive

- **Why:** the project's standing rule is that a passing test is not evidence — a guard must be shown
  to go red. Applied to six guards, including two (M2, M3) whose failure mode would be silent
  vacuity rather than a wrong answer.
- **What for:** M5 turned an assumption ("the seam tests cover T3.7") into a measured fact ("they
  cover the seam, nothing covers the call site"). M6 turned a prose proof into a reproduced result.
- **Experience:** M6 is the instructive one. A surviving mutant is normally a coverage gap; here it
  was the *predicted* outcome of a ratified inertness proof, and the right response was to record the
  confirmation and add nothing. Reading the mutation score without reading the decision behind it
  would have produced a vacuous test and a claim of improved coverage.

### D3 — Classify the stale UI copy as a finding; do not edit it

- **Why:** QA-1 is a genuine mismatch between what the screen claims and what the code does, and it
  was introduced by this epic. But the correct replacement sentence is not mechanical — priority
  still orders tasks, capacity still caps each day, and the feature is still called "CÂN BẰNG TẢI".
  It is user-facing Vietnamese product copy on a page the owner has design opinions about.
- **What for:** the owner decides the wording at runbook C7 with the actual screen in front of them,
  instead of inheriting a string this gate invented — and the finding is on record either way.
- **Experience:** the pull toward "it's one line, just fix it" was strong and would have been scope
  expansion dressed as tidiness. The distinguishing question that settled it: *is the correct output
  determined by the code, or by a preference?* Where it is a preference, a QA gate reports.

### D4 — Reconcile the test-count delta in this report rather than amending the closing note

- **Why:** the closing note's 470/471 was accurate at its SHA, and criterion 13's "≥ 391" still holds
  at 475. Editing a ratified closure document to track a later, unrelated addition would trade a
  correct historical record for a perpetually-maintained one.
- **What for:** a reader who runs the suite today and sees 476 finds the +5 itemised in §2.1 with its
  cause, without either document having to lie.
- **Experience:** F1's lesson is about documents asserting things the code never did — not about
  documents recording a measurement that was true when taken. Distinguishing the two is what kept
  this from becoming an unnecessary edit to ratified text.
