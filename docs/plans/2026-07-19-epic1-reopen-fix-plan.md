# Epic 1 Reopen Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **HARD CONSTRAINT (owner, 2026-07-19):** *"Do not begin implementation yet. The planning
> document will be reviewed and approved before any coding starts."* — no task below may
> start until the owner flips Status to `approved`.
> **SATISFIED 2026-07-19:** owner reviewed and approved the plan as-is; implementation is
> released. PM/QA review stays with the orchestrating session (owner confirmation, same date).

**Status:** `approved (owner, 2026-07-19) — implementation released`

**Goal:** Close the single P0 regression that drove B4 = Reopen (task creation never stamps
`MaMonHoc`, so the M1.2 FK-based reconcile crashes the app) plus the minimal P0-adjacent
crash-visibility hardening — nothing else — so the Epic 1 closure gate can re-run Phase 2
and proceed to Phase 3.

**Architecture:** Two-layer fix for one bug: the ViewModel stamps the FK at creation (primary
fix), and the repository reconcile heals unstamped FKs from navigation position (defense in
depth, restoring the pre-M1.2 EF graph-fixup semantics). Separately, a tiny `CrashLogger` +
three global WPF exception handlers convert silent process death into a logged, survivable
error — implementing the QA nuance attached to the F2 waiver.

**Tech Stack:** .NET 10 WPF (`net10.0-windows10.0.19041.0`), EF Core + SQLite, xUnit with in-memory SQLite (`TestDb` fixture),
CommunityToolkit.Mvvm.

---

## Context

- Gate: [`2026-07-11-epic-1-closure-gate.md`](2026-07-11-epic-1-closure-gate.md) — Phase 2
  (owner supervised launch, 2026-07-15) returned **B4 = Reopen**.
- Sole reopen driver: investigation finding **2.1** — `StudyTask`'s 4-arg ctor never sets
  `MaMonHoc` (stays `Guid.Empty`); `QuanLyTaskViewModel.ThemTask()` adds the task to
  `MonHocHienTai.DanhSachTask` without stamping the FK; M1.2's reconcile (commit `6734177`)
  resolves the owner by scalar FK — `First(m => m.MaMonHoc == newTask.MaMonHoc)` — and throws
  `InvalidOperationException`; with no global exception handler the process dies.
  Pre-M1.2, the remove-then-recreate save let EF graph fixup silently heal the FK from the
  navigation position, which is why this never crashed before.
- Why 331 green tests missed it: **fixture bias** — every test site sets
  `MaMonHoc = monHoc.MaMonHoc` explicitly, so the suite verified a contract the production
  ViewModel never honored. See
  [`../knowledge/incident-investigation.md`](../knowledge/incident-investigation.md).
- Full analysis: [QA investigation](../reports/2026-07-19-epic1-phase2-qa-investigation.md);
  owner acceptance + mandate:
  [owner decisions](../specs/2026-07-19-owner-epic-1-decisions.md) (Decision 3 requires this
  plan; scope must stay minimal, P0 first, mandatory vs deferred separated, verification
  criteria defined).

## Scope

### Mandatory (this reopen)

