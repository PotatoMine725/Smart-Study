using SmartStudyPlanner.Core.Risk;
using SmartStudyPlanner.Core.Risk.Evaluators;
using CoreRiskAssessment = SmartStudyPlanner.Core.Risk.Models.RiskAssessment;
using CoreRiskLevel = SmartStudyPlanner.Core.Risk.Models.RiskLevel;
using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.TestDoubles;

namespace SmartStudyPlanner.Tests.Core.Risk
{
    file class FixedRiskComponent : IRiskFactorEvaluator
    {
        private readonly double _value;
        public FixedRiskComponent(double value) => _value = value;
        public string Name => nameof(FixedRiskComponent);
        public double Evaluate(StudyTask task, MonHoc mon, IClock clock) => _value;
    }

    public class RiskAssessmentLevelTests
    {
        [Theory]
        [InlineData(0.0,  CoreRiskLevel.Low)]
        [InlineData(0.29, CoreRiskLevel.Low)]
        [InlineData(0.30, CoreRiskLevel.Medium)]
        [InlineData(0.59, CoreRiskLevel.Medium)]
        [InlineData(0.60, CoreRiskLevel.High)]
        [InlineData(0.79, CoreRiskLevel.High)]
        [InlineData(0.80, CoreRiskLevel.Critical)]
        [InlineData(1.0,  CoreRiskLevel.Critical)]
        public void FromScore_KhoangDiem_TraVeLevel(double score, CoreRiskLevel expected)
            => Assert.Equal(expected, CoreRiskAssessment.FromScore(score));
    }

    public class RiskOrchestratorTests
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
            var clock = new FakeClock(DateTime.Today);
            var sut = new RiskOrchestrator(
                new FixedRiskComponent(0.8),
                new FixedRiskComponent(0.6),
                new FixedRiskComponent(0.5),
                clock: clock);

            var result = sut.Assess(MakeTask(5), Mon);

            Assert.Equal(0.68, result.Score, precision: 2);
            Assert.Equal(CoreRiskLevel.High, result.Level);
        }

        [Fact]
        public void Assess_TaskHoanThanh_TraVeRuiRoThap()
        {
            var clock = new FakeClock(DateTime.Today);
            var sut = new RiskOrchestrator(
                new FixedRiskComponent(1.0),
                new FixedRiskComponent(1.0),
                new FixedRiskComponent(1.0),
                clock: clock);

            var task = MakeTask(-5, status: "Hoàn thành");
            var result = sut.Assess(task, Mon);
            Assert.InRange(result.Score, 0.0, 1.0);
        }

        [Fact]
        public void Assess_ScoreClampedToOneMax()
        {
            var clock = new FakeClock(DateTime.Today);
            var sut = new RiskOrchestrator(
                new FixedRiskComponent(1.0),
                new FixedRiskComponent(1.0),
                new FixedRiskComponent(1.0),
                clock: clock);

            var result = sut.Assess(MakeTask(1), Mon);
            Assert.Equal(1.0, result.Score);
            Assert.Equal(CoreRiskLevel.Critical, result.Level);
        }
    }

    public class DeadlineUrgencyRiskEvaluatorTests
    {
        private static readonly MonHoc Mon = new MonHoc { TenMonHoc = "T", SoTinChi = 2 };
        private readonly IClock _clock = new FakeClock(new DateTime(2025, 1, 10));

        private StudyTask Task(int daysFromNow) =>
            new StudyTask("X", new DateTime(2025, 1, 10).AddDays(daysFromNow), LoaiCongViec.BaiTapVeNha, 1);

        [Fact]
        public void Evaluate_QuaHan_TraVe1()
            => Assert.Equal(1.0, new DeadlineUrgencyRiskEvaluator().Evaluate(Task(-1), Mon, _clock));

        [Fact]
        public void Evaluate_HanHomNay_GanBang1()
        {
            double score = new DeadlineUrgencyRiskEvaluator().Evaluate(Task(0), Mon, _clock);
            Assert.Equal(1.0, score, precision: 1);
        }

        [Fact]
        public void Evaluate_Con9Ngay_NhoHon1()
        {
            double score = new DeadlineUrgencyRiskEvaluator().Evaluate(Task(9), Mon, _clock);
            Assert.True(score < 1.0);
            Assert.True(score > 0.0);
        }
    }

    public class PerformanceDropRiskEvaluatorTests
    {
        private static readonly MonHoc Mon = new MonHoc { TenMonHoc = "T", SoTinChi = 2 };
        private readonly IClock _clock = new FakeClock(DateTime.Today);

        private StudyTask TaskWithDifficulty(int doKho) =>
            new StudyTask("X", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, doKho);

        [Fact]
        public void Evaluate_DoKho1_TraVe0()
            => Assert.Equal(0.0, new PerformanceDropRiskEvaluator().Evaluate(TaskWithDifficulty(1), Mon, _clock));

        [Fact]
        public void Evaluate_DoKho5_TraVe1()
            => Assert.Equal(1.0, new PerformanceDropRiskEvaluator().Evaluate(TaskWithDifficulty(5), Mon, _clock));

        [Fact]
        public void Evaluate_HoanThanh_TraVe0()
        {
            var task = TaskWithDifficulty(5);
            task.TrangThai = "Hoàn thành";
            Assert.Equal(0.0, new PerformanceDropRiskEvaluator().Evaluate(task, Mon, _clock));
        }
    }
}