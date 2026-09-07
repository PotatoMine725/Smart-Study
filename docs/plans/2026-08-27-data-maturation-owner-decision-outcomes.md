# Data Maturation — Owner Decision Outcomes (filed)

> **Filed 2026-08-27.** Owner-authored review outcome for
> [`2026-08-26-data-maturation-coverage-expansion.md`](2026-08-26-data-maturation-coverage-expansion.md)
> (the proposal) and the decision surface recorded in
> [`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md).
> **Status: `ratified`** — the decision surface is closed. **Implementation is still not authorized**;
> the document says so twice, and this filing does not change that.
>
> **The text from "# Data Maturation — Owner Decision Outcomes" below is the owner's, verbatim and
> unedited.** It is a **ruling** in this project's evidence vocabulary — an authorised person's
> statement — not an observation. P-1 and P-2 in particular rest on **owner recall with no written
> collection record**; a downstream document repeating them must carry that grade with it.
>
> **Why this exists as a repository file.** The owner delivered it through `Prompt/`, which is
> gitignored. The decisions it carries are cited by the proposal, so the citation would otherwise point
> at nothing. Same reason, same shape as the 2026-08-26 handoff.
>
> **Relationship to the 2026-08-26 handoff.** This document *restates* P-1…P-3 and DFD-1…DFD-9b and
> *newly decides* Q-1…Q-5. Where the two wordings differ, **this one governs** — it is later, and it was
> written with the proposal in hand. One such difference is material and is recorded in §A.2.
>
> What this record obliges, and where each obligation was executed, is the **agent-authored** appendix
> at the end (§A). Do not read §A as part of the ruling.

---

# Data Maturation — Owner Decision Outcomes

## Status

The owner decision review for the Data Foundation / Data Maturation proposal is complete.

The decisions below are the authoritative inputs for the next proposal revision.

This document does **not** authorize implementation.

---

## Owner Decisions

### P-1 — `collected_v4.csv` provenance
**Status:** Resolved

Pipeline recalled by owner:

```text
Owner templates/examples
→ Meta AI generation
→ GitHub Copilot labelling
→ collected_v4.csv
```

Classification: synthetic / AI-authored, not real user data.

### P-2 — Previously untraceable 189 rows
**Status:** Resolved

The 189 rows originated from approximately 2,000 Meta AI-generated rows that GitHub Copilot aggregated into two datasheets. 136 eventually entered the production seed.

They are synthetic/AI-generated, even though their provenance is now known.

### P-3 — Taxonomy
**Status:** Ratified

Perform a **limited taxonomy review**, not a wholesale redesign.

Focus on retired-class transitions, major class collisions, ambiguous boundaries, and whether disagreement is caused by taxonomy semantics or annotation inconsistency.

The current five-class taxonomy remains the working baseline unless the review produces an explicit owner decision to change it.

### DFD-1 — Real Data Policy
**Status:** Ratified — Option A

The repository currently contains **no verified real/user-authored Smart Parser dataset**.

`collected_v4` and its related AI-generated lineage are synthetic/authored data.

Affected historical/specification/report references must receive a separate, traceable documentation correction pass.

### DFD-2 — Annotation Governance
**Status:** Ratified — Option A

Create a **full canonical annotation specification** before adding further labelled data.

It must define taxonomy, class definitions and boundaries, ambiguous examples, adjudication, label provenance, and guideline versioning.

### DFD-8 — Human Authority
**Status:** Ratified — Option A

The owner is the final authority for Gold labels.

AI may pre-label, suggest labels, detect disagreement, and assist Silver/training curation. AI may not autonomously establish Gold ground truth.

### DFD-5 — Provenance
**Status:** Ratified — Option C

Use both:

```text
Dataset-level datasheet
+
Row-level lineage
```

New rows must capture provenance at creation/ingestion time, including origin/collection event, provenance type, generator identity/version when applicable, label source, annotation-guideline version, dataset version, and licence metadata for imported data.

### DFD-3 — Gold
**Status:** Ratified — Option C

Maintain two distinct Gold concepts:

```text
Gold-A
human-verified authored data
→ label correctness / annotation regression

Gold-R
human-verified real user data
→ real-world evaluation / generalization
```

Never conflate them.

### DFD-4 — Real Evaluation
**Status:** Ratified — Option C

Use both:

1. bounded real-user collection outside the production app;
2. ongoing in-app organic accrual.

Keep their provenance and identities separate. Held-out real evaluation data must be reserved before training merge.

### DFD-6 — Synthetic Data
**Status:** Ratified — Option B

Synthetic data is allowed for **Silver/training augmentation only**.

It must follow the canonical annotation specification, carry provenance, identify generator/version, and pass validation before training use.

It must not enter Gold-R or held-out real evaluation.

Multi-model generation (e.g. Meta, Grok, Claude, Llama) is allowed as a generation mechanism, not as Gold authority.

### DFD-7 — External Data
**Status:** Ratified — A + conditional B

External datasets may be discovered, evaluated, and inspected for relevance/provenance/licensing. They are not approved for training ingestion.

Instrument use may be considered separately after owner licensing review.

### DFD-9a — Prediction Instrumentation
**Status:** Ratified — Option A

Raise the missing `PredictedMinutes` / `Confidence` telemetry as a separate shipped-code defect now.

Do not fold it into the Data Maturation proposal. Do not change any confidence threshold as part of this defect.

### DFD-9b — Future Telemetry Sources
**Status:** Ratified — Option A

Designate both `DifficultyLabelLogs` and `StudyTimeOutcomeLogs` as future potential real-data sources.

Actual use as training/evaluation data remains gated by provenance, privacy/consent, retention/handling, dataset contracts, and sufficient quantity/quality.

---

## Q-1 — Adjudication Effort

**Decision:** Agreed measurement

After S-2 annotation specification v1 exists:

- adjudicate 20 held-back rows;
- measure total time;
- measure time per row;
- record ambiguous cases;
- record guideline gaps discovered.

Use the result to estimate Gold-A workload rather than guessing.

## Q-2 — Real Data Access

**Decision:** B

Owner can access a small network of friends/students/other users for a bounded collection exercise. The scale and throughput are not assumed yet.

## Q-3 — Consent / Collection Model

**Decision:** C

Bounded real-user collection will happen through a **separate dataset collection exercise outside the production app**. Consent and collection metadata will be handled as part of that exercise.

Organic in-app accrual remains a separate track.

## Q-4 — Gold-R Collection Strategy

**Decision:** C — Hybrid

Use:

```text
Minimum floor for all five classes
        ↓
Do not force a 20/20/20/20/20 distribution
        ↓
Prefer naturally observed distribution
        ↓
Target important linguistic coverage gaps where justified
```

Do not invent fixed quotas before observing the real distribution.

## Q-5 — Dataset Maturity Model

**Decision:** B — Tiered maturity

Dataset maturity will be defined in progressive tiers rather than as one all-or-nothing gate.

The future proposal should define a tiered model (for example: unmanaged → governed → evaluation-ready → model-development-ready) without inventing arbitrary thresholds before evidence exists.

Hard invariants such as provenance completeness, held-out reservation, and Gold/Silver separation remain distinct from quality thresholds.

---

## Decision Phase Outcome

The owner decision surface is now sufficiently resolved to update the **Data Maturation & Coverage Expansion** proposal.

The next proposal revision should:

1. incorporate all ratified decisions above;
2. preserve unresolved implementation parameters as explicit open questions;
3. convert Q-1 into a planned measurement after S-2;
4. define the tiered maturity model without arbitrary thresholds;
5. separate foundational governance, real-data collection, evaluation, telemetry, and controlled expansion;
6. keep the Edge AI initiative stopped at S0;
7. keep DFD-9a as a separate defect.

Do not start implementation from this document alone.

---

# §A. Execution register — **agent-authored, not part of the ruling**

> Added 2026-08-27 by the agent that reconciled the proposal against this record. **Nothing below is the
> owner's text.** It exists so a later reader can tell what this ruling obliged, where each obligation
> landed, and what it deliberately did not do.

## A.1 What the ruling obliged, and where it went

The closing section sets seven requirements for the next proposal revision. All seven were executed as
**revision 2** of [`2026-08-26-data-maturation-coverage-expansion.md`](2026-08-26-data-maturation-coverage-expansion.md),
whose §9.3 tracks them individually so the work can be checked rather than trusted.

| Obligation | Source | Executed as | State |
|---|---|---|---|
| Incorporate all ratified decisions | Outcome 1 | Proposal §1 — a second input table carrying Q-1…Q-5, DFD-7's instrument route, and DFD-9a's no-threshold-change constraint | **Done** |
| Preserve unresolved implementation parameters as explicit open questions | Outcome 2 | Proposal §4.2 — every Q keeps its identity and gains a **residual open parameter** column. One residual did not map to a Q and was given its own ID, `R-1` | **Done** |
| Convert Q-1 into a planned measurement after S-2 | Outcome 3 | Proposal S-2 and §8 | **Done** — and see §A.3 |
| Define the tiered maturity model without arbitrary thresholds | Outcome 4 (Q-5) | Proposal §5 rebuilt: `T-0…T-3` ladder, with the three owner-named invariants split out as `I-1`/`I-2`/`I-3` | **Done** — the only threshold the ladder names is the Q-4 floor, left blank |
| Separate governance / real-data collection / evaluation / telemetry / controlled expansion | Outcome 5 | The telemetry material was pulled out of S-5's Track B into a new strand, **S-T**. Stage numbers unchanged, so inbound citations still resolve | **Done** |
| Keep the Edge AI initiative stopped at S0 | Outcome 6 | Unchanged from rev 1. DAT-04 stands | **Unchanged** |
| Keep DFD-9a a separate defect | Outcome 7 | [`2026-08-26-prediction-instrumentation-defect.md`](2026-08-26-prediction-instrumentation-defect.md) — shipped 2026-08-26, **no confidence threshold moved**, as this ruling requires. Its open end-to-end gate is tracked there | **Done** |
| File this ruling where the proposal can cite it | project convention | This document. `Prompt/` is gitignored | **Done** |

## A.2 One place where this ruling differs materially from the 2026-08-26 handoff

Both documents state **P-3**. The 2026-08-26 wording scopes the limited taxonomy review to
*"collision/boundary problems surfaced by the audit."* This one adds a fourth item:

> *"…and whether disagreement is caused by taxonomy semantics or annotation inconsistency."*

**That is not a fourth question about the taxonomy; it is a question about the other three answers, and
it decides whether S-2 can succeed.** If the 29.6% disagreement is annotation inconsistency, writing the
boundaries down closes it and S-2 *is* the fix. If it is taxonomy semantics, no guideline can make two
annotators agree, and P-3's own escape hatch — an explicit owner decision to change the taxonomy —
becomes live.

Rev 1 of the proposal did not have this item, because the 2026-08-26 wording did not contain it. It is
now S-1's fourth scope row and part of S-1's exit criteria. **Recorded here because the later wording
governs, and a reconciliation that read only the new *decisions* would have missed a change inside a
restated one.**

## A.3 What was deliberately not done

| Not done | Why |
|---|---|
| **Any implementation** | The ruling says *"does not authorize implementation"* and *"do not start implementation from this document alone."* Rev 2 changes what the proposal waits for — authorization instead of decisions — not what it is permitted to do |
| **S-1, the limited taxonomy review** | It is the proposal's immediate next step and needs no infrastructure, but it needs the owner: all four of its scope items resolve to owner rulings |
| **Filling in the Q-4 floor, the Q-5 thresholds, or a schedule** | Four of the five rulings are instructions *not to invent the number yet*. Supplying them would be the defect this workstream exists to correct |
| **Choosing who performs S-2's two independent passes (`R-1`)** | The three available shapes measure three different things and are not comparable, so the choice changes what threshold can honestly be pre-registered. Flagged, not decided |
| **Renumbering the stages** | Outcome 5 is satisfied by adding a strand. Renumbering would break every inbound citation for no gain |
| **Renaming the proposal file** | `2026-08-26` is its creation date. Four documents link to it; the revision is recorded in its banner and §9 instead |

## A.4 One thing this reconciliation caught that the ruling did not ask about

Rev 1 of the proposal was written on 2026-08-26 at ~15:57 and the DFD-9a fix shipped the same afternoon.
Two of rev 1's `[measured]` baselines — §2's telemetry row and M-8 — were **accurate when written and
stale within the hour**, and both are stated in the present tense. A reconciliation aimed only at the new
ruling would have carried both forward unchanged.

Both are corrected in rev 2 and the superseded wording is preserved in its §9.2, because the correction
is not simply *"now fixed"*: M-8's blocker moved from **impossible** to **pending volume**, and the ≥50-row
retrain gate has still never been met. **The fix removed the reason it was impossible; it did not make it
true**, and a document that recorded only the first half would over-claim.

`[observation]` The general point, filed because it will recur: **a document whose baselines are
`[measured]` acquires a maintenance obligation that a document of opinions does not.** Rev 1 marked its
§2 as measured, which is what made the staleness detectable — and what made carrying it forward a
factual error rather than a stale opinion.
