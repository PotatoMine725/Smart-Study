# Neural Encoder for the Smart Parser — Specification

**Effective date:** 2026-08-24 · **Status:** **RATIFIED by the owner, 2026-08-24** ·
**Lifecycle:** **`stopped_at_s0`** — the initiative this specification governs stopped at its S0
research gate on 2026-08-25; **the ratification itself is untouched**

> ## Closure metadata — added 2026-08-25, amending no requirement
>
> **The initiative governed by this specification STOPPED at S0.** S0 ran; the **EVA-16 kill
> criterion fired** (neither candidate encoder improved macro-F1 over the shipped n-gram baseline —
> both scored **below** it, at both precisions); the owner accepted that result on 2026-08-25.
> **S1–S4 were cancelled, not entered. No production code was written and none will be.**
>
> - **Evidence + CP1 ruling:** [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md)
> - **Execution plan** (`closed`): [`../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md`](../plans/2026-08-24-edge-ai-neural-encoder-execution-plan.md)
> - **Proposal** (`stopped_at_s0`): [`../plans/2026-08-24-edge-ai-encoder-adoption.md`](../plans/2026-08-24-edge-ai-encoder-adoption.md)
> - **Durable lessons:** [`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md)
>
> **This block is metadata, not an amendment.** Per the CP1 ruling, **no normative document is
> amended by the closure (DOC-04)**: not one requirement, requirement ID, threshold or acceptance
> criterion below has been changed, and none should be read as withdrawn. What changed is the
> *world*, not the contract — the requirements were never exercised because the gate that stood in
> front of them said stop.
>
> **What is still in force.** `ML_Heuristic_design.md` §9.1 — the narrow exception permitting frozen
> pretrained encoders as feature extractors under eight guardrails — **remains in force**. It was
> ratified on its own merits (PD-1) and landed by S-SPEC (`d141db1`); the S0 outcome did **not**
> withdraw it. A future encoder proposal re-enters through *that* gate and through a new owner
> decision — **DAT-04 is explicit that expanding the dataset does not by itself authorise a re-run.**
> Reading §9.1 as dead would reopen a settled decision; reading this specification as pending
> implementation would send someone looking for an encoder that was never built. Both are wrong.
>
> **How to read the rest of this file.** As the ratified contract it is — the record of what *would*
> have had to be true. Passages describing S0 as dispatchable or a later slice as next state what was
> true on 2026-08-24; each carries a dated superseded marker where it appears.

**Derives from:** [`../plans/2026-08-24-edge-ai-encoder-adoption.md`](../plans/2026-08-24-edge-ai-encoder-adoption.md)
(the approved proposal) and
[`../plans/2026-08-24-edge-ai-encoder-owner-decision-handoff.md`](../plans/2026-08-24-edge-ai-encoder-owner-decision-handoff.md)
(the owner's ratification record, PD-1 … PD-10; PD-11 and PD-12 ratified in session)
**Amends nothing.** It reads with [`ML_Heuristic_design.md`](ML_Heuristic_design.md) §5.1, §6, §9.1,
§10 (already amended 2026-08-24, `d141db1`) and [`system_roadmap.md`](system_roadmap.md) §8, §9.1.

> ### Ratified — and never a gate
>
> **Ratified by the owner on 2026-08-24.** The decisions it records were ratified the same day, and
> slices S-SPEC → S3 were **scope-frozen and activated** then too. **S-SPEC is executed**
> (`d141db1`) and **S0 is dispatchable now.** Ratification confirmed that this text faithfully
> expresses decisions already made — it did **not** re-open, re-gate, or suspend that activation,
> and **S0 never waited on it.**
>
> Where this document and the proposal disagree, this document governs *what must be true*; the
> proposal governs *why* and the execution plan will govern *how*.
>
> *Superseded 2026-08-25 as to forward state:* **S0 ran and ended the initiative** — see the closure
> metadata above. The ratification stated here stands; only "dispatchable now" is out of date.

**Required-section map** (per [`README.md`](README.md)): **Scope** → §1 · **Goal** → below ·
**Contracts** → §2, §3, §5, §10 · **Acceptance criteria** → §12 · **Non-goals** → §13.

---

## Goal

Smart Add must classify real Vietnamese student input — abbreviations, stripped diacritics,
run-together tokens, slang — by encoding meaning rather than matching literal n-grams, without
weakening the heuristic-first, offline-first, deterministic-fallback guarantees the product already
holds.

---

## Spec integrity labels

Every statement in this document is one of five kinds. Ambiguity between them is the failure mode
this legend exists to prevent.

| Label | Meaning |
|---|---|
| **MUST / MUST NOT / SHOULD / MAY** | **Normative requirement.** Binding on the implementation. MUST-level items each carry an ID and an acceptance criterion. |
| **[fact]** | **Measured or verified fact.** Confirmed in code, in a merged document, or in the dataset at commit `980eec6` / `9c747be`. Not a requirement. |
| **[limit]** | **Accepted limitation.** Knowingly carried; must not be reported as a defect, and must not be silently outgrown. |
| **[gate]** | **Conditional gate.** The outcome is not yet known; behaviour or eligibility depends on a later measured result. |
| **[choice]** | **Implementation choice.** Deliberately left open for the execution plan. Naming it here would over-constrain. |

Requirement IDs are grouped by contract: `BEH` behaviour · `ARC` architecture · `AST` asset ·
`TOK` tokenization · `EVA` evaluation · `PRF` performance · `CNF` confidence · `DAT` data ·
`FLB` fallback · `REL` release.

---

# 1. Scope

## 1.1 In scope

| # | Capability |
|---|---|
| 1 | Replacing the featurizer of the **M8-A task-type classifier** with a frozen pretrained neural sentence encoder executed locally on the user's machine. |
| 2 | An abstraction seam through which text becomes a dense vector, such that the encoder can be absent without the application changing behaviour. |
| 3 | **Re-derivation of the confidence gate** for the score distribution the new featurizer produces, released together with the swap. |
| 4 | **Runtime execution tiering** — heuristic-only, CPU, optional DirectML — resolved by capability probe at runtime. |
| 5 | **Bundled distribution** of the encoder asset with the application, with no runtime acquisition path. |
| 6 | The **S0 pre-production evaluation gate** that decides whether items 1–5 proceed at all. |

Items 4 and 5 are in scope, but **two of their parameters are deferred to the S4 owner
checkpoint** — the delivery mechanism and the size-cap value (PD-11). See §14.

## 1.2 Out of scope — excluded regardless of outcome

These are excluded by ratified decision, not by sequencing. Re-entering any of them requires a new
owner decision, not a plan revision. Enumerated in §13.

## 1.3 Future / conditional scope — eligible only after a gate

**Nothing in this subsection is authorised by this specification.** Each item names the gate that
would make it eligible, and eligibility is not approval.

| Item | Gate that must pass first | Approval still required after the gate? |
|---|---|---|
| **S5 — difficulty head** | (1) explicit owner approval of the capability (PD-2 governance); **and** (2) a count of `DifficultyLabelLogs` measured against the trigger conditions in `Difficulty_ML_model_proposal.md`, with an insufficient count recorded as a result and the slice stopped | **Yes — both gates are separate** |
| **S6 — temporal span head** | its own plan, commissioned separately | **Yes** |
| **Arm C** (`hiieu/halong_embedding`) | Arms A and B together failing to produce evidence strong enough for a trustworthy decision (PD-9 tie branch) | **Yes — owner decision after A and B report** |
| **S4 delivery mechanism and size cap** | S0 accepted, with measured packaged size in hand (PD-11) | **Yes — owner checkpoint at S4** |

**A shared encoder does not activate any head.** Acceptance of the encoder is not evidence for a
capability that rides on it (PD-2).

---

# 2. System Behaviour Contract

## 2.1 Where the encoder participates

**BEH-01 (MUST).** The encoder participates in **task-type classification within Smart Add
quick-parse only**. Deadline extraction, difficulty defaulting, and every scheduling, balancing, and
risk computation remain unchanged and remain deterministic.

**BEH-02 (MUST NOT).** Encoder inference MUST NOT be invoked per keystroke. It runs **once per
explicit user submit** of the quick-parse action.

> Quick-parse is submit-triggered today — the input textbox updates a view-model property on change,
> but parsing is invoked only from the explicit command **[fact]**. BEH-02 makes that property a
> requirement rather than an accident, because wiring inference to the change notification would be a
> small edit with a large cost on the reference hardware.

**BEH-03 (MUST).** The parser MUST NOT gain any scheduling, allocation, or balancing responsibility.
Its responsibility still ends at structured extraction plus confidence
(`ML_Heuristic_design.md` §5.1 *Isolation Rule*).

## 2.2 Input

**BEH-04 (MUST).** The encoder path accepts the **raw user input string as typed**. It MUST NOT
require diacritic restoration, word segmentation, spelling correction, or any other
language-specific preprocessing as a precondition for correct operation.

> This is a real constraint, not a convenience: a candidate encoder that requires external word
> segmentation would import a non-.NET runtime dependency into the application. See TOK-03.

## 2.3 Output

**BEH-05 (MUST).** For a given input string, a given encoder asset, and a given execution provider,
the encoder produces a **fixed-length dense numeric vector of documented rank**, and the result MUST
be reproducible across runs — identical inputs yield identical vectors, within a tolerance the
implementation documents and tests against.

**BEH-06 (MUST).** The vector is consumed as the **feature representation** for the existing linear
multiclass head. The head — not the encoder — produces the task-type label and its confidence score.

**BEH-07 (MUST).** The **structured output contract of quick-parse is unchanged**: the same fields
are produced, with the same types, and each parsed field still carries a provenance marker
distinguishing an ML-augmented value from a heuristic one.

## 2.4 Consumption, confidence, and heuristic authority

**BEH-08 (MUST).** ML output remains **advisory**. When confidence is at or above the gate, the
ML-derived task type MAY be applied; below the gate, the heuristic result is what the user receives
(`ML_Heuristic_design.md` §6). Confidence semantics are specified in §8.

> **Precedence note — §5.1 versus §6.** `ML_Heuristic_design.md` §5.1 frames the Smart Parser as
> *"ML-first"*, while §6 makes every ML output advisory with heuristic fallback. Where those two
> readings diverge, **§6 governs**, and BEH-08 is written to it. This initiative does **not** expand
> the parser's ML surface beyond task-type classification (BEH-01) — it changes how that one field is
> featurized, not how much of the parser is ML.

**BEH-09 (MUST NOT).** The ML path MUST NOT silently override heuristic logic, and MUST NOT produce
a user-visible result whose provenance the application cannot report.

**BEH-10 (MUST).** With the encoder asset absent, quick-parse behaviour MUST be **equivalent to the
legacy heuristic path** — same fields, same values, same provenance markers, for the same input.

## 2.5 Offline-first

**AST-01 (MUST NOT).** No component of the ML layer may perform a network operation at runtime —
not at first run, not at model load, not at inference, not for telemetry, not for updates. This is
an invariant of the product, and it is checkable statically (AC-04).

## 2.6 Lifecycle and cost placement

**BEH-11 (MUST NOT).** Encoder load MUST NOT block application startup, and an encoder lifecycle
exception MUST NOT fail startup (`docs/knowledge/machine-learning.md`, *Things to never do*).

**BEH-12 (MUST NOT).** Cold-start encoder load cost MUST NOT be paid on every parse. Session and
resource lifetime is otherwise **[choice]**.

**BEH-13 (MUST).** The existing head-retrain lifecycle MUST survive the swap intact: training from
the embedded seed, the seed-hash staleness gate, atomic model swap, and model-version increment all
continue to work.

---

# 3. Model Architecture Contract

## 3.1 Required

| ID | Requirement |
|---|---|
| **ARC-01** | The encoder MUST be **frozen and pretrained**. Its weights are immutable in the shipped application. |
| **ARC-02** | The application MUST NOT fine-tune, adapt, or otherwise update encoder weights **at runtime or on-device**, under any trigger. |
| **ARC-03** | The encoder MUST act as a **feature extractor only** — never an autonomous decision-maker. It emits a representation; it does not choose, schedule, or apply. |
| **ARC-04** | The **decision layer MUST remain linear or otherwise deterministic**, and MUST remain the authoritative producer of the label and its confidence. |
| **ARC-05** | The confidence and fallback policy of `ML_Heuristic_design.md` §6 MUST remain in force, unweakened. |
| **ARC-06** | Offline-first inference MUST be preserved (AST-01). |
| **ARC-07** | The encoder counts as **one deployed model artifact** against the `ML_Heuristic_design.md` §10 cap. Heads riding on it do not each count as an artifact. |
| **ARC-08** | **Each additional prediction head or capability requires its own explicit owner approval**, whatever the artifact count permits. A shared encoder MUST NOT be used to add heads silently. |
| **ARC-09** | Inference MUST run **locally on the user's machine**, through a runtime providing a **CPU execution provider** and an **optional DirectML execution provider** (§4.3). |

ARC-01 … ARC-08 are the eight PD-1 guardrails and the PD-2 governance model as recorded in
`ML_Heuristic_design.md` §9.1 and §10. They are reproduced here because this document is the
contract an implementer reads; §9.1 remains the normative source.

## 3.2 Implementation choice — open for the execution plan

| Item | Note |
|---|---|
| **Which encoder is adopted** | **[gate]** Decided by S0 evidence (PD-6, PD-9). This specification deliberately names **no winner**. The candidate set is fixed in §6.2. |
| Embedding dimensionality, and whether representation truncation is used | **[choice]** |
| Quantization of the encoder asset | **[choice]** — constrained only by AST-04 (size cap) and §7 |
| The specific inference runtime and its version | **[choice]** — constrained by ARC-09 |
| Session lifetime, threading, batching, warm-up strategy | **[choice]** — constrained by BEH-11, BEH-12 |
| The name and shape of the .NET abstraction, and its null implementation | **[choice]** — constrained by BEH-10, FLB-01 |
| Where the head's feature column is defined | **[choice]** — constrained by BEH-13 |

> **Not specified here on purpose.** The proposal's file map names concrete types. Those are the
> execution plan's to fix, and naming them in a product specification would freeze a design detail
> that S0's tokenization finding may still move (§5).

---

# 4. Model & Asset Constraints

## 4.1 Distribution

| ID | Requirement |
|---|---|
| **AST-02 (MUST)** | The encoder asset MUST be **bundled with the application** and present on disk after a normal installation, with no user action required. |
| **AST-03 (MUST NOT)** | There MUST be **no first-run download and no runtime acquisition path** — no CDN, no auto-update channel, no side-loading presented as a sanctioned route. |
| **AST-05 (MUST NOT)** | The encoder binary MUST NOT be committed to the git repository. |
| **AST-06 (MUST)** | The encoder MUST be treated as a **read-only asset**. Its location MUST be distinct from the writable trained-artifact store, and resolving or loading it MUST NOT require write access or create directories. |

> AST-06 exists because the current model-storage provider resolves to a writable per-user directory
> and creates it on construction **[fact]**. That is correct for trained artifacts and wrong for a
> bundled read-only one.

**The delivery mechanism that satisfies AST-02 is not yet chosen** **[gate]** — see §14. The repo
has no installer, packaging, or release pipeline today **[fact]**, and building one is deferred
behind Epic 2 **[fact]**. This is a real dependency of S4 and of nothing earlier.

## 4.2 Size

**AST-04 (MUST).** The bundled package MUST remain below an **owner-defined maximum size cap**.

**The cap value is not set** and is **not invented here** — see §14. On breach the ratified
instruction is unambiguous: **stop and reopen the owner decision.** Do **not** silently side-load,
raise the cap, or substitute a smaller model.

> The "1–2 GB acceptable, >2 GB reopens debate" remark recorded during requirements gathering is an
> **install-size preference, not the cap** **[fact]**, and MUST NOT be treated as one.

## 4.3 Execution tiers

| Tier | Condition | Required behaviour |
|---|---|---|
| **0** | Encoder unavailable — asset absent, unreadable, corrupt, or the inference session cannot be constructed | **Heuristic-only.** Fully functional application (BEH-10, FLB-01) |
| **1** | **Default** | Encoder on the **CPU execution provider** |
| **2** | **Opt-in**, DirectML-capable hardware present **and** the parity check of AST-08 passed | Same encoder, DirectML execution provider |

**AST-07 (MUST).** CPU execution is the **baseline and the default**. DirectML is acceleration only
and MUST NOT be a precondition for any specified behaviour or for meeting §7.

**AST-08 (MUST).** Tier 2 MUST be **opt-in** and MUST pass an **output-parity check against the CPU
provider** before being used for a user-visible result.

> A known metacommand defect between ONNX Runtime/DirectML and Intel drivers affects inference
> accuracy at certain dimensions **[fact]**. AST-08 is why Tier 2 is not trusted on availability
> alone.

**AST-09 (MUST).** **Tier 0 MUST remain functional** and MUST remain tested. Because bundling makes
Tier 0 look unreachable, it is a **fault-tolerance state**, not an install variant, and its
verification deliberately removes an asset the build placed (AC-03).

**Distribution policy and execution policy are separate.** Bundling the model does **not** mean
every machine runs the neural path.

---

# 5. Tokenization Contract

**TOK-01 (MUST).** The system MUST convert the raw input string into model input itself. Callers of
the parse path MUST NOT be required to supply tokens, ids, or masks.

**TOK-02 (MUST).** Tokenization MUST be **correct for the adopted encoder's own vocabulary** — it
must reproduce that encoder's reference tokenizer output for a documented fixture set spanning
Vietnamese with diacritics, Vietnamese with diacritics stripped, run-together tokens, and domain
abbreviations. Silent divergence from the reference tokenizer degrades the encoder to noise while
appearing to work, so this is verified, not assumed (AC-06).

**TOK-03 (MUST).** The tokenization path MUST work on the project's target runtime
(`net10.0-windows10.0.19041.0` **[fact]**), **fully offline**, with **no non-.NET runtime
dependency** (no JVM, no Python, no external process).

**TOK-04 (MUST) — route selection is a gate, not a design-time choice.** **[gate]** Two routes are
recognised: a **.NET-side tokenizer library**, or **tokenization embedded in the model graph**.
**Neither is selected by this specification.** S0 determines, **per candidate**, which route is
actually workable on the target runtime, verified by loading the real vocabulary — not by consulting
documentation (§6.3, output 6).

**TOK-05 (MUST).** A candidate encoder with **no workable, verified tokenization path** on the
target runtime MUST be **rejected regardless of its accuracy**. Tokenization viability is part of
candidate *selection*, not a post-selection implementation detail.

**TOK-06 (MUST).** If the route selected by S0 is later found unavailable or unworkable during
implementation, the implementation MUST NOT substitute a route or a candidate silently. The
candidate's viability is void; the other verified route is used if one exists for that candidate;
otherwise the candidate is rejected and, if no candidate survives, the initiative stops and the
owner decision is reopened.

**TOK-07 (MUST).** If adopting a route implies a **version change to an ML package shared with the
existing predictors**, that blast radius MUST be reported to the owner **before** it is taken, not
inside a dependency-addition commit.

> The relevant shared package is pinned at a version predating the .NET tokenizer work **[fact]**;
> a transitive bump would touch both shipped predictors. This is a reporting requirement, not a
> prohibition.

---

# 6. S0 Evaluation Specification

**S0 is a hard pre-production gate.** **EVA-01 (MUST NOT):** no production code for this initiative
may be written before the S0 report is **owner-accepted**.

**What S0 asks — and does not ask.** S0 answers: *does the neural encoder show enough evidence of
value on this project's real data that we should continue?* It does **not** claim the dataset is
mature enough to be the final production training or evaluation set. **A null result is a valid and
complete outcome.**

## 6.1 Dataset

| ID | Requirement |
|---|---|
| **EVA-02 (MUST)** | Train on the **synthetic subset only** — the 597 `m8a_uniform` and 101 `synthetic_v3` rows (698 total) **[fact]**. |
| **EVA-03 (MUST)** | Evaluate on the **205 held-out real `collected_v4` rows** **[fact]**, which MUST be excluded from training for this evaluation. |
| **EVA-04 (MUST)** | The split MUST be constructed **once** and consumed **verbatim** by every arm. No arm may re-split. |

> **EVA-03 as ratified 2026-08-24; partly superseded 2026-08-26 — see the Amendment at the end of this
> specification.** The word **real** and its **`[fact]`** tag are **withdrawn**: `collected_v4` is
> AI-generated and AI-labelled (DFD-1). **The requirement itself is unchanged** — the 205 rows, the
> hold-out, and the exclusion from training all stand exactly as written.

**[limit] Class coverage.** The real subset covers **3 of the 5 classes** — `ThiGiuaKy` 99,
`BaiTapVeNha` 56, `DoAnCuoiKy` 50; no `KiemTraThuongXuyen`, no `ThiCuoiKy` **[fact]**. This is
**accepted for the pilot** and is a reporting obligation (EVA-11), not a reason to defer S0.

**[limit] Data maturity.** The corpus is immature in the senses listed in §9. Also accepted for the
pilot.

**[fact] The published 96.2% held-out figure is not a generalization number.** The real rows were
merged into the training seed before it was measured. It MUST NOT be cited as a synthetic→real
baseline; EVA-02/03 construct the split that produces one.

> **Paragraph as written 2026-08-24; two of its three claims superseded 2026-08-26 — see the
> Amendment at the end of this specification.**
>
> - **Stands:** 96.2% is not a generalization number and must not be cited as a synthetic→real baseline.
> - **Withdrawn — the stated reason.** *"The real rows were merged into the training seed before it was
>   measured"* is chronologically impossible: 96.2% was measured 2026-06-05 at the 698-row v3 seed,
>   thirteen days **before** `collected_v4.csv` entered the repository (2026-06-18).
> - **Withdrawn — the closing clause.** *"EVA-02/03 construct the split that produces one"*: that split
>   is authored-vs-authored, so it cannot produce a synthetic→real baseline either.

## 6.2 Arms

| Arm | Featurizer | In the initial required experiment? |
|---|---|---|
| **baseline** | current production n-gram featurizer | **Yes** |
| **A** | EmbeddingGemma-300M | **Yes** |
| **B** | multilingual-e5-small | **Yes** |
| **C** | `hiieu/halong_embedding` | **No — conditional extension only** |

**EVA-05 (MUST).** Every arm uses the **same head family** and the same split, so that the featurizer
is the only variable.

**EVA-06 (MUST NOT).** Arm C MUST NOT be run as part of the initial S0 workload. It is unlocked only
by an explicit owner decision after A and B report, under the PD-9 tie branch. Running it "while
we're here" is a scope violation, not thoroughness.

**EVA-07 (MUST NOT).** No prior benchmark claim may be used as evidence that one candidate is better
for this project. The positional-encoding argument survives **only as an architectural prior**, and
the withdrawn VN-MTEB justification MUST NOT be restored.

## 6.3 Required measurements

**EVA-08 (MUST).** The S0 report MUST contain all eight of the following, per arm:

| # | Measurement | Where it must be measured |
|---|---|---|
| 1 | **Per-class** precision and recall for the three covered classes — **no single headline accuracy figure** | either harness |
| 2 | **Confidence-versus-accuracy relationship** — the input to the §8 recalibration, and not optional | either harness |
| 3 | **Cold-start model load time** | **.NET path, reference hardware** |
| 4 | **Per-inference latency** | **.NET path, reference hardware, CPU provider** |
| 5 | **Peak resident memory during inference**, reported against the 8 GB budget | **.NET path, reference hardware** |
| 6 | **Tokenization viability and route**, verified by loading the real vocabulary | **.NET path** |
| 7 | **Explicit statement of the limitations** arising from the 3-of-5 class coverage | the report itself |
| 8 | **Packaged on-disk size** — encoder plus tokenizer assets, as they would ship | either harness |

Output 8 is required under PD-11: the §4.2 size cap cannot be set to a defensible number before the
artifact is measured.

**EVA-09 (MUST).** Measurements 3, 4, 5 and 6 MUST be produced on the **same runtime stack the
product will use** — the real inference runtime, the real tokenizer, and the real head — on the §7
reference hardware class. Runtime numbers obtained from a different language stack do **not**
transfer and MUST NOT be used to satisfy these outputs.

> Accuracy and runtime may remain conceptually separate experiments. The runtime one must still run
> on the stack that ships, or it clears a gate it never tested.

**EVA-10 (MUST).** The report MUST **name the actual machine** each runtime measurement was taken
on. A developer-machine-only number is not an acceptable output.

**EVA-11 (MUST).** The report MUST state its own limitations in its own text — coverage, maturity,
and the measurement protocol actually used (§7.3) — rather than relying on this specification to
carry them.

**EVA-12 (MUST).** The report is written to `docs/reports/` and MUST NOT be written into the plan or
this specification.

## 6.4 Winner and kill logic

**EVA-13 (MUST NOT) — no fixed effect size.** No arbitrary threshold such as "+2 F1 points" may be
set, before or after the fact.

**EVA-14 (MUST) — a win requires all five dimensions.** An arm wins only when the evidence is strong
across **all** of:

1. improvement over baseline **beyond run-to-run variance**;
2. **per-class** results acceptable — not one class carrying the average;
3. **confidence behaviour usable** — the measured relationship can actually support a gate;
4. **latency and peak memory within the §7 budget**;
5. a **viable tokenization path** (TOK-05).

**EVA-15 (MUST) — the tie has an explicit branch.** If A and B cannot be reliably distinguished, the
process MUST NOT force a winner. The decision becomes whether more evidence is justified: if yes →
conditional Arm C and/or data expansion, by owner decision; if no → stop or defer. Declaring a winner
on a difference inside the noise is the specific failure this requirement prevents.

**EVA-16 (MUST) — kill criterion, stated in advance.** If both encoder arms fail to improve macro-F1
over baseline by a margin larger than run-to-run variance, the initiative **does not proceed to
implementation**. This is variance-based by construction; EVA-14 is a strictly higher bar than merely
surviving EVA-16.

---

# 7. Performance Specification

## 7.1 Reference hardware class

**PRF-01 (MUST).** Runtime measurements are taken on a reference class representing a common student
laptop:

- 10th-generation Intel Core mobile CPU, mainstream U-series class, **or equivalent capability**;
- **8 GB RAM**;
- integrated graphics;
- Windows 10 x64 at the supported floor, or an equivalent newer supported environment.

**[fact]** The build targets `net10.0-windows10.0.19041.0` with no platform-version override, so the
minimum OS the build admits is **Windows 10 build 19041** — tighter than "Windows 10 x64" generally,
and it subsumes the OS prerequisite of every runtime option considered here.

**PRF-02 (MUST).** The measurement surface is the **CPU execution provider**. A discrete GPU MUST NOT
be required for baseline viability.

**PRF-03 (MUST NOT).** Measurements MUST NOT be taken only on the developer's machine and treated as
the product floor. The exact machine used is an open parameter (§14) that the report must name
(EVA-10).

## 7.2 Latency

**PRF-04 (MUST).** Smart Add **submit-to-populate MUST stay under 500 ms** on the PRF-01 reference
class, on the CPU provider, with the model already loaded.

> **Provenance of the 500 ms figure.** It originated in the proposal, and the owner's handoff
> **presupposed** it — listing *"exact 500 ms measurement protocol"* among details intentionally not
> expanded — **without ratifying it as a decision** **[fact]**. The specification pass surfaced that
> gap, and **the owner ratified the figure explicitly on 2026-08-24 as PD-12**. It is now normative
> on an owner decision rather than on an unchallenged assumption.
>
> **What PD-12 does not settle:** the measurement **statistics** stay open (PRF-06). A ceiling and
> the protocol that makes it checkable are separate decisions; only the first is closed.

**PRF-05 (MUST) — the measurement boundary is normative.** Latency is measured from **invocation of
the quick-parse action to structured fields being populated**, and MUST include tokenization and the
encoder forward pass. Model load time is **excluded** from this boundary and reported separately
(EVA-08 output 3); this is what "model already loaded" means, and it is legitimate only because
BEH-12 forbids paying load cost per parse.

**PRF-06 (open) — the measurement statistics are not yet fixed.** Warm versus cold runs, the
percentile reported, and the sample count are an **open parameter owned by S0** (OP-3). Whatever is
chosen MUST be written into the S0 report **before** any number is compared against PRF-04.

> Splitting these two is deliberate, and PD-12 preserves the split rather than closing it. Fixing the
> **boundary** is what makes results comparable across arms and across slices; fixing the
> **statistics** is a planning question the owner declined to expand, and S0 owns it.

## 7.3 Memory

**PRF-07 (MUST).** **Peak resident memory during inference**, with the model resident, is measured
and reported against the 8 GB budget of PRF-01.

**PRF-08 (open).** **No peak-memory ceiling is asserted in advance.** The ceiling is derived from
S0's measurement and fixed at the S4 checkpoint (§14). Measuring first, then deriving, is required
precisely so the ceiling is not reverse-engineered from whatever the winning arm happened to use.

---

# 8. Confidence & Routing Specification

This section is a behaviour contract, not a tuning note. Changing what produces a score without
re-deriving the gate that reads it is a **user-visible behaviour change**, however much it looks like
a refactor.

## 8.1 What the gate may read

**CNF-01 (MUST NOT).** The routing gate MUST NOT rely on the model's **raw score as its only
signal**. This is an existing project rule — *"never trust raw model confidence as the only gating
signal — compare against the deterministic baseline"* **[fact]** — which the current task-type gate
does not satisfy **[fact]**, and which this initiative MUST NOT carry forward.

**CNF-02 (MUST).** At least one signal **independent of the model's own score** MUST contribute to
the routing decision. **Which** independent signal is used is **[choice]**; agreement with the
heuristic task-type parser is available at no additional cost, because that parser already runs on
every parse **[fact]**.

## 8.2 Recalibration

**CNF-03 (MUST).** The routing threshold MUST be **re-derived** from the confidence-versus-accuracy
relationship measured in S0 (EVA-08 output 2). **The existing 0.60 value MUST NOT be carried over
unexamined** — it is calibrated to the featurizer being replaced, and reusing it would silently move
the boundary between ML-augmented and heuristic results.

**CNF-04 (MUST).** The derivation MUST be recorded where the value lives — the date, the report it
came from, and the reasoning — so a future reader can tell a derived threshold from a guessed one.

**CNF-05 (MUST NOT) — no collateral retune.** Recalibration MUST NOT alter the effective thresholds
governing the **weight-optimizer review/apply tiers**, which share the same confidence-policy
abstraction **[fact]**. If the parser derivation implies a different number, the policies MUST be
separated rather than both retuned.

## 8.3 User-visible semantics

**CNF-06 (MUST).** The confidence percentage shown to the user MUST correspond to the **same quantity
the gate reads**. If recalibration changes what that number means or what value it typically takes,
that is a behaviour change and ships as one (REL-03).

**CNF-07 (MUST).** Below the gate, the **heuristic result is what the user receives**, and the
provenance marker on the parsed field MUST reflect that (BEH-07, BEH-08).

**CNF-08 (MUST).** Deterministic fallback MUST be preserved: with the encoder unavailable, routing
and output are equivalent to the legacy heuristic path (BEH-10).

## 8.4 Release coupling

**CNF-09 (MUST).** The featurizer swap and the recalibration are **one production release unit**.
A production state containing an uncalibrated swap MUST NOT exist.

They MAY be separated **internally** — and in fact must be, since the new score distribution cannot
be measured before the new featurizer exists. What is forbidden is shipping the intermediate.

---

# 9. Data Maturity Boundary

**[limit] The S0 evaluation is partial, and that is accepted.** The real evaluation subset covers
three of five classes and reflects a corpus that has not been deduplicated, versioned, or balanced
against real-world usage.

**DAT-01 (MUST NOT).** No claim of **general** production accuracy or generalization may be made from
the current 3-of-5-class real dataset. Results are reported per class and bounded by the coverage
statement (EVA-08 output 7, EVA-11).

**DAT-02 (MUST NOT).** Dataset immaturity MUST NOT be used as a reason to delay S0, and MUST NOT be
recorded as a **production acceptance failure**. It is a known, bounded limitation of the evidence,
not a defect in the implementation.

**DAT-03 (SHOULD) — separate ongoing workstream.** Dataset improvement continues independently of
this initiative: collect the two missing classes; improve real-world coverage and capture Vietnamese
linguistic variation; reduce class imbalance **where appropriate**; deduplicate and quality-filter;
**version datasets**; and build a stronger held-out evaluation set.

> **Perfect class balance is not the goal.** Adequate representation of real-world usage and of the
> linguistic phenomena that actually occur is. And a held-out split is only a generalization test if
> the rows were held out *before* training — the reason §6.1 spells out the split rather than
> inheriting one.

**DAT-04 (MUST).** Expanding the dataset does **not** by itself authorise re-running or reversing an
S0 outcome. A re-run after data expansion is a new owner decision.

## 9.1 Shared test fixture set

**DAT-05 (MUST).** A **single Vietnamese input fixture set MUST be committed to the repository** and
versioned with the code, and **every slice MUST use it** rather than introducing its own. It MUST
cover, at minimum: input with correct diacritics; input with diacritics stripped; run-together
tokens; domain abbreviations; empty and whitespace-only input; and pathologically long input.

> Four acceptance criteria compare behaviour against this set — tokenizer correctness (AC-06),
> the no-behaviour-delta claim for the seam slice (AC-13), output reproducibility (AC-17), and
> preprocessing independence (AC-30). Without one committed set they are four different claims
> wearing the same words, and "no behaviour delta" stops being comparable between slices.

---

# 10. Failure & Fallback Contract

**FLB-01 (MUST) — the unifying requirement.** In **every** condition below, quick-parse completes and
returns the heuristic result. No exception escapes the parse call, no user-visible error is raised, no
startup failure occurs, and the application remains fully usable.

| Condition | Required behaviour |
|---|---|
| **Model file missing** | Tier 0. Heuristic path; equivalent to legacy behaviour (BEH-10). |
| **Model load failure** — corrupt, truncated, wrong format, session construction throws | Tier 0. Failure is contained at load; the application does not retry per parse. |
| **Tokenizer failure** — vocabulary missing, unreadable, or incompatible with the loaded encoder | Tier 0 for the affected path. A tokenizer that cannot be trusted MUST NOT be used to produce a user-visible result. |
| **Inference failure after successful load** — exception, timeout, or malformed output tensor | This parse falls back to the heuristic result. Repeated failure MUST NOT degrade into an unbounded retry loop. |
| **Unsupported or unavailable execution provider** | Fall back to the CPU provider; if that is unavailable, Tier 0. A Tier 2 request that cannot be honoured MUST NOT fail the parse (AST-07). |
| **Tier 2 parity check fails** | Tier 2 MUST NOT be used for user-visible results; the CPU provider serves instead (AST-08). |
| **Confidence below gate** | Heuristic result is delivered, with provenance reflecting it (CNF-07). Not an error state. |
| **Malformed input** — empty, whitespace-only, or pathologically long | The parse call completes and returns the heuristic result. Any input-length bounding is **[choice]**, but truncation MUST NOT be silent in a way that changes a field the user can see without provenance saying so. |

**FLB-02 (MUST NOT).** An ML lifecycle exception MUST NOT fail application startup **[fact — existing
project rule]**.

**FLB-03 (MUST).** Every fallback above MUST be **observable to a test** — a fallback that cannot be
distinguished from success is not a fallback (AC-03, AC-09).

---

# 11. Release Boundaries

| Slice | What it is | Intended user-visible change | Gate to enter |
|---|---|---|---|
| **S0** | **Research gate.** Offline pilot; throwaway harnesses; no production code. | **None** | None — dispatchable now *(as written 2026-08-24; S0 has since **run** — see the note below the table)* |
| **S1** | **Encoder infrastructure seam.** The abstraction and its implementations exist; nothing consumes them for a user-visible result. | **None intended** — REL-01 | S0 report **owner-accepted** |
| **S2 + S3** | **One production release unit.** Featurizer swap plus confidence recalibration. | **Yes** — classification quality, routing, and the displayed confidence value all change | S1 complete |
| **S4** | Runtime tiering and model distribution. | Tier visibility and the Tier 2 opt-in | **Owner checkpoint** — delivery mechanism and size cap decided here (§14) |
| **S5** | Difficulty head. | — | **Not activated.** Two separate gates (§1.3) |
| **S6** | Temporal span head. | — | **Not activated.** Own plan, own approval (§1.3) |

> *Outcome, 2026-08-25 — the "Gate to enter" column is what was required; this is what happened.*
> **S0 ran and its report was owner-accepted — and acceptance meant *stop*, not proceed** (EVA-16).
> **S1, S2+S3 and S4 were therefore never entered and are cancelled.** S5 and S6 are **unchanged**:
> still not activated, and unaffected either way — a stopped encoder cannot activate a head (REL-04).
> The requirements in this table are not withdrawn; they were never reached.

**REL-01 (MUST).** S1 MUST NOT change user-visible behaviour. Its correctness is demonstrated by the
absence of a behaviour delta, not by a feature.

**REL-02 (MUST NOT).** S2 MUST NOT reach a production state without S3 (CNF-09).

**REL-03 (MUST).** The S2+S3 release MUST be described as a **behaviour change** in the changelog and
in its pull request — not as a refactor.

**REL-04 (MUST NOT).** S5 and S6 MUST NOT be folded into this initiative's implementation contract,
and MUST NOT be treated as activated by the encoder's acceptance.

---

# 12. Acceptance Criteria

Every MUST-level requirement appears at least once in the **Verifies** column. Criteria are stated so
that a reader can tell what a failure looks like.

| # | Criterion | Verifies | Observable from |
|---|---|---|---|
| **AC-01** | S0 report exists in `docs/reports/`, contains all eight measurements per arm, names the machine used, states its measurement protocol and its coverage limitations, and is **owner-accepted** before any production code exists. | EVA-01, EVA-02, EVA-03, EVA-08, EVA-10, EVA-11, EVA-12, PRF-03, PRF-06, DAT-01 | S0 report + owner sign-off |
| **AC-02** | Every arm's numbers derive from one split constructed once; runtime numbers (3–6) come from the .NET stack on the reference class; Arm C absent unless an owner decision unlocked it. | EVA-04, EVA-05, EVA-06, EVA-09, PRF-01, PRF-02 | S0 report + harness sources |
| **AC-03** | **With every model file deleted — including the bundled encoder asset — the application launches and Smart Add, Dashboard and Analytics all function.** Re-run at S2 **and** at S4. | BEH-10, CNF-08, AST-09, FLB-01, FLB-02, FLB-03 | Automated test where possible; manual QA at S2 and S4 |
| **AC-04** | An automated check asserts **no network-capable type is reachable from the ML layer**, and the check fails if one is introduced. | AST-01, AST-03, ARC-06 | Architecture/static test in CI |
| **AC-05** | Quick-parse invokes encoder inference **once per submit**; a test asserts that input-change notifications alone trigger no inference. | BEH-02 | Automated test |
| **AC-06** | Tokenization reproduces the adopted encoder's reference tokenizer output for the documented Vietnamese fixture set (diacritics, stripped diacritics, run-together, abbreviations). | TOK-01, TOK-02, TOK-03, DAT-05 | Automated test with committed fixtures |
| **AC-07** | The S0 report states a **verified** tokenization route per surviving arm; any arm without one is recorded as rejected. | TOK-04, TOK-05 | S0 report |
| **AC-08** | Any shared-ML-package version change implied by the tokenization route is reported to the owner **before** the dependency change is committed. | TOK-06, TOK-07 | Owner checkpoint record / PR description |
| **AC-09** | **A mutation test proves the confidence gate can fail** — a deliberately miscalibrated threshold makes a test go red. A gate whose pass is indistinguishable from a broken gate is not evidence. | CNF-01, CNF-02, CNF-03, FLB-03 | Automated test |
| **AC-10** | The shipped threshold's derivation — date, source report, reasoning — is recorded alongside the value. | CNF-04 | Code review of the S2+S3 PR |
| **AC-11** | **Weight-optimizer review/apply tier behaviour is unchanged** across the S2+S3 release; a regression test covers the tier boundaries. | CNF-05 | Automated regression test |
| **AC-12** | The displayed confidence and the gated quantity are the same value; the S2+S3 release is described as a behaviour change. | CNF-06, CNF-07, REL-03 | Code review + changelog entry |
| **AC-13** | S1 ships with **no behaviour delta**: the full suite passes at the pre-slice baseline count, and no parse output changes for a fixture corpus. | REL-01, BEH-07, BEH-13, DAT-05 | Automated suite + fixture comparison |
| **AC-14** | No production state exists in which the featurizer is swapped and the gate is not re-derived. | CNF-09, REL-02 | Release history / branch review |
| **AC-15** | Head retraining from the embedded seed still works after the swap: a stale seed hash forces retrain, the swap is atomic, and the model version increments. | BEH-13 | Automated test |
| **AC-16** | The encoder is loaded at most once per session and not on the startup path; startup succeeds with a corrupt asset present. | BEH-11, BEH-12, FLB-01, FLB-02 | Automated test |
| **AC-17** | Encoder output is reproducible for identical input within the documented tolerance, and the head's decision is unchanged for an unchanged vector. | BEH-05, BEH-06, ARC-03, ARC-04, DAT-05 | Automated test |
| **AC-18** | The encoder asset resolves from a **read-only** location distinct from the writable trained-artifact store, and resolution creates no directory. | AST-06 | Automated test |
| **AC-19** | Tiers 0, 1 and 2 are each exercised; Tier 2 is opt-in and is not used for user-visible results unless the CPU-parity check passed. | AST-07, AST-08, AST-09, ARC-09 | Automated test + manual QA at S4 |
| **AC-20** | Packaged size is recorded and compared against the owner-set cap; **the cap has a value before S4 packaging is implemented**; a breach stops the slice and reopens the owner decision rather than being absorbed. | AST-04 | S0 output 8 + S4 checkpoint record |
| **AC-21** | No encoder binary is present in the git repository at any commit of this initiative. | AST-05 | Repository check in CI |
| **AC-22** | The asset is present after a normal install with no user action and no network access; installing offline yields Tier 1, not Tier 0. | AST-02, AST-03 | Manual QA at S4 |
| **AC-23** | Smart Add submit-to-populate stays under the §7.2 ceiling on the reference class, measured over the boundary of PRF-05 using the protocol recorded in the S0 report. | PRF-04, PRF-05, PRF-06 | Runtime measurement at S0 and re-checked at S2+S3 |
| **AC-24** | Peak resident memory is measured and reported against the 8 GB budget; no ceiling is asserted before it is measured. | PRF-07, PRF-08 | S0 report |
| **AC-25** | The parser produces no scheduling, allocation, or balancing effect; isolation tests still pass. | BEH-01, BEH-03, BEH-08, BEH-09, ARC-05 | Automated test |
| **AC-26** | The encoder is frozen: no code path updates encoder weights, and no training of the encoder occurs in the shipped application. | ARC-01, ARC-02 | Code review + static check |
| **AC-27** | Exactly **one** deployed model artifact is introduced, and no additional prediction head exists without a recorded owner approval. | ARC-07, ARC-08, REL-04 | Owner checkpoint record + asset inventory |
| **AC-28** | Each failure condition in §10 has a test demonstrating the heuristic result is delivered and no exception escapes. | FLB-01, FLB-03, TOK-06 | Automated tests, one per row |
| **AC-29** | Reports and release notes carry the coverage limitation; no general-accuracy claim is made from the 3-class evaluation. | DAT-01, DAT-02, DAT-04, EVA-07, EVA-11 | Report and changelog review |
| **AC-30** | Preprocessing independence: classification quality is measured on inputs with diacritics stripped and with run-together tokens, without any segmentation or restoration step in the path. | BEH-04, DAT-05 | Automated test on the fixture set |
| **AC-31** | The tie branch is honoured: if A and B are indistinguishable, the report says so and no winner is declared. | EVA-13, EVA-14, EVA-15, EVA-16 | S0 report |
| **AC-32** | Architecture documents are updated **when S2+S3 ships**, and no normative document is amended as a side effect of an implementation commit. | DOC-03, DOC-04 | Doc review at the S2+S3 release + commit history |

`DAT-03` is SHOULD-level and describes a workstream outside this initiative's delivery; it carries no
acceptance criterion by design (§9). Every other MUST-level requirement appears above.

---

# 12.1 Traceability — ratified decisions to requirements

Each owner-ratified decision maps to the requirements that carry it. A decision with no requirement
would be a decision this specification lost.

| Decision | Substance | Carried by |
|---|---|---|
| **PD-1** | Narrow neural-encoder exception under a standing prohibition, with eight guardrails | ARC-01 … ARC-08; §13 (fine-tuning) |
| **PD-2** | Cap counts deployed artifacts, not heads; every capability separately approved | ARC-07, ARC-08, REL-04, §1.3 |
| **PD-3** | S0 is a hard gate; null result valid; 3-of-5 coverage accepted; data maturity is a separate workstream | EVA-01, EVA-16, DAT-01 … DAT-04, §9 |
| **PD-4** | S2 and S3 are one production release unit; internal separation allowed | CNF-09, REL-02, REL-03 |
| **PD-5** | One build; bundled model; no first-run download; size cap; tiers 0/1/2; Tier 0 stays functional | AST-01 … AST-09, ARC-09, §4.3 |
| **PD-6** | Withdrawn benchmark claim stays withdrawn; model choice justified by project evidence | EVA-07, OP-9 |
| **PD-7** | Tokenization route decided by S0; no viable path means rejection | TOK-04, TOK-05, TOK-06, OP-8 |
| **PD-8** | Initial arms are baseline + A + B; Arm C conditional | EVA-06, OP-11, §1.3 |
| **PD-9** | No fixed effect size; five-dimension win test; explicit tie branch | EVA-13, EVA-14, EVA-15 |
| **PD-10** | Named reference hardware class; CPU provider is the measurement surface; not the dev machine | PRF-01, PRF-02, PRF-03, EVA-09, EVA-10 |
| **PD-11** | Delivery mechanism and size-cap value deferred to S4 | OP-1, OP-6, AST-04, §1.1, §11 |
| *Handoff — measured gate* | The eight S0 outputs, accuracy and runtime kept separate but runtime on the shipping stack | EVA-08, EVA-09 |
| *Handoff — CPU-first* | CPU authoritative for viability; DirectML is acceleration | AST-07, PRF-02 |
| *Handoff — multi-head future* | Heads gated, not auto-activated by encoder acceptance | REL-04, §1.3 |
| *Handoff — not expanded* | Cap value, packaging mechanics, DirectML probe, 500 ms protocol left to planning | OP-1, OP-2, OP-3, OP-12 |
| **PD-12** | The 500 ms Smart Add latency ceiling is ratified; the measurement protocol stays S0's | PRF-04, PRF-05, PRF-06, OP-2 (resolved), OP-3 |
| *Owner direction 2026-08-24* | Conflicting documents reconciled against this spec; this spec governs where they disagree | §15 (DOC-01 resolved), DOC-03, DOC-04 |

---

# 13. Non-Goals

Excluded by ratified decision. Each requires a **new owner decision** to re-enter, not a plan
revision.

- **Generative SLM inference of any kind**, on any tier, for any field.
- **Windows AI APIs / Phi Silica / Aion Instruct / Foundry Local.** Ruled out permanently by the
  Windows 10 floor **[fact]**, independently of their roadmap.
- **Fine-tuning the encoder — prohibited, not merely declined** (ARC-01, ARC-02). Offline
  developer-side fine-tuning producing a new bundled artifact is **not** read as authorised by the
  runtime/on-device wording and would need its own owner decision.
- **Cloud model inference or storage.** The storage abstraction keeps a future swap possible; nothing
  uses it.
- **Any model acquisition beyond bundling** — no CDN, no auto-update channel, no first-run download,
  no sanctioned side-loading (AST-03).
- **Building the installer or release pipeline itself.** This initiative surfaces the dependency; it
  does not take on the work.
- **Uncontrolled model proliferation.** One artifact; every head separately approved (ARC-07,
  ARC-08).
- **The rule-based weight optimizer's ML replacement**, and **the study-time predictor retrain on
  focus telemetry**. Both remain where they are.
- **Epic 2 (LAN sync) and Epic 4 surfaces.** Untouched.
- **Any Epic 3 decision** — not reopened.
- **A second install variant or SKU.** One build, tiered at runtime.

---

# 14. Open Parameters

Genuinely unresolved inputs. **No value here has been silently assigned.** An entry that gets
resolved is **retained with its outcome** rather than deleted, so identifiers stay stable and the
record shows what closed, when, and by whose decision.

| # | Parameter | State | Owner | Must be fixed before |
|---|---|---|---|---|
| **OP-1** | **Package/model size cap value** | **Unset.** The "1–2 GB acceptable" remark is an install-size preference, not a cap **[fact]** | **Owner** | S4 implements packaging (AST-04, AC-20) |
| **OP-2** | ~~Confirmation of 500 ms as the ratified latency ceiling~~ | ✅ **RESOLVED 2026-08-24 — ratified as PD-12.** The ceiling is normative on an owner decision, no longer on a presupposition | — | — |
| **OP-3** | **Latency measurement statistics** — warm/cold, percentile, sample count | Open by decision; the *boundary* is fixed (PRF-05) and the *ceiling* is ratified (PD-12) — only the statistics remain | S0 | Any number is compared against the PRF-04 ceiling (PRF-06) |
| **OP-4** | **Peak-memory ceiling** | Not asserted; to be derived from S0's measurement against the 8 GB budget | S0 → S4 | S4 (PRF-08) |
| **OP-5** | **The exact reference machine used for measurement** | Class is fixed (PRF-01); the specific machine is not | S0 | S0 runs; must be named in the report (EVA-10, PRF-03) |
| **OP-6** | **Delivery mechanism for the bundled asset** | **Deferred to S4 by ratified decision.** Policy is settled (bundled, no runtime acquisition); the *mechanism* is not. The option set is recorded in the plan's §S4 | **Owner**, at S4 | S4 writes any packaging (AST-02) |
| **OP-7** | **How the application currently reaches its users** | **Unknown — no document in the repository records it** **[fact]** | Owner | OP-6 can be decided; the standing recommendation depends on this answer |
| **OP-8** | **Tokenization route, per candidate** | **[gate]** Determined by S0 output 6, not chosen in advance | S0 | S1 (TOK-04) |
| **OP-9** | **Which encoder is adopted** | **[gate]** Determined by S0 evidence; may be *none* | S0 → owner acceptance | S1 (EVA-14, EVA-15) |
| **OP-10** | **Post-swap confidence threshold value** | Derived, not chosen — from S0's confidence curve, subject to CNF-05 | S3, from S0 data | The S2+S3 release (CNF-03) |
| **OP-11** | **Arm C activation** | **[gate]** Conditional on the PD-9 tie branch and an explicit owner decision | Owner | Any Arm C work (EVA-06) |
| **OP-12** | Installer packaging mechanics; DirectML capability-probe mechanism | Planning questions the owner declined to expand; **not owner policy** | S4 | S4 implementation |

**Not open:** the acquisition **policy** (bundled — settled), the arm set for the initial experiment
(baseline + A + B — settled), the reference hardware **class** (settled), the **500 ms latency
ceiling** (ratified — PD-12), the win criterion's shape (settled), and the guardrails of §3.1
(settled).

---

# 15. Documentation Consistency

**DOC-01 — RESOLVED 2026-08-24.** The owner directed that the conflicting documents be reconciled
against this specification, which **governs where they disagree**. Six sites across four documents
were amended — each *narrowed and dated*, none rewritten:

| Document | Site | Amendment |
|---|---|---|
| `plans/2026-07-03-master-plan.md` | Epic 4 **Out of Scope** | "Deep learning" now defers to `ML_Heuristic_design.md` §9.1; "a third+ ML model" now reads **deployed model artifact** with §10's unit. Epic 4's scope, ordering and position are unchanged |
| `plans/2026-07-03-master-plan.md` | Epic 4 budget, gate, and 96.2% clauses | model budget names its unit; the ≥0.60 figures marked as today's value, re-derived under §8; 96.2% carries its provenance |
| `specs/system_roadmap.md` | §13 *DO NOT introduce deep learning* | points at the §9.1 exception, mirroring `ML_Heuristic_design.md` §9 |
| `specs/system_roadmap.md` | §9 cap, §9.1 gate, M8-A row | unit named; ≥0.60 marked as today's value; 96.2% annotated with its provenance |
| `specs/ML_Heuristic_design.md` | §5 cap restatement, §5.1 *ML Ownership* | cap points at §10's unit; §5.1 records that **§6 governs** where "ML-first" and the advisory policy diverge (§2.4) |
| `knowledge/machine-learning.md` | M8-A threshold in *project policy* | marked current-value, with §8's re-derivation requirement noted |

**DOC-03 (MUST).** Architecture documents under `docs/architecture/` describe **shipped** behaviour
and are correct as written. They MUST be updated when S2+S3 ships — not before — so they continue to
describe what the code does rather than what it is planned to do.

**DOC-04 (MUST NOT).** No further amendment to these documents may be made as a **side effect of
implementation**. Each is an owner-approved edit in its own right.

---

## Status & lifecycle

**RATIFIED by the owner, 2026-08-24 · `stopped_at_s0` since 2026-08-25.** Ratification confirmed that
this text faithfully expresses decisions already made; it was **not** a gate and did **not** hold S0
(see the note at the top). **The ratification stands. The initiative it governed does not** — S0 ran,
EVA-16 fired, and the owner stopped the work on 2026-08-25.

The sequence as specified, against what actually happened:

| # | Specified step | Outcome |
|---|---|---|
| 1 | ~~Owner reviews and ratifies this specification.~~ | ✅ **Done 2026-08-24**, together with **PD-12** (the 500 ms ceiling) |
| 2 | **S0 runs**; its report is accepted or rejected. Rejection ends the initiative — a valid outcome | ✅ **Ran. Report ACCEPTED 2026-08-25** — and under EVA-16 acceptance *ended* the initiative. The valid outcome is the one that occurred; see [the report](../reports/2026-08-25-encoder-pilot.md) |
| 3 | On acceptance, the execution plan for S1 and S2+S3 is written against this contract | ⛔ **Never reached.** No winner was declared, so there was nothing to write S1 against |
| 4 | **S4's parameters** (OP-1, OP-6) decided at the S4 checkpoint with S0's measurements in hand | ⛔ **Never reached.** CP3 never occurred: **OP-1** (size cap), **OP-6** (delivery mechanism) and **OP-4** (memory ceiling) **remain unset**, and the closure did not invent values for them. S0's output-8 sizes exist as measurements only |
| 5 | S5 and S6 remain unactivated, each requiring its own approval | **Unchanged.** Still unactivated |

**Retention.** This spec is **retained** as the ratified record of what was agreed, not as a contract
awaiting implementation — the capability it describes was never built, so there is no
`architecture/` description for it to hand off to (DOC-03 ties that to S2+S3 shipping, which never
happened). It is not superseded by a later spec; it was stopped by evidence. If the initiative is
ever revived, revival re-enters through a **new owner decision** with its own plan — not by editing
this file.

---

## Amendment, 2026-08-26 — `collected_v4` is not real data

**Provenance grade: ruling, not measurement.** Owner recall on 2026-08-26 established that
`datasheets/collected_v4.csv` was produced as *owner templates/examples → Meta AI generation → GitHub
Copilot labelling*. No collection record exists in or out of the repository, and no artifact
corroborates the recall — but it agrees with seven independently measured distributional regularities
and an exact quota match. The repository holds **zero verified real user rows**.

Ruling: [`../plans/2026-08-26-data-foundation-owner-decision-handoff.md`](../plans/2026-08-26-data-foundation-owner-decision-handoff.md) (**DFD-1**) ·
Evidence: [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md) §E.5–E.6,
[`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md) §2 ·
Pass record: [`../reports/2026-08-26-data-foundation-correction-pass.md`](../reports/2026-08-26-data-foundation-correction-pass.md)

