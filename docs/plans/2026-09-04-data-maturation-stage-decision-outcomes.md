# Data Maturation — Stage Decision Outcomes (S-1 … S-8, S-T)

> **Filed 2026-09-04.** Consolidated record of the owner-review pass over the **stage decisions** in
> [`2026-08-26-data-maturation-coverage-expansion.md`](2026-08-26-data-maturation-coverage-expansion.md)
> (the proposal, rev 2).
>
> **This is a decision record, not a plan.** It follows the shape of its predecessor,
> [`2026-08-27-data-maturation-owner-decision-outcomes.md`](2026-08-27-data-maturation-owner-decision-outcomes.md),
> and is exempt from `plans/README.md`'s six plan sections for the same reason that document is.
>
> **Relationship to the 2026-08-27 filing.** That document ruled the *governance surface* — P-1…P-3,
> DFD-1…DFD-9b, Q-1…Q-5. This one rules the *stage-level* decisions those rulings left open: S-1
> through S-8 plus the S-T strand. **Nothing here reopens a ratified P / DFD / Q decision.** Where a
> ruling below narrows one, it says so and names the owner's authority to do it (S-2.6 is the one
> case).
>
> **Status: `ratified` for the decisions below.** The stage decision surface is closed.
> **Implementation is still not authorized.** Proposal rev 2 remains `draft — awaiting owner review`
> and awaits **authorization** separately; these decisions are the inputs to **rev 3**, not a clearance
> to execute. Ruling every decision is not the same as authorizing the work.
>
> **What is owner text and what is not.** §2–§10 record owner rulings. For **S-7, S-8, the scope
> ruling, and S-T**, the owner's wording is reproduced **verbatim** in blockquotes. For **S-1 … S-6**
> the working record captured the *ruled outcome and its conditions* rather than a verbatim
> transcript; those are marked `[recorded ruling]` and must not be read as quotation. The verbatim
> exchanges live in the session transcript, not here.
>
> **§A is agent-authored and is not part of the ruling.** It holds standing principles, documentation
> errata, rewrite obligations, and open tasks. Do not read §A as owner policy.

---

# 1. Decision inventory

| Stage | Decisions | Outcome |
|---|---|---|
| **S-1** — Taxonomy review | 7 (S-1.0 … S-1.6) | Closed |
| **S-2** — Annotation spec + reproducibility | 12 (S-2.1 … S-2.12) | Closed |
| **S-3** — Provenance / lineage | 7 + 1 closing confirmation | Closed |
| **S-4** — Gold-A | 6 (S-4.1 … S-4.6) | Closed |
| **S-5** — Gold-R | 7 (S-5.1 … S-5.7) | Closed |
| **S-6** — Evaluation foundation | 3 (S-6.1 … S-6.3) | Closed |
| **S-7** — Controlled expansion | 4 (S-7.1 … S-7.4) | Closed |
| **S-8** — Future model work | 1 (S-8.1) | Closed |
| **Scope** — S-T sequencing | 1 | Closed |
| **S-T** — Telemetry readiness (strand) | 5 (S-T.1 … S-T.5) | Closed |

**Total: 52 numbered stage decisions + 1 S-3 closing confirmation + 1 scope ruling = 54 ruled items.**

---

# 2. S-1 — Taxonomy review  `[recorded ruling]`

| # | Ruling |
|---|---|
| **S-1.0** | **B.** The **production** taxonomy is S-1's subject, and retirement B (`OnTap`/`NhacNho` deletion) is **reopened as one explicit review question** rather than inherited silently. Default stays "out" unless ruled otherwise. Stays inside P-3 via P-3's explicit-decision escape hatch |
| **S-1.1** | **C.** Retirement B **confirmed**: `OnTap`/`NhacNho` do not return as production classes. **Reminder-ness is relocated, not deleted** — it remains a *derived* attribute/surface, and S-1 must say so explicitly so S-2 does not re-litigate it as a missing class |
| **S-1.2** | **B.** **Reserve first, then sample.** The S-2 reproducibility batch and Q-1's batch are reserved **before S-1 reads any row text**; S-1 reads from the remainder. Independence is established by sequence |
| **S-1.3** | **B.** The third annotation pass is **in scope as evidence, not as a new question**. Its 121 rows inform the existing `ThiCuoiKy`/`BaiTapVeNha` boundary; S-1 gains no new review item and rules nothing on whether the `ThiGiuaKy`/`DoAnCuoiKy` subdivision was correct |
| **S-1.4** | **B + owner addition.** S-1 **defines Difficulty as an explicit 1–5 semantic/ordinal scale with written anchors**, so S-2 can govern future labels. S-1 does **not** audit or change current consumers. *Owner addition:* any `(DoKho/5)*60` or downstream consistency check is a **separate follow-up** raised only after the semantics are frozen → **FU-1** |
| **S-1.5** | **B.** S-1 keeps a class-justification question **reframed onto product-intent grounds**, premise corrected. The **evaluation defect moves to S-6** (file-level split; entire test set is AI-generated `collected_v4`; 3-of-5 class coverage; zero train/test text overlap) |
| **S-1.6** | **B + owner addition.** S-1 issues a **separate cause ruling for each production-relevant contested boundary**. Do not collapse into one global verdict; do not assume the agent's two-axis reading. *Owner addition:* any hold-out/reservation conflict is a **sequencing constraint** — reservation executes **before** S-1 reads disputed rows, hardening S-1.2 into a binding precondition |

---

