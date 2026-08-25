# S0 Encoder Pilot — evaluation report

**Date:** 2026-08-25 · **Author:** Claude (agent), for owner decision at CP1
**Status:** **COMPLETE — awaiting owner ruling.** All eight EVA-08 outputs measured for all three arms.

**Decides:** whether the neural-encoder initiative proceeds past S0 at all.
**Governed by:** [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) §6 (RATIFIED)
**Executed per:** [`../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md`](../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md) WP-0.1 … WP-0.8
**Protocol pre-registered in:** [`../../tools/ml-pilot/README.md`](../../tools/ml-pilot/README.md) §2 — written **before** any number was measured (PRF-06)

---

## 0. Ruling, in one place

> **The EVA-16 kill criterion fires. Neither encoder arm improves macro-F1 over the baseline — both
> score *below* it — so under the specification the initiative does not proceed to implementation.**
>
> **This is a null result, and PD-3 makes it a complete, valid, successful outcome of S0.** It cost
> one throwaway harness and one report. No production symbol was touched.

**But the result is not one-dimensional, and EVA-08 forbids compressing it into one.** The encoders
lose on classification quality and **win clearly on confidence calibration** — the dimension S3
would have depended on. That split, and one finding about the *existing* production gate that stands
regardless of what the owner decides here, are why §12 and §14 deserve reading before the ruling is
accepted.

---

## 1. Scope

**Covers:** the eight EVA-08 measurements for the **baseline** (current production n-gram
featurizer), **Arm A** (EmbeddingGemma-300M) and **Arm B** (multilingual-e5-small), on one split
constructed once; and the winner / tie / kill ruling that follows.

Both encoder arms were measured in **two precisions** — fp32 and int8 — because reporting one and
inferring the other would be inventing a number. Five encoder configurations in total.

**Does not cover, by requirement:**

- **Arm C** (`hiieu/halong_embedding`) — **not run, and not acquired.** Acquisition is the first step
  of running it (EVA-06). Unlocked only by an explicit owner decision.
- Any claim of **general** production accuracy or generalization (DAT-01) — see §13.
- Any **memory ceiling** (PRF-08) — measured here, derived at S4.
- Any production implementation. **No file under `SmartStudyPlanner/` was created or modified**
  (EVA-01). The split builder reads `seed_intents.csv` read-only.

---

## 2. Machine used (EVA-10, PRF-01, PRF-03, OP-5)

| Field | Value |
|---|---|
| CPU | **Intel Core i7-12700H** (12th gen, H-series), 20 logical processors |
| RAM | 16 GB (16 788 885 504 bytes) |
| Graphics | NVIDIA RTX 4050 Laptop + Intel Iris Xe (integrated) |
| OS | Windows 11 Home Single Language, build 26200 |
| .NET | 10.0.9 · ONNX Runtime **1.29.0** · execution provider **CPU** |
| **Is this the PRF-01 reference class?** | **NO** |

### 2.1 UQ-1 — this is an open owner decision, not a footnote

PRF-01's reference class is a **10th-generation Intel U-series mobile CPU, 8 GB RAM, integrated
graphics**. The machine above is a 12th-generation **H-series** part with 16 GB — materially faster.
**PRF-03 forbids treating a developer-machine-only number as the product floor**, and no PRF-01-class
machine was available to this session.

**What is unaffected.** Outputs **6** (tokenization viability) and **8** (packaged size) are
hardware-independent: token ids are a function of the vocabulary and the input string, and file sizes
are a function of the artifact. EVA-09 lists outputs 3–6 together because most are runtime
measurements, but **output 6 is a correctness result and carries across machines**. Outputs **1** and
**2** are likewise hardware-independent. **Only outputs 3, 4 and 5 are affected.**

**The decision available to the owner** — three options, and the second is deliberately distinguished
from the thing UQ-1 forbids:

| Option | What it yields |
|---|---|
| **1. Measure on a PRF-01-class machine** | A valid output 3/4/5, and the only route to a legitimate **pass** on EVA-14 dimension 4 |
| **2. Accept these numbers as a labelled one-directional bound** | Valid **only in the FAIL direction**: if an i7-12700H cannot hit 500 ms, no 10th-gen U-series machine will. It can never establish a pass |
| **3. Treat outputs 3/4/5 as NOT RUN** | Dimension 4 undecided |

A *substitution* would treat the number as the product floor in both directions; a one-directional
bound is inadmissible for a pass by construction. **That distinction is the owner's to accept or
reject.** This report does not assume it: §12 dimension 4 is answered under option 2 **and** under
option 3, and **the ruling in §0 does not depend on which is chosen** — dimension 1 already fails.

---

## 3. Measurement protocol actually used (PRF-06, OP-3, EVA-11)

Pre-registered in `tools/ml-pilot/README.md` §2 **before any number was measured**. Reproduced here
because EVA-11 requires this report to state its protocol in its own text.

**Latency.** Boundary is PRF-05 and was not re-opened: invocation → structured fields populated,
**including** tokenization and the encoder forward pass, **excluding** model load. Statistics (OP-3):
**warm only**; **20 warm-up iterations discarded**; **200 samples**; **p50, p95 and max all
reported**; **p95** is the value compared against the 500 ms ceiling; **no outlier removal of any
kind** — no trimming, no winsorising. Inputs cycle deterministically over the DAT-05 realistic
categories. **No run was discarded.**

**Accuracy.** Head is `SdcaMaximumEntropy` with identical hyperparameters for **every** arm (EVA-05);
only the `Features` column differs. Seeds `{42, 1337, 2026, 7, 99}` — 42 is the shipped production
seed. Split consumed verbatim; **no arm re-split** (EVA-04). Confidence is `Score.Max()`, exactly
what `TextClassifierModelManager.Predict` reads.

