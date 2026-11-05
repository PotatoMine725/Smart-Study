# G3-1 — Wire the SOE into production: Phase 0 recon, effect size, wiring options

**Date:** 2026-11-05 · **Author:** Claude Code (agent session, worktree `.claude/worktrees/g3-1-recon`,
branch `docs-g3-1-soe-wiring-recon`) · **Base:** `origin/dev` `c58b0fa`

**Scope.** Roadmap §A.4 **G3-1**: should Epic 3's optimizer (`IScheduleOptimizer.Optimize`) be called
from production, and if so where? This is Phase 0, which measures and proposes and then **stops for the
owner**. It adds **no production code**: one test-only measurement class, one stdout data file and this
report. It does not touch `Sync/**`, `Mutations/**`, the schema, packages, `IScheduleOptimizer`'s
signature or the `SoeWeights` defaults.

Labels: **OBSERVED** means a command or test ran and printed it. **INFERENCE** means it comes from reading
code, not from running it. **NOT RUN** means it was not executed.

---

## 0. Verdict (read this first)

**The effect is small, positive on the objective, zero on feasibility, and it always takes work away from
"today".**

- **Corpus (230 items).** `Optimize` changes **36 of 230** schedules (15.7%) and lifts the summed
  objective by **+2.78%**. It never repairs a deadline violation: tasks 328→328, chunks 862→862, overdue
  minutes 50 480→50 480. It never makes a score worse. These figures reproduce the deleted 2026-08-20
  probe (doc-sync report §2) **line for line**, so that recollection is now a committed, re-runnable
  measurement (closes doc-sync follow-up **M-1**).
- **Generated family (120 scenarios).** **21 of 120** change (17.5%), objective **+8.07%**, violations
  again unchanged everywhere. **The effect shrinks as the semester grows**: 37.5% of scenarios change at
  5 tasks, 8.3% at 80, **0% at 160**. It is also zero at 1 h/day capacity and 5% under tight deadlines.
  In the scenarios Optimize leaves alone, the most common reason at the larger sizes is that the
  deadline gate rejected the proposed move (`Rejected_Infeasible`).
- **What a user would see.** In **55 of the 57** changed scenarios (corpus + family), today's planned
  minutes **go down**. In the other 2 they stay the same. They **never go up**. The stage moves a chunk
  from the fullest day (today, under earliest-feasible packing) to the lightest used day, which is
  usually the last one. On the Dashboard, which only shows today, wiring Optimize therefore means
  "today's plan gets shorter" in roughly one schedule in six on the corpus. Moves also create new
  display oddities: two "(Phần n)" rows of the same task on one day (25 new cases), and part numbers
  out of date order (17 cases).
- **Cost.** Negligible on the UI thread: Optimize p95 is ≤ 1.2 ms at 160 tasks / 511 chunks. Persisting
  telemetry is the dominant cost, at ~21 ms p50 and 36 ms p95 per call on file-backed SQLite.
- **Epic 2.** No wiring option adds a synced or stamped write. X-19 stays green unchanged (§3e).

Whether +2.8% / +8.1% objective on about one schedule in six, paid for with a shorter "today", is
worth wiring is **the owner's call (§4, Q-1)**. This report sets no threshold.

### Per-question verdicts

| Q | Question | Answer | Evidence |
|---|---|---|---|
| 3a | Effect size | Small (+2.8% corpus / +8.1% family on objective), 0 on feasibility, falls to 0 at large size | OBSERVED |
| 3b | Seam | Four options (A–D), not picked. A and C keep `IWorkloadService` unchanged; B forces a signature change or a new port | INFERENCE |
| 3c | User-visible change | Balancer: one chunk moves to a later day. Dashboard: today's minutes drop (55/57) or stay the same (2/57) | OBSERVED (data) + INFERENCE (UI) |
| 3d | Telemetry | 2–8 rows per call. Calls happen on Balancer open, Balancer button and Dashboard load; **not on slider moves**. No task text stored | OBSERVED + INFERENCE |
| 3e | Epic 2 | No synced or stamped write is added; X-19 unchanged | OBSERVED (suite) + INFERENCE |
| 3f | Performance | No need for off-thread work on Optimize's account; Persist is 20–40× costlier than Optimize | OBSERVED (Debug, one machine) |
| 3g | Gaps | Nothing rules on moving work out of today/later (tension with CP-3), on projection naming, or on a telemetry policy; also two overview §5.11 drift items | INFERENCE |