# 3. S-2 — Annotation specification and reproducibility  `[recorded ruling]`

| # | Ruling |
|---|---|
| **S-2.1** | **B + C1–C4.** TaskType and Difficulty are evaluated **independently**. C1 independent evaluation · C2 **both must pass their own pre-registered threshold** (no averaging, no trade-off) · C3 the **same** pre-reserved 20-row holdout serves both — one batch, two measurements · C4 the report must expose **per-dimension and per-boundary detail**; a single headline number is not an acceptable output |
| **S-2.2** | **B.** R-1 closed. **Owner = Gold/reference pass; one independent human reader from the Q-2 network = blind reproducibility probe.** Both annotate the same 20 rows independently. AI may be added later as a **supplementary** probe, never a substitute. Under DFD-8 the owner's pass is the label; the reader's pass is a measurement, never Gold |
| **S-2.3** | **B.** **One stratified batch**: contested rows carry the TaskType axis, additional rows span the Difficulty range. The **level-1/level-2 gap is declared, not solved** — the v1 Difficulty threshold governs levels 3–5 only; levels 1–2 defer to authored examples in S-4 |
| **S-2.4** | **B + G1–G4.** **Full-batch scoring**: the whole 20-row batch scores both dimensions. G1 composition **and** scoring denominators are pre-registered · G2 per-stratum breakdown required · G3 headline rates **must not** be described as corpus-wide agreement · G4 **batch composition travels with every quoted figure** — no bare percentage anywhere |
| **S-2.5** | **C.** **Reserve 40 once**, in a single reservation event, pre-partitioned into a **20-row scored batch** (12 contested / 8 Difficulty-spread) and a **clean 20-row retest batch** held for a v2 re-test. Composition of both partitions pre-registered before any S-1 inspection |
| **S-2.6** | **A + scoping condition.** Q-1 draws from the contested backlog. Its result estimates **contested adjudication time only** — explicitly **not** total Gold-A workload. J-3 disposal and J-7 residue must be estimated separately. **No invented weighting factor** may fold them together. *A narrowing of the ratified Q-1, on the owner's own authority; not a reopening* |
| **S-2.7** | **Bundle B + pre-registration lock.** **TaskType ≥ 17/20 (85%)** · **Difficulty ≥ 18/20 (90%)**, **exact match**. Within-one may be **reported as a diagnostic**; it is never the gate. Failure ⇒ spec revision plus the pre-reserved v2 retest batch. **Thresholds must not be changed after observing results** — an invariant of the test, recorded next to the numbers |
| **S-2.8** | **Ruled unprompted: 5-way.** "Adjudicate" means **assign the correct label from the full current production taxonomy (all five classes)** — not a two-way choice between the originally disputed labels. Adjudication and annotation become the same operation, plus a recorded ruling |
| **S-2.9** | **B + K1–K3.** **Boundary-indexed catalogue**, 3–5 representative examples per production-relevant boundary. K1 S-2 does **not** adjudicate the remaining rows · K2 **pre-register an example-selection rule** so the catalogue cannot become self-confirming · K3 **retain row IDs and provenance** for every example |
| **S-2.10** | **C + H1–H3.** Reservation and catalogue references key on **content hash + source file + line**; S-3 introduces a stable surrogate **RowId** retaining the originating hash permanently. H1 the hash is a **locator for the reserved snapshot**, not long-lived identity · H2 corrected source text **preserves old hash references** (supersession chain) · H3 the reservation event **materialises a snapshot** — an artifact, not a query |
| **S-2.11** | **A + bump rule + guardrail.** S-2 produces **only** the versioned annotation specification and its version/bump semantics; **S-3 owns the row-level `GuidelineVersion` field**. **Bump when:** a class definition, a class-boundary rule, or a Difficulty anchor changes. **No bump for:** typos, formatting, non-semantic example additions. *Owner guardrail:* an example addition that **changes the effective classification rule** is a semantic change and **requires a bump** |
| **S-2.12** | **B + AI-drafting disclosure.** S-2 carries the semantic contract **plus a concrete annotation-record template**, kept explicitly as a **working annotation artifact**, not an S-3 storage contract. The canonical spec **must identify which sections were AI-drafted or AI-assisted** (DFD-8). *Owner qualification:* this is **provenance/transparency metadata, not a quality or trust score** |

---

# 4. S-3 — Provenance and lineage  `[recorded ruling]`

