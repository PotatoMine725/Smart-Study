# Data Maturation — Phase 0: data audit and gap mapping

**Date:** 2026-08-25
**Author/agent:** Claude (Opus 5) via Claude Code, acting as independent ML/data auditor
**Branch at time of audit:** `docs/encoder-knowledge-consolidation`
**Mandate:** `DAT-03` (SHOULD, separate ongoing workstream) in
[`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) §9

---

## Scope

Audit only. This report inventories the data the repository actually holds, reconstructs how it
came to be, states which numbers rest on which rows, and ranks the gaps by *what would fix them*.

**It contains no proposal, no synthetic-data recommendation, no architecture change and no
implementation plan.** Whether synthetic generation is part of the answer is deliberately left
open — §5 ranks gaps by the kind of intervention they need, and generation is only one of several.

**Not covered:** LAN-sync data model (`docs/knowledge/sync-data-model.md`), model-artifact storage
mechanics, and the ONNX encoder artifacts under `tools/ml-pilot/models/` (weights, not training
data — they are inputs to a stopped pilot).

**Evidence convention.** Every number below is marked `[measured]` (computed in this session from
repository bytes), `[recorded]` (quoted from a committed document), or `[inferred]` (reasoned from
the two). Inferences are labelled as inferences.

---

## 1. Findings — Inventory

Twenty-one data sources bear on ML or parser training/evaluation. They fall into four groups; only
group A ever reaches a trained model.

### Group A — Corpora that train or evaluate a model

| # | Path | Rows | Real / synth / derived | Task + labels | Trained on? | Evaluated on? | Overlap |
|---|---|---|---|---|---|---|---|
| A1 | `SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` | **903** `[measured]` | mixed: 597 derived-synth + 101 synth + **205 real** | intent → `TaskType` (5 cls) | **Yes — production, all 903, no split** | Yes (contaminated, §3.2) | superset of A2, A3, A4, A9, A10 |
| A2 | `datasheets/collected_v4.csv` | **205** `[measured]` | **real** — the only real user-authored text in the repo | `TaskType` (3 cls), `Difficulty`, `DeadlineHint` | Yes, via A1 | Yes — is the S0 test set | 100% inside A1 and A10 |
| A3 | `datasheets/synthetic_v3_giuaky_doan.csv` | 101 `[measured]` | synthetic, hand-authored | `TaskType` (5 cls) + 4 aux cols | Yes, via A1 | no | 100% inside A1, A4, A9 |
| A4 | `datasheets/normalized_dataset_m8a_uniform.csv` | 1100 `[measured]` | derived 810 + synth 101 + **189 of unknown origin** (§4.5) | `TaskType` (7 cls) + aux | 698 of 1100 via A1 | no | 810 via A7; 402 rows never used |
| A5 | `datasheets/normalized_dataset.csv` | 1365 (**1028 unique**) `[measured]` | derived; **root of the in-repo lineage** | `LoaiTask` (7 cls, old schema) | Not directly | no | same 1028 inputs as A6 |
| A6 | `datasheets/normalized_dataset_m8a.csv` | 1365 (**1028 unique**) `[measured]` | relabelled copy of A5 | `TaskType` (5 cls) + aux | Not directly | no | see §4.1 — 533/1028 labels differ from A5 |
| A7 | `datasheets/normalized_dataset_m8a_balanced.csv` | 820 `[measured]` | derived subset — **the lineage intermediate, not a side branch** (§2) | `TaskType` (5 cls) | Indirectly: 810 of its rows reach A1 | no | strict subset of A6; 810 of 820 inside A4 |
| A8 | `SeedDataGenerator.Generate()` (code, not a file) | **180** `[recorded — read from source, not executed]` | **procedurally generated, labels are RNG draws** | study-time regression → minutes | **Yes — M7 predictor, every run in practice (§3.3)** | 20% internal split | none |
| A9 | `tools/ml-pilot/split/train.csv` | 698 `[measured]` | filter of A1 (`Source ∈ {m8a_uniform, synthetic_v3}`) | `TaskType` | S0 pilot arms only | no | 100% inside A1 |
| A10 | `tools/ml-pilot/split/test.csv` | 205 `[measured]` | filter of A1 (`Source = collected_v4`) | `TaskType` | **No (by design)** | S0 pilot arms | == A2, 100% inside A1 |

### Group B — Evaluation-only fixture and corpus assets

| # | Path | Size | Nature | Purpose |
|---|---|---|---|---|
| B1 | `datasheets/vn_input_fixtures.csv` + `.md` | 39 rows, 37 unique `[measured]` | curated fixtures | `DAT-05` robustness set: diacritics 8 / stripped 8 / abbrev 8 / runtogether 6 / empty 6 / pathological 3. **The only source in the repo with a datasheet.** 7 rows drawn from A2 |
| B2 | `docs/reports/data/2026-08-05-soe-t36-baseline.json` | 230 schedules `[measured]` | generated, seed 12345 | scheduler baseline capture (Epic 3 T3.6) — not ML |
| B3 | `docs/reports/data/2026-08-07-soe-t34-corpus-report.json` | 230 items `[measured]` | generated | three-arm scheduler partition — not ML |
| B4 | `SmartStudyPlanner.Tests/Services/Soe/SoeCorpusGenerator.cs` | — | deterministic generator | produces B2/B3; self-documents as a measurement crutch, not production truth |
| B5 | `tools/ml-pilot/results/*.json` (15 files) | — | S0 measurement outputs | per-arm accuracy, latency, memory, tokenization, `vocab_gap.json` |
| B6 | `tools/TextClassifierEval/` | — | throwaway eval harness, outside `SmartStudyPlanner.slnx` | produced the 97.24% / 97.25% figures in the 2026-06-25 recall eval; mirrors the production pipeline with an 80/20 stratified split |

### Group C — Telemetry: real signal, captured, mostly unconsumed

| # | Table / writer | Rows observed `[measured]` | Consumer | Status |
|---|---|---|---|---|
| C1 | `StudyTimeOutcomeLogs` ← `FocusViewModel.LuuThoiGianThucTe` | Debug DB **2**, Release DB **0** | `StudyTimeTrainingDataSource` (gate `MinRows = 50`) | **Gate never met → predictor always falls back to A8** |
| C2 | `DifficultyLabelLogs` ← `QuanLyTaskViewModel:333` | Debug **3**, Release **17** (12 overrides) | **none** — written, never read | Real human difficulty labels with no consumer |
| C3 | `WeightChangeLogs` + `OutcomeMaturationService` (14-day cohort fill) | Debug 1, Release 0 | rule engine, not a model | shipped 2026-06-11 |
| C4 | `OptimizerRunLogs` | 0 | — | **empty is correct**: G3-1 optimizer wiring unscheduled |
| C5 | `StudyLogs` (Debug DB) | 5404 | analytics UI | **generated demo data, not real usage** — see §4.6 |
| C6 | `UserStatsSnapshot` | derived aggregate | `WeightRuleEngine` | rule input, no training data |

> The application database is `SmartStudyData.db` beside the executable
> (`AppDbContext.cs:51`) and is **untracked** — `git ls-files | grep '\.db$'` returns nothing
> `[measured]`. **No production telemetry exists in the repository at all**; the counts above come
> from two local dev databases in `bin/` (Debug, mtime 2026-07-26; Release, mtime 2026-08-19) and
> describe this machine, not any user population.

### Group D — Hand-authored rule data (data living as code)

| # | Location | Size |
|---|---|---|
| D1 | `Services/Strategies/IDeadlineKeywordParser.cs` | 33 string literals `[measured — upper bound on lexicon size, not a coverage measure]` |
| D2 | `Services/Strategies/IDifficultyKeywordParser.cs` | 30 `[same bound]` |
| D3 | `Services/Strategies/ITaskTypeKeywordParser.cs` | 15 `[same bound]` |

These are the heuristic parser's lexicon. They are versioned with the code, have no held-out set,
and are the fallback the whole offline-first contract rests on.

---

## 2. Findings — Reconstructed lineage

```text
normalized_dataset.csv  (1365 rows / 1028 unique · 7-class old schema: VanBanGoc,TenTask,LoaiTask,DoKho)
   │   provenance UNDOCUMENTED — enters the repo in bulk commit b29cd24 "refactor(ui): commit
   │   remaining study planner changes", with no datasheet, no collection note, no annotator record
   │
   ├─► [schema migration + RE-ANNOTATION]
   │      VanBanGoc→InputText, LoaiTask→TaskType, DoKho→Difficulty,
   │      + HasDeadline, Urgency, TimeExpression, DeadlineType
   │      Labels Khac and DuAn retired.
   ▼
normalized_dataset_m8a.csv  (same 1028 unique inputs)
   │   ⚠ 533/1028 rows carry a DIFFERENT TaskType than A5 [measured] — §4.1
   │
   ├─► [balance pass]
   ▼
normalized_dataset_m8a_balanced.csv  (820 — a STRICT SUBSET of m8a [measured])
   │   Not a dead branch: it is the intermediate every m8a-derived seed row passes through.
   │   810 of its 820 rows continue; 10 are dropped [measured].
   │
   ├──────────────┬───────────────────────────┐
   │ 810 derived  │ + synthetic_v3 (101)      │ + 189 rows of UNKNOWN ORIGIN [measured]
   │              │                           │   present in NO other committed file — §4.5
   ▼              ▼                           ▼
normalized_dataset_m8a_uniform.csv  (810 + 101 + 189 = 1100 — set identity verified [measured])
   │   v3 relabel in place (commit 9603c17): over the 810 inputs shared with m8a,
   │   121 rows relabelled [measured] — BaiTap→DoAnCuoiKy 93, ThiCuoiKy→ThiGiuaKy 23, +5 others.
   │   CHANGELOG records the intent as "31 giữa kỳ + 96 đồ án rows" [recorded];
   │   96 reconciles exactly, 25-of-31 are visible in the shared window [measured].
   │   ⚠ CHANGELOG says "Added 100 synthetic rows (1000 → 1100)" [recorded] but the file holds 101
   │     and all 101 appear in uniform [measured]. Off-by-one unreconcilable from the artifacts.
   │
   ├─► [PROJECTION ONTO THE PRODUCTION ENUM]
   │      drop NhacNho (217) + OnTap (185) = 402 rows  ── verified by set difference [measured]
   │      rename BaiTap → BaiTapVeNha
   ▼
seed_intents.csv  v3  (698 rows, 5 classes, LabelVersion=v3)
   │   ── "held-out 96.2%" measured HERE, stratified 85/15, n=106 [recorded, CHANGELOG:212].
   │      That figure predates any real data and is an in-distribution synthetic number.
   │
   ├─► + collected_v4.csv (205 real rows) via datasheets/_merge_seed.py
   │      dedup key = re.sub(r"\s+"," ",s.strip().lower()) — no diacritic folding
   │      merge was purely additive: 205 insertions, 0 deletions (commit ab5112c) [measured]
   ▼
seed_intents.csv  v4  (903 rows, LabelVersion v3+v4)
   │   SHA-256 86abb454… — matches the pin in tools/ml-pilot/split/SPLIT.md ✓ [measured]
   │
   ├─────────────────────► PRODUCTION: TextClassifierModelManager trains on ALL 903, no split
   │                        (embedded resource; SeedHash change auto-triggers retrain)
   │
   └─► build_split.py (pure filter on Source column, no shuffle, no seed)
          ├─ train.csv 698  (synthetic only)
          └─ test.csv  205  (real only)  ────► S0 encoder pilot arms only
```

**Separately, and never joined to the above:**

```text
FocusViewModel session end ──► StudyTimeOutcomeLogs ──[gate: ≥50 rows]──► StudyTimePredictor
                                   (0–2 rows observed)        │
                                                        gate fails ▼
                                              SeedDataGenerator.Generate() — 180 rows, RNG labels

QuanLyTaskViewModel save   ──► DifficultyLabelLogs ──────────► (nothing)
```

---

## 3. Findings — What actually trains on what

### 3.1 The intent classifier uses one column of a seven-column corpus

`TextClassifierModelManager.TrainAndSaveAsync` builds
`MapValueToKey("Label","TaskType") → FeaturizeText("Features","InputText") → SdcaMaximumEntropy`
`[measured, TextClassifierModelManager.cs:147-150]`.

`InputText` is the only feature; `TaskType` the only label. **`Difficulty`, `DeadlineHint`,
`TaskName`, `Source` and `LabelVersion` are parsed, carried, validated — and never trained on.**
The archived retrain plan states the same intent explicitly: *"Difficulty skew is out of scope
(column ignored by the pipeline)"* `[recorded]`.

Consequence: the repository holds **903 difficulty labels and 903 task-name labels that no model
has ever seen**, and `TaskName` has 477 distinct values over 903 rows `[measured]`.

### 3.2 The shipped model has already seen every real row

The seed model trains on all 903 rows with **no train/test split and no accuracy gate** — the code
says so in its own comment `[measured]`. All 205 real `collected_v4` rows are inside those 903.

Therefore: **there is no uncontaminated real data left with which to evaluate the shipped
classifier.** The 205 real rows are simultaneously the only real *training* data and the only real
*evaluation* data. S0 avoided this by retraining every arm from `train.csv` alone — a property of
the pilot harness, not of production.

This confirms and extends what `SPLIT.md` and the S0 report already record about the 96.2% figure;
the extension is that the problem is *structural and current*, not merely historical.

### 3.3 The study-time predictor has never trained on real data

`StudyTimeTrainingDataSource.MinRows = 50`. Below that it returns empty and
`AnalyticsViewModel.RetrainModel` substitutes `SeedDataGenerator.Generate()` `[measured]`.
Observed `StudyTimeOutcomeLogs`: 2 rows and 0 rows `[measured]`.

What that fallback actually contains: three groups of 60 rows, each a single fixed
(difficulty, credits, daysLeft) point, with `Label = uniform(min,max) × (1 ± 0.15)` — **the label
is a random draw** `[measured, SeedDataGenerator.cs]`. There are **3 distinct feature vectors**.
Within a group the target carries no information; between groups it is a step function.

`[inferred]` The R² ≥ 0.45 persistence gate is therefore satisfied by between-group separation
(20–60 / 60–120 / 120–240 minutes) while the model learns nothing within a group. The gate is
passable by a three-value lookup table. This is an inference from the data construction and the
gate value; it was not measured by running the trainer.

### 3.4 The telemetry that exists cannot measure its own model

`FocusViewModel` writes the outcome row with `PredictedMinutes = null` and `Confidence = null`
`[measured, FocusViewModel.cs:151-153]`, while capturing `WasMlPrediction`.

So the log records what happened but not what was predicted. **Prediction error and calibration
are not computable from telemetry, no matter how many rows accumulate.** Fixing this after the
fact is impossible; only rows written after an instrumentation change can carry it.

---

## 4. Findings — Quality and distribution gaps, measured

### 4.1 Annotation instability: the same text, two label passes, 29.6% disagreement

Over the 1028 inputs common to `normalized_dataset.csv` and `normalized_dataset_m8a.csv`
`[measured]`:

| Comparison | Rows | Share |
|---|---|---|
| Labels identical | 495 | — |
| Differ because the old label was **retired** (`Khac`, `DuAn` — a forced move) | 325 | 31.6% of common |
| Differ **although the old label still exists** in the new taxonomy | **208** | **20.2% of common** |
| → disagreement rate restricted to rows whose old label survived | **208 / 703** | **29.6%** |

Largest genuine transitions: `OnTap→NhacNho` 105, `KiemTraThuongXuyen→NhacNho` 29,
`ThiCuoiKy→BaiTap` 26, `ThiCuoiKy→NhacNho` 17, `BaiTap→NhacNho` 16.

`Difficulty` on the same rows disagrees on **167/1028 = 16.2%**, with transitions
5→4 (50), 3→4 (41), 1→3 (27), 4→2 (23) `[measured]`.

`[inferred]` Two annotation passes over identical text disagreed on roughly three rows in ten
inside a shared label space. No annotation guideline, adjudication record, or annotator identity
exists anywhere in the repository for either pass. The archived retrain plan reached the same
conclusion qualitatively — *"the datasheets are noisier than the seed… bulk-merging injects
contradictory training signal"* `[recorded]` — and declined to merge them. This audit puts a
number on it.

**This is the ceiling nobody has priced.** Whatever accuracy a future model reports, it is being
graded against labels of this stability.

Within-file the corpora are clean: 0 conflicting labels across all 168 duplicate-input groups in
both files `[measured]`. The instability is strictly *between* passes.

### 4.2 The training distribution is not the input distribution

| Source in the seed | n | median chars | median words | `DeadlineHint` empty |
|---|---|---|---|---|
| `m8a_uniform` (derived-synth) | 597 | 46 | 11 | 58% |
| `synthetic_v3` (hand-authored synth) | 101 | 59 | 14 | 45% |
| **`collected_v4` (real)** | **205** | **33** | **7** | **0%** |

`[measured]` Real student input is roughly **half the length** of the synthetic rows the model
mostly trains on, and always carries a deadline expression.

The S0 pilot measured the vocabulary consequence `[recorded, vocab_gap.json]`: 25.0% of test
tokens are unseen in training; **94.6% of real test rows contain at least one token the training
set never shows**. The top unseen tokens are exactly the abbreviations students type — `bt` (54),
`tgk` (28), `t6` (8), plus course codes (`oop`, `attt`, `vxl`, `trr`, `qtm`).

### 4.3 Class coverage: real data exists for 3 of 5 classes

`collected_v4` — the entire real corpus — covers `ThiGiuaKy` 99, `BaiTapVeNha` 56, `DoAnCuoiKy` 50.
**`KiemTraThuongXuyen` and `ThiCuoiKy` have zero real examples** `[measured, SPLIT.md recorded]`.

Compounding it, the imbalance runs *opposite* between the two: `ThiGiuaKy` is the smallest training
class (85, 12.2%) and the largest real class (99, 48.3%).

The archived plan already established that `ThiGiuaKy` **cannot be augmented from any datasheet in
the repo** — after dedup, zero additive rows exist for it `[recorded]`.

### 4.4 Versioning and provenance governance

- **`collected_v4` has no datasheet.** Who collected 205 real student utterances, from what
  population, when, under what consent, and who assigned `Difficulty` — none of it is recorded
  anywhere `[measured: exhaustive grep of `docs/` and repo-root markdown]`. `_merge_seed.py`
  documents the *merge*; nothing documents the *collection*. `vn_input_fixtures.md` is the only
  datasheet in the repository, for the smallest source (39 rows).
- **`Difficulty` labels are source-dependent, not text-dependent.** In the seed: `m8a_uniform`
  puts 58% of rows at difficulty 3; `collected_v4` puts 63% at 4-or-5 `[measured]`. `[inferred]`
  This is an annotator/provenance effect, not a property of the text.
- **`LabelVersion` is a column, not a mechanism.** It takes values `v3`/`v4` inside the seed; the
  datasheet files carry no version at all, and there is no manifest, hash, or row-level lineage
  linking a seed row back to its datasheet origin beyond the `Source` string.
- **`normalized_dataset_m8a_balanced.csv` (820 rows) has zero *live* consumers** — referenced only
  by an archived plan `[measured]` — **but it is not disposable**: it is the lineage intermediate
  (§2), and 810 of its rows are the traceable half of the shipped seed. Zero consumers and zero
  value are different properties; only the first was measured here.
- **Duplicate inflation:** `normalized_dataset*.csv` hold 1365 rows over 1028 unique inputs — 337
  duplicate rows, 24.7% `[measured]`. Labels are consistent, so this is **sample weighting, not
  noise**: any training over those files silently weights 168 inputs 2–3×.

### 4.5 Provenance is broken *inside* the shipped corpus — 15% of it has no traceable origin

`normalized_dataset_m8a_uniform.csv` holds 1100 rows. Its composition, verified by set identity
`[measured]`:

| Component | Rows | Origin |
|---|---|---|
| carried from `_balanced.csv` (itself a strict subset of `m8a`) | 810 | traceable to `normalized_dataset.csv` |
| `synthetic_v3_giuaky_doan.csv` | 101 | traceable, hand-authored |
| **untraceable** | **189** | **present in no other committed file** |

Those 189 rows appear in no other datasheet: not in `m8a`, not in `normalized_dataset.csv`, not in
`_balanced.csv`, not in `synthetic_v3` `[measured]`. **136 of them are in the shipped seed** —
**15.1% of the 903-row production corpus, and 19.5% of the 698-row synthetic training split.**

They are not spread evenly. By class, the share of each shipped seed class that is untraceable
`[measured]`:

| Class | untraceable / total | share |
|---|---|---|
| `KiemTraThuongXuyen` | 71 / 188 | **38%** |
| `ThiCuoiKy` | 60 / 170 | **35%** |
| `ThiGiuaKy` | 5 / 184 | 3% |
| `BaiTapVeNha` | 0 / 180 | 0% |
| `DoAnCuoiKy` | 0 / 181 | 0% |

**The concentration is the finding.** `KiemTraThuongXuyen` and `ThiCuoiKy` are precisely the two
classes with **zero real evaluation data** (§4.3). The two classes nobody can measure on real input
are also the two classes that are roughly one-third made of rows whose origin nobody can state.

Two further observations:

- All 136 carry `Source = m8a_uniform` in the seed `[measured]`. **The `Source` column records the
  first file a row was seen in, not where it came from** — it reads as provenance and is not.
- They are a distinct population: median 23 characters, against 58 for the `m8a`-derived seed rows
  `[measured]`. `[inferred]` Short, terse rows entering `uniform` from an uncommitted step — the
  same shape as real student input (§4.2, median 33), which makes their absence of provenance
  matter more, not less: if any are real, the corpus mixes unlabelled-provenance real data into
  what every document calls the synthetic training half.

`[inferred]` Something produced or edited 189 rows between `_balanced.csv` and `uniform`, and that
step is not in the repository. Commit `b29cd24` — the bulk commit that introduced all four
`normalized_dataset*` files at once — is where the trail ends; it does not distinguish them.

### 4.6 What is *not* wrong — checks that came back clean

Stated so the next session does not re-spend effort here:

- **The S0 split is sound.** `seed_intents.csv` hashes to `86abb454…`, matching `SPLIT.md`'s pin
  exactly `[measured]` — the split is not stale and every S0 number still corresponds to it.
- **No train/test leakage, including near-duplicates.** Exact, whitespace-normalised, and
  full diacritic-folded + punctuation-stripped comparisons all return **0** overlap between
  `train.csv` and `test.csv`, and 0 between `collected_v4` and `train.csv` `[measured]` — this
  audit re-ran the check under a stricter normalisation than `SPLIT.md` certified.
- **Intra-file near-duplication is minor** in the shipped seed: 903 exact-unique → 897 under
  diacritic folding (6 collisions) `[measured]`.
- **Duplicate rows never carry conflicting labels** (§4.1).
- **`OptimizerRunLogs` being empty is correct**, not a data gap (G3-1 wiring unscheduled).
- **`StudyLogs` = 5404 rows in the Debug DB is not real telemetry.** 302 distinct `MaTask` against
  14 rows in `StudyTasks`, `DaHoanThanh` true on 5400/5404, and duration values recurring exactly
  120 times each `[measured]` — `[inferred]` generated demo data for the analytics UI.

---

## 5. Findings — Gaps ranked by what would fix them

Ranked by expected limit on future model performance. **Fix class** is the discriminator: it says
what kind of work closes the gap, so the later proposal can group by intervention rather than by
symptom. No gap below is a commitment; all are findings.

| # | Gap | Fix class | Why it ranks here |
|---|---|---|---|
| **G-1** | **Label instability (~29.6% between passes, §4.1) with no guideline, no adjudication, no annotator record** | **Adjudication + guideline** (not collection) | Bounds every accuracy number any future model can honestly report. Collecting more data *at this label quality* multiplies the noise. Ranked first because it is the only gap that degrades the value of work done on the others. |
| **G-2** | **No uncontaminated real evaluation set** (§3.2) — the 205 real rows are inside the shipped model's training data | **Collection with a hold-out discipline** | Without it, no claim about the shipped classifier is measurable. Cheapest structural fix: hold out *before* merging, which is the exact error `_merge_seed.py` already made once. |
| **G-3** | **15.1% of the shipped corpus has no traceable origin** (§4.5) — 136 rows in no other committed file, concentrated at 38% of `KiemTraThuongXuyen` and 35% of `ThiCuoiKy` | **Forensics first, then re-derivation or re-collection** | Ranked third because it *compounds* G-1 and G-4 rather than adding to them: for these rows one cannot ask who labelled them or against what guideline, and they sit in exactly the two classes with no real evaluation data. Until their origin is established, any statement about the corpus's synthetic/real composition is unverified. |
| **G-4** | **Training vocabulary excludes what students type** (§4.2) — 94.6% of real rows contain an unseen token; `bt`, `tgk`, course codes absent | **Collection** (real), possibly abbreviation-aware normalisation | Already the S0 report's F-2. The single strongest explanation for real-input degradation, and independent of model architecture. |
| **G-5** | **Two of five classes have zero real examples** (§4.3), and the thinnest class cannot be augmented from any existing file | **Targeted collection** | Blocks `DAT-01` from ever being lifted. A 5-class production claim is unreachable from present data by any transformation. |
| **G-6** | **Study-time predictor trains on 180 RNG-labelled rows** (§3.3); real-telemetry gate (≥50) never met | **Instrumentation + accrual** | An entire shipped model is trained on noise. Higher raw severity than G-4, ranked lower only because the fix is mechanical: rows accrue on their own once the app is used. |
| **G-7** | **`PredictedMinutes`/`Confidence` not recorded** (§3.4) — error and calibration uncomputable | **Instrumentation** (small, urgent) | Every day this stays unfixed produces rows that can never answer the question. Cheapest high-value item in the table; unlike the others, delay makes it strictly worse. |
| **G-8** | **`DifficultyLabelLogs` written but never read** (§C2); `Difficulty` column in the seed never trained on (§3.1) | **Plumbing / derivation** — data already exists | 17 rows on one machine with 12 overrides `[measured, n=17 — indicative only]`. A real supervised difficulty signal is being discarded at both ends. Bears directly on the deferred M8-A confidence-gate work. |
| **G-9** | **No datasheets, no dataset versioning, no row-level lineage** (§4.4) — `collected_v4` provenance unrecorded; the `Source` column names a file, not an origin (§4.5) | **Housekeeping / governance** | `DAT-03` names "version datasets" explicitly. G-3 is the acute case of this chronic gap: the governance absence is what let 136 untraceable rows into a shipped model unnoticed. |
| **G-10** | **Duplicate inflation, 24.7% of the datasheet rows** (§4.4) | **Housekeeping** | Consistent labels, so it is silent 2–3× weighting rather than noise. Matters only if the datasheets are ever used again. |
| **G-11** | **`TaskName` (477 distinct / 903) unused** (§3.1) | **Latent capability, not a gap to fix** | Recorded so a later design session knows a second labelled task already exists in the corpus. Not a limitation on current models. |
| **G-12** | **`normalized_dataset_m8a_balanced.csv` has no live consumer** — but it is the lineage intermediate (§2), **not** a dead branch | **Retain, do not delete** | Listed to *prevent* a housekeeping deletion: removing it would sever the only link between the shipped seed and `normalized_dataset.csv`. |

**The shape of the answer, stated plainly:** the repository holds **205 real labelled rows**. Of
the remaining 698, most descend from a single undocumented file by two annotation passes that
disagree with each other about three times in ten — and **136 descend from nothing traceable at
all**. The constraint is not corpus size. It is that *nothing in the corpus is known to be
correctly labelled*, that 15% of it cannot even be attributed, and that the only real data has
already been spent on training. **This is why "generate more synthetic data" cannot be assumed to
be the intervention: G-1, G-2 and G-3 are not size problems, and synthetic generation run against
the current label definitions makes G-1 worse while leaving G-3 untouched.**

---

## 6. Verification

| Check | How | Result |
|---|---|---|
| Seed integrity vs `SPLIT.md` pin | SHA-256 of `seed_intents.csv` | **match** — `86abb454…` |
| Row counts, class and label distributions, all 10 CSVs | `csv.DictReader` over repo bytes, UTF-8-sig strict | as tabulated |
| Pairwise text overlap, all 10 CSVs | exact set intersection | as tabulated §1/§2 |
| Near-duplicate leakage | whitespace-normalised + `đ→d` + NFD mark-strip + punctuation-strip | 0 across all train/test pairs |
| Label conflicts within duplicate groups | grouped by input, cardinality of label set | 0 / 168 groups, both files |
| 402-row drop | **set difference**, `uniform − (train ∪ synthetic)` | `OnTap` 185 + `NhacNho` 217 — verified, not inferred from arithmetic |
| Chain direction `m8a` / `balanced` / `uniform` | subset tests in both directions | `balanced ⊂ m8a` **true**; `uniform ∩ m8a` **==** `uniform ∩ balanced` (both 810) → `balanced` is the intermediate |
| Uniform composition | set identity `uniform == (uniform∩balanced) ∪ synthetic ∪ orphans` | **true**, 810 + 101 + 189 = 1100 |
| Untraceable rows reaching production | `(uniform − m8a − synthetic) ∩ seed`, then class tally | 136 rows; 38% of `KiemTraThuongXuyen`, 35% of `ThiCuoiKy` |
| Cross-pass label disagreement | join on input text, partitioned by whether the old label survived the taxonomy | 208 / 703 = 29.6% |
| Telemetry row counts | `sqlite3` **read-only** URI (`mode=ro`) on two untracked dev DBs | as tabulated §C |
| Training pipeline features | read `TextClassifierModelManager.cs:143-152`, `MLModelManager.cs:91-106` | `InputText` → `TaskType` only |
| Provenance search | grep of `docs/`, repo-root `*.md`, `datasheets/`, git history | no collection record for `collected_v4` |
| Lineage commits | `git log --diff-filter=A --follow`, `git show --stat` | `b29cd24`, `9603c17`, `ab5112c`, `8855874`, `df5ac68` |

**Not run, and why:**

- **No model was trained or evaluated.** The §3.3 claim about the R² gate is `[inferred]` from the
  data construction, not measured. Verifying it means running the trainer — out of audit scope.
- **No file was modified.** All CSV reads and both SQLite connections were read-only; the dev
  databases were opened with `mode=ro`.
- **Telemetry counts are from two dev machines' `bin/` databases**, mtimes 2026-07-26 and
  2026-08-19. They are an existence proof about instrumentation reachability, **not** a measurement
  of any user population. The 12/17 difficulty-override rate is indicative at n=17 and must not be
  cited as a rate.
- **The 100-vs-101 synthetic-row discrepancy (§2) was not resolved.** It cannot be settled from the
  committed artifacts; it is recorded rather than explained away.
- **Group D lexicons were counted, not quality-assessed.** String-literal counts are an upper bound
  on lexicon size, not a measure of coverage.

---

## 7. Follow-ups

| # | Item | Owner | Where it belongs | Status |
|---|---|---|---|---|
| 1 | **G-3** — 136 rows (15.1%) of the shipped corpus have no traceable origin, concentrated in the two classes with no real evaluation data | Owner | Data Maturation proposal (Phase 1 input) — **and a provenance question the owner may be able to answer directly from memory of the original data prep** | **finding, unscheduled** — the fastest resolution is owner recall, not forensics |
| 2 | **G-7** — outcome log omits `PredictedMinutes`/`Confidence`; every day's rows are permanently unanalysable | Owner | New defect candidate against shipped M8-C, independent of this workstream | **defect candidate** — needs a new owner decision |
| 3 | **G-1** — no annotation guideline or adjudication record exists for any label pass | Owner | Data Maturation proposal (Phase 1 input) | **finding, unscheduled** |
| 4 | **G-2/G-5** — collection discipline (hold out before merge; two classes have no real rows) | Owner | Data Maturation proposal | **finding, unscheduled** |
| 5 | **G-8** — `DifficultyLabelLogs` has no consumer; relates to the deferred M8-A gate work | Owner | Roadmap deferred list (§A.4 neighbourhood) | **deferred, knowledge only** |
| 6 | **G-9** — datasheet for `collected_v4`; dataset versioning (`DAT-03` names it explicitly); `Source` column is not provenance | Owner | Data Maturation proposal | **finding, unscheduled** |
| 7 | **G-12** — do **not** delete `normalized_dataset_m8a_balanced.csv`; it is the lineage intermediate | Owner | Repo hygiene — a *negative* instruction | **recorded to prevent an action** |
| 8 | 100-vs-101 synthetic-row discrepancy between CHANGELOG and the committed file | Owner | CHANGELOG amendment, if ever resolved | **unresolved, recorded** |

Nothing in this table is committed work. Each row is a finding awaiting an owner decision.

---

## 8. Decisions made

### D-1 — Audited repository bytes as the authority; treated S0 and CHANGELOG as claims to check

**Why it had to be made.** The brief said to treat the S0 report as experimental evidence rather
than as the description of the dataset, and the project's own history contains a case
(`ab5112c` → the 96.2% figure) where a number outlived the conditions that produced it.

**What it's for.** Every §1–§4 number is recomputed from committed bytes, and each is marked
`[measured]` / `[recorded]` / `[inferred]`. Where a document and the bytes disagree — the
100-vs-101 synthetic rows — the disagreement is reported rather than reconciled toward either side.

**Experience for future development.** It paid twice. Re-hashing the seed against `SPLIT.md`'s pin
turned "are the S0 numbers still valid?" from an assumption into a one-line fact. And re-running
the leakage check under stricter normalisation than `SPLIT.md` certified confirmed a clean split
instead of inheriting the claim — cheap, and the alternative was building on someone else's
`[recorded]`.

### D-2 — Verified the 402-row drop by set difference rather than accepting matching counts

**Why it had to be made.** `NhacNho` (217) + `OnTap` (185) = 402 exactly, which is suggestive but
is not proof that *those* rows are the dropped ones.

**What it's for.** The lineage claim "the seed is `m8a_uniform` minus `NhacNho`/`OnTap`" is load-
bearing: it is what establishes that two classes exist in the corpus but not in the production
label space. An arithmetic coincidence would have been an invisible error in the lineage diagram.

**Experience for future development.** Matching totals are the classic false positive in data
lineage. When a count reconciles, compute the set — it is usually a two-line change to the script
already open, and it converts an `[inferred]` into a `[measured]`.

### D-3 — Split the 51.8% raw label disagreement into "taxonomy retired the label" and "row moved anyway"

**Why it had to be made.** The raw figure — 533 of 1028 rows relabelled — overstates annotator
instability, because 325 of those rows had no choice: `Khac` and `DuAn` were removed from the label
space, so every row holding them *had* to move. Reporting 51.8% as an annotation-quality number
would have been an inflated claim, and G-1 is the audit's top-ranked gap.

**What it's for.** The honest figure is 208/703 = 29.6% — disagreement among rows where the
original label was still available. That is the number a proposal should plan against.

**Experience for future development.** When two label passes are compared, always partition by
whether the source label survived the target taxonomy. A schema change and an annotator
disagreement look identical in a naive diff and mean completely different things: one is a decision
to re-record, the other is noise to price.

### D-4 — Ranked gaps by fix class, and declined to treat corpus size as the finding

**Why it had to be made.** The brief explicitly forbade assuming synthetic data is the solution.
An audit that concludes "we need more data" is unactionable, and here it would also be wrong:
the two top-ranked gaps (G-1 label instability, G-2 no clean eval set) are not size problems, and
generation against unstable label definitions makes G-1 worse.

**What it's for.** The ranking's **Fix class** column groups gaps by intervention — adjudication,
collection, instrumentation, plumbing, housekeeping — so the later proposal can be organised around
what the work *is*, not around which file the symptom appeared in.

**Experience for future development.** "Rank by severity" and "rank by what fixes it" produce
different orderings, and the second is the one a proposal can consume. G-6 (a model trained on RNG
labels) is more severe than G-4, but ranks below it because rows accrue by themselves once the app
is used, while vocabulary coverage needs deliberate collection. The same column also earned a
*negative* entry — G-12 exists to stop a file being deleted — which a severity ranking has no
place to put.

### D-5 — Traced every arrow in the lineage diagram, after one of them turned out to be wrong

**Why it had to be made.** The first draft of §2 drew `m8a → uniform` with `_balanced.csv` as a
side branch, and labelled `_balanced.csv` a dead artifact recommended for deletion. Both were
inferences from file names and rough overlap, presented inside a diagram that read as measured.
A review pass asked why `uniform ∩ m8a` was only 810 of 1100 — a number visible in the audit's own
first overlap matrix, and never followed up.

**What it's for.** Subset-testing each link reversed two claims and produced the report's
third-ranked gap. `_balanced.csv` is a strict subset of `m8a` and is the intermediate every
m8a-derived seed row passes through — deleting it as "dead" would have severed the only link
between the shipped seed and its root. And the 290 rows unaccounted for resolved into 101 synthetic
plus **189 with no origin at all**, 136 of which are in the shipped model.

**Experience for future development.** A lineage diagram is an *assertion set*, and each arrow is a
separate claim needing separate evidence — the format makes inference look like measurement more
effectively than prose does. The specific tell was an overlap percentage that did not reach 100%
in either direction: when a derived file shares only 74% of its rows with its supposed parent, the
missing quarter is the finding, not a rounding detail. It had been sitting in the first table this
audit produced.

### D-6 — Read the untracked dev databases, and bounded what they may be used to claim

**Why it had to be made.** The brief asked about existing telemetry. The repository holds none —
the app database is untracked by design. Stopping there would have reported "no telemetry exists",
which is true of the repo and false of the system: the writers are wired and firing.

**What it's for.** Opening the two `bin/` databases read-only distinguishes *"instrumentation was
never built"* from *"instrumentation works and has produced 0–17 rows on one machine"*. Those imply
completely different Phase-1 work. Each figure is explicitly scoped as an existence proof about one
machine, never a population rate — the 12/17 override figure in particular.

**Experience for future development.** Untracked local state can answer questions the repository
cannot, and reading it is safe when the connection is read-only and the scope travels with the
number. The discipline that makes it safe is writing the bound into the same sentence as the
figure, not into a caveats section further down.
