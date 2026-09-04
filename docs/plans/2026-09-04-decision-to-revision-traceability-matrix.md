# Decision-to-Revision Traceability Matrix — Stage Decisions → Proposal rev 2

> **Filed 2026-09-04.** Maps every ruled item in
> [`2026-09-04-data-maturation-stage-decision-outcomes.md`](2026-09-04-data-maturation-stage-decision-outcomes.md)
> (the **Outcomes record**) onto the section of
> [`2026-08-26-data-maturation-coverage-expansion.md`](2026-08-26-data-maturation-coverage-expansion.md)
> (the **proposal, rev 2**) that it affects.
>
> **This is a map, not a rewrite and not a decision record.** It creates no policy, rules nothing, and
> **infers no owner decision**. Where a mapping is not determinable from ruled text, it is filed as a
> flag for the owner, not resolved here. The proposal is **not rewritten** by this document; rev 3
> remains unwritten and unauthorized.
>
> **Authority.** The Outcomes record is **authoritative**. The
> [working notes](2026-09-04-data-maturation-stage-decision-working-notes.md) are **evidence and
> rationale only** and are cited below only to establish what the record's own text rests on — never to
> add, extend, or reinterpret a ruling. Where the two differ, the Outcomes record governs.
> The **2026-08-27 outcomes** ([P-1…P-3, DFD-1…DFD-9b, Q-1…Q-5](2026-08-27-data-maturation-owner-decision-outcomes.md))
> were read in full, because four mappings below turn on whether a proposal statement is ratified text
> or the proposal author's gloss.
>
> **Three flags were subsequently ruled by the owner on 2026-09-04** — **TF-1, TF-2, TF-8** — after
> this matrix was filed and its provenance trace on erratum #9 was run. Those rulings are **owner
> text**, are **binding on rev 3**, and are collected in
> [§8.0](#80-owner-rulings-on-this-matrix--2026-09-04-after-filing) and marked `RULED` at each flag.
> **Everything else in this document remains agent-authored mapping**, not policy.
>
> **Exempt from `plans/README.md`'s six required sections**, on the same ground the Outcomes record
> claims its own exemption: this is a traceability artifact, not an implementation plan. It has no
> goal, no slices, and no verification gate because it ships nothing.

---

## 1. How to read this

### 1.1 Classification, and the tie-break rule

Primary class answers one mechanical question: **where does this item land in rev 3?** Secondary
markers record a second effect without blurring the primary.

| Class | Lands in rev 3 as | Test |
|---|---|---|
| **Policy** | New policy text | Normative content with **no existing text to correct** |
| **Rewrite Obligation** | Replacement policy text | **Existing text** is superseded, falsified, or made misleading by a ruling |
| **Erratum** | An **errata section**, kept out of the policy body | A statement that was **false when written** — not a consequence of the new rulings |
| **Open Task** | The task register — **not** proposal text | Work item; a ruled policy is not an implemented one |
| **No Change** | Nothing | The proposal text **already conforms**, quoted to prove it |

The Erratum / Rewrite Obligation split is not cosmetic. The Outcomes record's §A preamble requires it
by name: *"§A.3 (historical documentation corrections) is kept separate from §A.4 (normative rewrite
obligations) … the corrections belong in an errata section, not interleaved with policy text."*

### 1.2 Flag severity

| Band | Meaning |
|---|---|
| **`CONTRADICTION`** | Two ruled texts, or a ruled text and a ratified decision, cannot both be executed as written |
| **`MISSING MAPPING`** | A ruling or erratum is real, but the Outcomes record's §A does not name every location it reaches |
| **`NO HOME`** | A new policy requirement with no section in rev 2 to receive it |
| **`DISTINCTION`** | Not a defect. Two things rev 3 must keep apart, which the text currently invites merging |

A flag additionally marked **`RULED`** carries an owner ruling that **binds rev 3**; its band records
what the flag *was*, and the ruling governs what happens. Flags are `TF-*`, listed in §8.

---

## 2. Coverage ledger

The Outcomes record §1 states the arithmetic: **52 numbered stage decisions + 1 S-3 closing
confirmation + 1 scope ruling = 54 ruled items.** All 54 are mapped in §3. Nothing is dropped and
nothing is merged.

| Block | Items | Mapped in |
|---|---|---|
| S-1 Taxonomy review | 7 (S-1.0 … S-1.6) | §3.1 |
| S-2 Annotation spec + reproducibility | 12 (S-2.1 … S-2.12) | §3.2 |
| S-3 Provenance / lineage | 7 (S-3.1 … S-3.7) **+ 1 closing confirmation** | §3.3 |
| S-4 Gold-A | 6 (S-4.1 … S-4.6) | §3.4 |
| S-5 Gold-R | 7 (S-5.1 … S-5.7) | §3.5 |
| S-6 Evaluation foundation | 3 (S-6.1 … S-6.3) | §3.6 |
| S-7 Controlled expansion | 4 (S-7.1 … S-7.4) | §3.7 |
| S-8 Future model work | 1 (S-8.1) | §3.8 |
| **Scope ruling** — S-T runs before consolidation | 1 | §3.9 |
| S-T Telemetry readiness | 5 (S-T.1 … S-T.5) | §3.9 |
| | **54** | |

Non-ruled material is mapped separately and **must not be read as owner policy**: §4 (errata, 12),
§5 (rewrite obligations), §6 (open tasks, 9 + 3 unpriced costs).

---

## 3. Matrix A — the 54 ruled items

