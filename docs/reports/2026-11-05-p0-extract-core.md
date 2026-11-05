# Mobile P0 — Extract `SmartStudyPlanner.Core` (Phase 0: recon + move manifest)

- **Date:** 2026-11-05
- **Author/agent:** Claude Code (Opus 5.5), worktree `.claude/worktrees/p0-core`, branch `refactor-p0-extract-core`
- **Status:** Phase 0 delivered. **STOPPED for the owner.** No production or test code has been written.
  Phases 1–3 (move, verify, docs+PR) wait on the answers in §7.
- **Authority:** mobile spec `docs/specs/2026-10-31-mobile-companion-direction.md` §4.1 (layout, INV-1, INV-2),
  §8 phase table row P0, then CLAUDE.md, then code on `origin/dev`.

## 1. Scope

Phase 0 of P0 covers recon only:
- a per-file manifest for all 223 `.cs` files in `SmartStudyPlanner/`;
- the reverse edges found with a compiler probe;
- internals, packages and risks;
- owner questions.

It does **not** move any file. Not in scope: ViewModels, C-6 `MaTask`, C-7 study windows, Avalonia, TokenGen (P2),
and G3-1 wiring.

## 2. Preconditions and baseline (OBSERVED)

| Check | Result |
|---|---|
| PR #116 (G3-1 Phase 0) | **MERGED** 2026-11-05T00:44Z, merge commit `93cec97` |
| Open PRs (`gh pr list --state open`) | **none** (`[]`), so the freeze window is satisfied |
| `origin/dev` | `93cec97` |
| local `dev` | `c58b0fa`, **3 commits behind `origin/dev`, 0 ahead**. Not touched; the owner pulls. |
| Worktree base | `origin/dev` @ `93cec97` |
| Release build `SmartStudyPlanner.slnx` | **0 errors, 96 warning lines** (56 unique after normalising paths/project suffix) |
| Full suite (Release) | **1164 passed / 0 failed / 1 skipped / 1165 total** (skip = `SoeBaselineCaptureTests.CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing`, pre-existing) |

The normalised baseline warning list is kept at the job scratch path and will be regenerated in Phase 2 from the same
commit, so the diff is like-for-like. Warning kinds: CS8618×15, CS8625×4, CS8622×2, CS8602×1, xUnit1031×2,
xUnit2031×2, NU1903/NU1904 package advisories.

## 3. Method

1. Inventory every `.cs` file under `SmartStudyPlanner/`, excluding `bin/` and `obj/`, and grep for platform
   markers: `System.Windows`, `Dispatcher`, `System.Windows.Forms`/`System.Drawing`, `Microsoft.ML`,
   CommunityToolkit, LiveCharts, Toast, `ServiceLocator`, `ThemeManager`, `BaseDirectory`/`Assembly`.
2. **Compiler probe (authoritative).** Build a scratch `net10.0` project (`SmartStudyPlanner.Core`, Nullable +
   ImplicitUsings as in the app, EF Core Sqlite 10.0.5 only, no WPF, no ML.NET) that *links* the candidate
   `→ Core` files in place. Read the errors, then iterate.
   - Pass 1 (greedy: everything without `using Microsoft.ML`): 6 errors, all from `StudyTimeInput`.
   - Pass 2: 2 errors (`IMLModelManager` ← `StudyTimePredictorService`).
   - Pass 3 (brief default for `Services/ML`): 4 errors (`ITextClassifierModelManager` → `Schema/TextClassifierInput`).
   - **Pass 4 (final set, 176 files): exit 0, 0 errors.** Its only warnings are CS8618 in `Models/*` and the NU1903
     advisory, both already in the baseline.
   - Probe "no `Services/ML` at all" shows which ML files Core really needs (§4).
3. Read every test that anchors on a source path or on `typeof(X).Assembly`.
4. GitNexus: **NOT USABLE.** The `Smart-Study` index is 63 commits behind and indexes the main checkout
   (`D:\Code\C#\SmartStudyPlanner`, local `dev`), not this worktree. Its impact output would describe stale code, so
   the compiler probe stands in for it. See OQ-8.

## 4. Findings

### 4.1 Manifest summary

The full per-file table is in Appendix A.

| Label | Files | Content |
|---|---|---|
| **→ Core** | **176** | `Core/**` (28), `Models/**` (16), `Data/**` (9), `Infrastructure/**` (28), `Sync/**` (40), `Services/*.cs` except `ServiceLocator`/`ThemeManager` (8), `Services/{Analytics,Pipeline,Soe,Strategies,Telemetry}/**` (42), `Services/ML/{DefaultMlConfidencePolicy,DeviceHelper,DeviceIdentity,IModelStorageProvider,IStudyTimePredictor}.cs` (5) |
| **stays WPF** | **47** | UI (`ViewModels`, `Views`, `Controls`, `Converters`) + `App.xaml.cs`, `AssemblyInfo.cs`, `ServiceLocator.cs`, `ThemeManager.cs`, and 20 `Services/ML/**` files |
| **needs split** | **0** | — |

