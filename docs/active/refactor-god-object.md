# Active — God-Object Refactor + M8 Integration (Slices 5–8)

> Originated from `superpowers/plans/2026-05-17-god-object-refactor-and-m8-integration-plan.md`.
> Status (2026-06-06): **7 / 8 slices done**. Slice 8 finishes M8-B (Weight Optimizer review/apply UI) on top of the refactored core.
> Baseline: 191 tests pass.

> **Persistence god-repo split — DONE (2026-06-02):** `Data/StudyRepository` (5 aggregate) đã tách thành `IHocKyRepository` + `ITaskEditorRepository` + `IStudyLogRepository`, migrate 7 consumer, xóa god-repo. Plan: [`docs/plans/2026-06-02-split-studyrepository.md`](../plans/2026-06-02-split-studyrepository.md) (`done`). 158 test pass.

> **Slice 5 — DONE (2026-06-05):** M8-A TextClassifier scaffold landed standalone (commit `cb49a15`). Schema (`TextClassifier{Input,Output,Prediction}`), `ITextClassifierModelManager` + `TextClassifierModelManager` (own `text_classifier.zip` paths, embedded seed CSV, atomic save), `TextClassifierDatasetImporter` (fail-fast), `TextClassifierService : IIntentClassifierService` (DoKho null this slice), 50-row embedded `seed_intents.csv`, 8 tests. **No consumer wiring** — ServiceLocator/ParsingOrchestrator/SmartParser untouched (deferred to Slice 6). 158 → 166 test pass.

## Done in earlier slices

| Slice | What | Commit |
|---|---|---|
| 1 | `Core/{Scheduling,Parsing,ML}/Contracts` skeletons | `5ece84c` |
| 2 | `Core/Scheduling/{Engines,Evaluators,Orchestrators}` + `DecisionEngineService` 92→42 lines | `3b176fb` |
| 3 | `Core/Parsing/{Engines,Orchestrators}` + `SmartParser` static facade | 2026-05-18 |
| 4 | `Infrastructure/Persistence/{Repositories,SQLite/Repositories}` + `UserStatsSnapshot` | 2026-05-18 |

## Slice 5 — M8-A A1+A2: TextClassifier schema + service ✅ DONE (2026-06-05, `cb49a15`)

Goal: stand up the classifier service + lifecycle without touching `SmartParser` yet.

**Shipped:** all files below created; `IModelStorageProvider` was NOT reused (the M7 provider hardcodes `study_time.zip`) — `TextClassifierModelManager` owns its own paths instead. Added `ITextClassifierModelManager` (not in the original file map) so the service is unit-testable with a stub. Seed delivered as an **embedded** `seed_intents.csv` (50 rows × 5 classes) loaded via manifest-resource stream. Model = multiclass `SdcaMaximumEntropy` on `TaskType`; `DoKho` left null (difficulty = a deferred second model). Seed persists unconditionally (`SeedOnly=true`, no R² gate / no TrainTestSplit on the tiny seed set). 158 → 166 tests.

Files:
- Create `SmartStudyPlanner/Services/ML/TextClassifier/`
- Create `Services/ML/TextClassifierService.cs` (implements `Core.ML.Contracts.IIntentClassifierService`)
- Create `Services/ML/Schema/TextClassifierInput.cs`
- Create `Services/ML/Schema/TextClassifierOutput.cs`
- Create `Services/ML/Schema/TextClassifierPrediction.cs`
- Create `Services/ML/TextClassifierModelManager.cs`
- Create `Services/ML/TextClassifierDatasetImporter.cs`
- Create `SmartStudyPlanner.Tests/MLTests/TextClassifierSchemaTests.cs`

CSV columns the importer must validate:
`InputText, TaskName?, TaskType, Difficulty, DeadlineHint, Source?, LabelVersion?`

Exit criteria:
- schema compiles, importer fails fast on missing required columns.
- model lifecycle: load if present, train from seed CSV if absent, atomic save.
- `dotnet build` + tests pass (no regression on 156).

Guardrail: do not move any file under `Services/ML/*` that M7 depends on.

## Slice 6 — M8-A A3+A4+A5: parser integration + UX + tests ✅ DONE (2026-06-05)

