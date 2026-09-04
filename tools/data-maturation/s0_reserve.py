"""S-0 reservation event — materialise the sealed snapshot.

Executes the rule pre-registered in
docs/plans/2026-09-04-s0-reservation-preregistration.md, which was committed before
this script ran. Deterministic: same inputs, same snapshot, byte for byte.

Row text is never written to the snapshot. The SHA-256 of the input text is the
locator (S-2.10/H1); the text itself stays in the corpus.
"""
import csv
import hashlib
import io
import json
import os
import subprocess
import sys
from collections import Counter, OrderedDict

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PASSES = [
    ("normalized_dataset.csv", "VanBanGoc", "LoaiTask", "DoKho"),
    ("normalized_dataset_m8a.csv", "InputText", "TaskType", "Difficulty"),
    ("normalized_dataset_m8a_uniform.csv", "InputText", "TaskType", "Difficulty"),
]
RETIRED = {"DuAn", "Khac"}          # old labels the new taxonomy dropped — forced moves
NON_PRODUCTION = {"NhacNho", "OnTap"}  # classes not in the production enum


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load(fname, text_col, label_col, diff_col):
    """text -> (first line, label, difficulty); plus every (file, line) occurrence."""
    first, occ = {}, {}
    path = os.path.join(ROOT, "datasheets", fname)
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        for line, row in enumerate(csv.DictReader(f), start=2):
            t = row[text_col]
            occ.setdefault(t, []).append(line)
            if t not in first:
                first[t] = (line, row[label_col], row[diff_col])
    return first, occ


def largest_remainder(total, sizes):
    """Proportional allocation. Ties break toward the larger stratum, then name asc."""
    pool = sum(sizes.values())
    exact = dict((k, total * v / float(pool)) for k, v in sizes.items())
    alloc = dict((k, int(v)) for k, v in exact.items())
    left = total - sum(alloc.values())
    order = sorted(sizes, key=lambda k: (-(exact[k] - alloc[k]), -sizes[k], k))
    for k in order[:left]:
        alloc[k] += 1
    return alloc


