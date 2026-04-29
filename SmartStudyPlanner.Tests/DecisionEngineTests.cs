using System;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.Helpers;
using Xunit;

namespace SmartStudyPlanner.Tests
{
    public class DecisionEngineTests
    {
        private sealed class NullStudyTimePredictor : IStudyTimePredictor
        {
            public bool IsReady => false;
            public Task<StudyTimePredictionResult> PredictAsync(StudyTask task, MonHoc monHoc, CancellationToken ct = default)
                => Task.FromResult(new StudyTimePredictionResult(0, false, 0f));
        }

        private static DecisionEngineService BuildSut(WeightConfig? config = null, DateTime? now = null)
        {
            var clock = new FakeClock(now ?? new DateTime(2026, 4, 11, 9, 0, 0));
            return new DecisionEngineService(new DefaultTaskTypeWeightProvider(), clock, new NullStudyTimePredictor(), config);
        }

        [Fact]
        public void CalculatePriority_TaskHoacMonHocNull_TraVe0()
        {
            var sut = BuildSut();
            var monHocMock = new MonHoc("Toán", 3);
            var taskMock = new StudyTask("BT", DateTime.Now, LoaiCongViec.BaiTapVeNha, 3);

            Assert.Equal(0.0, sut.CalculatePriority(null, monHocMock));
            Assert.Equal(0.0, sut.CalculatePriority(taskMock, null));
            Assert.Equal(0.0, sut.CalculatePriority(null, null));
        }

        [Fact]
        public void CalculatePriority_TaskQuaHan_UuTienCaoHonTaskTrongTuongLai()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Lý", 2);
            var overdueTask = new StudyTask("Trễ hạn", DateTime.Now.AddDays(-2), LoaiCongViec.ThiCuoiKy, 5);
            var futureTask = new StudyTask("Tương lai", DateTime.Now.AddDays(10), LoaiCongViec.ThiCuoiKy, 5);

            double overdueScore = sut.CalculatePriority(overdueTask, monHoc);
            double futureScore = sut.CalculatePriority(futureTask, monHoc);

            Assert.True(overdueScore > futureScore);
            Assert.InRange(overdueScore, 0.0, 100.0);
        }

        [Fact]
        public void CalculatePriority_TaskToiHanHomNay_CaoHonTaskXaHon()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Hóa", 2);
            var todayTask = new StudyTask("Hôm nay", DateTime.Now, LoaiCongViec.BaiTapVeNha, 2);
            var futureTask = new StudyTask("Xa hơn", DateTime.Now.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);

            double todayScore = sut.CalculatePriority(todayTask, monHoc);
            double futureScore = sut.CalculatePriority(futureTask, monHoc);

            Assert.True(todayScore > futureScore);
        }

        [Fact]
        public void CalculatePriority_TaskDaHoanThanh_TraVe0()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Sinh", 2);
            var task = new StudyTask("Đã xong", DateTime.Now.AddDays(5), LoaiCongViec.ThiGiuaKy, 3)
            {
                TrangThai = "Hoàn thành"
            };

            double score = sut.CalculatePriority(task, monHoc);
            Assert.Equal(0.0, score);
        }

        [Fact]
        public void CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Toán", 3);
            var task = new StudyTask("Bài tập 45 ngày", DateTime.Now.AddDays(45), LoaiCongViec.BaiTapVeNha, 3);

            double score = sut.CalculatePriority(task, monHoc);
            Assert.True(score > 0.0);
        }

        [Fact]
        public void CalculatePriority_WeightConfigBiLoi_TuDongSuaLoi()
        {
            var sut = BuildSut(new WeightConfig
            {
                TimeWeight = 0.9,
                TaskTypeWeight = 0.9,
                CreditWeight = 0.9,
                DifficultyWeight = 0.9
            });

            var monHoc = new MonHoc("Toán", 3);
            var task = new StudyTask("BT", DateTime.Now.AddDays(10), LoaiCongViec.BaiTapVeNha, 3);

            _ = sut.CalculatePriority(task, monHoc);

            Assert.True(sut.Config.IsValid());
        }

        [Fact]
        public void CalculateRawSuggestedMinutes_DaHoanThanh_TraVe0()
        {
            var sut = BuildSut();
            var task = new StudyTask("Xong", DateTime.Now.AddDays(1), LoaiCongViec.BaiTapVeNha, 2)
            {
                TrangThai = "Hoàn thành",
                DiemUuTien = 80
            };

            Assert.Equal(0, sut.CalculateRawSuggestedMinutes(task));
        }

        [Fact]
        public void SuggestStudyTime_ConLaiItHon60Phut_TraVeGioPhutHoacGio()
        {
            var sut = BuildSut();
            var task = new StudyTask("Task", DateTime.Now.AddDays(1), LoaiCongViec.BaiTapVeNha, 2)
            {
                DiemUuTien = 50,
                ThoiGianDaHoc = 30
            };

            var text = sut.SuggestStudyTime(task);

            Assert.True(text.Contains("h") || text.Contains("phút"));
        }

        [Fact]
        public void SuggestStudyTime_DaDatMucTieu_TraVeThongBaoHoanThanh()
        {
            var sut = BuildSut();
            var task = new StudyTask("Task", DateTime.Now.AddDays(1), LoaiCongViec.BaiTapVeNha, 2)
            {
                DiemUuTien = 10,
                ThoiGianDaHoc = 500
            };

            Assert.Equal("Đã đạt mục tiêu 🎉", sut.SuggestStudyTime(task));
        }
    }
}
