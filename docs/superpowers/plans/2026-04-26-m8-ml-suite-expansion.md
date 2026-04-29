# M8 — ML Suite Expansion Implementation Plan
## Plan · 2026-04-26

> **For agentic workers:** implement M8 in two subplans. M8-A must land first because it feeds `SmartParser` and establishes the shared ML parsing path. M8-B should come after M8-A so the CSV/import/versioning patterns are already in place.

> **Status:** planned

>
> **Scope:**
> - **M8-A**: Text Classifier for `SmartParser`.
> - **M8-B**: Weight Optimizer for `WeightConfig` replacement.
>
> **Policy:** offline-first stays default; cloud sync/model sync may be opt-in later and must remain behind interfaces.

**Suggested confidence threshold:**
- `>= 0.75` auto-suggest + one-click apply
- `0.60 <= confidence < 0.75` suggest only + require review
- `< 0.60` no auto-suggest; manual review only

---

## Delivery strategy

### Why split into two subplans?
- `SmartParser` integration is the more user-visible and lower-risk entry point.
- The weight optimizer touches scoring configuration and therefore needs more review and guardrails.
- Splitting allows independent completion, independent review, and lower blast radius.

### Integration order
1. M8-A Text Classifier
2. M8-B Weight Optimizer
3. shared hardening + test sweep

---

## 0. Current-state notes from M7

- M7 already introduced the ML storage / lifecycle patterns.
- `DecisionEngineService` already has an ML study-time hook and `WeightConfig` already has a stable fallback contract.
- `SmartParser` is still heuristic-only and is the main insertion point for M8-A.
- `WeightConfig` is currently a simple POCO with validation via `IsValid()`; M8-B will replace or recommend new values, but must not silently mutate on low confidence.

---

# M8-A — Text Classifier for SmartParser

## Task A1 — Define text-classifier contracts and dataset schema

**Files:**
- Create: `SmartStudyPlanner/Services/ML/TextClassifier/`
- Create: `SmartStudyPlanner/Services/ML/Schema/TextClassifierInput.cs`
- Create: `SmartStudyPlanner/Services/ML/Schema/TextClassifierOutput.cs`
- Create: `SmartStudyPlanner/Services/ML/TextClassifierDatasetImporter.cs`
- Create: `SmartStudyPlanner.Tests/MLTests/TextClassifierSchemaTests.cs`

**Goal:** define the ML input/output schema and CSV import rules before touching `SmartParser`.

**CSV format to document in code comments / docs:**
- `InputText`
- `TaskName` (optional)
- `TaskType`
- `Difficulty`
- `DeadlineHint`
- `Source` (optional)
- `LabelVersion` (optional)

**Verification:**
- schema compiles
- importer validates required columns
- tests cover missing-column failure

---

## Task A2 — Implement classifier service and model lifecycle

**Files:**
- Create: `SmartStudyPlanner/Services/ML/TextClassifierService.cs`
- Create: `SmartStudyPlanner/Services/ML/ITextClassifierService.cs`
- Create: `SmartStudyPlanner/Services/ML/TextClassifierModelManager.cs`
- Create: `SmartStudyPlanner/Services/ML/Schema/TextClassifierPrediction.cs`

**Goal:** train/load/predict the classification model with offline-first storage and fallback-safe behavior.

**Expected behaviors:**
- load existing model if present
- train from CSV or seed data if absent
- expose confidence with predictions
- fail safe if model not ready

**Verification:**
- model round-trip test
- prediction returns a structured result
- fallback path works when model unavailable

---

## Task A3 — Integrate classifier into SmartParser

