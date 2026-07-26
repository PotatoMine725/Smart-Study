# Smart-Add Vietnamese Negation Fix (P1) — Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL — use `superpowers:test-driven-development`
> (RED-first) + `superpowers:verification-before-completion`. Steps use checkbox
> (`- [ ]`) syntax for tracking.
>
> **APPROVED (2026-07-26).** The owner approved this plan and locked **D-9 (NFC normalize)** in.
> Execution of Slice N1 is authorized under the write scope + acceptance gates below.

**Status:** `approved — 2026-07-26 (owner)`
**Revision:** `r2 (2026-07-26)` — architecture-review refinements folded in per the owner's
architect brief (`Prompt/P1-fix-plan-refine.md`). Milestone scope, owner decisions (D-2/D-3),
acceptance gates 1–5, the ~0.5-day estimate, the TDD workflow, and the write scope are all
unchanged; the code delta stays behavior-identical for real (NFC) input. See
**Architecture-review refinements (2026-07-26)** near the end.

**Goal:** Close the **P1 Vietnamese-negation defect** in the smart-add difficulty heuristic so
negated difficulty phrases (`"không dễ"`, `"chẳng khó"`, `"khong de"`) no longer produce the
wrong difficulty. As a pinned side-effect of the required matching change, remove the live
substring false-positive in the same rule set (`"de" ∈ "deadline"` → easy). **No UI/UX change**
of any kind — the fix is invisible to the UI; it only makes the pre-filled difficulty value
correct.

**Architecture:** Single-class change in `DefaultDifficultyKeywordParser` — replace the bare
substring match (difficulty rules only) with **word-boundary token matching** plus a
**preceding-window negation scan**. When a difficulty keyword is negated, **suppress it and
fall back to the task-type prior** (owner decision, 2026-07-24). `ContainsAnyRule<T>` and the
task-type parser (`DefaultTaskTypeKeywordParser`) are **untouched**. Internally the parser is
split at two named seams — a **semantic seam** (`DetectPole`: tokens → asserted pole or `null`)
and a **policy seam** (`Parse = DetectPole(...) ?? defaultValue`, where the *caller* owns
"prior") — so future rule growth and a future ML score both have clean insertion points without
a rewrite (D-8; see **Architecture-review refinements**, 2026-07-26).

**Tech Stack:** .NET 10 WPF (`net10.0-windows10.0.19041.0`), xUnit. No new dependencies, no
schema change.

---

## Context