### 3.1 S-1 — Taxonomy review

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-1.0** | Production taxonomy is S-1's subject; retirement B reopened as one explicit review question | §3 S-1 *Purpose* + scope table row 1 | Rewrite Obligation | | |
| **S-1.1** | Retirement B confirmed; **reminder-ness relocated, not deleted** — S-1 must say so | §3 S-1 scope table row 1; §3 S-1 *Exit criteria* | Rewrite Obligation | Policy | |
| **S-1.2** | **Reserve first, then sample** — S-2 and Q-1 batches reserved before S-1 reads any row text | §3 S-2 *"Reserve the reproducibility batch before S-2 starts"*; §8 ordered steps | Rewrite Obligation | | [TF-16](#tf-16) · errata #6 |
| **S-1.3** | Third annotation pass is **evidence, not a new question**; 121 rows inform the existing boundary | §3 S-1 *"It is smaller than it reads"* (evidence basis) | Rewrite Obligation | | |
| **S-1.4** | Difficulty defined as explicit **1–5 ordinal scale with written anchors**; consumers **not** audited here | §3 S-1 scope table row 3 (`Difficulty` semantics) | Policy | Rewrite Obligation | errata #3 · [FU-1](#6-matrix-d--open-tasks-outcomes-a5) |
| **S-1.5** | Class-justification question reframed onto product intent; **evaluation defect moves to S-6** | §3 S-1 scope table row 4 → §3 S-6 | Rewrite Obligation | | errata #4 |
| **S-1.6** | **Separate cause ruling per contested boundary**; reservation-before-reading hardened to a binding precondition | §3 S-1 scope table row 5 + `[inference]` fork block; §8 | Rewrite Obligation | Policy | [TF-16](#tf-16) |

### 3.2 S-2 — Annotation specification and reproducibility

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-2.1** | TaskType and Difficulty evaluated **independently**; both pass their own threshold; per-dimension/per-boundary reporting | §3 S-2 *Exit criteria*; §3 S-2 two-exercises table | Policy | Rewrite Obligation | |
| **S-2.2** | **R-1 closed** — owner = Gold pass; one Q-2 human reader = blind probe; AI supplementary only | §3 S-2 `R-1` subsection; §4.2 `R-1` row; §5.3 M-1; §8 step 1 | Rewrite Obligation | | [TF-9](#tf-9) |
| **S-2.3** | **One stratified batch**; level-1/2 Difficulty gap **declared, not solved** | §3 S-2 two-exercises table | Policy | | |
| **S-2.4** | **Full-batch scoring**; composition + denominators pre-registered; **batch composition travels with every quoted figure** | §3 S-2 *Exit criteria* | Policy | | A.1 principle 1 |
| **S-2.5** | **Reserve 40 once**, pre-partitioned 20 scored (12 contested / 8 Difficulty-spread) + 20 clean retest | §3 S-2 *"Use two disjoint batches of 20"* | Rewrite Obligation | | errata #7 |
| **S-2.6** | Q-1 estimates **contested adjudication time only**; J-3/J-7 estimated separately; no invented weighting. *A declared narrowing of ratified Q-1* | §4.2 Q-1 row; §3 S-2 *"Q-1 harvests more than a stopwatch"*; §3 S-4 workload sentence | Rewrite Obligation | Policy | |
| **S-2.7** | **TaskType ≥ 17/20, Difficulty ≥ 18/20, exact match**; within-one diagnostic only; thresholds unchangeable after results | §3 S-2 *Exit criteria* (*"a rate the owner sets in advance"*); §5.3 M-1 | Policy | Rewrite Obligation | [TF-24](#tf-24) |
| **S-2.8** | **"Adjudicate" = assign from the full five-class production taxonomy**, not a two-way choice | §3 S-2 *Required content* (adjudication procedure); §3 S-4 J-1 | Policy | | |
| **S-2.9** | **Boundary-indexed catalogue**, 3–5 examples per boundary; selection rule pre-registered; row IDs retained | §3 S-2 design constraint 1 (ambiguous-example catalogue) | Policy | Rewrite Obligation | |
| **S-2.10** | Keys on **content hash + source file + line**; S-3 introduces a stable surrogate **RowId**; reservation materialises a snapshot | §3 S-3 row-level lineage table (**new field**); §2 row *Row-level lineage fields* | Policy | Rewrite Obligation | errata #12 |
| **S-2.11** | S-2 owns the spec + bump semantics; **S-3 owns row-level `GuidelineVersion`**; example additions that change the rule **require a bump** | §3 S-2 design constraint 2; §3 S-3 lineage table row *annotation-guideline version* | Policy | | [TF-21](#tf-21) |
| **S-2.12** | Semantic contract **+ annotation-record template**; spec must identify **AI-drafted sections** (provenance metadata, not a trust score) | §3 S-2 *Owner authority (DFD-8)* | Policy | Rewrite Obligation | |

### 3.3 S-3 — Provenance and lineage

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-3.1** | Governed corpus lives **outside the application**; `seed_intents.csv` becomes a **generated export**; provenance edits no longer force a retrain | §3 S-3 *Purpose* + whole section; §2 row *Corpus files* | Policy | Rewrite Obligation | |
| **S-3.2** | Backfill provenance by join for the 903; the 136 marked **`untraceable`**; **derived/recorded marker mandatory on export** | §3 S-3 *"A boundary this stage must respect"* (`provenance = unknown`); §5.2 T-1 entry condition | Rewrite Obligation | Policy | [TF-10](#tf-10) · [TF-11](#tf-11) |
| **S-3.3** | Canonical corpus is **JSONL**; CSV retained as a **byte-stable** generated export; **S-3 incomplete until the consumer boundary is resolved and verified** | §3 S-3 whole section; §3 S-3 *Exit criteria* | Policy | Rewrite Obligation | |
| **S-3.4** | Non-production consumers read **canonical directly**; app alone reads the lean export; repoint consumers; **update both hash pins**; no sidecar, no second export | §3 S-3 (**new** consumer-boundary subsection — consumer list `RULED` 2026-09-04: **`build_split.py` · `TextClassifierEval` · downstream research tooling**); §2 row *Corpus files* | Policy | | [TF-2](#tf-2) `RULED` |
| **S-3.5** | Fixed core + nested **`Provenance`** object, required-key set **versioned and mechanically enforced**; **missing/null/invalid rejects ingestion** | §3 S-3 row-level lineage table; §3 S-3 *Exit criteria*; §4.1 provenance-cost row | Policy | Rewrite Obligation | errata #12 · [TF-6](#tf-6) |
| **S-3.6** | File-level datasheets for **canonical corpus + genuine source inputs only**; derived exports carry machine-generated provenance; **A8 named exclusion → FU-2** | §3 S-3 *File-level datasheets*; §4.1 provenance-cost row | Rewrite Obligation | Policy | errata #11 · [TF-7](#tf-7) |
| **S-3.7** | Authoritative fail-closed gate at the **canonical file / repository boundary**, enforced in **CI**; `_merge_seed.py` **retired, not converted** | §3 S-3 *Exit criteria* (*"enforced by the ingest path"*) — rev 3 states the `RULED` merge-scoped guarantee: invalid canonical content never enters **`main`/`dev` history through merge** | Policy | Rewrite Obligation | [TF-1](#tf-1) `RULED` · [TF-2](#tf-2) `RULED` |
| **S-3 closing** | **External build manifest** — canonical hash · export hash · generator identity/version, outside the export bytes; `ComputeSeedHash()` still covers export content only | §3 S-3 (**new**); §2 row *Corpus files* | Policy | | A.1 manifest-as-attestation |

### 3.4 S-4 — Gold-A

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-4.1** | Gold-A = **full contested pool + a designed authored sample**, ratified **at scope level, not a row count**; sample rule approved before selection | §3 S-4 *Scope, measured* table; §4.1 Gold-A adjudication-scope row | Rewrite Obligation | Policy | errata #8 · #10 · [TF-5](#tf-5) |
| **S-4.2** | The 136 **remain eligible for Gold-A after adjudication** — Gold-A certifies label correctness, not source realness; origin-unknown is a **persistent visible qualifier**, **derived not stored** | §1 P-2 row; §3 S-4 J-3 line; §3 S-7 Silver paragraph | Rewrite Obligation | Policy | [TF-4](#tf-4) · [TF-17](#tf-17) |
| **S-4.3** | **Commit-then-reveal** — AI predictions hidden until the owner commits; a post-reveal change is an explicit second review | §3 S-4 *"Where AI legitimately reduces the work"* | Rewrite Obligation | Policy | A.4 |
| **S-4.4** | Undecidable rows **excluded from `gold_a_v1` and logged as guideline gaps**; identity/reason/boundary/guideline version preserved; bump re-review only where the rule changed | §3 S-4 J-7 line; §3 S-4 *Exit criteria* (**new** gap-log artifact) | Rewrite Obligation | Policy | A.4 |
| **S-4.5** | **Targeted rule attribution** — cite a rule only where existing structure does not already answer it | §3 S-4 *Exit criteria* (adjudication record) | Policy | | A.2 item 1 |
| **S-4.6** | `gold_a_v1` is a **pinned manifest** (row identities · corpus hash · guideline version), **fails closed**; row content not duplicated | §3 S-4 *Exit criteria* | Rewrite Obligation | Policy | extended by S-5.6 |

### 3.5 S-5 — Gold-R

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-5.1** | **No row leaves the user's machine without explicit per-participant consent for that transfer**; consent record preserved. **Resolves the Q-3 residual** | §3 S-5 Track B; §4.2 Q-3 residual; §3 S-T consent gate | Policy | Rewrite Obligation | cost, [§6.2](#62-costs-surfaced-but-not-priced) |
| **S-5.2** | Pre-mechanism rows contributable with **derived/backfilled provenance**, capped **below the top tier**, **ineligible for the S-6 holdout**; grades reported explicitly | §5.2 tier ladder (**new hard invariant**); §3 S-6 exclusion list (**third entry**) | Policy | Rewrite Obligation | A.2 item 2 |
| **S-5.3** | **One corpus, one authority**, rows **physically partitioned by role**; partition is an enforcement boundary; **role moves explicit and hash-visible** | §3 S-3; §3 S-5 *Exit criteria*; §3 S-6 held-out rules | Policy | Rewrite Obligation | |
| **S-5.4** | **Gold-A stays in the training partition and export**; S-6 figures need a separately reserved holdout. **§S-6's wording must be corrected** | §3 S-6 *"Gold-A answers is the model consistent with the label definitions"*; §3 S-6 averaging rule | Rewrite Obligation | | A.4 |
| **S-5.5** | Gold-R labels **owner-assigned under the S-2 spec**; user `FinalDoKho`/`WasOverride` kept as **governed metadata, never the Gold label** → **FU-3** | §3 S-5 *Exit criteria*; §3 S-5 Track B | Policy | Rewrite Obligation | cost, [§6.2](#62-costs-surfaced-but-not-priced) |
| **S-5.6** | Manifest **fails closed on a missing/changed pinned row**; unrelated churn must not invalidate it; **every contextual dependency explicitly pinned** | §3 S-4 *Exit criteria*; §3 S-5 *Exit criteria*. **Ruling unchanged**; rev 3 must **not** cite the 703-vs-1028 example as its rationale | Policy | Rewrite Obligation | extends S-4.6 · [TF-8](#tf-8) `RULED` |
| **S-5.7** | **Pre-registered assignment rule** fixed before seeing content; fraction instantiated after volume is known; arrival order and post-hoc balancing excluded; **Q-4 floor is readiness, not allocation**; insufficient volume ⇒ *not yet ready* | §3 S-5 *Exit criteria*; §5.2 T-2 entry conditions; §5.3 M-3/M-4 | Policy | Rewrite Obligation | [TF-25](#tf-25) |

### 3.6 S-6 — Evaluation foundation

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-6.1** | Pre-register the **full metric set + the single headline before Gold-R exists**; chance baseline per metric from the observed marginal; all registered metrics reported as **mandatory diagnostics** | §3 S-6 (**net-new metric registry** — no such section exists) | Policy | | [TF-22](#tf-22) |
| **S-6.2** | Difficulty's headline is **MAE**; exact-match mandatory diagnostic; within-one diagnostic only; chance baseline recomputed from the real marginal | §3 S-6 (**net-new**) | Policy | | A.2 item 3 |
| **S-6.3** | **Interval required on every figure**; interval including the chance baseline ⇒ *"not distinguishable from chance"*; exclusion of chance is a **one-sided guard**, not validation; **no precision threshold before Q-5 permits one** | §3 S-6 (**net-new**); §5.3 threshold column | Policy | | [TF-24](#tf-24) |

### 3.7 S-7 — Controlled expansion

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-7.1** | **No blanket Silver promotability rule** — per-source Gold-A eligibility recorded in the S-3.6 datasheet; `unruled` blocks adjudication. **`collected_v4`'s ruling is required and unruled** | §3 S-7 Silver paragraph (*"never promotable to Gold"* — **superseded**); §1 P-1 row; §1 P-2 row; §3 S-3 datasheet schema | Rewrite Obligation | Policy · Open Task | [TF-4](#tf-4) · [TF-12](#tf-12) |
| **S-7.2** | **Creation of new synthetic batches suspended** for Data Maturation; existing synthetic rows untouched; resumption needs an **observed-data gap AND an explicit warrant**; the ban on synthetic for distributional/coverage claims is **standing** | §3 S-7 Synthetic paragraph; §1 DFD-6 row; §4.1 synthetic-opportunities row; §7 synthetic risk row | Rewrite Obligation | Policy | [TF-13](#tf-13) |
| **S-7.3** | **OD-4 scheduled**; its completion is a **hard gate on any ViLexNorm use**; scheduling authorises nothing. **ViLexNorm does not satisfy S-7.2's resumption condition.** All three public candidates **closed for this stage** | §3 S-7 Public table (all three rows); §1 DFD-7 row; §4.1 public-candidates row | Rewrite Obligation | Policy · Open Task | [TF-3](#tf-3) · [TF-14](#tf-14) |
| **S-7.4** | S-7 stays **governance-only**; **distribution check built and frozen now**, proven to fail on `collected_v4`; specified over **general phenomenon/feature families**, not the seven patterns; changes **versioned, never tuned** | §3 S-7 preamble + *Exit criteria*; §7 synthetic risk control | Rewrite Obligation | Policy · Open Task | A.1 principle 2 |

### 3.8 S-8 — Future model work

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **S-8.1** | Four **necessary-not-sufficient** revival preconditions (real Gold-R holdout · S-6 baseline with intervals · registered pre-result hypothesis with four elements · DAT-04 unchanged); amendment needs a new version and does not retroactively qualify; **no numerical threshold set**; **separate owner decision still mandatory** | §3 S-8 placeholder (**replaced**); §1 ruling-§18 row; §6 third bullet | Rewrite Obligation | Policy | [TF-21](#tf-21) · A.4 suggestion |

### 3.9 Scope ruling and S-T — Telemetry readiness

| # | Ruled, in short | Proposal section affected | Class | Also | Flag |
|---|---|---|---|---|---|
| **Scope ruling** | Run the S-T pass **before consolidation**; S-5.1 does not by itself close S-T's gate; **implementation-dependent details become follow-ups or amendments — a governance decision is never deferred on implementation grounds** | **No section in rev 2 receives this.** Process directive over the rewrite itself | Policy | | [TF-18](#tf-18) |
| **S-T.1** | **Two explicitly independent gates** — local collection/retention/handling (closable now) and egress/transfer (**`N/A` until a mechanism exists, then BLOCKING**); **S-T never reported "closed" on the local gate alone** | §3 S-T gate list; §3 S-T *Exit criteria*; §3 S-T *Silent-failure mode* | Rewrite Obligation | Policy | [TF-15](#tf-15) · [TF-23](#tf-23) |
| **S-T.2** | **Capture-complete write-time rule** — record every meaningful S-3 lineage field; **gate 4 validation mandatory** so a defaulted column cannot masquerade as valid provenance | §3 S-T gate 1; §3 S-T gate list ordering (**gates 1 and 4 mutually dependent**) | Policy | Rewrite Obligation | A.1 principle 5 |
| **S-T.3** | **Authorised: volume + technical usability only** (row counts, capture-date ranges, null/usability on `PredictedMinutes`/`Confidence`). **Prohibited until S-5.7 is pre-registered:** label and feature marginals. Scope recorded alongside every figure. **Authorised, not performed** | §3 S-T *Exit criteria* (`[measured]` row count); §2 telemetry row; §5.3 M-8 | Policy | Open Task | A.2 item 5 |
| **S-T.4** | **Bounded rolling retention** fixed now, **window instantiated only after S-T.3**; verify gate 5 stays attainable; **if no reasonable window does, return to owner decision** | §3 S-T gate 3; §3 S-T *Exit criteria* | Policy | Open Task | [TF-15](#tf-15) · A.1 principle 4 |
| **S-T.5** | **One artifact type — the S-3.6 datasheet**, with **static-source and live-source sections**; static-only fields schema-marked **`N/A`**, never blank; **no "dataset contract" vocabulary** | §3 S-T gate 4 (*"a dataset contract"* — **renamed**); §3 S-3 datasheet schema | Rewrite Obligation | Policy | [TF-19](#tf-19) |

---

## 4. Matrix B — documentation errata (Outcomes §A.3)

**Class for all: Erratum.** These were false when written and belong in an errata section of rev 3,
**not** interleaved with policy. `[measured]` unless the Outcomes record marks otherwise.

**Eleven survive, not twelve.** Erratum **#9 was dropped by owner ruling on 2026-09-04** after its
provenance was traced ([TF-8](#tf-8)). The remaining entries keep their original numbering so inbound
citations resolve; rev 3's errata section simply has no #9.

| # | Location the record names | Correction | Mapping status |
|---|---|---|---|
| 1 | §S-1 table | *"Two of the three largest transitions name retired classes"* — false as placed | **Confirmed** at L170. Audit §J-2 carries the same error — **owner call, see [§9](#9-explicitly-out-of-scope)** |
| 2 | §S-1 `[inference]` prose | *"the fourth item"* / *"the other three answers"* — the table has **five** rows | **Confirmed** at L176–178. Also propagates to **§8** (*"the four bounded questions"*, L695) and **§9.1 change 3** — [TF-26](#tf-26) |
| 3 | §S-1 table | *"Difficulty … is currently never trained on"* — false; ≥4 live consumers | **Confirmed** at L172 |
| 4 | §S-1 table | `KiemTraThuongXuyen`/`ThiCuoiKy` as possibly *"aspirational"* — they are **51.3% of training** | **Confirmed** at L173 |
| 5 | §8 | *"S-1 needs no infrastructure, no tooling and no data"* — false | **Confirmed** at L694. Does **not** cover §8's ordering defect — [TF-16](#tf-16) |
| 6 | §S-2 | *"Reserve the reproducibility batch before S-2 starts"* — too late; must precede **S-1** | **Confirmed** at L248 |
| 7 | §S-2 | *"Forty rows out of 208 is affordable"* — live pool is **36 + 121 = 157**, two pools, different meanings | **Confirmed** at L245 |
| 8 | §4.1 | *"Gold-A adjudication scope = 208 rows"* — measured against the **interim** label space | **Confirmed** at L561 |
| ~~9~~ | ~~§4.1~~ | ~~*"The 29.6% denominator is 703, not 1028"*~~ | **DROPPED — `RULED` (owner, 2026-09-04).** No artifact ever carried the error; it originated in the working notes. **No rev 3 errata entry** — [TF-8](#tf-8) |
| 10 | §4.1 / §4.2 / §S-4 | *"208"* and *"~188"* are dead numbers; J-1 is **36**, the distinct contested pool is **133** | **Under-scoped, and one pointer unsupported** — [TF-5](#tf-5) |
| 11 | §S-3 | *"File-level datasheets (8 files, 0 exist)"* — wrong on both counts | **Under-scoped** — [TF-7](#tf-7) |
| 12 | §S-3 | *"~5 new columns (7 → ~12)"* — really **~8 new fields, 7 → ~15** | **Under-scoped** — [TF-6](#tf-6) |

---

## 5. Matrix C — normative rewrite obligations (Outcomes §A.4)

### 5.1 Superseded or corrected text — **Class: Rewrite Obligation**

| Proposal text | Superseded by | Location |
|---|---|---|
| §S-4 **AI-assist paragraph** — *"reduces the work"* | S-4.3 — saving is ordering and clustering only | L340–342 |
| §S-4 **J-7 line** (`[unknown]` until J-1 runs) | S-4.4 — the **count** stays unknown; the **policy** does not | L333 |
| §S-4 **exit criteria** | S-4.6 / S-5.6 — restated as a manifest spec, **plus the guideline-gap log** as a named exit artifact | L344–345 |
| §S-5 *"This track can start earliest and costs the least"* | S-5.1 — **now false**; Track B carries consent + a mechanism that does not exist | L405 |
| §S-6 *"Gold-A answers is the model consistent with the label definitions"* | S-5.4 — **false** | L473–474 |
| §S-6 **averaging rule** | S-5.4 — survives, but its stated reason is now wrong | L473–475 |
| §S-7 Silver paragraph, synthetic paragraph, public track, gate list, exit criteria | S-7.1 / 7.2 / 7.3 / 7.4 | L491–533 |
| §S-T gate list | S-T.1 (gate 2 splits) · S-T.2 (gates 1 and 4 **mutually dependent**) · S-T.5 (gate 4 renamed) | L450–453 |
| §S-8 placeholder | S-8.1 — four preconditions, hypothesis schema, versioning, no threshold, mandatory owner decision | L539–541 |

### 5.2 Additions to ratified rule lists — **Class: Policy**

| Addition | Source | Target |
|---|---|---|
| §5's tier ladder gains a **hard invariant** (not a threshold): **provenance recorded at creation** | S-5.2 | §5.1 invariants table + §5.2 ladder |
| §S-6's exclusion list gains a **third** entry: **no derived-provenance Gold-R row** | S-5.2 | §3 S-6 structural rules (L472) |
| §4.2's **Q-3 residual can be closed**; §S-T's consent gate recorded in **two-state form** | S-5.1 · S-T.1 | §4.2 Q-3 row; §3 S-T |
| §S-T's silent-failure block gains a **second form**: reporting S-T closed on the local gate alone | S-T.1 | §3 S-T *Silent-failure mode* |

### 5.3 Version-stream hygiene — **Class: Policy** · [TF-21](#tf-21)

Four streams that must never collapse: **`GuidelineVersion`** (S-2.11) · **dataset version** (DFD-5) ·
**provenance-schema required-key-set version** (S-3.5) · **hypothesis version** (S-8.1).
**`LabelVersion` is disqualified** — one value spanning four label origins across 698 rows.

### 5.4 Agent suggestion — **not ruled, and must not be executed as if it were**

The Outcomes record proposes generalising S-8.1's four-element hypothesis schema into a reusable
pre-registration template, and states plainly: *"The owner has **not** ruled on generalising it."*
**Class: no class.** It enters rev 3 only if the owner rules it. Recorded here so it is neither
silently adopted nor silently lost.

### 5.5 Standing principles and the required reader artifact — **Class: Policy** · [TF-19](#tf-19) · [TF-20](#tf-20)

Outcomes §A.1 (five principles, the explicit-`N/A` rule, the rule/number split, manifest-as-attestation)
and §A.2 (the escalated-to-requirement artifact *"how to read a figure from this project"*, six
reader-facing non-uniformities plus two reading rules) are **agent-authored** and are **not owner
policy**. They describe how rev 3 should be organised. Neither has a home section in rev 2.

---

## 6. Matrix D — open tasks (Outcomes §A.5)

### 6.1 The nine tasks — **Class for all: Open Task**

**None is scheduled, and a ruled policy is not an implemented one.** These belong in a task register,
not in the proposal's policy body.

| Task | Source | State as recorded | Proposal section that must reference it |
|---|---|---|---|
| **OD-4 licensing review** (ViLexNorm `NC`/`SA`) | S-7.3 | Scheduled; blocking any ViLexNorm use; **no Data Maturation deadline** | §3 S-7 public table; §4.1 public-candidates row |
| **Telemetry volume/usability measurement** | S-T.3 | **Authorised, not performed** | §3 S-T exit criteria; §5.3 M-8 |
| **Retention window** | S-T.4 | `[unknown]` — instantiate after S-T.3, with the feasibility check | §3 S-T gate 3 |
| **Capture-complete provenance + gate-4 validation**, both telemetry tables | S-T.2 | Not started | §3 S-T gate 1 |
| **Frozen, versioned distribution check** + `collected_v4` proof case | S-7.4 | Not started | §3 S-7 exit criteria |
| **`collected_v4` Gold-A eligibility ruling** | S-7.1 | **Required, unruled** | §3 S-7 Silver; §1 P-1 row |
| **FU-1** — Difficulty consumer consistency check after the anchors freeze | S-1.4 | Not scheduled | §3 S-1 scope table row 3 |
| **FU-2** — governance for A8 / `SeedDataGenerator` (M7 domain) | S-3.6 | Not scheduled | §3 S-3 datasheets |
| **FU-3** — perceived-difficulty analysis (owner spec label vs user `FinalDoKho`) | S-5.5 | Not scheduled | §3 S-5 |

### 6.2 Costs surfaced but not priced — **Class: Open Task**

Named, not absorbed. Each is scope the proposal does not currently contain.

| Cost | Source | Where rev 2 is silent |
|---|---|---|
| **Track B's consent-and-transfer mechanism** — new scope | S-5.1 | §3 S-5 Track B (*"costs the least"* — see [§5.1](#51-superseded-or-corrected-text--class-rewrite-obligation)) |
| **Gold-R labelling effort scales linearly with collection volume**; `[inference]` plausibly the binding constraint on Track A/B volume | S-5.5 | §4 has no cost model row for it |
| **S-7's cost model** loses the synthetic line item, gains the distribution check as an engineering deliverable | S-7.2 · S-7.4 | §3 S-7; §4.1 |

---

## 7. Matrix E — reverse map, and the No-Change ledger

Built section-by-section so a zero-inbound section is visible rather than assumed. This is the view
the rev 3 author works from.

| Proposal section | Inbound ruled items / errata | Weight |
|---|---|---|
| **§0** one page | S-1.2/S-1.6 (property 3, critical path) · S-3.3 (S-T/S-3 completion gate) | Light — [TF-16](#tf-16) |
| **§1** table 1 (P/DFD) | S-4.2 (P-2 row) · S-7.1 (P-1 row) · S-7.2 (DFD-6 row) · S-8.1 (§18 row) | **Heavy, and entirely unnamed by §A** — [TF-4](#tf-4) · [TF-12](#tf-12) |
| **§1** table 2 (Q rows) | S-2.6 (Q-1) · S-5.1 (Q-3) · S-7.3 (DFD-7 row) | [TF-14](#tf-14) |
| **§2** measured baseline | S-2.10 + S-3.5 (lineage-fields row) · S-3.1/S-3.4/S-3 closing (corpus-files row) · S-T.3 (telemetry row) · errata #10 (L132) | Medium — [TF-5](#tf-5) |
| **§3 S-1** | S-1.0 … S-1.6 · errata #1, #2, #3, #4 | Heavy |
| **§3 S-2** | S-2.1 … S-2.12 · S-1.2 · errata #6, #7, #10 | Heavy |
| **§3 S-3** | S-3.1 … S-3.7 + closing · S-2.10 · S-2.11 · S-5.3 · S-T.5 · errata #11, #12 | Heavy — full rewrite |
| **§3 S-4** | S-4.1 … S-4.6 · S-2.6 · S-2.8 · S-5.4 · S-5.6 · errata #8, #10 | Heavy |
| **§3 S-5** | S-5.1 … S-5.7 | Heavy |
| **§3 S-T** | S-T.1 … S-T.5 | Heavy — [TF-15](#tf-15) |
| **§3 S-6** | S-6.1 · S-6.2 · S-6.3 · S-1.5 · S-5.2 · S-5.4 | **Heavy — mostly net-new** ([TF-22](#tf-22)) |
| **§3 S-7** | S-7.1 … S-7.4 | Heavy |
| **§3 S-8** | S-8.1 | Full replacement |
| **§4.1** | S-4.1 · S-3.5 · S-3.6 · S-7.2 · S-7.3 · errata #8, #9, #10, #11, #12 | **Heavy, and under-named** — [TF-5](#tf-5) · [TF-6](#tf-6) · [TF-7](#tf-7) · [TF-8](#tf-8) |
| **§4.2** | S-2.6 (Q-1) · S-5.1 (Q-3, closable) · S-2.2 (R-1, closed) · S-5.7 (Q-4) | [TF-9](#tf-9) |
| **§5.1** invariants | S-5.2 (new hard invariant) | Medium |
| **§5.2** ladder | S-3.2 (T-1 vocabulary) · S-5.2 · S-5.7 (T-2) | [TF-10](#tf-10) |
| **§5.3** measures | S-2.2 + S-2.7 (M-1) · S-5.7 (M-3, M-4) · S-T.3 (M-8) · S-6.3 (threshold column) | [TF-9](#tf-9) · [TF-24](#tf-24) |
| **§6** what this is not | S-8.1 (third bullet) | Light |
| **§7** risks | S-7.2 + S-7.4 (synthetic row) · S-T.1 (visible-half row) · errata #10 (L686) | Medium — [TF-23](#tf-23) |
| **§8** next step | S-1.2 · S-1.6 · S-2.2 · errata #2, #5 | Medium — [TF-16](#tf-16) |
| **§9.1–9.3** revision record | — | **No Change** |

### 7.1 No-Change ledger — each justified by quoted conforming text

A ledger that assigns change to all 54 rows would be unexamined. These are the cases where rev 2
already conforms.

| Location | Conforming text | Ruling it satisfies |
|---|---|---|
| **§5.2 T-2** | *"covers **all five classes at ≥ the Q-4 floor** (**M-4**)"* | **S-5.7 condition (2)** — the floor is used here as a *readiness/coverage* criterion, which is exactly what the ruling says it is. It is **not** used as an allocation rule |
| **§2**, datasheets row | *"File-level datasheets \| **0** for the corpora themselves"* | **Errata #11 does not reach it.** The qualifier *"for the corpora themselves"* holds: working-notes evidence (NEW EVIDENCE 19) classifies `vn_input_fixtures` as *"SEPARATE LINEAGE — fixtures, NOT the corpus"*. §4.1's unqualified restatement is the one that is wrong — [TF-7](#tf-7) |
| **§0**, S-T placement | *"**S-T is drawn outside the chain deliberately** … can start now"* | **S-T.1** — the local gate is closable now, so "can start now" survives. Only the *single*-gate framing downstream needs correction |
| **§6**, third bullet | *"**Not a reopening.** P-1…P-3 and DFD-1…DFD-9 are inputs. §18 keeps the Edge AI initiative stopped"* | **S-8.1** — adds preconditions without disturbing this statement; DAT-04 stands |
| **§9.1 / §9.2 / §9.3** | The rev 2 revision record | Historical record of what rev 2 did. Rev 3 **adds** §9.4; it does not edit these |

---

## 8. Flags

### 8.0 Owner rulings on this matrix — **2026-09-04, after filing**

Three flags were put to the owner and ruled. They are **binding on rev 3** and are marked `RULED`
in place below. The remaining flags are unruled and stand as filed.

| Flag | Ruling |
|---|---|
| **TF-1** | **Accept the narrower wording.** The guarantee is that invalid canonical content must never enter `main`/`dev` history **through merge**. Do **not** claim CI prevents invalid content from existing in feature-branch history |
| **TF-2** | **Confirmed: `_merge_seed.py` is retired, not repointed.** The S-3.4 consumer list in rev 3 is **`build_split.py` · `TextClassifierEval` · downstream research tooling** |
| **TF-8** | **Drop erratum #9 from rev 3.** No rev 3 errata entry for it. Also **remove or rewrite any S-5.6 rationale that cites this nonexistent error**; the **S-5.6 ruling and its dependency-pinning principle are unchanged** |

### Contradictions

<a id="tf-1"></a>**TF-1 · `CONTRADICTION` · S-3.7's **wording** overstates the reach of its named mechanism.**
S-3.7 as ratified requires CI to *"reject invalid canonical content **before it enters repository
history**."* The working notes' NEW EVIDENCE 21 establishes that the named enforcement point does not
reach that far as literally worded: *"CI gates the MERGE into main/dev, not the commit. A commit pushed
to a feature branch enters repository history before any gate runs"*, and the `pull_request` trigger
fires only for PRs targeting `main`/`dev`. The exactly-satisfiable form is **"invalid canonical
content never enters `main`/`dev` history."**

**Characterised as the evidence characterises it:** *"Both are precision, not objection; the owner's
intent is met on the trunk lines that matter."* The ruling is **satisfiable and the mechanism is
already wired** — branch protection plus CI have been in force since 2026-08-09, so the validator is a
step to add, not infrastructure to build. What needs fixing is the **sentence**, not the decision.

> **`RULED` (owner, 2026-09-04) — accept the narrower wording.** The guarantee rev 3 states is that
> **invalid canonical content must never enter `main`/`dev` history through merge.** Rev 3 must **not**
> claim that CI prevents invalid content from existing in **feature-branch** history.

**Consequence for rev 3.** §3 S-3's exit criteria state the merge-scoped guarantee and nothing wider.
S-3.7's ruled text in the Outcomes record keeps its original wording — the record is `ratified` and is
not edited by this; the narrowing governs **what rev 3 writes**.

<a id="tf-2"></a>**TF-2 · `CONTRADICTION` · `_merge_seed.py` is both repointed and retired.**
S-3.4 lists it among the consumers to repoint; S-3.7's stated consequence is that it is *"retired, not
converted."* Working-notes evidence shows this is **sequencing, not conflict**: at S-3.4 the choice was
explicitly *"queued as a small confirmation, not assumed"* (*"RETIRE it … or CONVERT it"*), and S-3.7
answers it. **Consequence for rev 3:** S-3.4's consumer list **as literally worded in the record is
stale** — a retired script is not repointed.

> **`RULED` (owner, 2026-09-04) — confirmed.** **`_merge_seed.py` is retired, not repointed.** The
> S-3.4 consumer list rev 3 writes is **`build_split.py` · `TextClassifierEval` · downstream research
> tooling**.

**Consequence for rev 3.** §3 S-3's consumer-boundary subsection is now writable. It names three
consumers, and records `_merge_seed.py`'s retirement as an S-3.7 consequence rather than listing it as
a repoint target. The invariant the working notes identified holds either way: **only the generator
writes the export.**

<a id="tf-4"></a>**TF-4 · `CONTRADICTION` (resolved to a Rewrite Obligation by checking the ratified text) · §1's P-2 row vs S-4.2.**
§1 states the 136 *"can never be promoted to Gold"* (L94); S-4.2 rules they **remain eligible for
Gold-A after owner adjudication**. **Checked against the 2026-08-27 ratified P-2**, which reads in
full: *"The 189 rows originated from approximately 2,000 Meta AI-generated rows … 136 eventually
entered the production seed. They are synthetic/AI-generated, even though their provenance is now
known."* **P-2 contains no promotability clause.** So §1's line is the **proposal author's gloss, not
ratified text**, and S-4.2 does not reopen a ratified decision. **Severity accordingly: a clean
Rewrite Obligation on §1 — which Outcomes §A.4 does not name.**

### Missing mappings

<a id="tf-5"></a>**TF-5 · `MISSING MAPPING` · Erratum #10 is under-scoped, and one of its pointers is unsupported.**
It names §4.1 / §4.2 / §S-4. The dead numbers also appear at **§2 L132**, **§S-1 L164 and L174**,
**§S-2 L212, L240, L245, L249, L263**, and **§7 L686**. Separately, **§4.2 contains no occurrence of
"208"** — that pointer has no target. Rev 3's errata entry must carry the full location list or the
dead numbers survive the rewrite in six sections.

<a id="tf-6"></a>**TF-6 · `MISSING MAPPING` · Erratum #12 names §S-3 only.** The identical
*"~5 new columns … (7 → ~12)"* figure appears in **§4.1 L562**, restated as a §19 quantification answer.

<a id="tf-7"></a>**TF-7 · `MISSING MAPPING` · Erratum #11 names §S-3 only.** *"8 file-level datasheets
where 0 exist"* also appears in **§4.1 L562**, unqualified. **§2 L140 is *not* affected** — see the
[No-Change ledger](#71-no-change-ledger--each-justified-by-quoted-conforming-text).

<a id="tf-8"></a>**TF-8 · `MISSING MAPPING` (inverted) · Erratum #9 corrects an error no artifact contains.**

**Provenance traced 2026-09-04 at owner instruction.** The claim *"the 29.6% denominator is 703, not
1028"* was checked against every candidate artifact and version:

| Artifact | Text | Verdict |
|---|---|---|
| **Audit §E.1** | Table row `208 / 703` → **29.6%** | Already correct |
| **Audit D-3** | *"The honest figure is 208/703 = 29.6%"* — D-3 **is** the audit's decision record for making that partition | Already correct |
| **Audit**, both commits (`3c19dcd`, `11222fb`) | no `208/1028` in either | Never wrong |
| **Proposal rev 1** (`b7e7b14`) | L23, L98 — *"the same **703** rows disagreed on 29.6%"* | Already correct |
| **Proposal rev 2** (`d087744` → current) | L30, L132 — same | Already correct |
| **Decision brief R-3**, both commits | *"Of **703** rows … **208** carry a different `TaskType`"* | Already correct |
| **2026-08-27 Outcomes** | no occurrence of 29.6% or 1028 | Silent |

**No artifact in the repository, in any version, ever stated 208/1028.** Every `1028` in the audit is
a correct use — 1028 unique common inputs, and `Difficulty`'s 167/1028 = 16.2%.

**Origin.** Working notes **NEW EVIDENCE 6, L220–221**: *"This fixes the denominator: **the audit's**
29.6% is 208/703, not 208/1028."* It attributes to the audit an error the audit did not make —
reproducing the audit's own partition and describing the reproduction as a correction of it. The rest
of that block is sound; the Difficulty finding beside it (96/703 = 13.7% vs 167/1028 = 16.2%, *"both
true, different scope"*) is a genuine scope distinction.

**How it acquired the `§4.1` pointer.** `[inference]` In the working notes' closing summary
(L2278–2285) every pending-fix bullet carries a section pointer **except this one**, and it sits
directly beneath the *"§4.1: Gold-A adjudication scope = 208 rows"* bullet. Those two became §A.3 **#8**
and **#9** — adjacent rows, both stamped `§4.1`. The adjacency and the missing pointer are observed;
the inheritance mechanism is inference.

> **`RULED` (owner, 2026-09-04) — drop erratum #9 from rev 3.** The 29.6% figure was already correct
> in every proposal and audit artifact checked; the erroneous "correction" originated in the working
> notes, not in the proposal. **Do not create a rev 3 errata entry for it.** Also **remove or rewrite
> any S-5.6 rationale that cites this nonexistent error.** The **S-5.6 ruling and its
> dependency-pinning principle are unchanged.**

**Consequences for rev 3.**

1. Rev 3's errata section carries **eleven** entries, not twelve. #9 is dropped; the others keep their
   identities so inbound citations still resolve.
2. Where rev 3 states S-5.6's manifest / dependency-pinning policy, it **must not cite the
   703-vs-1028 example** as the failure that motivates it. The policy is stated on its own terms: a
   figure's dependencies beyond its named rows — a corpus-wide denominator, a comparison set — are
   explicitly pinned, so row-level verification cannot silently leave contextual dependencies unpinned.
3. **Two existing artifacts still carry the dropped claim** and are **not** edited by this ruling —
   see [§9](#9-explicitly-out-of-scope) for why each is a separate act.

<a id="tf-9"></a>**TF-9 · `MISSING MAPPING` · R-1 is closed, and four locations still present it as open.**
S-2.2 closes it. Rev 2 still describes it as open at **§S-2's `R-1` subsection (L265–280)**, **§4.2's
R-1 row (L575)**, **§5.3 M-1** (*"it depends on `R-1`"*), and **§8 step 1** (*"`R-1` must be decided
before the threshold is pre-registered"*). Outcomes §A.4 names none of them.

<a id="tf-10"></a>**TF-10 · `MISSING MAPPING` · `provenance = unknown` vs S-3.2's `untraceable`.**
S-3.2 marks the 136 **`untraceable`** and adds a mandatory derived/recorded marker. Rev 2 uses
`provenance = unknown` at **§S-3 L313** and **§5.2's T-1 entry condition (L627)**. Unnamed in §A.4;
left unmapped it becomes two vocabularies for one state — which is the failure the explicit-`N/A` rule
exists to prevent.

<a id="tf-11"></a>**TF-11 · `MISSING MAPPING` · §S-3's no-retrofit boundary vs S-3.2's authorised backfill.**
§S-3 L311–314: *"DFD-5 governs **new** rows. Retrofitting lineage onto the existing 903 would mean
inventing it."* S-3.2 authorises backfill **for the 903 wherever mechanically derivable by join** —
which is derivation, not invention, and is exactly why the derived/recorded marker is mandatory.
The paragraph needs restating so the two are not read as opposed.

<a id="tf-12"></a>**TF-12 · `MISSING MAPPING` · §1's P-1 row vs S-7.1.** §1 L93 renders P-1's
consequence as *"It is a **Silver candidate at best**."* Under S-7.1 promotability is a per-source
ruling and **`collected_v4`'s is required and unruled** — so "at best" prejudges an open task. Ratified
P-1 says only *"Classification: synthetic / AI-authored, not real user data"*; the rest is the
proposal's gloss. §A.4 does not name §1.

<a id="tf-13"></a>**TF-13 · `MISSING MAPPING` · §4.1's synthetic-opportunities row vs S-7.2.**
§A.4 names §S-7's synthetic paragraph. **§4.1 L564** independently answers the §19 item
*"Controlled synthetic-generation opportunities"* as *"nameable but not yet actionable."* Under S-7.2
creation is **suspended** — there is no permitted purpose during this stage — so the row's framing
must change, not just S-7's.

<a id="tf-14"></a>**TF-14 · `MISSING MAPPING` · §1's DFD-7 row needs S-7.3's qualifier.**
§1 L116: ViLexNorm's licensing question *"now has a **route**, not just a blocker."* True, and S-7.3
adds the part that governs: **scheduling authorises nothing**, and completion is a hard gate on any
use. Unqualified, the row reads as progress toward permission.

<a id="tf-15"></a>**TF-15 · `MISSING MAPPING` · §S-T's exit criteria are unnamed.**
§A.4 names §S-T's gate list and silent-failure block. The **exit criteria (L455–457)** also need
rewriting: they must render **two gate states** (S-T.1) rather than one roll-up, and must carry the
retention window as a pending `[unknown]` (S-T.4) rather than assuming a policy *"exists."*

<a id="tf-16"></a>**TF-16 · `MISSING MAPPING` · §8's ordering omits the reservation event.**
Erratum #5 corrects §8's *"needs no infrastructure, no tooling and no data"* but not its **sequence**.
S-1.2, hardened by S-1.6 into *"a binding precondition"*, requires the 40-row reservation to execute
**before S-1 reads any row text**. §8 currently opens with S-1. The same omission reaches **§0's
property 3** (*"Only S-1 and S-2 are on the critical path"*).

**Circularity check performed — the sequence is executable.** S-2.5's composition (**12 contested /
8 Difficulty-spread**) could have depended on S-1's output, which would have made the precondition
circular and this flag far more serious. It does not: *contested* is the audit's measured disagreement
set, which §S-1 says S-1 *"inherits … rather than recomputing"*, and *Difficulty-spread* selects on
existing `DoKho` column values. Neither needs a boundary ruling. S-2.5 independently requires
composition *"pre-registered **before any S-1 inspection**"*, so the owner has ruled the order
executable. **This is an ordering fix to §8, not an unexecutable sequence.**

<a id="tf-17"></a>**TF-17 · `MISSING MAPPING` · §S-4's J-3 line is answered.**
§A.4 names §S-4's AI paragraph, J-7 line and exit criteria. **L332** still reads *"whether they may
ever be **promoted** to Gold is a separate judgement"* — S-4.2 has now made that judgement.

### New policy requirements with no home section

<a id="tf-18"></a>**TF-18 · `NO HOME` · The scope ruling's standing directive.**
*"Implementation-dependent details become follow-ups or amendments; a governance decision is never
deferred on implementation grounds"* — the record names the prohibited move explicitly (*"S-3 is not
implemented yet"* is not grounds to postpone a ruling). This is one of the 54 ruled items, it is
**owner text**, and **no section of rev 2 can receive it**: it governs how the rewrite is conducted,
not what the proposal says. Filing it as No Change would lose a ruled item. **Rev 3 needs a home for
it** — most naturally alongside §6 (*What this proposal is not*) or a new standing-rules section.

<a id="tf-19"></a>**TF-19 · `NO HOME` · The explicit-`N/A` rule.**
S-T.1, S-T.2 and S-T.5 each solve a *cannot-be-mistaken-for* problem with the same device. The general
form — *wherever absence is legitimate it gets an explicit value; blank is reserved for "not yet
supplied" and is always an error state* — is **agent-authored** (Outcomes §A.1), not ruled, but the
three instances that instantiate it **are** ruled. Rev 3 needs one place to state it or it is
re-derived three times.

<a id="tf-20"></a>**TF-20 · `NO HOME` · *"How to read a figure from this project."***
Outcomes §A.2 escalates this from suggestion to requirement over **six** reader-facing
non-uniformities plus two reading rules. Rev 2 has no such section. **It is agent-authored, not owner
policy** — flagged so rev 3 either creates the section or records why not.

<a id="tf-21"></a>**TF-21 · `NO HOME` · Version-stream hygiene and `LabelVersion`'s disqualification.**
Four streams must stay distinct, and **`LabelVersion` is disqualified**. Rev 2 still presents it as
load-bearing: **§2 L141** counts it as one of the *"2 of 7"* lineage fields present, and **§S-3's
lineage table (L304)** maps it to *"dataset version — partial."* Both need the disqualification stated
where the field is named, not only in a hygiene note.

<a id="tf-22"></a>**TF-22 · `NO HOME` · §S-6 has no metric registry to amend.**
S-6.1, S-6.2 and S-6.3 specify a pre-registered metric set, an MAE headline, chance baselines, and
mandatory intervals. **§S-6 currently contains no metrics section at all** — four structural rules and
an exit criterion. This is the largest block of **net-new** content in the rewrite, and it is
classified Policy rather than Rewrite Obligation for that reason.

<a id="tf-23"></a>**TF-23 · `NO HOME` · S-T's two-state status surface has no rendering location.**
S-T.1: *"An S-T status surface must render two states; a single roll-up field is non-conforming."*
S-T.5 places gate states in the datasheet's live-source section. But rev 2's **§7 risk row** *"A tier
declared reached on its visible half"* and **§5's ladder** both still model progress as single-valued.
Rev 3 must say where the two states are rendered outside the datasheet.

### Distinctions to preserve — **not defects**

<a id="tf-3"></a>**TF-3 · `DISTINCTION` · DFD-7's *inspection* permission does not cover corpus
measurement.** DFD-7 as ratified allows external datasets to be *"discovered, evaluated, and
**inspected** for relevance/provenance/licensing"* — inspection of the **licensing surface**, which is
exactly what OD-4 is. S-7.3 gates use of the **corpus contents**, including read-only measurement.
**Different objects, so this is not a narrowing** and needs no declaration under the Outcomes record's
narrowing clause (*"S-2.6 is the one case"*). Recorded because the two permissions are one word apart
and rev 3 must not let DFD-7's *inspected* be read as licence to measure the corpus before OD-4
completes.

<a id="tf-24"></a>**TF-24 · `DISTINCTION` · Three different threshold objects.** S-2.7 sets numbers
(17/20, 18/20) while Q-5 forbids inventing thresholds before evidence and S-6.3 forbids a numerical
precision threshold before Q-5 permits one. These do not conflict — a **spec-reproducibility gate**, a
**dataset-maturity threshold** and an **evaluation-precision threshold** are three objects. But §5.3's
M-1 currently reads *"threshold: owner, at T-3"* while S-2.7's thresholds are set **now** and gate
**T-1**. Rev 3 should state the distinction once rather than let the reader reconcile it.

<a id="tf-25"></a>**TF-25 · `DISTINCTION` · The Q-4 floor does two jobs.** §S-5's floor text governs
**collection**; S-5.7 condition (2) governs **holdout allocation** and rules the floor out of it. The
existing text is not wrong — see the [No-Change ledger](#71-no-change-ledger--each-justified-by-quoted-conforming-text)
for §5.2's conforming use. Rev 3 must simply keep the collection sense from leaking into the
allocation rule.

<a id="tf-26"></a>**TF-26 · `DISTINCTION` · Erratum #2's arithmetic propagates.** The *"fourth item"*
miscount also reaches **§8 L695** (*"the four bounded questions"*) and **§9.1 change 3**
(*"S-1 gains a fourth scope item"*). §9.1 is a historical record of rev 2 and should **not** be
edited — but rev 3's live text must not repeat the count.

---

## 9. Explicitly out of scope

Recorded so a later reader can tell what this pass declined to do, rather than inferring it was
overlooked.

- **The proposal was not rewritten, edited, or opened for edit.** Rev 3 remains unwritten; rev 2 keeps
  its `draft — awaiting owner review` status. Nothing here authorizes implementation.
- **No owner decision was inferred, extended, or invented.** The three flags that ruled text could not
  determine — **TF-1, TF-2, TF-8** — were put to the owner and **ruled on 2026-09-04**
  ([§8.0](#80-owner-rulings-on-this-matrix--2026-09-04-after-filing)). No mapping was resolved by
  inference. Where a mechanism rather than a fact is proposed, it is marked `[inference]` in place.
- **Two artifacts still carry the dropped erratum-#9 claim, and neither was edited.** TF-8's ruling
  governs **what rev 3 writes**; changing either of these is a **separate act needing its own
  decision**, because of what each document is:
  - **Outcomes §A.3 #9** — inside a `ratified` record. Correcting it is an **amendment to a ratified
    document**, not a rev 3 edit.
  - **Working notes L220–221, L1489, L1498** — a **dated** agent record whose job is to say what was
    believed when written. Project convention corrects those by amendment, not in place. They are
    already declared *"superseded by this record wherever the two differ."*
- **The audit's §J-2 error** (errata #1's second half) was **not** corrected here, on the same ground
  the Outcomes record gives: the audit is a separate document and editing it is scope expansion. It
  remains the **one open owner call** on this matrix.
- **The working notes were read in seven ranges only** — L218–240, L761–790, L804–832, L925–945,
  L1371–1400, L1476–1500, L2278–2290 — bearing on flags TF-1, TF-2, TF-7, TF-8 and the S-4.6/S-5.6
  closure. They are evidence and rationale; **no ruling below the Outcomes record's text was sourced
  from them.** The TF-8 trace additionally read **proposal rev 1 (`b7e7b14`) and rev 2 (`d087744`) from
  git history**, plus the audit's and decision brief's commit histories, to establish that no version
  of any artifact carried the claimed error.
- **No number was invented.** Where a figure is unknown it is carried as `[unknown]` — the retention
  window, J-7's count, the S-5.7 split fraction, Q-4's per-class floor, telemetry row counts.
- **`collected_v4`'s Gold-A eligibility was not ruled** and must not be inferred from S-4.2. S-7.1's
  scope limit governs: S-4.2 removed provenance as a *disqualifier*; it created no presumption of
  eligibility.
