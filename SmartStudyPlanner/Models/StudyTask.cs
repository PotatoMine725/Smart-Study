using System;

namespace SmartStudyPlanner.Models
{
    // 1. TẠO ENUM: Chuẩn hóa các loại sự kiện để người dùng không thể gõ sai chính tả
    public enum LoaiCongViec
    {
        BaiTapVeNha,
        KiemTraThuongXuyen,
        ThiGiuaKy,
        DoAnCuoiKy,
        ThiCuoiKy
    }

    public class StudyTask
    {
        public Guid MaTask { get; set; }
        public string TenTask { get; set; }
        public DateTime HanChot { get; set; }
        public string TrangThai { get; set; }

        // 2. THUỘC TÍNH MỚI: Phân loại sự kiện
        public LoaiCongViec LoaiTask { get; set; }

        // 3. THUỘC TÍNH MỚI: Điểm ưu tiên do DecisionEngine tính ra (Ta vẫn giữ lại nhưng sau này sẽ ép trọng số cao lên trong DecisionEngine)
        public double DiemUuTien { get; set; }

        // THUỘC TÍNH MỚI: Dùng để làm biển báo nhuộm màu giao diện
        public string MucDoCanhBao { get; set; }

        // Độ khó do người dùng nhập (Ta vẫn giữ lại nhưng sau này sẽ ép trọng số thấp xuống trong DecisionEngine)
        public int DoKho { get; set; }

        public StudyTask() { }

        public StudyTask(string tenTask, DateTime hanChot, LoaiCongViec loaiTask, int doKho)
        {
            MaTask = Guid.NewGuid();
            TenTask = tenTask;
            HanChot = hanChot;
            LoaiTask = loaiTask;
            DoKho = doKho;
            TrangThai = "Chưa làm";
        }

        // 3. HÀM QUY ĐỔI KHÁCH QUAN: DecisionEngine sẽ gọi hàm này để lấy Hệ số
        // Điểm quy đổi từ 0.1 đến 1.0 tùy theo mức độ ảnh hưởng đến điểm số môn học
        public double LayHeSoQuanTrong()
        {
            switch (LoaiTask)
            {
                case LoaiCongViec.ThiCuoiKy:
                    return 1.0; // Sống còn, quan trọng nhất
                case LoaiCongViec.DoAnCuoiKy:
                    return 0.8; // Rất quan trọng
                case LoaiCongViec.ThiGiuaKy:
                    return 0.6; // Quan trọng vừa
                case LoaiCongViec.KiemTraThuongXuyen:
                    return 0.3; // Ít quan trọng hơn
                case LoaiCongViec.BaiTapVeNha:
                    return 0.1; // Chỉ mang tính luyện tập
                default:
                    return 0.1;
            }
        }
    }
}