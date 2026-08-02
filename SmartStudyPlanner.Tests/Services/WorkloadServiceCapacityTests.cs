using System;
using System.Globalization;
using System.IO;
using SmartStudyPlanner.Services;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    /// <summary>
    /// Round-trip của <c>capacity.txt</c> (WP-5.2).
    ///
    /// <para>Kế hoạch WP-5 ban đầu kết luận cặp GetCapacity/SaveCapacity KHÔNG test được vì
    /// <c>FilePath</c> là <c>private static readonly</c> gắn vào <c>AppDomain.BaseDirectory</c>.
    /// Tiền đề đó lẫn lộn giữa "không inject được đường dẫn" và "không chạm tới được đường dẫn":
    /// khi chạy test, <c>BaseDirectory</c> chính là thư mục output của test assembly — ghi được,
    /// và KHÔNG phải profile thật của người chạy (đúng thứ WP-2.2 đi dọn). Nên vẫn test được
    /// qua đường dẫn thật, không cần sửa một dòng production nào.</para>
    ///
    /// <para>Cả class dùng chung một file nên phải nằm trong CÙNG một test class — xUnit chạy
    /// song song giữa các class, không song song trong một class. Test nào sau này cần đụng
    /// <c>capacity.txt</c> thì thêm vào đây, đừng mở class mới.</para>
    /// </summary>
    public class WorkloadServiceCapacityTests : IDisposable
    {
        private static readonly string CapacityFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capacity.txt");

        private readonly string? _snapshot;

        public WorkloadServiceCapacityTests()
        {
            // Chụp lại file có sẵn (nếu có) để lần chạy trước không mồi cho lần chạy sau.
            _snapshot = File.Exists(CapacityFile) ? File.ReadAllText(CapacityFile) : null;
        }

        public void Dispose()
        {
            if (_snapshot is null)
            {
                if (File.Exists(CapacityFile)) File.Delete(CapacityFile);
            }
            else
            {
                File.WriteAllText(CapacityFile, _snapshot);
            }
        }

        // GetCapacity/SaveCapacity chỉ đọc/ghi file — không chạm IDecisionEngine hay IClock,
        // nên không cần dựng stub 6 member chỉ để gọi constructor.
        private static WorkloadServiceImpl Sut() => new WorkloadServiceImpl(null!, null!);

        private static void WithCulture(string name, Action body)
        {
            var prev = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(name);
            try { body(); }
            finally { CultureInfo.CurrentCulture = prev; }
        }

        private static void GivenFile(string content) => File.WriteAllText(CapacityFile, content);

        [Fact]
        public void GetCapacity_ChuaCoFile_TraVeMacDinh3Gio()
        {
            if (File.Exists(CapacityFile)) File.Delete(CapacityFile);

            Assert.Equal(3.0, Sut().GetCapacity());
        }

        [Fact]
        public void GetCapacity_SoInvariant_DocDungTrenMoiCulture()
        {
            GivenFile("4.5");

            WithCulture("en-US", () => Assert.Equal(4.5, Sut().GetCapacity()));
            WithCulture("vi-VN", () => Assert.Equal(4.5, Sut().GetCapacity()));
        }

        [Fact]
        public void GetCapacity_FileCuKieuVietNam_VanDocDuocTrenMayVietNam()
        {
            // Backward compat: file do bản cũ ghi trên máy vi-VN chứa dấu phẩy.
            GivenFile("4,5");

            WithCulture("vi-VN", () => Assert.Equal(4.5, Sut().GetCapacity()));
        }

        [Fact]
        public void GetCapacity_DauPhayTrenMayEnUS_KhongConBiDocThanhHangNghin()
        {
            // WP-4 §3.2: double.TryParse 2 tham số ngầm bật AllowThousands, nên "4,5" trên
            // en-US ra 45 giờ/ngày — sai âm thầm, KHÔNG rơi về default. Reader mới phải để
            // nó fail và trả default, chứ tuyệt đối không được ra 45.
            GivenFile("4,5");

            WithCulture("en-US", () =>
            {
                double val = Sut().GetCapacity();
                Assert.NotEqual(45.0, val);
                Assert.Equal(3.0, val);
            });
        }

        [Fact]
        public void GetCapacity_NoiDungRac_TraVeMacDinh()
        {
            GivenFile("ba tiếng rưỡi");

            Assert.Equal(3.0, Sut().GetCapacity());
        }

        [Fact]
        public void GetCapacity_CoKhoangTrangThuaVaXuongDong_VanDocDuoc()
        {
            GivenFile("  4.5 \r\n");

            WithCulture("en-US", () => Assert.Equal(4.5, Sut().GetCapacity()));
        }

        [Theory]
        [InlineData("0.0001")]   // < 1/60 giờ -> capacityMinutes = 0 -> GenerateSchedule treo
        [InlineData("0")]
        [InlineData("-5")]
        public void GetCapacity_GiaTriQuaNho_BiKepLenSanToiThieu(string noiDung)
        {
            // WP-4 §3.1: capacityHours < 1/60 làm (int)(h*60) = 0, spaceLeft = 0, chunk = 0,
            // remainingMinutes không bao giờ giảm -> GenerateSchedule lặp vô hạn, nhồi
            // ScheduleDay tới khi hết RAM. WorkloadBalancerViewModel đọc GetCapacity() rồi
            // gọi thẳng GenerateSchedule trong constructor, TRƯỚC khi slider (Minimum="1")
            // kịp kẹp giá trị — nên file là đường vào thật sự và phải chặn ngay tại đây.
            GivenFile(noiDung);

            WithCulture("en-US", () => Assert.Equal(1.0, Sut().GetCapacity()));
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        [InlineData("1e400")]      // tràn double -> +Infinity (.NET Core trả true, không fail)
        public void GetCapacity_GiaTriKhongHuuHan_VanTraVeSoDungDuoc(string noiDung)
        {
            // NumberStyles.Float vẫn nhận "NaN"/"Infinity", và Math.Max(NaN, 1.0) = NaN chứ
            // KHÔNG chọn toán hạng khác NaN — nên sàn tối thiểu một mình không chặn được.
            // Lọt xuống GenerateSchedule thì (int)(NaN * 60) = 0 -> đúng lại lỗi treo vô hạn
            // mà sàn này sinh ra để chặn; còn Infinity thì cast ra int.MinValue, spaceLeft âm,
            // remainingMinutes TĂNG mỗi vòng. Hậu điều kiện phải là: luôn hữu hạn và >= sàn.
            GivenFile(noiDung);

            double val = Sut().GetCapacity();

            Assert.True(double.IsFinite(val), $"GetCapacity trả về {val} cho input {noiDung}");
            Assert.True(val >= 1.0, $"GetCapacity trả về {val} cho input {noiDung}");
        }

        [Fact]
        public void SaveCapacity_LuonGhiDauCham_KeCaTrenCultureVietNam()
        {
            WithCulture("vi-VN", () => Sut().SaveCapacity(4.5));

            Assert.Equal("4.5", File.ReadAllText(CapacityFile));
        }

        [Fact]
        public void Capacity_GhiTrenViVN_DocLaiTrenEnUS_VanRaDungGiaTri()
        {
            // Đây là kịch bản mà bug gốc phá: ghi một máy, đọc một máy khác locale.
            WithCulture("vi-VN", () => Sut().SaveCapacity(4.5));
            WithCulture("en-US", () => Assert.Equal(4.5, Sut().GetCapacity()));
        }
    }
}