Non-`.cs` files: `Themes/`, XAML, `Assets/icon.ico`, `entities.json` and `mempalace.yaml` stay in WPF.
`entities.json` and `mempalace.yaml` are mempalace tooling files that no code or csproj reads.
`Services/ML/TextClassifier/seed_intents.csv` stays with `TextClassifierModelManager`, which reads it through
`Assembly.GetExecutingAssembly()`.

**Recon corrections to the brief's "known state":**
- The app csproj also sets **`UseWindowsForms=true`**, which adds implicit `System.Windows.Forms`/`System.Drawing`
  usings. Pass 4 proves no `→ Core` file depends on them.
- The `ServiceLocator` mentions in `Sync/Apply/SyncApplySession.cs` and `LocalModelStorageProvider.cs` are **comments
  only**.
- `CommunityToolkit.Mvvm`, LiveCharts and Toast are used **only** inside UI folders.
- The `DeviceIdentity` cycle the brief warned about does **not** occur: `DeviceIdentity`, `AppDbContext` and
  `SyncSchema` all move to Core together.

### 4.2 Reverse edges (a `→ Core` file using a `stays WPF` type)

Every edge the compiler reported lives **inside `Services/ML`**. No file in `Models/Core/Data/Infrastructure/Sync`
or the non-ML `Services` uses a WPF-side type.

| # | Edge | Cause | Proposed fix (no code change) |
|---|---|---|---|
| E-1 | `IMLModelManager`, `IStudyTimeTrainingDataSource`, `StudyTimeTrainingDataSource`, `SeedDataGenerator` → `Schema/StudyTimeInput` | `StudyTimeInput` carries ML.NET `[ColumnName("Label")]` | **Keep all four in WPF.** Removing the attribute would change ML behaviour, which is out of scope. |
| E-2 | `StudyTimePredictorService` → `IMLModelManager` | follows E-1 | Keep in WPF (it is an ML implementation anyway). |
| E-3 | `ITextClassifierModelManager` → `Schema/TextClassifierInput`, `TextClassifierPrediction` | not a real edge: both DTOs are ML-free and compile in Core (pass 1) | Keep the contract and DTOs in WPF for P0: only ML implementations consume them. See OQ-3. |

What Core actually needs from `Services/ML`, from the "no ML files" probe:
- `IStudyTimePredictor` + `StudyTimePredictionResult`, used by `IDecisionEngine`, `DecisionEngineService`,
  `ISchedulingOrchestrator` and `SchedulingOrchestrator`;
- the `SmartStudyPlanner.Services.ML` namespace for `DeviceIdentity`, used by `AppDbContext` and `SyncSchema`.

`DefaultMlConfidencePolicy`, `DeviceHelper` and `IModelStorageProvider` are ML-free helpers or contracts that go along.

### 4.3 Internals across the new boundary

Proposal: Core gets its own `AssemblyInfo.cs` with `InternalsVisibleTo("SmartStudyPlanner")` and
`InternalsVisibleTo("SmartStudyPlanner.Tests")`. **No visibility is widened.** The WPF `AssemblyInfo.cs` stays as it
is, with `ThemeInfo` and its IVT to Tests.

| Consumer | Internal Core members used |
|---|---|
| WPF | `DeviceHelper` (`MLModelManager`, `TextClassifierModelManager`, `FocusViewModel`); `LocalSaveRejection` (`App.xaml.cs`) |
| Tests | ~30 internal types/members: `FenceRouter`, `ImpactResolver`, `ConflictDependencySelector`, the fence policies, `FencePolicyRegistry`, `ConflictStaging`, `EntitySnapshotMapper`, `LocalSemesterSaveExecutor` (+ `LoadLiveGraphAsync`, `ApplyIfPassedAsync`), `LocalTaskEditorExecutor.ApplyIfPassed`, `SemesterReconcilePlanner` and its plan types, `SemesterGraphWriter`, `TaskEditorPlanner`/`Writer`, `TaskCascadeHelper`, `SyncStamper.TryGetSpec`, `CanonicalJson.Sha256Hex`, `WeightConfigStore.LoadFrom/SaveTo`, `WorkloadServiceImpl.GenerateScheduleWithIdentity`, `DeviceHelper`, `LocalSaveRejection` |

### 4.4 Packages

| Package | Core | WPF |
|---|---|---|
| Microsoft.EntityFrameworkCore.Sqlite 10.0.5 | **add** | keep its direct reference unchanged (proposal: an identical restore graph keeps the warnings identical) |
| Microsoft.Extensions.DependencyInjection 10.0.7 | — (only `ServiceLocator` uses DI) | keep |
| Microsoft.ML, Microsoft.ML.FastTree 3.0.1 | **— (INV-2)** | keep |
| CommunityToolkit.Mvvm, LiveChartsCore WPF, Toolkit.Uwp.Notifications | — | keep |

