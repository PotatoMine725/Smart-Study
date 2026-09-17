# Epic 3 — Study Optimization Engine: Execution Plan

**Planning date:** 2026-08-04 · **Status:** **AWAITING OWNER APPROVAL** — not yet scope-frozen ·
**Implementation:** not started

> **Reads with:** [master plan](2026-07-03-master-plan.md) (Epic 3 §, T3.0–T3.7),
> [architecture freeze](2026-07-02-architecture-freeze-decisions.md) (D-G/D-H/D-J + §3 OPEN),
> [M3.0 allocator classification](2026-08-04-m3.0-allocator-baseline-vs-invariant.md) (five buckets),
> [handoff](2026-08-04-next-session-handoff.md), and `../specs/system_roadmap.md` §A.3 / §7.3.
>
> **This plan does not modify the master plan.** Every master-plan task ID (T3.0–T3.7) is preserved;
> one discovered prerequisite is added as **T3.8** (§3.1, PD-2). No code is changed by this document.

**Evidence labelling used throughout:** **[F]** confirmed project fact (verified in code or a merged
doc at HEAD `1a5ad7d`) · **[I]** engineering inference · **[R]** planning recommendation.

---

# Context — why this plan exists

Epic 1 (sync-ready data model) shipped and was Released 2026-07-20; post-Epic-1 stabilization closed
2026-08-02 (WP-1…WP-6). Nothing blocks Epic 3. **[F]**

The system's flagship known defect is now the one Epic 3 exists to fix: **the allocator places tasks
deadline-blind.** `WorkloadServiceImpl.GenerateSchedule` sorts by priority and then picks the
*least-loaded* day (`WorkloadServiceImpl.cs:139-141`), never reading `HanChot` — a task due in 3 days
can land on day 9. **[F]** The architecture freeze (D-G/D-H/D-J, 2026-07-02) already decided *how* to
fix it: deadline re-enters as a **hard constraint** owned by a Constraint Validator, the objective
scores **quality only** (`w1…w5`, `w6·DeadlineUrgency` dropped), and the engine may never worsen
feasibility (`violations(out) ≤ violations(in)`). **[F]**

Two things were left open on purpose and one was discovered during this planning pass:

1. **G2** — SOE pass accept/commit semantics. Open, overdue, blocks **M3.2 only**. **[F]**
2. **G3** — `w1…w5` weight-vector governance. Blocks M3.2 **ship**. **[F]**
3. **The schedule model cannot express a deadline violation** (§3.1 below) — a discovered
   prerequisite, not new scope. **[F]**

A fourth input arrived from the business side after Epic 1: the global 3-day grace period may not be
natural for every task type. §2 assesses it.

---

# 1. Context Summary

## 1.1 The pipeline that Epic 3 modifies

```
PipelineOrchestrator (5 stages)
  ParseInput → Prioritize → AssessRisk → BalanceWorkload → Adapt
                    │                          │
                    │                          └─ BalanceWorkloadStage.Execute:41
                    │                               → IWorkloadService.GenerateSchedule(HocKy, capacityHours)
                    │                                    → List<ScheduleDay>   ← Epic 3's target
                    └─ DecisionEngine.CalculatePriority → PriorityScore (DiemUuTien)
```

`GenerateSchedule` has exactly **two production call sites** — `BalanceWorkloadStage.cs:41` and
`WorkloadBalancerViewModel.cs:37`. **[F]** That is a small, tractable blast radius for the rework.

## 1.2 Current allocator behaviour (the thing being replaced)

`WorkloadServiceImpl.GenerateSchedule` (`:102-171`) **[F]**:

| Line | Behaviour | Epic 3 disposition |
|---|---|---|
| `:110` | filters out `HoanThanh` tasks | keep |
| `:112` | **writes `DiemUuTien` into the caller's model** (impure) | seam decision, T3.8 |
| `:118` | `OrderByDescending(t => t.DiemUuTien)` — priority is the task-ordering key | keep (load-bearing, see §3.4) |
| `:122-127` | seeds a fixed 7-day window from `_clock.Now.Date` | keep |
| `:139-141` | **least-loaded day with room** — never reads `HanChot` | **replace (T3.3)** |
| `:143-153` | appends days without bound when the window fills | keep; interacts with G2 |
| `:155-166` | splits into `(Phần n)` chunks against `capacityMinutes` | keep |
| `:94-100` | `ClampCapacityMinutes` — **termination guard** | **do not weaken** (see Risk R4) |

## 1.3 Decision Engine — where deadline lives today

Deadline reaches scheduling **only** through the priority scalar. `PriorityCalculator.cs:35` computes
`daysLeft` and is the single read of `HanChot` on the placement path. **[F]** The urgency chain is
composed in `SchedulingOrchestrator.cs:42-49` and returns on the **first** matching rule:

| # | Rule | Condition | Score |
|---|---|---|---|
| 1 | `OverdueRule` | `daysLeft < -3` | 0.0 |
| 2 | `JustOverdueRule` | `daysLeft < 0` | 100.0 |
| 3 | `ImminentRule` | `daysLeft < 1` | 95.0 |
| 4 | `CompletedRule` | status == `HoanThanh` | 0.0 |
| 5 | `BeyondHorizonRule` | `daysLeft > HorizonDays` | 1.0 |

**The 3-day grace period is live and reachable** — `OverdueRule` is first, so `-3 ≤ daysLeft < 0`
yields max urgency (100) and `daysLeft < -3` collapses to 0. **[F]** *(This was verified explicitly:
had the order been reversed, `OverdueRule` would have been unreachable and there would be no grace
period at all. It is not.)*

`WeightConfig` holds **four priority weights** (Time .40 / TaskType .30 / Credit .20 / Difficulty .10)
plus `HorizonDays = 60`. **The SOE's `w1…w5` vector does not exist anywhere in the codebase.** **[F]**
That fact is the entire substance of G3.

## 1.4 Optimization boundaries and hard constraints (frozen — do not relitigate)

