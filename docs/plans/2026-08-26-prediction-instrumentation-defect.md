# Defect — `StudyTimeOutcomeLog` records that a prediction happened, but not what it was

> **Raised 2026-08-26** under **DFD-9a** (owner ruling:
> [`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) §13, §17).
> Audit gap `G-7`; audit decision `OD-5`.
>
> **This is a shipped-code defect, deliberately separated from the Data Maturation workstream.** The
> ruling raises it *now* because every day of delay destroys calibration data that cannot be
> reconstructed — while the Data Maturation proposal is allowed to proceed at proposal pace.

## Status

**`draft` — raised and specified; implementation NOT authorized.**

The ruling authorizes *raising* the defect. It states explicitly that it is *"a separate engineering
task, not authorization to change the confidence gate"*, and the record it comes from does not
authorize implementation by itself. **This document does not entitle anyone to start the fix.** The
owner's go/no-go is the next event; §7 states what to decide.

## 1. The defect

Every completed study session writes one `StudyTimeOutcomeLog` row. The row records:

- **that** an ML prediction was used — `WasMlPrediction` is populated, correctly, from
  `TaskDashboardItem.IsMLPrediction`;
- **what actually happened** — `ActualMinutes`, plus all five input features;
- **not what was predicted** — `PredictedMinutes` is written as the literal `null`;
- **not how confident the model was** — `Confidence` is written as the literal `null`.

`SmartStudyPlanner/ViewModels/FocusViewModel.cs:151,154`:

```csharp
ActualMinutes       = phutDaHoc,
PredictedMinutes    = null,
WasMlPrediction     = TaskHienTai.IsMLPrediction,
Confidence          = null,
```

**That asymmetry is the whole defect.** A row saying *"a prediction was made, and the session took 37
minutes"* without saying what was predicted cannot produce an error, and a row without a confidence
cannot be binned. The telemetry can describe outcomes; it cannot evaluate the model that produced
them. The columns exist in the schema (`Data/TelemetrySchema.cs:46`) and in the model
(`Models/Telemetry/StudyTimeOutcomeLog.cs:20,23`) — they were designed for exactly this and are
never filled.

**What is lost, concretely:**

| Question the project already wants answered | Blocked by |
|---|---|
| What is the shipped predictor's error on real sessions? | No `PredictedMinutes` — no residual can be formed |
| Is the M8-C `R² ≥ 0.45` retrain gate meaningful on real data? | Same |
| Does the confidence score track accuracy on real input? (F-1, deferred) | No `Confidence` — no bins |
| Does the `≥ 0.6` ML-vs-formula switch in `StudyTimePredictorService` help or hurt? | Both |

The audit's F-1 finding — the confidence band immediately above the M8-A gate scoring worse than the
band below it — was measured on **authored** data, because authored data is all this project has
(DFD-1). The instrument that would let the same question be asked about **real** usage is this one,
and it is writing nulls.

## 2. Root cause — a data-flow truncation, not a forgotten assignment

The prediction and its confidence **exist**, and are then discarded twice before reaching the write
site. `FocusViewModel` writes `null` because by the time it runs, the values are genuinely gone.

```
StudyTimePredictorService.PredictAsync
  └─ returns StudyTimePredictionResult(int Minutes, bool IsMLPrediction, float Confidence)
        Confidence is computed here: 1 - |predicted - formula| / max(formula, 1)      ← both values live

SchedulingOrchestrator.PredictStudyMinutes(task, monHoc, out bool isMlPrediction) : int
  └─ returns result.Minutes; surfaces result.IsMLPrediction via out-param
        result.Confidence is DROPPED — the signature has nowhere to put it            ← loss #1

DashboardViewModel.BuildDashboardSummary
  └─ builds TaskDashboardItem { IsMLPrediction = isMl,
                                ThoiGianGoiY = $"{predictedMinutes} phút" }
        the number survives only inside a formatted display string;
        TaskDashboardItem has no PredictedMinutes / Confidence field                  ← loss #2

FocusViewModel.LuuThoiGianThucTe
  └─ writes the outcome row from TaskHienTai (a TaskDashboardItem)
        can only write null, because null is all it has                               ← the symptom
```

`[fact]` Read from `Services/ML/IStudyTimePredictor.cs:13`,
`Services/ML/StudyTimePredictorService.cs:36-48`,
`Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs:75-81`,
`ViewModels/DashboardViewModel.cs:191-194`, `Models/TaskDashboardItem.cs:19`,
`ViewModels/FocusViewModel.cs:141-155`.

**Why this matters for sizing.** The ruling calls the fix *"small"*. It is small in behaviour — no
user-visible change, no threshold moved — but it is **not** a one-line edit: the value has to be
carried across three layers, and the carrier at the first hop is an `out`-parameter signature
published on two interfaces.

### 2.1 The null is characterized by a passing test

`SmartStudyPlanner.Tests/ViewModels/FocusViewModelOutcomeLogTests.cs:80-81`, inside
`OutcomeRow_MappingIsCorrect`:

```csharp
Assert.True(row.WasMlPrediction);
Assert.Null(row.PredictedMinutes);
Assert.Null(row.Confidence);
```

`[inferred]` The null was **deliberate and locked in**, not overlooked — someone wrote the outcome-row
mapping, noticed these two fields had no source, and pinned the current behaviour. That is good
practice and it is why the gap survived silently: the suite is green *because* the columns are null.

**Consequence for the fix:** those two assertions are the regression signal. A correct fix turns them
red, and they must be **rewritten to assert the populated values**, never deleted. If a fix leaves
that test green, the fix did not work.

## 3. Why delay is irreversible — the reason this was separated

Rows already written cannot be repaired. The prediction that a completed session was measured against
existed only in memory at the moment the dashboard rendered; nothing persists it, and it is not
recomputable after the fact — the features it consumed (`StudiedMinutesSoFar`, `DaysLeft`) have since
moved, and the model file itself may have been retrained.

So the loss is not *"we have no calibration data yet"*. It is *"every session studied between now and
the fix is permanently unusable for calibration, while looking like a complete row"* — the second
half being the dangerous part, because the table will keep growing and keep looking healthy.

`[scope]` This states why the cost accrues, not how fast. **How many sessions per day are being lost
is unmeasured** — see §6.

## 4. The fix, in slices

Each slice is one shippable commit.

### Slice 1 — carry the confidence out of the orchestrator seam

| | |
|---|---|
| **Files** | `Core/Scheduling/Contracts/ISchedulingOrchestrator.cs`, `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs`, `Services/IDecisionEngine.cs`, `Services/DecisionEngineService.cs` |
| **Change** | Stop discarding `StudyTimePredictionResult.Confidence`. Prefer **returning the result record** over adding a second `out` parameter — two `out`s on a public seam is where this defect came from. |
| **Exit** | The predictor's confidence is observable at the `DecisionEngineService` boundary; build green; the 9 test doubles updated (§5) |

### Slice 2 — carry both values to the write site

| | |
|---|---|
| **Files** | `Models/TaskDashboardItem.cs`, `ViewModels/DashboardViewModel.cs`, `ViewModels/FocusViewModel.cs` |
| **Change** | Add `PredictedMinutes` (numeric — **not** parsed back out of `ThoiGianGoiY`) and `Confidence` to `TaskDashboardItem`; populate at construction; write both in `LuuThoiGianThucTe` |
| **Exit** | `FocusViewModelOutcomeLogTests.OutcomeRow_MappingIsCorrect` **rewritten** to assert the real values, and red before the fix |

**Log the confidence on both branches.** `StudyTimePredictorService` returns the *formula* estimate
with `IsMLPrediction = false` when confidence `< 0.6` — the confidence is still meaningful there, and
the rejected branch is exactly the population needed to tell whether the threshold sits in the right
place. Writing `Confidence` only when `WasMlPrediction` is true would rebuild the same blind spot one
level down.

### Slice 3 — row-level provenance (DFD-5), only if the owner scopes it in

DFD-5 requires row-level lineage on **new** rows: provenance type, label source, generator identity,
dataset version. `StudyTimeOutcomeLog` predates that policy and carries none of it. Adding it *while
the write site is already open* is cheaper than a second pass — but it is a **DFD-5 obligation, not
part of DFD-9a**, and the Data Maturation proposal is where its shape gets decided. **Do not fold it
in silently.** Named here so the sequencing is a choice rather than an oversight.

## 5. Pre-edit checklist

`gitnexus_impact` — `SchedulingOrchestrator.PredictStudyMinutes`, upstream, tests included:

| | |
|---|---|
| **Risk** | **LOW** — 3 impacted symbols, 1 direct caller |
| **d=1** | `DecisionEngineService.PredictStudyMinutes` |
| **d=2 / d=3** | `DashboardViewModel.BuildDashboardSummary` → `LoadDuLieuDashboard` |
| **Processes** | `Page_Loaded` (DashboardPage, earliest broken step 2), `DecisionEngineService.PredictStudyMinutes` |
| **Modules** | `Services` (direct), `ViewModels` (indirect) |

**The number that matters is not in that table.** `PredictStudyMinutes` resolves to **12 symbols**:
2 interface declarations (`IDecisionEngine:26`, `ISchedulingOrchestrator:16`), 1 production
implementation, and **9 test doubles** across `TaskNotesTests`, `SoeBaselineMetrics`,
`PipelineStageTests`, `WorkloadServiceScheduleTests` (×2), `WeightChangeLogLoggingTests`,
`WeightOptimizerViewModelTests`, `WorkloadServiceIdentityTests`. A signature change touches every one.

`[inference]` **Risk classification: LOW production risk, MEDIUM churn.** The production blast radius
is one call chain with no behavioural change; the cost is mechanical test-double updates. Nine
doubles implementing one seam is itself worth noting to whoever picks this up — it is the kind of
count that makes people prefer a hack at the write site over a fix at the source. Take the fan-out.

**Run `gitnexus_detect_changes()` before committing**, per `CLAUDE.md`.

## 6. Verification, and the check that must be able to fail

| Gate | Command / method | Expected |
|---|---|---|
| Build | `rtk dotnet build SmartStudyPlanner.slnx` | 0 errors |
| Suite | `rtk dotnet test --no-build` | **487 baseline**, no regression |
| Discriminating test | `FocusViewModelOutcomeLogTests.OutcomeRow_MappingIsCorrect`, rewritten | **Red on the unfixed tree**, green after. A fix whose test never went red proves nothing |
| Both branches | New case: prediction rejected (`confidence < 0.6`) still writes a non-null `Confidence` | Populated, `WasMlPrediction` false |
| End-to-end | Run the app, complete one focus session, read the row back from `StudyTimeOutcomeLogs` | `PredictedMinutes` and `Confidence` both non-null and plausible |

**The end-to-end check is not optional and not substitutable by unit tests.** The unit tests exercise
`FocusViewModel` with a stub; they cannot show that the *production* dashboard→focus path carries the
value, which is precisely the path that is broken today.

**Measure the loss rate while the tree is open** — count existing `StudyTimeOutcomeLogs` rows and their
date span in a dev database. It converts *"delay is irreversible"* from an argument into a number, and
it is the same instrument that will later show the fix working.

## 7. What the owner has to decide

1. **Implement now, or schedule it?** The ruling's reasoning — the loss is unrecoverable and the fix
   is independent — argues for now. This document does not assume that answer.
2. **Slice 3 in or out?** Fold DFD-5 row-level provenance into the same pass, or leave the write site
   to be opened twice.

## 8. Out of scope — explicitly

- **Any change to a confidence threshold or gate.** Not the `≥ 0.6` ML-vs-formula switch in
  `StudyTimePredictorService`, and not `DefaultMlConfidencePolicy`. The ruling says so in as many
  words. This defect is about **recording** the number, never about **acting** on it.
- **F-1**, the deferred M8-A confidence-gate calibration anomaly (`specs/system_roadmap.md` §A.4).
  This fix is a *prerequisite* for investigating F-1 on real data; it is not that investigation, and
  it does not reopen it. Note also that F-1 lives in a **different** consumer — `DefaultMlConfidencePolicy`
  is used by `IntentClassifierAdapter` and `WeightOptimizerViewModel`, **not** by
  `StudyTimePredictorService` — so the two do not collide.
- **Backfilling historical rows.** Impossible; §3.
- **Using this telemetry for training or evaluation.** DFD-9b designates it a *future* real-data
  source, gated behind provenance, privacy/consent, retention and a data contract. Instrumenting it
  does not unlock it.
- **`DifficultyLabelLogs`.** Real human judgements, small, currently unconsumed — a DFD-9b matter, not
  a defect.
- **The M8-C `R² ≥ 0.45` gate and the ≥ 50-row retrain threshold.** Untouched.
