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
  *(Current value. [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) §8
  forbids carrying 0.60 across a featurizer change unexamined and requires it to be re-derived from a
  measured confidence curve, with at least one signal independent of the model's raw score.)*
- **M8-B Weight Optimizer** — tiered (user trust matters more here):
  - `>= 0.75` → auto-suggest + one-click apply (still requires the click).
  - `0.60 ≤ c < 0.75` → suggest only, require explicit review.
  - `< 0.60` → do not surface, keep current config.

Thresholds are **hard-coded in the service layer** for this release — not user-configurable. They live behind `IMlConfidencePolicy` for testability.

## Algorithm choice

- **Regression (StudyTimePredictor)** → `FastTreeRegressionTrainer(numberOfLeaves: 20, numberOfTrees: 100)`. Handles non-linear interactions between difficulty/credits/days-left without feature engineering. Trains 180 rows in ~2-3 s on CPU.
- **Text classification (M8-A)** → planned `TextFeaturizer` → `SdcaMaximumEntropy` for multi-class on `TaskType`. Vietnamese text is handled by tokenization-friendly featurizer; no language-specific preprocessing needed for the MVP.
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