**Every description of `collected_v4` in this document as *real*, *collected* or *user-authored* is
withdrawn.** The load-bearing occurrences are marked in place above. The remainder are deliberately
**not** individually edited: rewriting them would erase what was believed when this document was
written, which is precisely what the amendment convention exists to preserve. Read the whole document
through this amendment.

### What is **not** withdrawn

- **No requirement is amended.** EVA-01 … EVA-16 stand as ratified. They were never exercised past the
  S0 gate, and a factual correction to the prose around them does not withdraw normative text.
- **The `ML_Heuristic_design.md` §9.1 exception remains in force**, as recorded at the initiative's
  closure. This amendment does not touch it.
- **EVA-02/03/04 describe the split that was actually built and consumed.** The counts (698 / 205 / 903),
  the disjointness assertion and the one-split rule are all correct as written.

### What is withdrawn or re-scoped

| Passage | As ratified 2026-08-24 | 2026-08-26 |
|---|---|---|
| **EVA-03** | *"205 held-out **real** `collected_v4` rows" **[fact]**"* | The word **real** and its `[fact]` tag are **withdrawn**. `[fact]` was asserted without any document recording a collection — the specific governance failure the audit names `G-9` |
| **§6.1, 96.2% paragraph** | *"The real rows were merged into the training seed before it was measured"* | **Chronologically impossible.** 96.2% was measured **2026-06-05** at the 698-row v3 seed (n=106 fits 698 × 0.15, not 903 × 0.15); `collected_v4.csv` entered the repository **2026-06-18** (`8855874`, merged `ab5112c`) — thirteen days later. The **conclusion** — not a generalization number, do not cite as a synthetic→real baseline — **stands and is stronger** |
| **§6.1 closing clause** | *"EVA-02/03 construct the split that produces one"* [a synthetic→real baseline] | **Withdrawn.** The split is authored-vs-authored. S0 could not have produced a synthetic→real baseline, because no real evaluation data exists |
| **`[limit] Class coverage`** | *"The **real** subset covers 3 of the 5 classes"* | Counts unchanged (`ThiGiuaKy` 99, `BaiTapVeNha` 56, `DoAnCuoiKy` 50). "Real" withdrawn |

### The consequence for DAT-01

DAT-01 bounded reporting because the evaluation set covered 3 of 5 classes. That bound is now
**strictly wider**: no claim of production accuracy or generalization may be made from this
specification's evaluation design **at any class coverage**, because the evaluation set is not drawn
from the population the claim would be about. Re-running S0 on a larger authored corpus would not
lift the bound (**DAT-04** already says corpus growth alone does not authorise a re-run).
