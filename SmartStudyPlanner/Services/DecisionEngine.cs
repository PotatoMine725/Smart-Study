using System;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services
{
    // CẢI TIẾN 1: Tách các hệ số ra thành Object Cấu hình (Externalise Config)
    public class WeightConfig
    {
        public double TimeWeight { get; set; } = 0.40;
        public double TaskTypeWeight { get; set; } = 0.30;
        public double CreditWeight { get; set; } = 0.20;
        public double DifficultyWeight { get; set; } = 0.10;
        public int MaxCredits { get; set; } = 4;
        public int MaxDifficulty { get; set; } = 5;
        public int HorizonDays { get; set; } = 60;
    }

    public static class DecisionEngine
    {
        public static WeightConfig Config { get; set; } = new WeightConfig();

        public static double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            // CẢI TIẾN 2: Thêm Guards chống lỗi Null (Null validation)
            if (task == null || monHoc == null) return 0.0;

            double soNgayConLai = (task.HanChot.Date - DateTime.Now.Date).TotalDays;

            // ==========================================
            // TẦNG 1: DICTATORSHIP MODULE 
            // ==========================================
            if (soNgayConLai < -3) return 0.0;
            if (soNgayConLai < 0) return 100.0;

            // CẢI TIẾN 3: Sửa lỗi Critical (Floating-point equality)
            if (soNgayConLai < 1) return 95.0;

            // ==========================================
            // TẦNG 2: VETO MODULE 
            // ==========================================
            if (task.TrangThai == "Hoàn thành") return 0.0;
            if (soNgayConLai > Config.HorizonDays) return 1.0;

            // ==========================================
            // TẦNG 3: DEMOCRACY MODULE 
            // ==========================================

            // CẢI TIẾN 4: Sửa lỗi High (Formula gap). Trải đều trên 60 ngày.
            double diemThoiGian = Math.Max(0, 100.0 * (1.0 - soNgayConLai / Config.HorizonDays));

            double diemLoaiTask = task.LayHeSoQuanTrong() * 100;

            int tinChiHopLe = Math.Max(1, monHoc.SoTinChi);
            double diemTinChi = (tinChiHopLe / (double)Config.MaxCredits) * 100;
            if (diemTinChi > 100) diemTinChi = 100;

            // CẢI TIẾN 5: Clamp giới hạn trần cho độ khó (Difficulty ceiling clamp)
            int doKhoHopLe = Math.Min(Config.MaxDifficulty, Math.Max(1, task.DoKho));
            double diemDoKho = (doKhoHopLe / (double)Config.MaxDifficulty) * 100;

            double finalPriority = (diemThoiGian * Config.TimeWeight) +
                                   (diemLoaiTask * Config.TaskTypeWeight) +
                                   (diemTinChi * Config.CreditWeight) +
                                   (diemDoKho * Config.DifficultyWeight);

            return Math.Round(finalPriority, 2);
        }

        // ==========================================
        // SMART SCHEDULING: GỢI Ý THỜI GIAN HỌC
        // ==========================================
        public static string SuggestStudyTime(StudyTask task)
        {
            if (task.TrangThai == "Hoàn thành" || task.DiemUuTien <= 0) return "0 phút";

            // Điểm ưu tiên càng cao, độ khó càng cao thì cần càng nhiều thời gian
            double baseMinutes = (task.DiemUuTien / 100.0) * 120.0; // Max 2 tiếng do áp lực deadline
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;     // Max 1 tiếng do độ khó môn học

            int totalMinutes = (int)(baseMinutes + difficultyBonus);

            // Làm tròn theo block 15 phút (Chuẩn phương pháp học Pomodoro)
            totalMinutes = (int)Math.Round(totalMinutes / 15.0) * 15;

            if (totalMinutes <= 0) return "15 phút";
            if (totalMinutes < 60) return $"{totalMinutes} phút";

            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}p" : $"{hours}h";
        }
    }
}