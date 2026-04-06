using System;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services
{
    public class WeightConfig
    {
        public double TimeWeight { get; set; } = 0.40;
        public double TaskTypeWeight { get; set; } = 0.30;
        public double CreditWeight { get; set; } = 0.20;
        public double DifficultyWeight { get; set; } = 0.10;
        public int MaxCredits { get; set; } = 4;
        public int MaxDifficulty { get; set; } = 5;
        public int HorizonDays { get; set; } = 60;

        // BẢO MẬT 1: Kiểm tra tổng trọng số có bằng 1.0 (100%) hay không
        // Dùng sai số 0.001 vì phép cộng số thập phân (double) trong C# có thể bị lệch một chút
        public bool IsValid()
        {
            return Math.Abs(TimeWeight + TaskTypeWeight + CreditWeight + DifficultyWeight - 1.0) < 0.001;
        }
    }

    public static class DecisionEngine
    {
        public static WeightConfig Config { get; set; } = new WeightConfig();

        // BẢO MẬT 2: Hàm lấy hệ số được chuyển về đây (Tuân thủ Single Responsibility Principle)
        public static double LayHeSoQuanTrong(LoaiCongViec loaiTask)
        {
            switch (loaiTask)
            {
                case LoaiCongViec.ThiCuoiKy: return 1.0;
                case LoaiCongViec.DoAnCuoiKy: return 0.8;
                case LoaiCongViec.ThiGiuaKy: return 0.6;
                case LoaiCongViec.KiemTraThuongXuyen: return 0.3;
                case LoaiCongViec.BaiTapVeNha: return 0.1;
                default: return 0.1;
            }
        }

        public static double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            if (task == null || monHoc == null) return 0.0;

            // BẢO MẬT 3: Nếu config bị ai đó (hoặc người dùng) chỉnh sửa sai tổng số, 
            // tự động fallback (quay về) cấu hình mặc định an toàn để app không bị tính sai điểm.
            if (!Config.IsValid())
            {
                Config = new WeightConfig();
            }

            double soNgayConLai = (task.HanChot.Date - DateTime.Now.Date).TotalDays;

            if (soNgayConLai < -3) return 0.0;
            if (soNgayConLai < 0) return 100.0;
            if (soNgayConLai < 1) return 95.0;

            if (task.TrangThai == "Hoàn thành") return 0.0;
            if (soNgayConLai > Config.HorizonDays) return 1.0;

            double diemThoiGian = Math.Max(0, 100.0 * (1.0 - soNgayConLai / Config.HorizonDays));

            // CẬP NHẬT GỌI HÀM MỚI Ở ĐÂY
            double diemLoaiTask = LayHeSoQuanTrong(task.LoaiTask) * 100;

            int tinChiHopLe = Math.Max(1, monHoc.SoTinChi);
            double diemTinChi = (tinChiHopLe / (double)Config.MaxCredits) * 100;
            if (diemTinChi > 100) diemTinChi = 100;

            int doKhoHopLe = Math.Min(Config.MaxDifficulty, Math.Max(1, task.DoKho));
            double diemDoKho = (doKhoHopLe / (double)Config.MaxDifficulty) * 100;

            double finalPriority = (diemThoiGian * Config.TimeWeight) +
                                   (diemLoaiTask * Config.TaskTypeWeight) +
                                   (diemTinChi * Config.CreditWeight) +
                                   (diemDoKho * Config.DifficultyWeight);

            return Math.Round(finalPriority, 2);
        }

        // HÀM MỚI 1: Trả về con số phút thô (int) để vẽ biểu đồ
        public static int CalculateRawSuggestedMinutes(StudyTask task)
        {
            if (task.TrangThai == "Hoàn thành" || task.DiemUuTien <= 0) return 0;

            double baseMinutes = (task.DiemUuTien / 100.0) * 120.0;
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;

            int totalMinutes = (int)(baseMinutes + difficultyBonus);
            return (int)Math.Round(totalMinutes / 15.0) * 15;
        }

        // HÀM MỚI 2: Dùng lại hàm trên để format ra chuỗi chữ cho DataGrid
        public static string SuggestStudyTime(StudyTask task)
        {
            int totalMinutes = CalculateRawSuggestedMinutes(task);
            if (totalMinutes == 0) return "0 phút";

            // Trừ đi thời gian người dùng đã cày cuốc
            int remainingMinutes = totalMinutes - task.ThoiGianDaHoc;

            // Nếu đã học đủ hoặc dư thời gian
            if (remainingMinutes <= 0) return "Đã đạt mục tiêu 🎉";

            if (remainingMinutes < 60) return $"{remainingMinutes} phút";

            int hours = remainingMinutes / 60;
            int mins = remainingMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}p" : $"{hours}h";
        }
    }
}