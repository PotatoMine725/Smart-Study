# Plan — Retrain the M8-A TextClassifier (intent / TaskType)

**Date:** 2026-06-16
**Status:** ✅ SHIPPED — Path A executed. `collected_v4` merged into the seed (698 → 903,
imbalance 2.21× → 1.11×) in `ab5112c`; data-prep tooling in `8855874`; SeedHash gate
auto-retrained the model on 903 rows. §7 verification done: build clean / 244 tests pass,
and the §7.3 per-class recall eval (before/after v698 vs v903, minority recall did not
regress) is in [`docs/reports/2026-06-25-m8a-textclassifier-v4-recall-eval.md`](../reports/2026-06-25-m8a-textclassifier-v4-recall-eval.md).
**Author:** planning session

---

## 1. Context

The request was to "re-check M8-B's ML status, review seed class imbalance,
read the labeled CSVs in `datasheets/`, then re-plan a retrain of the ML."
Investigation resolved the terminology and the actual scope, and a dedup
analysis of the datasheets overturned the initial premise. This plan records
the evidence and proposes the disciplined next step.

### What "M8-B" vs "M8-A" actually are
- **M8-B** (docs: *Ground-Truth Instrumentation* + `WeightRuleEngine` +
  telemetry like `DifficultyLabelLog`/`WeightChangeLog`) is the **Adaptive Rule
  Engine** (`ML_Heuristic_design.md` §3.5). It is **rule-driven and has no ML
  model**. Per §3.5 it MUST stay deterministic; its telemetry only accrues
  toward a *future* model.
- The **only retrainable ML model with these labels is the TextClassifier**
  (`TextClassifierModelManager`) — a multiclass `SdcaMaximumEntropy` over
  `TaskType` (5 classes = `LoaiCongViec`). This is the **intent-classification
  stage of the Smart Parser** (§5.1, the sanctioned ML-first component). Docs
  call it **M8-A**. The retrain target is this model.

### Spec verdict on "new model vs heuristic" (`docs/specs/ML_Heuristic_design.md`)
- §5 caps the system at **2 ML submodels**: Smart Parser + optional Performance
  Predictor. §9 forbids over-engineering.
- **Do NOT build a new Difficulty or weight ML model.** Difficulty is the
  Decision Engine's competency-gap heuristic (§3.1, deterministic); weights are
  the Adaptive Rule Engine (§3.5). Keep both heuristic.
- The TaskType pipeline featurizes **only `InputText` → `TaskType`**; the
  `Difficulty`/`DeadlineHint` columns are loaded and **ignored**. So the seed's
  severe *difficulty* skew is **irrelevant to this model** — out of scope.

---

## 2. Seed imbalance (the part that matters: TaskType)

`SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` — 698 rows:

| TaskType | rows |
|---|---|
| KiemTraThuongXuyen | 188 |
| ThiCuoiKy | 170 |
| DoAnCuoiKy | 131 |
| BaiTapVeNha | 124 |
| **ThiGiuaKy** | **85** |

Max/min ratio **2.21×** — *mild* for SDCA MaximumEntropy on ~700 rows. The real
weakness is **thin absolute coverage of the minority classes** (`ThiGiuaKy` 85,
`BaiTapVeNha` 124), which `Slice6ParserIntegrationTests` explicitly guards
(it asserts `ThiGiuaKy`/`DoAnCuoiKy` classify correctly — they were mislabeled
before seed v3). Tests currently pass.

---

## 3. Datasheets review — dedup against the current seed

All `*_m8a*` CSVs use schema `InputText,TaskName,TaskType,Difficulty,HasDeadline,
Urgency,TimeExpression,DeadlineType` (no `DeadlineHint`); `normalized_dataset.csv`
uses the old schema `VanBanGoc,TenTask,LoaiTask,DoKho`. Mapping `BaiTap→
BaiTapVeNha`, `DuAn→DoAnCuoiKy`, dropping non-enum labels (`NhacNho`,`OnTap`,
`Khac`), and **deduping `InputText` against the current 698-row seed**:

| Source (rows) | dropped (non-enum) | dup-of-seed | **net-new** | net-new by class |
|---|---|---|---|---|
| `synthetic_v3_giuaky_doan.csv` (101) | 0 | 101 | **0** | — (already in seed) |
| `normalized_dataset_m8a_uniform.csv` (1100) | 402 | 698 | **0** | — (= the seed) |
| `normalized_dataset_m8a_balanced.csv` (820) | 356 | 459 | **5** | BaiTapVeNha 4, ThiCuoiKy 1 |
| `normalized_dataset_m8a.csv` (1365) | 746 | 577 | **34** | BaiTapVeNha 28, KiemTra 4, ThiCuoiKy 2 |
| `normalized_dataset.csv` (1365, old schema) | 585 | 556 | **154** | DoAnCuoiKy 70, KiemTra 33, BaiTapVeNha 29, ThiCuoiKy 22 |

