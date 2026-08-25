# Machine Learning Lessons

> Distilled 2026-05-21 from M7 (Study Time Predictor — shipped) and the M8 plans (Text Classifier, Weight Optimizer). Stack: `Microsoft.ML` 3.0.1 + `Microsoft.ML.FastTree` 3.0.1.

## Architecture rules that survived contact

### Treat ML as enhancement, never a dependency
Three independent fallback layers, all required:
1. `IMLModelManager.IsReady == false` → formula fallback.
2. Ready but `confidence < 0.6` → formula fallback.
3. Prediction throws → formula fallback.

The app must run with **zero model files** on disk. Tested by deleting `study_time.zip` + `meta.json` and confirming Dashboard + Analytics still render.

### Offline-first is a hard contract
- No network call anywhere in `Services/ML/*`.
- `LocalModelStorageProvider` reads/writes `%AppData%\SmartStudyPlanner\models\` only.
- Cloud storage is an opt-in `IModelStorageProvider` swap in `App.xaml.cs`; nothing else changes.
- Any sync (`StudyLogSyncService`) is fire-and-forget and never blocks UI.

### Interface boundaries earn future flexibility
- `IModelStorageProvider` lets local ↔ cloud swap without touching `MLModelManager`.
- `IMLModelManager.{Export,Import}ModelBytesAsync()` exist as no-ops in M7 so cross-device sync can land later without breaking the lifecycle contract.
- `IStudyTimePredictor` is the single seam into `SchedulingOrchestrator` — the rest of the app never sees ML.NET types.

## Model lifecycle pattern (M7, ready to copy for M8)

```text
InitializeAsync():
  1. Reads model.zip via IModelStorageProvider.
  2. Exists → ML.NET ITransformer = Model.Load(stream, out _) → IsReady = true.
  3. Missing → SeedDataGenerator.Generate(180) → train → save → IsReady = true.
  4. Reads meta.LastRetrainedAt → GetStudyLogsSinceAsync(ts).Count >= 50?
        → Task.Run(RetrainAsync) — background, non-blocking.

RetrainAsync(logs):
  1. Lock with SemaphoreSlim (serializes lifecycle).
  2. Merge 70% real + 30% seed (anti-catastrophic-forgetting when real data is sparse).
  3. Train on thread pool via Task.Run.
  4. Validate R² >= 0.50 (retrain) / 0.55 (seed bootstrap). Below → keep old model.
  5. Write to model.tmp + meta.tmp.
  6. File.Move(tmp, canonical, overwrite: true) — atomic swap.
  7. Cleanup tmp.
```

The atomic swap means a crash during training cannot leave a half-written model file.

## Confidence as a first-class output

`StudyTimePredictorService.Predict` does:
```csharp
var predicted = Math.Max(10, (int)result.Score);
float confidence = 1f - Math.Clamp(
    Math.Abs(predicted - formulaFallback) / (float)Math.Max(1, formulaFallback), 0f, 1f);
