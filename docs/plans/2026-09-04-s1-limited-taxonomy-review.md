# S-1 — Limited taxonomy review (P-3)

**Status: `accepted`.** Accepted by the owner on **2026-09-04**, with four scope clarifications
recorded at §6. Executed 2026-09-04 under
the owner authorization of rev 3 as the governing executable plan, after **S-0 sealed the reservation**
([snapshot](../../datasheets/reservations/2026-09-04-s0-reservation-snapshot.json),
[pre-registration](2026-09-04-s0-reservation-preregistration.md)).

S-1 executes only the already-ratified questions of **S-1.0 – S-1.6**. It proposes no taxonomy change,
reopens no ratified decision, and does not reconcile the sealed 119-vs-121 residual.

---

## 1. Evidence boundary

S-1's entry criterion is that the reservation has executed and **S-1 reads from the remainder, never
from the reserved rows** (S-1.2, hardened by S-1.6). The boundary below was enforced **by construction**
— every selection filtered against the 60 sealed hashes before any text was rendered — not by intention.

### 1.1 The distinction that governs everything in this section

| Grade | What it means here | Permitted against reserved rows? |
|---|---|---|
| **Label-level metadata** | The `TaskType` / `Difficulty` values, row counts, and transition tallies | **Yes.** S-0's own ratified derivation read exactly this to build the pool; a reservation that could not be derived could not exist |
| **Row text** | The Vietnamese `InputText` / `VanBanGoc` content | **No.** This is the inspection S-1.2 orders to happen after reservation |

Every statistic in this document is **label-level** unless it appears in §1.3, which lists the complete
set of rows whose **text** was rendered.

### 1.2 What was available and what was used

| Set | Size | Used |
|---|---|---|
| Master contested pool (owner ruling 2026-09-04) | **133** distinct rows | — |
| Reserved by S-0 | **60** | **Text never rendered.** Excluded by hash before any read |
| **Unreserved remainder — S-1's text budget** | **89** | **25 read.** 64 deliberately not read (§1.4) |
| Third-pass evidence admitted by S-1.3 | **121** relabelled rows | Label-level only |

### 1.3 Exact rows and sources inspected as text

**Sources** (all committed, read-only; none modified by S-1):

| Pass | File | Label column | Read as |
|---|---|---|---|
| 1 | `datasheets/normalized_dataset.csv` | `LoaiTask` | labels + text of 25 rows |
| 2 | `datasheets/normalized_dataset_m8a.csv` | `TaskType` | labels + text of 25 rows |
| 3 | `datasheets/normalized_dataset_m8a_uniform.csv` | `TaskType` | **labels only** |
| — | `docs/CHANGELOG.md` (2026-06-05 / seed v3 entries) | — | provenance of pass 3 |
| — | `docs/architecture/pipeline.md` | — | production enum + Difficulty fallback prior |

**The 25 rows whose text was rendered** are exactly the unreserved **cross-pass** rows — the rows where
both passes chose from the *same* label set:

| Boundary | Rows read | Distinct templates behind them |
|---|---|---|
| `ThiCuoiKy` ↔ `BaiTap` | 17 | **9** |
| `ThiCuoiKy` ↔ `KiemTraThuongXuyen` | 6 | **3** |
| `KiemTraThuongXuyen` ↔ `BaiTap` | 2 | 2 |
| **Total** | **25** | **14** |

**25 rows are not 25 independent observations** `[measured]`. They collapse to **14 distinct sentence
templates** under subject-name substitution — one family of 5, one of 4, one of 4, one of 2. Corpus-wide
the same measurement gives **687 skeletons over 1028 rows, 44% of rows sharing a skeleton with at least
one other**. Every count in §4 therefore travels with its template count, and no ruling below rests on
row multiplicity alone.

### 1.4 What was deliberately **not** read, and why

The **64 unreserved third-pass rows** were **not** rendered as text. Once §3.1 establishes from labels
alone that every third-pass relabel is a target-class-availability move, row text cannot change the
cause ruling — and reading it would expose S-1 to forming an opinion on whether the
`ThiGiuaKy`/`DoAnCuoiKy` subdivision was correct, **which S-1.3 explicitly forbids S-1 to rule on**.
Not reading them is the stronger evidence record, and it is recorded here as a deliberate choice rather
than an omission.

