# -*- coding: utf-8 -*-
"""S-2 reader package - integrity and blindness validation.

Runs the pre-registered checks over a reader package directory and prints one
PASS/FAIL line per check. Exit status is 0 only when every check passes.

Usage:
    python tools/data-maturation/s2_reader_package_validate.py [package_dir]

`package_dir` defaults to docs/s2-reader-package/. It is a parameter so the
checks can be demonstrated FAILING against mutated copies - a validator that has
only ever been seen green is an assertion, not evidence.

The checks, and what makes each go red:

  C1  package holds exactly the three reader files          an extra/missing file
  C2  frozen v1 copy is byte-exact                          one flipped byte
  C3  sheet holds exactly 20 item blocks                    a deleted/added row
  C4  the 20 item blocks are byte-identical to b9691ae      any edit inside the instrument
  C5  every row hashes to a sealed scored_batch row, once   an edited or swapped row
  C6  no row outside the sealed scored batch appears        a row from the other 40 reserved
  C7  composition of the included rows is 12 + 8            a substituted row
  C8  item ids still reconcile against the scoring key      a renumbered or reordered item
  C9  reader files carry no scoring-key material            a hash, locator or seal leaking in
  C10 reader files carry no blindness leaks                 `gold`, `stratum`, a threshold, ...
  C11 reader files assert no scoring treatment (D-5)        "counts as", "correct outcome", ...
  C12 private artefacts are outside the package             manifest or key copied inside
  C13 the private manifest holds no label value             a `difficulty`/`task_type` field
  C14 package files are LF, as committed                    CRLF translation on checkout

C4 is the instrument check: it compares against the committed materialisation at
b9691ae rather than re-deriving anything, so a re-render that happened to differ
would fail here.

C6 is done by hash-set comparison, never by resolving the other 40 reserved rows:
the authorisation to read reserved text covers the instrument's 20 only.

C9/C10/C11 exempt 01-annotation-guideline-v1.md. Frozen v1 legitimately contains
its own section 6 catalogue and its section 10 thresholds, and the reader must
receive v1 EXACTLY - an excerpted v1 is a different instrument than the one
section 14 froze. Those prohibitions therefore bind the sheet and the
instructions, which is where the checks run.
"""
import hashlib
import io
import json
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from s2_reader_terms_vi import BANNED_VI, D5_CLAIMS_VI, TRIGGERS

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SNAPSHOT = os.path.join(ROOT, "datasheets", "reservations",
                        "2026-09-04-s0-reservation-snapshot.json")
MANIFEST = os.path.join(ROOT, "datasheets", "reservations",
                        "2026-09-05-s2-reader-package-manifest.json")
KEY = os.path.join(ROOT, "datasheets", "reservations", "2026-09-05-s2-scoring-key.json")

GUIDELINE_SHA = "dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433"
SHEET_COMMIT = "b9691ae131ad4b71d6e69a4b197d3a2de02757ec"
SHEET_PATH = "docs/specs/2026-09-05-s2-blind-reader-sheet.md"

RENDERING_FILE = "01b-huong-dan-tieng-viet.md"
EXPECTED_FILES = ["00-READ-ME-FIRST.md", "01-annotation-guideline-v1.md",
                  RENDERING_FILE, "02-annotation-sheet.md"]
GUIDELINE_FILE = "01-annotation-guideline-v1.md"
BLIND_FILES = ["00-READ-ME-FIRST.md", "02-annotation-sheet.md"]

# Terms that would tell a reader something about the measurement, the selection,
# or the history of a row.
BANNED = [
    "gold", "probe", "supplementary", "stratum", "strata", "third_only",
    "third-pass", "third_pass_forced", "shared_label", "shared-label",
    "contested", "difficulty_spread", "difficulty-spread", "sealed", "seal",
    "reserved", "reservation", "snapshot", "scored batch", "scored_batch",
    "17/20", "18/20", "85%", "90%", "threshold", "catalogue", "ambiguous",
    "normalized_dataset", "scoring", "scoring-key", "scoring_key", "answer key",
    "expected answer", "historical label", "pass label", "kiem tra defect",
    "s0-reservation", "b9691ae", "da98e73", "prediction", "predicted",
    "adjudicat", "disagree", "agreement rate", "other annotator",
]
# D-5: the owner has not authorised how an `unresolved` response scores. The
# reader files may preserve the ability to mark it and must claim nothing about
# how it is treated.
D5_CLAIMS = [
    "correct outcome", "correct answer here", "counts as", "does not count",
    "scored as", "is acceptable", "no penalty", "will not count", "counts toward",
    "counts against", "does not affect", "we accept", "is fine",
]
LABEL_FIELDS = ["difficulty", "task_type", "tasktype", "label", "gold_answer",
                "expected", "answer", "stratum", "d2_class", "kind"]