| # | Ruling |
|---|---|
| **S-3.1** | **B.** The governed corpus lives **outside the application**. `seed_intents.csv` becomes a **generated export** projecting only the columns the shipped app requires. Decisive property: provenance edits no longer change export bytes, so `ComputeSeedHash()` does not fire and **no retrain is forced on any installation** |
| **S-3.2** | **B + marker mandatory on export.** Backfill per-row provenance for the 903 existing rows wherever mechanically derivable by join; the 136 with no antecedent are marked **`untraceable`**. A second field distinguishes **derived-by-join** from **recorded-at-creation**. *Owner condition:* the **derived/recorded marker is mandatory on export** and may not be dropped when provenance values leave the corpus |
| **S-3.3** | **B + byte-stability + completion gate.** The canonical governance corpus is **JSONL**; `seed_intents.csv` is retained as a **deterministic generated export**. *Owner requirements:* the export must be **byte-stable for unchanged canonical content**; the consumer path is **S-3.4, not S-3.3**; and **S-3 is not complete until the consumer boundary is resolved and verified** |
| **S-3.4** | **A.** Non-production consumers read the **canonical corpus directly**. The production app **alone** consumes the lean CSV export — a **deployment artifact, not the source of truth**. Repoint `build_split.py`, `_merge_seed.py`, `TextClassifierEval` and downstream research tooling; preserve deterministic export; **update both existing hash pins**; **no sidecar joins, no second authoritative export** |
| **S-3.5** | **B + fail-closed.** Fixed core plus a nested **`Provenance`** object whose required-key set is **explicitly versioned and mechanically enforced**. **Missing, null, or structurally invalid required provenance rejects ingestion**; presence of the object alone is not sufficient. H2's supersession history is preserved **without** adopting full event sourcing — the concrete mechanism belongs to execution planning, not to this proposal |
| **S-3.6** | **C + A8 named exclusion.** Author file-level datasheets for the **canonical corpus and genuine source inputs only**; derived exports and splits carry **machine-generated provenance**. **A8 (`SeedDataGenerator.Generate()`) is explicitly out of scope for S-3**, as it belongs to the separate M7 study-time-predictor domain — a **deliberate, reasoned exclusion**, recorded as **FU-2**, not silently absorbed |
| **S-3.7** | **B.** The authoritative fail-closed gate sits at the **canonical file / repository boundary**: a complete validator must **reject invalid canonical content in CI before it enters repository history**. Direct owner edits remain allowed under DFD-8; a local validator may be added later as convenience, but **CI remains the authoritative enforcement point**. Consequence: **`_merge_seed.py` is retired, not converted** |
| **S-3 closing** | **Ratified: external build manifest.** The canonical→export relationship is recorded **outside the export bytes**, in a build manifest — an **attestation artifact**, not a runtime sidecar and not a second authoritative export. It records **canonical hash · export/content hash · generator identity and version**. `ComputeSeedHash()` continues to cover only the model-relevant export content |

---

# 5. S-4 — Gold-A  `[recorded ruling]`

| # | Ruling |
|---|---|
| **S-4.1** | **C, at scope level only.** Gold-A = the **full production-relevant contested pool** plus a **deliberately designed representative authored sample** covering the defined TaskType/Difficulty space, especially the currently absent low-Difficulty range. Ratified at **scope level, not a fixed row count**; the sample size and sampling rule must be **explicitly designed and approved before selection** — **do not invent the number from the current data**. The sample is **not a substitute** for the contested backlog: two components, two purposes, both required |
| **S-4.2** | **C, with the tier derived.** The 136 origin-unknown rows **remain eligible for Gold-A after owner adjudication**, because **Gold-A certifies label correctness, not source realness**. Origin-unknown status remains a **persistent, visible qualifier** wherever Gold-A is summarised or cited. **Do not duplicate provenance in a second schema field** — the provenance object is the single source of truth and the classification **derives** from it |
| **S-4.3** | **B — commit-then-reveal.** AI predictions are **computed but hidden until the owner commits the adjudication label**, then revealed as an optional adversarial/re-review signal. **AI is not an independent annotator here** and its output must never be treated as validation or authority. A label changed after the reveal is recorded as an **explicit second review**, never a silent overwrite |
| **S-4.4** | **A — exclude and log.** A row the canonical guideline cannot decide is **excluded from `gold_a_v1` and logged explicitly as a guideline gap**. Do not assign a confidence-qualified Gold label; do not spend an extra human pass to force a v1 ruling. Preserve the row's **identity, reason, boundary, and guideline version**. *Owner addendum:* a later guideline bump triggers re-review **only for rows whose applicable semantic rule actually changed** |
| **S-4.5** | **C — targeted rule attribution.** The adjudication record carries a rule citation **only where existing structure does not already answer "which rule decided this row"**: **Difficulty rulings** name the S-1.4 anchor applied; **authored-sample rows** name the rule that placed them; **contested-pool TaskType rulings inherit their recorded boundary and add nothing** |
| **S-4.6** | **B — pinned manifest, fail-closed.** `gold_a_v1` is fixed as a **pinned manifest** of selected row identities, canonical corpus hash, and guideline version; rows are resolved from the canonical corpus at computation time. The manifest **must fail closed if its pinned corpus state cannot be resolved**. Row content is **not** duplicated into a second authoritative artifact |

---

# 6. S-5 — Gold-R  `[recorded ruling]`

