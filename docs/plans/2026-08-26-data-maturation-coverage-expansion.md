# Data Maturation & Coverage Expansion — Proposal

**Revision 2 — 2026-08-27. Status: `draft` — awaiting *authorization*. Nothing here is approved,
scheduled, or committed work.**

> **What changed at rev 2, in one line:** rev 1 was waiting on decisions; it is now waiting on a
> go-ahead. The owner reviewed it and ruled on **Q-1 … Q-5**, the five questions rev 1 declined to
> answer with invented figures. Full list of changes: **§9**.
>
> Commissioned by the owner ruling of 2026-08-26
> ([`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) §19);
> revised against the owner review outcome of 2026-08-27
> ([`2026-08-27-data-maturation-owner-decision-outcomes.md`](2026-08-27-data-maturation-owner-decision-outcomes.md)).
> Evidence base: [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md)
> (Phase 0 audit) and [`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md).
>
> **The ratified decisions are inputs, not topics.** P-1 … P-3, DFD-1 … DFD-9b and Q-1 … Q-5 are cited
> here and **not reopened**. Where this proposal appears to raise one again, it is deriving a
> consequence, and the ruling wins. **Where the 2026-08-26 and 2026-08-27 wordings differ, the later
> one governs** — one such difference is material and is marked in §9.

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


    S-T  Telemetry readiness            (DFD-9b)  -- runs alongside, gated separately
         DifficultyLabelLogs / StudyTimeOutcomeLogs
```

**S-T is drawn outside the chain deliberately.** The 2026-08-27 ruling separates *telemetry* from
*real-data collection* as distinct concerns, and they behave differently: telemetry accrues passively
from ordinary use and can start now, while its gates (retention, dataset contract, volume) are not the
study-consent gates that govern S-5's Track A. It feeds S-5's Track B; it is not the same work.

**Three properties of this staging are worth stating before the detail:**

1. **S-1 → S-2 → S-3 is a genuine dependency chain, not a preference.** Each stage's output is the
   other's input: you cannot write class definitions before deciding whether the classes are right,
   cannot label without definitions, cannot trust a label without knowing who applied which guideline.
2. **S-3 is cheap now and impossible later.** Provenance is captured at creation time (DFD-5) or not
   at all — the repository's own 136 untraceable rows are the proof, and no forensic pass has
   recovered them.
3. **Only S-1 and S-2 are on the critical path for everything else.** The S-T telemetry strand and
   S-5's in-app accrual half can run in parallel from day one, because they collect *outcomes*, not
   labels. The DFD-9a instrumentation defect that unblocked them **shipped on 2026-08-26** and is no
   longer pending work for this proposal.

**What this proposal still deliberately does not do:** propose a row-count target, recommend a dataset
for ingestion, or estimate the owner's calendar. Rev 1 filed those as **Q-1 … Q-5**; the owner has now
ruled on all five, and **four of the rulings are instructions not to invent the number yet** — measure
adjudication effort rather than guess it (Q-1), do not assume collection scale (Q-2), do not set class
quotas before observing a real distribution (Q-4), do not set maturity thresholds before evidence
exists (Q-5). §4 records each ruling **and the parameter it deliberately leaves open**.

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

**Added by the owner review of 2026-08-27** — the five questions rev 1 left open:

| Input | Source | Consequence for this proposal |
|---|---|---|
| Adjudication effort is **measured, not estimated** | Q-1 | A defined protocol, gated on S-2 `v1`: 20 held-back rows, timed, ambiguous cases and guideline gaps recorded. Scoped in S-2 |
| The owner **has** access to a bounded participant network | Q-2 | S-5 Track A is feasible. **Scale and throughput remain unassumed** — and the sample is a convenience sample, which bounds what Gold-R may claim |
| Bounded collection happens **outside the production app** | Q-3 | Track A is a standalone exercise carrying its own consent and collection metadata. It does **not** settle Track B's consent basis |
| Gold-R sampling is **hybrid**: a floor for all five classes, otherwise the observed distribution | Q-4 | No forced 20/20/20/20/20. Coverage gaps may be targeted **by collecting**, never by generating (S-7) |
| Dataset maturity is **tiered**, not one gate | Q-5 | §5 is a tier ladder. Hard invariants are gates; quality thresholds wait for evidence |
| Instrument use of a licensed corpus is a **separate owner review** | DFD-7 (2026-08-27 wording) | ViLexNorm's `CC BY-NC-SA 4.0` question (OD-4) now has a route, not just a blocker |
| No confidence threshold moves as part of DFD-9a | DFD-9a (2026-08-27 wording) | Complied with — the shipped fix moved no threshold. F-1 stays deferred |

---

## 2. The starting position, measured

Everything in this table is `[measured]` from repository bytes by the Phase 0 audit (2026-08-25). It is
the baseline any maturity claim will be measured against. **One row has moved since rev 1** — the
telemetry row, because the DFD-9a fix shipped on 2026-08-26; it is marked below and the superseded
wording is kept in §9.

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
| Real telemetry available **(updated 2026-08-26)** | `DifficultyLabelLogs` — real human judgements, small, **never read**. `StudyTimeOutcomeLogs` — real outcomes; `PredictedMinutes` / `Confidence` were written `null` on every row, and **the DFD-9a fix shipped 2026-08-26**, so rows written after it carry both on both branches. **Three caveats stand:** rows written *before* the fix are permanently unusable for calibration — the values are not reconstructible; the **end-to-end check has not been run** (automated tests cover the three hops, not the production DI wiring); and the ≥50-row retrain gate has still never been met |

---

## 3. The stages

Each stage states its purpose, what must be true to start, what makes it done, and — where it
matters — what would make it fail silently.

**S-T is a strand, not a stage.** It sits between S-5 and S-6 for reading order only; it is outside the
dependency chain and can start at any time. See §0.

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
| **Cause of the disagreement itself** | The 208 rows, read as a set | **Is the 29.6% caused by taxonomy semantics, or by annotation inconsistency?** |

**The fourth item is the one that decides whether S-2 can succeed**, and it is new at rev 2 — the
2026-08-27 wording of P-3 adds it explicitly. It is not a fifth question about the taxonomy; it is a
question about the *other three answers*, and it has a fork:

- **Annotation inconsistency** — two passes applied different unwritten rules to a boundary that is
  genuinely well-defined. Then **S-2 is the fix**, and writing the boundary down closes the 29.6%.
- **Taxonomy semantics** — the classes themselves do not partition the input space cleanly, so no
  guideline can make two annotators agree. Then **S-2 cannot fix it alone**, and P-3's escape hatch
  applies: an explicit owner decision to change the taxonomy.

`[inference]` The evidence points both ways and that is why it must be checked rather than assumed:
`OnTap→NhacNho` (105) is a *retired-class* transition, which is a taxonomy fact; `ThiCuoiKy→BaiTap`
(26) is two live classes competing for the same row, which reads as a boundary that was never written
down. **If S-2 is written on the assumption that every disagreement is inconsistency, the taxonomic
share of the 29.6% survives the spec and reappears in Gold-A** — where it will look like adjudication
noise.

**Exit criteria.** A written ruling per item: *keep / redefine / retire*, with the boundary stated in
words that an annotator can apply to a row — **plus a stated finding on the fourth item**, because S-2's
scope depends on it. **Ambiguity resolved here is ambiguity S-2 does not have to catalogue.**

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

#### Two 20-row exercises, and they are not the same 20 rows

Rev 1's exit test and the Q-1 measurement ratified on 2026-08-27 both say *"20 held-back rows"*. They
measure different things and must not be collapsed:

| | **S-2 reproducibility test** | **Q-1 effort measurement** |
|---|---|---|
| Question | Does the spec make a label reproducible by someone other than its author? | How long does one adjudication actually take under the spec? |
| Shape | **Two** passes, compared | **One** pass, timed |
| Row requirement | Rows **withheld from spec authoring** — the spec must not have been written while looking at them | Rows drawn from the **real J-1 backlog** (the 208), so the timing generalizes to the work it is estimating |
| Output | An agreement rate against a pre-registered threshold | Total time, per-row time, ambiguous cases, **and guideline gaps discovered** |
| Fails how | Agreement below threshold ⇒ the spec is not finished | It does not fail; it produces a number. Discovering many guideline gaps sends work back to S-2 |

**Use two disjoint batches of 20.** Reusing one batch would time an adjudication on rows the adjudicator
had already ruled on once — which measures recall, not adjudication. Forty rows out of 208 is
affordable, and the Q-1 batch is not wasted: it is the first 20 rows of J-1, done for real.

**Q-1 harvests more than a stopwatch.** The ruling asks for ambiguous cases and guideline gaps as
outputs alongside the timing. That makes it a **pilot of S-4, and a second test of S-2** — if the first
20 real adjudications surface gaps the spec does not cover, the spec returns for a `v2` before the
remaining 188 are attempted. Running it as a pure stopwatch would throw away its more valuable half.

#### `R-1` — who performs the two independent passes *(open, and it changes what the number means)*

DFD-8 makes the owner the sole Gold authority, which constrains this but does not settle it. **The three
available shapes measure three different things**, and the pre-registered threshold is not
transferable between them:

| Shape | What the agreement rate measures | Standing under DFD-8 |
|---|---|---|
| **Owner twice**, separated in time | Intra-annotator consistency — a floor, not spec clarity. Memory of the first pass inflates it | Clean; both passes are the Gold authority |
| **Owner + a second human** from the Q-2 network | True inter-annotator reliability — the strongest evidence that the spec transfers to another reader | Clean **if** the second pass is a probe and the owner's pass is the label |
| **Owner + AI pre-labeller** | Whether the spec is explicit enough for a reader with no project context | **Permitted** — the AI pass is an *instrument* here, not a label source; its output is compared, never adopted. DFD-8 bars AI from *closing* a Gold label, not from being measured against one |

`[inference]` The second shape is the strongest and Q-2 has just made it available — the participant
network that exists for Track A could supply one reader for one hour. **This proposal does not choose;
it flags that choosing changes the threshold**, and a threshold pre-registered for the wrong shape is a
check that cannot fail honestly.

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
set of rows — not an open-ended labelling programme. **How long it takes is now a scheduled
measurement, not a guess** — Q-1, ratified 2026-08-27: the first 20 of these rows are adjudicated under
the S-2 spec and timed, and the remaining ~188 are estimated from that (§4.2, and S-2 for the protocol).

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

**Ratified 2026-08-27 (Q-2, Q-3):** the owner has access to a bounded network of friends, students and
other users, and the exercise runs **outside the production application** as a standalone collection
event carrying its own consent and collection metadata. Track A is therefore *feasible* — which is a
change from rev 1, where its feasibility was itself unknown. **Scale and throughput are still not
assumed.**

A controlled exercise with explicit consent and collection records, designed for **deliberate class
coverage** — because the two classes at zero evaluation coverage will not fix themselves by waiting.

Its measured target is unusually clear:

- `KiemTraThuongXuyen` and `ThiCuoiKy` need real rows because they currently have **zero**;
- `ThiGiuaKy` is over-weighted in evaluation (48.3%) relative to training (12.2%), so a real
  distribution is needed to know whether that skew is an artifact of the quota or a fact about students;
- the abbreviation vocabulary (`tgk` at 28/205 evaluation vs 0/698 training) is the clearest hypothesis
  the project holds about real input, and **it has never been tested against a real speaker**.

**Sampling shape, ratified 2026-08-27 (Q-4) — hybrid:** a **minimum floor for all five classes**, and
beyond the floor the **naturally observed distribution**, not a forced 20/20/20/20/20. Important
linguistic coverage gaps may be targeted where justified. **The floor number is not set here** — Q-4
rules that quotas must not be invented before a real distribution has been observed, which means the
first collection round's job is partly to *reveal* the distribution the later rounds sample against.

> **The floor and the observed distribution do different jobs, and both are needed.** The floor is a
> guarantee that no class is invisible in evaluation — it exists because `KiemTraThuongXuyen` and
> `ThiCuoiKy` are at zero today and a purely natural sample could leave them there. The observed
> distribution is what makes any prevalence claim honest. Reporting them as one number would hide which
> is which: **evaluation coverage and distributional fidelity must be reported separately**, or the
> floor silently becomes a quota and the project has re-created `_balanced.csv`'s 1.11× balancing
> against an unobserved target.

**A bound this track must carry into every claim it supports.** `[inference]` A network of friends,
students and acquaintances is a **convenience sample**, not a random sample of Vietnamese students. That
is a perfectly sound basis for Gold-R — it is real data from real people, which the project has none of
— but it does not license the sentence *"this is how students write."* The honest scope is
**"observed among N recruited participants, recruited through the owner's network."**

This matters more here than it usually would, because Q-4 makes the *observed distribution* the sampling
target. **A convenience sample's observed distribution is the distribution of that convenience sample**,
and treating it as the population distribution would repeat this workstream's founding error one level
up: a number produced by a known process, read downstream as a measurement of the world. The control is
cheap — record the recruitment route in the dataset's datasheet (S-3 requires one anyway) and state
participant count and route wherever a distribution figure appears.

**Sample size, recruitment mechanics and privacy handling remain the owner's** (§4). This proposal does
not design a human-subjects exercise.

#### Track B — ongoing in-app organic accrual

Real usage captured through the application, giving the natural production distribution — the
`[unknown]` that blocks the audit's §C.2 and every prevalence claim in the project.

**This track can start earliest and costs the least**, because the rows accrue from ordinary use rather
than from a recruited event — and it is the **only** source that yields the production distribution
rather than a recruited one. Its readiness gates are DFD-9b's, and they are the subject of the separate
**S-T** strand below.

**Q-3 does not settle this track's consent basis.** It rules on where Track A happens; Track B collects
from real users of a shipped application, which is a different question with a different answer, and it
is still open (§4, Q-3 residual).

**The two tracks never merge into one dataset.** Different consent basis, different provenance,
different sampling — and, now, different claim scope: Track A is a convenience sample of recruited
participants, Track B is the application's actual users. DFD-4 requires separate identity and this
proposal keeps it.

**Exit criteria.** `gold_r_v1`, owner-verified, with consent records, S-3 lineage, **a held-out
partition reserved before any training merge** — the specific error `_merge_seed.py` already made once —
and **a recorded claim scope**: participant count and recruitment route, carried in the datasheet and
repeated wherever a distribution figure from this dataset is cited.

---

### S-T — Telemetry readiness (DFD-9b)

**Purpose.** Make the two telemetry tables *eligible* to become real data. **A separate strand, not a
stage**, because the 2026-08-27 ruling separates telemetry from real-data collection and the two behave
differently: this one accrues passively from ordinary use, needs no recruitment, and can start now.

**It is designation, not consumption.** DFD-9b designates both tables as *potential* future sources and
gates actual use behind provenance, privacy/consent, retention/handling, dataset contracts, and
sufficient quantity/quality. S-T works the gates; **it does not read the tables as training or
evaluation data**, and reaching the end of S-T does not authorise doing so.

| Source | What it holds | Standing today |
|---|---|---|
| `DifficultyLabelLogs` | **Real human judgements** — the nearest thing to real labelled data the project owns | Already captured. **Never read by anything.** Small |
| `StudyTimeOutcomeLogs` | Real outcomes, with the model's prediction and confidence alongside | `PredictedMinutes` / `Confidence` were `null` on every row until the [DFD-9a fix](2026-08-26-prediction-instrumentation-defect.md) shipped **2026-08-26**. Rows written before it stay unusable — the values are not reconstructible. **The end-to-end check is still open** |

**Why this strand is cheap and time-sensitive at once.** Every day the application runs, it either
accrues rows that will be usable or rows that will not, and the difference was decided at write time —
the same S-3 principle that left 136 rows untraceable. `DifficultyLabelLogs` is the sharper case: it has
been capturing real human judgements for months and **not one of them has ever been read**.

**Gates, in dependency order:** provenance at write time (S-3's row-level lineage, applied to both
tables) → a consent basis for collecting from users of a shipped application (**open** — Q-3 rules on
Track A's venue, not this) → retention and handling rules → a dataset contract stating what a row means
and what it may be used for → sufficient volume.

**Exit criteria.** Both tables write complete lineage; a written consent, retention and handling policy
exists; a dataset contract exists for each table; and **a `[measured]` row count**, so "sufficient
quantity" becomes a number rather than an impression. **Consumption remains a separate owner decision.**

**Silent-failure mode.** Declaring the strand done because the columns now populate. Instrumentation is
one of five gates, and it is the only one that is engineering work — which makes it the one most likely
to be mistaken for the whole.

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
| **ViLexNorm** — >10,000 human-annotated normalization pairs, EACL 2024 | Highest value, and **as an instrument, not as data** — it addresses the teencode/abbreviation phenomena the audit could not measure. **`CC BY-NC-SA 4.0`: unresolved (OD-4)** — `NC` blocks any commercial distribution of this application. The 2026-08-27 ruling gives it a **route**: instrument use may be considered separately after an owner licensing review. That is a decision to schedule, not a blocker to work around |
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
> **Q-4 permits targeting coverage gaps. It permits targeting them by *collecting*, and this stage does
> not inherit that permission.** The two verbs differ in the only way that matters here: collecting
> against a gap goes and *looks* at what real people write, so a wrong guess about the gap is corrected
> by the evidence it gathers; generating against a gap *asserts* what they write, so a wrong guess is
> amplified into training data and becomes indistinguishable from a finding. Q-4 governs S-5. **The
> trap above still governs S-7 in full**, and the gap that justifies a collection round does not, by
> itself, justify a generation round.
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

## 4. Quantification, per §19 — measured, ruled, and still open

§19 requires nine quantifications before implementation is proposed. **Four were measurable from the
repository** (§4.1) and are unchanged. **Five were owner-only**; rev 1 filed them as Q-1 … Q-5 rather
than inventing figures, and the owner ruled on all five on 2026-08-27 (§4.2).

**Four of the five rulings are instructions not to invent the number yet** — and that is the substance
of them, not a deferral. Q-1 replaces an estimate with a measurement; Q-2 grants access while
withholding scale; Q-4 forbids quotas until a distribution is observed; Q-5 forbids thresholds until
evidence exists. §4.2 therefore records, for each, **both the ruling and the parameter it deliberately
leaves open** — because an open parameter that nobody wrote down is how a placeholder becomes a fact.

### 4.1 Measured from repository evidence

| §19 item | Answer | Basis |
|---|---|---|
| **Gold-A adjudication scope** | **208 rows** to adjudicate (J-1) + **≤136** to dispose of (J-3), plus an `[unknown]` residue of contested rows (J-7). A **bounded, enumerated** set — not an open programme | Audit §E.1, §E.5, §J `[measured]` |
| **Provenance implementation cost** | **~5 new columns** on the labelled-corpus schema (7 → ~12); **8 file-level datasheets** where 0 exist; provenance-at-write on **2 telemetry tables**. Model-path risk is low — the shipped classifier reads **one** of seven columns | Audit §B.1, §E.2 + CSV headers read this session `[measured]` |
| **Public-dataset evaluation candidates** | **3 checked, 0 approved.** ViLexNorm (`CC BY-NC-SA 4.0`, instrument-only, OD-4 unresolved); UIT-VSFC (16,175 rows, no licence field); PhoATIS (no licence, maximal domain distance). No public corpus carries this label space | Audit §I.2 — three dataset cards read at source `[fact]` |
| **Controlled synthetic-generation opportunities** | Nameable but **not yet actionable**: 2 classes at zero evaluation coverage, `ThiGiuaKy`'s 12.2%-vs-48.3% skew, an abbreviation vocabulary absent from training. All are gaps **in the authored corpus**; whether any is a gap in real behaviour is `[unknown]` until S-5. Generation targets cannot be set before then without inventing the target | Audit §C.1, §D.3, §G `[measured]` + the DFD-6 boundary |

### 4.2 Ruled by the owner, 2026-08-27 — and what each ruling leaves open

| # | §19 item | Ruling | **Residual open parameter** |
|---|---|---|---|
| **Q-1** | **Estimated owner effort** | **Agreed measurement.** After S-2 `v1`: adjudicate 20 held-back rows; measure total time and per-row time; record ambiguous cases and guideline gaps discovered. Estimate Gold-A workload from the result rather than guessing | **Per-row adjudication time — until the measurement runs.** No schedule may be quoted before it. Protocol and its separation from S-2's reproducibility test: S-2 |
| **Q-2** | **Data-collection throughput** | **B.** The owner can access a small network of friends, students and other users for a bounded collection exercise | **Scale and throughput — explicitly not assumed.** Also unstated, and load-bearing for claim scope: **the recruitment route**, which makes this a convenience sample (S-5 Track A) |
| **Q-3** | **Privacy / consent implications** | **C.** Bounded collection runs as a separate exercise **outside the production app**, handling consent and collection metadata itself. In-app organic accrual stays a separate track | **Track B's consent basis.** Q-3 rules on Track A's venue; collecting from users of a shipped application is a different question, and it gates S-T |
| **Q-4** | **Gold-R collection strategy** | **C — hybrid.** Minimum floor for all five classes; no forced 20/20/20/20/20; otherwise prefer the naturally observed distribution; target important linguistic coverage gaps where justified. **Do not invent quotas before observing the real distribution** | **The floor number**, deliberately unset — and the stopping rule. Both wait on a first observation (S-5 Track A) |
| **Q-5** | **Success criteria for dataset maturity** | **B — tiered.** Progressive tiers (unmanaged → governed → evaluation-ready → model-development-ready), **without arbitrary thresholds before evidence exists**. Hard invariants stay distinct from quality thresholds | **Every quality threshold in §5's tier ladder.** The invariants are set and binary; the thresholds are not, and §5 marks which is which |
| **R-1** | *(not a §19 item — surfaced by the rulings)* | — | **Who performs S-2's two independent passes**, which determines what its agreement rate measures and therefore what threshold can honestly be pre-registered (S-2) |

`[inference]` **Q-1 remains the keystone, and it is now a scheduled measurement rather than a hope.**
Three of the four remaining residuals are calendar or volume questions that collapse into arithmetic
once one number exists: how long the owner takes to adjudicate one row under the S-2 spec. It is
measurable in an afternoon. **The fourth — Track B's consent basis — does not collapse**, because it is
a policy question and no amount of measurement answers it.

---

## 5. Dataset maturity — a tiered model (Q-5)

Ratified 2026-08-27: maturity is **progressive tiers, not one all-or-nothing gate**, defined **without
inventing thresholds before evidence exists**, and with **hard invariants kept distinct from quality
thresholds**. §5 is rebuilt to that shape. **Rev 1's eight criteria all survive and none were dropped** —
what changed is that they are now sorted into two kinds that behave differently, and three of them keep
their meaning under a new ID: `M-2`/`M-5`/`M-7` are the owner's three invariants and become
`I-1`/`I-2`/`I-3`. The remaining five keep their numbers (`M-1`, `M-3`, `M-4`, `M-6`, `M-8`).

### 5.1 The two kinds, and why the distinction is load-bearing

| | **Invariants** (`I-*`) | **Quality measures** (`M-*`) |
|---|---|---|
| Shape | Binary. True or false | A number against a threshold |
| Threshold | **None needed** — the condition *is* the criterion | Owner-set, **and not yet settable** (Q-5) |
| Cost | Discipline only | Real work |
| When violated | The dataset is **not at that tier**, whatever else is true | The dataset is at that tier and **not yet good enough** |
| Direction | **Continuous** — a later violation demotes | Achieved, then maintained |

**The owner named the three invariants:** provenance completeness, held-out reservation, Gold/Silver
separation. They are exactly M-2, M-5 and M-7 from rev 1, and rev 1 had already observed that two of
them are *binary and free*. The ruling extends that to all three and gives it teeth: **an invariant is
not a criterion the dataset can score badly on and proceed anyway.**

| ID | Invariant | Check | Today |
|---|---|---|---|
| **I-1** *(was M-2)* | **Provenance completeness** | Every row added after S-3 carries all 7 DFD-5 properties. Enforced by the ingest path, not by discipline | **Not enforceable** — the path does not exist. 15.1% of the corpus is untraceable |
| **I-2** *(was M-5)* | **Held-out reservation** | The held-out partition was reserved **before** training merge, and the reservation is recorded | **Never done.** `_merge_seed.py` made this exact error once |
| **I-3** *(was M-7)* | **Gold/Silver separation** | No synthetic row and no `collected_v4`-derived row appears in Gold-R or in any held-out evaluation set. Automated | **Failed once already** — the shipped model trained on all 903 rows |

**All three are currently false, and all three are free.** That is the single most useful thing this
section says: the project is at the bottom tier for reasons that cost nothing but sequence.

### 5.2 The tier ladder

Each tier's entry conditions are stated so they can come back negative. **No tier is defined by a number
this proposal invented** — where a threshold is needed, it is named as owner-set and the evidence that
would inform it is identified.

| Tier | Name | Entry conditions | Threshold needed? |
|---|---|---|---|
| **T-0** | **Unmanaged** | *Where the project is today.* No annotation spec; provenance absent on 15.1% of rows; no held-out real data; two label passes disagreeing at 29.6% with no adjudication record | — |
| **T-1** | **Governed** | S-1 finding written · S-2 `v1` exists and **passed its reproducibility test** · S-3 shipped, so **I-1** holds for every new row · **I-3** holds · existing rows carry their *known* provenance, including `provenance = unknown` for the 136 | **No.** Every condition is an existence or a binary. T-1 is reachable **without a single new row** |
| **T-2** | **Evaluation-ready** | T-1 held · `gold_r_v1` exists with a held-out partition satisfying **I-2** · that partition is non-empty (**M-3**) and covers **all five classes at ≥ the Q-4 floor** (**M-4**) · claim scope recorded (participant count + recruitment route) · **I-1/I-3 still hold** | **One** — the Q-4 floor. Deliberately unset: it waits on the first observed distribution |
| **T-3** | **Model-development-ready** | T-2 held · **M-1** label reproducibility measured under the spec and accepted · **M-6** real class distribution observed rather than assumed · **M-8** confidence calibration computable from real telemetry · volume sufficient for the intended model work | **Yes, several** — and they are the ones Q-5 forbids setting now. T-2's evidence is what makes them settable |

**Two properties of this ladder worth stating explicitly:**

1. **T-1 is reachable with zero new data.** It is entirely governance: a finding, a spec, a schema and
   an ingest path. The project's instinct — that maturity requires collecting — is wrong about the
   first tier, and the first tier is the one blocking every other.
2. **The thresholds live only in T-3, and T-2 produces the evidence for them.** That is why Q-5's
   *"without inventing arbitrary thresholds before evidence exists"* is satisfiable rather than
   paralysing: the ladder is built so that the tier which needs numbers comes *after* the tier that
   generates them.

### 5.3 The quality measures, with baselines and blank thresholds

Thresholds stay blank by ruling, not by omission. **Each is written so it can come back negative** — a
maturity definition nothing can fail is a definition of nothing — and each states the baseline it must
beat, so a later threshold cannot be quietly set below the current state.

| # | Measure | Measurement | Baseline to beat | Threshold |
|---|---|---|---|---|
| **M-1** | **Label reproducibility** | Two independent passes over held-back rows under the S-2 spec; report agreement | **29.6% disagreement** | owner, at **T-3** — and it depends on `R-1`, since the three pass-shapes are not comparable |
| **M-3** | **Real evaluation existence** | Count of Gold-R held-out rows never seen in training | **0** | owner — **non-zero is itself a step change** |
| **M-4** | **Class coverage in real evaluation** | Classes with ≥ *floor* real held-out rows | **0 of 5** (3 of 5 even counting authored rows) | the Q-4 floor, at **T-2** |
| **M-6** | **Distributional grounding** | Real class distribution observed rather than assumed — **reported with its claim scope**, since Track A's sample is a convenience sample | `[unknown]`; the corpus was balanced to 1.11× against an unobserved distribution | owner, at **T-3** |
| **M-8** | **Instrument calibration measurable** | Confidence bins computable from real telemetry | **Was impossible** — `Confidence` was `null` on every row. **The DFD-9a fix shipped 2026-08-26**; new rows carry it, pre-fix rows never will, and the end-to-end check is still open | binary — and **not yet satisfied**: the columns populate, the volume does not exist |

`[inference]` **M-8 is the one most likely to be mis-scored**, and rev 1 would have mis-scored it
within a day of being written. Its blocker moved from *impossible* to *pending volume* when the fix
shipped, which reads like progress and is — but "the column now has values" is not "calibration is
measurable", and the ≥50-row gate has still never been met. **The fix removed the reason it was
impossible; it did not make it true.**

## 6. What this proposal is not

- **Not authorization.** Nothing here is scheduled or approved. Stage-by-stage go/no-go is the owner's.
  The 2026-08-27 outcomes document says so twice — *"does not authorize implementation"* and *"do not
  start implementation from this document alone"* — and rev 2 changes what this proposal is **waiting
  for**, not what it is permitted to do.
- **Not "generate more data".** The ruling forbids that framing and the evidence does not support it:
  the binding constraint is label authority, not row count.
- **Not a reopening.** P-1…P-3 and DFD-1…DFD-9 are inputs. §18 keeps the Edge AI initiative stopped.
- **Not the DFD-9a defect.** Raised separately and deliberately, because its delay cost is irreversible
  while this proposal's is not. It **shipped 2026-08-26** and moved no confidence threshold, as the
  ruling required; its remaining end-to-end gate is tracked on the defect record, not here.
- **Not a set of thresholds.** §5's tier ladder deliberately carries blanks. Q-5 forbids inventing them
  before evidence exists, and the ladder is arranged so the tier needing numbers comes after the tier
  producing them.
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
| **A maturity criterion that cannot fail** | The `M-*` measures could each be written to always pass | A baseline is stated for each, so a threshold cannot be quietly set below the current state. `I-1`/`I-2`/`I-3` are binary, and **all three are false today** |
| **A convenience sample read as the population** | Q-2's network is friends, students and acquaintances; Q-4 makes the *observed distribution* the sampling target. The distribution observed is that sample's, and it is the most natural over-claim available once real data finally exists | Claim scope recorded in the datasheet and repeated at every citation: *"observed among N recruited participants."* It is an S-5 exit criterion, and M-6 carries it |
| **A tier declared reached on its visible half** | `S-T` is the sharp case: instrumentation is the only one of its five gates that is engineering work, so it is the one that looks like completion | Every tier's entry conditions are enumerated, and `I-1`/`I-2`/`I-3` are continuous — a later violation demotes rather than being grandfathered |

## 8. Immediate next step

**S-1 — the limited taxonomy review.** It needs no infrastructure, no tooling and no data: only the
owner's rulings on the four bounded questions in §3's S-1 table, one of which (*is the 29.6% caused by
taxonomy semantics or annotation inconsistency?*) determines whether S-2 can succeed at all.

Then, in order:

1. **S-2** — write the annotation spec, and pass its reproducibility test. **`R-1` must be decided
   before the threshold is pre-registered**, because the three pass-shapes are not comparable.
2. **Q-1** — adjudicate 20 rows from the real J-1 backlog, timed, recording ambiguous cases and
   guideline gaps. This is the first measurement, and it is what makes a schedule quotable.
3. Everything else waits on those two, except **S-T**, which can start now.

**Nothing above is authorized.** This is the order the rulings imply, not a plan in motion.

---

## 9. Revision record

### 9.1 Rev 2 — 2026-08-27

Revised **in place** rather than by appended amendment: this is a `draft` that was never ratified, and
the owner commissioned *"the next proposal revision."* The convention for correcting a **dated** record
by amendment applies to artifacts whose job is to say what was true when written; a live proposal's job
is to be current. Rev 1's superseded statements are recorded here rather than left in the body.

| # | Change | Driven by |
|---|---|---|
| 1 | Banner: `awaiting owner review` -> **`awaiting authorization`** | The review happened |
| 2 | §1 gains the seven 2026-08-27 inputs (Q-1…Q-5, DFD-7 instrument route, DFD-9a no-threshold-change) | Requirement 1 |
| 3 | **S-1 gains a fourth scope item** — *is the disagreement taxonomy-semantic or annotation inconsistency?* | The 2026-08-27 wording of **P-3**, which adds it. **Material difference from 2026-08-26** |
| 4 | S-2 separates its reproducibility test from the Q-1 measurement — two disjoint batches of 20 — and opens **`R-1`** | Requirement 3 |
| 5 | S-5 Track A: feasibility ratified (Q-2), venue ratified (Q-3), hybrid sampling ratified (Q-4), **convenience-sample claim scope** added as an exit criterion | Requirements 1, 5 |
| 6 | **New strand `S-T`** — telemetry readiness, pulled out of S-5 Track B. Stage numbering unchanged | Requirement 5 (*separate … telemetry …*) |
| 7 | S-7 states that Q-4's permission to target gaps covers **collecting**, not **generating** | Requirement 5, and the trap already in S-7 |
| 8 | §4.2 rewritten: each Q carries its ruling **and its residual open parameter** | Requirement 2 |
| 9 | **§5 rebuilt as a tier ladder** — `T-0…T-3`, with `I-1/I-2/I-3` invariants split from `M-*` thresholds | Requirement 4 (Q-5) |
| 10 | §7 gains three risks: convenience sample, a tier declared on its visible half, and the reworded criterion risk | Consequences of the above |
| 11 | §8 reordered to lead with S-1; Q-1 correctly gated behind S-2 `v1` | Requirement 3 |

### 9.2 Statements rev 1 made that rev 2 supersedes

Kept here because rev 1's §2 was presented as `[measured]`, and one of its rows stopped being true the
same afternoon it was written.

| Rev 1 said | Superseded by | Why |
|---|---|---|
| §2: *"`StudyTimeOutcomeLogs` — real outcomes, but `PredictedMinutes` / `Confidence` written `null`"* | §2's telemetry row, marked *(updated 2026-08-26)* | The DFD-9a fix shipped hours after rev 1 was written. New rows carry both columns on both branches |
| §5 M-8: *"Today: impossible — `Confidence` is `null` (DFD-9a)"* | §5.3 M-8 | Same cause. The blocker moved from *impossible* to *pending volume* — **which is not the same as satisfied** |
| §4.2: Q-1…Q-5 as *"open questions, not estimates"* | §4.2's ruling + residual columns | The owner ruled on all five |
| §5: a flat `M-1…M-8` table with blank thresholds | §5.1–5.3 | Q-5 requires tiers, and invariants distinct from thresholds |
| §0: *"the DFD-9a instrumentation defect can run in parallel from day one"* | §0 property 3 | It did, and it finished |

`[observation]` The M-8 case is the useful one to keep. Rev 1 was accurate when written, went stale the
same afternoon, and would have been carried into rev 2 unnoticed had the reconciliation looked only at
the new ruling. **A document whose baselines are `[measured]` acquires a maintenance obligation that a
document of opinions does not.**

### 9.3 Requirements from the 2026-08-27 outcomes document

Its closing section sets seven requirements for this revision. Tracked so the next reader can check the
work rather than trust it.

| # | Requirement | Where | State |
|---|---|---|---|
| 1 | Incorporate all ratified decisions | §1 (two tables), §4.2 | **Done** |
| 2 | Preserve unresolved implementation parameters as explicit open questions | §4.2 residual column, plus `R-1` | **Done** |
| 3 | Convert Q-1 into a planned measurement after S-2 | S-2, §8 step 2 | **Done** |
| 4 | Define the tiered maturity model without arbitrary thresholds | §5.1–5.3 | **Done** — the only threshold named at T-2 is the Q-4 floor, and it is left blank |
| 5 | Separate governance, real-data collection, evaluation, telemetry, controlled expansion | S-1…S-3 · S-5 · S-6 · **S-T** · S-7 | **Done** — telemetry became its own strand |
| 6 | Keep the Edge AI initiative stopped at S0 | §1, S-8, §6 | **Unchanged from rev 1.** DAT-04 stands |
| 7 | Keep DFD-9a a separate defect | [defect record](2026-08-26-prediction-instrumentation-defect.md); §6 | **Done.** Shipped 2026-08-26, no threshold moved. Its open end-to-end gate is tracked there, not here |