**Files:**
- Modify: `SmartStudyPlanner/Services/SmartParser.cs`
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs`
- Modify: `SmartStudyPlanner/App.xaml.cs`
- Modify: parser-related tests

**Goal:** run classifier before or alongside the heuristic parser and merge results deterministically.

**Merge rule:**
1. run classifier
2. run current heuristic parser
3. combine outputs without breaking existing parsing behavior
4. use deadline engine for `DeadlineHint`
5. keep fallback deterministic when ML is uncertain

**Important:**
- `SmartParser` must still work without ML model files.
- classifier should improve results, not become a hard dependency.

**Verification:**
- quick-fill still works without ML artifacts
- parser output includes classifier-assisted values when available
- existing parser tests continue to pass

---

## Task A4 — Parser UX and preview wiring

**Files:**
- Modify: parser input ViewModel(s)
- Modify: task creation/edit ViewModel(s)
- Modify: task entry UI where parser preview is shown

**Goal:** show the user a cleaner preview of extracted task name/type/difficulty/deadline hint before save.

**UX requirements:**
- highlight extracted task name/type/difficulty
- do not block if ML is unavailable
- keep user editable fields visible

**Verification:**
- preview appears when classifier is ready
- fallback behavior remains unchanged when classifier is not ready

---

## Task A5 — Test and harden M8-A

**Files:**
- Create: `SmartStudyPlanner.Tests/MLTests/TextClassifierTests.cs`
- Modify: any parser integration tests

**Goal:** prove SmartParser remains stable while getting ML assistance.

**Must cover:**
- classifier available
- classifier unavailable
- confidence too low → fallback
- CSV import validation
- parser merge does not overwrite explicit user input incorrectly

---

# M8-B — Weight Optimizer

## Task B1 — Define optimizer contracts and dataset schema

**Files:**
- Create: `SmartStudyPlanner/Services/ML/WeightOptimizer/`
- Create: `SmartStudyPlanner/Services/ML/Schema/WeightOptimizerInput.cs`
- Create: `SmartStudyPlanner/Services/ML/Schema/WeightOptimizerOutput.cs`
- Create: `SmartStudyPlanner/Services/ML/WeightOptimizerDatasetImporter.cs`
- Create: `SmartStudyPlanner/Services/ML/WeightConfigSuggestion.cs`
- Create: `SmartStudyPlanner.Tests/MLTests/WeightOptimizerSchemaTests.cs`

**Goal:** define inputs, outputs, and CSV import structure before implementing suggestion/apply flows.

**CSV format to document:**
- `TaskCount`
- `CompletedCount`
- `AverageDelayDays`
- `MissRate`
- `AverageDifficulty`
- `AverageCredits`
- `DeadlinePressure`
- `FocusStreakDays`
- `CurrentTimeWeight`
- `CurrentTaskTypeWeight`
- `CurrentCreditWeight`
- `CurrentDifficultyWeight`
- `TargetTimeWeight`
- `TargetTaskTypeWeight`
- `TargetCreditWeight`
- `TargetDifficultyWeight`
- `ConfidenceLabel` (optional)

**Verification:**
- schema compiles
- importer validates required columns
- tests cover CSV schema mismatch

---

## Task B2 — Implement optimizer service and lifecycle

**Files:**
- Create: `SmartStudyPlanner/Services/ML/IWeightOptimizerService.cs`
- Create: `SmartStudyPlanner/Services/ML/WeightOptimizerService.cs`
- Create: `SmartStudyPlanner/Services/ML/WeightOptimizerModelManager.cs`

**Goal:** produce a suggested full `WeightConfig` plus confidence and explanation.

**Confidence policy:**
- model returns confidence
- app applies a **hard-coded threshold**
- high confidence → may auto-suggest/apply and still preserve fallback
- low confidence → user must review and explicitly choose
- threshold is not user-configurable in M8

**Verification:**
- can produce a `WeightConfigSuggestion`
- can load/save model artifacts
- can fallback safely when model missing

---

## Task B3 — Integrate optimizer with `WeightConfig`

**Files:**
- Modify: `SmartStudyPlanner/Services/WeightConfig.cs`
- Modify: `SmartStudyPlanner/Services/DecisionEngineService.cs`
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs`
- Modify: `SmartStudyPlanner/App.xaml.cs`

**Goal:** allow the optimizer to propose a replacement config and let the app choose whether to apply it.

**Guardrails:**
- never overwrite config silently on low confidence
- if suggestion is not accepted, continue with existing config unchanged
- existing `IsValid()` fallback remains the last line of defense

**Verification:**
- config suggestion can be applied explicitly
- low confidence leaves config unchanged
- `DecisionEngineService` continues to function with current config

---

## Task B4 — UI review/apply flow

**Files:**
- Modify: settings / analytics / optimization UI
- Possibly create a small review dialog or panel

**Goal:** show the suggested config and let the user decide.

**Required actions:**
- preview suggested config values
- show confidence
- apply suggestion
- ignore suggestion / keep current config

**Verification:**
- user can inspect values before applying
- high-confidence can be auto-suggested but not forced
- low-confidence always requires user review

---

## Task B5 — Test and harden M8-B

**Files:**
- Create: `SmartStudyPlanner.Tests/MLTests/WeightOptimizerTests.cs`
- Update service/repo tests as needed

**Must cover:**
- suggestion generation
- confidence gating
- explicit apply/ignore behavior
- fallback config remains active when suggestion rejected
- CSV import validation

---

# Shared tasks

## Task S1 — DI registration and startup wiring

**Files:**
- Modify: `SmartStudyPlanner/Services/ServiceLocator.cs`
- Modify: `SmartStudyPlanner/App.xaml.cs`

**Goal:** register new ML services and make sure startup initialization stays background-friendly.

**Verification:**
- app starts with and without model files
- DI resolves services cleanly

---

## Task S2 — Update docs and status tracking

**Files:**
- Modify: `docs/implementation_plan.md`
- Modify: relevant `docs/superpowers/` plans/specs after implementation

**Goal:** keep the docs aligned as M8 subplans land.

---

## Acceptance criteria for M8

- SmartParser can use the text classifier to improve parsing.
- Deadline hint is still resolved by the existing time/deadline engine.
- Weight optimizer can produce a full `WeightConfig` replacement.
- Low-confidence optimizer results require user review.
- CSV training format is documented and validated.
- Offline-first behavior remains intact.
- Fallback behavior remains deterministic and safe.

---

## Recommended implementation order

1. M8-A Task A1
2. M8-A Task A2
3. M8-A Task A3
4. M8-A Task A4
5. M8-A Task A5
6. M8-B Task B1
7. M8-B Task B2
8. M8-B Task B3
9. M8-B Task B4
10. M8-B Task B5
11. Shared tasks S1–S2

**Reasoning:** parser-first gives the highest user-visible value and establishes shared ML plumbing before touching the more sensitive config optimizer.
