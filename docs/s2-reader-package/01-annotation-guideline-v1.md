# Canonical Annotation Specification — SmartStudyPlanner task corpus

**`GuidelineVersion: v1`** · drafted 2026-09-04 ·
**Status: `ratified for test — frozen`. Not validated.**

> **Ratified for testing, not validated.** The owner ratified `v1` on **2026-09-04** as the **frozen
> specification the reproducibility test runs against**. Ratification says the instrument is fixed so the
> test can measure it — **it does not say S-2 has passed, and no figure from §10 exists.** See §14.

This is the S-2 deliverable: **the document that makes a label reproducible.** It carries the semantic
contract, the boundary rules, the Difficulty anchors, the ambiguous-example catalogue, the adjudication
procedure, the provenance requirements, and its own version semantics.

**It has not yet been tested.** The reproducibility test (§10) is pre-registered and authorized, and has
**not been performed**. Nothing here may be described as validated, and **§14 forbids changing any of it
after test results are seen.**

| | |
|---|---|
| **Governing plan** | [Data Maturation Proposal rev 3](../plans/2026-08-26-data-maturation-coverage-expansion.md), authorized 2026-09-04 |
| **Ratified decisions** | [Stage Decision Outcomes](../plans/2026-09-04-data-maturation-stage-decision-outcomes.md) — S-2.1 … S-2.12 |
| **Origin of §4 and §5** | [S-1 Limited taxonomy review](../plans/2026-09-04-s1-limited-taxonomy-review.md), accepted 2026-09-04 |
| **Catalogue selection rule** | [S-2 catalogue pre-registration](../plans/2026-09-04-s2-catalogue-preregistration.md), committed before selection |
| **Reserved rows** | [S-0 snapshot](../../datasheets/reservations/2026-09-04-s0-reservation-snapshot.json) — sealed, and **excluded from every example below** |

---

## 1. AI-drafting disclosure (S-2.12, DFD-8)

Per S-2.12 this specification must identify which sections were AI-drafted or AI-assisted. Per the
owner's qualification, **this is provenance and transparency metadata — not a quality or trust score.**

| Section | Provenance |
|---|---|
| §2 Taxonomy · §3 Decision procedure · §4 Boundary rules | **AI-drafted.** §4's B-1 and B-4 restate rules ruled in S-1 §4.2; the rest is new drafting |
| §5 Difficulty anchors | **AI-drafted in S-1, owner-accepted 2026-09-04**, imported here **verbatim** (§5 note) |
| §6 Ambiguous-example catalogue | **Mechanically selected** under a pre-registered rule; per-boundary commentary is **AI-drafted**. Row text is **quoted verbatim from the corpus and not authored** |
| §7 Catalogue gaps | **Measured**, not drafted |
| §8 Adjudication · §9 Provenance · §11 Versioning · §12 Record template | **AI-drafted** from the ratified S-2.8 / S-2.10 / S-2.11 / S-2.12 rulings |
| §10 Reproducibility test | **Ratified parameters** (S-2.1–S-2.7) restated; **not performed** |

**The circularity this disclosure exists to mark:** the corpus this guideline governs is
`[measured]` **AI-generated — 0 real rows** (Data Audit Phase 0), and this guideline is AI-drafted. A
guideline written by the same class of system that produced the labels it governs cannot be assumed
independent of them. **§10's human reproducibility probe is the check on that**, which is why S-2.2
allows AI only as a supplementary probe and never as a substitute.

---

## 2. The taxonomy

**Five production classes.** Established by S-1.0/S-1.1 with S-1 §4.1, accepted 2026-09-04.

| Class | Means |
|---|---|
| **`BaiTapVeNha`** | A **single piece of coursework** the student completes and hands in |
| **`KiemTraThuongXuyen`** | A **short, recurring in-class assessment** — quiz, 15-minute test, weekly check |
| **`ThiGiuaKy`** | The scheduled **mid-semester examination** |
| **`ThiCuoiKy`** | The scheduled **end-of-semester examination** |
| **`DoAnCuoiKy`** | A **multi-part deliverable produced over weeks** — `đồ án`, `bài tập lớn`, `BTL`, *project* |

**`OnTap` and `NhacNho` are not production classes** and do not return (S-1.1).

**Reminder-ness is a derived attribute and a surface, not a task class.** A row can *be* a
`BaiTapVeNha` and *carry* a reminder; the reminder is not the class. **This is settled — do not
re-litigate it as a missing class.**

**Every row gets exactly one of the five.** There is no `Khac`, no "other", and no null. If no class
fits, that is a finding about the taxonomy and is raised as one, not absorbed into a fallback.

---

## 3. Decision procedure

Apply in order. Stop at the first branch that resolves.

