# Smart Study Planner — Changelog

> Synced 2026-05-21 from `superpowers/reports/*-change-log.md`, `consolidated-change-report.md`, `phase-next-*-report.md`, `m6-1-completion.md`, `dev-reset-clean-slate-report.md`, `ui-ux-phases-a-f-implementation-report.md`, and `bug-report.md`.
>
> Format: one row per shipped change, newest first. Verification column shows the test count at the time of merge.

## 2026-07-27 → 2026-08-02 — Post-Epic 1 Engineering Stabilization (WP-1 → WP-6)

| Package | Change | Verification |
|---|---|---|
| WP-1 CI Gate | `.github/workflows/ci.yml` (restore → build → test on `windows-latest`); `build-test` made a required status check. Split `enforce_admins`: `false` on `dev` (daily direct-push workflow), `true` on `main` (already PR-only) | `f98e4c7`, green on `windows-latest` first try, **346 pass** |
| WP-2 Test Trust Restoration | De-dated `DecisionEngineTests.cs` (6 `DateTime.Now` deadlines → fixed clock; the 45-day case re-verified as legitimately passing, no priority-band defect found); `LocalModelStorageTests` points at a temp dir instead of the real user profile; `PipelineStageTests` constructs `RiskOrchestrator` directly instead of via `ServiceLocator` | `c701527`, `81f4759`, `078fdbe`; 346 → **348 pass** |
| WP-3 Persistence & Sync Identity | Closed 2 soft-delete read-path leaks (`SqliteStudyLogRepository`, `SqliteUserStatsRepository`) plus a third found beyond the plan (`WeightOptimizerService.SuggestAsync` was also scoring tombstoned tasks); `DeviceIdentity` persists device id instead of deriving it from `Environment.MachineName` each run; `StudyTask.MucDoCanhBao` defaulted so the NOT NULL column is never null on non-UI write paths | `bbb3c29`, `6704773`, `78f16bb`; 348 → **355 pass** |
| WP-4 Scheduling Characterization | 13 characterization tests pin `WorkloadServiceImpl.GenerateSchedule` (day-date contiguity, `DiemUuTien` write-back, chunk-naming boundary, placement policy, `TenMon` provenance); non-vacuity proved by 7 mutations, each turning the suite red; production code untouched. Found (handed to WP-5, not fixed here): `capacityHours < 1/60` hangs `GenerateSchedule` in an infinite loop | `e89f0ec`; 355 → **368 pass** |
| WP-5 Runtime Robustness | Background timer tick guarded + duplicate deadline toast removed; `capacity.txt` round-trip made culture-invariant, closing 3 defects at once (the scoped locale bug + both WP-4 hand-overs, including the infinite-loop hazard); post-review follow-up added the termination guard `GenerateSchedule` had declined (non-vacuity proved by removing the guard and watching tests **hang**, not fail) | `9a175b9`, `54f64ca` + post-review `0e5d448`/`d425068`/`866b5be`; 368 → 379 → **391 pass** |
| WP-6 Repo & Doc Hygiene | Untracked root `Assets/` retired (design sources preserved at `docs/assets/icon-source/`, committed *before* the irreversible delete); README/roadmap/`CLAUDE.md` counts corrected (337 → 391); unused `Verify.CommunityToolkit.Mvvm` package reference dropped; 3 corrections appended to the Epic 2 CSA | `12291d0`, `6416e50`; **391 pass**, unchanged (adds no tests/logic) |

