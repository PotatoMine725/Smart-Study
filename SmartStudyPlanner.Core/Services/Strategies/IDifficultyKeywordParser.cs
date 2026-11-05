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
        //  literal in Unicode NFC — the match is byte-exact ordinal.
        // ─────────────────────────────────────────────────────────────────────────────────

        // Ordered by priority: the "hard" pole is evaluated before the "easy" pole
        // (unchanged from the legacy rule order — "khó mà dễ" stays hard).
        private static readonly (int Value, string[] Keywords)[] _poles =
        {
            (5, new[] { "khó", "kho", "căng", "chết" }),
            (1, new[] { "dễ", "de", "chill", "nhàn", "ez" }),
        };

        // Vietnamese negators. Both diacritic and diacritic-less forms are needed because the
        // orchestrator only ToLower()s the input — it does NOT strip diacritics.
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

        // POLICY SEAM: an un-negated difficulty keyword wins; otherwise fall back to the
        // caller-supplied prior. The parser never names "prior" — the caller owns that policy.
        public int Parse(string lowerInput, int defaultValue)
            => DetectPole(Tokenize(lowerInput)) ?? defaultValue;

        // SEMANTIC SEAM: the difficulty pole the text asserts (5 or 1), or null when it makes no
        // un-negated difficulty claim. Natural insertion point for future rule growth.
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
                    // (owner decision: fall back rather than invert) and keep scanning.
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

            // Compose to NFC so combining-mark input matches the NFC lexicon literals under
            // ordinal comparison. Idempotent for already-NFC text; no real-input behavior change.
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
