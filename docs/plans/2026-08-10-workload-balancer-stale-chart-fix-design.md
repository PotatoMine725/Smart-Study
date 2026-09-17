# Design — Workload Balancer: stale-chart fix and algorithm copy correction

**Date:** 2026-08-10
**Status:** implemented — see `docs/plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md`
**Origin:** owner-led manual GUI test, `docs/reports/2026-08-10-epic3-soe-manual-observation.md`
**Related:** `docs/reports/2026-08-10-epic3-automated-qa-gate.md` (finding QA-1, accepted debt P2),
`docs/plans/2026-08-10-epic-3-manual-qa-runbook.md` (scenarios C1, C2, C7, E6)

---

## 1. Context

The Epic 3 automated QA gate passed and handed off to owner-led manual testing. The manual pass
returned three actionable findings. This document designs the response to all three.

Nothing here changes the scheduling engine. Epic 3's ratified decisions — T3.3 earliest-feasible
placement, the CP-2 `DiemUuTien` write-through, the D7 ruling on past-deadline placement — are
untouched.

---

## 2. Problem

### 2.1 The reported symptom (runbook C1)

> "when moving the slider the chart re-render once showing one type of schedule, however, when
> click 'Xếp lịch lại' button, the chart re-render once more but with different result."

### 2.2 Actual root cause

Not non-determinism. `WorkloadBalancerViewModel` has no `OnCapacityHoursChanged` handler, so moving
the slider never recomputes the schedule. What re-renders is the *chart*, because every bar is a
`MultiBinding` over `[TotalMinutes, CapacityHours]` (`Views/WorkloadBalancerPage.xaml:115-124`).
Changing capacity re-runs `LoadToLengthConverter` and `LoadToBrushConverter` against unchanged
allocation data.

The result on screen after a slider drag is **the old day-allocation measured against the new
capacity line** — a chart that is internally consistent, visually plausible, and describes a
schedule the algorithm never produced. Pressing **XẾP LỊCH LẠI** is the first moment
`GenerateSchedule` runs with the new capacity.

The underlying flaw is that one property, `CapacityHours`, serves two distinct roles: the value the
slider targets, and the yardstick the chart is drawn against. Only the first is meant to update on
a drag.

### 2.3 Provenance and severity

**Pre-existing, not an Epic 3 regression.** `WorkloadBalancerViewModel.cs` and
`WorkloadBalancerPage.xaml` were not modified by Epic 3; the only behaviour-changing production
files were `Services/WorkloadServiceImpl.cs` and `Data/AppStartup.cs`.

**But T3.3 made it severe.** Under the old least-loaded rule, days sat at similar mid-heights and
rescaling them against a new ceiling looked unremarkable. Under earliest-feasible packing every
used day but the last sits *at* the ceiling, so a capacity increase drops every bar to a fraction
of its height and clears the "ĐÃ ĐẠT MỨC TỐI ĐA" badges at once. The lie is now loud.

### 2.4 Two consequences

**Runbook C2's verdict does not hold.** "At every setting, the C1 shape still holds" can only be
read off a chart rebuilt at that setting. If the slider was moved and the chart observed without
pressing the button, the reading was of a stale allocation — and a stale allocation will
*systematically* appear to violate the C1 shape. C2 is returned to unverified pending re-run.

**The same root cause reaches E5.** `SaveCapacity` is called only inside `BuildSchedule`, so a
slider move the user never confirms is not persisted either.

### 2.5 Stale algorithm copy (runbook C7)

Three strings still describe the pre-T3.3 rule — two user-facing, one developer-facing:

| Location | Current text | Problem |
|---|---|---|
| `WorkloadBalancerPage.xaml:39` | "Thuật toán rải các bài tập chưa hoàn thành **đều khắp** những ngày tới…" | claims even distribution |
| `WorkloadBalancerPage.xaml:275` | "Mỗi bài luôn được dồn vào ngày **ít tải nhất** còn dưới mức trần…" | names the exact rule T3.3 replaced |
| `Services/IWorkloadService.cs:18` | "Chạy thuật toán **Greedy Least-Load**, trả về lịch **7 ngày**." | names the replaced rule *and* a fixed horizon |