1. **Is the task an examination sitting, or work the student produces and submits?**
   The object of the sentence decides this, not the vocabulary around it.
2. **If an examination sitting** — **check for a term marker first.**
   - Marked `giữa kỳ` / `GK` → **`ThiGiuaKy`**
   - Marked `cuối kỳ` / `CK` / final examination → **`ThiCuoiKy`**
   - **No term marker**, and the assessment is recurring, short or routine →
     **`KiemTraThuongXuyen`**

   The marker check comes **first**, by rule B-4. Reversing these two steps sends
   `bài kiểm tra giữa kỳ` to `KiemTraThuongXuyen`, which B-4 forbids.
3. **If produced work** — how is it shaped?
   - **Multi-part and sustained** — several artifacts (report *and* demo *and* source), or weeks of
     work, or a defence → **`DoAnCuoiKy`**
   - **A single deliverable**, however large → **`BaiTapVeNha`**

**Annotate the task the text describes, not the keywords it contains.** Rules B-1 to B-6 exist because
this sentence is not self-enforcing.

---

## 4. Boundary rules

**B-1 — `BaiTapVeNha` / `DoAnCuoiKy`** *(ruled in S-1 §4.2)*

> **`DoAnCuoiKy`** when the work is a **multi-part deliverable produced over weeks**, typically named
> `đồ án` / `bài tập lớn` / `BTL` / *project*, and typically carrying more than one artifact (report,
> demo, source, defence) or a group. **`BaiTapVeNha`** when it is a **single piece of coursework
> completed and handed in**, however large. The discriminator is **multi-part-and-sustained**, not size,
> and not whether the word `nhóm` appears.

**B-2 — `kiểm tra` as a verb** *(ruled in S-1 §4.2)*

> **`kiểm tra` used as a verb** (*to check, to re-check, to verify*) **does not make a row
> `KiemTraThuongXuyen`.** Classify on the task the text describes, not on a keyword's presence.

**B-3 — `cuối kỳ` on produced work is a timing modifier, not an exam marker**

> `cuối kỳ` / `final` attached to **produced work** (`đồ án cuối kỳ`, `project final`) says *when it is
> due*, not *that it is an exam*. It resolves to **`DoAnCuoiKy`**. `ThiCuoiKy` requires an
> **examination sitting**.

**B-4 — `giữa kỳ` versus `cuối kỳ` is decided lexically**

> When the text carries `giữa kỳ` / `GK`, the class is **`ThiGiuaKy`**; `cuối kỳ` / `CK` on an
> examination gives **`ThiCuoiKy`**. `bài kiểm tra giữa kỳ` is **`ThiGiuaKy`**, not
> `KiemTraThuongXuyen` — the `giữa kỳ` marker outranks the word `kiểm tra` (and see B-2).

**B-5 — group work does not imply `DoAnCuoiKy`**

> `nhóm` indicates who does the work, not what it is. `bài tập nhóm` with a single deliverable is
> **`BaiTapVeNha`**.

**B-6 — degraded register does not change the class**

> Missing diacritics, slang, abbreviations, typos and code-switching (`mon ktvm`, `ko kịp`, `BTL`)
> change nothing. Read for the task; do not downgrade to a fallback class because the text is informal.

---

## 5. Difficulty — the 1–5 anchors

**Imported verbatim from S-1 §4.3**, accepted 2026-09-04. *This specification is the authoritative text
going forward*; S-1 is the origin record. Any change to an anchor is a **semantic change requiring a
version bump** (§11), so the wording is reproduced rather than paraphrased.

Difficulty measures **scope of work × consequence of missing it**. It is **ordinal, not interval** — the
gap between 4 and 5 is not the gap between 1 and 2.

| Level | Anchor an annotator can apply |
|---|---|
| **1** | A single action, one sitting, **no preparation and nothing handed in** — remember, bring, check a time |
| **2** | One short piece of work, roughly **under two hours in a single sitting**, no coordination, little weight |
| **3** | **A normal graded task** needing one or two sessions of preparation. The unmarked default *only when the text gives no scope or stakes signal* |
| **4** | **Multi-session work**, or a deliverable with **several components** (report *and* demo *and* source), or work requiring **group coordination** |
| **5** | **High stakes and substantial scope together** — end-of-term assessment the course outcome rides on, or a sustained project at final defence |

Three rules travel with them:

1. **Difficulty must not be derived from `TaskType`.** A class-based prior is a *fallback for missing
   data*, never an annotation rule. `[measured]` In the legacy corpus `KiemTraThuongXuyen` sits at level
   3 in **120 of 121** rows — that is what deriving it looks like.
2. **Level 3 requires a positive reason** when the text carries any scope or stakes signal. "No strong
   signal" is the only admissible ground for 3. `[measured]` Level 3 held 567/1028 then 579/1028 rows.
