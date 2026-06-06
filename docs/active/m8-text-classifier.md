# Active — M8-A · Text Classifier for `SmartParser`

> Status: **Slice 5 + Slice 6 done (2026-06-05)**. Tracked in slices 5 + 6 of [refactor-god-object.md](refactor-god-object.md).
> Origin: `superpowers/specs/2026-04-26-m8-ml-suite-expansion.md` + `superpowers/plans/2026-04-26-m8-ml-suite-expansion.md`.
>
> **Slice 5 delivered** (standalone, no wiring): schema + importer (fail-fast) + `TextClassifierModelManager` lifecycle (own `text_classifier.zip` paths, embedded seed CSV, atomic save) + `TextClassifierService : IIntentClassifierService` + 8 tests. Model is multiclass on `TaskType`; `Difficulty` (`DoKho`) prediction is **deferred** — the service returns `DoKho = null`. The classifier contributes `TaskType` + `Confidence` only.
>
> **Seed v3 (2026-06-05) — 5-class fix.** The embedded `seed_intents.csv` now has **698 rows covering all 5 `LoaiCongViec` classes** (KiemTra 188 / ThiCuoiKy 170 / DoAnCuoiKy 131 / BaiTapVeNha 124 / ThiGiuaKy 85). The earlier v2 seed (596 rows) covered only 3/5 because the source datasheet mislabeled "giữa kỳ"→ThiCuoiKy and "đồ án"→BaiTap — making the model confidently *mis-map* those two classes at ~1.0 confidence (a regression vs the heuristic). Fix: relabeled the real contradictions in `datasheets/normalized_dataset_m8a_uniform.csv` (31 giữa-kỳ → `ThiGiuaKy`, 96 đồ-án/BTL/project → `DoAnCuoiKy`) and added 100 synthetic rows (`datasheets/synthetic_v3_giuaky_doan.csv`) simulating diverse VN-student text (personas, typos, slang/emoji, EN↔VI, varied density) + contrastive boundary pairs. Stratified 85/15 held-out eval: **96.2% accuracy, 1/106 dangerous miss**, confidence no longer saturated.
>
> **Slice 6 delivered (2026-06-05):** `DefaultMlConfidencePolicy` + `IntentClassifierAdapter` (drops below 0.60, try/catch→null), DI registration in `ServiceLocator`, `IParsingOrchestrator` injects the adapter, `App.xaml.cs` background warm-up, `QuanLyTaskViewModel` uses the DI orchestrator + surfaces an "AI gợi ý" hint. Merge threshold `>= 0.60` (ML overrides heuristic `Loai`); offline-first preserved. 166 → **178** tests.

## Why

`SmartParser` is heuristic-only today. M8-A adds an ML classifier on top so quick-input task creation can extract more structure: `TaskName / TaskType / Difficulty / DeadlineHint`. The deadline engine still resolves `DeadlineHint` into an actual `DateTime` — the classifier does not replace it.

## Outputs the classifier may contribute

- `TaskName` (optional)
- `TaskType` (canonical enum label)
- `Difficulty` (integer 1–5)
- `DeadlineHint` (raw phrase; resolved downstream)

Non-goals: it does not write to DB, does not touch `WeightConfig`, does not replace deadline parsing.

## Merge behavior inside `ParsingOrchestrator`

1. Run classifier (if present).
2. Run heuristic parsers (today's behavior).
3. Merge: classifier output is layered on top **only** if `IMlConfidencePolicy` allows it.
4. Use the deadline engine for `DeadlineHint` regardless of classifier output.
5. If confidence is insufficient, the result must be byte-equal to today's heuristic parse.

## Confidence policy (hard-coded for this release)

- `>= 0.60` → merge classifier output into parse result.
- `< 0.60` → drop classifier output, heuristic only.

Threshold lives behind `IMlConfidencePolicy` so it is testable and centrally tunable.

## Data contract — CSV import format

Required columns: `InputText, TaskType, Difficulty, DeadlineHint`.
Optional: `TaskName, Source, LabelVersion`.

Example rows:

| InputText | TaskName | TaskType | Difficulty | DeadlineHint |
|---|---|---:|---:|---|
| Nộp báo cáo AI thứ 6 tuần sau | báo cáo AI | DoAnCuoiKy | 3 | thứ 6 tuần sau |
| Ôn tập chương 3 trước tối mai | ôn tập chương 3 | BaiTapVeNha | 2 | tối mai |

Importer rules:
- Validate schema version (`LabelVersion` optional but recommended).
- Missing required columns → fail fast with a human-readable error.
- Seed and user datasets must be mergeable.

## File map

Create:
- `Services/ML/TextClassifier/`
- `Services/ML/TextClassifierService.cs` implementing `IIntentClassifierService`
- `Services/ML/TextClassifierModelManager.cs`
- `Services/ML/Schema/TextClassifierInput.cs`
- `Services/ML/Schema/TextClassifierOutput.cs`
- `Services/ML/Schema/TextClassifierPrediction.cs`
- `Services/ML/TextClassifierDatasetImporter.cs`
- `SmartStudyPlanner.Tests/MLTests/TextClassifierSchemaTests.cs`
- `SmartStudyPlanner.Tests/MLTests/TextClassifierTests.cs`

Modify:
- `Services/ServiceLocator.cs` — register `IIntentClassifierService` + thin `IIntentClassifier` adapter for `ParsingOrchestrator`.
- `Services/SmartParser.cs` — leave the static facade alone; the new path activates through DI in non-static callers.
- Parser preview ViewModel surfaces (`QuanLyTaskViewModel`, `DashboardViewModel` if needed).
- Task entry XAML to highlight classifier-extracted fields when present.

## Test coverage required

- Classifier present + high confidence → enriched parse output.
- Classifier present + low confidence → fallback to heuristic.
- Classifier absent → byte-equal to today.
- CSV import validation: missing columns fail fast.
- Parser merge: explicit user input is never overwritten.

## UX rules

- Parse preview shows extracted fields before save.
- If the classifier is uncertain, do not block — keep the existing editor unchanged.
- Quick parser must still not touch `NoteContent` or `StudyLinks` (M6.1 invariant).
- App must launch and parse without `text_classifier.zip` (offline-first).

## Lifecycle requirements

- Initialize / load existing artifact / train new artifact / validate quality / atomic save / rollback to last good model.
- Storage path follows M7 conventions under `%AppData%\SmartStudyPlanner\models\`.
- No network calls anywhere.

## Acceptance for M8-A

- `SmartParser` (via `ParsingOrchestrator`) consumes classifier output when available.
- Deadline hint still resolved by the existing engine.
- Fallback behavior remains deterministic when ML is unavailable or uncertain.
- CSV importer validates rigorously.
- Offline-first preserved.