| # | Ruling |
|---|---|
| **S-5.1** | **A.** Track B rows may continue to accrue **locally**, but **no row may leave the user's machine or enter `gold_r_v1` without explicit per-participant consent for that transfer**, with the consent record/version preserved alongside the contributed data. Keeps the governance standard consistent with Track A and prevents reproducing the project's historical provenance problem with real user data. **Resolves the Q-3 residual** |
| **S-5.2** | **C.** Pre-mechanism rows **may** be contributed with **explicitly derived/backfilled provenance**, but must remain **below the top maturity tier** and are **ineligible for the S-6 held-out partition**. Rows accrued after the mechanism ships may qualify for the higher tier. **All Gold-R reporting must distinguish these provenance grades explicitly** |
| **S-5.3** | **C.** **One governed canonical corpus and one authority**, with rows **physically partitioned by role** so the training/export path has no access to held-out Gold-R rows. The partition is an **enforcement boundary, not a second authority**; provenance, track and tier remain **governed metadata**; **any role move must be explicit and hash-visible**. Role is the **one** physical attribute — a bounded, deliberate exception to derived-not-stored |
| **S-5.4** | **B.** Gold-A rows **remain in the training partition and export**. Gold-A is a **reference/adjudication corpus, not a held-out performance set**; S-6 performance figures must use a **separately reserved holdout established before training**. §S-6's wording must be corrected so it does not imply otherwise |
| **S-5.5** | **A.** Gold-R labels are **owner-assigned under the S-2 specification**. User `FinalDoKho` / `WasOverride` signals are **preserved as governed metadata and never treated as the Gold label** — keeping Gold-A and Gold-R on the same spec-defined construct while preserving the real-user signal for a **separate future analysis of perceived difficulty** (**FU-3**) |
| **S-5.6** | **B + explicit dependency pinning.** A manifest **fails closed when a pinned row is missing or its content no longer matches**; **unrelated corpus churn must not invalidate it**. *Owner addition:* any figure dependency beyond its named rows — a corpus-wide denominator, a comparison set — **must itself be explicitly pinned in the manifest**, so row-level verification does not silently leave contextual dependencies unpinned |
| **S-5.7** | **B + three conditions.** A **pre-registered assignment rule**, fixed before observing row content, assigns Gold-R rows to held-out vs training; the **split fraction is instantiated after the initial volume is known**. (1) **Neither arrival order nor post-hoc class balancing** may determine holdout membership. (2) **Q-4's per-class floor is a readiness/coverage criterion, not the allocation rule.** (3) If volume is insufficient, mark Gold-R evaluation **not yet ready** rather than forcing an undersized split |

---

# 7. S-6 — Evaluation foundation  `[recorded ruling]`

| # | Ruling |
|---|---|
| **S-6.1** | **B + headline discipline.** Pre-register, **before Gold-R exists**, the full metric set **and** the single headline: **TaskType** exact-match; **Difficulty** exact-match, within-one, and MAE; and a **chance baseline for each applicable metric computed from the observed Gold-R marginal**. **Report all registered metrics**, but only the pre-registered headline is the primary figure — the others are **mandatory diagnostics, not post-hoc alternatives** |
| **S-6.2** | **B — MAE.** Difficulty's headline is **mean absolute error**, the registered metric most faithful to the ordinal construct and retaining full error-distance information. **Exact-match remains a mandatory diagnostic; within-one is diagnostic only**, because its chance baseline can be near-ceiling under a clustered marginal. The numeric chance baseline **must be recomputed from the actual Gold-R marginal** when available |
| **S-6.3** | **C + one-sided guard.** An **interval is required on every figure**, and a result whose interval **includes the pre-registered chance baseline** is labelled **"not distinguishable from chance."** **Exclusion of chance is not validation and not a precision guarantee** — it is a **one-sided negative-evidence guard**. **Interval width remains an explicit uncertainty qualifier.** **No numerical precision threshold before Q-5 permits one** |

---

# 8. S-7 — Controlled expansion (Silver, public, synthetic)

## S-7.1 — Silver promotability to Gold-A

> **C.** Do not apply a blanket Silver promotability rule. Rule Gold-A eligibility per source and
> record it in the source datasheet under S-3.6, with owner adjudication still required and
> provenance/methodology qualifiers preserved in reporting. S-4.2 means provenance alone is not a
> Gold-A gate; it does not make every Silver source automatically promotable.

**Established.** §S-7's blanket *"never promotable to Gold"* is **superseded**, and is **not** replaced
by a blanket permission. Gold-A eligibility is a **per-source ruling recorded in that source's S-3.6
datasheet**; promotability is a property of a *source*, not of the Silver tier. Owner adjudication
remains required in every case — an eligibility ruling grants only the right to be adjudicated, never
a label. **Scope limit, in the owner's own words:** S-4.2 removed provenance as a *disqualifier*; it
created no presumption of eligibility.

**Consequences.** Each Silver source needs an explicit eligibility ruling before any of its rows are
adjudicated. The 136 untraceable rows already have theirs (S-4.2: eligible). **`collected_v4`'s is
explicitly unruled, and is now required.** S-3.6's datasheet schema gains a mandatory **Gold-A
eligibility** field (eligible / not eligible / unruled); `unruled` blocks adjudication.

## S-7.2 — Authorisation for synthetic generation

> **A.** Suspend creation of new synthetic batches during Data Maturation. Existing synthetic rows
> remain in Silver and are untouched. Synthetic generation may resume only after an observed-data gap
> and an explicit warrant are available; this stage does not authorize synthetic generation for
> distributional or coverage claims.

**Established.** Creation of new synthetic batches is **suspended** for the duration of Data
Maturation — not restricted, suspended; there is no permitted purpose during this stage. Existing
synthetic rows (`synthetic_v3`, the 136, `collected_v4`) **remain in Silver and are untouched**; the
ruling is prospective only. **Resumption requires both, conjunctively:** (a) an **observed-data gap**,
measured against observed data rather than the authored corpus, and (b) an **explicit warrant** for
the batch. **The prohibition on synthetic generation for distributional or coverage claims is a
standing one** and survives the stage that suspended it.

