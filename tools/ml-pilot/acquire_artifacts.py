#!/usr/bin/env python3
"""Acquire the Arm A and Arm B encoder + tokenizer artifacts at pinned revisions.

WP-0.3. Downloads into `tools/ml-pilot/models/`, which is git-ignored, and writes
`tools/ml-pilot/ARTIFACTS.md` with per-file SHA-256 and byte size.

    python tools/ml-pilot/acquire_artifacts.py

SCOPE GUARD -- Arm C (`hiieu/halong_embedding`) is NOT acquired. EVA-06 makes
running it "while we're here" a scope violation, and acquisition is the first
step of running it. It is unlocked only by an explicit owner decision after A and
B report.

Two arms measured against different silently-updated exports are not comparable,
so every file is fetched at a pinned commit SHA resolved once and recorded.
"""

import hashlib
import json
import os
import sys
import time
import urllib.request

MODELS = os.path.join("tools", "ml-pilot", "models")

# Per arm: the ONNX encoder exports and the tokenizer assets that ship with them.
#
# Both precisions are acquired for both arms, deliberately:
#   * fp32 removes quantization as a confound from the ACCURACY comparison --
#     otherwise a gap between arms could be the quantizer rather than the encoder.
#   * the quantized export is what would actually ship under a size cap, so it is
#     what EVA-08 outputs 4, 5 and 8 must describe.
# Reporting one precision and inferring the other would be inventing a number.
ARMS = {
    "arm_a": {
        "label": "Arm A - EmbeddingGemma-300M",
        "repo": "onnx-community/embeddinggemma-300m-ONNX",
        "upstream": "google/embeddinggemma-300m",
        "licence": "Gemma Terms of Use (Google). The upstream google/ repo is "
                   "gated:manual; this ONNX mirror is not gated, but the Gemma "
                   "Terms still govern the weights. OWNER QUESTION -- see ARTIFACTS.md.",
        "files": [
            "onnx/model.onnx", "onnx/model.onnx_data",
            "onnx/model_quantized.onnx", "onnx/model_quantized.onnx_data",
            "tokenizer.json", "tokenizer_config.json",
            "special_tokens_map.json", "added_tokens.json", "config.json",
        ],
    },
    "arm_b": {
        "label": "Arm B - multilingual-e5-small",
        "repo": "intfloat/multilingual-e5-small",
        "upstream": "intfloat/multilingual-e5-small",
        "licence": "MIT. Not gated.",
        "files": [
            "onnx/model.onnx",
            "onnx/model_qint8_avx512_vnni.onnx",
            "onnx/tokenizer.json", "onnx/tokenizer_config.json",
            "onnx/special_tokens_map.json", "onnx/sentencepiece.bpe.model",
            "onnx/config.json",
            "1_Pooling/config.json", "sentence_bert_config.json", "config.json",
        ],
    },
}


def api(url):
    req = urllib.request.Request(url, headers={"User-Agent": "smartstudy-s0-pilot"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def resolve_revision(repo):
    return api(f"https://huggingface.co/api/models/{repo}")["sha"]


def fetch(repo, rev, path, dest):
    if os.path.exists(dest):
        return "cached"
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    url = f"https://huggingface.co/{repo}/resolve/{rev}/{path}"
    req = urllib.request.Request(url, headers={"User-Agent": "smartstudy-s0-pilot"})
    tmp = dest + ".part"
    with urllib.request.urlopen(req, timeout=600) as r, open(tmp, "wb") as f:
        while True:
            chunk = r.read(1 << 20)
            if not chunk:
                break
            f.write(chunk)
    os.replace(tmp, dest)
    return "downloaded"


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    if not os.path.isdir("tools"):
        print("error: run from the repository root", file=sys.stderr)
        return 1

    manifest = {}
    for arm, cfg in ARMS.items():
        rev = resolve_revision(cfg["repo"])
        print(f"[{arm}] {cfg['repo']} @ {rev}", flush=True)
        entries = []
        for path in cfg["files"]:
            dest = os.path.join(MODELS, arm, path.replace("/", os.sep))
            t0 = time.time()
            try:
                status = fetch(cfg["repo"], rev, path, dest)
            except Exception as e:                       # noqa: BLE001
                print(f"  FAILED {path}: {e}", flush=True)
                entries.append({"path": path, "status": f"FAILED: {e}"})
                continue
            size = os.path.getsize(dest)
            digest = sha256(dest)
            print(f"  {status:10} {path:45} {size/1048576:9.2f} MB  "
                  f"{digest[:16]}  ({time.time()-t0:.1f}s)", flush=True)
            entries.append({"path": path, "bytes": size, "sha256": digest,
                            "status": status})
        manifest[arm] = {"repo": cfg["repo"], "upstream": cfg["upstream"],
                         "revision": rev, "licence": cfg["licence"],
                         "label": cfg["label"], "files": entries}

    out = os.path.join("tools", "ml-pilot", "results", "artifacts.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    print(f"\nwrote {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
