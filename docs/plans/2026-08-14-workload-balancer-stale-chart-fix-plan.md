# Workload Balancer — Stale-Chart Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan phase-by-phase. Steps use checkbox (`- [ ]`) syntax for tracking. **Single-agent, sequential — no subagent dispatch.** The ViewModel, XAML, and test changes are one tightly-coupled edit; splitting them across agents costs more in coordination than it saves.

**Goal:** Stop the Workload Balancer chart from redrawing the *old* day-allocation against a *new* capacity line when the slider moves, and correct four strings that still describe the pre-T3.3 least-load rule.

**Architecture:** Split the single `CapacityHours` property into two roles — `CapacityHours` (what the slider targets) and `RenderedCapacityHours` (what `Schedule` was actually built with). All chart/meter converter bindings move to `RenderedCapacityHours`; the slider and its 38pt readout stay live. A computed `IsScheduleStale` drives a warning badge above the chart that explains the divergence. A `GetCapacity` ceiling clamp stops the badge misfiring on page open.

**Tech Stack:** .NET 10 (`net10.0-windows10.0.19041.0`), WPF, CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]`), xUnit 2.9.3.

**Source spec:** `docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` (approved). This plan implements §4.1–§4.6, §5, §6 of that document.

---

## Context

Owner-led manual GUI testing of Epic 3 (`docs/reports/2026-08-10-epic3-soe-manual-observation.md`) reported that the chart shows one schedule after a slider drag and a *different* one after pressing **XẾP LỊCH LẠI**. Root cause is not non-determinism: `WorkloadBalancerViewModel` has no `OnCapacityHoursChanged` handler, so a drag never recomputes the schedule — it only re-runs the converters, which are `MultiBinding`s over `[TotalMinutes, CapacityHours]`. What the user sees after a drag is the old allocation measured against the new ceiling: internally consistent, visually plausible, and describing a schedule the algorithm never produced.

The defect is pre-existing (neither file was touched by Epic 3) but T3.3's earliest-feasible packing made it loud: every used day but the last now sits *at* the ceiling, so raising capacity drops every bar at once and clears all the "ĐÃ ĐẠT MỨC TỐI ĐA" badges.

Two consequences already recorded in the spec: runbook C2's "Met" is withdrawn (its evidence was read off this faulty instrument), and E5 is affected because `SaveCapacity` runs only inside `BuildSchedule`.

**Intended outcome:** the chart is truthful exactly when `IsScheduleStale` is false, and when it isn't, the page says so in words instead of lying quietly.

---

## Deviations from the approved design (read before starting)

Three, all additive, all flagged here rather than buried in a task:

1. **A fourth stale string.** The design lists three (`WorkloadBalancerPage.xaml:39`, `:275`, `IWorkloadService.cs:18`). `IWorkloadService.cs:7` also says *"tạo lịch học **7 ngày** và quản lý capacity"* — same false horizon claim, on the interface summary. Fixed in Phase 3; without it the Phase 3 copy guard fails.
2. **Stronger and more numerous source guards.** The design specifies one XAML binding guard, stated as absence-only (`no "DataContext.CapacityHours"`). Absence alone passes if someone *deletes* the five bindings, so Phase 3 adds positive count assertions in the same method — which incidentally closes the limitation design §5.2 recorded as un-guardable (the `:150` caption). Acceptance criterion 4 (no stale algorithm copy anywhere) also had no automated backing, so Phase 3 adds two token guards following the `ObjectiveEvaluatorTests.SourceFiles_ContainNoHanChotOrDeadlineToken` precedent. Test count goes from the design's "seven" to **nine test methods / eleven xUnit cases**.
3. **Stale XAML header comment.** `WorkloadBalancerPage.xaml:9-14` asserts *"Không sửa ViewModel/Model"* and lists the bindings. Both become false. Updated in Phase 3.

Notifier seam shape follows the design (`Action<string>? notify = null` constructor parameter), **not** the `OnThongBao` property pattern used by `QuanLyMonHocViewModel` — owner-confirmed 2026-08-14.

---

## File structure

| File | Change | Responsibility after change |
|---|---|---|
| `SmartStudyPlanner/Services/WorkloadServiceImpl.cs` | Modify (~3 lines, `:31`–`:73`) | Adds `MaxCapacityHours`; `GetCapacity` clamps both ends. No scheduling behaviour change. |
| `SmartStudyPlanner/Services/IWorkloadService.cs` | Modify (`:7`, `:18`) | Doc comments describe the T3.3 contract truthfully. |
| `SmartStudyPlanner/ViewModels/WorkloadBalancerViewModel.cs` | Modify | Owns the two capacity roles, `IsScheduleStale`, and the notifier seam. |
| `SmartStudyPlanner/Views/WorkloadBalancerPage.xaml` | Modify (7 bindings, 1 badge, 2 strings, 1 comment) | Chart measured against `RenderedCapacityHours`; badge explains divergence. |
| `SmartStudyPlanner.Tests/Services/WorkloadServiceCapacityTests.cs` | Modify (append 1 `[Theory]`) | All `capacity.txt` tests live here — the class doc forbids opening a second class (shared file, xUnit parallelises across classes). |
| `SmartStudyPlanner.Tests/ViewModels/WorkloadBalancerViewModelTests.cs` | **Create** | The five behavioural pins on the two-role split. |
| `SmartStudyPlanner.Tests/Views/WorkloadBalancerPageSourceTests.cs` | **Create** (new dir) | Source-text guards no ViewModel test can express. Mirrors `SmartStudyPlanner.Views` per repo test convention. |
| `docs/plans/2026-08-10-epic-3-manual-qa-runbook.md` | Modify | C2 re-run, E6 retarget, new badge scenarios. |
| `docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` | Modify (status line) | Marks the design as implemented. |

---

## Tools and skills

**Skills:** `superpowers:executing-plans` (drives this plan). Project skills if a step stalls: `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md`, `.claude/skills/gitnexus/gitnexus-cli/SKILL.md`.

**Tools:** `Read`, `Edit`, `Write`, `Grep` for the edits; `Bash`/`PowerShell` for `rtk dotnet build|test` and `rtk git`; `mcp__gitnexus__impact` before each production-symbol edit and `mcp__gitnexus__detect_changes` before each commit (mandated by `CLAUDE.md`). No `Agent` dispatch.

**Every shell command is `rtk`-prefixed**, including inside `&&` chains (project rule).

---

## Phase 0 — Branch, carry-forward, baseline

**The working tree is dirty with real Epic 3 work that was never committed, and the `475` baseline depends on it.** Resolve that before measuring anything, or CI — which sees only branch HEAD — will compute different numbers than every gate in this plan.

Two uncommitted test files add **5 passing cases** that the `475` baseline already counts:

| File | Added | Cases |
|---|---|---|
| `SmartStudyPlanner.Tests/Data/AppStartupFileBasedTests.cs` | `EnsureDatabaseReady_OnPreT37Db_TaoLaiBangOptimizerRunLogs` + `TableCount` helper | 1 |
| `SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs` | `GenerateSchedule_TaskChuaTungDuocChamDiem_VanDuocXepLich`, `GenerateSchedule_DonVeNgaySomNhat_NgayDungLaTienToLienTuc_ChiNgayCuoiConCho` (`[Theory]` ×3), `Sut(StubDecisionEngine)` widened to `Sut(IDecisionEngine)` | 4 |

Without them HEAD is `470`, not `475`.

**Recommendation: carry them forward as their own commit.** They are the Epic 3 automated-QA-gate discriminating tests, they are green, and one of them covers the very service this change touches. Committing them separately keeps concerns split (repo convention) and makes CI's number match this plan's. *If the owner would rather they stay out of this PR, stash them instead and subtract 5 from every expected count below.*

Similarly, six `docs/` files this PR references are untracked. The provenance argument in design §6 — *link the owner's observation record rather than transcribe it* — only works if the link resolves in the repo.

- [ ] **Step 1: Branch off `dev`**

`dev` is PR-only since 2026-08-09; all work happens on a topic branch.

```bash
rtk git status --short
rtk git checkout -b fix/workload-balancer-stale-chart
```

Expected: branch created from `dev` at `dd41685`.

- [ ] **Step 2: Commit the carry-forward tests**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
rtk git add SmartStudyPlanner.Tests/Data/AppStartupFileBasedTests.cs SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs
rtk git commit -m "test(epic3): commit the QA-gate discriminating tests for T3.3 and T3.7"
```

