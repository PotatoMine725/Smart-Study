"""One-off byte-safe merge: append collected_v4 rows into seed_intents.csv.

Run dry-run:  python _merge_seed.py
Apply:        python _merge_seed.py --apply

Avoids the PowerShell BOM-less-UTF-8 mojibake trap by reading/writing bytes
in python and decoding strictly as UTF-8.
"""
import csv, io, sys, re

SEED = r"D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\Services\ML\TextClassifier\seed_intents.csv"
COLLECTED = r"D:\Code\C#\SmartStudyPlanner\datasheets\collected_v4.csv"
VALID = {"BaiTapVeNha", "KiemTraThuongXuyen", "ThiGiuaKy", "DoAnCuoiKy", "ThiCuoiKy"}

def read_text(path):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    txt = raw.decode("utf-8")  # strict; raises on corruption
    if bom:
        txt = txt.lstrip("﻿")
    return raw, txt, bom

def norm(s):
    return re.sub(r"\s+", " ", (s or "").strip().lower())

def rows(txt):
    return list(csv.reader(io.StringIO(txt)))

def main():
    apply = "--apply" in sys.argv
    seed_raw, seed_txt, seed_bom = read_text(SEED)
    col_raw, col_txt, col_bom = read_text(COLLECTED)

    seed_rows = rows(seed_txt)
    col_rows = rows(col_txt)
    seed_hdr, col_hdr = seed_rows[0], col_rows[0]

    print(f"seed: bom={seed_bom} header={seed_hdr} data_rows={len(seed_rows)-1}")
    print(f"collected: bom={col_bom} header={col_hdr} data_rows={len(col_rows)-1}")

    if seed_hdr != col_hdr:
        print("FATAL: header mismatch (order/names differ). Aborting.")
        sys.exit(1)

    idx = {c: i for i, c in enumerate(seed_hdr)}
    i_text, i_type, i_diff = idx["InputText"], idx["TaskType"], idx["Difficulty"]

    seen = {norm(r[i_text]) for r in seed_rows[1:]}
    survivors, errors, dups = [], [], 0
    for ln, r in enumerate(col_rows[1:], start=2):
        if len(r) != len(seed_hdr):
            errors.append(f"L{ln}: field count {len(r)} != {len(seed_hdr)}")
            continue
        t, ty, df = r[i_text].strip(), r[i_type].strip(), r[i_diff].strip()
        if not t:
            errors.append(f"L{ln}: empty InputText"); continue
        if ty not in VALID:
            errors.append(f"L{ln}: invalid TaskType '{ty}'"); continue
        if df:
            try: float(df)
            except ValueError: errors.append(f"L{ln}: bad Difficulty '{df}'"); continue
        n = norm(t)
        if n in seen:
            dups += 1; continue
        seen.add(n)
        survivors.append(r)

    print(f"survivors={len(survivors)} dups_skipped={dups} errors={len(errors)}")
    for e in errors[:20]:
        print("  ", e)

    from collections import Counter
    after = Counter(r[i_type].strip() for r in seed_rows[1:])
    for r in survivors:
        after[r[i_type].strip()] += 1
    print("projected per-class:", dict(after), "total=", sum(after.values()))

    if errors:
        print("Has errors -> NOT writing."); sys.exit(1)

    if not apply:
        print("DRY-RUN ok. Re-run with --apply to write."); return

    term = "\r\n" if b"\r\n" in seed_raw else "\n"
    base = seed_raw
    if not base.endswith(term.encode("utf-8")):
        base += term.encode("utf-8")
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator=term)
    for r in survivors:
        w.writerow(r)
    out = base + buf.getvalue().encode("utf-8")
    open(SEED, "wb").write(out)
    # re-validate written file
    _, chk_txt, _ = read_text(SEED)
    chk = rows(chk_txt)
    print(f"WROTE {len(out)} bytes; seed now {len(chk)-1} data rows.")

if __name__ == "__main__":
    main()
