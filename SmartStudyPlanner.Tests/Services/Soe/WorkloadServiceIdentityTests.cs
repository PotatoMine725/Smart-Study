using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Soe;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.8 (Epic 3, Card C) — chốt hành vi của <see cref="WorkloadServiceImpl.GenerateScheduleWithIdentity"/>:
    /// mỗi <see cref="ScheduledItem"/> phải mang đúng <c>MaTask</c>/<c>HanChot</c> của task nguồn, và
    /// việc chiếu (project) sang <c>ScheduledTask</c>/<c>ScheduleDay</c> (seam công khai qua
    /// <see cref="WorkloadServiceImpl.GenerateSchedule"/>) phải trùng khớp 1:1 với các item này —
    /// không phải một phép tính lại độc lập có thể lệch.
    ///
    /// Đây KHÔNG test tính đúng đắn của thuật toán phân bổ (đã có
    /// <c>WorkloadServiceScheduleTests</c> lo việc đó) — chỉ test rằng identity đi kèm đúng chunk,
    /// và rằng "internal representation, rồi project" không làm lệch giá trị hiển thị.
    /// </summary>
    public class WorkloadServiceIdentityTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 4, 11, 9, 0, 0);
        private static DateTime Today => FixedNow.Date;

        [Fact]
        public void GenerateScheduleWithIdentity_MoiItem_MangDungMaTaskVaHanChot()
        {
            var hocKy = new HocKy("HK Identity", Today);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Bài Toán", FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);
            mon.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(mon);

            var engine = new StubDecisionEngine();
            engine.Priorities["Bài Toán"] = 50;
            engine.Minutes["Bài Toán"] = 30;

            var (_, items) = Sut(engine).GenerateScheduleWithIdentity(hocKy, capacityHours: 3.0);

            var item = Assert.Single(items);
            Assert.Equal(task.MaTask, item.MaTask);
            Assert.Equal(task.HanChot, item.HanChot);
            Assert.Equal("Bài Toán", item.TenTaskGoc);
            Assert.Equal("Toán", item.TenMon);
            Assert.Equal(30, item.SoPhut);
            Assert.Equal(Today, item.Date);
        }

        [Fact]
        public void GenerateScheduleWithIdentity_TaskBiCatNho_TatCaPhanChungMotMaTask()
        {
            // 180 phút / 60 phút/ngày -> 3 chunk cùng một task.
            var hocKy = new HocKy("HK Identity", Today);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Dài", FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);
            mon.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(mon);

            var engine = new StubDecisionEngine();
            engine.Priorities["Dài"] = 90;
            engine.Minutes["Dài"] = 180;

            var (_, items) = Sut(engine).GenerateScheduleWithIdentity(hocKy, capacityHours: 1.0);

            Assert.Equal(3, items.Count);
            Assert.All(items, i => Assert.Equal(task.MaTask, i.MaTask));
            Assert.All(items, i => Assert.Equal(task.HanChot, i.HanChot));
            Assert.All(items, i => Assert.Equal("Dài", i.TenTaskGoc));
            Assert.Equal(
                new[] { "Dài (Phần 1)", "Dài (Phần 2)", "Dài (Phần 3)" },
                items.Select(i => i.TenHienThi).ToList());
        }

        [Fact]
        public void GenerateScheduleWithIdentity_TaskDaHoanThanh_KhongCoItem()
        {
            var hocKy = new HocKy("HK Identity", Today);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Xong", FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2)
            {
                TrangThai = StudyTaskStatus.HoanThanh
            };
            mon.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(mon);

            var engine = new StubDecisionEngine();
            engine.Priorities["Xong"] = 90;
            engine.Minutes["Xong"] = 120;

            var (days, items) = Sut(engine).GenerateScheduleWithIdentity(hocKy, capacityHours: 3.0);

            Assert.Empty(items);
            Assert.All(days, d => Assert.Empty(d.Tasks));
        }

        [Fact]
        public void GenerateSchedule_ChieuTuItems_TrungKhopVoiScheduledTask()
        {
            // Seam công khai (GenerateSchedule) phải khớp 1:1 với GenerateScheduleWithIdentity —
            // project không được tính lại độc lập, phải cùng nguồn dữ liệu.
            var hocKy = new HocKy("HK Identity", Today);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Dài", FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);
            mon.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(mon);

            var engine = new StubDecisionEngine();
            engine.Priorities["Dài"] = 90;
            engine.Minutes["Dài"] = 180;

            var (days, items) = Sut(engine).GenerateScheduleWithIdentity(hocKy, capacityHours: 1.0);
            var scheduledTasks = days.SelectMany(d => d.Tasks).ToList();

            Assert.Equal(items.Count, scheduledTasks.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Assert.Equal(items[i].TenHienThi, scheduledTasks[i].TenTask);
                Assert.Equal(items[i].TenMon, scheduledTasks[i].TenMon);
                Assert.Equal(items[i].SoPhut, scheduledTasks[i].SoPhut);
            }
        }

        private static WorkloadServiceImpl Sut(StubDecisionEngine engine)
            => new WorkloadServiceImpl(engine, new FakeClock(FixedNow));

        /// <summary>Cùng test double idiom với WorkloadServiceScheduleTests.cs — trả điểm ưu tiên
        /// và số phút theo bảng tra, không phụ thuộc công thức thật của DecisionEngine.</summary>
        private sealed class StubDecisionEngine : IDecisionEngine
        {
            public Dictionary<string, double> Priorities { get; } = new();
            public Dictionary<string, int> Minutes { get; } = new();

            public WeightConfig Config { get; } = new WeightConfig();

            public double CalculatePriority(StudyTask task, MonHoc monHoc)
                => Priorities.GetValueOrDefault(task.TenTask, 0);

            public int CalculateRawSuggestedMinutes(StudyTask task)
                => Minutes.GetValueOrDefault(task.TenTask, 0);

            public string SuggestStudyTime(StudyTask task) => string.Empty;

            public int PredictStudyMinutes(StudyTask task, MonHoc monHoc, out bool isMlPrediction)
            {
                isMlPrediction = false;
                return CalculateRawSuggestedMinutes(task);
            }

            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult<WeightConfigSuggestion?>(null);
        }
    }
}