3. **The v1 threshold governs levels 3–5 only.** Levels 1–2 are represented so the scale spans its
   range, but per S-2.3 the level-1/2 gap is **declared, not solved**, and no v1 figure may be quoted as
   governing them.

`[measured]` **Why anchors were needed:** between passes, with no rule changing, level 5 fell 184 → 116,
level 4 rose 79 → 147 and level 1 fell 57 → 30, with `5→4` (50), `3→4` (41) and `1→3` (27) the top
transitions. Systematic one-directional movement is **anchor absence, not carelessness.**

---

## 6. Ambiguous-example catalogue (S-2.9)

**Boundary-indexed**, selected mechanically under the
[pre-registered rule](../plans/2026-09-04-s2-catalogue-preregistration.md) committed before any example
was drawn: unreserved pool rows only, ordered by hash, **deduplicated by template first**, `min(4,
available)` per boundary.

**Per K1, no entry below is an adjudication.** Cataloguing is not adjudication; S-2 does not adjudicate
the remaining contested rows, and **no entry may be cited as its row's label.** Each carries its
`sha256`, every source file and line (K3, S-2.10/H1), and the labels each pass assigned.

**19 entries cover 15 distinct rows.** Four rows are contested on *both* the cross-pass and third-pass
axes, so the selection rule buckets each of them under two boundaries and they appear twice — different
entry numbers, same `sha256`. This is the rule working as pre-registered, not duplication.

**The reserved 60 rows are absent from this catalogue by construction** — an example drawn from the
scored batch would train a reader on a row they are later measured against.

### `BaiTapVeNha` ↔ `DoAnCuoiKy`
*Universe 64 unreserved rows / 39 templates — 4 selected, complete.*

**E-1** — `datasheets/normalized_dataset.csv`:373 · `datasheets/normalized_dataset_m8a.csv`:373 · `datasheets/normalized_dataset_m8a_uniform.csv`:386
> Deadline bài tập lớn môn Kỹ thuật phần mềm sắp tới, mình đang hoàn thiện phần cuối.

`sha256` `55eda311ac8c7cd4928b138e1a773873506662426663717d2bf82bdf1637d8c6`

Pass labels: `ThiCuoiKy` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-2** — `datasheets/normalized_dataset.csv`:36 · `datasheets/normalized_dataset_m8a.csv`:36 · `datasheets/normalized_dataset_m8a_uniform.csv`:561
> tình hình tiến độ cái đồ án tới đâu r ae

`sha256` `56ac7bbe4b44ef99769fef837452d654a24659b7396f75d7119f58b93f1cbaa2`

Pass labels: `DuAn` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 4 → 4

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-3** — `datasheets/normalized_dataset.csv`:333 · `datasheets/normalized_dataset_m8a.csv`:333 · `datasheets/normalized_dataset_m8a_uniform.csv`:82
> Mình phải nộp bài tập lớn Truyền thông đa phương tiện trong tuần này, ai rảnh review giúp mình không?

`sha256` `5c6042eb978360e67e739b7fdefc0370e1527be53dfcf03c6d49f399354dcc9b`

Pass labels: `BaiTapVeNha` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-4** — `datasheets/normalized_dataset.csv`:384 · `datasheets/normalized_dataset_m8a.csv`:384 · `datasheets/normalized_dataset_m8a_uniform.csv`:378
> Deadline đồ án Kỹ thuật phần mềm đã gần, mình cần hoàn thiện phần demo.

`sha256` `5d549f6c1a5e86b56160c5980fe82dee2853b114fbf3ab53ee8ac6829d937c05`

Pass labels: `ThiCuoiKy` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 4 → 4

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show.** The discriminator is *multi-part-and-sustained*, not size and not the word `nhóm`. `E-1` and `E-4` name the **same subject with different task nouns** — `bài tập lớn` and `đồ án` — and both resolve to `DoAnCuoiKy`. `E-2` is the informal register carrying no task noun beyond `đồ án`. `E-3` is a `bài tập lớn` that **both** legacy passes read as ordinary homework. **Also note the Difficulty split**: `E-1` carries 3 and `E-4` carries 4 for work of the same scope — an anchor disagreement sitting inside a TaskType example.

### `BaiTapVeNha` ↔ `KiemTraThuongXuyen`
*Universe 2 unreserved rows / 2 templates — 2 selected, **SHORT**.*

**E-5** — `datasheets/normalized_dataset.csv`:536 · `datasheets/normalized_dataset_m8a.csv`:536 · `datasheets/normalized_dataset_m8a_uniform.csv`:977
> cô cho bài tập về nhà mon ktvm nhưng mình ghi ko kịp đề, ai chụp giúp mình với