- **D-G** — deadline feasibility is a **hard constraint** in the Constraint Validator; the objective is
  `w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty + w5·FragmentationPenalty`.
  `w6·DeadlineUrgency` is **dropped**; `DeadlineUrgency` belongs **exclusively** to `PriorityScore`.
- **D-H** — `violations(output) ≤ violations(input)`, compared by violation count then by total
  overdue minutes. Defined on *every* input, including infeasible ones. Reporting infeasibility stays
  the **Risk Analyzer's** job.
- **D-J** — Constraint Validation is a hard filter; Objective Evaluation ranks only admitted
  candidates. **No score can purchase a violation.** Two independent seams, independently tested.
- **D-E core** — deterministic ordered pipeline, **never a global search**.
- **Stable seam** — `Optimize(schedule) → (schedule, report)`.

## 1.5 Accepted technical debt entering Epic 3

- `WorkloadServiceImpl` naming (`→ Balancer/SOE`) — master plan says **fold into Epic 3's rework**,
  not a standalone pass. **[F]**
- `ServiceLocator` residual usage; pipeline rehome; `System.Drawing.Common` NU1904;
  `SQLitePCLRaw` NU1903; `ParseSource.MlOverridden` unused — all **out of scope**, untouched. **[F]**
- WP-4 test comments cite pre-`0e5d448` line numbers for `WorkloadServiceImpl.cs`; refresh
  opportunistically **inside T3.3**, not as a churn commit. **[F]**

## 1.6 Measurement substrate — what actually exists

`SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs` — 16 methods / 21 cases
characterizing the allocator, mutation-proven non-vacuous (WP-4). Classified into five buckets by
[`2026-08-04-m3.0-allocator-baseline-vs-invariant.md`](2026-08-04-m3.0-allocator-baseline-vs-invariant.md). **[F]**

**The suite is deadline-degenerate**: `NewTask` hardcodes `FixedNow.AddDays(5)` for every task in every
fixture, so all 21 cases run a single-deadline corpus and *cannot* detect a deadline regression. **[F]**
Consequence, and this is the one that matters: under equal deadlines any deadline-driven sort
degenerates to the existing priority sort, so **"still green after T3.3" is much weaker evidence than
it looks.** **[F]**

**T3.6 is one third done** — the characterization tests exist; the ≥200-schedule corpus and the
quantified baseline do not. **[F]**

## 1.7 Correction to a carried-forward citation

The M3.0 input doc (§4) and the handoff both state that **`866b5be` changed `GenerateSchedule` after
WP-4 ran**. That is wrong. **[F]** Verified at HEAD:

- `866b5be` ("fix(ui): scan for deadlines once per process") touches **`MainWindow.xaml.cs` only**,
  10 insertions, one file.
- The commits that touched `WorkloadServiceImpl.cs` after WP-4's tests landed (`e89f0ec`) are
  **`54f64ca`, `c3f2286`, `0e5d448`** — all WP-5 capacity-robustness work.
- **The placement loop was untouched by all three.** **[F]** `0e5d448` changed exactly one line inside
  `GenerateSchedule` — `int capacityMinutes = (int)(capacityHours * 60)` →
  `ClampCapacityMinutes(capacityHours)` — plus the new method; it also explains the +25-line drift in
  the test comments. `54f64ca` and `c3f2286` changed `GetCapacity` (capacity *input* validation), which
  is upstream of the allocator; **their end-to-end effect on the `WorkloadBalancerViewModel` path was
  not audited here** and is not assumed.

**Planning consequence — the conclusion survives, the reason changes. [F]**
Re-measuring the baseline at HEAD is still mandatory, but not because the *placement* logic drifted: it
did not. The real reason is §1.6 — WP-4 never measured a deadline-inversion baseline **at all**,
because its corpus cannot express one, so any inversion number quoted from WP-4 is structurally zero.
The operative consequence for execution: **T3.6's corpus, not a re-run of WP-4, is the only thing that
can produce a meaningful baseline.** An agent acting on the old "the allocator changed" reason would
re-run WP-4 and stop.

---

# 2. Deadline Policy Assessment

**The observation.** The global 3-day grace period is not natural for every task type; midterms and
finals likely need hard deadlines, other categories may legitimately allow grace.

**The assessment is that this is one observation containing two separable questions**, and they belong
in different places. Answering it as a single include/exclude produces the wrong answer either way.

## 2.1 Question (a) — per-type urgency decay after the deadline → **EXCLUDE from Epic 3**

This is the `OverdueRule` / `JustOverdueRule` cliff: for 0–3 days overdue a task scores 100, beyond
that 0, uniformly across all five `LoaiCongViec` values. **[F]**

**Recommendation: exclude, and record as a deferred proposal. [R]** Three reasons, in order of weight:

1. **D-G puts it out of Epic 3's scope by construction. [F]** D-G states `DeadlineUrgency` belongs
   **exclusively** to the Decision Engine's `PriorityScore` and does **not** appear in the SOE
   objective; `w6` was dropped for precisely this reason. Per-type urgency decay is a `PriorityScore`
   shaping rule. Placing it in Epic 3 would reopen a frozen decision to add a term that decision
   deleted.
2. **The seam is not in Epic 3's blast radius. [F]** The change lands in `IUrgencyRule` +
   `SchedulingOrchestrator.cs:42-49` — the Decision Engine, which Epic 3 consumes but does not modify.
   Epic 3 touches `WorkloadServiceImpl`, `ScheduleModels`, and the two new SOE seams.
3. **The architecture already supports it cheaply, so deferring costs almost nothing. [I]**
   `ITaskTypeWeightProvider` / `DefaultTaskTypeWeightProvider` already maps every `LoaiCongViec` to a
   weight (ThiCuoiKy 1.0 … BaiTapVeNha 0.1). **[F]** A per-type grace policy is the same shape: one
   provider, one map, injected into `OverdueRule`. Deferring does not paint anyone into a corner.

