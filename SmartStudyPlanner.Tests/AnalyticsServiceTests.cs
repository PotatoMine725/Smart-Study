using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Tests.Helpers;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests
{
    public class AnalyticsServiceTests
    {
        [Fact]
        public void StudyLog_HasRequiredProperties_AndGeneratesId()
        {
            var log = new StudyLog
            {
                MaTask       = Guid.NewGuid(),
                NgayHoc      = DateTime.Today,
                SoPhutHoc    = 25,
                SoPhutDuKien = 30,
                DaHoanThanh  = true
            };
            Assert.Equal(25, log.SoPhutHoc);
            Assert.True(log.DaHoanThanh);
            Assert.NotEqual(Guid.Empty, log.Id);
        }

        [Fact]
        public void StudyTask_NgayHoanThanh_IsNullByDefault()
        {
            var task = new StudyTask("Test", DateTime.Today, LoaiCongViec.BaiTapVeNha, 1);
            Assert.Null(task.NgayHoanThanh);
        }

        [Fact]
        public void ComputeWeeklyMinutes_Returns7Entries_WithCorrectTotals()
        {
            var today = new DateTime(2026, 1, 7);
            var logs = new List<StudyLog>
            {
                new() { MaTask = Guid.NewGuid(), NgayHoc = today,               SoPhutHoc = 30 },
                new() { MaTask = Guid.NewGuid(), NgayHoc = today,               SoPhutHoc = 25 },
                new() { MaTask = Guid.NewGuid(), NgayHoc = today.AddDays(-1),   SoPhutHoc = 50 },
                new() { MaTask = Guid.NewGuid(), NgayHoc = today.AddDays(-8),   SoPhutHoc = 60 } // outside window
            };
            var service = new StudyAnalyticsService();

            var result = service.ComputeWeeklyMinutes(logs, today);

            Assert.Equal(7, result.DayLabels.Count);
            Assert.Equal(7, result.MinutesPerDay.Count);
            Assert.Equal(55, result.MinutesPerDay[6]); // today: 30+25
            Assert.Equal(50, result.MinutesPerDay[5]); // yesterday: 50
            Assert.Equal(0,  result.MinutesPerDay[0]); // 6 days ago: nothing
        }

        [Fact]
        public void ComputeProductivityScore_IsZero_WhenNoData()
        {
            var service = new StudyAnalyticsService();
            var score = service.ComputeProductivityScore(0, 0, 0);
            Assert.Equal(0, score.Value);
        }

        [Fact]
        public void ComputeProductivityScore_IsHigh_WhenPerfect()
        {
            var service = new StudyAnalyticsService();
            var score = service.ComputeProductivityScore(1.0, 30, 1.0);
            Assert.InRange(score.Value, 95, 100);
        }

        [Fact]
        public void ComputeSubjectInsights_ReturnsCorrectCompletionRate()
        {
            var hocKy = new HocKy("HK1", DateTime.Today);
            var mon   = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var t1 = new StudyTask("T1", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 1);
            t1.TrangThai = StudyTaskStatus.HoanThanh;
            var t2 = new StudyTask("T2", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 1);
            mon.DanhSachTask.Add(t1);
            mon.DanhSachTask.Add(t2);
            hocKy.DanhSachMonHoc.Add(mon);

            var service   = new StudyAnalyticsService();
            var insights  = service.ComputeSubjectInsights(hocKy, new List<StudyLog>());

            Assert.Single(insights);
            Assert.Equal(0.5, insights[0].CompletionRate, precision: 2);
            Assert.Equal("Toán", insights[0].SubjectName);
            Assert.Equal(1, insights[0].CompletedTaskCount);
        }
        [Fact]
        public void FocusViewModel_WritesStudyLog_OnHoanThanh()
        {
            var task = new StudyTask("Test", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2);
            var item = new TaskDashboardItem { TaskGoc = task, TenTask = "Test", TenMonHoc = "Toán" };
            var repo = new FakeStudyRepository();
            var vm   = new FocusViewModel(item, repo);

            vm.SimulateStudySeconds(300); // 5 minutes
            vm.HoanThanhCommand.Execute(null);

            Assert.Single(repo.AddedLogs);
            Assert.Equal(task.MaTask, repo.AddedLogs[0].MaTask);
            Assert.Equal(5, repo.AddedLogs[0].SoPhutHoc);
            Assert.True(repo.AddedLogs[0].DaHoanThanh);
            Assert.Equal(DateTime.Today, task.NgayHoanThanh);
            Assert.Equal(StudyTaskStatus.HoanThanh, task.TrangThai);
        }
    }
}