Expected before the commit: `Failed: 0, Passed: 475, Skipped: 1, Total: 476`. **If this does not match, stop and reconcile — every later expectation is stated relative to it.**

- [ ] **Step 3: Commit the Epic 3 docs this PR references**

```bash
rtk git add docs/plans/2026-08-10-epic-3-manual-qa-runbook.md \
            docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md \
            docs/reports/2026-08-10-epic3-automated-qa-gate.md \
            docs/reports/2026-08-10-epic3-qa-session-report.md \
            docs/reports/2026-08-10-epic3-soe-manual-observation.md
rtk git commit -m "docs: commit the Epic 3 manual-QA runbook, results, and the stale-chart design"
```

Committing the runbook and design doc **as-is** here means the Phase 4 edits land as a legible delta instead of appearing inside a whole-file addition.

The owner's screenshot `docs/reports/image.png` is evidence for the manual observation record — commit it too, renamed to something self-describing (e.g. `2026-08-10-epic3-workload-chart-observation.png`) and re-reference it from that report.

**Deliberately left uncommitted:** `.claude/settings.json`, `.claude/settings.local.json`, `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` (local tooling config, `settings.local.json` is machine-specific), `docs/assets/SmartStudyPlanner analytics UI.zip` (unrelated to this change), and the two session-handoff notes unless the owner wants them in.

- [ ] **Step 4: Copy this plan into the repo and commit**

Repo convention keeps plans in `docs/plans/`. Write this file's content to `docs/plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md`, then:

```bash
rtk git add docs/plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md
rtk git commit -m "docs(plans): add the workload-balancer stale-chart implementation plan"
```

No `Co-Authored-By` trailer (repo convention).

- [ ] **Step 5: Confirm HEAD alone reproduces the baseline**

```bash
rtk git status --short
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `status` shows only the deliberately-excluded files above, and the suite is still `Failed: 0, Passed: 475, Skipped: 1, Total: 476`. This is the number CI will compute.

---

## Phase 1 — Service: clamp the capacity ceiling

**Why first:** it is the only change with zero dependencies on the others, and the badge in Phase 3 misfires without it (design §4.6).

**Files:**
- Modify: `SmartStudyPlanner/Services/WorkloadServiceImpl.cs:31`, `:73`
- Test: `SmartStudyPlanner.Tests/Services/WorkloadServiceCapacityTests.cs` (append)

- [ ] **Step 1: Impact-check the symbol**

```
mcp__gitnexus__impact({target: "GetCapacity", direction: "upstream"})
```

Report the blast radius. Expect it to be small (`WorkloadBalancerViewModel` constructor). **Stop and warn the owner if it comes back HIGH or CRITICAL.**

- [ ] **Step 2: Write the failing test**

Append inside `WorkloadServiceCapacityTests`, immediately after `GetCapacity_GiaTriQuaNho_BiKepLenSanToiThieu` (keeps the floor and ceiling cases adjacent):

```csharp
        [Theory]
        [InlineData("8.5")]
        [InlineData("12")]
        [InlineData("99.5")]
        public void GetCapacity_GiaTriVuotTranSlider_BiKepXuongTranToiDa(string noiDung)
        {
            // Đối xứng với sàn ở test trên. Slider Maximum="8" (WorkloadBalancerPage.xaml:68)
            // sẽ coerce Value về 8 rồi ghi ngược qua TwoWay binding, nên CapacityHours thành 8.0
            // trong khi Schedule đã dựng ở 12.0 -> RenderedCapacityHours != CapacityHours và
            // badge "lịch cũ" hiện ngay trên trang người dùng chưa hề chạm vào.
            // File là đường vào duy nhất không tin được: SaveCapacity chỉ ghi giá trị đã qua
            // slider, nên chỉ file bị sửa tay/di cư mới rơi vào đây.
            GivenFile(noiDung);

            WithCulture("en-US", () => Assert.Equal(8.0, Sut().GetCapacity()));
        }
