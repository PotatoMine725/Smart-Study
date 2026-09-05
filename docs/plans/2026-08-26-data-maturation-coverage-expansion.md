# Data Maturation & Coverage Expansion — Proposal

**Revision 3 — 2026-09-04. Status: `authorized — governing executable plan`.**
**Authorized by the owner on 2026-09-04**, accepting commits `0265386`, `fc95385`, `ee4a969` and
`7fb6ab2` as the final rev 3 revision.

> **What changed at rev 3, in one line:** rev 2 carried eight stages whose internal decisions were
> still open; the owner has now ruled **all 54** of them, and rev 3 is those rulings written into the
> stages. Full list of changes: **§9.4**.
>
> **Authorization is of the plan, not evidence that any stage has been performed.** The owner's
> 2026-09-04 authorization makes this the governing executable plan and permits execution **according
> to the ruled stage dependencies and gates**. It rules nothing performed: every stage's state is read
> from its own exit criteria, and `authorized to perform` is not `performed`. Ratified decisions may
> not be reinterpreted, relaxed or reopened under this authorization.
>
> **Status vocabulary, normalised.** The 2026-09-04 Outcomes records rev 2 as `draft` pending two
> separate things: the stage-decision **review**, and **authorization**. Rev 3 discharged the review
> half; the authorization of 2026-09-04 discharges the other. **The 119-vs-121 residual recorded in
> §9.6 remains sealed** — authorization of this plan does not authorize its re-measurement.
>
> Commissioned by the owner ruling of 2026-08-26
> ([`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) §19);
> revised against the owner review of 2026-08-27
> ([`2026-08-27-data-maturation-owner-decision-outcomes.md`](2026-08-27-data-maturation-owner-decision-outcomes.md))
> and the stage decision pass of 2026-09-04
> ([`2026-09-04-data-maturation-stage-decision-outcomes.md`](2026-09-04-data-maturation-stage-decision-outcomes.md)).
> Section-by-section traceability from those rulings to this text:
> [`2026-09-04-decision-to-revision-traceability-matrix.md`](2026-09-04-decision-to-revision-traceability-matrix.md).
> Evidence base: [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md)
> (Phase 0 audit) and [`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md).
>
> **The ratified decisions are inputs, not topics.** P-1 … P-3, DFD-1 … DFD-9b, Q-1 … Q-5 and
> S-1 … S-8 / S-T are cited here and **not reopened**. Where this proposal appears to raise one again,
> it is deriving a consequence, and the ruling wins. **Where wordings differ, the later one governs.**
>
> **Corrections of statements that were false when written are in §11, not in the body's policy text.**
> That separation is required: a factual correction and a new rule are different things and must not be
> read as each other.

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
    S-0  Reservation event               (S-1.2, S-2.5)  -- reserve 40 rows before anything reads them
      ↓
    S-1  Limited taxonomy review         (P-3)           -- decides what the classes mean
      ↓
    S-2  Canonical annotation spec       (DFD-2, DFD-8)  -- decides how a row gets a label
      ↓
    S-3  Provenance system               (DFD-5)         -- decides how a row proves where it came from
      ↓
    S-4  Gold-A            +            S-5  Gold-R      (DFD-3, DFD-4)
         authored, adjudicated               real, consented, held out
      ↓
    S-6  Evaluation foundation           (DFD-4, DFD-6)
      ↓
    S-7  Controlled expansion — Silver / public / synthetic (DFD-6, DFD-7)
      ↓
    S-8  Future model work                               -- needs a new owner decision (§18)


    S-T  Telemetry readiness             (DFD-9b)  -- runs alongside, gated separately
         DifficultyLabelLogs / StudyTimeOutcomeLogs
```

**The reservation is drawn as a step because it is one.** S-1.2 reserves the batches **before S-1 reads
any row text**, and S-1.6 hardens that from a preference into a binding precondition. It is not part of
S-2, and it is not something S-1 does on its way past: independence is established by sequence and
cannot be reconstructed afterwards.

**S-T is drawn outside the chain deliberately.** The 2026-08-27 ruling separates *telemetry* from
*real-data collection* as distinct concerns, and they behave differently: telemetry accrues passively
from ordinary use and can start now, while its gates are not the study-consent gates that govern S-5's
Track A. It feeds S-5's Track B; it is not the same work.

**Three properties of this staging are worth stating before the detail:**

1. **S-1 → S-2 → S-3 is a genuine dependency chain, not a preference.** Each stage's output is the
   other's input: you cannot write class definitions before deciding whether the classes are right,
   cannot label without definitions, cannot trust a label without knowing who applied which guideline.
2. **S-3 is cheap now and impossible later.** Provenance is captured at creation time (DFD-5) or not
   at all — the repository's own 136 untraceable rows are the proof, and no forensic pass has
   recovered them.
3. **The reservation, S-1 and S-2 are on the critical path for everything else.** The S-T telemetry
   strand and S-5's in-app accrual half can run in parallel from day one, because they collect
   *outcomes*, not labels. The DFD-9a instrumentation defect that unblocked them **shipped on
   2026-08-26** and is no longer pending work for this proposal.

**What this proposal still deliberately does not do:** propose a row-count target, recommend a dataset
for ingestion, or estimate the owner's calendar. Those were filed as **Q-1 … Q-5** and ruled; **four of
the five rulings are instructions not to invent the number yet.** The 2026-09-04 stage pass added five
more parameters of the same kind — the retention window, the S-5.7 split fraction, the Gold-A sample
size, the encoder revival threshold, and every S-6 precision threshold — each **ruled as a rule now and
a number later**. §4 records each ruling **and the parameter it deliberately leaves open**.

---

## 1. What is already settled

### 1.1 Ratified inputs

Cited so the stages below can be read without re-deriving them.

| Input | Source | Consequence for this proposal |
|---|---|---|
| No verified real data exists | DFD-1 | Real-world claims stay disabled until S-5 produces Gold-R |
| `collected_v4` is AI-generated, AI-labelled | P-1 | Synthetic/AI-authored. It cannot be Gold-R and cannot enter held-out evaluation. **Its Gold-A eligibility is a per-source ruling under S-7.1 and is currently `unruled`** — which blocks adjudication of its rows until the owner rules it |
| The 136 untraceable rows stay in the seed | P-2 | Not removed. **Under S-4.2 they remain eligible for Gold-A after owner adjudication** — Gold-A certifies label correctness, not source realness — and origin-unknown stays a visible qualifier wherever they are cited |
| Limited taxonomy review, not redesign | P-3 | S-1 is bounded: collisions and retired-class transitions only |
| Annotation spec precedes further labelled data | DFD-2 | S-2 gates S-4, S-5's labelling half, and all of S-7 |
| Owner is the sole Gold authority | DFD-8 | AI reduces review *volume*; it never closes a Gold label |
| Dual-layer provenance | DFD-5 | S-3 delivers both layers; no row is added without it after that point |
| Gold-A ≠ Gold-R | DFD-3 | Two datasets, separately named and versioned. Never merged into one "gold" |
| Both bounded collection and in-app accrual | DFD-4 | S-5 has two independent tracks with different provenance |
| Synthetic → Silver/training only | DFD-6 | **Creation of new synthetic batches is suspended for the duration of Data Maturation (S-7.2).** Existing synthetic rows are untouched. No synthetic output may ever reach S-6's held-out set |
| External datasets: evaluate, do not ingest | DFD-7 | S-7's public track produces an evaluation memo, not rows |
| Instrumentation raised now, separately | DFD-9a | Out of this proposal's scope — [`2026-08-26-prediction-instrumentation-defect.md`](2026-08-26-prediction-instrumentation-defect.md) |
| Telemetry designated future real-data source | DFD-9b | S-5's accrual track prepares it; it does not consume it |
| Edge AI stays stopped at S0 | Ruling §18 | S-8 states four necessary preconditions (S-8.1). **Dataset growth alone does not authorise re-running the encoder experiment** (DAT-04), and a separate owner decision remains mandatory even when all four are met |

**Added by the owner review of 2026-08-27** — the five questions rev 1 left open:

| Input | Source | Consequence for this proposal |
|---|---|---|
| Adjudication effort is **measured, not estimated** | Q-1 | A defined protocol, gated on S-2 `v1`. **Narrowed by S-2.6:** it estimates **contested adjudication time only**, explicitly not total Gold-A workload |
| The owner **has** access to a bounded participant network | Q-2 | S-5 Track A is feasible. **Scale and throughput remain unassumed** — and the sample is a convenience sample, which bounds what Gold-R may claim |
| Bounded collection happens **outside the production app** | Q-3 | Track A is a standalone exercise carrying its own consent and collection metadata. **Its residual — Track B's consent basis — is closed by S-5.1** |
| Gold-R sampling is **hybrid**: a floor for all five classes, otherwise the observed distribution | Q-4 | No forced 20/20/20/20/20. Coverage gaps may be targeted **by collecting**, never by generating. **The floor is a readiness criterion, not an allocation rule** (S-5.7) |
| Dataset maturity is **tiered**, not one gate | Q-5 | §5 is a tier ladder. Hard invariants are gates; quality thresholds wait for evidence |
| Instrument use of a licensed corpus is a **separate owner review** | DFD-7 (2026-08-27 wording) | ViLexNorm's `CC BY-NC-SA 4.0` question (OD-4) has a **route**. **Scheduling authorises nothing** (S-7.3): OD-4's completion is a hard gate on any ViLexNorm use, explicitly including read-only measurement |
| No confidence threshold moves as part of DFD-9a | DFD-9a (2026-08-27 wording) | Complied with — the shipped fix moved no threshold. F-1 stays deferred |

**Added by the stage decision pass of 2026-09-04** — 54 ruled items, closing the stage decision
surface. They are written into §3 stage by stage rather than restated here; the mapping is in the
[traceability matrix](2026-09-04-decision-to-revision-traceability-matrix.md).

### 1.2 Rules that apply throughout

These were each ruled repeatedly, on unrelated subjects. Stated once here and cited afterwards, because
re-deriving them per stage is how the previous revisions grew inconsistent.

> **Provenance of this subsection.** The **standing directive** in the last row is **owner text**, from
> the 2026-09-04 scope ruling. The five numbered principles are an **agent-authored generalisation** of
> ruled instances — each instance is ruled, the collection into principles is not. They are stated as
> reading aids, and where one appears to conflict with a stage ruling, **the stage ruling wins.**

| # | Principle | Ruled instances |
|---|---|---|
| **1** | **The qualifier travels with the datum.** A qualifying fact lives with the value it qualifies, not in surrounding prose | S-2.4/G4 · S-3.2 · S-4.2 · S-5.1 · S-5.2 · S-5.6 · S-7.1 · S-T.3 |
| **2** | **Selection rules are fixed before the data is seen.** When a fixed rule must later change, **version it visibly rather than tune it silently** | S-2.4/G1 · S-2.9/K2 · S-4.1 · S-5.7 · S-6.1 · S-7.4 |
| **3** | **AI assists, AI never authorises** (DFD-8 instantiated) | S-2.2 · S-4.3 · S-5.5 |
| **4** | **When a bar is not met, revise or refuse; do not lower the bar.** Refuse, and say to whom — escalate rather than accommodate | S-2.7 · S-4.1 · S-4.4 · S-5.7 · S-7.2 · S-T.4 |
| **5** | **An unresolvable state is an error, never a default.** A defaulted field is an unresolvable state wearing a valid-looking value | S-3.5 · S-3.7 · S-4.6 · S-T.2 (structurally also S-5.3) |

**The explicit `N/A` state.** Wherever absence is legitimate it gets an **explicit value**; **blank is
reserved for "not yet supplied" and is always an error state.** Ruled instances: S-T.1 (the egress gate
is `N/A`, not `closed`), S-T.2 (a defaulted provenance column must be rejectable), S-T.5 (static-only
fields are `N/A` for live tables).

**Rule now, number later.** Five decisions fix a rule and defer its number until an observation exists:
S-4.1 (sample rule / size) · S-5.7 (allocation rule / fraction) · S-6.1 (baseline formula / value) ·
S-8.1 (revival preconditions / threshold) · S-T.4 (retention rule / window). **A deferred number is
recorded `[unknown]`, never omitted and never estimated.**

**Manifest as attestation — one mechanism, two instances.** S-3's build manifest (canonical hash ·
export hash · generator identity) and S-4.6/S-5.6's `gold_a_v1` pin are the same shape for different
purposes: a record *about* an artifact, held outside it.

**Four version streams that must never collapse:** `GuidelineVersion` (S-2.11) · dataset version
(DFD-5) · the provenance-schema required-key-set version (S-3.5) · hypothesis version (S-8.1).
**`LabelVersion` is disqualified as a version stream** — one value spans four label origins across 698
rows, so it versions nothing.

| | Standing directive — **owner text**, 2026-09-04 scope ruling |
|---|---|
| | **Implementation-dependent details become follow-ups or amendments; a governance decision is never deferred on implementation grounds.** *"S-3 is not implemented yet"* is not grounds to postpone a ruling |

---

## 2. The starting position, measured

Everything in this table is `[measured]` from repository bytes by the Phase 0 audit (2026-08-25),
amended where the 2026-09-04 pass measured it more precisely — **with one stated exception: the
**13.7%** restricted-Difficulty figure is marked inline as working-notes-derived and this header does
not cover it.** It is the baseline any maturity claim will be measured against. Corrections to figures that were **wrong when written** are recorded in §11.

| Dimension | Current state |
|---|---|
| Production seed | **903 rows** — 698 train (461 derived + 101 `synthetic_v3` + **136 untraceable**) + 205 `collected_v4` |
| **Verified real rows** | **0**, in every class |
| Label stability | **29.6%** disagreement (**208 / 703**) between the legacy and interim passes, over the rows whose old label survived that taxonomy change. A further 325 rows moved because their class was retired. **This is a measurement in the legacy→interim frame; it is not the Gold-A work scope** — see S-4 |
| `Difficulty` stability | **16.2%** disagreement (167 / 1028) across the full common set, **13.7%** (96 / 703) restricted to the same surviving-label rows — *working-notes-derived (2026-09-04 pass), not an audit figure*. Both true, different scope |
| Contested pool | **133** distinct rows, after deduplicating **157** contested appearances across two boundary-disjoint pools (**J-1 as originally defined is 36**; a third annotation pass contributes a further **121**). *Production-relevant* is established for the **36** under the narrower historical scope, **not** for all 133 |
| Untraceable share | **15.1%** of the shipped corpus; **37.8%** of `KiemTraThuongXuyen`, **35.3%** of `ThiCuoiKy` |
| Class coverage in evaluation | **3 of 5.** `KiemTraThuongXuyen` and `ThiCuoiKy` have **zero** evaluation rows |
| Evaluation skew | `ThiGiuaKy` is 12.2% of training and **48.3%** of evaluation |
| Uncontaminated evaluation set | **None.** The shipped model trained on all 903 rows |
| Vocabulary gap | 25.0% of evaluation tokens unseen in training; **94.6%** of evaluation rows carry ≥1 unseen token |
| Governed corpus files | Audit **Group A's 10 (+B1)** — the files S-3.6 places under datasheet governance |
| File-level datasheets | **1 exists** (`datasheets/vn_input_fixtures.md`), and it covers a **fixture set, not a corpus**. **0 of the governed corpora have one** |
| Row-level lineage fields | **2 of 7** nominally (`Source`, `LabelVersion`) — but `Source` names a *file*, not an origin, and **`LabelVersion` is disqualified**: one value spans four label origins across 698 rows. **Usable lineage is effectively 0 of 7** |
| Annotation guidelines / adjudication records / annotator identities | **0**, for any of the three passes |
| Real telemetry available | `DifficultyLabelLogs` — real human judgements, small, **never read**. `StudyTimeOutcomeLogs` — real outcomes; `PredictedMinutes` / `Confidence` were written `null` on every row, and **the DFD-9a fix shipped 2026-08-26**, so rows written after it carry both on both branches. **Three caveats stand:** pre-fix rows are permanently unusable for calibration — the values are not reconstructible; the **end-to-end check has not been run**; and the ≥50-row retrain gate has still never been met. **Volume is `[unknown]`** — S-T.3 authorises the measurement and it **has not been performed** |

---

## 3. The stages

Each stage states its purpose, what must be true to start, what makes it done, and — where it
matters — what would make it fail silently.

**S-T is a strand, not a stage.** It sits between S-5 and S-6 for reading order only; it is outside the
dependency chain and can start at any time. See §0.

### S-0 — The reservation event (S-1.2, S-2.5)

**Purpose.** Set aside the rows that later tests depend on being independent of, **before anything
reads them.**

> **`S-0` is a draft-document sequencing label, not a ratified stage.** The reservation itself *is*
> ratified — by **S-1.2** (it must precede S-1) and **S-2.5** (one event, forty rows, pre-registered
> composition). Numbering it `S-0` is this proposal's rendering choice, made so the dependency is
> visible in §0's chain. **It creates no stage, no decision, and no obligation that S-1.2 and S-2.5 do
> not already carry.**

**One reservation event, forty rows, pre-partitioned** (S-2.5):

| Partition | Size | Composition |
|---|---|---|
| **Scored batch** | 20 | **12 contested** (carrying the TaskType axis) + **8 spanning the Difficulty range** |
| **Clean retest batch** | 20 | Held unopened for a `v2` re-test if the spec fails its threshold |

**Composition of both partitions is pre-registered before any S-1 inspection** (S-2.5), and the
reservation executes **before S-1 reads any row text** (S-1.2, hardened by S-1.6 into a binding
precondition). Q-1's batch is reserved in the same event.

**Why it is a step and not a footnote.** `[inference]` This is `I-2` — held-out reservation — applied
to spec authoring instead of training merge, and the failure mode is the one `_merge_seed.py` already
committed once: carving out the held-out set after the fact, when the thing it was meant to be
independent of has already touched it. The invariant is not a rule about CSV files; it is that
*independence is established by sequence, and cannot be reconstructed afterwards.*

**It is executable before S-1 rules anything.** *Contested* is the audit's measured disagreement set,
which S-1 inherits rather than recomputes, and the Difficulty spread selects on existing `DoKho`
values. Neither needs a boundary ruling.

**Exit criteria.** A recorded reservation event: the 40 row identities, the pre-registered composition
of both partitions, and the timestamp — materialised as a **snapshot artifact, not a query** (S-2.10/H3).

---

### S-1 — Limited taxonomy review (P-3)

**Purpose.** Decide whether the five **production** classes mean what the corpus assumes, in the
specific places the audit measured a collision (S-1.0). **Not a redesign.** The five-class taxonomy
remains the working baseline unless this review produces an explicit owner decision to change it, and
**no silent change is permitted.**

**It is smaller than it reads.** The audit already partitioned the raw 51.8% label disagreement into
*"the taxonomy retired the old label"* (325 rows — a forced move) and *"the annotators genuinely
disagreed"* (208 rows in the legacy→interim frame). S-1 inherits that partition rather than
recomputing it. **A third annotation pass is in scope as evidence, not as a new question** (S-1.3): its
121 rows inform the existing `ThiCuoiKy`/`BaiTapVeNha` boundary, and S-1 rules nothing on whether the
`ThiGiuaKy`/`DoAnCuoiKy` subdivision was correct.

**Scope, from measured evidence — five items:**

| Item | Evidence | What S-1 must produce |
|---|---|---|
| Retired-class transitions | Largest genuine transitions among rows whose old label survived: `OnTap→NhacNho` (105), `KiemTraThuongXuyen→NhacNho` (29), `ThiCuoiKy→BaiTap` (26) `[audit §E.1]` | **Ruled (S-1.1):** retirement is **confirmed** — `OnTap`/`NhacNho` do not return as production classes. **Reminder-ness is relocated, not deleted:** it remains a *derived* attribute/surface, and S-1 must say so explicitly so S-2 does not re-litigate it as a missing class |
| The one genuine collision | `ThiCuoiKy→BaiTap` (26), plus the third pass's 121 rows on the same boundary | Where is the boundary between an exam task and coursework for it? |
| `Difficulty` semantics | 16.2% disagreement across the full set, 13.7% restricted (*working-notes-derived*); transitions cluster at 5→4, 3→4, 1→3 | **Ruled (S-1.4):** Difficulty is an **explicit 1–5 semantic/ordinal scale with written anchors**, defined here so S-2 can govern future labels. S-1 does **not** audit or change current consumers — that is **FU-1** |
| Two classes with no evaluation data | `KiemTraThuongXuyen`, `ThiCuoiKy` — the **two largest training classes, 358/698 = 51.3%** | **Ruled (S-1.5):** the question is kept but **reframed onto product-intent grounds** — do these classes earn their place in the product? The **evaluation defect moves to S-6** |
| **Cause of the disagreement itself** | The contested rows, read as a set | **Ruled (S-1.6):** issue a **separate cause ruling for each production-relevant contested boundary**. Do **not** collapse into one global verdict |

**The fifth item is the one that decides whether S-2 can succeed**, and it has a fork:

- **Annotation inconsistency** — two passes applied different unwritten rules to a boundary that is
  genuinely well-defined. Then **S-2 is the fix**, and writing the boundary down closes the disagreement.
- **Taxonomy semantics** — the classes themselves do not partition the input space cleanly, so no
  guideline can make two annotators agree. Then **S-2 cannot fix it alone**, and P-3's escape hatch
  applies: an explicit owner decision to change the taxonomy.

**S-1.6 forbids answering that fork once, globally.** Different boundaries may have different causes,
and a single verdict would assign one cause to all of them. **If S-2 is written on the assumption that
every disagreement is inconsistency, the taxonomic share survives the spec and reappears in Gold-A** —
where it will look like adjudication noise.

**Entry criteria.** **S-0 has executed.** S-1 reads from the remainder, never from the reserved 40.

**Exit criteria.** A written ruling per item: *keep / redefine / retire*, with the boundary stated in
words an annotator can apply to a row; **a separate cause ruling per production-relevant contested
boundary**; the **1–5 Difficulty anchors, written**; and an explicit statement that **reminder-ness is
a derived attribute, not a missing class**. **Ambiguity resolved here is ambiguity S-2 does not have to
catalogue.**

**Silent-failure mode.** A review that "confirms the taxonomy" without writing down *why* each
boundary sits where it does produces no artifact S-2 can consume, and S-2 then re-litigates it.

---

### S-2 — Canonical annotation specification (DFD-2, DFD-8)

**Purpose.** The document that makes a label reproducible. Until it exists, DFD-2 bars further
labelled data from being collected, imported, generated or promoted.

**Required content, from the ruling:** taxonomy · class definitions and boundaries · ambiguous-example
catalogue · adjudication procedure · label provenance · guideline versioning.

**S-2 produces the specification and its version semantics only.** The row-level `GuidelineVersion`
field belongs to **S-3** (S-2.11).

#### What "adjudicate" means (S-2.8)

**Assign the correct label from the full current production taxonomy — all five classes.** It is *not*
a two-way choice between the two labels that were originally disputed. Adjudication and annotation are
therefore the same operation, plus a recorded ruling.

#### The ambiguous-example catalogue (S-2.9)

**Boundary-indexed**, 3–5 representative examples per production-relevant boundary.

- **K1** — S-2 does **not** adjudicate the remaining contested rows. Cataloguing is not adjudication.
- **K2** — the **example-selection rule is pre-registered**, so the catalogue cannot become
  self-confirming by selecting the examples that fit the definition just written.
- **K3** — **row IDs and provenance are retained** for every example.

#### Row identity (S-2.10)

Reservation and catalogue references key on **content hash + source file + line**. S-3 introduces a
stable surrogate **`RowId`** that permanently retains the originating hash.

- **H1** — the hash is a **locator for the reserved snapshot**, not a long-lived identity.
- **H2** — corrected source text **preserves old hash references** through a supersession chain.
- **H3** — the reservation event **materialises a snapshot**: an artifact, not a query.

#### Guideline versioning (S-2.11)

**Bump when** a class definition, a class-boundary rule, or a Difficulty anchor changes.
**No bump for** typos, formatting, or non-semantic example additions.
**Owner guardrail:** an example addition that **changes the effective classification rule** is a
semantic change and **requires a bump**.

`[inference]` Versioning is load-bearing, not bureaucratic. The corpus already contains rows labelled
under different implicit taxonomies with no marker distinguishing them — the failure `LabelVersion` was
meant to prevent and did not, because it versions the *file*, not the *guideline*.

#### Owner authority and AI disclosure (DFD-8, S-2.12)

AI may draft definitions, mine the corpus for boundary cases, and propose adjudications. The owner
rules. The spec carries the semantic contract **plus a concrete annotation-record template**, kept
explicitly as a **working annotation artifact, not an S-3 storage contract**.

**The canonical spec must identify which sections were AI-drafted or AI-assisted.** *Owner
qualification:* this is **provenance and transparency metadata, not a quality or trust score.** A
guideline written by the same class of system that produced the labels it governs is a circularity
worth marking.

#### The reproducibility test

**Who performs the two passes — `R-1`, closed by S-2.2.** The **owner** performs the Gold/reference
pass; **one independent human reader from the Q-2 network** performs a blind reproducibility probe.
Both annotate the same 20 rows independently. AI may be added later as a **supplementary** probe,
**never a substitute**. Under DFD-8 the owner's pass is the label; the reader's pass is a measurement
and never Gold.

**What it measures (S-2.1).** TaskType and Difficulty are evaluated **independently**:

- **C1** — independent evaluation, not one blended judgement.
- **C2** — **both must pass their own pre-registered threshold.** No averaging, no trade-off.
- **C3** — the **same** pre-reserved 20-row batch serves both: one batch, two measurements.
- **C4** — the report exposes **per-dimension and per-boundary detail**. A single headline number is
  not an acceptable output.

**Batch composition and scoring (S-2.3, S-2.4).** One stratified batch: contested rows carry the
TaskType axis, additional rows span the Difficulty range. **The whole 20-row batch scores both
dimensions.**

- **G1** — composition **and** scoring denominators are pre-registered.
- **G2** — a per-stratum breakdown is required.
- **G3** — headline rates **must not** be described as corpus-wide agreement.
- **G4** — **batch composition travels with every quoted figure.** No bare percentage anywhere.

**The level-1/level-2 gap is declared, not solved** (S-2.3): the `v1` Difficulty threshold governs
**levels 3–5 only**; levels 1–2 defer to authored examples in S-4, because the low end of the scale is
synthetic-only in the current corpus.

**The thresholds (S-2.7), pre-registered and locked:**

| Dimension | Threshold | Basis |
|---|---|---|
| **TaskType** | **≥ 17/20 (85%)**, exact match | Pre-registered |
| **Difficulty** | **≥ 18/20 (90%)**, exact match | Pre-registered |

**Within-one may be reported as a diagnostic; it is never the gate.** Failure ⇒ spec revision plus the
**pre-reserved clean retest batch** for the `v2` re-test. **Thresholds must not be changed after
observing results** — an invariant of the test, recorded next to the numbers.

> **These numbers do not violate Q-5.** Q-5 forbids inventing *dataset-maturity* thresholds before
> evidence exists; S-6.3 forbids an *evaluation-precision* threshold before Q-5 permits one. S-2.7 sets
> a **spec-reproducibility gate**, which is a third object: it measures whether a document transfers to
> a second reader, and the evidence it needs is the test itself. See §10.

#### Two 20-row exercises, and they are not the same 20 rows

The S-2 exit test and the Q-1 measurement both say *"20 held-back rows"*. They measure different things
and must not be collapsed:

| | **S-2 reproducibility test** | **Q-1 effort measurement** |
|---|---|---|
| Question | Does the spec make a label reproducible by someone other than its author? | How long does one **contested** adjudication take under the spec? |
| Shape | **Two** passes, compared | **One** pass, timed |
| Rows | The pre-reserved scored batch (12 contested + 8 Difficulty-spread) | Drawn from the **contested backlog** (S-2.6) |
| Output | Per-dimension agreement against pre-registered thresholds | Total time, per-row time, ambiguous cases, **and guideline gaps discovered** |
| Fails how | Either dimension below threshold ⇒ the spec is not finished | It does not fail; it produces a number. Many guideline gaps send work back to S-2 |

**Both batches come from the single S-0 reservation event**, which is why forty rows are reserved and
not twenty. Reusing one batch would time an adjudication on rows the adjudicator had already ruled on
once — which measures recall, not adjudication. **Forty rows out of a live pool of 157** (36 + 121, two
pools with different meanings) is affordable.

**What Q-1 does and does not estimate (S-2.6).** Q-1 draws from the contested backlog, and its result
estimates **contested adjudication time only** — **explicitly not total Gold-A workload.** The `J-3`
eligibility adjudication and the `J-7` residue must be estimated **separately**, and **no invented weighting factor may fold them
together.** *This is a narrowing of the ratified Q-1, made on the owner's own authority; it is not a
reopening.*

**Q-1 harvests more than a stopwatch.** The ruling asks for ambiguous cases and guideline gaps
alongside the timing. That makes it a **pilot of S-4 and a second test of S-2** — if the first 20 real
adjudications surface gaps the spec does not cover, the spec returns for a `v2`.

**Exit criteria.** Versioned spec, `v1`, in `docs/specs/`, with its bump semantics, its AI-drafting
disclosure, its boundary-indexed catalogue and pre-registered selection rule — and **both dimensions
passing their own pre-registered threshold**, reported with per-dimension and per-boundary detail and
with batch composition attached to every figure.

---

### S-3 — Provenance system (DFD-5)

**Purpose.** Make every future row able to prove where it came from, at creation time. **The one stage
whose cost rises monotonically with delay.**

#### The corpus moves out of the application (S-3.1, S-3.3)

The **governed corpus lives outside the application**, canonically as **JSONL**. `seed_intents.csv`
becomes a **generated export** projecting only the columns the shipped app requires, and it must be
**byte-stable for unchanged canonical content**.

**The decisive property:** provenance edits no longer change export bytes, so `ComputeSeedHash()` does
not fire and **no retrain is forced on any installation.** Governance becomes free to improve.

#### The consumer boundary (S-3.4) — and S-3 is not complete without it

Non-production consumers read the **canonical corpus directly**. The production app **alone** consumes
the lean CSV export, which is a **deployment artifact, not the source of truth**.

| Consumer | After S-3 |
|---|---|
| **The production application** | Reads the **lean export** — unchanged in shape. The ruled category is *the production app*, not any named class |
| `build_split.py` | **Repointed to canonical** |
| `TextClassifierEval` | **Repointed to canonical** |
| Downstream research tooling | **Repointed to canonical** |
| `_merge_seed.py` | **Retired, not converted** (S-3.7) — it is a write path, and only the generator writes the export |

> **The ruled consumer set** (S-3.4, as narrowed by the owner on 2026-09-04) is `build_split.py`,
> `TextClassifierEval`, and downstream research tooling — repointed — with `_merge_seed.py` **retired
> rather than repointed**. Nothing else in this table is a ruling.
>
> *Codebase evidence, non-normative:* the production read path was inventoried on 2026-08-30 as
> `TextClassifierModelManager` / `TextClassifierDatasetImporter`. That inventory is **working-notes
> evidence, not ruled text**. It is recorded here as a starting point for the verification **S-3.3
> already requires** — *S-3 is not complete until the consumer boundary is resolved and verified* — and
> **not as a settled consumer list.**

**Both existing hash pins are updated**: `SPLIT.md`'s research-reproducibility pin repoints to
canonical; `ModelMeta.SeedHash` keeps doing its existing job on the export, because canonical is not
shipped and the app cannot see it at runtime. **No sidecar joins, and no second authoritative export.**

**S-3 is not complete until the consumer boundary is resolved and verified** (S-3.3). Shipping the
canonical corpus while consumers still read the export would leave two sources of truth, which is the
condition S-3 exists to end.

#### The build manifest (S-3 closing confirmation)

The canonical→export relationship is recorded **outside the export bytes**, in a **build manifest** —
an **attestation artifact**, not a runtime sidecar and not a second authoritative export. It records
**canonical hash · export/content hash · generator identity and version**. `ComputeSeedHash()`
continues to cover only the model-relevant export content.

> **Why it must sit outside.** If the export embedded canonical's hash, any canonical change —
> including a provenance-only edit — would change the export's bytes, churn `ComputeSeedHash()` and
> force a retrain on every installation. That would destroy the retrain-decoupling S-3.1 exists to buy.

#### Row-level lineage (S-3.5)

A fixed core plus a nested **`Provenance`** object whose required-key set is **explicitly versioned and
mechanically enforced**. **Missing, null, or structurally invalid required provenance rejects
ingestion**; presence of the object alone is not sufficient. H2's supersession history is preserved
**without** adopting full event sourcing — the concrete mechanism belongs to execution planning.

| DFD-5 property | Today | After S-3 |
|---|---|---|
| origin / collection event | absent | recorded at creation |
| provenance type (`collected` / `derived` / `generated` / `imported`) | absent — `Source` names a file | recorded |
| generator identity + version | absent | recorded where applicable |
| label source (who/what assigned it) | absent | recorded |
| annotation-guideline version | absent | **`GuidelineVersion`**, the field S-2.11 assigns to S-3 |
| dataset version | `LabelVersion` — **disqualified**, one value across four label origins | a real dataset version |
| licence metadata (imported rows) | absent | recorded |
| **row identity** | **absent** | **`RowId`** (S-2.10), retaining the originating content hash permanently |
| **derived / recorded-at-creation marker** | absent | **mandatory, and mandatory on export** (S-3.2) |

**Scope.** The schema carries **7** columns today `[measured]`. Provenance adds **~8 new fields
(7 → ~15)** — `[inference]`, a design estimate read off the field table above and **not** a
measurement. `[measured]` alongside it: the governed datasheets below, and provenance-at-write-time on
2 telemetry tables. The seed's consumer surface is narrow — the shipped intent classifier reads
**one** column — so widening the schema is low-risk for the model path and mostly touches tooling.

#### Existing rows (S-3.2)

Per-row provenance is **backfilled for the 903 wherever mechanically derivable by join**. The rows with
no antecedent — the 136 — are marked **`untraceable`**. A second field distinguishes
**derived-by-join** from **recorded-at-creation**, and *the derived/recorded marker is mandatory on
export and may not be dropped when provenance values leave the corpus.*

**This is derivation, not invention, and the distinction is the whole point.** DFD-5 governs new rows;
retrofitting *invented* lineage onto the existing 903 would be the failure being corrected. Recovering
a provenance value by joining records that already exist is a different act — and the marker is what
keeps a reader from mistaking one for the other.

#### File-level datasheets (S-3.6)

Datasheets are authored for the **canonical corpus and genuine source inputs only** — audit **Group A's
10 (+B1)**. Derived exports and splits carry **machine-generated provenance** instead; a hand-written
datasheet for a generated artifact would be a second place for the truth to drift.

**`A8` (`SeedDataGenerator.Generate()`) is explicitly out of scope for S-3** — it belongs to the
separate M7 study-time-predictor domain. **A deliberate, reasoned exclusion, recorded as FU-2**, not
silently absorbed.

**The datasheet schema carries three additions from the 2026-09-04 pass:**

1. **Gold-A eligibility** (S-7.1) — `eligible` / `not eligible` / `unruled`. **`unruled` blocks
   adjudication.**
2. **A static-source / live-source section split** (S-T.5), with static-only fields schema-marked
   **`N/A`** for live tables, never left blank.
3. **A live-source field set** (S-T.5): accrual, retention, provenance validation, permitted use, and
   **gate states**.

**One artifact type.** *"Dataset contract"* is **not** a separate artifact and must not enter the
vocabulary as one (S-T.5).

#### Enforcement (S-3.7)

The authoritative fail-closed gate sits at the **canonical file / repository boundary**: a complete
validator **rejects invalid canonical content in CI**. Direct owner edits remain allowed under DFD-8; a
local validator may be added later as a convenience, but **CI remains the authoritative enforcement
point**.

> **The guarantee, stated at the reach the mechanism has.** CI gates the **merge** into `main`/`dev`,
> not the commit. So the guarantee is: **invalid canonical content never enters `main`/`dev` history
> through merge.** It is *not* a claim that CI prevents invalid content from existing in feature-branch
> history. The enforcement point is already wired — branch protection plus CI have been in force since
> **2026-08-09** `[fact — working-notes-derived, not an audit figure]` — so the validator is a step to
> add, not infrastructure to build.

**Exit criteria.** No row enters any corpus without complete lineage, enforced by CI rather than by
discipline; the consumer boundary resolved **and verified**; the build manifest emitted; the governed
datasheets written. A check that can fail: **attempt to merge canonical content with a missing required
provenance key and confirm CI rejects it.**

---

### S-4 — Gold-A: human-verified authored data (DFD-3)

**Purpose.** Label correctness, annotation-regression detection, adjudication, and validation of the
S-2 guideline. **Explicitly not real-user evidence** — that separation is the point of DFD-3.

#### Scope (S-4.1) — ratified at scope level, not as a row count

Gold-A is **two components with two purposes, both required**:

1. **The full contested pool** — **133** distinct rows, after deduplicating **157** contested
   appearances across two boundary-disjoint pools (**J-1 as originally defined is 36**; the third
   annotation pass contributes **121**). *Production-relevant* is established for the **36** under the
   narrower historical scope; it is **not** established for all 133. **157 counts appearances; 133
   counts rows — the two are different dimensions and must never be added or equated.**
2. **A deliberately designed representative authored sample**, covering the defined TaskType/Difficulty
   space and **especially the currently absent low-Difficulty range**.

**The sample size and sampling rule must be explicitly designed and approved before selection.** **Do
not invent the number from the current data** — the sample size is `[unknown]` by ruling, not by
omission. **The authored sample is not a substitute for the contested backlog.**

| Item | Volume | Why a human, and why it cannot be delegated |
|---|---|---|
| **J-1** — adjudicate the contested rows | **133** distinct rows (**157** contested appearances, deduplicated) | Passes disagreed; no third opinion exists for the boundary. An automated tiebreak encodes whichever pass the current model was trained on |
| **W-1** — the authored sample | `[unknown]` — rule first, number after approval | It covers space the corpus does not contain, especially low Difficulty |
| **J-3** — **adjudicate the untraceable rows for Gold-A eligibility** | **≤136** | **S-4.2 rules them eligible for Gold-A after owner adjudication**, carrying the persistent origin-unknown qualifier |
| **J-7** — rows adjudication cannot decide | `[unknown]` count; **the policy is no longer unknown** (S-4.4) | The natural residue of J-1 |

> **Identifier namespaces in this table.** `J-*` are the **audit's §J** human-labeling items and keep
> the audit's numbering. In particular **the audit's `J-2` is *"define the class boundaries that
> actually collide"* — a guideline item that gates `J-1` — and it is not in this table.** Where a
> ratified stage decision changes what an audit item now requires, this table states the current
> operation and names the ruling (`J-3` under S-4.2, which replaced *dispose* with *adjudicate for
> eligibility*). **`W-*` is a namespace introduced by this proposal** for work the audit's §J does not
> enumerate; **`W-1` is the S-4.1 authored sample**, and it is not an audit item.

**How long it takes is a scheduled measurement, not a guess** — Q-1, **scoped by S-2.6 to contested
adjudication only.** J-3 and J-7 are estimated separately.

#### The 136 origin-unknown rows (S-4.2)

They **remain eligible for Gold-A after owner adjudication**, because **Gold-A certifies label
correctness, not source realness.** Origin-unknown status remains a **persistent, visible qualifier**
wherever Gold-A is summarised or cited.

**Do not duplicate provenance in a second schema field.** The provenance object is the single source of
truth and the classification **derives** from it.

#### Where AI legitimately helps (S-4.3) — commit-then-reveal

AI predictions are **computed but hidden until the owner commits the adjudication label**, then
revealed as an optional adversarial or re-review signal. **AI is not an independent annotator here**,
and its output must never be treated as validation or authority. A label changed after the reveal is
recorded as an **explicit second review, never a silent overwrite**.

**What this saves is ordering and clustering, not judgement.** Under commit-then-reveal, AI can rank by
expected disagreement and cluster by boundary so one ruling disposes of many — it cannot reduce the
number of labels the owner must actually decide.

#### Rows the guideline cannot decide (S-4.4)

A row the canonical guideline cannot decide is **excluded from `gold_a_v1` and logged explicitly as a
guideline gap.** Do **not** assign a confidence-qualified Gold label, and do **not** spend an extra
human pass to force a `v1` ruling. Preserve the row's **identity, reason, boundary, and guideline
version**.

*Owner addendum:* a later guideline bump triggers re-review **only for rows whose applicable semantic
rule actually changed**.

#### Rule attribution (S-4.5) — targeted, not universal

The adjudication record carries a rule citation **only where existing structure does not already answer
"which rule decided this row"**:

| Row kind | Attribution |
|---|---|
| **Difficulty rulings** | Name the S-1.4 anchor applied |
| **Authored-sample rows** | Name the rule that placed them |
| **Contested-pool TaskType rulings** | **Inherit their recorded boundary and add nothing** |

#### The artifact (S-4.6, extended by S-5.6)

`gold_a_v1` is a **pinned manifest** — **a dependency declaration, not merely a row list**:

- selected **row identities**;
- the **canonical corpus hash**;
- the **guideline version**;
- **every contextual dependency the figure rests on** — a corpus-wide denominator, a comparison set —
  **explicitly pinned** (S-5.6), so row-level verification does not silently leave contextual
  dependencies unpinned.

Rows are resolved from the canonical corpus at computation time; **row content is not duplicated into a
second authoritative artifact.** The manifest **fails closed** when a pinned row is missing or its
content no longer matches — but **unrelated corpus churn must not invalidate it**, because under S-5.3
role moves are routine and hash-visible by design, and a guard that fires constantly gets bypassed.

**Exit criteria.** `gold_a_v1` as a pinned manifest, every included row carrying its adjudication
record, the ruling owner, the guideline version and full S-3 lineage — **plus the guideline-gap log**
as a named exit artifact. Separately named and versioned from anything called Gold-R.

---

### S-5 — Gold-R: real-user data, on two independent tracks (DFD-4)

**Purpose.** The only stage that can produce evidence about actual student behaviour. **Everything the
project currently claims about real-world performance is waiting on this.**

#### Who assigns the label (S-5.5)

**Gold-R labels are owner-assigned under the S-2 specification** — the same spec-defined construct as
Gold-A. User `FinalDoKho` / `WasOverride` signals are **preserved as governed metadata and never
treated as the Gold label**.

That preserves the real-user signal for a **separate future analysis of perceived difficulty** (**FU-3**),
which is a genuinely different construct from the spec's Difficulty and must not be silently merged
into it.

> **A cost this creates, named rather than absorbed.** Gold-R labelling effort **scales linearly with
> collection volume**, and the cost model does not contain it. `[inference]` This is plausibly the
> binding constraint on Track A and Track B volume — more plausibly than recruitment.

#### Track A — bounded real-user collection

**Ratified 2026-08-27 (Q-2, Q-3):** the owner has access to a bounded network, and the exercise runs
**outside the production application** as a standalone collection event carrying its own consent and
collection metadata. Track A is *feasible*. **Scale and throughput are still not assumed.**

Its measured target is unusually clear:

- `KiemTraThuongXuyen` and `ThiCuoiKy` need real rows because they currently have **zero**;
- `ThiGiuaKy` is over-weighted in evaluation (48.3%) relative to training (12.2%), so a real
  distribution is needed to know whether that skew is an artifact of the quota or a fact about students;
- the abbreviation vocabulary (`tgk` at 28/205 evaluation vs 0/698 training) is the clearest hypothesis
  the project holds about real input, and **it has never been tested against a real speaker**.

**Sampling shape, ratified 2026-08-27 (Q-4) — hybrid:** a **minimum floor for all five classes**, and
beyond the floor the **naturally observed distribution**. **The floor number is not set here.**

> **The floor and the observed distribution do different jobs, and both are needed.** The floor
> guarantees no class is invisible in evaluation. The observed distribution is what makes any
> prevalence claim honest. **Evaluation coverage and distributional fidelity must be reported
> separately**, or the floor silently becomes a quota and the project has re-created `_balanced.csv`'s
> 1.11× balancing against an unobserved target.
>
> **The floor governs collection readiness. It is not the holdout allocation rule** — S-5.7 rules that
> explicitly, and the two senses must not leak into each other.

**A bound this track carries into every claim it supports.** `[inference]` A network of friends,
students and acquaintances is a **convenience sample**. That is a sound basis for Gold-R — it is real
data from real people, which the project has none of — but it does not license *"this is how students
write."* The honest scope is **"observed among N recruited participants, recruited through the owner's
network."** Record the recruitment route in the datasheet and state participant count and route
wherever a distribution figure appears.

#### Track B — ongoing in-app organic accrual

Real usage captured through the application, giving the natural production distribution — and the
**only** source that yields it rather than a recruited one.

**Consent at transfer (S-5.1), which closes the Q-3 residual.** Track B rows may continue to accrue
**locally**, but **no row may leave the user's machine or enter `gold_r_v1` without explicit
per-participant consent for that transfer**, with the consent record and its version preserved
alongside the contributed data.

This keeps the governance standard consistent with Track A and prevents reproducing the project's
historical provenance problem with real user data.

> **This track is no longer the cheap one.** Rev 2 said it *"can start earliest and costs the least."*
> That is **now false.** Track B carries a consent step and a **transfer mechanism that does not
> exist** — new scope this proposal does not contain and has not priced. Accrual is cheap; contribution
> is not.

**Pre-mechanism rows (S-5.2).** Rows accrued before the mechanism ships **may** be contributed with
**explicitly derived or backfilled provenance**, but they:

- remain **below the top maturity tier**;
- are **ineligible for the S-6 held-out partition**;
- must be **distinguished explicitly in all Gold-R reporting** by provenance grade.

Rows accrued after the mechanism ships may qualify for the higher tier.

#### One corpus, partitioned by role (S-5.3)

**One governed canonical corpus and one authority**, with rows **physically partitioned by role** so
the training/export path has **no access to held-out Gold-R rows**.

The partition is an **enforcement boundary, not a second authority**. Provenance, track and tier remain
**governed metadata**; **any role move must be explicit and hash-visible**. **Role is the one physical
attribute** — a bounded, deliberate exception to derived-not-stored, made because an access boundary
that is merely derived is not an access boundary.

#### Where Gold-A sits (S-5.4)

**Gold-A rows remain in the training partition and export.** Gold-A is a **reference and adjudication
corpus, not a held-out performance set**. S-6 performance figures must use a **separately reserved
holdout established before training**.

#### The held-out allocation rule (S-5.7)

A **pre-registered assignment rule**, fixed **before observing row content**, assigns Gold-R rows to
held-out vs training. The **split fraction is instantiated after the initial volume is known** —
`[unknown]` until then.

1. **Neither arrival order nor post-hoc class balancing** may determine holdout membership.
2. **Q-4's per-class floor is a readiness/coverage criterion, not the allocation rule.**
3. If volume is insufficient, mark Gold-R evaluation **not yet ready** rather than forcing an
   undersized split.

**Exit criteria.** `gold_r_v1`, owner-verified, with consent records and their versions, S-3 lineage,
provenance grades distinguished, **a held-out partition assigned by the pre-registered rule and
reserved before any training merge**, its manifest pinning every contextual dependency, and **a
recorded claim scope**: participant count and recruitment route, carried in the datasheet and repeated
wherever a distribution figure from this dataset is cited.

---

### S-T — Telemetry readiness (DFD-9b)

**Purpose.** Make the two telemetry tables *eligible* to become real data. **A separate strand, not a
stage.**

**It is designation, not consumption.** DFD-9b designates both tables as *potential* future sources.
S-T works the gates; **it does not read the tables as training or evaluation data**, and reaching the
end of S-T does not authorise doing so.

| Source | What it holds | Standing today |
|---|---|---|
| `DifficultyLabelLogs` | **Real human judgements** — the nearest thing to real labelled data the project owns | Already captured. **Never read by anything.** Volume `[unknown]` |
| `StudyTimeOutcomeLogs` | Real outcomes, with the model's prediction and confidence alongside | `PredictedMinutes` / `Confidence` were `null` on every row until the [DFD-9a fix](2026-08-26-prediction-instrumentation-defect.md) shipped **2026-08-26**. Pre-fix rows stay unusable — the values are not reconstructible. **The end-to-end check is still open.** Volume `[unknown]` |

**Why this strand is cheap and time-sensitive at once.** Every day the application runs, it either
accrues rows that will be usable or rows that will not, and the difference was decided at write time —
the same S-3 principle that left 136 rows untraceable.

#### Two independent gates (S-T.1)

S-T's consent question **splits in two**, and they close on different schedules:

| Gate | Governs | State |
|---|---|---|
| **Local** — collection / retention / handling | Accrual on the user's own device, retention duration, handling, disposal | **BLOCKED today.** S-T.1 rules it **closable once its policy and controls are satisfied** — nothing outside this strand gates it. But S-T.4 makes an **instantiated retention window** one of those controls, and the window is `[unknown]` because the **S-T.3 measurement has not been performed**. *Authorised to measure is not measured; closable in principle is not closed.* |
| **Egress / transfer** — governed by **S-5.1** | Any row leaving the user's machine | **`N/A` while no transfer mechanism exists**, and **BLOCKING the moment one is introduced** |

`N/A` is **not** `closed` and **not** `satisfied`: it must not be mistakable for a met condition.

**S-T must never be reported as "closed" on the strength of the local gate alone.** Both states remain
**explicitly visible** in any status report on this strand — **a single roll-up field is
non-conforming.** Building a transfer mechanism is a **gate-state transition, not a feature ship.**

#### The gates

**They are not a strict sequence.** Gate 4 supplies the validation for gate 1, so the two are
**mutually dependent**.

| # | Gate | State |
|---|---|---|
| **1** | **Provenance at write time** — S-3 row-level lineage applied to both tables | Not started |
| **2** | **Local** collection / retention / handling | **BLOCKED** — gate 3's window is `[unknown]`, so S-T.4's precondition for closing is unmet |
| **2** | **Egress / transfer** (S-5.1) | **`N/A`** — no mechanism exists |
| **3** | **Retention rule and window** | Rule fixed; **window `[unknown]`** |
| **4** | **The S-3.6 datasheet, live-source section** | Not started |
| **5** | **Sufficient volume** | **`[unknown]`** — measurement authorised, not performed |

> **Gate 2 is one consent question rendered as two gates** (S-T.1). **`Local` and `Egress` are
> descriptive labels, not separate ratified gate identifiers** — the ruling splits the question, it does
> not number the halves. Both states stay visible; a single roll-up field is non-conforming.

**Gate 1 — capture-complete provenance (S-T.2).** Record **every S-3 lineage field that is meaningful
for a runtime-captured row**, rather than predicting which fields may be needed later. When it is
unclear whether a field will be needed, **record it**. **Gate 4 validation is mandatory** on those
fields: an unpopulated or defaulted column **must not be able to masquerade as valid provenance**, and
validation must reject that state.

**Gate 4 — one artifact type (S-T.5).** The **S-3.6 datasheet**, with explicit **static-source** and
**live-source** sections. Static-only fields are **schema-marked `N/A` for live tables, never left
blank.** The live-source section carries **accrual, retention, provenance validation, permitted use,
and gate states** — which is where S-T.1's two-state visibility requirement is rendered.

#### Measurement authorisation (S-T.3)

**Authorised now:** row counts on both tables; capture-date ranges; null and usability rates on the
**already-defined evaluation fields** (`PredictedMinutes`, `Confidence`).

**Prohibited until the S-5.7 allocation rule is pre-registered:** label marginals (`FinalDoKho`,
`SuggestedDoKho`) and feature marginals (`TaskType`, `Difficulty`, `Credits`, `DaysLeft`,
`StudiedMinutesSoFar`, …). **The prohibition is tied to S-5.7's pre-registration**, not to a session or
a judgement of safety.

**The measurement scope is recorded explicitly alongside any figure produced**, so operational
usability checks **cannot be mistaken for Gold-R distribution analysis**. A number from this
measurement is a volume/usability fact and **must never be cited as evidence about the data's
distribution.**

**Status: authorised, not performed.** No measurement has been run.

#### Retention (S-T.4)

**Bounded rolling retention is the rule**, fixed now; indefinite device-bounded retention is rejected.
**The window is instantiated only after S-T.3's volume measurement**, and before the number is fixed,
**verify that the retained volume can still make gate 5 attainable.**

**Escalation clause:** if no reasonable window leaves gate 5 attainable, **return to owner decision** —
do not select a window that makes the gate unreachable, and **do not quietly relax the gate to fit the
window.**

**S-T.3's measurement therefore has two consumers**: it sets the retention window and it satisfies gate
5's `[measured]` count, and both uses stay inside its authorised scope. **The local gate cannot close
until the window is instantiated.**

**Exit criteria.** Both tables write complete lineage, validated so a defaulted column cannot pass; a
written collection, retention and handling policy exists with **its window instantiated**; an S-3.6
datasheet with a live-source section exists for each table; **a `[measured]` row count**; and **both
gate states rendered explicitly** — the local gate closed, the egress gate `N/A` or BLOCKING.
**Consumption remains a separate owner decision.**

**Silent-failure modes.** Two, now:

1. Declaring the strand done because the columns populate. Instrumentation is one gate of several, and
   it is the only one that is engineering work — which makes it the one most likely to be mistaken for
   the whole.
2. **Reporting S-T closed on the strength of the local gate alone**, when the egress gate is `N/A`
   rather than met.

---

### S-6 — Evaluation foundation (DFD-4, DFD-6)

**Purpose.** The first honest answer to *"does this model work for actual students?"*

**Structural rules, all inherited:**

- Held-out data is reserved **before** training merge, never carved out afterwards.
- **No synthetic row and no `collected_v4` row may enter it** (DFD-6, DFD-1).
- **No derived-provenance Gold-R row may enter it** (S-5.2) — the third entry on this list.
- **Gold-A does not produce performance figures.** It stays in the training partition (S-5.4), so a
  figure computed on it would be measured on rows the model trained on. Gold-A answers *was this label
  applied correctly*; **only Gold-R answers whether the model works on students.**
- **Gold-A and Gold-R figures are never averaged.** Post-S-5.4 the reason is simpler than it was: only
  one of the two sets produces performance figures at all.
- Every historical figure is re-baselined against it, or stays scoped as authored-only. The correction
  pass already downgraded 96.2%, 97.24%/97.25% and the S0 comparison.

#### The metric registry (S-6.1) — pre-registered before Gold-R exists

**Register the full metric set *and* the single headline before Gold-R exists.** Report **all**
registered metrics: the non-headline ones are **mandatory diagnostics, not post-hoc alternatives** that
may be promoted if the headline disappoints.

| Dimension | Metric | Role |
|---|---|---|
| **TaskType** | Exact match | **Headline** |
| **Difficulty** | **Mean absolute error (MAE)** | **Headline** (S-6.2) |
| **Difficulty** | Exact match | Mandatory diagnostic |
| **Difficulty** | Within-one | **Diagnostic only** |
| **Both** | **Chance baseline per applicable metric**, computed from the **observed Gold-R marginal** | Required alongside every figure |

**Why MAE is Difficulty's headline (S-6.2).** It is the registered metric most faithful to the ordinal
construct, and it retains full error-distance information — a prediction off by one and a prediction
off by four are not the same failure. **Within-one is diagnostic only**, because its chance baseline
can be near-ceiling under a clustered marginal. **The numeric chance baseline must be recomputed from
the actual Gold-R marginal** when available; it is not a constant.

#### Intervals and the chance guard (S-6.3)

**An interval is required on every figure.** A result whose interval **includes the pre-registered
chance baseline** is labelled **"not distinguishable from chance."**

**Exclusion of chance is not validation and not a precision guarantee.** It is a **one-sided negative-
evidence guard**: it can tell you a result is indistinguishable from chance; it cannot tell you a
result is good. **Interval width remains an explicit uncertainty qualifier**, reported, not summarised
away.

**No numerical precision threshold before Q-5 permits one.**

#### The evaluation defect S-1 handed over (S-1.5)

The zero-coverage finding belongs here, not in the taxonomy review, and it is worse than a coverage
gap: the split is **file-level**; the entire test set is AI-generated `collected_v4`; it covers **3 of
5** classes; and there is **zero train/test text overlap** by construction. **There is no evaluation
set to fix — there is one to build.**

**Exit criteria.** A held-out real set that no model has seen, with its reservation recorded and its
allocation rule pre-registered; the metric registry fixed **before** the set exists; and the first
real-input figure the project has ever produced, **with its interval and its chance baseline** —
**whatever it says.** A disappointing number here is a successful outcome of this stage.

---

### S-7 — Controlled expansion: Silver, public, synthetic

**S-7 is a governance-only stage during Data Maturation** (S-7.4): **no ingestion, no generation.** Its
work is the per-source eligibility rulings below and the distribution check. All three tracks remain
**training-side only**; none may touch S-6.

#### Silver, and promotability (S-7.1)

**There is no blanket Silver promotability rule** — in either direction. Gold-A eligibility is a
**per-source ruling recorded in that source's S-3.6 datasheet**, with **owner adjudication still
required in every case**. An eligibility ruling grants only the right to be adjudicated, **never a
label**.

**Promotability is a property of a *source*, not of the Silver tier.**

**Scope limit, in the owner's own words:** S-4.2 removed provenance as a **disqualifier**; it created
**no presumption of eligibility.**

| Source | Gold-A eligibility |
|---|---|
| The **136** untraceable rows | **Eligible** after owner adjudication (S-4.2) |
| **`collected_v4`** | **`unruled` — and a ruling is now required.** `unruled` blocks adjudication of its rows |
| Any future Silver source | Requires its own explicit ruling before any of its rows are adjudicated |

#### Synthetic (S-7.2) — suspended

**Creation of new synthetic batches is suspended for the duration of Data Maturation.** Not restricted
— **suspended**: there is no permitted purpose during this stage. Existing synthetic rows
(`synthetic_v3`, the 136, `collected_v4`) **remain in Silver and are untouched**; the ruling is
prospective only.

**Resumption requires both, conjunctively:**

1. an **observed-data gap**, measured against **observed data** rather than against the authored corpus;
2. an **explicit warrant** for the batch.

**The prohibition on synthetic generation for distributional or coverage claims is standing** and
survives the stage that suspended it.

> **Why entry controls were not enough.** Generator provenance, spec conformance and distribution
> checks answer *does this batch pass?* — never *should this batch exist?* And the trap below removes
> the only warrant anyone would invoke.
>
> *"Underrepresented in the current authored corpus" is not the same as "underrepresented in real
> student behaviour."* The corpus's measured gaps are gaps **in an authoring process**. Generating
> against them without Gold-R evidence would optimise the model toward a distribution nobody has
> observed — and the audit records **five linguistic phenomena as `[unknown]`**, so the measurement
> that bound would depend on does not currently exist. **A safeguard phrased as a measurement
> precondition is only as real as the measurement.**
>
> **Q-4 permits targeting coverage gaps by *collecting*, and this stage does not inherit that
> permission.** Collecting against a gap goes and *looks* at what real people write, so a wrong guess
> is corrected by the evidence it gathers; generating against a gap *asserts* what they write, so a
> wrong guess is amplified into training data and becomes indistinguishable from a finding.
>
> `[inference]` This project's track record with synthetic augmentation is poor: three passes
> (`synthetic_v3`, the untraceable 136, `collected_v4`), and the third was described as real in eight
> documents for two months. That is an argument for the controls, not against the technique.

#### Public (DFD-7) — all three candidates closed for this stage

**Evaluate, do not ingest.** The structural conclusion needs none of their names: **no public
Vietnamese corpus carries this label space**, so every imported row would need full relabelling under
S-2 — which is why the public track sits *behind* S-2 and is not a shortcut around it.

| Candidate | Standing after S-7.3 |
|---|---|
| **ViLexNorm** — >10,000 human-annotated normalization pairs, EACL 2024 | **Gated on OD-4.** Its completion is a **hard gate on any ViLexNorm use — no tier, no purpose, no exception.** OD-4 asks whether non-redistributive measurement use creates a derivative under **`SA`**, and whether **`NC`** binds a non-distributed internal artifact. **Scheduling authorises nothing** — explicitly not read-only measurement, not ingestion |
| **UIT-VSFC** — 16,175 rows, Vietnamese student feedback | Right population, wrong task. **No licence field on the dataset card**; "free for research" in third-party sources is not a licence |
| **PhoATIS** — Vietnamese intent + slot filling | Right task shape, maximal domain distance. **No licence surfaced.** Value is methodological |

*Rule carried forward verbatim:* `public/downloadable ≠ relevant ≠ licensed ≠ approved for project use.`

> **ViLexNorm does not satisfy S-7.2's resumption condition** (ratified correction, S-7.3). As an
> instrument it measures teencode in *our* corpus, which is authored and AI-generated, so it
> characterises the **authoring process** — the confusion the trap above warns against. As external
> evidence its population is **social-media writers**, not students entering study tasks. S-7.2
> requires an observed gap in the **relevant** population; **neither route supplies one.**
>
> **DFD-7's inspection permission does not reach this.** DFD-7 allows external datasets to be
> *inspected for relevance, provenance and licensing* — that is inspection of the **licensing surface**,
> which is what OD-4 *is*. S-7.3 gates use of the **corpus contents**. Different objects.

#### The distribution check (S-7.4)

**Built and frozen now, while no batch is pending.** It must be demonstrated to **fail** on
`collected_v4`'s known regularities — a check never shown to fail is not a check.

**Specification constraint:** the check is specified over **general phenomenon and feature families**,
**not hard-coded to the seven observed patterns.** The seven are a **proof case, never the
definition** — a check that recognises exactly the seven regularities already found would pass the next
batch by construction.

**Change control:** future changes are **versioned**, never tuned against a pending batch.

**Exit criteria for the stage as scoped during Data Maturation.** A Gold-A eligibility ruling recorded
in the datasheet of every Silver source, `collected_v4`'s included; a **frozen, versioned distribution
check with its `collected_v4` proof case demonstrated**; and OD-4 scheduled. **No rows are added by
this stage.**

---

### S-8 — Future model work

Requires new evidence **and** a new owner decision (ruling §18). **DAT-04 stands: dataset growth alone
does not authorise re-running the encoder experiment.**

#### The revival bar, pre-registered now (S-8.1)

**Four necessary — but explicitly not sufficient — preconditions:**

1. A **real Gold-R holdout** exists (S-5.7 allocation, S-5.3 partition).
2. An **S-6 baseline measured with the required intervals** (S-6.1 metric set, S-6.2 MAE headline,
   S-6.3 intervals and chance-baseline labelling).
3. A **registered pre-result hypothesis** with four mandatory elements — the **observed deficit**, the
   **proposed mechanism**, the **evaluation metric/contrast**, and the **predicted direction** — all
   stated **before results are seen**.
4. **DAT-04 unchanged.**

**Change control.** Amending the hypothesis requires a **new version**, and a later version **does not
retroactively qualify the prior experiment.**

**Explicitly not set.** **No numerical revival threshold.** Whether S-6.3's prohibition on precision
thresholds reaches revival thresholds too is left open rather than settled by implication.

**A separate owner decision remains mandatory even when all four preconditions are met.**

---

## 4. Quantification, per §19 — measured, ruled, and still open

§19 requires nine quantifications before implementation is proposed. **Four were measurable from the
repository** (§4.1). **Five were owner-only** and were ruled on 2026-08-27 (§4.2).

**Four of the five rulings are instructions not to invent the number yet** — and that is the substance
of them, not a deferral. §4.2 records, for each, **both the ruling and the parameter it deliberately
leaves open**, because an open parameter that nobody wrote down is how a placeholder becomes a fact.

### 4.1 Measured from repository evidence

| §19 item | Answer | Basis |
|---|---|---|
| **Gold-A adjudication scope** | **133** distinct contested rows — J-1, deduplicated from **157** contested appearances across two boundary-disjoint pools of **36** and **121** — plus **≤136** origin-unknown rows to adjudicate for eligibility (J-3, S-4.2), a designed **authored sample** whose size is `[unknown]` by ruling (S-4.1, **W-1**), and an `[unknown]` residue (J-7). **Bounded and enumerated for the contested half; the sample half is ruled at scope level, not as a count** | Audit §E.1, §E.5, §J + the 2026-09-04 pass. **The `[measured]` stamp covers the counts only** — **133**, **157**, **36**, **121**, **≤136**. The authored sample (S-4.1) and the `J-7` residue (S-4.4) are `[unknown]` **by ruling, not by failed measurement** |
| **Provenance implementation cost** | The labelled-corpus schema carries **7** columns today; provenance adds **~8** more, taking it to **~15** — `[inference]`, a design estimate read off the §S-3 field table, **not** a count of anything that exists. Alongside it: **datasheets for audit Group A's 10 (+B1)**, of which the governed corpora have **0** (one fixture datasheet exists); provenance-at-write on **2 telemetry tables**. The canonical/export split, the build manifest, and the CI validator are part of the same cost — **scope items rather than counts**. Model-path risk is low — the shipped classifier reads **one** column | Audit §B.1, §E.2 + CSV headers + the 2026-09-04 pass. **`[measured]`: the 7 current columns, Group A's 10 (+B1), the 0 governed datasheets, the 2 telemetry tables, the one model-read column.** The **~8** and **~15** figures are `[inference]` |
| **Public-dataset evaluation candidates** | **3 checked, 0 approved, and all three closed for this stage.** ViLexNorm gated on OD-4; UIT-VSFC no licence field; PhoATIS no licence surfaced. No public corpus carries this label space | Audit §I.2 — three dataset cards read at source `[fact]` |
| **Controlled synthetic-generation opportunities** | **None during Data Maturation.** S-7.2 **suspends** creation of new synthetic batches; there is no permitted purpose during this stage. The nameable gaps — 2 classes at zero evaluation coverage, `ThiGiuaKy`'s skew, the absent abbreviation vocabulary — are gaps **in the authored corpus**, and whether any is a gap in real behaviour is `[unknown]` until S-5. Resumption requires an observed-data gap **and** an explicit warrant | Audit §C.1, §D.3, §G `[measured]` + S-7.2 |

### 4.2 Ruled by the owner, 2026-08-27 — and what each ruling leaves open

| # | §19 item | Ruling | **Residual open parameter** |
|---|---|---|---|
| **Q-1** | **Estimated owner effort** | **Agreed measurement.** After S-2 `v1`: adjudicate 20 held-back rows; measure total and per-row time; record ambiguous cases and guideline gaps. **Narrowed by S-2.6** to contested adjudication only | **Per-row contested adjudication time.** No schedule may be quoted before it. **J-3 and J-7 are estimated separately — no invented weighting factor may fold them together** |
| **Q-2** | **Data-collection throughput** | **B.** The owner can access a small network for a bounded collection exercise | **Scale and throughput — explicitly not assumed.** Also load-bearing for claim scope: **the recruitment route**, which makes this a convenience sample |
| **Q-3** | **Privacy / consent implications** | **C.** Bounded collection runs outside the production app, handling consent and collection metadata itself | **Closed.** S-5.1 rules Track B's consent basis: explicit per-participant consent at transfer, with the record preserved |
| **Q-4** | **Gold-R collection strategy** | **C — hybrid.** Minimum floor for all five classes; prefer the naturally observed distribution; target linguistic gaps where justified. **Do not invent quotas before observing the real distribution** | **The floor number**, deliberately unset. **And a clarification, not a residual:** the floor is a **readiness/coverage criterion, not the holdout allocation rule** (S-5.7) |
| **Q-5** | **Success criteria for dataset maturity** | **B — tiered.** Progressive tiers **without arbitrary thresholds before evidence exists.** Hard invariants stay distinct from quality thresholds | **Every quality threshold in §5's tier ladder**, plus **every S-6 precision threshold** (S-6.3 defers to Q-5) |
| **R-1** | *(surfaced by the rulings)* | **Closed by S-2.2.** Owner = Gold/reference pass; one independent human reader from the Q-2 network = blind reproducibility probe. AI supplementary, never a substitute | **None.** The threshold was pre-registered for this shape (S-2.7) |

`[inference]` **Q-1 remains the keystone, and it is a scheduled measurement rather than a hope.** But
S-2.6 bounds what it settles: it produces contested adjudication time, and the **`J-3` eligibility
adjudication** and **`J-7` residue** costs are separate estimates. **Track B's consent basis, which did not collapse into arithmetic, is now
closed by ruling rather than by measurement** — which is what a policy question requires.

---

## 5. Dataset maturity — a tiered model (Q-5)

Maturity is **progressive tiers, not one all-or-nothing gate**, defined **without inventing thresholds
before evidence exists**, and with **hard invariants kept distinct from quality thresholds**. Rev 1's
eight criteria all survive: three became invariants `I-1`/`I-2`/`I-3` (was `M-2`/`M-5`/`M-7`), the
remaining five keep their numbers.

### 5.1 The two kinds, and why the distinction is load-bearing

| | **Invariants** (`I-*`) | **Quality measures** (`M-*`) |
|---|---|---|
| Shape | Binary. True or false | A number against a threshold |
| Threshold | **None needed** — the condition *is* the criterion | Owner-set, **and not yet settable** (Q-5) |
| Cost | Discipline only | Real work |
| When violated | The dataset is **not at that tier**, whatever else is true | The dataset is at that tier and **not yet good enough** |
| Direction | **Continuous** — a later violation demotes | Achieved, then maintained |

**An invariant is not a criterion the dataset can score badly on and proceed anyway.**

| ID | Invariant | Check | Today |
|---|---|---|---|
| **I-1** *(was M-2)* | **Provenance completeness** | Every row added after S-3 carries all required DFD-5 properties. Enforced by CI at the canonical boundary, not by discipline | **Not enforceable** — the path does not exist. 15.1% of the corpus is untraceable |
| **I-2** *(was M-5)* | **Held-out reservation** | The held-out partition was reserved **before** training merge, by a pre-registered rule, and the reservation is recorded | **Never done.** `_merge_seed.py` made this exact error once |
| **I-3** *(was M-7)* | **Gold/Silver separation** | No synthetic row and no `collected_v4`-derived row appears in Gold-R or in any held-out evaluation set. Automated | **Failed once already** — the shipped model trained on all 903 rows |
| **I-4** *(new, S-5.2)* | **Provenance recorded at creation** | Provenance was **recorded at creation**, not derived or backfilled. Binary, and set now — a property of the row, not of a tier | **False** for every existing row. Derived-provenance rows are capped below the top tier and barred from the S-6 holdout |

**All four are currently false, and the first three are free.** That is the single most useful thing
this section says: the project is at the bottom tier for reasons that mostly cost nothing but sequence.

### 5.2 The tier ladder

Each tier's entry conditions are stated so they can come back negative. **No tier is defined by a number
this proposal invented.**

| Tier | Name | Entry conditions | Threshold needed? |
|---|---|---|---|
| **T-0** | **Unmanaged** | *Where the project is today.* No annotation spec; provenance absent on 15.1% of rows; no held-out real data; label passes disagreeing with no adjudication record | — |
| **T-1** | **Governed** | The **S-1.2 / S-2.5** reservation recorded (drafted here as `S-0`) · S-1 findings written, with a cause ruling per contested boundary · S-2 `v1` exists and **passed both pre-registered thresholds** · S-3 shipped, so **I-1** holds for every new row · **I-3** holds · existing rows carry their *known* provenance, with the 136 marked **`untraceable`** and the derived/recorded marker present | **No.** Every condition is an existence or a binary. T-1 is reachable **without a single new row** |
| **T-2** | **Evaluation-ready** | T-1 held · `gold_r_v1` exists with a held-out partition assigned by the **pre-registered rule** and satisfying **I-2** · that partition is non-empty (**M-3**) and covers **all five classes at ≥ the Q-4 floor** (**M-4**) · claim scope recorded · **I-1/I-3/I-4 still hold** | **One** — the Q-4 floor, used here as a **readiness criterion**. Deliberately unset |
| **T-3** | **Model-development-ready** | T-2 held · **M-1** label reproducibility measured under the spec and accepted · **M-6** real class distribution observed rather than assumed · **M-8** confidence calibration computable from real telemetry · volume sufficient for the intended model work | **Yes, several** — and they are the ones Q-5 forbids setting now. T-2's evidence is what makes them settable |

**Two properties of this ladder worth stating explicitly:**

1. **T-1 is reachable with zero new data.** It is entirely governance: a reservation, a finding, a spec,
   a schema and an enforcement point. The project's instinct — that maturity requires collecting — is
   wrong about the first tier, and the first tier is the one blocking every other.
2. **The thresholds live only in T-3, and T-2 produces the evidence for them.** That is why Q-5's
   *"without inventing arbitrary thresholds before evidence exists"* is satisfiable rather than
   paralysing.

### 5.3 The quality measures, with baselines and blank thresholds

Thresholds stay blank by ruling, not by omission. **Each is written so it can come back negative**, and
each states the baseline it must beat, so a later threshold cannot be quietly set below the current
state.

| # | Measure | Measurement | Baseline to beat | Threshold |
|---|---|---|---|---|
| **M-1** | **Label reproducibility** | Owner pass vs an independent reader's blind probe under the S-2 spec (**R-1 closed, S-2.2**); per-dimension agreement | **29.6% disagreement** (legacy→interim frame) | owner, at **T-3**. **Distinct from S-2.7's gate**, which is set and governs T-1 — see §10 |
| **M-3** | **Real evaluation existence** | Count of Gold-R held-out rows never seen in training | **0** | owner — **non-zero is itself a step change** |
| **M-4** | **Class coverage in real evaluation** | Classes with ≥ *floor* real held-out rows | **0 of 5** (3 of 5 even counting authored rows) | the Q-4 floor, at **T-2** |
| **M-6** | **Distributional grounding** | Real class distribution observed rather than assumed — **reported with its claim scope** | `[unknown]`; the corpus was balanced to 1.11× against an unobserved distribution | owner, at **T-3** |
| **M-8** | **Instrument calibration measurable** | Confidence bins computable from real telemetry | **Was impossible** — `Confidence` was `null` on every row. **The DFD-9a fix shipped 2026-08-26**; new rows carry it, pre-fix rows never will, and the end-to-end check is still open | binary — and **not yet satisfied**: the columns populate, the volume is `[unknown]` and unmeasured |

`[inference]` **M-8 is the one most likely to be mis-scored.** Its blocker moved from *impossible* to
*pending volume* when the fix shipped, which reads like progress and is — but "the column now has
values" is not "calibration is measurable", and the ≥50-row gate has still never been met. **The fix
removed the reason it was impossible; it did not make it true.** S-T.3 authorises the measurement that
would settle it, and that measurement has not been run.

## 6. What this proposal is not

- **Not authorization.** Nothing here is scheduled or approved. Stage-by-stage go/no-go is the owner's.
  **Ruling all 54 stage decisions did not change this** — the 2026-09-04 record says so in its own
  banner, and rev 3 changes what this proposal **says**, not what it is permitted to do.
- **Not "generate more data".** The ruling forbids that framing and the evidence does not support it:
  the binding constraint is label authority, not row count. **S-7.2 now suspends synthetic creation
  outright for this stage.**
- **Not a reopening.** P-1…P-3, DFD-1…DFD-9b, Q-1…Q-5 and S-1…S-8/S-T are inputs. §18 keeps the Edge AI
  initiative stopped; S-8.1 states the bar a revival would face without lowering it.
- **Not the DFD-9a defect.** Raised separately and deliberately. It **shipped 2026-08-26** and moved no
  confidence threshold; its remaining end-to-end gate is tracked on the defect record, not here.
- **Not a set of thresholds.** §5's ladder deliberately carries blanks, and S-6.3 adds that no
  numerical precision threshold may be set before Q-5 permits one. **S-2.7's two numbers are a
  spec-reproducibility gate, which is a different object** — §10.
- **Not a taxonomy redesign.** S-1 is bounded by P-3 to the collisions the audit measured.
- **Not a schedule.** Q-1 must be measured before any timeline is credible, and it measures only the
  contested half.

## 7. Risks

| Risk | Why it is live here | Control |
|---|---|---|
| **Stage skipping under pressure** | S-4/S-5 produce visible artifacts; S-0/S-1/S-2/S-3 produce documents. The temptation is to collect first and formalise later — exactly how the current corpus was built | The dependency in §0 is a dependency, not a preference. Rows added before S-2 inherit unknown correctness |
| **Reading the reserved rows before reserving them** | S-0 looks like paperwork standing between the project and its first real stage | S-1.2/S-1.6 make it a **binding precondition**, and the reservation is a recorded snapshot artifact. Independence cannot be reconstructed afterwards |
| **Gold-A standing in for Gold-R** | Gold-A is cheaper, closer, and fully under owner control | Separate names and versions. **S-5.4 removes the ambiguity structurally**: Gold-A is in the training partition, so it cannot produce a performance figure at all |
| **Synthetic filling a measured gap that is not a real gap** | The most plausible-sounding error available | **S-7.2 suspends generation** for this stage; resumption needs an observed-data gap **and** a warrant. The distribution check is built and proven to fail on `collected_v4` before any batch is pending |
| **AI drifting into Gold authority** | The volume argument is genuine — a solo developer facing 133 adjudications plus an authored sample | DFD-8, instantiated three times: S-2.2 (probe, not label), S-4.3 (commit-then-reveal), S-5.5 (owner-assigned). The spec records which parts were AI-drafted |
| **Provenance deferred "until the schema settles"** | The single failure mode that cannot be repaired later | S-3 before S-4/S-5. The 136 untraceable rows are the standing evidence |
| **A maturity criterion that cannot fail** | The `M-*` measures could each be written to always pass | A baseline is stated for each. `I-1`…`I-4` are binary, and **all four are false today** |
| **A convenience sample read as the population** | Q-4 makes the *observed distribution* the sampling target, and the distribution observed is that sample's | Claim scope recorded in the datasheet and repeated at every citation: *"observed among N recruited participants."* An S-5 exit criterion, and M-6 carries it |
| **A tier or gate declared reached on its visible half** | `S-T` is the sharp case: instrumentation is the only gate that is engineering work | Every tier's conditions are enumerated; `I-1`…`I-4` are **continuously in force** — a later violation demotes rather than being grandfathered. **S-T.1 requires two gate states rendered explicitly — a single roll-up field is non-conforming** |
| **A figure read without its qualifier** | Six reader-facing non-uniformities now exist, each ruled for its own good reason | **§10.** Stated once, cited from the stages, rather than re-derived per figure |

## 8. Immediate next step

**S-0 — the reservation event.** Forty rows, pre-partitioned, composition pre-registered, executed
**before S-1 reads any row text.** It is not paperwork: it is the one step whose omission cannot be
repaired later, and S-1.6 makes it a binding precondition rather than a recommendation.

Then, in order:

1. **S-1** — the limited taxonomy review, reading only from the remainder. It resolves to owner rulings
   on five bounded items, including **a separate cause ruling per contested boundary**. **It is not
   free of tooling or data** — it needs the reservation executed, the audit's partition, and the third
   pass's 121 rows as evidence.
2. **S-2** — write the annotation spec and pass **both** pre-registered thresholds (TaskType ≥ 17/20,
   Difficulty ≥ 18/20, exact match). `R-1` is closed, so the shape the thresholds were registered for
   is fixed.
3. **Q-1** — adjudicate 20 rows from the contested backlog, timed, recording ambiguous cases and
   guideline gaps. It estimates **contested adjudication time only**.
4. Everything else waits on those, except **S-T**, which can start now — and whose authorised
   volume/usability measurement (S-T.3) is the input the retention window (S-T.4) is waiting on.

**Authorized 2026-09-04.** This is the order the rulings imply, and it is now the order in motion.
**S-0 executed 2026-09-04** — sealed snapshot at
[`../../datasheets/reservations/2026-09-04-s0-reservation-snapshot.json`](../../datasheets/reservations/2026-09-04-s0-reservation-snapshot.json),
pre-registration at
[`2026-09-04-s0-reservation-preregistration.md`](2026-09-04-s0-reservation-preregistration.md).
**S-1 executed 2026-09-04** — ruling record at
[`2026-09-04-s1-limited-taxonomy-review.md`](2026-09-04-s1-limited-taxonomy-review.md), agent-authored and
**awaiting owner acceptance**; it carries four items raised for owner decision and rules on none of them.
**S-2 specification drafted 2026-09-04** — canonical annotation specification at
[`../specs/annotation-guideline.md`](../specs/annotation-guideline.md) (`GuidelineVersion: v1`, **awaiting owner
ratification**), catalogue selection pre-registered at
[`2026-09-04-s2-catalogue-preregistration.md`](2026-09-04-s2-catalogue-preregistration.md).
**`v1` ratified for test and frozen 2026-09-04** — anchor at
[`2026-09-04-s2-v1-freeze-record.md`](2026-09-04-s2-v1-freeze-record.md). **Ratified for testing, not validated.**
**S-2's reproducibility test (S-2.1–S-2.7) is pre-registered and NOT performed** — it blocks on a recruited
independent reader, and **DFD-2 is ruled satisfied only once that test passes**. **No stage after S-2's
specification has been performed.**

**Test instrument materialised 2026-09-05**, under owner authorisation to read the 20 reserved row
texts **solely to render the instrument — this is test-material preparation, not adjudication and not
measurement.** Two artefacts, separated by content rather than by location:

| Artefact | Faces | Carries |
|---|---|---|
| [`../specs/2026-09-05-s2-blind-reader-sheet.md`](../specs/2026-09-05-s2-blind-reader-sheet.md) | **The annotators** | Item id `R-01`–`R-20`, raw text, four blank fields. **No hash, no source locator, no stratum, no historical label, no catalogue reference** |
| [`../../datasheets/reservations/2026-09-05-s2-scoring-key.json`](../../datasheets/reservations/2026-09-05-s2-scoring-key.json) | **No annotator, ever** | Item ↔ `sha256` ↔ occurrences, `kind`, contested stratum, D-2 class. **No label value of any kind** |

Both are rendered by
[`../../tools/data-maturation/s2_materialize.py`](../../tools/data-maturation/s2_materialize.py) from the
sealed snapshot and are deterministic — same inputs, same bytes. Every label needed at scoring time is
**derived then, from the occurrences**, which is why the key cannot anchor either pass. The sheet's
presentation order exists to **interleave** contested and Difficulty-spread rows so the sheet itself
carries no ambiguity signal; **it is not obscurity**, since the salt is committed.

**Gold-pass protocol — an execution control, not an amendment to frozen `v1`.** §10 is frozen and says
nothing about what an annotator may consult. This record supplies that control and **alters no
ratified text**:

1. While annotating, **each annotator consults `v1` and nothing else** — not the snapshot, not the
   corpus CSVs, not the scoring key. The pass-2 `Difficulty` of the eight Difficulty-spread rows sits
   in the sealed snapshot, so this protocol is what keeps it out of the Gold pass. **No file
   permission enforces it.**
2. **Each completed pass is hashed and committed before the other is opened**, and both before either
   is scored. Otherwise neither pass can be shown not to have been adjusted toward the other.
3. The reader receives **the sheet and `v1` only**, as exports. Blindness rests on that: the row text
   is greppable in the corpus, so anyone with repo access can recover the historical labels.

**Pre-test finding, raised and NOT ruled on.** §12 and §2 together require an annotator to mark a row
`unresolved` rather than guess, but §10's `≥ 17/20` and `≥ 18/20` do not say how an `unresolved` row
scores. **The gap is in frozen `v1`.** It is recorded here because finding it before the test is
legitimate, while ruling on it after seeing results is precisely what §14 forbids. **No figure depends
on it yet, and none may be produced until it is ruled on.**

**Reader package revised for handoff 2026-09-05 — PREPARED, NOT PERFORMED.** The §10 test has still
**not been run**, **no reader is recruited**, nothing was annotated, adjudicated or scored, and **no
figure exists.**

**The instrument was not re-made.** The materialisation at `b9691ae` is the source of truth: the
corpus was not read, the sealed snapshot was not re-drawn, and no row was re-selected or re-rendered.
[`../../tools/data-maturation/s2_reader_package_revise.py`](../../tools/data-maturation/s2_reader_package_revise.py)
takes the committed sheet **blob**, splits it into header · 20 item blocks · footer, and emits the
package sheet as *revised header + the same 20 blocks, byte for byte + the same footer*. It asserts
that identity before it writes. **Row text, row order, item ids `R-01`–`R-20` and the answer fields
are unchanged**, so the scoring key still reconciles without being re-rendered.

**Three prose edits, and nothing else** — each one required to match exactly once, so a silent no-op
cannot ship the leak it was meant to remove:

| Removed from the sheet's prose | Why it could not go to a reader |
|---|---|
| Title *"blind annotation sheet, sealed scored batch"* | Named the batch and its reserved status |
| *"Do not discuss the rows with **the other annotator**"* | Disclosed that a second, parallel pass exists |
| `\| **role** \| ``gold`` or ``probe`` \|` | Disclosed that a **reference pass** exists |

Nothing else in the header moved: the five class names, the `decided_by` line, the `unresolved` line
and the no-meaningful-order paragraph were already neutral and are byte-identical.

| Reader-facing — [`../s2-reader-package/`](../s2-reader-package/) | `sha256` (LF, as committed) |
|---|---|
| [`00-READ-ME-FIRST.md`](../s2-reader-package/00-READ-ME-FIRST.md) — minimal instructions | `01b3b9f9…14d6cc0d` |
| [`01-annotation-guideline-v1.md`](../s2-reader-package/01-annotation-guideline-v1.md) — **frozen `v1`, byte-exact from `da98e73`, unmodified** | `dd4fc273…684a433` |
| [`02-annotation-sheet.md`](../s2-reader-package/02-annotation-sheet.md) — the `b9691ae` instrument, prose-revised | `0e3ffea8…97ae7128` |

| Private — never handed to a reader, never linked from the package | Identity |
|---|---|
| [`../../datasheets/reservations/2026-09-05-s2-scoring-key.json`](../../datasheets/reservations/2026-09-05-s2-scoring-key.json) — **unchanged**, still the reconciliation map | as committed at `b9691ae` |
| [`../../datasheets/reservations/2026-09-05-s2-reader-package-manifest.json`](../../datasheets/reservations/2026-09-05-s2-reader-package-manifest.json) — revision record only | internal seal `a4d5a66c…ba9f97a5` |

The manifest holds **no item list, no row hash, no stratum, no D-2 class and no label** — the map
already exists in the scoring key, and duplicating it would have created a second thing to keep
private. The sheet at [`../specs/2026-09-05-s2-blind-reader-sheet.md`](../specs/2026-09-05-s2-blind-reader-sheet.md)
is **unmoved and unmodified**; it never reaches the reader, whose handoff is a copy of the package
directory.

**D-6 — the `unresolved` box stays, and says nothing about how it scores.** The owner has not
authorised a scoring treatment for an `unresolved` response. The reader can still mark it, no scoring
semantics were added to `v1`, and the instructions make **no claim** about its treatment. A validator
check enforces that and was demonstrated firing.

**Frozen `v1` ships whole, and that is deliberate.** `v1` contains its own §6 catalogue and its §10
thresholds. Excerpting either would hand the reader **a different instrument than the one §14 froze**,
so the no-catalogue and no-threshold constraints bind **the sheet and the instructions**, where they
are enforced and verified — not the guideline copy. **The reader therefore sees the §10 thresholds via
`v1` itself.** Flagged for the owner; it is not resolvable without either amending `v1` or breaking
byte-exactness, and **neither is done here.**

**Line endings.** `core.autocrlf=true` on the owner's machine, and this repo had no `.gitattributes`.
A clone would have CRLF-translated the packaged `v1` on checkout, so `sha256` of the reader's copy
would **not** be `dd4fc273…684a433` and the freeze check would fail on a file that was in fact
correct. A one-line `.gitattributes` marks `docs/s2-reader-package/**` as `-text`.

**Validation** —
[`../../tools/data-maturation/s2_reader_package_validate.py`](../../tools/data-maturation/s2_reader_package_validate.py),
**14 checks, all PASS**: file set · `v1` byte-exactness · 20 item blocks · **blocks byte-identical to
`b9691ae`** · every row a sealed `scored_batch` hash, once · no foreign row · 12 + 8 · item ids
reconcile against the scoring key · no key material · no measurement term · **no D-6 scoring claim** ·
private artefacts outside the package · manifest label-free · LF endings. The "no row outside the 20"
check is **hash-set comparison**, never a read of the other 40 reserved rows.

**Every check was demonstrated failing before the green run was believed** — 14 mutated throwaway
copies: flipped `v1` byte · row block deleted · row text altered · an answer field added inside a
block · foreign row substituted · items renumbered · `gold`/`probe` injected · a threshold disclosed ·
a D-6 scoring treatment asserted · row hash leaked · source locator leaked · scoring key copied in ·
manifest copied in · CRLF translation. **All 14 fired the expected check.**

**Untouched:** frozen `v1` (still `dd4fc273…684a433`), the sealed S-0 snapshot and its seed, the
scoring key, the `b9691ae` sheet, every Decision Outcome and Working Note, and the **119-vs-121 /
155-vs-157 residual**.

### Reader package translated to Vietnamese 2026-09-05 — PREPARED, **HANDOFF BLOCKED**

The recruited readers satisfy the Q-2 conditions but **do not study IT or any technical subject**,
and frozen `v1` is written in English. An instrument the reader cannot read measures reading
comprehension, not guideline reproducibility, so the owner directed that the package be translated.
The §10 test is still **not performed**, **no reader is recruited**, nothing was annotated,
adjudicated or scored, and **no figure exists.**

**Two independent blockers stand between this package and a reader.** Ruling on one does
**not** clear the other.

#### Blocker 1 — catalogue overlap. **Not a translation defect**

Found by a shingle check while preparing this revision. **It was already present in the
English package that validated 14 of 14 on 2026-09-05** — those checks did not cover it, and
the translation neither caused it nor changed it.

**Four of the twenty reserved rows are template-identical to entries in the frozen `v1` §6
catalogue, differing only in the course name.**

| Item | Frozen `v1` line | Similarity |
|---|---|---|
| `R-01` | 332 | 0.86 |
| `R-09` | 265 | 0.83 |
| `R-14` | 396 | 0.75 |
| `R-16` | 256 | 0.83 |

Those catalogue entries carry rulings. `00-READ-ME-FIRST.md` step 1 tells the reader to read the
guideline **before** the sheet, so a reader can answer these four rows by **pattern-matching a
worked example** rather than by applying the rules — and they are scored the same as any other row
against the pre-registered TaskType threshold. **Agreement on them is therefore not evidence that
the guideline reproduces a label.**

**The derived labels are deliberately not recorded** — not here, not in the manifest, not in the
report. The claim this record makes is the template match and nothing more.

**Neither side is fixable from here.** `v1` is frozen under §14 and the 20-row batch is sealed under
S-0; changing either is an owner decision. **The package must not be handed to a reader until this
is ruled on.** §6 of the Vietnamese rendering is therefore **held untranslated**, since the ruling
may change what §6 should say; the rendering says so in place and points at the English original.

#### What was translated, and what was not

| | |
|---|---|
| **Row text** | **Not translated.** It is the material under test, it is already Vietnamese, and one changed character changes what §10 measures. The 20 item blocks stay byte-identical to `b9691ae` |
| **Frozen `v1`** | **Not translated in place.** Still byte-exact at `dd4fc273…684a433`, still the text with authority |
| **The rendering** | New file beside `v1`, its own `sha256`, `derived_from` recorded in the manifest. **Unratified** |
| **Reader prose** | Instructions, sheet header and footer — the package's own prose, never part of the instrument |

**Fidelity over fluency.** S-2 measures whether **`v1`'s wording** reproduces, so a translation that
disambiguates `v1` would delete the very defect the test exists to detect. No rule was added, no
example was added, and **no ambiguity was resolved — `B-2` is rendered exactly as unclear as it is
in the source.**

**Terminology lock.** In English, the strings `B-2`/`B-4` decide on (`giữa kỳ`, `cuối kỳ`,
`kiểm tra`, `đồ án`, `bài tập lớn`, `nhóm`) appear only as marked foreign quotations. In Vietnamese
prose they are ordinary words that would happily turn up in an explanation, and **`B-4` acts on
their mere presence** — so a careless sentence could move a lexical boundary. They therefore appear
in the rendering **only inside code spans**, checked mechanically (C15); the surrounding prose uses
non-matching wordings instead. The check **fired once during drafting** on a stray `nhóm`, which was
reworded.

| Reader-facing — [`../s2-reader-package/`](../s2-reader-package/) | `sha256` (LF, as committed) |
|---|---|
| [`00-READ-ME-FIRST.md`](../s2-reader-package/00-READ-ME-FIRST.md) — Vietnamese instructions | `05adbad5…444da38f` |
| [`01-annotation-guideline-v1.md`](../s2-reader-package/01-annotation-guideline-v1.md) — **frozen `v1`, unmodified** | `dd4fc273…684a433` |
| [`01b-huong-dan-tieng-viet.md`](../s2-reader-package/01b-huong-dan-tieng-viet.md) — rendering of `v1`, **unratified** | `5f8c4877…bd24e483` |
| [`02-annotation-sheet.md`](../s2-reader-package/02-annotation-sheet.md) — the `b9691ae` instrument, Vietnamese prose | `a3c32814…daad96b5e` |

The packaged `v1` still stages as git blob `0dbfc474…`, **the exact blob of frozen `v1` at
`da98e73`**, and `git check-attr` confirms the `-text` rule now covers the rendering too.

#### Blocker 2 — the rendering is unratified

If the reader works from the rendering, then **the rendering is in practice the
instrument**, and it has not been ratified the way `v1` was — `v1` was frozen precisely so
that §10 measures a fixed target.
The sheet's `guideline_version` still reads `v1`, because `v1` is what is being tested and certifying
translation provenance is not the reader's job; the rendering's use is recorded in the manifest
instead. **Ratification is required before handoff.**

**D-6 unchanged.** The reader can still mark `unresolved`; neither the instructions, the sheet nor
the rendering say how it scores, and no scoring semantics were added to `v1`.

**Validation** — **17 checks, all PASS**, extended from 14. The three new ones are translation
checks: **C15** trigger terms only inside code spans · **C16** the rendering mirrors `v1`'s 14
sections · **C17** no reserved row text appears in the rendering, which guards the catalogue overlap
above from being carried into a future §6 translation.

**The English scans were the real risk and were fixed.** C9/C10/C11 were keyword lists over English
prose; against a Vietnamese package they would have **passed vacuously**, proving only that the
package contains no English. They now carry Vietnamese equivalents
([`../../tools/data-maturation/s2_reader_terms_vi.py`](../../tools/data-maturation/s2_reader_terms_vi.py)),
scoped to English parity rather than tighter — a Vietnamese list stricter than the English one it
mirrors fired on correct prose during drafting and was loosened, not the file.

**Every check was demonstrated failing before the green run was believed — 25 controls, all fired**,
including a Vietnamese threshold disclosure, a Vietnamese D-6 claim in the instructions **and** the
same claim in the rendering (proving C11 reaches the new file), a loose trigger term, a dropped
section, an echoed reserved row, and a leaked row hash. **C13 had never been seen red in any earlier
run**; it scans the private manifest, so it needed a control that mutates the real file and restores
it — the restore is verified by `sha256` comparison.

**Two real defects were caught by the controls, not by inspection.** `blocks_of()` cut the sheet on
the English footer's words, so translating the footer made the instrument itself look changed
(**C4 reported 19 of 20**); it now cuts on the closing `---` rule. And the first Vietnamese term list
was stricter than its English counterpart, firing on the legitimate instruction not to look for
answers.

**Untouched:** frozen `v1`, the sealed S-0 snapshot and its seed, the scoring key, the `b9691ae`
sheet, `s2_materialize.py`, every Decision Outcome and Working Note, and the **119-vs-121 /
155-vs-157 residual**.

### Both blockers ruled 2026-09-05 — `D-7`, `D-8`, `D-9`

Recorded in full in [`2026-09-05-s2-owner-decisions.md`](2026-09-05-s2-owner-decisions.md). The §10
test is still **not performed**, **no reader is recruited**, and **no figure exists.**

**The finding got worse when it was opened properly.** The first report described §6 as worked
examples a reader could pattern-match. Reading the whole section showed it also prints, across its 19
entries, **19 historical pass-label progressions, 19 Difficulty values, 19 row `sha256` and 57 source
`file:line` locators** — four categories that are all on the reader-package exclusion list, reaching
the reader inside the frozen guideline. **Two earlier reports understated this**, and the remedy was
re-proposed against the measured contents rather than the first impression.

**`v1` §6 states the invariant it fails**: *"The reserved 60 rows are absent from this catalogue by
construction — an example drawn from the scored batch would train a reader on a row they are later
measured against."* The exclusion ran at **row** granularity; the corpus is template-generated and the
catalogue's own rule deduplicates **by template**. Zero verbatim matches — the letter holds, the
spirit does not.

| Ruling | Effect |
|---|---|
| **`D-7`** | The frozen 20-row gate is **unchanged** (≥17/20, ≥18/20). The **16 non-twin rows** are additionally reported as the **primary evidence** at ≥14/16 and ≥15/16 — 85%/90% on a denominator of 16, **rounded up**, so the secondary reading is marginally stricter, not looser. Ruled **before any annotation**, so it is pre-registration; S-2.7 closes this door the moment the reader starts |
| **`D-8`** | The Vietnamese rendering is the instrument, and **both passes use it** — running the Gold pass on English `v1` would have made every disagreement ambiguous between a guideline defect and a translation artifact. Figures carry the scope *"reproducibility of `v1` as rendered in Vietnamese"* |
| **`D-9`** | Per catalogue entry, the `sha256`, the locators and the pass-label/Difficulty line are **removed from the rendering**; **every example row and every word of commentary is kept.** `v1` §6 itself says no entry may be cited as its row's label, so nothing the reader reasons from was taken |

**Numbering correction.** Earlier records on 2026-09-05 cited **`D-5`** for the unresolved-scoring
constraint. The owner-decision series `v1` cites — S-1 review §6, source of `D-2` and `D-4` — already
uses **`D-5` for "the sealed residual."** The constraint is renumbered **`D-6`**; the citations above
and in the manifest and validator are corrected. **No decision changed, only its label.**

**§6 is now rendered in full**: 19 entries, every example row **verbatim from `v1`** (checked — the
only non-verbatim quoted lines are the rendering's own note about the strip), all commentary
translated, zero hashes, zero locators, zero label progressions.

| Reader-facing | `sha256` (LF) |
|---|---|
| [`00-READ-ME-FIRST.md`](../s2-reader-package/00-READ-ME-FIRST.md) | `05adbad5…444da38f` |
| [`01-annotation-guideline-v1.md`](../s2-reader-package/01-annotation-guideline-v1.md) — **frozen, unmodified** | `dd4fc273…684a433` |
| [`01b-huong-dan-tieng-viet.md`](../s2-reader-package/01b-huong-dan-tieng-viet.md) — **unratified** | `3b1dbd24…a07ca61c` |
| [`02-annotation-sheet.md`](../s2-reader-package/02-annotation-sheet.md) | `a3c32814…aad96b5e` |

**Validation — 18 checks, all PASS.** New: **`C18`**, which enforces `D-9` by rejecting any row
`sha256`, source locator or label progression in the rendering. **`C15` was also corrected**: it now
exempts lines appearing **verbatim in frozen `v1`**, because §6's quoted corpus rows carry trigger
terms in bare prose and are `v1`'s text, not the translator's — without that fix the check would have
policed `v1`'s own words while claiming to police the translation.

**26 negative controls, all fire.** `C18` was demonstrated red three ways in isolation — a restored
`sha256`, a restored locator, a restored pass-label line. The trigger check fired for real a second
time during this pass, on a stray `nhóm` in the new §6 commentary, which was reworded.

**Still open, and still blocking handoff:** the owner has **not yet read the rendering through**.
Ratification is that read-through plus recording the rendering's `sha256` in the freeze record.
Reader recruitment remains a separate owner action.

**The derived labels for the four twins are recorded nowhere** — not here, not in the owner-decisions
file, not in the manifest. The owner performs the Gold pass, so writing them into a working record
would prime the pass they anchor.




---

## 9. Revision record

### 9.1 Rev 2 — 2026-08-27

Revised **in place** rather than by appended amendment: this is a `draft` that was never ratified, and
the owner commissioned *"the next proposal revision."* The convention for correcting a **dated** record
by amendment applies to artifacts whose job is to say what was true when written; a live proposal's job
is to be current. Rev 3 follows the same convention.

| # | Change | Driven by |
|---|---|---|
| 1 | Banner: `awaiting owner review` -> **`awaiting authorization`** | The review happened |
| 2 | §1 gains the seven 2026-08-27 inputs | Requirement 1 |
| 3 | **S-1 gains a fourth scope item** — *is the disagreement taxonomy-semantic or annotation inconsistency?* | The 2026-08-27 wording of **P-3**. **Material difference from 2026-08-26** |
| 4 | S-2 separates its reproducibility test from the Q-1 measurement, and opens **`R-1`** | Requirement 3 |
| 5 | S-5 Track A: feasibility, venue and hybrid sampling ratified; convenience-sample claim scope added | Requirements 1, 5 |
| 6 | **New strand `S-T`** — telemetry readiness, pulled out of S-5 Track B | Requirement 5 |
| 7 | S-7 states that Q-4's permission to target gaps covers **collecting**, not **generating** | Requirement 5 |
| 8 | §4.2 rewritten: each Q carries its ruling **and its residual open parameter** | Requirement 2 |
| 9 | **§5 rebuilt as a tier ladder** — `T-0…T-3`, with `I-*` split from `M-*` | Requirement 4 (Q-5) |
| 10 | §7 gains three risks | Consequences of the above |
| 11 | §8 reordered to lead with S-1; Q-1 gated behind S-2 `v1` | Requirement 3 |

### 9.2 Statements rev 1 made that rev 2 superseded

| Rev 1 said | Superseded by | Why |
|---|---|---|
| §2: *"`StudyTimeOutcomeLogs` — real outcomes, but `PredictedMinutes` / `Confidence` written `null`"* | §2's telemetry row | The DFD-9a fix shipped hours after rev 1 was written |
| §5 M-8: *"Today: impossible — `Confidence` is `null`"* | §5.3 M-8 | The blocker moved from *impossible* to *pending volume* — **not the same as satisfied** |
| §4.2: Q-1…Q-5 as *"open questions, not estimates"* | §4.2's ruling + residual columns | The owner ruled on all five |
| §5: a flat `M-1…M-8` table with blank thresholds | §5.1–5.3 | Q-5 requires tiers, and invariants distinct from thresholds |
| §0: *"the DFD-9a instrumentation defect can run in parallel from day one"* | §0 property 3 | It did, and it finished |

`[observation]` The M-8 case is the useful one to keep. Rev 1 was accurate when written, went stale the
same afternoon, and would have been carried into rev 2 unnoticed had the reconciliation looked only at
the new ruling. **A document whose baselines are `[measured]` acquires a maintenance obligation that a
document of opinions does not.**

### 9.3 Requirements from the 2026-08-27 outcomes document

| # | Requirement | Where | State |
|---|---|---|---|
| 1 | Incorporate all ratified decisions | §1.1, §4.2 | **Done** |
| 2 | Preserve unresolved implementation parameters as explicit open questions | §4.2 residual column | **Done** |
| 3 | Convert Q-1 into a planned measurement after S-2 | S-2, §8 step 3 | **Done**, and narrowed by S-2.6 |
| 4 | Define the tiered maturity model without arbitrary thresholds | §5.1–5.3 | **Done** |
| 5 | Separate governance, real-data collection, evaluation, telemetry, controlled expansion | S-0…S-3 · S-5 · S-6 · **S-T** · S-7 | **Done** |
| 6 | Keep the Edge AI initiative stopped at S0 | §1.1, S-8, §6 | **Unchanged.** DAT-04 stands |
| 7 | Keep DFD-9a a separate defect | [defect record](2026-08-26-prediction-instrumentation-defect.md); §6 | **Done** |

### 9.4 Rev 3 — 2026-09-04

Written from the [traceability matrix](2026-09-04-decision-to-revision-traceability-matrix.md), which
maps all 54 ruled items onto the sections below. **The stage decision surface is closed; authorization
is not granted.**

| # | Change | Driven by |
|---|---|---|
| 1 | Banner: rev 3, and an explicit statement that ruling 54 decisions is not authorization | 2026-09-04 record's own framing |
| 2 | **New §1.2 — rules that apply throughout**: five standing principles, the explicit `N/A` state, rule-now-number-later, manifest-as-attestation, the four version streams, and the owner's **standing directive** that a governance decision is never deferred on implementation grounds | Scope ruling · Outcomes §A.1 |
| 3 | **New sequencing label `S-0`** — the reservation event, drawn as a step in §0's chain and led with in §8. **A rendering choice, not a new ratified stage** | S-1.2 · S-1.6 · S-2.5 |
| 4 | §1.1 rows for **P-1, P-2, DFD-6, DFD-7 and ruling §18** restated | S-7.1 · S-4.2 · S-7.2 · S-7.3 · S-8.1 |
| 5 | S-1: retirement **confirmed**, reminder-ness relocated; third pass admitted as evidence; Difficulty anchors defined; zero-coverage reframed and its evaluation defect **moved to S-6**; **a cause ruling per boundary** | S-1.0 … S-1.6 |
| 6 | S-2: `R-1` **closed**; two thresholds pre-registered; independent per-dimension scoring; one stratified batch scored in full; catalogue boundary-indexed with a pre-registered selection rule; `RowId` and hash addressing; bump semantics; AI-drafting disclosure; **"adjudicate" defined as five-way** | S-2.1 … S-2.12 |
| 7 | **S-3 rewritten**: canonical JSONL corpus outside the app, byte-stable generated export, resolved consumer boundary, external build manifest, versioned fail-closed provenance object, datasheets scoped to Group A's 10 (+B1) with A8 excluded, CI enforcement stated at **merge scope** | S-3.1 … S-3.7 + closing confirmation |
| 8 | S-4: scope ruled at **scope level** (contested pool + designed authored sample); the 136 **eligible**; commit-then-reveal; exclude-and-log with a guideline-gap artifact; targeted rule attribution; manifest as a **dependency declaration** | S-4.1 … S-4.6 · S-5.6 |
| 9 | S-5: consent **at transfer**; provenance grades tier-capped; role partition; **Gold-A stays in training**; owner-assigned labels with `FinalDoKho` preserved as metadata; pre-registered allocation rule | S-5.1 … S-5.7 |
| 10 | S-T: **two independent gates**, capture-complete provenance, authorised volume-only measurement, bounded rolling retention with a `[unknown]` window, one datasheet artifact | S-T.1 … S-T.5 |
| 11 | **S-6 gains a metric registry** — pre-registered set, MAE headline for Difficulty, chance baselines from the observed marginal, intervals on every figure, and the one-sided chance guard | S-6.1 … S-6.3 |
| 12 | S-7 becomes **governance-only**: per-source promotability, synthetic **suspended**, all three public candidates closed, distribution check built and frozen now over feature families | S-7.1 … S-7.4 |
| 13 | S-8's placeholder replaced by **four necessary-not-sufficient preconditions**, the hypothesis schema, the versioning clause, and the explicitly unset threshold | S-8.1 |
| 14 | §5 gains **`I-4` provenance-recorded-at-creation**; §S-6's exclusion list gains a third entry | S-5.2 |
| 15 | **New §10 — how to read a figure from this project** | Outcomes §A.2 (agent-authored; see §10's preamble) |
| 16 | **Correction pass, 2026-09-04** — document-integrity repairs only, listed in §9.6. **No ratified decision was reopened, narrowed, or reinterpreted, and no new policy was introduced** | Owner direction after the rev 3 consistency pass |
| 16 | **New §11 — errata**, kept out of the policy body | Outcomes §A.3 |

### 9.5 Statements rev 2 made that rev 3 supersedes

Kept here rather than left in the body. These are **consequences of the new rulings** — statements that
were true or defensible when written and are not any more. Statements that were **false when written**
are in §11 instead, and the two must not be read as one list.

| Rev 2 said | Superseded by | Why |
|---|---|---|
| §1, P-2 row: the 136 *"can never be promoted to Gold"* | S-4.2 | **Not ratified text** — P-2 contains no promotability clause. They are eligible for Gold-A after adjudication |
| §1, P-1 row: `collected_v4` is *"a Silver candidate at best"* | S-7.1 | Promotability is a per-source ruling. `collected_v4`'s is **`unruled`**, which is an open task, not a settled "no" |
| §S-7: Silver is *"usable for training, never promotable to Gold"* | S-7.1 | Superseded — and **not** replaced by a blanket permission |
| §S-4's AI-assist paragraph: AI *"reduces the work"* | S-4.3 | Under commit-then-reveal the saving is ordering and clustering only |
| §S-4's J-7 line: `[unknown]` until J-1 runs | S-4.4 | The **count** stays `[unknown]`; the **policy** does not |
| §S-5: Track B *"can start earliest and costs the least"* | S-5.1 | Now false. It carries a consent step and a transfer mechanism that does not exist |
| §S-6: *"Gold-A answers is the model consistent with the label definitions"* | S-5.4 | Gold-A is in the training partition; it produces no performance figure |
| §S-6's averaging rule | S-5.4 | The rule survives; its stated reason does not |
| §S-T: a single consent gate, and *"a dataset contract"* | S-T.1 · S-T.5 | The gate splits in two with distinct state vocabularies; the contract is the S-3.6 datasheet |
| §S-T's gate list read as a sequence | S-T.2 | Gates 1 and 4 are **mutually dependent** |
| §S-8's placeholder | S-8.1 | Replaced by four preconditions and a hypothesis schema |
| §S-2's `R-1` open subsection; §4.2's `R-1` residual; §8's *"`R-1` must be decided"* | S-2.2 | `R-1` is closed |
| §S-3: *"Retrofitting lineage onto the existing 903 would mean inventing it"* | S-3.2 | Backfill **by join** is derivation, not invention — and the derived/recorded marker is what keeps them distinct |
| §5.2 T-1: `provenance = unknown` for the 136 | S-3.2 | The marker is **`untraceable`** |
| §4.1: synthetic opportunities *"nameable but not yet actionable"* | S-7.2 | Creation is **suspended**; there is no permitted purpose during this stage |
| §1: ViLexNorm *"has a route, not just a blocker"* | S-7.3 | The route exists and **authorises nothing**; OD-4's completion is a hard gate |

### 9.6 Correction pass — 2026-09-04 (document integrity only)

Rev 3 was checked for identifier collisions, arithmetic contradictions, unsupported factual claims and
status semantics before being put forward — **twice**: rows 1–12 from the first pass, rows 13–14 from a
second, narrower pass on the same date confined to evidence-scope wording. **Every item below is a
repair to how rev 3 states something. None reopens, narrows or reinterprets a ratified decision, and
none adds policy.** The historical records — the 2026-08-27 and 2026-09-04 Outcomes, the working
notes, and the audit — were **not amended** to match this text.

| # | Defect in rev 3 as drafted | Repair | Ratified content touched? |
|---|---|---|---|
| 1 | `J-2` was used for the authored sample, colliding with the **audit's `J-2`** (*define the class boundaries that actually collide*, which gates `J-1`) | Renamed to **`W-1`**, a proposal-local namespace, with an explicit namespace note under §S-4's workload table. **The audit's `J-2` is unchanged** | No |
| 2 | *"133 distinct (36 + 121)"* presented a **count of rows** as the sum of a **count of appearances** — 36 + 121 = **157**, which this document states elsewhere as the live pool | The two dimensions are now stated separately and never added: **157 contested appearances** across two boundary-disjoint pools, deduplicating to **133 distinct rows** | No — both figures are ratified (errata #7, #10); only the phrasing changed |
| 3 | `I-4` read *"for top-tier membership, provenance was recorded at creation"*, which makes a **binary invariant** look like a tier-specific eligibility rule | Restated as the general invariant: *provenance was recorded at creation* — a property of the row, not of a tier. The tier consequence stays in the *Today* column, where it was already stated | No — S-5.2 unchanged; `T-2` may legitimately require `I-1`/`I-3`/`I-4` |
| 4 | §S-4's `J-3` row still said **"dispose of"**, the pre-S-4.2 operation | Restated as **adjudicate the untraceable rows for Gold-A eligibility**, preserving the origin-unknown qualifier. *Dispose* is removed as an operation, here and in §S-2's Q-1 narrowing | No — this **applies** S-4.2 rather than changing it |
| 5 | The S-T **local gate** was rendered *"closable now"* / *"open — closable now"*, which reads as available | Rendered **BLOCKED today**: S-T.1's *closable once policy and controls are satisfied* is kept as the **condition**, and S-T.4's uninstantiated retention window — blocked on the **authorised but unperformed** S-T.3 measurement — is stated as the **current state** | No — this reconciles two ratified statements without altering either. The two-gate model is intact |
| 6 | Rev 3 and the 2026-09-04 Outcomes described the same status in different words | Normalised to `draft — awaiting authorization`, with the relationship to the Outcomes' wording stated in the banner. **The status itself did not change** | No |
| 7 | **133** was labelled *production-relevant*; the ratified erratum attaches that scope to **36** | *Production-relevant* now attaches to the **36** under its narrower historical scope. **133** is stated as *distinct contested rows* | No |
| 8 | **13.7%** (96 / 703) and the **2026-08-09** branch-protection date sat in the baseline as if audit-derived | Both labelled **working-notes-derived**, and §2's blanket *"everything in this table is `[measured]` by the audit"* header now names the 13.7% as its one exception. The `[measured]` stamp was removed from the date, which is a configuration fact, not a corpus measurement | No |
| 9 | `S-0` was presented as a new stage | Retained for traceability, explicitly marked a **draft-document sequencing label, not a ratified stage**; `T-1`'s entry condition now anchors to **S-1.2 / S-2.5** rather than to the label | No |
| 10 | §S-3's consumer table named `TextClassifierModelManager` / `TextClassifierDatasetImporter`, which come from **working-notes evidence**, not from S-3.4's ruling | The table carries the ruled category — *the production application* — and the two class names moved to an explicitly **non-normative codebase-evidence note** with their provenance and a re-verification instruction | No |
| 11 | S-T gate labels `2a` / `2b` read as ratified gate identifiers | Both are gate **2**, with **`Local`** and **`Egress`** as **descriptive labels**, marked as such. S-T.1 splits the question; it does not number the halves | No |
| 12 | §7 said `I-1`…`I-4` are *"binary"* in one row and *"continuous"* in another, two rows apart | *Continuous* restored to rev 2's own meaning — **continuously in force, a later violation demotes rather than being grandfathered** — which is not a contradiction of *binary*. Found by the second consistency check, not the first | No |
| 13 | §4.1's **Gold-A adjudication scope** row carried one trailing `[measured]` over an answer that mixes measured counts with two `[unknown]`s | The Basis cell now scopes the stamp to the counts — **133**, **157**, **36**, **121**, **≤136** — and states that the authored sample (S-4.1) and the `J-7` residue (S-4.4) are `[unknown]` **by ruling, not by failed measurement**. The Answer cell is unchanged | No |
| 14 | **~8 new fields (7 → ~15)** was presented as `[measured]` at **three sites** — §S-3's *"Scope, measured:"* line, §4.1's provenance-cost row, and §11's erratum #12 under its blanket `[measured]` header. The **7** is measured from the CSV headers; **~8** and **~15** are design estimates | All three now separate the two: the **7** stays `[measured]`, the **~8** / **~15** are marked `[inference]`, a design estimate. **Erratum #12's correction is not withdrawn** — only its evidentiary grade is marked | No |

**One residual, deliberately left open.** The two decompositions of the contested pool do not reconcile:
ratified erratum #7 gives **36 + 121 = 157**, implying a **24-row** overlap against the 133 distinct
rows, while the working notes decompose the same 133 as *14 cross-pass-only + 97 third-pass-only + 22 in
both*, implying **119** third-pass rows and a **22-row** overlap. **Both cannot be right.** Rev 3 states
only the ratified pair (157 appearances → 133 distinct rows) and **does not reproduce the working-notes
decomposition**, so no arithmetic contradiction enters this document. **Resolving 119 vs 121 requires
re-measurement and is not a drafting decision.**

---

## 10. How to read a figure from this project

> **Provenance of this section.** Every rule below derives from a ruled decision, cited inline.
> **Collecting them into one section is an agent presentation choice, not a ruling** — the source
> (Outcomes §A.2) is agent-authored and says so. It is stated once here because the alternative is
> honouring six reader-facing non-uniformities inconsistently across a dozen sections.

The project's reporting is deliberately non-uniform in six places. Each non-uniformity was ruled for
its own reason, and each is a way a figure can be misread.

1. **TaskType reports a proportion; Difficulty reports a distance** (S-6.2). Exact-match is a rate;
   MAE is an average error size. **These two can never be combined into an overall score**, and a
   document that presents "accuracy" for both is reporting something that does not exist.
2. **Gold-A carries two rule-attribution mechanisms** (S-4.5). Contested-pool TaskType rulings
   *inherit* their recorded boundary; Difficulty rulings and authored-sample rows *cite* a rule
   explicitly. Absence of a citation is not absence of a rule.
3. **Gold-R carries two provenance grades** (S-5.2). Recorded-at-creation and derived/backfilled are
   not interchangeable: the derived grade is capped below the top tier and barred from the S-6 holdout.
   Any Gold-R figure must say which grades it includes.
4. **Promotability is read from a source's datasheet, not from its tier** (S-7.1). "It is Silver" tells
   you nothing about whether its rows may be adjudicated. Check the datasheet's Gold-A eligibility
   field; `unruled` means blocked, not permitted.
5. **Operational figures carry an explicit measurement-scope tag** (S-T.3). A telemetry volume or
   null-rate number is a usability fact and **must never be cited as evidence about the data's
   distribution.**
6. **Datasheets have static and live sections, and `N/A` is not blank** (S-T.5, §1.2). `N/A` means
   "legitimately absent here"; blank means "not yet supplied" and is an error state.

**Two further reading rules belong with these:**

7. **An interval is required on every figure, and excluding chance is not validation** (S-6.3). A
   result whose interval includes the chance baseline is *"not distinguishable from chance."* One that
   excludes it has passed a **one-sided negative-evidence guard** — which is not a precision guarantee
   and not a claim that the result is good.
8. **A re-baselined historical figure is a new figure under the new registry** (S-6.2), not the same
   metric recomputed. **An old and a new number must never be presented as a before/after.**

**And one distinction that is easy to collapse.** Three different threshold objects exist and only one
of them is set:

| Object | Status | Ruling |
|---|---|---|
| **Spec-reproducibility gate** — does the spec transfer to a second reader? | **Set: 17/20 and 18/20**, exact match | S-2.7 |
| **Dataset-maturity threshold** — is the dataset good enough for a tier? | **Not set**, and not settable before evidence | Q-5 |
| **Evaluation-precision threshold** — is a model figure good enough? | **Not set**, and deferred to Q-5 | S-6.3 |

`M-1` at T-3 is the second kind and remains blank; S-2.7's numbers are the first kind and gate T-1.

---

## 11. Errata — statements that were false when written

**These are factual corrections, not policy.** They are kept out of the body's rule text deliberately:
a correction of a wrong number and a new rule are different things, and interleaving them makes it
impossible to tell which is which. Each has been applied in the body; this table records what was
corrected so a reader of an earlier revision can find it.

`[measured]` unless marked otherwise.

| # | Where it was | Correction |
|---|---|---|
| 1 | §S-1 table | *"Two of the three largest transitions name retired classes"* — **false as placed**: `OnTap→NhacNho` is live inside the audit §E.1 comparison frame. The row now reproduces the audit's own wording and **attaches no retirement claim**, because which frame applies is S-1's to establish. `[owner ruling 2026-09-04]` The **audit's §J-2 carries the identical error and is deliberately left unchanged** — a separate document, and out of scope |
| 2 | §S-1 prose | *"the fourth item"* / *"the other three answers"* — the table has **five** rows. The count also appeared in §8 |
| 3 | §S-1 table | *"`Difficulty` … is currently never trained on"* — **false**. True only of the text classifier; Difficulty has **≥4 live consumers** (`MLModelManager.cs:94`, `StudyTimePredictorService.cs:58`, `PerformanceDropRiskEvaluator`, `DifficultyLabelLogs`) |
| 4 | §S-1 table | `KiemTraThuongXuyen` / `ThiCuoiKy` described as possibly *"aspirational"* — they are the **two largest training classes, 358/698 = 51.3%** |
| 5 | §8 | *"S-1 needs no infrastructure, no tooling and no data"* — **false** |
| 6 | §S-2 | *"Reserve the reproducibility batch before S-2 starts"* — **too late**; it must precede **S-1** (S-1.2). Now `S-0` |
| 7 | §S-2 | *"Forty rows out of 208 is affordable"* — the live pool is **36 + 121 = 157**, in two pools with different meanings |
| 8 | §4.1 | *"Gold-A adjudication scope = 208 rows"* — measured against the **interim** label space, not production's |
| ~~9~~ | — | **Dropped.** An erratum claiming *"the 29.6% denominator is 703, not 1028"* was filed against §4.1. Tracing it found **no artifact that ever stated 208/1028**: the audit's §E.1 and D-3 both already record **208/703**, as did rev 1 and rev 2. The erroneous "correction" originated in agent working notes, not in this proposal. `[owner ruling 2026-09-04]` **The absence of #9 is deliberate; the numbering is preserved so this note is findable.** The historical records that carry the claim are **preserved unamended** |
| 10 | §4.1 / §S-4 / §7 | **"208" and "~188" were dead as a work scope.** J-1 as originally defined is **36** production-relevant rows; the **distinct contested pool is 133**. **208 survives only as a measurement** in the legacy→interim frame (§2, §S-1), where it is correct and is now labelled as such |
| 11 | §S-3 / §4.1 | *"File-level datasheets (8 files, 0 exist)"* — **wrong on both counts.** The governed set is audit **Group A's 10 (+B1)**, and **one datasheet already exists** (`datasheets/vn_input_fixtures.md`) — though it covers a fixture set, not a corpus, so **0 governed corpora have one** |
| 12 | §S-3 / §4.1 | *"~5 new columns (7 → ~12)"* — understated. The figure is **~8 new fields, 7 → ~15** — `[inference]`, a design estimate, **not** `[measured]`; only the **7** is measured, from the CSV headers |