| # | Item | Class | Slice |
|---|---|---|---|
| 1 | Stamp `MaMonHoc` on task creation in `ThemTask` | P0 regression fix (finding 2.1) | R1 |
| 2 | Reconcile heals `Guid.Empty` FK from navigation position; unknown non-empty FK fails loudly with context | P0 defense in depth | R1 |
| 3 | Regression tests at both layers (VM-level + repo-level), RED-first | P0 verification | R1 |
| 4 | `CrashLogger` + `DispatcherUnhandledException` / `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` hooks | P0-adjacent hardening (a crash must never again be silent + undiagnosable) | R2 |
| 5 | Observe the 3 fire-and-forget telemetry sites (F2 waiver nuance): `MatureAsync` at startup, `LogDifficultyLabelAsync`, `LogWeightChangeAsync` | F2 nuance | R2 |
| 6 | Owner re-closure re-run (B1.4 create-task + retests #2, #3) → B4 re-decision | Gate | after merge |

### Deferred (explicitly NOT in this reopen — tracked, not forgotten)

| Item | Class | Exit venue |
|---|---|---|
| Vietnamese negation not understood by quick-input parser | P1, **pre-existing** parser gap (not a regression) | Parser backlog; candidate for a future parsing milestone |
| Balancer empty-state + tasks >3 days overdue invisible in balancer | P1, product/design gap | Owner product decision, then UI-polish plan |
| Lưu Tiến Trình button off-Dashboard behavior (disable vs make global) | P2, product decision | Owner decision, then UI-polish plan |
| Heatmap bucket scale/legend + chart filter semantics | P2, UX improvement | Owner decisions, then UI-polish plan ([`2026-07-05-ui-mobile-ready-polish.md`](2026-07-05-ui-mobile-ready-polish.md)) |
| Sync-over-async call sites (investigation 2.8-2), `async void OnStartup` restructure (2.8-3) | Tech debt | Roadmap tech-debt list; note R2's Dispatcher hook already catches `async void OnStartup` faults, shrinking 2.8-3's blast radius without restructuring |
| 3 owner feature requests from the supervised launch | Feature | Stay in UI-polish PROPOSED row |

## Locked decisions

| ID | Decision | Why |
|---|---|---|
| D-R1 | Stamp the FK in `ThemTask` via object initializer — `new StudyTask(...) { MaMonHoc = MonHocHienTai.MaMonHoc }` — **not** a `StudyTask` ctor signature change | A 5-arg ctor would churn ~10 call/test sites for zero behavior gain; reopen scope stays minimal |
| D-R2 | Reconcile defense = normalize pass **before** the task dictionaries are built: `Guid.Empty` FK is healed from navigation position (authoritative — identical to pre-M1.2 EF fixup semantics). The owner lookup at the former `First()` becomes `FirstOrDefault() ?? throw` with task/FK/HocKy context so genuinely-corrupt non-empty FKs fail loudly, not cryptically | Two callers of truth: navigation position for "never stamped", scalar FK for "stamped but wrong". Healing the first, failing loudly on the second keeps reconcile deterministic |
| D-R3 | Tests use the in-memory SQLite `TestDb`/`NewDb` pattern (real repo, real SQL) at two layers: repo-level (defense) and VM-level (the actual production path through `ThemTaskCommand`) | The bug lived exactly in the gap between fixture setup and production path — the VM-level test is the discriminating one; the repo-level test keeps the defense pinned independently |
| D-R4 | `CrashLogger` is a ~40-line static file-appender (`%AppData%\SmartStudyPlanner\crash.log`, path overridable for tests), **not** a logging framework. `Observe()` uses a ContinueWith that always runs and logs only on fault (an `OnlyOnFaulted` continuation cancels on success, which would throw when awaited in tests) | Parking-lot compliant: structured logging stays out of Epic 1; the reopen only needs "a crash leaves a trace" |
| D-R5 | F2 waiver treated as granted (QA recommended grant; owner accepted the investigation in full). The nuance — faults from waived fire-and-forget writes must be observable — ships in R2. Formal sign-off happens at the re-closure gate; if the owner disagrees, the 3 `Observe`/catch-log edits are isolated and trivially strippable | Keeps the waiver decision with the owner while not shipping another silent-failure path |

## Open items for the owner (none block this plan)

1. Formal F2 waiver sign-off at re-closure (D-R5).
2. Deferred product decisions listed above (balancer overdue visibility, Lưu Tiến Trình
   scope, heatmap semantics) — needed only before the UI-polish plan, not before this fix.

## Parallel-dispatch decision

**No parallel dispatch.** Slice R2 edits `QuanLyTaskViewModel.cs` (line ~216) which R1 also
edits (line ~192); total effort is ~1.5 agent-days; and per Phase-1 experience (D-P7) more
parallelism here buys merge conflicts, not speed. Execution is sequential:
**Agent R1 → PM review → Agent R2 → PM review → merge → owner re-run.**

### Agent R1 — P0 regression fix

- **Mission:** Execute Slice R1 exactly as specified (Tasks R1-A, R1-B), RED-first.
- **Venue:** Worktree branch `reopen/fk-fix` off `ui_rf` (create via
  superpowers:using-git-worktrees at execution time).
- **Write scope:** `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs`,
  `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs`,
  `SmartStudyPlanner.Tests/ViewModels/QuanLyTaskViewModelTests.cs` (new),
  `SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs`. Nothing else.
- **Skills:** superpowers:test-driven-development, superpowers:verification-before-completion.
- **Key tools:** `gitnexus_impact` before each edit (see Pre-edit checklist), `rtk dotnet build` / `rtk dotnet test`.
- **Deliverables:** 2 commits (see tasks), full suite green.
- **Stop condition:** Any test failure not predicted by the RED steps below, or any needed
  edit outside Write scope → stop and report; do not improvise.

### Agent R2 — crash visibility

- **Mission:** Execute Slice R2 exactly as specified (Tasks R2-A, R2-B) on the same branch,
  after R1 is merged into it and PM-reviewed.
- **Venue:** Same branch `reopen/fk-fix`.
- **Write scope:** `SmartStudyPlanner/Services/CrashLogger.cs` (new),
  `SmartStudyPlanner/App.xaml.cs`, `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs`
  (line ~216 only), `SmartStudyPlanner/ViewModels/WeightOptimizerViewModel.cs` (line ~123 only),
  `SmartStudyPlanner.Tests/Services/CrashLoggerTests.cs` (new). Nothing else.
- **Skills:** superpowers:test-driven-development, superpowers:verification-before-completion.
- **Key tools:** `gitnexus_impact` before each edit, `rtk dotnet build` / `rtk dotnet test`.
- **Deliverables:** 2 commits, full suite green.
- **Stop condition:** Same as Agent R1.

### PM/QA (this session's role)

Review each agent's diff against this plan (scope adherence, test faithfulness — no
`MaMonHoc` stamping sneaking into the new tests' acted-on path), run
`gitnexus_detect_changes()` before each merge, merge `reopen/fk-fix` → `ui_rf`, append the
CHANGELOG row, then hand to the owner.

### Owner re-run (re-closure)

- B1.4 create-task scenario end-to-end (manual entry **and** quick-input smart add) — the
  crash scenario from 2026-07-15.
- Retest #2: heatmap — subject named "A" collapses to a single dim cell (was blocked by the
  crash; expected to pass now, observation was an artifact).
