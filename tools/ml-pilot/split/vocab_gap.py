#!/usr/bin/env python3
"""How much of the real test vocabulary does the synthetic training set never show?

WP-0.4 addendum. Writes `tools/ml-pilot/results/vocab_gap.json`.

WHY THIS IS MEASURED RATHER THAN ASSUMED
    Train is 100% synthetic; test is 100% real. If the synthetic generator's
    vocabulary does not contain the abbreviations students actually type -- `tgk`,
    `btvn`, `xstk`, `csdl` -- then the n-gram baseline is being asked to classify
    tokens it has literally never seen, and part of any encoder win is an artifact
    of the GENERATOR rather than evidence about encoders.

    A pretrained encoder's advantage on unseen surface forms is real and is
    exactly what this initiative is testing for. But the SIZE of the gap changes
    how the result should be read, and an unmeasured gap is the most likely way a
    reviewer discounts the whole pilot. Either answer is reportable; only the
    unmeasured version is a problem.

    This measures the evidence's shape. It does not filter, reweight, or
    otherwise change the split (EVA-04).
"""

import collections
import json
import os
import re
import sys
import unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))

# Abbreviations that appear in real student input, from the DAT-05 fixture set's
# `abbrev` category and collected_v4.
DOMAIN_ABBREV = ["tgk", "btvn", "xstk", "csdl", "ktvm", "ktct", "dacn", "ttcs",
                 "tck", "ktx", "dhqg", "bc"]


def tokens(s):
    return re.findall(r"\w+", s.lower(), flags=re.UNICODE)


def strip_diacritics(s):
    return "".join(c for c in unicodedata.normalize("NFD", s)
                   if unicodedata.category(c) != "Mn")


def read(path):
    import csv
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main():
    train = read(os.path.join(HERE, "train.csv"))
    test = read(os.path.join(HERE, "test.csv"))

    train_vocab = collections.Counter()
    for r in train:
        train_vocab.update(tokens(r["InputText"]))
    train_vocab_nd = {strip_diacritics(t) for t in train_vocab}

    test_tokens = 0
    oov_tokens = 0
    oov_counter = collections.Counter()
    rows_with_oov = 0
    for r in test:
        ts = tokens(r["InputText"])
        test_tokens += len(ts)
        oov = [t for t in ts if t not in train_vocab]
        oov_tokens += len(oov)
        oov_counter.update(oov)
        if oov:
            rows_with_oov += 1

    # Diacritic-insensitive view: "giua" vs "giữa" is a surface-form gap, not a
    # vocabulary gap, and the two tell different stories about the baseline.
    oov_nd = sum(1 for r in test for t in tokens(r["InputText"])
                 if strip_diacritics(t) not in train_vocab_nd)

    abbrev = {}
    for a in DOMAIN_ABBREV:
        in_train = train_vocab.get(a, 0)
        in_test = sum(1 for r in test if a in tokens(r["InputText"]))
        if in_test or in_train:
            abbrev[a] = {"train_occurrences": in_train, "test_rows_containing": in_test}

    result = {
        "_note": "Vocabulary reach of the synthetic training set over the real test "
                 "set. Measures the shape of the evidence; changes nothing about "
                 "the split (EVA-04).",
        "train_rows": len(train), "test_rows": len(test),
        "train_vocabulary_size": len(train_vocab),
        "test_token_count": test_tokens,
        "test_tokens_unseen_in_train": oov_tokens,
        "test_tokens_unseen_pct": round(100 * oov_tokens / test_tokens, 1),
        "test_tokens_unseen_diacritic_insensitive": oov_nd,
        "test_tokens_unseen_diacritic_insensitive_pct": round(100 * oov_nd / test_tokens, 1),
        "test_rows_with_at_least_one_unseen_token": rows_with_oov,
        "test_rows_with_unseen_pct": round(100 * rows_with_oov / len(test), 1),
        "most_common_unseen_tokens": oov_counter.most_common(30),
        "domain_abbreviations": abbrev,
    }

    dest = os.path.join("tools", "ml-pilot", "results", "vocab_gap.json")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    json.dump(result, open(dest, "w", encoding="utf-8"), indent=2, ensure_ascii=False)

    print(f"train vocabulary          : {len(train_vocab)} distinct tokens")
    print(f"test tokens unseen in train: {oov_tokens}/{test_tokens} "
          f"({result['test_tokens_unseen_pct']}%)")
    print(f"  diacritic-insensitive    : {oov_nd}/{test_tokens} "
          f"({result['test_tokens_unseen_diacritic_insensitive_pct']}%)")
    print(f"test rows with >=1 unseen  : {rows_with_oov}/{len(test)} "
          f"({result['test_rows_with_unseen_pct']}%)")
    print(f"most common unseen         : {[t for t,_ in oov_counter.most_common(12)]}")
    print("domain abbreviations (train occurrences / test rows):")
    for a, v in sorted(abbrev.items(), key=lambda kv: -kv[1]["test_rows_containing"]):
        print(f"  {a:6} train={v['train_occurrences']:4}  test_rows={v['test_rows_containing']}")
    print(f"wrote {dest}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
