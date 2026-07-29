using System;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    public class DecisionEngineTests
    {
        private sealed class NullStudyTimePredictor : IStudyTimePredictor
        {
            public bool IsReady => false;
            public Task<StudyTimePredictionResult> PredictAsync(StudyTask task, MonHoc monHoc, CancellationToken ct = default)
                => Task.FromResult(new StudyTimePredictionResult(0, false, 0f));
        }

        // Shared frozen "now" for the engine clock AND date-sensitive task deadlines, so the
        // priority-ordering asserts below don't flip as the real wall clock drifts past this date.
        private static readonly DateTime FixedNow = new DateTime(2026, 4, 11, 9, 0, 0);

        private static DecisionEngineService BuildSut(WeightConfig? config = null, DateTime? now = null)
        {
            var clock = new FakeClock(now ?? FixedNow);
            return new DecisionEngineService(new DefaultTaskTypeWeightProvider(), clock, new NullStudyTimePredictor(), config);
        }

        [Fact]
        public void CalculatePriority_TaskHoacMonHocNull_TraVe0()
        {
            var sut = BuildSut();
            var monHocMock = new MonHoc("Toán", 3);
            var taskMock = new StudyTask("BT", FixedNow, LoaiCongViec.BaiTapVeNha, 3);

            Assert.Equal(0.0, sut.CalculatePriority(null, monHocMock));
            Assert.Equal(0.0, sut.CalculatePriority(taskMock, null));
            Assert.Equal(0.0, sut.CalculatePriority(null, null));
        }

        [Fact]
        public void CalculatePriority_TaskQuaHan_UuTienCaoHonTaskTrongTuongLai()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Lý", 2);
            var overdueTask = new StudyTask("Trễ hạn", FixedNow.AddDays(-2), LoaiCongViec.ThiCuoiKy, 5);
            var futureTask = new StudyTask("Tương lai", FixedNow.AddDays(10), LoaiCongViec.ThiCuoiKy, 5);

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
            var todayTask = new StudyTask("Hôm nay", FixedNow, LoaiCongViec.BaiTapVeNha, 2);
            var futureTask = new StudyTask("Xa hơn", FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);

            double todayScore = sut.CalculatePriority(todayTask, monHoc);
            double futureScore = sut.CalculatePriority(futureTask, monHoc);

            Assert.True(todayScore > futureScore);
        }

        [Fact]
        public void CalculatePriority_TaskDaHoanThanh_TraVe0()
        {
            var sut = BuildSut();
            var monHoc = new MonHoc("Sinh", 2);
            var task = new StudyTask("Đã xong", FixedNow.AddDays(5), LoaiCongViec.ThiGiuaKy, 3)
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

            // 45 ngày: trong horizon (mặc định 60) nên phải đi qua component pipeline.
            var task = new StudyTask("Bài tập 45 ngày", FixedNow.AddDays(45), LoaiCongViec.BaiTapVeNha, 3);
            // 90 ngày: vượt horizon -> BeyondHorizonRule short-circuit, trả đúng sentinel 1.0.
            var beyondHorizon = new StudyTask("Bài tập 90 ngày", FixedNow.AddDays(90), LoaiCongViec.BaiTapVeNha, 3);
            // 10 ngày: gần hơn -> TimeComponent cao hơn, nên điểm phải lớn hơn mốc 45 ngày.
            var nearer = new StudyTask("Bài tập 10 ngày", FixedNow.AddDays(10), LoaiCongViec.BaiTapVeNha, 3);

            double score = sut.CalculatePriority(task, monHoc);
            double beyondScore = sut.CalculatePriority(beyondHorizon, monHoc);
            double nearerScore = sut.CalculatePriority(nearer, monHoc);

            Assert.Equal(1.0, beyondScore);          // sentinel, không phải điểm thật
            Assert.True(score > beyondScore);        // 45 ngày KHÔNG được rơi vào nhánh horizon
            Assert.True(score < nearerScore);        // và phải xếp dưới mốc gần hơn
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
            var task = new StudyTask("BT", FixedNow.AddDays(10), LoaiCongViec.BaiTapVeNha, 3);

            _ = sut.CalculatePriority(task, monHoc);

            Assert.True(sut.Config.IsValid());
        }

        [Fact]
        public void CalculateRawSuggestedMinutes_DaHoanThanh_TraVe0()
        {
            var sut = BuildSut();
            var task = new StudyTask("Xong", FixedNow.AddDays(1), LoaiCongViec.BaiTapVeNha, 2)
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
            var task = new StudyTask("Task", FixedNow.AddDays(1), LoaiCongViec.BaiTapVeNha, 2)
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
            var task = new StudyTask("Task", FixedNow.AddDays(1), LoaiCongViec.BaiTapVeNha, 2)
            {
                DiemUuTien = 10,
                ThoiGianDaHoc = 500
            };

            Assert.Equal("Đã đạt mục tiêu 🎉", sut.SuggestStudyTime(task));
        }

        // Chốt chặn: file này phải hoàn toàn tất định. Bất kỳ deadline nào dựng từ
        // wall clock đều trôi và sớm muộn sẽ pass/fail vì lý do sai
        // (xem CSA 2026-07-27, §8.4 "the false green").
        [Fact]
        public void TestFile_KhongDungWallClock()
        {
            // Ghép chuỗi để chính dòng assert này không tự trở thành một match.
            var needle = "DateTime" + "." + "Now";
            var source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(TestSourceRoot(), "Services", "DecisionEngineTests.cs"));

            Assert.DoesNotContain(needle, source);
        }

        private static string TestSourceRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !System.IO.File.Exists(
                       System.IO.Path.Combine(dir.FullName, "SmartStudyPlanner.Tests.csproj")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