- **Origin:** QA investigation §2.2 (`docs/reports/2026-07-19-epic1-phase2-qa-investigation.md`),
  from the owner's supervised B1.4 GUI test (2026-07-15). Classified **P1, pre-existing design
  gap** (Slice-6 parser era) — **not a regression**. It was explicitly **deferred** out of the
  Epic-1 reopen fix (`docs/plans/2026-07-19-epic1-reopen-fix-plan.md`, Deferred table row
  *"Vietnamese negation not understood by quick-input parser → candidate for a future parsing
  milestone"*). **This is that milestone.**
- **Causal chain (evidence-backed):**
  1. Smart-add ("Tự điền") → `QuanLyTaskViewModel.PhanTichNhanhCommand` →
     `_parsingOrchestrator.Parse(VanBanNhapNhanh)` (`ViewModels/QuanLyTaskViewModel.cs:263`).
  2. `ParsingOrchestrator.Parse` does `lowerInput = input.ToLower()` — **lowercase only, no
     diacritic stripping** (`Core/Parsing/Orchestrators/ParsingOrchestrator.cs:39`).
  3. `ExtractDifficulty(lowerInput, prior)` → `DefaultDifficultyKeywordParser.Parse`.
  4. Rules are bare substring matches: `ContainsAnyRule<int>(5,"khó","kho","căng","chết")` then
     `ContainsAnyRule<int>(1,"dễ","de","chill","nhàn","ez")`
     (`Services/Strategies/IDifficultyKeywordParser.cs:15-16`); matcher = `lowerInput.Contains(k)`
     (`Services/Strategies/IKeywordRule.cs:32`). **No negation token exists anywhere in the
     parsing layer.**
  5. `"btvn ngày mai không dễ đâu"` contains `"dễ"` → rule fires → difficulty **1 (easy)**,
     exactly as the owner reported. `"không khó"` symmetrically returns 5 (hard) — negation
     misreads **both** poles.
- **Why the ML layer can't rescue it:** `TextClassifierService.Predict` hard-codes
  `DoKho = null` (`Services/ML/TextClassifierService.cs:35`), so in the orchestrator
  `doKho = prediction?.DoKho ?? doKhoHeuristic` **always** resolves to the heuristic. The keyword
  heuristic is the **sole** difficulty source — see **RR-2**.
- **Single live path confirmed:** `ParseInputStage` is an MVP stub that only `Trim()`s
  (`Services/Pipeline/Stages/ParseInputStage.cs:38`); `AdaptStage`'s `Normalize` is
  `MonHocIdentity.Normalize` for subject-name dedup, unrelated to difficulty. There is **no
  second parsing site and no diacritic-stripping stage** ahead of the parser — so the negator
  set must keep diacritic forms (`"không"`) alongside diacritic-less forms (`"khong"`, `"ko"`).

## Normalization policy (explicit)

The difficulty parser assumes exactly this input contract. Documenting it — the brief's
normalization ask — keeps future parser work from silently breaking an assumption that is
currently only implicit:

| Concern | Policy | Where |
|---|---|---|
| Case | `ToLower()` (culture-default), applied by `ParsingOrchestrator` before the parser sees the text | orchestrator (unchanged) |
| Diacritics | **Not** stripped — the lexicon carries both accented (`"khó"`) and unaccented (`"kho"`) forms | lexicon (D-4/D-7) |
| Unicode composition | Composed to **NFC** inside `Tokenize` (`Normalize(FormC)`) — idempotent for already-NFC text — so combining-mark input still matches the NFC lexicon literals | `Tokenize` (D-9) |
| Token boundary | Unicode letter runs `\p{L}+`; whitespace / punctuation / digits separate | `WordSplit` (D-7) |
| Comparison | `StringComparer.Ordinal` — byte-exact; lexicon literals **must be saved NFC** | `_negators` / `Array.IndexOf` |

`Tokenize` is the **single seam** for any future normalization change (e.g. an accent-fold
pass — F5).

## Scope

### Mandatory (this milestone)

| # | Item | Class |
|---|---|---|
| 1 | Negation-aware, **word-boundary** difficulty matching in `DefaultDifficultyKeywordParser`; negated keyword → **suppress → task-type prior** | P1 fix (finding 2.2) |
| 2 | Tests, RED-first: negation both poles (diacritic + diacritic-less + compound), the `"deadline"` false-positive characterization, and a full-suite regression pass | P1 verification |

### Deferred / NOT in this milestone (tracked, not forgotten) — see Residual Risk

| Item | Why deferred | Exit venue |
|---|---|---|
| ML difficulty model so `DoKho` is no longer always `null` (**RR-2**) | Whole new model + training data; out of proportion to a heuristic fix | Roadmap / future ML slice |
| Task-type parser (`DefaultTaskTypeKeywordParser`) still substring-based (**RR-4**) | Lower risk (longer keywords); scope discipline | Roadmap tech-debt |
| Richer negation NLP — double negation, scope, sarcasm (**RR-3**) | Diminishing returns vs. a coarse, user-verified heuristic | Accepted residual |
| Sync-over-async (`SchedulingOrchestrator.cs:78`, inv. 2.8-2); `async void OnStartup` (2.8-3) | Unrelated to smart-add | Roadmap tech-debt (already listed in reopen plan) |

## Residual risk & tech debt (the "mentioned earlier" items)

- **RR-1 — substring false-positives in the difficulty rules (FIXED here, as a pinned
  consequence).** `.Contains()` matches keywords *inside* larger words. Two concrete live cases:
  `"báo cáo deadline thứ 6"` → `"de" ∈ "deadline"` → **1 (easy)**; and inconsistency by
  diacritics — `"khong de"` (no diacritics) accidentally matches `"kho" ∈ "khong"` → **5 (hard)**
  while `"không dễ"` → **1 (easy)**. Word-boundary matching (D-3) removes this whole class for
  the difficulty parser. Pinned by a characterization test so it can't silently regress.
- **RR-2 — the difficulty heuristic is the *sole* difficulty source (documented, NOT fixed).**
  `TextClassifierService.cs:35` hard-codes `DoKho = null`; `IntentPrediction.DoKho` is therefore
  always null and the ML layer never contributes difficulty. Consequence: there is **no fallback**
  behind this heuristic — its correctness is load-bearing, which is exactly why this fix matters.
  A dedicated difficulty model is deferred (roadmap), **not** built here.
- **RR-3 — negation stays a bounded heuristic (accepted).** Window-based detection handles the
  common `"không/chẳng + [hề] + khó/dễ"` shape; it will **not** resolve double negation
  (`"không phải là không khó"`), long-range scope, or sarcasm. Acceptable: the value is a
  *suggestion* the user verifies in the pre-fill form ("Lưu Deadline (Hãy kiểm tra lại)").
- **RR-4 — task-type parser unchanged (tracked).** `DefaultTaskTypeKeywordParser` still uses
  substring `ContainsAnyRule`; keeping it out of scope avoids blast radius. Its keywords are
  longer/lower-risk; logged as tech-debt, not fixed here.
- **RR-5 — Unicode composition mismatch (mitigated by D-9; residual named).** With
  `StringComparer.Ordinal` + `Array.IndexOf`, an accented keyword matches only when input and
  lexicon literal share the same Unicode form. If input arrived **NFD-decomposed** (base letter +
  combining mark), *every accented keyword would silently stop matching* — a **total**-failure
  mode, not a partial one, and one the RED tests can hide if the test literals happen to be
  decomposed identically. Mitigation: `Tokenize` composes to **NFC** (D-9), and the lexicon
  literals ship NFC (verified in N1-B, Step 4). Residual: exotic non-NFC forms outside FormC's
  remit — accepted, bounded by the same user-verification safety net as RR-3.

## Locked decisions

| ID | Decision | Why |
|---|---|---|
| D-1 | Fix lives **only** in `DefaultDifficultyKeywordParser`. `ContainsAnyRule<T>` and `DefaultTaskTypeKeywordParser` are untouched | Minimal blast radius; the parser is unit-tested in isolation (`DifficultyKeywordParserTests`, `SmartParserStrategiesTests`) |
| D-2 | Negation semantics = **suppress → task-type prior** (owner, 2026-07-24) | Never overshoots; composes correctly with compound negation (`"không khó không dễ"` → prior/medium, not an extreme). Chosen over invert-to-pole |
| D-3 | **Word-boundary (whole-token) matching** for difficulty keywords (owner, 2026-07-24) | Required to locate the keyword's *position* for the negation scan; simultaneously fixes RR-1 |
| D-4 | Negator set + window are **tunable `private static` constants**. Set = `{ "không","khong","ko","chẳng","chang","chả","đéo","đếch","deo","dech" }`; **window = 2 preceding tokens** (covers `"không hề dễ"` / `"chẳng hề khó"`) | Curated to bind tightly to the following predicate. Tradeoff logged: `"ko"`/`"chả"` as standalone tokens carry mild false-positive risk, bounded by the 2-token window; `"chưa"` (temporal "not yet") deliberately excluded |
| D-5 | **No signature change** to `IDifficultyKeywordParser.Parse(string, int)` and no change to `PriorForTaskType` | Callers (`TaskExtractionEngine`, `ParsingOrchestrator`) stay byte-identical; only the internal matching changes |
| D-6 | **RED-first TDD**; both new failing tests must be seen failing for the *right* reason before the fix | House convention; the bug is precisely a missing-coverage gap |
| D-7 | Tokenizer = `Regex` over `\p{L}+` (Unicode letter runs) — drops whitespace/punctuation/digits | `\p{L}` matches Vietnamese accented letters; keeps `"khó,"`/`"khó!"` matching |
| D-8 | **Two internal seams:** `DetectPole(tokens) → int?` (semantic — "what difficulty did the text assert?", or `null`) and `Parse = DetectPole(...) ?? defaultValue` (policy — the caller owns "prior"). Same behavior, same size | Separates lexical/semantic interpretation from difficulty policy (brief's SoC ask) and gives future rule growth *and* a future ML score distinct, obvious insertion points (brief's parser-evolution ask) — at zero added cost; the seam was already latent in "continue → fall to `defaultValue`" |
| D-9 | `Tokenize` composes input to **NFC** (`Normalize(NormalizationForm.FormC)`); lexicon literals ship NFC | Enforces the normalization policy instead of merely assuming it; idempotent for real (NFC) input so **no test outcome changes**; closes RR-5's silent total-failure mode in one line. **Owner-accepted 2026-07-26 — kept** (RR-5 stays *mitigated*) |

## Parallel-dispatch decision

**No parallel dispatch.** One production file + its two test files; ~0.5 day; a single
sequential agent avoids merge overhead. Flow: **Agent N1 → PM/QA review → merge `ui_rf`**.

### Agent N1 — negation fix

- **Mission:** Execute Slice N1 exactly as specified (Tasks N1-A, N1-B), RED-first.
- **Venue:** Worktree branch `fix/smart-add-negation` off `ui_rf` (via
  `superpowers:using-git-worktrees` at execution time).
- **Write scope (nothing else):**
  - `SmartStudyPlanner/Services/Strategies/IDifficultyKeywordParser.cs`
  - `SmartStudyPlanner.Tests/Services/Strategies/DifficultyKeywordParserTests.cs`
  - `SmartStudyPlanner.Tests/Services/Strategies/SmartParserStrategiesTests.cs` *(only if adding
    negation `[InlineData]` rows to the existing `DefaultDifficultyKeywordParserTests` there)*
- **Skills:** `superpowers:test-driven-development`, `superpowers:verification-before-completion`.
- **Key tools:** `gitnexus_impact` before the edit (see Pre-edit checklist), `rtk dotnet build` /
  `rtk dotnet test`.
- **Deliverables:** 2 commits (tests RED, then fix GREEN), full suite green.
- **Stop condition:** any test failing in a way **not** predicted by the RED steps, or any needed
  edit outside Write scope → stop and report; do not improvise.

### PM/QA (this session's role)

Review the diff for scope adherence + test faithfulness (no keyword logic leaking into
`ContainsAnyRule` or the task-type parser), run `gitnexus_detect_changes()` before merge, merge
`fix/smart-add-negation` → `ui_rf`, append the CHANGELOG row.

## Pre-edit checklist (Agent N1)

- [ ] `gitnexus_impact({target: "Parse", direction: "upstream"})` scoped to
  `DefaultDifficultyKeywordParser` (and/or `ExtractDifficulty`) — report the blast radius per
  CLAUDE.md; expected callers = `TaskExtractionEngine.ExtractDifficulty` → `ParsingOrchestrator`.
  **Warn the owner if it returns HIGH/CRITICAL** before proceeding.
- [ ] `gitnexus_detect_changes()` before each commit; affected symbols must match Write scope.
- [ ] All shell commands `rtk`-prefixed. Commit messages carry **no** Co-Authored-By trailer.
- [ ] Vietnamese text written **only** via native Write/Edit tools (PowerShell
  `Get-Content`/`Set-Content` corrupts BOM-less UTF-8).
- [ ] No schema change → migration/DoR checks n/a.

---

## Slice N1 — negation-aware difficulty parsing

### Task N1-A: RED — failing tests first

**File:** `SmartStudyPlanner.Tests/Services/Strategies/DifficultyKeywordParserTests.cs`
(append a `[Theory]` to the existing `DifficultyKeywordParserTests`).

- [ ] **Step 1: Add the failing tests.** They call the parser in isolation exactly as the
  existing tests do (`_parser.Parse(input.ToLower(), prior)`).

```csharp
// ── P1 fix (2026-07-24): negation must NOT flip difficulty, and keywords must match on
//    word boundaries (no "de" ∈ "deadline"). Owner decision: negated → suppress → prior.
//    This [Theory] is the negation *characterization corpus*: future rule work (new negators,
//    intensifiers/diminishers, phrase rules — F1/F2) appends rows here rather than adding
//    ad-hoc facts, so one table stays the regression surface. NOTE: keep the accented literals
//    below in NFC (see Normalization policy / RR-5).
[Theory]
// prior = 2 (BaiTapVeNha) unless noted
[InlineData("btvn ngày mai không dễ đâu", 2)] // was 1 (easy) — the reported bug
[InlineData("bài này không khó", 2)]          // was 5 (hard) — the other pole
[InlineData("khong de", 2)]                    // no diacritics — was 5 via "kho" ∈ "khong"
[InlineData("chẳng dễ tí nào", 2)]             // alt negator
[InlineData("không hề khó", 2)]                // negator + intensifier (window = 2)
[InlineData("không khó không dễ", 2)]          // compound: neither → prior, no overshoot
[InlineData("báo cáo deadline thứ 6", 2)]      // RR-1: "de" ∈ "deadline" must NOT fire
public void Parse_NegatedOrSubstring_FallsBackToPrior(string input, int prior)
{
    Assert.Equal(prior, _parser.Parse(input.ToLower(), prior));
}

// Guard: a *non-negated* keyword still wins (behavior preserved).
[Theory]
[InlineData("bài tập khó", 5)]
[InlineData("bài dễ thôi", 1)]
public void Parse_PlainKeyword_StillOverridesPrior(string input, int expected)
{
    Assert.Equal(expected, _parser.Parse(input.ToLower(), 2));
}
```

- [ ] **Step 2: Run, verify RED for the right reason.**
  `rtk dotnet test SmartStudyPlanner.slnx --filter "FullyQualifiedName~DifficultyKeywordParserTests"`
  Expected: `Parse_NegatedOrSubstring_FallsBackToPrior` **fails** — the negated `"dễ"`/`"khó"`
  rows return 1/5 instead of the prior, and the `"deadline"` row returns 1. The guard theory
  passes. If it fails any other way, **stop and report**.

### Task N1-B: GREEN — implement negation-aware, word-boundary matching

**File:** `SmartStudyPlanner/Services/Strategies/IDifficultyKeywordParser.cs`
(replace the body of `DefaultDifficultyKeywordParser`; keep the interface and
`PriorForTaskType` unchanged).

- [ ] **Step 3: Implement.** Reference implementation (executor may refine names/comments to
  match house style, but must preserve the pole order, the suppress-→-prior semantics, and the
  2-token window):

```csharp
using System;
using System.Collections.Generic;
using System.Text;                    // NormalizationForm (D-9)
using System.Text.RegularExpressions;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface IDifficultyKeywordParser
    {
        int Parse(string lowerInput, int defaultValue);
    }

    public class DefaultDifficultyKeywordParser : IDifficultyKeywordParser
    {
        // ─────────────────────────────────────────────────────────────────────────────────
        //  LEXICON — the single edit point. To support a new negator/keyword, edit HERE and
        //  add a regression row to DifficultyKeywordParserTests' negation corpus. Keep every
        //  literal in Unicode NFC (see "Normalization policy") — the match is byte-exact ordinal.
        // ─────────────────────────────────────────────────────────────────────────────────

        // Ordered by priority: the "hard" pole is evaluated before the "easy" pole
        // (unchanged from the legacy rule order — "khó mà dễ" stays hard).
        private static readonly (int Value, string[] Keywords)[] _poles =
        {
            (5, new[] { "khó", "kho", "căng", "chết" }),
            (1, new[] { "dễ", "de", "chill", "nhàn", "ez" }),
        };

        // Vietnamese negators. Both diacritic and diacritic-less forms are needed because the
        // orchestrator only ToLower()s the input — it does NOT strip diacritics. Tunable set.
        private static readonly HashSet<string> _negators = new(StringComparer.Ordinal)
        {
            "không", "khong", "ko", "chẳng", "chang", "chả", "đéo", "đếch", "deo", "dech",
        };

        // Tokens before a difficulty keyword to scan for a negator. 2 covers the
        // negator+intensifier shape "không hề dễ" / "chẳng hề khó". Tunable.
        private const int NegationWindow = 2;

        // Unicode letter runs → whitespace/punctuation/digits are separators. \p{L} matches
        // Vietnamese accented letters, so keywords match on word boundaries (fixes "de" ∈
        // "deadline" and "kho" ∈ "khong").
        private static readonly Regex WordSplit = new(@"\p{L}+", RegexOptions.Compiled);

        /// <summary>
        /// Prior difficulty by task type, derived from observed label distribution.
        /// Replaces hard-coded default-3 when no keyword is matched.
        /// </summary>
        public static int PriorForTaskType(LoaiCongViec taskType) => taskType switch
        {
            LoaiCongViec.DoAnCuoiKy         => 4,
            LoaiCongViec.ThiCuoiKy          => 4,
            LoaiCongViec.ThiGiuaKy          => 3,
            LoaiCongViec.KiemTraThuongXuyen => 3,
            LoaiCongViec.BaiTapVeNha        => 2,
            _                               => 3,
        };

        // POLICY SEAM (D-8): an un-negated difficulty keyword wins; otherwise fall back to the
        // caller-supplied prior. The parser never names "prior" — the caller (ParsingOrchestrator)
        // owns that policy via PriorForTaskType. This '?? defaultValue' is the rule-side fallback;
        // the ML-side composition point is the orchestrator's own '?? doKhoHeuristic' (RR-2, F3).
        public int Parse(string lowerInput, int defaultValue)
            => DetectPole(Tokenize(lowerInput)) ?? defaultValue;

        // SEMANTIC SEAM (D-8): the difficulty pole the text asserts (5 or 1), or null when it
        // makes no un-negated difficulty claim. Natural insertion point for future rule growth
        // (intensifiers/diminishers, more negators, phrase rules — F1/F2).
        private static int? DetectPole(List<string> tokens)
        {
            foreach (var (value, keywords) in _poles)
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (Array.IndexOf(keywords, tokens[i]) < 0)
                        continue;

                    // Keyword matched on a word boundary. If a negator sits within NegationWindow
                    // tokens immediately before it, the phrase is negated — suppress this pole
                    // (owner decision 2026-07-24: fall back rather than invert) and keep scanning.
                    if (IsNegated(tokens, i))
                        continue;

                    return value;
                }
            }

            return null;
        }

        private static List<string> Tokenize(string lowerInput)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(lowerInput)) return tokens;

            // NORMALIZATION SEAM (D-9): compose to NFC so combining-mark input matches the NFC
            // lexicon literals under ordinal comparison. Idempotent for already-NFC text, so no
            // real-input behavior changes; this is the one place any future normalization lives.
            string normalized = lowerInput.Normalize(NormalizationForm.FormC);
            foreach (Match m in WordSplit.Matches(normalized))
                tokens.Add(m.Value);
            return tokens;
        }

        private static bool IsNegated(List<string> tokens, int keywordIndex)
        {
            int from = Math.Max(0, keywordIndex - NegationWindow);
            for (int j = from; j < keywordIndex; j++)
                if (_negators.Contains(tokens[j]))
                    return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Verify GREEN + no collateral.**
  `rtk dotnet build SmartStudyPlanner.slnx` then `rtk dotnet test SmartStudyPlanner.slnx --no-build`
  Expected: the new theories PASS; **every pre-existing test stays green** (see "Why existing
  tests stay green" below). Watch specifically: `SmartParserStrategiesTests` (`"kho vler"`→5,
  `"ez game"`→1, `"khó mà dễ"`→5), `ParsingOrchestratorTests` (`"Ôn thi cuối kỳ cực dễ"`→1),
  `Slice6ParserIntegrationTests.ModelNotLoaded_FallsBackToHeuristic_ByteEqual`.
  Also confirm (implementation-correctness check, **not** a new acceptance gate) that the
  accented lexicon literals in the reference impl are stored **NFC** — e.g.
  `python -c "import unicodedata,io; s=io.open('SmartStudyPlanner/Services/Strategies/IDifficultyKeywordParser.cs',encoding='utf-8').read(); print(s==unicodedata.normalize('NFC',s))"`
  should print `True`. NFD-saved literals would break every accented match under ordinal
  comparison (RR-5), a failure the RED corpus can mask if its literals are decomposed the same way.

- [ ] **Step 5: Commit (two commits — RED test, then GREEN fix, per D-6).**

```bash
rtk git add SmartStudyPlanner.Tests/Services/Strategies/DifficultyKeywordParserTests.cs
rtk git commit -m "test(parser): RED — negation and word-boundary difficulty cases (P1, finding 2.2)"

rtk git add SmartStudyPlanner/Services/Strategies/IDifficultyKeywordParser.cs
rtk git commit -m "fix(parser): negation-aware, word-boundary difficulty matching — negated keyword falls back to prior (P1)"
```

---

## Why existing tests stay green (evidence, not assumption)

Token matching is a **strict subset** of substring matching for whole-word keywords:

- Every green test that asserts a **pole value (1/5)** uses the keyword as a **whole word**
  (`"bài tập khó"`, `"kho vler"`, `"ez game"`, `"đồ án căng"`, `"Ôn thi cuối kỳ cực dễ"`, …) →
  token matching still catches it.
- Every green test that asserts the **prior / default** has **no** difficulty keyword present →
  token matching (a subset of substring) also finds none → same result.
- The two `ParsingOrchestratorTests` that assert `DoKho = 5` via a stub classifier bypass the
  heuristic entirely (`prediction.DoKho` wins) → unaffected.
- `Slice6…ByteEqual` compares two orchestrators that both use the same parser → still equal.

The only way a test could break is if a **green** test asserted a pole value via a keyword
**inside a larger word** — traced across `SmartParserStrategiesTests`,
`DifficultyKeywordParserTests`, and both `ParsingOrchestratorTests`; **none do**.

## Verification / acceptance gates

1. Both RED assertions failed exactly as predicted **before** the fix (recorded in agent output).
2. New negation + false-positive theories PASS after the fix.
3. **Full suite green** at the final commit (pre-existing count + new theories); no unexpected
   failures.
4. `gitnexus_detect_changes()` shows only `DefaultDifficultyKeywordParser` (+ test files) changed
   — no drift into `ContainsAnyRule`, the task-type parser, the orchestrator, or any UI file.
5. Manual smart-add spot-check by the owner (optional, ~2 min): quick-add `"btvn không dễ"` →
   pre-filled difficulty is **not** 1; `"báo cáo deadline thứ 6"` → not 1.

## Future architecture notes (NOT this milestone)

Documented so the seams above get *used*, not rediscovered. None are in scope now; each is a
distinct future item, not a hidden expansion of this fix.

- **F1 — Intensifiers / diminishers.** `"rất khó"` (very hard), `"hơi khó"` (a bit) attach at the
  same preceding-window scan as negation, inside `DetectPole`. A future modifier could nudge the
  pole or map to a finer score; today's coarse 1/5 model does not carry them.
- **F2 — More negators / phrase rules.** Grow `_negators`, or add a small phrase-rule list, at
  the lexicon seam; the negation corpus (N1-A) catches regressions.
- **F3 — ML difficulty transition — two *distinct* seams, do not conflate.**
  (a) *Composition, present today:* `ParsingOrchestrator`'s
  `doKho = prediction?.DoKho ?? doKhoHeuristic` — the moment `TextClassifierService` stops
  hard-coding `DoKho = null` (RR-2), a model score composes in **here** with the heuristic as
  fallback, no parser change. (b) *Rule evolution:* `DetectPole` (tokens → `int?`) is where
  rule-based interpretation grows; a rules-vs-model swap, if ever wanted, hides behind
  `IDifficultyKeywordParser` without touching callers (D-5).
- **F4 — Lexicon externalization.** Only if the lexicon outgrows a hand-edited literal or needs
  non-developer editing (resource file / embedded JSON). **Rejected now:** cost > benefit at this
  size; a `private static readonly` list is the right tool today.
- **F5 — Accent-fold normalization.** If maintaining paired accented/unaccented lexicon entries
  ever becomes a cost, a fold pass in `Tokenize` (the one normalization seam, D-9) could let a
  single accented keyword match both forms. Deferred: it changes matching semantics and needs its
  own tests.

## Out of scope

Any UI/UX change (hint text, pre-fill display, colours); an ML difficulty model (RR-2, F3); the
task-type parser's substring matching (RR-4); `IDifficultyKeywordParser.Parse` signature or
`PriorForTaskType` changes; inversion/moderated negation semantics (owner chose suppress→prior);
intensifier/diminisher or phrase-rule grammar (F1/F2); lexicon externalization / config system
(F4); accent-fold normalization (F5); the unrelated 2.8-2 / 2.8-3 tech-debt.

## Effort

| Who | What | Estimate |
|---|---|---|
| Agent N1 | Slice N1 (tests + fix) | ~0.5 day |
| PM/QA | Review + `detect_changes` + merge + CHANGELOG | ~0.25 day |
| Owner | Optional smart-add spot-check | ~5 min |

---

## Architecture-review refinements (2026-07-26)

Folded in per the owner's architect brief (`Prompt/P1-fix-plan-refine.md`). **Every hard
constraint held:** milestone scope, owner decisions (D-2/D-3), acceptance gates 1–5, the
~0.5-day estimate, the TDD workflow, and the write scope are unchanged; the code delta is
behavior-identical for real (NFC) input.

**Kept as-is (already strong — do not change):**
- **Business-policy isolation** *(the brief's central question)* — the parser expresses policy as
  "suppress → `defaultValue`" and **never names "prior"**; the caller owns it via
  `PriorForTaskType`. Difficulty policy is already decoupled from lexical matching. Nothing to do.
- Single-class blast radius (D-1); no-signature-change seam (D-5); `\p{L}+` tokenizer (D-7);
  hardcoded `private static readonly` lexicon; RED-first TDD (D-6).

**Plan-impact classification:**

| Refinement | Classification | Changes behavior? |
|---|---|---|
| `DetectPole(tokens) → int?` semantic/policy seam split (D-8) | Small implementation refinement | No |
| `Tokenize` NFC normalize (D-9) + NFC-literal check (N1-B Step 4) | Small implementation refinement | No for NFC input; closes RR-5's NFD total-failure |
| Lexicon "single edit point" banner (ref impl) | Documentation / naming | No |
| Negation **characterization corpus** framing (N1-A) | Small test refinement (organization only — *not* a gate) | No |
| **Normalization policy** subsection + RR-5 | Documentation only | No |
| F1–F5 future notes | Future roadmap | No |
| Config / lexicon externalization *now*; NLP framework; separate golden-dataset file; phrase-grammar engine | **Reject** — complexity > value at 0.5 day | — |

**Owner decision (2026-07-26):** D-9 (NFC normalize) **kept** — RR-5 stays *mitigated*. The plan
is approved as written; Slice N1 is authorized.

## Decisions made

- **A semantic/policy seam split (`DetectPole → int?`, `Parse = … ?? defaultValue`) was adopted
  over the inline loop.** *Why:* the brief asked whether tokenization, semantic interpretation,
  and difficulty policy could be separated without a rewrite — returning a nullable pole does
  exactly that at identical size and behavior. *What for:* future negators / intensifiers /
  phrase rules and a future ML score each get an obvious, distinct insertion point. *Experience:*
  the cheapest refactor here cost nothing — the seam was already latent in "continue → fall to
  `defaultValue`"; naming it *was* the change.
- **Normalization was promoted from an assumption to an enforced, documented policy.** *Why:* an
  undocumented "NFC-assumed, ordinal-match" contract is a silent total-failure waiting for one
  NFD-decomposed input, and this repo has already been bitten by Vietnamese-encoding issues.
  *What for:* one `Normalize(FormC)` line in the single tokenize seam + an NFC-literal check
  closes the hole with zero real-input behavior change. *Experience:* writing the normalization
  table forced the ordinal-vs-composition interaction into the open, where it was cheap to fix.
- **Negation resolves by suppression to the task-type prior, not inversion.** *Why:* the owner
  chose it over invert-to-pole after seeing the discriminator — invert overshoots on compound
  negation (`"không khó không dễ"` should read medium, which suppression gives and inversion
  turns into "easy"). *What for:* predictable, never-worse-than-prior behavior on a value the
  user verifies anyway. *Experience:* framing the choice with three concrete phrase→value rows
  made a fuzzy semantic call decidable in one pass.
- **Word-boundary matching was adopted, folding RR-1 into the fix.** *Why:* detecting negation
  needs the keyword's token position, which substring `.Contains` cannot give; whole-token
  matching provides it and, as a consequence, kills the `"de" ∈ "deadline"` / `"kho" ∈ "khong"`
  false positives. *What for:* one coherent change fixes the reported bug and a latent one, each
  pinned by a test. *Experience:* the diacritic-inconsistency case (`"khong de"`→5 vs
  `"không dễ"`→1) was the clearest proof that substring matching, not just negation, was broken.
- **RR-2 (ML `DoKho = null`) is documented but not fixed.** *Why:* it is a whole model, wildly
  out of proportion to a heuristic patch, and building it now would violate scope discipline.
  *What for:* the owner knows the heuristic is the sole difficulty source (so this fix is
  load-bearing) without the plan ballooning. *Experience:* naming the residual risk explicitly is
  what turns "we'll get to it" into a tracked roadmap item instead of silent debt.
- **The fix is confined to `DefaultDifficultyKeywordParser`; `ContainsAnyRule` and the task-type
  parser are left alone.** *Why:* smallest blast radius, and the difficulty parser is the only
  place the reported negation bug lives. *What for:* a reviewer can verify scope with one
  `detect_changes`. *Experience:* the reopen plan's ruthless scope table set the bar — deferred
  items are listed, not folded in.