- Retest #3: Lưu Tiến Trình on Dashboard ×2 → two success dialogs (same: artifact retest).
- Then B4 re-decision; if Release → Phase 3 (C1–C3) per the existing gate doc.
- Estimated owner time: ~30 minutes.

## Pre-edit checklist (every agent, every task)

- [ ] `gitnexus_impact({target: "ThemTask", direction: "upstream"})` before touching
  `QuanLyTaskViewModel`; `gitnexus_impact({target: "LuuHocKyAsync", direction: "upstream"})`
  before touching the repository (**HIGH risk expected** — every save path funnels through
  it; report the blast radius per CLAUDE.md before proceeding); `gitnexus_impact({target:
  "OnStartup", direction: "upstream"})` before touching `App.xaml.cs`.
- [ ] `gitnexus_detect_changes()` before every commit; affected symbols must match the
  task's Write scope exactly.
- [ ] All shell commands `rtk`-prefixed. Commit messages carry **no** Co-Authored-By trailer.
- [ ] No schema change anywhere in this plan → migration/DoR-6 checks n/a.
- [ ] Vietnamese text in files is written only via the native Write/Edit tools (PowerShell
  `Get-Content`/`Set-Content` corrupts BOM-less UTF-8).

---

## Slice R1 — P0: task creation persists with correct FK

### Task R1-A: VM-level discriminating test + ViewModel fix

**Files:**
- Create: `SmartStudyPlanner.Tests/ViewModels/QuanLyTaskViewModelTests.cs`
- Modify: `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs:192-193`

