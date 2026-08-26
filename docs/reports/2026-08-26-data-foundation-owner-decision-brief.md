# Data Foundation — Owner Decision Brief

**Date:** 2026-08-26
**Author/agent:** Claude (Opus 5) via Claude Code, acting as independent senior ML/data architect
**Branch:** `docs/encoder-knowledge-consolidation`
**Primary evidence:** [`2026-08-25-data-audit-gap-map.md`](2026-08-25-data-audit-gap-map.md) (Data Maturation Phase 0)
**Status:** **Decision surface — awaiting owner ruling. No decision in this document is taken.**

---

## 0. Executive finding

**The Data Audit did not find that the project has too little data. It found that the project has
no data of known provenance and no labels of known correctness — and that the mechanism which hid
this for two months is still in place.** Nine foundational decisions must be made before a Data
Maturation proposal can be written, because every plausible proposal changes shape depending on how
they land.

Three consequences, stated before the evidence that earns them:

1. **Every accuracy figure the project holds is authored data measured against authored data.**
   96.2%, 97.24%/97.25%, and all five S0 arms. None of them is invalidated as a *measurement*; all
   of them must stop being cited as evidence of real-world generalization. §2 separates what stands
   from what must be downgraded.

2. **The binding constraint is label correctness (~29.6% inter-pass disagreement, no guideline, no
   adjudicator, no annotator record), not corpus size.** The audit establishes exactly one hard
   dependency in the entire gap set: *annotation governance precedes every intervention that adds
   rows*. Collection, generation, and public-data import all land behind it.

3. **Two of the nine decisions are irreversible if delayed**, and they are not the two that look
   most urgent. Missing prediction instrumentation (`PredictedMinutes`/`Confidence` written as
   `null`) writes permanently unanalysable rows every day the app runs; and provenance metadata
   cannot be retrofitted onto rows after they exist — which is precisely what the 15.1% untraceable
   block *is*.

**Minimum decision set before a proposal can be written:** two owner-recall prerequisites (P-1, P-2),
then **DFD-1** (real-data policy), **DFD-2 + DFD-8** (annotation governance and human authority —
inseparable), **DFD-5** (provenance policy), and the urgent half of **DFD-9** (raise the
instrumentation defect now, or not). The remaining four are sequencing, not blockers. §4 shows the
ranking; §5 shows the order.

**This brief does not choose any option.** Ranking the decisions is not the same as recommending
answers to them, and §5 is explicit about the difference.

---

## Scope

**In scope.** Convert the Data Audit's findings into an owner-facing decision surface: what the data
reality now is, what it does to previously published ML evidence, which foundational decisions must
be made first, what the realistic options and their consequences are, and in what order.

**Out of scope, deliberately.** No synthetic data generated. No dataset downloaded, imported or
evaluated. No production code, dataset, or historical report modified. **No Data Maturation
implementation plan** — this brief stops at the decision surface, per its stopping condition. No
option is chosen on the owner's behalf.

**Explicitly not done:** the historical reports (`2026-06-25-m8a-textclassifier-v4-recall-eval.md`,
`2026-08-25-encoder-pilot.md`, the roadmap and spec annotations) are **not** rewritten here. §2 is
an impact assessment; the lifecycle corrections it implies are themselves an owner decision (DFD-1).

### Evidence convention — and why it differs from the audit's

The audit tagged its own statements `[fact]` / `[measured]` / `[inferred]` / `[unknown]`. **This
brief ran no new measurement over the corpus, so it does not inherit the `[measured]` tag.** Carrying
someone else's measurement tag as your own is the exact failure mode the audit documented — the word
"real" propagating from a request into eight documents until a spec line carried it as `[fact]`.

| Tag | Meaning here |
|---|---|
| `[audit §X]` | Reported by the Data Audit at that section; not re-verified in this session |
| `[verified]` | Checked directly in this session — the check is named in §Verification |
| `[analysis]` | This brief's own reasoning over audit findings; the reasoning is stated so it can be attacked |
| `[open]` | Genuinely unresolved; recorded, not guessed |

---

## 1. Current data reality

Evidence-backed facts only. Nothing here is classified as an implementation bug unless the audit
classified it as one.

### 1.1 The seven facts that define the starting position

| # | Fact | Source | Audit's classification |
|---|---|---|---|
| **R-1** | **Zero verified real user-authored rows exist in the repository — in every class, not merely thin.** The "verified real" column of the class-coverage table is `0` for all five classes | `[audit §C.1, §E.6]` | Gap `G-0`, **Critical**, High confidence |
| **R-2** | **`collected_v4.csv` (205 rows) is not collected data.** Seven measured regularities plus an exact match to a committed quota table (`+95/+56/+50` asked; `99/56/50` delivered; **zero** rows lost to a dedup step the plan warned to over-collect against) | `[audit §E.6]` | **Confirmed defect** — "the report's central finding" |
| **R-3** | **Annotation instability ~29.6%.** Of 703 rows whose original label survived the taxonomy change, **208 carry a different `TaskType`** after the second pass. `Difficulty` disagrees on 167/1028 (16.2%). **No annotation guideline, adjudication record, or annotator identity exists anywhere in the repository for either pass** | `[audit §E.1]` | **Confirmed defect**, gap `G-1`, **Critical** |
| **R-4** | **No trustworthy held-out real evaluation set exists**, blocked three independent ways: there is no real input; the shipped model trains on all 903 rows with no split; and `train.csv`/`test.csv` are two filters on one file whose halves were authored against the same label definitions | `[audit §F.1]` | Gap `G-2`, **Critical** |
| **R-5** | **15.1% of the shipped 903-row corpus (136 rows) has no traceable origin** — present in no other committed file. Concentrated at **37.8%** of `KiemTraThuongXuyen` and **35.3%** of `ThiCuoiKy`: the two classes with **zero** evaluation rows | `[audit §E.5, §C.1]` | **Confirmed defect**, gap `G-3` |
| **R-6** | **The `Source` column is not provenance.** It records the first file a row was seen in. All 136 untraceable rows read `Source = m8a_uniform`; all 205 v4 rows read `Source = collected_v4` — the literal string the retrain plan told the author to type | `[audit §B.1]` | Gap `G-9`, Medium (chronic) |
| **R-7** | **The word "real" propagated into eight documents**, including a spec requirement line tagged `[fact]` in the spec's own convention, without any document ever recording an act of collection | `[audit §F.3]` | "The governance failure `G-9` describes, in its most consequential instance" |

### 1.2 Class and linguistic coverage, as it actually stands

**Class coverage** `[audit §C.1, §C.3]`:

- All five classes are present **only synthetically**. Verified-real count is `0` for each.
- **Evaluation covers 3 of 5 classes.** `KiemTraThuongXuyen` and `ThiCuoiKy` have **zero** evaluation
  rows — and are the two classes most made of untraceable rows.
- **The evaluation's dominant class is training's weakest.** `ThiGiuaKy` is 12.2% of training
  (85 rows, 65% of them `synthetic_v3` templates) and **48.3%** of evaluation (99 rows).
- The corpus was balanced to **1.11×** against a real distribution that has never been observed.
  `[audit §C.2]` records the consequence plainly: if the real distribution is skewed, balancing moved
  the training prior *away* from production, not toward it. This cannot be settled without telemetry.
- `ThiGiuaKy` **cannot be augmented from any datasheet in the repository** — after dedup, zero
  additive rows exist. That is why `v4` was requested `[audit §C.3]`.

**Linguistic coverage** `[audit §D]` — with the caveat the audit puts at the head of its own table:

> Every rate compares `train.csv` against `test.csv`. **Both sides are authored.** Every row of that
> table compares two authoring processes; **none of it measures how students actually write.**

- Largest divergences: task abbreviations **+33.8pp**, numeric dates **+28.9pp**, course-code
  abbreviations **+19.2pp**, weekday shorthand **+17.6pp**.
- **Three exact zeros in `collected_v4`:** 0 emoji, 0 sentence-initial capitals, 0 terminal `.!?`
  across 205 rows, verified at byte level. Consequence that stands regardless of what produced the
  file: **the shipped classifier trains on 23.1% emoji-bearing rows and is evaluated on 0%.**
  Whatever it learned to associate with emoji is measured by no evaluation the project has run.
- **Five phenomena the brief's own checklist asks about are `[unknown]`, not measured**: typos,
  informal phrasing, alternate word order, ambiguous wording; slang is a *lower bound on a named
  30-token list* only. The audit declined to proxy them with regexes — a decision §5 depends on,
  because "generate against measured coverage gaps" presumes measurements that do not exist.

