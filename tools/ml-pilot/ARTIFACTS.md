# S0 pilot — encoder artifact record

**WP-0.3** · **Date:** 2026-08-25

Every file below lives under `tools/ml-pilot/models/`, which is **git-ignored**. **No encoder binary
is tracked at any commit of this initiative** (AST-05), enforced by the CI step *Assert no model
binary is tracked* and proven red in CI run
[32792616833](https://github.com/PotatoMine725/Smart-Study/actions/runs/32792616833).

Each artifact is pinned to an **exact commit SHA**, not a branch. Two arms measured against
differently-updated exports are not comparable, and a floating `main` would make this pilot
irreproducible the next time either repository is touched.

---

## ⚠️ Arm C is NOT acquired

`hiieu/halong_embedding` was **not downloaded**. EVA-06 makes running it "while we're here" a scope
violation, and **acquisition is the first step of running it**. It is unlocked only by an explicit
owner decision after Arms A and B report (OP-11, PD-9 tie branch).

---

## ⚠️ Owner question — the Gemma Terms of Use (Arm A)

The upstream `google/embeddinggemma-300m` repository is **`gated: manual`** on HuggingFace: it
requires accepting Google's terms with an account before download.

**Arm A was obtained from `onnx-community/embeddinggemma-300m-ONNX`, which is NOT gated.** The mirror
routes around the *gate*; it does **not** route around the *licence*. **The Gemma Terms of Use still
govern these weights.**

**This was never a licence clearance, and it is recorded as an open owner question rather than
resolved by the mirror's convenience.** It is moot if the initiative stops at S0 — the artifacts are
untracked local files used for a throwaway measurement — but it would have been a **blocking**
question before any bundled distribution under AST-02.

Arm B (`intfloat/multilingual-e5-small`) is **MIT**-licensed and not gated. No equivalent question.

---

## Arm A - EmbeddingGemma-300M

| Field | Value |
|---|---|
| Download source | `onnx-community/embeddinggemma-300m-ONNX` |
| Upstream model | `google/embeddinggemma-300m` |
| **Pinned revision** | `5090578d9565bb06545b4552f76e6bc2c93e4a66` |
| Licence | Gemma Terms of Use (Google). The upstream google/ repo is gated:manual; this ONNX mirror is not gated, but the Gemma Terms still govern the weights. OWNER QUESTION -- see ARTIFACTS.md. |
| Total downloaded | 1497.9 MB |

| File | Bytes | MB | SHA-256 |
|---|---:|---:|---|
| `onnx/model.onnx` | 479,932 | 0.46 | `ea91fd315a7c152d427d231746f0f811…` |
| `onnx/model.onnx_data` | 1,234,521,088 | 1177.33 | `ef835ae565d8695236652475903078e8…` |
| `onnx/model_quantized.onnx` | 567,874 | 0.54 | `172efde319fe1542dc41f31be6154910…` |
| `onnx/model_quantized.onnx_data` | 308,890,624 | 294.58 | `705626e28e4c23c82ade34566b4197d9…` |
| `tokenizer.json` | 20,323,312 | 19.38 | `4dda02faaf32bc91031dc8c88457ac27…` |
| `tokenizer_config.json` | 1,156,830 | 1.10 | `3ca953eea6c3c9fcda9cf3df22949ff1…` |
| `special_tokens_map.json` | 662 | 0.00 | `2f7b0adf4fb469770bb1490e3e35df87…` |
| `added_tokens.json` | 35 | 0.00 | `50b2f405ba56a26d4913fd7720899922…` |
| `config.json` | 1,765 | 0.00 | `6e1f06404b7163e0325ed2ea3e6781cd…` |
| `tokenizer.model` | 4,689,074 | 4.47 | `1299c11d7cf632ef3b4e11937501358a…` |

## Arm B - multilingual-e5-small

| Field | Value |
|---|---|
| Download source | `intfloat/multilingual-e5-small` |
| Upstream model | `intfloat/multilingual-e5-small` |
| **Pinned revision** | `614241f622f53c4eeff9890bdc4f31cfecc418b3` |
| Licence | MIT. Not gated. |
| Total downloaded | 582.5 MB |

| File | Bytes | MB | SHA-256 |
|---|---:|---:|---|
| `onnx/model.onnx` | 470,268,510 | 448.48 | `ca456c06b3a9505ddfd9131408916dd7…` |
| `onnx/model_qint8_avx512_vnni.onnx` | 118,346,824 | 112.86 | `dd476dd0c2514e9b9be83aeb3853fac0…` |
| `onnx/tokenizer.json` | 17,082,730 | 16.29 | `0b44a9d7b51c3c62626640cda0e2c2f7…` |
| `onnx/tokenizer_config.json` | 443 | 0.00 | `a1d6bc8734a6f635dc158508bef000f8…` |
| `onnx/special_tokens_map.json` | 167 | 0.00 | `d05497f1da52c5e09554c0cd874037a0…` |
| `onnx/sentencepiece.bpe.model` | 5,069,051 | 4.83 | `cfc8146abe2a0488e9e2a0c56de7952f…` |
| `onnx/config.json` | 653 | 0.00 | `bbb7c1333fc4b3e27fbc9cd5d2070aab…` |
| `1_Pooling/config.json` | 200 | 0.00 | `987f7a67a38fa564c849bb5d277c52ab…` |
| `sentence_bert_config.json` | 57 | 0.00 | `948201d8329907aae938fa62f9ceeed5…` |
| `config.json` | 655 | 0.00 | `69137736cab8b8903a07fe8afaafdda2…` |

---

## Quantization — recorded explicitly, because it is a `[choice]` that moves the numbers

Spec §3.2 leaves quantization open. **Both precisions were acquired and measured for both arms**, so
the comparison never silently mixes them:

| Arm | fp32 export | Quantized export | Quantization |
|---|---|---|---|
| **A** | `onnx/model.onnx` + `.onnx_data` | `onnx/model_quantized.onnx` + `.onnx_data` | int8, dynamic |
| **B** | `onnx/model.onnx` | `onnx/model_qint8_avx512_vnni.onnx` | int8, **exported targeting AVX512-VNNI** |

**Arm B's quantized export names an instruction-set target the PRF-01 reference class may not have.**
A 10th-generation Intel U-series part may be Comet Lake (no AVX512) rather than Ice Lake (AVX512 with
VNNI). ONNX Runtime executes QLinear operators without VNNI, but more slowly — so Arm B's int8
latency measured on this 12th-gen machine is **doubly non-transferable**: faster CPU *and* possibly a
wider instruction set than the target. Recorded here because the file name alone would not have
raised it.

**Finding (see the S0 report §7):** Arm A's int8 export is **~6× slower** than its fp32 export on this
CPU, at roughly twice the peak memory. Quantization was not a free size win.

## Packaged sizes as they would ship

Encoder plus the tokenizer asset the route actually loads.

| Arm | Precision | Encoder | Tokenizer | **Total** |
|---|---|---|---|---|
| A | fp32 | 1 177.8 MB | 4.5 MB | **1 182.3 MB** |
| A | int8 | 295.1 MB | 4.5 MB | **299.6 MB** |
| B | fp32 | 448.5 MB | 4.8 MB | **453.3 MB** |
| B | int8 | 112.9 MB | 4.8 MB | **117.7 MB** |

`tokenizer.json` (16–19 MB per arm) is **not** counted: it is the oracle's input, not the shipped
route's. Route A loads the SentencePiece `.model` file.

**OP-1, the size cap, remains unset** and is not invented here. The *"1–2 GB acceptable"* remark from
requirements gathering is an install-size preference, **not a cap**, and is not treated as one.

## Reproducing

```
python tools/ml-pilot/acquire_artifacts.py
```

Idempotent: existing files are left alone. Every SHA-256 above must reproduce; if one does not, the
pinned revision was not honoured and no measurement in the report is comparable.
