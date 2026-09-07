# Edge AI — Neural Encoder Adoption for the Smart Parser (M8-A → M10)

**Planning date:** 2026-08-24 · **Revised:** 2026-08-24 (owner review rounds 1–3) ·
**Lifecycle:** **`stopped_at_s0`** (owner ruling, 2026-08-25)

> ## Closure banner — read before the rest of this document
>
> **This initiative STOPPED at S0 on 2026-08-25.** S0 ran, the **EVA-16 kill criterion fired**
> — neither candidate encoder improved macro-F1 over the shipped n-gram baseline; both scored
> **below** it — and the owner accepted that result. **S1–S4 were cancelled, not entered. No
> production code was written and none will be.**
>
> - **Evidence + CP1 ruling:** [`../reports/2026-08-25-encoder-pilot.md`](../reports/2026-08-25-encoder-pilot.md)
> - **Execution plan** (`closed`; only Phase S0 ever ran): [`2026-08-24-edge-ai-neural-encoder-execution-plan.md`](2026-08-24-edge-ai-neural-encoder-execution-plan.md)
> - **Durable lessons:** [`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md)
>
> **What this banner does and does not do.** It records the outcome. It does **not** amend a
> ratified decision: **PD-1 … PD-12 stand as ratified**, and `ML_Heuristic_design.md` §9.1 — the
> narrow exception permitting frozen pretrained encoders as feature extractors — **remains in
> force**. The exception was ratified on its own merits and was simply never exercised. A future
> proposal re-enters through that gate; it does not need §9.1 reopened.
>
> **Everything below is retained as the reasoning it was, on 2026-08-24.** Where the text says a
> phase is active, frozen-and-activated, dispatchable, or next, read it as *what was true when
> written* — the freeze boundary is historical, and the passages that state it carry a dated
> superseded marker. Nothing past S0 is authorised by this document today.

**Status as written 2026-08-24 — superseded 2026-08-25, see the banner above:**
*S-SPEC – S3 SCOPE-FROZEN and ACTIVE (owner, 2026-08-24) · S4 open by design (PD-11) ·
S5 – S6 not activated · Branch at planning time: `docs/epic3-state-sync` @ `9c747be`*

> **Owner review rounds 1, 2 and 3 are complete.** Decisions **PD-1 … PD-10** are ratified and recorded
> in [`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`](2026-08-24-edge-ai-encoder-owner-decision-handoff.md);
> **PD-11** (round 2) defers the delivery mechanism and the size cap to S4; **PD-12** (round 3)
> ratifies the **500 ms** Smart Add latency ceiling.
> **The owner scope-froze and activated S-SPEC through S3 on 2026-08-24**, approving the S-SPEC
> wording verbatim. **S-SPEC is executed** (`d141db1`). **S4 remains open by design** (PD-11) and
> **S5 – S6 are not activated** and each needs its own approval (PD-2 governance) — see §11.
>
> *Superseded 2026-08-25 as to forward state:* S-SPEC's execution and the PD ratifications stand;
> **S4 was cancelled with S1–S3, so it is no longer "open by design" — it is not open at all.**
> S5–S6 are unchanged: still not activated.

> **Reads with:** [`../specs/ML_Heuristic_design.md`](../specs/ML_Heuristic_design.md) (§4, §5.1, §9 —
> two clauses of which this plan proposes to amend), [`../specs/system_roadmap.md`](../specs/system_roadmap.md)
> (§8, §9.1, M9 target), [`../knowledge/machine-learning.md`](../knowledge/machine-learning.md)
> (lifecycle pattern + confidence rules), [`../../Prompt/Difficulty_ML_model_proposal.md`](../../Prompt/Difficulty_ML_model_proposal.md)
> (deferred difficulty capability + trigger conditions), [`../active/m8-weight-optimizer.md`](../active/m8-weight-optimizer.md),
> and [`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`](2026-08-24-edge-ai-encoder-owner-decision-handoff.md)
> (the owner's ratification of PD-1 … PD-10, which this revision reconciles against).
>
> **This plan does not modify the master plan.** It does not touch Epic 2 (LAN sync) or Epic 4
> surfaces, and it does not reopen any Epic 3 decision (G2/G3/D-G/D-H/D-J). No code is changed by
> this document.
>
> **It amends two clauses of `ML_Heuristic_design.md`** (§9 "DO NOT introduce deep learning" and
> §10 "1–2 ML submodels maximum"). Those amendments are **PD-1** and **PD-2**, both now **ratified**.
> **The spec edit itself has not been performed** — it is a prerequisite work item, **S-SPEC** (§3),
> with the proposed replacement text written out there. S1 does not start until S-SPEC is merged.

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
| **`collected_v4`** | **v4** | **205** | ~~**real, collected**~~ → **AI-generated, AI-labelled** *(corrected 2026-08-26, DFD-1 — see the Amendment)* |
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

## 1.7 Deployment constraints (owner-stated 2026-08-24; hardware floor ratified as PD-10)

| Constraint | Value | Consequence |
|---|---|---|
| Purpose | Improve current features **and** deploy modern edge AI | Two tracks: featurizer upgrade + new capability |
| "No deep learning" (§9) | Amended under **PD-1** — narrow, guardrailed exception | Spec edit required — **S-SPEC** |
| Install size | 1–2 GB acceptable; >2 GB reopens debate | Superseded in practice by **PD-5**'s size cap, whose value is still unset (§11) |
| Distribution | Solo dev, educational / internal group, non-commercial | License ceases to be a differentiator |
| Model delivery | **Bundled** — no first-run download (**PD-5**) | Requires a delivery mechanism the repo does not have (§1.8) |
| **Reference hardware** (**PD-10**) | 10th-gen Intel Core mobile, mainstream U-series or equivalent · **8 GB RAM** · integrated graphics · Win10 x64 at the supported floor | **CPU EP** is the surface the latency gate is measured on; RAM is now a stated budget, not a guess |

**Precision on the OS floor** **[F]**: the csproj already targets `net10.0-windows10.0.19041.0` with
no `SupportedOSPlatformVersion` override, so the minimum OS the build actually admits is **Windows 10
build 19041** (2004 / 20H1), not "Windows 10" generally. That is *tighter* than PD-10's "Win10 x64 at
the supported floor", and it subsumes every OS-version prerequisite in this plan — see §2.3.

**PD-10 makes the measurement surface unambiguous** and retires the vaguer "floor hardware profile"
used in the first draft: latency and RSS are measured on the reference class above, on the **CPU
execution provider**, and **not on the developer's machine alone**. DirectML is evaluated separately
as Tier 2 and is never the baseline for viability. The 8 GB figure is a real constraint on peak RSS;
S0 reports measured RSS against it rather than against an invented ceiling.

**[F]** The Windows 10 floor **permanently rules out Windows AI APIs / Phi Silica / Aion Instruct**
(Windows 11 Copilot+ PC, plus a Limited Access Feature token, plus — on the GPU path — Developer
Mode and an Insider Experimental build). Microsoft has additionally announced Phi Silica's
replacement by Aion Instruct with **Phi Silica removed from retail devices in November 2026**.
Building on that surface was never viable here and is now also a moving target.

## 1.8 Finding E — PD-5 bundles the model into an installer that does not exist

**[F]** The repo has **no installer, no packaging, and no release pipeline.** Verified three ways at
`9c747be`:

- No `.iss` / `.wxs` / `.wapproj` / `.pubxml` / `.nuspec` anywhere in the tree. (`SetupPage.xaml` and
  `SetupViewModel.cs` are the in-app first-run wizard, not an installer.)
- `.github/workflows/` contains **only** `ci.yml` — restore, build, test, a profile-write leak check,
  and a `.trx` upload. No publish, no packaging, no release artifact.
- Two merged documents already record it as a known gap:
  `docs/plans/2026-07-27-post-epic1-stabilization.md:1758` — *"No installer, no packaging, no release
  pipeline; no model artifacts committed (both models train per-machine at runtime) ... Post-Epic-2
  productionisation. WP-1 delivers CI, not CD."* — and
  `docs/reports/2026-07-27-epic2-prep-current-state-assessment.md:859`.

**Why this is load-bearing rather than a detail.** PD-5 ratifies *"the model is bundled into the
installer"*. That mechanism does not exist, and building it is explicitly deferred to
**post-Epic-2** — work this plan does not touch, behind an epic that has not started. PD-5 as written
therefore rests on a premise the repo cannot currently satisfy. This is the one place where a
**ratified decision** conflicts with repo state rather than with the draft.

**It is also a first for the project.** **[F]** M7 and M8-A ship **no model artifacts at all**: both
train per-machine at runtime from the embedded `seed_intents.csv`. A frozen pretrained encoder cannot
be trained per-machine — it is an asset that must arrive with the application. **This plan introduces
the project's first shipped model binary, into a project that has no mechanism for shipping
binaries.** That is the gap, stated plainly.

**Reconciliation candidate — build-time acquisition, runtime bundling.** **[R]** PD-5's prohibition is
on a **runtime** acquisition path (*"no first-run network download"*). It says nothing about the
**build**. Fetching the encoder during CI/publish and shipping it inside the build output satisfies
every clause of PD-5 — bundled, no first-run download, offline at runtime, no network anywhere in
`Services/ML/*` — while keeping ~250 MB of binary out of git (R-9).

Offered as a candidate, **not adopted.** It assumes distribution is a `dotnet publish` folder handed
to the group, and **that is not verified** — no document in the repo records how the app currently
reaches its users.

**Resolved by PD-11: this is decided at S4, not now.** S-SPEC through S3 need no delivery mechanism —
S0's harness is throwaway and S1–S3 read a model file from wherever it sits. Deciding after S0 means
deciding with the artifact's real packaged size in hand, which is also what the size cap needs. The
option set is in §S4.

**One design consequence lands earlier, regardless of which option wins** **[F]**:
`LocalModelStorageProvider` resolves to `%AppData%\SmartStudyPlanner\models` and holds *trained*
artifacts (`study_time.zip`, `meta.json`), creating the directory on construction. A bundled,
read-only, pretrained encoder does not belong there — it belongs next to the executable
(`AppContext.BaseDirectory`). **S1 therefore needs its own model-location resolver**, separate from
`IModelStorageProvider`, whose default S4 fixes. **[R]**

---

# 2. Approach

## 2.1 Chosen: frozen encoder + linear head

**EmbeddingGemma-300M, int8 ONNX, frozen, via `Microsoft.ML.OnnxRuntime`, feeding the existing
`SdcaMaximumEntropy` head.** **[R]**

Rationale, in the order that matters:

1. **The encoder is frozen and the head stays linear — under PD-1 this is a guardrail, not a
   preference.** PD-1 ratifies the neural exception *only* on those terms (guardrails 1-4: frozen
   only; no fine-tuning; feature extractor, not decision-maker; the linear/deterministic decision
   layer remains authoritative). The design the first draft chose on engineering grounds is the
   design PD-1 now requires. It remains the better one on the merits: a frozen encoder + linear head
   preserves on-device retrain in seconds, keeps the decision layer inspectable, and leaves the
   entire `TrainAndSaveAsync` / atomic-swap / seed-hash lifecycle working unchanged.
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
requirement and is the one member of that family worth testing at all. **Per PD-8 it is not in the
initial S0 set** — it is a conditional extension, run only if Arms A and B together fail to produce
evidence strong enough to decide on. **[F]** (owner-ratified)

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

## 2.3 Runtime and hardware tiering — one build, one bundled model

**Ratified as PD-5.** One build, one installer, model bundled, tier resolved at runtime. The two-SKU
option (lightweight / discrete-GPU) is rejected.

PD-5 draws a separation this plan holds to throughout:

| Axis | Ratified policy |
|---|---|
| **Distribution** | one build, model **bundled** — no first-run download, no CDN, no auto-update |
| **Execution** | **CPU is the default and the baseline**; DirectML is optional acceleration only |

Bundling does **not** mean every machine runs the neural path. Model tier is a **runtime capability
probe**, and the seams already exist: `IIntentClassifier` is nullable **[F]**, `IModelStorageProvider`
is swappable **[F]**, and "app runs with zero model files" is already a tested contract **[F]**.

| Tier | Condition | Behaviour |
|---|---|---|
| **0** | **encoder asset missing or unloadable** — file absent, corrupt, or `InferenceSession` construction throws | current heuristic — already tested, already shipped |
| **1** | **default** | encoder int8, **CPU execution provider** |
| **2** | opt-in; DX12 GPU present **and** CPU-parity check passed | same encoder, **DirectML EP** |

**Tier 0 changes meaning under PD-5, and the change matters.** In the first draft Tier 0 was a
*distribution* state — the user who did not receive the model. Under bundling that user does not
exist by design, so Tier 0 becomes a **robustness** state: asset absent, asset corrupt, or session
construction failed. PD-5 states *"Tier 0 must remain functional"*, so the tier survives — but the
zero-model-file gate in §5 is now testing **fault tolerance**, not an install variant, and it must be
exercised by deleting a file the build is expected to have placed there. **[R]**

Two installers would double the release and QA surface for a solo developer immediately after
WP-1…WP-6 spent six work packages on release hygiene — and would do it in a project that, per §1.8,
does not yet have a single release pipeline to double. **[I]**

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

**Neither route is recommended here, deliberately.** PD-7 ratifies the tokenization route as **an
S0 finding, not a design-time choice**: *"Do not choose tokenization route ahead of evidence."* The
first draft named Route B as the default recommendation; **that recommendation is withdrawn.** Route
B is plausibly cleaner for a C# consumer **[I]**, and that is the whole of what can be said before
measurement. Neither route is verified against `net10.0` in this project, and S0 verifies both per
arm by loading the vocabulary — not by reading a documentation page.

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
report is accepted** (PD-3). **S-SPEC** was a prerequisite for S1 only — it did not gate S0, and it
is now done.

## S-SPEC — Amend `ML_Heuristic_design.md` — **EXECUTED 2026-08-24 (`d141db1`)**

PD-1 and PD-2 were ratified while **the spec still said the opposite**, and the owner's handoff was
explicit: *"Update `ML_Heuristic_design.md` explicitly rather than silently working around the old
prohibition."* Until it merged, any S1 code would have contradicted a normative document.

**Owner approved the wording verbatim on 2026-08-24 and the amendment is committed.** What shipped,
against the proposal below:

- **§9** keeps the prohibition and gains **§9.1**, the narrow exception, with all eight guardrails.
  The `DO NOT: introduce deep learning` bullet was **not** softened — it now points at §9.1, so the
  default stays *prohibited* and anything outside those terms is a new owner decision.
- **§10** gains **"Unit of the cap"**: artifacts, not heads; the two governance axes; and the
  requirement that each new capability carry its own owner approval.
- Both amendments are dated and link back to this plan, so the derivation is recoverable from the
  spec rather than only from here.

**S1 is unblocked.** The text below is retained as the record of what was approved.

**File map:** `docs/specs/ML_Heuristic_design.md` only.

**Proposed §9 amendment** — retain the prohibition, add a bounded exception:

> **Deep learning remains prohibited**, with one narrow exception. Frozen, pretrained neural encoders
> may be used as feature extractors inside an existing prediction pipeline, subject to all of:
> (1) frozen only; (2) no fine-tuning at runtime or on-device; (3) the encoder is a feature
> extractor, never an autonomous decision-maker; (4) the linear/deterministic decision layer remains
> authoritative; (5) the confidence and fallback policy remain in force; (6) offline-first inference
> is preserved; (7) the deployed-artifact limits of §10 continue to apply; (8) this exception confers
> no general permission for model sprawl, generative SLMs, or autonomous deep-learning components.
> *(Amended 2026-08-24 under PD-1.)*

**Proposed §10 amendment** — define the unit of the cap:

> The "1–2 ML submodels maximum" cap counts **deployed model artifacts**, not prediction heads. One
> shared frozen encoder serving task-type, difficulty, and temporal heads counts as **one** artifact.
> Prediction heads are **not** unlimited: the artifact count governs deployment, runtime, maintenance
> and asset surface, while **each new prediction capability requires explicit owner approval through
> its own proposal**. A shared encoder must not be used as a loophole for adding heads silently.
> *(Amended 2026-08-24 under PD-2.)*

**Exit criteria:** both clauses amended and dated; every other clause untouched; no code changed.

## S0 — Offline pilot *(no production code — GATE)*

**Purpose:** establish whether a neural encoder actually beats n-grams on this project's real data,
before any dependency is added.

**Design** — train on the **698 synthetic rows only**, test on the **205 held-out real
`collected_v4` rows**:

| Arm | Featurizer | Head | In initial S0? |
|---|---|---|---|
| baseline | `FeaturizeText` (current production) | `SdcaMaximumEntropy` | yes |
| A | EmbeddingGemma-300M int8, 256-dim MRL | `SdcaMaximumEntropy` | yes |
| B | `multilingual-e5-small` | `SdcaMaximumEntropy` | yes |
| C | `hiieu/halong_embedding` | `SdcaMaximumEntropy` | **no — conditional extension only (PD-8)** |

**PD-8 fixes the initial set at baseline + A + B.** Arm C is run **only** if A and B together fail to
produce evidence strong enough for a trustworthy decision. It is a contingency, not a workload item,
and it must not be pulled forward to "be thorough".

**PD-3 fixes what S0 is asking.** The question is *"does the neural encoder show enough evidence of
value on the real data that we should continue?"* — **not** *"is this dataset mature enough to be the
final production training set?"* The 3-of-5 class coverage (§1.4) is **accepted for the pilot**.
Dataset maturity is a separate, parallel workstream (§3.1) and is **not** a prerequisite for running
S0.

**File map (all new, none shipped in the app):**
- `tools/ml-pilot/` — accuracy harness for outputs 1, 2, 7 (language at implementer's discretion;
  Python is acceptable here since nothing ships)
- `tools/ml-pilot/dotnet/` — **.NET console harness for outputs 3, 4, 5, 6**; required, not optional
  (see the harness-split table below)
- `docs/reports/2026-XX-XX-encoder-pilot.md` — results

**Required outputs** (the owner's enumerated list, in order):
1. **Per-class** precision/recall for the 3 covered classes. No single headline accuracy number.
2. **Confidence-vs-accuracy curve per arm** — the input to S3's threshold re-derivation, and not
   optional.
3. **Cold-start model load time.**
4. **Per-inference latency**, measured on the **PD-10 reference class** (10th-gen Intel Core mobile
   U-series or equivalent, 8 GB RAM, integrated graphics), on the **CPU execution provider** —
   explicitly *not* on the developer's machine alone.
5. **Peak RSS** during inference, reported against PD-10's 8 GB budget. No ceiling is asserted here;
   the number is measured first and a ceiling derived from it.
6. **Tokenization viability per arm** (§2.4, PD-7) — which of Route A / Route B works for that
   encoder on `net10.0`, verified by actually loading the vocabulary, not by reading a doc page. An
   arm with no workable .NET tokenization path is **rejected regardless of its accuracy**.
7. **Explicit limitations from the 3-of-5 class real dataset** — stated in the report itself, not
   only here.
8. **Packaged on-disk size per arm** — the encoder file(s) plus tokenizer assets, as they would ship.
   Added under PD-11: the PD-5 size cap cannot be set to a sensible number before this is measured,
   and S4 is where both are settled.

**Winner criterion (PD-9) — no fixed effect size.** No arbitrary threshold such as "+2 F1" is set. An
arm wins only when the evidence is strong across *all* of:

- improvement over baseline **beyond run-to-run variance**;
- per-class results acceptable (not one class carrying the average);
- confidence behaviour usable — i.e. the curve from output 2 can actually support a gate;
- latency and RSS fit the PD-10 hardware budget;
- tokenization path viable (output 6).

**If A and B cannot be reliably distinguished, do not force a winner.** The decision then becomes
whether more evidence is justified: if yes → conditional Arm C and/or data expansion; if no → stop or
defer. Declaring a winner on a difference inside the noise is the failure mode PD-9 exists to
prevent.

**The harness is deliberately split in two — the accuracy outputs and the runtime outputs are not
the same experiment** **[R]**:

| Outputs | Where measured | Why |
|---|---|---|
| 1, 2, 7 — accuracy, confidence curve, dataset limitations | whatever is fastest to write (Python is fine; nothing ships) | Only relative accuracy matters; the runtime is irrelevant to it |
| **3, 4, 5, 6 — load time, latency, RSS, tokenization** | **the .NET path: `InferenceSession` + real tokenizer + `SdcaMaximumEntropy`, on the PD-10 reference class** | Numbers from Python `onnxruntime` + an sklearn head **do not transfer**. These feed the 500 ms budget in §5 and the R-1 kill criterion — measuring them off-path would clear a gate that was never tested |

Concretely: a throwaway .NET console harness under `tools/ml-pilot/dotnet/` is required for the
load-time / latency / RSS / tokenization row. It is not production code and is not shipped, but it
must exercise the same runtime, tokenizer, and head that S1–S2 will use, on the PD-10 reference
class. Keeping accuracy and runtime experiments conceptually separate is fine — **the runtime one
must still run on the stack that ships.**

**Exit criteria:** report written to `docs/reports/` and **owner-accepted** (PD-3). **A null result
is a valid and useful outcome** — if the encoder cannot beat n-grams on real rows it has never seen,
it will not help in production, and this plan stops at S0 having cost one script.

**Kill criterion, stated in advance:** if Arm A and Arm B both fail to improve macro-F1 over baseline
by a margin larger than the run-to-run variance, **do not proceed to S1.** Note this is already a
variance-based criterion, not a fixed effect size, and so stands unchanged under PD-9 — PD-9 adds the
four further dimensions above that an arm must also satisfy to *win*, which is a stricter bar than
merely not being killed.

## 3.1 Parallel workstream — dataset maturity *(PD-3; not a prerequisite for S0)*

PD-3 ratifies dataset improvement as an **independent, ongoing** workstream that must **not** block
the pilot:

- collect the missing classes (`KiemTraThuongXuyen`, `ThiCuoiKy`);
- improve real-world coverage and capture Vietnamese linguistic variation;
- reduce class imbalance **where appropriate** — *"perfect class balance is not the goal; adequate
  representation of real-world usage and important linguistic phenomena is"*;
- deduplicate and quality-filter;
- version datasets;
- build a stronger held-out evaluation set.

**[R]** The last two are the ones that pay off soonest: §1.4 exists as a finding only because
`_merge_seed.py` made the merge history recoverable. Versioning and a held-out set that is held out
*before* training are what stop the 96.2% problem from recurring.

This workstream has no slice number here because it is not part of this plan's delivery. It is
recorded so that "the dataset is immature" is never used as a reason to delay S0, and never forgotten
once S0 is done.

## S1 — Encoder seam *(no behaviour change)* — requires **S-SPEC merged** and **S0 accepted**

**File map:**
- Create `Core/ML/Contracts/ITextEmbeddingProvider.cs` — `float[]? Embed(string text)`, returns
  `null` when unavailable
- Create `Services/ML/Embedding/OnnxTextEmbeddingProvider.cs` — owns **one** long-lived
  `InferenceSession`
- Create `Services/ML/Embedding/NullTextEmbeddingProvider.cs`
- **Create `Services/ML/Embedding/ITextTokenizer.cs` + implementation** — required only if S0 selects
  **Route A** (§2.4); under **Route B** tokenization lives inside the ONNX graph and no .NET
  tokenizer type exists. **S0's output 6 decides which of these two file maps applies.**
- Modify `SmartStudyPlanner.csproj` — add `Microsoft.ML.OnnxRuntime`; **under Route A also add
  `Microsoft.ML.Tokenizers`, and check whether it forces a `Microsoft.ML` bump off the pinned
  3.0.1** **[F]** (the SentencePiece tokenizer work landed with ML.NET 4.0). A transitive bump of the
  package M7 and M8-A both depend on is a blast radius that must be reported before it is taken.
- Modify `Services/ServiceLocator.cs` — register provider
- **Create a model-location resolver** — the encoder is a read-only asset next to the executable
  (`AppContext.BaseDirectory`), **not** a trained artifact in `%AppData%`. Do **not** extend
  `IModelStorageProvider` for it: that type is about writable trained models and creates its
  directory on construction (§1.8). S4 fixes the resolver's default once PD-11's delivery decision is
  made; S1 only needs it to be injectable.
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

Closes Finding D (§1.6). **Must ship in the same release as S2** (PD-4) — S2 without S3 silently
changes user-visible routing.

**PD-4 permits internal separation, not a partial ship.** S2 and S3 may be sequenced analytically:

```text
S2 measurement  →  new confidence distribution  →  S3 calibration / gate derivation
                                                        ↓
                                             ONE production release unit
```

That ordering is in fact necessary — the new distribution cannot be measured before the featurizer
exists. What PD-4 forbids is the **production state** containing an uncalibrated S2-only
intermediate. Work in whatever order the measurement requires; ship once.

**File map:**
- Modify `Services/ML/DefaultMlConfidencePolicy.cs` — threshold re-derived from S0's curve;
  **document the derivation in the XML doc comment, including the date and the report it came from**
- Modify `Services/ML/IntentClassifierAdapter.cs` — add heuristic-agreement as a second signal
  (available at zero cost: the heuristic task-type parser already runs)
- Modify `SmartStudyPlanner.Tests/Services/ML/IntentClassifierAdapterTests.cs`

**Exit criteria:** a mutation test proves the gate can go red — a deliberately miscalibrated
confidence must fail a test. A gate whose pass cannot be distinguished from a broken gate is not
evidence.

## S4 — Model tiering and delivery

**Acquisition is no longer an open question.** PD-5 decides it: **bundled**, one build, **no
first-run network download**, no CDN, no auto-update channel. What S4 must still solve is *how* an
asset gets bundled in a project with no packaging pipeline (§1.8) — a mechanism question, not a
policy one.

**File map:**
- Modify `Services/ML/LocalModelStorageProvider.cs` / `IModelStorageProvider.cs` — locate encoder assets
- Create `Services/ML/Embedding/ExecutionProviderProbe.cs` — Tier 1 / Tier 2 detection
- Modify `Views/` settings surface — tier display + Tier 2 opt-in toggle
- Delivery mechanism per the §11 owner decision — **must not** put the binary in git (R-9), **must
  not** add a runtime network path (PD-5)

**Delivery mechanism — decided here, per PD-11.** S4 opens by choosing from this set, with S0's
measured packaged size (output 8) in hand **[R]**:

| Option | Shape | Trade |
|---|---|---|
| **a** — build-time fetch, shipped in output | MSBuild target downloads the encoder at a **pinned revision** with SHA-256 verification; the `dotnet publish` folder is the deliverable | Satisfies every PD-5 clause; keeps git clean; needs no installer. Makes the build network-dependent (a first here) and needs a local cache. **Assumes folder-handoff distribution — unverified** |
| **b** — Git LFS | Asset travels with the clone | Build stays offline, no new pipeline. ~250 MB per version against a small free quota with CI clones; no `.gitattributes` exists today **[F]**; unwinding LFS later is painful |
| **c** — build the installer | Pull post-Epic-2 productionisation forward | PD-5 exactly as written, and the project gains the release story it lacks. A whole unscoped workstream, sequenced ahead of Epic 2 |
| **d** — documented manual asset drop | Group places the file once; Tier 0 until then | Zero pipeline work. **This is side-loading, which PD-5 refuses** — choosing it means *amending* PD-5, not answering the question |

Option **a** is the standing recommendation, conditional on confirming how the app actually reaches
its users — a question no document in the repo answers.

**Size governance (PD-5).** The bundled package must stay under an **owner-defined maximum size cap**,
whose value is set here, informed by S0 output 8 (PD-11). If the package exceeds it, the ratified
instruction is unambiguous: **stop.** Do not silently side-load, do not silently raise the cap, do not
silently substitute another model — **reopen the owner decision.**

**Exit criteria:** Tier 0 / 1 / 2 all exercised; Tier 2 passes the CPU-parity check; tier is visible
to the user; **Tier 0 remains fully functional with the asset deleted** (§2.3 — this is now a
fault-tolerance test); packaged size recorded and compared against the cap.

## S5 — Difficulty head *(gated twice — do not start blind)*

**Gate 1 — owner approval (PD-2).** A shared encoder is **not** a licence to add heads. *"Do not
automatically activate S5 or S6 merely because the shared encoder is accepted."* Each new prediction
capability needs its own explicit owner approval, whatever the artifact count says.

**Gate 2 — data volume.** This slice **opens with a measurement, not with code:** count rows in
`DifficultyLabelLogs` and re-read `Difficulty_ML_model_proposal.md` against that count. If the volume
is insufficient, **stop and record the count** — that is a useful result, and it updates a document
that currently says "no trigger applies."

Only if both gates pass: a second linear head on the same embedding, behind `IDifficultyPredictor`
exactly as that proposal specifies, with confidence-gated fallback to `DefaultDifficultyKeywordParser`.

## S6 — M9 temporal span head *(design intent only)*

Per §2.5. **Requires its own plan and its own owner approval** (PD-2 governance, as for S5). It is
**not** folded into encoder adoption and is **not** activated by the encoder being accepted. Listed
here only so the shared-encoder design intent is recorded now rather than rediscovered later.

---

# 4. Pre-edit checklist

Per `CLAUDE.md`, **`gitnexus_impact` is mandatory before editing any symbol.** Run upstream impact
on each of these and report blast radius before the corresponding slice:

| Symbol | Slice | Expected risk **[I]** |
|---|---|---|
| *(no code symbols)* | S-SPEC, S0 | **NONE** — but `detect_changes` will **not** report zero: this repo's graph indexes markdown headings as `Section:` symbols, so a docs-only change fires them (37 did on the 2026-08-24 revision of this file). The gate is **zero code symbols and zero affected processes**, not an empty result |
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
4. **Zero-model-file contract — now a fault-tolerance test** (§2.3) — delete
   `%AppData%\SmartStudyPlanner\models\*` **and the bundled encoder asset**, launch, confirm
   Dashboard + Analytics + Smart Add all function on Tier 0. Under PD-5 the asset is expected to be
   present, so this gate is deliberately deleting something the build put there. It is the contract
   most at risk from this plan and must be re-run at S2 **and** S4, not once.
5. **Latency budget** — Smart Add submit-to-populate must stay under **500 ms** on the **PD-10
   reference class** (CPU EP), model already loaded. **Ratified as PD-12**; measured, not assumed.
   The **measurement protocol** (warm/cold, percentile, sample count) remains an open planning
   question (§11.2 P3) and must be written down in the S0 report before the number means anything.
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
- **Fine-tuning the encoder — prohibited, not merely declined.** PD-1 guardrails 1 and 2 require
  "frozen only" and "no fine-tuning in runtime or on-device". The first draft said PD-1 *would permit*
  fine-tuning; **that is now wrong and is corrected.** This plan reads the guardrails conservatively:
  the encoder is frozen, full stop. Offline developer-side fine-tuning producing a new bundled
  artifact is *not* read as authorised by the runtime/on-device wording, and would need its own owner
  decision.
- **M8-B ML weight optimizer** (still awaiting matured `WeightChangeLog`).
- **M8-C** StudyTime retrain on Focus telemetry.
- **Cloud model storage.** `IModelStorageProvider` keeps the swap possible; nothing uses it.
- **Epic 2 (LAN sync) and Epic 4 surfaces.** Untouched.
- **Any Epic 3 decision** — G2, G3, D-G, D-H, D-J are not reopened.
- **Any model acquisition beyond bundling.** PD-5 authorises **no** CDN, **no** auto-update channel,
  and **no** first-run download. Bundling is the only sanctioned delivery route.
- **Building the installer / release pipeline itself.** §1.8 establishes it does not exist and is
  deferred to post-Epic-2. This plan surfaces the dependency; it does not take on the work.

---

# 7. Risks

| # | Risk | Mitigation |
|---|---|---|
| R-1 | Encoder shows no gain over n-grams | S0 is a gate with a stated kill criterion; cost is one script |
| R-2 | S0 result is over-read from 3 of 5 classes | Per-class reporting mandated; caveat stated in the report, not just here |
| R-3 | Threshold shift silently changes user-visible routing | S3 mandatory in the same release as S2; mutation test required |
| R-4 | Shared `IMlConfidencePolicy` retunes M8-B by accident | Called out in §4; split the policy if derivations diverge |
| R-5 | Bundled package exceeds the PD-5 size cap | int8 only; **stop and reopen the owner decision** — PD-5 forbids silently side-loading, raising the cap, or swapping models. Cap value still unset (§11) |
| R-6 | DirectML accuracy bug on Intel iGPU | Tier 2 opt-in + CPU-parity check (§2.3) |
| R-7 | Session lifetime mistake replicates the per-call `CreatePredictionEngine` pattern | S1 asserts single session construction |
| R-8 | Zero-model-file contract breaks | Re-verified at S2 **and** S4 |
| R-9 | ~250 MB of model binary lands in git because PD-5 says "bundled" | Model assets must never enter git. The §1.8 build-time-acquisition candidate reconciles bundling with this; the delivery mechanism is chosen at S4 from the §S4 option set (PD-11) and must be settled **before** S4 writes any packaging |
| R-10 | No workable .NET tokenizer for the winning encoder | S0 output 6 verifies the route per arm **before** S1; an arm without one is disqualified |
| R-11 | `Microsoft.ML.Tokenizers` forces a `Microsoft.ML` 3.0.1 → 4.x bump, touching M7 + M8-A | Report the bump as blast radius at S1; Route B (§2.4) avoids the dependency entirely |
| R-12 | S0 latency measured off the .NET path clears a gate it did not test | Harness split mandated in S0; outputs 3–6 must come from the .NET console harness on the PD-10 reference class |
| R-13 | **PD-5 names an installer the repo does not have** (§1.8), and building one is deferred behind Epic 2 | Surfaced, not absorbed. PD-11 confines it to S4 and defers the choice until S0 has measured the artifact; S-SPEC–S3 are unaffected. Stays open on the books until S4 picks an option |
| R-14 | Tier 0 rots because bundling makes it look unreachable | Tier 0 is redefined as a fault-tolerance state (§2.3) and its gate (§5.4) deliberately deletes a bundled asset |
| R-15 | S0 measured only on the developer's machine, quietly becoming the product floor | PD-10 fixes the reference class; the report must name the actual machine used, and a dev-machine-only number is not an acceptable output |
| R-16 | A head is added later on the strength of the shared encoder alone | PD-2 governance: artifact count ≠ capability count; every head needs its own owner approval (S5 gate 1, S6) |

---

# 8. Owner checkpoints

Round 1 is complete — PD-1 … PD-10 are ratified. What remains:

1. ~~Ratify PD-1 and PD-2.~~ **Done 2026-08-24.** ~~The spec edit still blocks S1.~~ **S-SPEC
   executed 2026-08-24 (`d141db1`); S1 unblocked.**
2. ~~Before scope freeze, settle delivery mechanism and size cap.~~ **Deferred to S4 by PD-11.**
   Both are now S4 decisions taken with S0's measurements in hand.
3. **After S0** — accept or reject the pilot report. Blocking; PD-3 gate, PD-9 winner criterion, kill
   criterion applies. A null result ends the plan.
4. ~~At S4, decide acquisition.~~ **Decided by PD-5: bundled.** Only the *mechanism* remains (§11).
5. **Before S5** — approve the capability (PD-2 governance) **and** review the `DifficultyLabelLogs`
   count against the deferred proposal's trigger. Two separate gates.
6. **Before S6** — approve the capability and commission a separate plan.

---

# 9. Parallel-dispatch decision

**Do not parallelise S0 → S4.** They are strictly sequential: S0 gates the whole plan, S2 depends on
S1's seam, S3 depends on S0's confidence curve, S4 depends on S2. **[R]**

The one parallelisable unit is **inside S0**: the pilot arms are independent and may be dispatched
concurrently, provided every arm reports against the identical split and the identical metric set.

Card S0-C (.NET runtime characterisation) may run concurrently with S0-B — it needs the candidate
model exports, not S0-B's accuracy results. Both depend on S0-A's split existing first.

**S-SPEC is independent of all of them** and may be done at any point before S1; it touches only
`docs/specs/ML_Heuristic_design.md`.

**Cards S0-A, S0-B and S0-C are dispatchable now** — S-SPEC through S3 are frozen and activated
(2026-08-24). **Cards S1 and S2+S3 are not**: they wait on S0's report being accepted (PD-3).

> *Superseded 2026-08-25.* The S0 cards **ran**; S1 and S2+S3 **never became dispatchable** — the S0
> report was accepted **and the initiative stopped there** (EVA-16). No card below S0 was executed.

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
- **Mission:** run **Arms A and B only** (PD-8) through the S0-A harness for outputs 1, 2, 7.
- **Scope:** `tools/ml-pilot/` only. Must consume S0-A's split verbatim — no re-splitting.
- **Do not run Arm C.** It is a conditional extension, unlocked only by an explicit owner decision
  after A and B are reported. Running it "while we're here" violates PD-8.
- **Stop when:** both arms reported on identical accuracy metrics and confidence curves. If A and B
  are indistinguishable, say so and stop — **do not force a winner** (PD-9).

### Card S0-C — .NET runtime characterisation
- **Mission:** outputs **3, 4, 5, 6** — cold-start load, per-inference latency, peak RSS, and the
  working tokenization route — measured through `InferenceSession` + real tokenizer +
  `SdcaMaximumEntropy`.
- **Hardware:** the **PD-10 reference class**, CPU execution provider. **Name the actual machine in
  the report.** A developer-machine-only number is not an acceptable output (R-15).
- **Scope:** `tools/ml-pilot/dotnet/` only. Throwaway console app; touches nothing under
  `SmartStudyPlanner/`.
- **Why separate from S0-B:** Python-measured latency does not transfer to the .NET path (R-12).
- **Report RSS against PD-10's 8 GB**, and do not invent a pass/fail ceiling — measure first.
- **Stop when:** every surviving arm has a verified tokenization route (PD-7) and load/latency/RSS
  figures from the reference class.

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

# 10. Decisions *(ADR-style — owner-ratified)*

**All twelve decisions below are ratified.** PD-1 … PD-7 were proposed in the first draft and
ratified — two of them (PD-1, PD-5) in a **modified** form, recorded as such. PD-8 … PD-10 are new in
round 1 and originate with the owner. **PD-11 is round 2; PD-12 is round 3.** The plan itself was
**active** as of 2026-08-24; §11 records the freeze boundary *(the plan is `stopped_at_s0` since
2026-08-25 — the twelve decisions remain ratified, PD-1's §9.1 exception included; see the banner)*.

### PD-1 — Amend `ML_Heuristic_design.md` §9: a narrow, guardrailed neural-encoder exception

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Ratified in modified form** — see below.

**Decision (as ratified).** The general prohibition on deep learning **stays**. A narrowly scoped
exception is added:

> Frozen, pretrained neural encoders may be used as feature extractors / featurizers inside existing
> prediction pipelines, provided the decision layer remains linear or deterministic and the existing
> confidence / fallback and offline-first architecture is preserved.

Subject to eight mandatory guardrails: (1) frozen only; (2) no fine-tuning at runtime or on-device;
(3) feature extractor, not autonomous decision-maker; (4) the linear/deterministic decision layer
remains authoritative; (5) confidence and fallback policy remain in force; (6) offline-first inference
intact; (7) existing model/deployment limits still apply; (8) **no** general permission for model
sprawl, generative SLMs, or autonomous deep-learning components.

**Modified from the draft.** The first draft proposed *rewriting* the clause into a conditional
permission. The owner instead **kept the prohibition and carved an exception under it**. The
difference is not cosmetic: under the draft's wording the default was "permitted if conditions hold";
under the ratified wording the default remains "prohibited", and the exception has to be argued for
each time. The second is the more durable construction, and it is what S-SPEC (§3) implements.

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

### PD-2 — Amend §10: count *models*, not *heads* — with capability governance

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`).

**Decision (as ratified).** The "1–2 ML submodels maximum" cap counts **deployed model artifacts**,
not prediction heads. One shared encoder with task-type, difficulty, and temporal heads counts as
**one**.

**Extended by the owner — two axes, not one:**

| Axis | Governs |
|---|---|
| **Artifact count** | deployment, runtime, maintenance, asset surface |
| **Capability / head count** | product scope and model responsibility |

Heads are **not** unlimited. **Every new prediction capability requires explicit owner approval
through its own proposal**, regardless of the artifact count. *"Do not use a shared encoder as a
loophole to silently add arbitrary heads."* This is why S5 now has **two** gates and S6 requires its
own plan and its own approval.

**Why it had to be made.** §10's cap is real and worth keeping, but the multi-head design makes the
count ambiguous — three heads on one encoder could be argued as one model or three. Left unresolved,
that ambiguity becomes an argument during code review of S5, at the least convenient moment.

**What it's for.** It keeps the cap's actual purpose — bounding deployment, download, and maintenance
surface — while allowing the design that minimises exactly those things. Three heads on one encoder
is *cheaper* on every axis §10 cares about than three separate models would be.

**Experience for future development.** When a numeric cap exists, define its unit before the
architecture makes the unit ambiguous, not after.

### PD-3 — S0 is a gate with a kill criterion, not a formality

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`).

**Decision (as ratified).** No production code before S0's report is accepted. A null result stops
the plan.

**Extended by the owner — what S0 is and is not asking.** S0 answers *"does the neural encoder show
enough evidence of value on the real data that we should continue?"* It does **not** claim the
dataset is mature enough to be the final production training/evaluation set. **The 3-of-5 class
coverage is accepted for the pilot.** Dataset maturity is a separate ongoing workstream (§3.1) and is
explicitly **not** a prerequisite for running S0 — *"do not make a perfectly mature dataset a
prerequisite for running the initial pilot"*, and *"avoid treating perfect class balance as the
goal"*.

This resolves a tension the draft left implicit: §1.4's caveats read as reasons to hesitate. Ratified,
they are reasons to **report carefully**, not to wait.

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

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`).

**Decision (as ratified).** The featurizer swap and the confidence re-derivation are one **production
release unit**, and one agent task card.

**Clarified by the owner.** They may be separated *internally* — S2 measurement → new confidence
distribution → S3 calibration → one release. What is forbidden is a **production state** containing
an uncalibrated S2-only intermediate. The distinction matters because the internal ordering is not
optional: the new score distribution cannot be measured before the featurizer exists.

**Why it had to be made.** The 0.60 threshold is calibrated to the old featurizer's score
distribution (§1.6). Shipping S2 alone would move the ML-vs-heuristic routing and the confidence
percentage shown to the user, while presenting as a pure refactor.

**What it's for.** It prevents a user-visible behaviour change from entering under a commit message
that does not mention it.

**Experience for future development.** Any threshold is a coupling between a gate and a score
distribution. Changing what produces the score without re-deriving the threshold is a behaviour
change, however much it looks like a refactor.

### PD-5 — One build, bundled model, size cap, runtime tiering

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Ratified as a superset of the draft** — see below.

**Decision (as ratified).** One installer, one build. **The model is bundled**; no first-run network
download, no CDN, no auto-update. Runtime capability determines the tier: Tier 0 heuristic-only,
Tier 1 CPU (default), Tier 2 DirectML (optional). The bundled package must stay under an
**owner-defined maximum size cap**; on breach, **stop and reopen the owner decision** — do not
silently side-load, raise the cap, or substitute a model.

**Broader than the draft's PD-5,** which decided only *one build vs. two SKUs*. The owner folded in
the acquisition question the draft had left open (§11 Q1 of the first draft) and answered it:
**bundled**. That is the decision that keeps the offline-first contract whole — the alternative
would have introduced the ML layer's first network call.

**Two consequences the ratification creates, both handled in this revision:**

1. **Tier 0 changes meaning.** It is no longer "the user who didn't get the model" but "the asset is
   missing or won't load" — a fault-tolerance state (§2.3). PD-5's *"Tier 0 must remain functional"*
   keeps it alive; §5.4 now tests it by deleting a bundled file.
2. **The mechanism does not exist.** §1.8: there is no installer, no packaging, no release pipeline,
   and building one is deferred behind Epic 2. This is the one ratified decision whose premise the
   repo cannot currently satisfy, and it is §11's first blocking question.

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

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Ratified exactly as proposed.**

**Decision.** The RoPE-vs-APE argument is demoted from evidence to architectural prior, in writing
(§2.2). The model recommendation is unchanged, and must be justified by project-specific S0 evidence.

**Added by the owner:** *"Do not silently restore the withdrawn benchmark claim."* Recorded here
because a withdrawn citation is exactly the kind of thing that reappears in a later summary written
by someone who only read §2.1.

**Why it had to be made.** The citation was load-bearing in the original recommendation and does not
support the weight placed on it: EmbeddingGemma postdates the benchmark and is not in it.

**What it's for.** It keeps the plan's evidence labelling honest, so that a future reader can tell
which parts of §2.1 are measured and which are reasoned.

**Experience for future development.** Check a benchmark's submission date against the release date
of every model you claim it ranks. "Model X is newer and better-architected" is a hypothesis; only
the pilot makes it a finding.

### PD-7 — Tokenization route is measured in S0, not chosen now

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Ratified and strengthened.**

**Decision (as ratified).** §2.4 names two routes (`Microsoft.ML.Tokenizers` vs. tokenization baked
into the ONNX graph) and picks neither. S0 output 6 decides, per arm, by loading the vocabulary on
`net10.0`. S1's file map is conditional on that outcome. **A candidate with no workable, verified
tokenization path on `net10.0` is rejected regardless of its offline accuracy** — tokenization
viability is part of candidate *selection*, not a post-selection implementation detail.

**Strengthened against the draft.** The draft, having said the route was S0's to decide, then named
Route B as "the default recommendation" two sentences later. The owner's *"do not choose tokenization
route ahead of evidence"* removes that. **The Route B recommendation is withdrawn** (§2.4). Naming a
default is how a measurement quietly becomes a confirmation.

**Why it had to be made.** `Microsoft.ML.OnnxRuntime` ships no tokenizer, so a string-in API needs
one — and this was missing from the first draft of the S1 file map entirely. Choosing a route from
documentation would have been guessing: the SentencePiece APIs are published under
`ml-dotnet-preview`, and this project pins `Microsoft.ML` 3.0.1 while that work landed in ML.NET 4.0.

**What it's for.** It keeps a package bump that would touch both M7 and M8-A from arriving as a
surprise inside an "add a NuGet reference" line, and it makes tokenizer availability a *selection
criterion* for the encoder rather than a problem discovered after one was chosen.

**Experience for future development.** When adopting a model runtime, check what it does **not**
ship. Inference engines routinely omit tokenization, and the gap between "string" and
"`input_ids` tensor" is a dependency with its own version graph — not glue code. And when you have
decided that something will be measured, do not also publish a favourite.

### PD-8 — S0 runs two encoder arms, not three

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Owner-originated.**

**Decision.** The initial S0 set is **baseline + Arm A (EmbeddingGemma-300M) + Arm B
(`multilingual-e5-small`)**. Arm C (`hiieu/halong_embedding`) is **not** run initially; it is a
conditional extension, reconsidered only if A and B together fail to produce evidence strong enough
for a trustworthy decision.

**Why it had to be made.** The draft listed Arm C as "optional" and asked the owner whether to include
it. "Optional" is not a schedule: it leaves an agent to decide workload by taste, and the natural bias
is to run everything. The owner converted a preference into a trigger.

**What it's for.** It keeps S0 cheap enough that a null result is genuinely affordable — which is the
whole basis of PD-3's kill criterion. A gate nobody wants to fail because it cost three arms of work
is not a gate.

**Experience for future development.** "Optional" in a plan is an unresolved decision wearing a
costume. Say *when* the optional thing runs, or leave it out.

### PD-9 — No fixed effect size; a multi-dimensional win criterion

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Owner-originated.**

**Decision.** No arbitrary threshold such as "+2 F1 points". An arm wins only on evidence that is
strong across **all** of: improvement beyond run-to-run variance; acceptable per-class results; usable
confidence behaviour; latency and RSS within the PD-10 budget; a viable tokenization path. **If A and
B cannot be reliably distinguished, do not force a winner** — decide instead whether more evidence is
justified (conditional Arm C, data expansion) or whether to stop/defer.

**Why it had to be made.** A single fixed number invites two failure modes at once: shipping a
regression in latency because F1 cleared the bar, and killing a real improvement because it landed at
+1.8. The draft's kill criterion was already variance-based rather than fixed, so it survives
unchanged — what was missing was the *win* test, which is a strictly higher bar than "not killed".

**What it's for.** It makes "we could not tell A from B" a reportable, legitimate outcome instead of a
gap someone fills with a coin flip and a paragraph of justification.

**Experience for future development.** Define the win condition on every dimension the decision
actually depends on, and give the tie an explicit branch. An unspecified tie always resolves in favour
of whoever writes the report.

### PD-10 — A named reference hardware class, measured on CPU

**Status:** **Ratified by the owner, 2026-08-24** (`2026-08-24-edge-ai-encoder-owner-decision-handoff.md`). **Owner-originated.**

**Decision.** S0 runtime measurements use a reference class representing a common student laptop:
**10th-gen Intel Core mobile, mainstream U-series or equivalent; 8 GB RAM; integrated graphics;
Windows 10 x64 at the supported floor.** The latency gate is measured on the **CPU execution
provider**. A discrete GPU is **not** required for baseline viability; DirectML is an optional Tier 2
capability evaluated separately. *"Do not benchmark only on the developer's machine and treat that as
the product floor."*

**Why it had to be made.** The draft said "floor hardware profile" and asked the owner which machine
that meant. "Integrated graphics" spans about a decade of Intel parts; the phrase cannot be measured
against, and R-1's outcome moves with it.

**What it's for.** It turns the 500 ms budget and the RSS figure into claims that can be checked, and
it forecloses the most common way a desktop performance gate goes wrong — being measured on the one
machine in the project that is fastest.

**Experience for future development.** A performance budget without a named machine is not a budget.
Fix the measurement surface at planning time, while it is still cheap to argue about.

---

### PD-11 — Delivery mechanism and size cap are decided at S4, not now

**Status:** **Ratified by the owner, 2026-08-24** (review round 2, in session). **Owner-originated
choice from an offered option set.**

**Decision.** The delivery mechanism for the bundled encoder (§1.8, R-13) and the PD-5 size-cap value
are **deferred to S4**. S-SPEC through S3 are frozen and may proceed; S4's scope stays open until S0
reports. The option set is recorded in §S4 so the deferral does not lose the analysis.

**Why it had to be made.** The previous revision called this "blocking scope freeze". On inspection
that was too broad: **only S4 depends on it.** S0's harness is throwaway, and S1–S3 read a model file
from wherever it happens to sit. Meanwhile the two questions are coupled to a number nobody has yet —
a size cap set before the artifact is measured is a guess, and PD-5's breach rule ("stop and reopen")
only means something if the cap was derived from a real figure.

**What it's for.** It stops the project from building a distribution pipeline for a model that PD-3's
kill criterion may discard. Options **a**, **b** and **c** all commit real work to shipping an encoder
that has not yet been shown to beat n-grams. Deferring costs nothing, because nothing before S4 needs
the answer, and it buys the one input the decision actually lacks.

**Experience for future development.** Before calling a decision "blocking", check *what* it blocks.
A dependency that stops one late slice is not the same as one that stops the plan, and conflating them
either stalls work that could proceed or forces a decision while the evidence for it is still missing.
Ask which slice fails without the answer — often it is later than it first appears.

---

### PD-12 — The 500 ms latency ceiling is ratified; its measurement protocol is not

**Status:** **Ratified by the owner, 2026-08-24** (review round 3, in session). **Owner confirmation
of a figure this plan had been carrying unratified.**

**Decision.** **500 ms** is the ratified ceiling for Smart Add submit-to-populate on the **PD-10
reference class**, **CPU execution provider**, model already loaded. It is normative on an owner
decision rather than on this plan's assertion that "the figure stands".

**What PD-12 does *not* settle.** The **measurement protocol** — warm versus cold runs, the
percentile reported, the sample count — remains an S0-owned planning question (§11.2 P3). The
**boundary** is fixed: invocation of quick-parse to structured fields populated, tokenization and the
encoder forward pass included, model load excluded. Only the statistics are open.

**Why it had to be made.** The specification pass found that the owner's handoff never ratified
500 ms — it *presupposed* it, listing *"exact 500 ms measurement protocol"* among details
intentionally not expanded. A figure that arrives by presupposition is one nobody has actually
agreed to. Left alone, the first S0 result that missed it would have reopened the question of whether
500 ms was ever the target, at the exact moment the answer mattered most.

**What it's for.** It makes the S0 latency result a **pass/fail gate** rather than a number in a
report. Without a ratified ceiling, PD-9's fourth win dimension — *"latency and RSS fit the hardware
budget"* — has no budget to test against, and an arm could win on accuracy while quietly failing the
constraint that motivated the reference class in the first place.

**Experience for future development.** A number that survives several review rounds without being
contradicted is not thereby approved. **Silence is not ratification**, and the gap only becomes
visible when the number is about to be enforced — which is the worst moment to find it. When a plan
says a figure "stands", check whose decision it stands on.

---

# 11. Freeze status

**Round 1 resolved four of the first draft's questions.** Acquisition policy → PD-5 (bundled). Arm C
→ PD-8 (conditional, not initial). Floor hardware → PD-10 (reference class). Latency ceiling → the
500 ms figure was left standing but **unratified**, and **round 3 ratified it as PD-12**; only its
measurement protocol stays open (P3). **Round 2 resolved the last two** by deferring them to the
slice that needs them (PD-11). **None are reopened here.**

## 11.1 Freeze boundary

**As written 2026-08-24 — the "Status" column is superseded; the "Outcome" column is what happened:**

| Scope | Status *(2026-08-24)* | Gate | **Outcome (2026-08-25)** |
|---|---|---|---|
| **S-SPEC** | ✅ **EXECUTED** 2026-08-24 (`d141db1`) | — | Stands. The §9.1 exception it landed **remains in force** |
| **S0** | 🔒 **FROZEN + ACTIVE** — not started | none; ready to run | ✅ **EXECUTED and reported.** [Report](../reports/2026-08-25-encoder-pilot.md); EVA-16 fired |
| **S1 – S3** | 🔒 **FROZEN + ACTIVE** — not started | S0 accepted (PD-3 gate + PD-9 winner criterion) | ⛔ **CANCELLED — never entered.** The gate resolved to *stop*, not to a winner |
| **S4** | **open by design** (PD-11) | delivery mechanism + size cap chosen at S4 from the §S4 option set, using S0 output 8 | ⛔ **CANCELLED — never entered.** CP3 was never reached, so **OP-1** (size cap), **OP-6** (delivery) and **OP-4** (memory ceiling) **remain unset** and were not invented by the closure |
| **S5 – S6** | **not activated** | each needs its own owner approval (PD-2 governance) | **Unchanged — still not activated.** A stopped encoder cannot activate a head (REL-04 unaffected) |

**The frozen boundary is S-SPEC → S3.** Scope inside it is locked; changing it requires a new owner
decision, not an edit. S4's scope is frozen later, when the evidence it depends on exists.

**Next action is S0** — nothing gates it. Its report decides whether S1–S3 ever run.

> *Superseded 2026-08-25.* **S0 ran, and its report ended the plan.** There is no next action: the
> initiative is `stopped_at_s0`. Reviving any part of it is a **new owner decision** needing its own
> plan — and **DAT-04** is explicit that expanding the dataset does not by itself authorise a re-run.

Two items remain **open by design**, not unresolved:

- **Delivery mechanism** — options **a**–**d** in §S4. Recommendation stands at **a** (build-time
  fetch), conditional on confirming how the app reaches its users, which no document records.
- **Size-cap value** — set at S4 from S0 output 8. §1.7's "1–2 GB acceptable" is an install-size
  remark, not a cap, and must not be treated as one.

## 11.2 Planning questions — not owner policy

The owner's handoff explicitly declined to expand these and directed that they be surfaced as
planning questions rather than silently decided. They are **not** blocking scope freeze; they are
inputs to the slice that owns them.

| # | Question | Owned by |
|---|---|---|
| P1 | Exact installer packaging mechanics, once Q1 picks a route | S4 |
| P2 | Exact DirectML capability-probe mechanism | S4 |
| P3 | Exact 500 ms measurement protocol — warm vs cold, percentile, sample count | S0 (must be written into the report) |
| P4 | Peak-RSS ceiling, derived from S0's measurement against PD-10's 8 GB — **not** asserted in advance | S0 → S4 |

**No further questions are raised.** The handoff asked for genuinely unresolved decisions only, and
padding the list would make it less useful, not more.

---

## Lifecycle

**`stopped_at_s0`** — owner ruling, **2026-08-25**. Owner review rounds 1–3 were completed and
**PD-1 … PD-12 remain ratified**; S-SPEC was executed (`d141db1`) and its `ML_Heuristic_design.md`
§9.1 exception **stays in force**. S0 then ran and the **EVA-16 kill criterion fired**, so nothing
past S0 was entered. The specification
[`../specs/2026-08-24-neural-encoder-smart-parser.md`](../specs/2026-08-24-neural-encoder-smart-parser.md)
was ratified the same day and still governs where it and this plan disagree — it is retained as the
contract that was ratified, not as a contract awaiting implementation.

The path as planned, against what actually happened:

| # | Planned step | What happened |
|---|---|---|
| 1 | Owner scope-freezes and activates S-SPEC through S3 | ✅ **Done 2026-08-24** |
| 2 | **S-SPEC** merges (blocks S1) | ✅ **Done — `d141db1`** |
| 3 | **S0 runs**; its report is accepted or rejected | ✅ **Ran. Report ACCEPTED 2026-08-25 — and acceptance meant *stop*** (EVA-16, PD-3). A null result is a complete, valid outcome of this step, not a failure of it |
| 4 | **S1 – S3** proceed on acceptance, shipping as two units (PD-4) | ⛔ **Never entered.** Acceptance did not produce a winner to proceed with |
| 5 | **S4's scope is frozen after S0** against measured numbers (PD-11) | ⛔ **Never entered.** OP-1 / OP-4 / OP-6 remain unset |

**This plan is RETAINED in `docs/plans/`, not archived** — for a stopped initiative the record of
*why* it stopped is the artifact worth keeping beside the report that closed it, on the same basis
the Epic 1 closure-gate record is kept here. Its row has left `../active/README.md`'s **Current**
table for that file's **"Closed from here"** section. The closure is recorded in
[`../CHANGELOG.md`](../CHANGELOG.md) (nothing shipped, and the entry says so), the one finding that
outlived it is tracked in [`../specs/system_roadmap.md`](../specs/system_roadmap.md) §A.4, and the
durable lessons are distilled into
[`../knowledge/ml-experimentation.md`](../knowledge/ml-experimentation.md). S0's results live in
`docs/reports/`, **not** in this file.

---

## Amendment, 2026-08-26 — `collected_v4` is not real data

**Provenance grade: ruling, not measurement.** Owner recall on 2026-08-26 established that
`datasheets/collected_v4.csv` was produced as *owner templates/examples → Meta AI generation → GitHub
Copilot labelling*. No collection record exists in or out of the repository, and no artifact
corroborates the recall — but it agrees with seven independently measured distributional regularities
and an exact quota match. The repository holds **zero verified real user rows**.

Ruling: [`2026-08-26-data-foundation-owner-decision-handoff.md`](2026-08-26-data-foundation-owner-decision-handoff.md) (**DFD-1**) ·
Evidence: [`../reports/2026-08-25-data-audit-gap-map.md`](../reports/2026-08-25-data-audit-gap-map.md) §E.5–E.6,
[`../reports/2026-08-26-data-foundation-owner-decision-brief.md`](../reports/2026-08-26-data-foundation-owner-decision-brief.md) §2 ·
Pass record: [`../reports/2026-08-26-data-foundation-correction-pass.md`](../reports/2026-08-26-data-foundation-correction-pass.md)

**Every description of `collected_v4` in this document as *real*, *collected* or *user-authored* is
withdrawn.** The load-bearing occurrences are marked in place above. The remainder are deliberately
**not** individually edited: rewriting them would erase what was believed when this document was
written, which is precisely what the amendment convention exists to preserve. Read the whole document
through this amendment.

### The premise this proposal was built on

The S0 design rests on *"test on the 205 held-out **real** rows"* — the proposal's own framing of the
gate question was *"does the encoder show enough evidence of value on the **real** data?"*. That
premise was false when written. S0 could not have answered the question as posed, because the project
had no real data to answer it with.

**This changes nothing about the outcome.** S0 ran, both arms lost to the n-gram baseline on the same
split, EVA-16 fired and the initiative stopped. A false premise that made the gate *easier to fail*
does not rescue an arm that failed it. The proposal stays `stopped_at_s0`, and **DAT-04 stands**:
dataset growth alone does not authorise re-running the experiment.

**What it does change** is what a revival would have to establish. Reviving this initiative now
requires real evaluation data (**Gold-R**, DFD-3/DFD-4) that does not yet exist — a strictly higher
bar than the one this proposal set, and a new owner decision on top.
