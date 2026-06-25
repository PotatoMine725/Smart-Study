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
    }
}