`sha256` `5df5ddbd7708917a78a68af186417212a51d71b433d88c585cf79d983ab2a3d3`

Pass labels: `KiemTraThuongXuyen` → `BaiTapVeNha` → `BaiTapVeNha`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-6** — `datasheets/normalized_dataset.csv`:551 · `datasheets/normalized_dataset_m8a.csv`:551 · `datasheets/normalized_dataset_m8a_uniform.csv`:130
> làm bài tập ktvm sao kho wá, chắc chớt mất

`sha256` `84c6b722acb1910430b05519f91cba0dde352bad0ed6ded57be7e897dc017a45`

Pass labels: `KiemTraThuongXuyen` → `BaiTapVeNha` → `BaiTapVeNha`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show.** Both are plainly homework (`bài tập về nhà`, `làm bài tập`) that pass 1 labelled `KiemTraThuongXuyen`. Both are in a degraded register — no diacritics, slang, typos (`mon ktvm`, `sao kho wá`). Whether register drives the error is **a hypothesis for the reproducibility probe to test, not a finding**; n=2.

### `BaiTapVeNha` ↔ `ThiCuoiKy`
*Universe 17 unreserved rows / 9 templates — 4 selected, complete.*

**E-7** — `datasheets/normalized_dataset.csv`:373 · `datasheets/normalized_dataset_m8a.csv`:373 · `datasheets/normalized_dataset_m8a_uniform.csv`:386
> Deadline bài tập lớn môn Kỹ thuật phần mềm sắp tới, mình đang hoàn thiện phần cuối.

`sha256` `55eda311ac8c7cd4928b138e1a773873506662426663717d2bf82bdf1637d8c6`

Pass labels: `ThiCuoiKy` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-8** — `datasheets/normalized_dataset.csv`:384 · `datasheets/normalized_dataset_m8a.csv`:384 · `datasheets/normalized_dataset_m8a_uniform.csv`:378
> Deadline đồ án Kỹ thuật phần mềm đã gần, mình cần hoàn thiện phần demo.

`sha256` `5d549f6c1a5e86b56160c5980fe82dee2853b114fbf3ab53ee8ac6829d937c05`

Pass labels: `ThiCuoiKy` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 4 → 4

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-9** — `datasheets/normalized_dataset.csv`:376 · `datasheets/normalized_dataset_m8a.csv`:376 · `datasheets/normalized_dataset_m8a_uniform.csv`:610
> Bài tập nhóm Nghiên cứu thị trường đang thiếu một vài phần, cần họp nhóm gấp.

`sha256` `5d811cf2a46b411357b1923084d0b55b641307c88b5ec361167e808e203df41d`

Pass labels: `ThiCuoiKy` → `BaiTapVeNha` → `BaiTapVeNha`  ·  Difficulty: 5 → 5 → 5

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-10** — `datasheets/normalized_dataset.csv`:478 · `datasheets/normalized_dataset_m8a.csv`:478 · `datasheets/normalized_dataset_m8a_uniform.csv`:126
> Nhóm mình còn thiếu phần kết luận báo cáo đồ án môn Cơ sở dữ liệu, hoàn thiện trước thứ 5.

`sha256` `6ce73b6da5031cd08b323ee8d27bf2bfa4e53054c07b72b52f2825a1c7045936`

Pass labels: `ThiCuoiKy` → `BaiTapVeNha` → `DoAnCuoiKy`  ·  Difficulty: 3 → 4 → 4

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show.** The audit’s “one genuine collision”. `E-9` is the clean case for `BaiTapVeNha` — `bài tập nhóm`, a single deliverable, group work notwithstanding — and pass 3 agreed. `E-7`, `E-8` and `E-10` are not really this boundary at all: all three end at `DoAnCuoiKy` once that class exists. They are catalogued here because the legacy passes contested them here, and they illustrate **why** it was contested — the class that fits was missing.

### `BaiTapVeNha` ↔ `ThiGiuaKy`
*Universe 0 unreserved rows / 0 templates — 0 selected, **no corpus evidence**.*

**No corpus evidence exists for this boundary.** No legacy pass ever contested it, so no row can be drawn. Routed to **S-4 authored examples**; nothing is authored here.
### `DoAnCuoiKy` ↔ `KiemTraThuongXuyen`
*Universe 0 unreserved rows / 0 templates — 0 selected, **no corpus evidence**.*

**No corpus evidence exists for this boundary.** No legacy pass ever contested it, so no row can be drawn. Routed to **S-4 authored examples**; nothing is authored here.
### `DoAnCuoiKy` ↔ `ThiCuoiKy`
*Universe 1 unreserved rows / 1 templates — 1 selected, **SHORT**.*

