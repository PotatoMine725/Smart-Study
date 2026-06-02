# Active — God-Object Refactor + M8 Integration (Slices 5–8)

> Originated from `superpowers/plans/2026-05-17-god-object-refactor-and-m8-integration-plan.md`.
> Status (2026-05-21): **4 / 8 slices done**. Slices 5–8 implement M8 on top of the refactored core.
> Baseline: 156 tests pass.

> **Active side-track (2026-06-02):** persistence god-repo chưa đóng — `Data/StudyRepository` (185 dòng, 5 aggregate) cần tách nốt theo seam Slice 4. Plan: [`docs/plans/2026-06-02-split-studyrepository.md`](../plans/2026-06-02-split-studyrepository.md) — `in-progress`, phased 2-commit, gitnexus impact = HIGH.

## Done in earlier slices

| Slice | What | Commit |
|---|---|---|
| 1 | `Core/{Scheduling,Parsing,ML}/Contracts` skeletons | `5ece84c` |
| 2 | `Core/Scheduling/{Engines,Evaluators,Orchestrators}` + `DecisionEngineService` 92→42 lines | `3b176fb` |
| 3 | `Core/Parsing/{Engines,Orchestrators}` + `SmartParser` static facade | 2026-05-18 |
| 4 | `Infrastructure/Persistence/{Repositories,SQLite/Repositories}` + `UserStatsSnapshot` | 2026-05-18 |

## Slice 5 — M8-A A1+A2: TextClassifier schema + service

Goal: stand up the classifier service + lifecycle without touching `SmartParser` yet.

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

## Slice 6 — M8-A A3+A4+A5: parser integration + UX + tests

Goal: wire the classifier into the parser flow already exposed by `ParsingOrchestrator` (Slice 3 reserved the seam via optional `IIntentClassifier`).

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

## Slice 7 — M8-B B1+B2+B3: Weight Optimizer

Goal: produce `WeightConfigSuggestion` from `UserStatsSnapshot` aggregates and ship it through `SchedulingOrchestrator` without mutating `WeightConfig` silently.

Files:
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

**Slice 5** — start with `TextClassifierSchemaTests` and the schema classes; do not touch `SmartParser` until Slice 6.

Pre-edit checklist:
1. `npx gitnexus analyze` if the index is stale.
2. `npx gitnexus impact SmartParser --direction upstream --repo Smart-Study` (re-confirm seam unchanged).
3. Confirm M7 ML files in `Services/ML/*` are untouched.