**Why the section needed this.** §S-7 listed only *entry controls* — generator provenance, spec
conformance, distribution checks — which answer *does this batch pass?*, never *should this batch
exist?* Its trap block removes the only warrant anyone would invoke (*"Q-4 permits targeting coverage
gaps … by collecting, and this stage does not inherit that permission"*), and the safeguard that would
restore it is unsatisfiable while five linguistic phenomena remain `[unknown]`.

## S-7.3 — ViLexNorm and the OD-4 licensing review

> **A.** Schedule OD-4 now as an owner licensing-review task, with completion required before any
> ViLexNorm use. The review should answer the stated NC/SA and derivative-use questions; scheduling
> does not authorize read-only measurement or ingestion, and ViLexNorm does not satisfy S-7.2's
> requirement for an observed real-population gap.

**Established.** OD-4 is **scheduled** as an owner licensing-review task, and its **completion is a
hard gate on any ViLexNorm use** — no tier, no purpose, no exception. Its question: does
non-redistributive measurement use create a derivative under **`SA`**, and does **`NC`** bind a
non-distributed internal artifact? **Scheduling authorises nothing** — explicitly not read-only
measurement, not ingestion.

**Ratified correction.** ViLexNorm does **not** satisfy S-7.2's resumption condition. As an instrument
it measures teencode in *our* corpus, which is authored/AI-generated, so it characterises the
**authoring process** — the confusion §S-7's trap block warns against. As external evidence its
population is **social-media writers**, not students entering study tasks. S-7.2 requires an observed
gap in the **relevant** population; neither route supplies one.

**Consequence.** With ViLexNorm gated, and UIT-VSFC (*"no licence field on the dataset card"*) and
PhoATIS (*"no licence surfaced"*) failing the same rule — `public/downloadable ≠ relevant ≠ licensed ≠
approved for project use` — **all three public candidates are closed for this stage.**

## S-7.4 — S-7's remaining scope and the distribution check

> **B.** Keep S-7 as a governance-only stage during Data Maturation and build/freeze the distribution
> check now. Prove it fails on collected_v4's known regularities, but specify the check over general
> phenomenon/feature families rather than hard-coding those seven patterns. Future changes must be
> versioned rather than tuned against a pending batch.

**Established.** S-7 remains in Data Maturation as a **governance-only stage** — no ingestion, no
generation. Its work is the per-source eligibility rulings of S-7.1 and the check below. The
**distribution check is built and frozen now, while no batch is pending**, and must be demonstrated to
**fail** on `collected_v4`'s known regularities. **Specification constraint:** the check is specified
over **general phenomenon / feature families**, not hard-coded to the seven observed patterns — the
seven are a proof case, never the definition. **Change control:** future changes are **versioned**,
never tuned against a pending batch.

---

# 9. S-8 — Future model work

## S-8.1 — Pre-registration of the encoder-revival bar

> **C.** Pre-register the necessary but not sufficient revival preconditions now: real Gold-R holdout,
> S-6 baseline with required intervals, a specific pre-result hypothesis identifying the deficit the
> encoder is expected to address, and DAT-04 unchanged. The hypothesis must state the observed
> deficit, proposed mechanism, evaluation metric/contrast, and predicted direction before results are
> seen; changing it later requires a new version and does not retroactively qualify the prior
> experiment. No numerical revival threshold is set yet, and a separate owner decision remains
> mandatory.

**Four necessary — not sufficient — preconditions.**

1. A **real Gold-R holdout** exists (S-5.7 allocation, S-5.3 partition).
2. An **S-6 baseline measured with the required intervals** (S-6.1 metric set, S-6.2 MAE headline,
   S-6.3 intervals and chance-baseline labelling).
3. A **registered pre-result hypothesis** with four mandatory elements — the **observed deficit**, the
   **proposed mechanism**, the **evaluation metric/contrast**, and the **predicted direction** — all
   stated **before results are seen**.
4. **DAT-04 unchanged:** dataset growth alone does not authorise re-running the experiment.

**Change control.** Amending the hypothesis requires a **new version**, and a later version **does not
retroactively qualify the prior experiment**.

**Explicitly not set.** No numerical revival threshold — leaving open, rather than settling by
implication, whether S-6.3's prohibition on precision thresholds reaches revival thresholds too.
**A separate owner decision remains mandatory** even when all four preconditions are met.

---

# 10. Scope ruling and S-T — Telemetry readiness (DFD-9b)

## Scope ruling — S-T runs before consolidation

> **A** — run the S-T decision pass before consolidation. S-5.1 establishes the consent rule for
> transfer but does not by itself close S-T's separate gate, so the interaction must be ruled
> explicitly rather than inferred during the rewrite. Keep any implementation-dependent details as
> later follow-ups or amendments; do not defer the governance decision itself.

**Two directives.** S-5.1 does **not** by itself close S-T's gate — inferring the interaction during
the rewrite is prohibited by name. And **implementation-dependent details become follow-ups or
amendments; a governance decision is never deferred on implementation grounds** — *"S-3 is not
implemented yet"* is not grounds to postpone a ruling.

## S-T.1 — What closes S-T's consent gate

> **C.** Split S-T into two explicitly independent gates: a local collection/retention/handling gate
> that can close now when its policy and controls are satisfied, and an egress/transfer gate governed
> by S-5.1 that is N/A while no transfer mechanism exists and becomes blocking when one is introduced.
> S-T itself must never be reported as "closed" merely because the local stage closed; both states
> remain explicitly visible

**Established — two explicitly independent gates.**

1. **Local collection / retention / handling gate** — accrual on the user's own device, retention
   duration, handling, disposal. **Closable now** once its policy and controls are satisfied.
2. **Egress / transfer gate**, governed by **S-5.1**. Its state is **`N/A` while no transfer mechanism
   exists**, and it **becomes BLOCKING the moment one is introduced**. `N/A`, not `closed` and not
   `satisfied` — it cannot be mistaken for a met condition.
3. **S-T must never be reported as "closed" on the strength of the local gate alone.** Both states
   remain **explicitly visible** in any status report on this strand.

**Consequences.** An S-T status surface must render **two states**; a single roll-up field is
non-conforming. Building a transfer mechanism is a **gate-state transition**, not a feature ship.
S-5.2 continues to govern pre-mechanism rows, unchanged.

## S-T.2 — Provenance at write time (gate 1)

> **A.** Use a capture-complete write-time rule: record every S-3 lineage field that is meaningful for
> a runtime-captured row rather than predicting which fields may be needed later. The fields must
> still be governed by Gate 4 validation/invariants so an unpopulated or defaulted column cannot
> masquerade as valid provenance.

**Established.** **Capture-complete tie-breaker** — when it is unclear whether a field will be needed,
**record it**; explicitly not a minimal, predict-what-matters rule. **Gate 4 validation is mandatory**
on those fields: an unpopulated or defaulted column **must not be able to masquerade as valid
provenance**, and validation must reject that state.

**Structural consequence.** §S-T lists gates in dependency order with the dataset contract at 4. This
ruling makes **gate 4 supply the validation for gate 1**, so the two are **mutually dependent** — the
list must not be read as a strict sequence.

## S-T.3 — Measurement authorisation (gate 5)

> **A.** Authorize only volume and technical-usability measurements now: row counts, capture-date
> ranges, and null/usability rates for the already-defined evaluation fields such as PredictedMinutes
> and Confidence. Do not inspect label or feature marginals until the S-5.7 allocation rule is
> pre-registered. Record the measurement scope explicitly so these operational checks cannot be
> mistaken for Gold-R distribution analysis.

**Authorised:** row counts (both tables); capture-date ranges; null/usability rates on the
already-defined evaluation fields (`PredictedMinutes`, `Confidence`).

**Prohibited until the S-5.7 allocation rule is pre-registered:** label marginals (`FinalDoKho`,
`SuggestedDoKho`) and feature marginals (`TaskType`, `Difficulty`, `Credits`, `DaysLeft`,
`StudiedMinutesSoFar`, …). The prohibition is tied to S-5.7's pre-registration, not to a session or a
judgement of safety.

**Scope-recording requirement.** The measurement scope is **recorded explicitly alongside any figure**
produced, so operational usability checks **cannot be mistaken for Gold-R distribution analysis**. A
number from this measurement is a volume/usability fact and must never be cited as evidence about the
data's distribution.

**Status: authorised, not performed.** No measurement has been run.

## S-T.4 — Retention, and its collision with gate 5

> **B.** Define a bounded rolling-retention rule now, but instantiate the window only after S-T.3
> measures its effect on usable volume. Before the number is fixed, verify that the resulting retained
> volume can still make Gate 5 attainable; if no reasonable window does so, return to owner decision
> rather than selecting a window that makes the gate unreachable.

**Established.** **Bounded rolling retention is the rule**, fixed now; indefinite device-bounded
retention is rejected. **The window is instantiated only after S-T.3's volume measurement.** Before
the number is fixed, **verify the retained volume can still make gate 5 attainable**. **Escalation
clause:** if no reasonable window leaves gate 5 attainable, **return to owner decision** — do not
select a window that makes the gate unreachable, and do not quietly relax the gate to fit the window.

**Consequences.** S-T.3's measurement has a **second consumer** — it sets the retention window as well
as satisfying gate 5's `[measured]` count, and both uses stay inside its authorised scope. The window
is a **pending number**, recorded as `[unknown]`, neither omitted nor estimated. S-T.1's local gate
cannot close until the window is instantiated.

## S-T.5 — One governance artifact, or two

> **A.** Unify under the existing S-3.6 datasheet as the single governance artifact type, with
> explicit static-source and live-source sections. Static-only fields must be schema-marked N/A for
> live tables rather than left blank; the live section must carry accrual, retention, provenance
> validation, permitted-use, and gate-state requirements. Do not introduce a second contract
> vocabulary or rename the ratified datasheet artifact.

**Established.** **One artifact type: the S-3.6 datasheet.** "Dataset contract" is not a separate
artifact and must not enter the vocabulary as one. Two explicit sections, **static-source** and
**live-source**. **Static-only fields are schema-marked `N/A` for live tables, never left blank.** The
live-source section must carry **accrual, retention, provenance validation, permitted use, and gate
states**. No rename of the ratified datasheet artifact, and no second vocabulary.

**Consequence.** S-3.6's datasheet schema takes **three additions** from this pass: S-7.1's Gold-A
eligibility field, the static/live section split, and the live-source field set. Gate states live in
the datasheet, which is where S-T.1's two-state visibility requirement is rendered.

---
---

# §A. Agent-authored appendix — **not part of the ruling**

> Added 2026-09-04 by the agent that ran the decision pass. **Nothing below is the owner's text.** It
> holds the standing principles the rulings instantiate, documentation errata, the obligations the
> rewrite inherits, and the open tasks. Per the Proposal Integrity Rules, **§A.3 (historical
> documentation corrections) is kept separate from §A.4 (normative rewrite obligations)** and the two
> must not be folded together when rev 3 is written: the corrections belong in an errata section, not
> interleaved with policy text.
>
> **Supporting detail.** The evidence blocks and per-decision consequence chains behind these
> rulings are preserved in
> [`2026-09-04-data-maturation-stage-decision-working-notes.md`](2026-09-04-data-maturation-stage-decision-working-notes.md)
> — agent working notes, superseded by this record wherever the two differ.

## A.1 Standing principles — state once in the rewrite, then cite

These were each ruled repeatedly on unrelated subjects. Re-deriving them per stage is how the
proposal grew inconsistent in the first place.

| # | Principle | Instances |
|---|---|---|
| **1** | **The qualifier travels with the datum.** A qualifying fact lives with the value it qualifies, not in surrounding prose | 8 — S-2.4/G4 · S-3.2 · S-4.2 · S-5.1 · S-5.2 · S-5.6 · S-7.1 · S-T.3 |
| **2** | **Selection rules are fixed before the data is seen.** *Corollary (S-7.4):* when a fixed rule must later change, **version it visibly rather than tune it silently** | 6 — S-2.4/G1 · S-2.9/K2 · S-4.1 · S-5.7 · S-6.1 · S-7.4 |
| **3** | **AI assists, AI never authorises** (DFD-8 instantiated) | 3 — S-2.2 · S-4.3 · S-5.5 |
| **4** | **When a bar is not met, revise or refuse; do not lower the bar.** *Routed form (S-T.4):* refuse, and say to whom — escalate to the owner rather than accommodate | 6 — S-2.7 · S-4.1 · S-4.4 · S-5.7 · S-7.2 · S-T.4 |
| **5** | **An unresolvable state is an error, never a default.** *Extended by S-T.2:* a defaulted field is an unresolvable state wearing a valid-looking value | 4 explicit — S-3.5 · S-3.7 · S-4.6 · S-T.2 (structurally also S-5.3) |

**Emerging pattern — the explicit `N/A` state.** Three rulings solve a *cannot-be-mistaken-for* problem
with the same device rather than with prose: S-T.1 (egress gate is `N/A`, not `closed`), S-T.2 (a
defaulted provenance column must be rejectable), S-T.5 (static-only fields are `N/A` for live tables).
General rule, worth stating once: **wherever absence is legitimate it gets an explicit value; blank is
reserved for "not yet supplied" and is always an error state.**

**Rule/number split — 5 instances.** Fix the rule now, instantiate the number after observation:
S-4.1 (sample rule / size) · S-5.7 (allocation rule / fraction) · S-6.1 (baseline formula / value) ·
S-8.1 (revival preconditions / threshold) · S-T.4 (retention rule / window).

**Manifest-as-attestation — 2 instances, one mechanism.** S-3's closing confirmation (canonical hash ·
export hash · generator identity) and S-4.6/S-5.6's `gold_a_v1` pin are the same shape for different
purposes. Present as one mechanism with two instances, not two inventions.