def main():
    p1, occ1 = load(*PASSES[0])
    p2, occ2 = load(*PASSES[1])
    p3, occ3 = load(*PASSES[2])

    common = set(p1) & set(p2)
    genuine = set(t for t in common
                  if p1[t][1] != p2[t][1] and p1[t][1] not in RETIRED)
    cross = set(t for t in genuine
                if p1[t][1] not in NON_PRODUCTION and p2[t][1] not in NON_PRODUCTION)
    shared = set(p2) & set(p3)
    third = set(t for t in shared
                if p2[t][1] != p3[t][1]
                and p2[t][1] not in NON_PRODUCTION and p3[t][1] not in NON_PRODUCTION)

    pool = cross | third
    strata = {
        "cross_only": sorted(cross - third, key=sha),
        "third_only": sorted(third - cross, key=sha),
        "both": sorted(cross & third, key=sha),
    }
    sizes = dict((k, len(v)) for k, v in strata.items())
    if len(pool) != 133:
        sys.exit("pool is %d, ruled master pool is 133 — stopping" % len(pool))

    cursor = dict((k, 0) for k in strata)

    def take_contested(n):
        out = []
        for name, count in sorted(largest_remainder(n, sizes).items()):
            rows = strata[name][cursor[name]:cursor[name] + count]
            cursor[name] += count
            out.extend((t, name) for t in rows)
        return out

    scored_contested = take_contested(12)
    q1_batch = take_contested(20)
    retest_contested = take_contested(12)

    # Difficulty spread: not contested, so it measures the Difficulty axis only.
    rest = common - pool
    by_level = {}
    for t in rest:
        by_level.setdefault(p2[t][2], []).append(t)
    for lv in by_level:
        by_level[lv].sort(key=sha)
    levels = sorted(by_level)
    # one per level, surplus to the levels the v1 threshold governs (S-2.3)
    spread_alloc = dict((lv, 1) for lv in levels)
    for lv in [l for l in ("3", "4", "5") if l in spread_alloc]:
        spread_alloc[lv] += 1
    lvcur = dict((lv, 0) for lv in levels)

    def take_spread():
        out = []
        for lv in levels:
            n = spread_alloc[lv]
            rows = by_level[lv][lvcur[lv]:lvcur[lv] + n]
            lvcur[lv] += n
            out.extend((t, lv) for t in rows)
        return out

    scored_spread = take_spread()
    retest_spread = take_spread()

    def entry(text, stratum, kind):
        occ = []
        for (fname, _, _, _), table in zip(PASSES, (occ1, occ2, occ3)):
            for line in table.get(text, []):
                occ.append(OrderedDict([("file", "datasheets/" + fname), ("line", line)]))
        e = OrderedDict()
        e["hash"] = sha(text)
        e["kind"] = kind
        e["stratum" if kind == "contested" else "difficulty"] = stratum
        e["occurrences"] = occ
        return e

    def partition(contested, spread):
        rows = [entry(t, s, "contested") for t, s in contested]
        rows += [entry(t, lv, "difficulty_spread") for t, lv in spread]
        return sorted(rows, key=lambda e: e["hash"])

    partitions = OrderedDict()
    partitions["scored_batch"] = partition(scored_contested, scored_spread)
    partitions["q1_timed_adjudication_batch"] = partition(q1_batch, [])
    partitions["clean_retest_batch"] = partition(retest_contested, retest_spread)

    # Disjointness: every reserved hash appears exactly once across all partitions.
    allh = [e["hash"] for rows in partitions.values() for e in rows]
    dupes = [h for h, c in Counter(allh).items() if c > 1]

    body = OrderedDict()
    body["event"] = "S-0 reservation"
    body["authority"] = ["S-1.2", "S-1.6", "S-2.3", "S-2.5", "S-2.6",
                         "S-2.10/H1", "S-2.10/H3", "owner ruling 2026-09-04 (pool)"]
    body["pre_registration"] = "docs/plans/2026-09-04-s0-reservation-preregistration.md"
    body["identity_key"] = "sha256(input_text, utf-8); occurrences give source file + line"
    body["master_contested_pool"] = OrderedDict([
        ("distinct_rows", len(pool)),
        ("strata", OrderedDict(sorted(sizes.items()))),
        ("note", "133 distinct rows. 157 is a count of contested appearances and is "
                 "not a pool size; the two dimensions are never added or equated."),
    ])
    body["difficulty_spread_universe"] = OrderedDict([
        ("rows", len(rest)),
        ("levels_present", OrderedDict(sorted(Counter(p2[t][2] for t in rest).items()))),
        ("v1_threshold_governs", ["3", "4", "5"]),
        ("note", "Levels 1-2 are represented so the batch spans the range, but the v1 "
                 "Difficulty threshold does not govern them (S-2.3)."),
    ])
    body["partitions"] = partitions
    body["reserved_total"] = len(allh)
    return body, dupes, pool, cross, third, p1, p2, p3


if __name__ == "__main__":
    body, dupes, pool, cross, third, p1, p2, p3 = main()
    if dupes:
        sys.exit("partitions overlap on %d hashes — stopping" % len(dupes))

    stamp = subprocess.check_output(
        ["git", "log", "-1", "--format=%cI"], cwd=ROOT).decode().strip()
    head = subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip()
    body["timestamp"] = stamp
    body["pre_registration_commit"] = head

    payload = json.dumps(body, ensure_ascii=False, indent=2, sort_keys=False)
    body["seal"] = OrderedDict([
        ("algorithm", "sha256 over this document with the seal field absent"),
        ("value", hashlib.sha256(payload.encode("utf-8")).hexdigest()),
    ])

    out = os.path.join(ROOT, "datasheets", "reservations",
                       "2026-09-04-s0-reservation-snapshot.json")
    if not os.path.isdir(os.path.dirname(out)):
        os.makedirs(os.path.dirname(out))
    with io.open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(body, ensure_ascii=False, indent=2, sort_keys=False) + "\n")
    print("wrote", out)
    print("seal", body["seal"]["value"])