**Shipped:** `DefaultMlConfidencePolicy` (thresholds 0.75/0.60) + `IntentClassifierAdapter`
(wraps `IIntentClassifierService` + policy, drops below 0.60, try/catch→null for offline-first).
`ServiceLocator` registers `ITextClassifierModelManager`/`IIntentClassifierService`/`IMlConfidencePolicy`/
`IIntentClassifier` and injects the adapter into `IParsingOrchestrator`. `App.xaml.cs` warms the
classifier in the background. `QuanLyTaskViewModel` prefers the DI orchestrator (falls back to static
`SmartParser` when null, so heuristic-only unit tests stay valid) and surfaces an "AI gợi ý Loại …"
hint when `Source == MlAugmented`; M6.1 invariant (never touches Note/Links) preserved. **Prereq fix:**
seed v3 (5-class) — see [m8-text-classifier.md](m8-text-classifier.md) — so the ≥0.60 override no longer
regresses `ThiGiuaKy`/`DoAnCuoiKy`. 166 → **179** tests. `SmartParser.cs` static facade left untouched.

Goal (original): wire the classifier into the parser flow already exposed by `ParsingOrchestrator` (Slice 3 reserved the seam via optional `IIntentClassifier`).

Wiring:
- Register `IIntentClassifierService` (and a thin `IIntentClassifier` adapter) in `ServiceLocator`.
- Adapter calls `IMlConfidencePolicy` to decide if the classifier output is merged into the heuristic parser output.
- Confidence policy (hard-coded for this release): `>= 0.60` → merge; `< 0.60` → heuristic only.
- Merge order: classifier → heuristic → resolve `DeadlineHint` via existing deadline engine.

UX:
- Surface classifier-extracted `TaskName / TaskType / Difficulty / DeadlineHint` in the task creation/edit preview before save.
- If ML is missing, do not block — show the existing editor.

Tests:
- Classifier present + high confidence → merged output.
- Classifier present + low confidence → fallback to heuristic.
- Classifier absent → behavior identical to today.
- CSV importer rejects bad schema.
- Parser merge does not overwrite explicit user input.

Exit criteria:
- App still runs without `text_classifier.zip` (offline-first).
- Test count grows; no regression.

## Slice 7 — M8-B B1+B2+B3: Weight Optimizer ✅ DONE (2026-06-06)

**Shipped (rule-based MVP — KHÔNG ML scaffold):** quyết định với user vì repo chưa log lịch sử `weight-change → outcome` (DbSet chỉ HocKy/MonHoc/StudyTask/StudyLog/TaskNote/TaskReferenceLink) ⇒ không có ground-truth thật; mọi "ML" chỉ là regression train trên seed synthetic-từ-heuristic (như M7) → chỉ xấp xỉ lại heuristic.
- `WeightRuleEngine.Compute(WeightConfig current, UserStatsSnapshot stats)` — hàm THUẦN deterministic: MissRate/AverageDelayDays cao → dịch tối đa 15% về `TimeWeight` (lấy từ 3 weight còn lại theo tỷ lệ), FocusStreak dài giảm nudge tới 50%; `Normalize()` về tổng 1.0, last-line giữ `current` nếu lệch. Confidence = độ đủ dữ liệu (TaskCount + StudyMinutes30d), ép < 0.60 khi < 5 task.
- `WeightOptimizerService : IWeightOptimizerService` — wrapper **async** mỏng (inject `IUserStatsRepository` + `IClock`), fetch snapshot bằng EF async thật.
- **Contract đổi sang async** `Task<WeightConfigSuggestion?> SuggestAsync(...)` (tránh sync-over-async deadlock; contract chưa có caller nào nên đổi miễn phí).
- `WeightConfig.Normalize()` (clamp sàn 0.05 + scale 1.0) — guardrail mới.
- Seam read-only: `SuggestWeightConfigAsync` thêm vào `ISchedulingOrchestrator`/`SchedulingOrchestrator` + `IDecisionEngine`/`DecisionEngineService` (optional dep `IWeightOptimizerService`, trả null nếu chưa inject) — **KHÔNG** chạm `_config`/self-heal. Đăng ký trong `ServiceLocator`.
- Bỏ so với plan gốc: ModelManager/Importer/Schema In-Out/seed CSV/zip + `WeightOptimizerSchemaTests` (hệ quả rule-based). Scope = global snapshot (không sửa repo). 179 → **191** tests.

Goal (original): produce `WeightConfigSuggestion` from `UserStatsSnapshot` aggregates and ship it through `SchedulingOrchestrator` without mutating `WeightConfig` silently.

