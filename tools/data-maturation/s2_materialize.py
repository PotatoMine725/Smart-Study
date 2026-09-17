# -*- coding: utf-8 -*-
"""S-2 test-instrument materialisation - blind reader sheet + private scoring key.

Reads the 20 sealed rows of the S-0 scored batch and renders the instrument the
section-10 reproducibility test is performed on. Authorised by the owner on
2026-09-05 as test-material preparation: **not adjudication and not measurement.**

Two artefacts, strictly separated by content:

  docs/specs/2026-09-05-s2-blind-reader-sheet.md
      Reader-facing. Item id + raw text + blank fields. Carries no hash, no
      source locator, no stratum, no historical label, no catalogue reference.

  datasheets/reservations/2026-09-05-s2-scoring-key.json
      Never handed to an annotator. Identity and allocation structure only -
      item id, sha256, occurrences, kind, contested stratum, D-2 class. It
      holds **no label value**; every label is derived at scoring time from
      the occurrences, so the key cannot anchor either pass.

Nothing here writes to the snapshot, the corpus, or the frozen specification.
Deterministic: same inputs, same two files, byte for byte.
"""
import csv
import hashlib
import io
import json
import os
import sys
from collections import OrderedDict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SNAPSHOT = os.path.join(ROOT, "datasheets", "reservations",
                        "2026-09-04-s0-reservation-snapshot.json")
SHEET = os.path.join(ROOT, "docs", "specs", "2026-09-05-s2-blind-reader-sheet.md")
KEY = os.path.join(ROOT, "datasheets", "reservations", "2026-09-05-s2-scoring-key.json")

PASSES = {
    "normalized_dataset.csv": "VanBanGoc",
    "normalized_dataset_m8a.csv": "InputText",
    "normalized_dataset_m8a_uniform.csv": "InputText",
}
# Presentation order. Its purpose is to INTERLEAVE contested and Difficulty-spread
# rows so the sheet itself carries no ambiguity signal. It is not obscurity: this
# salt is committed, so item -> snapshot index stays recomputable from the repo.
SALT = "s2-presentation-order-v1|"

GUIDELINE = "docs/specs/annotation-guideline.md"
GUIDELINE_SHA = "dd4fc2736d83c18c373161fc371070a967e082bbbf7987a142a434103684a433"


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load_corpus():
    """(filename, line) -> input text, using the S-0 line convention."""
    out = {}
    for fname, text_col in PASSES.items():
        path = os.path.join(ROOT, "datasheets", fname)
        with io.open(path, encoding="utf-8-sig", newline="") as f:
            for line, row in enumerate(csv.DictReader(f), start=2):
                out[(fname, line)] = row[text_col]
    return out


def resolve(entry, corpus):
    """Row text for one snapshot entry, verified against its recorded hash."""
    texts = set(corpus[(o["file"].split("/")[-1], o["line"])]
                for o in entry["occurrences"])
    if len(texts) != 1:
        sys.exit("%s resolves to %d distinct texts - stopping"
                 % (entry["hash"], len(texts)))
    text = texts.pop()
    if sha(text) != entry["hash"]:
        sys.exit("%s does not match its recorded hash - stopping" % entry["hash"])
    return text


ITEM = u"""### {item}

````text
{text}
````

| Field | Your answer |
|---|---|
| **TaskType** | |
| **Difficulty** | |
| **decided_by** | |
| **unresolved** | |

"""

HEADER = u"""# S-2 - blind annotation sheet, sealed scored batch

**Fill this in on your own.** Do not discuss the rows with the other annotator, and do not look
anything up in the project's data files. The only reference you use is the annotation guideline
`GuidelineVersion: v1`, which is supplied with this sheet.

Twenty short pieces of Vietnamese student text follow. For each one, record:

| Field | What to put |
|---|---|
| **TaskType** | Exactly one of `BaiTapVeNha`, `KiemTraThuongXuyen`, `ThiGiuaKy`, `ThiCuoiKy`, `DoAnCuoiKy` - see guideline section 2 |
| **Difficulty** | A whole number `1`-`5` - see the anchors in guideline section 5 |
| **decided_by** | Which rule decided it: a boundary rule `B-1`-`B-6` for TaskType, and the anchor level for Difficulty |
| **unresolved** | `false` normally. **`true` when no rule in guideline section 4 resolves the row** - in that case say so rather than guessing, and leave TaskType blank |

The rows are in no meaningful order. They are not ranked, not grouped, and not sorted by
difficulty. Some may be easy and some may not; nothing about the order tells you which.

Fields correspond one-to-one with the annotation record in guideline section 12.

## Your details

| | |
|---|---|
| **annotator** | |
| **role** | `gold` or `probe` |
| **date** | |
| **guideline_version** | `v1` |

---

"""

