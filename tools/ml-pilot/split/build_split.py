#!/usr/bin/env python3
"""Build the S0 evaluation split ONCE, so the featurizer is genuinely the only variable.

WP-0.4. EVA-02 / EVA-03 / EVA-04.

    python tools/ml-pilot/split/build_split.py

Writes `train.csv`, `test.csv` and `SPLIT.md` beside this file. They are
COMMITTED -- they are small text files, and committing them is what makes "no arm
re-split" auditable after the fact rather than a promise.

Reads `SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv` READ-ONLY.
S0 does not modify anything under `SmartStudyPlanner/` (EVA-01).

The 205 `collected_v4` rows were merged INTO the seed by `datasheets/_merge_seed.py`.
A leaky split here would recreate that exact error inside S0 itself, so the
disjointness check below is an assertion, not a comment.

CORRECTED 2026-08-26 (DFD-1). This docstring previously called those 205 rows
"real" and gave the merge as the reason the published 96.2% figure is not a
generalization number. Both are wrong. They are AI-generated and AI-labelled
(owner ruling 2026-08-26), and 96.2% was measured 2026-06-05 at the 698-row v3
seed -- thirteen days BEFORE collected_v4 entered the repository. The split this
script builds is train-authored vs test-authored, not synthetic vs real.
"""

import csv
import collections
import hashlib
import os
import re
import sys
import unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
SEED = os.path.join("SmartStudyPlanner", "Services", "ML", "TextClassifier",
                    "seed_intents.csv")

TRAIN_SOURCES = ("m8a_uniform", "synthetic_v3")
TEST_SOURCE = "collected_v4"

# The specification records these as [fact] at commit 980eec6 / 9c747be. If they
# no longer hold, the seed changed since the spec was written -- that is a
# FINDING to report, not a number to absorb.
EXPECT = {"train": 698, "test": 205, "total": 903}
EXPECT_TEST_CLASSES = {"ThiGiuaKy": 99, "BaiTapVeNha": 56, "DoAnCuoiKy": 50}
FIELDS = ["InputText", "TaskName", "TaskType", "Difficulty", "DeadlineHint",
          "Source", "LabelVersion"]


def norm(s):
    """Aggressive normalisation, used ONLY to COUNT near-duplicates.

    Never used to filter. §9 records the corpus as un-deduplicated; removing
    rows here would silently change the split the specification defines. The
    overlap is measured and reported as a limitation instead (EVA-11).
    """
    s = unicodedata.normalize("NFD", s.lower())
    s = "".join(c for c in s if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]+", " ", s).strip()


def write_csv(path, rows):
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS, quoting=csv.QUOTE_ALL,
                           lineterminator="\n")
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k, "") for k in FIELDS})