**E-11** — `datasheets/normalized_dataset.csv`:38 · `datasheets/normalized_dataset_m8a.csv`:38 · `datasheets/normalized_dataset_m8a_uniform.csv`:968
> deadline project final là 23h59 chủ nhật nha

`sha256` `5f2575589e063913c41ff10c8f2960082cbe2e6ed668ef9a194fb3d36c31b442`

Pass labels: `ThiCuoiKy` → `ThiCuoiKy` → `DoAnCuoiKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show.** One row, and it is the genuinely hard case: `project final` reads as *the final project* or *the final exam*. The spec resolves it by object — a `project` is produced work, so `DoAnCuoiKy` — and `final`/`cuối kỳ` attached to produced work is a **timing modifier, not an exam marker** (rule B-3).

### `DoAnCuoiKy` ↔ `ThiGiuaKy`
*Universe 0 unreserved rows / 0 templates — 0 selected, **no corpus evidence**.*

**No corpus evidence exists for this boundary.** No legacy pass ever contested it, so no row can be drawn. Routed to **S-4 authored examples**; nothing is authored here.
### `KiemTraThuongXuyen` ↔ `ThiCuoiKy`
*Universe 6 unreserved rows / 3 templates — 3 selected, complete.*

**E-12** — `datasheets/normalized_dataset.csv`:482 · `datasheets/normalized_dataset_m8a.csv`:482 · `datasheets/normalized_dataset_m8a_uniform.csv`:283
> Cô Hương nhắc cả lớp bài kiểm tra giữa kỳ môn Kế toán tài chính vào thứ 2 tuần 10.

`sha256` `464cb17217d801a0343a9b36c7a8b8ea5d2de30befd65c40bae95915abcfea3d`

Pass labels: `ThiCuoiKy` → `KiemTraThuongXuyen` → `ThiGiuaKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-13** — `datasheets/normalized_dataset.csv`:386 · `datasheets/normalized_dataset_m8a.csv`:386 · `datasheets/normalized_dataset_m8a_uniform.csv`:929
> Thầy nhắc nộp báo cáo Hóa phân tích, mình cần kiểm tra lại format trước khi gửi.

`sha256` `6e8e46d7c82308a3d1da5a0f79aba2f08b1a5dd5ca6056b699c77543e8dc3f73`

Pass labels: `ThiCuoiKy` → `KiemTraThuongXuyen` → `KiemTraThuongXuyen`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-14** — `datasheets/normalized_dataset.csv`:497 · `datasheets/normalized_dataset_m8a.csv`:497 · `datasheets/normalized_dataset_m8a_uniform.csv`:653
> Kiểm tra giữa kỳ môn Hệ điều hành vào tuần 8, ôn phần quản lý tiến trình và bộ nhớ.

`sha256` `d658a149be6ac32c459a102d0423cba03043a562c08f4b99d13c5e12c7ed5708`

Pass labels: `ThiCuoiKy` → `KiemTraThuongXuyen` → `ThiGiuaKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show — and what they fail to show.** Only `E-13` is genuinely on this boundary, and it is the `kiểm tra`-as-verb defect (D-4): the task is submitting a report, `kiểm tra lại format` means *re-check the formatting*, and pass 2’s class came from the keyword. `E-12` and `E-14` say `giữa kỳ` and belong to `ThiGiuaKy`; they sit in this bucket only because neither legacy pass had that class. **This boundary is short in substance though it meets the count** — reported, not re-drawn (§7).

### `KiemTraThuongXuyen` ↔ `ThiGiuaKy`
*Universe 2 unreserved rows / 2 templates — 2 selected, **SHORT**.*

**E-15** — `datasheets/normalized_dataset.csv`:482 · `datasheets/normalized_dataset_m8a.csv`:482 · `datasheets/normalized_dataset_m8a_uniform.csv`:283
> Cô Hương nhắc cả lớp bài kiểm tra giữa kỳ môn Kế toán tài chính vào thứ 2 tuần 10.

`sha256` `464cb17217d801a0343a9b36c7a8b8ea5d2de30befd65c40bae95915abcfea3d`

Pass labels: `ThiCuoiKy` → `KiemTraThuongXuyen` → `ThiGiuaKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-16** — `datasheets/normalized_dataset.csv`:497 · `datasheets/normalized_dataset_m8a.csv`:497 · `datasheets/normalized_dataset_m8a_uniform.csv`:653
> Kiểm tra giữa kỳ môn Hệ điều hành vào tuần 8, ôn phần quản lý tiến trình và bộ nhớ.

`sha256` `d658a149be6ac32c459a102d0423cba03043a562c08f4b99d13c5e12c7ed5708`

