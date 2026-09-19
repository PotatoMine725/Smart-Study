# Epic 4 / Data Maturation — state reconstruction after the §10 human probe

**Date:** 2026-09-18
**Author:** agent, read-only reconstruction session
**Baseline:** `origin/dev` `6556923` (local `dev` identical, 0 ahead / 0 behind)
**Repository modifications made while reconstructing:** 0 — this report is the only file added.

---

## §0. Verdict

> **As written 2026-09-18; superseded 2026-09-18 — see §12.** The owner ruled `D-6`/`F-1` the same
> day this report was filed. The verdict below is left exactly as submitted; §12 records what
> changed and why the ruling supersedes rather than corrects it.

**The §10 reproducibility probe was performed. Its verdict is NOT established, and must not be
recorded as a PASS yet.**

The owner's Gold pass and one independent human pass over the sealed 20-row scored batch agree
**cell-for-cell on all four fields of all 20 rows**. The instrument itself verifies clean: frozen
`v1` still hashes to its anchor, all 20 tested rows hash into the sealed S-0 batch, and the committed
reader package passes 18/18 of its own validator checks.

**What blocks the verdict is `D-6`.** On 2026-09-05 the owner deliberately left the scoring treatment
of an `unresolved` response **unauthorised**, so that the instrument would not tell a reader whether
flagging a row was safe or costly. Both annotators then flagged `R-12` and `R-20`. That unruled
question is now **outcome-determinative**: under one of the two available constructions, `D-7`'s
**primary** 16-row Difficulty reading is **14/16 against a ≥ 15/16 threshold — a FAIL**.

`S-2.7`'s invariant — thresholds and procedure must not change after results are seen — means `D-6`
cannot simply be settled now as if nothing had been observed. It has to be ruled **and recorded as a
post-hoc ruling**, with that fact travelling on every figure it governs.

Two further cautions for anyone reading only this section:

- **Do not quote "20/20" on its own.** `D-7` pre-registered the 16-row figures as the *primary*
  evidence and states that both figures travel together, neither quoted alone.
- **"Epic 4" is being used for two different things.** The canonical Epic 4 is **ML Maturation**
  (`T4.1`–`T4.3`), which has **not started**. The annotation work below is **Data Maturation**
  (`S-0`…`S-8`, `S-T`), its upstream prerequisite. See §5.1.

---

## §1. Scope

What this report covers:

- the current state of the Data Maturation ladder and of Epic 4 proper;
- the completed 20-row human reproducibility probe and what it does and does not establish;
- whether `DFD-2`'s labelled-data gate is thereby satisfied;
- discrepancies between documents, and between the working tree and committed state.

What it does **not** do — and deliberately did not run:

| Not done | Why |
|---|---|
| Re-derive or re-score any row's label from the guideline | Would create a third annotation pass and contaminate the record |
| Fill `R-12` / `R-20` | Both parties independently declared them unresolved; §8 forbids suppressing that |
| Rule on `D-6` | Owner decision. This report scopes it, it does not take it |
| Edit, move or commit the two annotation sheets | They are evidence artifacts |
| Run the test suite / GitNexus re-analyze | Out of scope; no production code is involved |

---

## §2. Per-dimension verdict

`D-7` (ruled 2026-09-05, **before any annotation**) keeps the 20-row gate unchanged and pre-registers
a 16-row reading beside it as the **primary evidence**, excluding the four template twins
`R-01`, `R-09`, `R-14`, `R-16`. Both figures travel together.

> **Note on labels.** "Reading A" and "Reading B" below are *this report's* names for the two
> available constructions of the question `D-6` leaves open. They are **not** repository terms and
> must not be cited as though `D-7` named them.

**Reading A** — an agreed `unresolved` counts as an exact match:

| Set | TaskType | Threshold | | Difficulty | Threshold | |
|---|---|---|---|---|---|---|
| 20-row gate | 20/20 | ≥ 17/20 | **PASS** | 20/20 | ≥ 18/20 | **PASS** |
| 16-row primary | 16/16 | ≥ 14/16 | **PASS** | 16/16 | ≥ 15/16 | **PASS** |

**Reading B** — only a substantive label can exact-match; denominator unchanged:

| Set | TaskType | Threshold | | Difficulty | Threshold | |
|---|---|---|---|---|---|---|
| 20-row gate | 18/20 | ≥ 17/20 | **PASS** | 18/20 | ≥ 18/20 | **PASS** |
| 16-row primary | 14/16 | ≥ 14/16 | **PASS** (zero margin) | 14/16 | ≥ 15/16 | **FAIL** |

**Overall: NOT ESTABLISHABLE.** Three of the four dimensions pass under both readings. The 16-row
Difficulty dimension — the primary evidence for the primary dimension — splits, and `D-6` decides it.

---

## §3. Findings

### 3.1 The probe as performed `[observation]`

| | |
|---|---|
| Batch | Sealed 20-row scored batch — **confirmed**, 0 foreign rows, 0 duplicated |
| Gold / reference | Owner — `docs/s2-reader-package/02-annotation-sheet-gold.md` (untracked) |
| Independent reader | Human — `docs/s2-reader-package/02-annotation-sheet.md` (modified, uncommitted) |
| Instrument | `GuidelineVersion v1` as rendered in Vietnamese (`D-8`) |
| Independence | Confirmed — **owner-attested** `[ruling]`; no recruitment record in the repository (§5.5) |
| Raw agreement | TaskType 20/20 · Difficulty 20/20 · `decided_by` 20/20 · `unresolved` 20/20 |
| Unresolved rows | `R-12`, `R-20` — both parties, blank label, `unresolved=true` |

### 3.2 Mandatory companions — the figures that must travel with every quoted result

**`G4` — batch composition.** Matches pre-registration exactly:
12 contested (`cross_only` 1 · `third_only` 9 · `both` 2) + 8 Difficulty-spread
(L1 1 · L2 1 · L3 2 · L4 2 · L5 2).

**`G2` — per-stratum breakdown** (Reading A):

| Stratum | n | TaskType | Difficulty | Agreed-unresolved |
|---|---|---|---|---|
| contested / `both` | 2 | 2/2 | 2/2 | — |
| contested / `cross_only` | 1 | 1/1 | 1/1 | — |
| contested / `third_only` | 9 | 9/9 | 9/9 | — |
| Difficulty-spread L1 | 1 | 1/1 | 1/1 | `R-20` |
| Difficulty-spread L2 | 1 | 1/1 | 1/1 | — |
| Difficulty-spread L3 | 2 | 2/2 | 2/2 | `R-12` |
| Difficulty-spread L4 | 2 | 2/2 | 2/2 | — |
| Difficulty-spread L5 | 2 | 2/2 | 2/2 | — |

**`C4` — per-boundary detail** (labelled rows): `B-1` 11/11 · `B-2` 1/1 · `B-4` 2/2 · `B-5` 1/1 ·
`B-6` 3/3. **`B-3` was never exercised by this batch** — the probe says nothing about it.

**`D-2` diagnostic:** `shared_label_cross_pass` 3/3 · `third_pass_forced` 9/9 — matches the
pre-registered 3 + 9 exactly.

**Level-1/2 caveat:** assigned Difficulty spans only {3, 4, 5}; `v1`'s threshold governs levels 3–5.
The two agreed-unresolved rows are `R-20` (S-0 stratum L1) and `R-12` (L3).

**`G3` scope:** this is reproducibility of `v1` *as rendered in Vietnamese*, against the owner's Gold
pass, on a 20-row sealed batch. It is **not** corpus-wide agreement, **not** ML accuracy, **not**
dataset validation, and **not** evidence about what a future annotator would do.

### 3.3 The instrument verified sound `[observation]`

| Check | Result |
|---|---|
| Frozen `v1` (`docs/specs/annotation-guideline.md`) | `dd4fc273…684a433` — matches the anchor **and** the anchor commit `da98e73` |
| Reader-package copy of `v1` | Byte-identical to the above |
| Vietnamese rendering | `3b1dbd24…ca61c` — matches the ratified anchor |
| All 20 tested rows | Hash into the sealed S-0 `scored_batch` |
| Reader-package validator, committed package | **18/18 PASS** |

**The guideline was not modified after the result.** This is verified by recomputed hash, not asserted.

### 3.4 The result is not recorded anywhere `[observation]`

Every **committed** document still reads `§10 NOT performed`, `no reader recruited`,
`DFD-2 NOT satisfied` — the freeze record's "gates that remain shut" table, `s2-owner-decisions`
§preamble and §138, `v1` §10 and §13, and the coverage-expansion plan at L1280 / L1319 / L1400.

