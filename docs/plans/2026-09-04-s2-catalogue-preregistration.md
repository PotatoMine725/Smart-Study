# S-2 — Ambiguous-example catalogue, selection pre-registration (K2)

**Written and committed BEFORE the examples are selected.** The git history is the evidence: this file
is committed in its own commit, the catalogue in a later one. S-2.9/K2 requires the example-selection
rule to be pre-registered **so the catalogue cannot become self-confirming by selecting the examples
that fit the definition just written** — and the definitions in question (the S-1 §4.2 boundary rules
and the §4.3 Difficulty anchors) already exist, which is exactly the condition that makes
cherry-picking possible and pre-registration necessary.

## Authority

| Element | Ruling |
|---|---|
| Boundary-indexed catalogue, **3–5 representative examples per production-relevant boundary** | **S-2.9** |
| Cataloguing is **not** adjudication; S-2 does not adjudicate the remaining contested rows | **S-2.9 / K1** |
| **Pre-register the example-selection rule** | **S-2.9 / K2** |
| **Retain row IDs and provenance** for every example | **S-2.9 / K3** |
| Identity keys on **content hash + source file + line** | **S-2.10 / H1** |
| The reserved batch stays sealed | **S-0**, **D-2** (2026-09-04 acceptance) |

## Selection universe

**The 89 unreserved rows of the master contested pool.** The 60 rows sealed by S-0 are excluded **by
hash, before any row is considered.**

This exclusion is not optional bookkeeping. The catalogue is **training material for the two readers
whose agreement S-2.7 scores.** An example drawn from the scored batch would train a reader on the exact
row they are later measured against, and the reproducibility figure would measure recall of the
catalogue rather than reproducibility of the spec. The reservation exists to prevent precisely this.

## Selection rule

**Deterministic, fixed here, and unpredictable from content.**

1. **Bucket** every unreserved pool row by its boundary — the unordered pair of the two classes that
   disagreed — under the datasheet→enum rename `BaiTap → BaiTapVeNha`. A row contested on both the
   cross-pass and third-pass axes is bucketed under both.
2. **Order** each bucket by `sha256(input_text)` ascending. Hash order cannot be steered toward or away
   from any row, so the rule cannot be tuned to the definitions it is meant to test.
3. **Deduplicate by template, first.** Take the **first row in hash order from each distinct skeleton**,
   continuing in hash order. The corpus is template-generated — 25 rows measured in S-1 §1.3 collapsed to
   14 skeletons, and 44% of the pass-2 window shares a skeleton with another row — so hash-order-first-N
   without this step would return the same sentence three times with different subject names and call it
   three representative examples.
4. **Skeleton**, defined exactly: lowercase the text, replace every non-word non-space character with a
   space, split on whitespace, and join `first 3 tokens + "|" + last 3 tokens`. Two rows with the same
   skeleton are one template.
5. **Take `min(4, distinct skeletons available)`** per boundary — mid-range of S-2.9's 3–5, with room
   above the floor.
6. **A boundary yielding fewer than 3 is declared SHORT**, not padded. Every available row is listed, the
   shortfall is recorded as a measured gap, and the boundary is routed to **S-4 authored examples**.

**No example is authored, edited, normalised or invented.** Rows are quoted verbatim from the corpus.
Authoring examples to fill a gap would place AI-generated material inside the instrument that governs
AI-generated labels, and S-4 is where authored samples belong.

## Which boundaries the catalogue must cover

`[observation]` **S-2.9 says "production-relevant boundary" where S-1.6 said "production-relevant
*contested* boundary", and the two readings differ.** This pre-registration adopts the **plain reading —
all ten class pairs of the five-class production taxonomy** — because once the taxonomy is five classes,
the boundaries a reader can confuse are the ten pairs, whether or not the legacy corpus happened to
contest them. The narrower reading (the 7 observed contested boundaries) is **flagged, not adopted**;
under it the three zero-evidence pairs simply fall out of scope. **Both readings produce the same
catalogue** for the boundaries that have evidence, and the same shortfall list for those that do not, so
the choice changes the framing and not the artifact.

## Feasibility, measured before selection

`[measured]` Distinct templates available per boundary, after excluding the reserved rows:

| Boundary | Rows | Templates | Status |
|---|---|---|---|
| `BaiTapVeNha` \| `DoAnCuoiKy` | 64 | 39 | **OK** |
| `BaiTapVeNha` \| `ThiCuoiKy` | 17 | 9 | **OK** |
| `KiemTraThuongXuyen` \| `ThiCuoiKy` | 6 | 3 | **OK** (at the floor) |
| `ThiCuoiKy` \| `ThiGiuaKy` | 12 | 3 | **OK** (at the floor) |
| `BaiTapVeNha` \| `KiemTraThuongXuyen` | 2 | 2 | **SHORT** |
| `KiemTraThuongXuyen` \| `ThiGiuaKy` | 2 | 2 | **SHORT** |
| `DoAnCuoiKy` \| `ThiCuoiKy` | 1 | 1 | **SHORT** |
| `BaiTapVeNha` \| `ThiGiuaKy` | 0 | 0 | **NO CORPUS EVIDENCE** |
| `DoAnCuoiKy` \| `KiemTraThuongXuyen` | 0 | 0 | **NO CORPUS EVIDENCE** |
| `DoAnCuoiKy` \| `ThiGiuaKy` | 0 | 0 | **NO CORPUS EVIDENCE** |

**Only 4 of 10 boundaries can meet S-2.9's 3–5 requirement from the corpus.** This is recorded here,
before selection, so that the shortfall is a measured property of the corpus and not an artifact of how
the examples were chosen. Note that two of the four sit exactly at the floor: `KiemTraThuongXuyen |
ThiCuoiKy` and `ThiCuoiKy | ThiGiuaKy` have 6 and 12 rows respectively but only **3 templates each**, so
row counts overstate their evidence.

## What each catalogue entry carries

Per K3 and S-2.10/H1: the **`sha256` hash**, **source file and line** for every occurrence, the
**boundary** it indexes, the **labels each pass assigned**, and the **rule from the spec that the example
illustrates**.

Per K1, every entry also carries, explicitly:

> **Catalogue illustration. Not an adjudication record. No Gold label attaches to this row.**

The operational test for K1 compliance is whether an entry could later be cited as that row's label. It
must not be able to be.

## Pre-commitment

The catalogue is selected **once**, by the rule above, and the result is reported as it comes out —
including boundaries where the selected examples turn out to illustrate the boundary rule poorly. **If an
example contradicts the definition it was drawn to illustrate, that is a finding about the definition and
is reported as one**, not grounds for re-drawing. Re-running selection with a changed rule would void the
pre-registration, and any such change must be recorded as a new pre-registration with its reason.