return confidence >= 0.6f ? (predicted, true) : (formulaFallback, false);
```

Key idea: **confidence is the agreement between ML and the deterministic baseline**, not the model's own raw probability. This gives a reliable gating signal even for FastTree regression where there's no built-in `P(y|x)`.

M8 uses an explicit `IMlConfidencePolicy` contract so thresholds are testable + tunable centrally.

## Confidence thresholds (project policy)

- **M7 Study Time** — single threshold: `>= 0.6` use ML, else formula.
- **M8-A Text Classifier** — single threshold: `>= 0.60` merge classifier output into parse result.
  *(Current value, unchanged.
  [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) §8
  — ratified, though the initiative it governed is `stopped_at_s0` — forbids carrying 0.60 across a
  featurizer change unexamined and requires it to be re-derived from a measured confidence curve,
  with at least one signal independent of the model's raw score. See the next section for what
  happened when that curve was actually measured for the **current** featurizer.)*
- **M8-B Weight Optimizer** — tiered (user trust matters more here):
  - `>= 0.75` → auto-suggest + one-click apply (still requires the click).
  - `0.60 ≤ c < 0.75` → suggest only, require explicit review.
  - `< 0.60` → do not surface, keep current config.

Thresholds are **hard-coded in the service layer** for this release — not user-configurable. They live behind `IMlConfidencePolicy` for testability.

## Validating a confidence threshold: plot the curve, don't reason about the number

A threshold is a claim about a distribution — *above this value the model is right often enough to
trust*. Until someone measures the confidence-versus-accuracy curve, that claim has never been
checked, no matter how reasonable the number looks.

The M8-A gate is `>= 0.60` (`Services/ML/DefaultMlConfidencePolicy.cs:13`; the type's own XML doc
records the effective rule — *callers treat anything except `Reject` as merge*). In 2026-08 the curve
was measured for the first time, as a by-product of the encoder pilot's **baseline** arm — the
shipped n-gram classifier, no encoder involved — over 205 real held-out rows:

| Bin | Relative to the `0.60` gate | observed accuracy (seed 42) | n |
|---|---|---|---|
| `[0.5, 0.6)` | **below** — rejected, heuristic used | 0.273 | 22 |
| **`[0.6, 0.7)`** | **above — ML result merged** | **0.000** | **11** |
| `[0.7, 0.8)` | above; spans the 0.75 auto-apply boundary | 0.333 | 15 |
| `[0.8, 0.9)` | above | 0.571 | 7 |
| `[0.9, 1.0]` | above | 0.983 | 119 |

**The band immediately above the gate scored worse than the band immediately below it.** The
distribution is also **bimodal and non-monotonic**: 58 % of rows land in the top bin at 0.983, the
rest scatter across bins that never exceed 0.571. It behaves like a near-binary confident /
not-confident flag, not a graded score — which means a *threshold* is the wrong instrument shape for
it, independent of where the threshold sits.

**Scope, which must travel with this.** Bin populations are small (11 rows at seed 42; 0.033 pooled
across five seeds, n=60), and this is real `collected_v4` input scored against a model trained on
synthetic rows — it says nothing about the synthetic-heavy distribution the shipped model was
validated against. **It is an indication, not a proven defect**, it is **deferred, not scheduled**
(`../specs/system_roadmap.md` §A.4), and **nothing here was changed**: re-deriving a shipped
threshold is a user-visible behaviour change that needs its own decision.

**The coupling that makes a naive fix dangerous.** `DefaultMlConfidencePolicy` is consumed by
**both** `IntentClassifierAdapter` (the parser path) **and** `WeightOptimizerViewModel` (the M8-B
suggestion path). These are different models with different error costs sharing one policy instance.
Any future re-derivation must **separate the policies rather than retune both** — a change that fixes
the parser gate by moving a shared constant is a regression wearing a fix's clothes.

**The transferable lessons:**

- **Measure the curve before defending or moving a number.** A threshold nobody has plotted is a
  guess with a decimal point.
- **Check monotonicity, not just the cut point.** A non-monotonic score cannot be gated well at *any*
  threshold; the finding is about the signal's shape, and moving the number would not fix it.
- **Bin populations are part of the finding.** 0.000 over 11 rows and 0.000 over 1 100 rows are
  different claims. Report `n` beside every rate, and don't pool across seeds to make a bin look
  populated — the same rows appearing five times are not five samples.
- **A shared policy object couples unrelated gates.** Find every consumer before touching one.
- This is the first quantitative evidence for a rule the project already held: **never trust a raw
  model score as the only gating signal — compare against the deterministic baseline.**

Evidence and method: [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md)
§14 F-1 · deferred item: [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.4.

## Algorithm choice

- **Regression (StudyTimePredictor)** → `FastTreeRegressionTrainer(numberOfLeaves: 20, numberOfTrees: 100)`. Handles non-linear interactions between difficulty/credits/days-left without feature engineering. Trains 180 rows in ~2-3 s on CPU.
- **Text classification (M8-A)** → planned `TextFeaturizer` → `SdcaMaximumEntropy` for multi-class on `TaskType`. Vietnamese text is handled by tokenization-friendly featurizer; no language-specific preprocessing needed for the MVP.
  - **Replacing that n-gram featurizer with a frozen neural sentence encoder was evaluated and
    rejected on measured evidence in 2026-08** — two candidates, two precisions each, all four
    scoring **below** the n-gram baseline's macro-F1 on the same split. **Read
    [`ml-experimentation.md`](ml-experimentation.md) before proposing it again**: the confounds are
    documented there (untuned head, 698 synthetic training rows, 3-of-5 class coverage), the result
    is scoped to *this* setting rather than to encoders generally, and the evidence points at the
    **dataset** as the binding constraint. Re-running is a new owner decision — dataset growth alone
    does not authorise it. The policy exception permitting frozen encoders
    (`../specs/ML_Heuristic_design.md` §9.1) **remains in force**; it was never exercised.
- **Multi-output regression for weights (M8-B)** → either AutoML over the dataset or 4 independent FastTree regressors with a normalization post-step so the 4 outputs sum to 1.0.

## Pipeline definition (M7, working code)

```csharp
var pipeline = mlContext.Transforms.Categorical.OneHotEncoding("TaskType")
    .Append(mlContext.Transforms.Concatenate("Features",
        "TaskType", "Difficulty", "Credits", "DaysLeft", "StudiedMinutesSoFar"))
    .Append(mlContext.Regression.Trainers.FastTree(
        numberOfLeaves: 20, numberOfTrees: 100));
```

Construct `MLContext` with `seed: 42` for reproducible training.

## Schema patterns

### Input class with `[ColumnName("Label")]`
```csharp
public class StudyTimeInput {
    public string TaskType { get; set; }
    public float Difficulty { get; set; }
    public float Credits { get; set; }
    public float DaysLeft { get; set; }
    public float StudiedMinutesSoFar { get; set; }

