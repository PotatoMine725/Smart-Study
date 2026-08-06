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
    /// WorkloadServiceImpl.cs trước — implementation là spec, và câu hỏi đầu tiên luôn là
    /// "hành vi có đổi không", không phải "sửa production cho test xanh".
    ///
    /// T3.3 (Epic 3, Card F, CP-3 2026-08-05): allocator giờ CÓ đọc <c>HanChot</c> để chọn
    /// ngày — "earliest-feasible": ngày sớm nhất còn chỗ, trong số các ngày không vượt hạn
    /// chót, ưu tiên hơn "ngày ít tải nhất" (least-loaded, quy tắc cũ). Thứ tự ưu tiên
    /// (<c>OrderByDescending(t => DiemUuTien)</c>) không đổi — deadline chỉ chi phối CHỌN
    /// NGÀY, không chi phối thứ tự xếp task. Xem
    /// GenerateSchedule_ChonNgayItaiNhat_ChuKhongPhaiNgayItTaiNhat cho test pin quy tắc mới.
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

            // Ngày mở thêm lấy offset từ days.Count đang lớn dần (WorkloadServiceImpl.cs, nhánh
            // "if (targetDay == null)" trong vòng phân bổ). Một off-by-one ở đó sẽ tạo lỗ hổng
            // hoặc ngày trùng mà không assert nào khác bắt được.
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
            // Ranh giới đặt tên: chỉ gắn "(Phần n)" khi thực sự phải cắt. Vừa khít sức học
            // vẫn là tên trần. Decouple khỏi days[0] (T3.3): assertion chỉ cần "ngày nào có
            // task thì đúng nội dung", không cần đúng VỊ TRÍ trong list — vị trí là chi tiết
            // của quy tắc chọn ngày (least-loaded cũ / earliest-feasible mới), không phải bất
            // biến mà test này nhắm tới.
            var (hocKy, engine) = BuildFixture(("Vừa đủ", 90, 60));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);
            var ngayCoTask = days.First(d => d.Tasks.Count > 0);

            Assert.Equal("Vừa đủ", Assert.Single(ngayCoTask.Tasks).TenTask);
            Assert.Equal(60, ngayCoTask.TotalMinutes);
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
        public void GenerateSchedule_ChonNgayItaiNhat_ChuKhongPhaiNgayItTaiNhat()
        {
            // T3.3 (CP-3 2026-08-05): earliest-feasible thay least-loaded. "Cao" (90p, cắt
            // theo capacity 60p/ngày) chiếm trọn ngày 0 (60p) rồi 30p đầu ngày 1, để ngày 1
            // còn trống đúng 30 phút. "Thấp" (30p, xếp sau vì ưu tiên thấp hơn) giờ phải rơi
            // vào ngày SỚM NHẤT còn chỗ — ngày 1 — chứ không phải ngày 2 đang rỗng (đó là kết
            // quả của quy tắc least-loaded CŨ, đã bị thay thế).
            //
            // Cả hai task cùng HanChot (NewTask: FixedNow.AddDays(5)), xa hơn nhiều so với
            // ngày 0/1/2 dùng ở đây — nên "trong hạn chót" không loại bất kỳ ngày nào trong
            // phạm vi test này; điều phân biệt hai quy tắc thuần tuý là "sớm nhất" so với "ít
            // tải nhất" giữa các ngày còn chỗ.
            var (hocKy, engine) = BuildFixture(("Cao", 90, 90), ("Thấp", 10, 30));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 1.0);

            Assert.Equal(60, days[0].TotalMinutes);
            Assert.Equal(60, days[1].TotalMinutes);
            Assert.Equal(0, days[2].TotalMinutes);
            Assert.Equal("Thấp", Assert.Single(days[1].Tasks, t => t.TenTask == "Thấp").TenTask);
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

        // ---- sàn capacity: bất biến của vòng phân bổ ----
        //
        // CẢNH BÁO CHO NGƯỜI SỬA SAU: nếu ClampCapacityMinutes bị gỡ, những test dưới đây
        // KHÔNG đỏ — chúng TREO. Vòng while trong GenerateSchedule không tiến triển khi
        // capacityMinutes < 1, nên test runner sẽ đứng im cho tới khi CI hết giờ. Đó là
        // hành vi mong đợi của một test đặt đúng chỗ cho lỗi này: treo là tín hiệu ồn ào,
        // không phải tín hiệu im lặng. Đừng "sửa" bằng cách xoá test.

        [Theory]
        [InlineData(0.0)]           // slider/file không bao giờ ra 0, nhưng method tự bảo vệ mình
        [InlineData(-5.0)]
        [InlineData(0.001)]         // (int)(0.001*60) = 0 — đúng lỗi treo WP-4 §3.1
        [InlineData(double.NaN)]    // Math.Max(NaN, x) = NaN nên GetCapacity từng để lọt
        [InlineData(double.NegativeInfinity)]
        public void GenerateSchedule_CapacityDuoiSan_BiKepVeMotGio_KhongTreo(double capacityHours)
        {
            // 180 phút với sàn 1 giờ phải trải đúng 3 ngày, mỗi ngày 60 phút.
            var (hocKy, engine) = BuildFixture(("Dài", 90, 180));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours);

            Assert.All(days, d => Assert.True(d.TotalMinutes <= 60));
            Assert.Equal(180, days.Sum(d => d.TotalMinutes));
            Assert.Equal(3, days.Count(d => d.Tasks.Count > 0));
        }

        [Theory]
        [InlineData(double.PositiveInfinity)]
        [InlineData(1e30)]
        public void GenerateSchedule_CapacityVoHan_BaoHoaChuKhongTranSoAm(double capacityHours)
        {
            // +∞ nằm TRÊN sàn, nên nó không đi vào nhánh kẹp — và không cần. Nguy hiểm thật
            // sự của nó là cast: (int)(∞*60) ngoài dải double->int là undefined, thực tế ra
            // int.MinValue, làm spaceLeft âm và remainingMinutes TĂNG mỗi vòng. Bão hoà về
            // int.MaxValue giữ đúng ý nghĩa "sức học không giới hạn" mà vẫn kết thúc:
            // mọi việc dồn vào một ngày.
            //
            // (GetCapacity vẫn chặn không-hữu-hạn ở biên file — đây là lớp thứ hai cho
            // caller nào không đi qua file, không phải thay thế lớp đó.)
            var (hocKy, engine) = BuildFixture(("Dài", 90, 180));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours);

            Assert.Equal(180, days.Sum(d => d.TotalMinutes));
            Assert.Single(days.Where(d => d.Tasks.Count > 0));
            // Decouple khỏi days[0] (T3.3): chỉ ngày DUY NHẤT có task mới cần đúng tổng phút.
            Assert.Equal(180, days.First(d => d.Tasks.Count > 0).TotalMinutes);
        }

        [Fact]
        public void GenerateSchedule_CapacityTrenSan_KhongBiKep()
        {
            // Chốt rằng cái kẹp KHÔNG đụng vào dải giá trị thật (slider 1..8): 2 giờ vẫn
            // là 120 phút, không bị hạ về 60. Thiếu test này thì một cái kẹp hỏng
            // (ví dụ luôn trả MinCapacityMinutes) vẫn làm mọi test ở trên xanh.
            var (hocKy, engine) = BuildFixture(("Vừa", 90, 120));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 2.0);

            Assert.Single(days.Where(d => d.Tasks.Count > 0));
            Assert.Equal(120, days.First(d => d.Tasks.Count > 0).TotalMinutes);
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