---

## 1. Git state (OBSERVED)

- `rtk git fetch`. Local `dev` == `origin/dev` == `c58b0fa` (0 ahead / 0 behind). The owner's checkout
  has uncommitted changes (`AGENTS.md`, `CLAUDE.md`, skills, `docs/s2-reader-package/…`). Not touched.
- Preconditions: **PR #113 MERGED** 2026-10-31T00:11Z, **PR #114 MERGED** 2026-10-31T01:38Z, both into
  `dev`.
- Worktree `.claude/worktrees/g3-1-recon`, branch `docs-g3-1-soe-wiring-recon`, created from `origin/dev`.
- Baseline `dotnet test SmartStudyPlanner.slnx` at `c58b0fa`: **1160 passed / 0 failed / 1 skipped**
  (1161). The skip is the existing `SoeBaselineCaptureTests.CaptureBaseline_…` (frozen-artifact guard).

## 2. Impact (OBSERVED: GitNexus `impact` upstream, cross-checked with `git grep`)

| Symbol | Risk | Production callers | Notes |
|---|---|---|---|
| `WorkloadServiceImpl.GenerateSchedule` | **CRITICAL** | `WorkloadBalancerViewModel.BuildSchedule` (ctor + `GenerateScheduleCommand`), `BalanceWorkloadStage.Execute` → `PipelineOrchestrator.Execute` → `DashboardViewModel.LoadDuLieuDashboard` | 21 direct callers, 19 of them tests (`WorkloadServiceScheduleTests`, Soe harness). One flow. |
| `WorkloadServiceImpl.GenerateScheduleWithIdentity` (internal) | CRITICAL (transitively, through `GenerateSchedule`) | only `GenerateSchedule` | The other direct callers are tests (`WorkloadServiceIdentityTests`, `SoeScheduleMetrics.RunScenarioWithIdentity`). |
| `ScheduleOptimizer` | MEDIUM | **none** | 9 direct callers, all in `SmartStudyPlanner.Tests/Services/Soe/`. |
| `OptimizerRunLogWriter` | LOW | **none** | 4 direct callers, all tests. |

`git grep` agrees: no reference to `ScheduleOptimizer`, `IScheduleOptimizer`, `OptimizerRunLogWriter` or
`SoeWeights` outside `Services/Soe/`, apart from the `OptimizerRunLogs` DbSet (`AppDbContext.cs:73`) and the
table bootstrap (`AppStartup.cs:41`). No `ServiceLocator` registration; `IWorkloadService` is registered
at `ServiceLocator.cs:79`. Any wiring that runs `Optimize` inside `GenerateSchedule` therefore lands on a
CRITICAL symbol. That is a warning for Phase 1, not a blocker for Phase 0, which edits nothing in
production.

