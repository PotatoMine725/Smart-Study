# Edge AI — Neural Encoder Adoption for the Smart Parser (M8-A → M10)

**Planning date:** 2026-08-24 · **Status:** `draft` — **AWAITING OWNER APPROVAL**; not scope-frozen ·
**Implementation:** not started · **Branch at planning time:** `docs/epic3-state-sync` @ `980eec6`

> **Reads with:** [`../specs/ML_Heuristic_design.md`](../specs/ML_Heuristic_design.md) (§4, §5.1, §9 —
> two clauses of which this plan proposes to amend), [`../specs/system_roadmap.md`](../specs/system_roadmap.md)
> (§8, §9.1, M9 target), [`../knowledge/machine-learning.md`](../knowledge/machine-learning.md)
> (lifecycle pattern + confidence rules), [`../../Prompt/Difficulty_ML_model_proposal.md`](../../Prompt/Difficulty_ML_model_proposal.md)
> (deferred difficulty capability + trigger conditions), and [`../active/m8-weight-optimizer.md`](../active/m8-weight-optimizer.md).
>
> **This plan does not modify the master plan.** It does not touch Epic 2 (LAN sync) or Epic 4
> surfaces, and it does not reopen any Epic 3 decision (G2/G3/D-G/D-H/D-J). No code is changed by
> this document.
>
> **It does propose amending two clauses of `ML_Heuristic_design.md`** (§9 "DO NOT introduce deep
> learning" and §10 "1–2 ML submodels maximum"). Those amendments are **PD-1** and **PD-2** and are
> owner decisions, not agent decisions — S1 does not start until they are ratified.

**Evidence labelling used throughout:** **[F]** confirmed project fact (verified in code or a merged
doc at `980eec6`) · **[I]** engineering inference · **[R]** planning recommendation.

---

# Goal

Replace the bag-of-n-grams featurizer in the M8-A task-type classifier with a **frozen multilingual
neural sentence encoder running locally through ONNX Runtime**, so that Smart Add understands real
Vietnamese student input — abbreviations, stripped diacritics, run-together tokens, slang — instead
of matching literal n-grams.

Shipping this looks like:

- A student types `tgk giải tích tuần sau má ơi cứu` into Smart Add and the task type resolves
  correctly, where today it depends on whether that exact n-gram appeared in training. **[R]**
- The app still runs, unchanged, with **zero model files on disk** — the existing tested contract. **[F]**
- No network call is added anywhere in `Services/ML/*`. **[F]** (contract preserved)
- The heuristic parser remains the authority whenever confidence is below gate.

Secondary goal, unlocked by the same encoder and **explicitly gated to later slices**: a difficulty
head (S5) and a temporal-span head (S6), which together constitute the "multi-task Smart Add
predictor" that `Difficulty_ML_model_proposal.md` names as a possible future implementation. **[F]**

---

# 1. Context

## 1.1 What the ML layer actually is today

| # | Component | Technique | Status | Primary file |
|---|---|---|---|---|
| M7 | StudyTimePredictor | ML.NET `FastTreeRegression` (20 leaves / 100 trees) | shipped | `Services/ML/MLModelManager.cs` |
| M8-A | TextClassifier (task type) | `FeaturizeText` → `SdcaMaximumEntropy`, 5-class | shipped 2026-06-05 | `Services/ML/TextClassifierModelManager.cs` |
| M8-B | WeightOptimizer | **rule-based** `WeightRuleEngine`; ML training deferred | shipped 2026-06-06 | `Services/ML/WeightOptimizer/` |

**[F]** All three verified in code at `980eec6`. M8-B is a rule engine behind an ML-shaped interface
(`IWeightOptimizerService` + confidence + separate suggestion object), deliberately, pending matured
`WeightChangeLog` ground truth.

## 1.2 The seam this plan modifies

```
QuanLyTaskViewModel.PhanTichNhapNhanh()          [RelayCommand — button "Phân tích nhanh"]
  └─ IParsingOrchestrator.Parse(VanBanNhapNhanh)              QuanLyTaskViewModel.cs:263
       ├─ RuleBasedTimeParsingEngine  → HanChot               [heuristic]
       ├─ TaskExtractionEngine        → Loai, DoKho           [heuristic + type prior]
       └─ IIntentClassifier?          → Loai                  [ML — nullable port]
            └─ IntentClassifierAdapter — gate via IMlConfidencePolicy
                 └─ TextClassifierService → TextClassifierModelManager.Predict
                                              ↑ THIS PLAN CHANGES THE FEATURIZER HERE
```

**[F]** `IIntentClassifier` is a nullable constructor parameter on `ParsingOrchestrator`; passing
`null` yields behaviour byte-equal to the legacy heuristic. `ServiceLocator.cs:104-108` wires the
adapter in production. This nullable port is the reason the change is containable.

## 1.3 Finding A — Smart Add is submit-triggered, not keystroke-triggered

**[F]** `Parse` is invoked from inside `[RelayCommand] PhanTichNhapNhanh()`
(`QuanLyTaskViewModel.cs:258-279`), bound to an explicit button. The TextBox at
`Views/QuanLyTaskPage.xaml:54` uses `UpdateSourceTrigger=PropertyChanged`, but that updates only the
`VanBanNhapNhanh` VM property — it does not invoke `Parse`.

**Consequence:** one encoder forward pass per user submit, not per keystroke. A 50–200 ms inference
is invisible inside a button click on the floor hardware. **[I]**

This was checked *before* committing to the approach precisely because the opposite answer would
have invalidated it: a per-keystroke encoder on an integrated-graphics laptop would have required
debouncing or an embed-on-submit-only redesign. It does not.

## 1.4 Finding B — 205 of the 903 seed rows are real, and they are the hard cases

`Services/ML/TextClassifier/seed_intents.csv` (embedded resource, `SmartStudyPlanner.csproj:28`):

| `Source` | `LabelVersion` | Rows | Nature |
|---|---|---|---|
| `m8a_uniform` | v3 | 597 | synthetic |
| `synthetic_v3` | v3 | 101 | synthetic |
| **`collected_v4`** | **v4** | **205** | **real, collected** |
| | | **903** | |

**[F]** Verified by parsing the CSV. The 205 real rows also exist standalone as
`datasheets/collected_v4.csv`, and were folded into the seed by `datasheets/_merge_seed.py`.

Representative real rows **[F]**:

```
tgk giải tích tuần sau má ơi cứu
t4 tuần tới thi giữa kỳ xstk, ai có đề k
mai thi giua ky ly 1 r, chua hoc chu nao
thigiuaky csdl tuan sau, kho v~
```

Domain abbreviations (`tgk`, `xstk`, `ktvm`, `csdl`), stripped diacritics, run-together tokens,
slang, typos. **This is the distribution where an n-gram featurizer has no recourse and a subword
encoder does** — and it is the single strongest argument for this plan. **[I]**

**Two caveats that constrain the evaluation** **[F]**:

1. `collected_v4` covers only 3 of 5 classes — `ThiGiuaKy` 99, `BaiTapVeNha` 56, `DoAnCuoiKy` 50.
   No `KiemTraThuongXuyen`, no `ThiCuoiKy`. Any eval on it is **partial** and must be reported
   per-class, never as a single headline accuracy.
2. The 205 rows are already **inside** the training seed. The roadmap's **96.2% held-out** figure
   was therefore measured after the merge and is **not** a synthetic→real generalization number.
   S0 constructs the split that produces one.

## 1.5 Finding C — the difficulty trigger has been quietly instrumented

**[F]** `Models/Telemetry/DifficultyLabelLog` captures `InputText`, `TaskType`, `SuggestedDoKho`,
`FinalDoKho`, `WasOverride`, `Source`, `CreatedUtc`. It is written from the add-task path
(`QuanLyTaskViewModel.cs:333`) via `SqliteDifficultyLabelLogRepository`, registered at
`ServiceLocator.cs:61`. `datasheets/collected_v4.csv` additionally carries Difficulty labels (1–5)
for all 205 real rows.

**[F]** `Difficulty_ML_model_proposal.md` names *"sufficient corrected difficulty labels exist"* as a
trigger condition and states "No trigger currently applies."

**[I]** That statement is stale with respect to the instrumentation. Whether the *volume* threshold
is met is an empirical question — S5 opens with counting `DifficultyLabelLogs`, and the proposal
should be re-read against that count rather than against its own prose.

## 1.6 Finding D — the confidence gate is not what the project's own rules require

**[F]** `TextClassifierModelManager.Predict` computes `confidence = output.Score.Max()` — raw SDCA
softmax. `IntentClassifierAdapter.Classify` gates on that single number via
`DefaultMlConfidencePolicy` (`ReviewThreshold = 0.60`).

**[F]** `docs/knowledge/machine-learning.md`, "Things to never do": *"Never trust raw model
confidence as the only gating signal — compare against the deterministic baseline."* M7 complies
(confidence = agreement with the formula baseline). **M8-A does not.**

**[I]** SDCA max-entropy softmax is characteristically overconfident, so the 0.60 gate is likely
looser in practice than it reads. This may have been a knowingly accepted MVP tradeoff; the
documentation and the code nonetheless disagree, and S3 exists to close it.

**This is a live risk for the migration, not just a tidiness issue:** the 0.60 threshold is
calibrated to *SDCA-over-bag-of-n-grams*. Swapping the featurizer shifts the score distribution,
which silently moves `ParseSource.MlAugmented` vs `Heuristic` routing **and** the percentage shown
to the user in `QuickInputHint` (`"AI gợi ý Loại: {loai} ({conf:P0})"`). A featurizer swap that does
not re-derive the threshold is a behaviour change disguised as a refactor.

## 1.7 Deployment constraints (owner-stated, 2026-08-24)

| Constraint | Value | Consequence |
|---|---|---|
| Purpose | Improve current features **and** deploy modern edge AI | Two tracks: featurizer upgrade + new capability |
| "No deep learning" (§9) | **Open to revision** | Needs explicit amendment — PD-1 |
| Install size | 1–2 GB acceptable; >2 GB reopens debate | Disk is **not** the binding constraint |
| Floor hardware | **Windows 10 x64, integrated graphics** (owner-stated) | Latency + RAM are the binding constraints |

**Precision on the floor** **[F]**: the csproj already targets `net10.0-windows10.0.19041.0` with no
`SupportedOSPlatformVersion` override, so the minimum OS the build actually admits is **Windows 10
build 19041** (2004 / 20H1), not "Windows 10" generally. That is *tighter* than the owner's stated
floor, and it subsumes every OS-version prerequisite in this plan — see §2.3.
| Distribution | Solo dev, educational / internal group, non-commercial | License ceases to be a differentiator |

**[F]** The Windows 10 floor **permanently rules out Windows AI APIs / Phi Silica / Aion Instruct**
(Windows 11 Copilot+ PC, plus a Limited Access Feature token, plus — on the GPU path — Developer
Mode and an Insider Experimental build). Microsoft has additionally announced Phi Silica's
replacement by Aion Instruct with **Phi Silica removed from retail devices in November 2026**.
Building on that surface was never viable here and is now also a moving target.

---

# 2. Approach

## 2.1 Chosen: frozen encoder + linear head

**EmbeddingGemma-300M, int8 ONNX, frozen, via `Microsoft.ML.OnnxRuntime`, feeding the existing
`SdcaMaximumEntropy` head.** **[R]**

Rationale, in the order that matters:

1. **The linear head is kept deliberately, even though PD-1 would permit fine-tuning.** A frozen
   encoder + linear head preserves on-device retrain in seconds, keeps the decision layer
   inspectable, and leaves the entire `TrainAndSaveAsync` / atomic-swap / seed-hash lifecycle
   working unchanged. Fine-tuning the encoder would forfeit all three for an unmeasured gain.
2. **Multilingual subword tokenization is the actual mechanism** that handles `thigiuaky`,
   `giua ky`, and `tgk` — not model size. Any of the candidate encoders provides it; that is why S0
   compares two rather than assuming one.
3. **Matryoshka (MRL) truncation** — 768 → 256 dims — keeps the head small and CPU inference fast,
   giving one dial to trade accuracy against latency without re-exporting the encoder.
4. **Size fits comfortably**: ~200–300 MB at int8, under 20% of the stated budget.
5. Offline by construction; no network in `Services/ML/*`.

**Fallback candidate:** `multilingual-e5-small` (118M, MIT, mature ONNX exports). Same integration
shape — a swap, not a rewrite. S0 measures both.

**Explicitly not chosen: PhoBERT-family Vietnamese specialists.** `bkai-foundation-models/vietnamese-bi-encoder`
and `dangvantuan/vietnamese-embedding` likely have better raw Vietnamese quality, but PhoBERT
requires VnCoreNLP word segmentation as preprocessing — **a Java runtime dependency inside a WPF
installer**. `hiieu/halong_embedding` (multilingual-e5-base backbone) avoids the segmentation
requirement and is the one member of that family worth adding as an S0 arm if the owner wants a
Vietnamese-specialist datapoint. **[R]**

## 2.2 Correction to an earlier justification

An earlier version of this recommendation (in session, 2026-08-24) cited **VN-MTEB**
(arXiv:2507.21500) as Vietnamese evidence for EmbeddingGemma, via its finding that models using
Rotary Positional Embedding outperform those using Absolute Positional Embedding.

**That citation is withdrawn.** VN-MTEB v1 was submitted 2025-07-29; EmbeddingGemma shipped
2025-09-04 and is **not in that benchmark**. The paper's RoPE-vs-APE finding is also confounded in
its own abstract with "bigger and more complex models."

The architectural prior is still reasonable and is retained **as a prior**. It is not evidence. S0 is
what produces evidence. This correction is recorded here rather than silently dropped because an
unlabelled inference is exactly the kind of thing that becomes a wrong line in a future report.

## 2.3 Runtime and hardware tiering — one build, not two SKUs

The owner raised shipping two installers (lightweight / discrete-GPU). **Recommend against.** **[R]**

Model tier is a **runtime capability probe**, not a build variant, and the seams already exist:
`IIntentClassifier` is nullable **[F]**, `IModelStorageProvider` is swappable **[F]**, and "app runs
with zero model files" is already a tested contract **[F]**.

| Tier | Condition | Behaviour |
|---|---|---|
| **0** | no model files present | current heuristic — already tested, already shipped |
| **1** | default | encoder int8, **CPU execution provider** |
| **2** | opt-in, DX12 GPU present | same encoder, **DirectML EP** |

Two installers would double the release and QA surface for a solo developer immediately after
WP-1…WP-6 spent six work packages on release hygiene. **[I]**

**DirectML caveat — CPU stays the default.** DirectML requires Windows 10 1903+, which the build's
own 19041 floor already exceeds **[F]** — so OS version is a non-issue and only the DX12 GPU
requirement is a real gate. DirectML runs on any DX12 GPU including Intel Iris Xe, so Tier 2 is
reachable on integrated parts too. But there is a known metacommand bug
between ONNX Runtime / DirectML and Intel drivers affecting inference accuracy at certain dimensions
(microsoft/onnxruntime#18652). **Tier 2 is opt-in and must pass an output-parity check against the
CPU EP before it is trusted.** **[R]**

## 2.4 Tokenization — the dependency that is easy to miss

`Microsoft.ML.OnnxRuntime` ships **no tokenizer**. An ONNX encoder export consumes `input_ids` /
`attention_mask` tensors, but `ITextEmbeddingProvider.Embed(string)` takes a string. Something has to
cross that gap, and it is a real dependency, not an implementation detail. **[F]**

Both candidates use SentencePiece with large vocabularies — EmbeddingGemma ~262k (Gemma 3 tokenizer),
`multilingual-e5-small` ~250k (XLM-R). **[I]** Two routes:

| Route | Shape | Trade |
|---|---|---|
| **A** — `Microsoft.ML.Tokenizers` | `SentencePieceTokenizer.Create(...)` from the model's `.model` file | Pure .NET; **but** the SentencePiece APIs are documented under `ml-dotnet-preview`, and this project pins `Microsoft.ML` **3.0.1** while the tokenizer work landed with ML.NET 4.0 — a package bump may be implied **[I]** |
| **B** — bundle tokenization into the ONNX graph (`onnxruntime-extensions`) | .NET side stays tensor-free: string in, vector out | Cleaner C# consumer; moves the problem into the model asset, built once offline |

**[R]** Route B is generally cleaner for a C# consumer and is the default recommendation, but
**neither route is verified against net10.0 in this project yet** — that verification is S0's
responsibility, not S1's.

**This is an input to S0 arm selection, not only to S1.** Tokenizer availability in .NET can differ
between the two candidate encoders; an arm that wins on accuracy but has no workable .NET
tokenization path has not actually won. S0 must report the tokenization route per arm alongside the
metrics.

## 2.5 M9 temporal parsing — span tagging before generation

When M9 is picked up (S6, out of scope for this plan's committed slices), **try a token-classification
head on the same encoder before reaching for a generative SLM**: BIO-tag the temporal span
(`"t4 tuần tới"`, `"mai"`, `"trước thứ 6 tuần sau"`), then resolve it with deterministic date maths
in `RuleBasedTimeParsingEngine`. **[R]**

- One forward pass, shared with the task-type head; no second model download.
- **Reproducible** — the test suite can assert on it; a sampling decoder cannot be pinned that way.
- Temporal *resolution* stays in code, which is where `system_roadmap.md` §9.1's parser-isolation
  rule puts it. The model locates the span; the arithmetic stays deterministic.
- The `DeadlineHint` column already exists in the seed CSV — partial supervision is already there. **[F]**

A generative SLM (Qwen3-0.6B int4 via `Microsoft.ML.OnnxRuntimeGenAI`, which supports Gemma/Qwen/Phi
from C#) remains the **Tier-2 opt-in** for compositional cases such as `"sau khi thi giữa kỳ"` that
reference other tasks. Not the default path, and not in this plan.

---

# 3. Slices

Each slice is one shippable commit. **S0 is a hard gate: no production code is written until S0's
report is accepted.**

## S0 — Offline pilot *(no production code — GATE)*

**Purpose:** establish whether a neural encoder actually beats n-grams on this project's real data,
before any dependency is added.

**Design** — train on the **698 synthetic rows only**, test on the **205 held-out real
`collected_v4` rows**:

| Arm | Featurizer | Head |
|---|---|---|
| baseline | `FeaturizeText` (current production) | `SdcaMaximumEntropy` |
| A | EmbeddingGemma-300M int8, 256-dim MRL | `SdcaMaximumEntropy` |
| B | `multilingual-e5-small` | `SdcaMaximumEntropy` |
| C *(optional)* | `hiieu/halong_embedding` | `SdcaMaximumEntropy` |

**File map (all new, none shipped in the app):**
- `tools/ml-pilot/` — accuracy harness for outputs 1, 2, 5 (language at implementer's discretion;
  Python is acceptable here since nothing ships)
- `tools/ml-pilot/dotnet/` — **.NET console harness for outputs 3, 4, 6**; required, not optional
  (see the harness-split table below)
- `docs/reports/2026-XX-XX-encoder-pilot.md` — results

**Required outputs:**
1. **Per-class** precision/recall for the 3 covered classes. No single headline accuracy number.
2. **Confidence-vs-accuracy curve per arm** — this is the input to S3's threshold re-derivation and
   is not optional.
3. **Cold-start model load time** and **per-inference latency**, measured on the floor hardware
   profile (CPU EP, integrated graphics laptop) — not on the dev machine only.
4. Peak RSS during inference.
5. Explicit statement of what the 2 uncovered classes mean for confidence in the result.
6. **Tokenization route per arm** (§2.4) — which of Route A / Route B works for that encoder on
   net10.0, verified by actually loading the vocab, not by reading a doc page. An arm with no
   workable .NET tokenization path has not won, whatever its F1.

**The harness is deliberately split in two — outputs 1–2 and outputs 3–4 are not the same
experiment** **[R]**:

| Outputs | Where measured | Why |
|---|---|---|
| 1, 2, 5 — accuracy, confidence curve | whatever is fastest to write (Python is fine; nothing ships) | Only relative accuracy matters; the runtime is irrelevant to it |
| **3, 4, 6 — latency, RSS, tokenization** | **the .NET path: `InferenceSession` + real tokenizer + `SdcaMaximumEntropy`** | Numbers from Python `onnxruntime` + an sklearn head **do not transfer**. These three feed the 500 ms budget in §5 and the R-1 kill criterion — measuring them off-path would clear a gate that was never tested |

Concretely: a throwaway .NET console harness under `tools/ml-pilot/dotnet/` is required for the
latency/RSS/tokenization row. It is not production code and is not shipped, but it must exercise the
same runtime, tokenizer, and head that S1–S2 will use.

**Exit criteria:** report written and owner-accepted. **A null result is a valid and useful
outcome** — if the encoder cannot beat n-grams on real rows it has never seen, it will not help in
production, and this plan stops at S0 having cost one script.

**Kill criterion, stated in advance:** if Arm A and Arm B both fail to improve macro-F1 over
baseline by a margin larger than the run-to-run variance, **do not proceed to S1.**

## S1 — Encoder seam *(no behaviour change)* — requires PD-1 + PD-2 ratified

**File map:**
- Create `Core/ML/Contracts/ITextEmbeddingProvider.cs` — `float[]? Embed(string text)`, returns
  `null` when unavailable
- Create `Services/ML/Embedding/OnnxTextEmbeddingProvider.cs` — owns **one** long-lived
  `InferenceSession`
- Create `Services/ML/Embedding/NullTextEmbeddingProvider.cs`
- **Create `Services/ML/Embedding/ITextTokenizer.cs` + implementation** — required only if S0 selects
  **Route A** (§2.4); under **Route B** tokenization lives inside the ONNX graph and no .NET
  tokenizer type exists. **S0's output #6 decides which of these two file maps applies.**
- Modify `SmartStudyPlanner.csproj` — add `Microsoft.ML.OnnxRuntime`; **under Route A also add
  `Microsoft.ML.Tokenizers`, and check whether it forces a `Microsoft.ML` bump off the pinned
  3.0.1** **[F]** (the SentencePiece tokenizer work landed with ML.NET 4.0). A transitive bump of the
  package M7 and M8-A both depend on is a blast radius that must be reported before it is taken.
- Modify `Services/ServiceLocator.cs` — register provider
- Create `SmartStudyPlanner.Tests/Services/ML/OnnxTextEmbeddingProviderTests.cs`

**Note:** `Microsoft.ML.OnnxTransformer`'s documentation still cites opset 7–10 support, which would
be too old for a Gemma 3 export. **Prefer calling `InferenceSession` directly** and handing ML.NET a
plain `float[]` feature column. Smaller integration, and it gives lifetime control over the session —
which matters, because the existing per-call `CreatePredictionEngine` pattern **[F]** would be a
serious performance bug if replicated for an encoder.

**Exit criteria:** provider returns a vector of documented rank for a known input; returns `null`
with no throw when the model file is absent; session created exactly once (asserted); suite green at
baseline count.

## S2 — Featurizer swap behind the seam

**File map:**
- Modify `Services/ML/TextClassifierModelManager.cs` — pipeline consumes the embedding column;
  `TrainAndSaveAsync` / atomic swap / seed-hash gate preserved
- Modify `Services/ML/Schema/TextClassifierInput.cs` — embedding feature column
- Modify `Services/ML/TextClassifierService.cs` if the DTO shape moves
- Modify `SmartStudyPlanner.Tests/Services/ML/TextClassifierSchemaTests.cs`

**Exit criteria:** classifier trains from the embedded seed and predicts; **zero-model-file contract
re-verified by deleting every file in `%AppData%\SmartStudyPlanner\models\` and confirming Dashboard
+ Analytics + Smart Add still work**; `ModelVersion` increments; a stale seed-hash still forces
retrain.

## S3 — Confidence recalibration + dual-signal gate

Closes Finding D (§1.6). **Must ship in the same release as S2** — S2 without S3 silently changes
user-visible routing.

**File map:**
- Modify `Services/ML/DefaultMlConfidencePolicy.cs` — threshold re-derived from S0's curve;
  **document the derivation in the XML doc comment, including the date and the report it came from**
- Modify `Services/ML/IntentClassifierAdapter.cs` — add heuristic-agreement as a second signal
  (available at zero cost: the heuristic task-type parser already runs)
- Modify `SmartStudyPlanner.Tests/Services/ML/IntentClassifierAdapterTests.cs`

**Exit criteria:** a mutation test proves the gate can go red — a deliberately miscalibrated
confidence must fail a test. A gate whose pass cannot be distinguished from a broken gate is not
evidence.

## S4 — Model tiering and acquisition

**File map:**
- Modify `Services/ML/LocalModelStorageProvider.cs` / `IModelStorageProvider.cs` — locate encoder assets
- Create `Services/ML/Embedding/ExecutionProviderProbe.cs` — Tier 1 / Tier 2 detection
- Modify `Views/` settings surface — tier display + Tier 2 opt-in toggle
- Decide and document **acquisition**: bundled in installer vs. first-run side-load (owner call; see
  Open Questions)

**Exit criteria:** Tier 0 / 1 / 2 all exercised; Tier 2 passes CPU-parity check; tier is visible to
the user; Tier 0 remains fully functional.

## S5 — Difficulty head *(gated — do not start blind)*

**Opens with a measurement, not with code:** count rows in `DifficultyLabelLogs` and re-read
`Difficulty_ML_model_proposal.md` against that count. If the volume is insufficient, **stop and
record the count** — that is a useful result and updates a document that currently says "no trigger
applies."

If sufficient: second linear head on the same embedding, introduced behind `IDifficultyPredictor`
exactly as that proposal specifies, with confidence-gated fallback to `DefaultDifficultyKeywordParser`.

## S6 — M9 temporal span head *(design only in this plan)*

Per §2.5. Requires its own plan. Listed so the shared-encoder design intent is recorded now rather
than rediscovered later.

---

# 4. Pre-edit checklist

Per `CLAUDE.md`, **`gitnexus_impact` is mandatory before editing any symbol.** Run upstream impact
on each of these and report blast radius before the corresponding slice:

| Symbol | Slice | Expected risk **[I]** |
|---|---|---|
| `TextClassifierModelManager.Predict` | S2 | **MEDIUM** — single consumer via `TextClassifierService` |
| `TextClassifierModelManager.TrainAndSaveAsync` | S2 | **MEDIUM** — lifecycle; atomic swap must survive |
| `IntentClassifierAdapter.Classify` | S3 | **HIGH** — user-visible routing + `QuickInputHint` string |
| `DefaultMlConfidencePolicy.Decide` | S3 | **HIGH** — shared with M8-B WeightOptimizer tiers |
| `ParsingOrchestrator.Parse` | S1–S3 | **HIGH** — Smart Add entry point |
| `ServiceLocator` ML registrations | S1, S4 | **MEDIUM** — composition root |

**`DefaultMlConfidencePolicy` is shared with M8-B** **[F]** (`AutoApplyThreshold = 0.75` drives the
WeightOptimizer review/apply tiers). Re-deriving the parser threshold **must not** move the
WeightOptimizer's. If S3's derivation implies a different number, split the policy rather than
retuning both. **[R]**

Per `CLAUDE.md`: **warn the owner before proceeding on any HIGH or CRITICAL result**, and never
rename via find-and-replace — use `gitnexus_rename`.

---

# 5. Acceptance gates

Applied to every slice S1 onward:

1. `rtk dotnet build` — clean.
2. `rtk dotnet test` — **record the baseline count on the branch at slice start and require
   no regression.** The last documented figure is **470 pass** (Epic 3 close, 2026-08-07) **[F]**;
   re-measure rather than assuming it, since this plan branches from a docs branch.
3. `gitnexus_detect_changes()` before every commit — affected symbols must match the slice's file map.
4. **Zero-model-file contract** — delete `%AppData%\SmartStudyPlanner\models\*`, launch, confirm
   Dashboard + Analytics + Smart Add all function. This is the contract most at risk from this plan
   and must be re-run at S2 and S4, not just once.
5. **Latency budget** — Smart Add submit-to-populate must stay under **500 ms** on the floor
   hardware profile, cold model already loaded. Measured, not assumed.
6. **Tag slow ML tests** `[Trait("Category", "ML")]` per existing convention **[F]** so
   `--filter "Category!=ML"` stays fast.
7. CI green + PR (branch protection: `dev`/`main` are PR-only since 2026-08-09 **[F]**).

---

# 6. Out of scope

Explicit deferrals — none of these are started by this plan:

- **Generative SLM inference of any kind** (Qwen3, Gemma, Phi, `OnnxRuntimeGenAI`). §2.5 records why
  span-tagging is preferred first.
- **Windows AI APIs / Phi Silica / Aion Instruct / Foundry Local.** Ruled out permanently by the
  Windows 10 floor (§1.7).
- **Fine-tuning the encoder.** PD-1 would permit it; §2.1 declines it deliberately.
- **M8-B ML weight optimizer** (still awaiting matured `WeightChangeLog`).
- **M8-C** StudyTime retrain on Focus telemetry.
- **Cloud model storage.** `IModelStorageProvider` keeps the swap possible; nothing uses it.
- **Epic 2 (LAN sync) and Epic 4 surfaces.** Untouched.
- **Any Epic 3 decision** — G2, G3, D-G, D-H, D-J are not reopened.
- **Model asset distribution mechanics** beyond the S4 decision point (no CDN, no auto-update
  channel).

---

# 7. Risks

| # | Risk | Mitigation |
|---|---|---|
| R-1 | Encoder shows no gain over n-grams | S0 is a gate with a stated kill criterion; cost is one script |
| R-2 | S0 result is over-read from 3 of 5 classes | Per-class reporting mandated; caveat stated in the report, not just here |
| R-3 | Threshold shift silently changes user-visible routing | S3 mandatory in the same release as S2; mutation test required |
| R-4 | Shared `IMlConfidencePolicy` retunes M8-B by accident | Called out in §4; split the policy if derivations diverge |
| R-5 | Install size creeps past 2 GB | int8 only; owner re-consult required before any fp16/larger variant |
| R-6 | DirectML accuracy bug on Intel iGPU | Tier 2 opt-in + CPU-parity check (§2.3) |
| R-7 | Session lifetime mistake replicates the per-call `CreatePredictionEngine` pattern | S1 asserts single session construction |
| R-8 | Zero-model-file contract breaks | Re-verified at S2 **and** S4 |
| R-9 | `.gitignore` / repo bloat from model binaries | Model assets must never enter git; decide S4 acquisition path first |
| R-10 | No workable .NET tokenizer for the winning encoder | S0 output #6 verifies the route per arm **before** S1; an arm without one is disqualified |
| R-11 | `Microsoft.ML.Tokenizers` forces a `Microsoft.ML` 3.0.1 → 4.x bump, touching M7 + M8-A | Report the bump as blast radius at S1; Route B (§2.4) avoids the dependency entirely |
| R-12 | S0 latency measured off the .NET path clears a gate it did not test | Harness split mandated in S0; outputs 3/4/6 must come from the .NET console harness |

---

# 8. Owner checkpoints

1. **Before S1** — ratify PD-1 and PD-2 (spec amendments). Blocking.
2. **After S0** — accept or reject the pilot report. Blocking; kill criterion applies.
3. **At S4** — decide model acquisition: bundled installer vs. first-run side-load.
4. **Before S5** — review the `DifficultyLabelLogs` count against the deferred proposal's trigger.

---

# 9. Parallel-dispatch decision

**Do not parallelise S0 → S4.** They are strictly sequential: S0 gates the whole plan, S2 depends on
S1's seam, S3 depends on S0's confidence curve, S4 depends on S2. **[R]**

The one parallelisable unit is **inside S0**: the pilot arms are independent and may be dispatched
concurrently, provided every arm reports against the identical split and the identical metric set.

Card S0-C (.NET runtime characterisation) may run concurrently with S0-B — it needs the candidate
model exports, not S0-B's accuracy results. Both depend on S0-A's split existing first.

## 9.1 Per-agent task cards

> **Common to every card** — **Venue:** `D:\Code\C#\SmartStudyPlanner`, branch off `dev`.
> **Tools:** GitNexus MCP first (`gitnexus_impact` before any symbol edit, `gitnexus_detect_changes`
> before any commit), then Read/Edit/Grep. RTK prefix on all shell commands.
> **Never:** modify the master plan; touch Epic 2 / Epic 4 surfaces; reopen G2/G3/D-G/D-H/D-J;
> commit model binaries; write results into this plan file (results go to `docs/reports/`).

### Card S0-A — pilot harness + baseline arm
- **Mission:** build the train-on-synthetic / test-on-real harness; produce the baseline
  (`FeaturizeText` + SDCA) numbers.
- **Scope:** `tools/ml-pilot/` only. No file under `SmartStudyPlanner/` is touched.
- **Stop when:** baseline per-class P/R + confidence curve produced and committed.

### Card S0-B — encoder arms (accuracy)
- **Mission:** run Arms A and B (and C if the owner opts in) through the S0-A harness for outputs 1, 2, 5.
- **Scope:** `tools/ml-pilot/` only. Must consume S0-A's split verbatim — no re-splitting.
- **Stop when:** all arms reported on identical accuracy metrics and confidence curves.

### Card S0-C — .NET runtime characterisation
- **Mission:** outputs **3, 4, 6** — cold-start load, per-inference latency, peak RSS, and the working
  tokenization route — measured through `InferenceSession` + real tokenizer + `SdcaMaximumEntropy`.
- **Scope:** `tools/ml-pilot/dotnet/` only. Throwaway console app; touches nothing under
  `SmartStudyPlanner/`.
- **Why separate from S0-B:** Python-measured latency does not transfer to the .NET path (R-12).
- **Stop when:** every arm that survived S0-B has a verified tokenization route and a latency/RSS
  figure from the floor hardware profile.

### Card S1 — encoder seam
- **Mission:** `ITextEmbeddingProvider` + ONNX implementation + null implementation. Zero behaviour change.
- **Scope:** file map in §S1 only. Do **not** touch `TextClassifierModelManager` in this card.
- **Stop when:** exit criteria in §S1 met, suite at baseline count.

### Card S2+S3 — featurizer swap + recalibration *(one card, deliberately)*
- **Mission:** swap the featurizer and re-derive the gate together.
- **Scope:** file maps in §S2 and §S3.
- **Why one card:** shipping S2 without S3 is a silent behaviour change (§1.6). Splitting them across
  agents invites exactly that.
- **Stop when:** both exit criteria met, including the mutation test proving the gate can fail.

---

# 10. Planning Decisions *(ADR-style — only those that materially change implementation)*

### PD-1 — Amend `ML_Heuristic_design.md` §9: permit frozen neural encoders

**Decision.** Amend the "DO NOT introduce deep learning" clause to read, in substance: *no
trained-on-device deep learning; frozen pre-trained encoders are permitted as featurizers behind an
existing confidence gate, provided the decision layer remains a linear, locally-retrainable model.*

**Why it had to be made.** The clause is currently a flat prohibition. This plan's core proposal is a
neural encoder. Proceeding without amending the spec would leave code contradicting a normative
document — and in a project whose docs are explicitly code-normative, that is a defect, not a
technicality. The alternative (rationalising a frozen encoder as "just a featurizer" and leaving §9
untouched) was rejected: it wins the argument on a wording technicality while leaving the next reader
misled.

**What it's for.** It preserves the *intent* of §9 — no black-box autonomous scheduling, no
self-modifying planner, no unbounded model sprawl — while removing a blanket ban that predates the
current small-model landscape and now blocks the project's most valuable available improvement.

**Experience for future development.** When a directive written at time T blocks a change at time
T+n, amend the directive explicitly and date the amendment. A spec that is quietly worked around
stops being normative for everything else it says, not just the clause that was dodged.

### PD-2 — Amend §10: count *models*, not *heads*

**Decision.** The "1–2 ML submodels maximum" cap counts **deployed model artifacts**, not prediction
heads. One shared encoder with task-type, difficulty, and temporal heads counts as **one**.

**Why it had to be made.** §10's cap is real and worth keeping, but the multi-head design makes the
count ambiguous — three heads on one encoder could be argued as one model or three. Left unresolved,
that ambiguity becomes an argument during code review of S5, at the least convenient moment.

**What it's for.** It keeps the cap's actual purpose — bounding deployment, download, and maintenance
surface — while allowing the design that minimises exactly those things. Three heads on one encoder
is *cheaper* on every axis §10 cares about than three separate models would be.

**Experience for future development.** When a numeric cap exists, define its unit before the
architecture makes the unit ambiguous, not after.

### PD-3 — S0 is a gate with a kill criterion, not a formality

**Decision.** No production code before S0's report is accepted. A null result stops the plan.

**Why it had to be made.** The roadmap's **96.2%** figure is not a synthetic→real generalization
number — the real rows were merged into training before it was measured (§1.4). Without S0 the
project would be adding a dependency, an install-size cost, and a runtime on the strength of a figure
that cannot support the claim being made of it.

**What it's for.** It converts an architectural prior (§2.2, now explicitly downgraded from evidence)
into a measured result, using data that already exists, at the cost of one script and no shipped code.

**Experience for future development.** A held-out split is only a generalization test if the held-out
rows were held out *before* training. Check the merge history of a dataset before quoting its
accuracy — `_merge_seed.py` is in the repo precisely because someone will need to check this again.

### PD-4 — S2 and S3 ship together

**Decision.** The featurizer swap and the confidence re-derivation are one release unit, and one
agent task card.

**Why it had to be made.** The 0.60 threshold is calibrated to the old featurizer's score
distribution (§1.6). Shipping S2 alone would move the ML-vs-heuristic routing and the confidence
percentage shown to the user, while presenting as a pure refactor.

**What it's for.** It prevents a user-visible behaviour change from entering under a commit message
that does not mention it.

**Experience for future development.** Any threshold is a coupling between a gate and a score
distribution. Changing what produces the score without re-deriving the threshold is a behaviour
change, however much it looks like a refactor.

### PD-5 — One build with a runtime tier probe; not two installers

**Decision.** Reject the two-SKU proposal. Tier 0/1/2 resolved at runtime in a single build.

**Why it had to be made.** The owner raised two installers as a way to serve both integrated-graphics
and discrete-GPU users. It is a plausible option that would have doubled the release and QA surface
for a solo developer.

**What it's for.** The seams for runtime tiering already exist and are already tested — nullable
`IIntentClassifier`, swappable `IModelStorageProvider`, the zero-model-file contract. Using them
costs a capability probe; the alternative costs a permanent second release pipeline.

**Experience for future development.** Before adding a build variant, check whether an existing
runtime seam already expresses the same variation. In this codebase it usually does — the nullable-port
pattern was designed for exactly this.

### PD-6 — Withdraw the VN-MTEB justification; keep the recommendation

**Decision.** The RoPE-vs-APE argument is demoted from evidence to architectural prior, in writing
(§2.2). The model recommendation is unchanged.

**Why it had to be made.** The citation was load-bearing in the original recommendation and does not
support the weight placed on it: EmbeddingGemma postdates the benchmark and is not in it.

**What it's for.** It keeps the plan's evidence labelling honest, so that a future reader can tell
which parts of §2.1 are measured and which are reasoned.

**Experience for future development.** Check a benchmark's submission date against the release date
of every model you claim it ranks. "Model X is newer and better-architected" is a hypothesis; only
the pilot makes it a finding.

### PD-7 — Tokenization route is measured in S0, not chosen now

**Decision.** §2.4 names two routes (`Microsoft.ML.Tokenizers` vs. tokenization baked into the ONNX
graph) and picks neither. S0 output #6 decides, per arm, by loading the vocab on net10.0.
S1's file map is written conditionally on that outcome.

**Why it had to be made.** `Microsoft.ML.OnnxRuntime` ships no tokenizer, so a string-in API needs
one — and this was missing from the first draft of the S1 file map entirely. Choosing a route from
documentation would have been guessing: the SentencePiece APIs are published under
`ml-dotnet-preview`, and this project pins `Microsoft.ML` 3.0.1 while that work landed in ML.NET 4.0.

**What it's for.** It keeps a package bump that would touch both M7 and M8-A from arriving as a
surprise inside an "add a NuGet reference" line, and it makes tokenizer availability a *selection
criterion* for the encoder rather than a problem discovered after one was chosen.

**Experience for future development.** When adopting a model runtime, check what it does **not**
ship. Inference engines routinely omit tokenization, and the gap between "string" and
"`input_ids` tensor" is a dependency with its own version graph — not glue code.

---

# 11. Open questions for the owner

1. **Model acquisition** — bundle the encoder in the installer (simple, +~250 MB download for every
   user including Tier 0 users who will not use it), or side-load on first run (smaller base
   installer, but adds a first-run acquisition path to a codebase whose ML layer currently has
   **zero** network calls — and that contract is load-bearing)? **This is the one decision in the
   plan that could compromise the offline-first contract, and it is deliberately left to the owner.**
2. **Arm C** — include `hiieu/halong_embedding` as a Vietnamese-specialist datapoint in S0? Costs one
   more pilot run, no production commitment.
3. **Latency budget** — is 500 ms the right ceiling for Smart Add submit-to-populate, or tighter?
4. **Floor hardware** — which specific machine should S0's latency numbers be measured on? "Integrated
   graphics" spans roughly a decade of Intel parts, and the answer materially changes R-1's outcome.

---

## Lifecycle

`draft` → awaiting owner approval of PD-1/PD-2 and the S0 gate. On approval, add a pointer row to
[`../active/README.md`](../active/README.md) per the plans README. Record each shipped slice in
[`../CHANGELOG.md`](../CHANGELOG.md). S0's results belong in `docs/reports/`, **not** in this file.