FOOT = u"""---

**When you are done**, return the sheet as it stands. Do not revise it after any discussion of the
rows with anyone else.
"""


def main():
    with io.open(SNAPSHOT, encoding="utf-8") as f:
        snap = json.load(f)
    batch = snap["partitions"]["scored_batch"]
    if len(batch) != 20:
        sys.exit("scored batch is %d rows, expected 20 - stopping" % len(batch))

    corpus = load_corpus()
    order = sorted(batch, key=lambda e: sha(SALT + e["hash"]))

    parts, entries = [HEADER], []
    for i, e in enumerate(order, start=1):
        item = "R-%02d" % i
        text = resolve(e, corpus)
        if "````" in text:
            sys.exit("%s contains a fence sequence - stopping" % item)
        parts.append(ITEM.format(item=item, text=text))

        k = OrderedDict()
        k["item"] = item
        k["hash"] = e["hash"]
        k["kind"] = e["kind"]
        if e["kind"] == "contested":
            k["stratum"] = e["stratum"]
            # D-2 diagnostic: rows contested on both axes count as shared-label.
            k["d2_class"] = ("third_pass_forced" if e["stratum"] == "third_only"
                             else "shared_label_cross_pass")
        k["occurrences"] = e["occurrences"]
        entries.append(k)
    parts.append(FOOT)

    sheet = u"".join(parts)
    with io.open(SHEET, "w", encoding="utf-8", newline="\n") as f:
        f.write(sheet)

    body = OrderedDict()
    body["event"] = "S-2 test-instrument materialisation"
    body["authority"] = ["owner authorisation 2026-09-05 (materialise the instrument)",
                         "S-2.2", "S-2.7", "S-2.10/H1", "S-2.12", "D-2 (2026-09-04)"]
    body["not_an_adjudication"] = (
        "Identity and allocation structure only. This file holds NO label value and NO Gold "
        "answer. Every label is derived at scoring time from the occurrences below, so this "
        "file cannot anchor either annotation pass.")
    body["never_hand_to_an_annotator"] = True
    body["derived_from"] = OrderedDict([
        ("snapshot", "datasheets/reservations/2026-09-04-s0-reservation-snapshot.json"),
        ("partition", "scored_batch"),
        ("snapshot_seal", snap["seal"]["value"]),
    ])
    body["reader_sheet"] = OrderedDict([
        ("path", "docs/specs/2026-09-05-s2-blind-reader-sheet.md"),
        ("sha256", hashlib.sha256(sheet.encode("utf-8")).hexdigest()),
        ("presentation_order", "sha256('%s' + row_hash) ascending" % SALT),
        ("purpose_of_that_order",
         "interleaves contested and Difficulty-spread rows so the sheet carries no "
         "ambiguity signal; it is not obscurity - this salt is committed"),
    ])
    body["guideline"] = OrderedDict([("path", GUIDELINE), ("version", "v1"),
                                     ("sha256", GUIDELINE_SHA)])
    body["items"] = entries
    payload = json.dumps(body, ensure_ascii=False, indent=2, sort_keys=False)
    body["seal"] = OrderedDict([
        ("algorithm", "sha256 over this document with the seal field absent"),
        ("value", hashlib.sha256(payload.encode("utf-8")).hexdigest()),
    ])
    with io.open(KEY, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(body, ensure_ascii=False, indent=2, sort_keys=False) + "\n")

    print("sheet", SHEET)
    print("sheet sha256", body["reader_sheet"]["sha256"])
    print("key  ", KEY)
    print("key seal", body["seal"]["value"])


if __name__ == "__main__":
    main()