Core csproj: `net10.0`, `Nullable`/`ImplicitUsings` enabled as in the app, and `RootNamespace=SmartStudyPlanner` so
the folder-to-namespace convention stays true. The Tests csproj adds an explicit `ProjectReference` to Core next to
the app reference.

### 4.5 Tests that anchor on source paths or assemblies

| Test file | Anchor today | After move | Proposed edit (needs owner approval, OQ-2) |
|---|---|---|---|
| `Infrastructure/.../Mutations/SyncedEntityWritePathFenceTests.cs` (**X-19**, OD-5 entry) | `ProductionRoot()` = `SmartStudyPlanner/`, allowlist keyed by root-relative path, self-check `files.Count > 50` | about 32 `.cs` files left, so it **fails loudly** | scan **both** roots, computing the relative path per root; allowlist keys stay identical if OQ-1 = mirror layout |
| `Infrastructure/.../Repositories/StudyTaskRepositoryMutationGuardTests.cs` (**OD-5 guard**) | whole tree, `> 50` | fails loudly | both roots |
| `Infrastructure/.../Mutations/MutationRejectedCatchSourceScanTests.cs` | whole tree, `> 50` | fails loudly | both roots |
| `Sync/Apply/SyncApplyAuditFenceTests.cs` | whole tree, `> 50` + `NotEmpty` | fails loudly | both roots |
| `Sync/Apply/ConflictResolverTests.cs` `FindProductionFile` | file by name under `SmartStudyPlanner/` | `Assert.NotNull` fails | search both roots |
| `Sync/Fence/FenceSourceFenceTests.cs` | `SmartStudyPlanner/Sync/Fence`, `>= 15` | `DirectoryNotFoundException` | `SmartStudyPlanner.Core/Sync/Fence` |
| `Sync/Merge/MergeCorePurityTests.cs` (source half) | `SmartStudyPlanner/Sync/Merge`, `>= 8` | throws | `SmartStudyPlanner.Core/Sync/Merge` |
| `Services/Soe/ObjectiveEvaluatorTests.cs` `SourceFiles_ContainNoHanChotOrDeadlineToken` | `SmartStudyPlanner/Services/Soe` | throws | `SmartStudyPlanner.Core/Services/Soe` |
| `Views/WorkloadBalancerPageSourceTests.cs` line 63 | `SmartStudyPlanner/Services/IWorkloadService.cs` | throws (the XAML path on line 25 stays valid) | `SmartStudyPlanner.Core/...` |

**No edit needed** (OBSERVED):
- `MergeCorePurityTests` reflection half and F-4
  (`LocalTaskEditorFenceTests.EveryProductionTaskEditorRepository_...`) use `typeof(X).Assembly`, and the anchor
  type moves together with every type they inspect. `SqliteTaskEditorRepository` is the only implementer of
  `ITaskEditorRepository`.
- `LocalModelStorageTests` uses an AppData path.
- The SOE baseline tests use `docs/reports/data/...`.

Every path-anchored scanner fails **loudly** after the move: either `> 50`/`>= N` self-checks or
`DirectoryNotFound`/`NotNull`. None would pass vacuously, so a missed anchor cannot slip through Phase 2.

### 4.6 Risks

| Risk | Finding |
|---|---|
| EF Core migrations / design-time factory | **None exist.** No `IDesignTimeDbContextFactory`, no `Migrations/` folder and no `Migrate()` call in production code. |
| `AppDomain.BaseDirectory` paths (`SmartStudyData.db` in `AppDbContext`, `capacity.txt` in `WorkloadServiceImpl`, `streak_data.json` in `ServiceLocator`) | Unchanged: `BaseDirectory` is the exe folder and `SmartStudyPlanner.Core.dll` is copied next to the exe. |
| `Assembly.GetExecutingAssembly()` | Only `TextClassifierModelManager`, which stays in WPF with its embedded CSV. |
| XAML `clr-namespace` without `;assembly=` | The XAML maps only `SmartStudyPlanner`, `.Controls`, `.Converters` (all WPF) and LiveCharts. No moved namespace is referenced from XAML. INFERENCE: the Phase 1 build is the proof. |
| Pack URIs | Only `ThemeManager` (WPF). |
| Legacy `SmartStudyPlanner/SmartStudyPlanner.sln` | Lists only the app project. `docs/architecture/overview.md` says to use `.slnx`; CI uses `.slnx`. It keeps building through the ProjectReference. OQ-6. |
| Warning attribution | CS8618 (`Models/*`) and NU1903 will also be attributed to `SmartStudyPlanner.Core.csproj`. The Phase 2 diff normalises file+code and ignores the project suffix; one extra NU1903 line for the new project is expected. |
| INV-2 placement | `IStudyTimePredictor` lands in Core at `Services/ML/` (namespace `SmartStudyPlanner.Services.ML`), not under `Core/ML/Contracts`, because the namespace must not change in P0. OQ-7. |
| F-4 scope after split (INFERENCE) | F-4 scans only the Core assembly. A future `ITaskEditorRepository` implementation in WPF would escape it. Today there is none. OQ-9. |