**Outcome:** suite 346 → **391**, green in Debug, Release, and on CI. All 12 Epic 2 entry criteria met — the last (#1, branch protection) closed 2026-08-02 once the owner authorised the `dev`/`main` split above. WP-6.3 (dependency advisory pin) deliberately not run — opt-in, and its own verification needs a manual ML retrain no agent can perform. Full detail: one report per package under [`docs/reports/`](reports/) (`2026-07-31-wp3-persistence-sync-identity.md`, `2026-07-31-wp4-scheduling-characterization.md`, `2026-08-02-wp5-runtime-robustness.md`, `2026-08-02-wp6-repo-doc-hygiene.md`); durable lessons folded into [`knowledge/review-methodology.md`](knowledge/review-methodology.md) and [`knowledge/release-engineering.md`](knowledge/release-engineering.md).

## 2026-07-26 — P1: Smart-add Vietnamese negation fix (parser)

| Area | Change | Verification |
|---|---|---|
| Services/Strategies/DifficultyKeywordParser | Negation-aware, **word-boundary token** difficulty matching in `DefaultDifficultyKeywordParser`. A difficulty keyword negated within a 2-token preceding window (`"không dễ"`, `"chẳng khó"`, `"khong de"`) is suppressed and falls back to the task-type prior (owner decision 2026-07-24: suppress→prior, not invert — never overshoots on compound negation `"không khó không dễ"`). Replaces bare `.Contains()` substring matching, which as a pinned consequence removes the RR-1 substring false-positives (`"de"` ∈ `"deadline"`, `"kho"` ∈ `"khong"`). Input NFC-normalized before tokenizing over `\p{L}+` (D-9). `ContainsAnyRule`, the task-type parser, and the `Parse`/`PriorForTaskType` signatures are untouched; **no UI change**. RED-first (7-row negation characterization corpus + 2 guard rows). Plan: `2026-07-24-smart-add-negation-fix-plan.md` (archived to `legacy/Archived plans/`, local-only) | `47cded6` (RED) + `c163135` (fix), **346 pass** |

## 2026-07-05 → 2026-07-20 — Epic 1 (Sync-Ready Data Model) — **Released 2026-07-20** (reopened at B4 on 2026-07-15, refixed 2026-07-19, re-closed & released 2026-07-20)

| Milestone | Change | Verification |
|---|---|---|
| M1.1 | Single stamping seam — `SyncStamper` (in `AppDbContext.SaveChanges*`) stamps `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId` on every write; A6 closed — `StudyLog` write awaited (was fire-and-forget), `DeviceId` populated | merged `3193adf`, review R1/R5 closed |
| M1.2 | `SyncSchema.EnsureColumns` versioned upgrade seam (backup-before-upgrade + migration report) on every synced entity; soft-delete tombstones (`IsDeleted`/`DeletedAtUtc`) replace hard cascade deletes (gate G1); `TaskCascadeHelper` cascade-tombstones FK-only children (`TaskNote`/`TaskReferenceLink`) — M1.2-R1 remediation | merged `e2f8268`, 330 pass |
| M1.3 | `Models/MonHocIdentity.Normalize` — single dedup definition (NFC → trim → collapse whitespace → invariant-culture lowercase) routed through by all 4 read-side dedup sites + `QuanLyMonHocViewModel.ThemMon` prevent-at-source; folded fix for a pre-existing M1.2 `LuuHocKyAsync` task-reconcile gap surfaced by the widened dedup key (Option A — scoped the task diff to the whole `HocKy` instead of per-`MonHoc`-parent) | merged `a3a0a3d` — **Epic 1 closed**, 330 pass |
| post-close | `101aaa3` — duplicate-subject warning routed through the `OnThongBao` seam (fixes a `MessageBox.Show` popping a real modal during headless test runs; escape from the M1.3 review, fixed same day) | — |
| A1 (release gate, C3a) | `DbBackup.CreateBackup` runs `PRAGMA wal_checkpoint(TRUNCATE)` before `File.Copy` — closes closure-verdict finding F5 (naive file-copy backup silently dropped committed-but-un-checkpointed WAL pages); RED-first discriminating test with a live-WAL fixture (writer open, autocheckpoint off); 2 existing tests converted from fake-text to real SQLite fixtures | fix `2d04be5`, merged `8740350`, 330+ pass |
| B4 reopen — R1 (P0 regression fix) | `QuanLyTaskViewModel.ThemTask` now stamps `MaMonHoc` when creating a `StudyTask` — the B4 crash driver: the 4-arg ctor left the FK `Guid.Empty`, so M1.2's FK-keyed reconcile (`First(m => m.MaMonHoc == …)`) threw `InvalidOperationException` and, with no global handler, killed the process. Defense in depth (D-R2): reconcile heals a `Guid.Empty` FK from navigation position (restoring the pre-M1.2 EF graph-fixup semantics) and now fails loudly with task/FK/`HocKy` context on a genuinely-unknown FK. RED-first at both layers; the VM-level test drives the real `ThemTaskCommand` against real SQLite with **no manual FK stamping**, closing the fixture-bias gap that let 331 green tests miss this | `3bb56c6` + `63b9611`, 333 pass |
| B4 reopen — R2 (crash visibility) | `Services/CrashLogger` — minimal last-resort fault sink (`%AppData%\SmartStudyPlanner\crash.log`, path overridable for tests; deliberately not a logging framework). `DispatcherUnhandledException` (logs, shows a dialog, sets `Handled = true` so the app survives) / `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` wired at the top of `OnStartup` — also catches `async void OnStartup` faults, shrinking tech-debt item 2.8-3 without restructuring. The 3 waived fire-and-forget telemetry sites now log their faults rather than swallowing them — `LogDifficultyLabelAsync` + `LogWeightChangeAsync` via `CrashLogger.Observe`, `MatureAsync` via an inline `catch → CrashLogger.Log` (F2 waiver nuance, D-R5) | `b0061e7` + `c18e1e7`, 336 pass |
| B4 gate — Analytics (separate, pre-existing) | Stale-render fix: `AnalyticsViewModel` now resets its chart outputs on the `!HasData` (no-data filter) branch, so a filter returning no rows can no longer leave the previous filter's chart/heatmap on screen. Surfaced during the B4 Step-2 owner re-run and diagnosed as a **pre-existing** bug, **not** an Epic 1 regression. Part 1 (VM reset) RED→GREEN unit-verified; Part 2 (`AnalyticsPage.xaml` panel `Visibility`→`HasData`) shipped as code, **visual toggle pending owner re-run**. Two-section restructure + filter semantics → post-release backlog | `c4291c7`, **337 pass** |
| B4 = Released (owner sign-off) | After the R1/R2 reopen fix and the Analytics stale-render fix, the owner re-ran the supervised launch and signed off **Epic 1 = Released (2026-07-20)** — release decision record appended to the [closure gate](plans/2026-07-11-epic-1-closure-gate.md). Supersedes the earlier "do not release yet" hold (which was pending the Step-2 Analytics diagnosis) | `3e00ce0` |

Epic 1's acceptance criteria and success metrics are all met on the merged tree; the full suite is at **337 pass** after the B4 gate work. **Released 2026-07-20:** the [closure verdict](review/2026-07-11-epic1-closure-verdict.md) ratified the close with conditions C1–C3, and the [release gate](plans/2026-07-11-epic-1-closure-gate.md) sequenced the remaining release-engineering work (agent-side docs/knowledge tasks A1–A4, then the owner-supervised first real database upgrade, B1–B4). B1–B3 passed; **B4 reopened** on a latent M1.2 FK regression (fixed by R1/R2 above); the owner re-ran the supervised launch and — with the separate pre-existing Analytics stale-render bug also fixed — signed off **Epic 1 Released** on 2026-07-20. `NU1903` (`SQLitePCLRaw` high-severity advisory) is now tracked in `specs/system_roadmap.md` §A.4 (verdict carry-forward ledger #8) — pre-existing, not Epic-1-caused.

## 2026-06-27 — Analytics UI redesign + duplicate-data fixes

| Area | Change | Verification |
|---|---|---|
| Themes/AnalyticsStyles.xaml | **Created** — 16 component styles: `AnCard`, `AnPanel`, `AnEyebrow`, `AnPageTitle`, `AnSubText`, `AnSectionTick`, `AnSectionTitle`, `AnSectionSub`, `AnFieldLabel`, `AnKpiLabel`, `AnKpiValue`, `AnSolidButton`, `AnGhostButton`, `AnDataGridHeader`, `AnDataGridRow`, `AnDataGridCell`, `AnDataGrid`. Toàn bộ màu via `{DynamicResource}` → follow Light/Dark theme. | build pass |
| App.xaml | Thêm `AnalyticsStyles.xaml` vào `MergedDictionaries` (sau `DashboardStyles.xaml`) | — |
| Views/AnalyticsPage.xaml | **Thay toàn bộ** — layout narrative-first: header+filters (Grid 2-col), Narrative Hero card (câu chuyện tuần + điểm năng suất 46px), loading/empty states, Band A (7*/5* weekly+subject charts), Heatmap (7×52 UniformGrid), DataGrid `AnDataGrid`. 14 binding ViewModel giữ nguyên. | — |
| ViewModels/AnalyticsViewModel.cs | Sửa `SubjectOptions`: thêm `.Distinct().OrderBy()` — dropdown môn học không còn hiện duplicate khi `DanhSachMonHoc` chứa nhiều instance cùng tên | — |
| Services/Analytics/StudyAnalyticsService.cs | Sửa `ComputeSubjectInsights`: `GroupBy(TenMonHoc)` + `SelectMany(DanhSachTask)` — DataGrid chi tiết môn không còn lặp hàng | — |
| Services/Pipeline/Stages/AdaptStage.cs | Sửa `Execute`: lặp theo `GroupBy(TenMonHoc)` thay vì từng `MonHoc` instance — "Gợi ý thích nghi" trên Dashboard không còn duplicate entry cùng môn | 244 pass |

Root cause (3 bugs): `DanhSachMonHoc` lưu nhiều `MonHoc` object có cùng `TenMonHoc`. Mọi code duyệt list trực tiếp sinh ra 1 kết quả per-instance thay vì per-subject. Fix pattern: `GroupBy(m => m.TenMonHoc)` + `SelectMany(DanhSachTask)` áp dụng nhất quán cho cả service layer, pipeline, và ViewModel.

## 2026-06-18 — M8-A TextClassifier seed v4 (collected_v4 merge + recall eval)

| Area | Change | Verification |
|---|---|---|
| Services/ML/TextClassifier | Merge 205 vetted/deduped `collected_v4` rows into embedded `seed_intents.csv` (**698 → 903**, purely additive). Per-class: ThiGiuaKy +99 (85→184), BaiTapVeNha +56 (124→180), DoAnCuoiKy +50 (131→181); majorities untouched. Imbalance 2.21× → **1.11×**. SHA-256 change trips the `SeedHash` gate → seed-only model auto-retrains on next init (no code change) | `ab5112c` |
| datasheets/ | Byte-safe one-off merge script `_merge_seed.py` (UTF-8 strict, dedup on normalized `InputText`) + the 205-row `collected_v4.csv` source. Kept for provenance; outside the build | `8855874` |
| tools/TextClassifierEval | Throwaway net10.0 harness (not in `.slnx`) — stratified 80/20 per-class recall eval mirroring the prod pipeline. Before/after (v698 vs v903): MacroAccuracy flat at 97.25%, minority recall did not regress, minority test support grew (ThiGiuaKy 17→37, BaiTapVeNha 25→36). Report: `docs/reports/2026-06-25-m8a-textclassifier-v4-recall-eval.md` | build pass; 244 pass |

Merge: continues the project's own label policy (drop `NhacNho`/`OnTap`/`Khac`; `BaiTap→BaiTapVeNha`, `DuAn→DoAnCuoiKy`) with no enum/UI change. TextClassifier remains the sole ML component (`ML_Heuristic_design.md` §5.1); Difficulty/weights stay heuristic.

## 2026-06-11 — M8 Ground-Truth Instrumentation (Slices 0–2B)

| Slice | Area | Change | Verification |
|---|---|---|---|
| 0 | Models/Telemetry | `DifficultyLabelLog` + `WeightChangeLog` entities; `IDifficultyLabelLogRepository` + `IWeightChangeLogRepository` interfaces + SQLite implementations; `App.xaml.cs` `CREATE TABLE IF NOT EXISTS` for both tables (safe on existing DBs) | build pass |
| 1A | Services/Strategies | `DefaultDifficultyKeywordParser` — fallback prior by `TaskType` (`DoAnCuoiKy/ThiCuoiKy→4`, `ThiGiuaKy→3`, `KiemTraThuongXuyen→3`, `BaiTapVeNha→2`) replaces hard-coded 3 | parser tests |
| 1B | ViewModels/QuanLyTask | Fire-and-forget `DifficultyLabelLog` on every task save: `InputText`, `SuggestedDoKho`, `FinalDoKho`, `WasOverride`, `TaskType`, `MaTask`; try/catch — never blocks save | label logging tests |
| 2A | ViewModels/WeightOptimizer | `ApplySuggestion()` captures before-config snapshot then fires `_ = LogWeightChangeAsync(...)`: before/after weights, `UserStatsSnapshot` baseline, cohort = open-task IDs at apply time (`TrangThai != HoanThanh`) | weight log tests |
| 2B | Services/Telemetry | `OutcomeMaturationService` + `IOutcomeMaturationService`: scans `WeightChangeLog` rows where `OutcomeMaturedUtc == null && AppliedUtc + 14d ≤ now`; fills miss-rate/avg-delay/completed-in-window from **cohort only**; idempotent; registered in `ServiceLocator`; triggered at app launch fire-and-forget | maturation tests |
| Tests | TestDoubles/ | `FakeWeightChangeLogRepository` (seed + real `GetPendingMaturationAsync` filter), `FakeUserStatsRepository`, `FakeStudyTaskRepository` | — |

Verification: **237 pass / 1 pre-existing fail** (`DecisionEngineTests.CalculatePriority_TaskToiHanHomNay`).

## 2026-06-11 — Retire `RiskAnalyzerService` fully (Core/Risk is sole risk subsystem)

| Area | Change | Verification |
|---|---|---|
| Core/Risk/RiskOrchestrator | Now implements `IRiskAnalyzer` directly; facade/bridge removed | risk tests |
| Services/RiskAnalyzer | Folder **deleted entirely** — legacy `RiskAnalyzerService` adapter (`0346637` → `1b4c2ba`) and the last DTO file `IRiskComponent.cs` (`191dd17`) both gone | — |
| Tests | Risk tests relocated to mirror `Core.Risk` namespace | `74ed39b` |

Completes the gradual migration started 2026-05-12 (below). `IRiskAnalyzer` consumers (`DashboardViewModel`, `AssessRiskStage`) depend only on the interface; DI binds `IRiskAnalyzer → RiskOrchestrator`. No `RiskAnalyzerService` / `Services.RiskAnalyzer` references remain in any `.cs` file.

## 2026-06-09 — Infrastructure clean-up (test structure + parser facade)

| Area | Change | Commit |
|---|---|---|
| Tests | Split shared test infrastructure into `TestDoubles/` (in-memory fakes) and `Fixtures/` (DB helpers); all test files now mirror production namespace 1:1 | `41a88d0` |
| Services/SmartParser | Retired static `SmartParser` facade; `QuanLyTaskViewModel` now requires `IParsingOrchestrator` directly (no more static fallback) | `222cb5a` |

## 2026-06-06 — M8-B Slices 7-8 (WeightOptimizer — rule-based + review/apply UI)

| Slice | Area | Change | Verification |
|---|---|---|---|
| 7 | Services/ML/WeightOptimizer | `WeightOptimizerService` (rule-based, reads `UserStatsSnapshot`): high miss-rate → boost TimeWeight, long avg delay → boost DifficultyWeight; `WeightRuleEngine` applies adjustment heuristics; registered in `ServiceLocator` | optimizer tests |
| 7 | Core/ML/Contracts | `IWeightOptimizerService.SuggestAsync()` → `WeightConfigSuggestion` (SuggestedConfig, Confidence, Rationale); `IMlConfidencePolicy` gates at 0.75/0.60/<0.60 | policy tests |
| 8 | WeightOptimizerViewModel | `LoadSuggestionCommand` calls optimizer; `ApplySuggestionCommand` writes config + calls `_onSave`; `IsHighConfidence`, `HasReview`, `ApplyStatus`, `AutoApplyBadgeVisible` properties | VM tests |
| 8 | Views/WeightOptimizerWindow.xaml | Side-by-side current vs suggested weight rows; confidence card (percentage + progress bar + `Rationale`); "AI khuyên dùng" badge (gated); styled Apply button; `ApplyStatus` footer | — |
| 8 | WeightConfigStore | `Load()`/`Save(WeightConfig)` — JSON persistence to `%AppData%\SmartStudyPlanner\weight_config.json`; atomic temp-file swap | — |
| 8 | MainWindow | `NavWeightOptimizer` button wired; opens `WeightOptimizerWindow` as `ShowDialog` | — |

Merge: rule-based backbone — `WeightRuleEngine` heuristics produce suggestions deterministically; no ML model required. `WeightConfig.IsValid()`/`Normalize()` remain last-line. Suggestion is a separate object until user clicks Apply (never silently overwrites). Offline-first.

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
| Tests/DevTools/DbSeedTests.cs | `[Trait("Category","Seed")]` test that deletes stale ML artifacts and seeds 180 synthetic StudyLogs (3×60 difficulty groups, `Random(42)`) into an isolated in-memory SQLite DB; run via `dotnet test --filter "Category=Seed"` |

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
