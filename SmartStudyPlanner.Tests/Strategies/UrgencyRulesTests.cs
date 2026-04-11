using System;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Strategies;
using Xunit;

namespace SmartStudyPlanner.Tests.Strategies
{
    public class UrgencyRulesTests
    {
        private readonly WeightConfig _cfg = new() { HorizonDays = 60 };
        private readonly StudyTask _task = new() { TenTask = "x", TrangThai = "Đang làm", DoKho = 3 };

        // ---------------- OverdueRule ----------------

        [Theory]
        [InlineData(-10, true, 0.0)]
        [InlineData(-3.01, true, 0.0)]
        [InlineData(-3, false, 0)]   // boundary: d < -3 false tại -3
        [InlineData(-2, false, 0)]
        [InlineData(0, false, 0)]
        public void OverdueRule_FiresOnlyWhenDaysLeftLessThanMinus3(double daysLeft, bool expectedApplied, double expectedScore)
        {
            var sut = new OverdueRule();
            var applied = sut.TryApply(_task, daysLeft, _cfg, out var score);
            Assert.Equal(expectedApplied, applied);
            if (expectedApplied) Assert.Equal(expectedScore, score);
        }

        // ---------------- JustOverdueRule ----------------

        [Theory]
        [InlineData(-2, true, 100.0)]
        [InlineData(-0.01, true, 100.0)]
        [InlineData(0, false, 0)]    // boundary: d < 0 false tại 0
        [InlineData(1, false, 0)]
        public void JustOverdueRule_FiresWhenNegative(double daysLeft, bool expectedApplied, double expectedScore)
        {
            var sut = new JustOverdueRule();
            var applied = sut.TryApply(_task, daysLeft, _cfg, out var score);
            Assert.Equal(expectedApplied, applied);
            if (expectedApplied) Assert.Equal(expectedScore, score);
        }

        // ---------------- ImminentRule ----------------

        [Theory]
        [InlineData(0, true, 95.0)]
        [InlineData(0.5, true, 95.0)]
        [InlineData(0.99, true, 95.0)]
        [InlineData(1, false, 0)]    // boundary: d < 1 false tại 1
        [InlineData(2, false, 0)]
        public void ImminentRule_FiresWhenLessThanOneDay(double daysLeft, bool expectedApplied, double expectedScore)
        {
            var sut = new ImminentRule();
            var applied = sut.TryApply(_task, daysLeft, _cfg, out var score);
            Assert.Equal(expectedApplied, applied);
            if (expectedApplied) Assert.Equal(expectedScore, score);
        }

        // ---------------- CompletedRule ----------------

        [Fact]
        public void CompletedRule_FiresOnlyForHoanThanhStatus()
        {
            var sut = new CompletedRule();
            var done = new StudyTask { TrangThai = "Hoàn thành" };
            var doing = new StudyTask { TrangThai = "Đang làm" };

            Assert.True(sut.TryApply(done, daysLeft: 5, _cfg, out var doneScore));
            Assert.Equal(0.0, doneScore);

            Assert.False(sut.TryApply(doing, daysLeft: 5, _cfg, out _));
        }

        // ---------------- BeyondHorizonRule ----------------

        [Theory]
        [InlineData(61, true, 1.0)]
        [InlineData(1000, true, 1.0)]
        [InlineData(60, false, 0)]   // boundary: d > HorizonDays false tại 60
        [InlineData(30, false, 0)]
        public void BeyondHorizonRule_FiresWhenBeyondHorizon(double daysLeft, bool expectedApplied, double expectedScore)
        {
            var sut = new BeyondHorizonRule();
            var applied = sut.TryApply(_task, daysLeft, _cfg, out var score);
            Assert.Equal(expectedApplied, applied);
            if (expectedApplied) Assert.Equal(expectedScore, score);
        }
    }
}