Files (original plan — ML scaffold, KHÔNG dùng):
- Create `Services/ML/WeightOptimizer/`
- Create `Services/ML/WeightOptimizerService.cs` (implements `Core.ML.Contracts.IWeightOptimizerService`)
- Create `Services/ML/Schema/WeightOptimizerInput.cs`
- Create `Services/ML/Schema/WeightOptimizerOutput.cs`
- Create `Services/ML/WeightOptimizerModelManager.cs`
- Create `Services/ML/WeightOptimizerDatasetImporter.cs`
- Create `Services/ML/WeightConfigSuggestion.cs` (already declared in `Core/ML/Contracts` from Slice 1)
- Create `SmartStudyPlanner.Tests/MLTests/WeightOptimizerSchemaTests.cs`

Input feature source: `IUserStatsRepository.GetSnapshotAsync(hocKy)` → `UserStatsSnapshot` (MissRate, AverageDelayDays, FocusStreakDays, TotalStudyMinutesLast30Days, ...).

CSV columns the importer must validate:
`TaskCount, CompletedCount, AverageDelayDays, MissRate, AverageDifficulty, AverageCredits, DeadlinePressure, FocusStreakDays, CurrentTimeWeight, CurrentTaskTypeWeight, CurrentCreditWeight, CurrentDifficultyWeight, TargetTimeWeight, TargetTaskTypeWeight, TargetCreditWeight, TargetDifficultyWeight, ConfidenceLabel?`

Confidence policy (`IMlConfidencePolicy`, **hard-coded**, not user-configurable in this release):
- `>= 0.75` → auto-suggest + allow one-click apply (still requires explicit click).
- `0.60 <= c < 0.75` → suggest only; require explicit user review.
- `< 0.60` → do not surface; keep current config.

Guardrails:
- Never silently overwrite `WeightConfig` on low confidence.
- `WeightConfig.IsValid()` remains the last-line fallback.
- Sum of 4 weights must remain 1.0 → post-process normalize.
- Suggestion stays a separate object until the user applies it.

## Slice 8 — M8-B B4+B5: review/apply UI + harden

UI:
- Settings or Analytics surface a small panel showing: current vs suggested weights, confidence, an "Apply" and "Keep current" action.
- Explainability: minimum panel showing what changed + confidence + auto-suggest vs review-required.

Tests:
- Suggestion generation produces a valid `WeightConfig`.
- Confidence gating: low confidence → no suggestion surface.
- User explicit apply path mutates config; ignore path leaves it unchanged.
- Fallback config remains active when the suggestion is rejected.
- CSV importer rejects bad schema.

## Acceptance gates (every slice)

1. `dotnet build SmartStudyPlanner.slnx` clean.
2. `dotnet test SmartStudyPlanner.slnx --no-build` ≥ current baseline.
3. `gitnexus_detect_changes()` before commit; blast radius matches scope.
4. If editing `DecisionEngineService` / `SmartParser` / `WeightConfig` / `SchedulingOrchestrator` → `gitnexus_impact({direction: "upstream"})` first; report HIGH/CRITICAL.
5. One slice = one commit, conventional message (`refactor(area): ...` or `feat(M8-A/B): ...`).

## Explicit out of scope

- Pipeline rehome (`Services/Pipeline/*` → `Application/UseCases/*`).
- `Core/Capacity` module.
- `Core/Sync` + PostgreSQL.
- Cloud model storage.

## Immediate next action

**Slice 8** — M8-B review/apply UI cho WeightOptimizer (Settings hoặc Analytics). Gọi `IDecisionEngine.SuggestWeightConfigAsync()` (seam đã sẵn từ Slice 7); hiển thị panel current vs suggested + confidence + `Rationale`; gate qua `DefaultMlConfidencePolicy` (≥0.75 auto-suggest+1-click apply, 0.60..0.75 review, <0.60 không surface). Apply path mutate `WeightConfig` chỉ khi user click; `IsValid()` là last-line. Suggestion vẫn là object tách rời tới khi apply.

Pre-edit checklist:
1. `npx gitnexus analyze` if the index is stale (hoặc fallback Grep nếu MCP gitnexus/code-review-graph không kết nối — như session 2026-06-06).
2. Xác nhận seam Slice 7 (`SuggestWeightConfigAsync` trên `IDecisionEngine`/orchestrator) không đổi; UI chỉ consume.
3. Confirm M7 ML files in `Services/ML/*` remain untouched.