**Run-to-run variance** is the spread of macro-F1 across those five seeds, per arm. The comparison
rule, fixed before any number existed: *an arm improves beyond run-to-run variance only if its
**minimum** macro-F1 across seeds exceeds the baseline's **maximum***. This is variance-relative, not
a fixed effect size — **EVA-13 forbids "+2 F1" or any invented margin, and none was used.**

### 3.1 Two protocol amendments, both made before the numbers they govern

Recorded rather than silently applied. Each is dated in the pilot README.

1. **Latency input set.** As first written, all six fixture categories fed the reported percentile.
   With the fixture set built, three pathological rows (2 040 / 5 269 / 20 159 chars) would have sat
   at ~8 % of a 200-sample run — landing on the p95 and making the ceiling comparison a report on a
   20 000-character input no user types. Realistic categories now form the distribution; empty and
   pathological are still measured, reported as **named cases** (§7.1).
2. **Confidence-bin independence.** 205 test rows × 5 seeds is **not** 1 025 independent samples.
   Per-seed populations are primary; pooled views are labelled non-independent; raw rows persist as
   `(row_id, seed, confidence, correct)` so a later reader can de-pool them.

---

## 4. Output 1 — Per-class precision and recall

**No single headline accuracy figure appears in this document, or in any intermediate JSON**
(EVA-08). Per class, seed 42 (the production seed):

| Arm | Class | Precision | Recall | F1 | Support |
|---|---|---|---|---|---|
| **baseline** | `BaiTapVeNha` | 0.889 | **0.143** | 0.246 | 56 |
| | `DoAnCuoiKy` | 0.676 | 1.000 | 0.807 | 50 |
| | `ThiGiuaKy` | 1.000 | 0.849 | 0.918 | 99 |
| **Arm A** fp32 | `BaiTapVeNha` | 0.889 | **0.286** | 0.432 | 56 |
| | `DoAnCuoiKy` | 0.683 | 0.820 | 0.746 | 50 |
| | `ThiGiuaKy` | 0.923 | 0.606 | 0.732 | 99 |
| **Arm A** int8 | `BaiTapVeNha` | 0.895 | **0.304** | 0.453 | 56 |
| | `DoAnCuoiKy` | 0.683 | 0.820 | 0.746 | 50 |
| | `ThiGiuaKy` | 0.953 | 0.616 | 0.749 | 99 |
| **Arm B** fp32 | `BaiTapVeNha` | 1.000 | **0.143** | 0.250 | 56 |
| | `DoAnCuoiKy` | 0.662 | 0.900 | 0.763 | 50 |
| | `ThiGiuaKy` | 0.954 | 0.626 | 0.756 | 99 |
| **Arm B** int8 | `BaiTapVeNha` | 0.933 | **0.250** | 0.394 | 56 |
| | `DoAnCuoiKy` | 0.687 | 0.920 | 0.786 | 50 |
| | `ThiGiuaKy` | 0.957 | 0.667 | 0.786 | 99 |

**Macro-F1 across the five seeds** — the quantity EVA-16 and EVA-14 dimension 1 are defined against:

| Arm | seed 42 | 1337 | 2026 | 7 | 99 | **mean** | **min** | **max** | SD |
|---|---|---|---|---|---|---|---|---|---|
| **baseline** | 0.6569 | 0.6582 | 0.6569 | 0.6588 | 0.6569 | **0.6575** | **0.6569** | **0.6588** | 0.0009 |
| **Arm A** fp32 | 0.6365 | 0.6240 | 0.6628 | 0.6345 | 0.6392 | **0.6394** | 0.6240 | 0.6628 | 0.0143 |
| **Arm A** int8 | — | — | — | — | — | **0.6484** | 0.6392 | 0.6557 | 0.0059 |
| **Arm B** fp32 | 0.5896 | 0.5816 | 0.5827 | 0.6280 | 0.5853 | **0.5934** | 0.5816 | 0.6280 | 0.0196 |
| **Arm B** int8 | — | — | — | — | — | **0.6404** | 0.6150 | 0.6619 | 0.0226 |

**Every encoder configuration scores below the baseline mean.** Per-seed values for the int8 runs are
in `tools/ml-pilot/results/arm_*_int8.json`.

**Two observations that the per-class view makes visible and an average would hide:**

- **`BaiTapVeNha` is the failing class for everyone.** The baseline recalls **8 of 56** homework rows.
  Every encoder configuration is **better** on this class — Arm A int8 reaches 0.304 recall, more
  than double the baseline. The encoders' macro-F1 deficit comes entirely from `ThiGiuaKy`, where the
  baseline reaches 0.849 recall and no encoder exceeds 0.667.
- **The baseline's variance is near zero** (SD 0.0009) while the encoders' is 6–25× larger. SDCA on a
  sparse n-gram representation is close to deterministic here; on a 384/768-dimensional dense
  representation it is not.

---

## 5. Output 2 — Confidence-versus-accuracy relationship

**This is the input to CNF-03's threshold re-derivation, and it is where the encoders win.**

Seed 42, the production seed — **primary view, genuinely independent samples**:

| Confidence bin | baseline n | baseline acc | Arm A n | Arm A acc | Arm B n | Arm B acc |
|---|---|---|---|---|---|---|
| [0.2, 0.4) | 7 | 0.143 | 22 | 0.136 | 20 | 0.100 |
| [0.4, 0.5) | 24 | 0.375 | 38 | 0.342 | 36 | 0.361 |
| [0.5, 0.6) | 22 | 0.273 | 24 | 0.458 | 30 | 0.400 |
| **[0.6, 0.7)** | 11 | **0.000** | 25 | 0.480 | 34 | 0.500 |
| [0.7, 0.8) | 15 | 0.333 | 25 | 0.680 | 28 | 0.750 |
| [0.8, 0.9) | 7 | 0.571 | 37 | 0.811 | 27 | 0.889 |
| [0.9, 1.0] | **119** | 0.983 | 34 | 0.912 | 30 | 0.867 |

Pooled-across-seed counts are in the result JSON, **labelled non-independent** — the same 205 rows
appear five times, and a bin that looks 5× more populated than it is would weaken any gate derived
from it.

**Reading this table:**

- **Both encoder arms are monotonic.** Accuracy rises with confidence at every step. That is what a
  gate needs, and it is precisely EVA-14 dimension 3.
- **The baseline is not monotonic and is not usable as a graded signal.** It rises, falls, hits
  **0.000 at [0.6, 0.7)**, rises again. Below 0.9 its confidence carries almost no information.
- **The baseline is bimodal**: 119 of 205 rows (58 %) land in the top bin at 0.983 accuracy, and the
  remaining 86 are scattered across bins whose accuracy never exceeds 0.571. It behaves like a
  near-binary confident/not-confident flag rather than a graded score.
- **The encoders spread their population** across bins (22–38 per bin for Arm A), which is what gives
  a re-derived threshold somewhere to land.

**See §14, finding F-1:** the existing production gate is **0.60**, and the baseline's `[0.6, 0.7)`
bin has **0.000** observed accuracy on real held-out input. That finding is independent of this
initiative's outcome.

---

## 6. Output 3 — Cold-start model load time

Measured on the **.NET path**, in **5 fresh processes** per configuration — process start to the
`InferenceSession` being ready to serve its first inference. Measured **separately** from inference,
because PRF-05 excludes model load from the latency boundary.

| Arm | Precision | median | min | max |
|---|---|---|---|---|
| **Arm A** | fp32 | 898 ms | 887 | 909 |
| **Arm A** | int8 | 862 ms | 849 | 1 072 |
| **Arm B** | fp32 | **1 684 ms** | 1 683 | 1 702 |
| **Arm B** | int8 | 1 303 ms | 1 209 | 1 476 |

> ⚠️ **Not a PRF-01 number** — see §2.1.

**Counter-intuitive and worth recording:** the *smaller* model (Arm B, 448 MB) loads roughly **twice
as slowly** as the *larger* one (Arm A, 1 178 MB). Arm A's export splits weights into an external
`.onnx_data` file, which ONNX Runtime memory-maps; Arm B's is a single self-contained protobuf that
must be parsed. Model size does not predict load time here.

BEH-11/BEH-12 would have required this cost to be paid off the startup path and once per session,
not per parse. At ~0.9–1.7 s it is not negligible.

---

## 7. Output 4 — Per-inference latency

**.NET path, CPU execution provider, PRF-05 boundary, protocol as recorded in §3.**

| Arm | Precision | p50 | **p95** | max | p95 vs 500 ms |
|---|---|---|---|---|---|
| **Arm A** | fp32 | 21.1 ms | **24.3 ms** | 38.2 ms | under by 476 ms |
| **Arm A** | int8 | 130.8 ms | **149.2 ms** | 167.3 ms | under by 351 ms |
| **Arm B** | fp32 | 5.3 ms | **6.8 ms** | 26.2 ms | under by 493 ms |
| **Arm B** | int8 | 3.7 ms | **5.7 ms** | 26.4 ms | under by 494 ms |

> ⚠️ **These are NOT PRF-01 numbers and CANNOT establish a pass** (§2.1). They are reported because a
> missing output is a failed report, and because they are decisive in the FAIL direction — which none
> of them is.

**Finding: Arm A's int8 export is ~6× slower than its fp32 export** (130.8 vs 21.1 ms p50) while using
roughly twice the peak memory (§8). Quantization is not a free size win on this CPU; it is a
size-for-speed trade that runs the wrong way. Arm B's int8 export behaves as expected.

### 7.1 Named cases — reported separately, not blended into the percentile

| Fixture | chars | Arm A fp32 | Arm A int8 | Arm B fp32 | Arm B int8 |
|---|---|---|---|---|---|
| `empty` (F031–F036) | 0–6 | 16.6–22.2 ms | — | 3.9–5.5 ms | — |
| **F038** (no whitespace) | 2 040 | 652 ms | 915 ms | 75.3 ms | 57.3 ms |
| **F037** | 5 269 | 994 ms | 1 187 ms | 77.5 ms | 54.3 ms |
| **F039** | 20 159 | **2 256 ms** | **2 622 ms** | 73.0 ms | 55.0 ms |

**Finding: unbounded input breaches the 500 ms ceiling on Arm A by up to 5×**, even on this
faster-than-reference machine. Arm B is flat across input sizes because its 512-token limit truncates
first; Arm A's 2 048-token window does not.

Input-length bounding is `[choice]` under spec §10, constrained only by the requirement that
truncation must not silently change a user-visible field without provenance saying so. **This
measurement is the evidence that such a bound would have been required, not optional.**

---

## 8. Output 5 — Peak resident memory during inference

Model resident, reported against the **8 GB** PRF-01 budget.