Pass labels: `ThiCuoiKy` → `KiemTraThuongXuyen` → `ThiGiuaKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show.** Both say `giữa kỳ` explicitly and both resolve to `ThiGiuaKy` under rule B-2. Neither is genuinely ambiguous under the current taxonomy; they were contested only because `ThiGiuaKy` did not exist when they were labelled. n=2.

### `ThiCuoiKy` ↔ `ThiGiuaKy`
*Universe 12 unreserved rows / 3 templates — 3 selected, complete.*

**E-17** — `datasheets/normalized_dataset.csv`:311 · `datasheets/normalized_dataset_m8a.csv`:311 · `datasheets/normalized_dataset_m8a_uniform.csv`:507
> Đợt thi giữa kỳ môn Toán rời rạc khó hơn mình dự kiến, phải tập trung hơn.

`sha256` `7c22cf5a4261f61a9979044e8d2b3a3388c0665fce3fb36389b810e67a662248`

Pass labels: `ThiCuoiKy` → `ThiCuoiKy` → `ThiGiuaKy`  ·  Difficulty: 5 → 4 → 4

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-18** — `datasheets/normalized_dataset.csv`:329 · `datasheets/normalized_dataset_m8a.csv`:329 · `datasheets/normalized_dataset_m8a_uniform.csv`:651
> Mình cần ôn gấp cho bài thi giữa kỳ Toán cao cấp A3, nội dung khá nhiều.

`sha256` `ba8b535397c9cd83b1d80534a07b31ad3d3f56088406cd0c63488b10ab9beffb`

Pass labels: `ThiCuoiKy` → `ThiCuoiKy` → `ThiGiuaKy`  ·  Difficulty: 5 → 4 → 4

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**E-19** — `datasheets/normalized_dataset.csv`:322 · `datasheets/normalized_dataset_m8a.csv`:322 · `datasheets/normalized_dataset_m8a_uniform.csv`:764
> Thi giữa kỳ môn Tài chính doanh nghiệp sắp diễn ra, mình đang ôn theo đề cương.

`sha256` `d4dd943184cbfa8b348aaa5f30398fe2cc8034661de82593020220ce726b55dd`

Pass labels: `ThiCuoiKy` → `ThiCuoiKy` → `ThiGiuaKy`  ·  Difficulty: 3 → 3 → 3

*Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.*

**What these show.** The cleanest boundary in the catalogue: `giữa kỳ` versus `cuối kỳ` is a **lexical marker the spec can rely on**, and all three resolve on it alone. They are contested only because `ThiGiuaKy` post-dates the passes. Note `E-17` and `E-18` carry Difficulty 5→4 — the drift documented at §5.


---

## 7. Catalogue gaps — measured, not filled

`[measured]` **Only 4 of the 10 class pairs reach S-2.9's floor of 3 examples.** The shortfall was
measured and recorded *before* selection ran, so it is a property of the corpus and not of how the
examples were chosen.

| Status | Boundaries |
|---|---|
| **Complete** (3–4 examples) | `BaiTapVeNha`\|`DoAnCuoiKy` · `BaiTapVeNha`\|`ThiCuoiKy` · `KiemTraThuongXuyen`\|`ThiCuoiKy` · `ThiCuoiKy`\|`ThiGiuaKy` |
| **Short** (1–2 examples) | `BaiTapVeNha`\|`KiemTraThuongXuyen` (2) · `KiemTraThuongXuyen`\|`ThiGiuaKy` (2) · `DoAnCuoiKy`\|`ThiCuoiKy` (1) |
| **No corpus evidence** (0) | `BaiTapVeNha`\|`ThiGiuaKy` · `DoAnCuoiKy`\|`KiemTraThuongXuyen` · `DoAnCuoiKy`\|`ThiGiuaKy` |

**The per-boundary target was pre-registered at `min(4, available)`** — mid-range of S-2.9's 3–5, leaving
headroom above the floor without exhausting the thin boundaries. It was fixed before selection and is
not a post-hoc choice.

**Two of the four "complete" boundaries sit exactly on the floor.** `KiemTraThuongXuyen`\|`ThiCuoiKy`
has 6 rows but **3 templates**; `ThiCuoiKy`\|`ThiGiuaKy` has 12 rows but **3 templates**. Row counts
overstate the evidence behind both.

**`KiemTraThuongXuyen`\|`ThiCuoiKy` is short in substance although it meets the count.** Of its three
examples, two say `giữa kỳ` and belong to `ThiGiuaKy` under B-4; only one is genuinely on the boundary,
and that one is the B-2 defect. Per the pre-registration's pre-commitment this is **reported as a
finding, not re-drawn.**

**The three zero-evidence boundaries are uncatalogueable from this corpus** and are routed to **S-4
authored examples**. Nothing was authored here: writing examples to fill the gap would place
AI-generated material inside the instrument that governs AI-generated labels.

`[observation]` The gap has a single cause. Nine of the ten pairs involve `DoAnCuoiKy` or `ThiGiuaKy`
in at least one position, and **those two classes did not exist during either legacy annotation pass**
(S-1 §3.1). The corpus cannot evidence boundaries that were unreachable when it was labelled.

---

## 8. Adjudication procedure (S-2.8)

**To adjudicate is to assign the correct label from the full current production taxonomy — all five
classes.** It is **not** a two-way choice between the two labels originally disputed. Adjudication and
annotation are the same operation, plus a recorded ruling.

1. Read the row. Apply §3, then §4.
2. Assign **one** of the five classes and a Difficulty from §5. Both, always — never one without the
   other.
3. Record the ruling: the label, the **rule from §4 or the anchor from §5 that decided it**, and the
   annotator.
4. Where §4 does not decide the row, **say so** and raise it. A row the spec cannot resolve is
   evidence about the spec, and suppressing it by picking the closest class destroys that evidence.

Per S-4.5, a rule citation is required **only where existing structure does not already answer "which
rule decided this row"**: Difficulty rulings name the §5 anchor applied; authored-sample rows name the
rule that placed them; contested-pool TaskType rulings inherit their recorded boundary and add nothing.

---

## 9. Label provenance

Every label carries, at minimum: **who or what assigned it**, **when**, **under which
`GuidelineVersion`**, and **the row's identity** (`sha256` + source file + line, per S-2.10/H1).

- **The hash is a locator for the reserved snapshot, not long-lived identity** (H1). **S-3** introduces
  the stable `RowId` that permanently retains the originating hash.
- **Corrected source text preserves old hash references** through a supersession chain (H2) — a
  corrected row does not silently become a different row.
- **The row-level `GuidelineVersion` field is S-3's, not this document's** (S-2.11). §11 defines the
  version semantics; S-3 stores them per row.
- Under DFD-8, **the owner's pass is the label**; any other pass is a measurement.

---

## 10. The reproducibility test — pre-registered, **not performed**

> **Authorized to perform is not performed.** Nothing in this section has been executed, and no figure
> from it exists.

**Who** (S-2.2, closing R-1): the **owner** performs the Gold/reference pass; **one independent human
reader from the Q-2 network** performs a blind reproducibility probe. Both annotate the same 20 rows
independently. **AI may be added later as a supplementary probe, never a substitute.**

**On what**: the **sealed S-0 scored batch** — 20 rows, 12 contested + 8 Difficulty-spread. One batch,
two measurements (C3).

**What is measured** (S-2.1): TaskType and Difficulty **independently** (C1). **Both must pass their own
threshold — no averaging, no trade-off** (C2).

**Thresholds** (S-2.7), pre-registered and **not to be changed after observing results**:

| Dimension | Denominator | Threshold |
|---|---|---|
| **TaskType** | 20 — full-batch scoring | **≥ 17/20 (85%)**, exact match |
| **Difficulty** | 20 — full-batch scoring | **≥ 18/20 (90%)**, exact match |

Within-one may be **reported as a diagnostic; it is never the gate.** Failure ⇒ spec revision plus the
pre-reserved **v2 retest batch**.

**Reporting rules that bind every figure produced:**

- **G4 — batch composition travels with every quoted figure. No bare percentage anywhere.**
- **G3 — headline rates must not be described as corpus-wide agreement.**
- **G2 — per-stratum breakdown required.** **C4 — per-dimension and per-boundary detail required**; a
  single headline number is not an acceptable output.
- **The level-1/2 caveat travels with every Difficulty figure** — the v1 threshold governs levels 3–5.
- **D-2 (owner ruling, 2026-09-04) — the shared-label vs third-pass-forced composition travels with
  every S-2 figure as a diagnostic:**

  | Reserved batch | Contested | shared-label (cross-pass) | third-pass-forced |
  |---|---|---|---|
  | **Scored batch** | 12 | 3 | 9 |
  | Q-1 timed adjudication | 20 | 5 | 15 |
  | Clean retest | 12 | 3 | 9 |

  Rows contested on both axes count as shared-label, a cross-pass disagreement existing for them
  independently. **This is a diagnostic and never a selection or exclusion criterion.** S-2 measures
  reproducibility **against the current five-class taxonomy**, not agreement with the historical pass
  labels.

**Blocking dependency:** a **recruited independent reader**. Q-2 ratifies that a bounded network
*exists*; it does not schedule anyone. Until a reader is recruited, this test cannot start.

---

## 11. Guideline versioning (S-2.11)

**This document's version is `GuidelineVersion`, declared at the top.** S-2 owns the version semantics;
**S-3 owns the row-level field.**

**Bump when:** a class definition changes · a class-boundary rule changes · a Difficulty anchor changes.

**No bump for:** typos · formatting · non-semantic example additions.

**Owner guardrail:** an example addition that **changes the effective classification rule** is a
semantic change and **requires a bump** — including an example added to the §6 catalogue that decides a
case §4 did not previously decide.

`[inference]` Versioning is load-bearing, not bureaucratic. The corpus already contains rows labelled
under different implicit taxonomies with no marker distinguishing them — the failure `LabelVersion` was
meant to prevent and did not, because it versions the **file**, not the **guideline**.

---

## 12. Annotation-record template (S-2.12)

**A working annotation artifact — explicitly *not* an S-3 storage contract.** S-3 defines storage.

```yaml
row:
  sha256:            # full 64-hex of the input text, UTF-8
  source:            # file:line for every occurrence