The **2 `NhacNho→DoAnCuoiKy`** rows were likewise not read. They are non-production on the pass-2 side
and fall outside S-1.6's *production-relevant* scope.

---

## 2. Provenance of the third pass `[measured]`

Before any ruling: the pass-3 file's production-class distribution is

`BaiTap 124 · DoAnCuoiKy 131 · KiemTraThuongXuyen 188 · ThiCuoiKy 170 · ThiGiuaKy 85` — **total 698**

which matches, exactly and class for class, the seed v3 counts published in `docs/CHANGELOG.md`
(*"698 rows, all 5 enum classes (KiemTra 188 / ThiCuoiKy 170 / DoAnCuoiKy 131 / BaiTapVeNha 124 /
ThiGiuaKy 85)"*, `BaiTap → BaiTapVeNha` being the datasheet→enum rename).

`[fact]` **The third pass is not an independent annotation pass.** It is the **seed v3 coverage repair**,
and the CHANGELOG describes what it did in its own words: the prior seed *"covered only 3/5 classes
because the datasheet mis-labeled 'giữa kỳ'→ThiCuoiKy and 'đồ án'→BaiTap"*, and v3 fixed this *"by
relabeling the contradictions and synthesizing the two classes."*

This does not alter S-1.3 — the third pass remains **evidence, not a new question**. It establishes what
*kind* of evidence it is, which §3.1 needs.

---

## 3. Ruling on the cause fork (S-1.6)

S-1.6 requires **a separate cause ruling for each production-relevant contested boundary**, forbids one
global verdict, and warns against assuming the agent's two-axis reading. There are **seven** such
boundaries.

### 3.1 The precondition finding: four boundaries are outside the fork

`[measured]` **121 of 121** third-pass relabels move a row **into a class that did not exist in pass 2.**
The classes present in pass 3 but absent from pass 2 are exactly `DoAnCuoiKy` and `ThiGiuaKy`; the
classes present in pass 2 but absent from pass 3 are **none**.

| Third-pass transition | Rows | Target existed in pass 2? |
|---|---|---|
| `BaiTap → DoAnCuoiKy` | 93 | **No** |
| `ThiCuoiKy → ThiGiuaKy` | 23 | **No** |
| `KiemTraThuongXuyen → ThiGiuaKy` | 2 | **No** |
| `NhacNho → DoAnCuoiKy` | 2 | **No** *(non-production, out of S-1.6 scope)* |
| `ThiCuoiKy → DoAnCuoiKy` | 1 | **No** |

**This is the audit's own forced-move rule applied symmetrically.** The cross-pass derivation already
excludes `Khac`/`DuAn` transitions on the ground that *the old label did not exist in the new taxonomy*,
so the transition carries no information about annotator judgment. The third-pass join is the exact
inverse: **the new label did not exist in the old taxonomy.** Pass 2's annotator could not have chosen
`DoAnCuoiKy` — the option was not on the form.

**Ruling.** For the four third-pass boundaries, **the S-1.6 fork does not apply**, because its
precondition — two annotators choosing from a shared label set — is absent. These rows are
**forced-move-equivalent**, not disagreement. This discharges S-1.6 for those boundaries and rules
nothing whatever on whether the subdivision was correct.

### 3.2 The three genuine boundaries

| # | Boundary | Rows (total / unreserved / templates read) | **Cause ruling** |
|---|---|---|---|
| **A** | `ThiCuoiKy` ↔ `BaiTap` | 26 / 17 / 9 | **Mixed — ruled separately below** |
| **B** | `ThiCuoiKy` ↔ `KiemTraThuongXuyen` | 8 / 6 / 3 | **Mixed — ruled separately below** |
| **C** | `KiemTraThuongXuyen` ↔ `BaiTap` | 2 / 2 / 2 | **Annotation inconsistency — provisional** |

**A — `ThiCuoiKy` ↔ `BaiTap`, the audit's "one genuine collision".** Splits cleanly in two:

- **13 of 17 — taxonomy semantics.** The text describes a *large graded project* (`bài tập lớn`,
  `đồ án`, with a `demo`, a `báo cáo`, group coordination). Neither pass had a class for it: one
  annotator reached for `ThiCuoiKy` (an end-of-term graded deliverable), the other for `BaiTap` (an
  assignment). **Pass 3 moved all 13 to `DoAnCuoiKy` once that class existed** — which is the
  confirmation, not a coincidence. No guideline could have made these two annotators agree, because the
  correct answer was not available to either.
- **4 of 17 — annotation inconsistency.** The text says `bài tập nhóm` plainly and pass 3 kept `BaiTap`;
  pass 1's `ThiCuoiKy` is simply a misapplication. A written rule fixes these.

**B — `ThiCuoiKy` ↔ `KiemTraThuongXuyen`.** Also splits:

- **2 of 6 — taxonomy semantics.** The text says `giữa kỳ` (*mid*-term); pass 1 chose `ThiCuoiKy`
  (*final*). `ThiGiuaKy` did not exist. Pass 3 moved both to `ThiGiuaKy`. Same shape as A.
- **4 of 6 — annotation inconsistency, from one template.** `[inference]` The class appears driven by a
  **lexical trigger rather than the task described**: the rows read *"nộp báo cáo … cần **kiểm tra** lại
  format trước khi gửi"* — `kiểm tra` here is the verb *to check*, not the noun *a test*. The task is
  submitting a report. **Pass 3 kept `KiemTraThuongXuyen`**, so this survives into the current seed.
  Evidence strength is **one template with four subject substitutions**, not four independent rows.

**C — `KiemTraThuongXuyen` ↔ `BaiTap`.** Both rows say `bài tập (về nhà)` and both later passes agree on
`BaiTap`; pass 1 is wrong. Ruled **annotation inconsistency**, marked **provisional** — n=2 is too small
to rule confidently. `[inference]` Both rows are in a degraded register (no diacritics, slang, typos:
*"mon ktvm"*, *"sao kho wá"*); that register may be the driver, but with n=2 that is a **hypothesis for
S-2 to test, not a finding**.

### 3.3 Consequence for S-2 — reported, not acted on

`[observation]` §3.1 reclassifies most of the pool as forced-move-equivalent. The reserved batches were
drawn before that was known, and their composition is therefore weighted toward those rows:

| Reserved batch | Contested rows | of which third-pass-only |
|---|---|---|
| S-2 scored batch | 12 | **9** (+2 in both strata) |
| Q-1 timed adjudication | 20 | **15** (+3 in both strata) |
| Clean retest | 12 | **9** (+2 in both strata) |

**Nothing is done about this here.** The snapshot is not reopened, no pool is recomputed, no
re-reservation is proposed. **Ruled at acceptance (D-2): the reservation stands and the stratum is not
re-reserved**; the composition instead travels with every S-2 figure as a diagnostic. See §6.

---

## 4. Rulings on the remaining four scope items

### 4.1 Retired-class transitions — **confirmed** (S-1.1)

`OnTap` and `NhacNho` **do not return as production classes**. The production enum is and remains
**`BaiTapVeNha` · `KiemTraThuongXuyen` · `ThiGiuaKy` · `ThiCuoiKy` · `DoAnCuoiKy`**.

**Reminder-ness is relocated, not deleted.** It is a **derived attribute and a surface**, not a task
class: a row can *be* a `BaiTapVeNha` and *carry* a reminder. **S-2 must not re-litigate it as a missing
class.** Stated explicitly here so that S-2 has the sentence to cite.

### 4.2 The one genuine collision — boundary stated in words

For an annotator to apply, the `BaiTapVeNha` / `DoAnCuoiKy` line — the line whose absence caused
boundary A:

> **`DoAnCuoiKy`** when the work is a **multi-part deliverable produced over weeks**, typically named
> `đồ án` / `bài tập lớn` / `BTL` / *project*, and typically carrying more than one artifact (report,
> demo, source, defence) or a group. **`BaiTapVeNha`** when it is a **single piece of coursework
> completed and handed in**, however large. The discriminator is **multi-part-and-sustained**, not size,
> and not whether the word `nhóm` appears.