**Does it change existing optimization assumptions?** Marginally, and not in a way that blocks Epic 3.
**[I]** A true hard deadline for exams implies an overdue exam task should stop being *scheduled*, not
merely deprioritized — today `GenerateSchedule` schedules any non-completed task no matter how overdue
(`:110`). **[F]** But D-H already gives the mechanism: an overdue task is simply **infeasible**, and the
SOE must report rather than silently absorb it. So **Epic 3 delivers the mechanism; only the per-type
policy is deferred.** That is the clean split, and it is why the deferral is safe.

## 2.2 Question (b) — the validator's deadline predicate → **INCLUDE in Epic 3, as T3.1**

Separately from *policy*, T3.1 must decide what the validator's deadline predicate **is**:

- Does any placement dated after `HanChot` count as a violation?
- At what granularity — **date** or **time-of-day**? The M3.0 doc's bucket ③ already surfaces this as
  live: with a 09:00 deadline on day 5, is day 5 usable? The two readings give 360 vs 300 available
  minutes on the same fixture and change whether it is infeasible. **[F]**
- What is the violation *magnitude* for D-H's second comparison key (total overdue minutes)?

**T3.1 cannot be built without answering these. [F]** They are not optional and not deferrable.

**Recommendation. [R]** Epic 3 ships **one uniform hard-deadline predicate** — no per-type behaviour —
with the predicate placed behind an **extension point** (`IDeadlinePolicy` or equivalent) that has
exactly one implementation in Epic 3. Per-type policy then becomes a second implementation later,
touching no SOE internals.

**Rationale for the extension point specifically. [I]** It costs one interface and zero behaviour, and
it prevents the failure mode where the uniform rule gets hardcoded into the validator's guts and the
deferred work turns into a rewrite. It is *not* speculative generality: we have a named, dated business
observation that a second implementation is wanted. Declaring the seam without implementing it is the
cheapest honest response.

## 2.3 Architectural impact if question (a) were later accepted

Recorded now so the deferred proposal is costed, not just filed. **[I]**

| Surface | Impact |
|---|---|
| `IUrgencyRule` + `SchedulingOrchestrator.cs:42-49` | `OverdueRule` takes an injected per-type policy; rule ordering must be re-verified (§5.2) |
| `ITaskTypeWeightProvider` | precedent to copy; possibly a sibling `ITaskTypeDeadlinePolicy` |
| SOE Constraint Validator | second `IDeadlinePolicy` implementation — **no SOE internals change** if T3.1 lands the extension point |
| Risk Analyzer | `DeadlineUrgencyRiskEvaluator` must adopt the same definition (§5.1) |
| `DecisionEngineTests`, `UrgencyRulesTests` | per-type cases; composed-chain tests (§5.2) |
| Persistence / schema | **none** — `LoaiCongViec` already exists on `StudyTask` |

No schema change, no migration, no sync-metadata impact. This is a genuinely cheap deferral.

## 2.4 Verdict

| Sub-question | Verdict | Owner |
|---|---|---|
| (a) Per-type grace / urgency decay after deadline | **Deferred out of Epic 3** | Deferred proposal DP-1 (§5.3) |
| (b) Validator deadline predicate + granularity | **In Epic 3** | **T3.1**, named deliverable |
| (c) Extension point for future per-type policy | **In Epic 3** (declared, one impl) | **T3.1** |

---

# 3. Epic 3 Execution Plan

## 3.1 The discovered prerequisite — the schedule model cannot express a violation

**[F]** `Models/ScheduleModels.cs`:

```csharp
public class ScheduledTask {
    public string TenTask { get; set; }   // mangled: "Bài 1 (Phần 2)"
    public string TenMon  { get; set; }
    public int    SoPhut  { get; set; }
}
public class ScheduleDay {
    public DateTime Date; public string DisplayName; public int TotalMinutes;
    public List<ScheduledTask> Tasks;
}
```

**There is no task identity and no deadline on a scheduled item, and `TenTask` is mangled with
`(Phần n)` suffixes** (`WorkloadServiceImpl.cs:160`). So there is **no path from a scheduled item back
to `HanChot`**.

**Consequence. [I]** `IConstraintValidator` cannot validate a `List<ScheduleDay>` — it cannot compute
whether an item sits after its deadline. This blocks **T3.1**, **T3.4's inversion test**, and the
frozen `Optimize(schedule) → (schedule, report)` seam itself. The frozen seam names a *schedule*; it
does not freeze that schedule to be `List<ScheduleDay>`.

**This is a discovered prerequisite, not scope expansion. [R]** It is the direct precedent of **T1.8**,
which master-plan v2 added when the review found `EnsureCreated()` could not alter existing DBs —
"without T1.8, Epic 1 could not ship to testers at all." Same shape: without T3.8, Epic 3's frozen seam
is not constructible. It is added as **T3.8**, keeping every existing master-plan task ID intact and
leaving the master plan itself unmodified.

**Design. [R]** Introduce an SOE-internal schedule type whose items carry `MaTask` (Guid) + `HanChot` +
the base task name, with `List<ScheduleDay>` retained as a **UI projection** produced at the boundary.
The two existing call sites (`BalanceWorkloadStage.cs:41`, `WorkloadBalancerViewModel.cs:37`) and the
XAML bindings keep consuming `List<ScheduleDay>` unchanged. **[F]** Small blast radius, no UI rework.

## 3.2 Objectives

1. Replace deadline-blind placement with a deadline-aware allocator behind an `IScheduleOptimizer`
   seam, under the frozen guardrails.
2. Ship `IConstraintValidator` and `IObjectiveEvaluator` as independent, independently-tested seams.
3. Prove D-G (no inversions) and D-H (`violations(out) ≤ violations(in)`) on a real corpus, against a
   real committed baseline.
4. Make every rejection explainable — "infeasible" distinguished from "lower score".
5. Close G2 and G3 as merged decision notes.