### 1.3 Telemetry — real signal, captured, mostly unconsumed

`[audit §A group C, §E.3, §E.4]`

| Table | Observed | Consumer | State |
|---|---|---|---|
| `DifficultyLabelLogs` | Debug 3, Release **17** (12 overrides) | **none** | Real human difficulty labels, written and never read |
| `StudyTimeOutcomeLogs` | Debug 2, Release 0 | `StudyTimeTrainingDataSource`, gate `MinRows = 50` | **Gate never met** → the predictor always falls back to `SeedDataGenerator.Generate()` |
| `WeightChangeLogs` | Debug 1, Release 0 | rule engine, not a model | shipped 2026-06-11 |
| `OptimizerRunLogs` | 0 | — | **Empty is correct** — G3-1 wiring unscheduled. Not a data gap |
| `StudyLogs` (Debug) | 5404 | analytics UI | **Generated demo data, not real usage** |

Two findings inside this that bear directly on decisions:

- **The study-time predictor trains on noise.** The fallback is 180 rows over **3 distinct feature
  vectors**, with `Label = uniform(min,max) × (1 ± 0.15)` — the label is a random draw. `[audit §E.3]`
  classifies this a **confirmed defect**; the inference that the R² ≥ 0.45 gate is passable by a
  three-value lookup table is `[inferred]`, not measured.
- **The telemetry that exists cannot measure its own model.** `FocusViewModel` writes
  `PredictedMinutes = null` and `Confidence = null`. **Prediction error and calibration are not
  computable from telemetry no matter how many rows accumulate**, and the deficiency cannot be
  repaired retroactively — only rows written after an instrumentation change can carry it.

**No production telemetry exists in the repository at all.** The application database is untracked;
the counts above come from two local dev databases and describe this machine, not any user
population. The 12/17 override rate is indicative at n=17 and must not be cited as a rate.

### 1.4 Data governance gaps

`[audit §B.1, §G-9]` — **no datasheets** (one exception: `vn_input_fixtures`, the only source in the
repository with one), **no dataset versioning**, **no row-level lineage**, **no collection records**,
**no annotation guideline versioning**, and a `Source` column that names a file rather than an origin.
Three provenance breaks: the undocumented root (`normalized_dataset.csv`, entering in a bulk commit),
the mid-chain 189 rows, and `collected_v4`.

`[analysis]` These are not separate failures. They are one missing control — *nothing recorded where
a row came from* — observed at three points in one lineage. That framing matters for DFD-5: a policy
that fixes only the symptom nearest to hand fixes none of them.

---

## 2. Impact on previous ML evidence

**This section does not rewrite the historical reports.** It states what may and may not still be
cited from them. Whether the affected documents are amended is DFD-1.

### 2.1 The 96.2% held-out figure — and a correction annotation that states the wrong mechanism

**What it is** `[audit §B; CHANGELOG:212]`: a stratified 85/15 held-out evaluation at the **v3 /
698-row** seed, n=106, reported 2026-06-05.

**What two documents currently say about it** `[verified]`:

> `docs/specs/system_roadmap.md:40` (annotated 2026-08-24): *"it was measured **after** the 205 real
> `collected_v4` rows had been merged into the training seed"*
>
> `docs/specs/2026-08-24-neural-encoder-smart-parser.md:371` (§6.1, tagged `[fact]`): *"The real rows
> were merged into the training seed **before** it was measured."*

**That mechanism is chronologically impossible** `[verified — git log + arithmetic]`:

| Event | Date | Evidence |
|---|---|---|
| 96.2% measured at seed v3 (698 rows) | **2026-06-05** | CHANGELOG entry heading; commit `9603c17` |
| n=106 held-out at 85/15 | — | 698 × 0.15 ≈ **104.7 ≈ 106**. 903 × 0.15 ≈ 135.5 — does not fit |
| `collected_v4.csv` enters the repository | **2026-06-18** | `8855874`, `--diff-filter=A` |
| `collected_v4` merged into the seed (698 → 903) | **2026-06-18** | `ab5112c` |

The figure **predates `collected_v4` by thirteen days**. At the moment it was measured the seed
contained **zero** `collected_v4` rows.

**What this changes, and what it does not:**

- **The conclusion stands, and is strengthened.** 96.2% is not a synthetic→real generalization
  number and must not be cited as one. It is a *stronger* statement than the annotations make: it is
  an in-distribution number over a corpus that at that date was **entirely** derived-plus-synthetic,
  with the derived half carrying the ~29.6% label instability (R-3) and the untraceable 136 (R-5).
- **The stated reason is wrong**, in two live documents, one of which tags it `[fact]`.
  `[analysis]` This is the same failure as R-7 running in the opposite direction: a *correction*
  written from an assumption rather than from the lineage. It is evidence that DFD-1's lifecycle
  correction cannot be a search-and-replace of the word "real" — the annotations that already tried
  to correct it are themselves wrong.
- **Verdict:** `96.2%` — **downgrade, and re-annotate.** Citable only as *"an in-distribution
  held-out score over the 698-row v3 authored seed, 2026-06-05, n=106, against labels of ~29.6%
  inter-pass stability."*

### 2.2 The 97.24% / 97.25% recall evaluation (2026-06-25)

**What remains valid** `[audit §E.7, §E.8]`:

- The methodology. A stratified 80/20 split mirroring the production pipeline exactly, deterministic,
  with per-class tallies. The audit found no fault in it.
- The **relative** before/after comparison it was commissioned to make — *"minority recall did not
  regress"* (MacroAccuracy identical at 97.25%). That comparison is between two models over the same
  authored corpus and does not depend on the corpus being real.
- The report's own stated limitation, which was correct when written: *"the ~97% absolute accuracy is
  optimistic, not a clean generalization figure"*, because the seed contains templated near-duplicates
  and the split is random *within* the seed.

**What must be downgraded:**

- **The absolute figures.** `[audit §E.8]` states it directly: 97.24% micro-accuracy on data authored
  against the label definitions it is graded by *is the expected result, not a model quality signal*.
- **The report's stated substantive win.** Its verdict says the value of `v4` was that *"the recall
  estimate for exactly the thin, test-guarded classes is now far more reliable"* — because held-out
  minority support grew (ThiGiuaKy 17→37, BaiTapVeNha 25→36, DoAnCuoiKy 26→36). **That growth came
  entirely from `collected_v4` rows** `[analysis]`. Under R-2 the added support is more rows from one
  authoring process, so the estimate became *more precise about the same authored distribution*, not
  more reliable about production. This is the specific sentence the audit newly breaks; the rest of
  the report's limitations section already anticipated the others.
- **Verdict:** `97.24% / 97.25%` — **valid as a before/after regression check; must no longer be
  cited as accuracy on real input, and the "more reliable estimate" claim must be re-scoped.**

### 2.3 The S0 encoder comparison

**What remains valid** `[audit §G.1, §G.2]`:

- **S0's arithmetic reproduces exactly.** The audit re-derived the vocabulary-gap claims with a
  word-boundary regex and **no tokenizer**, so the check does not inherit S0's tokenization choices.
  `tgk` train 0 / test 28, `bt` train 0 / test 54, and every other reported token reproduce. The
  headline 25.0% unseen test tokens and **94.6% of test rows carrying ≥1 unseen token** stand.
- **The macro-F1 result stands.** Every encoder configuration scored below the baseline mean
  (baseline 0.6575; best encoder arm 0.6484). The EVA-16 kill criterion fired correctly.
- **The confidence-calibration finding stands**, including the F-1 defect candidate — the shipped
  0.60 gate sitting above a bin with **0.000** observed accuracy.
- **S0's `DAT-01` reporting bound was right** — it forbade generalization claims. The audit says it
  was right *for a stronger reason than S0 recorded*: S0 bounded itself because the evaluation covered
  3 of 5 classes; the real bound is that it covered **0 of 5 classes with real data**.

**What must be downgraded:**

- **The framing of `test.csv` as real.** The vocabulary gap is a gap between **two authoring
  processes**, not a measurement of student vocabulary `[audit §G.3]`. The audit is careful that this
  cuts both ways: it does not license *"we need more data"* (a 94.6% unseen rate between two authored
  corpora is evidence the *authoring* was inconsistent), and it does not license dismissing the
  concern either (the diverging forms — `tgk`, `bt`, `btvn`, course codes — are plausibly what students
  type; that is a **well-motivated hypothesis**, not a finding).