## 5. Verification

| Command | Result |
|---|---|
| `gh pr view 116`, `gh pr list --state open` | MERGED, `[]` |
| `git rev-parse origin/dev dev`, `rev-list --left-right --count` | `93cec97` / `c58b0fa`, 0 ahead / 3 behind |
| `dotnet build SmartStudyPlanner.slnx -c Release` | 0 errors, 96 warnings |
| `dotnet test SmartStudyPlanner.Tests -c Release --no-build` | 1164 / 0 / 1 / 1165 |
| scratch Core build, passes 1–4 | 6 → 2 → 4 → **0 errors** |
| GitNexus | NOT RUN (stale index, wrong checkout) |

## 6. Follow-ups (non-blocking, not P0)

- Move `IStudyTimePredictor` to `Core/ML/Contracts` with a namespace change, once INV-2 placement is enforced (OQ-7).
- Decide the home of the ML-free `Services/ML` files when the mobile "ML off" implementation is designed (OQ-3).
- Widen F-4 to both assemblies if the owner wants it (OQ-9).
- Package advisories NU1903/NU1904 (SQLitePCLRaw 2.1.11, System.Drawing.Common 4.7.0) are pre-existing.

## 7. Owner questions

Each question has a recommendation (**R**). Phase 1 starts only after these are answered.

1. **OQ-1 — Folder layout inside Core.** R: **mirror today's layout exactly**: `SmartStudyPlanner.Core/Core/...`,
   `SmartStudyPlanner.Core/Models/...`, `SmartStudyPlanner.Core/Sync/...` and so on, with
   `RootNamespace=SmartStudyPlanner`. Paths relative to each project root are unchanged, so the X-19 allowlist keys
   stay valid and folder still matches namespace. The alternative is to flatten `Core/` into the project root
   (`SmartStudyPlanner.Core/Scheduling`); it reads more nicely but changes the relative paths that the allowlists use.
2. **OQ-2 — Test anchor edits (9 files, §4.5).** R: approve **path-only** edits. Each file's private
   `ProductionRoot()` becomes "both project roots", or is re-pointed to `SmartStudyPlanner.Core/...`, in place and in
   each file's own style (no new shared helper). No assertion, allowlist, threshold or regex changes. Also confirm
   that the stop condition "a file under `Sync/**` or `Mutations/**` needs a code change" covers **production**
   files only: 4 of these test files sit under `Tests/Sync/**` or `Tests/.../Mutations/**`.
3. **OQ-3 — ML-free `Services/ML` files that Core does not need.** These are `ITextClassifierModelManager`,
   `TextClassifierService`, `IntentClassifierAdapter`, `TextClassifierDatasetImporter`, `LocalModelStorageProvider`,
   `WeightOptimizer/*`, `Schema/{ModelMeta,TextClassifierInput,TextClassifierPrediction}`. R: **keep them in WPF for
   P0.** They compile ML-free, but only ML implementations or `ServiceLocator` use them; moving them is cheap later
   and avoids guessing the mobile ML-off shape now. Alternative: move all of them now, since pass 1 proved it compiles.
4. **OQ-4 — IVT.** R: Core gets IVT to `SmartStudyPlanner` and `SmartStudyPlanner.Tests`, with no visibility
   widening. Confirm.
5. **OQ-5 — Packages + warning diff rule.** R: Core gets **only** EF Core Sqlite 10.0.5; the WPF csproj is unchanged.
   The Phase 2 warning diff compares normalised `file(line,col): code` and ignores the project suffix. An extra NU1903
   line for `SmartStudyPlanner.Core.csproj` is accepted as expected. Confirm.
6. **OQ-6 — Legacy `SmartStudyPlanner/SmartStudyPlanner.sln`.** R: **leave it untouched** (it still builds, and
   deleting it is not P0). Alternatives: delete it, or add Core to it.
7. **OQ-7 — INV-2 contract location.** R: accept that `IStudyTimePredictor`/`StudyTimePredictionResult` live in Core
   under `Services/ML` with an unchanged namespace for P0, and record it in the spec as a known deviation with a
   follow-up.
8. **OQ-8 — GitNexus.** The index is 63 commits stale and sees the main checkout. R: Phase 2 records
   `gitnexus_detect_changes` as **NOT RUN** with this reason, with the compiler + full suite + `git diff -M` as the
   evidence. Alternative: the owner pulls local `dev` and runs `npx gitnexus analyze`, but the worktree would still
   not be indexed.
