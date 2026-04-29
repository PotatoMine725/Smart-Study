using System;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.Helpers;
using Xunit;

namespace SmartStudyPlanner.Tests.Strategies
{
    public class DefaultTaskTypeKeywordParserTests
    {
        private readonly DefaultTaskTypeKeywordParser _sut = new();

        [Theory]
        [InlineData("ôn giữa kỳ môn toán", LoaiCongViec.ThiGiuaKy)]
        [InlineData("giua ky sap toi", LoaiCongViec.ThiGiuaKy)]
        [InlineData("thi gk", LoaiCongViec.ThiGiuaKy)]
        [InlineData("ôn cuối kỳ", LoaiCongViec.ThiCuoiKy)]
        [InlineData("thi cuoi ky", LoaiCongViec.ThiCuoiKy)]
        [InlineData("deadline ck", LoaiCongViec.ThiCuoiKy)]
        [InlineData("nộp đồ án", LoaiCongViec.DoAnCuoiKy)]
        [InlineData("do an cuoi", LoaiCongViec.DoAnCuoiKy)]
        [InlineData("final project", LoaiCongViec.DoAnCuoiKy)]
        [InlineData("btl lập trình", LoaiCongViec.DoAnCuoiKy)]
        [InlineData("kiểm tra 15p", LoaiCongViec.KiemTraThuongXuyen)]
        [InlineData("test nhanh", LoaiCongViec.KiemTraThuongXuyen)]
        [InlineData("1 tiết hôm nay", LoaiCongViec.KiemTraThuongXuyen)]
        public void Parse_MatchesExpectedKeyword(string input, LoaiCongViec expected)
        {
            Assert.Equal(expected, _sut.Parse(input.ToLower(), LoaiCongViec.BaiTapVeNha));
        }

        [Fact]
        public void Parse_NoKeyword_ReturnsDefault()
        {
            Assert.Equal(LoaiCongViec.BaiTapVeNha,
                _sut.Parse("làm bài tập chương 3", LoaiCongViec.BaiTapVeNha));
        }
    }

    public class DefaultDifficultyKeywordParserTests
    {
        private readonly DefaultDifficultyKeywordParser _sut = new();

        [Theory]
        [InlineData("chương này khó quá", 5)]
        [InlineData("kho vler", 5)]
        [InlineData("bài căng đét", 5)]
        [InlineData("chết rồi", 5)]
        [InlineData("bài dễ", 1)]
        [InlineData("chill thôi", 1)]
        [InlineData("nhàn", 1)]
        [InlineData("ez game", 1)]
        public void Parse_MatchesExpectedDifficulty(string input, int expected)
        {
            Assert.Equal(expected, _sut.Parse(input.ToLower(), 3));
        }

        [Fact]
        public void Parse_NoKeyword_ReturnsDefault()
        {
            Assert.Equal(3, _sut.Parse("làm bài tập chương 3", 3));
        }

        [Fact]
        public void Parse_KhoKeyword_WinsOverDe()
        {
            // "khó" rule đứng trước "dễ" rule -> khó thắng
            Assert.Equal(5, _sut.Parse("khó mà dễ", 3));
        }
    }

    public class DefaultDeadlineKeywordParserTests
    {
        // Fake: Thứ Bảy, 11/04/2026
        private static readonly DateTime FakeToday = new(2026, 4, 11, 9, 0, 0);
        private readonly FakeClock _clock = new(FakeToday);
        private readonly DefaultDeadlineKeywordParser _sut;

        public DefaultDeadlineKeywordParserTests()
        {
            _sut = new DefaultDeadlineKeywordParser(_clock);
        }

        private DateTime DefaultVal => FakeToday.AddDays(1);

        // ---------------- Relative date phase ----------------

        [Fact]
        public void Parse_HomNay_ReturnsToday()
        {
            var result = _sut.Parse("nộp bài hôm nay", DefaultVal);
            Assert.Equal(FakeToday, result);
        }

        [Fact]
        public void Parse_NgayMai_ReturnsPlusOne()
        {
            var result = _sut.Parse("deadline ngày mai", DefaultVal);
            Assert.Equal(FakeToday.AddDays(1), result);
        }

        [Fact]
        public void Parse_NgayMot_ReturnsPlusTwo()
        {
            var result = _sut.Parse("ngày mốt nộp", DefaultVal);
            Assert.Equal(FakeToday.AddDays(2), result);
        }

        [Fact]
        public void Parse_TuanSau_ReturnsPlusSeven()
        {
            var result = _sut.Parse("hẹn tuần sau", DefaultVal);
            Assert.Equal(FakeToday.AddDays(7), result);
        }

        [Fact]
        public void Parse_NoKeyword_ReturnsDefault()
        {
            var result = _sut.Parse("làm bài tập", DefaultVal);
            Assert.Equal(DefaultVal, result);
        }

        // ---------------- Day-of-week phase (overrides relative) ----------------

        [Fact]
        public void Parse_Thu2_ReturnsNextMonday()
        {
            // FakeToday = Saturday 11/04/2026 -> Monday gần nhất là 13/04/2026
            var result = _sut.Parse("thứ 2 nộp", DefaultVal);
            Assert.Equal(new DateTime(2026, 4, 13, 9, 0, 0), result);
        }

        [Fact]
        public void Parse_T6_Alias_ReturnsNextFriday()
        {
            // Saturday -> Friday kế tiếp là 17/04/2026
            var result = _sut.Parse("t6 kiểm tra", DefaultVal);
            Assert.Equal(new DateTime(2026, 4, 17, 9, 0, 0), result);
        }

        [Fact]
        public void Parse_ChuNhat_ReturnsNextSunday()
        {
            // Saturday -> Sunday là 12/04/2026
            var result = _sut.Parse("chủ nhật đi chơi", DefaultVal);
            Assert.Equal(new DateTime(2026, 4, 12, 9, 0, 0), result);
        }

        [Fact]
        public void Parse_SameDayOfWeek_ReturnsNextWeek()
        {
            // FakeToday là thứ 7; "thứ 7" -> 7 ngày sau = 18/04/2026
            var result = _sut.Parse("thứ 7 deadline", DefaultVal);
            Assert.Equal(new DateTime(2026, 4, 18, 9, 0, 0), result);
        }

        [Fact]
        public void Parse_Thu2TuanSau_BumpsExtraWeek()
        {
            // "thứ 2" gần nhất là 13/04; "tuần sau" bump thêm 7 ngày -> 20/04/2026
            var result = _sut.Parse("thứ 2 tuần sau kiểm tra", DefaultVal);
            Assert.Equal(new DateTime(2026, 4, 20, 9, 0, 0), result);
        }

        [Fact]
        public void Parse_DayOfWeek_OverridesRelativeDate()
        {
            // Cả "ngày mai" lẫn "thứ 2" — rule day-of-week phải thắng
            var result = _sut.Parse("ngày mai hay thứ 2 gì đó", DefaultVal);
            Assert.Equal(new DateTime(2026, 4, 13, 9, 0, 0), result);
        }
    }
}