| Arm | Precision | Peak working set | % of 8 GB |
|---|---|---|---|
| **Arm A** | fp32 | 772 MB | 9.4 % |
| **Arm A** | int8 | 1 488 MB | 18.2 % |
| **Arm B** | fp32 | 954 MB | 11.6 % |
| **Arm B** | int8 | 1 488 MB | 18.2 % |

> ⚠️ Not a PRF-01 number (§2.1). An 8 GB machine's behaviour under memory pressure is not modelled by
> a 16 GB machine that never reached it.

**No ceiling is asserted** (PRF-08). Measuring first and deriving later, at S4 (OP-4), is required
precisely so the ceiling is not reverse-engineered from whatever the winning arm happened to use.

---

## 9. Output 6 — Tokenization viability and route

Verified by **loading the real vocabulary** and diffing element-wise against each candidate's own
reference tokenizer (HuggingFace `tokenizers`, from the candidate's own `tokenizer.json`) — **not by
reading a documentation page** (TOK-04).

> **Why this output is legitimate off the reference machine.** Token ids are a function of the
> vocabulary and the input string. They do not vary with CPU, RAM or OS. §2.1 refers.

### 9.1 Route B is unavailable for both candidates

Neither ONNX export contains in-graph tokenization. Both take `input_ids` / `attention_mask` (Arm B
additionally `token_type_ids`). **Route A is the only candidate route**, which is a measurement, not
a preference.

### 9.2 Route A results, on the DAT-05 fixture set

| Arm | Route A source | diacritics | stripped | runtogether | abbrev | pathological | empty | **Total** |
|---|---|---|---|---|---|---|---|---|
| **Arm A** | `tokenizer.model` (4.6 MB) | 8/8 | 8/8 | 6/6 | 8/8 | 3/3 | 6/6 | **39/39 ✅** |
| **Arm B** | `sentencepiece.bpe.model` (4.8 MB) | 8/8 | 8/8 | 6/6 | 8/8 | 3/3 | **0/6** | **33/39** |

**TOK-02 names four categories** — diacritics, stripped diacritics, run-together tokens, domain
abbreviations. **Both arms reproduce the reference exactly on all four**, plus the pathological rows.

### 9.3 The finding that justified the harness

**Arm B requires a fairseq +1 id offset** over the raw SentencePiece ids to reach the HuggingFace id
space. Without it:

```
reference : [0, 41, 1294, 12, 6117, 19865, 13850, 8652, 14346, 39550, 858, 2]
.NET, raw : [0, 40, 1293, 11, 6116, 19864, 13849, 8651, 14345, 39549, 857, 2]
```

A sequence that **looks entirely plausible and is wrong in every position**. The model still returns
a vector, the head still returns a label, and nothing fails. **This is exactly the silent divergence
TOK-02 exists to catch, and reading documentation would not have caught it.** Arm A needs no offset.

### 9.4 Characterisation of the residual divergence

A divergence reported without its axis is a rumour, so the axis was measured — a 181-case
whitespace / punctuation / emoji stress corpus:

| Comparison | Arm A | Arm B |
|---|---|---|
| Raw, all 181 cases | 163/181 | 69/181 |
| **Cases with no leading/trailing whitespace** | **20/20** | **20/20** |
| Both sides trimmed | 181/181 | 167/181 (residual = empty-after-trim) |

**Every divergence in both arms lies on the leading/trailing-whitespace axis.** No divergence arises
from Vietnamese diacritics, run-together tokens, abbreviations, punctuation, digits or emoji.
`Microsoft.ML.Tokenizers.SentencePieceTokenizer` and HuggingFace `tokenizers` disagree about how
surrounding whitespace is normalised, and about nothing else observed.

**Disposition (TOK-05).** Neither arm is rejected for tokenization. Both have a workable, verified
Route A on `net10.0-windows10.0.19041.0`, fully offline, with **no non-.NET runtime dependency**
(TOK-03) — Python was the verification **oracle**, never the route. Arm A's route is cleaner: exact on
all 39 fixtures with no adaptation.

### 9.5 Red demonstrations — the check was shown able to fail before its pass was trusted

| Perturbation | Result | What it proves |
|---|---|---|
| Byte-flip inside the piece table | **0/39 both arms** | The comparison reads the real vocabulary |
| Drop the fairseq +1 offset | **Arm B 0/39**, Arm A unchanged | The offset is load-bearing where applied, correctly absent where not |

### 9.6 TOK-07 — shared-ML-package blast radius: **none**

`Microsoft.ML.Tokenizers` **2.0.0 declares no dependency on `Microsoft.ML` at all.** The harness pins
`Microsoft.ML` to **3.0.1** — the version the product pins — and NuGet resolves
`Microsoft.ML/3.0.1` + `Microsoft.ML.CpuMath/3.0.1` + `Microsoft.ML.DataView/3.0.1` +
`Microsoft.ML.Tokenizers/2.0.0` + `Microsoft.ML.OnnxRuntime/1.29.0` cleanly.

**Route A implies no version change to any package shared with M7 `StudyTimePredictor` or M8-A
`TextClassifier`.** On this evidence **CP2 (WP-1.1) would have been a documented skip**, not a
blocking checkpoint. Recorded, not acted on (AC-08) — moot if the owner confirms the stop.

---

## 10. Output 7 — Limitations arising from 3-of-5 class coverage

The real evaluation subset covers **three of five** classes: `ThiGiuaKy` 99, `BaiTapVeNha` 56,
`DoAnCuoiKy` 50. **`KiemTraThuongXuyen` and `ThiCuoiKy` have zero real rows** and are untested here.

**The imbalance runs opposite to the training set.** `ThiGiuaKy` is the **smallest** training class
(85 of 698) and the **largest** test class (99 of 205); `KiemTraThuongXuyen` is the **largest**
training class (188) with **no** test rows at all.

Every arm predicted into the two absent classes on real input — baseline 37–39 of 205, Arm A 61–70,
Arm B 58–71. Those predictions are wrong by construction and are counted as errors.

**No claim of general production accuracy or generalization is made from this evaluation** (DAT-01).

---

## 11. Output 8 — Packaged on-disk size

Encoder plus tokenizer assets, **as they would ship**. Input to **OP-1**, decided by the owner at CP3.

| Arm | Precision | Encoder | Tokenizer | **Total packaged** |
|---|---|---|---|---|
| **Arm A** | fp32 | 1 177.8 MB | 4.5 MB | **1 182.3 MB** |
| **Arm A** | int8 | 295.1 MB | 4.5 MB | **299.6 MB** |
| **Arm B** | fp32 | 448.5 MB | 4.8 MB | **453.3 MB** |
| **Arm B** | int8 | 112.9 MB | 4.8 MB | **117.7 MB** |

The size cap **OP-1 remains unset** and is not invented here. The *"1–2 GB acceptable"* remark
recorded during requirements gathering is an **install-size preference, not the cap**, and is not
treated as one.

Note that Arm A's int8 export is the one that is **6× slower** than its fp32 export (§7). The
cheapest configuration by size is the most expensive by latency.

---

## 12. Findings — the EVA-14 ruling

**EVA-13: no fixed effect size.** No threshold such as "+2 F1 points" was set, before or after the
fact. The comparison is against measured variance, using the rule pre-registered in §3.

**A win requires all five dimensions.** Each answered individually, per arm, **before** the overall
conclusion:

| # | Dimension | Arm A | Arm B |
|---|---|---|---|
| **1** | Improvement over baseline **beyond run-to-run variance** | ❌ **FAIL** | ❌ **FAIL** |
| **2** | **Per-class** results acceptable | ⚠️ **Mixed** | ⚠️ **Mixed** |
| **3** | **Confidence behaviour usable** | ✅ **PASS** | ✅ **PASS** |
| **4** | Latency and peak memory within the §7 budget | ⚠️ **Undetermined** | ⚠️ **Undetermined** |
| **5** | **Viable, verified tokenization path** | ✅ **PASS** | ✅ **PASS** |

**Dimension 1 — FAIL, both arms, decisively.** Pre-registered rule: arm min > baseline max.

| Arm | min | baseline max | Passes? | mean delta |
|---|---|---|---|---|
| Arm A fp32 | 0.6240 | 0.6588 | ❌ | **−0.0181** |
| Arm A int8 | 0.6392 | 0.6588 | ❌ | **−0.0091** |
| Arm B fp32 | 0.5816 | 0.6588 | ❌ | **−0.0641** |
| Arm B int8 | 0.6150 | 0.6588 | ❌ | **−0.0435** |

The rule is not close to being met, and the direction is wrong: **no configuration even beats the
baseline's mean.** The ranges do not overlap in the arms' favour; they overlap in the baseline's.

**Dimension 2 — Mixed.** Not one class carrying the average, but not acceptable either. Every encoder
configuration is **better than baseline** on `BaiTapVeNha` (the class the baseline fails at, 0.143
recall) and **worse** on `ThiGiuaKy` (where the baseline reaches 0.849). The encoders trade the
baseline's strongest class for its weakest and come out behind on the aggregate.

**Dimension 3 — PASS, both arms, and this is the clearest positive result in the pilot.** Both arms
produce a **monotonic** confidence-accuracy relationship with 22–38 rows per bin at seed 42 — enough
population near any plausible boundary to support a gate. The baseline does not: non-monotonic, a
**0.000-accuracy bin at [0.6, 0.7)**, and 58 % of its mass in the top bin.

**Dimension 4 — Undetermined, by UQ-1, and it does not change the ruling.**
*Under option 2* (one-directional bound): warm p95 of 5.7–149.2 ms is far under 500 ms, and peak RSS
of 772–1 488 MB is 9–18 % of the 8 GB budget — but neither can establish a **pass** on a
faster-than-reference machine. *Under option 3*: NOT RUN. **One sub-result is decisive in the FAIL
direction regardless**: unbounded pathological input takes up to **2 622 ms** on Arm A, breaching the
ceiling on hardware faster than the target (§7.1).

**Dimension 5 — PASS, both arms.** §9.

### 12.1 EVA-16 — the kill criterion, applied

> **Both encoder arms fail to improve macro-F1 over baseline by a margin larger than run-to-run
> variance. Under EVA-16 the initiative does not proceed to implementation.**

The criterion fires unambiguously: both arms are **below** the baseline, not merely within its noise.

**EVA-14 is a strictly higher bar than EVA-16, and neither arm cleared either.**

### 12.2 EVA-15 — the tie branch does not apply

A and B are distinguishable — Arm A's mean exceeds Arm B's at both precisions, and at fp32 their
seed-wise ranges barely overlap. **But distinguishing the arms is moot: neither beat the baseline.**
This is not a tie between two viable candidates; it is a failure of both against the incumbent.

**Arm C is therefore not indicated by the tie branch.** Arm C's gate (PD-9, OP-11) is *"A and B
together failing to produce evidence strong enough for a trustworthy decision."* The evidence here is
**not weak — it is clear and negative.** A third encoder from the same family, evaluated on the same
698 synthetic training rows, would be testing the same hypothesis that just failed. §14 F-2 explains
why the constraint looks like the data rather than the encoder.

### 12.3 Ruling

**No winner is declared. The EVA-16 kill criterion fires. Under the ratified specification the
initiative stops at S0** — a valid, complete outcome (PD-3). The owner's decision is §17.

---

## 13. Limitations (EVA-11, DAT-01, DAT-02)

Stated in this report's own text.

1. **3-of-5 class coverage.** §10. **No claim of general production accuracy or generalization is
   made** (DAT-01).
2. **Corpus maturity.** The corpus is un-deduplicated, unversioned, and unbalanced against real-world
   usage. **This is not recorded as a production acceptance failure** (DAT-02) — it is a known,
   bounded limitation of the evidence.
3. **Near-duplicate overlap across the split boundary: 0 test rows**, measured under
   diacritic- and punctuation-insensitive normalisation. Exact-text overlap: **0**, asserted in code.
   Near-duplicates were **counted, never filtered** — filtering would silently change the split the
   specification defines.
4. **The train/test distribution shift is large, and it is a property of the generator** — see §14
   F-2. This is the most important caveat on the *negative* result, and it cuts both ways.
5. **Outputs 3, 4 and 5 are not PRF-01 numbers.** §2.1.
6. **Head hyperparameters were not tuned for dense features.** `SdcaMaximumEntropy` defaults were
   used for every arm, which EVA-05 requires — the featurizer must be the only variable. A dense
   384/768-dimensional representation may well want different regularisation than a sparse n-gram
   one. **This pilot measures "the encoder, dropped into the existing head, untuned"**, which is the
   comparison the specification asked for, and it is not the same question as "the best achievable
   result from this encoder."
7. **One evaluation set, one domain.** 205 rows from one collection pass.
8. **The published 96.2 % held-out figure is not a generalization number** and is not cited here as a
   synthetic→real baseline: the real rows were merged into the training seed before it was measured.

---

## 14. Findings that outlive this decision

Three results matter regardless of what the owner rules at CP1.

### F-1 — The existing production confidence gate sits at the worst-calibrated point of the baseline's distribution

The shipped M8-A gate is **≥ 0.60**. On the baseline's own measured confidence-versus-accuracy
relationship over 205 real held-out rows, the **`[0.6, 0.7)` bin has 0.000 observed accuracy** at seed
42 (0.033 pooled across five seeds). The adjacent `[0.5, 0.6)` bin — *below* the gate — scores 0.273.

The bin populations are small (11 rows at seed 42), so this is **an indication, not a proven defect**,
and it says nothing about the gate's behaviour on the synthetic-heavy production distribution. But
CNF-01 already records that gating on a raw model score alone is a rule this project holds and the
current task-type gate does not satisfy. **This measurement is the first quantitative evidence on
that point, and it was produced by the baseline arm — no encoder required.**

**Recommendation:** track as a defect candidate against the shipped classifier, independent of this
initiative. **Not acted on here** — S0 writes no production code (EVA-01), and re-deriving a shipped
threshold is a user-visible behaviour change requiring its own decision (§8 of the spec).

### F-2 — The training set is synthetic and does not contain the vocabulary students actually type

Measured off the committed split (`tools/ml-pilot/results/vocab_gap.json`):

| Measure | Value |
|---|---|
| Training vocabulary | 934 distinct tokens over 698 rows |
| Test tokens **unseen** in training | **401 / 1 604 = 25.0 %** |
| …diacritic-insensitive | 373 / 1 604 = 23.3 % |
| **Test rows containing ≥ 1 unseen token** | **194 / 205 = 94.6 %** |

| Abbreviation | Occurrences in 698 training rows | Test rows containing it |
|---|---|---|
| **`tgk`** | **0** | **28** |
| `xstk` | 1 | 4 |
| `csdl` | 4 | 4 |
| `ktvm` | 2 | 2 |
| `ktct` | 1 | 2 |
| `btvn` | 1 | 1 |

**`tgk` — the single most common domain abbreviation in real input, in 28 of 205 test rows — appears
zero times in the training set.**

**Why this matters to reading the negative result, in both directions.** The pilot asked: *does a
pretrained encoder beat n-grams on real input?* The answer measured is no. But the experiment as
specified trains **both** featurizers on 698 rows that are 100 % synthetic and largely lack the
surface forms the test set is made of. That is a hard setting for the n-gram baseline — and it
**still won**. It is also a hard setting for the encoder, whose advantage must be realised through a
linear head fitted on that same unrepresentative distribution: the encoder can represent `tgk`
perfectly well and still have no way to learn what it *means for this label set* from data that never
shows it.

**This is the strongest argument that the constraint is the data, not the encoder** — and it is the
reason DAT-03's dataset workstream is the more promising next step than a third encoder. **It is
offered as an interpretation, not as grounds to overturn the ruling.** DAT-04 is explicit: expanding
the dataset does not by itself authorise re-running or reversing an S0 outcome — that is a new owner
decision.

### F-3 — The encoders were verified to work; the null result is not a broken harness

A broken embedder produces the *same* "no improvement" verdict as a working encoder that genuinely
does not help. The two conclusions are nothing alike, so the instrument was checked:

- Vectors are **L2-normalised** (‖v‖ = 1.0000), fully distinct across components (768/768, 384/384).
- **Reproducible**: max |Δ| over a repeat run = **0.00E+000** — bit-identical (BEH-05 evidence).
- **Rank test on the DAT-05 diacritics/stripped pairs**: each encoder retrieves the correct partner at
  **rank 1 in 5/8 (Arm A) and 6/8 (Arm B)**, mean rank 1.75, against a chance rate of 1/8.

**The encoders demonstrably encode meaning and survive diacritic stripping** — the exact capability
the initiative was betting on. They simply do not convert it into a better label on this data.

> **A discarded check, recorded because it would have misled.** The first sanity test compared cosine
> *magnitudes* between paired and unpaired fixtures and reported "no separation" for both arms. That
> test was wrong, not the encoders: all eight fixtures are same-domain Vietnamese student task text,
> so unrelated pairs are legitimately similar and absolute cosine says almost nothing. The rank test
> is immune to that. Had the first version been trusted, a sound null result would have been reported
> as a broken harness.

---

## 15. Verification

| Claim | Evidence |
|---|---|
| Split built once, consumed verbatim | `tools/ml-pilot/split/SPLIT.md`; counts asserted in code, **exit 2 on drift** — proven by dropping 3 rows → `test 202 != 205` |
| Seed unchanged since the spec's `[fact]` | SHA-256 `86abb454c139bf2c0dd3f7a4698a5f2fbde144de2f02a17950b32f5bfa36dbd6`; 597 + 101 = 698 / 205 / 903 all match |
| No leakage | Exact overlap 0 (asserted); near-duplicate overlap 0 (measured) |
| Split determinism | Re-run reproduces `train.csv` / `test.csv` byte-identically |
| Fixture set integrity | `python tools/ml-pilot/fixtures.py` — **proven red 4 ways** (dropped PairId, plain-token empty rows, truncated pathological rows, injected `0xff`), each exit 1 |
| Tokenization verified against the real vocabulary | `tokcheck` — **proven red 2 ways** (corrupted vocabulary → 0/39; dropped offset → Arm B 0/39) |
| **AC-21 — no model binary in git** | CI step `Assert no model binary is tracked`, asserting over `git ls-files`. **Proven red in CI**: run [32792616833](https://github.com/PotatoMine725/Smart-Study/actions/runs/32792616833) → `##[error]AST-05 violation`. Locally red on all three arms (extension, >1 MB size, `tokenizer.model`); green on the real tree, 477 files checked |
| Runtime on the shipping stack | `Microsoft.ML.OnnxRuntime` `InferenceSession` + real SentencePiece tokenizer + real `SdcaMaximumEntropy`, `net10.0-windows10.0.19041.0` |
| TOK-07 | `project.assets.json` resolves `Microsoft.ML/3.0.1` beside `Microsoft.ML.Tokenizers/2.0.0` |
| Prefixes are `[fact]`, not recall | Verified against each model card (§3 of the pilot README). **The tokenizer check cannot catch a prefix error** — it prepends the same string to both sides — so the prefix was verified against its own source |
| **No production code written** | `git status` clean under `SmartStudyPlanner/`; `gitnexus_detect_changes` returned **0 changed symbols, 0 affected processes** on every S0 commit |
| Arm C not run | Not acquired; `Arm.ByKey` throws for any key other than `arm_a` / `arm_b` |

**Not run, stated as a result rather than left blank:** outputs 3/4/5 on a **PRF-01-class machine**
(UQ-1, §2.1). Head-hyperparameter tuning for dense features (§13.6) — out of scope by EVA-05.

---

## 16. Follow-ups

| # | Item | Owner | Where it belongs |
|---|---|---|---|
| 1 | **F-1** — production gate at 0.60 vs a 0.000-accuracy bin | Owner | New defect candidate against shipped M8-A, **independent of this initiative** |
| 2 | **F-2** — dataset vocabulary gap | Owner | **DAT-03**, already an independent ongoing workstream; DAT-04 governs any re-run |
| 3 | Collect real rows for `KiemTraThuongXuyen` and `ThiCuoiKy` | Owner | DAT-03 |
| 4 | If any encoder work is ever revived: input-length bounding is **required**, not optional (§7.1) | — | Recorded here so it is not rediscovered |
| 5 | Retire or retain `tools/ml-pilot/` | Owner | §17 question 5 |

---

## 17. Decisions made

### D-1 — S0 was executed end-to-end despite the kill criterion being likely to fire early

**Why it had to be made.** After the baseline and Arm B, the direction was already visible. Stopping
there would have been cheaper.
**What it is for.** EVA-08 requires **all eight outputs per arm**, and WP-0.8 states plainly that a
missing output is a failed report, not a caveat. The owner is ruling on whether to stop an
initiative; ruling on a partial report would mean re-running S0 if the ruling ever needs revisiting.
Outputs 6, 7 and 8 also feed decisions (CP2's blast radius, OP-1's cap) that keep their value if the
initiative is ever revived.
**Experience.** Completing the measurement after the answer was visible cost roughly an hour and
produced F-1, F-2 and the quantization finding — none of which the abbreviated version contains.

### D-2 — The whole pilot was built in .NET, with Python only as the tokenizer oracle

**Why.** EVA-09 requires outputs 3–6 on the shipping stack. A Python accuracy harness would have
needed the "head family divergence reported" caveat WP-0.5 permits.
**What it is for.** One harness, one head — literally the same `SdcaMaximumEntropy` for every arm — so
EVA-05's "featurizer is the only variable" is structural rather than argued. Python appears only as
the reference-tokenizer oracle, which TOK-02 requires be independent: diffing a .NET tokenizer against
itself would detect nothing.
**Experience.** `Microsoft.ML.Tokenizers` 2.0.0 works cleanly on `net10.0-windows` beside a pinned
`Microsoft.ML` 3.0.1, and `tokenizers` 0.23.1 installs on Python 3.14. Neither was certain going in;
both are now measured.

### D-3 — Both precisions were measured for both arms

**Why.** fp32 removes quantization as a confound from the accuracy comparison; the quantized export is
what would ship under a size cap.
**What it is for.** Reporting one precision and inferring the other would have been inventing a number.
**Experience.** It caught the finding that Arm A's int8 export is ~6× *slower* than its fp32 export
while using twice the peak memory. A single-precision pilot would have reported the opposite
size/speed story with equal confidence.

### D-4 — The dev machine was measured, and labelled as inadmissible for a pass

**Why.** No PRF-01-class machine was available. PRF-03 forbids treating a developer-machine number as
the product floor; WP-0.7 says to stop and escalate rather than substitute.
**What it is for.** Escalating with *no* number would have left dimension 4 blank and given the owner
nothing to decide with. Escalating with a number **labelled valid only in the FAIL direction** gives
the owner a real option without smuggling in a pass. §2.1 offers all three options and the ruling is
constructed not to depend on which is chosen.
**Experience.** The distinction between *substitution* and *one-directional bound* is what makes the
number reportable at all. It also turned out to matter: the pathological-input result **does** fail
decisively in that direction (§7.1), which is a conclusion the escalation-without-measuring path would
never have reached.

### D-5 — The tie branch was ruled inapplicable rather than invoked

**Why.** EVA-15 fires when A and B cannot be reliably distinguished. Here they can be — and both lost
to the incumbent.
**What it is for.** Invoking the tie branch would have put Arm C in front of the owner as an
indicated next step. It is not indicated: Arm C's gate is *"evidence not strong enough for a
trustworthy decision"*, and this evidence is clear, just negative. §14 F-2 argues the binding
constraint is the training data.
**Experience.** The tie branch is a guard against declaring a winner inside the noise. It is not a
general escape hatch toward "run another arm", and reading it as one would have converted a clean
null result into open-ended scope.

### D-6 — The plan was committed with `status: draft` unchanged

**Why.** The owner directed execution from this plan in session, which is what authorises S0. Its own
lifecycle field still says `draft — awaiting owner review`.
**What it is for.** Flipping a document's status on the strength of a verbal direction would misreport
who signed what.
**Experience.** Surfaced here for the owner to resolve: either mark the plan reviewed, or leave it
draft and let this report's acceptance close it — S0 is the only phase it authorised, and it is done.

### D-7 — A discarded verification is reported, not deleted

**Why.** The first embedder sanity check was methodologically wrong and reported a false alarm.
**What it is for.** It would have made a sound null result look like a broken harness. A reader
weighing whether to trust §12 needs to know the instrument was challenged and *how*.
**Experience.** §14 F-3. Same-domain corpora compress absolute cosine similarity; rank-based tests
are the right instrument there.

---

## 18. Owner decision requested — CP1

**This is the hard gate. Until a written owner ruling exists, no file under `SmartStudyPlanner/` may
be created or modified for this initiative** (EVA-01).

### The primary decision

> **1. Accept or reject this report.**
>
> **On acceptance, the ratified consequence is that the initiative STOPS at S0** (EVA-16, PD-3, and
> STOP-1 in the execution plan). S1 through S4 are not entered. **This is a valid and complete
> conclusion of the plan, not a failure.**

**Accepting the report is not the same as agreeing with every interpretation in §14** — the findings
are labelled fact, measurement and inference separately so they can be accepted or discounted
individually.

### Secondary decisions the owner may wish to make at the same time

| # | Question | Recommendation |
|---|---|---|
| **2** | **Arm C** (OP-11) — unlock it? | **No.** The tie branch did not fire (§12.2). A third encoder trained on the same 698 synthetic rows tests the hypothesis that just failed |
| **3** | **F-1** — the shipped 0.60 gate vs a 0.000-accuracy bin | **Track as a defect candidate** against M8-A, separate from this initiative. Not acted on here |
| **4** | **F-2 / DAT-03** — dataset workstream | Already independent and ongoing. **DAT-04 applies**: expanding the dataset does not by itself authorise re-running or reversing this outcome — a re-run is a new owner decision |
| **5** | **`tools/ml-pilot/`** — keep or delete? | **Keep for now.** It is outside `SmartStudyPlanner.slnx`, so it costs nothing in build or CI, and it is the only way to re-derive these numbers without rebuilding everything |
| **6** | **The execution plan's `draft` status** (D-6) | Owner's call: mark reviewed, or let this report's acceptance close it |
| **7** | **UQ-1** (§2.1) — which of the three options | **Moot for this ruling** — dimension 1 already fails. Needed only if the owner rejects the stop and wants dimension 4 settled |

### If the owner rejects the stop

The report should say what that would mean rather than leaving it implicit. Rejecting the stop does
**not** re-open S1 — EVA-16 is ratified. It would mean commissioning **new evidence**, and the
evidence §14 F-2 points at is **dataset expansion**, not another encoder. That is a **new owner
decision** under DAT-04, and it would need its own plan.

---

## Artifacts

| Artifact | Path |
|---|---|
| Pilot ground rules + pre-registered protocol | `tools/ml-pilot/README.md` |
| Split record (counts, distribution, source hash, leakage) | `tools/ml-pilot/split/SPLIT.md` |
| Artifact manifest (pinned revisions, SHA-256, sizes, licence) | `tools/ml-pilot/ARTIFACTS.md` |
| DAT-05 fixture set + guide | `datasheets/vn_input_fixtures.csv` / `.md` |
| Per-arm results, raw predictions, tokenization diffs | `tools/ml-pilot/results/*.json` |
| .NET harness | `tools/ml-pilot/dotnet/` |
| Reference-tokenizer oracle | `tools/ml-pilot/tokenizer-oracle/` |
