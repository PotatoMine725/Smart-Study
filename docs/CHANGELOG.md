# Smart Study Planner — Changelog

> Synced 2026-05-21 from `superpowers/reports/*-change-log.md`, `consolidated-change-report.md`, `phase-next-*-report.md`, `m6-1-completion.md`, `dev-reset-clean-slate-report.md`, `ui-ux-phases-a-f-implementation-report.md`, and `bug-report.md`.
>
> Format: one row per shipped change, newest first. Verification column shows the test count at the time of merge.

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
| Tests/DevTools/DbSeedTests.cs | `[Trait("Category","Seed")]` test that deletes stale ML artifacts and seeds 180 synthetic StudyLogs (3×60 difficulty groups, `Random(42)`); run via `dotnet test --filter "Category=Seed"` |

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