The probe exists **only** as uncommitted working-tree files. No results document, no evidence record,
nothing committed. `[fact]` The gap is that the event is unrecorded — not that either side is wrong.

### 3.5 `DFD-2`

| | |
|---|---|
| Reproducibility gate | **UNCLEAR.** The rule is that `DFD-2` is satisfied *only after the §10 test passes*. Whether it passed is not establishable while `D-6` is unruled, so the gate cannot be declared satisfied — nor failed |
| Downstream authorization | **NONE established.** `[fact]` Even a clean PASS would lift **only** the §10 gate. No repository evidence authorizes S-3 storage work, labelled-data collection / import / generation, or any promotion |
| Maturity invariants | `I-1` / `I-2` / `I-3` were all false as of 2026-08-27, and nothing in the working tree changes that |

Modelled on the freeze record's own *"What ratification does not do"*: the probe performs the test.
It does not rule `D-6`, does not record a result, and does not open a downstream stage.

---

## §4. Lifecycle map

| Stage | State | Evidence |
|---|---|---|
| Data audit / corpus provenance (`DFD-1..9`, `Q-1..Q-5`) | **CLOSED** (decisions) | rev 3 authorized |
| `S-0` reservation | **SEALED, UNTOUCHED** | `datasheets/reservations/2026-09-04-s0-reservation-snapshot.json`, commit `5c0047b`, seal `5b4a0eae…` |
| `S-1` taxonomy review | **CLOSED** | commit `913e5bb` |
| `S-2` specification | **AUTHORED + RATIFIED FOR TESTING + FROZEN** | commit `da98e73` |
| `GuidelineVersion v1` | **FROZEN, VERIFIED INTACT** | hash recomputed 2026-09-18 |
| Vietnamese rendering (`D-8`) | **RATIFIED** 2026-09-05 | anchor verified |
| Reader package / instrument | **SOUND AS ISSUED** | validator 18/18 on committed package |
| 20-row sealed scored batch | **CONFIRMED AS TESTED** | all 20 rows hash into `scored_batch` |
| Owner Gold pass | **PERFORMED, UNCOMMITTED** | `02-annotation-sheet-gold.md` (untracked) |
| Independent human probe | **PERFORMED, UNCOMMITTED** | `02-annotation-sheet.md` (modified) |
| Reproducibility **result** | **COMPUTED, NOT ESTABLISHED, NOT RECORDED** | blocked on `D-6` |
| `S-3` provenance / lineage | **DECISIONS CLOSED, IMPLEMENTATION NOT STARTED** | — |
| `S-4` / `S-5` / `S-6` / `S-T` | **DECISIONS CLOSED, NOT PERFORMED** | — |
| **Epic 4 proper** (`T4.1`/`T4.2`/`T4.3`) | **NOT STARTED** | `docs/plans/2026-07-03-master-plan.md` |

The distinction that matters throughout: **specification exists** ≠ **specification passed its test**
≠ **implementation complete** ≠ **downstream data may be promoted.** Decisions being *Closed* in the
stage-decision table means the *rulings* are settled — not that the stage was built.

---

## §5. Discrepancies

Reported, not corrected. No document was rewritten.

### 5.1 Scope — "Epic 4" names two different things

The canonical master plan (`docs/plans/2026-07-03-master-plan.md` L252ff, as amended 2026-08-24)
defines **Epic 4 = ML Maturation**: `T4.1` M8-C retrain, `T4.2` difficulty model, `T4.3` M9 NL
deadline parsing — executing **last**, and not started. The annotation/corpus work is the **Data
Maturation** ladder. **Authoritative: the master plan.** Anyone filing this probe under "Epic 4
progress" will be wrong on both halves.

### 5.2 Verdict — premise vs evidence

The session's framing premise was "S-2 §10 PASS (20/20, 20/20)". This is a premise-vs-evidence gap,
not a document-vs-document one. The repository's own pre-registered rulings do not support asserting
it: `D-6` leaves `unresolved` scoring unauthorised, and under Reading B the primary 16-row Difficulty
reading fails. **Authoritative: `docs/plans/2026-09-05-s2-owner-decisions.md` (`D-6`, `D-7`)**, which
`v1` §14 locks against post-hoc change.

### 5.3 Reporting rule

Quoting "20/20" alone breaches `D-7` ("both figures travel together, neither may be quoted alone")
and `G4`/`G2`/`C4`. Any record of this result must carry §3.2 in full.

