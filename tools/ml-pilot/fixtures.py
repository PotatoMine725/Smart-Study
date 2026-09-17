#!/usr/bin/env python3
"""The single reader for the DAT-05 fixture set, plus its self-verification.

DAT-05 requires ONE committed Vietnamese input fixture set that every slice uses.
One set needs one reader, or the escaping convention gets reimplemented slightly
differently per consumer and the four acceptance criteria drift apart again.

Run the verification from the repository root:
    python tools/ml-pilot/fixtures.py
"""

import csv
import collections
import hashlib
import io
import os
import sys

FIXTURE_PATH = os.path.join("datasheets", "vn_input_fixtures.csv")

REQUIRED_CATEGORIES = (
    "diacritics", "stripped", "runtogether", "abbrev", "empty", "pathological",
)

_UNESCAPE = {"t": "\t", "r": "\r", "n": "\n", "\\": "\\"}


def unescape(s):
    """Reverse build_fixtures.esc(). Every Input column value is escaped."""
    out = []
    i = 0
    while i < len(s):
        if s[i] == "\\" and i + 1 < len(s):
            c = s[i + 1]
            if c in _UNESCAPE:
                out.append(_UNESCAPE[c])
                i += 2
                continue
        out.append(s[i])
        i += 1
    return "".join(out)


def load(path=FIXTURE_PATH):
    """Read the fixture set with a byte-level reader.

    Deliberately NOT PowerShell Get-Content: it mangles BOM-less UTF-8
    Vietnamese in this environment, which would corrupt every downstream
    comparison while looking like it worked.
    """
    with open(path, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8")            # invalid UTF-8 raises here, loudly
    rows = list(csv.DictReader(io.StringIO(text)))
    for r in rows:
        r["Input"] = unescape(r["Input"])
    return rows


def by_category(rows):
    d = collections.defaultdict(list)
    for r in rows:
        d[r["Category"]].append(r)
    return d


def realistic(rows):
    """The four categories that represent input a student actually types.

    Used for the latency distribution; `empty` and `pathological` are reported
    as named cases instead of being blended into a percentile (pilot README
    §2.1, amended 2026-08-25).
    """
    keep = {"diacritics", "stripped", "runtogether", "abbrev"}
    return [r for r in rows if r["Category"] in keep]


def _verify():
    with open(FIXTURE_PATH, "rb") as f:
        raw = f.read()
    print(f"file      : {FIXTURE_PATH}")
    print(f"bytes     : {len(raw)}")
    print(f"sha256    : {hashlib.sha256(raw).hexdigest()}")

    assert not raw.startswith(b"\xef\xbb\xbf"), "BOM present -- must be BOM-less UTF-8"
    rows = load()
    cats = by_category(rows)
    print(f"rows      : {len(rows)}")
    print("categories: " + ", ".join(f"{c}={len(cats[c])}" for c in REQUIRED_CATEGORIES))

    for c in REQUIRED_CATEGORIES:
        assert cats[c], f"DAT-05 category '{c}' is empty"

    dia = {r["PairId"] for r in cats["diacritics"]}
    strip = {r["PairId"] for r in cats["stripped"]}
    assert dia == strip, f"unpaired diacritics/stripped rows: {dia ^ strip}"
    assert "" not in dia, "a paired row is missing its PairId"
    print(f"pairs     : {len(dia)} diacritics/stripped pairs, all matched")

    # The escaping convention exists for exactly these rows. If the round trip
    # is lossy, AC-28's empty/whitespace cases silently become the same case.
    em = [r["Input"] for r in cats["empty"]]
    assert "" in em, "no empty-string fixture survived the round trip"
    assert " " in em, "no single-space fixture survived the round trip"
    assert any("\n" in x for x in em), "no newline fixture survived the round trip"
    assert any("\t" in x for x in em), "no tab fixture survived the round trip"
    print("empty     : " + ", ".join(repr(x) for x in em))

    lens = sorted(len(r["Input"]) for r in cats["pathological"])
    assert lens[-1] > 10000, "no genuinely pathological input present"
    print(f"pathologic: lengths {lens}")

    ids = [r["Id"] for r in rows]
    assert len(ids) == len(set(ids)), "duplicate fixture Id"

    print("\nALL FIXTURE ASSERTIONS PASS")
    return 0


if __name__ == "__main__":
    if not os.path.isdir("datasheets"):
        print("error: run from the repository root", file=sys.stderr)
        raise SystemExit(1)
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(_verify())