## 3.3 Scope

**In scope.** T3.6 corpus + baseline; T3.0/G2; T3.8 schedule identity seam; T3.1 validator (incl. the
deadline predicate + extension point); T3.2 objective evaluator (`w1…w5`); T3.3 allocator rework +
pass loop; T3.4 D-H + inversion property suite; T3.7 `OptimizerRunLog`; T3.5/G3; the
`WorkloadServiceImpl` → Balancer/SOE **naming** debt folded into T3.3; architecture-doc + roadmap
updates after code lands (D-C).

**Out of scope.** Global search / metaheuristics / argmax (D-E core); `w6·DeadlineUrgency` (D-G);
the SOE proposal's six sub-engines (roadmap §13); autonomous ML scheduling (§6); per-type deadline
policy (§2.1); anything in Epic 2 or Epic 4; every item in §1.5.

## 3.4 Assumptions — stated so they can be falsified, not assumed silently

| # | Assumption | If false |
|---|---|---|
| A1 | **Priority stays the task-ordering key; deadline governs day selection only.** **[F]** this is the M3.0 doc's explicit load-bearing assumption | Bucket ② collapses into ①; `UuTienCaoDuocXepTruoc` goes red even under earliest-feasible; re-triage before T3.3 |
| A2 | **Earliest-feasible placement** is ratified at T3.3 DoR **[R]** (recommended, *not* ratified) | Bucket ② (4 methods / 5 cases) goes red by design, not by regression |
| A3 | T3.1/T3.2 are buildable **before G2 closes** — the deadline predicate and the quality objective are independent of accept/commit semantics **[I]** | M3.1 blocks on G2; Epic 3 stalls entirely |
| A4 | The **report/reason-code shape depends on G2** and is therefore scoped to M3.2 **[I]** | T3.1/T3.2 would need rework once G2 lands |
| A5 | `List<ScheduleDay>` stays the UI contract; T3.8 adds a richer internal type behind it **[R]** | UI rework enters scope — re-plan |
| A6 | The corpus harness may use a corpus-owned name→deadline side table for T3.6's baseline, **before** T3.8 exists **[R]** | T3.6 blocks on T3.8 and M3.0 loses its parallel-safety |

**A6 carries a specific risk and a specific mitigation. [I]** The harness's inversion count and the
production validator's could disagree. **T3.4 must re-measure the same corpus through the production
validator and the two numbers must agree.** That cross-check is an acceptance criterion, not a nicety —
it is what converts the harness crutch into verified equivalence.

## 3.5 Dependencies

- **Epic 1 complete** — satisfied (Released 2026-07-20). **[F]**
- **Epic 2 not required.** **[F]** Execution order is E1 → E3 → E2 → E4. The stabilization plan's
  "Epic 2 entry criteria (12/12)" is a **gate name, not the order** — this has been misread before.
- **G2 blocks M3.2 only.** M3.0 and M3.1 proceed now. **[F]**
- **G3 blocks M3.2 ship.** **[F]** **G4 is Epic 2 only** — not Epic 3's problem.
- Internal: T3.6a → T3.6b; T3.8 → T3.1 → T3.3 → T3.4; T3.2 ∥ T3.1; T3.7 after T3.3.

## 3.6 Work packages

### M3.0 — Measurement substrate + G2 *(no production code)*

**T3.6a — Corpus generator.** ≥200 generated schedules, ≥25% infeasible, deterministic seed, committed
in the test project.
**Mandatory: varied deadlines.** The existing fixtures cannot express a deadline inversion, so any
inversion count derived from them is structurally zero — a meaningless baseline that would make T3.3
look like a free win. **[F]** Corpus must cover: deadline inversions; ties; deadlines inside and
outside the 7-day seed window; overdue tasks; the exam-vs-homework type mix; infeasible semesters;
and the capacity edge values already pinned by `CapacityDuoiSan_BiKepVeMotGio_KhongTreo`.