- **Any sentence containing "real held-out"** in the pilot report, `SPLIT.md`, `build_fixtures.py`,
  the adoption plan, the knowledge article, and the spec (§F.3 lists all eight).

**What is explicitly NOT reopened.** `[audit §G.3]` — *"Not reopened."* `EVA-16` is ratified;
**`DAT-04` already establishes that dataset expansion does not by itself re-authorize or reverse an
S0 outcome.** Nothing in this brief is a basis for restarting the encoder initiative. The null result
stands on a **stronger** basis than when it was recorded. Reviving it would require commissioning new
evidence under a new owner decision, exactly as the spec's status section already says.

### 2.4 The "real held-out" interpretation of `collected_v4` — summary of standing

| Claim | Standing after the audit |
|---|---|
| `collected_v4` is real / collected / user-authored | **Withdrawn.** Confidence **High**; two innocent explanations tested and excluded `[audit §E.6]` |
| Whether a *human* wrote the surface strings on top of the quota scaffold | **`[open]`.** The repository cannot answer it. `[audit]`: the distinction barely matters for evaluation — either way the file is not a sample of student behaviour |
| `collected_v4` is held out from S0 training | **Stands.** Mechanically clean; 0 leakage under the strictest normalisation tested |
| That hold-out constitutes evaluation hygiene | **Withdrawn.** *"Mechanically clean, semantically void"* — a clean split between two authored corpora measures generator-transfer, not generalization |
| The `_merge_seed.py` merge into the production seed | **Stands as a fact, and is the structural error to not repeat**: rows were held out *after* merging, not before |

---

## 3. Prerequisites that are not policy decisions

**Two of the audit's open items are owner recall, not judgement — likely minutes of effort — and they
change the content of four DFDs below.** They are listed ahead of the decision table because
answering them first is strictly cheaper than deciding around them.

| # | Question | Audit ref | Why it comes first |
|---|---|---|---|
| **P-1** | **How was `collected_v4.csv` actually produced?** Was the surface text written by a person, by a model, or assembled from a template? | `OD-2`, `§K.2` — *"open, **highest value**"* | Determines whether DFD-1 declares the corpus **authored** or merely **unverified**; determines whether DFD-6's "generation has been run three times" is three or two; and determines whether any of the 205 rows can be salvaged as label-gold under DFD-3 |
| **P-2** | **What produced the 189 rows between `_balanced.csv` and `uniform`** (136 of which ship)? | `OD-3`, `§K.1` — *"recall is faster than forensics"* | Names the one provenance field whose absence caused R-5, which is the concrete input to DFD-5. Also settles J-3: keep, relabel, or remove |

`[analysis]` If either answer is *"I don't remember"*, that is itself a usable answer — it converts
the question from recall into a disposal decision (DFD-1 Option B, and J-3), and it removes the
option of waiting for it.

**A third prerequisite is a genuine decision, and it sits upstream of DFD-2** rather than inside it:

| # | Question | Audit ref |
|---|---|---|
| **P-3** | **Is the taxonomy itself reopened?** Two of the three largest label transitions name **retired** classes (`OnTap→NhacNho` 105, `KiemTraThuongXuyen→NhacNho` 29) | `OD-6`, `§J-2` |

`[analysis]` P-3 must be settled **before** DFD-2, not with it. An annotation guideline written
against a five-class taxonomy that then changes is discarded work, and the disagreement data says the
collisions are partly *about* the taxonomy — not about annotator care.

---

## 4. Owner decision table

Nine decisions. For each: the evidence, why it matters, the realistic options, their trade-offs, and
what follows from choosing each. **No option is recommended.** There is deliberately no
"recommended" column.

Where the audit already listed an owner decision, it is mapped rather than duplicated:

| Audit `OD` | Where it lives here |
|---|---|
| `OD-1` — eight documents asserting "real" | **DFD-1**, lifecycle-correction half |
| `OD-2` — how `collected_v4` was made | **P-1** (prerequisite, §3) |
| `OD-3` — what produced the 189 rows | **P-2** (prerequisite, §3) |
| `OD-4` — ViLexNorm `NC` compatibility | **DFD-7** |
| `OD-5` — raise the instrumentation gap as a defect now | **DFD-9**, urgent half |
| `OD-6` — reopen the taxonomy | **P-3** (upstream of DFD-2, §3) |

---

### DFD-1 — Real Data Policy

**The question.** Does the project formally declare: *the repository currently contains no verified
production or user-authored Smart Parser dataset* — and if so, what receives lifecycle correction?

**Evidence.** R-1, R-2, R-7. Verified-real count is `0` in all five classes `[audit §C.1]`. Seven
regularities plus an exact quota match `[audit §E.6]`, confidence **High**. No production telemetry
in the repository; the app DB is untracked `[audit §A]`. Eight documents assert the opposite
`[audit §F.3]`, and two *correction* annotations state a mechanism that is chronologically impossible
(§2.1) `[verified]`.

**Why it matters.** Every other decision's wording depends on this one. A Data Maturation proposal
that opens with "expand the real corpus" is describing an expansion of zero. And the declaration is
what makes the failure *visible in the artifacts* rather than only in a report nobody re-reads.

**Options.**

| Option | What it says | Trade-off | Consequence |
|---|---|---|---|
| **A — Full declaration** | *"The repository contains no verified real dataset. `collected_v4` is authored, not collected."* | Strongest and unblocks everything downstream. Slightly overstates if P-1 reveals a human wrote the surface strings — though `[audit §E.6]` holds that distinction immaterial for evaluation | Lifecycle correction to all eight documents in `[audit §F.3]` **plus** the two wrong-mechanism annotations (§2.1). Roadmap M8-A row, spec §6.1/EVA-03, `SPLIT.md`, pilot report, adoption plan, knowledge article, fixtures datasheet, `build_fixtures.py` |
| **B — Provenance-unverified declaration** | *"No dataset in the repository has a verified collection record; `collected_v4`'s origin is unverified."* | Precise and defensible without P-1. Weaker: leaves eight documents saying "real" unless separately corrected | Same document list, milder wording. Keeps P-1 open as a live question rather than closing it |
| **C — Defer until P-1 answers** | No declaration yet | Costs nothing today | Stalls DFD-3, DFD-4, DFD-6 — all three need to know whether existing material can serve as any kind of ground truth. **The eight documents keep propagating "real" while deferred** |
| **D — No declaration** | Status quo | — | `[analysis]` The audit's central finding lives only in a report. The mechanism that produced R-7 stays fully intact |

