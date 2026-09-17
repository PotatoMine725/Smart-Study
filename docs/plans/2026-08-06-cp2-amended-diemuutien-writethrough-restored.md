# CP-2 amended: restore the `DiemUuTien` write-through

**Date:** 2026-08-06 · **Status:** Ratified (owner decision, Card F fix round) ·
**Amends:** CP-2 (`4f49153`, 2026-08-05) — "`DiemUuTien` write-through is dropped when T3.3 lands."

**Reads with:** [`2026-08-04-m3.0-allocator-baseline-vs-invariant.md`](2026-08-04-m3.0-allocator-baseline-vs-invariant.md)
§2 bucket ④; [`2026-08-04-epic-3-execution-plan.md`](2026-08-04-epic-3-execution-plan.md) §3.10 CP-2 row.

## What was found

Card F's spec-compliance review (dispatched fresh, instructed to verify rather than trust either
implementer report) traced the call chain from `WorkloadServiceImpl.GenerateScheduleWithIdentity`'s
`_decisionEngine.CalculateRawSuggestedMinutes(task)` call through
`DecisionEngineService → SchedulingOrchestrator → RawMinutesCalculator.Calculate(task)`
(`SmartStudyPlanner/Core/Scheduling/Engines/RawMinutesCalculator.cs:11-13`):

```csharp
if (task.TrangThai == StudyTaskStatus.HoanThanh || task.DiemUuTien <= 0) return 0;
double baseMinutes = (task.DiemUuTien / 100.0) * 120.0;
```

This reads `task.DiemUuTien` **off the model** — not from any parameter, not from the local
`Dictionary<StudyTask, double>` T3.3 (`5197784`) introduced to replace the write-through. Verified
directly (not taken on the reviewer's word): `RawMinutesCalculator.cs:11-13` confirmed by direct read;
the call chain `CalculateRawSuggestedMinutes → CalculateRawSuggestedMinutes → Calculate` confirmed via
grep across `SmartStudyPlanner/Core` and `SmartStudyPlanner/Services`; `StudyTask.DiemUuTien`
(`Models/StudyTask.cs:26`) confirmed to have no initializer, defaulting to `0.0`;
`WorkloadBalancerViewModel`'s constructor (`ViewModels/WorkloadBalancerViewModel.cs:37`) confirmed to
call `GenerateSchedule` directly, with no prior priority-stamping step in that file.

**Reachable consequence:** a task whose `DiemUuTien` has not already been stamped by some other UI path
(Dashboard's full pipeline, or `QuanLyTaskViewModel.TinhDiemVaSapXep` for the currently-viewed subject)
returns `0` from `CalculateRawSuggestedMinutes`, so `minutesNeeded <= 0` at
`WorkloadServiceImpl.cs:160`, and the task is **silently absent from the schedule** — no error, no
chunk, nothing. Reachable in practice: opening Workload Balancer before Dashboard or the owning
subject's task page has run.

## Why CP-2's original premise was incomplete

CP-2's own round-1 verification checked **UI display bindings** (grepped for `.DiemUuTien` in XAML and
found none) and confirmed `PrioritizeStage`/`TinhDiemVaSapXep` independently re-stamp the field
elsewhere — both true, both irrelevant to this failure mode. What was missed: `RawMinutesCalculator`,
called later in the **same method**, has a hard *functional* read-dependency on `task.DiemUuTien`
already being populated on the model — not a display concern, a computational precondition of
`IDecisionEngine`'s existing contract. This coupling pre-dates Epic 3 and is not something Card F
introduced; Card F's removal of the write-through only exposed it, by no longer accidentally
satisfying it.

## Decision

**Restore the write-through.** `GenerateScheduleWithIdentity` goes back to writing
`task.DiemUuTien = <computed priority>` in the loop that populates `tatCaTask`, before the sort and
before any `CalculateRawSuggestedMinutes` call — exactly as at `f7655d1`. The local-dictionary approach
Card F introduced is removed; the sort reads `task.DiemUuTien` again, post-write, as before.

**The pinning test `GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh`** (deleted by Card F,
`5197784`) is **restored** — it pins exactly the behavior now confirmed necessary, not an arbitrary
impurity. Bucket ④'s original classification ("seam decision, not an invariant — decide it at M3.1, in
writing") stands as written; what changes is which way the decision resolved, on new evidence unavailable
at CP-2 time.

**Not decided here, and explicitly out of scope for Card F's fix round:** decoupling
`RawMinutesCalculator` from reading `task.DiemUuTien` directly (e.g., taking priority as a parameter)
was raised as an alternative and rejected for now — it touches `IDecisionEngine`, `SchedulingOrchestrator`,
`RawMinutesCalculator`, and every existing caller (`ProgressGapRiskEvaluator`, dashboard summary
construction, several test suites), a blast radius well beyond a fix round. If pursued later, it is a
separate, standalone task, not folded into Epic 3.

## Why

- **What for:** `Optimize(schedule) → (schedule, report)` — the actual pure seam D-G/D-J care about —
  is still untouched by this; `GenerateSchedule`/`GenerateScheduleWithIdentity` is the *allocator*, a
  different seam (confirmed separately in `2026-08-06-t3.3-scope-narrowed-optimize-seam-deferred.md`),
  and its purity was a nice-to-have precedent, not a load-bearing architectural requirement. A correctness
  regression (silently dropped tasks) is not an acceptable price for that precedent.
- **Experience:** CP-2's verification checked the consumer surfaces that were easy to grep for (XAML
  bindings) and missed a same-call-chain, same-method computational dependency — a reminder that
  "nothing downstream depends on this" claims need to trace *all* call paths reachable from the code
  being changed, not just the obviously UI-facing ones. This is the same lesson as the earlier false
  general-contract findings in Card C/D's reviews: a claim of "nothing relies on X" is a claim to verify
  by tracing, not by grepping the first plausible surface.