### Key findings
1. **`synthetic_v3` and `uniform` add ZERO net-new** — the seed already absorbed
   them. Re-merging is pure duplication (skewing toward duplicated rows).
2. **`ThiGiuaKy` — the thinnest, test-guarded class — has ZERO additive rows in
   any datasheet.** The classes that get net-new data are the ones already at
   adequate counts.
3. **The datasheets are noisier than the seed.** The identical sentence
   *"tình hình tiến độ cái đồ án tới đâu r ae"* is labeled `DuAn` in
   `normalized_dataset.csv` but `BaiTap` in `m8a.csv`; the m8a `BaiTap` bucket is
   polluted with project/proposal texts. Bulk-merging injects contradictory
   training signal into a curated set.
4. `DuAn` rows are genuinely academic projects (đồ án môn …), so `DuAn→DoAnCuoiKy`
   is defensible — but those 70 rows live in the **old schema** (column remap
   needed) and carry the cross-file label-noise risk above.

**Net:** the user's premise ("datasheets contain useful labels missing from the
seed") is only weakly true. After dedup, clean low-risk additive data is ~30–40
rows concentrated in `BaiTapVeNha`; the class that actually needs help
(`ThiGiuaKy`) cannot be augmented from these files at all.

---

## 4. Settled decisions (grounded — not open questions)
- **Retrain target = M8-A TextClassifier only.** No new model. (§5, §9)
- **Difficulty & weights stay heuristic.** (§3.1, §3.5) Difficulty skew is out of
  scope (column ignored by the pipeline).
- **Label policy = drop `NhacNho`/`OnTap`/`Khac`; map `BaiTap→BaiTapVeNha`,
  `DuAn→DoAnCuoiKy`.** This continues the project's *own* precedent (the current
  seed = `m8a_uniform` minus `NhacNho`/`OnTap` with `BaiTap` renamed) and avoids
  expanding the deterministic `LoaiCongViec` enum (§9). No enum/UI/prior changes.
- **Keep `Difficulty` as a valid float and a `DeadlineHint` column present** in
  any new rows so `TextClassifierDatasetImporter` (requires those 4 columns)
  doesn't throw. Values may be empty/derived — they aren't featurized.

---

## 5. Retrain mechanics (already supported — minimal code)
- Seed is hand-authored and embedded: `<EmbeddedResource Include="Services\ML\
  TextClassifier\seed_intents.csv" />` (`SmartStudyPlanner.csproj`).
- Replacing the file + rebuild changes the SHA-256 in
  `TextClassifierModelManager.ComputeSeedHash()`, so the **SeedHash gate
  auto-retrains** the seed-only model on next `InitializeAsync` (no code change).
- `TextClassifierModelManager.RetrainAsync(IReadOnlyList<TextClassifierInput>)`
  already exists for user/augmented data.
- No hard-coded row-count or label-set assertions in tests; only
  `Slice6ParserIntegrationTests` requires `ThiGiuaKy`/`DoAnCuoiKy` to keep
  classifying correctly. `TextClassifierSchemaTests` covers importer + lifecycle.
- There is **no runtime label normalizer** — TaskType strings must be exact enum
  names, so all label mapping/cleaning happens **offline during data prep**.

---

## 6. Chosen approach — Path A (targeted curated augmentation)

Author new minority-class examples (the only way to close the real gap, since no
datasheet supplies net-new `ThiGiuaKy`) + fold the *vetted* clean net-new
`BaiTapVeNha` from `m8a.csv`. Skip `uniform`/`synthetic_v3` (0 net-new) and the
noisy old-schema `DuAn` bucket.

### 6.1 Target counts (reduce 2.21× → ~1.3×, thicken minorities)
| Class | now | target | source of additions |
|---|---|---|---|
| KiemTraThuongXuyen | 188 | 188 | unchanged (ceiling) |
| ThiCuoiKy | 170 | 170 | unchanged |
| DoAnCuoiKy | 131 | ~145 | ~14 authored (optional) |
| BaiTapVeNha | 124 | ~150 | vetted net-new from `m8a.csv` + a few authored |
| **ThiGiuaKy** | **85** | **~150** | **~65 authored (new phrasings)** |

Resulting max/min ≈ 188/150 ≈ **1.25×**. Final counts flexible; the constraint is
"thicken `ThiGiuaKy`/`BaiTapVeNha`, do not inflate the two majorities."