**T3.6b — Baseline capture.** Run the **current** allocator over the corpus; emit a **committed
artifact** (JSON or CSV under `docs/reports/data/` or a fixtures path — *not* prose in a report;
ledger #3). **[F]** Must record the exact HEAD commit SHA in the artifact. Metrics: deadline-inversion
count, D-H violation count, total overdue minutes, per-schedule objective inputs, runtime. Uses the A6
side table; label that in the artifact.

**T3.0 — GATE G2.** Close SOE pass accept/commit semantics as a `docs/plans/YYYY-MM-DD-*.md` decision
note. Must resolve L8's two named defects — the **all-or-nothing veto** and the **determinism paradox**
("a deterministic rejected pass re-runs identically, so iteration is a no-op") — by either stating
explicitly what varies between iterations or explicitly claiming single-shot semantics and accepting
the consequences. **[F]** Must also finalize the **objective non-worsening threshold**, which master
plan v2 deliberately deferred to this note. **[F]** Frozen regardless of outcome: D-E core, D-G/D-H/D-J,
`w1…w5`, the `Optimize` seam.

### M3.1 — Frozen-semantics stages *(pure, independently tested)*

**T3.8 — Schedule identity seam** *(discovered prerequisite, §3.1)*. Internal schedule type carrying
`MaTask` + `HanChot` + base name; `List<ScheduleDay>` becomes a boundary projection; both call sites
and all XAML bindings unchanged.
**Also decides, in writing, the bucket-④ `DiemUuTien` write-through** (`WorkloadServiceImpl.cs:112`):
does the pure `Optimize` seam keep the side effect, or does priority stamping move to whatever owns it?
**The failure mode to avoid is letting `GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh` go
red mid-T3.3 and get "fixed" by re-adding the write-through out of reflex.** **[F]**

**T3.1 — `IConstraintValidator`.** Deadline / capacity / calendar predicates as a **hard filter**
(D-J). Deliverables: the deadline predicate with its **date-vs-time-of-day granularity decided and
documented** (§2.2); violation magnitude in overdue minutes for D-H's second key; the `IDeadlinePolicy`
extension point with exactly one uniform implementation; per-seam unit tests. Buildable pre-G2 (A3).

**T3.2 — `IObjectiveEvaluator`.** `w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality +
w4·FatiguePenalty + w5·FragmentationPenalty`, **quality only**. Weights live in a **new SOE weight
vector, separate from `WeightConfig`** — see G3 input in §4/PD-5. Per-seam unit tests; independent of
the validator (D-J). Buildable pre-G2. Runs in parallel with T3.1.

### M3.2 — Optimizer assembly *(gated on G2; G3 before ship)*

**T3.3 — Deadline-aware allocator rework + pass loop per the G2 outcome.**
- **DoR: ratify the placement strategy in writing** (A2 — earliest-feasible recommended, not decided).
- Replace `:139-141`; keep the priority sort at `:118` (A1).
- **Rewrite bucket ① `GenerateSchedule_ChonNgayITAINHAT_ChuKhongPhaiNgaySomNhatConCho`** to pin the new
  rule, with the same mutation check WP-4 used. Expected casualty — do **not** revert the code. **[F]**
- **Decouple bucket ② assertions** from `days[0]` to `First(d => d.Tasks.Count > 0)`. That single habit
  is the whole difference between bucket ② and bucket ⑤ and is the pattern to copy. **[F]**
- Refresh the drifted line-number comments opportunistically (§1.5).
- Fold in the `WorkloadServiceImpl` → Balancer/SOE **naming** debt.
- **Do not weaken `ClampCapacityMinutes`** — see R4.

**T3.4 — D-H invariant + inversion property suite** on the T3.6 corpus, including infeasible inputs.
Includes the **A6 cross-check**: re-measure the baseline through the production validator; the numbers
must agree with T3.6b's artifact.

**T3.7 — `OptimizerRunLog` telemetry**, following the M8 telemetry pattern (`TelemetrySchema` patch-seam
precedent). Reason codes per the G2-defined report shape.

**T3.5 — GATE G3.** `w1…w5` governance: ownership, guardrails, and the relation to `WeightOptimizer`.
Decision note merged **before ship**.

## 3.7 Execution order

```
M3.0   T3.6a ──► T3.6b                    [test project only]
       T3.0 (G2) ────────── parallel ─────┘   [docs only]      ◄── OWNER CP-1
         │
M3.1   T3.8 ──┬──► T3.1                   [T3.8 gates both]    ◄── OWNER CP-2 (DiemUuTien seam)
              └──► T3.2   (T3.1 ∥ T3.2)
         │
M3.2   T3.3 ──► T3.4 ──► T3.7             [sequential; G2 must be closed]  ◄── OWNER CP-3 (strategy)
       T3.5 (G3) ── parallel during M3.2 ──┘                     ◄── OWNER CP-4 (ship gate)
```

**Parallel-dispatch decision. [R]**

| Phase | Agents | Rationale |
|---|---|---|
| M3.0 | **2** — one T3.6a→b, one T3.0 | genuinely independent: test project vs. docs, zero shared files |
| M3.1 | **1 then 2** — T3.8 alone, then T3.1 ∥ T3.2 | T3.8 changes a shared model; parallelizing across it would conflict. T3.1/T3.2 are independent by D-J |
| M3.2 | **1** (+1 for T3.5) | T3.3→T3.4→T3.7 all touch the allocator and its suite; serialize. G3 is a docs track |

**Do not parallelize within T3.3.** It edits one file and one test suite.

## 3.8 Acceptance criteria

Epic-level (from the master plan, unchanged):

1. **Inversion test** — a near-deadline task is never displaced past its deadline by a
   quality-improving rearrangement (D-G).
2. **Property test on the corpus** — `violations(out) ≤ violations(in)` (count, then overdue minutes)
   on **every** input, including infeasible ones (D-H).
3. **No objective score can purchase a constraint violation** (D-J); validator and evaluator tested
   independently.
4. Same input ⇒ same output; **every rejection carries a machine-readable reason**.

Success metrics (measured on the T3.6 corpus):

5. **0** D-H breaches; **0** deadline inversions — against a baseline that is **> 0**, since a
   structurally-zero baseline would make T3.3 look like a free win (§1.7).
6. Determinism: byte-identical outputs across 3 repeated full-corpus runs.
7. Explainability: 100% of rejected candidates carry a reason code.
8. Objective delta vs. baseline reported per corpus schedule; **non-worsening threshold finalized in
   the G2 note**, not invented here.
9. Runtime: full SOE run **< 2 s p95** on the reference semester fixture (provisional).

Added by this plan:

10. **A6 cross-check** — T3.4's validator-measured baseline agrees with T3.6b's harness artifact on
    the same corpus.
11. **Bucket ⑤ stays green** (8 methods / 12 cases). A red there is a real regression — full stop.
12. **Buckets ① and ② are triaged in writing before any test edit**, each with a mutation check beside
    it. A test edited without one records nothing. **[F]**
13. Suite total ≥ 391 and no pre-existing test deleted without a written justification. **[F]**

## 3.9 Risks

| # | Risk | Mitigation |
|---|---|---|
| **R1** | **Green suite misread as validating deadline-awareness.** The corpus is deadline-degenerate; under equal deadlines any deadline-driven sort degenerates to the priority sort, so most tests survive T3.3 *by construction*. **[F]** | T3.6a's varied-deadline corpus is the **only** valid evidence for T3.3. State this in the T3.3 review. Bucket ⑤ green ≠ deadline-awareness proven |
| **R2** | **G2 stalls M3.2.** Open and overdue. **[F]** | M3.0 + M3.1 are unblocked and are ~⅔ of Epic 3. Escalate at CP-1 if G2 is not closed by the time T3.2 lands |
| **R3** | **First red test becomes an argument instead of a decision**, and the likeliest wrong resolution ("make it green") silently reverts the rework. **[F]** | The five-bucket classification is the triage table; each bucket carries a different instruction. "Make it green" is correct for exactly one of the five |
| **R4** | **Weakening `ClampCapacityMinutes` makes the capacity tests HANG, not fail** — `while (remainingMinutes > 0)` makes no progress when `capacityMinutes < 1`, so CI sits until timeout. Goes live the moment T3.3 touches the allocation loop. **[F]** | **A CI timeout during T3.3 must be read as this first.** Do not weaken `:94-100`. Named in the T3.3 task card |
| **R5** | Chosen pass semantics reintroduce local-optimum or no-op-iteration defects (L8) | Corpus metrics detect both; G2 note must address them explicitly |
| **R6** | T3.8 scope-creeps into a UI rework | A5 binds it: `List<ScheduleDay>` stays the UI contract; both call sites unchanged |
| **R7** | Per-type deadline policy gets pulled in mid-execution | §2 is the written exclusion; DP-1 is the parking spot; T3.1's extension point removes the pressure |
| **R8** | Baseline captured after T3.3 starts → worthless | T3.6b is a **hard blocker** for T3.3's acceptance evidence, not an M3.0 nicety. Artifact records its SHA |

## 3.10 Owner checkpoints

| CP | When | Decision the owner owns |
|---|---|---|
| **CP-0** | **Now** | Accept this plan's scope, incl. the §2 deadline-policy split and T3.8 as a discovered prerequisite |
| **CP-1** | End of M3.0 | **G2 ratified** (pass accept/commit semantics + non-worsening threshold). Blocks M3.2 |
| **CP-2** | T3.8 | **`DiemUuTien` write-through seam** — keep or drop, in writing |
| **CP-3** | T3.3 DoR | **Placement strategy ratified** — earliest-feasible or an argued alternative |
| **CP-4** | Before ship | **G3 ratified** (`w1…w5` governance) + Epic 3 success metrics reported (DoD-7) |

## 3.11 Engineering gates

- **DoR per task** (master plan, 6 checks): decisions closed · upstream done · acceptance criteria +
  owning metric identified · `gitnexus_impact` run with HIGH/CRITICAL surfaced to the owner · test
  strategy named and placed per the mirror-namespace convention · schema tasks state their upgrade path
  (**Epic 3 touches no schema except T3.7's telemetry table** — that one does).
- **DoD per epic** (7 checks): `gitnexus_impact` before edits · `gitnexus_detect_changes` before every
  commit · `dotnet build SmartStudyPlanner.slnx` + `dotnet test --no-build` green · acceptance tests
  present · architecture docs + roadmap §A.3/§7.3 updated **after** code lands (D-C/D-C.1) · open
  decisions closed in dated notes · **success metrics measured and reported in the closing note**.
- **Reports** (repo rule, from 2026-07-07): every `docs/reports/*.md` carries an ADR-style
  **"Decisions made"** section — why / what for / experience.

## 3.12 Per-agent task cards

> Common to every card — **Venue:** `D:\Code\C#\SmartStudyPlanner`, branch off `dev`.
> **Tools:** GitNexus MCP first (`gitnexus_impact` before any symbol edit, `gitnexus_detect_changes`
> before any commit), then Read/Edit/Grep. RTK prefix on all shell commands.
> **Never:** modify [`2026-07-03-master-plan.md`](2026-07-03-master-plan.md); touch Epic 2 / Epic 4
> surfaces; reopen D-G/D-H/D-J/D-E.

**Card A — T3.6a/b (corpus + baseline).** *Mission:* build the ≥200-schedule, ≥25%-infeasible,
**varied-deadline** corpus generator and capture the current-allocator baseline as a committed
artifact naming its HEAD SHA. *Scope:* `SmartStudyPlanner.Tests/**` + one artifact file. **Zero
production files.**
*Must — the A6 crutch has to self-check (PD-7):* the generator emits **unique task base names**, and
the harness **asserts** that every scheduled item's `(Phần n)`-stripped name resolves to **exactly one**
corpus task, failing loudly on ambiguity. Without this, criterion 10 catches a harness/validator
disagreement only at T3.4 — after T3.3 has already been reviewed against the harness number.
*Stop:* artifact committed; deterministic across 3 runs; name-resolution assertion active; inversion
count **> 0** (a zero baseline means the corpus is still degenerate — go back); **and at least one
schedule feasible-but-improvable**, otherwise T3.2's objective delta has nothing to measure on.

**Card B — T3.0 (G2).** *Mission:* close SOE pass accept/commit semantics as a dated decision note.
*Scope:* `docs/plans/` only. *Must:* resolve L8's all-or-nothing veto **and** determinism paradox; set
the objective non-worsening threshold. *Stop:* note merged, owner-ratified at CP-1.

**Card C — T3.8 (schedule identity).** *Mission:* give scheduled items `MaTask` + `HanChot`; keep
`List<ScheduleDay>` as the UI projection; decide the `DiemUuTien` write-through in writing.
*Scope:* `Models/ScheduleModels.cs`, `Services/WorkloadServiceImpl.cs`, the two call sites, their tests.
*Stop:* suite green at ≥ 391, both call sites and all XAML unchanged, seam decision written down.

**Card D — T3.1 (validator).** *Mission:* `IConstraintValidator` as a hard filter; deadline predicate
with granularity decided; `IDeadlinePolicy` extension point, one uniform impl. *Scope:* new SOE
namespace + mirrored tests. *Stop:* per-seam tests green, granularity decision documented, **no
per-type behaviour shipped**.

**Card E — T3.2 (evaluator).** *Mission:* `IObjectiveEvaluator` over `w1…w5`, quality only, in a
**separate** weight vector from `WeightConfig`. *Scope:* new SOE namespace + mirrored tests.
*Stop:* per-seam tests green; **no deadline term anywhere** (D-G); independent of the validator (D-J).

**Card F — T3.3 (allocator rework).** *Mission:* deadline-aware placement + pass loop per G2.
*Pre-flight:* read the five-bucket classification; ratify placement strategy in writing at DoR.
*Scope:* `WorkloadServiceImpl.cs` (+ SOE naming), `WorkloadServiceScheduleTests.cs`.
*Stop:* bucket ⑤ green; ① rewritten with a mutation check; ② decoupled; **a CI timeout means
`ClampCapacityMinutes` — check that first**.

**Card G — T3.4 + T3.7.** *Mission:* D-H + inversion property suite on the corpus; A6 cross-check;
`OptimizerRunLog` per the M8 telemetry pattern. *Scope note — T3.7 is the one schema-touching task in
Epic 3, so DoR check 6 applies:* the upgrade mechanism is the existing **`TelemetrySchema.EnsureTables`
patch-seam** (M8 precedent, the same route `EnsureCreated()` cannot take on existing DBs); state the
backup/rollback story before the change. *Stop:* all 13 acceptance criteria in §3.8 demonstrated with
output pasted, not asserted.

---

# 4. Planning Decisions *(ADR-style — only those that materially change implementation)*

### PD-1 — Split the deadline-policy observation in two; exclude (a), include (b)

- **Why:** answering include/exclude as one question gives the wrong answer either way. D-G puts
  per-type *urgency decay* outside the SOE by construction, while the validator's deadline *predicate*
  is something T3.1 literally cannot be written without.
- **What for:** T3.1 gets an unambiguous, non-negotiable deliverable; the business observation gets a
  costed home (DP-1) instead of quietly leaking into the allocator rework mid-execution.
- **Experience:** "is this in scope?" asked of a compound observation is how frozen decisions get
  reopened by accident. Splitting first made D-G decisive rather than debatable.

### PD-2 — T3.8 added as a discovered prerequisite, not new scope

- **Why:** `ScheduledTask` carries no identity and no deadline, so `IConstraintValidator` cannot
  validate a `List<ScheduleDay>` at all. The frozen `Optimize(schedule) → (schedule, report)` seam is
  not constructible without it. Exactly the T1.8 precedent from master-plan v2.
- **What for:** the blocker surfaces in planning with a task ID and a card, instead of surfacing on day
  one of T3.1 as an unbudgeted redesign.
- **Experience:** v2's own change log distinguishes "discovered prerequisite" from "scope expansion".
  Naming which one this is, up front, is what keeps the distinction credible.

### PD-3 — Corpus before rework; baseline as a committed artifact with its SHA

- **Why:** a baseline measured after T3.3 starts is worthless, and a baseline derived from the existing
  fixtures is **structurally zero** because they cannot express a deadline inversion — which would make
  T3.3 look like a free win.
- **What for:** T3.6b becomes a hard blocker on T3.3's acceptance evidence rather than an M3.0 nicety,
  and criterion 5's "baseline > 0" is a falsifiable check on the corpus itself.
- **Experience:** ledger #3 — numbers live in a committed artifact, not in narrative. Prose baselines
  get quoted after the code they described has changed.

### PD-4 — Correct the `866b5be` citation; keep the conclusion, replace the reason

- **Why:** `866b5be` touched only `MainWindow.xaml.cs`. The commits that touched the allocator file
  after WP-4 are `54f64ca` / `c3f2286` / `0e5d448`, and **none of them changed the placement loop**;
  `0e5d448` changed only the capacity-clamp entry line. *(Scoped claim: `54f64ca`/`c3f2286` changed
  `GetCapacity`, upstream of the allocator — their end-to-end effect was not audited and is not
  assumed.)*
- **What for:** the real reason to re-measure is **corpus degeneracy** (§1.6), not allocator drift —
  and a re-run of WP-4 does not fix corpus degeneracy. An agent acting on the old reason would re-run
  WP-4 and stop, producing a structurally-zero baseline and declaring T3.3 a free win.
- **Experience:** the prompt asks that inconsistencies between prior decisions and current state be
  resolved through planning. Verifying the citation cost one `git show` and changed what T3.6 must do.

### PD-5 — Two separate weight vectors; `WeightOptimizer` does not touch SOE weights in Epic 3

- **Why:** `WeightConfig` holds four **priority** weights (Time/TaskType/Credit/Difficulty) consumed by
  `PriorityCalculator`. SOE `w1…w5` is a **different vector for a different stage** and does not exist
  in the codebase. Merging them would let a priority-tuning change silently move schedule quality.
- **What for:** T3.2 has an unambiguous home for its weights, and G3 opens with a stated position to
  argue against rather than an open field.
- **Experience:** D-G dropped `w6` precisely to stop deadline leaking across the priority/quality
  boundary. Merging the vectors would reintroduce the same coupling one level up.

### PD-6 — G2 does not gate M3.1; the report shape does

- **Why:** the deadline predicate (T3.1) and the quality objective (T3.2) are independent of
  accept/commit semantics. What *does* depend on G2 is the report and reason-code shape.
- **What for:** ~⅔ of Epic 3 proceeds while an overdue gate is open, without pre-committing anything
  G2 might invalidate. Scoping the report to M3.2 is what makes that safe.
- **Experience:** G2 has been open since 2026-07-02. A plan that blocks everything on it would have
  delivered nothing for a month.

### PD-7 — A6's measurement crutch is allowed, and cross-checked

- **Why:** T3.6's baseline needs a name→deadline mapping that T3.8 has not built yet. Blocking T3.6 on
  T3.8 would destroy M3.0's parallel-safety — the one property that lets the corpus land early.
- **What for:** M3.0 runs at full width; the crutch is confined to the test harness and labelled in the
  artifact.
- **Experience:** an unverified measurement path is how a baseline and a validator end up disagreeing
  silently. Acceptance criterion 10 makes the two numbers meet.

---

# 5. Deferred Considerations *(discovered, deliberately excluded)*

### 5.1 DF-1 — Two definitions of "overdue" coexist **[F]**

`IUrgencyRule` (Decision Engine): `daysLeft < -3 → 0`, `daysLeft < 0 → 100` — a 3-day cliff.
`DeadlineUrgencyRiskEvaluator` (Risk Analyzer, `Core/Risk/Evaluators/`): `daysLeft < 0 → 1.0`, **no
cliff**. Two components hold different definitions of the same word.

**Why it matters to Epic 3:** D-H assigns infeasibility **reporting** to the Risk Analyzer while the
SOE owns the feasibility **predicate**. The SOE report must agree with one of these.
**Disposition:** **T3.1 must state which definition its predicate adopts and why**, and note the
divergence in the SOE report. Reconciling the two components is **deferred** — it is Decision Engine /
Risk Analyzer work, not SOE work. *Owner: T3.1 (statement) → DP-1 (reconciliation).*

### 5.2 DF-2 — `CompletedRule` is shadowed in the composed urgency chain **[F]** / observability **[I]**

`SchedulingOrchestrator.cs:42-49` orders the rules `Overdue → JustOverdue → Imminent → Completed →
BeyondHorizon`, and `PriorityCalculator.Calculate` returns on the **first** match. So for
`daysLeft ∈ [-3, 1)`, `JustOverdueRule` (100) or `ImminentRule` (95) fires **before** `CompletedRule`
is ever reached: **a completed task with a deadline in the last 3 days or the next <1 day scores 100 or
95 instead of 0.** `UrgencyRulesTests` calls each rule's `TryApply` directly and never exercises the
composed chain, so nothing catches it. **[F]**

**Blast radius. [F]** `PrioritizeStage:48`, `WorkloadServiceImpl:110` and `MainWindow.xaml.cs:148` all
filter completed tasks *before* calling `CalculatePriority` — unaffected.
`QuanLyTaskViewModel.TinhDiemVaSapXep()` (`:103-105`) does **not** filter, and sorts the list by
`DiemUuTien` descending. The `MucDoCanhBao` label is separately forced to `"Đã xong"`, so the badge is
right while the **sort position** is wrong: a just-finished task sorts above genuinely urgent pending
ones in the deadline-management list.

**Confidence:** the rule order, the missing composed-chain test, and the unfiltered call site are all
**[F]** (read at HEAD). That this is user-visible follows from the sort, and is **[I]** — **not
reproduced at runtime.** Reproduce before treating it as a defect report.

**Disposition: deferred — out of Epic 3.** All three **scheduling** call sites pre-filter completed
tasks, so **the SOE is unaffected and Epic 3 inherits nothing from this.** The one unfiltered call site
is a **UI sort** in `QuanLyTaskViewModel` — Decision Engine / UI territory, not SOE. It is recorded here
because **it lives in exactly the rule chain a per-type deadline policy would edit**, so whoever picks
up DP-1 inherits both the fix and the ordering hazard. *Owner: DP-1, or a standalone bugfix.*

### 5.3 DP-1 — Per-type deadline policy *(the deferred proposal)*

Per-type grace periods / hard deadlines by `LoaiCongViec` — exams hard, homework graced. Excluded per
§2.1. **Seam:** `IUrgencyRule` + `SchedulingOrchestrator.cs:42-49`, with
`DefaultTaskTypeWeightProvider` as the shape to copy. **Cost:** §2.3 — no schema, no migration, no sync
impact. **Prerequisite it inherits from Epic 3:** T3.1's `IDeadlinePolicy` extension point, so the
validator side is a second implementation rather than a rewrite. **Bundle with DF-1 and DF-2** — all
three touch the same chain.

### 5.4 DP-2 — Objective-weight tuning / `WeightOptimizer` extension to `w1…w5`

G3 decides governance; **tuning** is a separate, later question. Epic 3 ships fixed default `w1…w5`.
Excluded: no data exists to tune against until T3.7's `OptimizerRunLog` has accrued rows — the same
data-gated pattern as M8-B.

### 5.5 DP-3 — `ScheduleDay` / `ScheduledTask` as persisted entities

T3.8 gives scheduled items identity **in memory only**. Persisting schedules (and thus syncing them)
is a data-model change with D-I metadata implications and belongs to Epic 2's substrate discussion, not
Epic 3. Explicitly excluded.

### 5.6 Untouched by this plan *(context only)*

Analytics two-section redesign (design brief only) and UI mobile-ready polish (proposed, unimplemented)
— both queued in [`../active/README.md`](../active/README.md), neither blocking Epic 3. **[F]**
`dev` is **one commit ahead of `origin/dev`** (unpushed) and the working tree holds three modified
`.claude/` files plus an untracked `docs/assets/*.zip`, all deliberately left alone. **[F]**
Per its own header, `2026-08-04-next-session-handoff.md` is deleted once M3.0 is underway.

---

# Verification

**This plan produces no code.** Verification of the *plan* is that its factual claims hold at HEAD:

```bash
rtk git log --oneline -1                      # 1a5ad7d
rtk git show --stat --oneline 866b5be         # MainWindow.xaml.cs only — PD-4
rtk git log --oneline e89f0ec..HEAD -- SmartStudyPlanner/Services/WorkloadServiceImpl.cs
                                              # 0e5d448, c3f2286, 54f64ca — PD-4
```

Read `SchedulingOrchestrator.cs:42-49` (rule order — §1.3, DF-2) and `Models/ScheduleModels.cs`
(no identity, no deadline — §3.1, PD-2).

**Verification of the implementation** is the DoD in §3.11 plus the 13 acceptance criteria in §3.8,
with the two that are easiest to fake called out explicitly:

- Criterion 5's baseline must be **> 0**. A zero baseline means the corpus is still deadline-degenerate,
  not that the allocator was already correct.
- Criteria 11/12: bucket ⑤ green is necessary, **not sufficient** — under the degenerate corpus it
  proves the rework did not break the equal-deadline case and nothing more (R1).

Full-suite baseline to hold or beat: **391 passed / 0 failed**, build 0 errors / 84 warnings. **[F]**
