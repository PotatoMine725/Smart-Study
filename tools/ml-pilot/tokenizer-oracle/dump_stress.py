#!/usr/bin/env python3
"""Whitespace/edge stress corpus for TOK-02, beyond the DAT-05 fixtures.

WP-0.7. The DAT-05 set is the CONTRACT surface; this corpus exists because the
first tokenization diff exposed a trailing-whitespace divergence that the
realistic fixtures happened not to contain. Characterising a divergence honestly
means probing the axis it lives on, not only the axis that found it.

Writes tools/ml-pilot/results/stress_tokens.json  (reference ids).
"""
import json, os, sys
sys.path.insert(0, os.path.join("tools", "ml-pilot"))
from tokenizers import Tokenizer

BASE = ["", "a", "x", "thi giữa kỳ", "tgk giai tich", "thigiuaky csdl", "a b", "đồ án"]
WS = ["", " ", "  ", "\t", " \t ", "\n", "\r\n", "   \t\n  "]

CASES = []
for b in BASE:
    for w in WS:
        CASES.append(w + b)          # leading
        CASES.append(b + w)          # trailing
        CASES.append(w + b + w)      # both
CASES += ["a  b", "a   b", "a\tb", "a\nb", "a \t b", "thi  giữa   kỳ", "-", ".", "!!!", "123", "…", "🙂", "a🙂b"]
CASES = list(dict.fromkeys(CASES))   # dedupe, order-stable

ARMS = {
    "arm_a": ("tools/ml-pilot/models/arm_a/tokenizer.json", "task: classification | query: ", 2048),
    "arm_b": ("tools/ml-pilot/models/arm_b/onnx/tokenizer.json", "query: ", 512),
}

out = {"cases": CASES, "arms": {}}
for arm, (tj, prefix, maxlen) in ARMS.items():
    t = Tokenizer.from_file(tj); t.enable_truncation(max_length=maxlen)
    out["arms"][arm] = {"prefix": prefix, "ids": [t.encode(prefix + c).ids for c in CASES]}
    print(f"{arm}: {len(CASES)} cases")

dest = "tools/ml-pilot/results/stress_tokens.json"
json.dump(out, open(dest, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("wrote", dest)