```

- [ ] **Step 3: Run it and confirm it fails**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~GetCapacity_GiaTriVuotTranSlider"
```

Expected: `Failed: 3, Passed: 0` — `Assert.Equal() Failure: Expected: 8, Actual: 8.5 / 12 / 99.5`.

This red run **is** the mutation evidence for this test: production is currently in the unmutated pre-fix state and the test catches it.

- [ ] **Step 4: Add the ceiling constant**

In `WorkloadServiceImpl.cs`, directly after the `MinCapacityMinutes` line (`:33`):

```csharp
        /// <summary>
        /// Trần sức học. Lấy đúng theo Maximum của slider ở WorkloadBalancerPage.xaml:68 — đối
        /// xứng với <see cref="MinCapacityHours"/>. Lý do kẹp sàn áp dụng y nguyên cho đầu trên:
        /// WorkloadBalancerViewModel đọc GetCapacity() rồi dựng lịch ngay trong constructor,
        /// TRƯỚC khi slider kịp kẹp giá trị. Không kẹp thì lịch dựng ở mức ngoài dải, slider
        /// coerce Value về 8 rồi ghi ngược — RenderedCapacityHours lệch CapacityHours ngay lúc
        /// mở trang và badge "lịch cũ" báo động giả.
        /// </summary>
        private const double MaxCapacityHours = 8.0;
```

- [ ] **Step 5: Clamp both ends**

Replace `WorkloadServiceImpl.cs:73`:

```diff
-            return Math.Max(val, MinCapacityHours);
+            return Math.Clamp(val, MinCapacityHours, MaxCapacityHours);
```

`Math.Clamp` throws on a NaN `min`/`max` but both are constants; a NaN `val` is already returned early by the `IsFinite` guard on the line above, so the existing `GetCapacity_GiaTriKhongHuuHan_*` theory is unaffected.

- [ ] **Step 6: Run the whole capacity class**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~WorkloadServiceCapacityTests"
```

Expected: `Failed: 0, Passed: 18` — 15 existing cases (10 `[Fact]`-equivalents plus the 3- and 4-case theories) plus the 3 new.

- [ ] **Step 7: Full suite**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `Failed: 0, Passed: 478, Skipped: 1, Total: 479`.

- [ ] **Step 8: Detect changes, then commit**

```
mcp__gitnexus__detect_changes()
```

Expected affected scope: `WorkloadServiceImpl.GetCapacity` only. Nothing in the scheduling path.

```bash
rtk git add SmartStudyPlanner/Services/WorkloadServiceImpl.cs SmartStudyPlanner.Tests/Services/WorkloadServiceCapacityTests.cs
rtk git commit -m "fix(workload): clamp capacity.txt to the slider ceiling, not just its floor"
```

---

## Phase 2 — ViewModel: split the two capacity roles

**Files:**
- Modify: `SmartStudyPlanner/ViewModels/WorkloadBalancerViewModel.cs` (whole file)
- Create: `SmartStudyPlanner.Tests/ViewModels/WorkloadBalancerViewModelTests.cs`

- [ ] **Step 1: Impact-check**

```
mcp__gitnexus__impact({target: "WorkloadBalancerViewModel", direction: "upstream"})
```

Expect one production caller: `WorkloadBalancerPage.xaml.cs:15`. Warn the owner on HIGH/CRITICAL.

- [ ] **Step 2: Write the failing test file**

Create `SmartStudyPlanner.Tests/ViewModels/WorkloadBalancerViewModelTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// Ghim việc tách hai vai của sức học: <c>CapacityHours</c> là mức slider đang trỏ tới,
    /// <c>RenderedCapacityHours</c> là mức mà <c>Schedule</c> hiện tại THỰC SỰ được dựng bằng.
    /// Bất biến: biểu đồ nói thật đúng khi <c>IsScheduleStale</c> false.
    /// Xem docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md §4.1.
    /// </summary>
    public sealed class WorkloadBalancerViewModelTests
    {
        /// <summary>Fake ghi lại lời gọi. Chỉ dùng trong file này nên khai báo inline
        /// theo quy ước test-doubles của repo.</summary>
        private sealed class RecordingWorkloadService : IWorkloadService
        {
            public double StoredCapacity = 3.0;
            public readonly List<double> SaveCapacityCalls = new();
            public readonly List<double> GenerateScheduleCalls = new();

            public double GetCapacity() => StoredCapacity;

            public void SaveCapacity(double capacity) => SaveCapacityCalls.Add(capacity);

            public List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours)
            {
                GenerateScheduleCalls.Add(capacityHours);
                return new List<ScheduleDay>
                {
                    new ScheduleDay
                    {
                        Date = new DateTime(2026, 8, 10),
                        DisplayName = "T2 10/08",
                        TotalMinutes = (int)(capacityHours * 60),
                        Tasks = { new ScheduledTask { TenTask = "T-A", TenMon = "Toán", SoPhut = 60 } },
                    },
                };
            }
        }

        private static (WorkloadBalancerViewModel Vm, RecordingWorkloadService Svc, List<string> Notified)
            Sut(double stored = 3.0)
        {
            var svc = new RecordingWorkloadService { StoredCapacity = stored };
            var notified = new List<string>();
            var vm = new WorkloadBalancerViewModel(
                new HocKy("HK1", DateTime.Today), svc, notified.Add);
            return (vm, svc, notified);
        }

        [Fact]
        public void Constructor_LichVuaDungXong_KhongBaoStale()
        {
            var (vm, svc, notified) = Sut(stored: 5.0);

            Assert.Equal(5.0, vm.CapacityHours);
            Assert.Equal(5.0, vm.RenderedCapacityHours);
            Assert.False(vm.IsScheduleStale);
            Assert.Single(svc.GenerateScheduleCalls);
            Assert.Empty(notified);   // mở trang không bung dialog
        }

        [Fact]
        public void DoiCapacityHours_ChuaBamXepLai_BaoStaleVaGiuNguyenRendered()
        {
            var (vm, _, _) = Sut(stored: 3.0);

            vm.CapacityHours = 6.0;

            Assert.Equal(3.0, vm.RenderedCapacityHours);
            Assert.True(vm.IsScheduleStale);
        }

        [Fact]
        public void GenerateScheduleCommand_DungLaiLichBangMucMoi_VaTatStale()
        {
            var (vm, svc, notified) = Sut(stored: 3.0);
            vm.CapacityHours = 6.0;

            vm.GenerateScheduleCommand.Execute(null);

            Assert.Equal(6.0, svc.GenerateScheduleCalls.Last());
            Assert.Equal(6.0, svc.SaveCapacityCalls.Last());
            Assert.Equal(6.0, vm.RenderedCapacityHours);
            Assert.False(vm.IsScheduleStale);
            Assert.Single(notified);
        }

        [Fact]
        public void DoiCapacityHours_KhongTuDongXepLaiVaKhongLuu()
        {
            // Ghim quyết định D1: KHÔNG auto-reschedule khi kéo slider. SaveCapacity chạm đĩa
            // và GenerateSchedule kéo theo write-through DiemUuTien (CP-2) xuống database —
            // không đặt hai thứ đó sau một cử chỉ kéo.
            var (vm, svc, _) = Sut(stored: 3.0);
            int genTruoc = svc.GenerateScheduleCalls.Count;
            int saveTruoc = svc.SaveCapacityCalls.Count;

            vm.CapacityHours = 7.0;

            Assert.Equal(genTruoc, svc.GenerateScheduleCalls.Count);
            Assert.Equal(saveTruoc, svc.SaveCapacityCalls.Count);
        }

        [Fact]
        public void DoiCapacityHours_PhatPropertyChangedChoIsScheduleStale()
        {
            // IsScheduleStale là property tính toán, không phải [ObservableProperty]; thông báo
            // đổi của nó đến từ [NotifyPropertyChangedFor] trên hai field nguồn. Mất attribute
            // đó thì badge không bao giờ hiện trong app thật — mà 4 test trên vẫn xanh, vì
            // chúng đọc thẳng property chứ không nghe thông báo.
            var (vm, _, _) = Sut(stored: 3.0);
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.CapacityHours = 4.0;

            Assert.Contains(nameof(WorkloadBalancerViewModel.IsScheduleStale), raised);
        }
    }
}
```

- [ ] **Step 3: Run and confirm it fails to compile**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~WorkloadBalancerViewModelTests"
```

