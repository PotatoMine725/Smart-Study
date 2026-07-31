# Post-Epic 1 Engineering Stabilization Plan

**Status:** Approved 2026-07-29 (revised against `docs/reports/2026-07-27-stabilization-plan-architecture-review.md`) · in progress — WP-1 and WP-2 landed; WP-3 unblocked
**Baseline:** `fcd4b42` (tree `d3deca7`) — all line numbers are hints at this commit; see *On line numbers* below
**Lifecycle:** Execution artifact, not living documentation. Superseded when WP-6 lands.
**Successor doc:** the stabilization report (to be written) is the durable record.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the six CSA findings that make Epic 2's assumptions unsound, and land the high-ROI cleanups around them, without touching architecture.

**Architecture:** No architectural change. Every task is a localised edit behind an existing seam — a missing `!IsDeleted` predicate, a missing `try`/`catch`, a ctor default, an optional ctor parameter, a new CI workflow file. The one new abstraction is a persisted device identity, and it wraps the existing `DeviceHelper.GetId()` as its fallback so already-stamped rows keep their identity.

**Tech Stack:** C#/.NET 10 (`net10.0-windows10.0.19041.0`), WPF, EF Core 10 + SQLite, xUnit 2.9.3, GitHub Actions (`windows-latest`).

**Input:** `docs/reports/2026-07-27-epic2-prep-current-state-assessment.md` (commit `fcd4b42`, baseline `d3deca7`) — the sole source of findings, per `Prompt/2026-07-27-post-epic1-cleanup.md`.

**On line numbers.** Every `file.cs:NN` in this document is a navigational hint taken at baseline `fcd4b42`. Where a step also quotes the code, **the quoted snippet is the authoritative anchor** — search for it. If a line number and a snippet disagree, the snippet wins. Steps that insert lines shift every reference below them within the same file.

---

## Context

Epic 1 (sync-ready data model) shipped and released on 2026-07-20. The Current State Assessment at `docs/reports/2026-07-27-epic2-prep-current-state-assessment.md` (commit `fcd4b42`, baseline `d3deca7`) established the technical baseline for Epic 2 planning. It was deliberately descriptive: it created no tasks and sequenced nothing.

This plan is the missing half. It takes the CSA's findings as given — per `Prompt/2026-07-27-post-epic1-cleanup.md`, no re-review, no rediscovery — groups them into work packages, classifies each into exactly one of four categories, and defines an execution order.

**The problem it addresses:** Epic 2 is the LAN-sync epic. Three CSA findings mean Epic 2 would be built on assumptions that are not currently true — soft delete is stamped but not enforced on read, device identity is derived rather than persisted, and there is no CI, so "the suite protects us" is unfounded. A fourth, the demonstrated false green, means the suite's own signal is weaker than its count suggests.

**Intended outcome:** a green, enforced test suite whose assertions mean what they say; tombstones that are actually invisible to readers; a stable device identity; and every remaining finding explicitly classified so nothing is silently carried into Epic 2.

**Scope decided with the owner:** Category A + Category B (6 packages, ~4–6 working days). Categories C and D are documented only — no work items.

---

## Three CSA corrections this plan carries

The CSA is the source of truth, but three of its statements did not survive contact with the files. Recording them here so the plan does not schedule phantom work, and so the CSA can be amended in WP-6.

1. **`Assets/` — Finding 15 and the matching follow-up are wrong.** `SmartStudyPlanner.csproj` sits *inside* `SmartStudyPlanner/`, so its two `Assets\icon.ico` references (`:13` `<ApplicationIcon>`, `:34` `<Resource Include>`) resolve to `SmartStudyPlanner/Assets/icon.ico` — which **is tracked** (`git ls-files` confirms). The untracked directory is the *root* `Assets/`, and nothing in the build references it. The clean clone built because the referenced icon is committed, not in spite of a missing one. Root `Assets/icon.ico` is byte-identical to the tracked copy (md5 `d2bc90edcb01d398ce82ded7ff497177`); the rest of root `Assets/` is unique design source.
2. **§8.4's "11 of 346 cases are pure duplicates — three byte-identical file pairs" does not reproduce.** The only byte-identical `.cs` pairs under `SmartStudyPlanner.Tests/` are generated build artifacts under `obj/` (`.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`, `SmartStudyPlanner.Tests.GlobalUsings.g.cs`, `Verify.Attributes.cs`) — Debug/Release copies of the same generated file. No duplicated *source* test file exists. **No dedup work is scheduled.**
3. **§8.2 anchors the notification timer to `App.xaml.cs`. Wrong file.** `App.xaml.cs` wraps all three of its background `Task.Run` bodies in `try`/`catch` already. The unguarded `async void` is `Views/MainWindow.xaml.cs:96` (`BackgroundTimer_Tick`), with the 1-minute interval at `:87-94`.

---

## Classification summary

Every CSA finding belongs to exactly one category.

| Cat | Meaning | Packages |
|---|---|---|
| **A** | Critical before Epic 2 — sync, persistence, data integrity, correctness, Epic 2 assumptions | WP-1, WP-2, WP-3 |
| **B** | High-value cleanup, good ROI | WP-4, WP-5, WP-6 |
| **C** | Intentional debt — documented, deliberately unchanged | see *Deferred Decisions* |
| **D** | Deferred roadmap — later phases, not scheduled | see *Deferred Decisions* |

---

# Executive Summary

**Overall assessment.** The codebase is in good shape for Epic 2 *planning* — the CSA found no genuine blockers, and the sync data model Epic 2 builds on is the most mature layer in the system. What is missing is not architecture but **enforcement**: three mechanisms that the roadmap and the team already assume exist do not. None is expensive to build.

**Recommended stabilization duration: 4–6 working days.** WP-1 through WP-3 (the Category A core) are ~2.5–3 days; WP-4 through WP-6 add ~1.5–2 days and can be parallelised or dropped without affecting Epic 2 readiness.

**Main risks.**

1. *The false green may be hiding a real defect.* WP-2 Task 2.1 changes a deadline from `DateTime.Now.AddDays(45)` to `FixedNow.AddDays(45)`. The arithmetic says the test should then pass legitimately (`TimeComponent` scores 25/100 at 45 days against a 60-day horizon), but this has never actually been asserted. If it fails, a real priority-band bug has surfaced and WP-2 grows. The plan includes an explicit contingency step.
2. *Fixing the soft-delete leak changes numbers the owner has seen.* `GetForHocKyAsync` is Analytics' only data source. After the fix, the weekly chart, subject insights, productivity score, heatmap, and narrative will all drop tombstoned logs. This is the correct behaviour, but it will look like a regression to anyone comparing against a previous screenshot. Flag it at release, don't treat it as a bug. **Corrected 2026-07-31 (WP-3 execution): two surfaces move, not one.** `GetSnapshotAsync`'s other direct caller is `WeightOptimizerService.SuggestAsync`, so `TotalTaskCount` / `OverdueTaskCount` / `MissRate` / `AverageDelayDays` — and therefore weight suggestions — were also being computed over tombstoned tasks. The release note must cover both.
3. *CI on first run may be red for reasons unrelated to the code.* The suite currently writes to the real `%APPDATA%` and builds the production composition root. Those paths exist on `windows-latest`, so it should pass — but WP-1 lands before WP-2 fixes them, so budget one debugging round.

---

# Work Package Inventory

## WP-1 — CI Gate `[Category A]`

| | |
|---|---|
| **Objective** | Make "the build and tests are green" a fact the repository enforces, not a claim someone made locally. |
| **Findings covered** | §8.4 *No CI*; §10.4 the single sourced plan deviation (`system_roadmap.md:18` defers the test count to CI; `:120` requires green with no enforcing mechanism); §10.5 assumption 5; Key Finding 2 |
| **Dependencies** | None. Goes first. |
| **Effort** | **S** (0.5 day) |
| **Priority** | **Critical** · **Risk if ignored: High** |
| **Reasoning** | Every later package's claim of "still green" is unverifiable without this, and it is the cheapest package in the plan. Doing it first means WP-2 and WP-3 land under enforcement rather than retrofitting it. |
| **Exit criteria** | Entry Criteria #1; the PR check is green *before* merge, and `build-test` is a required status check on `dev`. |
| **Rollback** | Revert the workflow commit / delete `ci.yml`. No code or data touched. |

## WP-2 — Test Trust Restoration `[Category A]`

| | |
|---|---|
| **Objective** | Make a green suite mean what it says: no assertion that passes for the wrong reason, no test that depends on today's date, no test that touches the machine it runs on. |
| **Findings covered** | §8.4 *the false green* + six residual `DateTime.Now` deadlines in `DecisionEngineTests.cs`; tests touching the real user profile (`LocalModelStorageTests.cs:13-14`); a test building the production composition root (`PipelineStageTests.cs:200`); Key Findings 12, 13 |
| **Dependencies** | None functionally; **sequence after WP-1** so the fixes land under enforcement. |
| **Effort** | **M** (1–2 days) |
| **Priority** | **Critical** · **Risk if ignored: High** |
| **Reasoning** | **Task 2.1 is a scheduling gate on WP-3**, not a validation gate: if freezing the drifted deadline surfaces a genuine priority-band defect, Category A scope grows and the owner has to re-decide before WP-3 starts. Tasks 2.2 and 2.3 are grouped here by theme, not by sequence — they gate nothing and may run in parallel with WP-3. One caveat: run **2.3 before 3.2**, because 3.2 makes any test that still resolves through `ServiceLocator` write `device-id.txt` into the real user profile (full reasoning in the WP-3 *Cross-package ordering* row). |
| **Explicitly not in scope** | The 184 repo-wide wall-clock call sites, and the phantom "11 duplicate cases" (correction 2 above). Only `DecisionEngineTests.cs` is de-dated — it is the file with a *proven* drift. |
| **Exit criteria** | Entry Criteria #2, #3. |
| **Rollback** | Revert the three commits. The only production change is one **optional** ctor parameter — revert is clean, the DI registration never moved. |

## WP-3 — Persistence & Sync Identity `[Category A]`

| | |
|---|---|
| **Objective** | Make tombstones invisible to every read path, make device identity stable, and make a `StudyTask` savable without a UI pass. |
| **Findings covered** | §8.1 no global soft-delete filter, two proven leak sites (`SqliteStudyLogRepository.cs:45-48`, `SqliteUserStatsRepository.cs:25`); §8.2 `DeviceId` recomputed, never persisted; §8.2/§9.1 `MucDoCanhBao` NOT NULL but never set by the 4-arg ctor; Key Findings 10, 18 |
| **Dependencies** | **WP-2 Task 2.1 only** — a scheduling gate (if 2.1 surfaces a real defect, this package may be re-scoped). Tasks 2.2/2.3 do not gate it. |
| **Effort** | **M** (1–2 days) |
| **Priority** | **Critical** · **Risk if ignored: High** |
| **Internal order** | 3.1 → 3.2 → 3.3. **3.1 before 3.3 is a hard dependency**, not a preference: Task 3.3's test is appended to the `SoftDeleteReadPathTests` class Task 3.1 creates and reuses its `NewDb()` fixture. 3.2 is genuinely independent of both and may run at any point in the package. Note that 3.1's fixtures set `MucDoCanhBao = "An toàn"` explicitly *because* 3.3 has not landed; that is expected, and 3.3 does not require removing it. |
| **Cross-package ordering** | **Run WP-2 Task 2.3 before Task 3.2.** Not a hard dependency — no conflict, no failure — but 3.2 wires `DeviceIdProvider = deviceIdentity.GetId` into `ServiceLocator.cs:44`, and `DeviceIdentity`'s default directory is the real `%APPDATA%/SmartStudyPlanner`. `PipelineStageTests:200` is the one remaining test that resolves through `ServiceLocator` and so builds that factory. Landing 3.2 while 2.3 is outstanding makes that test write `device-id.txt` into the runner's real profile — reintroducing exactly the defect class WP-2.2 exists to remove. |
| **Reasoning** | Epic 2's raw material is tombstone semantics and peer identity. Shipping sync on top of readers that return tombstoned rows would propagate deleted data to peers. Shipping it on a device ID derived from `Environment.MachineName` means renaming a PC silently creates a second peer identity, and two machines with the same hostname collide. The `MucDoCanhBao` gap belongs here because Epic 2 introduces the first **non-UI write path** — a sync-apply path constructing a `StudyTask` would hit `SQLite Error 19`, and the roadmap has tracked this at `:103-113` while explicitly marking it *not surveyed*. |
| **Exit criteria** | Entry Criteria #4, #5, #6. |
| **Rollback** | Revert the three commits. **3.1** is read-path-only — no schema, no writes, no data migration. **3.2**'s revert leaves `device-id.txt` on disk; harmless on any machine whose hostname has not changed since 3.2 landed, because seeding makes the file's contents identical to `DeviceHelper.GetId()`. **If the hostname *has* changed, delete `device-id.txt` as part of the revert** — the file then holds the old derived ID while `DeviceHelper.GetId()` returns a new one, so reverting would silently switch identity back on a machine whose rows are already stamped both ways. **3.3** is a property default; reverting re-exposes `SQLite Error 19` on non-UI writes only. |

## WP-4 — Scheduling Characterization Tests `[Category B]`

| | |
|---|---|
| **Objective** | Put a behavioural net under `WorkloadServiceImpl.GenerateSchedule` before anyone changes it. |
| **Findings covered** | §8.4 `WorkloadServiceImpl` untested; Key Finding 4 (*"the most behaviour-defining method in scheduling is exercised by nothing"*) |
| **Dependencies** | None |
| **Effort** | **S** (0.5 day) |
| **Priority** | **High** · **Risk if ignored: Medium** |
| **Reasoning** | `GenerateSchedule` is directly constructible with a fake `IDecisionEngine` and the existing `FakeClock`; `capacityHours` is a parameter, so no file I/O is involved. High coverage-per-hour. These are *characterization* tests — they pin current behaviour, they do not assert it is correct. Deadline-blindness (Key Finding 3) is Epic 3/SOE territory and stays untouched. |
| **Exit criteria** | Entry Criteria #7. |
| **Rollback** | Delete the test file. Zero production code touched. |