And the `kiểm tra` rule that boundary B requires:

> **`kiểm tra` used as a verb** (*to check, to re-check, to verify*) **does not make a row
> `KiemTraThuongXuyen`.** Classify on the task the text describes, not on a keyword's presence.

### 4.3 Difficulty — the 1–5 anchors (S-1.4)

**Why anchors are needed, from evidence** `[measured]`:

- **Difficulty was applied as a near-function of `TaskType`**, not as an independent scale:
  `KiemTraThuongXuyen` sits at level 3 in **120 of 121** rows; `OnTap` occupies only levels 2–3.
- **The scale drifted between passes without any rule changing**: level 5 fell 184 → 116, level 4 rose
  79 → 147, level 1 fell 57 → 30. Top transitions are `5→4` (50), `3→4` (41), `1→3` (27), `4→2` (23).
  Systematic movement in one direction is **anchor absence, not carelessness**.
- **Level 3 is a refuge**: 567/1028 then 579/1028 rows sit there.

**The anchors.** Difficulty measures **scope of work × consequence of missing it**. It is
**ordinal, not interval** — the gap between 4 and 5 is not the gap between 1 and 2.

| Level | Anchor an annotator can apply |
|---|---|
| **1** | A single action, one sitting, **no preparation and nothing handed in** — remember, bring, check a time |
| **2** | One short piece of work, roughly **under two hours in a single sitting**, no coordination, little weight |
| **3** | **A normal graded task** needing one or two sessions of preparation. The unmarked default *only when the text gives no scope or stakes signal* |
| **4** | **Multi-session work**, or a deliverable with **several components** (report *and* demo *and* source), or work requiring **group coordination** |
| **5** | **High stakes and substantial scope together** — end-of-term assessment the course outcome rides on, or a sustained project at final defence |

Three rules travel with them:

1. **Difficulty must not be derived from `TaskType`.** A class-based prior is a *fallback for missing
   data*, never an annotation rule. The corpus shows what happens otherwise.
2. **Level 3 requires a positive reason** when the text carries any scope or stakes signal. "No strong
   signal" is the only admissible ground for 3.
3. **The v1 threshold governs levels 3–5 only.** Levels 1–2 are represented so the scale spans its range,
   but per S-2.3 the level-1/2 gap is **declared, not solved**, and no v1 figure may be quoted as
   governing them.

**These anchors were written without reading a single row of Difficulty evidence beyond label-level
statistics — deliberately.** They are a *definition*, and validating a definition against the rows it
will be scored on is how a spec is tuned to its own test set. They face the reserved scored batch
**unvalidated**, which is exactly what makes that batch a test.

S-1 **does not audit or change current consumers**; the `(DoKho/5)*60` and downstream consistency check
remains **FU-1**, unscheduled.

### 4.4 Two classes with no evaluation data (S-1.5)

Kept and **reframed onto product-intent grounds**: `KiemTraThuongXuyen` and `ThiCuoiKy` — the two
largest training classes at **358/698 = 51.3%** — are retained because they name distinct things a
student schedules differently. **The evaluation defect is not ruled here; it is S-6's** (file-level
split, AI-generated test set, 3-of-5 class coverage, zero train/test text overlap).

---

## 5. Guard on the sealed residual

§3.1's finding is about **all 121** third-pass relabels and is **indifferent to how they split between
production-relevant and non-production**. It holds identically at 119 and at 121. **Nothing in this
document closes, reconciles or amends the 119-vs-121 residual recorded in rev 3 §9.6**, and the
per-boundary counts in §3.1 are reported as measurements of each transition, **not summed into a
reconciliation**. Re-measurement remains separately authorized work that has not been authorized.

---

## 6. Owner decisions, recorded 2026-09-04

S-1 was **accepted** on 2026-09-04. The four items S-1 raised were ruled at acceptance. They are
recorded here as **execution and evidence-scope clarifications**. **No ratified decision is rewritten
by them** — S-1.3, S-0, P-3 and the S-2.x rulings stand exactly as ratified.

### D-1 — Third-pass scope (clarifies the *execution* of S-1.3, does not amend it)