label:
  task_type:         # one of the five production classes
  difficulty:        # 1-5
annotation:
  annotator:         # person or system
  role:              # gold | probe | supplementary
  date:
  guideline_version: # e.g. v1
ruling:
  decided_by:        # rule id (B-1..B-6) or anchor level; required per S-4.5
  note:              # only where the spec did not decide the row
  unresolved: false  # true when no rule in section 4 resolves it - raise, do not guess
```

---

## 13. What this specification does **not** do

| Not done here | Where it lives |
|---|---|
| Adjudicate the remaining contested rows | Not S-2 (K1). Gold-A |
| Author examples for the three zero-evidence boundaries | **S-4** authored samples |
| Define the row-level `GuidelineVersion` field or any storage | **S-3** (S-2.11) |
| Audit or change current Difficulty consumers | **FU-1**, unscheduled (S-1.4) |
| Rule on the evaluation defect | **S-6** (S-1.5) |
| Correct the B-2 defect rows in the seed | **Not corrected.** D-4 keeps them as observed material |
| Perform the reproducibility test | **§10** — pre-registered, not performed |

**DFD-2 — ruled by the owner, 2026-09-04.** DFD-2 bars further labelled data *"until [the spec] exists."*
Whether *existence* lifts that bar or only *passing* §10 does was not settled by the ratified text. **For this
execution, DFD-2 is satisfied only after the pre-registered §10 test passes** — unless a later explicit
owner decision changes the interpretation.

**DFD-2 therefore remains unsatisfied today.** Neither the existence of this specification nor its
ratification for testing lifts the labelled-data gate. **No labelled data may be collected, imported,
generated or promoted on the strength of this document.**

---

## 14. Freeze (owner ratification, 2026-09-04)

**`v1` is frozen.** It was ratified as the specification the §10 test runs **against**, so that the test
measures the instrument rather than a moving target.

**Locked — and specifically locked against change after results are seen:**

| Locked | Where |
|---|---|
| The decision procedure | §3 |
| The six boundary rules `B-1`–`B-6` | §4 |
| The Difficulty 1–5 anchors and their three rules | §5 |
| The catalogue, **exactly as evidence-derived** | §6 |
| The 5-way adjudication procedure | §8 |
| The thresholds — TaskType ≥ 17/20, Difficulty ≥ 18/20, exact match | §10 |
| Every reporting rule — G2, G3, G4, C4, the level-1/2 caveat, the D-2 diagnostic | §10 |

**S-2.7's invariant is the reason:** *thresholds must not be changed after observing results.* §14
extends the same discipline to the procedure, the anchors and the reporting rules — a spec revised
after seeing its own score measures nothing.

**The catalogue stays exactly as it is.** The three zero-evidence boundaries are **not** to be filled by
authored examples and the short boundaries are **not** to be re-drawn. They stand as
**catalogue-coverage findings** (§7) and are routed to **S-4 authored examples**.

**The sealed 20-row scored batch stays unchanged.** It is **not** re-reserved on the strength of its
shared-label vs third-pass-forced composition; that composition is a **required diagnostic on every S-2
figure, never a selection criterion**.

**If the test fails**, the route is the one already ratified: spec revision — which is a
**`GuidelineVersion` bump** under §11 — plus the pre-reserved **v2 retest batch**. Revision happens
*after* a recorded failure, never *instead of* one.

### Freeze anchor

The frozen text is the content of this file at the commit that carries this section. The anchor — that
commit and the file's `sha256` at it — is recorded in
[`../plans/2026-09-04-s2-v1-freeze-record.md`](../plans/2026-09-04-s2-v1-freeze-record.md), so the freeze
is **verifiable rather than asserted**: recompute the hash and compare.

### Blocking prerequisite

**One independent human reader from the Q-2 network must be recruited before the test can start.** Q-2
establishes that a network *exists*; it assigns nobody. **No AI substitute** (S-2.2). Recruitment is
owner action, and until it completes §10 cannot begin.