## WP-5 — Runtime Robustness `[Category B]`

| | |
|---|---|
| **Objective** | Stop two latent runtime faults that surface as user-visible misbehaviour. |
| **Findings covered** | §8.2 notification timer is `async void` with no `try`/`catch` and no toast de-dup (**correct anchor:** `Views/MainWindow.xaml.cs:87-94, 96-129`); §8.2 `capacity.txt` parsed culture-sensitively (`Services/WorkloadServiceImpl.cs:30`, and symmetrically written at `:37`) |
| **Dependencies** | None |
| **Effort** | **S** (0.5 day) |
| **Priority** | **Medium** · **Risk if ignored: Medium** |
| **Reasoning** | Both are small and both are real. An exception in `BackgroundTimer_Tick` is caught by App's `DispatcherUnhandledException` handler — so the app survives, but it shows a modal error dialog **every minute**. The capacity round-trip works today only because read and write are *both* culture-sensitive on the same machine; a hand-edited file, a locale change, or any config sync breaks it silently back to the 3.0 default. Fix must read invariant-first with a current-culture fallback so existing `vi-VN` files ("4,5") still load. |
| **Not in scope** | The three fire-and-forget persistence writes. They are deliberate (see *Deferred Decisions*). |
| **User-visible side effect** | Task 5.1 also raises the timer interval from 1 minute to 5 and adds a 30-minute toast cooldown. Deadline notifications therefore become materially less frequent — the owner *will* notice. Defensible and small, but it is a third change beyond the two fault fixes, and it belongs in the release note. |
| **Exit criteria** | Entry Criteria #11 (*deadline toast fires once*) and #12 (*`capacity.txt` survives a restart*) — the two manual checks, each with a recorded verifier and date. |
| **Rollback** | Revert one commit per task, independently. |

## WP-6 — Repo & Doc Hygiene `[Category B]`

| | |
|---|---|
| **Objective** | Retire the duplicate `Assets/` folder without losing design sources; make the stale numbers true; amend the CSA. |
| **Findings covered** | Follow-up *`Assets/` untracked* (**as corrected above**); §8.5 README "337 tests" vs actual 346; §8.5 roadmap §A.1 / `CLAUDE.md` GitNexus counts; §8.2 `Verify.CommunityToolkit.Mvvm` referenced but unused; §9.1 NU1903/NU1904 advisories (roadmap ledger `:100-102`, ~30 min) |
| **Dependencies** | None, but **run last** — the test count it writes into the README must be the post-WP-2 number. |
| **Effort** | **XS** (<2h) |
| **Priority** | **Medium** · **Risk if ignored: Low** |
| **Reasoning** | Pure hygiene, zero behavioural risk, and it clears the noise that makes `git status` unreadable. |
| **Optional sub-task** | **Task 6.3 (dependency advisory bump) is opt-in, owner's call.** It is the only item in the plan that can break something the suite does not cover (the ML pipeline — its own verification requires a manual *Retrain*), it delivers zero Epic 2 benefit, and it is already on the roadmap's deferred ledger. Time-boxed at 30 minutes with a revert. Skipping it costs nothing. |
| **Exit criteria** | Entry Criteria #8, #9. |
| **Rollback** | Revert per task — **except 6.1, which deletes untracked data** and is therefore guarded by a pre-flight completeness assertion (Step 3b) instead. |

---

# Execution Order

```
WP-1 CI Gate  (S)                        — land via PR, then require the check
   │
   ├─ WP-2.1 False green + de-date  (S)  — the ONLY gate on WP-3
   │     ↓  contingency: if 2.1 fails, a real priority-band defect
   │     ↓  exists — stop and re-scope with the owner before WP-3.
   │  WP-3 Persistence & Sync Identity (M)
   │     3.1 → 3.3 (hard, see WP-3 row);  3.2 independent
   │
   ├─ WP-2.2 / WP-2.3  test isolation    (S)  ┐  disjoint files from
   ├─ WP-4 Scheduling characterization   (S)  ├─ WP-3 and from 2.1;
   └─ WP-5 Runtime robustness            (S)  ┘  safe in parallel
                    ↓
WP-6 Repo & Doc Hygiene  (XS)            — strictly last (needs the final test count)
```

**One soft ordering inside the parallel block:** run **WP-2.3 before WP-3.2** — see the WP-3 *Cross-package ordering* row. It is a preference with a reason, not a constraint.

**Why this order.** WP-1 is first because it is cheap and it converts every later "still green" into an enforced fact rather than a local claim. **WP-2.1 → WP-3 is the plan's only declared dependency, and it is a *scheduling* gate, not a validation gate:** if Task 2.1 surfaces a genuine priority-band defect, Category A scope grows and the owner must re-decide before WP-3 begins. (It is *not* that the false green makes WP-3 unverifiable — the false green sits in the DecisionEngine priority band and has no bearing on whether the Analytics and stats tests that would catch a WP-3.1 regression are trustworthy.) Tasks 2.2 and 2.3 belong to WP-2 by theme only; they gate nothing. WP-4/5/6 depend on nothing and exist to be dropped first if time runs short.

**One dependency deliberately not acted on.** WP-4 would make the Category C item *"two divergent copies of the raw-minutes formula"* safe to deduplicate. That unblocking is recorded, not scheduled — see *Deferred Decisions*.

## Progress

Update this table as packages land; it is the resumption point for a later session.