# Against a Vietnamese package the English lists above would pass vacuously.
BANNED = BANNED + BANNED_VI
D5_CLAIMS = D5_CLAIMS + D5_CLAIMS_VI

FENCE = re.compile(u"^````text\n(.*?)\n````$", re.M | re.S)


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def sha_file(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def read(path):
    with io.open(path, encoding="utf-8", newline="") as f:
        return f.read()


def blocks_of(text):
    """The 20 item blocks, in order, exactly as they appear."""
    if u"### R-01" not in text:
        return []
    first = text.index(u"### R-01")
    # Cut on the `---` rule that closes the last item, never on the footer's
    # words: the footer is prose, and translating it must not make the
    # instrument itself look changed.
    m = re.search(u"\n---\n\n[*][*]", text[first:])
    tail = first + m.start() + 1 if m else len(text)
    return [b for b in re.split(r"(?m)^(?=### R-\d\d$)", text[first:tail]) if b.strip()]


class Checks(object):
    def __init__(self):
        self.rows = []

    def add(self, cid, what, ok, detail=""):
        self.rows.append((cid, what, bool(ok), detail))

    def report(self):
        for cid, what, ok, detail in self.rows:
            print("%-5s %-54s %s%s" % (cid, what, "PASS" if ok else "FAIL",
                                       ("  <- " + detail) if detail else ""))
        failed = sum(1 for r in self.rows if not r[2])
        print("")
        print("RESULT: %s (%d checks, %d failed)"
              % ("PASS" if failed == 0 else "FAIL", len(self.rows), failed))
        return 1 if failed else 0


def main():
    pkg = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "docs", "s2-reader-package")
    pkg = os.path.abspath(pkg)
    c = Checks()
    print("package: %s\n" % pkg)

    # --- C1 file set -------------------------------------------------------
    present = sorted(os.listdir(pkg)) if os.path.isdir(pkg) else []
    c.add("C1", "package holds exactly the 4 reader files",
          present == sorted(EXPECTED_FILES), "found %r" % (present,))

    # --- C2 frozen v1 ------------------------------------------------------
    gpath = os.path.join(pkg, GUIDELINE_FILE)
    got = sha_file(gpath) if os.path.isfile(gpath) else "<missing>"
    c.add("C2", "frozen v1 copy is byte-exact (dd4fc273...684a433)",
          got == GUIDELINE_SHA, "sha256 %s" % got)

    # --- the instrument ----------------------------------------------------
    spath = os.path.join(pkg, "02-annotation-sheet.md")
    sheet = read(spath) if os.path.isfile(spath) else u""
    pkg_blocks = blocks_of(sheet)

    src = subprocess.check_output(
        ["git", "cat-file", "blob", "%s:%s" % (SHEET_COMMIT, SHEET_PATH)],
        cwd=ROOT).decode("utf-8")
    src_blocks = blocks_of(src)

    c.add("C3", "sheet holds exactly 20 item blocks", len(pkg_blocks) == 20,
          "found %d" % len(pkg_blocks))
    same = [a == b for a, b in zip(pkg_blocks, src_blocks)]
    c.add("C4", "20 item blocks byte-identical to b9691ae materialisation",
          len(pkg_blocks) == len(src_blocks) == 20 and all(same),
          "%d of %d identical" % (sum(same), len(src_blocks)))

    texts = FENCE.findall(sheet)
    hashes = [sha(t) for t in texts]
    with io.open(SNAPSHOT, encoding="utf-8") as f:
        snap = json.load(f)
    batch = snap["partitions"]["scored_batch"]
    batch_hashes = [e["hash"] for e in batch]
    kind_of = dict((e["hash"], e["kind"]) for e in batch)

    c.add("C5", "every row matches a sealed scored_batch row by hash",
          sorted(hashes) == sorted(batch_hashes) and len(set(hashes)) == len(hashes),
          "%d of %d matched, %d duplicated"
          % (len(set(hashes) & set(batch_hashes)), len(batch_hashes),
             len(hashes) - len(set(hashes))))
    outside = [h for h in hashes if h not in kind_of]
    c.add("C6", "no row outside the sealed 20 appears", not outside,
          "%d foreign row(s)" % len(outside))
    kinds = {}
    for h in hashes:
        k = kind_of.get(h, "UNKNOWN")
        kinds[k] = kinds.get(k, 0) + 1
    c.add("C7", "composition is 12 contested + 8 Difficulty-spread",
          kinds == {"contested": 12, "difficulty_spread": 8}, "%r" % (kinds,))

    ids = [re.match(r"### (R-\d\d)", b).group(1) for b in pkg_blocks]
    with io.open(KEY, encoding="utf-8") as f:
        key = json.load(f)
    key_map = dict((k["item"], k["hash"]) for k in key["items"])
    reconciles = (ids == ["R-%02d" % n for n in range(1, 21)]
                  and all(key_map.get(i) == h for i, h in zip(ids, hashes)))
    c.add("C8", "item ids reconcile against the private scoring key", reconciles,
          "%d id(s), %d resolve to the key's hash"
          % (len(ids), sum(1 for i, h in zip(ids, hashes) if key_map.get(i) == h)))

    # --- C9/C10/C11 scans over the blind files -----------------------------
    blind = {}
    for name in BLIND_FILES:
        p = os.path.join(pkg, name)
        blind[name] = read(p) if os.path.isfile(p) else u""
    joined = u"\n".join(blind.values())
    low = joined.lower()
    rpath = os.path.join(pkg, RENDERING_FILE)
    rendering = read(rpath) if os.path.isfile(rpath) else u""
    # The rendering mirrors frozen v1, so C10's measurement terms exempt it
    # exactly as they exempt the v1 copy - v1 carries its own thresholds.
    # C9 and C11 still bind it: a rendering may carry no row identity, and
    # may claim nothing about how a response scores.
    low9 = (joined + u"\n" + rendering).lower()

    key_material = list(batch_hashes) + [snap["seal"]["value"], key["seal"]["value"]]
    key_material += ["normalized_dataset.csv", "normalized_dataset_m8a.csv",
                     "normalized_dataset_m8a_uniform.csv"]
    hits9 = [m for m in key_material if m.lower() in low9]
    c.add("C9", "no row hash, locator or seal in reader files or rendering",
          not hits9, "%d hit(s): %r" % (len(hits9), hits9[:3]))

    hits10 = [t for t in BANNED if t.lower() in low]
    c.add("C10", "no historical-label or measurement term in reader files",
          not hits10, "%d hit(s): %r" % (len(hits10), hits10[:5]))

    hits11 = [t for t in D5_CLAIMS if t.lower() in low9]
    c.add("C11", "no claim about how a response scores (D-5)",
          not hits11, "%d hit(s): %r" % (len(hits11), hits11[:5]))

    # --- C12 separation ----------------------------------------------------
    stray = [n for n in present if n not in EXPECTED_FILES]
    c.add("C12", "manifest and scoring key are outside the package",
          not stray
          and not os.path.exists(os.path.join(pkg, os.path.basename(MANIFEST)))
          and not os.path.exists(os.path.join(pkg, os.path.basename(KEY))),
          "stray %r" % (stray,))

    # --- C13 manifest holds no label --------------------------------------
    if os.path.isfile(MANIFEST):
        raw = read(MANIFEST)
        man = json.loads(raw)

        def walk(node, path=""):
            bad = []
            if isinstance(node, dict):
                for k, v in node.items():
                    if k.lower() in LABEL_FIELDS:
                        bad.append(path + "/" + k)
                    bad += walk(v, path + "/" + k)
            elif isinstance(node, list):
                for i, v in enumerate(node):
                    bad += walk(v, "%s[%d]" % (path, i))
            return bad

        bad = walk(man)
        leaked = [h for h in batch_hashes if h in raw]
        c.add("C13", "private manifest carries no label value and no row hash",
              not bad and not leaked,
              "fields %r, %d row hash(es)" % (bad[:4], len(leaked)))
    else:
        c.add("C13", "private manifest carries no label value and no row hash",
              False, "manifest missing")

    # --- C14 line endings --------------------------------------------------
    crlf = [n for n in present
            if os.path.isfile(os.path.join(pkg, n))
            and open(os.path.join(pkg, n), "rb").read().count(b"\r\n")]
    c.add("C14", "package files are LF, as committed", not crlf, "CRLF in %r" % (crlf,))

    # --- C15/C16/C17 the translation --------------------------------------
    bare = re.sub(r"(?s)```.*?```", u"", rendering)
    bare = re.sub(r"`[^`]*`", u"", bare)
    loose = [t for t in TRIGGERS if t in bare]
    c.add("C15", "B-2/B-4 trigger terms appear only inside code spans",
          bool(rendering) and not loose,
          "%d loose term(s)" % len(loose) if rendering else "rendering missing")

    gtext = read(gpath) if os.path.isfile(gpath) else u""
    want = re.findall(r"(?m)^## (\d+)\.", gtext)
    have = re.findall(r"(?m)^## (\d+)\.", rendering)
    c.add("C16", "rendering mirrors v1's section structure",
          bool(want) and have == want, "%d of %d section(s)" % (len(have), len(want)))

    # Guards the known v1 catalogue overlap from being carried into the
    # rendering if section 6 is translated later: no reserved row text may
    # appear in a file the reader reads before annotating.
    echoed = [i for i, t in enumerate(texts, 1) if t.strip() and t.strip() in rendering]
    c.add("C17", "no reserved row text appears in the rendering",
          not echoed, "%d echoed row(s)" % len(echoed))

    return c.report()


if __name__ == "__main__":
    sys.exit(main())
