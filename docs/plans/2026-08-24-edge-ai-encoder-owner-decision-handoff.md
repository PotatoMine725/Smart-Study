# Owner Decision Handoff — Edge AI Neural Encoder Proposal

> **Historical record — added 2026-08-25.** This is the **round-1 ratification record**, preserved as
> written on 2026-08-24. Its "Current Status" section below (*proposal is a draft; not scope-frozen;
> implementation not started*) was **already superseded on 2026-08-24** by review rounds 2–3 and the
> owner's scope-freeze — the execution plan's §2.2 **D-3** ruled on exactly that, so a reader hitting
> it should not stall.
>
> **The initiative has since STOPPED at S0** (owner ruling, 2026-08-25, EVA-16 kill criterion). S1–S4
> were cancelled and no production code was written. **The decisions ratified here — PD-1 … PD-10 —
> are not withdrawn**, `ML_Heuristic_design.md` §9.1 included; they were simply never exercised past
> the S0 gate. Nothing below is rewritten.
>
> Outcome: [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md) ·
> Proposal (`stopped_at_s0`): [`2026-08-24-edge-ai-encoder-adoption.md`](2026-08-24-edge-ai-encoder-adoption.md) ·
> Lessons: [`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md)

## Purpose

This document records the owner's decisions made during the review of:

`Edge AI — Neural Encoder Adoption for the Smart Parser (M8-A → M10)`

The proposal is still a **draft / proposal**.

It is **NOT scope-frozen** and implementation must NOT begin from this handoff.

Use this document to reconcile the proposal with the owner's decisions and
produce the next planning-ready revision.

---

# Current Status

- Proposal status: `draft`
- Implementation: not started
- This review does not authorize implementation.
- The proposal must remain separate from the Master Plan until the owner
  explicitly approves activation.
- All decisions below are owner decisions and should be recorded as such.

---

# Ratified Decisions

## PD-1 — Conditional Neural Encoder Exception

**Status:** Ratified

The project does NOT remove the general restriction against deep learning.

Instead, introduce a narrowly scoped exception:

> Frozen, pretrained neural encoders may be used as feature extractors /
> featurizers inside existing prediction pipelines, provided the decision layer
> remains linear or deterministic and the existing confidence / fallback and
> offline-first architecture is preserved.

### Mandatory guardrails

1. Frozen only.
2. No fine-tuning in runtime or on-device.
3. Encoder acts as a feature extractor, not an autonomous decision-maker.
4. Existing linear / deterministic decision architecture remains authoritative.
5. Confidence and fallback policy remain in force.
6. Offline-first inference remains intact.
7. Existing ML model/deployment limits remain applicable.
8. This exception must not become a general permission for model sprawl,
   generative SLMs, or autonomous deep-learning components.

Update `ML_Heuristic_design.md` explicitly rather than silently working around
the old prohibition.

---

## PD-2 — ML Model Count Governance

**Status:** Ratified

The existing `1–2 ML submodels maximum` limit is counted by **deployed model
artifact**, not by the number of prediction heads.

Example:

```text
one shared frozen encoder
  ├── Task Type head
  ├── Difficulty head
  └── Temporal head
```

counts as **one deployed model artifact**.

However, prediction heads / capabilities are NOT unlimited.

Every new prediction capability still requires explicit owner approval through
an appropriate proposal / plan.

### Governance model

- Model artifact count controls deployment, runtime, maintenance, and asset
  surface.
- Capability/head count controls product scope and model responsibility.

Do not use a shared encoder as a loophole to silently add arbitrary heads.

---

## PD-3 — S0 Is a Hard Gate

**Status:** Ratified

S0 (offline pilot) is a mandatory gate before production implementation.

No production code may begin before the S0 report is accepted.

A null result is a valid outcome.

If the neural encoder does not demonstrate sufficient value over the current
n-gram baseline, the proposal stops at S0.

### Important clarification

The current real dataset covers only 3 of 5 classes. This is accepted for the
pilot.

S0 is answering:

> Does the neural encoder show enough evidence of value on the real data that
> we should continue?

It is NOT claiming that the dataset is already mature enough to be the final
production training/evaluation dataset.

### Separate long-term workstream: data maturity

The project should continue improving the dataset independently:

- collect missing classes;
- improve real-world coverage;
- reduce class imbalance where appropriate;
- capture Vietnamese linguistic variation;
- deduplicate / quality-filter;
- version datasets;
- build a stronger held-out evaluation set.

Do not make "perfectly mature dataset" a prerequisite for running the initial
pilot.

Also avoid treating perfect class balance as the goal. The goal is adequate
representation of real-world usage and important linguistic phenomena.

---

## PD-4 — S2 + S3 Ship Together

**Status:** Ratified

S2 (neural featurizer swap) and S3 (confidence recalibration / gating) are one
production release unit.

Do not ship S2 alone.

Reason:

Changing the featurizer changes the confidence-score distribution. Keeping the
old confidence threshold would silently change ML-vs-heuristic routing and
user-visible confidence without explicitly treating it as a behaviour change.

### Allowed execution structure

S2 and S3 may be experimentally / analytically separated internally:

```text
S2 measurement
    ↓
new confidence distribution
    ↓
S3 calibration / gate derivation
    ↓
one production release unit
```

The production state must not contain an uncalibrated S2-only intermediate.

---

## PD-5 — Single Build + Bundled Model + Size Cap

**Status:** Ratified

Use one installer / one build.

The model is bundled into the installer.

No first-run network download / model acquisition path is introduced.

Runtime capability determines execution tier:

```text
Tier 0
model unavailable
→ heuristic-only fallback

Tier 1
default
→ CPU execution provider

Tier 2
optional
→ DirectML acceleration when supported
```

### Size governance

The bundled model/package must remain below an **owner-defined maximum size
cap**.

If the model/package exceeds the cap:

- stop;
- do not silently switch to side-loading;
- do not silently increase the cap;
- do not silently add another model;
- reopen the owner decision.

### Important separation

Distribution policy:

```text
bundled model
```

Execution policy:

```text
CPU default
DirectML optional
```

Bundling does not mean every machine must use the neural path. Tier 0 must
remain functional.

---

## PD-6 — Evidence Integrity for Model Selection

**Status:** Ratified

The earlier VN-MTEB justification must not be used as evidence that
EmbeddingGemma is better for this project.

The RoPE-vs-APE argument may remain only as an architectural prior.

Model choice must be justified by project-specific S0 evidence.

Do not silently restore the withdrawn benchmark claim.

---

## PD-7 — Tokenization Route Is an S0 Decision

**Status:** Ratified

Do not choose tokenization route ahead of evidence.

S0 must test the actual viable path for each candidate on the intended C# /
`net10.0` runtime.

Candidates may use either:

- Route A — .NET tokenizer
- Route B — tokenization embedded in the ONNX graph

A candidate that has no workable, verified tokenization path on `net10.0`
must be rejected regardless of its offline accuracy.

Tokenization viability is part of candidate selection, not a post-selection
implementation detail.

---

## PD-8 — S0 Candidate Set

**Status:** Ratified

Initial S0 contains only:

- Arm A — EmbeddingGemma-300M
- Arm B — multilingual-e5-small

Do NOT run Arm C initially.

`hiieu/halong_embedding` may be reconsidered only if A + B produce insufficient
evidence to make a trustworthy decision.

This is a conditional extension, not part of the initial S0 workload.

---

## PD-9 — S0 Winner Criterion

**Status:** Ratified

Do not define an arbitrary fixed effect-size such as "+2 F1 points".

A candidate wins only when the evidence is sufficiently strong across the
relevant dimensions:

- improvement over baseline beyond run-to-run variance;
- per-class results are acceptable;
- confidence behaviour is usable;
- latency / RSS fit the hardware budget;
- tokenization path is viable.

If A and B cannot be distinguished reliably, do not force a winner.

Possible follow-up:

```text
A ≈ B
  ↓
Is more evidence justified?
  ├─ Yes → conditional Arm C / data expansion
  └─ No  → stop / defer
```

---

## PD-10 — Reference Hardware Floor

**Status:** Ratified

S0 runtime measurements should use a reference hardware class representing a
common student laptop.

Reference class:

- 10th-generation Intel Core mobile CPU, mainstream U-series class, or
  equivalent capability;
- 8 GB RAM;
- integrated graphics;
- Windows 10 x64 at the supported floor, or equivalent newer supported
  environment.

### Runtime rule

The S0 latency gate is measured using the **CPU execution provider** on this
reference class.

A discrete GPU is NOT required for baseline viability.

DirectML is an optional Tier 2 capability and is evaluated separately.

Do not benchmark only on the developer's machine and treat that as the product
floor.

---

# Additional Decisions Already Embedded in the Proposal

These were not separate owner-question loops but are accepted as the proposal's
current direction unless new evidence contradicts them:

## S0 as measured gate

S0 should report:

- per-class precision / recall;
- confidence-vs-accuracy curve;
- cold-start model load time;
- per-inference latency;
- peak RSS;
- tokenization viability;
- explicit limitations from the 3/5 class real dataset.

Keep accuracy and runtime experiments conceptually separate, while ensuring the
runtime path uses the actual .NET stack intended for production.

## CPU-first

CPU remains the authoritative baseline for viability.

DirectML is acceleration, not the minimum requirement.

## Multi-head future direction

Difficulty and temporal heads remain gated future slices.

Do not automatically activate S5 or S6 merely because the shared encoder is
accepted.

---

# Still Separate / Not Yet Activated

The following remain future or separately gated work:

## S5 — Difficulty Head

Before starting S5:

- count `DifficultyLabelLogs`;
- compare the observed volume against the trigger conditions in
  `Difficulty_ML_model_proposal.md`.

If the data volume is insufficient, stop and record the result.

Do not build the model merely because the encoder exists.

## S6 — Temporal Span Head

Design intent only.

Requires a separate plan.

Do not fold it into the initial encoder-adoption implementation.

## Data Maturity

Continue independently as a future engineering workstream.

Do not block S0 on perfect dataset maturity.

## Model Acquisition Beyond Bundling

No CDN, auto-update, or first-run download path is authorized by this decision.

---

# Owner Decisions That Were Intentionally Not Expanded

The original proposal contained several details that may require implementation
planning later.

Do not invent additional owner policy for them.

Examples:

- exact package-size cap value;
- exact installer packaging mechanics;
- exact DirectML probing mechanism;
- exact 500 ms measurement protocol beyond the reference hardware decision.

These should be converted into explicit planning questions at the next stage,
not silently decided by implementation agents.

---

# What the Agent Should Do Now

Do NOT implement.

Do NOT scope-freeze the proposal.

Instead:

1. Update the proposal to reflect all ratified decisions above.
2. Remove or rewrite any text that conflicts with these decisions.
3. Preserve the proposal's evidence / inference / recommendation labeling.
4. Make the owner decisions visible as explicit planning decisions.
5. Ensure S0, S1–S4 and future slices remain consistent with the decisions.
6. Keep the proposal as `draft / awaiting owner approval` unless the owner
   explicitly activates it.
7. Identify any remaining owner-level decisions that are genuinely blocking
   proposal activation.
8. Do not invent new questions merely to make the decision list longer.

---

# Output

Return:

### 1. Updated Proposal

Path to the revised proposal.

### 2. Decision Reconciliation

A concise table:

```text
Decision | Status | Proposal section updated | Consequence
```

### 3. Remaining Owner Decisions

Only genuinely unresolved decisions that must be made before scope freeze.

### 4. Conflicts / Inconsistencies Found

Any place where the original proposal still contradicts a ratified decision.

### 5. Readiness Assessment

State whether the proposal is now ready for owner review and possible
scope-freeze, or what still prevents that.

No implementation.

---

## Amendment, 2026-08-26 — `collected_v4` is not real data

**Provenance grade: ruling, not measurement.** Owner recall on 2026-08-26 established that
`datasheets/collected_v4.csv` was produced as *owner templates/examples → Meta AI generation → GitHub
Copilot labelling*. No collection record exists in or out of the repository, and no artifact
corroborates the recall — but it agrees with seven independently measured distributional regularities
and an exact quota match. The repository holds **zero verified real user rows**.

Ruling: [`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) (**DFD-1**) ·
Evidence: [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md) §E.5–E.6,
[`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md) §2 ·
Pass record: [`../reports/2026-08-26-data-foundation-correction-pass.md`](../reports/2026-08-26-data-foundation-correction-pass.md)

**Every description of `collected_v4` in this document as *real*, *collected* or *user-authored* is
withdrawn.** The load-bearing occurrences are marked in place above. The remainder are deliberately
**not** individually edited: rewriting them would erase what was believed when this document was
written, which is precisely what the amendment convention exists to preserve. Read the whole document
through this amendment.

**This is an owner record and its body is not edited** — not one word above this line is changed. The
amendment is filed here only so a reader of the 2026-08-24 ruling learns what a later ruling
established.

Where this record says *"the current **real** dataset covers only 3 of 5 classes"* and frames the S0
gate as *"value on the **real** data"*, the dataset in question is `collected_v4`, since established as
AI-generated and AI-labelled. **PD-1 … PD-10 are not withdrawn or reopened** — they were ratified on a
premise that later proved wrong, and the initiative they governed had already stopped on independent
evidence (the EVA-16 kill criterion, which fired on measurements this correction does not touch).

The later ruling — [`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md)
§18 — states explicitly that the Data Foundation decisions **do not reopen or reactivate** the Edge AI
initiative. Future model work needs new evidence and a new owner decision.