**Ruled.** The third pass's introduction of `DoAnCuoiKy` and `ThiGiuaKy` is a **non-shared-label-space
transition**. It is **contextual evidence about taxonomy evolution**, not an S-1.6 two-annotator
disagreement on a shared label space. **That subdivision is not adjudicated under S-1.6.**

**S-1.6 is not narrowed anywhere else.** It continues to apply in full to the cross-pass production
boundaries where the compared passes **did** share the relevant label space, and **the separate cause
rulings in §3.2 stand** — boundary A, boundary B and boundary C each keep their own ruling, including
the two that split.

**S-1.3's ratified text is unchanged.** §3.1's measurement — 121 of 121 relabels enter a class absent
from pass 2 — is retained as the evidence the clarification rests on. What changes is only *how S-1
executes against it*: the four third-pass boundaries are recorded as out of S-1.6's reach by label-space
provenance, rather than ruled on their merits.

### D-2 — The S-0 reservation is unchanged

**Ruled.** The sealed reservation **stands**; the 12-row contested stratum is **not re-reserved**. The
snapshot is not reopened.

**Why the composition does not invalidate the batch.** S-2 evaluates **reproducibility against the
current five-class taxonomy** — whether two readers applying the written spec reach the same label —
**not agreement with the historical pass labels**. A row's provenance as third-pass-forced says nothing
about whether the spec makes it reproducible.

**Binding reporting requirement on S-2.** The **cross-pass / shared-label vs third-pass-forced
composition travels with every S-2 figure**, as a diagnostic:

| Reserved batch | Contested | shared-label (cross-pass) | third-pass-forced |
|---|---|---|---|
| S-2 scored batch | 12 | 3 | 9 |
| Q-1 timed adjudication | 20 | 5 | 15 |
| Clean retest | 12 | 3 | 9 |

*(Rows in the `both` stratum are counted as shared-label, since a cross-pass disagreement exists for
them independently of the third pass.)* This joins the batch-composition disclosure S-2.4/G4 already
requires; it is a diagnostic and **never a selection or exclusion criterion**.

### D-3 — The current five-class taxonomy is established

**Ruled.** The historical provenance statement and the current ratification status are **separate
things and must not be conflated.**

`[observation]` **Governance history:** `DoAnCuoiKy` and `ThiGiuaKy` entered the production enum through
the seed v3 coverage repair rather than through a P-3 decision. That is a fact about how the enum came
to be.

**Current status:** the **five-class production taxonomy — `BaiTapVeNha` · `KiemTraThuongXuyen` ·
`ThiGiuaKy` · `ThiCuoiKy` · `DoAnCuoiKy` — is established** by S-1.0/S-1.1 with §4.1 of this record, now
accepted. **It is not to be described as unratified**, and the enum-provenance gap is an
**observation and a governance-history item, not a reason to reopen the taxonomy.** P-3 is not reopened.

### D-4 — The `kiểm tra`-as-verb defect

**Ruled.** Preserved as an **observed live annotation ambiguity**. **The seed is not modified and no
label is corrected during S-1.** It carries into S-2 as **guideline and worked-example material** — the
rule drafted at §4.2 and the template itself as the example a reader is trained against.

### D-5 — The sealed residual

The **119-vs-121 / 155-vs-157 residual remains untouched and unresolved.**

## 7. Exit criteria

| S-1 exit requirement | Where |
|---|---|
| A written ruling per item: keep / redefine / retire | §3, §4 |
| Boundary stated in words an annotator can apply to a row | §4.2 |
| **A separate cause ruling per production-relevant contested boundary** | §3.1 (four), §3.2 (three) — **7 of 7** |
| **The 1–5 Difficulty anchors, written** | §4.3 |
| Explicit statement that reminder-ness is a derived attribute, not a missing class | §4.1 |

**Silent-failure check.** S-1's named failure mode is a review that "confirms the taxonomy" without
writing down *why* each boundary sits where it does. Each of the seven boundaries above carries a stated
cause and the evidence it rests on, including the two where the honest answer is *the fork does not
apply* and the one where it is *insufficient evidence*.