## A.2 Required artifact of the rewrite — "how to read a figure from this project"

**Escalated from suggestion to requirement.** The pass accepted **six** reader-facing
non-uniformities. Honouring six obligations inconsistently is worse than stating them once:

1. S-4.5 — two rule-attribution mechanisms inside Gold-A (inherited boundary vs explicit citation).
2. S-5.2 — two provenance grades inside Gold-R (derived vs recorded-at-creation).
3. S-6.2 — asymmetric headlines: TaskType reports a **proportion**, Difficulty reports a **distance**.
   **These two can never be combined into an overall score.**
4. S-7.1 — promotability read from a source's datasheet, not from its tier.
5. S-T.3 — figures carrying an explicit measurement-scope tag.
6. S-T.5 — datasheets with static and live sections, and `N/A` distinct from blank.

Two further reading rules belong in the same statement: **S-6.3** — an interval is required on every
figure, exclusion of chance is a one-sided guard and not validation; and **S-6.2** — a "re-baselined"
historical figure is *a new figure under the new registry*, not the same metric recomputed, so an old
and a new number must never be presented as a before/after.

## A.3 Documentation errata — factual corrections, **kept separate from policy**

`[measured]` unless marked otherwise. These are corrections of statements that were **false when
written**, not consequences of the new rulings. They belong in an errata section of rev 3.

