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
    /// <see cref="WorkloadServiceImpl.GenerateSchedule"/>) phải trùng khớp với các item này.
    ///
    /// <b>Hợp đồng đúng giữa <c>Items</c> và <c>Days</c> là theo <see cref="ScheduledItem.Date"/>,
    /// KHÔNG phải theo vị trí phẳng.</b> Trong <c>days.SelectMany(d =&gt; d.Tasks)</c>
    /// ("day-major flatten"), một item có thể xuất hiện SAU các item của những ngày có index cao
    /// hơn: <c>OrderBy(d =&gt; d.TotalMinutes)</c> là stable sort, nên khi nhiều ngày hoà điểm
    /// TotalMinutes, ngày index thấp nhất luôn thắng tie-break — một chunk xếp SAU về mặt thời
    /// gian (thứ tự trong <c>Items</c>) hoàn toàn có thể quay lại một ngày sớm đã dùng trước đó.
    /// Xem <see cref="GenerateSchedule_ChieuTuItems_TrungKhopTheoTungNgay_KhongTheoViTri"/> cho
    /// phản ví dụ cụ thể. Bất biến thật là <b>within-day order</b>: mỗi <c>ScheduleDay.Tasks</c>
    /// được append CÙNG một vòng lặp với <c>ScheduledItem</c> khớp của nó, nên lọc
    /// <c>items.Where(i =&gt; i.Date == day.Date)</c> rồi so theo thứ tự chèn luôn đúng.
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
            Assert.Equal("Bài Toán", item.TenHienThi); // không bị cắt -> không hậu tố "(Phần n)"
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
        public void GenerateSchedule_ChieuTuItems_TrungKhopTheoTungNgay_KhongTheoViTri()
        {
            // PHẢN VÍ DỤ cho "khớp theo vị trí phẳng": 8 task x 40 phút, sức chứa 120 phút/ngày
            // (3 chunk vừa một ngày). A..G lần lượt chiếm các ngày 0..6 (mỗi ngày đang rỗng khi
            // task đó tới lượt xếp). Đến task H, cả 7 ngày đều đang hoà 40 phút — OrderBy là
            // stable sort nên tie-break chọn ngày index THẤP NHẤT, tức ngày 0 (đã có A). Kết quả:
            //   - Items (thứ tự xếp theo thời gian): A, B, C, D, E, F, G, H
            //   - Days flatten (day-major, ngày 0 trước): A, H, B, C, D, E, F, G
            // Hai thứ tự này KHÁC NHAU — khớp theo index sẽ sai ngay từ vị trí 1 (B vs H). Đây
            // chính xác là bug mà bản test cũ (khớp theo index, chỉ có 1 task) không lộ ra được.
            var hocKy = new HocKy("HK Identity Multi", Today);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var engine = new StubDecisionEngine();
            var tenTheoThuTu = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
            double pri = 80;
            foreach (var ten in tenTheoThuTu)
            {
                mon.DanhSachTask.Add(new StudyTask(ten, FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2));
                engine.Priorities[ten] = pri;
                engine.Minutes[ten] = 40;
                pri -= 10; // giữ nguyên thứ tự sort ưu tiên A..H, không có hoà điểm ưu tiên
            }
            hocKy.DanhSachMonHoc.Add(mon);

            var (days, items) = Sut(engine).GenerateScheduleWithIdentity(hocKy, capacityHours: 2.0); // 120 phút/ngày

            // Chứng minh phản ví dụ có thật trước khi dựa vào nó.
            Assert.Equal(tenTheoThuTu, items.Select(i => i.TenTaskGoc).ToList());
            Assert.Equal(
                new[] { "A", "H", "B", "C", "D", "E", "F", "G" },
                days.SelectMany(d => d.Tasks).Select(t => t.TenTask).ToList());

            // Hợp đồng ĐÚNG: correlate theo Date, so trong từng ngày theo thứ tự chèn — không
            // đụng tới vị trí phẳng toàn cục.
            foreach (var day in days)
            {
                var itemsCuaNgay = items.Where(i => i.Date == day.Date).ToList();
                Assert.Equal(itemsCuaNgay.Count, day.Tasks.Count);
                for (int i = 0; i < itemsCuaNgay.Count; i++)
                {
                    Assert.Equal(itemsCuaNgay[i].TenHienThi, day.Tasks[i].TenTask);
                    Assert.Equal(itemsCuaNgay[i].TenMon, day.Tasks[i].TenMon);
                    Assert.Equal(itemsCuaNgay[i].SoPhut, day.Tasks[i].SoPhut);
                }
            }
        }

        [Fact]
        public void GenerateSchedule_ChieuTuItems_TrungKhopVoiScheduledTask_MotTask()
        {
            // Trường hợp đơn giản (1 task bị cắt nhỏ): items và day-major flatten trùng thứ tự vì
            // chỉ một task duy nhất tồn tại nên không có cơ hội tie-break quay lại ngày cũ. Giữ
            // lại test này để chốt trường hợp cơ bản; hợp đồng tổng quát (theo Date) đã được
            // GenerateSchedule_ChieuTuItems_TrungKhopTheoTungNgay_KhongTheoViTri chứng minh riêng.
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