9. **OQ-9 — F-4 scope.** R: no change in P0 (it would be a semantic test edit); file it as a follow-up.
10. **OQ-10 — INV-1 guard test placement.** R: a new test `SmartStudyPlanner.Tests/Core/CoreAssemblyPurityTests.cs`.
    It asserts that `typeof(ThreeWayMerge).Assembly.GetReferencedAssemblies()` contains none of
    `PresentationFramework`, `PresentationCore`, `WindowsBase`, `System.Windows.Forms`, `Microsoft.ML*`. Its self-check
    asserts that the anchor assembly is named `SmartStudyPlanner.Core` and that the same probe **does** find
    `PresentationFramework` on `typeof(App).Assembly`. Test count becomes **1166**: +1 new file, approved here, the
    only count change. Confirm this is the one allowed count delta.

## 8. Decisions made

### D-1 — Compiler probe, not grep or GitNexus, defines the reverse edges
- *Why it had to be made:* the brief allows either. GitNexus is 63 commits stale and indexes another checkout, and
  grep cannot see transitive type use (for example, `IMLModelManager` has no `using Microsoft.ML` but still depends
  on ML.NET through `StudyTimeInput`).
- *What it's for:* the edge list is exactly what Phase 1's real build will report, with no guessing.
- *Experience:* a linked-file scratch csproj (`EnableDefaultCompileItems=false` + `<Compile Include Link>`) probes a
  project split without touching the tree. Iterate passes, because the first error wave hides later ones.

### D-2 — Smallest Core: only what Core code needs, plus ML-free helpers
- *Why it had to be made:* pass 1 showed that 15 more `Services/ML` files would compile in Core. Moving them is a
  design choice about the mobile ML-off shape that P0 must not pre-empt.
- *What it's for:* P0 stays a pure location change. INV-2 holds: Core contains no ML.NET reference and no ML
  implementation.
- *Experience:* "it compiles in Core" does not mean "it belongs in Core". OQ-3 keeps that decision with the owner.

### D-3 — Phase 0 stops before writing any test edit
- *Why it had to be made:* every path-anchored guard would fail after the move, and changing a guard test is exactly
  what needs owner sign-off (brief Phase 2 + stop conditions).
- *What it's for:* the owner sees all 9 edits, and why each one fails loudly rather than vacuously, before approving.

## Appendix A — Full manifest (223 files)