| # | Location | Correction |
|---|---|---|
| 1 | §S-1 table | *"Two of the three largest transitions name retired classes"* — **false as placed**. `OnTap`→`NhacNho` is live inside §E.1's comparison. **Audit §J-2 carries the identical error** — see A.6 |
| 2 | §S-1 `[inference]` prose | Says *"the fourth item"* / *"the other three answers"*; the table has **five** rows |
| 3 | §S-1 table | *"Difficulty … is currently never trained on"* — **false**. True only of the text classifier; Difficulty has ≥4 live consumers (`MLModelManager.cs:94`, `StudyTimePredictorService.cs:58`, `PerformanceDropRiskEvaluator`, `DifficultyLabelLogs`) |
| 4 | §S-1 table | `KiemTraThuongXuyen` / `ThiCuoiKy` described as possibly *"aspirational"* — they are the **two largest training classes, 358/698 = 51.3%** |
| 5 | §8 | *"S-1 needs no infrastructure, no tooling and no data"* — **false** |
| 6 | §S-2 | *"Reserve the reproducibility batch before S-2 starts"* — **too late**; must be before S-1 (S-1.2) |
| 7 | §S-2 | *"Forty rows out of 208 is affordable"* — the live pool is **36 + 121 = 157**, in two pools with different meanings |
| 8 | §4.1 | *"Gold-A adjudication scope = 208 rows"* — measured against the **interim** label space, not production's |
| 9 | §4.1 | The 29.6% denominator is **703, not 1028** |
| 10 | §4.1 / §4.2 / §S-4 | *"208"* and *"~188"* are **dead numbers**. J-1 as originally defined is **36** production-relevant rows; the total distinct contested pool is **133** |
| 11 | §S-3 | *"File-level datasheets (8 files, 0 exist)"* — **wrong on both counts**. The governed set is audit Group A's 10 (+B1); **one datasheet already exists** (`datasheets/vn_input_fixtures.md`) |
| 12 | §S-3 | *"~5 new columns (7 → ~12)"* — understated. The real figure is **~8 new fields, 7 → ~15** |

