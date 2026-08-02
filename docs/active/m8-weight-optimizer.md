# Active — M8-B · Weight Optimizer for `WeightConfig`

> Status: **shipped** — Slices 7+8 (rule-based engine + UI) shipped 2026-06-06. Ground-truth telemetry (Slices 0–2B) shipped 2026-06-11.
> Implementation: `Services/ML/WeightOptimizer/WeightOptimizerService.cs` (rule-based `WeightRuleEngine`) + `Views/WeightOptimizerWindow.xaml` + `Services/Telemetry/OutcomeMaturationService.cs`.
> ML training deferred — exits when `WeightChangeLog` has sufficient matured rows with class balance. See Phase 3 in `2026-06-11-m8-ground-truth-instrumentation.md` (removed from the tracked `docs/plans/` tree 2026-08-02 as a stale duplicate; content preserved in git history and at `legacy/Archived plans/`, local-only).
> Origin: `superpowers/specs/2026-04-26-m8-ml-suite-expansion.md` + `superpowers/plans/2026-04-26-m8-ml-suite-expansion.md` + `2026-05-04-m8-design-chosen.md`.

## Why

`WeightConfig` (TimeWeight + TaskTypeWeight + CreditWeight + DifficultyWeight, summing to 1.0) is currently a static POCO. M8-B learns from study/task history and proposes a full replacement config — never silently.

## Required outputs

- A complete proposed `WeightConfig` (4 weights, sum normalized to 1.0).
- A confidence score in `[0, 1]`.
- An optional explanation / summary of why the model suggested the change.

The suggestion stays a separate object (`WeightConfigSuggestion` declared in `Core/ML/Contracts`) until the user explicitly applies it.

## Confidence policy (hard-coded for this release, not user-configurable)

- `>= 0.75` → auto-suggest + one-click apply (still requires explicit click).
- `0.60 ≤ c < 0.75` → suggest only; require explicit user review.
- `< 0.60` → do not auto-suggest; keep current config.

Threshold lives behind `IMlConfidencePolicy` for testability.

## Feature inputs

Sourced from `IUserStatsRepository.GetSnapshotAsync(hocKy)` → `UserStatsSnapshot` (already built in Slice 4 specifically for this):
- `MissRate`
- `AverageDelayDays`
- `FocusStreakDays`
- `TotalStudyMinutesLast30Days`
- plus `TaskCount`, `CompletedCount`, `AverageDifficulty`, `AverageCredits`, `DeadlinePressure` derived from snapshot+context.

## Data contract — CSV import format

Required columns:
`TaskCount, CompletedCount, AverageDelayDays, MissRate, AverageDifficulty, AverageCredits, DeadlinePressure, FocusStreakDays, CurrentTimeWeight, CurrentTaskTypeWeight, CurrentCreditWeight, CurrentDifficultyWeight, TargetTimeWeight, TargetTaskTypeWeight, TargetCreditWeight, TargetDifficultyWeight`.
Optional: `ConfidenceLabel`.

Importer rules: validate schema; fail fast on missing required columns.

## File map

Create:
- `Services/ML/WeightOptimizer/`
- `Services/ML/WeightOptimizerService.cs` implementing `IWeightOptimizerService`
- `Services/ML/WeightOptimizerModelManager.cs`
- `Services/ML/Schema/WeightOptimizerInput.cs`
- `Services/ML/Schema/WeightOptimizerOutput.cs`
- `Services/ML/WeightOptimizerDatasetImporter.cs`
- `SmartStudyPlanner.Tests/MLTests/WeightOptimizerSchemaTests.cs`
- `SmartStudyPlanner.Tests/MLTests/WeightOptimizerTests.cs`

Modify:
- `Services/ServiceLocator.cs` — register optimizer + reuse `IMlConfidencePolicy`.
- `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs` — accept an optional suggestion and apply only when explicitly chosen.
- Settings / Analytics UI — add review/apply panel.

## Guardrails (non-negotiable)

- Never overwrite `WeightConfig` silently on low confidence.
- `WeightConfig.IsValid()` is the last-line fallback.
- Suggestion stays a separate object until the user applies it.
- Post-process the 4 weights to sum to 1.0 (normalize).
- Offline-first: no cloud dependency. CSV imports + local files only.
- The decision engine continues to function with the current config at all times.

## UX requirements

- Display: current weights vs suggested weights vs confidence.
- Actions: Apply suggestion / Keep current config / (optional) Preview impact.
- Explainability: short panel showing what changed and whether the change is auto-suggested or review-required.
- Even auto-suggest (`>= 0.75`) must let the user inspect before applying.

## Test coverage required

- Suggestion generation: well-formed `WeightConfig` proposed.
- Confidence gating: low confidence does not surface in UI.
- Apply path: explicit apply mutates config; ignore leaves it unchanged.
- Fallback config stays active when suggestion is rejected.
- CSV importer rejects bad schema.
- `DecisionEngineService` still works unchanged when no suggestion is present.

## Acceptance for M8-B

- Optimizer can produce a full `WeightConfig` replacement with confidence.
- Low-confidence suggestions require explicit review.
- CSV training format is documented and validated.
- Offline-first preserved.
- Fallback behavior remains deterministic and safe.
