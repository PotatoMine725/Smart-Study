using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.Helpers;
using Xunit;

namespace SmartStudyPlanner.Tests.Strategies
{
    public class PriorityCalculatorTests
    {
        private static readonly DateTime Today = new(2026, 4, 11);

        private static PriorityCalculator BuildCalculator(WeightConfig cfg, IClock clock)
        {
            var provider = new DefaultTaskTypeWeightProvider();
            return new PriorityCalculator(
                cfgAccessor: () => cfg,
                rules: new IUrgencyRule[]
                {
                    new OverdueRule(), new JustOverdueRule(), new ImminentRule(),
                    new CompletedRule(), new BeyondHorizonRule(),
                },
                components: new IPriorityComponent[]
                {
                    new TimeComponent(),
                    new TaskTypeComponent(provider),
                    new CreditComponent(),
                    new DifficultyComponent(),
                },
                clock: clock);
        }

        // ---------------- Null guards ----------------

        [Fact]
        public void Calculate_NullTask_ReturnsZero()
        {
            var sut = BuildCalculator(new WeightConfig(), new FakeClock(Today));
            Assert.Equal(0.0, sut.Calculate(null!, new MonHoc("X", 3)));
        }

        [Fact]
        public void Calculate_NullMonHoc_ReturnsZero()
        {
            var sut = BuildCalculator(new WeightConfig(), new FakeClock(Today));
            var task = new StudyTask { HanChot = Today, TrangThai = "Đang làm" };
            Assert.Equal(0.0, sut.Calculate(task, null!));
        }

        // ---------------- Urgency rule short-circuit ----------------

        [Fact]
        public void Calculate_VeryOverdue_ReturnsZeroFromOverdueRule()
        {
            var sut = BuildCalculator(new WeightConfig(), new FakeClock(Today));
            var task = new StudyTask { HanChot = Today.AddDays(-10), TrangThai = "Đang làm", LoaiTask = LoaiCongViec.ThiCuoiKy, DoKho = 5 };
            Assert.Equal(0.0, sut.Calculate(task, new MonHoc("X", 4)));
        }

        [Fact]
        public void Calculate_JustOverdue_Returns100()
        {
            var sut = BuildCalculator(new WeightConfig(), new FakeClock(Today));
            var task = new StudyTask { HanChot = Today.AddDays(-1), TrangThai = "Đang làm", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 3 };
            Assert.Equal(100.0, sut.Calculate(task, new MonHoc("X", 3)));
        }

        [Fact]
        public void Calculate_DueToday_Returns95FromImminentRule()
        {
            var sut = BuildCalculator(new WeightConfig(), new FakeClock(Today));
            var task = new StudyTask { HanChot = Today, TrangThai = "Đang làm", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 2 };
            Assert.Equal(95.0, sut.Calculate(task, new MonHoc("X", 3)));
        }

        [Fact]
        public void Calculate_Completed_ReturnsZero()
        {
            var sut = BuildCalculator(new WeightConfig(), new FakeClock(Today));
            var task = new StudyTask { HanChot = Today.AddDays(5), TrangThai = "Hoàn thành", LoaiTask = LoaiCongViec.ThiCuoiKy, DoKho = 5 };
            Assert.Equal(0.0, sut.Calculate(task, new MonHoc("X", 4)));
        }

        [Fact]
        public void Calculate_BeyondHorizon_ReturnsOne()
        {
            var sut = BuildCalculator(new WeightConfig { HorizonDays = 60 }, new FakeClock(Today));
            var task = new StudyTask { HanChot = Today.AddDays(100), TrangThai = "Đang làm", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 3 };
            Assert.Equal(1.0, sut.Calculate(task, new MonHoc("X", 3)));
        }

        // ---------------- Weighted sum path ----------------

        [Fact]
        public void Calculate_WithinHorizon_SumsAllComponents()
        {
            var cfg = new WeightConfig
            {
                TimeWeight = 0.40,
                TaskTypeWeight = 0.30,
                CreditWeight = 0.20,
                DifficultyWeight = 0.10,
                HorizonDays = 60,
                MaxCredits = 4,
                MaxDifficulty = 5,
            };
            var sut = BuildCalculator(cfg, new FakeClock(Today));

            // daysLeft = 30 -> TimeScore = 50
            // ThiCuoiKy = 1.0 * 100 = 100
            // SoTinChi = 4/4 * 100 = 100
            // DoKho = 5/5 * 100 = 100
            // total = 50*0.4 + 100*0.3 + 100*0.2 + 100*0.1 = 20 + 30 + 20 + 10 = 80
            var task = new StudyTask { HanChot = Today.AddDays(30), TrangThai = "Đang làm", LoaiTask = LoaiCongViec.ThiCuoiKy, DoKho = 5 };
            var mon = new MonHoc("X", 4);

            Assert.Equal(80.0, sut.Calculate(task, mon));
        }

        [Fact]
        public void Calculate_UsesInjectedClock_NotWallClock()
        {
            // Task due 30 ngày sau "fake today" phải tính daysLeft = 30,
            // không liên quan đến DateTime.Now thật
            var cfg = new WeightConfig { HorizonDays = 60 };
            var clock = new FakeClock(Today);
            var sut = BuildCalculator(cfg, clock);

            var task = new StudyTask { HanChot = Today.AddDays(30), TrangThai = "Đang làm", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 3 };
            var score1 = sut.Calculate(task, new MonHoc("X", 3));

            // Dịch clock đi 20 ngày -> daysLeft còn 10 -> score phải khác
            clock.Now = Today.AddDays(20);
            var score2 = sut.Calculate(task, new MonHoc("X", 3));

            Assert.NotEqual(score1, score2);
            Assert.True(score2 > score1); // gần deadline hơn -> điểm cao hơn
        }
    }
}
