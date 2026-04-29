# M8 — ML Suite Expansion
## Design Spec · 2026-04-26

> **Scope:** expand the ML suite from M7 by adding two sub-models:
> 1. **Text Classifier** — feed `SmartParser` with richer parsing output.
> 2. **Weight Optimizer** — replace `WeightConfig` when confidence is high, otherwise let the user decide whether to fallback or accept the suggestion.
>
> **Core direction:** offline-first remains the default. Cloud sync / cloud model storage is opt-in later, not required for M8.
>
> **User decision model:** if the optimizer is not confident enough, the app must expose the suggested weights and ask the user whether to apply them. The system must never silently mutate the user's scoring configuration on low confidence.

---

## 0. Executive summary

M8 is a follow-up to M7 and is split into two subplans:

- **M8-A — Text Classifier**
  - improves `SmartParser`
  - extracts `TaskName` (optional), `TaskType`, `Difficulty`, and `DeadlineHint`
  - `DeadlineHint` continues to be interpreted by the existing deadline/time parsing engine
  - training data is imported from a dedicated CSV format

- **M8-B — Weight Optimizer**
  - learns from study/task history and produces a full replacement `WeightConfig`
  - if confidence is high, the config can be applied automatically
  - if confidence is low, the app surfaces the suggested config and lets the user choose whether to keep the current config or apply the new one

---

## 1. Architecture goals

### 1.1 Functional goals
- Improve natural-language task parsing beyond the current heuristic parser.
- Preserve existing parser fallback behavior when ML is unavailable or uncertain.
- Learn and recommend a full `WeightConfig` replacement.
- Keep the user in control whenever confidence is not strong enough.
- Support CSV-based training dataset import for both sub-models.

### 1.2 UX goals
- The parser should feel smarter, not more intrusive.
- Task creation must still be fast even when ML is disabled.
- Weight optimization should be clearly presented as a recommendation, not an irreversible silent action.
- User should be able to preview the new config before applying it.

### 1.3 Technical goals
- Keep offline-first as the default runtime model.
- Preserve existing formula fallback paths.
- Keep blast radius controlled by isolating model contracts and adapter layers.
- Use explicit interfaces so future cloud storage can be plugged in later.

---

## 2. M8 scope split

### 2.1 M8-A — Text Classifier

**Purpose:** enhance `SmartParser`.

#### Required outputs
The classifier may contribute the following parse targets:
- `TaskName` if present in the input sentence
- `TaskType`
- `Difficulty`
- `DeadlineHint`

#### Responsibility split
- The text classifier extracts structure from raw input.
- The deadline/time engine resolves `DeadlineHint` into an actual `DateTime`.
- The parser merge layer decides how to combine classifier output with deterministic keyword parsing.

#### Non-goals
- It does not replace the existing deadline engine.
- It does not write directly to DB.
- It does not decide weight configuration.

### 2.2 M8-B — Weight Optimizer

**Purpose:** learn a better `WeightConfig`.

#### Required outputs
- Proposed full `WeightConfig`
- Confidence score
- Optional explanation / summary of why the model suggested the change

#### Confidence behavior
Use a hard-coded threshold in the app layer:
- `confidence >= 0.75` → auto-suggest and allow one-click apply
- `0.60 <= confidence < 0.75` → suggest only; require user review before apply
- `confidence < 0.60` → do not auto-suggest; force manual review / keep current config

The threshold is hard-coded in the app / service layer for M8; it is **not user-configurable** in this release.

#### Non-goals
- It does not replace the core decision engine.
- It does not mutate config silently on weak confidence.
- It does not depend on network access.

---

## 3. Data contracts

### 3.1 Shared M8 principles
Both sub-models must work from explicit schema classes and versioned CSV inputs.

### 3.2 Text Classifier dataset format
Recommended CSV columns:
- `InputText` — raw Vietnamese sentence
- `TaskName` — extracted or canonical task name, optional if unknown
- `TaskType` — canonical enum-like label
- `Difficulty` — integer or class label
- `DeadlineHint` — raw deadline hint phrase
- `Source` — optional source label (`seed`, `user`, `imported`)
- `LabelVersion` — optional dataset version number

#### Example rows
| InputText | TaskName | TaskType | Difficulty | DeadlineHint |
|---|---|---:|---:|---|
| Nộp báo cáo AI thứ 6 tuần sau | báo cáo AI | DoAnCuoiKy | 3 | thứ 6 tuần sau |
| Ôn tập chương 3 trước tối mai | ôn tập chương 3 | BaiTapVeNha | 2 | tối mai |

### 3.3 Weight Optimizer dataset format
Recommended CSV columns:
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
- `ConfidenceLabel` or numeric score if available

#### Example row intent
- input features describe current user behavior and task mix
- target columns describe the recommended new config