def main():
    if not os.path.exists(SEED):
        print(f"error: {SEED} not found -- run from the repository root",
              file=sys.stderr)
        return 1

    raw = open(SEED, "rb").read()
    seed_sha = hashlib.sha256(raw).hexdigest()
    rows = list(csv.DictReader(raw.decode("utf-8-sig").splitlines()))

    train = [r for r in rows if r["Source"] in TRAIN_SOURCES]
    test = [r for r in rows if r["Source"] == TEST_SOURCE]

    problems = []
    if len(train) != EXPECT["train"]:
        problems.append(f"train {len(train)} != {EXPECT['train']}")
    if len(test) != EXPECT["test"]:
        problems.append(f"test {len(test)} != {EXPECT['test']}")
    if len(rows) != EXPECT["total"]:
        problems.append(f"total {len(rows)} != {EXPECT['total']}")

    test_classes = collections.Counter(r["TaskType"] for r in test)
    if dict(test_classes) != EXPECT_TEST_CLASSES:
        problems.append(f"test classes {dict(test_classes)} != {EXPECT_TEST_CLASSES}")

    if problems:
        print("STOP -- the seed has changed since the specification's [fact] was "
              "established. This is a finding, not something to absorb:",
              file=sys.stderr)
        for p in problems:
            print(f"  - {p}", file=sys.stderr)
        return 2

    # EVA-04: no arm re-splits. That is only enforceable if the split is
    # disjoint to begin with.
    train_texts = {r["InputText"] for r in train}
    exact = sorted(t for t in (r["InputText"] for r in test) if t in train_texts)
    assert not exact, f"train and test share {len(exact)} exact inputs: {exact[:5]}"

    train_norm = collections.Counter(norm(r["InputText"]) for r in train)
    near = [(r["InputText"], train_norm[norm(r["InputText"])])
            for r in test if train_norm[norm(r["InputText"])] > 0]

    write_csv(os.path.join(HERE, "train.csv"), train)
    write_csv(os.path.join(HERE, "test.csv"), test)

    train_classes = collections.Counter(r["TaskType"] for r in train)
    all_classes = sorted(set(train_classes) | set(test_classes))
    missing = [c for c in all_classes if c not in test_classes]

    def table(counter, total):
        return "\n".join(
            f"| `{c}` | {counter.get(c, 0)} | {counter.get(c, 0)/total*100:.1f}% |"
            for c in all_classes)

    near_block = ("None. No test row normalises onto any training row.\n"
                  if not near else
                  "| Test input | Matching training rows |\n|---|---|\n" +
                  "\n".join(f"| `{t}` | {n} |" for t, n in near[:40]) +
                  (f"\n\n…and {len(near)-40} more." if len(near) > 40 else "") + "\n")

    md = f"""# S0 evaluation split — the record that makes EVA-04 auditable

**Built:** 2026-08-25 (WP-0.4) · **Rebuild:** `python tools/ml-pilot/split/build_split.py`

**Source:** `{SEED.replace(os.sep, '/')}` — read **read-only**
**Source SHA-256:** `{seed_sha}`

> A later reader can tell whether this split still corresponds to the seed by
> re-hashing that file. If the hash differs, the split is stale and every number
> derived from it is suspect.

---

## Counts — asserted, not assumed

| Split | Rows | Rule | Spec |
|---|---|---|---|
| **train** | **{len(train)}** | `Source ∈ {{{', '.join('`'+s+'`' for s in TRAIN_SOURCES)}}}` — synthetic only | EVA-02 |
| **test** | **{len(test)}** | `Source = ` `{TEST_SOURCE}` — held out, **excluded from training** | EVA-03 |

> **This is not a synthetic→real split** *(corrected 2026-08-26, DFD-1)*. `collected_v4` is
> AI-generated and AI-labelled, established by owner recall with no collection record. Both sides are
> authored; the split measures generalization across **authoring processes**, not to real student
> input. No result derived from it may be reported as accuracy on real input.
| total | {len(rows)} | | |

`build_split.py` **stops with exit 2** if these do not match 698 / 205 / 903. The
specification records them as `[fact]`; a drift is a finding to report, not a
number to absorb.

## Class distribution

**Training set ({len(train)} rows)**

| Class | Rows | Share |
|---|---|---|
{table(train_classes, len(train))}

**Test set ({len(test)} rows) — the 3-of-5 coverage limit**

| Class | Rows | Share |
|---|---|---|
{table(test_classes, len(test))}

**Absent from the held-out evaluation subset: {', '.join('`'+c+'`' for c in missing)}.**

This is the source of EVA-08 output 7 and of the **DAT-01** reporting bound: no
claim of *general* production accuracy or generalization may be made from a
3-of-5-class evaluation. It is **accepted for the pilot** (PD-3) and is a
reporting obligation, not a reason to defer S0.

Note the direction of the imbalance: `ThiGiuaKy` is the **smallest** training
class ({train_classes.get('ThiGiuaKy', 0)} rows) and the **largest** test class
({test_classes.get('ThiGiuaKy', 0)} rows). Per-class reporting is what keeps that
visible; a single averaged figure would not.

## Leakage

**Exact-text overlap between train and test: {len(exact)}.** Asserted in
`build_split.py`, not assumed.

This matters more here than it usually would. The 205 `collected_v4` rows were merged
*into* the seed by `datasheets/_merge_seed.py`, so any split drawn from the seed after
that date can leak. A leaky split would recreate that error inside S0 itself.

*Corrected 2026-08-26 (DFD-1): this paragraph previously said the merge happened before
the published 96.2% figure was measured, and called the rows real. Neither holds — 96.2%
was measured 2026-06-05 at the 698-row v3 seed, thirteen days before `collected_v4`
entered the repository, and `collected_v4` is AI-generated. The figure is still not a
generalization number; the reason is that its held-out rows came from its own training
corpus.*

**Near-duplicate overlap (diacritic-and-punctuation-insensitive): {len(near)} test rows.**

{near_block}
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
"""
    with open(os.path.join(HERE, "SPLIT.md"), "w", encoding="utf-8",
              newline="\n") as f:
        f.write(md)

    print(f"seed sha256 : {seed_sha}")
    print(f"train       : {len(train)}  {dict(train_classes)}")
    print(f"test        : {len(test)}  {dict(test_classes)}")
    print(f"absent      : {missing}")
    print(f"exact leak  : {len(exact)}")
    print(f"near-dup    : {len(near)} test rows")
    print("wrote train.csv, test.csv, SPLIT.md")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