`:275` is the "information note at the bottom of the page" identified in the owner's C7 ruling, and
is the most precisely wrong of the three.

The third was found during design review, not during the GUI test — it is a doc comment, so no
tester could have seen it. It is doubly stale: the algorithm is no longer least-load, and the
schedule is not capped at seven days (confirmed by runbook D5, which the owner recorded as met).
It sits on the interface that *defines* the scheduling contract, which is where a developer looks
first.

### 2.6 Runbook defect (E6)

Scenario E6 asks the tester to delete a semester. No such capability exists: there is no
`XoaHocKy`, `DeleteHocKy`, or `HocKys.Remove` anywhere in production code. The scenario was written
against a capability that was never built. This is a defect in the runbook, not in the app.

The regression E6 was *meant* to guard — the EF cascade-fixup defect, where reparenting tasks away
from a soon-to-be-deleted parent needs the FK reassignment and `DetectChanges()` to happen before
`Remove()` — is reachable through **subject** deletion, not semester deletion:
`ViewModels/QuanLyMonHocViewModel.cs:89` (`XoaMon`) into
`Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs:163`. E6 should target
that path.

---

## 3. Non-goals

- No change to scheduling behaviour. `WorkloadServiceImpl` is touched in exactly one place —
  `GetCapacity` input validation (§4.6) — which cannot alter output for any in-range value.
- No semester-management UI. The absence of one is a real product gap, recorded as a deferred
  proposal in §7, not addressed here.
- No auto-reschedule on slider drag (considered and rejected — see §8, D1).
- No change to what the user sees on a successful reschedule; the confirmation dialog stays.

---

## 4. Design

### 4.1 ViewModel — separate the two roles

`ViewModels/WorkloadBalancerViewModel.cs`:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsScheduleStale))]
private double capacityHours;          // what the slider targets

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsScheduleStale))]
private double renderedCapacityHours;  // what Schedule was actually built with

public bool IsScheduleStale => Math.Abs(CapacityHours - RenderedCapacityHours) > 0.01;
```

`BuildSchedule` assigns `RenderedCapacityHours = CapacityHours` after `GenerateSchedule` returns.
The invariant is then: **the chart is truthful exactly when `IsScheduleStale` is false.**

The `0.01` tolerance is a float-equality guard, not a threshold. The slider snaps to integer ticks
(`IsSnapToTickEnabled="True"`, `TickFrequency="1"`), so the smallest real difference is 1.0.

### 4.2 ViewModel — notifier seam

`BuildSchedule(notify: true)` calls `System.Windows.MessageBox.Show` directly, which blocks
headless test runs. This is the P2 accepted debt from the automated gate, and it sits directly on
the transition this change most needs to cover.

```csharp
private readonly Action<string> _notify;