- [ ] **Step 1: Write the failing test** — the production path, exactly as the app drives it:
  real `SqliteHocKyRepository`, the 6-param VM ctor (which injects `NullStudyTelemetry` and a
  null difficulty-log repo, so telemetry is inert), command execution, **no manual FK
  stamping anywhere**. `FakeDecisionEngine` already exists in this namespace
  (`TaskNotesTests.cs`); `FakeTaskEditorRepository` and `FakeClock` live in `TestDoubles`.

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    // Reopen 2026-07: the B4 crash lived in the gap between test fixtures (which always
    // stamped MaMonHoc by hand) and the real ThemTask path (which never did). This class
    // drives the ViewModel command against the real SQLite repository — fixture bias is
    // the failure mode it exists to prevent. Do NOT set MaMonHoc manually in these tests.
    public class QuanLyTaskViewModelTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly Func<AppDbContext> _factory;

        public QuanLyTaskViewModelTests()
        {
            _conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(_conn)) { /* EnsureCreated */ }
            _factory = () => TestDb.Create(_conn);
        }

        public void Dispose() => _conn.Dispose();

        [Fact]
        public async Task ThemTask_NewTask_PersistsWithOwnerSubjectFk()
        {
            var repo = new SqliteHocKyRepository(_factory);
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            hocKy.DanhSachMonHoc.Add(monHoc);
            await repo.LuuHocKyAsync(hocKy); // semester already exists → reconcile path on next save

            var vm = new QuanLyTaskViewModel(hocKy, monHoc, repo, new FakeTaskEditorRepository(),
                new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(DateTime.Today)))
            {
                TenTask = "Bai tap 1",
                HanChot = DateTime.Today.AddDays(3),
                DoKho = "2",
            };

            await vm.ThemTaskCommand.ExecuteAsync(null);

            using var ctx = _factory();
            var saved = await ctx.StudyTasks.SingleAsync(t => t.TenTask == "Bai tap 1");
            Assert.Equal(monHoc.MaMonHoc, saved.MaMonHoc);
        }
    }
}
```

- [ ] **Step 2: Run it, verify it fails for the right reason**

Run: `rtk dotnet test SmartStudyPlanner.slnx --filter "FullyQualifiedName~QuanLyTaskViewModelTests"`
Expected: FAIL with `System.InvalidOperationException : Sequence contains no matching element`
thrown from `SqliteHocKyRepository.LuuHocKyAsync` (the reconcile owner lookup) — the exact
production crash. If it fails any other way, stop and report.

- [ ] **Step 3: Minimal fix** — in `QuanLyTaskViewModel.ThemTask()`, the new-task branch
  currently reads:

```csharp
savedTask = new StudyTask(TenTask, HanChot.Value, loaiTask, doKhoInt);
MonHocHienTai.DanhSachTask.Add(savedTask);
```

Replace with (D-R1 — object initializer, no ctor change):

```csharp
savedTask = new StudyTask(TenTask, HanChot.Value, loaiTask, doKhoInt)
{
    MaMonHoc = MonHocHienTai.MaMonHoc,
};
MonHocHienTai.DanhSachTask.Add(savedTask);
```

- [ ] **Step 4: Verify green + no collateral**

Run: `rtk dotnet build SmartStudyPlanner.slnx` then `rtk dotnet test SmartStudyPlanner.slnx --no-build`
Expected: new test PASS; full suite green (~331 pre-existing + 1).

- [ ] **Step 5: Commit**

```bash
rtk git add SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs SmartStudyPlanner.Tests/ViewModels/QuanLyTaskViewModelTests.cs
rtk git commit -m "fix(reopen): stamp MaMonHoc when ThemTask creates a StudyTask (B4 driver, finding 2.1)"
```

### Task R1-B: repository defense — reconcile heals unstamped FK

**Files:**
- Modify: `SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs` (append a `[Fact]` to `public class RepositoriesTests`)
- Modify: `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs` (two edits inside `LuuHocKyAsync`)

- [ ] **Step 1: Write the failing test** (uses the class's existing `NewDb()` helper). It
  stays meaningful after R1-A because it bypasses the ViewModel entirely — it pins the
  repository contract for *any* future call site that forgets to stamp.

```csharp
[Fact]
public async Task LuuHocKyAsync_TaskAddedWithoutFkStamp_PersistsUnderNavigationOwner()
{
    var (conn, factory) = NewDb();
    using var _ = conn;
    var repo = new SqliteHocKyRepository(factory);

    // First save: HocKy + MonHoc exist in DB, so the next save takes the reconcile path.
    var hocKy = new HocKy("HK Reopen", DateTime.Today);
    var monHoc = new MonHoc("MH Reopen", 3) { MaHocKy = hocKy.MaHocKy };
    hocKy.DanhSachMonHoc.Add(monHoc);
    await repo.LuuHocKyAsync(hocKy);

    // A task that enters the graph only via the navigation collection — MaMonHoc left
    // Guid.Empty, exactly what an unstamped call site produces.
    var task = new StudyTask("Task khong stamp FK", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2);
    monHoc.DanhSachTask.Add(task);

    await repo.LuuHocKyAsync(hocKy);

    using var ctx = factory();
    var saved = await ctx.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
    Assert.Equal(monHoc.MaMonHoc, saved.MaMonHoc);
}
```

- [ ] **Step 2: Run it, verify it fails**

Run: `rtk dotnet test SmartStudyPlanner.slnx --filter "FullyQualifiedName~LuuHocKyAsync_TaskAddedWithoutFkStamp"`
Expected: FAIL with `System.InvalidOperationException : Sequence contains no matching element`
(second `LuuHocKyAsync` call).

- [ ] **Step 3: Implement the defense (D-R2), edit 1 — normalize pass.** In
  `SqliteHocKyRepository.LuuHocKyAsync`, inside the `else` (reconcile) branch, directly
  **before** the `oldTasksByMaTask` / `newTasksByMaTask` dictionaries are built (currently
  ~line 124), insert:

```csharp
// Reopen fix 2026-07: a task can arrive having entered the graph only through a
// navigation collection, with MaMonHoc never stamped (Guid.Empty). Its navigation
// position is authoritative in that case — the same semantics EF graph fixup gave the
// pre-M1.2 save — so heal the FK before the FK-keyed diff below treats it as identity.
foreach (var mon in hocKy.DanhSachMonHoc)
    foreach (var t in mon.DanhSachTask)
        if (t.MaMonHoc == Guid.Empty)
            t.MaMonHoc = mon.MaMonHoc;
