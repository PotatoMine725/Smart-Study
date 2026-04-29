using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Strategies;
using Xunit;

namespace SmartStudyPlanner.Tests.Strategies
{
    public class PriorityComponentsTests
    {
        private readonly WeightConfig _cfg = new();
        private readonly MonHoc _mon = new("Toán", soTinChi: 3);
        private readonly StudyTask _task = new() { TenTask = "x", TrangThai = "Đang làm", DoKho = 3, LoaiTask = LoaiCongViec.BaiTapVeNha };

        // ---------------- TimeComponent ----------------

        [Fact]
        public void TimeComponent_DueToday_ReturnsMaxScore()
        {
            var sut = new TimeComponent();
            // daysLeft = 0 => 100 * (1 - 0/60) = 100
            Assert.Equal(100.0, sut.Score(_task, _mon, _cfg, daysLeft: 0));
        }

        [Fact]
        public void TimeComponent_HalfwayToHorizon_ReturnsHalfScore()
        {
            var sut = new TimeComponent();
            // daysLeft = 30, Horizon = 60 => 100 * (1 - 30/60) = 50
            Assert.Equal(50.0, sut.Score(_task, _mon, _cfg, daysLeft: 30));
        }

        [Fact]
        public void TimeComponent_BeyondHorizon_ClampedToZero()
        {
            var sut = new TimeComponent();
            // daysLeft = 90 > 60 => (1 - 90/60) = -0.5 => Math.Max(0, -50) = 0
            Assert.Equal(0.0, sut.Score(_task, _mon, _cfg, daysLeft: 90));
        }

        [Fact]
        public void TimeComponent_ZeroHorizon_GuardReturnsZero()
        {
            var sut = new TimeComponent();
            var badCfg = new WeightConfig { HorizonDays = 0 };
            Assert.Equal(0.0, sut.Score(_task, _mon, badCfg, daysLeft: 5));
        }

        [Fact]
        public void TimeComponent_NegativeHorizon_GuardReturnsZero()
        {
            var sut = new TimeComponent();
            var badCfg = new WeightConfig { HorizonDays = -10 };
            Assert.Equal(0.0, sut.Score(_task, _mon, badCfg, daysLeft: 5));
        }

        [Fact]
        public void TimeComponent_Weight_ReadsFromConfig()
        {
            var sut = new TimeComponent();
            var cfg = new WeightConfig { TimeWeight = 0.77 };
            Assert.Equal(0.77, sut.Weight(cfg));
        }

        // ---------------- TaskTypeComponent ----------------

        [Fact]
        public void TaskTypeComponent_ReadsFromProvider()
        {
            var sut = new TaskTypeComponent(new DefaultTaskTypeWeightProvider());
            var task = new StudyTask { LoaiTask = LoaiCongViec.ThiCuoiKy };
            // ThiCuoiKy = 1.0 * 100 = 100
            Assert.Equal(100.0, sut.Score(task, _mon, _cfg, daysLeft: 5));
        }

        [Fact]
        public void TaskTypeComponent_Weight_ReadsFromConfig()
        {
            var sut = new TaskTypeComponent(new DefaultTaskTypeWeightProvider());
            var cfg = new WeightConfig { TaskTypeWeight = 0.42 };
            Assert.Equal(0.42, sut.Weight(cfg));
        }

        // ---------------- CreditComponent ----------------

        [Theory]
        [InlineData(1, 25.0)]   // 1/4 * 100
        [InlineData(2, 50.0)]
        [InlineData(4, 100.0)]  // tại max
        [InlineData(8, 100.0)]  // vượt max vẫn clamp ở 100
        public void CreditComponent_Score_ClampsAt100(int soTinChi, double expected)
        {
            var sut = new CreditComponent();
            var mon = new MonHoc("Foo", soTinChi);
            Assert.Equal(expected, sut.Score(_task, mon, _cfg, daysLeft: 5));
        }

        [Fact]
        public void CreditComponent_ZeroCredit_ClampsToOne()
        {
            var sut = new CreditComponent();
            var mon = new MonHoc("Foo", 0);
            // Max(1, 0) = 1 => 1/4 * 100 = 25
            Assert.Equal(25.0, sut.Score(_task, mon, _cfg, daysLeft: 5));
        }

        // ---------------- DifficultyComponent ----------------

        [Theory]
        [InlineData(1, 20.0)]   // 1/5 * 100
        [InlineData(3, 60.0)]
        [InlineData(5, 100.0)]
        [InlineData(10, 100.0)] // clamp ở MaxDifficulty=5
        [InlineData(0, 20.0)]   // clamp dưới ở 1
        public void DifficultyComponent_Score_ClampedWithinMinMax(int doKho, double expected)
        {
            var sut = new DifficultyComponent();
            var task = new StudyTask { DoKho = doKho };
            Assert.Equal(expected, sut.Score(task, _mon, _cfg, daysLeft: 5));
        }
    }
}
