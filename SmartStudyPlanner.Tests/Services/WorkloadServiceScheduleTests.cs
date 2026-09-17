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
using SmartStudyPlanner.Services.ML;

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
    /// T3.3 (Epic 3, Card F, CP-3 2026-08-05): allocator giờ ĐỌC <c>HanChot</c> để chọn ngày —
    /// "earliest-feasible": ngày sớm nhất còn chỗ, trong số các ngày không vượt hạn chót, thay
    /// cho "ngày ít tải nhất" (least-loaded, quy tắc cũ). Thứ tự ưu tiên
    /// (<c>OrderByDescending(t => DiemUuTien)</c>) không đổi — deadline chỉ chi phối CHỌN
    /// NGÀY, không chi phối thứ tự xếp task. Xem
    /// GenerateSchedule_ChonNgaySomNhatConCho_ChuKhongPhaiNgayItTaiNhat cho test pin quy tắc mới.
    ///
    /// <b>Đọc, nhưng provably KHÔNG BAO GIỜ đổi ngày được chọn, với BẤT KỲ input nào</b> — bất
    /// biến về OUTPUT (nhánh lọc theo hạn vẫn CHẠY bình thường mỗi chunk, chỉ là kết quả luôn
    /// khớp nhánh bỏ-qua-hạn), không phải "nhánh này không thực thi". Đừng đọc dòng trên là
    /// "deadline chi phối vị trí xếp": nó chi phối về MẶT CODE, nhưng inert về mặt OUTPUT với
    /// thuật toán hiện tại. Chứng minh đầy đủ (canonical, đừng chép lại):
    /// docs/plans/2026-08-06-deadline-tier-provably-inert.md.
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
        public void GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh()
        {
            // CP-2 AMENDED (2026-08-06, docs/plans/2026-08-06-cp2-amended-diemuutien-writethrough-restored.md):
            // test này bị Card F round 1 xoá (5197784) khi ghi-đè DiemUuTien tưởng như là một
            // impurity thừa, rồi được PHỤC HỒI khi review phát hiện RawMinutesCalculator.Calculate
            // (gọi từ CalculateRawSuggestedMinutes ngay trong GenerateScheduleWithIdentity) đọc
            // thẳng task.DiemUuTien TRÊN MODEL, không qua tham số nào -- bỏ ghi-đè làm task chưa
            // được chấm điểm ở nơi khác âm thầm rớt khỏi lịch (0 phút cần xếp). Đây không phải
            // impurity tuỳ tiện: nó là điều kiện tiên quyết bắt buộc cho bước tính phút bên dưới.
            //
            // GenerateSchedule KHÔNG thuần: nó ghi thẳng DiemUuTien vào model của caller
            // (WorkloadServiceImpl.cs, dòng "task.DiemUuTien = ..." trong vòng lặp populate
            // tatCaTask), và chỉ cho những task lọt qua bộ lọc TrangThai != HoanThanh ngay trên
            // dòng đó.
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
        public void GenerateSchedule_ChonNgaySomNhatConCho_ChuKhongPhaiNgayItTaiNhat()
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
            // Pin TOÀN BỘ nội dung ngày 1, không chỉ sự có mặt của "Thấp" -- assertion trước
            // (Assert.Single(days[1].Tasks, t => t.TenTask == "Thấp")) tự lọc theo tên rồi assert
            // lại đúng cái tên đó, nên KHÔNG BAO GIỜ có thể đỏ (tautology). Thứ tự đúng: "Cao
            // (Phần 2)" chèn trước (Cao ưu tiên cao hơn, xử lý trước) rồi mới "Thấp".
            Assert.Equal(
                new[] { "Cao (Phần 2)", "Thấp" },
                days[1].Tasks.Select(t => t.TenTask).ToArray());
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

        [Fact]
        public void GenerateSchedule_TaskChuaTungDuocChamDiem_VanDuocXepLich()
        {
            // GUARD Ý ĐỊNH cho CP-2 AMENDED (2026-08-06,
            // docs/plans/2026-08-06-cp2-amended-diemuutien-writethrough-restored.md).
            //
            // GenerateSchedule_GhiDeDiemUuTien_ChiTrenTaskChuaHoanThanh ở trên chốt CƠ CHẾ
            // (ghi-đè có xảy ra không). Test này chốt LÝ DO cơ chế đó phải tồn tại — thứ mà một
            // refactor "làm cho thuần" (đúng thứ Card F round 1 đã làm ở 5197784) sẽ xoá cùng
            // lúc với chính test cơ chế kia, vì cả hai đọc như "test cái impurity".
            //
            // Coupling thật: RawMinutesCalculator.Calculate (Core/Scheduling/Engines, dòng 11)
            // đọc THẲNG task.DiemUuTien trên model — "task.DiemUuTien <= 0 return 0" — và
            // StudyTask.DiemUuTien mặc định 0.0. StubDecisionEngine ở cuối file tra bảng theo
            // TÊN task nên KHÔNG tái hiện coupling đó; double dưới đây tái hiện đúng nó.
            //
            // Hậu quả nếu ghi-đè bị bỏ: MỌI task chưa được chấm điểm ở nơi khác (Dashboard
            // pipeline / QuanLyTaskViewModel.TinhDiemVaSapXep) im lặng rớt khỏi lịch — lịch rỗng,
            // không exception, không cảnh báo. WorkloadBalancerViewModel gọi GenerateSchedule
            // thẳng trong constructor, không có bước chấm điểm nào chạy trước.
            var hocKy = new HocKy("HK Sched", Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            monHoc.DanhSachTask.Add(NewTask("Chưa chấm điểm"));
            hocKy.DanhSachMonHoc.Add(monHoc);

            var engine = new PriorityCoupledDecisionEngine();
            engine.Priorities["Chưa chấm điểm"] = 60;
            engine.Minutes["Chưa chấm điểm"] = 90;

            // Tiền đề của test: task đi vào với DiemUuTien mặc định — chưa từng được chấm.
            Assert.Equal(0.0, monHoc.DanhSachTask[0].DiemUuTien);

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours: 3.0);

            Assert.Equal(90, days.Sum(d => d.TotalMinutes));
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(2.0)]
        [InlineData(3.0)]
        public void GenerateSchedule_DonVeNgaySomNhat_NgayDungLaTienToLienTuc_ChiNgayCuoiConCho(
            double capacityHours)
        {
            // T3.3 (CP-3 2026-08-05) earliest-feasible, dạng BẤT BIẾN thay vì một ví dụ 3 ngày.
            // GenerateSchedule_ChonNgaySomNhatConCho_ChuKhongPhaiNgayItTaiNhat chốt quy tắc trên
            // MỘT bố cục cụ thể; test này chốt hệ quả cấu trúc của nó trên nhiều mức sức học —
            // đây chính là thứ người dùng NHÌN THẤY trên màn hình Workload Balancer (các thẻ ngày
            // đặc, liên tục từ hôm nay, thay vì tải rải mỏng khắp 7 ngày như quy tắc least-loaded
            // CŨ).
            //
            // Vì mỗi chunk luôn vào ngày SỚM NHẤT còn chỗ và chunk được cắt vừa đúng chỗ trống
            // (chunk = min(remaining, spaceLeft)), một ngày chỉ còn chỗ khi KHÔNG còn việc nào
            // sau nó — nên các ngày có việc là một tiền tố liên tục, và mọi ngày trừ ngày cuối
            // phải đầy đúng capacity. Quy tắc least-loaded CŨ làm cả hai assert này đỏ.
            var (hocKy, engine) = BuildFixture(
                ("A", 90, 130), ("B", 70, 45), ("C", 50, 200), ("D", 30, 25));

            var days = Sut(engine).GenerateSchedule(hocKy, capacityHours);

            int capacityMinutes = (int)(capacityHours * 60);
            var used = days.Select((d, i) => (Day: d, Index: i))
                           .Where(x => x.Day.Tasks.Count > 0)
                           .ToList();

            Assert.NotEmpty(used);

            // (1) Các ngày có việc là tiền tố liên tục bắt đầu từ hôm nay — không có ngày trống
            //     xen giữa hai ngày có việc.
            Assert.Equal(
                Enumerable.Range(0, used.Count).ToList(),
                used.Select(x => x.Index).ToList());

            // (2) Mọi ngày dùng TRỪ ngày cuối đầy đúng capacity (đặc, không rải mỏng).
            Assert.All(used.Take(used.Count - 1), x => Assert.Equal(capacityMinutes, x.Day.TotalMinutes));

            // (3) Không phút nào bị mất hay nhân đôi khi dồn.
            Assert.Equal(130 + 45 + 200 + 25, days.Sum(d => d.TotalMinutes));
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

        private static WorkloadServiceImpl Sut(IDecisionEngine engine)
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

            public StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc)
                => new StudyTimePredictionResult(CalculateRawSuggestedMinutes(task), false, 0f);

            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult<WeightConfigSuggestion?>(null);
        }

        /// <summary>
        /// Như <see cref="StubDecisionEngine"/>, NHƯNG tái hiện đúng một coupling của
        /// production mà bảng-tra-theo-tên cố tình bỏ qua: <c>RawMinutesCalculator.Calculate</c>
        /// đọc <c>task.DiemUuTien</c> TRÊN MODEL và trả 0 khi điểm &lt;= 0. Chỉ dùng cho
        /// <see cref="GenerateSchedule_TaskChuaTungDuocChamDiem_VanDuocXepLich"/> — các test khác
        /// giữ stub thuần bảng tra để không phụ thuộc vào công thức thật.
        /// </summary>
        private sealed class PriorityCoupledDecisionEngine : IDecisionEngine
        {
            public Dictionary<string, double> Priorities { get; } = new();
            public Dictionary<string, int> Minutes { get; } = new();

            public WeightConfig Config { get; } = new WeightConfig();

            public double CalculatePriority(StudyTask task, MonHoc monHoc)
                => Priorities.GetValueOrDefault(task.TenTask, 0);

            // Cùng cổng "<= 0 thì 0 phút" như RawMinutesCalculator.Calculate.
            public int CalculateRawSuggestedMinutes(StudyTask task)
                => task.DiemUuTien <= 0 ? 0 : Minutes.GetValueOrDefault(task.TenTask, 0);

            public string SuggestStudyTime(StudyTask task) => string.Empty;

            public StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc)
                => new StudyTimePredictionResult(CalculateRawSuggestedMinutes(task), false, 0f);

            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult<WeightConfigSuggestion?>(null);
        }
    }
}