Expected: build errors — `CS1061: 'WorkloadBalancerViewModel' does not contain a definition for 'RenderedCapacityHours'` and `CS1729`/`CS1503` on the three-argument constructor. A compile failure is a weak red, which is why Step 7 mutates production explicitly.

- [ ] **Step 4: Rewrite the ViewModel**

Replace the whole body of `SmartStudyPlanner/ViewModels/WorkloadBalancerViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System;
using System.Collections.ObjectModel;

namespace SmartStudyPlanner.ViewModels
{
    public partial class WorkloadBalancerViewModel : ObservableObject
    {
        private readonly HocKy _hocKy;
        private readonly IWorkloadService _workloadService;
        private readonly Action<string> _notify;

        /// <summary>Mức sức học slider đang trỏ tới. Đổi ngay khi người dùng kéo.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsScheduleStale))]
        private double capacityHours;

        /// <summary>
        /// Mức sức học mà <see cref="Schedule"/> hiện tại THỰC SỰ được dựng bằng. Biểu đồ và
        /// meter phải đo theo giá trị này, không phải theo <see cref="CapacityHours"/>: một
        /// property phục vụ hai vai chính là lỗi gốc — kéo slider chỉ chạy lại converter, nên
        /// màn hình vẽ phân bổ CŨ đo bằng mức trần MỚI.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsScheduleStale))]
        private double renderedCapacityHours;

        [ObservableProperty] private ObservableCollection<ScheduleDay> schedule = new();

        /// <summary>
        /// Biểu đồ đang mô tả một mức sức học khác mức slider đang trỏ tới. 0.01 là chặn
        /// so-sánh-float, không phải ngưỡng: slider snap theo tick nguyên (IsSnapToTickEnabled,
        /// TickFrequency=1) nên chênh lệch thật nhỏ nhất là 1.0.
        /// </summary>
        public bool IsScheduleStale => Math.Abs(CapacityHours - RenderedCapacityHours) > 0.01;

        // Constructor mặc định — resolve từ DI
        public WorkloadBalancerViewModel(HocKy hocKy)
            : this(hocKy, ServiceLocator.Get<IWorkloadService>()) { }

        // Constructor có injection — dùng cho unit test.
        // notify: seam để test chạy headless; mặc định giữ nguyên MessageBox, không đổi UX.
        public WorkloadBalancerViewModel(
            HocKy hocKy,
            IWorkloadService workloadService,
            Action<string>? notify = null)
        {
            _hocKy = hocKy;
            _workloadService = workloadService;
            _notify = notify ?? (m => System.Windows.MessageBox.Show(m, "Workload Balancer"));
            CapacityHours = _workloadService.GetCapacity();
            BuildSchedule(notify: false);   // khởi tạo: không popup, tránh modal mỗi lần nav
        }

        [RelayCommand]
        private void GenerateSchedule() => BuildSchedule(notify: true);

        private void BuildSchedule(bool notify)
        {
            _workloadService.SaveCapacity(CapacityHours);

            var generatedList = _workloadService.GenerateSchedule(_hocKy, CapacityHours);

            Schedule.Clear();
            foreach (var day in generatedList)
            {
                if (day.Tasks.Count > 0) Schedule.Add(day);
            }

            // Sau điểm này biểu đồ mới được phép đo theo mức vừa dùng.
            RenderedCapacityHours = CapacityHours;

            if (notify)
                _notify($"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!");
        }
    }
}
```

