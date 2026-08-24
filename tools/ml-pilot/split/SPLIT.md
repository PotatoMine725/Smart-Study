# S0 evaluation split — the record that makes EVA-04 auditable

**Built:** 2026-08-25 (WP-0.4) · **Rebuild:** `python tools/ml-pilot/split/build_split.py`

**Source:** `SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` — read **read-only**
**Source SHA-256:** `86abb454c139bf2c0dd3f7a4698a5f2fbde144de2f02a17950b32f5bfa36dbd6`

> A later reader can tell whether this split still corresponds to the seed by
> re-hashing that file. If the hash differs, the split is stale and every number
> derived from it is suspect.

---

## Counts — asserted, not assumed

| Split | Rows | Rule | Spec |
|---|---|---|---|
| **train** | **698** | `Source ∈ {`m8a_uniform`, `synthetic_v3`}` — synthetic only | EVA-02 |
| **test** | **205** | `Source = ` `collected_v4` — real, held out, **excluded from training** | EVA-03 |
| total | 903 | | |

`build_split.py` **stops with exit 2** if these do not match 698 / 205 / 903. The
specification records them as `[fact]`; a drift is a finding to report, not a
number to absorb.

## Class distribution

**Training set (698 rows)**

| Class | Rows | Share |
|---|---|---|
| `BaiTapVeNha` | 124 | 17.8% |
| `DoAnCuoiKy` | 131 | 18.8% |
| `KiemTraThuongXuyen` | 188 | 26.9% |
| `ThiCuoiKy` | 170 | 24.4% |
| `ThiGiuaKy` | 85 | 12.2% |

**Test set (205 rows) — the 3-of-5 coverage limit**

| Class | Rows | Share |
|---|---|---|
| `BaiTapVeNha` | 56 | 27.3% |
| `DoAnCuoiKy` | 50 | 24.4% |
| `KiemTraThuongXuyen` | 0 | 0.0% |
| `ThiCuoiKy` | 0 | 0.0% |
| `ThiGiuaKy` | 99 | 48.3% |

**Absent from the real evaluation subset: `KiemTraThuongXuyen`, `ThiCuoiKy`.**

This is the source of EVA-08 output 7 and of the **DAT-01** reporting bound: no
claim of *general* production accuracy or generalization may be made from a
3-of-5-class evaluation. It is **accepted for the pilot** (PD-3) and is a
reporting obligation, not a reason to defer S0.

Note the direction of the imbalance: `ThiGiuaKy` is the **smallest** training
class (85 rows) and the **largest** test class
(99 rows). Per-class reporting is what keeps that
visible; a single averaged figure would not.

## Leakage

**Exact-text overlap between train and test: 0.** Asserted in
`build_split.py`, not assumed.

This matters more here than it usually would. The 205 real rows were merged *into*
the seed by `datasheets/_merge_seed.py` before the published **96.2%** figure was
measured — which is exactly why that figure **is not a generalization number** and
must not be cited as a synthetic→real baseline. A leaky split would recreate that
error inside S0 itself.

**Near-duplicate overlap (diacritic-and-punctuation-insensitive): 0 test rows.**

None. No test row normalises onto any training row.

Near-duplicates are **counted and reported, never filtered.** The corpus is
un-deduplicated (spec §9 `[limit]`); dropping rows here would silently change the
split the specification defines. This number is carried into the report's
limitations (EVA-11).

## Determinism

No shuffling, no sampling, no seed. The split is a **filter on the `Source`
column**, so re-running produces byte-identical `train.csv` and `test.csv`.

## Consumption

Every arm — baseline, A, B — consumes these two files **verbatim**. **No
re-splitting, no re-shuffling, no stratification pass.** EVA-04 is absolute, and
two arms that each re-split would produce numbers that cannot be compared and
would not be detectable after the fact.
