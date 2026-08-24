# S0 Encoder Pilot — venue, ground rules, and the pre-registered measurement protocol

**Date:** 2026-08-25 · **Slice:** S0 (WP-0.1 … WP-0.9) · **Status:** in progress

**Governed by:** [`../../docs/specs/2026-08-24-neural-encoder-smart-parser.md`](../../docs/specs/2026-08-24-neural-encoder-smart-parser.md)
(RATIFIED) · **Executed per:** [`../../docs/plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md`](../../docs/plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md)

> **This directory is throwaway.** Nothing here ships. `tools/` is outside
> `SmartStudyPlanner.slnx`, so nothing here enters the product build or CI's build-and-test job.
> If S0 ends in a null result, deleting this tree and the report is the entire cost of the
> initiative.

---

## 1. The EVA-01 boundary — what S0 may and may not touch

S0 is a hard pre-production gate. **No production code may be written before the S0 report is
owner-accepted.** Ruled once, for the whole phase:

| May be created / modified in S0 | May **not** |
|---|---|
| `tools/ml-pilot/**` | **Any file under `SmartStudyPlanner/`** |
| `datasheets/**` | `SmartStudyPlanner.csproj` |
| `.gitignore`, `.github/workflows/ci.yml` | `SmartStudyPlanner.slnx` |
| `docs/reports/**`, `docs/plans/**`, `docs/active/**` | Any normative document, as a side effect (DOC-04) |

`SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` is read **read-only** by the split
builder. S0 does not modify it.

---

## 2. Pre-registered measurement protocol

> **Why this section exists and why it is dated before the numbers.** PRF-06 requires the latency
> statistics to be written down *before* any number is compared against the 500 ms ceiling, and
> WP-0.5 makes run-to-run variance the denominator of both EVA-16's kill criterion and EVA-14's
> first dimension. A protocol chosen after seeing the numbers is not a protocol — it is a
> rationalisation. **Every rule below was fixed before any arm was run.**
>
> If a rule here turns out to be unworkable, it is **amended in place with a dated note saying what
> changed and why**, and the amendment is carried into the report. It is not silently replaced.

### 2.1 Latency (OP-3) — the statistics, not the boundary

**The boundary is not open and is not restated as a choice.** PRF-05 fixes it: from invocation of
the quick-parse action to structured fields being populated, **including** tokenization and the
encoder forward pass, **excluding** model load. Only the statistics below are S0's to choose.

| Parameter | Pre-registered value | Why |
|---|---|---|
| **Warm or cold** | **Warm.** PRF-04 says "with the model already loaded" | Cold-start load is EVA-08 output 3, measured separately and excluded from the PRF-05 boundary |
| **Warm-up discarded** | First **20** iterations after session construction, discarded and not reported | JIT, first-allocation and ONNX Runtime arena growth are load-adjacent costs; BEH-12 forbids paying them per parse, so including them would measure a cost the product does not pay |
| **Sample count** | **200** measured iterations per arm | At n=200 the reported percentile rests on ~10 samples. A p99 at this n would rest on 2, and would be noise wearing a percentile's name |
| **Reported statistics** | **p50, p95, max** — all three, always | A median alone hides one submit in five |
| **Compared against the 500 ms ceiling** | **p95** | PRF-04 is a user-experience ceiling on an explicit submit. p50 lets a fifth of submits breach it unreported |
| **Outlier handling** | **None removed.** No trimming, no winsorising, no "excluding the first run" beyond the declared warm-up | Discarding the tail of a latency distribution is precisely how a ceiling gets cleared that was never tested. `max` is reported so the tail stays visible |
| **Whole-run invalidation** | A run may be discarded **only in its entirety**, only for a *named, externally observed* cause (e.g. a build running concurrently), and the discard is **recorded in the report with its reason** | Prevents per-sample cherry-picking while still allowing an honestly contaminated run to be redone |
| **Inputs** | Drawn from the committed DAT-05 fixture set (`datasheets/vn_input_fixtures.csv`), cycled deterministically across the 200 iterations, `empty` and `pathological` categories **included** | Measuring one short string would report a latency the product never sees. Including the pathological row keeps the tail honest |
| **Provider** | **CPU execution provider only** (PRF-02, AST-07) | DirectML is acceleration; it must not be a precondition for meeting §7 |
| **Machine** | **Named in the report** — model, CPU, RAM, OS build (EVA-10) | PRF-03: a developer-machine-only number is not an acceptable output. See §4 |

