using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Strategies
{
    public class DifficultyKeywordParserTests
    {
        private readonly DefaultDifficultyKeywordParser _parser = new();

        // ── PriorForTaskType mapping ──────────────────────────────────────────

        [Theory]
        [InlineData(LoaiCongViec.DoAnCuoiKy, 4)]
        [InlineData(LoaiCongViec.ThiCuoiKy, 4)]
        [InlineData(LoaiCongViec.ThiGiuaKy, 3)]
        [InlineData(LoaiCongViec.KiemTraThuongXuyen, 3)]
        [InlineData(LoaiCongViec.BaiTapVeNha, 2)]
        public void PriorForTaskType_ReturnsExpectedValue(LoaiCongViec taskType, int expected)
        {
            Assert.Equal(expected, DefaultDifficultyKeywordParser.PriorForTaskType(taskType));
        }

        // ── Keyword override takes precedence over any prior ──────────────────

        [Theory]
        [InlineData("bài tập khó", LoaiCongViec.BaiTapVeNha, 5)]
        [InlineData("thi cuối kỳ dễ", LoaiCongViec.ThiCuoiKy, 1)]
        [InlineData("ôn thi chill", LoaiCongViec.ThiGiuaKy, 1)]
        [InlineData("đồ án căng", LoaiCongViec.DoAnCuoiKy, 5)]
        public void Parse_KeywordMatch_OverridesPrior(string input, LoaiCongViec taskType, int expected)
        {
            int prior = DefaultDifficultyKeywordParser.PriorForTaskType(taskType);
            int result = _parser.Parse(input.ToLower(), prior);
            Assert.Equal(expected, result);
        }

        // ── No keyword → prior is returned ───────────────────────────────────

        [Theory]
        [InlineData("ôn thi cuối kỳ", LoaiCongViec.ThiCuoiKy, 4)]
        [InlineData("làm bài tập về nhà", LoaiCongViec.BaiTapVeNha, 2)]
        [InlineData("kiểm tra thường xuyên tuần sau", LoaiCongViec.KiemTraThuongXuyen, 3)]
        public void Parse_NoKeyword_ReturnsPrior(string input, LoaiCongViec taskType, int expectedPrior)
        {
            int prior = DefaultDifficultyKeywordParser.PriorForTaskType(taskType);
            int result = _parser.Parse(input.ToLower(), prior);
            Assert.Equal(expectedPrior, result);
        }

        // ── P1 fix (2026-07-24): negation must NOT flip difficulty, and keywords must match on
        //    word boundaries (no "de" ∈ "deadline"). Owner decision: negated → suppress → prior.
        //    This [Theory] is the negation *characterization corpus*: future rule work (new negators,
        //    intensifiers/diminishers, phrase rules) appends rows here rather than adding ad-hoc facts.
        //    Keep the accented literals in NFC (Unicode composed form).
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
    }
}
