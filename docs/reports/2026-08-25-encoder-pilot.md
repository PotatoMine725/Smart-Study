# S0 Encoder Pilot — evaluation report

**Date:** 2026-08-25 · **Author:** Claude (agent), for owner review at CP1
**Status:** 🔴 **SKELETON — NOT YET FILLED.** Every measurement below reads `NOT YET MEASURED` until
its work package produces it. A heading that still says that at CP1 is a **failed report**, not a
caveat (WP-0.8).

**Decides:** whether the neural-encoder initiative proceeds past S0 at all.
**Governed by:** [`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md) §6 (RATIFIED)
**Executed per:** [`../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md`](../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md) WP-0.1 … WP-0.8
**Protocol pre-registered in:** [`../../tools/ml-pilot/README.md`](../../tools/ml-pilot/README.md) §2 — written **before** any number was measured (PRF-06)

> **A null result is a complete, valid, successful outcome of S0** (PD-3). This report is not written
> to justify continuing. The five EVA-14 dimensions are answered individually, per arm, **before**
> any overall conclusion is drafted.

---

## 1. Scope

What this report covers, and what it deliberately does not claim.

**Covers:** the eight EVA-08 measurements for the **baseline** arm (current production n-gram
featurizer), **Arm A** (EmbeddingGemma-300M) and **Arm B** (multilingual-e5-small), on one split
constructed once; and the winner / tie / kill ruling that follows from them.

**Does not cover, by requirement:**

- **Arm C** (`hiieu/halong_embedding`) — not run, not acquired. Unlocked only by an explicit owner
  decision after A and B report (EVA-06).
- Any claim of **general** production accuracy or generalization (DAT-01) — see §11.
- Any **memory ceiling** (PRF-08) — measured here, derived at S4.
- Any production implementation. **No file under `SmartStudyPlanner/` was created or modified**
  (EVA-01).

---

## 2. Machine used (EVA-10, PRF-01, PRF-03, OP-5)

> **NOT YET MEASURED** — WP-0.7.

| Field | Value |
|---|---|
| Machine model | *pending* |
| CPU | *pending* |
| RAM | *pending* |
| Graphics | *pending* |
| OS + build | *pending* |
| Is this the PRF-01 reference class? | *pending — see §12, UQ-1* |

**PRF-03 is explicit: a developer-machine-only number is not an acceptable output.** If the numbers
below were not taken on a PRF-01-class machine, that is stated here plainly and the affected outputs
are marked as such, rather than annotated and treated as the product floor.

---

## 3. Measurement protocol actually used (PRF-06, OP-3, EVA-11)

> **NOT YET FILLED** — transcribed from `tools/ml-pilot/README.md` §2 at WP-0.8, together with any
> dated amendment made during execution.

The protocol was pre-registered **before** any number was compared against the 500 ms ceiling
(PRF-06). It fixes: warm-vs-cold, warm-up discard, sample count, reported percentiles, the
percentile compared against the ceiling, outlier handling, input set, provider, and the accuracy
variance protocol (seed set, repeat count, and the variance-relative comparison rule).

**The PRF-05 boundary is not a choice and was not re-opened:** invocation of the quick-parse action
→ structured fields populated, **including** tokenization and the encoder forward pass, **excluding**
model load.

---

## 4. Output 1 — Per-class precision and recall

> **NOT YET MEASURED** — WP-0.5 (baseline), WP-0.6 (Arms A, B).

Reported **per class**, for the three covered classes, per arm, per seed. **EVA-08 forbids a single
headline accuracy figure** — none appears anywhere in this document.

| Arm | Class | Precision | Recall | Support |
|---|---|---|---|---|
| *pending* | | | | |

**Macro-F1 across seeds, per arm** — mean, min, max, sample SD. This is the input to the
variance-relative comparison in §12.

| Arm | seed 42 | 1337 | 2026 | 7 | 99 | mean | min | max | SD |
|---|---|---|---|---|---|---|---|---|---|
| *pending* | | | | | | | | | |

---

## 5. Output 2 — Confidence-versus-accuracy relationship

> **NOT YET MEASURED** — WP-0.5, WP-0.6.

**Not optional.** This is the input to the §8 recalibration (CNF-03) that S3 will consume.

Per arm: observed accuracy per confidence bin, **with bin population counts**. A bin holding four
samples cannot support a gate, and a table without populations conceals that.

| Arm | Confidence bin | n | Observed accuracy |
|---|---|---|---|
| *pending* | | | |

Raw per-row `(confidence, correct)` pairs are persisted under `tools/ml-pilot/results/` so WP-2.5
can re-derive the threshold without re-running S0.

---

## 6. Output 3 — Cold-start model load time

> **NOT YET MEASURED** — WP-0.7, **.NET path, reference hardware**.

Measured **separately** from inference, because PRF-05 excludes model load from the latency boundary.
This is what "model already loaded" in PRF-04 means, and it is legitimate only because BEH-12 forbids
paying load cost per parse.

| Arm | median (5 cold constructions) | min | max |
|---|---|---|---|
| *pending* | | | |

---

## 7. Output 4 — Per-inference latency

> **NOT YET MEASURED** — WP-0.7, **.NET path, reference hardware, CPU execution provider**.

Over the **PRF-05 boundary**. Compared against the **500 ms** ceiling ratified as PD-12, using the
percentile fixed in §3 **before** any number existed.

| Arm | p50 | p95 | max | p95 under 500 ms? |
|---|---|---|---|---|
| *pending* | | | | |

Any whole-run discard, and its named external cause, is recorded here.

---

## 8. Output 5 — Peak resident memory during inference

> **NOT YET MEASURED** — WP-0.7, **.NET path, reference hardware**.

Reported against the **8 GB** budget of PRF-01, with the model resident.

| Arm | Peak RSS | % of 8 GB budget |
|---|---|---|
| *pending* | | |

**No ceiling is asserted here** (PRF-08). Measuring first and deriving later, at S4 (OP-4), is
required precisely so the ceiling is not reverse-engineered from whatever the winning arm happened
to use.

---

## 9. Output 6 — Tokenization viability and route

> **NOT YET MEASURED** — WP-0.7, **.NET path**, verified by loading the **real vocabulary**.

Verified by comparing against the candidate's **reference tokenizer** on the committed DAT-05 fixture
set — not by reading a documentation page (TOK-04). **An arm with no workable, verified route is
rejected regardless of its accuracy** (TOK-05), and is recorded as rejected here (AC-07).

| Arm | Route A (.NET tokenizer lib) | Route B (in-graph) | Verified route(s) | Verdict |
|---|---|---|---|---|
| *pending* | | | | |

**Red demonstration:** a deliberately corrupted vocabulary must make the comparison fail before any
pass is trusted. *pending*

**TOK-07 finding — shared-ML-package blast radius.** Whether the selected route implies moving
`Microsoft.ML` off its pinned 3.0.1, which **both** shipped predictors depend on (M7
`StudyTimePredictor`, M8-A `TextClassifier`). **Recorded here, not acted on** — it becomes owner
checkpoint CP2 at WP-1.1, before any dependency change is committed (AC-08). *pending*

---

## 10. Output 7 — Limitations arising from 3-of-5 class coverage

> **NOT YET FILLED** — WP-0.4 (source data), WP-0.8 (prose).

Stated in this report's **own text**, not by reference to the specification (EVA-11).

---

## 11. Output 8 — Packaged on-disk size

> **NOT YET MEASURED** — WP-0.3 (measured), WP-0.7 / WP-0.8 (carried here).

Encoder plus tokenizer assets, **as they would ship**. Required under PD-11: the §4.2 size cap
cannot be set to a defensible number before the artifact is measured. This is the input to **OP-1**,
decided by the owner at **CP3**.

| Arm | Encoder file(s) | Tokenizer assets | Total packaged | Quantization |
|---|---|---|---|---|
| *pending* | | | | |

---

## 12. Findings — the EVA-14 ruling

> **NOT YET RULED** — WP-0.8.

**EVA-13: no fixed effect size.** No threshold such as "+2 F1 points" is set, before or after the
fact. The comparison is against measured variance.

**A win requires all five dimensions.** Each is answered **individually and in writing, per arm,
before** any overall conclusion is drafted.

| # | Dimension | Arm A | Arm B |
|---|---|---|---|
| 1 | Improvement over baseline **beyond run-to-run variance** | *pending* | *pending* |
| 2 | **Per-class** results acceptable — not one class carrying the average | *pending* | *pending* |
| 3 | **Confidence behaviour usable** — the relationship can actually support a gate, with enough population near any plausible boundary | *pending* | *pending* |
| 4 | **Latency and peak memory within the §7 budget** | *pending* | *pending* |
| 5 | **Viable, verified tokenization path** (TOK-05) | *pending* | *pending* |

**EVA-16 — kill criterion, applied.** *pending*

**EVA-15 — tie branch.** If A and B cannot be reliably distinguished, **no winner is declared** and
this report says so. The decision then becomes whether more evidence is justified — conditional Arm C
(OP-11) or data expansion — and that is an **owner decision at CP1**, not this report's to make.
*pending*

**Ruling:** *pending*

---

## 13. Limitations (EVA-11, DAT-01, DAT-02)

> **NOT YET FILLED** — WP-0.8. Stated in this report's own text.

Must cover, at minimum:

- **3-of-5 class coverage** in the real evaluation subset — `ThiGiuaKy`, `BaiTapVeNha`, `DoAnCuoiKy`
  only; no `KiemTraThuongXuyen`, no `ThiCuoiKy`. **No claim of general production accuracy or
  generalization may be made from it** (DAT-01).
- **Corpus maturity** — un-deduplicated, unversioned, unbalanced against real-world usage.
- **Near-duplicate overlap** across the split boundary, as a measured number (from `SPLIT.md`).
- **The measurement protocol actually used**, including any dated amendment.
- Dataset immaturity is **not** recorded as a production acceptance failure (DAT-02). It is a known,
  bounded limitation of the evidence.

**The published 96.2% held-out figure is not a generalization number** and is not cited here as a
synthetic→real baseline: the real rows were merged into the training seed before it was measured.

---

## 14. Verification

> **NOT YET FILLED** — WP-0.8.

Commands run, split hashes, guard red-demonstrations, and the WP-0.1 CI guard's recorded red run.

---

## 15. Follow-ups

> **NOT YET FILLED** — WP-0.8. Non-blocking items, each with the checkpoint that owns it.

---

## 16. Decisions made

> **NOT YET FILLED** — WP-0.8.

ADR-style, one sub-section per non-trivial decision: *why it had to be made* / *what it is for* /
*experience for future development*. Standing owner requirement since 2026-07-07
(`docs/reports/README.md`).

---

## 17. Owner decision requested — CP1

> **NOT YET REACHED** — WP-0.9.

The owner is asked to decide:

1. **Accept or reject this report** (PD-3). **Rejection ends the initiative — a valid outcome.**
2. **If a winner is declared** — confirm the adopted encoder (OP-9) and note the verified
   tokenization route (OP-8) that comes with it.
3. **If the tie branch fired** (EVA-15) — choose: unlock **Arm C** (OP-11), expand the dataset and
   re-run, defer, or stop. **Arm C requires an explicit owner decision; nothing else unlocks it.**
4. **If the kill criterion fired** (EVA-16) — confirm the stop.

**Until a written owner ruling exists, no file under `SmartStudyPlanner/` may be created or modified
for this initiative** (EVA-01).