**Known state, verified** (the task's list, line by line):

| Claim | Status | Where |
|---|---|---|
| `Optimize(IReadOnlyList<ScheduledItem>) → (schedule, OptimizeReport)`, no capacity context | ✔ OBSERVED | `IScheduleOptimizer.cs:25`; weights come through the constructor (`ScheduleOptimizer.cs:42-46`) |
| `GenerateScheduleWithIdentity` is internal and returns `ScheduledItem`s; `IWorkloadService` exposes only `GenerateSchedule` | ✔ | `WorkloadServiceImpl.cs:135`, `IWorkloadService.cs:19` |
| Two production callers | ✔ | `WorkloadBalancerViewModel.cs:65`, `BalanceWorkloadStage.cs:41` |
| `GenerateSchedule` writes `task.DiemUuTien` (CP-2 amended) | ✔ | `WorkloadServiceImpl.cs:154-161` |
| `OptimizerRunLogs` table exists; zero production rows expected | ✔ (table) / INFERENCE (rows: nothing in production calls the writer) | `TelemetrySchema.cs:97`, `AppStartup.cs:41` |
| The schedule is computed, not persisted | ✔ INFERENCE (code read): no `ScheduleDay`/`ScheduledItem` type under `Data/`, `Infrastructure/` or `Sync/`. It lives in `WorkloadBalancerViewModel.Schedule` and `PipelineContext.Schedule` only | `PipelineContracts.cs:56,78` |
| Fence guards synced writes (X-19); E-2: Derived-only changes don't stamp | ✔ | `SyncedEntityWritePathFenceTests.cs:62`; `DiemUuTien` is `FieldClass.Derived` at `MergeSurfaceRegistry.cs:83` |

---

## 3. Phase 0 answers

### 3a. Effect size (OBSERVED)

**Instrument.** `SmartStudyPlanner.Tests/Services/Soe/SoeG31EffectSizeMeasurementTests.cs`,
`[Trait("Kind","Measurement")]`. It compares S0 = `GenerateScheduleWithIdentity` output against
S = `Optimize(S0)`, pairing items by index. The stage only rewrites `Date`, and the harness throws if any
other field changes. Weights are `SoeOptimizeCorpusMetrics.DefaultWeights` (= record defaults, per G3
governance G3-1). It asserts nothing about effect size or latency.

**Can it go red?** `Instrument_DetectsKnownChange_AndKnownNoChange` runs the two real-stage fixtures from
`ScheduleOptimizerTests`. The single-used-day fixture must report no change, and the imbalanced fixture
must report a move, a later move, a day-0 change, a smaller spread and day-0 = 70 min. It passes.
**Reproduction check:** the corpus half prints the exact line set of the deleted 2026-08-20 probe. Every
line matches. The probe's "VIOLATIONS 862/862" turned out to be chunk-level
(`ConstraintValidationResult.Violations.Count`); task-level is 328/328. Full stdout:
[`data/2026-11-05-g3-1-effect-size-stdout.txt`](data/2026-11-05-g3-1-effect-size-stdout.txt).

**Generated family.** Seed `20261105`, today 2026-11-02. Task count {5, 15, 40, 80, 160} × deadline
density {tight 0–3 d, medium 0–14 d, loose 7–45 d} × capacity {1, 3, 5, 8} h × 2 replicates = 120.
Minutes are 15–240 per task, with 8 subjects (2 at n=5) and unique task names, because `MaTask` comes
from MD5(name). n=80 matches the reference semester used by `LocalSaveLatencyMeasurementTests`
(8 × 10).

| Metric | Corpus (230) | Family (120) |
|---|---|---|
| Schedules changed | **36 (15.7%)** | **21 (17.5%)** |
| Violating tasks in → out | 328 → 328 | 5 363 → 5 363 |
| Violating chunks in → out | 862 → 862 | 10 534 → 10 534 |
| Overdue minutes in → out | 50 480 → 50 480 | 671 194 → 671 194 |
| Objective sum in → out | 437.8589 → 450.0417 (**+2.78%**) | 116.1299 → 125.4998 (**+8.07%**) |
| Score improved / worse | 36 / 0 | 21 / 0 |
| Δ on changed, relative: min / median / max | 4.0% / 14.3% / 111.9% | 5.3% / 45.1% / 89.9% |
| Where the gain comes from (term sums) | LoadBalance +7.23, Fatigue +4.65, Context +0.31, Session 0, Fragmentation 0 | LB +3.08, FP +6.18, CC +0.11, 0, 0 |
| Chunks moved / of | 50 / 1 873 (2 295 min) | 52 / 13 368 (5 655 min) |
| Move direction later / earlier | **37 / 13** | 26 / 26 |
| Max−min daily spread (sum) | 9 275 → 7 390 min | 15 741 → 12 187 min |
| Changed scenarios whose day 0 changed | **36 / 36** | **21 / 21** |
| Day-0 minutes (sum) | 20 735 → 19 590 | 30 410 → 28 769 |
| New same-task-same-day pairs | 17 | 8 |
| New "(Phần n)" order breaks | 7 | 10 |
| Chunks newly past deadline | 0 | 0 |
| Days over capacity after Optimize / days emptied | 0 / 0 | 0 / 0 |
| Termination FixedPoint / PassLimit | 229 / 1 | 113 / 7 |
| Passes 1 / 2 / 3 / 4 | 194 / 24 / 11 / 1 | 99 / 6 / 5 / 10 |

**Where the effect lives (family slices).**

| Slice | Changed | Δ objective | Unchanged because infeasible / no-op |
|---|---|---|---|
| 5 tasks | 37.5% | +12.8% | 5 / 8 |
| 15 tasks | 29.2% | +16.1% | 7 / 10 |
| 40 tasks | 12.5% | +5.2% | 12 / 9 |
| 80 tasks | 8.3% | +1.5% | 12 / 10 |
| 160 tasks | **0%** | 0 | 11 / 13 |
| tight / medium / loose | 5% / 20% / 27.5% | +1.4% / +10.2% / +12.6% | 22·16 / 15·17 / 10·17 |
| 1 h / 3 h / 5 h / 8 h | **0%** / 13% / 23% / 33% | 0 / +6.0% / +14.5% / +12.9% | 3·27 / 16·9 / 15·7 / 13·7 |

The corpus categories `infeasible` (60) and `capacity-edge` (10) never change. Of 194 unchanged corpus
items, 190 stop on `Rejected_NoImprovement` (the stage was a no-op) and 3 on `Rejected_Infeasible`.

**Plain statement.** The effect is **not ~0**: about one schedule in six changes, and on those the
objective rises by a median 14% (corpus) to 45% (family). It is, however, purely load-smoothing.
`LoadBalance` and `FatiguePenalty` carry it; feasibility never changes, and it fades to nothing on large
or tight semesters.

**Why it fades (INFERENCE, consistent with the reason codes).** Earliest-feasible packing fills days
to capacity in date order, so the only light used day is the last one. `LoadRebalanceStage` moves a
chunk of at most (max − min) minutes onto it. At large sizes that last day is past most deadlines, and
the move is vetoed as `Rejected_Infeasible`. At 1 h/day the eligible gap is usually smaller than any
chunk, so the stage is a no-op.

### 3b. Seam options (INFERENCE; none picked)

| | **A. Inside `WorkloadServiceImpl.GenerateSchedule`** | **B. `BalanceWorkloadStage` only** | **C. Decorator / flag: `OptimizingWorkloadService : IWorkloadService`** | **D. Don't wire (status quo)** |
|---|---|---|---|---|
| Callers affected | Both: Balancer and Dashboard | Dashboard only | Both, through DI. Code that builds `WorkloadServiceImpl` directly (tests) is unaffected | None |
| `IWorkloadService` signature | Unchanged | **Changes**: the stage only sees `IWorkloadService`, whose sole schedule member returns `List<ScheduleDay>` (identity already lost). It needs `GenerateScheduleWithIdentity` made public on the interface, or a new port | Unchanged | Unchanged |
| Projection `ScheduledItem → ScheduleDay` | Inside the impl: keep the allocator's `Days` frame (7-day window, "Hôm nay"/"Ngày mai" names, overflow days), clear `Tasks`/`TotalMinutes`, then re-add the optimized items grouped by `Date`. `Optimize` never sees the frame, so it must be kept from S0. Never empties a day (OBSERVED 0) and never creates one (INFERENCE: the destination is always a used day) | Same projection, but in the stage | Same projection, in the decorator, which calls the internal `GenerateScheduleWithIdentity` on the concrete type (same assembly) | n/a |
| DI / `ServiceLocator` | Register `IScheduleOptimizer` (stage list `[LoadRebalanceStage]`, `ConstraintValidator`, `ObjectiveEvaluator`, `new SoeWeights()`) and add it to `WorkloadServiceImpl`'s constructor | Same registration, plus the new port | Register the decorator as `IWorkloadService` in place of `WorkloadServiceImpl` (`ServiceLocator.cs:79`); the flag is read here | — |
| Test impact | **High.** `WorkloadServiceScheduleTests` pins the allocator through `GenerateSchedule`, including `…NgayDungLaTienToLienTuc_ChiNgayCuoiConCho` ("only the last day has room"), which a committed move breaks by construction. Either those tests construct the impl without an optimizer (optional ctor parameter, null = off) or they are re-pinned | Medium | **Low.** Allocator characterization stays as-is; the decorator gets its own tests | None |
| Consistency | Balancer and Dashboard agree | **Balancer and Dashboard disagree** on the same semester (today differs in ~1 in 6) | Agree | Agree |
| Kill switch | Needs an extra flag | Needs an extra flag | Natural: register the impl or the decorator | — |

### 3c. What the user would see (data OBSERVED; UI mapping INFERENCE)

- **Nothing changes** in ~84% of corpus schedules (and 100% at 160 tasks or at 1 h/day). The output is
  identical to today's: same days, same rows.
- **Something changes** (one move, sometimes 2–4 over passes):
  - **Workload Balancer** (all non-empty days). One or more chunks leave the fullest day, almost always
    "Hôm nay", whose bar stops touching the capacity line ("cột chạm vạch là ngày đã đầy",
    `WorkloadBalancerPage.xaml:169`). They land on the lightest used day, usually the last one. Some
    tasks show two "(Phần n)" rows on the same day, or "(Phần 3)" dated before "(Phần 2)". The success
    popup text is unchanged.
  - **Dashboard** (only `Schedule.FirstOrDefault()`, `DashboardViewModel.cs:146`). "KẾ HOẠCH HỌC TẬP HÔM
    NAY (N phút)" shows a **smaller N**, or the same N with different rows. It **never shows more**
    (0 of 57 changed scenarios).
- **A toggle / kill switch** is an owner question (Q-3). Option C provides one structurally.

### 3d. Telemetry (OBSERVED counts; frequency INFERENCE)

- **Who.** Whoever owns the seam: the impl (A), the stage (B) or the decorator (C). It would call
  `OptimizerRunLogWriter.Persist(runId, report)` with the existing `Func<AppDbContext>` factory
  (`ServiceLocator.cs:54`).
- **How often.** Once per schedule build: on every **Workload Balancer open** (constructor), every
  **"generate" button** press (`GenerateScheduleCommand`, `WorkloadBalancerPage.xaml:84`), and every
  **`LoadDuLieuDashboard`** (Dashboard construction and after Focus Mode, `DashboardViewModel.cs:87,330`).
  **The slider does not rebuild**: it only sets `CapacityHours` (`:73`), and `IsScheduleStale` flags
  the gap. The task's "slider moves can fire many runs" premise does not hold on current code.
- **Volume.** 2 rows per call in 99 of 120 calls, 4/6/8 rows in the rest (2 × passes). Growth is
  unbounded: there is no retention or pruning anywhere.
- **Dedupe.** `Optimize` is deterministic (pinned by `SoeT34InvariantTests.Optimize_CorpusRun_IsDeterministic…`),
  so repeated opens of an unchanged semester write identical rows under new `RunId`s. An input-hash
  dedupe or a "skip when k* = 0 on pass 1" rule (194 / 230 = 84% of corpus calls, 99 / 120 of family calls) would cut most rows. That is a
  policy choice, Q-4.
- **Privacy / size.** A row holds `RunId`, timestamps, termination, pass/checkpoint indices, k*,
  violation count, overdue minutes, score and reason (`OptimizerRunLogRow.cs`). There are **no task
  names, ids or subject names**. Not a synced table (§3e).

### 3e. Epic 2 interaction (OBSERVED where stated)

- **Synced / stamped writes.** No option writes to the six synced tables. `Optimize` is pure over
  `ScheduledItem` (INFERENCE, code read: `ScheduleOptimizer.cs`, `LoadRebalanceStage.cs:94-96` copy the
  list). It runs **after** the existing `DiemUuTien` write-through, which stays exactly as it is
  (`Derived`, so not stamped under E-2).
- **Telemetry save.** `AppDbContext.SaveChanges` always runs `SyncStamper.Apply`
  (`AppDbContext.cs:188-192`), but the stamper skips any entity that is not `ISyncMetadata`
  (`SyncStamper.cs:53`), and `OptimizerRunLogRow` is not one. The existing writer tests already save
  through this path (`OptimizerRunLogWriterTests`, green in the baseline).
- **X-19.** `OptimizerRunLogWriter.cs` is already production code, and its `db.OptimizerRunLogs.Add(`
  does not match X-19's synced pattern (`SyncedEntityWritePathFenceTests.cs:39-44`). X-19 is green at
  the baseline and with this branch (OBSERVED, full suite). Options A–C add a `Persist` call, not a
  DbSet write, so X-19 stays green unchanged (INFERENCE).
- **Fence / sync files.** None are touched by this phase. None need to be touched by A, B or C
  (INFERENCE). **No STOP condition triggered.**

### 3f. Performance (OBSERVED: Debug build, this machine, 3 warm-up + 15 samples per scenario)

| Tasks | Max chunks | Allocator* p50 / p95 / max ms | Optimize p50 / p95 / max ms | Alloc + Opt p95 ms |
|---|---|---|---|---|
| 5 | 15 | 0.026 / 0.059 / 1.576 | 0.043 / 0.093 / 0.151 | 0.129 |
| 15 | 50 | 0.096 / 0.220 / 1.424 | 0.128 / 0.295 / 0.729 | 0.408 |
| 40 | 131 | 0.254 / 0.677 / 2.858 | 0.171 / 0.537 / 4.997 | 1.044 |
| 80 | 260 | 0.639 / 2.000 / 2.703 | 0.264 / 0.695 / 2.255 | 2.652 |
| 160 | 511 | 0.882 / 7.439 / 8.730 | 0.279 / 1.104 / 3.158 | 8.523 |

\*Includes building the `HocKy` fixture, so it is an upper bound on `GenerateScheduleWithIdentity`.

`OptimizerRunLogWriter.Persist` on file-backed SQLite (WAL, `EnsureCreated`): **p50 20.6 / p95 36.3 /
max 183 ms** per call (120 calls). The Balancer constructor already does a synchronous file write
(`SaveCapacity`, `WorkloadBalancerViewModel.cs:63`) on the UI thread.

**Reading.** `Optimize` itself does not justify going async (≈1 ms at 2× the reference semester).
Writing telemetry synchronously on the UI thread would be the dominant added cost, and its tail
(183 ms) is noticeable. Fire-and-forget persistence, the pattern `App.xaml.cs` warmups already use, is
the obvious candidate; deciding is Q-4.

### 3g. Gaps against the ratified decisions (INFERENCE)

1. **Direction of moves vs CP-3.** T3.3/CP-3 ruled earliest-feasible placement (work as early as
   possible). `LoadRebalanceStage` (T3.9 §8, ratified) moves work off the earliest full day, usually
   today, and onto a later one: 37 later vs 13 earlier on the corpus, and today never gains. Each
   ruling is internally consistent; nothing rules on the composition. **Undecided, and the main thing
   wiring would change.** (Q-2)
2. **Projection.** G2/T3.8 define `ScheduledItem` but not how an optimized list is projected back:
   ordering within a day, and whether "(Phần n)" labels are renumbered or merged when two chunks of a
   task share a day. Undecided. (Q-5)
3. **Telemetry policy.** G2-6 says T3.7 "persists these rows" but sets no frequency, dedupe or retention
   policy. Undecided. (Q-4)
4. **G3 weight governance (G3-1 there).** Satisfied if production builds `new SoeWeights()`: the record
   defaults are the ratified home and are numerically identical to `SoeOptimizeCorpusMetrics.DefaultWeights`.
   Its G3-2 note that the all-zero vector is unreachable stays true. **Nothing violated.**
5. **G2 metric #4 (objective delta vs baseline)** was defined against the old allocator B and stays
   unevaluated for arm 2. This report measures Optimize against S0, a different and complementary
   comparison; it does not close F3's B-side question.
6. **Doc drift found in passing** (not fixed here; follow-up F-2). `docs/architecture/overview.md` §5.11
   writes `Optimize(schedule, weights)`, but weights come through the constructor
   (`IScheduleOptimizer.cs:25`). It also says the validator guards "deadline / capacity / calendar";
   only a deadline predicate exists (`ConstraintValidator.cs`, `IDeadlinePolicy.cs:64`), and capacity is
   preserved by the stage's gap rule, not by the validator (OBSERVED 0 over-capacity days).
7. **Deadline tier inertness** (`docs/plans/2026-08-06-deadline-tier-provably-inert.md`). Optimize runs
   after the allocator and leaves day capacity untouched during allocation, so wiring it does not make
   the tier live (INFERENCE).

---

## 4. Owner questions

- **Q-1 (the G3-1 decision).** Wire or not, given §0: about one schedule in six changes, objective
  +2.8% (corpus) / +8.1% (family) on the whole set, zero feasibility effect, no effect on large or
  tight semesters, cost ≈ 1 ms plus telemetry.
- **Q-2.** If wired: is "Optimize may move work out of today onto a later day" acceptable as the
  composition of CP-3 and T3.9 §8? If not, which options are on the table: restrict the destination,
  exclude day 0 as a source, or accept it as is? Any change to the stage is a new T3.9-level ruling.
- **Q-3.** Seam: A, B, C or D (§3b), and whether a user-visible toggle or only a code-level kill switch
  is wanted.
- **Q-4.** Telemetry: persist every call, dedupe, or only calls with k* > 0? Synchronous or
  fire-and-forget? Any retention cap?
- **Q-5.** Projection naming: keep the allocator's "(Phần n)" labels as-is after a move, renumber them
  by date, or merge same-day chunks of one task?

## 5. Not run

- No production wiring, of any option, was built or run. Every claim about A–C is INFERENCE.
- The real app UI was not launched. §3c's UI mapping comes from code (XAML + VMs) plus harness data.
- Latency comes from a **Debug** build on one machine. Release and other hardware: NOT RUN. The existing
  gated latency test (`SoeT34InvariantTests.Runtime_…`, < 2 s p95) was not re-run separately; it is
  part of the full suite, which passed.
- Real user semesters: NOT RUN (no production data used). Both families are synthetic.
- Corpus scenarios with `ThoiGianDaHoc > 0` or completed tasks: not varied by the generated family.
- `gitnexus_detect_changes`: **NOT RUN (not applicable).** The GitNexus index `Smart-Study` points at the
  owner's main checkout, not this worktree, so running it would analyse the owner's uncommitted diff.
  The worktree path is not an indexed repo (the tool rejects it). Replaced by the `git status` scope
  check in §6. This branch adds only untracked new files, so no indexed symbol changes.

## 6. Verification (OBSERVED)

- `dotnet test SmartStudyPlanner.Tests --filter FullyQualifiedName~SoeG31EffectSize --logger "console;verbosity=detailed"`:
  **4 passed / 0 failed**. Output saved in `data/2026-11-05-g3-1-effect-size-stdout.txt`.
- `dotnet test SmartStudyPlanner.slnx` on this branch: **1164 passed / 0 failed / 1 skipped** (1165).
  That is the baseline 1160 + the 4 new tests; the skip is the same pre-existing one.
- Diff scope (`git status --short`): exactly three new files, namely
  `SmartStudyPlanner.Tests/Services/Soe/SoeG31EffectSizeMeasurementTests.cs`,
  `docs/reports/data/2026-11-05-g3-1-effect-size-stdout.txt` and this report. No file under
  `SmartStudyPlanner/` changed, and nothing under `Sync/**` or `Mutations/**`.

## 7. Follow-ups

| # | Item | Owner | Where it belongs | Status |
|---|---|---|---|---|
| F-1 | G3-1 decision record (doc-sync **M-2**), written once Q-1..Q-5 are answered | owner (decision) / agent (write-up) | `docs/plans/` | needs owner decision |
| F-2 | Fix overview §5.11 drift (§3g.6) | agent | `docs/architecture/overview.md` | defect candidate (docs) |
| F-3 | Roadmap §A.4 G3-1: "effect size does not exist as a durable artifact" is no longer true after this PR merges; point to this report | agent | `docs/specs/system_roadmap.md` | deferred until merge |
| F-4 | Doc-sync report M-1 is closed by this harness | agent | `docs/reports/2026-08-20-doc-synchronization.md` follow-ups | knowledge only |

---

## Decisions made

### D1 — Measure Optimize against its own input (S0), not against the old allocator B
- **Why it had to be made.** The committed T3.4 artifact already compares S with the frozen pre-T3.3
  baseline B. Re-running that would not answer "what does wiring buy", because B is not what production
  runs today.
- **What it's for.** S vs S0 is exactly the delta the user would see if `Optimize` were switched on, and
  it is computable for 100% of items. B-side scores are not computable for 90% (closing-note F3).
- **Experience.** When a gate question is "what does switching X on change", measure against the
  current output, not the historical baseline. They answer different questions.

### D2 — Reproduce the deleted probe line for line before extending it
- **Why.** The doc-sync report held figures from an instrument that no longer existed. The new harness
  had to show it measures the same thing before its new numbers deserved trust.
- **What it's for.** All seven probe lines matched. The one apparent mismatch (862 vs 328) was resolved
  by adding the chunk-level count, not by adjusting the harness toward the old number.
- **Experience.** An unexplained difference against a recollection is a unit question first. Print both
  units rather than pick one.

### D3 — Measurement-only, plus one asserting instrument check; no write-or-verify JSON
- **Why.** Latency is non-deterministic, and a new gating artifact would turn a recon into a new
  invariant nobody asked for. But a null result from an instrument that cannot see a change is not
  evidence.
- **What it's for.** The effect-size Facts never fail on numbers. `Instrument_DetectsKnownChange_AndKnownNoChange`
  fails if the change detector goes blind. Stdout is committed as a dated data file for provenance.
- **Experience.** Keep "this test can go red" and "this test gates a number" apart. Phase 0 needs the
  first, not the second.

### D4 — Measure the user-visible proxies (day 0, move direction, projection hazards), not just the objective
- **Why.** The objective says "+2.8%", but the user sees today's plan on the Dashboard. The advisor
  pass flagged that earliest-feasible packing makes today the likely move source.
- **What it's for.** This turned a vague "small effect" into the concrete owner question Q-2: in 55 of
  57 changed schedules, today's plan gets shorter.
- **Experience.** For a scheduling change, always report what happens to *today*. It is the only day
  one of the two surfaces shows.

### D5 — Do not pick a seam
- **Why.** The task forbids it, and the choice depends on Q-2/Q-3 (whether today may lose work, and
  whether a kill switch is wanted), which are owner calls.
- **What it's for.** §3b lays out the options with their concrete cost, notably that B forces an
  `IWorkloadService` change and A breaks a pinned allocator characterization test, so the owner chooses
  with the trade-offs visible.
- **Experience.** In a recon, the useful output for a CRITICAL-risk symbol is "which pinned tests does
  each option break", not a recommendation.
