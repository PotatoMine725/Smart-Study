using System;
using System.IO;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    public class DeviceIdentityTests : IDisposable
    {
        private readonly string _dir =
            Path.Combine(Path.GetTempPath(), "ssp-devid-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void LanDauTien_SeedTuDeviceHelper_VaGhiXuongFile()
        {
            var id = new DeviceIdentity(_dir).GetId();

            // Install cũ đã stamp hàng loạt row bằng giá trị dẫn xuất; seed phải trùng
            // để những row đó không đột nhiên thuộc về một "peer" khác.
            Assert.Equal(DeviceHelper.GetId(), id);

            // Và phải THỰC SỰ persist — nếu chỉ trả về giá trị dẫn xuất mà không ghi file
            // thì assert ở trên vẫn xanh trong khi class này chẳng làm gì cả.
            var file = Path.Combine(_dir, "device-id.txt");
            Assert.True(File.Exists(file));
            Assert.Equal(id, File.ReadAllText(file).Trim());
        }

        [Fact]
        public void LanThuHai_DocLaiTuFile_KhongPhuThuocMachineName()
        {
            var first = new DeviceIdentity(_dir).GetId();

            // Ghi đè bằng giá trị khác để chứng minh lần đọc sau lấy từ file,
            // không tính lại từ Environment.MachineName.
            File.WriteAllText(Path.Combine(_dir, "device-id.txt"), "desktop-deadbeef");

            Assert.Equal("desktop-deadbeef", new DeviceIdentity(_dir).GetId());
            Assert.NotEqual(first, new DeviceIdentity(_dir).GetId());
        }

        [Fact]
        public void FileRong_ThiSeedLai()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "device-id.txt"), "   ");

            Assert.Equal(DeviceHelper.GetId(), new DeviceIdentity(_dir).GetId());
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
    }
}
