# ML Experimentation Lessons

> Distilled 2026-08-25 from the **S0 encoder pilot** — an offline evaluation that ran to completion,
> produced a **null result**, and stopped the initiative it was testing. Primary evidence:
> [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md) (measurements,
> limitations, and the CP1 owner ruling). Harness and pre-registered protocol: `tools/ml-pilot/`.
>
> **Nothing shipped from that pilot**, which is exactly why the method is worth keeping. This article
> is about *how to run an ML experiment whose answer you can trust* — including when the answer is
> "no". It sits beside [`machine-learning.md`](machine-learning.md), which covers building and
> shipping ML in this application.

---

## A pre-registered kill criterion is only worth what the process pays to obey it

The encoder initiative wrote its kill criterion (**EVA-16** — *both arms fail to beat the baseline by
more than run-to-run variance → do not proceed*) **before any number was measured**. It then fired,
after three owner review rounds, a ratified specification and a 27-work-package execution plan had
already been written. **The process stopped.** S1–S4 were cancelled; no production code was written.

That is the whole lesson. A kill criterion costs nothing to write and everything to honour, and the
moment it costs something is the moment it is doing its job. **Sunk planning investment is the
pressure pre-registration exists to resist** — by the time the criterion fires, the case for "just
one more arm" is at its most persuasive and its least evidenced.

Three design details that made it enforceable rather than arguable:

- **Variance-relative, not a made-up effect size.** The comparison rule was fixed in advance: *an arm
  improves only if its **minimum** macro-F1 across seeds exceeds the baseline's **maximum***. The
  spec explicitly forbade inventing a fixed margin ("+2 F1") before or after the fact. A threshold
  chosen after seeing the numbers is not a threshold.
- **The tie-breaker is a guard, not an escape hatch.** A tie branch existed (*if the two arms cannot
  be reliably distinguished, unlock a third candidate*). It was ruled **inapplicable** rather than
  invoked: the arms *were* distinguishable, and both lost to the incumbent. The branch exists to stop
  you declaring a winner inside the noise — reading it as licence to keep running arms converts a
  clean null result into open-ended scope.
- **Finish the pre-registered outputs even after the answer is visible.** The direction was obvious
  once the baseline and one arm were measured. Completing the remaining outputs cost about an hour
  and produced **both findings that outlived the initiative** — the dataset gap and the production
  confidence-gate anomaly — neither of which the abbreviated version contains. A partial report also
  cannot be re-ruled on later without re-running the experiment.

**Corollary for the incumbent.** When no candidate wins, the status quo is not "undecided" — it is
**selected, on evidence**. The baseline stayed because it was measured and won, which is a stronger
position than the one it held before the pilot.

---

## Representation quality ≠ task-level improvement

This is the pilot's most reusable result, and it is easy to flatten into something false.

**What was measured — the encoders demonstrably work:**

| Check | Result |
|---|---|
| Vectors L2-normalised, all components distinct | ‖v‖ = 1.0000; 768/768 and 384/384 |
| Reproducible across repeat runs | max \|Δ\| = **0.00E+000** — bit-identical |
| Retrieves the stripped-diacritic partner of a fixture | **rank 1 in 5/8 and 6/8**, mean rank 1.75, against a chance rate of **1/8** |

**And yet, every encoder configuration scored *below* the n-gram baseline** on the downstream task:
baseline macro-F1 mean **0.6575**; the four encoder configurations **0.5934 – 0.6484**. Not inside
the baseline's noise — below it.

```text
representation quality  ≠  task-level classification improvement
```

**Why the two come apart.** An encoder that represents a variation correctly still has to convert
that representation into a *label*, through a head fitted on whatever training distribution exists.
It can encode a domain abbreviation perfectly and have no way to learn **what that abbreviation means
for this label set** from training rows that never contain it. Encoding is upstream of the thing
being scored, and an upstream win does not propagate on its own.

**What this evidence does NOT support.** Not *"neural encoders are worse"*, and not *"pretrained
embeddings don't help Vietnamese text"*. Four confounds travel with the result and must travel with
any citation of it:

1. **The head was untuned for dense features.** `SdcaMaximumEntropy` defaults were used for every
   arm, because the experiment required the featurizer to be the only variable. A dense 384/768-dim
   representation may well want different regularisation than a sparse n-gram one. The pilot measured
   *"the encoder, dropped into the existing head, untuned"* — **not** *"the best achievable result
   from this encoder"*.
2. **698 synthetic training rows**, largely lacking the surface forms the test set is made of (see
   the dataset section below).
3. **3 of 5 classes** present in the real evaluation set; two classes had zero real rows.
4. **One domain, one collection pass, one evaluation set** of 205 rows.

State the scope every time: this is evidence about *this encoder family, in this head, untuned, on
this dataset*. Anything wider is a claim the experiment did not buy.

---

## Verify the instrument before you believe a null result

**A broken embedder and a working-but-unhelpful encoder produce the same verdict.** The two
conclusions are nothing alike, so a null result is not reportable until the instrument has been
checked independently of the outcome it produced.

The checks that worked are in the table above: norm, distinctness, bit-level reproducibility, and a
**rank-based retrieval test against a known chance rate**.

**The check that didn't — recorded because it would have misled.** The first sanity test compared
absolute cosine *magnitudes* between paired and unpaired fixtures, and reported "no separation" for
both arms. **The test was wrong, not the encoders**: every fixture was same-domain Vietnamese student
task text, so unrelated pairs are legitimately similar and absolute cosine says almost nothing. Had
that version been trusted, a **sound null result would have been reported as a broken harness** — and
the initiative would have been revived on a false premise.

Two rules fall out:

- **On a same-domain corpus, absolute similarity is compressed. Use rank-based retrieval against a
  stated chance rate, not a magnitude threshold.** Rank is immune to the compression that breaks the
  magnitude test.
- **Report the discarded check; do not delete it.** A reader deciding whether to trust a null result
  needs to know the instrument was challenged and *how* it failed. A clean-looking report is worth
  less than one that shows where it changed its mind.

---

## Prove every check red before you trust its green