public WorkloadBalancerViewModel(
    HocKy hocKy,
    IWorkloadService workloadService,
    Action<string>? notify = null)
{
    _notify = notify ?? (m => System.Windows.MessageBox.Show(m, "Workload Balancer"));
    ...
}
```

Optional parameter, defaulting to present behaviour. **No UX change** — users see the same dialog.
The existing DI constructor chains through unchanged.

### 4.3 View — binding corrections

`Views/WorkloadBalancerPage.xaml`. Five converter bindings move from `DataContext.CapacityHours` to
`DataContext.RenderedCapacityHours`:

| Line | Element |
|---|---|
| `:117` | chart bar height (`LoadToLengthConverter`) |
| `:123` | chart bar colour (`LoadToBrushConverter`) |
| `:182` | detail-card meter width |
| `:188` | detail-card meter colour |
| `:198` | "ĐÃ ĐẠT MỨC TỐI ĐA" visibility (`FullDayToVisibilityConverter`) |

The dashed-line caption at `:150` also moves to `RenderedCapacityHours` — it names the line the
bars are measured against, so it must name the same number.

**Deliberately left on live `CapacityHours`:** the slider itself (`:68`) and the 38pt readout
(`:56`). These are the slider's target value and must track the drag immediately. That divergence
is the feature, not a bug — and it is what the badge exists to explain.

### 4.4 View — stale badge

Placed **above the chart**, inside the balance-chart `WbPanel` border, between the section-header
row (`:89-93`) and the chart `Grid` (`:95`) — adjacent to the thing it is warning about.

Visibility bound to `IsScheduleStale` through WPF's built-in `BooleanToVisibilityConverter`
(declared in `Page.Resources`; no new converter class).

> ⚠ Lịch đang theo mức **{RenderedCapacityHours:0.0}** giờ/ngày — bấm **Xếp lịch lại** để áp dụng mức mới.

### 4.5 Copy corrections

`:39` — header stops making any claim about how work is distributed:

```diff
- Thuật toán rải các bài tập chưa hoàn thành đều khắp những ngày tới — theo điểm ưu tiên và sức học mỗi ngày của bạn.
+ Tự động xếp các bài tập chưa hoàn thành vào những ngày tới — theo điểm ưu tiên và sức học mỗi ngày của bạn.
```

`:275` — the bottom note becomes the single place that explains the mechanism, in plain language:

```diff
- Mỗi bài luôn được dồn vào ngày ít tải nhất còn dưới mức trần — nhờ vậy tải các ngày luôn cân đối, không dồn cục.
+ Mỗi bài được xếp vào ngày sớm nhất còn chỗ trống — học xong sớm, không để dồn về sát hạn.
```

One source of truth for the user-facing rule, so the two strings cannot drift apart again.

`Services/IWorkloadService.cs:18` — the interface contract, developer-facing:

```diff
- /// <summary>Chạy thuật toán Greedy Least-Load, trả về lịch 7 ngày.</summary>
+ /// <summary>Xếp lịch theo ngày sớm nhất còn chỗ (T3.3), trả về các ngày có bài.</summary>
```

Both claims in the original are false: the rule is no longer least-load, and the horizon is not
fixed at seven days.

### 4.6 Capacity range — a latent bug the badge would expose

`WorkloadServiceImpl.GetCapacity()` ends at `return Math.Max(val, MinCapacityHours)`
(`WorkloadServiceImpl.cs:73`) — a floor, with **no ceiling**. Its own doc comment explains the floor
exists precisely because the file is the one untrusted entry point and
"`WorkloadBalancerViewModel` đọc `GetCapacity()` rồi gọi thẳng `GenerateSchedule` ngay trong
constructor, trước khi slider kịp kẹp giá trị." The symmetric argument for a ceiling was missed.

Today that is invisible. With the badge it stops being invisible:

1. `capacity.txt` holds a value above the slider maximum — say `12`.
2. Constructor sets `CapacityHours = 12.0`, builds the schedule, sets `RenderedCapacityHours = 12.0`.
3. The Slider template applies `Maximum="8"`; WPF coerces `Value` to 8 and the TwoWay binding
   writes back, so `CapacityHours` becomes 8.0.
4. `IsScheduleStale` is now true, and the badge appears on a page the user has not touched.

`SaveCapacity` only ever writes slider-bounded values, so this needs an externally edited or
migrated file to trigger. It is nonetheless the exact failure mode the floor clamp was written to
prevent, in the other direction.

**Fix:** add `MaxCapacityHours = 8.0` and clamp both ends, mirroring the existing rationale — the
slider bounds are what the UI admits, so they are what the untrusted file is normalised to.

```diff
- return Math.Max(val, MinCapacityHours);
+ return Math.Clamp(val, MinCapacityHours, MaxCapacityHours);
```

This touches `WorkloadServiceImpl.cs`, which Epic 3 modified. It is included because without it the
badge misfires, so it is a necessary correctness fix for this change rather than opportunistic
cleanup. It cannot affect scheduling output for any in-range value.

---

## 5. Testing

New file `SmartStudyPlanner.Tests/ViewModels/WorkloadBalancerViewModelTests.cs`, with a recording
fake `IWorkloadService` (per the project's test-doubles convention: mirror production namespace,
inline the stub if used only here).

| # | Test | Pins |
|---|---|---|
| 1 | Construction leaves `Rendered == Capacity`, `IsScheduleStale` false | baseline truthfulness |
| 2 | Setting `CapacityHours` leaves `Rendered` unchanged, `IsScheduleStale` true | the defect itself |
| 3 | `GenerateScheduleCommand` updates `Rendered`, clears stale, and calls the service **with the new capacity** | the fix |
| 4 | A slider move alone calls neither `GenerateSchedule` nor `SaveCapacity` | that we built §4, not auto-reschedule |
| 5 | Setting `CapacityHours` raises `PropertyChanged` for `IsScheduleStale` | that the badge can ever appear |

One further test belongs on the service, not the ViewModel, alongside the existing `GetCapacity`
coverage: **a `capacity.txt` holding a value above the slider maximum returns `8.0`** (§4.6). The
ViewModel cannot cover this — it reads whatever `GetCapacity` hands it, so a fake would simply
assert the fake.

Test 4 is what keeps a future "helpful" change from silently converting this into the rejected
auto-reschedule design.

Test 5 exists because `IsScheduleStale` is a plain computed property, not `[ObservableProperty]`.
Its change notification comes from `[NotifyPropertyChangedFor]` on the two source fields. If that
attribute were dropped, the badge would never appear in the running app — and tests 1-3 would all
still pass, because they read the property directly rather than observing the notification.

### 5.1 XAML source guard

The defect lived in *bindings*, and no ViewModel unit test can prove the XAML points at the right
property. Following the precedent already in this repo
(`SoeT34…SourceFiles_ContainNoHanChotOrDeadlineToken`), add a source-assertion test:

> `Views/WorkloadBalancerPage.xaml` contains no occurrence of `DataContext.CapacityHours`.

After §4.3 every ancestor-scoped reference is `RenderedCapacityHours`, and the two intentionally
live bindings are plain `{Binding CapacityHours}` with no `DataContext.` prefix. The assertion is
therefore exact, and goes red if any converter binding is re-pointed at the live value.

### 5.2 Stated limitation

The guard in §5.1 covers the five converter bindings. It **cannot** cover the `:150` caption, whose
binding form (`Path="CapacityHours"`) is indistinguishable from the two that must stay. If that
caption regresses it produces a cosmetic mismatch only, and it is covered by manual test alone.

Every guard above is to be mutation-tested — production mutated to confirm the test goes red —
before any of it is reported as evidence. A passing test that was never shown to fail is not
coverage.

---

## 6. Documentation and QA follow-up

- **Runbook C2:** add an explicit instruction to press **XẾP LỊCH LẠI** after every slider change,
  and mark the existing C2 result as requiring re-run. Its recorded "Met" was read off a
  potentially stale chart.
- **Runbook E6:** correct the defect in §2.6 — retarget the scenario from semester deletion to
  **subject** deletion (`XoaMon` on a subject that has tasks), which is the reachable path to the
  cascade regression E6 was meant to guard, and note the missing semester capability.
- **Runbook, new scenarios:** badge appears on slider move; badge clears on reschedule; chart bars
  do not move until the button is pressed.
- **Owner's observation record:** link
  `docs/reports/2026-08-10-epic3-soe-manual-observation.md` from the runbook's §4 table rather than
  transcribing it. It is the owner's primary evidence; copying it into a document authored by
  someone else muddies provenance instead of clarifying it.

---

## 7. Deferred — no semester-management UI

There is no UI to rename, delete, or otherwise manage semesters; only creation exists. Recorded as
a **proposal for improvement**, out of scope here. It is what made E6 untestable, and it should be
scoped as its own piece of work rather than absorbed into a bug-fix package.

---

## 8. Decisions made

**D1 — Freeze the chart to its rendered capacity, plus a stale badge; do not auto-reschedule.**
*Why:* three options were weighed — (A) rebuild on slider change, (B) freeze the chart only,
(C) freeze plus an explicit badge. A gives the best UX but puts `SaveCapacity` (disk) and the CP-2
`DiemUuTien` write-through (database) behind a drag gesture, firing on a path with no test
coverage, immediately after Epic 3 closed on the strength of that exact seam. B removes the lie but
leaves an unexplained dead zone where slider and chart disagree silently.
*What for:* C is B plus one binding and one TextBlock, and it converts a confusing mismatch into a
legible instruction. *Experience:* the failure here was a single property serving two roles; the
fix is to name the second role rather than to add machinery.

**D2 — Header goes claim-neutral; the bottom note carries the explanation.**
*Why:* the owner's C7 ruling was to keep the algorithm and fix the explanatory note. Two strings
described the rule, and keeping both accurate means keeping both in sync forever.
*What for:* one source of truth for how scheduling works. *Experience:* stale copy is a
recurring cost of behaviour changes; concentrating the claim in one place lowers it.

**D3 — Inject a notifier seam rather than remove the dialog.**
*Why:* the untestable `MessageBox.Show` sits exactly on the badge-clearing transition. Removing the
dialog would also have worked and would be more testable still, but it changes what users see.
*What for:* full ViewModel testability at zero UX cost, closing P2 debt. *Experience:* a default
parameter buys a test seam without a policy argument about modals.

**D4 — Return C2 to unverified rather than accept its recorded pass.**
*Why:* C2's evidence was gathered through the very view this document proves was misleading.
*What for:* the standard applied to the automated gate — pass is not evidence unless the check
could have failed correctly — applies equally to manual results. *Experience:* when a measuring
instrument is found faulty, prior readings taken with it are withdrawn, not defended.

**D5 — Fix the runbook's E6 rather than file a defect against the app.**
*Why:* the scenario tested a capability that was never built; the app is not at fault.
*What for:* keeps the runbook trustworthy. *Experience:* a scenario that cannot be executed is a
documentation defect and should be corrected at its source.

**D6 — Clamp the capacity ceiling in `GetCapacity`, inside this change rather than deferred.**
*Why:* the missing ceiling (§4.6) is latent today but makes the new badge misfire on page open, so
it stops being cosmetic the moment §4.4 ships. Deferring it would mean shipping a badge with a
known false-positive path. *What for:* the badge means exactly one thing — the chart is stale —
and a warning that also fires for unrelated reasons stops being read. *Experience:* the floor clamp
was written with the right reasoning and applied to one end only; when a guard exists because an
input is untrusted, the argument rarely stops at one bound.

---

## 9. Execution

Single-agent, sequential. No parallel dispatch: the ViewModel, XAML, and test changes are a single
tightly-coupled edit (the tests assert against the same properties the XAML binds to), and the
total diff is small. Splitting it across agents would cost more in coordination than it saves.

---

## 10. Acceptance criteria

1. Moving the slider changes the readout and the badge, and moves no chart bar.
2. Pressing **XẾP LỊCH LẠI** rebuilds the schedule, clears the badge, and rescales the chart.
3. The five converter bindings and the `:150` caption reference `RenderedCapacityHours`.
4. All three strings updated; no user-facing text describes least-loaded or even distribution, and
   no doc comment claims a least-load rule or a fixed seven-day horizon.
5. Opening the page never shows the badge, whatever `capacity.txt` contains.
6. Seven new automated tests pass — five ViewModel, one XAML source guard, one service-level
   capacity clamp — each shown to fail against a mutated production file.
7. Full suite green (baseline 475 passed / 0 failed / 1 skipped / 476 total), build 0 errors.
8. Runbook corrected per §6, with C2 marked for re-run.
