using SmartStudyPlanner.Models;
using System.IO;
using System.Text.Json;

namespace SmartStudyPlanner.Data
{
    public static class DataManager
    {
        // Đường dẫn tới file lưu trữ (nằm ngay trong thư mục chạy của app)
        private static readonly string filePath = "dulieu_hocky.json";

        // HÀM 1: LƯU DỮ LIỆU
        public static void LuuHocKy(HocKy hocKy)
        {
            // Tùy chọn này giúp file Json sinh ra được thụt lề đẹp mắt dễ đọc
            var options = new JsonSerializerOptions { WriteIndented = true };

            // Đóng gói Object thành chuỗi Json
            string jsonString = JsonSerializer.Serialize(hocKy, options);

            // Ghi ra file
            File.WriteAllText(filePath, jsonString);
        }

        // HÀM 2: ĐỌC DỮ LIỆU
        public static HocKy DocHocKy()
        {
            // Nếu chưa từng lưu file nào thì trả về null
            if (!File.Exists(filePath))
            {
                return null;
            }

            // Đọc chuỗi Json từ file
            string jsonString = File.ReadAllText(filePath);

            // Giải nén Json thành Object
            return JsonSerializer.Deserialize<HocKy>(jsonString);
        }
    }
}