    [ColumnName("Label")]
    public float Label { get; set; }   // training only — actual minutes
}
```

### Output class with `[ColumnName("Score")]`
```csharp
public class StudyTimeOutput {
    [ColumnName("Score")]
    public float Score { get; set; }   // predicted minutes
}
```

### Metadata file alongside the model
`meta.json` next to `study_time.zip`:
```json
{
  "lastRetrainedAt": "2026-04-26T10:00:00Z",
  "logsUsedCount": 52,
  "modelVersion": 3,
  "seedOnly": false,
  "deviceId": "desktop-a1b2c3d4",
  "modelHash": "sha256-of-zip-bytes"
}
```
- `seedOnly` lets the UI hide the "AI" indicator when the model has only ever seen synthetic data.
- `modelHash` enables verifying that the loaded model is the file on disk.
- `deviceId` = `"desktop-" + sha256(MachineName)[..8]` — stable per machine, no PII.

## Synthetic seed data

`SeedDataGenerator.Generate(180)` produces 3 × 60 rows across difficulty groups with ±15% Gaussian-like noise:

| Group | Difficulty | Credits | DaysLeft | Label (minutes) |
|---|---|---|---|---|
| Light | ≤ 2 | ≤ 2 | ≥ 7 | 20-60 |
| Medium | = 3 | = 3 | 3-7 | 60-120 |
| Heavy | ≥ 4 | ≥ 4 | ≤ 3 | 120-240 |

Fixed `Random(42)` so the bootstrap model is reproducible.

When real logs are still scarce (< 100 rows), `RetrainAsync` mixes 70% real + 30% seed to prevent the model from collapsing onto a tiny biased sample.

## UI surfacing patterns

- **Subtle indicator** — `*` next to the suggested minutes, with `ToolTip="Dự đoán bằng AI (thử nghiệm)"`. Driven by `TaskDashboardItem.IsMLPrediction`. The ViewModel is the place to set the flag; do not put UI flags on domain models.
- **Manual retrain button** — `"Tối ưu AI"` on Analytics page. `IsEnabled = HasEnoughData` (≥ 20 logs). On click → spinner → `await IMLModelManager.RetrainAsync(_allLogs)` → status label `"Đã cập nhật model lúc HH:mm"`.
- **No surprise side effects** — auto-retrain happens only after `>= 50` new logs since last train, always on background `Task.Run`.

## Testing ML code

| Test | Asserts |
|---|---|
| `MLModelManager_TrainsOnSeedData_AchievesMinR2` | R² ≥ 0.55 on the 180 synthetic rows |
| `MLModelManager_RetrainAsync_UpdatesMeta` | `meta.LogsUsedCount` + `meta.SeedOnly == false` after retrain |
| `MLModelManager_AtomicSwap_PreservesOldModelOnFailure` | mock low R² → old `study_time.zip` not overwritten |
| `StudyTimePredictorService_ReturnsFallback_WhenModelNotReady` | `IsMLPrediction == false` |
| `StudyTimePredictorService_ReturnsFallback_WhenLowConfidence` | formula wins when ML diverges |
| `StudyTimePredictorService_ReturnsMlResult_WhenHighConfidence` | `IsMLPrediction == true` |
| `LocalModelStorageProvider_WriteRead_Roundtrip` | byte-equal |

Tag slow ML training tests with `[Trait("Category", "ML")]` so fast unit runs can skip them with `dotnet test --filter "Category!=ML"`.

## Open follow-ups (from M7 code review)

1. Verify retrain path actually pulls from `StudyLog` table (not just synthetic seed) once real users accumulate logs.
2. Benchmark R² thresholds with real data after a few months — current values are seed-tuned.
3. Watch `PredictMinutes` latency if it becomes hot-path (cache `PredictionEngine<T,U>` if needed — it's not thread-safe but cheap to recreate per call).
4. Consider async prediction path if `SchedulingOrchestrator` ever needs many predictions in a tight loop.
5. ONNX export via `IMLModelManager.ExportModelBytesAsync` becomes interesting if mobile clients arrive.

## Things to never do

- Never silently mutate `WeightConfig` on low confidence.
- Never let an ML lifecycle exception fail app startup.
- Never put ML.NET types into `Models/*` or `ViewModels/*`.
- Never train inline on the UI thread.
- Never trust raw model confidence as the only gating signal — compare against the deterministic baseline.

## See also

- [`ml-experimentation.md`](ml-experimentation.md) — how to run an ML experiment whose answer you can
  trust: pre-registered kill criteria, instrument verification before believing a null result,
  split-drift guards, dataset-maturity measurement, and edge-inference numbers with their context.
- [`review-methodology.md`](review-methodology.md) — *"A green check is evidence only after you've
  shown it can go red"*; *"Set the bar before you measure"*.
- [`../specs/ML_Heuristic_design.md`](../specs/ML_Heuristic_design.md) — the normative ML/heuristic
  boundary, including §9.1's narrow frozen-encoder exception.
