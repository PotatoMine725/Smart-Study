# Data Maturation — Phase 0: data audit and gap map

**Date:** 2026-08-25
**Author/agent:** Claude (Opus 5) via Claude Code, acting as independent ML/data auditor
**Branch at time of audit:** `docs/encoder-knowledge-consolidation`
**Mandate:** `DAT-03` (SHOULD, separate ongoing workstream) in
[`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) §9

---

## Scope

Audit only. This report inventories the data the repository actually holds, reconstructs how it
came to be, maps class and linguistic coverage, states which numbers rest on which rows, and ranks
the gaps by *what would fix them*.

**It contains no proposal, no synthetic-data recommendation, no architecture change and no
implementation plan.** Whether synthetic generation is part of the answer is deliberately left
open — §H ranks gaps by the kind of intervention they need, and generation is only one of several.

**Not covered:** LAN-sync data model (`docs/knowledge/sync-data-model.md`), model-artifact storage
mechanics, and the ONNX encoder artifacts under `tools/ml-pilot/models/` (weights, not training
data — they are inputs to a stopped pilot).

### Evidence convention

Every substantive statement carries one of five tags. **An inference is never written as a fact.**

| Tag | Meaning |
|---|---|
| `[fact]` | Directly verified in the repository — a file's bytes, a line of code, a commit message |
| `[measured]` | Produced by analysis in this session over repository bytes |
| `[inferred]` | Reasoned from evidence; the reasoning is stated so it can be attacked |
| `[rec]` | A suggested future direction, not a decision |
| `[unknown]` | Insufficient evidence — recorded rather than guessed |

---

## ⚠ Correction notice — what the second pass reversed

This report was first committed (`53d9f17`) covering inventory and lineage. A second pass, adding
class and linguistic coverage, **overturned two claims the first pass made and one premise the rest
of the repository rests on.** They are corrected in place below; they are called out here because
anyone who read the first version needs to know.

| # | First pass said | Second pass measured | Where |
|---|---|---|---|
| 1 | `collected_v4.csv` is "**real** — the only real user-authored text in the repo" | It has none of the distributional structure of free-form input. Seven independent regularities, plus an exact quota match, say it was **authored to a spec** | §E.6 |
| 2 | The 136 untraceable rows are "short and terse **like real student input** — if any are real…" | They carry generator fingerprints: **100.0%** all-lowercase and four *exact* zeros on phenomena present everywhere else | §E.5 |
| 3 | "The training distribution is not the input distribution" | The comparison was train-vs-test. Both are authored. The **input** distribution remains unobserved | §D.4 |

**The consequence.** The repository does not hold 205 real rows. It holds **zero verified real user
rows**, and every accuracy figure the project has produced — 96.2%, 97.24%, and each S0 arm — is
authored data measured against authored data. This makes the first pass's bottom line *stronger*,
not weaker: the constraint was never corpus size.

---

## A. Current Data Inventory

Twenty-one data sources bear on ML or parser training/evaluation. They fall into four groups; only
group A ever reaches a trained model.

### Group A — Corpora that train or evaluate a model

| # | Path | Rows | Real / synth / derived | Task + labels | Trained on? | Evaluated on? | Overlap |
|---|---|---|---|---|---|---|---|
| A1 | `SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` | **903** `[measured]` | **authored throughout**: 461 derived + 101 synthetic + 136 untraceable + 205 `collected_v4` (§E.6) | intent → `TaskType` (5 cls) | **Yes — production, all 903, no split** | Yes (contaminated, §F.1) | superset of A2, A3, A4, A9, A10 |
| A2 | `datasheets/collected_v4.csv` | **205** `[measured]` | **labelled "collected"; structurally authored** — §E.6 | `TaskType` (3 cls), `Difficulty`, `DeadlineHint` | Yes, via A1 | Yes — is the S0 test set | 100% inside A1 and A10 |
| A3 | `datasheets/synthetic_v3_giuaky_doan.csv` | 101 `[measured]` | synthetic, hand-authored | `TaskType` (5 cls) + 4 aux cols | Yes, via A1 | no | 100% inside A1, A4, A9 |
| A4 | `datasheets/normalized_dataset_m8a_uniform.csv` | 1100 `[measured]` | derived 810 + synth 101 + **189 of unknown origin** (§E.5) | `TaskType` (7 cls) + aux | 698 of 1100 via A1 | no | 810 via A7; 402 rows never used |
| A5 | `datasheets/normalized_dataset.csv` | 1365 (**1028 unique**) `[measured]` | derived; **root of the in-repo lineage** | `LoaiTask` (7 cls, old schema) | Not directly | no | same 1028 inputs as A6 |
| A6 | `datasheets/normalized_dataset_m8a.csv` | 1365 (**1028 unique**) `[measured]` | relabelled copy of A5 | `TaskType` (5 cls) + aux | Not directly | no | §E.1 — 533/1028 labels differ from A5 |
| A7 | `datasheets/normalized_dataset_m8a_balanced.csv` | 820 `[measured]` | derived subset — **the lineage intermediate, not a side branch** (§B) | `TaskType` (5 cls) | Indirectly: 810 of its rows reach A1 | no | strict subset of A6; 810 of 820 inside A4 |
| A8 | `SeedDataGenerator.Generate()` (code, not a file) | **180** `[fact — read from source, not executed]` | **procedurally generated, labels are RNG draws** | study-time regression → minutes | **Yes — M7 predictor, every run in practice (§E.3)** | 20% internal split | none |
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

> **B1 is a probe set, not corpus coverage.** Its 39 rows are hand-built to *exercise* the
> phenomena in §D. They are never counted as coverage anywhere in this report, and 2 of its 8
> abbreviations (`dacn`, `ttcs`) appear in **neither** split `[measured]`.

### Group C — Telemetry: real signal, captured, mostly unconsumed

| # | Table / writer | Rows observed `[measured]` | Consumer | Status |
|---|---|---|---|---|
| C1 | `StudyTimeOutcomeLogs` ← `FocusViewModel.LuuThoiGianThucTe` | Debug DB **2**, Release DB **0** | `StudyTimeTrainingDataSource` (gate `MinRows = 50`) | **Gate never met → predictor always falls back to A8** |
| C2 | `DifficultyLabelLogs` ← `QuanLyTaskViewModel:333` | Debug **3**, Release **17** (12 overrides) | **none** — written, never read | Real human difficulty labels with no consumer |
| C3 | `WeightChangeLogs` + `OutcomeMaturationService` (14-day cohort fill) | Debug 1, Release 0 | rule engine, not a model | shipped 2026-06-11 |
| C4 | `OptimizerRunLogs` | 0 | — | **empty is correct**: G3-1 optimizer wiring unscheduled |
| C5 | `StudyLogs` (Debug DB) | 5404 | analytics UI | **generated demo data, not real usage** — §E.7 |
| C6 | `UserStatsSnapshot` | derived aggregate | `WeightRuleEngine` | rule input, no training data |

> The application database is `SmartStudyData.db` beside the executable (`AppDbContext.cs:51`) and
> is **untracked** — `git ls-files` returns no `.db` path `[measured]`. **No production telemetry
> exists in the repository at all**; the counts above come from two local dev databases in `bin/`
> (Debug, mtime 2026-07-26; Release, mtime 2026-08-19) and describe this machine, not any user
> population.

### Group D — Hand-authored rule data (data living as code)

| # | Location | Size |
|---|---|---|
| D1 | `Services/Strategies/IDeadlineKeywordParser.cs` | 33 string literals `[measured — upper bound on lexicon size, not a coverage measure]` |
| D2 | `Services/Strategies/IDifficultyKeywordParser.cs` | 30 `[same bound]` |
| D3 | `Services/Strategies/ITaskTypeKeywordParser.cs` | 15 `[same bound]` |

These are the heuristic parser's lexicon. They are versioned with the code, have no held-out set,
and are the fallback the whole offline-first contract rests on.

---

## B. Data Lineage

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
   │   ⚠ 533/1028 rows carry a DIFFERENT TaskType than the root [measured] — §E.1
   │
   ├─► [balance pass + DEDUP: 1365 → 820; the 24.7% duplicate inflation is removed here]
   ▼
normalized_dataset_m8a_balanced.csv  (820 — a STRICT SUBSET of m8a [measured])
   │   Not a dead branch: it is the intermediate every m8a-derived seed row passes through.
   │   810 of its 820 rows continue; 10 are dropped [measured].
   │
   ├──────────────┬───────────────────────────┐
   │ 810 derived  │ + synthetic_v3 (101)      │ + 189 rows of UNKNOWN ORIGIN [measured]
   │              │                           │   present in NO other committed file — §E.5
   ▼              ▼                           ▼
normalized_dataset_m8a_uniform.csv  (810 + 101 + 189 = 1100 — set identity verified [measured])
   │   v3 relabel in place (commit 9603c17): over the 810 inputs shared with m8a,
   │   121 rows relabelled [measured] — BaiTap→DoAnCuoiKy 93, ThiCuoiKy→ThiGiuaKy 23, +5 others.
   │   CHANGELOG records the intent as "31 giữa kỳ + 96 đồ án rows" [fact];
   │   96 reconciles exactly, 25-of-31 are visible in the shared window [measured].
   │   ⚠ CHANGELOG says "Added 100 synthetic rows (1000 → 1100)" [fact] but the file holds 101
   │     and all 101 appear in uniform [measured]. Off-by-one unreconcilable from the artifacts.
   │
   ├─► [PROJECTION ONTO THE PRODUCTION ENUM]
   │      drop NhacNho (217) + OnTap (185) = 402 rows  ── verified by set difference [measured]
   │      rename BaiTap → BaiTapVeNha
   ▼
seed_intents.csv  v3  (698 rows, 5 classes, LabelVersion=v3)
   │   ── "held-out 96.2%" measured HERE, stratified 85/15, n=106 [fact, CHANGELOG:212].
   │      That figure predates v4 and is an in-distribution number over authored rows.
   │
   ├─► + collected_v4.csv (205 rows) via datasheets/_merge_seed.py
   │      ⚠ AUTHORED TO A QUOTA, NOT COLLECTED — §E.6. The retrain plan §8.3 asked for
   │        net-new +95 / +56 / +50; the file delivered 99 / 56 / 50, two of them EXACT
   │        [measured], with ZERO rows lost to dedup despite §8.3 warning to "collect a bit
   │        extra to absorb dedup loss" [fact]. Plan §8.6 supplied the label definitions to
   │        author against; plan §8.4 supplied the Source string to type.
   │      dedup key = lowercase + trim + collapse whitespace — no diacritic folding
   │      merge was purely additive: 205 insertions, 0 deletions (commit ab5112c) [measured]
   ▼
seed_intents.csv  v4  (903 rows, LabelVersion v3+v4)
   │   SHA-256 86abb454… — matches the pin in tools/ml-pilot/split/SPLIT.md ✓ [measured]
   │
   ├─────────────────────► PRODUCTION: TextClassifierModelManager trains on ALL 903, no split
   │                        (embedded resource; SeedHash change auto-triggers retrain)
   │
   └─► build_split.py (pure filter on Source column, no shuffle, no seed)
          ├─ train.csv 698  ("synthetic")  ─┐
          └─ test.csv  205  ("real")       ─┴─► S0 arms. BOTH SIDES ARE AUTHORED — §G
```

**Separately, and never joined to the above:**

```text
FocusViewModel session end ──► StudyTimeOutcomeLogs ──[gate: ≥50 rows]──► StudyTimePredictor
                                   (0–2 rows observed)        │
                                                        gate fails ▼
                                              SeedDataGenerator.Generate() — 180 rows, RNG labels

QuanLyTaskViewModel save   ──► DifficultyLabelLogs ──────────► (nothing)
```

### B.1 Where provenance becomes unclear — the three breaks

| Break | What is unknown | Reach |
|---|---|---|
| **Root** — `normalized_dataset.csv` enters in bulk commit `b29cd24` | Who wrote or collected 1028 inputs, from what population, when `[unknown]` | Everything downstream. Nothing upstream of this file exists in the repo `[measured]` |
| **Mid-chain** — 189 rows appear between `_balanced.csv` and `uniform` | What produced them. They are in no other committed file `[measured]` | 136 reach production (§E.5) |
| **`collected_v4`** — the word "collected" has no record behind it | Whether a human authored the surface text, and if so under what instruction `[unknown]` | The premise 8 documents rest on (§F.3) |

**The `Source` column is not provenance.** It records the first file a row was seen in. All 136
untraceable rows carry `Source = m8a_uniform` `[measured]`; all 205 v4 rows carry
`Source = collected_v4`, which is the literal string the retrain plan §8.4 told the author to
type `[fact]`.

---

## C. Class Coverage Map

### C.1 Per class, by provenance

Training half (698) decomposed by verified set identity; evaluation half (205) is all
`collected_v4` `[measured]`.

| Class | train | derived | `synthetic_v3` | untraceable | untraceable % | eval (v4) | **verified real** |
|---|---|---|---|---|---|---|---|
| `BaiTapVeNha` | 124 | 123 | 1 | 0 | 0.0% | 56 | **0** |
| `DoAnCuoiKy` | 131 | 96 | 35 | 0 | 0.0% | 50 | **0** |
| `KiemTraThuongXuyen` | 188 | 115 | 2 | **71** | **37.8%** | 0 | **0** |
| `ThiCuoiKy` | 170 | 102 | 8 | **60** | **35.3%** | 0 | **0** |
| `ThiGiuaKy` | 85 | 25 | 55 | 5 | 5.9% | 99 | **0** |
| **total** | **698** | **461** | **101** | **136** | **19.5%** | **205** | **0** |

Three things this table says that no previous document does:

1. **The "verified real" column is zero for every class.** Not thin — zero. §E.6.
2. **`ThiGiuaKy` is the weakest class in training and the largest in evaluation.** 85 training rows
   (12.2%, the smallest) against 99 evaluation rows (48.3%, the largest), and **65% of its training
   rows are `synthetic_v3` templates** `[measured]`. The class the evaluation weights most heavily
   is the one whose training data is most templated.
3. **Untraceable rows are not spread evenly.** They are 37.8% and 35.3% of exactly the two classes
   that have **no evaluation rows at all**. The two classes nobody can measure are the two classes
   most made of rows nobody can attribute.

### C.2 The four distributions the brief asks to distinguish

| Distribution | Status | Evidence |
|---|---|---|
| **True real-world** — what students actually type | **`[unknown]`** | No production telemetry (§A group C), no verified real corpus (§E.6). Nothing in the repository observes it |
| **Observed project** — what the corpus contains | Measurable, but it observes *authoring behaviour*, not usage `[measured]` | §C.1, §D |
| **Synthetic balancing** — deliberate rebalancing | `[fact]`: the `_balanced` → `uniform` passes, then the §8.3 quota (+95/+56/+50) that produced v4. CHANGELOG records imbalance 2.21× → **1.11×** | §B |
| **Evaluation balancing** — what the test set weights | `[measured]`: 3 of 5 classes, and `ThiGiuaKy` at 48.3%. Not a sample of anything — a filter on a quota | `SPLIT.md`, §C.1 |

**Balance is not assumed to be desirable here.** The corpus was balanced to 1.11× against a real
distribution that has never been observed `[measured + unknown]`. `[inferred]` If the real
distribution of student task-types is skewed — and there is no reason to assume it is uniform —
then the balancing has moved the training prior *away* from production, not toward it. This cannot
be settled without C1-class telemetry.

### C.3 Missing-class taxonomy

| Class | Status against the brief's four categories |
|---|---|
| `KiemTraThuongXuyen` | **Present only synthetically**, and 37.8% of that is untraceable. Zero evaluation rows |
| `ThiCuoiKy` | **Present only synthetically**, 35.3% untraceable. Zero evaluation rows |
| `ThiGiuaKy` | Present only synthetically; **poorly represented in training** (85 rows, 65% templated) while dominating evaluation |
| `BaiTapVeNha` | Present only synthetically; cleanest lineage (123/124 derived) |
| `DoAnCuoiKy` | Present only synthetically; 27% `synthetic_v3` |

`[fact]` The archived retrain plan already established that `ThiGiuaKy` **cannot be augmented from
any datasheet in the repo** — after dedup, zero additive rows exist for it. That is why `v4` was
requested, and it is why `v4`'s authenticity matters more than any other file's.

---

## D. Linguistic Phenomenon Coverage Map

### D.1 Read this first — what these numbers can and cannot say

Every rate below compares **`train.csv` (698)** against **`test.csv` (205)**. Before §E.6, that
read as *synthetic vs real*. It is not. Both sides are authored, so **every row of this table is a
comparison between two authoring processes**, and none of it measures how students actually write
`[measured + inferred]`.

The table is still worth having, for three reasons: it shows what the shipped model was trained to
expect, it shows what the evaluation is able to test, and the *divergence* between two authoring
processes bounds how stable any of these figures are.

**No sentence in this section may be read as "students type X% of the time." That number is
`[unknown]` and is `G-2`.**

### D.2 Measured phenomena

Detection is by closed lexicon or codepoint class; each is a **lower bound on a named list**, not
an estimate of the phenomenon in general. Lexicons are in the analysis script, not inferred.

| Phenomenon | Detector | train 698 | test 205 | Δ | Coverage reading |
|---|---|---|---|---|---|
| **Task abbreviations** (`tgk`, `bt`, `btvn`, `ktra`…) | closed list | 8.2% | **42.0%** | **+33.8pp** | The largest divergence. §G |
| **Course-code abbreviations** (`xstk`, `csdl`, `vxl`…) | closed list | 1.3% | **20.5%** | **+19.2pp** | Training barely contains them |
| **Numeric dates** (`25/10`, `23h59`) | regex | 2.3% | **31.2%** | **+28.9pp** | See §E.6 — this is a generator sweep, not a habit |
| **Weekday shorthand** (`t2`–`t7`, `cn`) | regex | 8.7% | 26.3% | +17.6pp | |
| **Stripped diacritics** (Vietnamese written bare) | ≥2 stripped VN function words | 3.0% | 12.7% | +9.7pp | `AC-30`'s phenomenon; thin on both sides |
| **Run-together tokens** (`thigiuaky`) | ≥8 ASCII alpha, non-English | 2.3% | 7.8% | +5.5pp | Thin on both sides |
| **Mixed register** (marked + bare VN in one row) | per-token | 57.6% | 53.7% | −3.9pp | The one phenomenon both processes produce alike |
| **Emoji** | codepoint class | **23.1%** | **0.0%** | **−23.1pp** | **Trained on, untestable.** §D.3 |
| **Emoticons** (`:))`, `T_T`) | regex | 4.6% | **0.0%** | −4.6pp | Same |
| **English loanwords** (closed list) | closed list | 45.1% | 21.0% | −24.2pp | |
| **Teencode** (closed list: `ko`, `dc`, `r`, `vl`…) | closed list | 27.2% | 9.8% | −17.5pp | Training is *more* slangy than evaluation |
| **Sentence-initial capital** | first char | 33.5% | **0.0%** | −33.5pp | §D.3 |
| **Terminal `.!?`** | last char | 31.7% | **0.0%** | −31.7pp | §D.3 |
| **All-lowercase** | whole row | 42.1% | 74.6% | +32.5pp | |
| Length (chars), p25–p75 | — | 33–70 | 29–36 | — | Evaluation IQR is 7 chars wide |

### D.3 The three exact zeros, and what they mean

`collected_v4.csv` contains **0 emoji, 0 sentence-initial capitals, and 0 terminal `.!?`** — out of
205 rows, verified at byte level (the file contains no 4-byte UTF-8 sequence at all) `[measured]`.

This is not a low rate. It is an absence, on three independent features at once. It is the first
evidence in §E.6, and it has one immediate consequence regardless of what produced the file:

> **The shipped classifier trains on 23.1% emoji-bearing rows and is evaluated on 0%.** Whatever
> the model learned to associate with emoji is **not measured by any evaluation the project has
> run** `[measured]`. `TextClassifierService.Predict` passes raw user input straight to the model
> with only a whitespace guard `[fact]`, so production input can contain emoji freely.

`[inferred]` Casing is likely neutralised inside the pipeline — ML.NET `FeaturizeText` lowercases by
default — so the capitalisation gap probably does not reach the model. **This was not verified**;
verifying it means inspecting the fitted transformer, which is a one-hour check and is listed in §K.
Emoji and punctuation are not neutralised by that default.

### D.4 Phenomena that cannot be detected — recorded as unknown, not estimated

The brief asks about typos, slang, informal phrasing, alternate word order, and ambiguous wording.
**No heuristic available here measures these.** A regex proxy would produce numbers that look like
measurements and would be built on by the next session, which is worse than an honest gap.

| Phenomenon | Status | What is actually known |
|---|---|---|
| **Typos** | `[unknown]` | Distinguishing a typo from teencode from an abbreviation requires a Vietnamese lexicon + normalisation reference the repo does not have. ViLexNorm (§I) is exactly this instrument |
| **Slang** | **lower bound only** | 27.2% train / 9.8% test on a *named* 30-token list. The true rate is `[unknown]` and is certainly higher |
| **Informal phrasing** | `[unknown]` | Not separable from register without annotation |
| **Alternate word order** | `[unknown]` | Requires parsing. Not attempted |
| **Ambiguous wording** | `[unknown]`, and it is `G-1` | Ambiguity is what the 29.6% annotator disagreement *is* (§E.1). It is measurable only by re-annotation, not by inspection |

**Sampled instead of measured.** 30 of the 205 evaluation rows were drawn deterministically
(`Random(42).sample`) and read `[measured, n=30]`. The reading found no typos, no ambiguity, and no
word-order variation — and instead found the template structure that became §E.6. That is a finding
about the corpus, **not** an estimate of how often students make typos.

---

## E. Data Quality Findings

Confirmed defect / possible concern / unknown are marked per item.

### E.1 Annotation instability: the same text, two label passes, 29.6% disagreement — **confirmed defect**

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
inside a shared label space. **No annotation guideline, adjudication record, or annotator identity
exists anywhere in the repository for either pass** `[measured — exhaustive grep]`. The archived
retrain plan reached the same conclusion qualitatively — *"the datasheets are noisier than the
seed… bulk-merging injects contradictory training signal"* `[fact]` — and declined to merge them.
This audit puts a number on it.

**This is the ceiling nobody has priced.** Whatever accuracy a future model reports, it is graded
against labels of this stability.

Within-file the corpora are clean: 0 conflicting labels across all 168 duplicate-input groups in
both files `[measured]`. The instability is strictly *between* passes.

### E.2 The intent classifier uses one column of a seven-column corpus — **confirmed defect (latent capability)**

`TextClassifierModelManager.TrainAndSaveAsync` builds
`MapValueToKey("Label","TaskType") → FeaturizeText("Features","InputText") → SdcaMaximumEntropy`
`[fact, TextClassifierModelManager.cs:143-152]`.

`InputText` is the only feature; `TaskType` the only label. **`Difficulty`, `DeadlineHint`,
`TaskName`, `Source` and `LabelVersion` are parsed, carried, validated — and never trained on.**
The archived retrain plan states the same intent explicitly: *"Difficulty skew is out of scope
(column ignored by the pipeline)"* `[fact]`.

Consequence: the repository holds **903 difficulty labels and 903 task-name labels that no model
has ever seen**, and `TaskName` has 477 distinct values over 903 rows `[measured]`.

### E.3 The study-time predictor trains on noise — **confirmed defect**

`StudyTimeTrainingDataSource.MinRows = 50`. Below that it returns empty and
`AnalyticsViewModel.RetrainModel` substitutes `SeedDataGenerator.Generate()` `[fact]`.
Observed `StudyTimeOutcomeLogs`: 2 rows and 0 rows `[measured]`.

What that fallback contains: three groups of 60 rows, each a single fixed
(difficulty, credits, daysLeft) point, with `Label = uniform(min,max) × (1 ± 0.15)` — **the label
is a random draw** `[fact, SeedDataGenerator.cs]`. There are **3 distinct feature vectors**. Within
a group the target carries no information; between groups it is a step function.

`[inferred]` The R² ≥ 0.45 persistence gate is therefore satisfied by between-group separation
(20–60 / 60–120 / 120–240 minutes) while the model learns nothing within a group. The gate is
passable by a three-value lookup table. This is an inference from the data construction and the
gate value; it was **not** measured by running the trainer.

### E.4 The telemetry that exists cannot measure its own model — **confirmed defect, and it worsens daily**

`FocusViewModel` writes the outcome row with `PredictedMinutes = null` and `Confidence = null`
`[fact, FocusViewModel.cs:151-153]`, while capturing `WasMlPrediction`.

So the log records what happened but not what was predicted. **Prediction error and calibration are
not computable from telemetry, no matter how many rows accumulate.** Fixing this after the fact is
impossible; only rows written after an instrumentation change can carry it.

### E.5 15% of the shipped corpus has no traceable origin — **confirmed defect**

`normalized_dataset_m8a_uniform.csv` holds 1100 rows. Its composition, verified by set identity
`[measured]`:

| Component | Rows | Origin |
|---|---|---|
| carried from `_balanced.csv` (itself a strict subset of `m8a`) | 810 | traceable to `normalized_dataset.csv` |
| `synthetic_v3_giuaky_doan.csv` | 101 | traceable, hand-authored |
| **untraceable** | **189** | **present in no other committed file** |

Those 189 appear in no other datasheet: not in `m8a`, not in `normalized_dataset.csv`, not in
`_balanced.csv`, not in `synthetic_v3`, and not in `collected_v4` `[measured]`. **136 are in the
shipped seed** — **15.1% of the 903-row production corpus, 19.5% of the 698-row training split** —
concentrated at 37.8% of `KiemTraThuongXuyen` and 35.3% of `ThiCuoiKy` (§C.1).

**What they are.** The first pass called them "short and terse like real student input" and left
open that some might be real. Stratifying the linguistic profile settles it `[measured]`:

| Signal | untraceable (136) | derived (461) | `synthetic_v3` (101) | `collected_v4` (205) |
|---|---|---|---|---|
| all-lowercase | **100.0%** | 22.3% | 54.5% | 74.6% |
| numeric dates | **0.0%** | 3.0% | 2.0% | 31.2% |
| stripped diacritics | **0.0%** | 2.4% | 9.9% | 12.7% |
| run-together tokens | **0.0%** | 2.0% | 6.9% | 7.8% |
| course abbreviations | **0.0%** | 0.4% | 6.9% | 20.5% |
| emoji | 63.2% | 11.3% | 22.8% | 0.0% |
| length p25–p75 | 19–28 | 42–73 | 50–69 | 29–36 |

`[inferred]` **These rows were machine-produced.** The load-bearing evidence is the **100.0%**
all-lowercase rate and the **four exact zeros** — a natural population does not produce a style
feature at exactly 100% or four separate phenomena at exactly 0%, whatever baseline it is compared
against. The emoji contrast is supporting only, since §E.6 removes `collected_v4` as a real-world
baseline.

Supporting, and weaker: six rows share the corrupted token `ô n` (a spurious space inside `ôn`),
with varied pronoun prefixes and emoji suffixes — `ô n thi đợt này`, `tao ô n thi đợt này kk 🤯`,
`em ô n exam đợt này`, `team mình ô n thi đợt này` `[measured]`. This proves machine *processing*
touched them; it does not by itself prove machine *authorship*, since a diacritic-restoration pass
over human text would leave the same mark.

`[unknown]` **What process, and when.** Commit `b29cd24` introduced all four `normalized_dataset*`
files at once and does not distinguish them. The step is not in the repository. §K item 1.

### E.6 `collected_v4.csv` is not collected data — **confirmed defect, and the report's central finding**

Every document in the repository that mentions this file calls it real (§F.3). **Nothing in the
repository records the act of collecting it.** The only document describing its creation is the
*request*: the archived retrain plan §8.7, *"Drop the collected file anywhere"* `[fact]`.

The bytes disagree with the label. Seven measured regularities, each individually odd, jointly
inconsistent with an organic collection of 205 student messages:

| # | Measurement | `collected_v4` | derived | `synthetic_v3` | untraceable |
|---|---|---|---|---|---|
| 1 | rows ending `, <≤25-char tail>` | **97.6%** | 18.0% | 21.8% | 0.0% |
| 2 | rows beginning with a time expression | **64.4%** | 5.2% | 7.9% | 5.1% |
| 3 | length p25–p75 / max | **29–36 / 52** | 42–73 / 111 | 50–69 / 85 | 19–28 / 43 |
| 4 | emoji / initial capital / terminal `.!?` | **0 / 0 / 0** | — | — | — |
| 5 | distinct `TaskName` per row | **202 / 205** | — | — | 276 / 698 (train) |
| 6 | `TaskType` order in file | **perfectly blocked**: 99 `ThiGiuaKy`, then 56 `BaiTapVeNha`, then 50 `DoAnCuoiKy` | — | — | — |
| 7 | `dd/mm` dates | **a contiguous sweep, cycled** — see below | — | — | — |

**On (7).** The 63 date occurrences cover 28 distinct `dd/mm` values at 2–3 occurrences each, and
they are a shuffled permutation of a contiguous range, re-drawn in cycles: October **20–31**
(midterms and homework), December **15–30** (final projects) `[measured]`. Months partition
*perfectly* by class — `ThiGiuaKy` and `BaiTapVeNha` draw only from month 10, `DoAnCuoiKy` only from
month 12, with **zero** cross-contamination `[measured]`.

**And the decisive external evidence — the quota.** The archived retrain plan §8.3 specified
net-new rows per class. The file delivered `[measured vs fact]`:

| Class | plan §8.3 asked | file delivered | |
|---|---|---|---|
| `ThiGiuaKy` | +95 (min +65) | 99 | +4 |
| `BaiTapVeNha` | +56 | **56** | **exact** |
| `DoAnCuoiKy` | +50 | **50** | **exact** |
| dedup loss | plan: *"Collect a bit extra to absorb dedup loss"* | **0 rows lost** | 698 + 205 = 903 |

Two classes hit the requested number **exactly**, and nothing was lost to a dedup step the plan
explicitly warned to over-collect against.

`[inferred]` **`collected_v4.csv` was authored to fill the retrain plan's quota table.** The plan
supplied the quota (§8.3), the label definitions to author against (§8.6), the column format, and
the literal `Source` string to type (§8.4). The file matches all four.

**What the evidence excludes.** Two innocent explanations were considered and do not survive:

- *"Students were asked to write in a template"* — does not explain a date sweep over consecutive
  calendar days, nor 202 distinct courses among 205 rows. A cohort does not span 202 courses with
  deadlines on every consecutive day.
- *"It was collected, then normalised on import"* — does not explain the per-class month partition
  or the exact quota match. Normalisation strips characters; it does not assign dates or hit a
  target row count.

**What remains genuinely `[unknown]`:** whether a human wrote the surface strings on top of that
scaffold, or a model generated them. The repository cannot answer it, and the distinction barely
matters for evaluation — either way the file is not a sample of student behaviour. **The fastest
resolution is owner recall, not forensics** (§K item 2).

### E.7 What is *not* wrong — checks that came back clean

Stated so the next session does not re-spend effort:

- **The S0 split is sound.** `seed_intents.csv` hashes to `86abb454…`, matching `SPLIT.md`'s pin
  exactly `[measured]` — the split is not stale and every S0 number still corresponds to it.
- **No train/test leakage, including near-duplicates.** Exact, whitespace-normalised, and full
  diacritic-folded + punctuation-stripped comparisons all return **0** overlap between `train.csv`
  and `test.csv` `[measured]` — stricter than `SPLIT.md` certified. *This remains true and remains
  worth little:* §F.2 explains why a clean split between two authored corpora is not evaluation
  hygiene.
- **No duplicates in anything shipped.** `seed_intents.csv`, `train.csv`, `test.csv`, `_balanced`,
  `uniform`, `synthetic_v3` and `collected_v4` are all 100% unique under lowercase + whitespace
  normalisation `[measured]`. The 24.7% duplicate inflation is confined to `normalized_dataset*.csv`
  (1365 rows / 1028 unique / 168 groups / max multiplicity 9) and is removed by the `_balanced`
  dedup step `[measured]`.
- **Duplicate rows never carry conflicting labels** (§E.1).
- **`OptimizerRunLogs` being empty is correct**, not a data gap (G3-1 wiring unscheduled).
- **`StudyLogs` = 5404 rows in the Debug DB is not real telemetry.** 302 distinct `MaTask` against
  14 rows in `StudyTasks`, `DaHoanThanh` true on 5400/5404, and duration values recurring exactly
  120 times each `[measured]` — `[inferred]` generated demo data for the analytics UI.

### E.8 Distribution artifacts and template fingerprints — summary

| Artifact | Where | Status |
|---|---|---|
| Date sweep over contiguous ranges, cycled | `collected_v4` | **confirmed** `[measured]` |
| Per-class month partition, zero overlap | `collected_v4` | **confirmed** `[measured]` |
| Comma-tail syntax on 97.6% of rows | `collected_v4` | **confirmed** `[measured]` |
| 100.0% all-lowercase; four exact zeros | untraceable 136 | **confirmed** `[measured]` |
| Corrupted token `ô n` replicated 6× | untraceable 136 | **confirmed** `[measured]`; implies processing, not necessarily authorship |
| Templated near-duplicates leaking to a bag-of-words featurizer | `synthetic_v3` | **confirmed** `[fact]` — the 2026-06-25 eval says so in its own limitations |
| "Suspiciously easy examples" | whole corpus | `[inferred]` — 97.24% micro-accuracy on data authored against the label definitions it is graded by (§8.6) is the expected result, not a model quality signal |

---

## F. Evaluation Hygiene

### F.1 Can the project answer "does a new model generalize to unseen real student input?"

**No.** Three independent blocks, any one of which is sufficient `[measured]`:

| Block | Detail |
|---|---|
| **There is no real input** | §E.6. Zero verified real rows exist in the repository |
| **The shipped model has consumed the entire corpus** | It trains on all 903 rows with no split and no accuracy gate — the source comment says so `[fact]`. All 205 v4 rows are inside those 903 |
| **The only held-out set is authored to the same spec as training** | `train.csv` and `test.csv` are both filters on one 903-row file whose two halves were authored against the same §8.6 label definitions |

S0 avoided the second block by retraining every arm from `train.csv` alone — a property of the
pilot harness, not of production `[fact]`.

### F.2 Audit against the brief's checklist

| Item | Finding |
|---|---|
| Train/eval separation | **Mechanically clean, semantically void.** 0 leakage under the strictest normalisation `[measured]`; but a clean split between two authored corpora measures generator-transfer, not generalization |
| Provenance | Broken at three points (§B.1) `[measured]` |
| Held-out **real** data | **None exists** `[measured]` |
| Leakage controls | `build_split.py` asserts counts and exits 2 on drift `[fact]` — good discipline, correctly implemented, on the wrong premise |
| Class coverage | 3 of 5 classes in evaluation; `ThiGiuaKy` at 48.3% `[measured]` |
| Phenomenon coverage | Emoji 23.1% train / 0.0% eval — trained on, untestable (§D.3) `[measured]` |
| Metrics dominated by synthetic data | **Entirely.** Every figure the project holds `[measured]` |
| Same source examples indirectly in both train and test | **Not by row** (0 overlap) — but **by construction**, yes: both halves descend from one authoring effort against one spec `[inferred]` |

### F.3 Documents that assert `collected_v4` is real

Listed, not corrected — correcting other documents is outside an audit's scope. Each is an owner
decision (§Owner Decisions, OD-1).

| Document | Assertion `[fact — quoted]` |
|---|---|
| `docs/specs/2026-08-24-neural-encoder-smart-parser.md:361` | `EVA-03`: *"the **205 held-out real `collected_v4` rows** **[fact]**"* — tagged `[fact]` in the spec's own convention |
| `tools/ml-pilot/split/SPLIT.md` | *"`Source = collected_v4` — real, held out, **excluded from training**"* |
| `docs/reports/2026-08-25-encoder-pilot.md:558` | *"real `collected_v4` input"* |
| `docs/plans/2026-08-24-edge-ai-encoder-adoption.md:140` | *"**real, collected**"* |
| `docs/knowledge/machine-learning.md:105` | *"this is real `collected_v4` input"* |
| `docs/specs/system_roadmap.md:180` | *"205 real held-out `collected_v4` rows"* |
| `datasheets/vn_input_fixtures.md:80` | *"drawn from `collected_v4.csv` — real collected student input"* |
| `tools/ml-pilot/build_fixtures.py:13` | *"real collected user"* |

`[inferred]` The word "real" propagated from the retrain plan's *request* into eight downstream
documents as an assumption, and was tagged `[fact]` at least once, without any document ever
recording the collection. **This is the governance failure `G-9` describes, in its most consequential
instance.**

---

## G. S0 Evidence Reassessment

The brief asks to verify the S0 unseen-token finding independently and to resist concluding from it
that data volume is the bottleneck.

### G.1 Did S0 compute what it claimed? — Yes

Re-derived with a word-boundary regex, **no tokenizer involved**, so the check does not inherit S0's
tokenization choices `[measured]`:

| Token | `vocab_gap.json` claim | This audit, independent | Verdict |
|---|---|---|---|
| `tgk` | train 0 / test 28 rows | **train 0 / test 28** | reproduces exactly |
| `bt` | 54 unseen occurrences | **train 0 / test 54 rows** | reproduces |
| `btvn` | train 1 / test 1 | train 1 / test 1 | reproduces |
| `xstk` | train 1 / test 4 | train 1 / test 4 | reproduces |
| `csdl`, `ktvm`, `ktct` | 4/4, 2/2, 1/2 | 4/4, 2/2, 1/2 | reproduces |
| `dacn`, `ttcs` | not reported | **0 / 0 — in neither split**, though both are `vn_input_fixtures` rows | new finding |

S0's arithmetic is sound and its headline numbers stand: 25.0% of test tokens unseen, **94.6% of
test rows contain ≥1 unseen token** `[fact, vocab_gap.json]`.

### G.2 Is the phenomenon concentrated? — Yes, in abbreviations

The gap is **not** a broad vocabulary mismatch. Of the most-common unseen tokens, `bt` (54) and
`tgk` (28) alone account for 82 of 401 unseen occurrences, and the tail is dominated by course codes
(`oop`, `attt`, `vxl`, `trr`, `qtm`, `tkht`, `cnxhkh`) and bare day-numbers (`23`–`29`, `t6`)
`[fact + measured]`. §D.2 shows the same thing distributionally: task abbreviations +33.8pp and
course abbreviations +19.2pp are the two largest divergences in the entire phenomenon table.

Stripped diacritics (+9.7pp) and run-together forms (+5.5pp) diverge far less — so the S0 finding is
**specific to abbreviations**, not a general "students write differently" effect.

### G.3 What the finding actually is — the reassessment

`[inferred]` **The vocabulary gap is a gap between two authoring processes, not evidence about real
student vocabulary.**

Both `train.csv` and `test.csv` are authored (§E.6). The author of `train.csv` did not write `tgk`;
the author of `test.csv` wrote it 28 times. That is a fact about two authoring passes. It is **not**
a measurement of what students type, because nothing in the repository has ever observed that.

This matters in both directions, and neither is comfortable:

- **It does not license "we need more data."** The brief's caution was exactly right. A 94.6%
  unseen-token rate between two authored corpora is evidence that the *authoring* was inconsistent.
  It cannot establish that a real-input model would fail, because the real input distribution is
  `[unknown]`.
- **It does not license dismissing the concern either.** `[inferred]` The abbreviations that
  diverge — `tgk`, `bt`, `btvn`, course codes — are precisely the forms a Vietnamese student would
  plausibly type, and the v4 author, writing student-facing text, evidently thought so too. The
  *hypothesis* that production input is abbreviation-heavy is well-motivated. It is a hypothesis.

**Net effect on S0's standing:** S0's measurements are correct and its `DAT-01` reporting bound was
right to forbid generalization claims — but for a **stronger reason than S0 recorded**. S0 bounded
its claims because the evaluation covered 3 of 5 classes. The real bound is that the evaluation
covers 0 of 5 classes *with real data* `[inferred]`.

**Not reopened.** The `EVA-16` kill decision and the 2026-06-25 recall eval both rest on the v4
premise. `DAT-04` already establishes that dataset expansion does not re-authorize S0, and this
report is audit-only. The affected documents are listed in §F.3 as an owner decision, not amended
here.

---

## H. Ranked Data Gaps

Ranked by **fix class** — what kind of work closes the gap — so a proposal can be organised around
the intervention rather than the symptom. Severity, impact, confidence and the brief's category are
carried as separate columns; note that severity order and fix-class order genuinely differ, and the
second is the one a plan can consume.

| # | Gap | Fix class | Category | Severity | Confidence | Likely impact on model development |
|---|---|---|---|---|---|---|
| **G-0** | **No verified real data exists.** `collected_v4` is authored (§E.6); no production telemetry is captured (§A group C) | **Collection — from actual users** | Provenance + evaluation hygiene | **Critical** | **High** — 7 measured regularities + an exact quota match | Every figure the project holds is authored-vs-authored. No claim about production behaviour is currently supportable at all |
| **G-1** | **Label instability ~29.6% between passes** (§E.1), no guideline, no adjudication, no annotator record | **Adjudication + guideline** | Label quality | **Critical** | **High** `[measured]` | Bounds every accuracy number any future model can honestly report. Collecting *or generating* more data at this label quality multiplies the noise. **Blocks G-0 and G-4 from being spent well** |
| **G-2** | **No uncontaminated evaluation set** (§F.1) — the shipped model trained on all 903 rows | **Collection with hold-out discipline** | Evaluation hygiene | **Critical** | **High** `[fact]` | Without it no claim about the shipped classifier is measurable. Cheapest structural fix: hold out *before* merging — the exact error `_merge_seed.py` already made |
| **G-3** | **15.1% of the shipped corpus has no traceable origin** (§E.5), concentrated at 37.8% / 35.3% of the two classes with no evaluation data | **Forensics, then re-derivation or removal** | Provenance | High | **High** for the measurement; **Medium** for "machine-produced" `[inferred]` | Compounds G-1: for these rows one cannot ask who labelled them or against what guideline |
| **G-4** | **Training vocabulary excludes the abbreviations the evaluation uses** (§G) — 94.6% of eval rows carry an unseen token | **Collection** (real), possibly abbreviation-aware normalisation | Linguistic coverage | High | **High** for the measurement; **Low** for "this predicts real-input failure" (§G.3) | Reframed by this audit: strong evidence of authoring inconsistency, weak evidence about production |
| **G-5** | **Two of five classes have zero evaluation data** (§C.1); the thinnest training class cannot be augmented from any existing file | **Targeted collection** | Class coverage | High | **High** `[measured]` | Blocks `DAT-01` from ever being lifted. A 5-class production claim is unreachable from present data by any transformation |
| **G-6** | **Study-time predictor trains on 180 RNG-labelled rows over 3 feature vectors** (§E.3); the ≥50-row real gate has never been met | **Instrumentation + accrual** | Data quantity (real) | **Critical** in isolation | **High** `[fact]` | An entire shipped model is trained on noise. Ranked below G-4 only because the fix is mechanical — rows accrue by themselves once the app is used |
| **G-7** | **`PredictedMinutes` / `Confidence` not recorded** (§E.4) | **Instrumentation** (small, urgent) | Evaluation hygiene | High | **High** `[fact]` | Cheapest high-value item here. Unlike every other gap, **delay makes it strictly worse** — each day writes rows that can never answer the question |
| **G-8** | **`DifficultyLabelLogs` written but never read** (§A C2); the `Difficulty` column is never trained on (§E.2) | **Plumbing / derivation** — data already exists | Latent capability | Medium | **High** `[fact]`; the 12/17 override rate is `[measured, n=17 — indicative only]` | A real supervised difficulty signal is discarded at both ends. Bears on the deferred M8-A confidence-gate work |
| **G-9** | **No datasheets, no dataset versioning, no row-level lineage** (§B.1); the `Source` column names a file, not an origin | **Housekeeping / governance** | Provenance | Medium (chronic) | **High** `[measured]` | `DAT-03` names "version datasets" explicitly. §F.3 is this gap's most consequential instance: "real" propagated into 8 documents unchecked |
| **G-10** | **Duplicate inflation, 24.7%, in `normalized_dataset*.csv`** (§E.7) | **Housekeeping** | Data quality | Low | **High** `[measured]` | Consistent labels, so silent 2–3× weighting rather than noise. Already removed downstream; matters only if those files are used again |
| **G-11** | **`TaskName` (477 distinct / 903) unused** (§E.2) | **Latent capability, not a gap** | — | — | `[measured]` | Recorded so a later design session knows a second labelled task already exists in the corpus |
| **G-12** | **`normalized_dataset_m8a_balanced.csv` has no live consumer** — but it is the lineage intermediate (§B) | **Retain, do not delete** | — | — | **High** `[measured]` | A *negative* entry: deleting it would sever the only link between the shipped seed and its root |

### The shape of the answer, stated plainly

The repository holds **zero verified real rows**. The 903-row production corpus is one authoring
lineage: 461 rows derived from an undocumented root through two annotation passes that disagree
about three times in ten, 101 hand-written templates, 136 rows that descend from nothing traceable,
and 205 rows authored to fill a quota table and then labelled "real" in eight documents.

**The constraint is not corpus size, and it never was.** It is that nothing in the corpus is known
to be correctly labelled (`G-1`), nothing in it is known to resemble production input (`G-0`), and
15% of it cannot be attributed at all (`G-3`).

**This is why "generate more synthetic data" cannot be assumed to be the intervention.** The project
has already run that experiment three times — `synthetic_v3`, the untraceable 136, and `v4` — and
the third one is the reason nobody noticed the first two. Generation against the current label
definitions makes `G-1` worse, leaves `G-3` untouched, and — on this project's demonstrated track
record — risks producing another corpus that is described as real for two months.

---

## I. Candidate Data Sources

**No dataset was downloaded, imported, or ingested.** Three dataset cards were read to verify
identity and licensing metadata rather than assert them from recall. Retrieved 2026-08-25.

### I.1 The structural conclusion, which needs no dataset name

`[inferred]` **No public Vietnamese corpus carries this project's five task-type labels.** The label
space (`ThiGiuaKy`, `ThiCuoiKy`, `KiemTraThuongXuyen`, `BaiTapVeNha`, `DoAnCuoiKy`) is specific to
this application's domain model. Therefore **every public-data path is "acquire linguistic
diversity, then relabel from scratch"** — which lands directly on `G-1`.

**Public data is blocked behind the annotation guideline that does not exist.** That ordering
constraint is the actionable content of this section; it holds regardless of which corpus is chosen.

### I.2 Verified candidates

| Dataset | Identity `[fact — from the source]` | Relevance | Licensing metadata | Relabel burden | Mismatch risk |
|---|---|---|---|---|---|
| **ViLexNorm** | Lexical normalization corpus for Vietnamese social media text; >10,000 human-annotated sentence pairs from public comments on Vietnam's most popular social media platforms. EACL 2024 (Nguyen, Le, Nguyen) | **Highest.** It is an *instrument* for the phenomena §D.4 cannot measure — teencode, abbreviations, phonetic misspellings, code-switching | **CC BY-NC-SA 4.0**, stated explicitly in the repo | N/A as a corpus — used as a normalisation resource, not training data | **`NC` = non-commercial.** If this application is ever distributed commercially this is a blocker. **Owner decision, OD-4** |
| **UIT-VSFC** | Vietnamese Students' Feedback Corpus; **16,175 rows**, sentiment + topic classification, student feedback text | Medium. Right *population* (Vietnamese students), wrong *task* (sentiment/topic, not task-type intent) | **No license field on the dataset card at retrieval time** `[measured]`. Described in third-party sources as "free for research" — **not a license** | Full relabel; the 5-class taxonomy does not exist in it | Feedback prose ≠ task-entry text. Register mismatch is large |
| **PhoATIS** (via `VinAIResearch/JointIDSF`) | First public Vietnamese intent-detection + slot-filling dataset; Vietnamese ATIS. INTERSPEECH 2021 | Low-medium. Right *task shape* (intent classification), wrong domain entirely | **No license surfaced on the repo page**; it states *"Please CITE our paper whenever our dataset or model implementation is used"* `[fact]` | Full relabel | **ATIS is airline-travel.** Domain distance is maximal; value is as a *format/methodology* reference, not a data source |

### I.3 Rules this section deliberately follows

- **Repository visibility is not a licence.** Two of three candidates surface **no licence at all**;
  both are publicly downloadable. Neither is therefore approved for use.
- **A dataset card is not legal clearance.** ViLexNorm's `CC BY-NC-SA 4.0` is quoted as the source
  words it; whether `NC` permits this project's use is an owner/legal question, not an audit finding.
- **No dataset here is approved.** All are candidates for a later evaluation that must include a
  licensing review.
- `[unknown]` Row counts and label-set sizes beyond what the cards state were **not** verified
  against the data files, because verifying them would require downloading them.

---

## J. Human-Labeling Boundaries

Where human judgement is required and cannot be delegated to generation or automated relabeling.
**This is an analysis of where authority is needed, not a request to label anything now.**

| # | Area | Why a human must decide | Volume |
|---|---|---|---|
| **J-1** | **Adjudicate the 208 disagreeing rows** (§E.1) | Two passes disagreed; no third opinion exists. An automated tiebreak would encode whichever pass the model was trained on | 208 rows `[measured]` |
| **J-2** | **Define the class boundaries that actually collide** | The largest transitions are `OnTap→NhacNho` (105), `KiemTraThuongXuyen→NhacNho` (29), `ThiCuoiKy→BaiTap` (26). **Two of these name retired classes** — so part of J-2 is the taxonomy question itself, not annotation | Guideline, not rows |
| **J-3** | **Dispose of the 136 untraceable rows** (§E.5) | Keep, re-label, or remove — a judgement about acceptable provenance in a shipped model. 37.8% / 35.3% of two classes go with the decision | Decision, then ≤136 rows |
| **J-4** | **Rule on `collected_v4`** (§E.6) | Whether the file stays in the seed, stays in the evaluation split, or is reclassified as synthetic. Eight documents change with the answer (§F.3) | Decision |
| **J-5** | **Relabel any imported public data** (§I.1) | No public corpus carries this label space. Every imported row needs a human label under the J-2 guideline | Scales with import |
| **J-6** | **Culturally specific slang and abbreviations** | `tgk`, `btvn`, `xstk`, course codes — a non-Vietnamese-speaking annotator or a general-purpose model cannot reliably distinguish an abbreviation from a typo. ViLexNorm (§I.2) reduces but does not remove this | Ongoing |
| **J-7** | **Ambiguous rows where models disagree** | The natural output of J-1: rows where adjudication itself is contested need an owner ruling to become ground truth | `[unknown]` until J-1 runs |

**Ordering constraint.** `J-2` gates `J-1`, which gates `J-5`. Labelling anything before the
guideline exists reproduces `G-1` at greater volume.

---

## K. Open Questions

> **Table as written 2026-08-25; rows 1 and 2 superseded — see the Amendment at the end of
> this report.** Both were answered by owner recall on 2026-08-26. Rows 3–7 stand.

| # | Question | Who can answer | Status |
|---|---|---|---|
| 1 | **What produced the 189 rows between `_balanced.csv` and `uniform`?** 136 are in the shipped model. `[inferred]` machine-produced; the process is not in the repo | **Owner recall** — faster than forensics | open |
| 2 | **How was `collected_v4.csv` actually produced?** `[inferred]` authored to the §8.3 quota. Whether a human wrote the surface text is `[unknown]` | **Owner recall** — the repository cannot answer | open, **highest value** |
| 3 | Was any real student data ever collected and *not* committed? | Owner | open |
| 4 | Does `FeaturizeText`'s default lowercasing neutralise the casing gap (§D.3)? Emoji and punctuation are not neutralised regardless | Verifiable in ~1h by inspecting the fitted transformer | open, cheap |
| 5 | 100-vs-101 synthetic-row discrepancy between CHANGELOG and the committed file | Unresolvable from committed artifacts | recorded, not resolved |
| 6 | What is the true real-world class distribution? | Requires C1-class telemetry | open, blocks §C.2 |
| 7 | Do `dacn` / `ttcs` belong in `vn_input_fixtures` given they appear in neither split? | Owner | minor |

---

## Verification

| Check | How | Result |
|---|---|---|
| Seed integrity vs `SPLIT.md` pin | SHA-256 of `seed_intents.csv` | **match** — `86abb454…` |
| Row counts, class and label distributions, all 10 CSVs | `csv.DictReader` over repo bytes, UTF-8-sig strict | as tabulated |
| Pairwise text overlap, all 10 CSVs | exact set intersection | as tabulated §A/§B |
| Near-duplicate leakage | whitespace-normalised + `đ→d` + NFD mark-strip + punctuation-strip | 0 across all train/test pairs |
| Label conflicts within duplicate groups | grouped by input, cardinality of label set | 0 / 168 groups, both files |
| Duplicate rates, all 7 corpora | normalised counter | 24.7% in `normalized_dataset*` only; 0% everywhere shipped |
| 402-row drop | **set difference**, `uniform − (train ∪ synthetic)` | `OnTap` 185 + `NhacNho` 217 — verified, not inferred from arithmetic |
| Chain direction `m8a` / `balanced` / `uniform` | subset tests in both directions | `balanced ⊂ m8a` **true**; `uniform ∩ m8a` **==** `uniform ∩ balanced` (both 810) |
| Uniform composition | set identity `uniform == (uniform∩balanced) ∪ synthetic ∪ orphans` | **true**, 810 + 101 + 189 = 1100 |
| Untraceable rows reaching production | `(uniform − m8a − synthetic) ∩ seed`, then class tally | 136; 37.8% `KiemTraThuongXuyen`, 35.3% `ThiCuoiKy` |
| Untraceable rows present anywhere else | membership test against all 4 other corpora | **0** in each |
| Cross-pass label disagreement | join on input text, partitioned by whether the old label survived | 208 / 703 = 29.6% |
| **`tgk` / `bt` unseen-token claim** | **word-boundary regex, no tokenizer** | train 0 / test 28 and train 0 / test 54 — **S0 reproduces** |
| **Linguistic phenomena, 17 detectors** | closed lexicons + codepoint classes, train vs test vs 3 provenance buckets | §D.2, §E.5 |
| **Emoji absence in `collected_v4`** | byte-level scan for 4-byte UTF-8 sequences | **none present** — a property of the file, not of `build_split.py` |
| **`collected_v4` template structure** | comma-tail regex, leading-time regex, length/token quantiles, `dd/mm` extraction, class-run detection | §E.6 items 1–7 |
| **Quota match** | plan §8.3 table vs class counts of the delivered file | 99 / **56** / **50** against +95 / +56 / +50; 0 dedup loss |
| Telemetry row counts | `sqlite3` **read-only** URI (`mode=ro`) on two untracked dev DBs | as tabulated §A group C |
| Training pipeline features | read `TextClassifierModelManager.cs:143-152`, `MLModelManager.cs:91-106`, `TextClassifierService.cs` | `InputText` → `TaskType` only; **no normalisation in the service path** |
| Provenance search | grep of `docs/`, `legacy/`, `datasheets/`, `tools/`, git history | no collection record for `collected_v4`; 8 documents assert it is real |
| Lineage commits | `git log --diff-filter=A --follow`, `git show --stat` | `b29cd24`, `9603c17`, `ab5112c`, `8855874`, `df5ac68` |
| Public dataset identity + licence | 3 dataset cards fetched and read | §I.2 |

**Not run, and why:**

- **No model was trained or evaluated.** §E.3's R² claim is `[inferred]` from the data construction,
  not measured. §D.3's `FeaturizeText` claim is `[inferred]` from documented defaults (§K item 4).
- **No file was modified.** All CSV reads and both SQLite connections were read-only (`mode=ro`).
  Analysis scripts were written to a scratchpad outside the repository, not to `tools/`.
- **No dataset was downloaded.** §I read three dataset cards over HTTP; nothing was ingested.
- **Telemetry counts are from two dev machines' `bin/` databases**, mtimes 2026-07-26 and
  2026-08-19 — an existence proof about instrumentation reachability, **not** a measurement of any
  user population. The 12/17 difficulty-override rate is indicative at n=17 and must not be cited
  as a rate.
- **Typos, ambiguity, word order, informal phrasing were not measured** (§D.4). Recorded as
  `[unknown]` rather than proxied.
- **The 100-vs-101 synthetic-row discrepancy was not resolved.** It cannot be settled from committed
  artifacts; recorded rather than explained away.
- **Group D lexicons were counted, not quality-assessed.**

---

## Data Maturation Inputs

Which intervention types the evidence supports. **No mix is decided here.**

| Intervention | Evidence-backed standing |
|---|---|
| **Real-data collection** | **Necessary and currently unsubstitutable.** `G-0`. Nothing else produces a real distribution. Two forms: instrument the app (`G-6`/`G-7` — rows accrue free once wired) and collect genuine student input with a hold-out held back *before* any merge (`G-2`) |
| **Curation / relabeling** | **Necessary, and it gates the others.** `G-1` + `J-2`. Until a guideline and an adjudication record exist, every other intervention adds rows of unknown correctness. This is the cheapest high-value work in the whole set — it needs judgement, not infrastructure |
| **Evaluation-set expansion** | **Necessary and structurally distinct from training collection.** `G-2` + `G-5`. Must include the two classes at zero coverage, and must be held out before merge — the error `_merge_seed.py` already made once |
| **Public-data acquisition** | **Plausible, for linguistic diversity only, and blocked behind `G-1`.** §I.1. No public corpus carries this label space, so every row needs relabeling. ViLexNorm is valuable as a *normalisation instrument* rather than as training data, subject to its `NC` term |
| **Synthetic augmentation** | **Not ruled out; not assumable; and this project's track record with it is poor.** It has been run three times (`synthetic_v3`, the untraceable 136, `v4`) and the third was described as real in eight documents for two months. `[inferred]` If used at all it should be: (a) after `G-1`, (b) never in an evaluation set, (c) labelled at generation time in a way `Source` cannot lose, (d) held to a distributional check that would have caught §E.6's seven regularities |

**The one ordering constraint the evidence establishes:** `G-1` (guideline + adjudication) precedes
every intervention that adds rows. Everything else is a sequencing choice; this one is a dependency.

---

## Owner Decisions Required

> **Table as written 2026-08-25; all six ruled 2026-08-26 — see the Amendment at the end of
> this report.** Left unedited: what an audit could not decide is part of its record.

Only questions that genuinely need owner judgement.

| # | Decision | Why it cannot be decided from evidence |
|---|---|---|
| **OD-1** | **How to dispose of the eight documents asserting `collected_v4` is real** (§F.3), including a spec line tagged `[fact]` | Amending other people's specs is outside an audit's scope, and `EVA-16`/`DAT-04` interact with it |
| **OD-2** | **What actually happened when `collected_v4` was produced** (§K.2) | Only owner recall can answer. Everything downstream of `G-0` depends on it |
| **OD-3** | **What produced the 189 untraceable rows** (§K.1), and whether the 136 in the seed stay | Same — recall is faster than forensics, and the disposal is a judgement about acceptable provenance |
| **OD-4** | **Whether `CC BY-NC-SA 4.0` (ViLexNorm) is compatible with this project's distribution** | A licensing/commercial question, not a data question |
| **OD-5** | **Whether `G-7` (missing `PredictedMinutes`) is raised as a defect now**, separately from this workstream | It is a shipped-code defect that worsens daily; the audit can only flag it |
| **OD-6** | **Whether the taxonomy itself is reopened** (`J-2`) — two of the three largest label transitions involve retired classes | A product decision about what the app models, upstream of any annotation work |

---

## Research Confidence

| Conclusion | Confidence | Basis |
|---|---|---|
| `collected_v4` is not organically collected | **High** | 7 independent measured regularities + exact quota match against a committed plan. Two innocent explanations tested and excluded (§E.6) |
| No verified real data exists anywhere in the project | **High** | Follows from the above plus the untracked-DB finding `[measured]` |
| Label instability is ~29.6% | **High** | Direct measurement, correctly partitioned for the taxonomy change `[measured]` |
| The shipped classifier has consumed its whole corpus | **High** | `[fact]` — source comment and set membership |
| The study-time predictor trains on noise | **High** | `[fact]` — read from `SeedDataGenerator.cs` |
| The R² ≥ 0.45 gate is passable by a lookup table | **Medium** | `[inferred]` from data construction; not measured by running the trainer |
| The 136 untraceable rows are machine-produced | **Medium-High** | 100.0% and four exact zeros are strong; the `ô n` artifact proves processing, not authorship |
| The vocabulary gap does *not* prove real-input failure | **High** (as a negative claim) | Follows from `collected_v4` not being real. The positive claim — that production *is* abbreviation-heavy — is **Low** confidence and explicitly a hypothesis (§G.3) |
| No public corpus carries this label space | **Medium-High** | `[inferred]` from the domain-specific taxonomy; 3 candidates checked, not an exhaustive survey |
| Casing differences do not reach the model | **Low** | `[inferred]` from documented ML.NET defaults; unverified (§K.4) |

---

## Recommendation

**Yes — the evidence justifies a formal Data Maturation & Coverage Expansion proposal, and it is
now more urgent than the audit's first pass suggested.**

The trigger is not that the data is thin. It is that **the project has been measuring itself against
its own authored output while believing otherwise**, and the mechanism that allowed it — a `Source`
column read as provenance, no datasheets, no collection record — is still in place and would allow
it again.

The proposal should address these five, in this order:

1. **`G-1` — Annotation guideline and adjudication.** Nothing else can be spent well until this
   exists. Cheapest of the five, needs judgement rather than infrastructure, and gates the rest.
2. **`G-0`/`G-2` — Obtain genuinely real data, with a hold-out reserved before any merge.** Includes
   deciding the standard of evidence that makes a future corpus *provably* real — a datasheet, a
   collection record, and a distributional check that would have caught §E.6.
3. **`G-7` — Record `PredictedMinutes`/`Confidence` now.** The only gap where delay is irreversible;
   every day writes permanently unanalysable rows. Small, and independent of the other four.
4. **`G-3`/`G-9` — Resolve the 136 untraceable rows and close the governance hole.** `OD-3` is
   likely a five-minute answer from memory; the governance fix is what stops recurrence.
5. **`G-5` — Coverage for the two classes at zero evaluation data**, which is also where the
   untraceable rows concentrate.

**What the proposal should not assume:** that synthetic generation is the intervention. It may have
a role after (1), bounded as described in *Data Maturation Inputs*, but on this project's track
record it is the intervention most likely to recreate the problem this audit found.

---

## Decisions made

### D-1 — Audited repository bytes as the authority; treated S0 and CHANGELOG as claims to check

**Why it had to be made.** The brief said to treat the S0 report as experimental evidence rather
than as the description of the dataset, and the project's own history contains a case
(`ab5112c` → the 96.2% figure) where a number outlived the conditions that produced it.

**What it's for.** Every number is recomputed from committed bytes and tagged. Where a document and
the bytes disagree — the 100-vs-101 synthetic rows — the disagreement is reported rather than
reconciled toward either side.

**Experience for future development.** It paid three times. Re-hashing the seed against `SPLIT.md`'s
pin turned "are the S0 numbers still valid?" into a one-line fact. Re-running the leakage check
under stricter normalisation confirmed a clean split instead of inheriting the claim. And the third
time it overturned the premise eight documents shared (§E.6) — which no amount of reading those
documents would have surfaced, because they all cite each other.

### D-2 — Verified the 402-row drop by set difference rather than accepting matching counts

**Why it had to be made.** `NhacNho` (217) + `OnTap` (185) = 402 exactly, which is suggestive but is
not proof that *those* rows are the dropped ones.

**What it's for.** The lineage claim "the seed is `m8a_uniform` minus `NhacNho`/`OnTap`" is
load-bearing: it establishes that two classes exist in the corpus but not in the production label
space. An arithmetic coincidence would have been an invisible error in the lineage diagram.

**Experience for future development.** Matching totals are the classic false positive in data
lineage. When a count reconciles, compute the set — it is usually a two-line change to the script
already open, and it converts an `[inferred]` into a `[measured]`.

### D-3 — Split the 51.8% raw label disagreement into "taxonomy retired the label" and "row moved anyway"

**Why it had to be made.** The raw figure — 533 of 1028 rows relabelled — overstates annotator
instability, because 325 of those rows had no choice: `Khac` and `DuAn` were removed from the label
space. Reporting 51.8% as an annotation-quality number would have been inflated, and `G-1` is a
top-ranked gap.

**What it's for.** The honest figure is 208/703 = 29.6% — disagreement among rows where the original
label was still available. That is the number a proposal should plan against.

**Experience for future development.** When two label passes are compared, always partition by
whether the source label survived the target taxonomy. A schema change and an annotator disagreement
look identical in a naive diff and mean completely different things: one is a decision to re-record,
the other is noise to price.

### D-4 — Ranked gaps by fix class, and declined to treat corpus size as the finding

**Why it had to be made.** The brief explicitly forbade assuming synthetic data is the solution. An
audit that concludes "we need more data" is unactionable, and here it would also be wrong: the
top-ranked gaps are not size problems.

**What it's for.** The **Fix class** column groups gaps by intervention — adjudication, collection,
instrumentation, plumbing, governance — so the proposal can be organised around what the work *is*.
Severity, impact and confidence are carried as separate columns rather than folded into the order,
because the two orderings genuinely differ.

**Experience for future development.** "Rank by severity" and "rank by what fixes it" produce
different orderings, and the second is the one a proposal can consume. `G-6` (a model trained on RNG
labels) is more severe than `G-4` but ranks below it, because rows accrue by themselves once the app
is used while vocabulary coverage needs deliberate work. The same column also earned a *negative*
entry — `G-12` exists to stop a file being deleted — which a severity ranking has no place to put.

### D-5 — Traced every arrow in the lineage diagram, after one of them turned out to be wrong

**Why it had to be made.** The first draft drew `m8a → uniform` with `_balanced.csv` as a side
branch, and labelled `_balanced.csv` a dead artifact recommended for deletion. Both were inferences
from file names and rough overlap, presented inside a diagram that read as measured.

**What it's for.** Subset-testing each link reversed two claims and produced `G-3`. `_balanced.csv`
is the intermediate every m8a-derived seed row passes through — deleting it as "dead" would have
severed the only link between the shipped seed and its root. The 290 unaccounted rows resolved into
101 synthetic plus **189 with no origin at all**, 136 of which are in the shipped model.

**Experience for future development.** A lineage diagram is an *assertion set*, and each arrow is a
separate claim needing separate evidence — the format makes inference look like measurement more
effectively than prose does. The specific tell was an overlap percentage that did not reach 100% in
either direction: when a derived file shares only 74% of its rows with its supposed parent, the
missing quarter is the finding. It had been sitting in the first table this audit produced.

### D-6 — Read the untracked dev databases, and bounded what they may be used to claim

**Why it had to be made.** The brief asked about telemetry. The repository holds none — the app
database is untracked by design. Stopping there would have reported "no telemetry exists", which is
true of the repo and false of the system: the writers are wired and firing.

**What it's for.** Opening the two `bin/` databases read-only distinguishes *"instrumentation was
never built"* from *"instrumentation works and has produced 0–17 rows on one machine"*. Those imply
completely different Phase-1 work. Each figure is scoped as an existence proof about one machine,
never a population rate.

**Experience for future development.** Untracked local state can answer questions the repository
cannot, and reading it is safe when the connection is read-only and the scope travels with the
number. The discipline that makes it safe is writing the bound into the same sentence as the figure,
not into a caveats section further down.

### D-7 — Reported `[unknown]` for five linguistic phenomena rather than proxying them with regexes

**Why it had to be made.** The brief asks for coverage of typos, slang, informal phrasing, word order
and ambiguity. Detectors for these are writable in minutes and would have filled the table.

**What it's for.** Five rows of §D.4 say `[unknown]`, and the slang row is explicitly a lower bound
on a *named* 30-token list rather than a slang rate. A table where seven of twelve rows carry numbers
produced by a regex proxy for "slang" is worse than one that says `[unknown]` seven times, because
the next session builds on the numbers and cannot see the proxy.

**Experience for future development.** The cost of a fabricated measurement is paid later and by
someone else. When the instrument does not exist, naming the instrument that would work — ViLexNorm,
for four of these five — is more useful than a number. It also converted a gap in the audit into a
concrete entry in §I.

### D-8 — Verified the S0 vocabulary claim with a tokenizer-free method before reassessing it

**Why it had to be made.** The audit was going to *reframe* S0's headline finding (§G.3). Reframing a
finding one has not reproduced is how a review becomes an opinion.

**What it's for.** `tgk` 0/28 and `bt` 0/54 were re-derived by word-boundary regex, which shares no
machinery with S0's tokenizer. S0's arithmetic reproduces exactly. The reassessment therefore attacks
the *interpretation* while confirming the *measurement* — two separable questions that a single
"S0 was wrong" would have blurred.

**Experience for future development.** Separate "did they compute what they claimed" from "does the
number mean what they said". A different figure from a different tokenizer is not a refutation. Here
the first question came back clean, which made the second finding much harder to dismiss.

### D-9 — Checked how `collected_v4` was made before overturning the premise built on it

**Why it had to be made.** Seven structural regularities are a strong inference, but they say nothing
about *how* the file was produced. Publishing "this is generated" on structure alone, against eight
documents saying otherwise, would have been an escalation by reasoning — the exact failure this
project has a standing rule against.

**What it's for.** Four git and grep commands found the retrain plan's §8.3 quota table. The delivered
file matches two of three quotas **exactly** and lost **zero** rows to a dedup step the plan warned
to over-collect against. That converted the inference from "structurally improbable" to
"structurally improbable *and* matching a specification that was written down first" — and it found
the documented contradiction (§F.3), which is stronger evidence than the structure alone.

**Experience for future development.** When evidence points at a conclusion that invalidates other
people's committed work, spend the ten minutes on provenance first. All three possible outcomes beat
publishing the inference: a document describing generation makes it `[fact]`; a document describing
collection makes it a *contradiction*, which is more useful still; and nothing found means the
inference stands and one can say where it was looked for.

### D-10 — Read three dataset cards rather than naming public datasets from recall

**Why it had to be made.** Dataset names, row counts and especially licence terms recalled from
training are unreliable at the granularity a durable report implies, and the brief explicitly forbids
inferring licensing permission from repository visibility.

**What it's for.** §I.2 quotes ViLexNorm's `CC BY-NC-SA 4.0` as its repository words it, and records
that **UIT-VSFC and PhoATIS surface no licence at all** — which is itself the finding the brief's
caution anticipated. The `NC` term became `OD-4`, a real constraint that a recalled "it's open" would
have hidden.

**Experience for future development.** Fetching three cards cost about two minutes and changed the
section's content, not just its citations. It also clarified that the *actionable* conclusion needed
no dataset names at all: no public Vietnamese corpus carries this label space, so every public-data
path routes through `G-1`. Lead with the conclusion that survives regardless of which corpus is
picked.

---

## Amendment, 2026-08-26 — the owner ruling closed every decision this audit deferred

This report deferred six decisions (`OD-1 … OD-6`) and two provenance questions (`K.1`, `K.2`). All
eight were closed by the owner on 2026-08-26. The ruling is filed verbatim at
[`../plans/2026-08-26-data-foundation-owner-decision-handoff.md`](../plans/2026-08-26-data-foundation-owner-decision-handoff.md);
the brief that framed the choices is
[`2026-08-26-data-foundation-owner-decision-brief.md`](2026-08-26-data-foundation-owner-decision-brief.md).

**Nothing in the body above is rewritten.** Two tables carry a superseded marker at their heading; the
rows themselves stand as written, because what the audit could and could not establish is the part of
this document with the longest shelf life.

### K.1 and K.2 — answered by owner recall

| # | Audit status | Answer, 2026-08-26 |
|---|---|---|
| **K.2** | open, *"highest value"* — how was `collected_v4.csv` produced? | **Owner templates/examples → Meta AI generation → GitHub Copilot labelling.** Synthetic/AI-authored throughout; the labels are AI-assigned, not owner-verified |
| **K.1** | open — what produced the 189 rows between `_balanced.csv` and `uniform`? | **~2 000 Meta AI-generated rows, aggregated by GitHub Copilot into two datasheets**, from which the 189 descend; **136** reached the production seed. They stay in the seed |

**Provenance grade — read this before citing either answer.** Both are **rulings**: an authorised
person's statement, with no written collection or generation record in or out of the repository. They
are not observations, and no artifact was found that corroborates them. What they establish is
*process-level* origin. They do **not** establish label correctness, and they do not convert either
corpus into evidence about real student input.

**What this confirms, and what it changes.** §E.6 inferred from seven distributional regularities and
an exact quota match that `collected_v4` was authored to a spec; §E.5 inferred from generator
fingerprints that the 136 are machine-produced. The ruling **agrees with both inferences and names the
mechanism**, which the measurements could not. The `[inferred]` tags in §E.5/§E.6 are therefore
corroborated rather than replaced — a measured inference plus an independent recall pointing the same
way is a stronger record than either alone. **No number in this report changes.**

### OD-1 … OD-6 — every deferred decision, ruled

| # | Audit's question | Ruling | Consequence for this report |
|---|---|---|---|
| **OD-1** | Disposal of the eight documents asserting `collected_v4` is real (§F.3) | **DFD-1 ratified** — corrected by dated amendment, never by rewrite | §F.3's list was executed on 2026-08-26; see [`2026-08-26-data-foundation-correction-pass.md`](2026-08-26-data-foundation-correction-pass.md) |
| **OD-2** | What actually happened when `collected_v4` was produced | **P-1 resolved** (above) | §K.2 closed; §E.6 corroborated |
| **OD-3** | What produced the 189 rows, and whether the 136 stay | **P-2 resolved** (above); the 136 **stay** | §K.1 closed; §E.5 corroborated |
| **OD-4** | Is `CC BY-NC-SA 4.0` (ViLexNorm) compatible with this project? | **Unresolved by design.** DFD-7 permits evaluation of external datasets, prohibits training ingestion, and holds the licence question open as an owner decision | §I.2 stands; ViLexNorm remains a *candidate instrument*, not an approved source |
| **OD-5** | Raise `G-7` (missing `PredictedMinutes`) as a defect now? | **DFD-9a ratified — yes, now, separately** | Raised as [`../plans/2026-08-26-prediction-instrumentation-defect.md`](../plans/2026-08-26-prediction-instrumentation-defect.md) |
| **OD-6** | Is the taxonomy reopened? | **P-3 — limited review only.** The five-class production taxonomy remains the working baseline; no silent changes | §J-2 narrows: the review targets retired-class transitions and boundary collisions, not a redesign. §D-3's partition of the 51.8% raw disagreement is its starting point |

### The one ordering constraint, re-confirmed

The audit's *Data Maturation Inputs* section stated a single dependency: `G-1` (guideline +
adjudication) precedes every intervention that adds rows. The ruling ratifies that as policy — DFD-2
requires a canonical annotation specification **before** further labelled data is collected, imported,
generated or promoted, and P-3 precedes DFD-2's finalization. The staged foundation the ruling
prescribes is carried into
[`../plans/2026-08-26-data-maturation-coverage-expansion.md`](../plans/2026-08-26-data-maturation-coverage-expansion.md).
