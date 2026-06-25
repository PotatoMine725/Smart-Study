# M8-A TextClassifier — v4 Seed Per-Class Recall Eval

**Date:** 2026-06-25
**Agent:** Claude (Opus 4.8) via Claude Code
**Plan verified:** [`docs/plans/2026-06-16-m8a-textclassifier-retrain.md`](../plans/2026-06-16-m8a-textclassifier-retrain.md) §7.3

## Scope

Close §7.3 of the retrain plan: *"split the new seed (80/20, stratified), fit the
pipeline, report MicroAccuracy + per-class recall, and confirm minority recall did
not regress."* The `collected_v4` merge (commit `ab5112c`, seed **698 → 903** rows,
imbalance 2.21× → 1.11×) was already shipped and the model auto-retrained on 903
rows. This report supplies the missing analytical confirmation.

Since the pre-v4 (698-row) seed model was never evaluated this way, "did not regress"
needs a baseline — so **both** seeds were evaluated under identical methodology
(before/after).

## Method

A throwaway harness, [`tools/TextClassifierEval/`](../../tools/TextClassifierEval/)
(net10.0, `Microsoft.ML` 3.0.1, **not** in `SmartStudyPlanner.slnx`), mirrors the
production pipeline in `SmartStudyPlanner/Services/ML/TextClassifierModelManager.cs`
exactly: `MLContext(seed: 42)` → `MapValueToKey(Label←TaskType)` →
`FeaturizeText(Features←InputText)` → `SdcaMaximumEntropy` → `MapKeyToValue`.

- **Split:** stratified 80/20 per class, deterministic (`Random(42)`).
- **Metrics:** manual per-class tally by exact class name (TP/FN/FP) → recall,
  precision, support; MicroAccuracy = overall correct/total; MacroAccuracy = mean
  per-class recall.
- The pre-v4 seed was extracted from git: `git show ab5112c^:…/seed_intents.csv`.

Run:
```
dotnet run --project tools/TextClassifierEval -- <v698.csv> SmartStudyPlanner/Services/ML/TextClassifier/seed_intents.csv
```

## Results — before (v698) vs after (v903)

Per-class **recall** (with held-out test support), the metric §7.3 targets:

| Class | v698 recall (support) | v903 recall (support) | Δ recall |
|---|---|---|---|
| **ThiGiuaKy** (minority) | 94.1% (17) | 94.6% (37) | **+0.5pp**, 2.2× more test support |
| **BaiTapVeNha** (minority) | 96.0% (25) | 94.4% (36) | −1.6pp (= 1 row), more support |
| **DoAnCuoiKy** (thickened) | 96.2% (26) | 97.2% (36) | **+1.0pp** |
| KiemTraThuongXuyen (majority) | 100.0% (38) | 100.0% (38) | 0 |
| ThiCuoiKy (majority) | 100.0% (34) | 100.0% (34) | 0 |

Precision held at/near 95–100% across the board (v903: BaiTapVeNha 94.4%,
DoAnCuoiKy 97.2%, KiemTra 95.0%, ThiCuoiKy 100%, ThiGiuaKy 100%).

| Aggregate | v698 | v903 |
|---|---|---|
| MicroAccuracy | 97.86% | 97.24% |
| MacroAccuracy (mean recall) | 97.25% | 97.25% |

## Verdict

**Minority recall did not regress.** MacroAccuracy is identical (97.25%); the two
minority classes the v4 merge targeted are flat within noise (ThiGiuaKy +0.5pp;
BaiTapVeNha −1.6pp ≈ a single misclassified row). The substantive win is **test
support**: the held-out minority sets grew (ThiGiuaKy 17→37, BaiTapVeNha 25→36,
DoAnCuoiKy 26→36), so the recall estimate for exactly the thin, test-guarded classes
is now far more reliable — which was the stated purpose of v4. §7.3 closed.

## Limitations (read before trusting absolute numbers)

- **The ~97% absolute accuracy is optimistic, not a clean generalization figure.**
  The split is random *within* the seed, and the seed contains templated/near-duplicate
  phrasings (same construction, different subject/time word). Exact-duplicate
  `InputText` is deduped, but near-duplicates can still straddle train/test, leaking
  signal to a bag-of-words featurizer. The plan itself flagged this collapse
  (§8.1: `synthetic_v3` added 101 templated rows, 0 survived dedup). Treat absolutes
  as an *in-distribution upper bound*.
- **The before/after Δ is still valid** — both seeds were measured with the identical
  pipeline, split seed, and methodology, so the leakage bias is common-mode and the
  comparison (the actual §7.3 question) holds.
- This eval is diagnostic only; it does **not** alter the shipped model, which trains
  on all 903 rows (no split). Per `ML_Heuristic_design.md` §5.1 the TextClassifier is
  the sole ML component; Difficulty/weights remain heuristic (§3.1/§3.5).

## Verification

- `dotnet build tools/TextClassifierEval` → 0 errors (standalone).
- `rtk dotnet build SmartStudyPlanner.slnx` + `rtk dotnet test --no-build` →
  unchanged at 0 errors / 244 pass (tool is outside the solution).
- Temp `_seed_v698.csv` was deleted after the run (not committed).
