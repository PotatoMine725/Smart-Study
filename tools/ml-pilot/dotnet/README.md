# S0 .NET runtime harness

**WP-0.7.** Throwaway. **Deliberately NOT added to `SmartStudyPlanner.slnx`**, so it never enters the
product build or CI (EVA-01).

EVA-09 requires outputs 3, 4, 5 and 6 to be produced **on the stack that ships** — the real inference
runtime, the real tokenizer, the real head. Numbers from a Python `onnxruntime` + scikit-learn stack
do not transfer and must not be used to satisfy them. This project exists solely because measuring
off-path would clear a gate that was never tested.

```
dotnet run -c Release --project S0Pilot -- <command>

  tokcheck [--corrupt-vocab] [--no-offset]   EVA-08 output 6 / TOK-02, on the DAT-05 set
  stress   [--trimmed]                       whitespace-axis characterisation, 181 cases
```

## TOK-07 — answered here, empirically

`Microsoft.ML` is **pinned to 3.0.1** in `S0Pilot.csproj`, the version the product pins. If adding
`Microsoft.ML.Tokenizers` forced it off that pin, restore would have said so **here**, before any
dependency change was proposed for the product.

It does not. Resolved graph: `Microsoft.ML/3.0.1`, `Microsoft.ML.CpuMath/3.0.1`,
`Microsoft.ML.DataView/3.0.1`, `Microsoft.ML.Tokenizers/2.0.0`, `Microsoft.ML.OnnxRuntime/1.29.0`.
`Microsoft.ML.Tokenizers` 2.0.0 declares **no dependency on `Microsoft.ML` at all**.

`TargetFramework` is `net10.0-windows10.0.19041.0`, matching the product exactly — TOK-03 binds the
tokenization route to that runtime, and verifying it on plain `net10.0` would clear a constraint it
never tested.

## What the three comparisons mean

They are not interchangeable, and conflating them would manufacture a verification.

| Comparison | Command | What it answers |
|---|---|---|
| **Contract** | `tokcheck` | Does .NET reproduce the reference tokenizer on the **DAT-05 set**, both sides untrimmed? This is the literal TOK-02 question |
| **Characterisation** | `stress` | Where do divergences live? 181 whitespace/punctuation/emoji cases |
| **Route variant** | `stress --trimmed` | Does trimming the input **on both sides** close the gap? |

## Red demonstrations

A passing check is not evidence until it has been shown able to fail.

| Perturbation | Effect | What it proves |
|---|---|---|
| `--corrupt-vocab` (byte-flip inside the piece table) | **0/39 both arms** | The comparison reads the real vocabulary, not a cached or synthesised one |
| `--no-offset` (drop the fairseq +1) | **Arm B 0/39**, Arm A unaffected | The offset is load-bearing for Arm B, and correctly absent for Arm A |

The second is the one that matters. Without the offset, Arm B's ids are `[0,40,1293,11,6116,…]`
against a reference of `[0,41,1294,12,6117,…]` — a sequence that looks entirely plausible and is
wrong in every position. That is the silent divergence TOK-02 exists to catch, reproduced on demand.