### 5.4 Committed state vs working tree

See §3.4. Authoritative for *decisions*: the committed records. Authoritative for *what happened*:
the working-tree artifacts plus owner attestation. Both are true simultaneously.

### 5.5 Independence record-keeping

Independence is **owner-attested and taken as given**. What the repository lacks is the paperwork,
which matters for whoever records this result:

- no recruitment record for the annotator anywhere in `docs/` — the freeze record's
  *"Independent reader: NOT recruited"* row is still the committed state;
- neither sheet declares the `role: gold | probe` field that the committed English blind-reader sheet
  spec carries (`docs/specs/2026-09-05-s2-blind-reader-sheet.md`), required by `v1` §12;
- file mtimes are weak ordering evidence `[inference]` — the gold sheet's mtime is 2026-09-11; the
  returned sheet's mtime is today, so it does not date the pass. The in-sheet date field reads
  `12/09/2026`.

An owner-authored evidence record would close all three.

### 5.6 Instrument hygiene — the package currently fails its own validator

`C1`, `C4` and `C12` fail against the working tree. **Isolated:** rebuilding the package from
committed blobs gives **18/18 PASS**. All three failures are caused by the probe artifacts living
inside `docs/s2-reader-package/` — the gold sheet is a stray 5th file (`C1`, `C12`) and the sheet's
item blocks now carry answers (`C4`). This is a **housekeeping consequence, not an instrument
defect** — but the validator is designed to police exactly this, so returned evidence should not be
stored in the package directory.

### 5.7 Minor — scoring key's sheet-level hash is stale

The scoring key's recorded `reader_sheet.sha256` (`151d3f80…`) predates the ratified Vietnamese
revision and no longer identifies the issued sheet. The item-block-level check (`C4`) still holds, so
row identity is unaffected. `[fact]`

### 5.8 Stratification labels are not Gold

S-0's per-row `difficulty` values are **historical pass labels used for sampling**, not reference
labels. Divergence between them and the annotated Difficulty is expected and is **not** a defect —
`v1` §10 states S-2 measures against the current taxonomy, not agreement with historical pass labels.
Flagged because it is an easy misreading for the next reader.

---

## §6. Verification

Commands and checks actually run, all read-only:

| Check | Result |
|---|---|
| `sha256sum` of `v1`, reader copy, VN rendering vs recorded anchors | All three match |
| `git show da98e73:docs/specs/annotation-guideline.md \| sha256sum` | Matches anchor |
| SHA-256 of all 20 sheet row texts vs `scored_batch` hashes | 20/20 matched, both sheets |
| Field-by-field diff, gold vs returned sheet (4 fields × 20 rows) | 80/80 identical, 0 diffs |
| `python tools/data-maturation/s2_reader_package_validate.py` (working tree) | FAIL — 3 of 18 (`C1`, `C4`, `C12`) |
| Same validator against package rebuilt from `HEAD` blobs in scratchpad | **PASS — 18/18** |
| `git fetch` + `git rev-list --left-right --count dev...origin/dev` | `0  0` |

**Not run:** test suite, build, GitNexus re-analyze — no production code is in scope.

**Working tree:** dirty before this session and left exactly as found. Both annotation sheets are
byte-unchanged (`02-annotation-sheet.md` → `e3bf1102…`, `02-annotation-sheet-gold.md` → `d7102896…`).

---

## §7. Findings that outlive this decision

These stand regardless of how `D-6` is ruled, and should be lifted somewhere permanent rather than
left only here:

1. **`B-3` has never been exercised.** No row in the sealed scored batch triggers it. Whatever §10
   concludes, it concludes nothing about `B-3`. Candidate for `docs/knowledge/` or the S-4 authored-
   samples backlog.
2. **Agreed-unresolved rows are evidence about the specification.** `R-12` and `R-20` were flagged
   independently by both annotators — §8's "a row the spec cannot resolve is evidence about the spec"
   case, occurring for real. That finding survives any scoring convention.
3. **A pre-registered instrument can still leave an outcome-determinative question open.** `D-6` was
   a deliberate, well-reasoned silence that became the deciding variable. The lesson generalises:
   pre-registration should enumerate how *every* permitted response value scores, including the
   escape hatch.
4. **The validator earns its keep.** It caught the package-hygiene regression immediately and its
   `C11` check confirmed the `D-6` silence held in the shipped instrument.