**How correction would be done, if authorized.** This repository's convention `[verified —
`docs/reports/README.md`]`: **append a dated amendment, mark the superseded passage in place, never
rewrite the original into a cleaner story.** For specs and the roadmap that means annotating the
line, not deleting it. This brief lists what would be corrected; it does not correct anything.

---

### DFD-2 — Annotation Governance

**The question.** Is a canonical annotation specification established *before* any further labelled
data is collected or generated?

**Predecessor: P-3 (§3).** If the taxonomy is reopened, a guideline written first is discarded.

**Evidence.** R-3: 208 of 703 rows (29.6%) disagree between passes inside a shared label space;
`Difficulty` 16.2%. **No guideline, no adjudication record, no annotator identity for either pass** —
established by exhaustive grep `[audit §E.1]`. Largest collisions: `OnTap→NhacNho` 105,
`KiemTraThuongXuyen→NhacNho` 29, `ThiCuoiKy→BaiTap` 26 — **two of the three name retired classes**
`[audit §J-2]`. Within-file the corpora are clean (0 conflicting labels in 168 duplicate groups); the
instability is strictly *between* passes.

**The audit's one hard dependency:** *"`G-1` (guideline + adjudication) precedes every intervention
that adds rows. Everything else is a sequencing choice; this one is a dependency."* And `[audit
§I.1]`: **public data is blocked behind the annotation guideline that does not exist**, because no
public corpus carries this label space, so every imported row needs a fresh label.

**Why it matters.** `[audit §E.1]`: *"This is the ceiling nobody has priced. Whatever accuracy a
future model reports, it is graded against labels of this stability."* Adding rows at the current
label quality multiplies noise rather than signal — for collected, generated, and imported rows alike.

**What a canonical specification would have to contain**, from what the disagreement data shows is
actually contested:

| Component | Why the evidence demands it |
|---|---|
| **Taxonomy definition** | P-3. The retired classes are inside the largest transitions |
| **Class boundaries** | `ThiCuoiKy` vs `BaiTap` (26 rows) and the `NhacNho` attractor (150 rows across three sources) are boundary failures, not carelessness |
| **Ambiguous-example catalogue** | `[audit §D.4]`: ambiguity *is* what the 29.6% is, and it is measurable only by re-annotation |
| **Adjudication protocol** | 208 rows have two opinions and no tiebreak. See **DFD-8** — inseparable |
| **Label provenance** | Who or what assigned each label, under which guideline version. See **DFD-5** |
| **Guideline versioning** | Two passes exist with no version identity; a third would be indistinguishable |

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Full canonical spec first** (taxonomy + boundaries + ambiguity catalogue + adjudication + versioning) | Highest up-front judgement cost; needs no infrastructure. `[audit]` calls curation *"the cheapest high-value work in the whole set"* | Every later intervention becomes spendable. Blocks nothing permanently — it *is* the blocker being cleared |
| **B — Minimal guideline, five production classes only**, taxonomy question deferred | Faster; leaves the largest transitions (which involve retired classes) undocumented | Partial unblock. `[analysis]` Risk: the guideline is silent on exactly the boundaries that failed |
| **C — Guideline written jointly with the first collection round** | Guideline is grounded in real examples rather than recalled ones | Violates the audit's single stated dependency: rows get labelled before the rules exist |
| **D — No guideline** | Zero cost now | `[audit §J]`: *"Labelling anything before the guideline exists reproduces `G-1` at greater volume."* Status quo, at scale |

---

### DFD-3 — Gold Dataset (label-correctness foundation)

**The question.** Is a small human-verified Gold set established as the foundation for future
evaluation — and can it come from existing material?

**Read DFD-3 and DFD-4 as two axes, not two sizes of the same thing.**

| Axis | DFD-3 (Gold) | DFD-4 (Held-out real) |
|---|---|---|
| Guarantees | **Label correctness** | **Provenance — real origin** |
| Can existing material supply it? | **Yes** — a human can re-label rows that already exist | **Structurally never.** No amount of re-labelling makes an authored row real (R-1, R-2) |
| Blocked by | DFD-2 (a label is only "verified" against a guideline) | Collection capacity; DFD-1; DFD-5 |

`[analysis]` If one artifact is authorized as though it solved both, the project will believe it has
a real evaluation set when it has a correctly-labelled authored one. **They are separate
authorizations.**

**Evidence.** The 208 disagreeing rows are **already identified** `[audit §E.1]` — the expensive part
(finding them) is done, and `[audit §J-1]` scopes the adjudication at exactly 208 rows. Within-file
label consistency is clean. Two classes have **zero** evaluation rows `[audit §C.1]`, so any gold set
built by random sampling would inherit that hole.

**Why it matters.** Without a set of labels somebody stands behind, no future model comparison has a
denominator. `[analysis]` A label-gold set is also the only artifact that could retroactively *measure*
R-3 — it converts "29.6% disagreement" into "here is which pass was right."

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Label-gold sampled from existing material**, every label human-reviewed under the DFD-2 guideline, and **explicitly labelled "not real"** | Available immediately after DFD-2. Cheapest path to a trustworthy denominator | Usable for label-quality regression and for adjudicating R-3. **Cannot** support a generalization claim. Already consumed by the shipped model (which trains on all 903 with no split), so it serves only *future* models trained with a hold-out |
| **B — Gold only from newly collected real data** | Solves both axes at once | Blocked behind DFD-4 and collection capacity. Nothing usable in the interim |
| **C — Two-tier: label-gold now, real-gold later** | Two artifacts to govern, and a naming discipline to maintain so nobody conflates them | `[analysis]` Preserves the axis separation *by construction* — the failure mode in R-7 was one artifact wearing two descriptions |
| **D — No gold set** | — | Every future accuracy figure is graded against labels of ~29.6% stability, indefinitely |

**Size is not decided here.** `[audit]` supports exactly two structural constraints, and no number:
the adjudication population is **208 rows**, and any gold set must be constructed **class-wise**
rather than by random sample, because two classes are at zero.

**Separation from training.** Whatever is chosen must be held out **before** any merge — the specific
error `_merge_seed.py` already made once `[audit §G-2]`.

---

### DFD-4 — Held-Out Real Evaluation Set (provenance foundation)

**The question.** Is a protected real-data evaluation set created before any future model comparison?

**Evidence.** R-4: three independent blocks, any one sufficient `[audit §F.1]`. `DAT-03` already
names *"build a stronger held-out evaluation set"* as in-scope for this workstream `[verified — spec
§9]`. `DAT-01` remains in force: **no claim of general production accuracy may be made from the
current 3-of-5-class dataset** — and `[audit §G.3]` sharpens it to 0-of-5 with real data.

**Why it matters.** This is the artifact that would let the project answer *"does a new model
generalize to unseen real student input?"* — currently **no**, on three grounds. It is also the only
thing that could ever lift `DAT-01`.

**Required properties, from the evidence.**

| Property | What the evidence requires | Source |
|---|---|---|
| **Real origin** | A recorded act of collection — the thing whose absence made R-2 undetectable for two months | `[audit §E.6, §B.1]` |
| **Held out before merge** | Reserved *before* any merge into a training seed | `[audit §G-2]` |
| **Immutable once frozen** | Hash-pinned. The `SPLIT.md` SHA-256 pin worked correctly and is the precedent to reuse | `[audit §E.7]` |
| **Row-level provenance** | Per DFD-5. `Source` is not sufficient (R-6) | `[audit §B.1]` |
| **No training contamination** | Structural, not just row-level: `[audit §F.2]` notes the current split is clean by row but contaminated **by construction** — both halves descend from one authoring effort against one spec |
| **All five classes** | Currently 3 of 5. The two missing are the two most made of untraceable rows | `[audit §C.1]` |
| **Representative linguistic phenomena** | **Cannot yet be specified numerically.** Five phenomena are `[unknown]` (§1.2) and the real distribution is unobserved. `[analysis]` "Representative" is currently undefinable — which is an argument for collecting first and characterising after, not for a coverage quota | `[audit §D.4, §C.2]` |
| **A distributional acceptance check** | One that would have caught §E.6's seven regularities before the corpus was trusted | `[audit, Data Maturation Inputs]` |

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Instrument the app and accrue** | Rows arrive free once wired; genuinely real; slowest | Depends on DFD-9 and on the app being used. `[audit]`: the study-time gate (`MinRows = 50`) has **never** been met, so accrual rate is an open question at this usage level |
| **B — Bounded collection exercise with real students** | Fastest route to real rows; needs recruitment, consent, and a collection record | Must not be run before DFD-2, or it reproduces R-3 at new volume |
| **C — Both** | Highest cost; the two sources are independent, which is itself evidence value | `[analysis]` Two independent real sources would let the project check one against the other — the check nothing in the corpus currently permits |
| **D — Defer** | — | `DAT-01` cannot be lifted. No claim about the shipped classifier is measurable |

---

### DFD-5 — Provenance Policy

**The question.** Must every future row carry machine-readable lineage metadata?

**Evidence.** R-5, R-6. Three provenance breaks `[audit §B.1]`. 136 untraceable rows (15.1% of the
production corpus) present in no other committed file, concentrated in the two unmeasurable classes.
`vn_input_fixtures` is **the only source in the repository with a datasheet** — a working precedent.
`DAT-03` names *"version datasets"* explicitly `[verified — spec §9]`.

**Why it matters — and why it is time-sensitive.** `[analysis]` Provenance shares the irreversibility
property with the instrumentation gap in DFD-9: **it cannot be added to a row after the row exists.**
R-5 is not a bug that was introduced; it is the absence of a field, observed later. Every row added
before this decision joins the untraceable population.

**Minimum fields the evidence supports** — stated as *what each field would have prevented*, not as a
schema:

| Field | Which finding it would have prevented |
|---|---|
| **Origin / collection event** (not a filename) | R-6 — `Source` names the first file a row appeared in |
| **Provenance type** — collected / derived / generated / imported | R-2 — a generated file could not have been labelled "collected" in eight documents |
| **Generation process + generator identity/version** | R-5 — 136 rows whose producing step is not in the repository |
| **Label source** — who or what assigned the label | R-3 — no annotator identity for either pass |
| **Annotation guideline version** | R-3 — two passes with no version identity |
| **Dataset version** | `[audit §G-9]` — no dataset versioning exists |
| **License** (imported rows only) | DFD-7 — two of three candidates surface no licence at all |

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Row-level lineage columns**, mandatory on every new row | Highest discipline; touches every ingest path. Cannot be backfilled for the existing 903 | Prevents R-5's recurrence outright. `[analysis]` Existing rows would carry an explicit `unknown` rather than a silently misleading `Source` |
| **B — File-level datasheet only**, one per source | Cheapest; follows the `vn_input_fixtures` precedent that already works | Catches R-2 (a datasheet would have had to state how `collected_v4` was collected). **Does not** catch R-5, where rows from an unknown process were merged into a file that did have provenance |
| **C — Both tiers** | Most work | The only option the evidence shows covers both failure modes, since they failed at different granularities |
| **D — No policy** | — | The mechanism `[audit, Recommendation]` describes as *"still in place and would allow it again"* stays in place |

**P-2 (§3) sharpens this decision** by naming which field would have caught the 189 rows. It does not
block it.

---

### DFD-6 — Synthetic Data Policy

**The question.** Is synthetic generation prohibited, bounded, or unrestricted — and under what
conditions?

**Evidence — and the audit's explicit caution.** `[audit, Data Maturation Inputs]`: *"Not ruled out;
not assumable; and this project's track record with it is poor. It has been run three times
(`synthetic_v3`, the untraceable 136, `v4`) and the third was described as real in eight documents
for two months."* And `[audit §H]`: **generation against the current label definitions makes `G-1`
worse, leaves `G-3` untouched**, and risks producing another corpus described as real.

**Why it matters.** `[analysis]` Synthetic generation is the intervention most likely to be reached
for first — it is the only one available to a solo developer without recruiting anyone — and it is the
one the audit warns most specifically about. The decision is not *whether generation is bad*; it is
*what would have to be true before a generated row is allowed to count*.

**Sub-decisions the evidence supports putting to the owner:**

| # | Sub-decision | What the evidence says |
|---|---|---|
| 6a | **Prohibited in Gold and evaluation sets?** | `[audit]` lists *"never in an evaluation set"* among its bounds. R-2 is what happens when this is not stated |
| 6b | **Allowed for Silver/training augmentation only?** | `[audit]` allows it *after* `G-1`, i.e. behind DFD-2 |
| 6c | **Only against measured coverage gaps?** | **A trap worth seeing.** The coverage gaps are currently *not measured* in the direction that matters: five phenomena are `[unknown]` `[audit §D.4]` and the real class distribution is `[unknown]` `[audit §C.2]`. `[analysis]` "Generate against measured gaps" is therefore itself blocked behind DFD-4/telemetry, not merely behind DFD-2 |
| 6d | **Must carry generator provenance?** | `[audit]`: *"labelled at generation time in a way `Source` cannot lose"* — this is DFD-5, restated for generated rows specifically |
| 6e | **Independently validated before entering training?** | `[audit]`: *"held to a distributional check that would have caught §E.6's seven regularities"* |

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Prohibited entirely until DFD-2 closes** | Removes the fastest-feeling intervention from the table; costs nothing the evidence values | `[analysis]` Consistent with the audit's single hard dependency, applied to generation like any other row-adding intervention |
| **B — Allowed for training only, with 6a/6d/6e binding and 6b/6c as stated bounds** | Preserves a real capability; requires building the distributional check before the first generated row | The audit's own bounded description. Note 6c cannot be satisfied yet (see above) — so B in practice means "training augmentation without a coverage target" until DFD-4 lands |
| **C — Unrestricted** | — | Status quo. `[audit]`: the project has run this three times, and *"the third one is the reason nobody noticed the first two"* |

---

### DFD-7 — Public Dataset Policy

**The question.** May external datasets be **evaluated as candidates** — and under which of four
distinct conditions?

**The four things the audit insists on keeping separate** `[audit §I.3]`:

| Availability | Relevance | Licensing metadata | **Permission to use** |
|---|---|---|---|
| Publicly downloadable | Fits the task/population | What the card states | An owner/legal ruling |

*"Repository visibility is not a licence. A dataset card is not legal clearance. No dataset here is
approved."*

**Evidence — the three verified candidates** `[audit §I.2, dataset cards read 2026-08-25; no data
downloaded]`:

| Dataset | Relevance | Licensing metadata | Blocker |
|---|---|---|---|
| **ViLexNorm** — Vietnamese lexical normalization, >10,000 human-annotated sentence pairs, EACL 2024 | **Highest** — an *instrument* for the phenomena §D.4 cannot measure (teencode, abbreviations, phonetic misspellings, code-switching). Used as a normalisation resource, **not** as training data | **`CC BY-NC-SA 4.0`**, stated explicitly | **`NC` = non-commercial.** A blocker if this application is ever distributed commercially — `OD-4`, a licensing question, not a data question |
| **UIT-VSFC** — Vietnamese Students' Feedback Corpus, 16,175 rows | Medium: right population (Vietnamese students), wrong task (sentiment/topic) | **No license field on the card at retrieval time.** "Free for research" in third-party sources is **not a license** | Full relabel; large register mismatch (feedback prose ≠ task-entry text) |
| **PhoATIS** (via `VinAIResearch/JointIDSF`) | Low-medium: right task shape (intent + slots), wrong domain entirely (airline travel) | **No license surfaced**; a citation request is not a license | Maximal domain distance. Value is as a format/methodology reference |

**The structural conclusion that needs no dataset name** `[audit §I.1]`: **no public Vietnamese
corpus carries this project's five task-type labels.** The label space is specific to this
application's domain model. Therefore *every* public-data path is *"acquire linguistic diversity,
then relabel from scratch"* — which lands directly on DFD-2. **Public data is blocked behind the
annotation guideline that does not exist.**

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Authorize candidate *evaluation* only** (read cards, run a licensing review; no ingestion) | Cheap, reversible, produces the licensing answers before they are needed | Nothing enters the repository. `OD-4` gets answered on its own timeline |
| **B — Authorize ViLexNorm as a normalisation instrument**, subject to an `NC` ruling | Highest-value single item — it is the instrument for the five `[unknown]` phenomena | Requires answering `OD-4` first. `[analysis]` Note this use is as a *tool*, not as training rows, so it is the one public-data path that does **not** land on DFD-2 |
| **C — Prohibit external data entirely** | Removes a licensing surface and a relabelling burden | Forfeits the only available instrument for measuring typos/slang/normalisation |
| **D — Defer until DFD-2 closes** | Matches the audit's stated ordering for corpora | `[analysis]` Over-applies it to Option B: using ViLexNorm as an instrument does not add labelled rows, so the DFD-2 dependency does not bind that use |

---

### DFD-8 — Human Authority

**The question.** Where is human judgement mandatory — and how does a solo developer retain
trustworthy ground truth without labelling everything?

**Inseparable from DFD-2.** `[analysis]` A guideline with no named adjudicator is not a guideline;
an adjudicator with no guideline is a second annotation pass, which is exactly how R-3 was produced.
They are one decision with two halves and should be ruled on together.

**Evidence — where the audit says authority cannot be delegated** `[audit §J]`:

| # | Area | Why a human | Volume |
|---|---|---|---|
| **J-1** | Adjudicate the 208 disagreeing rows | No third opinion exists; an automated tiebreak encodes whichever pass the model was trained on | **208 rows** |
| **J-2** | Define the class boundaries that actually collide | Two of the three largest transitions name retired classes — part of this is the taxonomy question (P-3) | Guideline |
| **J-3** | Dispose of the 136 untraceable rows | Keep / relabel / remove — a judgement about acceptable provenance in a shipped model | Decision, then ≤136 rows |
| **J-4** | Rule on `collected_v4` | Whether it stays in the seed, stays in the evaluation split, or is reclassified. Eight documents move with the answer | Decision |
| **J-5** | Relabel any imported public data | No public corpus carries this label space | Scales with import |
| **J-6** | Culturally specific slang and abbreviations | *"A non-Vietnamese-speaking annotator or a general-purpose model cannot reliably distinguish an abbreviation from a typo."* ViLexNorm reduces but does not remove this | Ongoing |
| **J-7** | Rows where adjudication is itself contested | Need an owner ruling to become ground truth | `[open]` until J-1 runs |

**Ordering constraint** `[audit §J]`: **J-2 gates J-1, which gates J-5.**

**Where leverage genuinely exists** `[analysis]`, so the solo-developer constraint is designed around
rather than ignored:

- **The expensive half of J-1 is already done.** The 208 rows are *identified*. What remains is
  ruling, not searching.
- **Propose/dispose splits cleanly.** A model can pre-label and can flag disagreement; per J-1 it
  cannot break a tie, and per J-6 it cannot reliably tell a Vietnamese abbreviation from a typo.
- **Volume scales with what is admitted, not with what exists.** J-5 and J-3 are bounded by DFD-7
  and DFD-6 choices — restricting intake is itself a labelling-cost decision.

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Owner is sole labeller for Gold; model-assisted pre-labelling permitted for Silver/training only** | Highest trust, bounded volume (Gold is small by construction) | Gold throughput is owner-limited. Silver carries model-origin labels that DFD-5 must record |
| **B — Owner adjudicates contested rows only; model labels the rest** | Lowest owner effort | `[analysis]` Contested-ness is decided by the model, so the owner never sees rows the model was confidently wrong about — the failure mode `[audit §E.8]` calls *"suspiciously easy examples"* |
| **C — Owner defines the guideline and adjudicates; an external Vietnamese-speaking annotator does volume** | Scales; satisfies J-6 | Introduces a second annotator — the exact configuration that produced R-3, and therefore only safe *after* DFD-2 |
| **D — No boundary declared** | — | Status quo: authority is wherever the next session assumes it is |

---

### DFD-9 — Telemetry as a Future Real-Data Source

**This decision splits, and only one half can wait.** Treating it as one item is how the urgent half
gets buried.

#### 9a — The urgent half: prediction instrumentation (`OD-5`, gap `G-7`)

**Evidence.** `FocusViewModel` writes `PredictedMinutes = null` and `Confidence = null` while
capturing `WasMlPrediction` `[audit §E.4, cited to `FocusViewModel.cs:151-153`]`. **Confirmed defect.**
*"Prediction error and calibration are not computable from telemetry, no matter how many rows
accumulate. Fixing this after the fact is impossible."* `[audit §H]` ranks it: *"Unlike every other
gap, **delay makes it strictly worse** — each day writes rows that can never answer the question."*

`[analysis]` This also bears on the deferred M8-A confidence-gate anomaly — the shipped 0.60 gate
sitting above a bin with **0.000** observed accuracy (S0 finding F-1, currently deferred). **That
defect is not investigable on real data without this instrumentation.** The two are connected in one
direction only: fixing 9a does not fix the gate, but leaving 9a unfixed keeps the gate permanently
unmeasurable outside authored corpora.

**Options.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Raise now as a shipped-code defect**, separately from Data Maturation | Small and isolated; the fix touches one write path. Costs a context switch away from the data workstream | Rows written from the fix onward can answer prediction-error and calibration questions. Rows already written cannot, at any volume — that loss is already incurred and stops growing |
| **B — Fold into the Data Maturation proposal** | Keeps the workstream tidy; one plan instead of two | `[analysis]` Ties an hours-scale fix to a proposal that is itself blocked behind DFD-2 and P-1/P-2. Every day of that wait writes permanently unanalysable rows |
| **C — Defer** | Zero cost today | Same failure mode as B, without an end date. `[audit §H]`: *"delay makes it strictly worse"* |

`[analysis]` B and C differ from each other only in duration, not in kind. A is the only option whose
cost of delay is bounded.

#### 9b — The deferrable half: existing telemetry as a future data source

| Table | Standing | What it could become |
|---|---|---|
| `DifficultyLabelLogs` | Written, **never read**. Debug 3 / Release 17, 12 overrides `[audit §A C2]` | `[analysis]` **The only genuinely real supervised labels in the project.** They are human difficulty judgements from actual use. n=17 is indicative only and must not be cited as a rate. Compounded by `[audit §E.2]`: the `Difficulty` column is never trained on anyway — the signal is discarded at both ends |
| `StudyTimeOutcomeLogs` | Gate `MinRows = 50` never met (0–2 observed) → predictor always falls back to 180 RNG-labelled rows over 3 feature vectors `[audit §E.3, §C1]` | Real study-time regression targets. `[analysis]` Accrual rate at current usage is an open question: the gate has never been met since M7 shipped |
| `WeightChangeLogs`, `UserStatsSnapshot` | Rule-engine inputs, not training data | Out of scope for this decision |
| `OptimizerRunLogs` | **Empty is correct** — not a data gap | — |
| `StudyLogs` (5404 rows) | **Generated demo data, not real usage** `[audit §E.7]` | Not a candidate. Recorded so it is not mistaken for a corpus |

**Privacy and provenance.** `[analysis]` Telemetry is real user text and real behaviour — which is
exactly why it is valuable and exactly why it carries obligations the CSV corpora never did. The app
DB is untracked and lives beside the executable `[audit §A]`. If telemetry is ever designated a
training or evaluation source, consent, retention, and — critically — **DFD-5 metadata applied at
write time, not at import** become preconditions. Retrofitting provenance onto accrued telemetry
reproduces R-5 with real data in it.

**Scope guard.** This is not a telemetry implementation plan. The decisions here are: *is telemetry
formally designated a future real-data source*, and *is 9a raised now*.

**Options for 9b.**

| Option | Trade-off | Consequence |
|---|---|---|
| **A — Designate both tables as future real-data sources**, with DFD-5 provenance and privacy preconditions attached now | Widest future option value; requires the preconditions to be settled before rows accrue, so it partly depends on DFD-5 landing first | Both signals become admissible later. `[analysis]` The designation is what makes anyone check that rows are being written with provenance *while* they accrue, rather than at import |
| **B — Designate `DifficultyLabelLogs` only** | Narrowest and cheapest: it already holds real human labels, whereas the study-time gate (`MinRows = 50`) has **never** been met | `[analysis]` Cheap but currently supports nothing quantitative — n=17 is indicative only `[audit §A C2]`. Its value is that it stops a real supervised signal being discarded at both ends (`G-8`), not that it enables a measurement today |
| **C — Defer until DFD-4 defines what a real evaluation set is** | Avoids designating a source before the standard it must meet exists | Low cost **provided DFD-5 lands first.** Otherwise rows accrue without lineage and this quietly builds a second untraceable population — R-5 again, this time out of real user data |
| **D — No designation** | — | The only real signals the project captures stay outside every data plan. `DifficultyLabelLogs` continues to be written and never read |

---

## 5. Prioritization and recommended decision order

### 5.1 Ranking criteria applied

**Ranking is not choosing.** The order below says *which decisions must be settled first*; it says
nothing about which option within each decision is preferable. §4 deliberately offers no
recommendation, and this section does not smuggle one in.

Rows are listed in the order §5.2 concludes, so the two tables agree.

| Decision | Blocking power | Impact on model validity | Reversibility | Cost | Owner-policy dependence |
|---|---|---|---|---|---|
| **P-1, P-2** (recall) | Sharpen 4 DFDs | Indirect | n/a | **Minutes** | Total — only the owner can answer |
| **P-3** (taxonomy) | Gates DFD-2 | High | Expensive to reverse once labelling starts | Judgement | **Total** |
| **DFD-1** | Gates the wording of DFD-3/4/6, **and protects the process while the rest is decided** | High — stops the "real" propagation at the source | Reversible | Low (a declaration); medium (the corrections) | **Total** |
| **DFD-2 + DFD-8** | **Highest** — the audit's one hard dependency; gates every row-adding intervention | **Highest** — sets the ceiling on every future accuracy figure | Reversible (a guideline can be revised and versioned) | Judgement, no infrastructure | High |
| **DFD-5** | Gates every row added after today | High — provenance is what makes a later claim checkable | **Irreversible if delayed** — cannot be backfilled | Low–medium | Medium |
| **DFD-9a** | Blocks nothing | Medium — enables calibration measurement, incl. the deferred gate anomaly | **Irreversible if delayed** — each day writes unanalysable rows | Small, isolated | Low (a defect call) |
| **DFD-3** | Blocked by DFD-2 | High | Reversible | Medium (bounded: 208 rows) | Medium |
| **DFD-4** | Blocked by DFD-1, DFD-5 | **Highest** — the only thing that could lift `DAT-01` | Reversible, but expensive to redo | **Highest** | High |
| **DFD-6** | Blocked by DFD-2 | Medium (can make things worse) | Reversible | Low | High |
| **DFD-7** | Blocked by DFD-2 (corpora) — *not* for instrument use | Medium | Reversible | Low to evaluate; `OD-4` is legal | **Total** for `OD-4` |

### 5.2 The minimum decision set

**Before a Data Maturation proposal can be written:**

| Order | Item | Why it is in the minimum set |
|---|---|---|
| **1** | **P-1, P-2** — owner recall | Minutes of effort; changes the content of DFD-1, DFD-5, DFD-6 and DFD-3. Cheaper to answer than to design around |
| **2** | **P-3** — is the taxonomy reopened? | Gates DFD-2. A guideline written against a taxonomy that then changes is discarded work |
| **3** | **DFD-1** | Cheapest item in the set, fully reversible, and its only dependency is P-1. **It protects the process while items 4–6 are decided** — deferring it means a proposal could be drafted while eight documents still assert the corpus is real, which is the recurrence this brief exists to prevent |
| **4** | **DFD-2 + DFD-8** (ruled together) | The audit's **only** hard dependency: *guideline + adjudication precedes every intervention that adds rows*. Nothing else can be spent well first |
| **5** | **DFD-5** | Irreversible if delayed. Every row added before it becomes another untraceable population |
| **6** | **DFD-9a** | Irreversible if delayed, small, and independent of everything above — it can proceed in parallel with items 3–5 |

**Six items. DFD-3, DFD-4, DFD-6, DFD-7 and DFD-9b are not in the minimum set** — not because they
are unimportant (DFD-4 has the highest impact of any item here), but because each is *blocked by*
something in the set, so deciding it first would be deciding it without the information that
determines the answer.

`[analysis]` Items 1, 2 and 5 are hours of work, not weeks. Item 3 is the substantive one, and the
audit describes it as *"the cheapest high-value work in the whole set — it needs judgement, not
infrastructure."*

---

## 6. Decisions that can wait

Deferring these is a defensible choice; each row says what waiting costs.

| Item | Why it can wait | What deferral costs |
|---|---|---|
| **DFD-3** (Gold) | Cannot be executed before DFD-2 defines what a "verified" label means | Nothing, provided DFD-2 moves. If both stall, no future model has a trustworthy denominator |
| **DFD-4** (held-out real) | Highest impact but also highest cost, and depends on DFD-1 + DFD-5 for its properties | `DAT-01` stays in force; no claim about the shipped classifier is measurable. **This is a real ongoing cost, not a free deferral** |
| **DFD-6** (synthetic) | `[audit]` places generation behind `G-1` regardless | Low — as long as no generation happens in the interim. If it does, the deferral has cost the whole thing |
| **DFD-7** (public data) | `[audit §I.1]` places corpora behind DFD-2 | Low. **Except `OD-4`** (the ViLexNorm `NC` ruling), which is cheap, independent, and answering it early removes an unknown from DFD-4 planning |
| **DFD-9b** (telemetry as a source) | Rows are accruing (slowly) regardless; the designation can be made later | Low — **provided DFD-5 lands first**, so that rows written from here on carry provenance. Otherwise deferral quietly builds a second untraceable population, this time out of real data |
| **`G-10`** duplicate inflation in `normalized_dataset*.csv` | Already removed downstream; labels are consistent | Negligible. Matters only if those files are used again |
| **`G-11`** unused `TaskName` (477 distinct / 903) | Latent capability, not a gap | None |
| **`G-12`** `_balanced.csv` has no live consumer | **A negative entry: do not act on it.** It is the lineage intermediate — deleting it severs the only link between the shipped seed and its root | Deleting it would be irreversible |

**Not on this list, deliberately: DFD-9a.** The instrumentation gap looks like a small telemetry item
and is the one thing in this brief whose cost of delay is unrecoverable. It belongs in §5, not here.

---

## 7. What a future Data Maturation proposal must not assume

Each line is a claim the evidence no longer supports. A proposal that assumes any of them inherits
the failure this audit found.

**About the data:**

1. **That any row in the repository is real.** Zero verified real rows exist, in every class `[audit §C.1, §E.6]`.
2. **That corpus size is the constraint.** `[audit §H]`: *"The constraint is not corpus size, and it never was."*
3. **That `Source` is provenance.** It names the first file a row appeared in `[audit §B.1]`.
4. **That any label is correct.** ~29.6% inter-pass disagreement, no guideline, no adjudicator `[audit §E.1]`.
5. **That balance is desirable.** The corpus is balanced to 1.11× against a distribution never observed; if the real distribution is skewed, balancing moved the prior *away* from production `[audit §C.2]`.
6. **That the `collected_v4` rows can be repaired into an evaluation set.** The problem is provenance, and re-labelling does not create it.
7. **That `_balanced.csv` is a dead branch.** It is the lineage intermediate; deleting it severs the seed from its root `[audit §G-12]`.

**About the evidence:**

8. **That 96.2%, 97.24%/97.25%, or any S0 arm figure is real-world generalization evidence.** None is (§2).
9. **That the correction annotations on the 96.2% figure are accurate.** Their conclusion is right; their stated mechanism is chronologically impossible (§2.1) `[verified]`.
10. **That a clean train/test split is evaluation hygiene.** `[audit §F.2]`: *"Mechanically clean, semantically void."*
11. **That the linguistic phenomenon table describes student behaviour.** Every row compares two authoring processes `[audit §D.1]`.
12. **That the S0 vocabulary gap predicts real-input failure.** It is evidence of authoring inconsistency; the production-is-abbreviation-heavy reading is an explicitly **Low**-confidence hypothesis `[audit §G.3]`.
13. **That data expansion reopens S0.** `DAT-04` is in force and `[audit §G.3]` says *"Not reopened."*
14. **That typo / slang / ambiguity / word-order rates are known.** Five phenomena are `[unknown]`; slang is a lower bound on a 30-token list `[audit §D.4]`.

**About the interventions:**

15. **That synthetic generation is the intervention.** `[audit, Recommendation]`: *"on this project's track record it is the intervention most likely to recreate the problem this audit found."*
16. **That generation can target "measured coverage gaps."** Those gaps are largely unmeasured, and measuring them is blocked behind DFD-4 (§4, DFD-6 sub-decision 6c).
17. **That public datasets are available for use.** Two of three candidates surface **no licence at all**; the third is `NC`. *"Repository visibility is not a licence"* `[audit §I.3]`.
18. **That a public corpus can supply labels.** None carries this label space; every path is *acquire diversity, relabel from scratch* `[audit §I.1]`.
19. **That accrued telemetry is usable as-is.** Prediction and confidence are written as `null`; calibration is not computable from existing rows at any volume `[audit §E.4]`.
20. **That the study-time predictor has a real model.** It trains on 180 RNG-labelled rows over 3 feature vectors, on every run in practice `[audit §E.3]`.

**And one about method:**

21. **That measurements in this brief are this brief's own.** They are not. Everything quantitative here is the audit's, cited to its section. Re-verifying before building on any of it is cheap and is what the audit's own convention exists to make possible.

---

## Verification

**No new measurement over any dataset was performed. No dataset, model, or production file was read
for content, modified, or trained. The only file written is this report.**

| Check | How | Result |
|---|---|---|
| Data Audit read in full | `docs/reports/2026-08-25-data-audit-gap-map.md`, all 1069 lines | Basis for §1, §4, §7 |
| **96.2% chronology** | `git log --diff-filter=A -- datasheets/collected_v4.csv`; `git log -- …/seed_intents.csv`; CHANGELOG entry heading | 96.2% dated **2026-06-05** (`9603c17`, 698 rows); `collected_v4` added **2026-06-18** (`8855874`), merged `ab5112c`. **13-day gap** |
| **96.2% sample-size corroboration** | Arithmetic on the reported n=106 at 85/15 | 698 × 0.15 ≈ 104.7 ≈ 106 ✓; 903 × 0.15 ≈ 135.5 ✗ |
| Wording of the two correction annotations | Read `docs/specs/system_roadmap.md:40` and `2026-08-24-neural-encoder-smart-parser.md:371` | Both state the merge preceded the measurement — contradicted above |
| Citations of the three headline figures | Repo-wide grep for `96.2%`, `97.24`, `97.25` | 17 sites across CHANGELOG, master plan, adoption plan, execution plan, recall eval, spec, roadmap, archived plan, `build_split.py`, `SPLIT.md` |
| Encoder spec §6.1, §9, Status | Read `2026-08-24-neural-encoder-smart-parser.md` | `EVA-02/03/04`, `DAT-01..05` quoted as written; `stopped_at_s0` confirmed |
| S0 macro-F1 results | Read `2026-08-25-encoder-pilot.md` §4 | baseline mean **0.6575**; best encoder **0.6484** (Arm A int8). EVA-16 fired |
| Recall eval verdict + limitations | Read `2026-06-25-m8a-textclassifier-v4-recall-eval.md` in full | §2.2 quotes it as written |
| Report conventions | Read `docs/reports/README.md` | Verdict-first, required sections, evidence-scoping, follow-up column requirements applied |

**Not done, and why:**

- **Nothing in the audit was re-measured.** Re-running its corpus analysis was out of scope for a
  decision brief; the audit's own §Verification records its methods, and this brief's tag convention
  keeps its numbers attributed rather than absorbed.
- **No historical report was amended.** §2 is an impact assessment; whether corrections are made is
  **DFD-1**, an owner decision.
- **`OD-4` was not researched further.** Whether `CC BY-NC-SA 4.0` permits this project's distribution
  is a licensing question, not a data question — the audit says so and this brief does not overreach it.
- **P-1/P-2 were not investigated by forensics.** The audit already found the repository cannot answer
  them and that owner recall is faster.

---

## Follow-ups

**Nothing in this table is committed work.** Each row's status says what it is.

| # | Item | Owner | Where it belongs | Status |
|---|---|---|---|---|
| 1 | Answer **P-1** (how `collected_v4` was produced) and **P-2** (what produced the 189 rows) | Owner | Reply into this brief, or an appended ruling | **Needs owner recall — prerequisite to the minimum decision set** |
| 2 | Rule on **DFD-1 … DFD-9** | Owner | Appended ruling on this report, per the reports-README convention | **Needs a new owner decision** |
| 3 | The 96.2% correction annotations state a chronologically impossible mechanism (§2.1) | Owner | `docs/specs/system_roadmap.md:40`, `2026-08-24-neural-encoder-smart-parser.md:371` — dated amendment, superseded passage marked in place | **Defect candidate (documentation)** — inside DFD-1's scope; not acted on here |
| 4 | The recall eval's *"more reliable estimate"* verdict rests on `collected_v4`-derived test support (§2.2) | Owner | `2026-06-25-m8a-textclassifier-v4-recall-eval.md` — dated amendment | **Defect candidate (documentation)** — inside DFD-1's scope |
| 5 | `PredictedMinutes` / `Confidence` written as `null` (`G-7` / `OD-5`) | Owner | A defect against shipped M8/M7 code, **separate from Data Maturation** | **Defect candidate — delay is irreversible** (DFD-9a) |
| 6 | `FeaturizeText` lowercasing: does the casing gap reach the model? (`[audit §K.4]`, ~1h) | Owner to schedule | Investigation report | **Deferred — cheap, unresolved** |
| 7 | The deferred M8-A confidence-gate anomaly (0.60 gate vs a 0.000-accuracy bin) is not investigable on real data until DFD-9a lands | Owner | Roadmap deferred list, already filed | **Knowledge only** — a dependency now named, no new work proposed |
| 8 | Lessons about provenance-as-a-control and about correction annotations written from assumption | Agent, once decisions land | `docs/knowledge/machine-learning.md` or `ml-experimentation.md` | **Knowledge only — not yet written** |

---

## Decisions made

ADR-style, per the standing convention. Process decisions taken by the author of this brief — **not**
owner rulings, which do not yet exist.

### D-1 — Did not inherit the audit's `[measured]` tags; introduced a separate provenance convention

**Why it had to be made.** The audit's central finding is that a word ("real") propagated from a
request into eight documents, gaining authority at each hop until a spec line carried it as `[fact]`.
A brief that restated the audit's measurements in its own voice would be the ninth hop.

**What it's for.** Every quantitative statement here is tagged `[audit §X]` and is therefore
traceable in one step to the section that measured it. `[verified]` marks only the checks run in this
session, and each is named in §Verification. `[analysis]` marks reasoning, so it can be attacked
separately from the evidence it rests on.

**Experience for future development.** When a document's subject is a provenance failure, the
document's own provenance discipline is part of the deliverable. The cost is a slightly heavier
sentence; the benefit is that a reader who distrusts a number knows exactly where to check it.

### D-2 — Checked the 96.2% chronology instead of restating the existing correction

**Why it had to be made.** Two documents already carried a correction on the 96.2% figure, and the
easy path was to cite them. But the audit's lineage diagram places the measurement at the **v3/698**
stage, while both corrections say it was measured *after* the `collected_v4` merge. Both cannot be
right about the mechanism, and an impact assessment that repeated the wrong one would be building on
an unchecked claim in a brief about unchecked claims.

**What it's for.** `git log` plus one arithmetic check (n=106 fits 698, not 903) settled it in
minutes: the figure predates `collected_v4` by thirteen days. The conclusion those annotations reach
is right; their stated reason is impossible. That is now a named item in DFD-1's correction scope
rather than a latent error two documents deep.

**Experience for future development.** A correction is a claim like any other, and it is *more*
likely to go unchecked than the claim it corrects — it arrives wearing the authority of having already
caught something. Verify the mechanism, not just the verdict.

### D-3 — Mapped the audit's `OD-1..OD-6` into the DFD set rather than presenting two decision tables

**Why it had to be made.** The audit already produced an owner-decision surface. Adding nine more
decisions without reconciling them would have handed the owner two overlapping tables and no way to
know which was authoritative.

**What it's for.** §4 opens with an explicit `OD → DFD` map. Two audit items (`OD-2`, `OD-3`) had no
DFD counterpart and are **owner recall, not policy** — they were promoted to prerequisites in §3
rather than dropped or renumbered as DFD-10. One (`OD-6`, the taxonomy) was placed **upstream** of
DFD-2 rather than inside it, because a guideline written against a taxonomy that then changes is
discarded work.

**Experience for future development.** When a second document adds decisions to a first, the mapping
is not documentation overhead — it is the thing that stops the owner from ruling on the same question
twice with different answers.

### D-4 — Kept DFD-3 and DFD-4 as separate axes, and said so explicitly in both

**Why it had to be made.** "Gold set" and "held-out real evaluation set" sound like the same artifact
at two sizes. They are not: Gold guarantees **label correctness**, held-out-real guarantees
**provenance**. The audit's zero-real finding means existing material can supply the first and can
**never** supply the second, no matter how carefully it is re-labelled.

**What it's for.** If they were presented as one decision, the owner could authorize a Gold set and
reasonably believe the evaluation problem was solved. The brief states the axis separation in both
decisions and gives DFD-3 a two-tier option (label-gold now, real-gold later) that preserves the
distinction by construction.

**Experience for future development.** One artifact wearing two descriptions is precisely how
`collected_v4` became "real held-out" in eight documents. Where two guarantees can be conflated, name
the conflation in the document before someone makes it.

### D-5 — Split DFD-9, because only one half can wait

**Why it had to be made.** The task grouped telemetry into a single decision, and §6 asks for
"decisions that can wait." Telemetry-as-a-future-source genuinely defers. The missing
`PredictedMinutes`/`Confidence` instrumentation does not — the audit says twice that every day of
delay writes rows that can never answer the question. Filed as one item, the urgent half would have
landed in the "can wait" bucket by association.

**What it's for.** DFD-9a (raise the defect now) sits in the minimum decision set in §5; DFD-9b
(designate telemetry a real-data source) sits in §6 with its deferral cost stated. §6 also names the
omission explicitly so the split is not mistaken for an oversight.

**Experience for future development.** Irreversibility, not severity, is what decides whether an item
can wait. Two items of equal severity belong in different buckets when one of them is losing
information while it waits.

### D-6 — Ranked the decisions without recommending any option, and said so in the document

**Why it had to be made.** The task ranks decisions (§4) and forbids solving them (§5). Those
instructions collide in the reader's eye: an ordered list reads as a preference list unless the
document says otherwise.

**What it's for.** §5 opens with *"Ranking is not choosing"*, the decision table has deliberately no
"recommended" column, and every option carries trade-offs and consequences rather than a verdict. The
minimum-set table gives a *reason for inclusion* (blocked-by / irreversible / gates-others), never a
preferred answer.

**Experience for future development.** When a deliverable must be decisive about sequence and neutral
about content, the neutrality has to be stated as a property of the document, not merely practised. A
reader skimming a ranked table will otherwise supply the recommendation themselves.

### D-7 — Named the trap inside "generate against measured coverage gaps"

**Why it had to be made.** DFD-6's sub-decision list, taken from the task, includes *"generated only
against measured coverage gaps"* — which reads as a safe bound. But the audit records five linguistic
phenomena as `[unknown]` and the real class distribution as `[unknown]`. The gaps that matter are not
currently measured, so the bound is unsatisfiable rather than merely strict.

**What it's for.** Stating it means the owner can choose the bound knowing it defers generation
behind DFD-4 as well as DFD-2 — instead of adopting it and discovering later that nothing qualifies.

**Experience for future development.** A safeguard phrased as a measurement precondition is only as
real as the measurement. Check that the instrument exists before writing the constraint that depends
on it — the same lesson the audit recorded when it declined to proxy those five phenomena with
regexes.
