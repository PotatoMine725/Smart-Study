#!/usr/bin/env python3
"""Generate the DAT-05 shared Vietnamese input fixture set.

WP-0.2. Writes `datasheets/vn_input_fixtures.csv`, the SINGLE fixture set that
AC-06, AC-13, AC-17 and AC-30 all read. DAT-05 requires one committed set --
without it those four criteria are four different claims wearing the same words,
and "no behaviour delta" stops being comparable between slices.

Run from the repository root:  python tools/ml-pilot/build_fixtures.py

Deterministic: re-running produces a byte-identical file.

Realistic rows are drawn from `datasheets/collected_v4.csv`. That file is NOT real
user input: it is AI-generated (Meta AI, from owner templates) and AI-labelled
(GitHub Copilot) -- owner ruling 2026-08-26, DFD-1. The fixtures reflect how an
authoring process rendered student input, not how students actually type, and no
result derived from them may be reported as evidence about real input.
"""

import csv
import os
import sys

OUT = os.path.join("datasheets", "vn_input_fixtures.csv")

# --- diacritics / stripped pairs -------------------------------------------------
# Each pair is the SAME semantic content, one row with correct Vietnamese
# diacritics and one with them removed. AC-30 measures preprocessing
# independence as a within-pair comparison; unrelated inputs would not support
# that claim. Sources noted per pair.
PAIRS = [
    # (diacritics, stripped, note)
    ("thi giữa kỳ giải tích tuần sau",
     "thi giua ky giai tich tuan sau",
     "canonical mid-term phrasing; collected_v4 contains both spellings"),
    ("tuần sau thi giữa kì ktvm, chết t r",
     "tuan sau thi giua ki ktvm, chet t r",
     "verbatim collected_v4 row + its stripped partner; slang tail retained"),
    ("mai nộp bài tập về nhà môn cấu trúc dữ liệu",
     "mai nop bai tap ve nha mon cau truc du lieu",
     "homework class; 'kì' vs 'kỳ' variation deliberately not normalised"),
    ("đồ án cuối kỳ môn web, nhóm 3 người",
     "do an cuoi ky mon web, nhom 3 nguoi",
     "final-project class, drawn from a collected_v4 row"),
    ("15/12 deadline cuối kỳ môn web, đồ án nhóm 3 người",
     "15/12 deadline cuoi ky mon web, do an nhom 3 nguoi",
     "verbatim collected_v4 row; carries a date the deadline extractor reads"),
    ("t4 tuần tới thi giữa kỳ xstk, ai có đề k",
     "t4 tuan toi thi giua ky xstk, ai co de k",
     "verbatim collected_v4 row; abbreviated weekday + abbreviated subject"),
    ("thứ 2 thi giữa kì toán rồi mà tớ chưa ôn gì hết",
     "thu 2 thi giua ki toan roi ma to chua on gi het",
     "long conversational form; the label sits at the front"),
    ("báo cáo thực tập nộp cuối tháng này",
     "bao cao thuc tap nop cuoi thang nay",
     "phrasing outside the three covered classes -- exercises the fallback path"),
]

RUNTOGETHER = [
    ("thigiuaky csdl tuan sau, kho v~",
     "verbatim collected_v4 row -- run-together label + abbreviated subject"),
    ("baitapvenha mon toan roi rac",
     "run-together homework label"),
    ("doancuoiky lap trinh web",
     "run-together final-project label"),
    ("thicuoiky vatly1 t7 nay",
     "run-together label for a class ABSENT from the real evaluation subset"),
    ("kiemtrathuongxuyen tieng anh mai",
     "run-together label for the other class absent from the real subset"),
    ("mai thigiuakymonhoadaicuong chua on gi",
     "run-together label fused to a run-together subject -- worst case for a "
     "whitespace-dependent tokenizer"),
]

