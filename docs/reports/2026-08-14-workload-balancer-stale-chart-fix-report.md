# Report — Workload Balancer stale-chart fix: verification and evidence

**Date:** 2026-08-14
**Branch:** `fix/workload-balancer-stale-chart` (off `dev` at `dd41685`)
**Design:** `docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md`
**Plan:** `docs/plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md`
**Origin:** owner-led manual GUI test, `docs/reports/2026-08-10-epic3-soe-manual-observation.md`

---

## 1. What shipped

Eight commits — two carry-forward, one plan, three fixes, two docs:

| Commit | Scope |
|---|---|
| `d1ab3a3` | carry-forward: the Epic 3 QA-gate discriminating tests (T3.3, T3.7) |
| `0c6426f` | carry-forward: Epic 3 runbook, QA results, stale-chart design, B2 screenshot |
| `6c1d5f6` | the implementation plan |
| `5bd0a6a` | `GetCapacity` clamps to the slider ceiling as well as its floor |
| `c0ff867` | ViewModel: `RenderedCapacityHours` split out from `CapacityHours` |
| `f120df5` | View: bindings, stale badge, four corrected strings |
| `1afd3fa` | runbook: C2 re-run, E6 retarget, C8–C10, provenance link |
| `0667146` | this report |

The defect: one property, `CapacityHours`, served two roles — the value the slider targets and the
yardstick the chart is drawn against. With no `OnCapacityHoursChanged`, dragging the slider never
rebuilt the schedule; it only re-ran the `[TotalMinutes, CapacityHours]` converters. The screen
therefore showed the **old allocation measured against the new ceiling**: internally consistent,
visually plausible, and describing a schedule the algorithm never produced.

---

## 2. Automated evidence

### 2.1 Suite counts

| Point | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| Branch point (working tree, incl. uncommitted carry-forward tests) | 475 | 0 | 1 | 476 |
| After Phase 1 (capacity ceiling) | 478 | 0 | 1 | 479 |
| After Phase 2 (ViewModel split) | 483 | 0 | 1 | 484 |
| After Phase 3 (view + copy) — **final** | **486** | **0** | **1** | **487** |

Build: `dotnet build SmartStudyPlanner.slnx -c Debug` → **0 errors**. Release → **0 errors**.

`dev` at `dd41685` would compute 470, not 475 — the 5-case difference is the two carry-forward test
files, which were uncommitted in the working tree when the baseline was first measured. They were
committed first (`d1ab3a3`) precisely so that the number this plan gates on is the number CI
computes. That figure of 470 is arithmetic, not a measurement; the 475 was measured directly.

Eleven new xUnit cases across nine test methods:

| Where | Methods | Cases |
|---|---|---|
| `SmartStudyPlanner.Tests/Services/WorkloadServiceCapacityTests.cs` | 1 `[Theory]` | 3 |
| `SmartStudyPlanner.Tests/ViewModels/WorkloadBalancerViewModelTests.cs` | 5 `[Fact]` | 5 |
| `SmartStudyPlanner.Tests/Views/WorkloadBalancerPageSourceTests.cs` | 3 `[Fact]` | 3 |

### 2.2 Red-before-green (the tests whose failure came free)

| Test | Red run | Evidence quality |
|---|---|---|
| `GetCapacity_GiaTriVuotTranSlider_BiKepXuongTranToiDa` | 0 passed / **3 failed** — `Expected: 8, Actual: 8.5 / 12 / 99.5` | Behavioural. Production was in its unmutated pre-fix state. |
| all 3 source guards | 0 passed / **3 failed** | Behavioural. Ran against the pre-edit XAML and interface. |
| 5 ViewModel tests | compile errors `CS1729`, `CS1061`, `CS0117` | **Weak** — a compile failure proves nothing about coverage. Hence §2.3. |

### 2.3 Mutation matrix — ViewModel

Each mutation applied to `WorkloadBalancerViewModel.cs`, suite run, then reverted. Observed, not
predicted:

| # | Mutation | Predicted red | **Observed** |
|---|---|---|---|
| M1 | delete `RenderedCapacityHours = CapacityHours;` from `BuildSchedule` | tests 1, 3 | **3 failed / 2 passed** — tests 1, 2, 3 |
| M2 | move that assignment into `partial void OnCapacityHoursChanged` | test 2 | **1 failed / 4 passed** — `DoiCapacityHours_ChuaBamXepLai_BaoStaleVaGiuNguyenRendered` |
| M3 | `OnCapacityHoursChanged` → `BuildSchedule(notify: false)` (the rejected auto-reschedule) | test 4 | **3 failed / 2 passed** — `DoiCapacityHours_KhongTuDongXepLaiVaKhongLuu`, plus tests 1, 2 |
| M4 | remove `[NotifyPropertyChangedFor(nameof(IsScheduleStale))]` from `capacityHours` | test 5 **only** | **1 failed / 4 passed** — `DoiCapacityHours_PhatPropertyChangedChoIsScheduleStale`, and nothing else |
| M5 | `GenerateSchedule(_hocKy, RenderedCapacityHours)` | test 3 | **1 failed / 4 passed** — `GenerateScheduleCommand_DungLaiLichBangMucMoi_VaTatStale` |

No mutation survived. M4 is the one that earns its keep: it confirms empirically that tests 1–4
cannot detect a missing change notification — they read `IsScheduleStale` directly rather than
observing it — so without test 5 the badge could have silently never appeared in the running app
while the suite stayed green.

Post-revert check: no `MUTATION` marker remains in the file, and the suite returned to 483/1/484
before the Phase 2 commit.

### 2.4 Impact analysis (mandated by `CLAUDE.md`)

| Symbol | Risk | Reading |
|---|---|---|
| `GetCapacity` | **CRITICAL** | Genuine. Three production callers, not one: `WorkloadBalancerViewModel:26`, `DashboardViewModel:114` (→ `PipelineUserSettings`), `BalanceWorkloadStage:40` (fallback). Escalated to the owner before editing; owner chose to proceed (§4, D3). |
| `WorkloadBalancerViewModel` | **HIGH** | Noise. 24 of 25 edges are `IMPORTS` (`using SmartStudyPlanner.ViewModels;` in unrelated files); `processes_affected: 0`, `modules_affected: 0`. One real dependency, `WorkloadBalancerPage.xaml.cs:15`, which needs no change because the constructor addition is an optional parameter. Reported, proceeded. |

`detect_changes` before each commit: Phase 1 low risk, `GetCapacity` only, no affected flows.
Phase 2 medium, confined to the `GenerateSchedule → SaveCapacity` flow inside this ViewModel.
Phase 3 medium, `IWorkloadService.GenerateSchedule` doc comment only.

---

## 3. Manual evidence — **NOT RUN**

Three acceptance criteria are visual and no automated test reaches them. **I did not run these** —
they need a human at the GUI. Release build is ready and provenance-checked:

- `SmartStudyPlanner/bin/Release/net10.0-windows10.0.19041.0/SmartStudyPlanner.exe`
- mtime `2026-08-14 08:38:18`, wall clock at build `08:38:19` — this binary *is* the current tree.

Criteria stated in advance so each check can actually fail:

| # | Check | PASS | FAIL | Result |
|---|---|---|---|---|
| M1 | Drag the slider one tick; do **not** press the button | Readout and badge change; **no bar moves, no bar changes colour** | Any bar changes height or colour, or the "ĐÃ ĐẠT MỨC TỐI ĐA" labels appear/vanish | **NOT RUN** |
| M2 | Press **XẾP LỊCH LẠI** | Dialog names the new capacity; badge gone; chart rescales *and* re-allocates | Badge persists, or the dialog names the old value | **NOT RUN** |
| M3 | Read the dashed-line caption in both states | Caption equals the **badge's** number in M1, the **slider's** in M2 | Caption tracks the slider during M1 | **NOT RUN** |
| M4 | Read the page header and information note 02 | Neither says "đều khắp" or "ít tải nhất" | Either still describes the old rule | **NOT RUN** |
| M5a | Close app, set `capacity.txt` to `12`, relaunch, open the page | Readout `8.0`, **no badge** on an untouched page | Badge visible | **NOT RUN** |
| M5b | Same, but `capacity.txt` = `4.5` | Readout `4.5`, **no badge** | Readout `5.0` and a badge appears | **NOT RUN** |