```

- [ ] **Step 4: Implement the defense (D-R2), edit 2 — loud failure for real corruption.**
  In the task add/update loop (currently ~line 184), replace:

```csharp
var owner = hocKyCu.DanhSachMonHoc.First(m => m.MaMonHoc == newTask.MaMonHoc);
```

with:

```csharp
var owner = hocKyCu.DanhSachMonHoc.FirstOrDefault(m => m.MaMonHoc == newTask.MaMonHoc)
    ?? throw new InvalidOperationException(
        $"Reconcile: task '{newTask.TenTask}' ({newTask.MaTask}) references MonHoc {newTask.MaMonHoc} not present in HocKy {hocKyCu.MaHocKy}.");
```

- [ ] **Step 5: Verify green + no collateral**

Run: `rtk dotnet build SmartStudyPlanner.slnx` then `rtk dotnet test SmartStudyPlanner.slnx --no-build`
Expected: full suite green (~331 + 2).

- [ ] **Step 6: Commit**

```bash
rtk git add SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs
rtk git commit -m "fix(reopen): reconcile heals unstamped MaMonHoc from navigation position; unknown FK fails with context"
```

---

## Slice R2 — crash visibility (global handlers + F2 nuance)

### Task R2-A: CrashLogger + tests

**Files:**
- Create: `SmartStudyPlanner/Services/CrashLogger.cs`
- Create: `SmartStudyPlanner.Tests/Services/CrashLoggerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using SmartStudyPlanner.Services;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    public class CrashLoggerTests : IDisposable
    {
        private readonly string _origPath;
        private readonly string _tempPath;

        public CrashLoggerTests()
        {
            _origPath = CrashLogger.LogPath;
            _tempPath = Path.Combine(Path.GetTempPath(), $"crashlog-test-{Guid.NewGuid():N}.log");
            CrashLogger.LogPath = _tempPath;
        }

        public void Dispose()
        {
            CrashLogger.LogPath = _origPath;
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Fact]
        public void Log_WritesContextAndException()
        {
            CrashLogger.Log("unit-test", new InvalidOperationException("boom"));

            var content = File.ReadAllText(_tempPath);
            Assert.Contains("unit-test", content);
            Assert.Contains("boom", content);
        }

        [Fact]
        public async Task Observe_FaultedTask_LandsInCrashLog()
        {
            await CrashLogger.Observe(
                Task.FromException(new InvalidOperationException("bang")), "observe-test");

            var content = File.ReadAllText(_tempPath);
            Assert.Contains("observe-test", content);
            Assert.Contains("bang", content);
        }

        [Fact]
        public async Task Observe_SuccessfulTask_WritesNothing()
        {
            await CrashLogger.Observe(Task.CompletedTask, "no-fault");

            Assert.False(File.Exists(_tempPath));
        }
    }
}
```

Known minor caveat (accepted): `LogPath` is a global static, so this class temporarily
redirects it while other test classes run in parallel. No other test has a faulting
observed path, so nothing else writes during the window; do not "fix" this with a
collection attribute unless it actually flakes.

- [ ] **Step 2: Run, verify they fail**

Run: `rtk dotnet test SmartStudyPlanner.slnx --filter "FullyQualifiedName~CrashLoggerTests"`
Expected: build FAILURE — `CrashLogger` does not exist yet. That is the RED state here.

- [ ] **Step 3: Implement**

```csharp
using System;
using System.IO;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Last-resort fault sink: appends to %AppData%\SmartStudyPlanner\crash.log.
    /// Deliberately NOT a logging framework (structured logging is parked outside Epic 1).
    /// Must never throw — a failing crash logger would mask the original fault.
    /// </summary>
    public static class CrashLogger
    {
        /// <summary>Overridable for tests. Production default: %AppData%\SmartStudyPlanner\crash.log.</summary>
        public static string LogPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartStudyPlanner", "crash.log");

        public static void Log(string context, Exception ex)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath,
                    $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] {context}: {ex}{Environment.NewLine}");
            }
            catch
            {
                // Last line of defense — swallowing here is the point.
            }
        }

        /// <summary>
        /// Observes a fire-and-forget task so faults land in crash.log instead of vanishing.
        /// Uses an always-run continuation (not OnlyOnFaulted, whose continuation cancels on
        /// success and would throw when awaited in tests).
        /// </summary>
        public static Task Observe(Task task, string context)
            => task.ContinueWith(
                t => { if (t.IsFaulted) Log(context, t.Exception!.GetBaseException()); },
                TaskContinuationOptions.ExecuteSynchronously);
    }
}
```

- [ ] **Step 4: Verify green**

Run: `rtk dotnet build SmartStudyPlanner.slnx` then `rtk dotnet test SmartStudyPlanner.slnx --no-build`
Expected: 3 new tests PASS; full suite green (~331 + 5).

- [ ] **Step 5: Commit**

```bash
rtk git add SmartStudyPlanner/Services/CrashLogger.cs SmartStudyPlanner.Tests/Services/CrashLoggerTests.cs
rtk git commit -m "feat(reopen): CrashLogger — minimal last-resort fault sink with observable fire-and-forget"
```

### Task R2-B: global handlers + the 3 F2 sites

**Files:**
- Modify: `SmartStudyPlanner/App.xaml.cs`
- Modify: `SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs:216`
- Modify: `SmartStudyPlanner/ViewModels/WeightOptimizerViewModel.cs:123`

No unit test can live-fire WPF `Application` handlers headlessly — coverage here is
`CrashLoggerTests` (behavior) + PM diff review (wiring) + the owner re-run (real launch).
This residual risk is recorded in the re-closure checklist.

- [ ] **Step 1: Wire global handlers.** In `App.xaml.cs`: add `using System;` to the top of
  the file (it currently fully-qualifies `System.*`), then insert at the very start of
  `OnStartup`, immediately after `base.OnStartup(e);`:

```csharp
// Reopen R2: last-resort crash visibility. A UI-thread exception now shows a dialog and
// keeps the app alive instead of silently killing the process (the B4 failure mode);
// background faults leave a trace in crash.log. Also catches async-void OnStartup faults
// (they post to the Dispatcher), shrinking investigation item 2.8-3 without restructuring.
DispatcherUnhandledException += (_, args) =>
{
    CrashLogger.Log("DispatcherUnhandledException", args.Exception);
    MessageBox.Show(
        "Đã xảy ra lỗi không mong muốn. Thao tác vừa rồi có thể chưa được lưu.\nChi tiết đã ghi vào crash.log.",
        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
    args.Handled = true;
};
AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    CrashLogger.Log("AppDomain.UnhandledException",
        args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString() ?? "unknown"));