ABBREV = [
    ("tgk giải tích tuần sau má ơi cứu",
     "verbatim collected_v4 row -- 'tgk' = thi giua ky, plus slang"),
    ("tgk anh 2 t6 tuần sau nha",
     "verbatim collected_v4 row -- abbreviation + abbreviated weekday"),
    ("btvn xstk han thu 5",
     "'btvn' = bai tap ve nha, 'xstk' = xac suat thong ke"),
    ("dacn csdl nop tuan sau",
     "'dacn' = do an chuyen nganh, 'csdl' = co so du lieu"),
    ("tuần sau nữa tgk mác, xỉu ngang",
     "verbatim collected_v4 row -- abbreviation WITH diacritics elsewhere"),
    ("tgk ktvm + xstk cung tuan, toang",
     "two abbreviated subjects in one input"),
    ("dk thi lai ktct truoc t6",
     "'ktct' = kinh te chinh tri; phrasing the heuristic parser handles weakly"),
    ("nop bc ttcs cho thay mai",
     "chained abbreviations with no diacritics anywhere"),
]

# FLB-01 / AC-28: empty and whitespace-only input must complete the parse call
# and return the heuristic result. Stored escaped -- see the header comment in
# the companion .md.
EMPTY = [
    ("", "empty string"),
    (" ", "single space"),
    ("   ", "three spaces"),
    ("\t \t", "tabs and spaces"),
    ("\n", "bare newline -- survives the round trip only because inputs are escaped"),
    ("  \r\n  ", "CRLF surrounded by spaces"),
]

# FLB-01 / AC-28: pathologically long input. Sized against the candidates'
# sequence limits (e5-small 512 tokens, EmbeddingGemma 2048) so truncation
# behaviour is actually exercised rather than assumed.
PATHOLOGICAL = [
    (("thi giữa kỳ giải tích tuần sau " * 170).strip(),
     "~5k chars of repeated realistic Vietnamese -- exceeds both candidates' "
     "sequence limits with diacritics intact"),
    (("thigiuakygiaitichtuansau" * 85),
     "~2k chars with NO whitespace at all -- worst case for any tokenizer that "
     "leans on word boundaries"),
    (("bai tap ve nha mon cau truc du lieu va giai thuat han nop cuoi tuan nay " * 280).strip(),
     "~20k chars -- an order of magnitude past anything a user types, present "
     "so the latency tail and any input bounding are observable rather than "
     "theoretical"),
]


def esc(s: str) -> str:
    """Escape a fixture input for storage.

    EVERY row is stored escaped, uniformly, so consumers have exactly one code
    path and no conditional. Vietnamese text contains no backslashes, so this is
    a no-op for the realistic rows and the file stays readable.
    """
    return (s.replace("\\", "\\\\")
             .replace("\t", "\\t")
             .replace("\r", "\\r")
             .replace("\n", "\\n"))


def main() -> int:
    if not os.path.isdir("datasheets"):
        print("error: run from the repository root", file=sys.stderr)
        return 1

    rows = []
    n = 0

    def add(category, text, note, pair_id=""):
        nonlocal n
        n += 1
        rows.append({
            "Id": f"F{n:03d}",
            "Category": category,
            "PairId": pair_id,
            "Input": esc(text),
            "Note": note,
        })

    for i, (dia, strip, note) in enumerate(PAIRS, start=1):
        pid = f"P{i:02d}"
        add("diacritics", dia, note, pid)
        add("stripped", strip, note, pid)

    for text, note in RUNTOGETHER:
        add("runtogether", text, note)
    for text, note in ABBREV:
        add("abbrev", text, note)
    for text, note in EMPTY:
        add("empty", text, note)
    for text, note in PATHOLOGICAL:
        add("pathological", text, note)

    os.makedirs("datasheets", exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["Id", "Category", "PairId", "Input", "Note"],
                           quoting=csv.QUOTE_ALL, lineterminator="\n")
        w.writeheader()
        w.writerows(rows)

    print(f"wrote {OUT}: {len(rows)} rows")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