**Cold-start load time (output 3)** is measured as a separate quantity: process start → the
`InferenceSession` being constructed and ready to serve its first inference. Reported as the
**median of 5 cold constructions**, each in a fresh process, with min and max alongside.

### 2.2 Accuracy and run-to-run variance

The encoder is deterministic for a given input, asset and provider (BEH-05), so run-to-run variance
in these arms originates in the **head's training seed**, not the featurizer.

| Parameter | Pre-registered value | Why |
|---|---|---|
| **Head** | `SdcaMaximumEntropy`, identical hyperparameters across **every** arm (EVA-05) | The featurizer is the only variable. A tuned arm against a default baseline would be a rigged comparison |
| **Seeds** | `MLContext(seed: N)` for **N ∈ {42, 1337, 2026, 7, 99}** — 5 runs per arm | 42 is the shipped production seed and must be included; the other four exist to expose spread |
| **Split** | WP-0.4's split, consumed **verbatim** by every arm and every seed. The test set never varies | EVA-04 is absolute — no re-splitting, no re-shuffling, no stratification pass |
| **Reported per arm** | Per-class precision / recall / support for the 3 covered classes, per seed; and macro-F1 per seed with **mean, min, max, sample SD** across seeds | EVA-08 output 1 is per-class. **No single headline accuracy figure is emitted anywhere**, including in intermediate JSON |
| **"Run-to-run variance"** | The observed spread of macro-F1 across the 5 seeds, **per arm** | EVA-16 and EVA-14 dimension 1 are both defined relative to this quantity |

