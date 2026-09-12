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
using SmartStudyPlanner.Services.ML;

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
    /// ("day-major flatten"), về NGUYÊN TẮC một item có thể xuất hiện SAU các item của những
    /// ngày có index cao hơn -- không có gì trong shape {Items, Days} đảm bảo ngược lại. Bất biến
    /// thật là <b>within-day order</b>: mỗi <c>ScheduleDay.Tasks</c> được append CÙNG một vòng lặp
    /// với <c>ScheduledItem</c> khớp của nó, nên lọc <c>items.Where(i =&gt; i.Date == day.Date)</c>
    /// rồi so theo thứ tự chèn luôn đúng.
    ///
    /// T3.3 (2026-08-06 review round): allocator hiện tại (earliest-feasible, CP-3) chọn ngày
    /// đơn điệu không giảm qua toàn bộ vòng chạy, với BẤT KỲ input nào -- hệ quả trực tiếp của
    /// nhánh lọc theo HanChot provably inert về output (chứng minh đầy đủ, canonical: xem
    /// docs/plans/2026-08-06-deadline-tier-provably-inert.md, đừng chép lại ở đây). Nghĩa là
    /// "day-major flatten" và thứ tự chèn <c>Items</c> giờ LUÔN trùng nhau với allocator thật hôm
    /// nay -- phản ví dụ không còn tái tạo được qua <c>GenerateScheduleWithIdentity</c> với BẤT KỲ
    /// fixture nào. Vì vậy <see cref="GenerateSchedule_ChieuTuItems_TrungKhopTheoTungNgay_KhongTheoViTri"/>
    /// dựng thẳng {Items, Days} bằng tay thay vì gọi allocator, để chứng minh hợp đồng tiêu thụ
    /// đúng độc lập với việc thuật toán cụ thể hôm nay có bộc lộ nó hay không -- hợp đồng vẫn phải
    /// giữ cho một allocator tương lai (vd. T3.9 Optimize() seam, có thể không đơn điệu).
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
            // T3.3 (Epic 3, Card F, 2026-08-06 review round): trước T3.3, allocator dùng
            // least-loaded (OrderBy(d => d.TotalMinutes)); với stable-sort tie-break, một task
            // xếp SAU (H) có thể "quay lại" một ngày index THẤP mà task khác (A) đã dùng trước
            // đó -- đó là phản ví dụ gốc của test này, chứng minh khớp theo vị trí phẳng SAI.
            //
            // T3.3 đổi placement rule sang earliest-feasible. Đã CHỨNG MINH bằng đại số (không
            // chỉ thử nghiệm) rằng với thuật toán hiện tại, quyết định chọn ngày ở MỌI chunk luôn
            // quy về đúng một giá trị E = "ngày sớm nhất (theo Date) còn chỗ trong danh sách
            // days hiện tại", BẤT KỂ có lọc theo HanChot hay không -- vì:
            //   - Nếu E <= HanChot: E tự nó đã thoả điều kiện lọc, nên tier-1 (lọc theo hạn) trả
            //     về đúng E (E là min của toàn tập, cũng là min của một tập con chứa E).
            //   - Nếu E > HanChot: mọi ngày <= HanChot đều KHÔNG còn chỗ (vì E là ngày sớm nhất
            //     CÒN CHỖ, nên mọi ngày trước E chắc chắn đã đầy) -- tier-1 rỗng, rơi về tier-2
            //     (bỏ qua hạn), vẫn ra E.
            // Vì capacity một ngày chỉ TĂNG (không có gì làm rỗng lại một ngày đã lấp), E chỉ có
            // thể đứng yên hoặc tăng qua từng chunk -- KHÔNG BAO GIỜ giảm. Kết luận: chuỗi ngày
            // được chọn qua toàn bộ vòng chạy luôn ĐƠN ĐIỆU KHÔNG GIẢM, với BẤT KỲ input nào --
            // kể cả deadline trải rộng/xen kẽ thứ tự ưu tiên (đã thử nghiệm thực tế với nhiều tổ
            // hợp deadline lệch nhau, kể cả deadline trong quá khứ, trước khi kết luận). Nghĩa là
            // "day-major flatten" và "thứ tự chèn Items" giờ LUÔN trùng nhau -- phản ví dụ gốc
            // (dựa vào allocator thật) không còn tái tạo được với BẤT KỲ fixture nào nữa.
            //
            // Vì vậy test này giờ CHỦ Ý KHÔNG gọi allocator thật -- nó dựng thẳng
            // ScheduledItem/ScheduleDay bằng tay để chứng minh HỢP ĐỒNG tiêu thụ (correlate theo
            // Date, không theo vị trí phẳng) một cách độc lập với việc thuật toán CỤ THỂ hôm nay
            // có tạo ra tình huống này hay không. Hợp đồng này vẫn phải đúng cho MỌI {Items, Days}
            // thoả hai bất biến mà GenerateScheduleWithIdentity đảm bảo (dù allocator nào sinh ra
            // chúng): (1) trong một ngày, thứ tự ScheduledTask = thứ tự chèn của các ScheduledItem
            // cùng Date đó; (2) không có gì đảm bảo thứ tự flatten theo ngày trùng thứ tự Items.
            // Bất biến (2) là điều IConstraintValidator/IObjectiveEvaluator (Card D/E) và một
            // allocator tương lai (T3.9 Optimize() seam, có thể không đơn điệu) phải tiếp tục dựa
            // vào -- nên test vẫn cần tồn tại, chỉ là nguồn dữ liệu đổi từ "allocator thật" sang
            // "dựng tay", đúng cách hợp đồng consumption độc lập với implementation cụ thể.
            var maA = Guid.NewGuid();
            var maB = Guid.NewGuid();
            var hanChot = FixedNow.AddDays(5);

            // Items (thứ tự chèn theo thời gian, mô phỏng "task ưu tiên cao xếp trước rồi mới
            // quay lại ngày đầu"): A@ngày0, B@ngày1, rồi MỘT CHUNK KHÁC của A quay lại ngày0.
            var items = new List<ScheduledItem>
            {
                new ScheduledItem(maA, hanChot, "A", "A (Phần 1)", "Toán", Today, 40),
                new ScheduledItem(maB, hanChot, "B", "B", "Toán", Today.AddDays(1), 40),
                new ScheduledItem(maA, hanChot, "A", "A (Phần 2)", "Toán", Today, 40),
            };

            // Days: ScheduledTask trong từng ngày phải theo ĐÚNG thứ tự chèn của các item cùng
            // Date đó (bất biến (1) ở trên) -- ngày 0 có "A (Phần 1)" TRƯỚC "A (Phần 2)" vì đó là
            // thứ tự items[0] rồi items[2] được chèn, mặc dù items[1] (B) nằm GIỮA chúng theo vị
            // trí phẳng toàn cục.
            var days = new List<ScheduleDay>
            {
                new ScheduleDay
                {
                    Date = Today,
                    Tasks = { new ScheduledTask { TenTask = "A (Phần 1)", TenMon = "Toán", SoPhut = 40 },
                              new ScheduledTask { TenTask = "A (Phần 2)", TenMon = "Toán", SoPhut = 40 } },
                },
                new ScheduleDay
                {
                    Date = Today.AddDays(1),
                    Tasks = { new ScheduledTask { TenTask = "B", TenMon = "Toán", SoPhut = 40 } },
                },
            };

            // Chứng minh phản ví dụ có thật: khớp theo vị trí phẳng SAI ngay từ vị trí 1.
            Assert.Equal(
                new[] { "A (Phần 1)", "B", "A (Phần 2)" },
                items.Select(i => i.TenHienThi).ToList());
            Assert.Equal(
                new[] { "A (Phần 1)", "A (Phần 2)", "B" },
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

            public StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc)
                => new StudyTimePredictionResult(CalculateRawSuggestedMinutes(task), false, 0f);

            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult<WeightConfigSuggestion?>(null);
        }
    }
}