- [ ] **Step 5: Run the new tests and confirm they pass**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~WorkloadBalancerViewModelTests"
```

Expected: `Failed: 0, Passed: 5`.

- [ ] **Step 6: Full suite**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `Failed: 0, Passed: 483, Skipped: 1, Total: 484`.

- [ ] **Step 7: Mutation-test the five pins**

Apply each mutation to `WorkloadBalancerViewModel.cs`, run `--filter "FullyQualifiedName~WorkloadBalancerViewModelTests"`, record the result, then **revert before the next one**. Record the table in the Phase 5 report.

| # | Mutation | Must go red |
|---|---|---|
| M1 | Delete `RenderedCapacityHours = CapacityHours;` from `BuildSchedule` | `Constructor_LichVuaDungXong…`, `GenerateScheduleCommand_DungLaiLichBangMucMoi…` |
| M2 | Move `RenderedCapacityHours = CapacityHours;` into a new `partial void OnCapacityHoursChanged(double value) => RenderedCapacityHours = value;` | `DoiCapacityHours_ChuaBamXepLai…` |
| M3 | Add `partial void OnCapacityHoursChanged(double value) => BuildSchedule(notify: false);` | `DoiCapacityHours_KhongTuDongXepLaiVaKhongLuu` |
| M4 | Remove `[NotifyPropertyChangedFor(nameof(IsScheduleStale))]` from `capacityHours` | `DoiCapacityHours_PhatPropertyChangedChoIsScheduleStale` **and nothing else** — the other four must stay green, which is the point of test 5 |
| M5 | Change `_workloadService.GenerateSchedule(_hocKy, CapacityHours)` to `…, RenderedCapacityHours)` | `GenerateScheduleCommand_DungLaiLichBangMucMoi…` |

If any mutation leaves every test green, that pin is vacuous — fix the test before moving on.

- [ ] **Step 8: Verify the tree is back to the unmutated state**

```bash
rtk git diff --stat SmartStudyPlanner/ViewModels/WorkloadBalancerViewModel.cs
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: the diff shows only the Step 4 rewrite, and the suite is back to `Passed: 483, Skipped: 1, Total: 484`.

- [ ] **Step 9: Detect changes, then commit**

```
mcp__gitnexus__detect_changes()
```

```bash
rtk git add SmartStudyPlanner/ViewModels/WorkloadBalancerViewModel.cs SmartStudyPlanner.Tests/ViewModels/WorkloadBalancerViewModelTests.cs
rtk git commit -m "fix(workload): separate rendered capacity from the slider's target capacity"
```

---

## Phase 3 — View: bindings, stale badge, and the four stale strings

**Files:**
- Modify: `SmartStudyPlanner/Views/WorkloadBalancerPage.xaml` (`:9-14`, `:29`, `:39`, `:94`, `:117`, `:123`, `:150`, `:182`, `:188`, `:198`, `:275`)
- Modify: `SmartStudyPlanner/Services/IWorkloadService.cs:7`, `:18`
- Create: `SmartStudyPlanner.Tests/Views/WorkloadBalancerPageSourceTests.cs`

Line numbers are pre-edit; the badge insert shifts everything below `:94` down.

- [ ] **Step 1: Write the failing source-guard tests**

Create `SmartStudyPlanner.Tests/Views/WorkloadBalancerPageSourceTests.cs`:

```csharp
using System;
using System.IO;
using System.Text.RegularExpressions;
using SmartStudyPlanner.Tests.Services.Soe;   // RepoLocator (internal, cùng assembly)
using Xunit;

namespace SmartStudyPlanner.Tests.Views
{
    /// <summary>
    /// Lỗi gốc nằm trong BINDING và trong CHUỖI hiển thị — không unit test ViewModel nào chứng
    /// minh được XAML trỏ đúng property hay bản copy mô tả đúng thuật toán. Dùng lại tiền lệ
    /// source-assertion có sẵn trong repo: ObjectiveEvaluatorTests
    /// .SourceFiles_ContainNoHanChotOrDeadlineToken.
    /// </summary>
    public sealed class WorkloadBalancerPageSourceTests
    {
        private static string ReadRepoFile(params string[] parts)
        {
            string path = Path.Combine(RepoLocator.FindRepoRoot(), Path.Combine(parts));
            Assert.True(File.Exists(path), $"Không tìm thấy file: {path}");
            return File.ReadAllText(path);
        }

        private static string Xaml() =>
            ReadRepoFile("SmartStudyPlanner", "Views", "WorkloadBalancerPage.xaml");

        [Fact]
        public void Xaml_MoiBindingDoLuong_DeuTroVaoRenderedCapacityHours()
        {
            string xaml = Xaml();

            // (a) Chặn âm: sau §4.3 mọi tham chiếu qua ancestor đều là RenderedCapacityHours;
            //     hai binding cố ý giữ giá trị sống (slider :68, số 38pt :56) là
            //     {Binding CapacityHours} trần, KHÔNG có tiền tố "DataContext.". Nên khẳng định
            //     này chính xác, và đỏ nếu có converter binding nào bị trỏ ngược về giá trị sống.
            Assert.DoesNotContain("DataContext.CapacityHours", xaml, StringComparison.Ordinal);

            // (b) Chặn dương. Chỉ có (a) thì XOÁ sạch năm binding cũng xanh — đúng loại lỗ hổng
            //     mà probe M5 của automated gate đã phơi ra. Đếm cụ thể mới chứng minh được
            //     tiêu chí nghiệm thu 3, và tiện thể phủ luôn dòng caption :150 mà design §5.2
            //     ghi là "không guard được".
            Assert.Equal(5, Regex.Matches(xaml, @"Path=""DataContext\.RenderedCapacityHours""").Count);
            Assert.Equal(2, Regex.Matches(xaml, @"Path=""RenderedCapacityHours""").Count);   // caption :150 + badge
        }

        [Fact]
        public void Xaml_KhongConMoTaLuatXepLichCu()
        {
            string xaml = Xaml();

            foreach (var token in new[] { "đều khắp", "ít tải nhất" })
            {
                Assert.False(
                    xaml.Contains(token, StringComparison.Ordinal),
                    $"WorkloadBalancerPage.xaml còn chuỗi '{token}' — mô tả luật least-load mà T3.3 đã thay bằng ngày-sớm-nhất-còn-chỗ.");
            }
        }

        [Fact]
        public void IWorkloadService_DocComment_KhongConNoiLeastLoadHayChanHorizon7Ngay()
        {
            string src = ReadRepoFile("SmartStudyPlanner", "Services", "IWorkloadService.cs");

            foreach (var token in new[] { "Least-Load", "7 ngày" })
            {
                Assert.False(
                    src.Contains(token, StringComparison.Ordinal),
                    $"IWorkloadService.cs còn chuỗi '{token}' — hợp đồng scheduling mô tả sai sau T3.3 (không còn least-load, cũng không chốt 7 ngày).");
            }
        }
    }
}
```