The project rule ([`review-methodology.md`](review-methodology.md), *"A green check is evidence only
after you've shown it can go red"*) has a specific and sharp form in ML tooling, because **ML
pipelines fail silently by design**: they are built to return an answer for any input.

What was done, and worth copying:

| Check | Proven red by |
|---|---|
| Fixture-set verifier | 4 mutations — dropped pair id, plain-token empty rows, truncated pathological rows, an injected `0xff` byte |
| Tokenizer equivalence | 2 mutations — byte-flip inside the piece table (**0/39 both arms**); dropping the required id offset (**0/39** on the arm that needs it, **unchanged** on the arm that does not) |
| Split integrity | Dropping 3 rows → `test 202 != 205`, exit 2 |
| CI guard against tracked model binaries | Proven red **in CI**, not just locally |

**The silent-failure example worth remembering.** One candidate's ONNX export needed a **fairseq +1
id offset** over raw SentencePiece ids to reach the tokenizer's id space. Without it:

```text
reference : [0, 41, 1294, 12, 6117, 19865, 13850, 8652, 14346, 39550, 858, 2]
raw       : [0, 40, 1293, 11, 6116, 19864, 13849, 8651, 14345, 39549, 857, 2]
```

A sequence that **looks entirely plausible and is wrong in every position**. The model still returns
a vector, the head still returns a label, and nothing throws. **Reading a documentation page would
not have caught it.** Only diffing element-wise against the candidate's *own* reference tokenizer,
loading the *real* vocabulary, did — and the oracle has to be an independent implementation, because
diffing a tokenizer against itself detects nothing.

**Name what the check cannot catch.** The tokenizer diff is blind to a wrong prompt prefix: it
prepends the same string to both sides, so an incorrect prefix passes cleanly. That was verified
against each model card separately instead. **An instrument's blind spot is part of reporting its
pass** — otherwise a green check silently covers ground it never touched.

---

## Build the split once, then make drift loud

Every arm consumed **one** split, constructed once and read verbatim. No arm re-split, and no arm
filtered.

- **Counts asserted in code, exit non-zero on drift** — not documented, asserted. Proven able to fire.
- **Source hashed** (SHA-256) so "the seed is unchanged" is checkable rather than remembered.
- **Determinism proven**: re-running the builder reproduces `train.csv` / `test.csv` byte-identically.
- **Leakage measured, not assumed**: exact overlap asserted 0 in code; near-duplicate overlap measured
  0 under diacritic- and punctuation-insensitive normalisation.
- **Near-duplicates were counted, never filtered.** Filtering would silently change the split the
  specification defines — a cleanup that makes the numbers prettier and the comparison invalid.

The point: **a shared split is a contract between arms.** Cross-arm numbers mean nothing unless
something actively asserts the split has not moved between them. "We used the same data" is a claim;
`exit 2 on drift` is evidence.

---

## Don't manufacture independence, and choose the input distribution before measuring

Two statistical traps the pilot hit and defused, both in the same way — by fixing the rule in advance
and writing down the amendment.

**Pooling across seeds does not create samples.** 205 test rows × 5 seeds is **not** 1 025
independent observations; it is the same 205 rows five times. Per-seed populations were treated as
primary, pooled views were **labelled non-independent**, and the raw `(row_id, seed, confidence,
correct)` tuples were persisted so a later reader can de-pool them. A confidence bin that looks 5×
more populated than it is will happily support a gate that has no evidence behind it.

**A percentile is a report about whatever you fed it.** Three pathological fixtures (2 040 / 5 269 /
20 159 characters) would have made up ~8 % of a 200-sample latency run and landed squarely on the
p95 — turning a latency-ceiling comparison into a report about a 20 000-character input no user
types. The fix: realistic inputs form the measured distribution; **pathological and empty inputs are
reported as named cases, not blended into the percentile**. Both are reported; neither contaminates
the other.

The rest of the latency protocol, fixed before any number existed: warm measurements only, warm-ups
discarded, p50/p95/max **all** reported, **no outlier removal of any kind**, no run discarded.

**Amendments are recorded, not silently applied.** Both of the above were changes to the protocol
made *before the numbers they govern* — and each was dated and written down. A protocol change made
after seeing results is a different thing entirely, and the only way to tell them apart later is the
date.

---

## A negative result is a deliverable

The pilot cost one throwaway harness and one report, touched **zero production symbols**, and
returned a decision. That is a successful outcome, and the plan said so in advance: a null result was
pre-declared a *complete, valid* conclusion, not a failure.

For a negative result to hold its value it has to be **discoverable and correctly bounded**:

- **Discoverable** — otherwise the next agent re-runs the same experiment. Keep it in
  `docs/knowledge/`, and keep the harness that produced it if re-deriving the numbers is otherwise
  expensive.
- **Bounded** — say what *would* count as new evidence. Here: the evidence points at **dataset
  expansion**, not a third encoder from the same family evaluated on the same rows. And expanding the
  dataset **does not by itself authorise a re-run**; that is a new owner decision with its own plan.

Distinguish four states that are easy to blur, and name which one applies:

| State | Means |
|---|---|
| **Stopped by evidence** | The experiment ran, the pre-registered criterion fired, the work stopped. *(This pilot.)* |
| **Failed technically** | The experiment could not be run or its instrument was broken. *(Explicitly ruled out here — see the instrument checks.)* |
| **Deferred** | Not evaluated; postponed to a later decision. |
| **Future hypothesis** | Not evaluated; no commitment of any kind exists. |

Only the first is a result. The other three carry no evidence, and a document that lets them read
alike will send someone chasing a conclusion nobody reached.

---

## Dataset maturity can be the binding constraint — an evidence-backed hypothesis

Measured off the committed split for this task:

| Measure | Value |
|---|---|
| Training vocabulary | 934 distinct tokens over **698** rows (100 % synthetic) |
| Test tokens unseen in training | **401 / 1 604 = 25.0 %** (23.3 % diacritic-insensitive) |
| **Test rows containing ≥ 1 unseen token** | **194 / 205 = 94.6 %** |
| The most common real domain abbreviation, `tgk` | **28** of 205 test rows · **0** of 698 training rows |
| Class coverage in real collected data | **3 of 5** — two classes had zero real rows |
| Class balance | Runs *opposite* to training: the smallest training class (85/698) is the largest test class (99/205); the largest training class (188/698) has no test rows |

**The engineering implication:**

> For this task, **dataset maturity may be a stronger bottleneck than encoder architecture.**

**This is an evidence-backed hypothesis, not a proven universal rule** — and it is offered as an
interpretation of the null result, not as grounds to overturn it. What supports it: both featurizers
were trained on a distribution largely lacking the surface forms they were tested on, which is a hard
setting for the n-gram baseline — **and the baseline still won**. It is also a hard setting for an
encoder, whose representational advantage has to be realised through a linear head fitted on that
same unrepresentative distribution.

What it does **not** establish: that a matured dataset would change the encoder verdict. Nobody has
measured that. It changes where the next measurement should be spent, and nothing else.

**The reusable form of the lesson.** When a synthetic training set feeds a model evaluated on real
input, measure the **unseen-token rate and the per-class coverage of the real set before drawing
architectural conclusions** — otherwise you are attributing to the model what belongs to the data.
And a held-out accuracy figure computed after real rows were merged into the training seed is **not**
a synthetic→real generalization number; it cannot be cited as one.

---

## Measuring on the wrong machine: a one-directional bound is not a substitution

The pilot's reference hardware class was a 10th-generation U-series mobile CPU with 8 GB RAM. The
only machine available was a 12th-generation H-series part with 16 GB — **materially faster**. The
project rule forbids treating a developer-machine number as the product floor.

Escalating with *no* number would have left the performance dimension blank. The resolution:

- A number from a **faster-than-reference** machine is **inadmissible for a pass** — it cannot show
  the product floor clears a ceiling.
- The same number is **decisive in the FAIL direction** — if a faster machine cannot hit the ceiling,
  a slower one will not either.
- So it was reported, **labelled valid in one direction only**, with the three options open to the
  owner (measure properly / accept the bound / treat as NOT RUN) stated — and the ruling was
  constructed so it did not depend on which was chosen.

**It paid off.** Unbounded pathological input took **2 622 ms** against a 500 ms ceiling — a failure
on hardware *faster* than the target, which is decisive. Escalating without measuring would never
have reached that conclusion.

**The rule: label which direction a number is admissible in.** Don't discard it for being off-spec,
and don't quietly promote it to a pass. And separately — **"not run" is a result; write it down.** A
blank cell in a criteria table reads as a pass to the next reader.

---

## Edge inference on this stack — measured observations, with their context

> **Measurement context, which travels with every number below.** Intel Core i7-12700H, 16 GB RAM,
> Windows 11 build 26200, .NET 10.0.9, **ONNX Runtime 1.29.0, CPU execution provider**. **Not the
> reference hardware class** — these are one-directional bounds (see above), not product floors.
> Candidates: **EmbeddingGemma-300M** and **multilingual-e5-small**, both as ONNX exports.

**int8 is not automatically the cheap option.** EmbeddingGemma-300M's int8 export ran **~6× slower**
than its own fp32 export (130.8 vs 21.1 ms p50) at **~2× the peak memory** (1 488 vs 772 MB), while
being the smallest configuration on disk (299.6 vs 1 182.3 MB packaged). multilingual-e5-small's int8
export behaved as expected (3.7 vs 5.3 ms p50). **This is not a general claim that int8 is slower
than fp32** — it is a property of *that export, on that runtime, on that CPU*. The transferable part:
**quantization is a trade, not a free win, and it has to be measured per export.** A single-precision
pilot would have reported the opposite size/speed story with equal confidence.

**Model size does not predict load time.** The **smaller** model (448 MB) cold-loaded roughly **twice
as slowly** (1 684 ms) as the **larger** one (1 178 MB, 898 ms). The larger export splits weights
into an external data file the runtime memory-maps; the smaller is a single self-contained protobuf
that must be parsed. Cold-start across all configurations was **0.9–1.7 s** — not negligible, and it
would have had to be paid off the startup path and once per session, not per invocation.

**Unbounded input length is a latency hazard, and bounding is not optional.** A 2 048-token context
window took **2 256 ms** on a 20 159-character input; the 512-token candidate was **flat** across
input sizes because truncation happened first. If encoder work is ever revived here,
**input-length bounding is required** — with the caveat that truncation must not silently change a
user-visible field without provenance saying so.

**In-graph tokenization was unavailable for both candidates.** Both ONNX exports took `input_ids` /
`attention_mask` as inputs, so a host-side tokenizer was the only route — a measurement about the
available artifacts, not a preference. On this stack that route worked fully offline with no non-.NET
runtime dependency, and **`Microsoft.ML.Tokenizers` 2.0.0 declares no dependency on `Microsoft.ML`
at all**, so it resolves cleanly beside the pinned `Microsoft.ML` 3.0.1 — adding a tokenizer implied
no version change to any package shared with the existing ML models.

**Tokenizer divergence, characterised on its axis.** Comparing the .NET SentencePiece tokenizer
against the reference implementation over a 181-case whitespace/punctuation/emoji corpus, **every**
divergence in both candidates lay on the **leading/trailing-whitespace normalisation** axis — 20/20
agreement on inputs with no surrounding whitespace, 181/181 with both sides trimmed. None arose from
Vietnamese diacritics, run-together tokens, abbreviations, punctuation, digits or emoji. **A
divergence reported without its axis is a rumour**; measuring the axis is what turns "they sometimes
disagree" into a bounded, dispositionable fact.

**An anomaly left unexplained, on purpose.** One candidate's int8 export scored **0.047 macro-F1
higher** than its own fp32 export — roughly 2× that arm's own standard deviation, and the wrong
direction for quantization. The plausible reading is quantization noise acting as a mild regulariser
when a 384-dimensional dense representation is fitted on only 698 rows, but **it was not
investigated**, because the ruling was unaffected either way: the *better* of each arm's two
precisions still lost to the baseline. It is recorded as **unexplained**.

That last one is itself the lesson: **name the anomaly you cannot explain.** A reader who spots an
unremarked oddity in your numbers reasonably stops trusting the numbers around it — and the cost of
saying "we saw this, we did not chase it, here is why it doesn't move the conclusion" is one
paragraph.

---

## See also

- [`machine-learning.md`](machine-learning.md) — building and shipping ML in this application:
  lifecycle, fallbacks, confidence policy, and **how to validate a confidence threshold**.
- [`review-methodology.md`](review-methodology.md) — *"A green check is evidence only after you've
  shown it can go red"*, *"Set the bar before you measure"*, and mutation-testing practice.
- [`qa-gates.md`](qa-gates.md) — *"A pass read through a faulty instrument is withdrawn, not
  defended"*; observation vs. ruling vs. inference.
- [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md) — the primary
  evidence: all measurements, all limitations, and the CP1 owner ruling appended verbatim.
- [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.4 — the one finding deferred out of
  that pilot as a separate investigation candidate.

## Sources

- `docs/reports/2026-08-25-encoder-pilot.md` (S0 evaluation report + CP1 ruling, 2026-08-25).
- `tools/ml-pilot/README.md` §2 — the measurement protocol, pre-registered before any number existed.
- `tools/ml-pilot/split/SPLIT.md`, `tools/ml-pilot/ARTIFACTS.md`, `tools/ml-pilot/results/*.json`.
- `docs/plans/2026-08-24-edge-ai-encoder-adoption.md`,
  `docs/specs/2026-08-24-neural-encoder-smart-parser.md`,
  `docs/plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md` — the stopped initiative's
  proposal, ratified specification and execution plan, all `stopped_at_s0`.