TaskScheduler.UnobservedTaskException += (_, args) =>
{
    CrashLogger.Log("TaskScheduler.UnobservedTaskException", args.Exception);
    args.SetObserved();
};
```

- [ ] **Step 2: F2 site 1 — startup maturation.** In the same file, the `MatureAsync`
  `Task.Run` block's `catch` currently swallows silently:

```csharp
catch
{
    // Maturation is an enhancement; never block launch.
}
```

Replace with:

```csharp
catch (Exception ex)
{
    // Maturation is an enhancement; never block launch — but the fault is no longer silent.
    CrashLogger.Log("OutcomeMaturation.MatureAsync", ex);
}
```

(The two ML warm-up `Task.Run` blocks above it keep their silent catches — they are not F2
sites; do not touch them.)

- [ ] **Step 3: F2 site 2 — difficulty label log.** In `QuanLyTaskViewModel.ThemTask()`
  replace:

```csharp
_ = LogDifficultyLabelAsync(loaiTask, doKhoInt, TenTask, savedTask.MaTask);
```

with:

```csharp
CrashLogger.Observe(LogDifficultyLabelAsync(loaiTask, doKhoInt, TenTask, savedTask.MaTask), "DifficultyLabelLog");
```

(`using SmartStudyPlanner.Services;` is already present in this file.)

- [ ] **Step 4: F2 site 3 — weight change log.** In
  `WeightOptimizerViewModel.ApplySuggestion` replace:

```csharp
_ = LogWeightChangeAsync(before, Suggestion);
```

with:

```csharp
CrashLogger.Observe(LogWeightChangeAsync(before, Suggestion), "WeightChangeLog");
```

(`using SmartStudyPlanner.Services;` is already present in this file.)

- [ ] **Step 5: Verify**

Run: `rtk dotnet build SmartStudyPlanner.slnx` then `rtk dotnet test SmartStudyPlanner.slnx --no-build`
Expected: full suite green (~331 + 5); no new tests in this task.

- [ ] **Step 6: Commit**

```bash
rtk git add SmartStudyPlanner/App.xaml.cs SmartStudyPlanner/ViewModels/QuanLyTaskViewModel.cs SmartStudyPlanner/ViewModels/WeightOptimizerViewModel.cs
rtk git commit -m "feat(reopen): global exception handlers + observe the 3 waived fire-and-forget telemetry sites (F2 nuance)"
```

---

## Re-closure gate checklist (after merge to `ui_rf`)

- [ ] PM: `gitnexus_detect_changes()` clean vs plan scope; CHANGELOG row appended.
- [ ] Owner: B1.4 create-task (manual + quick-input) — **no crash, task visible after
  restart under the correct subject**.
- [ ] Owner: retest #2 (heatmap "A" single dim cell) and #3 (Lưu Tiến Trình ×2 dialogs).
- [ ] Owner: formal F2 waiver sign-off (D-R5).
- [ ] Residual risk acknowledged: global handlers are review-verified + owner-launch-verified,
  not unit-live-fired.
- [ ] B4 re-decision recorded in the gate doc; if Release → Phase 3 (C1–C3).

## Acceptance gates

1. Both RED steps failed exactly as predicted before their fix (recorded in agent output).
2. Full suite green at every commit; final count ≈ 336 (331 pre-existing + 5 new).
3. No file outside the agents' Write scopes changed (`gitnexus_detect_changes` + diff review).
4. Owner re-run passes B1.4 + retests #2/#3.

## Out of scope

Everything in the Deferred table; any `StudyTask` ctor signature change; any logging
framework; Epic 2/3 work of any kind (gate Execution Rules remain in force); UI changes.

## Effort

| Who | What | Estimate |
|---|---|---|
| Agent R1 | Slice R1 | 0.5–1 day |
| Agent R2 | Slice R2 | 0.5 day |
| PM | 2 reviews + merge + CHANGELOG | 0.5 day |
| Owner | Re-closure re-run | ~30 min |