| # | File (under `SmartStudyPlanner/`) | Label | Reason |
|---|---|---|---|
| 1 | `Core/ML/Contracts/IIntentClassifierService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 2 | `Core/ML/Contracts/IMlConfidencePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 3 | `Core/ML/Contracts/IWeightOptimizerService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 4 | `Core/Parsing/Contracts/IIntentClassifier.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 5 | `Core/Parsing/Contracts/IParsingOrchestrator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 6 | `Core/Parsing/Contracts/ITaskExtractionEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 7 | `Core/Parsing/Contracts/ITimeParsingEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 8 | `Core/Parsing/Engines/RuleBasedTimeParsingEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 9 | `Core/Parsing/Engines/TaskExtractionEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 10 | `Core/Parsing/Models/ParseResult.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 11 | `Core/Parsing/Orchestrators/ParsingOrchestrator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 12 | `Core/Risk/Aggregators/RiskAggregator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 13 | `Core/Risk/Contracts/IRiskAnalyzer.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 14 | `Core/Risk/Contracts/IRiskFactorEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 15 | `Core/Risk/Evaluators/DeadlineUrgencyRiskEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 16 | `Core/Risk/Evaluators/PerformanceDropRiskEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 17 | `Core/Risk/Evaluators/ProgressGapRiskEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 18 | `Core/Risk/Models/RiskAssessment.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 19 | `Core/Risk/Models/RiskLevel.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 20 | `Core/Risk/RiskOrchestrator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 21 | `Core/Scheduling/Contracts/IPriorityEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 22 | `Core/Scheduling/Contracts/IRawMinutesCalculator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 23 | `Core/Scheduling/Contracts/ISchedulingOrchestrator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 24 | `Core/Scheduling/Contracts/IStudyTimeSuggestionEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 25 | `Core/Scheduling/Engines/RawMinutesCalculator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 26 | `Core/Scheduling/Engines/StudyTimeSuggestionEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 27 | `Core/Scheduling/Evaluators/PriorityEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 28 | `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 29 | `Data/AppDbContext.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 30 | `Data/AppStartup.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 31 | `Data/DbBackup.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 32 | `Data/MigrationReporter.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 33 | `Data/SyncBaseSnapshotSchema.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 34 | `Data/SyncConflictRecordSchema.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 35 | `Data/SyncSchema.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 36 | `Data/SyncStamper.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 37 | `Data/TelemetrySchema.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 38 | `Infrastructure/Persistence/Repositories/IDifficultyLabelLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 39 | `Infrastructure/Persistence/Repositories/IHocKyRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 40 | `Infrastructure/Persistence/Repositories/IMonHocRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 41 | `Infrastructure/Persistence/Repositories/IStudyLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 42 | `Infrastructure/Persistence/Repositories/IStudyTaskRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 43 | `Infrastructure/Persistence/Repositories/IStudyTimeOutcomeLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 44 | `Infrastructure/Persistence/Repositories/ITaskEditorRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 45 | `Infrastructure/Persistence/Repositories/IUserStatsRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 46 | `Infrastructure/Persistence/Repositories/IWeightChangeLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 47 | `Infrastructure/Persistence/Repositories/UserStatsSnapshot.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 48 | `Infrastructure/Persistence/SQLite/Mutations/LocalSaveRejection.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 49 | `Infrastructure/Persistence/SQLite/Mutations/LocalSemesterSaveExecutor.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 50 | `Infrastructure/Persistence/SQLite/Mutations/LocalTaskEditorExecutor.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 51 | `Infrastructure/Persistence/SQLite/Mutations/SemesterGraphRestorer.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 52 | `Infrastructure/Persistence/SQLite/Mutations/SemesterGraphWriter.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 53 | `Infrastructure/Persistence/SQLite/Mutations/SemesterReconcilePlanner.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 54 | `Infrastructure/Persistence/SQLite/Mutations/TaskEditorPlanner.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 55 | `Infrastructure/Persistence/SQLite/Mutations/TaskEditorWriter.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 56 | `Infrastructure/Persistence/SQLite/Repositories/SqliteDifficultyLabelLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 57 | `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 58 | `Infrastructure/Persistence/SQLite/Repositories/SqliteMonHocRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 59 | `Infrastructure/Persistence/SQLite/Repositories/SqliteStudyLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 60 | `Infrastructure/Persistence/SQLite/Repositories/SqliteStudyTaskRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 61 | `Infrastructure/Persistence/SQLite/Repositories/SqliteStudyTimeOutcomeLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 62 | `Infrastructure/Persistence/SQLite/Repositories/SqliteTaskEditorRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 63 | `Infrastructure/Persistence/SQLite/Repositories/SqliteUserStatsRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 64 | `Infrastructure/Persistence/SQLite/Repositories/SqliteWeightChangeLogRepository.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 65 | `Infrastructure/Persistence/SQLite/TaskCascadeHelper.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 66 | `Models/HeatCell.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 67 | `Models/HocKy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 68 | `Models/ISyncMetadata.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 69 | `Models/MonHoc.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 70 | `Models/MonHocIdentity.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 71 | `Models/ScheduleModels.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 72 | `Models/StudyLog.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 73 | `Models/StudyTask.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 74 | `Models/StudyTaskStatus.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 75 | `Models/TaskDashboardItem.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 76 | `Models/TaskEditorBundle.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 77 | `Models/TaskNote.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 78 | `Models/TaskReferenceLink.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 79 | `Models/Telemetry/DifficultyLabelLog.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 80 | `Models/Telemetry/StudyTimeOutcomeLog.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 81 | `Models/Telemetry/WeightChangeLog.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 82 | `Services/Analytics/IStudyAnalytics.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 83 | `Services/Analytics/Models/ProductivityScore.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 84 | `Services/Analytics/Models/SubjectInsight.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 85 | `Services/Analytics/Models/WeeklyReport.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 86 | `Services/Analytics/StudyAnalyticsService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 87 | `Services/CrashLogger.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 88 | `Services/DecisionEngineService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 89 | `Services/IDecisionEngine.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 90 | `Services/IWorkloadService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 91 | `Services/ML/DefaultMlConfidencePolicy.cs` | → Core | ML-free; required by Core (`IStudyTimePredictor` ← DecisionEngine/SchedulingOrchestrator; `DeviceIdentity` ← AppDbContext/SyncSchema) or ML-free helper |
| 92 | `Services/ML/DeviceHelper.cs` | → Core | ML-free; required by Core (`IStudyTimePredictor` ← DecisionEngine/SchedulingOrchestrator; `DeviceIdentity` ← AppDbContext/SyncSchema) or ML-free helper |
| 93 | `Services/ML/DeviceIdentity.cs` | → Core | ML-free; required by Core (`IStudyTimePredictor` ← DecisionEngine/SchedulingOrchestrator; `DeviceIdentity` ← AppDbContext/SyncSchema) or ML-free helper |
| 94 | `Services/ML/IModelStorageProvider.cs` | → Core | ML-free; required by Core (`IStudyTimePredictor` ← DecisionEngine/SchedulingOrchestrator; `DeviceIdentity` ← AppDbContext/SyncSchema) or ML-free helper |
| 95 | `Services/ML/IStudyTimePredictor.cs` | → Core | ML-free; required by Core (`IStudyTimePredictor` ← DecisionEngine/SchedulingOrchestrator; `DeviceIdentity` ← AppDbContext/SyncSchema) or ML-free helper |
| 96 | `Services/Pipeline/PipelineContracts.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 97 | `Services/Pipeline/PipelineOrchestrator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 98 | `Services/Pipeline/Stages/AdaptStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 99 | `Services/Pipeline/Stages/AssessRiskStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 100 | `Services/Pipeline/Stages/BalanceWorkloadStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 101 | `Services/Pipeline/Stages/ParseInputStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 102 | `Services/Pipeline/Stages/PrioritizeStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 103 | `Services/Soe/ConstraintValidator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 104 | `Services/Soe/Eval.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 105 | `Services/Soe/IConstraintValidator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 106 | `Services/Soe/IDeadlinePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 107 | `Services/Soe/IObjectiveEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 108 | `Services/Soe/IOptimizerStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 109 | `Services/Soe/IScheduleOptimizer.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 110 | `Services/Soe/LoadRebalanceStage.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 111 | `Services/Soe/ObjectiveEvaluator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 112 | `Services/Soe/ObjectiveScore.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 113 | `Services/Soe/OptimizeReport.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 114 | `Services/Soe/OptimizerComparator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 115 | `Services/Soe/OptimizerRunLogRow.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 116 | `Services/Soe/OptimizerRunLogWriter.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 117 | `Services/Soe/ScheduleOptimizer.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 118 | `Services/Soe/ScheduledItem.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 119 | `Services/Soe/SoeWeights.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 120 | `Services/Strategies/IClock.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 121 | `Services/Strategies/IDeadlineKeywordParser.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 122 | `Services/Strategies/IDifficultyKeywordParser.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 123 | `Services/Strategies/IKeywordRule.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 124 | `Services/Strategies/IPriorityComponent.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 125 | `Services/Strategies/ITaskTypeKeywordParser.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 126 | `Services/Strategies/ITaskTypeWeightProvider.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 127 | `Services/Strategies/IUrgencyRule.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 128 | `Services/Strategies/PriorityCalculator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 129 | `Services/StreakManager.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 130 | `Services/Telemetry/DebugStudyTelemetry.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 131 | `Services/Telemetry/IOutcomeMaturationService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 132 | `Services/Telemetry/IStudyTelemetry.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 133 | `Services/Telemetry/OutcomeMaturationService.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 134 | `Services/WeightConfig.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 135 | `Services/WeightConfigStore.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 136 | `Services/WorkloadServiceImpl.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 137 | `Sync/Apply/ConflictResolutionModels.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 138 | `Sync/Apply/ConflictResolver.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 139 | `Sync/Apply/ConflictStaging.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 140 | `Sync/Apply/EntitySnapshotMapper.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 141 | `Sync/Apply/IncomingChanges.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 142 | `Sync/Apply/SyncApplyModels.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 143 | `Sync/Apply/SyncApplySession.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 144 | `Sync/ChangedEntity.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 145 | `Sync/Fence/ConflictDependencySelector.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 146 | `Sync/Fence/ConflictShapeClassifier.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 147 | `Sync/Fence/FencePolicyRegistry.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 148 | `Sync/Fence/FenceResults.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 149 | `Sync/Fence/FenceRouter.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 150 | `Sync/Fence/IConflictFencePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 151 | `Sync/Fence/ImpactResolver.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 152 | `Sync/Fence/ImpactSet.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 153 | `Sync/Fence/MutationRejectedException.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 154 | `Sync/Fence/MutationRequest.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 155 | `Sync/Fence/Policies/AbsentLocalParentTombstonePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 156 | `Sync/Fence/Policies/ConcurrentReparentFencePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 157 | `Sync/Fence/Policies/ConstraintOccupancyFencePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 158 | `Sync/Fence/Policies/ParentTombstoneFencePolicy.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 159 | `Sync/Fence/ProtectedContract.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 160 | `Sync/Fence/StructuralDependencyRegistry.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 161 | `Sync/Merge/CanonicalJson.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 162 | `Sync/Merge/ConflictKeys.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 163 | `Sync/Merge/ConstraintMerge.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 164 | `Sync/Merge/Lww.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 165 | `Sync/Merge/MergeDtos.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 166 | `Sync/Merge/MergeExceptions.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 167 | `Sync/Merge/MergeOutcomes.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 168 | `Sync/Merge/MergeSurfaceRegistry.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 169 | `Sync/Merge/ThreeWayMerge.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 170 | `Sync/PeerChangeSet.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 171 | `Sync/SyncBaseSnapshotRow.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 172 | `Sync/SyncBaseSnapshotStore.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 173 | `Sync/SyncChangeEnumerator.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 174 | `Sync/SyncConflictRecordRow.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 175 | `Sync/SyncConflictRecordStore.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 176 | `Sync/SyncEntityTypes.cs` | → Core | non-UI; compiles in net10.0 scratch without WPF/ML |
| 177 | `App.xaml.cs` | stays WPF | WPF app entry |
| 178 | `AssemblyInfo.cs` | stays WPF | WPF `ThemeInfo` + IVT to Tests (Core gets its own IVT file) |
| 179 | `Controls/DonutChart.xaml.cs` | stays WPF | UI |
| 180 | `Converters/DashboardConverters.cs` | stays WPF | UI |
| 181 | `Converters/HeatLevelToBrushConverter.cs` | stays WPF | UI |
| 182 | `Converters/WorkloadConverters.cs` | stays WPF | UI |
| 183 | `Services/ML/IMLModelManager.cs` | stays WPF | signature uses `Schema.StudyTimeInput` (ML.NET `[ColumnName]`) |
| 184 | `Services/ML/IStudyTimeTrainingDataSource.cs` | stays WPF | signature uses `StudyTimeInput` |
| 185 | `Services/ML/ITextClassifierModelManager.cs` | stays WPF | ML-free, but ML-model-manager contract; only ML impls consume it (OQ-3) |
| 186 | `Services/ML/IntentClassifierAdapter.cs` | stays WPF | ML-free adapter (OQ-3) |
| 187 | `Services/ML/LocalModelStorageProvider.cs` | stays WPF | ML-free model-file storage; only ML impls consume it (OQ-3) |
| 188 | `Services/ML/MLModelManager.cs` | stays WPF | `using Microsoft.ML` (INV-2) |
| 189 | `Services/ML/Schema/ModelMeta.cs` | stays WPF | ML model metadata DTO (OQ-3) |
| 190 | `Services/ML/Schema/StudyTimeInput.cs` | stays WPF | `using Microsoft.ML` (INV-2) |
| 191 | `Services/ML/Schema/StudyTimeOutput.cs` | stays WPF | `using Microsoft.ML` (INV-2) |
| 192 | `Services/ML/Schema/TextClassifierInput.cs` | stays WPF | ML schema DTO, ML-free (OQ-3) |
| 193 | `Services/ML/Schema/TextClassifierOutput.cs` | stays WPF | `using Microsoft.ML` (INV-2) |
| 194 | `Services/ML/Schema/TextClassifierPrediction.cs` | stays WPF | ML schema DTO, ML-free (OQ-3) |
| 195 | `Services/ML/SeedDataGenerator.cs` | stays WPF | builds `StudyTimeInput`; ML training seed |
| 196 | `Services/ML/StudyTimePredictorService.cs` | stays WPF | ML impl; depends on `IMLModelManager` |
| 197 | `Services/ML/StudyTimeTrainingDataSource.cs` | stays WPF | builds `StudyTimeInput` |
| 198 | `Services/ML/TextClassifierDatasetImporter.cs` | stays WPF | ML-free training-data importer (OQ-3) |
| 199 | `Services/ML/TextClassifierModelManager.cs` | stays WPF | `using Microsoft.ML` (INV-2) |
| 200 | `Services/ML/TextClassifierService.cs` | stays WPF | ML-free adapter over `ITextClassifierModelManager` (OQ-3) |
| 201 | `Services/ML/WeightOptimizer/WeightOptimizerService.cs` | stays WPF | ML-free rule engine under Services/ML (OQ-3) |
| 202 | `Services/ML/WeightOptimizer/WeightRuleEngine.cs` | stays WPF | ML-free rule engine (OQ-3) |
| 203 | `Services/ServiceLocator.cs` | stays WPF | composition root |
| 204 | `Services/ThemeManager.cs` | stays WPF | uses `System.Windows` (INV-1) |
| 205 | `ViewModels/AnalyticsViewModel.cs` | stays WPF | UI |
| 206 | `ViewModels/DashboardChartModels.cs` | stays WPF | UI |
| 207 | `ViewModels/DashboardViewModel.cs` | stays WPF | UI |
| 208 | `ViewModels/FocusViewModel.cs` | stays WPF | UI |
| 209 | `ViewModels/QuanLyMonHocViewModel.cs` | stays WPF | UI |
| 210 | `ViewModels/QuanLyTaskViewModel.cs` | stays WPF | UI |
| 211 | `ViewModels/SetupViewModel.cs` | stays WPF | UI |
| 212 | `ViewModels/TaskReferenceLinkItemVm.cs` | stays WPF | UI |
| 213 | `ViewModels/WeightOptimizerViewModel.cs` | stays WPF | UI |
| 214 | `ViewModels/WorkloadBalancerViewModel.cs` | stays WPF | UI |
| 215 | `Views/AnalyticsPage.xaml.cs` | stays WPF | UI |
| 216 | `Views/DashboardPage.xaml.cs` | stays WPF | UI |
| 217 | `Views/FocusWindow.xaml.cs` | stays WPF | UI |
| 218 | `Views/MainWindow.xaml.cs` | stays WPF | UI |
| 219 | `Views/QuanLyMonHocPage.xaml.cs` | stays WPF | UI |
| 220 | `Views/QuanLyTaskPage.xaml.cs` | stays WPF | UI |
| 221 | `Views/SetupPage.xaml.cs` | stays WPF | UI |
| 222 | `Views/WeightOptimizerWindow.xaml.cs` | stays WPF | UI |
| 223 | `Views/WorkloadBalancerPage.xaml.cs` | stays WPF | UI |