---

## §8. Follow-ups

| # | Item | Status | Owner | Where it belongs |
|---|---|---|---|---|
| F-1 | Rule `D-6` — how an `unresolved` response scores — and record it **as a post-hoc ruling** | **needs a new owner decision** (blocking) | Owner | Appended to this report, or a new `s2-owner-decisions` entry |
| F-2 | Record the §10 result with its §3.2 companions once `D-6` is ruled | deferred until F-1 | Owner / agent | New report + `CHANGELOG` |
| F-3 | Author an evidence record for the probe (annotator, role, dates, custody) | recommendation | Owner | `docs/reports/…-observation.md` (owner-authored, exempt from report format) |
| F-4 | Decide whether to commit the two annotation sheets, and where they live | **needs a new owner decision** | Owner | Evidence location outside `docs/s2-reader-package/` |
| F-5 | Restore `docs/s2-reader-package/` to the blank 4-file instrument once F-4 is settled | deferred until F-4 | Owner / agent | Working tree |
| F-6 | Lift the `B-3` gap and the §7 lessons into `docs/knowledge/` | knowledge only | Agent | `docs/knowledge/` |

None of the above is committed work. F-1 and F-4 require the owner; the rest wait on them.

---

## §9. Decisions made

### 9.1 Report the verdict as NOT ESTABLISHED rather than as the PASS the session was framed around

**Why it had to be made.** The session opened with the result stated as a PASS and asked for a state
reconstruction around it. Reading `D-6` and `D-7` showed the repository's own pre-registered rulings
do not support that assertion, and that the gap is outcome-determinative rather than pedantic.

**What it's for.** A documentation-housekeeping pass runs next and will record whatever this report
establishes. Recording a PASS here would launder an unruled question into settled fact, inside the one
measurement `S-2.7` exists to protect.

**Experience for future development.** When a task supplies its own conclusion, the conclusion is a
premise to be checked against the repository, not a finding to be dressed. The check cost one file
read; getting it wrong would have been near-unrecoverable, because a recorded PASS becomes the thing
later work cites.

### 9.2 Compute and report both scoring readings instead of picking one

**Why it had to be made.** `D-6` leaves the question open and `S-2.7` forbids settling procedure after
results are visible. Choosing a reading would have been exactly the post-hoc adjustment the invariant
prohibits — and would have been invisible in the output.

**What it's for.** It converts an unanswerable question into a precisely scoped owner decision: three
of four dimensions pass either way, and the ruling decides exactly one number.

**Experience for future development.** When a pre-registered rule is silent, report the fork and name
what each branch implies. That is more useful than a confident single figure and it keeps the decision
with the person entitled to take it.

### 9.3 Verify the batch by hashing rather than trusting the sheet's row ids

**Why it had to be made.** The sheets identify rows only as `R-01`…`R-20`; neither carries the
`sha256` + `file:line` identity that §9/§12 require of a label. Without an independent check there was
no evidence the tested rows were the sealed rows.

**What it's for.** It upgrades "the sheet says these are the rows" to "these rows hash into the sealed
batch" — and independently reproduced what the project's own validator asserts via `C5`/`C8`.

**Experience for future development.** Two instruments agreeing beats either alone. Writing the check
separately and then finding the validator already covered it was worth the duplication: it tested the
validator as much as the batch.

### 9.4 Isolate the validator failure instead of reporting it as a finding

**Why it had to be made.** The package fails 3 of 18 checks today. Reported flat, that reads as an
instrument defect and would cast doubt on the whole probe.

**What it's for.** Rebuilding the package from committed blobs gave 18/18, which located the cause
precisely: the evidence artifacts are sitting inside the instrument directory.

**Experience for future development.** A red check on a dirty tree is not a finding until it has been
run against a clean baseline. The isolation cost one command and changed the conclusion from "the
instrument may be compromised" to "move two files".

---

## §10. Evidence

