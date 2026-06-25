using SmartStudyPlanner.Core.Scheduling.Contracts;
using SmartStudyPlanner.Core.Scheduling.Engines;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Core.Scheduling
{
    public class StudyTimeSuggestionEngineTests
    {
        private sealed class FixedRawCalculator : IRawMinutesCalculator
        {
            private readonly int _value;
            public FixedRawCalculator(int value) { _value = value; }
            public int Calculate(StudyTask task) => _value;
        }

        [Fact]
        public void Suggest_RawBang0_TraVeChuoi0Phut()
        {
            var sut = new StudyTimeSuggestionEngine(new FixedRawCalculator(0));
            Assert.Equal("0 phút", sut.Suggest(new StudyTask()));
        }

        [Fact]
        public void Suggest_DaHocVuotMucTieu_TraVeThongBaoHoanThanh()
        {
            var sut = new StudyTimeSuggestionEngine(new FixedRawCalculator(60));
            var task = new StudyTask { ThoiGianDaHoc = 75 };
            Assert.Equal("Đã đạt mục tiêu 🎉", sut.Suggest(task));
        }

        [Fact]
        public void Suggest_ConLaiDuoi60Phut_TraVePhut()
        {
            var sut = new StudyTimeSuggestionEngine(new FixedRawCalculator(60));
            var task = new StudyTask { ThoiGianDaHoc = 15 };
            Assert.Equal("45 phút", sut.Suggest(task));
        }

        [Fact]
        public void Suggest_ConLaiTronGio_TraVeChiGio()
        {
            var sut = new StudyTimeSuggestionEngine(new FixedRawCalculator(120));
            var task = new StudyTask { ThoiGianDaHoc = 0 };
            Assert.Equal("2h", sut.Suggest(task));
        }

        [Fact]
        public void Suggest_ConLaiGioVaPhutLe_TraVeDinhDangXhYp()
        {
            var sut = new StudyTimeSuggestionEngine(new FixedRawCalculator(135));
            var task = new StudyTask { ThoiGianDaHoc = 0 };
            Assert.Equal("2h 15p", sut.Suggest(task));
        }
    }
}