| WP | Status | Commit(s) | Notes |
|---|---|---|---|
| WP-1 | ◐ workflow landed, **gate not yet enforced** | `f98e4c7` (via PR #50) | CI green on `dev` first try — 0 errors, 346 passed on `windows-latest`. No debugging round needed; the tests that touch `%APPDATA%` and build the composition root passed on a fresh runner. **Step 6 outstanding (owner action):** require `build-test` on `dev` and `main`. Until then WP-1 measures but does not enforce, so it is not complete. |
| WP-2 | ☑ done | `c701527` (2.1), `81f4759` (2.2), `078fdbe` (2.3) | **2.1 passed — no priority-band defect was hiding behind the false green, so WP-3 proceeds as scoped and no re-scoping is needed.** Suite 346 → 348. Entry Criteria #2 and #3 met; `DecisionEngineTests.cs` has zero wall-clock deadlines (verified by the guard *and* independently by `grep`), and no test resolves through `ServiceLocator`. |
| WP-3 | ☑ done | `bbb3c29` (3.1), `6704773` (3.2), `78f16bb` (3.3) | Suite 348 → 355. Entry Criteria #4, #5, #6 met. **Two findings beyond the plan.** (a) `GetSnapshotAsync`'s other direct caller is `WeightOptimizerService.SuggestAsync`, not just Analytics — weight suggestions were also being computed over tombstoned tasks. (b) The plan's 3.2 Step 5 would have reintroduced the WP-2.2 defect from two directions it does not name: `SyncSchema.EnsureColumns` is called by six tests, and `FocusViewModel`'s write site by four. Both now read a provider seam whose default does no I/O; verified by deleting `%APPDATA%/SmartStudyPlanner/device-id.txt` and confirming a full run does not recreate it. |
| WP-4 | ☐ not started | | |
| WP-5 | ☐ not started | | Two manual checks must be recorded, with verifier and date |
| WP-6 | ☐ not started | | Strictly last; 6.3 is opt-in |

---

# File Structure

| File | Responsibility | Package |
|---|---|---|
| **Create** `.github/workflows/ci.yml` | The only CI definition. Restore → build → test on `windows-latest`. | WP-1 |
| **Modify** `SmartStudyPlanner.Tests/Services/DecisionEngineTests.cs` | De-date the file; make the 31–60-day band discriminating. | WP-2 |
| **Modify** `SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs` | Add an optional base-directory ctor param; default unchanged. | WP-2 |
| **Modify** `SmartStudyPlanner.Tests/Services/ML/LocalModelStorageTests.cs` | Point at a temp dir; clean up. | WP-2 |
| **Modify** `SmartStudyPlanner.Tests/Services/Pipeline/PipelineStageTests.cs` | Construct `RiskOrchestrator` directly instead of via `ServiceLocator`. | WP-2 |
| **Modify** `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteStudyLogRepository.cs` | Add the missing `!l.IsDeleted`. | WP-3 |
| **Modify** `.../SqliteUserStatsRepository.cs` | Add the missing `!t.IsDeleted`. | WP-3 |
| **Create** `SmartStudyPlanner/Services/ML/DeviceIdentity.cs` | Persisted device identity; seeded from and falling back to `DeviceHelper.GetId()`. Filed under `Services/ML/` because that is where `DeviceHelper` — the value it seeds from and falls back to — already lives, and because the `Data` → `Services.ML` reference exists today at the stamping seam, so this introduces no new coupling. Relocating it to a sync-specific namespace mid-stabilization would be scope creep; Epic 2 can move it when it owns the surface. | WP-3 |
| **Modify** `SmartStudyPlanner/Data/AppDbContext.cs:15-19, 103, 109` | Add a `DeviceIdProvider` seam mirroring the existing `Clock` seam; the single stamping seam reads from it. | WP-3 |
| **Modify** `SmartStudyPlanner/Services/ServiceLocator.cs:44` | Composition root supplies `DeviceIdentity.GetId` to the context factory. | WP-3 |
| **Modify** `SmartStudyPlanner/Models/StudyTask.cs` | Default `MucDoCanhBao` so the NOT NULL column is never null. | WP-3 |
| **Create** `SmartStudyPlanner.Tests/Infrastructure/Persistence/SoftDeleteReadPathTests.cs` | One regression test per fixed read path. | WP-3 |
| **Create** `SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs` | Characterization of `GenerateSchedule`. | WP-4 |
| **Modify** `SmartStudyPlanner/Views/MainWindow.xaml.cs` | Guard the timer tick; de-dup the toast. | WP-5 |
| **Modify** `SmartStudyPlanner/Services/WorkloadServiceImpl.cs` | Culture-invariant capacity round-trip. | WP-5 |
| **Delete** root `Assets/` · **Create** `docs/assets/icon-source/` | Retire the duplicate, preserve the sources. | WP-6 |
| **Modify** `README.md`, `docs/specs/system_roadmap.md`, `CLAUDE.md`, `docs/reports/2026-07-27-…-current-state-assessment.md`, `SmartStudyPlanner/SmartStudyPlanner.csproj` | Stale numbers, CSA corrections, unused package ref. | WP-6 |

---

# Tasks

## WP-1 — CI Gate

### Task 1.1: Add the CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

The solution is `SmartStudyPlanner.slnx` (XML solution format, 2 projects — `SmartStudyPlanner` and `SmartStudyPlanner.Tests`). `tools/TextClassifierEval/TextClassifierEval.csproj` is **not** in the solution; do not add it. Local SDK is 10.0.301.

- [ ] **Step 1: Confirm the solution builds and tests from a clean state locally**

```bash
rtk dotnet restore SmartStudyPlanner.slnx
rtk dotnet build SmartStudyPlanner.slnx --no-restore --configuration Release
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --no-build --configuration Release
```

Expected: 0 errors; `Passed! - Failed: 0, Passed: 346`. If Release differs from Debug, resolve that before writing the workflow — CI will use Release.

- [ ] **Step 2: Write the workflow**

```yaml
name: CI

on:
  push:
    branches: [main, dev]
  pull_request:
    branches: [main, dev]
  workflow_dispatch:

jobs:
  build-test:
    name: Build & Test (Windows)
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore SmartStudyPlanner.slnx

      - name: Build
        run: dotnet build SmartStudyPlanner.slnx --no-restore --configuration Release

      - name: Test
        run: >
          dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
          --no-build
          --configuration Release
          --logger "trx;LogFileName=test-results.trx"
          --results-directory ${{ github.workspace }}/TestResults

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: TestResults/*.trx
```

`windows-latest` is required — the target framework is `net10.0-windows10.0.19041.0` with `UseWPF` and `UseWindowsForms`. Do not attempt `ubuntu-latest`.

- [ ] **Step 3: Verify the file is actually tracked**

`.github/workflows/` currently exists on disk but is empty and entirely untracked. `.gitignore` does not ignore it, so this is just an artifact of nothing ever having been committed there.

Run: `rtk git status --short .github`
Expected: `?? .github/workflows/ci.yml`

- [ ] **Step 4: Commit on a branch and open a PR — do not push straight to `dev`**

The workflow already carries `on: pull_request`, so a PR runs the gate *before* the merge. That matters: Step 5 anticipates a red first run caused by the very tests WP-2 fixes, and pushing that to `dev` would leave it red for the 1–2 days WP-2 and WP-3 take — a normally-red signal is worse than none. On a branch, the same red is just an iteration. Matches repo convention (PR #49).

```bash
rtk git checkout -b ci/build-test-workflow
rtk git add .github/workflows/ci.yml
rtk git commit -m "ci: add build + test workflow on windows-latest

The roadmap assumes CI exists (system_roadmap.md:18 defers the test count
to it, invariant :120 requires green) but none has ever run. Every green
build in this repo's history has been local and manual."
rtk git push -u origin ci/build-test-workflow
rtk gh pr create --base dev --fill
```

- [ ] **Step 5: Iterate on the branch until the PR check is green, then merge**

Run: `rtk gh pr checks`
Expected: `build-test` passes.

If it is red, read the failure before changing anything else — the likely causes are (a) `.slnx` support in the installed SDK, (b) a test that behaves differently on a fresh runner profile. Cause (b) is exactly what WP-2 fixes. **If that is the failure, do not patch CI around it** — either fix the workflow if the fault is in the workflow, or land WP-2's relevant task on this same branch so the PR merges green. Merge only once the check passes.

- [ ] **Step 6: OWNER ACTION — make the check required**

**This is not an agent action.** Enabling branch protection needs admin scope on the repository and cannot be done from the CLI without it.

In *Settings → Branches*, add a rule for `dev` and for `main` with **Require status checks to pass before merging** → select `build-test`.

Until this is done, WP-1 has delivered a workflow that *measures*, not one that *enforces* — and enforcement is the package's stated objective. Flag it to the owner explicitly rather than marking WP-1 complete without it.

---

## WP-2 — Test Trust Restoration

### Task 2.1: Fix the false green and de-date `DecisionEngineTests`

**Files:**
- Modify: `SmartStudyPlanner.Tests/Services/DecisionEngineTests.cs`

**The defect.** `CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0` (`:87-96`) builds `DateTime.Now.AddDays(45)` against a clock frozen at `FixedNow = 2026-04-11`. Today that deadline is ≈152 days out, so `BeyondHorizonRule` short-circuits (`daysLeft > cfg.HorizonDays`, default 60) and `PriorityCalculator.Calculate` returns `1.0` immediately (`PriorityCalculator.cs:37-41`). `Assert.True(score > 0.0)` passes — but on the horizon sentinel, not on the 31–60-day band, which is now asserted nowhere.

Seven `DateTime.Now` deadline constructions exist in this file at baseline (`:37, 78, 92, 110, 121, 134, 149`). Step 1 consumes `:92`; the remaining six are Step 3's job.

- [ ] **Step 1: Make the failing test — replace the trivially-true assertion with a discriminating one**

Replace lines `87-96` entirely:

```csharp
        [Fact]
        public void CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Toán", 3);

            // 45 ngày: trong horizon (mặc định 60) nên phải đi qua component pipeline.
            var task = new StudyTask("Bài tập 45 ngày", FixedNow.AddDays(45), LoaiCongViec.BaiTapVeNha, 3);
            // 90 ngày: vượt horizon -> BeyondHorizonRule short-circuit, trả đúng sentinel 1.0.
            var beyondHorizon = new StudyTask("Bài tập 90 ngày", FixedNow.AddDays(90), LoaiCongViec.BaiTapVeNha, 3);
            // 10 ngày: gần hơn -> TimeComponent cao hơn, nên điểm phải lớn hơn mốc 45 ngày.
            var nearer = new StudyTask("Bài tập 10 ngày", FixedNow.AddDays(10), LoaiCongViec.BaiTapVeNha, 3);

            double score = sut.CalculatePriority(task, monHoc);
            double beyondScore = sut.CalculatePriority(beyondHorizon, monHoc);
            double nearerScore = sut.CalculatePriority(nearer, monHoc);

            Assert.Equal(1.0, beyondScore);          // sentinel, không phải điểm thật
            Assert.True(score > beyondScore);        // 45 ngày KHÔNG được rơi vào nhánh horizon
            Assert.True(score < nearerScore);        // và phải xếp dưới mốc gần hơn
        }
```

This cannot pass via `BeyondHorizonRule`: the sentinel is asserted separately and the band score must strictly exceed it.

- [ ] **Step 2: Run it and read the result carefully**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~CalculatePriority_TaskTrongVung31Den60Ngay"`

Expected: **PASS.** The arithmetic: at 45 days with `HorizonDays = 60`, `TimeComponent.Score` is `100 * (1 - 45/60) = 25` (`IPriorityComponent.cs:23`), and `TaskTypeComponent` / `CreditComponent` / `DifficultyComponent` all contribute independently of `daysLeft`, so the weighted total lands well above `1.0`.

**Contingency — if it FAILS:** a real defect in the 31–60-day band has surfaced. **Stop. Do not "fix" the test to make it pass.** Record the actual score values, report to the owner, and treat it as a new Category A finding. That is the honest outcome of removing a false green, and it is why this task is sequenced before WP-3.

- [ ] **Step 3: De-date the remaining six deadlines in this file**

Replace every remaining `DateTime.Now` in this file with `FixedNow`, preserving any `.AddDays(n)` suffix. At baseline there are exactly six (`:37, 78, 110, 121, 134, 149`; `:92` was consumed by Step 1, and Step 1's replacement has already shifted these by roughly eleven lines — search for the text, do not count lines). If the count differs, the file has moved on from the baseline: replace all of them and note it in the commit body. The Step-5 guard test is the check.

- [ ] **Step 4: Add a guard so this cannot regress**

Append inside the class, after the last test:

**The needle must be built by concatenation, and the comment must not spell it out.** A guard that reads its own source cannot contain the literal it forbids — otherwise it matches itself and fails permanently. Both the comment and the assertion below are written accordingly; do not "simplify" either back to a literal.

```csharp
        // Chốt chặn: file này phải hoàn toàn tất định. Bất kỳ deadline nào dựng từ
        // wall clock đều trôi và sớm muộn sẽ pass/fail vì lý do sai
        // (xem CSA 2026-07-27, §8.4 "the false green").
        [Fact]
        public void TestFile_KhongDungWallClock()
        {
            // Ghép chuỗi để chính dòng assert này không tự trở thành một match.
            var needle = "DateTime" + "." + "Now";
            var source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(TestSourceRoot(), "Services", "DecisionEngineTests.cs"));

            Assert.DoesNotContain(needle, source);
        }

        private static string TestSourceRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !System.IO.File.Exists(
                       System.IO.Path.Combine(dir.FullName, "SmartStudyPlanner.Tests.csproj")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return dir!.FullName;
        }
```

- [ ] **Step 5: Run the whole file**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~DecisionEngineTests"`
Expected: all pass, including `TestFile_KhongDungWallClock`.

- [ ] **Step 6: Commit**

```bash
rtk git add SmartStudyPlanner.Tests/Services/DecisionEngineTests.cs
rtk git commit -m "test(decision): remove false green in the 31-60 day priority band

CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0 built its deadline from
DateTime.Now against a clock frozen at 2026-04-11. The deadline had drifted
to ~152 days, so BeyondHorizonRule short-circuited and the assertion passed
on the 1.0 sentinel — the band itself was asserted nowhere. Now asserts the
band strictly between the horizon sentinel and a nearer task, and the file
is fully deterministic."
```

---

### Task 2.2: Stop tests writing to the real user profile

**Files:**
- Modify: `SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs`
- Modify: `SmartStudyPlanner.Tests/Services/ML/LocalModelStorageTests.cs`

`LocalModelStorageTests.CreatesModelDirectory` constructs `new LocalModelStorageProvider()`, whose ctor unconditionally does `Directory.CreateDirectory(%APPDATA%/SmartStudyPlanner/models)` — the developer's real profile, with no temp dir and no cleanup.

- [ ] **Step 1: Write the failing test**

Replace `SmartStudyPlanner.Tests/Services/ML/LocalModelStorageTests.cs` entirely:

```csharp
using System;
using System.IO;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.ML
{
    public class LocalModelStorageTests : IDisposable
    {
        private readonly string _tempRoot =
            Path.Combine(Path.GetTempPath(), "ssp-modelstore-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void CreatesModelDirectory()
        {
            var provider = new LocalModelStorageProvider(_tempRoot);

            Assert.True(Directory.Exists(provider.BaseDirectory));
            Assert.Equal(_tempRoot, provider.BaseDirectory);
        }

        [Fact]
        public void DefaultBaseDirectory_TroToProfileAppData()
        {
            // Không construct provider mặc định ở đây — chỉ khẳng định hình dạng đường dẫn,
            // để test không bao giờ ghi vào profile thật của người chạy.
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner", "models");

            Assert.Equal(expected, LocalModelStorageProvider.DefaultBaseDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~LocalModelStorageTests"`
Expected: **compile error** — `LocalModelStorageProvider` has no ctor taking a string, and no `DefaultBaseDirectory`.

- [ ] **Step 3: Add the seam — production default unchanged**

In `SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs`, replace lines `12-16`:

```csharp
        public static string DefaultBaseDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartStudyPlanner", "models");

        // baseDirectory chỉ để test trỏ vào thư mục tạm; production luôn dùng default,
        // nên DI registration (ServiceLocator.cs:72) không phải đổi.
        public LocalModelStorageProvider(string? baseDirectory = null)
        {
            BaseDirectory = baseDirectory ?? DefaultBaseDirectory;
            Directory.CreateDirectory(BaseDirectory);
        }
```

`services.AddSingleton<IModelStorageProvider, LocalModelStorageProvider>()` at `ServiceLocator.cs:72` keeps working — the parameter is optional.

- [ ] **Step 4: Run to verify it passes**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~LocalModelStorageTests"`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
rtk git add SmartStudyPlanner/Services/ML/LocalModelStorageProvider.cs SmartStudyPlanner.Tests/Services/ML/LocalModelStorageTests.cs
rtk git commit -m "test(ml): stop LocalModelStorageTests writing to the real user profile

Optional baseDirectory ctor param; production default and the DI
registration are unchanged."
```

---

### Task 2.3: Stop a test building the production composition root

**Files:**
- Modify: `SmartStudyPlanner.Tests/Services/Pipeline/PipelineStageTests.cs:200`

`AssessRiskStage_SetsTaskId_OnEveryAssessment` calls `ServiceLocator.Get<IRiskAnalyzer>()`, which triggers `ServiceLocator.Configure()` and builds the entire production container — real app DB, real streak JSON, real `%LOCALAPPDATA%` weight config, real model paths — to obtain one object that takes two trivial dependencies.

`ServiceLocator.cs:70` registers `IRiskAnalyzer → RiskOrchestrator`, whose ctor is `RiskOrchestrator(IDecisionEngine decisionEngine, IClock clock)`.

- [ ] **Step 1: Replace the resolution with direct construction**

Replace line `200`:

```csharp
            var riskAnalyzer = ServiceLocator.Get<IRiskAnalyzer>();
```

with:

```csharp
            // Dựng thẳng RiskOrchestrator thay vì ServiceLocator.Get: resolve qua container
            // sẽ build cả composition root production (DB thật, weight config thật ở
            // %LOCALAPPDATA%, model path thật) chỉ để lấy 1 object 2 dependency.
            var clock = new FakeClock(start);
            var riskAnalyzer = new RiskOrchestrator(
                new DecisionEngineService(
                    new DefaultTaskTypeWeightProvider(),
                    clock,
                    new NullStudyTimePredictorForRisk()),
                clock);
```

Add a local double inside the test class (the file already uses hand-written doubles — no mocking library is used anywhere in this repo):

```csharp
        private sealed class NullStudyTimePredictorForRisk : IStudyTimePredictor
        {
            public bool IsReady => false;
            public Task<StudyTimePredictionResult> PredictAsync(
                StudyTask task, MonHoc monHoc, CancellationToken ct = default)
                => Task.FromResult(new StudyTimePredictionResult(0, false, 0f));
        }
```

Add whichever of these `using` directives the file does not already have: `SmartStudyPlanner.Core.Risk`, `SmartStudyPlanner.Services`, `SmartStudyPlanner.Services.ML`, `SmartStudyPlanner.Services.Strategies`, `SmartStudyPlanner.Tests.TestDoubles`, `System.Threading`, `System.Threading.Tasks`.

- [ ] **Step 2: Run to verify it passes**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~PipelineStageTests"`
Expected: all pass. The test's own assertions are unchanged; only how it obtains `IRiskAnalyzer` changed.

- [ ] **Step 3: Check for other `ServiceLocator` uses in the test project**

Run: `rtk grep -n "ServiceLocator" SmartStudyPlanner.Tests`

If more appear, fix any that are equally trivial to construct directly and **leave the rest** — this task's scope is the one site the CSA named. Note any survivors in the commit body.

- [ ] **Step 4: Run the full suite and record the count**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj`
Expected: `Failed: 0`. Write down the total — WP-6 puts it in the README.

- [ ] **Step 5: Commit**

```bash
rtk git add SmartStudyPlanner.Tests/Services/Pipeline/PipelineStageTests.cs
rtk git commit -m "test(pipeline): construct RiskOrchestrator directly instead of via ServiceLocator

Resolving IRiskAnalyzer from the container built the whole production
composition root (real DB, real weight config, real model paths) for one
object with two dependencies."
```

---

## WP-3 — Persistence & Sync Identity

### Task 3.1: Close the two soft-delete read leaks

**Files:**
- Modify: `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteStudyLogRepository.cs:45-48`
- Modify: `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteUserStatsRepository.cs:25`
- Create: `SmartStudyPlanner.Tests/Infrastructure/Persistence/SoftDeleteReadPathTests.cs`

There is no global query filter; each read path filters individually. A full sweep of `SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/` confirms **exactly two** read paths miss it — the CSA's count is correct. `SqliteHocKyRepository.cs:98` and `SqliteStudyTaskRepository.cs:56` also lack the predicate but are **write/upsert paths that must see tombstoned rows**; do not touch them.

`GetForHocKyAsync` is Analytics' only data source (`AnalyticsViewModel.cs:87`), so this fix changes the weekly chart, subject insights, productivity score, heatmap, and narrative. That is the point — but announce it, because it will look like numbers moved.

- [ ] **Step 1: Write the failing tests**

Create `SmartStudyPlanner.Tests/Infrastructure/Persistence/SoftDeleteReadPathTests.cs`. `RepositoriesTests.NewDb()` is `private static` (`RepositoriesTests.cs:30-37`) and therefore not reachable — duplicate it verbatim, as reproduced below. It builds a real in-memory SQLite database via `TestDb.Create(conn)` from `SmartStudyPlanner.Tests.Fixtures`, keeping one connection open so data persists across contexts. `MucDoCanhBao` must be set explicitly on any `StudyTask` you persist **until Task 3.3 lands**.

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence
{
    public class SoftDeleteReadPathTests
    {
        // Sao nguyên từ RepositoriesTests.cs:30-37 — helper ở đó là private static.
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated done */ }
            return (conn, () => TestDb.Create(conn));
        }

        [Fact]
        public async Task GetForHocKyAsync_BoQuaLogDaTombstone()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            var hocKy  = new HocKy("HK Soft", DateTime.Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task   = new StudyTask("Bài 1", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
                MaMonHoc = monHoc.MaMonHoc,
            };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await new SqliteHocKyRepository(factory).LuuHocKyAsync(hocKy);

            var logs = new SqliteStudyLogRepository(factory);
            await logs.AddAsync(new StudyLog { MaTask = task.MaTask, NgayHoc = DateTime.Today, SoPhutHoc = 60 });
            await logs.AddAsync(new StudyLog
            {
                MaTask = task.MaTask, NgayHoc = DateTime.Today, SoPhutHoc = 999,
                IsDeleted = true, DeletedAtUtc = DateTime.UtcNow,
            });

            var result = await logs.GetForHocKyAsync(hocKy);

            Assert.Single(result);
            Assert.Equal(60, result[0].SoPhutHoc);
            Assert.DoesNotContain(result, l => l.IsDeleted);
        }

        [Fact]
        public async Task GetSnapshotAsync_KhongDemTaskDaTombstone()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            var hocKy  = new HocKy("HK Stats", DateTime.Today);
            var monHoc = new MonHoc("Lý", 3) { MaHocKy = hocKy.MaHocKy };
            var alive = new StudyTask("Còn sống", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn", MaMonHoc = monHoc.MaMonHoc,
            };
            var dead = new StudyTask("Đã xoá", DateTime.Today.AddDays(-5), LoaiCongViec.ThiCuoiKy, 5)
            {
                MucDoCanhBao = "An toàn", MaMonHoc = monHoc.MaMonHoc,
                IsDeleted = true, DeletedAtUtc = DateTime.UtcNow,
            };
            monHoc.DanhSachTask.Add(alive);
            monHoc.DanhSachTask.Add(dead);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await new SqliteHocKyRepository(factory).LuuHocKyAsync(hocKy);

            var snapshot = await new SqliteUserStatsRepository(factory)
                .GetSnapshotAsync(DateTime.UtcNow);

            // Task đã tombstone không được đếm vào tổng, và cũng không được tính là quá hạn.
            Assert.Equal(1, snapshot.TotalTaskCount);
            Assert.Equal(0, snapshot.OverdueTaskCount);
        }
    }
}
```

This mirrors the production namespace 1:1 and keeps its fixture inline, per the repo's test-structure convention.

- [ ] **Step 2: Run to verify both fail**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~SoftDeleteReadPathTests"`
Expected: **2 failed.** `GetForHocKyAsync` returns 2 logs (expected 1); `TotalTaskCount` is 2 (expected 1) and `OverdueTaskCount` is 1 (expected 0).

- [ ] **Step 3: Fix `SqliteStudyLogRepository.GetForHocKyAsync`**

At `SqliteStudyLogRepository.cs:46`, change:

```csharp
                .Where(l => taskIds.Contains(l.MaTask))
```

to:

```csharp
                .Where(l => taskIds.Contains(l.MaTask) && !l.IsDeleted)
```

The file's other three methods (`GetByTaskAsync:33`, `GetSinceAsync:55`) already carry the predicate — this was the outlier.

- [ ] **Step 4: Fix `SqliteUserStatsRepository.GetSnapshotAsync`**

At `SqliteUserStatsRepository.cs:25`, change:

```csharp
            var tasks = await db.StudyTasks.AsNoTracking().ToListAsync(ct);
```

to:

```csharp
            var tasks = await db.StudyTasks.AsNoTracking().Where(t => !t.IsDeleted).ToListAsync(ct);
```

The two `db.StudyLogs` queries in this file (`:43`, `:47`) already filter correctly.

- [ ] **Step 5: Run to verify both pass**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~SoftDeleteReadPathTests"`
Expected: 2 passed.

- [ ] **Step 6: Run the full suite**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj`
Expected: `Failed: 0`. If an existing Analytics or stats test now fails, it was asserting on tombstoned rows — read it, and fix the *test's* fixture rather than reverting the repository.

- [ ] **Step 7: Commit**

```bash
rtk git add SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteStudyLogRepository.cs SmartStudyPlanner/Infrastructure/Persistence/SQLite/Repositories/SqliteUserStatsRepository.cs SmartStudyPlanner.Tests/Infrastructure/Persistence/SoftDeleteReadPathTests.cs
rtk git commit -m "fix(persistence): enforce soft delete on the two leaking read paths

GetForHocKyAsync is Analytics' only data source, so tombstoned study logs
were feeding the weekly chart, subject insights, productivity score,
heatmap and narrative. GetSnapshotAsync counted tombstoned tasks in both
the total and the overdue tally.

A full sweep of the repository folder confirms these were the only two
read paths missing the predicate; SqliteHocKyRepository:98 and
SqliteStudyTaskRepository:56 omit it deliberately (write/upsert paths that
must see tombstoned rows)."
```

---

### Task 3.2: Persist the device identity

**Files:**
- Create: `SmartStudyPlanner/Services/ML/DeviceIdentity.cs`
- Modify: call sites of `DeviceHelper.GetId()` — `Data/SyncSchema.cs:56`, `ViewModels/FocusViewModel.cs:158`, `Services/ML/MLModelManager.cs:124`, `Services/ML/TextClassifierModelManager.cs:171`
- Create: `SmartStudyPlanner.Tests/Services/DeviceIdentityTests.cs`

`DeviceHelper.GetId()` returns `"desktop-" + SHA256(Environment.MachineName)[..8]`. It is deterministic, so it is stable *as long as the hostname never changes* — and it collides for any two machines sharing a hostname (cloned VMs, factory defaults).

**Backward compatibility is mandatory.** Rows already stamped with the derived ID exist in every user's DB. The persisted identity is therefore *seeded from* `DeviceHelper.GetId()` on first run — never randomised — so existing installs keep the identity their rows already carry.

**Be precise about what this fixes.** Seeding from the derived value is what makes the change safe, and it is also what limits it:

| Failure mode | Fixed here? |
|---|---|
| User renames their PC → identity silently changes → the device looks like a new peer to its own history | **Yes.** After first run the ID comes from the file, not from `Environment.MachineName`. |
| Two machines share a hostname → identical derived IDs → they collide as peers | **No.** Both seed to the same value and persist it. |

The collision case cannot be fixed by seeding — randomising instead would fix it and break every existing install's row history, which is the worse trade. Epic 2 needs duplicate-peer-ID detection at handshake regardless; that is where collision belongs. **Do not claim collision is fixed in the commit message**, and carry it into *Follow-ups* as an open Epic 2 concern.

- [ ] **Step 1: Write the failing tests**

Create `SmartStudyPlanner.Tests/Services/DeviceIdentityTests.cs`:

```csharp
using System;
using System.IO;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    public class DeviceIdentityTests : IDisposable
    {
        private readonly string _dir =
            Path.Combine(Path.GetTempPath(), "ssp-devid-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void LanDauTien_SeedTuDeviceHelper_VaGhiXuongFile()
        {
            var id = new DeviceIdentity(_dir).GetId();

            // Install cũ đã stamp hàng loạt row bằng giá trị dẫn xuất; seed phải trùng
            // để những row đó không đột nhiên thuộc về một "peer" khác.
            Assert.Equal(DeviceHelper.GetId(), id);

            // Và phải THỰC SỰ persist — nếu chỉ trả về giá trị dẫn xuất mà không ghi file
            // thì assert ở trên vẫn xanh trong khi class này chẳng làm gì cả.
            var file = Path.Combine(_dir, "device-id.txt");
            Assert.True(File.Exists(file));
            Assert.Equal(id, File.ReadAllText(file).Trim());
        }

        [Fact]
        public void LanThuHai_DocLaiTuFile_KhongPhuThuocMachineName()
        {
            var first = new DeviceIdentity(_dir).GetId();

            // Ghi đè bằng giá trị khác để chứng minh lần đọc sau lấy từ file,
            // không tính lại từ Environment.MachineName.
            File.WriteAllText(Path.Combine(_dir, "device-id.txt"), "desktop-deadbeef");

            Assert.Equal("desktop-deadbeef", new DeviceIdentity(_dir).GetId());
            Assert.NotEqual(first, new DeviceIdentity(_dir).GetId());
        }

        [Fact]
        public void FileRong_ThiSeedLai()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "device-id.txt"), "   ");

            Assert.Equal(DeviceHelper.GetId(), new DeviceIdentity(_dir).GetId());
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~DeviceIdentityTests"`
Expected: **compile error** — `DeviceIdentity` does not exist. `DeviceHelper` is `internal static`, so it is visible to the test project only if `InternalsVisibleTo` is already configured; if the error is *"DeviceHelper is inaccessible"*, change `internal static class DeviceHelper` to `public static class DeviceHelper` in `Services/ML/DeviceHelper.cs` rather than adding an `InternalsVisibleTo` attribute.

- [ ] **Step 3: Implement**

Create `SmartStudyPlanner/Services/ML/DeviceIdentity.cs`:

```csharp
using System;
using System.IO;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Danh tính thiết bị bền vững, dùng cho D-I sync metadata (ModifiedByDeviceId).
    ///
    /// DeviceHelper.GetId() dẫn xuất từ Environment.MachineName: ổn định chừng nào
    /// hostname không đổi, và trùng nhau giữa 2 máy cùng hostname. Với LAN sync thì
    /// cả hai đều không chấp nhận được.
    ///
    /// Lần đầu chạy, giá trị được SEED từ DeviceHelper.GetId() — không random — để
    /// những row đã stamp trong DB của install cũ vẫn thuộc về đúng thiết bị này.
    /// </summary>
    public sealed class DeviceIdentity
    {
        private const string FileName = "device-id.txt";

        private readonly string _directory;
        private string? _cached;

        public DeviceIdentity(string? directory = null)
        {
            _directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner");
        }

        public string GetId()
        {
            if (!string.IsNullOrWhiteSpace(_cached)) return _cached!;

            var path = Path.Combine(_directory, FileName);

            try
            {
                if (File.Exists(path))
                {
                    var existing = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(existing))
                        return _cached = existing;
                }

                var seeded = DeviceHelper.GetId();
                Directory.CreateDirectory(_directory);
                File.WriteAllText(path, seeded);
                return _cached = seeded;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Không đọc/ghi được thì vẫn phải trả về danh tính dùng được — hành vi
                // suy biến đúng bằng hành vi cũ, không bao giờ chặn app.
                return _cached = DeviceHelper.GetId();
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~DeviceIdentityTests"`
Expected: 3 passed.

- [ ] **Step 5: Route the single stamping seam through it**

**This is the step that decides whether the package achieves anything.** The identity that actually lands in `ModifiedByDeviceId` on every write comes from `AppDbContext`, which calls `SyncStamper.Apply(ChangeTracker, Clock, DeviceHelper.GetId())` at **both** `AppDbContext.cs:103` (`SaveChanges`) and `:109` (`SaveChangesAsync`). Those two lines are the single stamping seam all 12 write sites pass through.

`AppDbContext` is **parameterless-constructed**, not DI-resolved, so it cannot take `DeviceIdentity` as a dependency. The file already solved this exact problem for the clock — `AppDbContext.cs:15-19`:

```csharp
        // Sync-metadata stamping clock seam — settable for deterministic tests, mirrors the
        // repo's existing FakeClock convention. Not IClock/DI: AppDbContext is parameterless-
        // constructed (see DeviceHelper reuse below), so a plain delegate keeps Data decoupled
        // from Services.Strategies.
        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;
```

Mirror it. Add directly beneath that property:

```csharp
        // Cùng kiểu seam với Clock: composition root gán DeviceIdentity.GetId vào đây.
        // Default vẫn là DeviceHelper.GetId (thuần tính toán, không I/O) nên test và
        // bootstrap dựng AppDbContext trực tiếp không đụng vào %APPDATA%.
        public Func<string> DeviceIdProvider { get; set; } = () => DeviceHelper.GetId();
```

Then replace `DeviceHelper.GetId()` with `DeviceIdProvider()` at **both** `SyncStamper.Apply(...)` call sites (`SaveChanges` and `SaveChangesAsync`; `:103` and `:109` at baseline). Their line numbers shift once the `DeviceIdProvider` property above is inserted, so **search for the `DeviceHelper.GetId()` argument rather than counting lines**:

```csharp
            SyncStamper.Apply(ChangeTracker, Clock, DeviceIdProvider());
```

Wire it at the composition root. `SmartStudyPlanner/Services/ServiceLocator.cs:44` is the one factory every repository receives:

```csharp
            Func<AppDbContext> ctxFactory = () => new AppDbContext();
```

becomes:

```csharp
            var deviceIdentity = new DeviceIdentity();
            Func<AppDbContext> ctxFactory = () => new AppDbContext { DeviceIdProvider = deviceIdentity.GetId };
```

`DeviceIdentity` caches after the first read, so this costs one file read per process.

Also update the two direct-consumer call sites:

- `Data/SyncSchema.cs:56` — the one-time backfill that stamps `ModifiedByDeviceId` on legacy NULL rows. It runs during startup before DI is configured, so give it its own `new DeviceIdentity().GetId()`.
- `ViewModels/FocusViewModel.cs:158` — `StudyLog.DeviceId`.

**Leave `Services/ML/MLModelManager.cs:124` and `Services/ML/TextClassifierModelManager.cs:171` alone.** Those write `ModelMeta.DeviceId` — model provenance, not sync identity. Changing them is scope creep the CSA does not ask for.

`DeviceHelper` stays; it is now the seed function and the default, not the live source.

- [ ] **Step 6: Run the full suite**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj`
Expected: `Failed: 0`. Pay attention to `SyncMetadataStampingTests` and `SyncSchemaDualPathTests` — if either asserts on a literal derived ID, update the assertion to compare against `DeviceIdentity`.

- [ ] **Step 7: Commit**

```bash
rtk git add SmartStudyPlanner/Services/ML/DeviceIdentity.cs SmartStudyPlanner/Data/AppDbContext.cs SmartStudyPlanner/Services/ServiceLocator.cs SmartStudyPlanner/Data/SyncSchema.cs SmartStudyPlanner/ViewModels/FocusViewModel.cs SmartStudyPlanner.Tests/Services/DeviceIdentityTests.cs
rtk git commit -m "feat(sync): persist device identity instead of deriving it per write

DeviceHelper.GetId() hashes Environment.MachineName, so renaming a PC
silently changed its sync identity and made the device look like a new
peer to its own row history. The ID is now read from a file under the
app data directory.

Seeded from DeviceHelper.GetId() on first run rather than randomised, so
rows already stamped in existing installs keep the identity they carry.

Wired through AppDbContext.DeviceIdProvider, mirroring the existing Clock
seam — AppDbContext is parameterless-constructed, so the default stays
DeviceHelper.GetId (no I/O) and only the composition root swaps it.

Does NOT address hostname collision: two machines sharing a hostname seed
to the same value. That needs duplicate-peer detection at handshake and
belongs to Epic 2. Model-provenance call sites (ModelMeta.DeviceId) are
unchanged."
```

---

### Task 3.3: Close the `MucDoCanhBao` constructor gap

**Files:**
- Modify: `SmartStudyPlanner/Models/StudyTask.cs:27`
- Modify: `SmartStudyPlanner.Tests/Infrastructure/Persistence/SoftDeleteReadPathTests.cs` (add one test to the class created in Task 3.1)

`MucDoCanhBao` is `string` (non-nullable, so EF emits NOT NULL) but the 4-arg ctor at `:45-53` never sets it. Every current save path goes through `QuanLyTaskViewModel.TinhDiemVaSapXep()` (`:107-110`), which stamps it. Any path that does not — and Epic 2's sync-apply path will be exactly that — hits `SQLite Error 19: NOT NULL constraint failed`.

`RepositoriesTests.cs:577-583` already documents this in a comment and works around it. Tracked on the roadmap at `:103-113`, explicitly marked *not surveyed*.

The four values `TinhDiemVaSapXep` produces are `"Đã xong"`, `"Khẩn cấp"`, `"Chú ý"`, `"An toàn"`. `"An toàn"` is the correct default: it is what the VM assigns to a task with `DiemUuTien` below 50, and a brand-new task has `DiemUuTien == 0`.

- [ ] **Step 1: Write the failing test**

```csharp
        [Fact]
        public async Task TaskDungTuCtor_LuuDuocMaKhongCanUIStampMucDoCanhBao()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            var hocKy  = new HocKy("HK Ctor", DateTime.Today);
            var monHoc = new MonHoc("Hoá", 3) { MaHocKy = hocKy.MaHocKy };
            // KHÔNG set MucDoCanhBao — mô phỏng write path không đi qua
            // QuanLyTaskViewModel.TinhDiemVaSapXep(), đúng như sync-apply của Epic 2.
            var task = new StudyTask("Không qua UI", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHoc.MaMonHoc,
            };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);

            await new SqliteHocKyRepository(factory).LuuHocKyAsync(hocKy);

            var saved = await new SqliteStudyTaskRepository(factory).GetAsync(task.MaTask);
            Assert.NotNull(saved);
            Assert.False(string.IsNullOrWhiteSpace(saved!.MucDoCanhBao));
        }
```

Add this to the `SoftDeleteReadPathTests` class created in Task 3.1, so it reuses that file's `NewDb()`. The getter is `GetAsync` (`SqliteStudyTaskRepository.cs:21-25`); it filters `!IsDeleted`, which is correct here since the task under test is not tombstoned.

- [ ] **Step 2: Run to verify it fails**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~TaskDungTuCtor"`
Expected: **FAIL** with `SqliteException: SQLite Error 19: 'NOT NULL constraint failed: StudyTasks.MucDoCanhBao'`.

- [ ] **Step 3: Default the field**

In `SmartStudyPlanner/Models/StudyTask.cs`, change line `27`:

```csharp
        public string MucDoCanhBao { get; set; }
```

to:

```csharp
        // NOT NULL trong schema. VM stamp lại giá trị thật qua TinhDiemVaSapXep()
        // (QuanLyTaskViewModel:107-110); default ở đây để mọi write path KHÔNG qua UI
        // — sync-apply, import, test — không đâm vào SQLite Error 19.
        public string MucDoCanhBao { get; set; } = "An toàn";
```

Defaulting the property rather than assigning inside one ctor covers the parameterless ctor and object-initializer construction too. `"An toàn"` matches what the VM assigns for `DiemUuTien < 50`, and a new task starts at 0.

- [ ] **Step 4: Run to verify it passes**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~TaskDungTuCtor"`
Expected: PASS.

- [ ] **Step 5: Run the full suite**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj`
Expected: `Failed: 0`.

- [ ] **Step 6: Commit**

```bash
rtk git add SmartStudyPlanner/Models/StudyTask.cs SmartStudyPlanner.Tests/Infrastructure/Persistence/SoftDeleteReadPathTests.cs
rtk git commit -m "fix(model): default MucDoCanhBao so non-UI write paths can save

The column is NOT NULL but neither ctor set it — every save today works
only because QuanLyTaskViewModel.TinhDiemVaSapXep() stamps it first. Epic
2's sync-apply path is the first writer that won't, and would have hit
SQLite Error 19. Tracked at system_roadmap.md:103-113 as not surveyed."
```

---

## WP-4 — Scheduling Characterization Tests

### Task 4.1: Pin `GenerateSchedule`'s current behaviour

**Files:**
- Create: `SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs`

`WorkloadServiceImpl.GenerateSchedule` (`:40-109`) is the most behaviour-defining method in scheduling and is exercised by nothing. It is directly testable: `capacityHours` is a parameter (no `capacity.txt` I/O), and both dependencies — `IDecisionEngine`, `IClock` — are constructor-injected.

**These are characterization tests.** They record what the method does today so a future change has to be deliberate. They do not assert the behaviour is *correct*. In particular, do **not** add a deadline-ordering assertion: `HanChot` appears nowhere in the allocator (Key Finding 3), and making it deadline-aware is Epic 3 / SOE work behind open gate G2.

- [ ] **Step 1: Write the tests**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    public class WorkloadServiceScheduleTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 4, 11, 9, 0, 0);

        // Trả điểm ưu tiên và số phút theo bảng tra, để test không phụ thuộc
        // vào công thức thật của DecisionEngine. Phải implement đủ 6 member của
        // IDecisionEngine (IDecisionEngine.cs:12-34) dù GenerateSchedule chỉ dùng 2.
        private sealed class StubDecisionEngine : IDecisionEngine
        {
            public Dictionary<string, double> Priorities { get; } = new();
            public Dictionary<string, int> Minutes { get; } = new();

            public WeightConfig Config { get; } = new WeightConfig();

            public double CalculatePriority(StudyTask task, MonHoc monHoc)
                => task is null ? 0 : Priorities.GetValueOrDefault(task.TenTask, 0);

            public int CalculateRawSuggestedMinutes(StudyTask task)
                => Minutes.GetValueOrDefault(task.TenTask, 0);

            public string SuggestStudyTime(StudyTask task) => string.Empty;

            public int PredictStudyMinutes(StudyTask task, MonHoc monHoc, out bool isMlPrediction)
            {
                isMlPrediction = false;
                return CalculateRawSuggestedMinutes(task);
            }

            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult<WeightConfigSuggestion?>(null);
        }

        private static (HocKy, StubDecisionEngine) BuildFixture(params (string name, double pri, int mins)[] tasks)
        {
            var hocKy  = new HocKy("HK Sched", FixedNow.Date);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var engine = new StubDecisionEngine();

            foreach (var (name, pri, mins) in tasks)
            {
                monHoc.DanhSachTask.Add(
                    new StudyTask(name, FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2));
                engine.Priorities[name] = pri;
                engine.Minutes[name] = mins;
            }

            hocKy.DanhSachMonHoc.Add(monHoc);
            return (hocKy, engine);
        }

        private static WorkloadServiceImpl Sut(StubDecisionEngine engine)
            => new WorkloadServiceImpl(engine, new FakeClock(FixedNow));

        [Fact]
        public void GenerateSchedule_LuonTaoItNhat7Ngay_BatDauTuHomNay()
        {
            var (hocKy, engine) = BuildFixture();

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.Equal(7, days.Count);
            Assert.Equal(FixedNow.Date, days[0].Date);
            Assert.Equal("Hôm nay", days[0].DisplayName);
            Assert.Equal("Ngày mai", days[1].DisplayName);
        }

        [Fact]
        public void GenerateSchedule_BoQuaTaskDaHoanThanh()
        {
            var (hocKy, engine) = BuildFixture(("Xong", 90, 120));
            hocKy.DanhSachMonHoc[0].DanhSachTask[0].TrangThai = StudyTaskStatus.HoanThanh;

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.All(days, d => Assert.Empty(d.Tasks));
        }

        [Fact]
        public void GenerateSchedule_TruThoiGianDaHoc_KhongConViecThiKhongXep()
        {
            var (hocKy, engine) = BuildFixture(("Gần xong", 90, 100));
            hocKy.DanhSachMonHoc[0].DanhSachTask[0].ThoiGianDaHoc = 100;

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.All(days, d => Assert.Empty(d.Tasks));
        }

        [Fact]
        public void GenerateSchedule_XepVaoNgayItTaiNhat_KhongVuotCapacity()
        {
            // capacity 1h = 60 phút; 180 phút phải trải ra đúng 3 ngày khác nhau.
            var (hocKy, engine) = BuildFixture(("Dài", 90, 180));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.All(days, d => Assert.True(d.TotalMinutes <= 60));
            Assert.Equal(180, days.Sum(d => d.TotalMinutes));
            Assert.Equal(3, days.Count(d => d.Tasks.Count > 0));
        }

        [Fact]
        public void GenerateSchedule_ViecBiCatNho_DuocDanhSoPhan()
        {
            var (hocKy, engine) = BuildFixture(("Dài", 90, 180));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);
            var names = days.SelectMany(d => d.Tasks).Select(t => t.TenTask).ToList();

            Assert.Contains("Dài (Phần 1)", names);
            Assert.Contains("Dài (Phần 2)", names);
            Assert.Contains("Dài (Phần 3)", names);
        }

        [Fact]
        public void GenerateSchedule_HetChoTrong7Ngay_ThiMoThemNgay()
        {
            // capacity 1h x 7 ngày = 420 phút; 600 phút buộc phải tràn sang ngày thứ 8+.
            var (hocKy, engine) = BuildFixture(("Rất dài", 90, 600));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.True(days.Count > 7);
            Assert.Equal(600, days.Sum(d => d.TotalMinutes));
        }

        [Fact]
        public void GenerateSchedule_UuTienCaoDuocXepTruoc()
        {
            var (hocKy, engine) = BuildFixture(("Thấp", 10, 60), ("Cao", 95, 60));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);
            var firstScheduled = days.First(d => d.Tasks.Count > 0).Tasks[0].TenTask;

            Assert.Equal("Cao", firstScheduled);
        }
    }
}
```

`StubDecisionEngine` above implements all six members of `IDecisionEngine` (`SmartStudyPlanner/Services/IDecisionEngine.cs:12-34`), verified against the interface — `GenerateSchedule` only calls `CalculatePriority` (`:50`) and `CalculateRawSuggestedMinutes` (`:69`), but C# requires the rest. `WeightConfigSuggestion` comes from `SmartStudyPlanner.Core.ML.Contracts`. `FakeClock` is in `SmartStudyPlanner.Tests.TestDoubles` (used that way at `DecisionEngineTests.cs:8, 28`).

- [ ] **Step 2: Run**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --filter "FullyQualifiedName~WorkloadServiceScheduleTests"`
Expected: 7 passed.

**If any fail:** these are characterization tests — the *implementation* is the specification. Read `WorkloadServiceImpl.cs:40-109`, work out what it actually does, and correct the **test** to match. Do not change `WorkloadServiceImpl`.

- [ ] **Step 3: Commit**

```bash
rtk git add SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs
rtk git commit -m "test(scheduling): characterize WorkloadServiceImpl.GenerateSchedule

The most behaviour-defining method in scheduling had zero coverage. These
pin current behaviour (7-day window, least-loaded-day allocation, capacity
splitting, overflow days, priority ordering); they do not assert it is
correct. Deadline-blindness is left untouched — that is Epic 3 / SOE."
```

---

## WP-5 — Runtime Robustness

### Task 5.1: Guard the background notification timer

**Files:**
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml.cs:87-94, 96-129`

`BackgroundTimer_Tick` (`:96`) is `async void` with no `try`/`catch`. `App.xaml.cs:23-30` installs a `DispatcherUnhandledException` handler that catches it and shows a `MessageBox` — so the app survives, but a transient DB fault produces a **modal error dialog every 60 seconds**. The interval at `:91` is 1 minute with a comment saying it is temporary for testing, and there is no toast de-duplication, so a persistent urgent task fires a toast every minute too.

- [ ] **Step 1: Wrap the tick body**

Replace lines `96-129`:

```csharp
        // De-dup: cùng một cảnh báo không bắn lại trong 30 phút, tránh spam toast mỗi phút.
        private static readonly TimeSpan ToastCooldown = TimeSpan.FromMinutes(30);
        private DateTime _lanCanhBaoGanNhat = DateTime.MinValue;
        private int _soTaskKhanCapDaBao = -1;

        private async void BackgroundTimer_Tick(object sender, EventArgs e)
        {
            // async void + DispatcherTimer: exception ở đây rơi vào
            // DispatcherUnhandledException của App, tức là bật MessageBox MỖI PHÚT.
            // Nuốt lỗi tại chỗ và ghi crash.log thay vì làm phiền người dùng.
            try
            {
                var repo = ServiceLocator.Get<IHocKyRepository>();
                var decisionEngine = ServiceLocator.Get<IDecisionEngine>();

                var danhSachHocKy = await repo.LayDanhSachHocKyAsync();
                int soTaskKhanCap = 0;

                foreach (var hk in danhSachHocKy)
                {
                    foreach (var mon in hk.DanhSachMonHoc)
                    {
                        foreach (var task in mon.DanhSachTask)
                        {
                            if (task.TrangThai != StudyTaskStatus.HoanThanh)
                            {
                                double diem = decisionEngine.CalculatePriority(task, mon);
                                if (diem >= 80) soTaskKhanCap++;
                            }
                        }
                    }
                }

                if (soTaskKhanCap == 0)
                {
                    _soTaskKhanCapDaBao = -1;   // hết khẩn cấp -> reset để lần sau báo lại ngay
                    return;
                }

                bool conNguoi = soTaskKhanCap == _soTaskKhanCapDaBao
                                && DateTime.Now - _lanCanhBaoGanNhat < ToastCooldown;
                if (conNguoi) return;

                _lanCanhBaoGanNhat = DateTime.Now;
                _soTaskKhanCapDaBao = soTaskKhanCap;

                new ToastContentBuilder()
                    .AddText("🔥 CẢNH BÁO DEADLINE (Chạy ngầm)!")
                    .AddText($"Bạn đang có {soTaskKhanCap} bài tập KHẨN CẤP chưa làm!")
                    .AddText("Click vào đây để mở app và giải quyết ngay!")
                    .AddAudio(new Uri("ms-winsoundevent:Notification.Default"))
                    .Show();
            }
            catch (Exception ex)
            {
                CrashLogger.Log("MainWindow.BackgroundTimer_Tick", ex);
            }
        }
```

Add `using SmartStudyPlanner.Services.Telemetry;` if `CrashLogger` is not already in scope — check its namespace in `App.xaml.cs`'s using block first.

- [ ] **Step 2: Raise the interval off the debug value**

At `:91`, replace:

```csharp
            // Tạm thời để 1 phút réo 1 lần để em test.
            _backgroundTimer.Interval = TimeSpan.FromMinutes(1);
```

with:

```csharp
            // 5 phút: đủ kịp thời cho cảnh báo deadline, không đốt CPU cho một lượt
            // quét toàn bộ học kỳ mỗi phút. De-dup ở BackgroundTimer_Tick lo phần spam.
            _backgroundTimer.Interval = TimeSpan.FromMinutes(5);
```

- [ ] **Step 3: Build**

Run: `rtk dotnet build SmartStudyPlanner.slnx --configuration Debug`
Expected: 0 errors.

There is no test for this — `MainWindow` is WPF code-behind with zero coverage by design (§8.4 lists `Views/` as a zero-coverage directory, and this package does not change that). Verify manually: launch the app with an urgent task present, confirm one toast rather than a stream.

- [ ] **Step 4: Commit**

```bash
rtk git add SmartStudyPlanner/Views/MainWindow.xaml.cs
rtk git commit -m "fix(ui): guard the background timer tick and de-dup deadline toasts

BackgroundTimer_Tick was async void with no try/catch. A transient DB fault
reached App's DispatcherUnhandledException handler, which shows a modal
error dialog — every 60 seconds. Faults now go to crash.log instead.

Toast repeats every tick while any urgent task exists; added a 30-minute
cooldown keyed on the urgent count, and moved the interval off the 1-minute
debug value the comment flagged as temporary."
```

---

### Task 5.2: Make the capacity round-trip culture-invariant

**Files:**
- Modify: `SmartStudyPlanner/Services/WorkloadServiceImpl.cs:28-38`

`GetCapacity` uses `double.TryParse(File.ReadAllText(FilePath), out val)` (`:30`) and `SaveCapacity` uses `capacity.ToString()` (`:37`) — both culture-sensitive. They round-trip correctly on one machine, so this only bites when the file crosses a locale boundary, is hand-edited, or the user changes their Windows region. On `vi-VN` the file holds `4,5`; parsed under an invariant reader it fails and silently reverts to the 3.0 default.

**Both halves must change together, and the reader must stay backward compatible** — existing files on Vietnamese machines contain `4,5` and must keep loading.

- [ ] **Step 1: Rewrite both methods**

Replace lines `28-38`:

```csharp
        public double GetCapacity()
        {
            if (!File.Exists(FilePath)) return DefaultCapacityHours;

            var raw = File.ReadAllText(FilePath).Trim();

            // Invariant trước (định dạng mới), rồi mới tới culture hiện tại — file cũ trên
            // máy vi-VN chứa "4,5" và vẫn phải đọc được sau khi đổi sang ghi invariant.
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double val)
                || double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out val))
            {
                return val;
            }

            return DefaultCapacityHours;
        }

        public void SaveCapacity(double capacity)
        {
            File.WriteAllText(FilePath, capacity.ToString(CultureInfo.InvariantCulture));
        }
```

Add the constant next to `FilePath` (`:16-17`):

```csharp
        private const double DefaultCapacityHours = 3.0; // Mặc định 3 tiếng nếu chưa từng cài đặt
```

Add `using System.Globalization;` to the file's using block.

- [ ] **Step 2: Write a test for the fallback**

Add to `SmartStudyPlanner.Tests/Services/WorkloadServiceScheduleTests.cs` — **only if** `FilePath` can be redirected. It is currently `private static readonly` bound to `AppDomain.CurrentDomain.BaseDirectory`, so it cannot.

**Decision: do not add a test, and do not refactor `FilePath` to make one possible.** Making the path injectable is a production change with no other justification, and this package is explicitly not a refactoring package. Record it instead: note in the commit body that `GetCapacity`/`SaveCapacity` remain untested because the path is a static, and add it to WP-4's characterization scope only if the owner later asks for it.

- [ ] **Step 3: Build and run the suite**

```bash
rtk dotnet build SmartStudyPlanner.slnx --configuration Debug
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```
Expected: 0 errors, `Failed: 0`.

- [ ] **Step 4: Verify the round-trip by hand**

Launch the app, set capacity to a fractional value (e.g. 4.5 hours) in the workload UI, close, and inspect `capacity.txt` under the build output directory. Expected content: `4.5` (a dot, regardless of system locale). Reopen the app and confirm the value survives.

- [ ] **Step 5: Commit**

```bash
rtk git add SmartStudyPlanner/Services/WorkloadServiceImpl.cs
rtk git commit -m "fix(workload): write capacity.txt invariantly, read it with a fallback

Read and write were both culture-sensitive, so the file round-tripped only
on the machine that wrote it — a locale change, a hand edit, or any config
sync silently reverted capacity to the 3.0 default. Writes are now
invariant; reads try invariant first then current culture, so existing
vi-VN files holding '4,5' still load.

GetCapacity/SaveCapacity remain untested: FilePath is a static bound to
AppDomain.BaseDirectory and making it injectable is out of scope here."
```

---

## WP-6 — Repo & Doc Hygiene

### Task 6.1: Retire the root `Assets/` folder

**Files:**
- Create: `docs/assets/icon-source/` (`icon.svg`, `favicon.svg`, `Icon Preview.html`, `png/icon-{16,32,48,256}.png`)
- Delete: root `Assets/`
- Unchanged: `SmartStudyPlanner/Assets/icon.ico` (tracked; the one the build uses)

Per the owner's decision. The root folder's `icon.ico` is byte-identical to the tracked copy (md5 `d2bc90edcb01d398ce82ded7ff497177`); everything else in it exists nowhere else and is worth keeping.

- [ ] **Step 1: Re-confirm the duplicate before deleting anything**

```bash
rtk git ls-files | grep -i assets
md5sum Assets/icon.ico SmartStudyPlanner/Assets/icon.ico
```

Expected: `SmartStudyPlanner/Assets/icon.ico` is the only tracked path; the two hashes match. **If the hashes differ, stop** — the root copy is not a duplicate and the plan's premise is wrong.

- [ ] **Step 2: Move the unique sources into the repo**

```bash
mkdir -p docs/assets/icon-source/png
cp Assets/icon.svg Assets/favicon.svg "Assets/Icon Preview.html" docs/assets/icon-source/
cp Assets/png/*.png docs/assets/icon-source/png/
```

- [ ] **Step 3: Add a README so the folder explains itself**

Create `docs/assets/icon-source/README.md`:

```markdown
# App icon — design sources

Source artwork for the application icon. **Not referenced by the build.**

The icon the build actually ships is `SmartStudyPlanner/Assets/icon.ico`,
referenced twice from `SmartStudyPlanner/SmartStudyPlanner.csproj`
(`<ApplicationIcon>` and `<Resource Include>`, both project-relative).

If you change the artwork here, re-export `icon.ico` and update that file —
copying into this directory alone changes nothing.

| File | Role |
|---|---|
| `icon.svg` | Master vector |
| `favicon.svg` | Favicon variant |
| `png/icon-{16,32,48,256}.png` | Raster exports |
| `Icon Preview.html` | Local preview sheet |
```

- [ ] **Step 3b: Assert the copy is complete before deleting anything**

This is the only irreversible step in the plan — root `Assets/` is **untracked**, so git cannot restore it. Steps 2–3 copy an enumerated list; nothing so far proves that enumeration is complete, and the plan may be executing days after it was written.

```bash
diff -r --brief Assets docs/assets/icon-source
```

Expected: differences limited to `icon.ico` (kept only at `SmartStudyPlanner/Assets/`, md5-verified in Step 1) and `README.md` (new). **Any other `Only in Assets/` line means a file would be lost — stop, copy it, re-run.**

- [ ] **Step 4: Delete the root folder**

```bash
rm -rf Assets
```

- [ ] **Step 5: Verify the build is unaffected**

```bash
rtk dotnet build SmartStudyPlanner.slnx --configuration Debug
```
Expected: 0 errors. The `<ApplicationIcon>` and `<Resource Include>` paths are project-relative and resolve to `SmartStudyPlanner/Assets/icon.ico`, which was never touched.

- [ ] **Step 6: Commit**

```bash
rtk git add docs/assets/icon-source
rtk git commit -m "chore(assets): retire the untracked root Assets/ folder

Its icon.ico was byte-identical to the tracked SmartStudyPlanner/Assets/
icon.ico that the csproj actually references (both paths are
project-relative, so the root folder was never part of the build). The
unique design sources — svg, png exports, preview sheet — move to
docs/assets/icon-source/ so they survive a fresh clone."
```

---

### Task 6.2: Correct the stale numbers and the CSA

**Files:**
- Modify: `README.md:84`
- Modify: `docs/specs/system_roadmap.md` §A.1 (GitNexus symbol count)
- Modify: `CLAUDE.md` (GitNexus symbol count)
- Modify: `docs/reports/2026-07-27-epic2-prep-current-state-assessment.md`
- Modify: `SmartStudyPlanner/SmartStudyPlanner.csproj:24`

- [ ] **Step 1: Get the real test count**

Run: `rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj`

Record the `Passed:` figure. It will exceed 346 — WP-2 and WP-3 add roughly a dozen cases.

- [ ] **Step 2: Fix the README**

`README.md:84` reads *"The test suite has 337 tests, all passing…"*. Replace `337` with the number from Step 1.

- [ ] **Step 3: Refresh the GitNexus index and reconcile the two symbol counts**

`docs/specs/system_roadmap.md` §A.1 cites 3,333 symbols at `5e54220`; `CLAUDE.md` says 4,001. Both predate `d3deca7`.

```bash
npx gitnexus analyze
```

Then update both files with the figure it reports, and note the commit it was taken at so the next reader can tell whether it is stale.

- [ ] **Step 4: Drop the unused package reference**

`SmartStudyPlanner/SmartStudyPlanner.csproj:24` references `Verify.CommunityToolkit.Mvvm` 1.1.0, used by neither project. Delete the line, then:

```bash
rtk dotnet build SmartStudyPlanner.slnx --configuration Debug
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```
Expected: 0 errors, `Failed: 0`. If anything breaks, restore the line — something references it that a text search missed.

- [ ] **Step 5: Amend the CSA with the three corrections**

Edit `docs/reports/2026-07-27-epic2-prep-current-state-assessment.md`. Do **not** rewrite it — append a `## Corrections (2026-07-2x)` block after the *Follow-ups* section and add an inline `> **Corrected:**` note at each affected anchor:

- **Finding 15 (`:926-931`)** and the `Assets/` follow-up (`:1089-1090`) — the csproj references resolve to the *tracked* `SmartStudyPlanner/Assets/icon.ico`; the untracked directory was the *root* `Assets/`, referenced by nothing. The clean clone built because the icon is committed.
- **§8.4 (`:634`)** — "11 of 346 cases are pure duplicates / three byte-identical file pairs" does not reproduce; the only byte-identical `.cs` pairs are generated artifacts under `obj/`.
- **§8.2 (`:620`)** — the unguarded `async void` timer is `Views/MainWindow.xaml.cs:96`, not `App.xaml.cs`, whose three background tasks are all already guarded.

- [ ] **Step 6: Commit**

```bash
rtk git add README.md docs/specs/system_roadmap.md CLAUDE.md docs/reports/2026-07-27-epic2-prep-current-state-assessment.md SmartStudyPlanner/SmartStudyPlanner.csproj
rtk git commit -m "docs: refresh stale counts and correct three CSA findings

README test count, and the GitNexus symbol figures in the roadmap and
CLAUDE.md, are re-measured at HEAD. Also drops the unused
Verify.CommunityToolkit.Mvvm package reference.

CSA corrections: the csproj's Assets references are project-relative and
resolve to the tracked icon (finding 15 was wrong about the build depending
on an untracked file); the '11 duplicate test cases' claim does not
reproduce outside obj/; and the unguarded async void timer is in
MainWindow.xaml.cs, not App.xaml.cs."
```

---

### Task 6.3 *(OPTIONAL — owner opt-in)*: Time-boxed dependency advisory bump

**Skip unless the owner asks for it.** The only item in the plan that can break something the suite does not cover; no Epic 2 benefit; already on the roadmap's deferred ledger; nothing depends on it.

**Files:**
- Modify: `SmartStudyPlanner/SmartStudyPlanner.csproj`

NU1903 (high, `SQLitePCLRaw`) and NU1904 (critical, `System.Drawing.Common`) surface on every build — 14 of the 192 warning messages. Both are already on the roadmap's deferred ledger (`system_roadmap.md:100-102`) at ~30 minutes. Now that CI prints them on every run, clearing them is worth the half hour.

**Hard stop: 30 minutes.** These are transitive dependencies of `Microsoft.ML` and `Microsoft.EntityFrameworkCore.Sqlite`; pinning them can break the ML pipeline in ways the suite may not catch.

- [ ] **Step 1: Identify the vulnerable versions and their sources**

```bash
rtk dotnet list SmartStudyPlanner/SmartStudyPlanner.csproj package --vulnerable --include-transitive
```

- [ ] **Step 2: Pin the fixed versions directly**

Add explicit `<PackageReference>` entries for the patched versions the report names. Direct references override transitive resolution.

- [ ] **Step 3: Verify**

```bash
rtk dotnet build SmartStudyPlanner.slnx --configuration Release
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj
```
Expected: NU1903 and NU1904 gone, 0 errors, `Failed: 0`.

**Also exercise ML by hand** — the suite does not cover model training end to end. Launch the app, open Analytics, trigger *Retrain*, and confirm it completes.

- [ ] **Step 4: If it is not clean inside 30 minutes, revert**

```bash
rtk git checkout -- SmartStudyPlanner/SmartStudyPlanner.csproj
```

Then leave the advisories on the roadmap ledger and say so in the stabilization report. A working ML pipeline beats a clean warning list.

- [ ] **Step 5: Commit (only if Step 3 was clean)**

```bash
rtk git add SmartStudyPlanner/SmartStudyPlanner.csproj
rtk git commit -m "chore(deps): pin patched SQLitePCLRaw and System.Drawing.Common

Clears NU1903 (high) and NU1904 (critical), which CI now surfaces on every
run. Both were on the roadmap's deferred ledger at system_roadmap.md:100-102."
```

---

# Deferred Decisions

## Category C — Intentional technical debt, deliberately unchanged

No work items. Each entry records *why* it stays.

| Item | Why it stays |
|---|---|
| **ViewModels bypass the service layer and call repositories directly**; **27 residual `ServiceLocator` call sites**; **ViewModels never resolved from DI** (§8.1) | This is the architecture, not a defect in it. Rewiring 8 ViewModels plus `App.xaml.cs` and `MainWindow.xaml.cs` immediately before a sync epic trades a stable known shape for an unstable unknown one, against a presentation layer with zero test coverage. |
| **Data-load + priority-scoring loop inside the window** (`MainWindow.xaml.cs:98-117`); **ViewModel constructs and `ShowDialog()`s a Window** (`DashboardViewModel.cs:349-350`); **full pipeline run synchronously in a VM ctor** (`DashboardViewModel.cs:89`) | Same reasoning. All three are in `Views/`-adjacent code with no coverage; changing them is a UI refactor, and the CSA found no user-visible fault. |
| **Two divergent copies of the raw-minutes formula**; **`MucDoCanhBao` derivation duplicated at 3 sites — 4 once WP-3.3 lands** (§8.1) | Genuine DRY violations. WP-3.3's `"An toàn"` property default is deliberately a fourth site: it agrees with `QuanLyTaskViewModel.TinhDiemVaSapXep()` rather than competing with it, and whoever consolidates these later should treat the model default as one of the sites, not an exception. WP-4 gives `GenerateSchedule` the coverage that would make deduplication safe — **that unblocking is recorded, not scheduled.** Consolidating a scheduling formula immediately before Epic 2 is exactly the "delays Epic 2 without meaningful benefit" the brief warns against. Revisit when Epic 3 opens scheduling anyway. |
| **Chart rendering split across two technologies** — LiveChartsCore on Analytics, native `DonutChart` on Dashboard (§8.1) | Unsourced as a plan deviation (the roadmap says nothing about charting unification), and the queued Analytics 2-section redesign will settle it. Unifying now would be redone. |
| **192 build warnings, CS8618 ×156** (§8.6) | Nullable annotations enabled but unsatisfied. Fixing 156 sites touches nearly every model and service, produces an enormous diff with zero behavioural change, and would bury the WP-1..WP-6 diffs it lands beside. CI records the count; no reduction attempted. |
| **`IsReady = true` set past a failed R² gate** (`MLModelManager.cs:151`); **`IMlConfidencePolicy` bypassed by the A1 path** (`StudyTimePredictorService.cs:41-45`); **no accuracy gate on the A2 classifier** | ML output is display-only by hard roadmap invariant (`:122` — "never let ML availability gate the app"), so none of these can produce a wrong schedule. The real guard is a sentinel return value, and the missing A2 gate is documented as intentional at `MLModelManager.cs:139-141`. The three-tier confidence policy is Part B (aspirational) — Category D. |
| **Dead code**: `ParseSource.MlOverridden` (zero consumers), `ParseInputStage` (dead in production), `EvaluateR2Async` (no production caller), `TextClassifierService.RetrainAsync` (no UI entry), `AutoApplyThreshold` (dead on the parsing path) | These are declared seams, not accidents. Deleting them buys a marginally smaller surface and loses the extension points Epic 4 is expected to use. `ParseInputStage` in particular is the pipeline abstraction's input stage — it is unused because Smart Add does not consume the pipeline (§10.4 confirms the roadmap never said it should), not because it is broken. |
| **Three fire-and-forget persistence writes** (§8.2) | Deliberate: they keep UI interactions responsive, and `docs/architecture/async-workflow.md` documents what is intentionally not async. Converting them means threading async through ViewModel command paths — an architectural change. |
| **No EF Migrations** — `EnsureCreated()` + 3 patch seams; **backup only on schema upgrade** | Roadmap-specified, not drift: M1.2 planned "`SyncSchema.EnsureColumns` versioned upgrade + backup + migration report" (`:40`). Introducing Migrations now would mean baselining an existing schema across every installed database. |
| **`WorkloadServiceImpl.FilePath` is a static bound to `AppDomain.BaseDirectory`**, so `GetCapacity`/`SaveCapacity` cannot be tested | Making it injectable is a production change whose only justification is testability, in a package that is explicitly not a refactoring package. Recorded in WP-5's commit body. |
| **`ServiceLocator.cs:27` can build a second container**; **dead DI registration at `:40`** | The second-provider path requires a specific call ordering that no production path takes. Both are one-line fixes with no observed failure — bundled here rather than scheduled, to keep WP-6 to pure hygiene. |
| **Zero presentation-layer test coverage** — `Views/` (9), `Converters/` (11), `Controls/DonutChart` | Testing WPF code-behind and converters needs an STA harness and a UI-testing approach this project has never had. Establishing one is a project of its own, not stabilization. |
| **No parallelism control** (no `[Collection]`, no `xunit.runner.json`; 57 classes run in parallel); **`await Task.Delay(50)` as a synchronisation primitive in 5 places** | The suite is green and runs in ~3s. WP-2 removes the two tests that touched shared machine state, which is what made parallelism risky. The `Task.Delay` calls are a smell but no flake has been observed since the streak-store fix (`27ffd3e`). Leave until something actually flakes — and now CI will notice if it does. |
| **184 wall-clock call sites across 27 files** | WP-2 de-dates only `DecisionEngineTests.cs`, the file with a *proven* drift. A repo-wide `IClock` sweep is a multi-day refactor of production code, not a test fix. |

## Category D — Deferred roadmap

No work items. These belong to later phases.

| Item | Where it belongs |
|---|---|
| **Epic 2 — LAN sync** (no networking, discovery, transport, conflict resolution, or peer code exists) | Epic 2 itself. This plan prepares its assumptions; it does not start it. |
| **Gate G4 — tombstone retention policy** | **Epic 2 planning.** This is a decision Epic 2 must make (how long tombstones live, when they are purged), not something stabilization can settle. WP-3 makes tombstones invisible to readers; how long they persist is Epic 2's call. |
| **Epic 3 — SOE**; **gate G2 (SOE pass semantics)**; **`IScheduleOptimizer`** (`system_roadmap.md:376-377`, does not exist); **scheduler deadline-blindness** (`WorkloadServiceImpl.cs:77-91`, `ScheduledTask` has no deadline field) | Epic 3, blocked on G2. Note the Master Plan's order is E1 → E3 → E2, so whoever sequences the work should surface that G2 is still open. |
| **Epic 4 — ML maturation**; **Part B §10 ML Confidence & Fallback Policy** (`:543-586` — uncertainty estimation, three-tier High/Medium/Low gate, single Confidence Validation node) | Epic 4. Part B is self-labelled aspirational (`:7`), so this is unbuilt design, not drift. |
| **M9 — natural-language deadline parsing; cross-semester analytics** (`:91`) | M9. This is why deadline extraction is keyword/substring based and analytics is scoped to one `HocKy`. |
| **Analytics 2-section restructure** (`docs/plans/2026-07-20-analytics-two-section-redesign.md` — designed, uncoded) — **including `ComputeWeeklyMinutes` always emitting 7 buckets** regardless of the 7/30/90-day range selection (`StudyAnalyticsService.cs:11-25`, Key Finding 17) | The redesign. The 7-bucket behaviour is a real mismatch with the range selector, but fixing it in isolation means designing the 30/90-day chart twice. Fold it into the redesign brief. |
| **#46/#47 native-charts "mission-control" redesign** — parked in history (`ced372b`, `4122a42`), reachable from HEAD but not in its tree (141 files differ) | Unscheduled, and deliberately so. The `d3deca7` sync was ui_rf-authoritative precisely because taking this tree would overwrite manually-tested UI. **Standing owner ask: mention it exists at the start of each continuing session.** |
| **Analytics computation split between service and ViewModel** — heatmap (`AnalyticsViewModel.cs:219-248`), narrative (`:201-217`), productivity-score inputs (`:165-171`) live in the VM (Key Finding 17) | The Analytics redesign. Pulling them into `IStudyAnalytics` is the right shape but it is a refactor of the surface being redesigned. |
| Roadmap ledger §A.4 items: **pipeline rehome** (`Services/Pipeline/*` → `Application/UseCases/*`), **`Core/Capacity`** ("only when a real need surfaces"), **cloud model storage** (opt-in via `IModelStorageProvider`), **mobile/hybrid clients** ("revisit after LAN sync lands"), **end-to-end async pipeline** ("current sync MVP is acceptable") | Already on the roadmap's own deferred ledger (`:95-114`). Not re-litigated here. |
| **No installer, no packaging, no release pipeline**; **no model artifacts committed** (both models train per-machine at runtime); **theme not persisted** | Post-Epic-2 productionisation. WP-1 delivers CI, not CD. |

---

# Out of Scope

Explicitly **not** addressed during stabilization, beyond the Category C/D tables above:

- **Anything in the CSA's §10.4 "Checked and found *not* to be deviations" table.** Each is a plausible-sounding deviation the roadmap in fact specifies. Do not "fix" the `EnsureCreated` path, the tombstones-replace-cascades design, the unstaged Smart Add, the display-only ML, or the thin `DecisionEngineService` facade.
- **The Part A / Part B distinction itself.** Any future comparison against `system_roadmap.md` must apply it (Part A `:11-126` factual; Part B `:127`–end aspirational). Findings sourced only to Part B are unbuilt design and must not be scheduled as drift.
- **Epic 2 features of any kind.** No networking, no peer discovery, no conflict resolution. WP-3 makes Epic 2's *assumptions* true; it does not begin Epic 2.
- **Epic 3 work**, including any change that makes the scheduler deadline-aware.
- **New abstractions**, except `DeviceIdentity` (WP-3), which exists solely to make an Epic 2 assumption true and wraps the existing derivation as its fallback.
- **Roadmap rewriting.** WP-6 corrects two stale *numbers* in it. Its structure, decisions, and gates are untouched.

---

# Epic 2 Entry Criteria

Objective, checkable. Epic 2 may begin when all are true.

- [ ] **CI runs on every push and PR to `main` and `dev`, the latest run on `dev` is green, and `build-test` is a required status check on `dev`.** Verify: `rtk gh run list --limit 5` shows `completed success`; the required-check setting is visible in *Settings → Branches* (owner action — see WP-1 Step 6).
- [x] **`DecisionEngineTests.cs` contains zero occurrences of `DateTime.Now`,** and the 31–60-day priority band is asserted strictly between the horizon sentinel and a nearer task. Verify: `TestFile_KhongDungWallClock` passes. (The guard builds its search needle by concatenation so the assertion line does not match itself — the file's bytes genuinely contain no `DateTime.Now`, and a plain `grep` confirms it independently.)
- [x] **No test writes to the real user profile or builds the production composition root.** Verify: `grep -rn "ServiceLocator" SmartStudyPlanner.Tests` returns only sites documented as deliberate, and `LocalModelStorageTests` uses a temp directory. *(Met at `078fdbe`. Strengthened 2026-07-31: WP-3.2 briefly re-broke this through two seams the plan never listed, so the claim is now enforced rather than asserted — CI fails the build on any file appearing under `%APPDATA%`/`%LOCALAPPDATA%\SmartStudyPlanner`, `16be27f`.)*
- [x] **Every repository read path filters `!IsDeleted`,** with a regression test per previously-leaking path. Verify: `SoftDeleteReadPathTests` passes; a sweep of `Infrastructure/Persistence/SQLite/Repositories/` finds unfiltered `DbSet` reads only on write/upsert paths. *(Met at `bbb3c29`. Sweep re-run independently: the only unfiltered accesses are `SqliteHocKyRepository:98` and `SqliteStudyTaskRepository:56`, both upsert/delete lookups that must see tombstones.)*
- [x] **Device identity is read from a persisted file rather than recomputed from `Environment.MachineName`,** seeded from the previous derived value so existing stamped rows keep their identity, and routed through `AppDbContext.DeviceIdProvider` so every write uses it. Verify: `DeviceIdentityTests` passes, and `grep -rn "DeviceHelper.GetId" SmartStudyPlanner/` returns **six** sites, none of them a live identity source: the seed and the `IOException` fallback inside `DeviceIdentity`, the `AppDbContext.DeviceIdProvider` default, the `FocusViewModel` provider default, and the two `ModelMeta` provenance sites. *(The original wording named four and omitted the fallback and the `FocusViewModel` default — corrected here rather than left unsatisfiable. Met at `6704773`. Also verified: nothing resolves `AppDbContext` from the container, so no write path bypasses the seam; the registration is wired anyway so a future resolve cannot silently regress.)*
- [x] **A `StudyTask` built from its constructor saves without `SQLite Error 19`** — no UI pass required. Verify: `TaskDungTuCtor_LuuDuocMaKhongCanUIStampMucDoCanhBao` passes. *(Met at `78f16bb`; the test failed with exactly that SQLite error beforehand.)*
- [ ] **`WorkloadServiceImpl.GenerateSchedule` has characterization coverage.** Verify: `WorkloadServiceScheduleTests` passes.
- [ ] **The README's test count equals what `dotnet test` reports.**
- [x] **Every CSA finding is classified A/B/C/D** — satisfied by this document, and the three CSA corrections are recorded in the report itself. *(The corrections still need writing back into the CSA itself — that is WP-6 Task 6.2 Step 5, and it is criterion #8's package, not this one's.)*
- [ ] **Gate G4 (tombstone retention) is explicitly on the Epic 2 planning agenda.** It is not resolved by stabilization and must not be silently inherited.
- [ ] **Deadline toast fires once, not once per tick** (WP-5.1). Manual — no automated coverage exists for `Views/`. Verified by: ______ on ______.
- [ ] **`capacity.txt` contains a dot-decimal value that survives a restart** (WP-5.2). Manual. Verified by: ______ on ______.

---

# Verification

End-to-end, after all six packages:

```bash
# 1. Clean build from the solution
rtk dotnet restore SmartStudyPlanner.slnx
rtk dotnet build SmartStudyPlanner.slnx --no-restore --configuration Release

# 2. Full suite
rtk dotnet test SmartStudyPlanner.Tests/SmartStudyPlanner.Tests.csproj --no-build --configuration Release
#    Expect: Failed: 0. Record the total; it must match README.md:84.

# 3. CI agrees with local
rtk git push
rtk gh run list --limit 3
#    Expect: newest CI run 'completed success'.

# 4. Clean-clone check — nothing depends on an untracked file
#    (Windows-only toolchain: clone into the session scratchpad, not /tmp)
git clone . "$TEMP/ssp-verify" && cd "$TEMP/ssp-verify"
rtk dotnet build SmartStudyPlanner.slnx --configuration Debug
#    Expect: 0 errors, SmartStudyPlanner.exe produced.
#    Expect: no root Assets/; SmartStudyPlanner/Assets/icon.ico present.

# 5. Confirm the scope of the change
#    GitNexus is exposed as MCP tools and as `npx gitnexus` — it is not an rtk-proxied
#    shell command. Use the MCP tool gitnexus_detect_changes, or:
npx gitnexus analyze
#    Expect: repositories, StudyTask, DeviceIdentity, MainWindow,
#    WorkloadServiceImpl — and nothing in Core/Scheduling, Core/Parsing,
#    or Services/ML beyond the new DeviceIdentity.
```

**Manual checks that no test covers:**

1. Launch with an urgent task present → **one** deadline toast, not one per tick (WP-5.1).
2. Set a fractional capacity, close, inspect `capacity.txt` → contains `4.5` with a dot; reopen and confirm it survives (WP-5.2).
3. Open Analytics → the page renders and the numbers are *lower* than before if the database contains deleted study logs. **This is the expected effect of WP-3.1, not a regression.**
4. If WP-6.3 landed: trigger *Retrain* in Analytics and confirm it completes (the suite does not cover model training end to end).

---

# Final Recommendation

**Stabilization as scoped here is sufficient. Epic 2 can begin immediately after it, with one caveat.**

The CSA found no genuine blockers to Epic 2 *planning*, and this plan does not manufacture any. What it closes is the gap between what the roadmap assumes and what the repository enforces — three mechanisms (CI, trustworthy assertions, tombstone-aware reads) plus a stable device identity. None requires an architectural change, and Categories C and D together account for every remaining finding without scheduling a single line of work.

**The caveat is WP-2 Task 2.1.** Removing a false green is the one step in this plan whose outcome is not known in advance. The arithmetic says the 31–60-day band will assert cleanly once the deadline is frozen; it has simply never been checked, because the assertion has been passing on the horizon sentinel. If it fails, a real priority defect has surfaced, that becomes a new Category A finding, and this recommendation should be re-read.

**No additional investigation is needed before starting.** One item, gate **G4** (tombstone retention), is deliberately left open — it is a policy decision Epic 2 must make, and stabilization would only be guessing at it.

**One item to surface at Epic 2 kickoff, unrelated to readiness:** the Master Plan's execution order is E1 → E3 → E2, and gate **G2** (SOE pass semantics) — which blocks Epic 3, not Epic 2 — is still open. Whoever sequences the work should confirm that running E2 before E3 is the intended deviation from that order, rather than an accident of G2 being blocked.

---

# Decisions made

Per repo convention, every report from 2026-07-07 onward carries an ADR-style record.

**Decision: classify CI as Category A, and run it first.**
*Why:* Category A is defined as "Epic 2 assumptions." The roadmap assumes CI exists (`:18` defers the test count to it; invariant `:120` requires green with no enforcing mechanism), and the CSA's readiness section states plainly that "the suite protects us from regression on merge" is currently unfounded. That is an assumption, not a convenience.
*What for:* So every subsequent package's "still green" is an enforced fact rather than a local claim.
*Experience:* It is also the cheapest package in the plan, which makes ordering it first nearly free. The known cost is that WP-1 lands before WP-2 fixes the tests that touch machine state — hence the explicit "budget one debugging round" note.

**Decision: WP-2 Task 2.1 before WP-3 — as a scheduling gate, not a validation gate.**
*Why:* Task 2.1's outcome is unknown in advance. If freezing the drifted deadline surfaces a genuine priority-band defect, Category A scope grows and the owner has to re-decide what WP-3 is being sequenced against. That is a reason to run it first.
*What for:* So the owner's scope decision is made on complete information, before two days of persistence work depend on it.
*Experience:* The first draft justified this dependency differently — that WP-3 changes Analytics' sole data source, so validating it against a suite with a demonstrated false green would be circular. **That argument is a non-sequitur and has been removed.** The false green sits in the DecisionEngine priority band; it says nothing about whether the Analytics and stats tests that would catch a WP-3.1 regression are trustworthy. The order was right and the reason was wrong — and the wrong reason cost real parallelism, because it serialized *all* of WP-2 ahead of WP-3 when only Task 2.1 gates it. Tasks 2.2 and 2.3 belong to WP-2 by theme, not by sequence.

**Decision: run WP-2.3 before WP-3.2, and say why rather than calling the packages fully independent.**
*Why:* WP-3.2 wires `DeviceIdProvider = deviceIdentity.GetId` into `ServiceLocator.cs:44`, and `DeviceIdentity` defaults to the real `%APPDATA%/SmartStudyPlanner`. `PipelineStageTests:200` is the last test that resolves through `ServiceLocator`, so it would build that factory and start writing `device-id.txt` into the runner's real profile.
*What for:* So the package that makes tests stop touching the machine is not undone by the package that was declared parallel to it.
*Experience:* The files are disjoint, so a file-level dependency check says "independent" and a behavioural one says otherwise. Worth recording as a preference with a reason rather than either silently ordering it or asserting a hard dependency that does not exist.

**Decision: seed the persisted `DeviceIdentity` from `DeviceHelper.GetId()` rather than a fresh GUID — accepting that this fixes the rename case and not the collision case.**
*Why:* Every existing install has rows already stamped with the derived value. A random seed would fix hostname collision but make every existing device look like a new peer to its own history. Breaking real users' row provenance to fix a scenario that requires two same-named machines on one LAN is the worse trade.
*What for:* Identity that stops moving when a PC is renamed, without rewriting `ModifiedByDeviceId` on a single existing row.
*Experience:* The temptation was to describe this as "stable and unique." It is stable, not unique — and the first draft's commit message claimed both. Epic 2 needs duplicate-peer detection at handshake anyway, which is where uniqueness actually belongs; it is recorded in *Follow-ups* rather than half-solved here.

**Decision: wire `DeviceIdentity` through a new `AppDbContext.DeviceIdProvider` property rather than through DI.**
*Why:* `AppDbContext` is parameterless-constructed (`App.xaml.cs:43`, `TestDb.Create`), never resolved from the container, so it cannot take a constructor dependency. The file had already hit this exact problem for the clock and solved it with a settable `Func<DateTime> Clock` property whose comment explicitly says "AppDbContext is parameterless-constructed… so a plain delegate keeps Data decoupled from Services" (`AppDbContext.cs:15-19`).
*What for:* One consistent seam instead of two mechanisms for the same shape of problem, and `AppDbContext.cs:103`/`:109` — the single stamping seam all 12 write sites pass through — is where identity actually has to change for the package to mean anything.
*Experience:* The default stays `DeviceHelper.GetId()`, which does no I/O. That matters: had the default been `DeviceIdentity`, every test constructing an `AppDbContext` would have started writing to the real `%APPDATA%` — reintroducing precisely the defect WP-2 Task 2.2 removes.

**Decision: default `MucDoCanhBao` on the property, not inside the 4-arg constructor.**
*Why:* A property initializer also covers the parameterless constructor and object-initializer construction — both used in tests and both reachable from a future sync-apply path.
*What for:* One edit closes every non-UI write path rather than the one the CSA happened to name.
*Experience:* `"An toàn"` is not arbitrary — it is exactly what `QuanLyTaskViewModel.TinhDiemVaSapXep()` (`:107-110`) assigns for `DiemUuTien < 50`, and a new task starts at 0. The default therefore agrees with the VM instead of competing with it.

**Decision: do not fix `ComputeWeeklyMinutes`' fixed 7-bucket output, despite it contradicting the range selector.**
*Why:* The Analytics 2-section redesign is already designed and queued (`docs/plans/2026-07-20-analytics-two-section-redesign.md`). Fixing the bucketing in isolation means designing the 30/90-day chart twice.
*What for:* Keeps stabilization out of a surface that is about to be rebuilt.
*Experience:* This is the clearest case in the plan of the brief's "avoid work that delays Epic 2 without meaningful benefit." It is a real finding, correctly deferred rather than dismissed.

**Decision: record three CSA corrections rather than silently working around them.**
*Why:* The CSA is the sole input, and `Prompt/2026-07-27-post-epic1-cleanup.md` says to assume every finding is verified. Three did not survive contact with the files: the `Assets/` reference resolution, the phantom duplicate test cases, and the notification-timer anchor. Working from them unamended would have scheduled work that does not exist.
*What for:* So the next reader of the CSA is not misled, and so no phantom dedup task is created.
*Experience:* The `Assets/` correction came from the owner's recollection that a copy already existed inside the project. It was right, and it inverted a CSA finding. Worth remembering that "assume verified" applies to findings, not to anchors and paths.

**Decision: leave the 192 build warnings, the 184 wall-clock call sites, and the ViewModel/DI debt entirely alone.**
*Why:* Each is real, each is large, and none affects Epic 2's correctness. The brief's constraints are explicit: no redesign, no speculative improvement, conservative.
*What for:* Keeps the stabilization diff small enough that the changes that *do* matter are reviewable.
*Experience:* The temptation with a 192-warning count is to treat it as a to-do. It is a measurement. CI will keep measuring it.

---

# Follow-ups

Not scheduled — flagged for the owner.

- **Gate G4 (tombstone retention)** must be an explicit Epic 2 planning item. WP-3 makes tombstones invisible to readers; nothing decides how long they live.
- **Hostname collision remains unfixed after WP-3 Task 3.2.** Two machines sharing a hostname seed to the same persisted device ID. Seeding is deliberate — randomising would break existing installs' row history — so Epic 2 must detect duplicate peer IDs at handshake rather than trusting the ID to be unique.
- **Gate G2 (SOE pass semantics)** remains open and blocks Epic 3. The Master Plan's order is E1 → E3 → E2; confirm that running E2 first is intended.
- **The #46/#47 redesign** stays parked in `origin/dev`'s history behind `d3deca7`, reachable but not in the tree. Standing owner ask: mention it at the start of each continuing session. Use content diffs (`git diff ui_rf origin/dev`), never `is-ancestor` — earlier `-X ours` syncs poisoned ancestry on this repo.
- **The GitNexus index is stale** (last analyzed at `d3deca7`; HEAD is `fcd4b42`+). WP-6.2 refreshes it as a side effect of reconciling the symbol counts.
- **If WP-2 Task 2.1 fails**, a real 31–60-day priority-band defect exists and becomes a new Category A finding.
- **After WP-4**, deduplicating the two divergent raw-minutes formulas becomes safe. Recorded as unblocked, deliberately unscheduled.