| Artifact | Role |
|---|---|
| `docs/specs/annotation-guideline.md` | Frozen `v1` — `dd4fc273…684a433`, verified today |
| `docs/s2-reader-package/01-annotation-guideline-v1.md` | Byte-identical copy |
| `docs/s2-reader-package/01b-huong-dan-tieng-viet.md` | VN instrument — `3b1dbd24…ca61c`, verified |
| `docs/s2-reader-package/02-annotation-sheet.md` | **Returned independent probe** (uncommitted) |
| `docs/s2-reader-package/02-annotation-sheet-gold.md` | **Owner Gold pass** (untracked) |
| `docs/plans/2026-09-04-s2-v1-freeze-record.md` | Freeze anchor; "gates that remain shut" |
| `docs/plans/2026-09-05-s2-owner-decisions.md` | `D-6`, `D-7`, `D-8`, `D-9` |
| `docs/plans/2026-09-04-s0-reservation-preregistration.md` | Reservation authority + composition |
| `datasheets/reservations/2026-09-04-s0-reservation-snapshot.json` | Sealed batch, seal `5b4a0eae…` |
| `datasheets/reservations/2026-09-05-s2-scoring-key.json` | id ↔ hash allocation; holds **no** label, **no** Gold answer |
| `datasheets/reservations/2026-09-05-s2-reader-package-manifest.json` | `D-7` secondary reading; blocking finding |
| `tools/data-maturation/s2_reader_package_validate.py` | 18 checks |
| `docs/plans/2026-07-03-master-plan.md` | Canonical Epic 4 definition |
| `docs/plans/2026-09-04-data-maturation-stage-decision-outcomes.md` | `S-1`…`S-8` decision closure |

**Evidence-integrity note.** The two annotation sheets are primary evidence. They must not be
edited, normalised, reformatted or "tidied"; `R-12` and `R-20` must not be filled in; and the result
must never be recomputed by changing the independent reader's record. The comparison that counts is
*independent reader vs the owner's pre-existing Gold pass* — never against labels revised after
seeing the outcome.

---

## §11. Do not repeat

- **Knowledge distillation — already completed.** `docs/reports/2026-07-12-a3-knowledge-distillation.md`
  and `docs/reports/2026-08-19-epic3-knowledge-distillation.md`. No further KD is required.
- Do not re-run, re-score or re-derive the 20-row probe, and do not re-annotate any row.
- Do not re-draw the sealed batch or the §6 catalogue (§14 forbids both).
- Do not re-reserve or spend the `Q-1` timed-adjudication or clean-retest partitions.
- Do not bump `v1` — a bump requires a **recorded failure**, and none exists.
- Do not re-ratify the Vietnamese rendering — `D-8` was discharged 2026-09-05.
- Do not update `docs/architecture/*` — `DOC-03` requires those wait until S2 + S3 **ships**.

---

## §12. Owner ruling

**Appended 2026-09-18. Nothing above this heading is changed** — §0's verdict stands exactly as
submitted; it is *superseded*, not rewritten, by the ruling below.

The owner ruled `F-1` on `D-6`, verbatim:

> D-6 = Reading A.
>
> When the Owner Gold annotation and the independent human reader both record `unresolved=true` for a
> row because Guideline v1 cannot resolve that row, that shared `unresolved` outcome counts as an
> exact agreement for §10 reproducibility scoring.
>
> Therefore the §10 human probe result is PASS under Reading A.

**Recorded in full**, with its semantic effect and what it deliberately does not do, as `F-1` in
[`../plans/2026-09-05-s2-owner-decisions.md`](../plans/2026-09-05-s2-owner-decisions.md) — the D-series'
canonical home; that is the entry to cite for the ruling itself. `R-12` and `R-20` remain
`unresolved=true`, unedited, in both sheets.

**Result, formally recorded with its full `G2`/`G4`/`C4`/`D-2` companions**, per `D-7` ("both figures
travel together, neither may be quoted alone") and this report's own §3.2:
[`2026-09-18-epic4-s2-section10-result.md`](2026-09-18-epic4-s2-section10-result.md).

**§0's "NOT ESTABLISHED" and §2's "NOT ESTABLISHABLE"** were correct as of 2026-09-18 before this
ruling and are superseded by it, not corrected — see the Lifecycle convention in
`docs/reports/README.md`. Under Reading A, §2's own table already carries the PASS figures; what
changed is that the fork is now resolved rather than open.

`DFD-2`'s status following this ruling is recorded separately, appended to
[`../plans/2026-09-04-s2-v1-freeze-record.md`](../plans/2026-09-04-s2-v1-freeze-record.md) — this
report's §3.5/§5 explicitly scoped that as *not* this report's decision to take, and the narrower
question of what `F-1` does and does not unlock downstream is answered there, not here.