- [ ] **Step 2: Run and confirm all three fail**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~WorkloadBalancerPageSourceTests"
```

Expected: `Failed: 3, Passed: 0`. This red run is the mutation evidence for all three guards — production is in the unmutated pre-fix state and each guard catches it. Note the third fails on **both** tokens (`:7` and `:18`), confirming deviation #1.

- [ ] **Step 3: Register the visibility converter**

`WorkloadBalancerPage.xaml`, after `:28` (`<conv:FullDayToVisibilityConverter x:Key="FullVis"/>`):

```xml
            <BooleanToVisibilityConverter x:Key="BoolVis"/>
```

`BooleanToVisibilityConverter` lives in the default WPF presentation namespace — no new converter class, no extra `xmlns` (design §4.4).

- [ ] **Step 4: Repoint the five converter bindings**

Five identical replacements at `:117`, `:123`, `:182`, `:188`, `:198`. Each line is currently:

```xml
                                                        <Binding Path="DataContext.CapacityHours" RelativeSource="{RelativeSource AncestorType=Page}"/>
```

Because the five lines differ only in leading whitespace, use `Edit` with `replace_all: true` on the substring `Path="DataContext.CapacityHours"` → `Path="DataContext.RenderedCapacityHours"`. `Edit` reports success, not a hit count, so bracket it with counts:

```
Grep pattern: "DataContext\.CapacityHours",         path: …/WorkloadBalancerPage.xaml, output_mode: count   → expect 5
(apply the Edit)
Grep pattern: "DataContext\.CapacityHours",         output_mode: count                                     → expect 0 matches
Grep pattern: "DataContext\.RenderedCapacityHours", output_mode: count                                     → expect 5
```

- [ ] **Step 5: Repoint the dashed-line caption**

`:150` — it names the line the bars are measured against, so it must name the same number:

```diff
-                            <Binding Path="CapacityHours" StringFormat="Đường nét đứt = mức sức học {0:0.0} giờ/ngày · cột chạm vạch là ngày đã đầy"/>
+                            <Binding Path="RenderedCapacityHours" StringFormat="Đường nét đứt = mức sức học {0:0.0} giờ/ngày · cột chạm vạch là ngày đã đầy"/>
```

**Design §5.2 recorded this line as un-guardable** — its binding form (`Path="CapacityHours"`) is indistinguishable from the two that must stay live, so an absence-only guard cannot see it. The positive count assertion added in Step 1 (`Path="RenderedCapacityHours"` must appear exactly twice — this caption plus the badge) closes that hole. Manual test M3 in Phase 5 still covers the rendered result.

- [ ] **Step 6: Insert the stale badge**

Between the section-header `</StackPanel>` (`:93`) and `<Grid Margin="0,18,0,0">` (`:95`), as a direct child of the chart panel's `StackPanel` — adjacent to the thing it warns about:

```xml
                    <Border Background="#1AFB3B53" BorderBrush="{DynamicResource SeverityUrgent}"
                            BorderThickness="1" CornerRadius="4" Padding="10,7" Margin="0,10,0,0"
                            Visibility="{Binding IsScheduleStale, Converter={StaticResource BoolVis}}">
                        <TextBlock FontSize="11.5" TextWrapping="Wrap"
                                   Foreground="{DynamicResource PrimaryText}">
                            <TextBlock.Text>
                                <Binding Path="RenderedCapacityHours"
                                         StringFormat="⚠ Lịch đang theo mức {0:0.0} giờ/ngày — bấm XẾP LỊCH LẠI để áp dụng mức mới."/>
                            </TextBlock.Text>
                        </TextBlock>
                    </Border>
```

The badge sits outside every `ItemTemplate`, so its `DataContext` is the Page's — the ViewModel. Save the file as UTF-8; `⚠` and the Vietnamese diacritics must survive.

- [ ] **Step 7: Fix the two user-facing strings**

`:39` — the header stops claiming anything about distribution:

```diff
-                <TextBlock Text="Thuật toán rải các bài tập chưa hoàn thành đều khắp những ngày tới — theo điểm ưu tiên và sức học mỗi ngày của bạn."
+                <TextBlock Text="Tự động xếp các bài tập chưa hoàn thành vào những ngày tới — theo điểm ưu tiên và sức học mỗi ngày của bạn."
```

`:275` — the bottom note becomes the single place that explains the mechanism:

```diff
-                        <TextBlock Grid.Column="1" Text="Mỗi bài luôn được dồn vào ngày ít tải nhất còn dưới mức trần — nhờ vậy tải các ngày luôn cân đối, không dồn cục."
+                        <TextBlock Grid.Column="1" Text="Mỗi bài được xếp vào ngày sớm nhất còn chỗ trống — học xong sớm, không để dồn về sát hạn."
```

- [ ] **Step 8: Update the stale XAML header comment**

`:9-14` currently claims the ViewModel is not modified and lists the old binding set. Replace those lines:

```diff
-      GIỮ NGUYÊN binding gốc của WorkloadBalancerViewModel + ScheduleModels:
-        CapacityHours · GenerateScheduleCommand
-        Schedule (ScheduleDay: DisplayName / TotalMinutes / HeaderText / Tasks)
-        Tasks (ScheduledTask: TenTask / TenMon / SoPhut / ThoiGianHienThi)
-      Không sửa ViewModel/Model. Thanh tải tương đối so với sức học được tính
-      bằng converter [TotalMinutes, CapacityHours] (Converters/WorkloadConverters.cs).
+      Binding của WorkloadBalancerViewModel + ScheduleModels:
+        CapacityHours (mức slider đang trỏ tới — CHỈ slider :68 và số 38pt :56 dùng)
+        RenderedCapacityHours (mức lịch hiện tại thực sự được dựng bằng — MỌI converter dùng)
+        IsScheduleStale · GenerateScheduleCommand
+        Schedule (ScheduleDay: DisplayName / TotalMinutes / HeaderText / Tasks)
+        Tasks (ScheduledTask: TenTask / TenMon / SoPhut / ThoiGianHienThi)
+      Thanh tải tương đối so với sức học được tính bằng converter
+      [TotalMinutes, RenderedCapacityHours] (Converters/WorkloadConverters.cs). Đo theo
+      CapacityHours là lỗi gốc: kéo slider chỉ chạy lại converter, không dựng lại lịch.
```

- [ ] **Step 9: Fix both `IWorkloadService.cs` doc comments**

```diff
     /// <summary>