M4 is additionally covered by an automated guard, so it is the one manual check that would be
redundant to run. M1, M2, M3 and M5 are not covered by anything automated and are the real gate.

**M5b probes a second false-positive path that criterion 5 does not currently cover, and that no
automated test in this change can reach.** `4.5` is an in-range, fully supported `capacity.txt`
value — `GetCapacity_SoInvariant_DocDungTrenMoiCulture` asserts it round-trips and
`SaveCapacity_LuonGhiDauCham` writes it — so the new ceiling clamp passes it through untouched. It
then reaches a `Slider` with `TickFrequency="1" IsSnapToTickEnabled="True"` through a TwoWay
binding. *If* WPF snaps it to `5.0` and writes back, `CapacityHours` (5.0) diverges from
`RenderedCapacityHours` (4.5) and the badge appears on a page nobody touched — the exact failure
mode the ceiling clamp exists to prevent, arriving by a different door.

Reading the WPF source, snapping lives in `Slider.UpdateValue` on the user-interaction path, not in
the `RangeBase.Value` coercion callback that a binding write goes through — which would mean no
snap and no badge. **That is reasoning, not evidence, and it is not the basis for any claim here.**
M5b is what settles it. Until it runs, acceptance criterion 5 ("opening the page never shows the
badge, whatever `capacity.txt` contains") is **unproven for non-integer values**, and only proven
for the above-ceiling case by M5a.

If M5b fails, the fix is a further decision — snap `GetCapacity` to whole hours, or drop
`IsSnapToTickEnabled` — and should be taken before this PR merges, not after.

These correspond to runbook scenarios **C8, C9, C10** plus C7's copy check. Runbook **C2 is
withdrawn and needs re-running** — its 2026-08-10 "Met" was read through the very instrument this
change repairs.

---

## 4. Decisions made

**D1 — Freeze the chart to its rendered capacity and add a stale badge; do not auto-reschedule.**
*Why:* three options were weighed. (A) rebuild on slider change gives the best UX but puts
`SaveCapacity` (disk) and the CP-2 `DiemUuTien` write-through (database) behind a drag gesture,
firing on a path with no test coverage, immediately after Epic 3 closed on the strength of that
exact seam. (B) freeze only removes the lie but leaves an unexplained dead zone. (C) is B plus one
binding and one `TextBlock`.
*What for:* it converts a confusing mismatch into a legible instruction.
*Experience:* the failure was a single property serving two roles; the fix is to name the second
role, not to add machinery. Mutation M3 now pins the rejection of (A), so a future "helpful" change
cannot quietly reintroduce it.

**D2 — Carry the two uncommitted test files forward as their own commit rather than stashing them.**
*Why:* the `475` baseline every gate in the plan is stated against **included** them, but they were
uncommitted, and CI sees only branch HEAD. Left alone, the plan's numbers would have been wrong for
CI and Phase 5 would have failed on something unrelated to this change.
*What for:* the number gated on is the number CI computes.
*Experience:* a baseline measured on a dirty tree is not a baseline. Check `git status` before
quoting a test count, not after.

**D3 — Apply the capacity ceiling inside `GetCapacity`, accepting that it reaches the pipeline.**
*Why:* impact came back CRITICAL and revealed the design's assumption of a single call site was
wrong — `DashboardViewModel` and `BalanceWorkloadStage` also read it. Escalated rather than
absorbed. Two alternatives existed: clamp only in the ViewModel, or drop the ceiling entirely.
*What for:* the floor clamp already applies to all three callers; making the ceiling narrower would
put the slider's bounds in two layers and let two screens report different numbers from the same
file. The delta is confined to hand-edited or migrated `capacity.txt` values above 8.
*Experience:* the floor's own doc comment made the right argument and applied it to one end only.
When a guard exists because an input is untrusted, the argument rarely stops at one bound — and a
CRITICAL risk score is worth reading rather than obeying, because it measured fan-out, not meaning.

**D4 — Strengthen the binding guard from absence-only to a counted assertion.**
*Why:* the design specified `Assert.DoesNotContain("DataContext.CapacityHours", …)`. That passes if
someone *deletes* the five bindings outright — the same vacuity pattern mutation probe M5 exposed in
the automated QA gate, in a change whose whole premise is that a passing check is not evidence.
*What for:* acceptance criterion 3 ("the five converter bindings … reference
`RenderedCapacityHours`") is now actually asserted, and the count also covers the dashed-line
caption that design §5.2 had written off as un-guardable.
*Experience:* a negative assertion tests that something is absent, not that the right thing is
present. When the criterion is positive, so must the assertion be.

**D5 — Add two copy guards beyond the design's test list.**
*Why:* acceptance criterion 4 (no user-facing text or doc comment describing the replaced rule) had
no automated backing at all, and stale copy is precisely the class of defect that recurs silently
after a behaviour change — this fix exists partly because it already did.
*What for:* the criterion is enforced rather than remembered. Precedent already existed
(`ObjectiveEvaluatorTests.SourceFiles_ContainNoHanChotOrDeadlineToken`).
*Experience:* writing the guard found the fourth stale string. `IWorkloadService.cs:7` makes the
same false "7 ngày" claim as `:18`; the design had named three strings and there were four. A guard
written before the edit is also a search.

**D6 — Return runbook C2 to unverified rather than accept its recorded pass.**
*Why:* C2's evidence was gathered through the view this change proves was misleading, and a stale
reading *systematically* looks like a C1-shape violation — the failure mode is biased, not random.
*What for:* the standard applied to the automated gate — a pass is not evidence unless the check
could have failed correctly — applies equally to manual results.
*Experience:* when a measuring instrument is found faulty, prior readings taken with it are
withdrawn, not defended.

**D7 — Fix runbook E6 rather than file a defect against the app.**
*Why:* E6 asked the tester to delete a semester. No `XoaHocKy`, `DeleteHocKy`, or `HocKys.Remove`
exists anywhere in production code — the scenario was written against a capability that was never
built.
*What for:* retargeted at subject deletion, the reachable path to the EF cascade-fixup regression
E6 was always meant to guard. The missing semester UI is recorded as a "Known gaps" proposal.
*Experience:* a scenario that cannot be executed is a documentation defect and gets corrected at its
source. Not every finding from a QA pass is a finding about the code.

**D8 — Link the owner's observation record from the runbook rather than transcribing it.**
*Why:* it is the owner's primary evidence, in the owner's words.
*What for:* copying it into a document authored by someone else muddies provenance instead of
clarifying it. The link only works if the target is tracked, which is why the untracked Epic 3 docs
were committed in Phase 0.
*Experience:* a cross-reference to an untracked file is a broken reference with extra steps.

---

## 5. Open items

1. **Manual checks M1, M2, M3, M5a, M5b are unrun.** They are the only evidence for acceptance
   criteria 1, 2 and 5. Release binary is built and provenance-checked. **M5b is the one that could
   change the code**: if a non-integer `capacity.txt` snaps on the slider and raises a false badge,
   a further clamp decision is needed before merge (§3).
2. **Runbook C2 needs re-running** with the corrected procedure.
3. **No semester-management UI** — proposal, out of scope, see design §7.
4. Pre-existing package advisories surfaced during the build (`NU1903` SQLitePCLRaw,
   `NU1904` System.Drawing.Common). Untouched by this change, noted only so they are not mistaken
   for new.
