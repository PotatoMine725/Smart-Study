# Data Foundation — Owner Decision Record & Handoff

> **Filed 2026-08-26.** Owner-authored ruling on
> [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md) (Phase 0
> audit) and [`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md)
> (decision brief). **Status: `ratified`** — the decision phase is closed.
>
> **The text from "# Data Foundation — Owner Decision Record & Handoff" below is the owner's, verbatim
> and unedited.** It is a **ruling** in this project's evidence vocabulary — an authorised person's
> statement — not an observation, and §2/§3 in particular rest on owner recall with no written
> collection record. Nothing below was measured by an agent; where a downstream document repeats these
> provenance facts it must carry that grade with it.
>
> What this record obliges, and where each obligation was executed, is the **agent-authored** appendix
> at the end (§A). Do not read §A as part of the ruling.

---

# Data Foundation — Owner Decision Record & Handoff

## Purpose

This document records the owner's decisions made during the review of:

- `docs/reports/2026-08-25-data-audit-gap-map.md`
- `docs/reports/2026-08-26-data-foundation-owner-decision-brief.md`

The decision phase is complete. These decisions are the input to the next phase:
writing the **Data Maturation & Coverage Expansion proposal**.

This document does **not** authorize implementation, dataset ingestion, synthetic
generation, or ML architecture changes by itself.

---

# 1. Final Owner Decision Summary

| ID | Status | Decision |
|---|---|---|
| **P-1** | Resolved | `collected_v4.csv` was generated from owner-provided templates/examples by Meta AI and subsequently labelled by GitHub Copilot. It is synthetic/AI-authored data, not real user data. |
| **P-2** | Resolved | The previously untraceable 189 rows originated from approximately 2,000 Meta AI-generated rows that GitHub Copilot aggregated into two datasheets; 136 eventually entered the production seed. |
| **P-3** | Ratified | Perform a **limited taxonomy review**, not a wholesale taxonomy redesign. Focus on collision/boundary problems surfaced by the audit. |
| **DFD-1** | Ratified | The repository currently contains **no verified real/user-authored Smart Parser dataset**. |
| **DFD-2** | Ratified | Establish a **full canonical annotation specification** before adding further labelled data. |
| **DFD-8** | Ratified | **Owner is the Gold authority**. AI may assist Silver/training curation, but may not establish Gold ground truth autonomously. |
| **DFD-5** | Ratified | Use **dual-layer provenance**: file-level datasheet + row-level lineage metadata. |
| **DFD-3** | Ratified | Establish two distinct Gold tiers: **Gold-A** for human-verified authored data and **Gold-R** for human-verified real user data. |
| **DFD-4** | Ratified | Build real evaluation evidence through **both** bounded real-user collection and ongoing in-app organic accrual. |
| **DFD-6** | Ratified | Synthetic data is allowed for **Silver/training augmentation only**. It is prohibited from Gold-R and held-out evaluation. |
| **DFD-7** | Ratified | External datasets may be evaluated as candidates. They are not approved for training ingestion. Instrument use may be considered separately after licensing is resolved. |
| **DFD-9a** | Ratified | Raise the missing prediction instrumentation as a **separate shipped-code defect now**, not as a deferred part of the Data Maturation proposal. |
| **DFD-9b** | Ratified | Designate both `DifficultyLabelLogs` and `StudyTimeOutcomeLogs` as **future real-data sources**, subject to provenance, privacy, and relevant data-contract prerequisites. |

---

# 2. P-1 — `collected_v4.csv` Provenance

Owner recall establishes:

    owner templates / examples
        ↓
    Meta AI generation
        ↓
    GitHub Copilot labelling
        ↓
    collected_v4.csv

Therefore:

- it is not real user data;
- its provenance is known at the process level;
- its labels are AI-assisted labels, not owner-verified Gold labels;
- it must not support real-world generalization claims.

---

# 3. P-2 — 189 Previously Untraceable Rows

Owner recall establishes:

    ~2,000 Meta AI-generated rows
        ↓
    GitHub Copilot aggregation / processing
        ↓
    two datasheets
        ↓
    lineage into the 189 rows
        ↓
    136 rows eventually entered the production seed

The rows are no longer “unknown origin” at the process level, but remain
synthetic/AI-generated. Provenance clarity does not establish label correctness
or real-user provenance.

---

# 4. P-3 — Limited Taxonomy Review

**Ratified.** Do not redesign the taxonomy wholesale.

Before the canonical annotation specification is finalized, review:

- retired-class transitions;
- major class collisions;
- ambiguous boundaries;
- whether disagreement comes from taxonomy semantics or annotation inconsistency.

The five-class production taxonomy remains the working baseline unless the limited
review produces an explicit owner decision to change it.

No silent taxonomy changes are permitted.

---

# 5. DFD-1 — Real Data Policy

**Ratified.** The project formally recognizes:

> The repository currently contains no verified real/user-authored Smart Parser dataset.

Consequences:

- `collected_v4` must no longer be described as real/collected/user-authored;
- the related AI-generated lineage is synthetic/authored data;
- real-world generalization claims remain disabled until Gold-R / held-out real evidence exists;
- affected historical/specification/report documents require a separate, traceable correction pass.

Do not rewrite history. Use dated amendments/corrections according to project documentation convention.

---

# 6. DFD-2 — Canonical Annotation Specification

**Ratified.** Establish a full canonical annotation specification before further labelled data is
collected, imported, generated, or promoted.

It must cover:

- taxonomy;
- class definitions and boundaries;
- ambiguous-example catalogue;
- adjudication procedure;
- label provenance;
- guideline versioning.

The limited taxonomy review (P-3) precedes finalization.

---

# 7. DFD-8 — Human Authority

**Ratified.** Owner is the final authority for Gold.

AI assistance is allowed for:

- candidate/pre-labelling;
- disagreement detection;
- Silver/training curation;
- prioritizing samples for review.

AI assistance must not silently establish Gold ground truth.

The solo-developer constraint is handled by using AI to reduce review volume,
not by delegating final Gold authority.

---

# 8. DFD-5 — Provenance Policy

**Ratified.** Use two complementary layers.

### File-level datasheet

Where applicable, describe:

- origin;
- collection/generation process;
- license;
- dataset version.

### Row-level lineage

Every new row must preserve sufficient metadata for:

- origin / collection event;
- provenance type (`collected`, `derived`, `generated`, `imported`, etc.);
- generator identity/version where generated;
- label source;
- annotation-guideline version;
- dataset version;
- license metadata where imported.

Capture provenance at creation/ingest time; do not rely on later reconstruction.

---

# 9. DFD-3 — Two-Tier Gold

**Ratified.** Keep two distinct concepts.

### Gold-A

Human-verified **authored** data.

Purpose:

- label correctness;
- annotation regression;
- adjudication;
- taxonomy/guideline validation.

It is explicitly **not** real-user evidence.

### Gold-R

Human-verified **real-user** data.

Purpose:

- real-world evaluation;
- generalization measurement;
- production-distribution analysis.

Keep the two datasets separately named, versioned, and traceable.

---

# 10. DFD-4 — Real Evaluation Evidence

**Ratified.** Use both:

### Bounded real-user collection

A controlled collection exercise with explicit consent and collection records.

Purpose:

- build an initial Gold-R;
- obtain deliberate class coverage;
- create a clean held-out real evaluation foundation.

### Ongoing in-app accrual

Organic real usage collected through the application.

Purpose:

- observe natural production distribution;
- supplement bounded collection;
- detect real-world phenomena not anticipated by curated collection.

The two sources retain separate provenance and dataset identity.

Held-out data must be reserved before training merge.

---

# 11. DFD-6 — Synthetic Data Policy

**Ratified.** Synthetic generation is allowed but controlled.

Synthetic data:

- may enter Silver/training augmentation;
- must carry generator and transformation provenance;
- must obey the canonical annotation specification;
- must pass validation/distribution checks before entering training;
- must not enter Gold-R;
- must not enter held-out real evaluation.

Important boundary:

> “Underrepresented in the current authored corpus” is not automatically the same as
> “underrepresented in real student behaviour.”

Until Gold-R / telemetry establishes production distribution, synthetic augmentation must not
make unsupported claims about real-world prevalence.

This policy permits future multi-model generation (Meta, Grok, Claude, Llama, etc.) as an
augmentation mechanism without letting those models become Gold authority.

---

# 12. DFD-7 — External Dataset Policy

**Ratified.** External datasets may be:

- discovered;
- evaluated;
- checked for relevance;
- checked for provenance;
- checked for licensing metadata.

They are **not currently approved for training ingestion**.

Instrument use may be considered separately.

Example: ViLexNorm may be evaluated as a linguistic normalization/instrument source; the
`CC BY-NC-SA 4.0` licensing question requires owner resolution before sanctioned use.

General rule:

    public/downloadable
        ≠
    relevant
        ≠
    licensed
        ≠
    approved for project use

---

# 13. DFD-9a — Prediction Instrumentation

**Ratified.** Raise the missing `PredictedMinutes` / `Confidence` instrumentation as an
independent defect **now**.

Reason:

- delay permanently increases unusable telemetry;
- historical rows cannot be repaired retrospectively;
- the fix is small and independent of the broader Data Maturation proposal.

This is a separate engineering task, not authorization to change the confidence gate.

---

# 14. DFD-9b — Telemetry as Future Real Data

**Ratified.** Designate both:

- `DifficultyLabelLogs`;
- `StudyTimeOutcomeLogs`

as future potential real-data sources.

This designation does **not** authorize immediate training or evaluation use.

Before use, they require:

- provenance metadata at write time;
- privacy/consent policy;
- retention/handling rules;
- relevant dataset/evaluation contract;
- adequate data quantity and quality.

Current interpretation:

- `DifficultyLabelLogs` already contains real human judgements but is small and currently unused.
- `StudyTimeOutcomeLogs` may eventually provide real study-time outcomes, but current usage has not reached the existing row-count gate.

---

# 15. Global Data Governance Principles

The following principles are now owner-ratified:

## Realness and label correctness are separate axes

A row can be real but incorrectly labelled, synthetic but correctly labelled, authored and correctly
labelled, or synthetic and incorrectly labelled. Never collapse provenance and label correctness.

## Gold is authority, not quantity

A small trusted Gold set is more valuable than a large ambiguous corpus for evaluation and adjudication.

## AI is an accelerator, not Gold authority

AI can reduce annotation effort but does not replace owner authority over Gold.

## Provenance is captured at creation time

Do not plan to reconstruct provenance later.

## Evaluation must remain protected

Real held-out data must be reserved before training merge.

## Synthetic data is augmentation, not ground truth

Synthetic data can increase coverage and diversity but does not establish real user behaviour.

---

# 16. Consequences for the Data Maturation Proposal

The next proposal must **not** be framed as:

> “We need more data, so generate more data.”

It should instead address this staged foundation:

    Limited taxonomy review
        ↓
    Canonical annotation specification
        +
    Owner Gold authority / adjudication
        ↓
    Provenance system
        ↓
    Gold-A
        +
    Gold-R collection
        ↓
    Evaluation foundation
        ↓
    Controlled Silver / public / synthetic expansion
        ↓
    Future model development

The proposal should distinguish:

- foundational controls;
- real-data collection;
- evaluation;
- public-data acquisition;
- synthetic augmentation;
- future model retraining.

---

# 17. Immediate Separation

The following must **not** wait for the Data Maturation proposal:

> **DFD-9a — missing prediction instrumentation**

Track it as a separate engineering defect because delay irreversibly loses future calibration data.

The broader Data Maturation work can proceed through proposal → specification → execution planning.

---

# 18. State of the Edge AI Initiative

The Edge AI / neural-encoder initiative remains **stopped at S0**.

The Data Foundation decisions in this handoff do not reopen or reactivate that initiative.

Future model work requires new evidence and a new owner decision.

---

# 19. Next Phase

The owner decision phase is complete enough to commission a formal:

**Data Maturation & Coverage Expansion Proposal**

The proposal must use this decision record as an input and must not re-open the above decisions
without explicit owner instruction.

Before proposing implementation, the new proposal should quantify:

- estimated owner effort;
- data collection throughput;
- privacy/consent implications;
- Gold-A adjudication scope;
- Gold-R collection strategy;
- provenance implementation cost;
- controlled synthetic-generation opportunities;
- public-dataset evaluation candidates;
- success criteria for dataset maturity.

---

# Decision Phase Completion

The owner decision surface is complete:

- P-1 through P-3 resolved/ratified;
- DFD-1 through DFD-9 resolved/ratified;
- enough policy is locked to write a Data Maturation proposal without inventing foundational rules.

Do not implement from this document alone.

---

# §A. Execution register — **agent-authored, not part of the ruling**

> Added 2026-08-26 by the agent that executed this record. **Nothing below is the owner's text.** It
> exists so a later reader can tell what this ruling obliged, where each obligation landed, and what
> it deliberately did not do.

## A.1 What the ruling obliged, and where it went

| Obligation | Source | Executed as | State |
|---|---|---|---|
| Correct the documents asserting `collected_v4` is real, by dated amendment | §5 (DFD-1) | 12 documents + 3 tool files. Record: [`../reports/2026-08-26-data-foundation-correction-pass.md`](../reports/2026-08-26-data-foundation-correction-pass.md) | **Done** |
| Close the audit's deferred decisions | §1, §2, §3 | Dated amendment on [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md) — `OD-1…OD-6`, `K.1`, `K.2` all closed | **Done** |
| Record the ruling against the brief that asked for it | §1 | *Owner ruling* section appended to [`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md), per the reports-README convention | **Done** |
| Raise the prediction-instrumentation defect **now**, separately | §13, §17 (DFD-9a) | [`2026-08-26-prediction-instrumentation-defect.md`](2026-08-26-prediction-instrumentation-defect.md) — root-caused, sliced, impact-analysed | **Raised. NOT implemented** — the ruling authorizes raising, not fixing |
| Commission the Data Maturation & Coverage Expansion proposal | §16, §19 | [`2026-08-26-data-maturation-coverage-expansion.md`](2026-08-26-data-maturation-coverage-expansion.md) | **Written. `draft`, awaiting owner review** |
| Record the state change even though nothing shipped | project convention | `docs/CHANGELOG.md`, entry dated 2026-08-26 | **Done** |