### 6.2 Authoring method (mirrors how `synthetic_v3` was built)
- The existing 85 `ThiGiuaKy` rows already include the `synthetic_v3` batch, so
  new rows **MUST use distinct phrasings/subjects/time-expressions** or they
  dedup out. Build a small **offline generator** (one-off script — Python or a
  throwaway C# console; not shipped) that crosses:
  - a **subject list** not already saturated (e.g. Giải tích, Đại số tuyến tính,
    CTDL&GT, Mạng máy tính, Hệ điều hành, Vật lý đại cương, Kinh tế vĩ mô, …),
  - **new `ThiGiuaKy` templates** mixing formal + colloquial VN + some English
    ("thi giữa kỳ môn {m} {when}", "{when} kiểm tra giữa kỳ {m} rồi", "midterm
    {m} {when}", "giữa kỳ {m} sắp tới ôn thôi ae", …),
  - **time expressions** ({when}: tuần sau, thứ 5 này, 2 tuần nữa, cuối tháng…).
- Render → **normalize + dedup `InputText` against the current seed** → take N
  unique per class. Same recipe for the optional `DoAnCuoiKy` additions.

### 6.3 Vet the `m8a.csv` `BaiTapVeNha` net-new (28 rows)
- Map `BaiTap→BaiTapVeNha`; **discard project/proposal-mislabeled rows** (e.g.
  "nộp proposal", "đồ án …", "frontend/backend"); keep genuine homework
  (~15–25 expected to survive).

### 6.4 Schema-normalize every new row to the seed's columns
`InputText, TaskName, TaskType, Difficulty, DeadlineHint, Source, LabelVersion`.
- `TaskType` = exact enum name (no runtime normalizer exists).
- `Difficulty` = valid float (default `3` if unknown) — required by importer,
  *not* featurized.
- `DeadlineHint` = derived from `{when}` or empty (column must exist; not
  featurized).
- `Source` = e.g. `"synthetic_v4"` / `"m8a"`; `LabelVersion` = `"v4"`.

### 6.5 Apply
- Append vetted/authored rows to `seed_intents.csv` (existing 698 rows untouched),
  dedup `InputText` within + against the seed. Rebuild → **SeedHash gate
  auto-retrains** on next launch.

### Out of scope (explicitly skipped)
`normalized_dataset_m8a_uniform.csv`, `synthetic_v3_giuaky_doan.csv` (0 net-new);
`DuAn`→`DoAnCuoiKy` old-schema remap (label noise + column remap, §9); any
Difficulty/weight ML model (heuristic per §3.1/§3.5); enum changes.

---

## 7. Verification (applies to Path A / B)
1. `rtk dotnet build` — confirm the new seed embeds and parses.
2. `rtk dotnet test` — full suite; specifically `TextClassifierSchemaTests`
   (importer + train/cache/stale-hash retrain) and `Slice6ParserIntegrationTests`
   (`ThiGiuaKy`/`DoAnCuoiKy` still classify correctly) must pass.
3. One-off evaluation: split the new seed (e.g. 80/20, stratified), fit the
   pipeline, report `MulticlassClassification.Evaluate` MicroAccuracy +
   **per-class recall**, and confirm minority recall did not regress.
4. Delete `%APPDATA%\SmartStudyPlanner\models\text_classifier.zip` (or just launch
   — the SeedHash gate retrains automatically) and smoke-test a few intents.
5. `gitnexus_detect_changes()` before committing; commit the new seed CSV
   separately from any data-prep tooling (split by concern).

---

## 8. Data-collection spec (for user-gathered rows → new CSV)

User will collect rows by hand into a new CSV. This section is the contract that
CSV must meet. **Read §8.2 first — it is the rule that protects the dataset.**

### 8.1 Is +100 too little? — Yes, it is only a floor
- +100 rows (→ ~800 total) trains, but leaves the two thin classes still thin
  (`ThiGiuaKy` 85, `BaiTapVeNha` 124). If you are collecting anyway, the
  high-value target is **raising those two specifically toward the majorities**,
  not padding the total.
- **The binding constraint is phrasing diversity, not row count.** Proof from this
  repo: `synthetic_v3` added 101 templated rows and **0 survived dedup** because
  permutations of the same template collapse to duplicates. Collecting 200
  near-identical rows yields ~20 useful ones. Aim for *distinct real phrasings*
  (different subjects, registers, abbreviations, word order), not template fills.

### 8.2 Two kinds of "noise" — DO NOT conflate them
This is the single most important rule. The datasheets failed exactly here (same
sentence labeled `DuAn` in one file, `BaiTap` in another).

| Kind | Meaning | Target | Rule |
|---|---|---|---|
| **Label noise** | wrong `TaskType` for the text | **0%** | Every row must be correctly labeled. A mislabeled row actively poisons training — worse than no row. |
| **Input diversity / difficulty** | colloquial, abbreviations, code-switching (VN+EN), typos, no explicit keyword | **~20–30%** | *Wanted.* These hard examples are what make the classifier robust. The other ~70% should be clear/canonical phrasings. |

So: collect ~20–30% "hard" inputs, but **0% wrong labels**.

### 8.3 Per-class target (net-new UNIQUE rows after dedup)
"Net-new unique" = rows that survive dedup against the existing 698 (see §8.5).
Collect a bit extra to absorb dedup loss.

| Class | now | ideal target | net-new to collect | priority |
|---|---|---|---|---|
| **ThiGiuaKy** | 85 | ~180 | **+95** (min +65) | highest |
| **BaiTapVeNha** | 124 | ~180 | **+56** (min +25) | high |
| **DoAnCuoiKy** | 131 | ~180 | **+50** (optional) | medium |
| KiemTraThuongXuyen | 188 | 188 | 0 (do NOT inflate) | — |
| ThiCuoiKy | 170 | 170 | 0 (do NOT inflate) | — |

Result ≈ 180–190 per class, max/min ≈ **1.05×**. **The one knob that is yours to
set:** near-balance floor of ~180 (above) vs a lower floor (~150) — say which you
want and I adjust. Quality bar: **at least the seed's.** Below that bar, fewer
rows is better than more.

### 8.4 CSV format (hard constraints — importer throws otherwise)
`TextClassifierDatasetImporter` fails fast; these are not optional:

1. **Header row required.** Use the seed's exact columns, in order:
   `InputText,TaskName,TaskType,Difficulty,DeadlineHint,Source,LabelVersion`
   - Required by importer: `InputText, TaskType, Difficulty, DeadlineHint`.
   - `TaskName, Source, LabelVersion` optional but include for consistency.
2. **`TaskType` MUST be one of these 5 EXACT strings** (case-sensitive enum names —
   no runtime normalizer exists; `BaiTap`/`DuAn`/`NhacNho`/`OnTap`/`Khac` will be
   rejected or silently unparseable):
   `BaiTapVeNha`, `KiemTraThuongXuyen`, `ThiGiuaKy`, `DoAnCuoiKy`, `ThiCuoiKy`
3. **`Difficulty` must be a parseable float** (importer throws on non-float). Put
   `3` if unknown. It is **not featurized** — value is irrelevant to the model,
   it just must parse.
4. **`DeadlineHint`** may be empty; column must exist. Not featurized.
5. **Encoding UTF-8** (Vietnamese diacritics). If a field contains a comma or
   quote, wrap it in double quotes and escape inner quotes by doubling (`""`).
6. `Source` = e.g. `collected_v4`; `LabelVersion` = `v4` (free text, for
   provenance only).

**Example rows** (copy this shape):
```csv
InputText,TaskName,TaskType,Difficulty,DeadlineHint,Source,LabelVersion
thi giữa kỳ giải tích tuần sau,,ThiGiuaKy,3,tuần sau,collected_v4,v4
giữa kỳ CTDL&GT thứ 5 này ôn thôi ae,,ThiGiuaKy,3,thứ 5,collected_v4,v4
midterm OS in 2 weeks,,ThiGiuaKy,3,2 weeks,collected_v4,v4
"nộp bài tập OOP, deadline thứ 6",,BaiTapVeNha,2,thứ 6,collected_v4,v4
lab mạng máy tính nộp cuối tuần,,BaiTapVeNha,3,cuối tuần,collected_v4,v4
đồ án môn hệ điều hành nộp cuối kỳ,,DoAnCuoiKy,4,cuối kỳ,collected_v4,v4
```

### 8.5 Dedup rule I will apply (so you don't waste effort)
A new row is a duplicate (discarded) if its **normalized `InputText`** equals any
existing seed row's. Normalization = lowercase → trim → collapse internal
whitespace runs to one space. So "Thi giữa kỳ   Toán" and "thi giữa kỳ toán" are
the *same* row. Vary the actual words/subjects, not just spacing/casing.

### 8.6 Label definitions (collect against THESE, to keep label noise at 0%)
- **`ThiGiuaKy`** — midterm exam / kiểm tra giữa kỳ. NOT a regular quiz, NOT final.
- **`ThiCuoiKy`** — final exam / thi cuối kỳ / thi hết môn.
- **`KiemTraThuongXuyen`** — routine/short quiz, kiểm tra 15', kiểm tra miệng,
  kiểm tra thường xuyên. NOT midterm/final.
- **`BaiTapVeNha`** — homework / bài tập về nhà / lab / exercise to submit. NOT a
  semester project.
- **`DoAnCuoiKy`** — end-of-term project / đồ án môn / đồ án cuối kỳ / capstone.
  (This is where datasheet `DuAn` belongs — but only genuine projects.)

### 8.7 Hand-off
Drop the collected file anywhere (e.g. `datasheets/collected_v4.csv`). I will:
normalize + dedup against the seed (report how many survived per class) → append
survivors to `seed_intents.csv` (existing 698 untouched) → rebuild → SeedHash gate
auto-retrains → run §7 verification (incl. per-class recall) before committing.
