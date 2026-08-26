# Smart Study Planner — Changelog

> Synced 2026-05-21 from `superpowers/reports/*-change-log.md`, `consolidated-change-report.md`, `phase-next-*-report.md`, `m6-1-completion.md`, `dev-reset-clean-slate-report.md`, `ui-ux-phases-a-f-implementation-report.md`, and `bug-report.md`.
>
> Format: one row per shipped change, newest first. Verification column shows the test count at the time of merge.

## 2026-08-26 — DFD-9a: prediction instrumentation **fixed** — *no user-visible change*

| Area | Change | Verification |
|---|---|---|
| Core/Scheduling + Services | `PredictStudyMinutes` returns `StudyTimePredictionResult` instead of `int` + `out bool isMlPrediction`. The `out` seam had nowhere to put `Confidence` and silently dropped it — root cause, not a symptom | build 0 errors; 8 test doubles updated |
| Models + ViewModels | `TaskDashboardItem` gains `PredictedMinutes` / `Confidence`; `DashboardViewModel` populates both **on both branches**; `FocusViewModel` logs them instead of literal `null` | **492 pass** (487 → 492) |
| Tests | 5 new, each proven able to fail by mutating production: seam ×2, dashboard hop ×2 (the project's first `DashboardViewModel` tests), rejected-branch write ×1. `OutcomeRow_MappingIsCorrect` rewritten from `Assert.Null` — it failed `Expected: 45, Actual: null` on the unfixed tree | `gitnexus_detect_changes`: 2 affected processes, both expected |

**The mutation that mattered:** deleting the dashboard's two assignments left all 490 tests green — the
hop that actually caused the defect was covered by nothing. That is why the dashboard tests exist.

**Not undone, only stopped:** rows written before today remain unusable for calibration; the value is
not reconstructible. `Confidence = 0` stays ambiguous between "model not ready" and "zero agreement" —
separating them needs DFD-5 provenance, deliberately out of scope. No threshold moved; F-1 remains
deferred, now with its prerequisite met. **The end-to-end check has not been run.**

## 2026-08-26 — Data foundation: **decision phase closed, nine policies ratified** — *no user-visible change*

> **Nothing shipped, and nothing was implemented.** This entry exists because the project's position on
> its own training data changed: it now formally holds **zero verified real user rows**, and the
> documents that said otherwise have been corrected. A change to what the repository claims is true is
> a change to the project's state.

| Area | Change | Verification |
|---|---|---|
| Owner ruling | **P-1 … P-3 resolved, DFD-1 … DFD-9 ratified.** Real-data policy, canonical annotation spec before further labelled data, two-tier Gold (**Gold-A** authored / **Gold-R** real), dual-layer provenance, synthetic-for-Silver-only, external datasets evaluable but not ingestible, owner as sole Gold authority. Filed at [`plans/2026-08-26-data-foundation-owner-decision-handoff.md`](plans/2026-08-26-data-foundation-owner-decision-handoff.md) | Ruling appended to the brief; audit's `OD-1…OD-6` + `K.1`/`K.2` all closed |
| Provenance | **`collected_v4.csv` is AI-generated, not collected** — owner templates → Meta AI generation → GitHub Copilot labelling. The 189 untraceable rows descend from ~2 000 Meta AI rows aggregated by Copilot; the 136 in the seed stay | Owner recall (**ruling**, no written record); corroborates the audit's 7 measured regularities |
| Correction pass | **12 documents + 3 tool files corrected** under DFD-1 — live artifacts in place, dated artifacts by appended amendment with the superseded passage marked. Includes the 96.2% footnote, which was itself a *wrong correction*: the figure predates `collected_v4` by 13 days | [`reports/2026-08-26-data-foundation-correction-pass.md`](reports/2026-08-26-data-foundation-correction-pass.md) |
| Defect raised | **`PredictedMinutes` / `Confidence` written as `null`** on every `StudyTimeOutcomeLog` row — the telemetry records *that* a prediction happened but not *what it was*. Raised under DFD-9a; **fixed the same day** — see the entry above | [`plans/2026-08-26-prediction-instrumentation-defect.md`](plans/2026-08-26-prediction-instrumentation-defect.md) |
| Next phase | **Data Maturation & Coverage Expansion proposal** written, staged behind taxonomy review → annotation spec → provenance → Gold | [`plans/2026-08-26-data-maturation-coverage-expansion.md`](plans/2026-08-26-data-maturation-coverage-expansion.md) — proposal, not authorization |

**Figures re-scoped, none retracted:** 96.2% (2026-06-05) is an in-distribution held-out score over the
698-row authored seed; 97.24%/97.25% (2026-06-25) remains a valid before/after regression check but is
not accuracy on real input; the S0 encoder comparison measured cross-authoring-process generalization.
Every measurement stands — the *inferences* drawn from them are what narrowed.

**The Edge AI initiative stays stopped at S0.** These decisions do not reopen it (§18 of the ruling).

## 2026-08-25 — Edge AI neural encoder: **evaluated and rejected at the S0 gate** — *no user-visible change*

> **Nothing shipped.** This entry exists because the alternative is a repository containing a ratified
> encoder specification, a narrow deep-learning exception in `ML_Heuristic_design.md` §9.1, and no
> record of what happened to either. A decision not to build is a change to the project's state.

The initiative proposed replacing the M8-A task-type classifier's n-gram featurizer with a frozen,
bundled, locally-executed neural sentence encoder. **S0 — a hard pre-production gate with a kill
criterion stated in advance — measured it and said no.** The owner accepted that ruling on
2026-08-25 and the initiative **stopped at S0**. S1–S4 were **cancelled, not entered**.

| Item | Result | Verification |
|---|---|---|
| **Ruling** | **EVA-16 kill criterion fired.** Neither candidate improved macro-F1 over the shipped n-gram baseline — both scored **below** it, at both precisions | Baseline mean **0.6575**; EmbeddingGemma-300M 0.6394 (fp32) / 0.6484 (int8); multilingual-e5-small 0.5934 / 0.6404. Pre-registered rule *(arm min > baseline max)* fails for all four |
| **Production code** | **None.** Zero files under `SmartStudyPlanner/` created or modified (EVA-01) | `gitnexus_detect_changes` → 0 changed symbols, 0 affected processes on every commit; suite **487 passed / 0 failed**, unchanged |
| **User-visible change** | **None** | No parse path, threshold, model or dependency was touched |
| **Instrument verified before the null was believed** | Encoders demonstrably work — bit-identical vectors across runs, stripped-diacritic partner retrieved at rank 1 in 5/8 and 6/8 vs chance 1/8 | Report §14 F-3 |
| **New CI guard (retained)** | `Assert no model binary is tracked` — asserts over `git ls-files`, blocks any `.onnx` / `.safetensors` / oversized file entering git (AST-05) | **Proven red in CI** — run [32792616833](https://github.com/PotatoMine725/Smart-Study/actions/runs/32792616833) |
| **New test data (retained)** | `datasheets/vn_input_fixtures.csv` — the DAT-05 Vietnamese input fixture set, 39 rows across six categories | Verifier proven red 4 ways |

**Findings kept, though the initiative stopped:**

- **Data, not the encoder, is the binding constraint.** `tgk` appears in **28 of 205** test rows
  and **0 of 698** training rows; **94.6 %** of test rows contain a token the training set
  never shows. *(As written 2026-08-25 this said "real test rows" — corrected 2026-08-26: the test set
  is authored, not real. The counts are unchanged; the divergence is between two authoring processes.)* Both featurizers were trained on a distribution largely lacking the surface forms they
  were tested on — and the n-gram baseline still won.
- **Deferred to `system_roadmap.md` §A.4:** the shipped M8-A merge gate is `≥0.60`, while the
  **baseline** classifier's own `[0.6,0.7)` band scored **0.000** on the held-out rows — worse than
  the band *below* the gate. *(2026-08-26: "real" withdrawn — the gate has never been measured on real
  student input.)* An indication, not a proven defect; produced by the baseline arm, so it
  outlives the encoder decision.
- **Tokenization / runtime:** in-graph tokenization is unavailable for both candidates; e5-small needs
  a fairseq **+1 id offset** that, if missed, yields plausible-looking token ids wrong in every
  position; `Microsoft.ML.Tokenizers` 2.0.0 needs **no `Microsoft.ML` version bump**; EmbeddingGemma's
  int8 export is **~6× slower** than its fp32 export on CPU.

**`ML_Heuristic_design.md` §9.1 remains in force** — the frozen-encoder policy exception was ratified
on its own merits and is **not withdrawn**, only never exercised. Full evidence and the CP1 ruling:
[`reports/2026-08-25-encoder-pilot.md`](reports/2026-08-25-encoder-pilot.md).

## 2026-08-04 → 2026-08-19 — Epic 3: Study Optimization Engine (SOE) — **code complete 2026-08-07, manual gate CLOSED 2026-08-19**

> **Read the scope line before the table.** Epic 3 shipped **two** things, and only one of them runs
> in the product. The allocator rework (T3.3) is live on every scheduling path. The optimizer itself
> (T3.9 and its seams) is built, tested, gated and **has zero production call sites** — wiring it is
> unscheduled integration work outside every Epic 3 task card (G3-1). Nothing below claims the
> optimizer changed a schedule a user has seen.

| Card / Task | Change | Verification |
|---|---|---|
| Gate **G2** (2026-08-05) | Pass accept/commit semantics ratified: a pass runs **all** stages unconditionally, then commits the best admissible checkpoint prefix `C_k*` (**G2-1**, "run-all, commit-best-prefix" — the candidate L8 of `architecture/lessons-learned.md` had floated); admissibility = D-H first, quality second (**G2-2**); zero tolerance for objective regression bar a numerical-noise guard (**G2-3**); `Optimize` is a deterministic fixed-point loop where the *committed state* is the only thing that varies and `k*=0` is the fixed point (**G2-4**); every checkpoint carries exactly one reason code (**G2-6**) | [decision note](plans/2026-08-04-g2-optimization-pass-semantics.md), ratified at CP-1 `cc8eba5` |
| Gate **G3** (2026-08-07) | `w1…w5` weight-vector governance — ownership, guardrails, and the relation to the existing M8-B `WeightOptimizer` (they are different weights, deliberately) | [decision note](plans/2026-08-07-g3-weight-vector-governance.md), ratified `1e18bb7` |
| Card C — **T3.8** | Schedule identity seam — a stable identity for a scheduled item so a rearrangement can be compared to its input | `308e85c` + review fixes `63aa79d`; seam shape ratified at CP-2 (`4f49153`) |
| Card D — **T3.1** | `IConstraintValidator` — the hard-filter seam. Deadline feasibility, capacity and calendar limits are **hard constraints**, per the 2026-07-02 freeze (D-G) | `cca9d9b`, review fix `f7655d1` |
| Card E — **T3.2** | `IObjectiveEvaluator` — **quality-only** objective, no deadline term (D-J). Deadline information reaches the engine as a constraint, never as a score term | `fde4aeb` |
| Card F — **T3.3** | **The one change users can observe.** Allocator placement reworked least-loaded → **earliest-feasible** in `WorkloadServiceImpl.GenerateSchedule`: the earliest day with room that does not pass the task's `HanChot`, falling back to the earliest day with room at all. The allocator never *refuses* to place — the deadline chooses *which* day, never *whether*. **The deadline tier is provably output-inert today** (tier-1 and tier-2 return the same day for every input — algebraic proof + empirical confirmation), so no discriminating test was written for it and the branch is retained only because it stops being inert once day-capacity becomes non-monotonic. Scope narrowed mid-card (`b608db2`): the G2 pass loop was split out to T3.9. `DiemUuTien` write-through was dropped, then **restored** under an amended CP-2 (`de01561`/`60fae4d`) | `5197784` + review rounds `390e353`, `24e62e8`, `0208b7f`; [inertness proof](plans/2026-08-06-deadline-tier-provably-inert.md); naming-debt descope ratified `50a274b` |
| Card G — **T3.9** | `IScheduleOptimizer.Optimize(schedule, weights) → (schedule, OptimizeReport)` — the G2 pass loop, with **N=1** (`LoadRebalanceStage`) as the ratified stage list; Candidate 2 deferred; OQ2 resolved as one-move-per-invocation. The N=1-only assumption in the reject-branch labelling is flagged in code, not hidden | design `76906c4`/`adda4d0`, ratified `3dcff85`, impl `961f453`, N=1 gap recorded `5de1721`, `d0fc968` |
| Card G — **T3.4** | D-H invariant property suite + inversion + A6 cross-check + G2-5 partition. **D-H is `violations(out) ≤ violations(in)` — a non-worsening guarantee, not an improvement metric**; two findings surfaced (arm-3 / self-inversion) were pinned as characterization asserts with the root cause traced, then ruled on by the owner rather than silently fixed | `4114365`, `ffe400a`, `a8076e0`; owner ruling `eabea1e` (D7) |
| Card G — **T3.7** | `OptimizerRunLogs` telemetry table + `OptimizerRunLogWriter` — one flat row per checkpoint per pass per `Optimize` call (G2-6's report contract). No FK, same rationale as the M8 telemetry tables | `e837830` |
| Convergence (2026-08-07) | 4 docs-only commits closing the epic's DoD 7/7 — closing note with **independently re-measured** success metrics, the review documents it cites, and roadmap correction F1 (the roadmap had asserted a capability T3.3 never shipped) | `82155d9`, `6e35420`, `c0ec38a`, `881f498`, `8cf53da`, `c305a8d`, `8b58ec0`; **470 passed / 1 skipped / 471 total** |
| Automated QA gate (2026-08-10) | Discriminating tests for T3.3 and T3.7 behind the manual gate — written to fail against the pre-change behaviour, not merely to pass | `10b5039`; 470 → **475 passed / 1 skipped / 476 total** |
| Workload-balancer stale chart (2026-08-14) | **A defect found by the manual gate, fixed inside it.** `RenderedCapacityHours` now records the capacity the *displayed* schedule was built with, separately from the slider's `CapacityHours`; `IsScheduleStale` drives a badge when they diverge; `capacity.txt` clamped to the slider **ceiling**, not only its floor. Moving the slider without pressing the button can no longer read as a re-allocated schedule. Non-vacuity proved by mutation, then reverted and the tree confirmed clean | `dde5cc8`, `b084e40`, `545870d`; [design](plans/2026-08-10-workload-balancer-stale-chart-fix-design.md) · [plan](plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md) · [report](reports/2026-08-14-workload-balancer-stale-chart-fix-report.md); suite → **486 passed / 1 skipped / 487 total** |
| Manual QA gate — **CLOSED 2026-08-19** | **PASS WITH FINDINGS.** Every scenario passed; no scenario produced a defect. The three findings are non-defects: a UX enhancement candidate, a ratified limitation (D3, past-deadline placement — owner-accepted, Decision D7) observed in the running product, and a gap in *automated* coverage behind a manual check that passed. **E1–E4 close on an owner ruling, not a written observation**, and the closure says so rather than blurring the two. B2's pass is `OptimizerRunLogs` being **empty** — 0 rows *is* the expected result while the seam is unwired | [runbook](plans/2026-08-10-epic-3-manual-qa-runbook.md) · [closure](reports/2026-08-19-epic3-manual-gate-closure.md) · owner evidence records [2026-08-10](reports/2026-08-10-epic3-soe-manual-observation.md), [2026-08-19](reports/2026-08-19-epic3-manual-observation-updated.md); `33c0ffe` |
| E6 coverage test (2026-08-20) | The closure's E6 follow-up, executed. Subject-delete-with-≥2-tasks beside a surviving sibling is now covered. **The pre-registered acceptance bar was not met** — of five mutants, none was killed by the new test while the pre-existing suite survived — so it is filed as **scenario-fidelity coverage, not regression protection**, in those words, invoking the plan's §7 fallback rather than reporting "test added, green". One mutant (`DetectChanges()` moved after the removal loop) **survived all 487 tests**; the call must not be deleted on the strength of that | `35e2f14`; [plan](plans/2026-08-19-e6-cascade-coverage-test.md) · [report](reports/2026-08-20-e6-cascade-coverage-test.md); **487 passed / 1 skipped / 488 total** |

**Outcome:** suite 391 → **487**, green in Debug, Release and on CI. *(Chain, each figure taken from
the report that measured it: 391 at stabilization close → **470** at Epic 3 code complete, on `dev`
at `dd41685` → **475** after the automated gate's additions → **486 passed / 487 total** after the
stale-chart fix → **487 passed / 488 total** after the E6 test. The intermediate 475 was first
measured on a working tree carrying two uncommitted carry-forward test files; they were committed at
`d1ab3a3` precisely so the gated number is the number CI computes.)* Epic acceptance criteria and DoD
**7/7** met on the merged tree; gates G2 and G3 ratified; the manual gate CLOSED. **Known and
deliberate — the optimizer is not in the product:** `ScheduleOptimizer`, `SoeWeights` and
`IConstraintValidator` have no production call site and no `ServiceLocator` registration, so
`BalanceWorkloadStage` and `WorkloadBalancerViewModel` still call `IWorkloadService.GenerateSchedule`
directly. Success metric #4 (objective delta vs. baseline) is **unevaluated for 90% of the corpus**
and disclosed as such (closing-note F3). Full detail: [Epic 3 closing
note](reports/2026-08-07-epic3-closing-note.md) (DoD-7, independently re-measured) ·
[closure verdict](review/2026-08-07-epic3-closure-verdict.md) ·
[owner triage](review/2026-08-07-epic3-owner-triage.md). Durable lessons distilled into
[`knowledge/qa-gates.md`](knowledge/qa-gates.md) and
[`knowledge/review-methodology.md`](knowledge/review-methodology.md) —
see the [distillation report](reports/2026-08-19-epic3-knowledge-distillation.md).

## 2026-07-27 → 2026-08-02 — Post-Epic 1 Engineering Stabilization (WP-1 → WP-6)

| Package | Change | Verification |
|---|---|---|
| WP-1 CI Gate | `.github/workflows/ci.yml` (restore → build → test on `windows-latest`); `build-test` made a required status check. Split `enforce_admins`: `false` on `dev` (daily direct-push workflow), `true` on `main` (already PR-only) | `f98e4c7`, green on `windows-latest` first try, **346 pass** |
| WP-2 Test Trust Restoration | De-dated `DecisionEngineTests.cs` (6 `DateTime.Now` deadlines → fixed clock; the 45-day case re-verified as legitimately passing, no priority-band defect found); `LocalModelStorageTests` points at a temp dir instead of the real user profile; `PipelineStageTests` constructs `RiskOrchestrator` directly instead of via `ServiceLocator` | `c701527`, `81f4759`, `078fdbe`; 346 → **348 pass** |
| WP-3 Persistence & Sync Identity | Closed 2 soft-delete read-path leaks (`SqliteStudyLogRepository`, `SqliteUserStatsRepository`) plus a third found beyond the plan (`WeightOptimizerService.SuggestAsync` was also scoring tombstoned tasks); `DeviceIdentity` persists device id instead of deriving it from `Environment.MachineName` each run; `StudyTask.MucDoCanhBao` defaulted so the NOT NULL column is never null on non-UI write paths | `bbb3c29`, `6704773`, `78f16bb`; 348 → **355 pass** |
| WP-4 Scheduling Characterization | 13 characterization tests pin `WorkloadServiceImpl.GenerateSchedule` (day-date contiguity, `DiemUuTien` write-back, chunk-naming boundary, placement policy, `TenMon` provenance); non-vacuity proved by 7 mutations, each turning the suite red; production code untouched. Found (handed to WP-5, not fixed here): `capacityHours < 1/60` hangs `GenerateSchedule` in an infinite loop | `e89f0ec`; 355 → **368 pass** |
| WP-5 Runtime Robustness | Background timer tick guarded + duplicate deadline toast removed; `capacity.txt` round-trip made culture-invariant, closing 3 defects at once (the scoped locale bug + both WP-4 hand-overs, including the infinite-loop hazard); post-review follow-up added the termination guard `GenerateSchedule` had declined (non-vacuity proved by removing the guard and watching tests **hang**, not fail) | `9a175b9`, `54f64ca` + post-review `0e5d448`/`d425068`/`866b5be`; 368 → 379 → **391 pass** |
| WP-6 Repo & Doc Hygiene | Untracked root `Assets/` retired (design sources preserved at `docs/assets/icon-source/`, committed *before* the irreversible delete); README/roadmap/`CLAUDE.md` counts corrected (337 → 391); unused `Verify.CommunityToolkit.Mvvm` package reference dropped; 3 corrections appended to the Epic 2 CSA | `12291d0`, `6416e50`; **391 pass**, unchanged (adds no tests/logic) |

**Outcome:** suite 346 → **391**, green in Debug, Release, and on CI. All 12 Epic 2 entry criteria met — the last (#1, branch protection) closed 2026-08-02 once the owner authorised the `dev`/`main` split above. WP-6.3 (dependency advisory pin) deliberately not run — opt-in, and its own verification needs a manual ML retrain no agent can perform. Full detail: one report per package under [`docs/reports/`](reports/) (`2026-07-31-wp3-persistence-sync-identity.md`, `2026-07-31-wp4-scheduling-characterization.md`, `2026-08-02-wp5-runtime-robustness.md`, `2026-08-02-wp6-repo-doc-hygiene.md`); durable lessons folded into [`knowledge/review-methodology.md`](knowledge/review-methodology.md) and [`knowledge/release-engineering.md`](knowledge/release-engineering.md).

## 2026-07-26 — P1: Smart-add Vietnamese negation fix (parser)

| Area | Change | Verification |
|---|---|---|
| Services/Strategies/DifficultyKeywordParser | Negation-aware, **word-boundary token** difficulty matching in `DefaultDifficultyKeywordParser`. A difficulty keyword negated within a 2-token preceding window (`"không dễ"`, `"chẳng khó"`, `"khong de"`) is suppressed and falls back to the task-type prior (owner decision 2026-07-24: suppress→prior, not invert — never overshoots on compound negation `"không khó không dễ"`). Replaces bare `.Contains()` substring matching, which as a pinned consequence removes the RR-1 substring false-positives (`"de"` ∈ `"deadline"`, `"kho"` ∈ `"khong"`). Input NFC-normalized before tokenizing over `\p{L}+` (D-9). `ContainsAnyRule`, the task-type parser, and the `Parse`/`PriorForTaskType` signatures are untouched; **no UI change**. RED-first (7-row negation characterization corpus + 2 guard rows). Plan: `2026-07-24-smart-add-negation-fix-plan.md` (archived to `legacy/Archived plans/`, local-only) | `47cded6` (RED) + `c163135` (fix), **346 pass** |

## 2026-07-05 → 2026-07-20 — Epic 1 (Sync-Ready Data Model) — **Released 2026-07-20** (reopened at B4 on 2026-07-15, refixed 2026-07-19, re-closed & released 2026-07-20)

| Milestone | Change | Verification |
|---|---|---|
| M1.1 | Single stamping seam — `SyncStamper` (in `AppDbContext.SaveChanges*`) stamps `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId` on every write; A6 closed — `StudyLog` write awaited (was fire-and-forget), `DeviceId` populated | merged `3193adf`, review R1/R5 closed |
| M1.2 | `SyncSchema.EnsureColumns` versioned upgrade seam (backup-before-upgrade + migration report) on every synced entity; soft-delete tombstones (`IsDeleted`/`DeletedAtUtc`) replace hard cascade deletes (gate G1); `TaskCascadeHelper` cascade-tombstones FK-only children (`TaskNote`/`TaskReferenceLink`) — M1.2-R1 remediation | merged `e2f8268`, 330 pass |
| M1.3 | `Models/MonHocIdentity.Normalize` — single dedup definition (NFC → trim → collapse whitespace → invariant-culture lowercase) routed through by all 4 read-side dedup sites + `QuanLyMonHocViewModel.ThemMon` prevent-at-source; folded fix for a pre-existing M1.2 `LuuHocKyAsync` task-reconcile gap surfaced by the widened dedup key (Option A — scoped the task diff to the whole `HocKy` instead of per-`MonHoc`-parent) | merged `a3a0a3d` — **Epic 1 closed**, 330 pass |
| post-close | `101aaa3` — duplicate-subject warning routed through the `OnThongBao` seam (fixes a `MessageBox.Show` popping a real modal during headless test runs; escape from the M1.3 review, fixed same day) | — |
| A1 (release gate, C3a) | `DbBackup.CreateBackup` runs `PRAGMA wal_checkpoint(TRUNCATE)` before `File.Copy` — closes closure-verdict finding F5 (naive file-copy backup silently dropped committed-but-un-checkpointed WAL pages); RED-first discriminating test with a live-WAL fixture (writer open, autocheckpoint off); 2 existing tests converted from fake-text to real SQLite fixtures | fix `2d04be5`, merged `8740350`, 330+ pass |
| B4 reopen — R1 (P0 regression fix) | `QuanLyTaskViewModel.ThemTask` now stamps `MaMonHoc` when creating a `StudyTask` — the B4 crash driver: the 4-arg ctor left the FK `Guid.Empty`, so M1.2's FK-keyed reconcile (`First(m => m.MaMonHoc == …)`) threw `InvalidOperationException` and, with no global handler, killed the process. Defense in depth (D-R2): reconcile heals a `Guid.Empty` FK from navigation position (restoring the pre-M1.2 EF graph-fixup semantics) and now fails loudly with task/FK/`HocKy` context on a genuinely-unknown FK. RED-first at both layers; the VM-level test drives the real `ThemTaskCommand` against real SQLite with **no manual FK stamping**, closing the fixture-bias gap that let 331 green tests miss this | `3bb56c6` + `63b9611`, 333 pass |
| B4 reopen — R2 (crash visibility) | `Services/CrashLogger` — minimal last-resort fault sink (`%AppData%\SmartStudyPlanner\crash.log`, path overridable for tests; deliberately not a logging framework). `DispatcherUnhandledException` (logs, shows a dialog, sets `Handled = true` so the app survives) / `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` wired at the top of `OnStartup` — also catches `async void OnStartup` faults, shrinking tech-debt item 2.8-3 without restructuring. The 3 waived fire-and-forget telemetry sites now log their faults rather than swallowing them — `LogDifficultyLabelAsync` + `LogWeightChangeAsync` via `CrashLogger.Observe`, `MatureAsync` via an inline `catch → CrashLogger.Log` (F2 waiver nuance, D-R5) | `b0061e7` + `c18e1e7`, 336 pass |
| B4 gate — Analytics (separate, pre-existing) | Stale-render fix: `AnalyticsViewModel` now resets its chart outputs on the `!HasData` (no-data filter) branch, so a filter returning no rows can no longer leave the previous filter's chart/heatmap on screen. Surfaced during the B4 Step-2 owner re-run and diagnosed as a **pre-existing** bug, **not** an Epic 1 regression. Part 1 (VM reset) RED→GREEN unit-verified; Part 2 (`AnalyticsPage.xaml` panel `Visibility`→`HasData`) shipped as code, **visual toggle pending owner re-run**. Two-section restructure + filter semantics → post-release backlog | `c4291c7`, **337 pass** |
| B4 = Released (owner sign-off) | After the R1/R2 reopen fix and the Analytics stale-render fix, the owner re-ran the supervised launch and signed off **Epic 1 = Released (2026-07-20)** — release decision record appended to the [closure gate](plans/2026-07-11-epic-1-closure-gate.md). Supersedes the earlier "do not release yet" hold (which was pending the Step-2 Analytics diagnosis) | `3e00ce0` |

Epic 1's acceptance criteria and success metrics are all met on the merged tree; the full suite is at **337 pass** after the B4 gate work. **Released 2026-07-20:** the [closure verdict](review/2026-07-11-epic1-closure-verdict.md) ratified the close with conditions C1–C3, and the [release gate](plans/2026-07-11-epic-1-closure-gate.md) sequenced the remaining release-engineering work (agent-side docs/knowledge tasks A1–A4, then the owner-supervised first real database upgrade, B1–B4). B1–B3 passed; **B4 reopened** on a latent M1.2 FK regression (fixed by R1/R2 above); the owner re-ran the supervised launch and — with the separate pre-existing Analytics stale-render bug also fixed — signed off **Epic 1 Released** on 2026-07-20. `NU1903` (`SQLitePCLRaw` high-severity advisory) is now tracked in `specs/system_roadmap.md` §A.4 (verdict carry-forward ledger #8) — pre-existing, not Epic-1-caused.

## 2026-06-27 — Analytics UI redesign + duplicate-data fixes

| Area | Change | Verification |
|---|---|---|
| Themes/AnalyticsStyles.xaml | **Created** — 16 component styles: `AnCard`, `AnPanel`, `AnEyebrow`, `AnPageTitle`, `AnSubText`, `AnSectionTick`, `AnSectionTitle`, `AnSectionSub`, `AnFieldLabel`, `AnKpiLabel`, `AnKpiValue`, `AnSolidButton`, `AnGhostButton`, `AnDataGridHeader`, `AnDataGridRow`, `AnDataGridCell`, `AnDataGrid`. Toàn bộ màu via `{DynamicResource}` → follow Light/Dark theme. | build pass |
| App.xaml | Thêm `AnalyticsStyles.xaml` vào `MergedDictionaries` (sau `DashboardStyles.xaml`) | — |
| Views/AnalyticsPage.xaml | **Thay toàn bộ** — layout narrative-first: header+filters (Grid 2-col), Narrative Hero card (câu chuyện tuần + điểm năng suất 46px), loading/empty states, Band A (7*/5* weekly+subject charts), Heatmap (7×52 UniformGrid), DataGrid `AnDataGrid`. 14 binding ViewModel giữ nguyên. | — |
| ViewModels/AnalyticsViewModel.cs | Sửa `SubjectOptions`: thêm `.Distinct().OrderBy()` — dropdown môn học không còn hiện duplicate khi `DanhSachMonHoc` chứa nhiều instance cùng tên | — |
| Services/Analytics/StudyAnalyticsService.cs | Sửa `ComputeSubjectInsights`: `GroupBy(TenMonHoc)` + `SelectMany(DanhSachTask)` — DataGrid chi tiết môn không còn lặp hàng | — |
| Services/Pipeline/Stages/AdaptStage.cs | Sửa `Execute`: lặp theo `GroupBy(TenMonHoc)` thay vì từng `MonHoc` instance — "Gợi ý thích nghi" trên Dashboard không còn duplicate entry cùng môn | 244 pass |

Root cause (3 bugs): `DanhSachMonHoc` lưu nhiều `MonHoc` object có cùng `TenMonHoc`. Mọi code duyệt list trực tiếp sinh ra 1 kết quả per-instance thay vì per-subject. Fix pattern: `GroupBy(m => m.TenMonHoc)` + `SelectMany(DanhSachTask)` áp dụng nhất quán cho cả service layer, pipeline, và ViewModel.

## 2026-06-18 — M8-A TextClassifier seed v4 (collected_v4 merge + recall eval)

| Area | Change | Verification |
|---|---|---|
| Services/ML/TextClassifier | Merge 205 vetted/deduped `collected_v4` rows into embedded `seed_intents.csv` (**698 → 903**, purely additive). Per-class: ThiGiuaKy +99 (85→184), BaiTapVeNha +56 (124→180), DoAnCuoiKy +50 (131→181); majorities untouched. Imbalance 2.21× → **1.11×**. SHA-256 change trips the `SeedHash` gate → seed-only model auto-retrains on next init (no code change) | `ab5112c` |
| datasheets/ | Byte-safe one-off merge script `_merge_seed.py` (UTF-8 strict, dedup on normalized `InputText`) + the 205-row `collected_v4.csv` source. Kept for provenance; outside the build | `8855874` |
| tools/TextClassifierEval | Throwaway net10.0 harness (not in `.slnx`) — stratified 80/20 per-class recall eval mirroring the prod pipeline. Before/after (v698 vs v903): MacroAccuracy flat at 97.25%, minority recall did not regress, minority test support grew (ThiGiuaKy 17→37, BaiTapVeNha 25→36). Report: `docs/reports/2026-06-25-m8a-textclassifier-v4-recall-eval.md` | build pass; 244 pass |

Merge: continues the project's own label policy (drop `NhacNho`/`OnTap`/`Khac`; `BaiTap→BaiTapVeNha`, `DuAn→DoAnCuoiKy`) with no enum/UI change. TextClassifier remains the sole ML component (`ML_Heuristic_design.md` §5.1); Difficulty/weights stay heuristic.

## 2026-06-11 — M8 Ground-Truth Instrumentation (Slices 0–2B)

| Slice | Area | Change | Verification |
|---|---|---|---|
| 0 | Models/Telemetry | `DifficultyLabelLog` + `WeightChangeLog` entities; `IDifficultyLabelLogRepository` + `IWeightChangeLogRepository` interfaces + SQLite implementations; `App.xaml.cs` `CREATE TABLE IF NOT EXISTS` for both tables (safe on existing DBs) | build pass |
| 1A | Services/Strategies | `DefaultDifficultyKeywordParser` — fallback prior by `TaskType` (`DoAnCuoiKy/ThiCuoiKy→4`, `ThiGiuaKy→3`, `KiemTraThuongXuyen→3`, `BaiTapVeNha→2`) replaces hard-coded 3 | parser tests |
| 1B | ViewModels/QuanLyTask | Fire-and-forget `DifficultyLabelLog` on every task save: `InputText`, `SuggestedDoKho`, `FinalDoKho`, `WasOverride`, `TaskType`, `MaTask`; try/catch — never blocks save | label logging tests |
| 2A | ViewModels/WeightOptimizer | `ApplySuggestion()` captures before-config snapshot then fires `_ = LogWeightChangeAsync(...)`: before/after weights, `UserStatsSnapshot` baseline, cohort = open-task IDs at apply time (`TrangThai != HoanThanh`) | weight log tests |
| 2B | Services/Telemetry | `OutcomeMaturationService` + `IOutcomeMaturationService`: scans `WeightChangeLog` rows where `OutcomeMaturedUtc == null && AppliedUtc + 14d ≤ now`; fills miss-rate/avg-delay/completed-in-window from **cohort only**; idempotent; registered in `ServiceLocator`; triggered at app launch fire-and-forget | maturation tests |
| Tests | TestDoubles/ | `FakeWeightChangeLogRepository` (seed + real `GetPendingMaturationAsync` filter), `FakeUserStatsRepository`, `FakeStudyTaskRepository` | — |

Verification: **237 pass / 1 pre-existing fail** (`DecisionEngineTests.CalculatePriority_TaskToiHanHomNay`).

## 2026-06-11 — Retire `RiskAnalyzerService` fully (Core/Risk is sole risk subsystem)

| Area | Change | Verification |
|---|---|---|
| Core/Risk/RiskOrchestrator | Now implements `IRiskAnalyzer` directly; facade/bridge removed | risk tests |
| Services/RiskAnalyzer | Folder **deleted entirely** — legacy `RiskAnalyzerService` adapter (`0346637` → `1b4c2ba`) and the last DTO file `IRiskComponent.cs` (`191dd17`) both gone | — |
| Tests | Risk tests relocated to mirror `Core.Risk` namespace | `74ed39b` |

Completes the gradual migration started 2026-05-12 (below). `IRiskAnalyzer` consumers (`DashboardViewModel`, `AssessRiskStage`) depend only on the interface; DI binds `IRiskAnalyzer → RiskOrchestrator`. No `RiskAnalyzerService` / `Services.RiskAnalyzer` references remain in any `.cs` file.

## 2026-06-09 — Infrastructure clean-up (test structure + parser facade)

| Area | Change | Commit |
|---|---|---|
| Tests | Split shared test infrastructure into `TestDoubles/` (in-memory fakes) and `Fixtures/` (DB helpers); all test files now mirror production namespace 1:1 | `41a88d0` |
| Services/SmartParser | Retired static `SmartParser` facade; `QuanLyTaskViewModel` now requires `IParsingOrchestrator` directly (no more static fallback) | `222cb5a` |

## 2026-06-06 — M8-B Slices 7-8 (WeightOptimizer — rule-based + review/apply UI)

| Slice | Area | Change | Verification |
|---|---|---|---|
| 7 | Services/ML/WeightOptimizer | `WeightOptimizerService` (rule-based, reads `UserStatsSnapshot`): high miss-rate → boost TimeWeight, long avg delay → boost DifficultyWeight; `WeightRuleEngine` applies adjustment heuristics; registered in `ServiceLocator` | optimizer tests |
| 7 | Core/ML/Contracts | `IWeightOptimizerService.SuggestAsync()` → `WeightConfigSuggestion` (SuggestedConfig, Confidence, Rationale); `IMlConfidencePolicy` gates at 0.75/0.60/<0.60 | policy tests |
| 8 | WeightOptimizerViewModel | `LoadSuggestionCommand` calls optimizer; `ApplySuggestionCommand` writes config + calls `_onSave`; `IsHighConfidence`, `HasReview`, `ApplyStatus`, `AutoApplyBadgeVisible` properties | VM tests |
| 8 | Views/WeightOptimizerWindow.xaml | Side-by-side current vs suggested weight rows; confidence card (percentage + progress bar + `Rationale`); "AI khuyên dùng" badge (gated); styled Apply button; `ApplyStatus` footer | — |
| 8 | WeightConfigStore | `Load()`/`Save(WeightConfig)` — JSON persistence to `%AppData%\SmartStudyPlanner\weight_config.json`; atomic temp-file swap | — |
| 8 | MainWindow | `NavWeightOptimizer` button wired; opens `WeightOptimizerWindow` as `ShowDialog` | — |

Merge: rule-based backbone — `WeightRuleEngine` heuristics produce suggestions deterministically; no ML model required. `WeightConfig.IsValid()`/`Normalize()` remain last-line. Suggestion is a separate object until user clicks Apply (never silently overwrites). Offline-first.

## 2026-06-05 — Refactor Slice 6 (M8-A classifier wired into parser)

| Area | Change | Verification |
|---|---|---|
| Services/ML | `DefaultMlConfidencePolicy : IMlConfidencePolicy` — hard-coded thresholds (`>=0.75` AutoApply, `0.60–0.75` Review, `<0.60` Reject) | policy boundary tests |
| Services/ML | `IntentClassifierAdapter : IIntentClassifier` — wraps `IIntentClassifierService` + policy; drops prediction below `0.60` (heuristic wins); try/catch → null (offline-first) | adapter tests |
| Services/ServiceLocator | Registered `ITextClassifierModelManager`, `IIntentClassifierService`, `IMlConfidencePolicy`, `IIntentClassifier`; `IParsingOrchestrator` now injects the adapter | DI smoke |
| App.xaml.cs | Background warm-up of `ITextClassifierModelManager.InitializeAsync()` (silent-catch, mirrors M7) | — |
| ViewModels/QuanLyTaskViewModel | `PhanTichNhapNhanh` prefers DI `IParsingOrchestrator` (ML-augmented), falls back to static `SmartParser`; surfaces "AI gợi ý Loại … (xx%)" in `QuickInputHint` when `Source == MlAugmented`. M6.1 invariant preserved (never touches Note/Links) | VM tests |
| Tests/MLTests | `IntentClassifierAdapterTests` (10) + `Slice6ParserIntegrationTests` (2) | 166 → **178** pass |

Merge: classifier output overrides the heuristic `Loai` only at confidence `>= 0.60`; deadline still resolved by the existing engine; offline-first (byte-equal heuristic when `text_classifier.zip` absent / model unloaded). Acceptance test asserts "giữa kỳ" → `ThiGiuaKy` and "đồ án cuối kỳ" → `DoAnCuoiKy` through the real seed model (no regression).

## 2026-06-05 — M8-A seed v3 (5-class: real relabel + synthetic)

| Area | Change | Verification |
|---|---|---|
| datasheets/normalized_dataset_m8a_uniform.csv | Relabeled real contradictions in-place: 31 "giữa kỳ" rows (were `ThiCuoiKy`/`KiemTra`) → `ThiGiuaKy`; 96 "đồ án/BTL/project" rows (were `BaiTap`/`NhacNho`/`ThiCuoiKy`) → `DoAnCuoiKy`. Added **100** synthetic rows (1000 → 1100). | held-out 96.2% |
| datasheets/synthetic_v3_giuaky_doan.csv | New provenance file — 101 hand-authored rows simulating VN student behavior (diligent/lazy/abbreviated personas, typos, no-diacritics, slang/emoji, EN↔VI code-switching, varied info density) + contrastive boundary pairs ("đồ án cuối kỳ"↔"thi cuối kỳ"). | — |
| Services/ML/TextClassifier/seed_intents.csv | Regenerated → **698 rows, all 5 enum classes** (KiemTra 188 / ThiCuoiKy 170 / DoAnCuoiKy 131 / BaiTapVeNha 124 / ThiGiuaKy 85). `LabelVersion=v3`; synthetic rows tagged `Source=synthetic_v3`. | round-trip 0 bad labels |

Root cause fixed: the prior seed (v2, 596 rows) covered only 3/5 classes because the datasheet mis-labeled "giữa kỳ"→ThiCuoiKy and "đồ án"→BaiTap, which made the seed model confidently mis-map those two classes (~1.0 confidence). Stratified 85/15 held-out eval after the fix: **96.2% accuracy**, only **1/106 dangerous miss** (wrong & conf≥0.60, genuinely ambiguous), confidence no longer saturated (ambiguous rows correctly drop below 0.60 so the merge gate catches them).

## 2026-06-05 — M8-A seed upgrade (real data, remap to enum) — superseded by seed v3 above

| Area | Change | Verification |
|---|---|---|
| Services/ML/TextClassifier | Regenerated embedded `seed_intents.csv` from `datasheets/normalized_dataset_m8a_uniform.csv` (50 hand rows → **596**). Remapped datasheet taxonomy → `LoaiCongViec`: `BaiTap → BaiTapVeNha`, dropped `NhacNho`/`OnTap` (no enum home). `TimeExpression → DeadlineHint`, `Difficulty` 1–5. | 166 pass |

Coverage trade-off (per the "remap to enum" decision): seed model covered **3 of 5** enum classes — `BaiTapVeNha` (216) / `KiemTraThuongXuyen` (188) / `ThiCuoiKy` (192). `ThiGiuaKy` / `DoAnCuoiKy` had no rows. **This gap was fixed in seed v3 (above)** by relabeling the contradictions and synthesizing the two classes. Commit `9068e65`.

## 2026-06-05 — Refactor Slice 5 (M8-A TextClassifier scaffold)

| Area | Change | Verification |
|---|---|---|
| Services/ML/Schema | `TextClassifierInput` (CSV row + ML features), `TextClassifierOutput` (PredictedLabel + Score[]), `TextClassifierPrediction` (domain DTO) | build pass |
| Services/ML | `ITextClassifierModelManager` + `TextClassifierModelManager` — own paths `text_classifier.zip`/`_meta.json`, load-if-present else train from embedded seed CSV, atomic temp-file swap, `SeedOnly` meta | — |
| Services/ML | `TextClassifierDatasetImporter` — fails fast on missing required columns (`InputText/TaskType/Difficulty/DeadlineHint`) | importer tests green |
| Services/ML | `TextClassifierService : IIntentClassifierService` — maps to Core `IntentPrediction`; `DoKho` null this slice | — |
| Services/ML/TextClassifier | `seed_intents.csv` embedded resource (50 rows × 5 `LoaiCongViec` classes) | — |
| Tests/MLTests | `TextClassifierSchemaTests` (8 new) | 158 → **166** pass |

Pipeline: `MapValueToKey(TaskType→Label)` → `FeaturizeText(InputText)` → `SdcaMaximumEntropy` (multiclass) → `MapKeyToValue`; confidence = `Score.Max()`.

Scope: the classifier is standalone — **no consumer wiring** (ServiceLocator / ParsingOrchestrator / SmartParser untouched). DI registration + parser merge + UX preview are Slice 6. Commit `cb49a15`.

## 2026-05-18 — Refactor Slice 3 + Slice 4

| Area | Change | Verification |
|---|---|---|
| Core/Parsing | Added `RuleBasedTimeParsingEngine`, `TaskExtractionEngine`, `ParsingOrchestrator` (`IParsingOrchestrator`) | build pass |
| Services/SmartParser | Static `Parse(string)` kept as facade, delegates to default `ParsingOrchestrator(SystemClock())` — no breaking change at `QuanLyTaskViewModel.cs:246` | parser tests green |
| Services/ServiceLocator | Registered `IParsingOrchestrator` | DI smoke pass |
| Tests/Parsing | `ParsingOrchestratorTests` (5 new) | 147 → **152** pass |
| Infrastructure/Persistence | `IStudyTaskRepository`, `IStudyLogRepository`, `IMonHocRepository`, `IUserStatsRepository` + `UserStatsSnapshot` aggregate | — |
| Infrastructure/Persistence/SQLite | 4 implementations using `Func<AppDbContext>` factory pattern | — |
| Tests/Infrastructure | `RepositoriesTests` (4 in-memory SQLite integration) | 152 → **156** pass |

Deviation: Slice 3 used a static default instance (not DI singleton) so static-path tests don't need to boot `ServiceLocator`. Functionally equivalent.

Deviation: Slice 4 did not migrate existing consumers — `StudyAnalyticsService` is a pure function over `IEnumerable<StudyLog>`, not a DB consumer. Migrations of `Focus`/`Dashboard` VMs deferred to a separate slice.

## 2026-05-17 — Refactor Slice 1 + Slice 2 (god-object refactor kickoff)

| Slice | Area | Change | Commit |
|---|---|---|---|
| 1 | Core/Scheduling/Contracts | Added `ISchedulingOrchestrator`, `IPriorityEvaluator`, `IRawMinutesCalculator`, `IStudyTimeSuggestionEngine` | `5ece84c` |
| 1 | Core/Parsing/Contracts | Added `IParsingOrchestrator`, `IIntentClassifier`, `ITimeParsingEngine`, `ITaskExtractionEngine`, `ParseResult`+`ParseSource` | `5ece84c` |
| 1 | Core/ML/Contracts | Added `IMlConfidencePolicy`, `IIntentClassifierService`, `IWeightOptimizerService` + `WeightConfigSuggestion` | `5ece84c` |
| 2 | Core/Scheduling/Engines | Added `RawMinutesCalculator` (pure formula) + `StudyTimeSuggestionEngine` (formatting) | `3b176fb` |
| 2 | Core/Scheduling/Evaluators | Added `PriorityEvaluator` wrapping `PriorityCalculator` | `3b176fb` |
| 2 | Core/Scheduling/Orchestrators | Added `SchedulingOrchestrator` composing leaves + owning `WeightConfig` self-heal + ML predict | `3b176fb` |
| 2 | Services/DecisionEngineService | Reduced **92 → 42 lines**, now thin facade; `IDecisionEngine` contract unchanged | `3b176fb` |
| 2 | Tests/Scheduling | Added `RawMinutesCalculatorTests` (4) + `StudyTimeSuggestionEngineTests` (5) | 138 → **147** pass |

GitNexus snapshot post-Slice 2: 1,842 nodes / 4,392 edges / 65 clusters / 101 flows.

## 2026-05-12 — Core/Risk extraction + URL fix

| Area | Change |
|---|---|
| Core/Risk/Models | Moved `RiskAssessment` and `RiskLevel` into `Core/Risk/Models` |
| Core/Risk/Contracts | Added `IRiskAnalyzer`, `IRiskFactorEvaluator` (gradual migration) |
| Core/Risk | Added `RiskAggregator` + `RiskOrchestrator` |
| Services/RiskAnalyzer | Reduced `RiskAnalyzerService` to **51-line adapter** mapping Core → legacy contract |
| ViewModels/QuanLyTaskViewModel | `AddLink()` stores `uri.OriginalString` instead of `uri.ToString()` (preserves user-typed URL) |
| Tests | `RiskAnalyzerTests` + `PipelineStageTests` updated for the bridge; `TaskNotesViewModelTests` URL assertion now passes |

Verification: **146 → 138** passed (after risk bridge); URL fix re-greened to 146. Final verified count 138 in `2026-05-12-phase-next-final-report.md` and 146 in `2026-05-12-build-test-fix-report.md`.

## 2026-05-01 — UI/UX phases A → F shipped

| Phase | Area | Change |
|---|---|---|
| A | Design system | Shared `CommonStyles.xaml` for card/header/button/datagrid/empty-state; merged into `App.xaml` |
| B | Navigation | Sidebar current-semester label + Workload popup indicator + nav telemetry |
| C | Dashboard | Added `IsLoading`, `HasData`, `HasError`, `EmptyStateMessage`; risk tooltip; telemetry on save/goto/focus_start |
| D | Analytics | Range filter (7/30/90) + subject filter + week-over-week narrative + recommended next action |
| E | Notes/Links | URL validation, domain fallback title, host preview, parser-isolation hint |
| F | Quality gate | `IStudyTelemetry` + `DebugStudyTelemetry`, DI registration, `UxViewModelTests`, `ux_quality_gate_checklist.md` |

## 2026-04-30 — Analytics heatmap

Created `Models/HeatCell.cs` (record with Date / TotalMinutes / Level + Tooltip), `Converters/HeatLevelToBrushConverter.cs` (5-level GitHub-green palette, theme-aware). `AnalyticsViewModel.BuildHeatmap()` aggregates `_allLogs` into 52×7 cells aligned to Monday 51 weeks ago. XAML uses `ItemsControl` + `UniformGrid(Rows=7, Columns=52)` — no extra library.

## 2026-04-29 — Sidebar UI upgrade + ML retrain post-reset verification

| Area | Change |
|---|---|
| Themes/SidebarStyles.xaml | New `SidebarNavButton` `ToggleButton` style with hover/active triggers + 3px accent bar |
| Themes/LightTheme.xaml + DarkTheme.xaml | Added `SidebarHoverBackground/Text` tokens; brightened `SidebarText` + `SidebarIconColor` |
| Views/MainWindow.xaml(.cs) | All nav `Button` → `ToggleButton`; `SetActiveNav` toggles `IsChecked` |
| Tests/DevTools/DbSeedTests.cs | `[Trait("Category","Seed")]` test that deletes stale ML artifacts and seeds 180 synthetic StudyLogs (3×60 difficulty groups, `Random(42)`) into an isolated in-memory SQLite DB; run via `dotnet test --filter "Category=Seed"` |

## 2026-04-26 — Consolidated change report

| Area | Change |
|---|---|
| App.xaml.cs | Dev startup preserves SQLite by default; `EnsureDeleted()` only on `DEV_RESET_DB=1`; `EnsureCreated` for schema |
| App.xaml.cs (later) | Switched bootstrap to `db.Database.Migrate()` after the `s.NgayHoanThanh` missing-column bug (see Bugs §) |
| Views/MainWindow.xaml.cs | Theme toggle calls `ThemeManager.ToggleTheme()` directly — works from every page |
| Models/HocKy.cs | End date defaults to `NgayBatDau + 150 days`, internal auto/manual flag |
| ViewModels/SetupViewModel.cs | Editable `NgayKetThuc`, auto/manual sync, restore-default command |
| Views/SetupPage.xaml | Exposed end-date field + restore-default action |
| Dev reset | M7 ML artifacts (`study_time.zip`, `meta.json`) deleted; baseline retrains from `SeedDataGenerator.Generate(180)` |

## 2026-04-26 — M6.1 Task Notes & Study Links

| Layer | Change |
|---|---|
| Models | `TaskNote` (1-1, freeform + `UpdatedAtUtc`), `TaskReferenceLink` (1-N, Title/Url/Category/SortOrder), `TaskEditorBundle` (aggregate DTO) |
| Data/AppDbContext | New DbSets + Fluent cascade delete from `StudyTask`; added `DbContextOptions` ctor for testability |
| Data/StudyRepository | 7 new methods (`GetTaskEditorBundleAsync`, `UpsertTaskNoteAsync`, `Get/Add/Update/DeleteTaskReferenceLinkAsync`, `SaveTaskEditorBundleAsync`) |
| ViewModels | `TaskReferenceLinkItemVm` round-trip; `QuanLyTaskViewModel` async `SuaTask`, 5 new commands |
| Views/QuanLyTaskPage.xaml | 3 zones: core task / Note / Study Links |
| Invariant | `PhanTichNhapNhanh` (quick parser) never touches notes/links |
| Tests | `TaskNotesTests.cs` (13 new) covering upsert, cascade, parser isolation, VM commands |

Verification: 128 → **141** pass.

## 2026-04-26 — M7 Study Time Predictor (ML MVP)

Offline-first FastTree regression. See [knowledge/machine-learning.md](knowledge/machine-learning.md) for full design notes.

| Module | What shipped |
|---|---|
| Schema | `StudyTimeInput` (6 features), `StudyTimeOutput`, `ModelMeta` |
| Storage | `IModelStorageProvider` + `LocalModelStorageProvider` (`%AppData%\SmartStudyPlanner\models\`) |
| Manager | `MLModelManager` with seed bootstrap (180 rows), 70/30 real+seed retrain merge, atomic `.tmp → rename` swap, R² gates (≥0.55 seed, ≥0.50 retrain) |
| Predictor | `StudyTimePredictorService` returns `(int Minutes, bool IsMLPrediction)` with confidence-vs-formula gate (≥0.6 to use ML) |
| Schema additions | `StudyLog.CreatedAtUtc`, `DeviceId`, `IsDeleted` (sync-ready, `EnsureCreated` adds columns) |
| UI | Dashboard `*` indicator with tooltip "Dự đoán bằng AI (thử nghiệm)" via `TaskDashboardItem.IsMLPrediction` |
| UI | Analytics "Tối ưu AI" button (enabled when ≥20 logs) |
| Tests | `MLModelManagerTests`, `StudyTimePredictorTests`, `LocalModelStorageTests` |

Code review verdict (`2026-04-26-m7-code-review.md`): MVP ship-ready, watch retrain semantics and benchmark R² with real data.

## 2026-04-25 — M5 Pipeline + UI shell + M6 Analytics

| Module | What shipped |
|---|---|
| M5 | `IPipelineOrchestrator` + 5 stages (`ParseInput`, `Prioritize`, `BalanceWorkload`, `AssessRisk`, `Adapt`); `PipelineContext` carries Semester/Settings/ReferenceTime/RiskReport/Adaptations |
| M5-TD1 | `StudyTaskStatus` constants (`ChuaLam`, `HoanThanh`) — 7 magic strings removed |
| M5-TD2 | `HocKy.NgayKetThuc [NotMapped]` + `AdaptStage` uses real end date with `FallbackSemesterDays=120` |
| M5-TD3 | Dashboard reads `pipelineResult.RiskReport`, drops duplicate priority+risk computation |
| M5-TD4 | Dashboard surfaces `Adaptations` as collapsible "GỢI Ý THÍCH NGHI" section |
| M6-1 | `StudyLog` entity + `StudyTask.NgayHoanThanh` + DbSet |
| M6-2 | `IStudyAnalytics` + `StudyAnalyticsService` (weekly minutes, subject insights, productivity score `completionRate×50 + min(streak,30)/30×30 + timeEfficiency×20`) |
| M6-3 | `FocusViewModel` writes `StudyLog` async on Pomodoro complete |
| M6-4 | `AnalyticsViewModel` with LiveChartsCore `ISeries`/`Axis` properties |
| M6-5 | `AnalyticsPage.xaml` with productivity score card + weekly bar + subject bar + details DataGrid |
| M6-6 | Sidebar `NavAnalytics` button |

Verification: 87 → 119 (M5) → 121 → 123 → 127 → 128 pass across the chain.

## 2026 — M1 → M4.6 (foundational refactor)

| Module | What shipped | Commit |
|---|---|---|
| M1+M2 | `ServiceLocator` DI root + `IDecisionEngine` / `DecisionEngineService` | `1cfe438` |
| M3 | `IWorkloadService` / `WorkloadServiceImpl` + `ScheduleModels.cs` | `45cbbb3` |
| M4 | `RiskAnalyzer/` strategy engine + Dashboard "Rủi Ro" column | `7b5d7d3` |
| M4.6 | Removed static facades `DecisionEngine.cs` + `WorkloadService.cs`; extracted `WeightConfig.cs`; migrated `MainWindow.xaml.cs` | `af673d2` |

After M4.6 the Services layer has zero `static class` in the domain. 110 tests pass.

## Bugs fixed

### `SqliteException: no such column: s.NgayHoanThanh`

- Root cause: `EnsureCreated()` only generates the DB if it doesn't already exist. After `StudyTask.NgayHoanThanh` was added, old local DBs were out of sync.
- Fix: swapped to `db.Database.Migrate()` in `App.xaml.cs`. Missing alterations are now applied on launch.

### `TaskNotesViewModelTests` URL assertion mismatch

- Root cause: `Uri.ToString()` normalizes `https://example.com` → `https://example.com/`.
- Fix: store `uri.OriginalString` in `QuanLyTaskViewModel.AddLink()` to preserve user-typed shape.

## Known pre-existing warnings (not in scope)

- Nullable reference type warnings across various files.
- `NU1904 — System.Drawing.Common 4.7.0` vulnerability (~30 min upgrade, tracked as backlog item N6).