## A.2 What was deliberately not done

| Not done | Why |
|---|---|
| **The DFD-9a fix itself** | §Purpose withholds implementation authorization; §13 says *raise*. The defect record states the owner's go/no-go as its next event |
| **The limited taxonomy review (P-3)** | §16 places it as stage 1 of the pipeline the proposal must describe, and DFD-2 makes it the precursor to the annotation spec. Executing it ahead of the proposal would front-run the sequencing just ratified. It is scoped as **S-1** in the proposal |
| **Quantifying owner effort, collection throughput, privacy implications, Gold-R strategy** | §19 asks for them; the repository cannot answer them. They are filed as named open questions **Q-1 … Q-5** in the proposal's §4.2. Supplying plausible figures would repeat the defect this record exists to correct |
| **Rewriting the non-load-bearing "real" mentions in closed initiative documents** | §5: *"Do not rewrite history."* Each such document carries one amendment withdrawing the claim document-wide, and says the remaining instances are covered rather than missed |
| **Retrofitting lineage onto the existing 903 rows** | DFD-5 governs rows at creation. Backfilling would mean inventing provenance |
| **Reopening the Edge AI initiative** | §18 |

## A.3 One thing the ruling's §5 assumed that turned out to need care

§5 says the affected documents *"require a separate, traceable correction pass"*, and the audit's §F.3
listed eight of them. **The sweep found twelve.** Four had dropped the corpus name and said only *"the
real rows"* — including `docs/active/README.md` and a knowledge article, i.e. the two artifact types
most likely to be read after everything else is archived.

**And two of the documents already contained a correction that was itself wrong**: the roadmap footnote
and the encoder spec both explained the 96.2% figure by saying `collected_v4` had been merged into the
seed before it was measured. The figure predates `collected_v4` by thirteen days. Both are now replaced
with the verified chronology, and both say what the previous annotation got wrong rather than quietly
overwriting it.

This is recorded here because it bears on §15's *"Provenance is captured at creation time"* principle:
a correction written from assumption is a new unsourced claim wearing the clothes of a fix.
