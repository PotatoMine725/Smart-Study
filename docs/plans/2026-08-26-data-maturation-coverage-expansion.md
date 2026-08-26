# Data Maturation & Coverage Expansion — Proposal

**Status: `draft` — proposal awaiting owner review. Nothing here is authorized, scheduled, or
committed work.**

> Commissioned by the owner ruling of 2026-08-26
> ([`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) §19).
> Evidence base: [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md)
> (Phase 0 audit) and [`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md).
>
> **The ratified decisions are inputs, not topics.** P-1 … P-3 and DFD-1 … DFD-9 are cited here and
> **not reopened**. Where this proposal appears to raise one again, it is deriving a consequence, and
> the ruling wins.

---

## 0. The proposal in one page

**The project does not have a data-quantity problem. It has a data-authority problem, and quantity is
downstream of it.**

The repository holds **903 labelled rows and zero verified real ones**. But the binding constraint is
not the zero: it is that **two annotation passes over the same 703 rows disagreed on 29.6% of them**,
with no guideline, no adjudication record and no annotator identity anywhere in the repository. Until
that is fixed, every row added — collected, imported or generated — inherits an unknown label
correctness. Adding rows first multiplies the noise instead of the signal.

So the sequence this proposal defends is the one the ruling already prescribed, and the audit
independently derived as its single hard dependency:

```
    S-1  Limited taxonomy review        (P-3)          -- decides what the classes mean
      ↓
    S-2  Canonical annotation spec      (DFD-2, DFD-8) -- decides how a row gets a label
      ↓
    S-3  Provenance system              (DFD-5)        -- decides how a row proves where it came from
      ↓
    S-4  Gold-A            +            S-5  Gold-R    (DFD-3, DFD-4)
         authored, adjudicated               real, consented, held out
      ↓
    S-6  Evaluation foundation          (DFD-4, DFD-6)
      ↓
    S-7  Controlled expansion — Silver / public / synthetic (DFD-6, DFD-7)
      ↓
    S-8  Future model work                              -- needs a new owner decision (§18)
```

**Three properties of this staging are worth stating before the detail:**

1. **S-1 → S-2 → S-3 is a genuine dependency chain, not a preference.** Each stage's output is the
   other's input: you cannot write class definitions before deciding whether the classes are right,
   cannot label without definitions, cannot trust a label without knowing who applied which guideline.
2. **S-3 is cheap now and impossible later.** Provenance is captured at creation time (DFD-5) or not
   at all — the repository's own 136 untraceable rows are the proof, and no forensic pass has
   recovered them.
3. **Only S-1 and S-2 are on the critical path for everything else.** S-5's in-app accrual half and
   the DFD-9a instrumentation defect can run in parallel from day one, because they collect *outcomes*,
   not labels.

**What this proposal deliberately does not do:** propose a row-count target, recommend a dataset, or
estimate the owner's calendar. §4 explains which of §19's quantifications are measurable from the
repository and which are owner-only — and answers only the first kind.

---

## 1. What is already settled

Cited so the stages below can be read without re-deriving them.

| Input | Source | Consequence for this proposal |
|---|---|---|
| No verified real data exists | DFD-1 | Real-world claims stay disabled until S-5 produces Gold-R |
| `collected_v4` is AI-generated, AI-labelled | P-1 | It is a **Silver** candidate at best; it cannot be Gold-R and cannot be held-out evaluation |
| The 136 untraceable rows stay in the seed | P-2 | Not removed — but they can never be promoted to Gold, and S-3 must make that visible per row |
| Limited taxonomy review, not redesign | P-3 | S-1 is bounded: collisions and retired-class transitions only |
| Annotation spec precedes further labelled data | DFD-2 | S-2 gates S-4, S-5's labelling half, and all of S-7 |
| Owner is the sole Gold authority | DFD-8 | AI reduces review *volume*; it never closes a Gold label |
| Dual-layer provenance | DFD-5 | S-3 delivers both layers; no row is added without it after that point |
| Gold-A ≠ Gold-R | DFD-3 | Two datasets, separately named and versioned. Never merged into one "gold" |
| Both bounded collection and in-app accrual | DFD-4 | S-5 has two independent tracks with different provenance |
| Synthetic → Silver/training only | DFD-6 | S-7 may generate; the output can never reach S-6's held-out set |
| External datasets: evaluate, do not ingest | DFD-7 | S-7's public track produces an evaluation memo, not rows |
| Instrumentation raised now, separately | DFD-9a | Out of this proposal's scope — [`2026-08-26-prediction-instrumentation-defect.md`](2026-08-26-prediction-instrumentation-defect.md) |
| Telemetry designated future real-data source | DFD-9b | S-5's accrual track prepares it; it does not consume it |
| Edge AI stays stopped at S0 | Ruling §18 | S-8 exists as a placeholder. **Dataset growth alone does not authorise re-running the encoder experiment** (DAT-04) |

---

## 2. The starting position, measured

Everything in this table is `[measured]` from repository bytes by the Phase 0 audit. It is the
baseline any maturity claim will be measured against.

| Dimension | Current state |
|---|---|
| Production seed | **903 rows** — 698 train (461 derived + 101 `synthetic_v3` + **136 untraceable**) + 205 `collected_v4` |
| **Verified real rows** | **0**, in every class |
| Label stability | **29.6%** disagreement between two passes on the 703 rows whose old label survived the taxonomy change (208 rows). A further 325 rows moved because their class was retired |
| `Difficulty` stability | **16.2%** disagreement (167 / 1028) |
| Untraceable share | **15.1%** of the shipped corpus; **37.8%** of `KiemTraThuongXuyen`, **35.3%** of `ThiCuoiKy` |
| Class coverage in evaluation | **3 of 5.** `KiemTraThuongXuyen` and `ThiCuoiKy` have **zero** evaluation rows |
| Evaluation skew | `ThiGiuaKy` is 12.2% of training and **48.3%** of evaluation |
| Uncontaminated evaluation set | **None.** The shipped model trained on all 903 rows |
| Vocabulary gap | 25.0% of evaluation tokens unseen in training; **94.6%** of evaluation rows carry ≥1 unseen token |
| Corpus files | **8** labelled CSVs (7 in `datasheets/` + the embedded seed), 4–8 columns each |
| File-level datasheets | **0** for the corpora themselves |
| Row-level lineage fields | **2 of 7** required by DFD-5 (`Source`, `LabelVersion`) — and `Source` names a *file*, not an origin |
| Annotation guidelines / adjudication records / annotator identities | **0**, for either pass |
| Real telemetry available | `DifficultyLabelLogs` — real human judgements, small, **never read**. `StudyTimeOutcomeLogs` — real outcomes, but `PredictedMinutes` / `Confidence` written `null`; the ≥50-row retrain gate has never been met |

---

## 3. The stages

Each stage states its purpose, what must be true to start, what makes it done, and — where it
matters — what would make it fail silently.

### S-1 — Limited taxonomy review (P-3)

**Purpose.** Decide whether the five production classes mean what the corpus assumes, in the specific
places the audit measured a collision. **Not a redesign.** The five-class taxonomy remains the working
baseline unless this review produces an explicit owner decision to change it, and **no silent change
is permitted.**

**It is smaller than it reads.** The audit already partitioned the raw 51.8% label disagreement into
*"the taxonomy retired the old label"* (325 rows — a forced move, not a disagreement) and *"the
annotators genuinely disagreed"* (208 rows). S-1 inherits that partition rather than recomputing it.

**Scope, from measured evidence:**

| Item | Evidence | Question for the owner |
|---|---|---|
| Retired-class transitions | `OnTap→NhacNho` (105), `KiemTraThuongXuyen→NhacNho` (29) — two of the three largest transitions name **retired** classes | Were `OnTap` / `NhacNho` / `Khac` / `DuAn` right to retire? Does the app model reminders and revision at all? |
| The one genuine collision | `ThiCuoiKy→BaiTap` (26) | Where is the boundary between an exam task and coursework for it? |
| `Difficulty` semantics | 16.2% disagreement, transitions clustered at 5→4, 3→4, 1→3 | Is `Difficulty` an ordinal judgement with defined anchors, or a free impression? It is currently **never trained on** |
| Two classes with no evaluation data | `KiemTraThuongXuyen`, `ThiCuoiKy` | Do they earn their place in a 5-class taxonomy, or are they aspirational? |

**Exit criteria.** A written ruling per item: *keep / redefine / retire*, with the boundary stated in
words that an annotator can apply to a row. **Ambiguity resolved here is ambiguity S-2 does not have
to catalogue.**

**Silent-failure mode.** A review that "confirms the taxonomy" without writing down *why* each
boundary sits where it does produces no artifact S-2 can consume, and S-2 then re-litigates it.

---

### S-2 — Canonical annotation specification (DFD-2, DFD-8)

**Purpose.** The document that makes a label reproducible. Until it exists, DFD-2 bars further
labelled data from being collected, imported, generated or promoted.

**Required content, from the ruling:** taxonomy · class definitions and boundaries · ambiguous-example
catalogue · adjudication procedure · label provenance · guideline versioning.

**Two design constraints the evidence imposes:**

1. **The ambiguous-example catalogue is not hypothetical — it already exists as data.** The 208
   disagreeing rows *are* the catalogue's raw material, and the largest transitions name the boundaries
   that actually failed. A guideline written from imagination would miss `ThiCuoiKy→BaiTap` and dwell
   on distinctions nobody ever confused.
2. **Guideline versioning is load-bearing, not bureaucratic.** The corpus already contains rows labelled
   under two different implicit taxonomies with no marker distinguishing them. That is exactly the
   failure `LabelVersion` was meant to prevent and did not, because it versions the *file*, not the
   *guideline*.

**Owner authority (DFD-8).** AI may draft definitions, mine the corpus for boundary cases, and propose
adjudications. The owner rules. **The spec itself must record which parts were AI-drafted** — a
guideline written by the same class of system that produced the labels it is meant to govern is a
circularity worth marking.

**Exit criteria.** Versioned spec, `v1`, in `docs/specs/`. A test of it: **two independent passes over
20 held-back rows using only the spec, agreeing at a rate the owner sets in advance.** If it does not
reproduce, the spec is not finished — and the pre-registered threshold is what makes that check
capable of failing.

---

### S-3 — Provenance system (DFD-5)

**Purpose.** Make every future row able to prove where it came from, at creation time. **The one stage
whose cost rises monotonically with delay.**

**File-level datasheets** — origin, generation/collection process, licence, dataset version. Needed for
**8** existing corpus files, of which **0** have one. `collected_v4.csv` is the priority: its datasheet
is now writable, because P-1 established the process.

**Row-level lineage** — DFD-5 requires 7 properties; the corpus carries 2, and one of those is
mislabelled in meaning:

| DFD-5 property | Today |
|---|---|
| origin / collection event | **absent** |
| provenance type (`collected` / `derived` / `generated` / `imported`) | **absent** — `Source` names a file, which is why 136 rows are untraceable |
| generator identity + version | **absent** |
| label source (who/what assigned it) | **absent** |
| annotation-guideline version | **absent** — S-2 creates the thing to reference |
| dataset version | partial — `LabelVersion` |
| licence metadata (imported rows) | **absent** |

**Scope, measured:** ~5 new columns on the labelled-corpus schema (7 → ~12), 8 datasheets, and
provenance-at-write-time on 2 telemetry tables. The seed's consumer surface is narrow — the shipped
intent classifier reads **one** column of seven — so widening the schema is low-risk for the model
path and mostly touches tooling.

**A boundary this stage must respect.** DFD-5 governs **new** rows. Retrofitting lineage onto the
existing 903 would mean inventing it, which is the failure being corrected. Existing rows get their
*known* provenance recorded — including, explicitly, `provenance = unknown` for the 136 — and nothing
more.

**Exit criteria.** No row can enter any corpus without complete lineage, enforced by the ingest path
rather than by discipline. A check that can fail: **attempt to ingest a row with a missing field and
confirm it is rejected.**

---

### S-4 — Gold-A: human-verified authored data (DFD-3)

**Purpose.** Label correctness, annotation-regression detection, adjudication, and validation of the
S-2 guideline. **Explicitly not real-user evidence** — that separation is the point of DFD-3.

**Scope, measured — this is the one stage whose human cost the repository can bound:**

| Item | Volume | Why a human, and why it cannot be delegated |
|---|---|---|
| **J-1** — adjudicate the disagreeing rows | **208 rows** | Two passes disagreed; no third opinion exists. An automated tiebreak encodes whichever pass the current model was trained on |
| **J-3** — dispose of the untraceable rows | **≤136 rows** (decided, then applied) | P-2 rules they stay; whether they may ever be *promoted* to Gold is a separate judgement about acceptable provenance |
| **J-7** — rows where adjudication is itself contested | `[unknown]` until J-1 runs | The natural residue of J-1 |

**208 + ≤136 is the measured Gold-A adjudication scope.** It is a bounded, finite, already-enumerated
set of rows — not an open-ended labelling programme. **How long it takes is owner-only** (§4, Q-1).

**Where AI legitimately reduces the work (DFD-8).** Pre-label and rank by expected disagreement so the
owner reviews contested rows first; cluster the 208 by boundary so one ruling disposes of many; flag
rows where a pre-label contradicts the S-2 spec. **None of these closes a label.**

**Exit criteria.** `gold_a_v1` — every row carrying its adjudication record, the ruling owner, the
guideline version, and full S-3 lineage. Separately named and versioned from anything called Gold-R.

---

### S-5 — Gold-R: real-user data, on two independent tracks (DFD-4)

**Purpose.** The only stage that can produce evidence about actual student behaviour. **Everything the
project currently claims about real-world performance is waiting on this.**

#### Track A — bounded real-user collection

A controlled exercise with explicit consent and collection records, designed for **deliberate class
coverage** — because the two classes at zero evaluation coverage will not fix themselves by waiting.

Its measured target is unusually clear:

- `KiemTraThuongXuyen` and `ThiCuoiKy` need real rows because they currently have **zero**;
- `ThiGiuaKy` is over-weighted in evaluation (48.3%) relative to training (12.2%), so a real
  distribution is needed to know whether that skew is an artifact of the quota or a fact about students;
- the abbreviation vocabulary (`tgk` at 28/205 evaluation vs 0/698 training) is the clearest hypothesis
  the project holds about real input, and **it has never been tested against a real speaker**.

**Consent, sample size, recruitment and privacy handling are owner-only** (§4, Q-2 and Q-3). This
proposal does not design a human-subjects exercise.

#### Track B — ongoing in-app organic accrual

Real usage captured through the application, giving the natural production distribution — the
`[unknown]` that blocks the audit's §C.2 and every prevalence claim in the project.

**This track can start earliest and costs the least**, because the rows accrue from ordinary use. Its
prerequisites are exactly DFD-9b's: provenance at write time, privacy/consent policy, retention rules,
a dataset contract, and adequate volume. Two of them are already sitting in the repository:

- `DifficultyLabelLogs` — **real human judgements, already captured, never read.** The nearest thing to
  real labelled data the project owns.
- `StudyTimeOutcomeLogs` — real outcomes, but the prediction column is `null`; the
  [DFD-9a defect](2026-08-26-prediction-instrumentation-defect.md) is what unblocks it, and it is
  already raised independently.

**The two tracks never merge into one dataset.** Different consent basis, different provenance,
different sampling. DFD-4 requires separate identity and this proposal keeps it.

**Exit criteria.** `gold_r_v1`, owner-verified, with consent records, S-3 lineage, and **a held-out
partition reserved before any training merge** — the specific error `_merge_seed.py` already made once.

---

### S-6 — Evaluation foundation (DFD-4, DFD-6)

**Purpose.** The first honest answer to *"does this model work for actual students?"*

**Structural rules, all inherited:**

- Held-out data is reserved **before** training merge, never carved out afterwards.
- **No synthetic row and no `collected_v4` row may enter it** (DFD-6, DFD-1).
- Gold-A and Gold-R evaluate different things and are reported separately: Gold-A answers *is the model
  consistent with the label definitions*, Gold-R answers *does it work on students*. **Averaging them
  destroys the only distinction that matters.**
- Every historical figure is re-baselined against it, or stays scoped as authored-only. The correction
  pass already downgraded 96.2%, 97.24%/97.25% and the S0 comparison; S-6 is what could eventually
  replace them with something citable.

**Exit criteria.** A held-out real set that no model has seen, with its reservation recorded, and the
first real-input figure the project has ever produced — **whatever it says.** A disappointing number
here is a successful outcome of this stage.

---

### S-7 — Controlled expansion: Silver, public, synthetic

Only after S-2 (labels are reproducible) and S-3 (rows carry lineage). All three tracks are
**training-side only**; none may touch S-6.

**Silver.** Bulk-labelled training data — AI pre-labelled, spec-conformant, not owner-adjudicated.
`collected_v4` and the 136 untraceable rows land here: usable for training, never promotable to Gold.

**Public (DFD-7).** Evaluate, do not ingest. The audit checked three candidates and the structural
conclusion needs none of their names: **no public Vietnamese corpus carries this label space**, so every
imported row would need full relabelling under S-2 — which is why the public track is *behind* S-2 and
not a shortcut around it.

| Candidate | Standing after DFD-7 |
|---|---|
| **ViLexNorm** — >10,000 human-annotated normalization pairs, EACL 2024 | Highest value, and **as an instrument, not as data** — it addresses the teencode/abbreviation phenomena the audit could not measure. **`CC BY-NC-SA 4.0`: unresolved owner question (OD-4).** `NC` blocks any commercial distribution of this application |
| **UIT-VSFC** — 16,175 rows, Vietnamese student feedback | Right population, wrong task. **No licence field on the dataset card**; "free for research" in third-party sources is not a licence |
| **PhoATIS** — Vietnamese intent + slot filling | Right task shape, maximal domain distance (airline travel). **No licence surfaced.** Value is methodological |

*Rule carried forward verbatim:* `public/downloadable ≠ relevant ≠ licensed ≠ approved for project use.`

**Synthetic (DFD-6).** Permitted for Silver/training augmentation with generator provenance,
spec conformance, and distribution checks before entry. Multi-model generation (Meta, Grok, Claude,
Llama…) is allowed as a *mechanism* — none of those models becomes a Gold authority.

> **The trap this stage must not walk into**, stated in the ruling and worth repeating where the work
> happens: *"underrepresented in the current authored corpus" is not the same as "underrepresented in
> real student behaviour."* The corpus's measured gaps are gaps **in an authoring process**. Generating
> against them without Gold-R evidence would optimise the model toward a distribution nobody has
> observed — and the audit records **five linguistic phenomena as `[unknown]`**, so the measurement
> that bound would depend on does not currently exist. **A safeguard phrased as a measurement
> precondition is only as real as the measurement.**
>
> `[inference]` This project's track record with synthetic augmentation is poor: three passes
> (`synthetic_v3`, the untraceable 136, `collected_v4`), and the third was described as real in eight
> documents for two months. That is an argument for the controls, not against the technique.

**Exit criteria.** Every added row carries provenance, conforms to the spec, and passed a distribution
check that would have caught `collected_v4`'s seven regularities. **The check must be proven capable of
failing** — run it against `collected_v4` and confirm it flags it.

---

### S-8 — Future model work

Placeholder. Requires new evidence **and** a new owner decision (ruling §18). **DAT-04 stands: dataset
growth alone does not authorise re-running the encoder experiment.** After S-6, a revival would face a
*higher* bar than S0 did — real held-out evaluation data, which S0 never had.

---

## 4. Quantification, per §19 — and what cannot be quantified from here

§19 requires nine quantifications before implementation is proposed. **Four are measurable from the
repository. Five are owner-only.** Supplying plausible figures for the second group would be a literal
repetition of the defect this whole workstream exists to correct: numbers authored to a shape, read
downstream as measurements. They are stated as named open questions instead.

### 4.1 Measured from repository evidence

| §19 item | Answer | Basis |
|---|---|---|
| **Gold-A adjudication scope** | **208 rows** to adjudicate (J-1) + **≤136** to dispose of (J-3), plus an `[unknown]` residue of contested rows (J-7). A **bounded, enumerated** set — not an open programme | Audit §E.1, §E.5, §J `[measured]` |
| **Provenance implementation cost** | **~5 new columns** on the labelled-corpus schema (7 → ~12); **8 file-level datasheets** where 0 exist; provenance-at-write on **2 telemetry tables**. Model-path risk is low — the shipped classifier reads **one** of seven columns | Audit §B.1, §E.2 + CSV headers read this session `[measured]` |
| **Public-dataset evaluation candidates** | **3 checked, 0 approved.** ViLexNorm (`CC BY-NC-SA 4.0`, instrument-only, OD-4 unresolved); UIT-VSFC (16,175 rows, no licence field); PhoATIS (no licence, maximal domain distance). No public corpus carries this label space | Audit §I.2 — three dataset cards read at source `[fact]` |
| **Controlled synthetic-generation opportunities** | Nameable but **not yet actionable**: 2 classes at zero evaluation coverage, `ThiGiuaKy`'s 12.2%-vs-48.3% skew, an abbreviation vocabulary absent from training. All are gaps **in the authored corpus**; whether any is a gap in real behaviour is `[unknown]` until S-5. Generation targets cannot be set before then without inventing the target | Audit §C.1, §D.3, §G `[measured]` + the DFD-6 boundary |

### 4.2 Owner-only — open questions, not estimates

| # | §19 item | Why the repository cannot answer it | What is needed |
|---|---|---|---|
| **Q-1** | **Estimated owner effort** | Nobody has adjudicated a row under a spec that does not exist yet. Per-row time is unmeasured | Adjudicate **20 rows** after S-2 and time it. That converts every downstream estimate from a guess into arithmetic — and it is the cheapest measurement in this proposal |
| **Q-2** | **Data-collection throughput** | Depends on recruitment, which depends on who the participants are — a fact about the owner's context, not the codebase | Owner names the population and access route |
| **Q-3** | **Privacy / consent implications** | Consent basis, retention and handling are policy, and Track B collects from real users of a shipped application | Owner ruling; DFD-9b already lists the prerequisites |
| **Q-4** | **Gold-R collection strategy** | Sample size, class targets and stopping rule all depend on Q-1–Q-3 | Owner decision after Q-1's measurement |
| **Q-5** | **Success criteria for dataset maturity** | Partly derivable (§5), but the thresholds are the owner's risk appetite | Owner sets the numbers in §5's blanks |

`[inference]` **Q-1 is the keystone.** Four of the five unknowns are calendar questions that collapse
into arithmetic once one number exists: how long the owner takes to adjudicate one row under the S-2
spec. It is measurable in an afternoon, and it should be measured before any schedule is proposed.

---

## 5. Success criteria for dataset maturity

Stated as criteria with the thresholds left blank, because the thresholds are the owner's (Q-5).
**Every criterion is written so that it can come back negative** — a maturity definition nothing can
fail is a definition of nothing.

| # | Criterion | Measurement | Threshold |
|---|---|---|---|
| **M-1** | **Label reproducibility** | Two independent passes over held-back rows under the S-2 spec; report agreement. Baseline to beat: **29.6% disagreement** | owner |
| **M-2** | **Provenance completeness** | % of rows with all 7 DFD-5 properties. Today: **0%** for new-row standards; **15.1%** of the corpus is untraceable | owner (100% for rows added after S-3) |
| **M-3** | **Real evaluation existence** | Count of Gold-R held-out rows never seen in training. Today: **0** | owner (non-zero is itself a step change) |
| **M-4** | **Class coverage in real evaluation** | Classes with ≥ N real held-out rows. Today: **0 of 5** (and 3 of 5 even counting authored rows) | owner |
| **M-5** | **Evaluation independence** | Held-out set reserved before training merge, with the reservation recorded. Today: **never done** | binary — yes/no |
| **M-6** | **Distributional grounding** | Real class distribution observed rather than assumed. Today: `[unknown]`; the corpus was balanced to 1.11× against an unobserved distribution | owner |
| **M-7** | **Gold/Silver separation held** | No synthetic or `collected_v4`-derived row in Gold-R or in held-out evaluation | binary — automated check |
| **M-8** | **Instrument calibration measurable** | Confidence bins computable from real telemetry. Today: impossible — `Confidence` is `null` (DFD-9a) | binary |

**M-5 and M-7 are binary and free.** They cost nothing but discipline, and they are the two the project
has already failed once each.

---

## 6. What this proposal is not

- **Not authorization.** Nothing here is scheduled or approved. Stage-by-stage go/no-go is the owner's.
- **Not "generate more data".** The ruling forbids that framing and the evidence does not support it:
  the binding constraint is label authority, not row count.
- **Not a reopening.** P-1…P-3 and DFD-1…DFD-9 are inputs. §18 keeps the Edge AI initiative stopped.
- **Not the DFD-9a defect.** Raised separately and deliberately, because its delay cost is irreversible
  while this proposal's is not.
- **Not a taxonomy redesign.** S-1 is bounded by P-3 to the collisions the audit measured.
- **Not a schedule.** Q-1 must be measured before any timeline is credible.

## 7. Risks

| Risk | Why it is live here | Control |
|---|---|---|
| **Stage skipping under pressure** | S-4/S-5 produce visible artifacts; S-1/S-2/S-3 produce documents. The temptation is to collect first and formalise later — which is exactly how the current corpus was built | The dependency in §0 is a dependency, not a preference. Rows added before S-2 inherit unknown correctness |
| **Gold-A standing in for Gold-R** | Gold-A is cheaper, closer, and fully under owner control. It is also, by DFD-3's definition, **not** evidence about students | Separate names, separate versions, separate reporting in S-6. Never averaged |
| **Synthetic filling a measured gap that is not a real gap** | The most plausible-sounding error available, and the ruling names it explicitly | S-7 is gated behind Gold-R for any prevalence claim; distribution checks proven able to flag `collected_v4` |
| **AI drifting into Gold authority** | The volume argument for it is genuine — a solo developer facing 208 adjudications | DFD-8. AI ranks and pre-labels; the owner closes. The S-2 spec records which parts were AI-drafted |
| **Provenance deferred "until the schema settles"** | The single failure mode that cannot be repaired later | S-3 before S-4/S-5. The 136 untraceable rows are the standing evidence |
| **A maturity criterion that cannot fail** | M-1…M-8 could each be written to always pass | Baselines are stated for each; M-5 and M-7 are binary and have already been failed once each |

## 8. Immediate next step

**Not a stage — a measurement.** Once S-2 exists, adjudicate 20 rows and time it (Q-1). Everything the
owner asked to be quantified in §19 that this proposal could not answer follows from that number.

Before that: **S-1**, which needs no infrastructure, no tooling and no data — only the owner's rulings
on four bounded questions (§3, S-1).