## A.4 Normative rewrite obligations — consequences of the rulings

**Superseded or corrected text.**

- §S-4's **AI-assist paragraph** — superseded by S-4.3. Its claim that AI *"reduces the work"* must be
  restated: under commit-then-reveal the saving is ordering and clustering only.
- §S-4's **J-7 line** (`[unknown]` until J-1 runs) — superseded by S-4.4. The **count** stays
  `[unknown]`; the **policy** no longer is.
- §S-4's **exit criteria** — restated as a **manifest specification** (S-4.6/S-5.6), and extended with
  the **guideline-gap log** as a named S-4 exit artifact.
- §S-5's *"This track can start earliest and costs the least"* — **now false** (S-5.1). Track B carries
  a consent step and a transfer mechanism that does not exist.
- §S-6's *"Gold-A answers is the model consistent with the label definitions"* — **false** under S-5.4.
- §S-6's **averaging rule** — survives, but its stated reason is now wrong: post-S-5.4 only one of the
  two sets produces performance figures at all.
- §S-7's Silver paragraph (*"never promotable to Gold"*), synthetic paragraph, public track, gate list
  and exit criteria — all restated per S-7.1/7.2/7.3/7.4.
- §S-T's gate list — gate 2 splits into two independent gates with their state vocabularies; gates 1
  and 4 are **mutually dependent**, not sequential; gate 4's *"dataset contract"* becomes the S-3.6
  datasheet.
- §S-8's placeholder — replaced by the four preconditions, the hypothesis schema, the versioning
  clause, the explicit non-setting of a threshold, and the mandatory separate owner decision.

**Additions to ratified rule lists.**

- §5's tier ladder gains a **hard invariant** (not a threshold): **provenance recorded at creation**
  (S-5.2). It is binary and it is set now.
- §S-6's exclusion list gains a **third** entry: **no derived-provenance Gold-R row** (S-5.2).
- §4.2's Q-3 residual can be **closed** (S-5.1). §S-T's consent gate is no longer simply "open" — record
  the two-state form.
- §S-T's silent-failure block gains a second form: reporting S-T closed on the local gate alone.

**Version-stream hygiene.** Four distinct version streams now exist and must never be collapsed:
`GuidelineVersion` (S-2.11) · dataset version (DFD-5) · provenance-schema required-key-set version
(S-3.5) · hypothesis version (S-8.1). `LabelVersion` is disqualified (one value spanning four label
origins across 698 rows).

**Agent suggestion, not ruled.** S-8.1's four-element hypothesis schema (deficit / mechanism /
metric-contrast / direction) is not encoder-specific and reads as a **reusable pre-registration
template** for any future experiment. Recommend stating it once and having S-8 reference it. The owner
has **not** ruled on generalising it.

## A.5 Open tasks — these are tasks, not decisions

A ruled policy is not an implemented one. Nothing below is scheduled.

| Task | Source | State |
|---|---|---|
| **OD-4 licensing review** (ViLexNorm `NC`/`SA`, derivative use) | S-7.3 | Scheduled, blocking any ViLexNorm use; no Data Maturation deadline |
| **Telemetry volume/usability measurement** | S-T.3 | **Authorised, not performed** |
| **Retention window** | S-T.4 | `[unknown]` — instantiate after S-T.3, with the feasibility check |
| **Capture-complete provenance + gate-4 validation** on both telemetry tables | S-T.2 | Not started |
| **Frozen, versioned distribution check** with its `collected_v4` proof case | S-7.4 | Not started |
| **`collected_v4` Gold-A eligibility ruling** | S-7.1 | **Required, unruled** |
| **FU-1** — Difficulty consumer consistency check after the 1–5 anchors freeze | S-1.4 | Not scheduled |
| **FU-2** — governance for A8 / `SeedDataGenerator` in the M7 domain | S-3.6 | Not scheduled |
| **FU-3** — perceived-difficulty analysis: owner spec label vs user `FinalDoKho` | S-5.5 | Not scheduled |

**Costs surfaced but not priced — named, not absorbed.**

- **Track B's consent-and-transfer mechanism** is new scope the proposal does not contain (S-5.1).
- **Gold-R labelling effort scales linearly with collection volume** (S-5.5) and is absent from the
  cost model. `[inference]` This is plausibly the binding constraint on Track A/B volume.
- **S-7's cost model** loses its synthetic line item (S-7.2) and gains one engineering deliverable,
  the distribution check (S-7.4).

## A.6 Deliberately not done

- **No individual row text was read at any point in this pass.** Every finding is aggregate statistics,
  preserving the clean held-out reservation S-1.2/S-1.6 require before S-1 executes.
- **Q-1 was not performed.** It remains a scheduled measurement, not a result.
- **The proposal was not rewritten.** This record is its input.
- **The audit's §J-2 error (errata #1) was not corrected.** The audit is a separate document and
  editing it is scope expansion — **surfaced as an owner call**, not folded in.
- **No number was invented.** Where a figure is unknown it is recorded `[unknown]` — the retention
  window, J-7's count, the S-5.7 split fraction, Q-4's per-class floor, telemetry row counts.
