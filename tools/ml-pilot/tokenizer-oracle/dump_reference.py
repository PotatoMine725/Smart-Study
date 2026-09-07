#!/usr/bin/env python3
"""Reference-tokenizer oracle for TOK-02 / EVA-08 output 6.

WP-0.7. Dumps the token id sequence each candidate's OWN reference tokenizer
produces for every DAT-05 fixture, so the .NET route can be diffed against it
element-wise.

    python tools/ml-pilot/tokenizer-oracle/dump_reference.py

Writes `tools/ml-pilot/results/reference_tokens.json`.

WHY THIS EXISTS
    TOK-02 is verified, never assumed. Silent divergence from the reference
    tokenizer degrades the encoder to noise WHILE APPEARING TO WORK -- the model
    still returns a vector, the head still returns a label, and nothing fails.
    Comparing a .NET tokenizer against itself would detect none of that.

    XLM-RoBERTa (Arm B) is the specific reason this is not paranoia: HuggingFace
    applies a fairseq id offset over the raw SentencePiece ids. A .NET
    SentencePiece reader that returns raw ids produces a sequence that looks
    entirely plausible and is wrong in every position.

PYTHON IS THE ORACLE, NEVER THE ROUTE
    TOK-03's "no non-.NET runtime dependency" binds the SHIPPED tokenization
    route -- what the product would execute. It does not bind a throwaway
    verification harness outside the solution. A route that shells out to Python
    is not a route; a Python program that tells you whether the .NET route is
    correct is exactly what TOK-02 asks for.
"""

import json
import os
import sys

sys.path.insert(0, os.path.join("tools", "ml-pilot"))
from fixtures import load  # noqa: E402

from tokenizers import Tokenizer  # noqa: E402

ARMS = {
    "arm_a": {
        "label": "EmbeddingGemma-300M",
        "tokenizer_json": "tools/ml-pilot/models/arm_a/tokenizer.json",
        # EmbeddingGemma's own prompt template for classification-style use.
        # Recorded, not invented -- see tools/ml-pilot/README.md §2.3.
        "prefix": "task: classification | query: ",
        "max_len": 2048,
    },
    "arm_b": {
        "label": "multilingual-e5-small",
        "tokenizer_json": "tools/ml-pilot/models/arm_b/onnx/tokenizer.json",
        # e5 is trained with a "query: " / "passage: " prefix; the model card
        # directs "query: " for non-retrieval tasks.
        "prefix": "query: ",
        "max_len": 512,
    },
}


def main():
    if not os.path.isdir("tools"):
        print("error: run from the repository root", file=sys.stderr)
        return 1

    rows = load()
    out = {"_note": "Reference token ids per arm per fixture. Produced by the "
                    "HuggingFace `tokenizers` library from each candidate's own "
                    "tokenizer.json. This is the oracle the .NET route is diffed "
                    "against (TOK-02).",
           "arms": {}}

    for arm, cfg in ARMS.items():
        tok = Tokenizer.from_file(cfg["tokenizer_json"])
        tok.enable_truncation(max_length=cfg["max_len"])
        entries = {}
        for r in rows:
            text = cfg["prefix"] + r["Input"]
            enc = tok.encode(text)
            entries[r["Id"]] = {
                "category": r["Category"],
                "ids": enc.ids,
                "n": len(enc.ids),
                # First 12 tokens only -- enough to make a diff readable in a
                # report without pasting a 2 000-token sequence into it.
                "head_tokens": enc.tokens[:12],
            }
        out["arms"][arm] = {
            "label": cfg["label"],
            "tokenizer_json": cfg["tokenizer_json"],
            "prefix": cfg["prefix"],
            "max_len": cfg["max_len"],
            "library": "huggingface tokenizers",
            "fixtures": entries,
        }
        lens = [e["n"] for e in entries.values()]
        print(f"{arm:6} {cfg['label']:24} fixtures={len(entries)} "
              f"token_len min={min(lens)} median={sorted(lens)[len(lens)//2]} max={max(lens)}")

    dest = os.path.join("tools", "ml-pilot", "results", "reference_tokens.json")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with open(dest, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
    print(f"wrote {dest}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
