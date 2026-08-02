using System;
using System.IO;

namespace SmartStudyPlanner.Services.ML
{
    /// <summary>
    /// Danh tính thiết bị bền vững, dùng cho D-I sync metadata (ModifiedByDeviceId).
    ///
    /// DeviceHelper.GetId() dẫn xuất từ Environment.MachineName: ổn định chừng nào
    /// hostname không đổi, và trùng nhau giữa 2 máy cùng hostname. Với LAN sync thì
    /// cả hai đều không chấp nhận được.
    ///
    /// Lần đầu chạy, giá trị được SEED từ DeviceHelper.GetId() — không random — để
    /// những row đã stamp trong DB của install cũ vẫn thuộc về đúng thiết bị này.
    /// </summary>
    public sealed class DeviceIdentity
    {
        private const string FileName = "device-id.txt";

        private readonly string _directory;
        private string? _cached;

        public DeviceIdentity(string? directory = null)
        {
            _directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner");
        }

        public string GetId()
        {
            if (!string.IsNullOrWhiteSpace(_cached)) return _cached!;

            var path = Path.Combine(_directory, FileName);

            try
            {
                if (File.Exists(path))
                {
                    var existing = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(existing))
                        return _cached = existing;
                }

                var seeded = DeviceHelper.GetId();
                Directory.CreateDirectory(_directory);
                File.WriteAllText(path, seeded);
                return _cached = seeded;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Không đọc/ghi được thì vẫn phải trả về danh tính dùng được — hành vi
                // suy biến đúng bằng hành vi cũ, không bao giờ chặn app.
                return _cached = DeviceHelper.GetId();
            }
        }
    }
}
