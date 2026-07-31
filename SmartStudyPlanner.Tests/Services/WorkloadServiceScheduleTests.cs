using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    /// <summary>
    /// CHARACTERIZATION tests cho <see cref="WorkloadServiceImpl.GenerateSchedule"/>.
    ///
    /// Đây KHÔNG phải test tính đúng đắn. Chúng ghi lại hành vi hiện tại để một thay đổi
    /// sau này phải là chủ ý chứ không phải vô tình. Nếu một test ở đây đỏ, hãy đọc
    /// WorkloadServiceImpl.cs:40-109 trước — implementation là spec, và câu hỏi đầu tiên
    /// luôn là "hành vi có đổi không", không phải "sửa production cho test xanh".
    ///
    /// Cố ý KHÔNG assert thứ tự theo deadline: HanChot không xuất hiện ở đâu trong
    /// allocator (CSA 2026-07-27, Key Finding 3). Deadline-awareness là Epic 3 / SOE
    /// sau gate G2, không phải thiếu sót cần vá ở đây.
    /// </summary>
    public class WorkloadServiceScheduleTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 4, 11, 9, 0, 0);
        private static DateTime Today => FixedNow.Date;

        [Fact]
        public void GenerateSchedule_KhongCoTask_VanTao7Ngay_BatDauTuHomNay()
        {
            var (hocKy, engine) = BuildFixture();

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.Equal(7, days.Count);
            Assert.Equal(Today, days[0].Date);
            Assert.Equal("Hôm nay", days[0].DisplayName);
            Assert.Equal("Ngày mai", days[1].DisplayName);
            // Ngày thứ 3 trở đi hiển thị dd/MM/yyyy. So sánh với chính biểu thức format
            // thay vì literal "13/04/2026": dấu "/" trong format string là date separator
            // theo CurrentCulture, nên literal sẽ vỡ trên runner có culture khác.
            Assert.Equal(Today.AddDays(2).ToString("dd/MM/yyyy"), days[2].DisplayName);
        }

        [Fact]
        public void GenerateSchedule_CacNgayLuonLienTiep_KeCaNgayMoMoThem()
        {
            // 600 phút với sức học 60 phút/ngày -> tràn qua 7 ngày đầu.
            var (hocKy, engine) = BuildFixture(("Rất dài", 90, 600));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            // Ngày mở thêm lấy offset từ days.Count đang lớn dần (WorkloadServiceImpl.cs:83-84).
            // Một off-by-one ở đó sẽ tạo lỗ hổng hoặc ngày trùng mà không assert nào khác bắt được.
            Assert.Equal(
                Enumerable.Range(0, days.Count).Select(i => Today.AddDays(i)).ToList(),
                days.Select(d => d.Date).ToList());
        }

        [Fact]
        public void GenerateSchedule_BoQuaTaskDaHoanThanh()
        {
            var (hocKy, engine) = BuildFixture(("Xong", 90, 120));
            hocKy.DanhSachMonHoc[0].DanhSachTask[0].TrangThai = StudyTaskStatus.HoanThanh;

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.All(days, d => Assert.Empty(d.Tasks));
        }

        [Fact]
        public void GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh()
        {
            // GenerateSchedule KHÔNG thuần: nó ghi thẳng DiemUuTien vào model của caller
            // (WorkloadServiceImpl.cs:50), và chỉ cho những task lọt qua bộ lọc ở :48.
            var (hocKy, engine) = BuildFixture(("Chưa làm", 42.5, 0), ("Đã xong", 99, 0));
            var tasks = hocKy.DanhSachMonHoc[0].DanhSachTask;
            tasks[1].TrangThai = StudyTaskStatus.HoanThanh;
            tasks[1].DiemUuTien = -1;

            Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.Equal(42.5, tasks[0].DiemUuTien);
            Assert.Equal(-1, tasks[1].DiemUuTien); // task đã xong không bị chấm lại
        }

        [Fact]
        public void GenerateSchedule_TruThoiGianDaHoc_ChiXepPhanConLai()
        {
            var (hocKy, engine) = BuildFixture(("Đang dở", 90, 100));
            hocKy.DanhSachMonHoc[0].DanhSachTask[0].ThoiGianDaHoc = 40;

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.Equal(60, days.Sum(d => d.TotalMinutes));
        }

        [Fact]
        public void GenerateSchedule_DaHocDuSoPhut_ThiKhongXepNua()
        {
            var (hocKy, engine) = BuildFixture(("Gần xong", 90, 100));
            hocKy.DanhSachMonHoc[0].DanhSachTask[0].ThoiGianDaHoc = 100;

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.All(days, d => Assert.Empty(d.Tasks));
        }

        [Fact]
        public void GenerateSchedule_KhongNgayNaoVuotSucHoc_VaTongPhutDuocBaoToan()
        {
            // sức học 1h = 60 phút; 180 phút phải trải ra đúng 3 ngày.
            var (hocKy, engine) = BuildFixture(("Dài", 90, 180));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.All(days, d => Assert.True(d.TotalMinutes <= 60));
            Assert.Equal(180, days.Sum(d => d.TotalMinutes));
            Assert.Equal(3, days.Count(d => d.Tasks.Count > 0));
        }

        [Fact]
        public void GenerateSchedule_ViecBiCatNho_DuocDanhSoPhan()
        {
            var (hocKy, engine) = BuildFixture(("Dài", 90, 180));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);
            var names = days.SelectMany(d => d.Tasks).Select(t => t.TenTask).ToList();

            Assert.Equal(new[] { "Dài (Phần 1)", "Dài (Phần 2)", "Dài (Phần 3)" }, names);
        }

        [Fact]
        public void GenerateSchedule_ViecVuaDuMotNgay_KhongBiDanhSoPhan()
        {
            // Ranh giới đặt tên ở WorkloadServiceImpl.cs:98: chỉ gắn "(Phần n)" khi thực sự
            // phải cắt. Vừa khít sức học vẫn là tên trần.
            var (hocKy, engine) = BuildFixture(("Vừa đủ", 90, 60));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.Equal("Vừa đủ", Assert.Single(days[0].Tasks).TenTask);
            Assert.Equal(60, days[0].TotalMinutes);
        }

        [Fact]
        public void GenerateSchedule_HetChoTrong7Ngay_ThiMoThemNgay()
        {
            // 60 phút x 7 ngày = 420; 600 phút buộc phải tràn sang ngày thứ 8 trở đi.
            var (hocKy, engine) = BuildFixture(("Rất dài", 90, 600));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.Equal(10, days.Count);
            Assert.Equal(600, days.Sum(d => d.TotalMinutes));
        }

        [Fact]
        public void GenerateSchedule_UuTienCaoDuocXepTruoc()
        {
            var (hocKy, engine) = BuildFixture(("Thấp", 10, 60), ("Cao", 95, 60));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);
            var firstScheduled = days.First(d => d.Tasks.Count > 0).Tasks[0].TenTask;

            Assert.Equal("Cao", firstScheduled);
        }

        [Fact]
        public void GenerateSchedule_ChonNgayITAINHAT_ChuKhongPhaiNgaySomNhatConCho()
        {
            // "Cao" chiếm trọn ngày 0 và 30 phút của ngày 1, để ngày 1 còn trống 30 phút —
            // vừa đủ chứa "Thấp". Nhưng allocator sắp theo TotalMinutes tăng dần
            // (WorkloadServiceImpl.cs:77-79), nên nó chọn ngày 2 đang rỗng. Công việc bị
            // TRẢI RA chứ không dồn vào các ngày sớm.
            var (hocKy, engine) = BuildFixture(("Cao", 90, 90), ("Thấp", 10, 30));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.Equal(60, days[0].TotalMinutes);
            Assert.Equal(30, days[1].TotalMinutes);
            Assert.Equal(30, days[2].TotalMinutes);
            Assert.Equal("Thấp", Assert.Single(days[2].Tasks).TenTask);
        }

        [Fact]
        public void GenerateSchedule_TenMonLayTuMonHocSoHuuTask()
        {
            var hocKy = new HocKy("HK Đa môn", Today);
            var toan = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var ly = new MonHoc("Lý", 4) { MaHocKy = hocKy.MaHocKy };
            toan.DanhSachTask.Add(NewTask("Bài Toán"));
            ly.DanhSachTask.Add(NewTask("Bài Lý"));
            hocKy.DanhSachMonHoc.Add(toan);
            hocKy.DanhSachMonHoc.Add(ly);

            var engine = new StubDecisionEngine();
            engine.Priorities["Bài Toán"] = 50;
            engine.Minutes["Bài Toán"] = 30;
            engine.Priorities["Bài Lý"] = 40;
            engine.Minutes["Bài Lý"] = 30;

            var scheduled = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0)
                                       .SelectMany(d => d.Tasks).ToList();

            Assert.Equal("Toán", scheduled.Single(t => t.TenTask == "Bài Toán").TenMon);
            Assert.Equal("Lý", scheduled.Single(t => t.TenTask == "Bài Lý").TenMon);
        }

        // ---- fixture ----

        private static StudyTask NewTask(string ten)
            => new StudyTask(ten, FixedNow.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);

        /// <summary>Một học kỳ, một môn, n task — mỗi task kèm điểm ưu tiên và số phút thô.</summary>
        private static (HocKy, StubDecisionEngine) BuildFixture(
            params (string name, double pri, int mins)[] tasks)
        {
            var hocKy = new HocKy("HK Sched", Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var engine = new StubDecisionEngine();

            foreach (var (name, pri, mins) in tasks)
            {
                monHoc.DanhSachTask.Add(NewTask(name));
                engine.Priorities[name] = pri;
                engine.Minutes[name] = mins;
            }

            hocKy.DanhSachMonHoc.Add(monHoc);
            return (hocKy, engine);
        }

        private static WorkloadServiceImpl Sut(StubDecisionEngine engine)
            => new WorkloadServiceImpl(engine, new FakeClock(FixedNow));

        // ---- test double ----

        /// <summary>
        /// Trả điểm ưu tiên và số phút theo bảng tra, để test không phụ thuộc vào công thức
        /// thật của DecisionEngine. Phải implement đủ 6 member của IDecisionEngine dù
        /// GenerateSchedule chỉ gọi CalculatePriority và CalculateRawSuggestedMinutes.
        /// </summary>
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