**Pre-registered comparison rule (EVA-16's "beyond run-to-run variance").** An arm improves beyond
run-to-run variance **only if its minimum macro-F1 across the 5 seeds exceeds the baseline's maximum
macro-F1 across the 5 seeds** — i.e. the two seed-wise ranges do not overlap.

- This is **variance-relative, not a fixed effect size**. EVA-13 forbids "+2 F1 points" or any
  equivalent invented margin, and none is used here.
- The raw per-seed values, both means, and both spreads are reported regardless of the outcome, so
  the owner can see the full picture at CP1 rather than only this rule's verdict.
- **EVA-14 is a strictly higher bar.** Passing this rule means an arm survived the kill criterion; it
  does not mean the arm won. All five EVA-14 dimensions are answered separately and in writing.

### 2.3 Model invocation — the prefix question, ruled before the run

`multilingual-e5-small` is trained with a `"query: "` / `"passage: "` input prefix, and
EmbeddingGemma carries task-specific prompt templates. **Each encoder is invoked exactly as its own
model card specifies.** Handicapping a candidate by misusing it produces worse evidence than a
documented invocation difference does.

Consequences, recorded here rather than discovered later:

- The **user-supplied substring** handed to every arm is byte-identical (this is the assertion the
  harness makes, and it is what BEH-04's "raw user input string as typed" actually protects). The
  model-mandated wrapper around it is not part of that substring.
- The exact wrapper used per arm is recorded in `ARTIFACTS.md` and reported beside the numbers.
- **No preprocessing is applied to the user substring under any arm** — no diacritic restoration, no
  word segmentation, no spelling correction, no case folding (BEH-04). A wrapper mandated by a model
  card is not preprocessing of the user's text; a normalisation step applied to the user's text is,
  and none is permitted.

### 2.4 Tokenization verification (output 6) — the oracle

TOK-02 is verified, never assumed: silent divergence from the reference tokenizer degrades the
encoder to noise while appearing to work.

- **Oracle:** the HuggingFace `tokenizers` library (Rust-backed), loading the candidate's **real
  `tokenizer.json`**, run in a throwaway Python venv. Verified working here on Python 3.14,
  `tokenizers` 0.23.1.
- **Under test:** the .NET-side route (`Microsoft.ML.Tokenizers`, Route A) and/or tokenization
  embedded in the model graph (Route B), on `net10.0-windows10.0.19041.0`.
- **Comparison:** token id sequences, compared element-wise across the `diacritics`, `stripped`,
  `runtogether` and `abbrev` fixture categories. Not a documentation page (TOK-04).
- **The check is proven able to fail before a pass is trusted:** a deliberately corrupted vocabulary
  must make the comparison go red.

> **Python is the oracle, never the route.** TOK-03's "no non-.NET runtime dependency" binds the
> *shipped* tokenization route, which is what the product would execute. It does not bind a
> throwaway verification harness that lives outside the solution. A route that shells out to Python
> is not a route; a Python program that tells you whether the .NET route is correct is exactly what
> TOK-02 asks for.

---

## 3. Directory map

| Path | WP | Tracked? | Contents |
|---|---|---|---|
| `README.md` | 0.1 | ✅ | This file |
| `ARTIFACTS.md` | 0.3 | ✅ | Per arm: source, pinned revision, SHA-256, file sizes, quantization, licence status |
| `models/` | 0.3 | ❌ **git-ignored** | Encoder + tokenizer artifacts. Hundreds of MB. Never tracked (AST-05) |
| `split/` | 0.4 | ✅ | `build_split.py`, `train.csv`, `test.csv`, `SPLIT.md` — small text, committed so "no arm re-split" is auditable |
| `accuracy/` | 0.5, 0.6 | ✅ | The accuracy harness |
| `dotnet/` | 0.7 | ✅ | Throwaway .NET console harness. **Not added to `SmartStudyPlanner.slnx`** |
| `tokenizer-oracle/` | 0.7 | ✅ | The Python reference-tokenizer oracle |
| `results/` | 0.5–0.7 | ✅ | `baseline.json`, `arm_a.json`, `arm_b.json`, `runtime_<arm>.json` |

The report itself lives in `docs/reports/2026-08-25-encoder-pilot.md` (EVA-12 — never in the plan
or the specification).

---

## 4. Reference hardware (PRF-01) — an open escalation

Runtime outputs 3, 4 and 5 must be taken on the PRF-01 reference class: a 10th-generation Intel Core
mobile U-series CPU or equivalent capability, **8 GB RAM**, integrated graphics, Windows 10 build
19041 or a supported newer environment.

**The development machine is not that class** — it is materially faster — and PRF-03 forbids
treating a developer-machine-only number as the product floor. This is UQ-1 in the execution plan's
§16.1, and it is **an open owner escalation**, recorded in the report and raised at CP1. Outputs 6
(tokenization viability) and 8 (packaged size) are hardware-independent and are unaffected.

---

## 5. Guards active from this slice onward

| Guard | Where | Demonstrated red? |
|---|---|---|
| **AC-21** — no model binary tracked in git | `.github/workflows/ci.yml`, step *Assert no model binary is tracked*. Asserts over `git ls-files`, **not** the working tree | ✅ WP-0.1 — banned extension, oversized file, and `tokenizer.model` each made it exit 1 |
| **AST-05** — model artifacts stay untracked | `.gitignore` | Convenience only; the CI step is the contract |

---

## 6. Scope guards carried through every S0 task

- **Arm C (`hiieu/halong_embedding`) is NOT run and NOT acquired.** It is unlocked only by an
  explicit owner decision after A and B report (EVA-06). Acquisition is the first step of running it.
- **No prior benchmark claim** may appear in any output as evidence that one candidate is better for
  this project. The positional-encoding argument survives only as an architectural prior; the
  withdrawn VN-MTEB justification is not restored, quoted or paraphrased (EVA-07).
- **No memory ceiling is asserted.** Peak RSS is measured and reported against the 8 GB budget; the
  ceiling is derived at S4 (PRF-08, OP-4).
- **No single headline accuracy figure** is emitted anywhere, including intermediate JSON (EVA-08).
- **A null result is a complete, valid, successful outcome of S0** (PD-3).