-    /// Contract cho Workload Balancer — tạo lịch học 7 ngày và quản lý capacity.
+    /// Contract cho Workload Balancer — xếp lịch học theo ngày và quản lý capacity.
     /// Inject interface này thay vì gọi static WorkloadService trực tiếp.
     /// </summary>
```

```diff
-        /// <summary>Chạy thuật toán Greedy Least-Load, trả về lịch 7 ngày.</summary>
+        /// <summary>Xếp lịch theo ngày sớm nhất còn chỗ (T3.3), trả về các ngày có bài.</summary>
```

Both original claims are false: the rule is no longer least-load, and the horizon is not fixed at seven days (confirmed by runbook D5).

- [ ] **Step 10: Run the guards, then build, then the full suite**

```bash
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~WorkloadBalancerPageSourceTests"
rtk dotnet build SmartStudyPlanner.slnx -c Debug
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: guards `Failed: 0, Passed: 3`; build `0 Error(s)` (94 pre-existing warnings are the known baseline); suite `Failed: 0, Passed: 486, Skipped: 1, Total: 487`.

XAML errors surface at build, not at test — if the badge markup is malformed the build fails here with an `MC3000`/`MC4103` on `WorkloadBalancerPage.xaml`.

- [ ] **Step 11: Detect changes, then commit**

```
mcp__gitnexus__detect_changes()
```

```bash
rtk git add SmartStudyPlanner/Views/WorkloadBalancerPage.xaml SmartStudyPlanner/Services/IWorkloadService.cs SmartStudyPlanner.Tests/Views/WorkloadBalancerPageSourceTests.cs
rtk git commit -m "fix(workload): measure the chart against the rendered capacity and warn when it is stale"
```

---

## Phase 4 — Runbook and design-doc follow-up

**Files:**
- Modify: `docs/plans/2026-08-10-epic-3-manual-qa-runbook.md`
- Modify: `docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md` (status line only)

Read the runbook first — it is already modified in the working tree from the earlier session, and these edits go on top.

- [ ] **Step 1: C2 — instruct the button press and mark the result for re-run**

Add to the C2 steps: *"After every slider change, press **XẾP LỊCH LẠI** before reading the chart. The chart is only truthful for the capacity it was last built with; a warning badge above the chart tells you when it isn't."*

In the §4 results table, change C2's recorded verdict to **`Re-run required`** with the note: *"Recorded Met on 2026-08-10 was read off a chart that could have been stale (design §2.4, D4). Withdrawn, not failed."*

- [ ] **Step 2: E6 — retarget from semester deletion to subject deletion**

E6 currently asks the tester to delete a semester. No such capability exists — there is no `XoaHocKy`, `DeleteHocKy`, or `HocKys.Remove` in production code. Retarget the scenario to **subject** deletion, which is the reachable path to the EF cascade-fixup regression E6 was meant to guard:

`ViewModels/QuanLyMonHocViewModel.cs:89` (`XoaMon`) → `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs:163` (`db.MonHocs.Remove(oldMon)`).

Rewrite E6 as: create a subject with at least two tasks → delete the subject → confirm no crash, the tasks go with it, and other subjects in the semester are untouched. Add a footnote that no semester-management UI exists (see Phase 4 Step 4).

- [ ] **Step 3: Add three badge scenarios**

Append to the C-series:

| ID | Steps | Expected |
|---|---|---|
| C8 | Open the page. Drag the slider from its current value to a different tick. Do **not** press the button. | Readout and slider move. **No chart bar moves.** The badge appears above the chart naming the *old* capacity. |
| C9 | From C8's end state, press **XẾP LỊCH LẠI**. | Confirmation dialog shows the *new* capacity. Badge disappears. Chart rescales and re-allocates. |
| C10 | Close the app, edit `capacity.txt` in the build output to `12`, relaunch, open the page. | Readout shows `8.0`. **No badge.** (Guards the §4.6 ceiling clamp from the user's side.) |

- [ ] **Step 4: Link the owner's observation record**

In the runbook's §4 results table, add a reference row pointing at `docs/reports/2026-08-10-epic3-soe-manual-observation.md` rather than transcribing it. It is the owner's primary evidence; copying it into a document authored by someone else muddies provenance instead of clarifying it (design §6).

- [ ] **Step 5: Note the missing semester-management capability**

Add a short "Known gaps" note to the runbook: there is no UI to rename or delete a semester, only to create one. Recorded as a proposal for separate work (design §7), not a defect in this change.

- [ ] **Step 6: Flip the design doc's status**

```diff
-**Status:** design approved, implementation plan not yet written
+**Status:** implemented — see `docs/plans/2026-08-14-workload-balancer-stale-chart-fix-plan.md`
```

- [ ] **Step 7: Commit**

```bash
rtk git add docs/plans/2026-08-10-epic-3-manual-qa-runbook.md docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md
rtk git commit -m "docs(qa): retarget E6, add the badge scenarios, and return C2 to unverified"
```

---

## Phase 5 — Verification and PR

- [ ] **Step 1: Clean full-suite run**

```bash
rtk dotnet build SmartStudyPlanner.slnx -c Debug
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```

Expected: `0 Error(s)`; `Failed: 0, Passed: 486, Skipped: 1, Total: 487` — baseline 476 plus 11 new cases (3 service theory cases + 5 ViewModel facts + 3 source guards).

- [ ] **Step 2: Manual GUI check (Release)**

Three acceptance criteria are visual and no automated test reaches them. Build Release, **verify the `.exe` mtime matches this build** before trusting anything you see (repo convention — verify artifact provenance), then run:

```bash
rtk dotnet build SmartStudyPlanner.slnx -c Release
```

| # | Check | PASS | FAIL |
|---|---|---|---|
| M1 | Open the page, drag the slider one tick, do not press the button | Readout changes, badge appears naming the old capacity, **no bar moves** | Any bar changes height or colour |
| M2 | Press **XẾP LỊCH LẠI** | Dialog names the new capacity; badge gone; chart rescaled | Badge persists, or dialog names the old value |
| M3 | Read the dashed-line caption in both states | Caption number always equals the badge's number in M1, and the slider's in M2 | Caption tracks the slider during M1 |
| M4 | Read the header (top) and note 02 (bottom) | Neither mentions "đều khắp" or "ít tải nhất" | Either still describes the old rule |
| M5 | Close app, set `capacity.txt` to `12`, relaunch, open the page | Readout `8.0`, no badge | Badge visible on an untouched page |

Report the actual PASS/FAIL for each. **Do not fabricate results** — if a check was not run, say so.

- [ ] **Step 3: Write the report**

Create `docs/reports/2026-08-14-workload-balancer-stale-chart-fix-report.md` containing:
- the Phase 2 Step 7 mutation matrix with actual observed red/green per mutation;
- the Step 2 manual PASS/FAIL table;
- before/after suite counts;
- a **"Decisions made"** section in ADR style (why / what for / experience) — mandatory for every `docs/reports/*.md` since 2026-07-07. Cover at minimum: the three deviations listed at the top of this plan, and why the copy guards were added rather than left to manual test.

- [ ] **Step 4: Commit the report**

```bash
rtk git add docs/reports/2026-08-14-workload-balancer-stale-chart-fix-report.md
rtk git commit -m "docs(reports): record the stale-chart fix verification and mutation evidence"
```

- [ ] **Step 5: Push and open the PR**

`dev` is PR-only; direct push is blocked.

```bash
rtk git push -u origin fix/workload-balancer-stale-chart
rtk gh pr create --base dev --title "fix(workload): stop the balance chart from redrawing stale allocations" --body @'
## Root cause

`WorkloadBalancerViewModel` had no `OnCapacityHoursChanged`, so moving the slider never rebuilt the
schedule — it only re-ran the `[TotalMinutes, CapacityHours]` converters, drawing the **old**
allocation against the **new** capacity line. Pre-existing, but T3.3 earliest-feasible packing made
it loud: every used day but the last now sits at the ceiling.

## Change

- `CapacityHours` (slider target) split from `RenderedCapacityHours` (what `Schedule` was built
  with); all five converter bindings and the dashed-line caption now measure against the latter.
- New `IsScheduleStale` drives a warning badge above the chart.
- `GetCapacity` clamps `capacity.txt` to the slider ceiling as well as its floor — without it the
  badge fires on an untouched page.
- Four strings corrected that still described the pre-T3.3 least-load rule:
  `WorkloadBalancerPage.xaml:39`, `:275`, `IWorkloadService.cs:7`, `:18`.
- Notifier seam (`Action<string>? notify = null`) makes the button path testable headless; no UX
  change.

## Deviations from the approved design

1. A fourth stale string (`IWorkloadService.cs:7`) found during implementation.
2. Two extra source-guard tests, so acceptance criterion 4 is not manual-only.
3. Stale XAML header comment at `:9-14` updated.

## Verification

Design: `docs/plans/2026-08-10-workload-balancer-stale-chart-fix-design.md`
Report (mutation matrix + manual PASS/FAIL): `docs/reports/2026-08-14-workload-balancer-stale-chart-fix-report.md`

Suite 476 → 487 (486 passed / 0 failed / 1 skipped), build 0 errors.
'@
```

Adjust the here-string form to the shell in use — the block above is PowerShell (`@'…'@`, closing token at column 0); under the Bash tool use a heredoc instead. Wait for CI green before merging.

---

## Acceptance criteria

Mapped from design §10, with the deviations folded in.

1. Moving the slider changes the readout and the badge, and moves no chart bar. *(M1)*
2. Pressing **XẾP LỊCH LẠI** rebuilds the schedule, clears the badge, and rescales the chart. *(M2, ViewModel test 3)*
3. The five converter bindings and the `:150` caption reference `RenderedCapacityHours` — asserted by exact count, not just by absence. *(source guard 1, M3)*
4. All **four** stale strings updated; no user-facing text describes least-loaded or even distribution, and no doc comment claims a least-load rule or a fixed seven-day horizon. *(source guards 2 and 3, M4)*
5. Opening the page never shows the badge, whatever `capacity.txt` contains. *(service theory, M5)*
6. **Nine** new test methods / **eleven** xUnit cases pass — five ViewModel, three source guards, one service theory — each shown to fail against unmutated-or-mutated production, per Phase 1 Step 3, Phase 2 Step 7, and Phase 3 Step 2.
7. Full suite green: `Passed: 486, Failed: 0, Skipped: 1, Total: 487`; build `0 Error(s)`.
8. Runbook corrected per Phase 4, with C2 marked for re-run and E6 retargeted at subject deletion.

## Out of scope

- Scheduling behaviour. T3.3 earliest-feasible placement, the CP-2 `DiemUuTien` write-through, and the D7 past-deadline ruling are untouched. `WorkloadServiceImpl` is modified in exactly one place — `GetCapacity` input validation — which cannot change output for any in-range value.
- Auto-reschedule on slider drag (considered and rejected — design D1; pinned by ViewModel test 4).
- Semester-management UI (design §7 — recorded as a proposal, scoped as its own work).
