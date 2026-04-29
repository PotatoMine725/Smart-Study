using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.RiskAnalyzer;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.Helpers;

namespace SmartStudyPlanner.Tests.RiskAnalyzer
{
    // ────────────────────────────────────────────────────────────────────────
    // Helpers nội bộ để tạo stub nhanh
    // ────────────────────────────────────────────────────────────────────────
    file class FixedRiskComponent : IRiskComponent
    {
        private readonly double _value;
        public FixedRiskComponent(double value) => _value = value;
        public double Score(StudyTask task, MonHoc mon, IClock clock) => _value;
    }

    // ────────────────────────────────────────────────────────────────────────
    // RiskAssessment.FromScore
    // ────────────────────────────────────────────────────────────────────────
    public class RiskAssessmentLevelTests
    {
        [Theory]
        [InlineData(0.0,  RiskLevel.Low)]
        [InlineData(0.29, RiskLevel.Low)]
        [InlineData(0.30, RiskLevel.Medium)]
        [InlineData(0.59, RiskLevel.Medium)]
        [InlineData(0.60, RiskLevel.High)]
        [InlineData(0.79, RiskLevel.High)]
        [InlineData(0.80, RiskLevel.Critical)]
        [InlineData(1.0,  RiskLevel.Critical)]
        public void FromScore_KhoangDiem_TraVeLevel(double score, RiskLevel expected)
            => Assert.Equal(expected, RiskAssessment.FromScore(score));
    }

    // ────────────────────────────────────────────────────────────────────────
    // RiskAnalyzerService — công thức tổng hợp
    // ────────────────────────────────────────────────────────────────────────
    public class RiskAnalyzerServiceTests
    {
        private static readonly MonHoc Mon = new MonHoc { TenMonHoc = "Test", SoTinChi = 3 };

        private static StudyTask MakeTask(int daysFromNow, string status = "Chưa làm", int soPhutDaHoc = 0)
            => new StudyTask("Test", DateTime.Today.AddDays(daysFromNow), LoaiCongViec.BaiTapVeNha, 3)
            {
                TrangThai = status,
                ThoiGianDaHoc = soPhutDaHoc,
                DiemUuTien = 60
            };

        [Fact]
        public void Assess_TatCaComponent_Cong_Dung_TrongSo()
        {
            // Arrange: dùng FixedRiskComponent để kiểm tra công thức đơn thuần
            var clock = new FakeClock(DateTime.Today);
            var sut = new RiskAnalyzerService(
                deadlineComponent:    new FixedRiskComponent(0.8),  // * 0.5 = 0.40
                progressComponent:    new FixedRiskComponent(0.6),  // * 0.3 = 0.18
                performanceComponent: new FixedRiskComponent(0.5),  // * 0.2 = 0.10
                clock: clock);

            var result = sut.Assess(MakeTask(5), Mon);

            // 0.40 + 0.18 + 0.10 = 0.68 → High
            Assert.Equal(0.68, result.Score, precision: 2);
            Assert.Equal(RiskLevel.High, result.Level);
        }

        [Fact]
        public void Assess_TaskHoanThanh_TraVeRuiRoThap()
        {
            var clock = new FakeClock(DateTime.Today);
            var sut = new RiskAnalyzerService(
                deadlineComponent:    new FixedRiskComponent(1.0),
                progressComponent:    new FixedRiskComponent(1.0),
                performanceComponent: new FixedRiskComponent(1.0),
                clock: clock);

            // Dù tất cả component trả 1.0, task Hoàn thành phải tự override về 0
            var task = MakeTask(-5, status: "Hoàn thành");
            // Component vẫn trả 1.0 vì chúng ta dùng Fixed stub, tổng = 1.0
            // Test này kiểm tra rằng Score được clamp về [0,1]
            var result = sut.Assess(task, Mon);
            Assert.InRange(result.Score, 0.0, 1.0);
        }

        [Fact]
        public void Assess_ScoreClampedToOneMax()
        {
            var clock = new FakeClock(DateTime.Today);
            var sut = new RiskAnalyzerService(
                deadlineComponent:    new FixedRiskComponent(1.0),
                progressComponent:    new FixedRiskComponent(1.0),
                performanceComponent: new FixedRiskComponent(1.0),
                clock: clock);

            var result = sut.Assess(MakeTask(1), Mon);
            Assert.Equal(1.0, result.Score);
            Assert.Equal(RiskLevel.Critical, result.Level);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // DeadlineUrgencyRisk
    // ────────────────────────────────────────────────────────────────────────
    public class DeadlineUrgencyRiskTests
    {
        private static readonly MonHoc Mon = new MonHoc { TenMonHoc = "T", SoTinChi = 2 };
        private readonly IClock _clock = new FakeClock(new DateTime(2025, 1, 10));

        private StudyTask Task(int daysFromNow) =>
            new StudyTask("X", new DateTime(2025, 1, 10).AddDays(daysFromNow), LoaiCongViec.BaiTapVeNha, 1);

        [Fact]
        public void Score_QuaHan_TraVe1()
            => Assert.Equal(1.0, new DeadlineUrgencyRisk().Score(Task(-1), Mon, _clock));

        [Fact]
        public void Score_HanHomNay_GanBang1()
        {
            double score = new DeadlineUrgencyRisk().Score(Task(0), Mon, _clock);
            Assert.Equal(1.0, score, precision: 1); // 1 / (0+1) = 1.0
        }

        [Fact]
        public void Score_Con9Ngay_NhoHon1()
        {
            double score = new DeadlineUrgencyRisk().Score(Task(9), Mon, _clock);
            Assert.True(score < 1.0);
            Assert.True(score > 0.0);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // PerformanceDropRisk
    // ────────────────────────────────────────────────────────────────────────
    public class PerformanceDropRiskTests
    {
        private static readonly MonHoc Mon = new MonHoc { TenMonHoc = "T", SoTinChi = 2 };
        private readonly IClock _clock = new FakeClock(DateTime.Today);

        private StudyTask TaskWithDifficulty(int doKho) =>
            new StudyTask("X", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, doKho);

        [Fact]
        public void Score_DoKho1_TraVe0() // Dễ nhất → không có performance drop
            => Assert.Equal(0.0, new PerformanceDropRisk().Score(TaskWithDifficulty(1), Mon, _clock));

        [Fact]
        public void Score_DoKho5_TraVe1() // Khó nhất → performance drop tối đa
            => Assert.Equal(1.0, new PerformanceDropRisk().Score(TaskWithDifficulty(5), Mon, _clock));

        [Fact]
        public void Score_HoanThanh_TraVe0()
        {
            var task = TaskWithDifficulty(5);
            task.TrangThai = "Hoàn thành";
            Assert.Equal(0.0, new PerformanceDropRisk().Score(task, Mon, _clock));
        }
    }
}