### 3.4 Dataset import rules
- CSV import must validate schema version.
- Missing required columns should fail fast with a human-readable error.
- Seed and real datasets should be mergeable later if needed.
- For M8, user data remains the preferred source when available.

---

## 4. ML / parser integration spec

### 4.1 `SmartParser` integration
The parser pipeline should be adapted so the text classifier runs before or alongside the existing heuristic parser.

Required merge behavior:
1. run classifier
2. run existing keyword-based parser
3. merge outputs deterministically
4. use the deadline engine for `DeadlineHint`
5. preserve fallback behavior when classifier confidence is insufficient

### 4.2 SmartParser output contract
The enriched parser should eventually expose something like:
- `TaskName`
- `TenTask` / normalized task title
- `TaskType`
- `Difficulty`
- `DeadlineHint`
- resolved `HanChot`
- confidence metadata

### 4.3 Weight Config integration
When the optimizer returns a candidate config:
- if confidence is high, the app may apply it directly or show a confirmation toast + auto-apply setting flow
- if confidence is low, the app must show the candidate config to the user and let them choose
- the existing `WeightConfig` remains available as fallback and as the last-known-good config

### 4.4 Guardrails
- Never overwrite user config without a confidence threshold.
- Never break deterministic fallback.
- Never require cloud connectivity.

---

## 5. UI/UX spec

### 5.1 Parser UX
- Show a smarter parse preview when classifier output is available.
- Highlight extracted task name / type / deadline hint before save.
- If ML is uncertain, keep the current editor behavior and do not block.

### 5.2 Weight Optimizer UX
- Display suggested config values and confidence.
- Provide explicit actions:
  - Apply suggestion
  - Keep current config
  - Preview impact
- If confidence is high enough for auto-suggest, the user must still be able to review the change.

### 5.3 Explainability
The optimizer should present at least a minimal explanation panel:
- what changed
- confidence level
- whether the change is auto-suggested or requires confirmation

---

## 6. Training and lifecycle spec

### 6.1 Training modes
- bootstrap from seed CSV
- train from imported user CSV
- retrain from accumulated app data later if enabled

### 6.2 Model lifecycle
Both M8 models should support:
- initialize
- load existing artifact
- train new artifact
- validate basic quality metrics
- save atomically
- rollback to last good model if needed

### 6.3 Offline-first
- local file storage remains default
- no cloud requirement
- any future cloud storage must be opt-in and behind interface boundaries

---

## 7. Blast radius analysis

### 7.1 Highest-risk surfaces
- `Services/SmartParser.cs`
- `Services/WeightConfig.cs`
- `Services/DecisionEngineService.cs`
- any parser-related ViewModel that consumes quick-fill output

### 7.2 Medium-risk surfaces
- new ML services and schema classes
- training/import pipeline
- review/apply UI for weight suggestions

### 7.3 Low-risk surfaces
- tests
- dataset import validators
- helper adapters

### 7.4 Blast radius strategy
- M8-A and M8-B must be split into separate subplans.
- Parser integration should land first because it is the main user-facing improvement and provides shared ML plumbing.
- Weight optimizer should come second once the import/versioning pipeline is stable.

---

## 8. File map

### Create for M8-A
- `Services/ML/TextClassifier/` or equivalent folder
- `Services/ML/TextClassifierService.cs`
- `Services/ML/Schema/TextClassifierInput.cs`
- `Services/ML/Schema/TextClassifierOutput.cs`
- `Services/ML/TextClassifierDatasetImporter.cs`
- `Tests/MLTests/TextClassifierTests.cs`

### Create for M8-B
- `Services/ML/WeightOptimizer/` or equivalent folder
- `Services/ML/WeightOptimizerService.cs`
- `Services/ML/Schema/WeightOptimizerInput.cs`
- `Services/ML/Schema/WeightOptimizerOutput.cs`
- `Services/ML/WeightConfigSuggestion.cs`
- `Tests/MLTests/WeightOptimizerTests.cs`

### Modify shared surfaces
- `Services/SmartParser.cs`
- `Services/DecisionEngineService.cs`
- `Services/WeightConfig.cs`
- `Services/ServiceLocator.cs`
- `App.xaml.cs`
- relevant parser / settings ViewModels

---

## 9. Done criteria

M8 is complete when:
- SmartParser can use ML text classification for task name/type/difficulty parsing
- deadline hints are still resolved by the existing deadline engine
- the optimizer can produce a full `WeightConfig` replacement
- low-confidence weight suggestions require user review
- CSV training import is documented and validated
- offline-first behavior remains intact
- fallback behavior remains deterministic and safe

---

## 10. Final decision summary

**M8 structure:**
- M8-A Text Classifier
- M8-B Weight Optimizer

**Integration order:**
1. SmartParser integration
2. Weight optimizer integration
3. UI review/apply flow
4. dataset import and validation
5. tests and hardening

**Policy:**
> ML should improve the planning experience, not replace user control.
