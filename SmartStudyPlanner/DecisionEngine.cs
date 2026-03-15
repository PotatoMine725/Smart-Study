using System;

namespace SmartStudyPlanner
{
    public static class DecisionEngine
    {
        // Hàm này sẽ nhận vào 1 Task và Môn học của nó, sau đó trả về Điểm ưu tiên (0 - 100)
        public static double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            // Tính số ngày còn lại từ hôm nay đến hạn chót
            double soNgayConLai = (task.HanChot.Date - DateTime.Now.Date).TotalDays;

            // ==========================================
            // TẦNG 1: DICTATORSHIP MODULE (Mệnh lệnh độc tài)
            // ==========================================
            // 1. Nếu quá hạn HƠN 3 NGÀY (soNgayConLai < -3) -> Hết cứu, tự động ép về 0 điểm
            if (soNgayConLai < -3) return 0.0;

            // 2. Nếu quá hạn từ 1 đến 3 ngày (-3 <= soNgayConLai < 0) -> Báo động đỏ (100 điểm) bắt nộp bù
            if (soNgayConLai < 0) return 100.0;

            // 3. Hạn chót là hôm nay
            if (soNgayConLai == 0) return 95.0;

            // ==========================================
            // TẦNG 2: VETO MODULE (Quyền phủ quyết)
            // ==========================================
            // Bộ lọc loại bỏ các công việc không cần quan tâm.
            if (task.TrangThai == "Hoàn thành") return 0.0; // Đã làm xong -> Không thèm quan tâm nữa
            if (soNgayConLai > 60) return 1.0; // Hơn 2 tháng nữa mới nộp -> Quá xa, cho điểm chạm đáy

            // ==========================================
            // TẦNG 3: DEMOCRACY MODULE (Tổng hợp ý kiến / Dân chủ)
            // ==========================================
            // Nếu thoát được 2 tầng trên, các "chuyên gia" sẽ bắt đầu chấm điểm (thang 100)

            // 1. Chuyên gia Thời gian: Càng gần ngày nộp điểm càng cao. 
            // (Giả sử mốc 30 ngày là 0 điểm, 1 ngày là gần 100 điểm)
            double diemThoiGian = 100 - (soNgayConLai * (100.0 / 30.0));
            if (diemThoiGian < 0) diemThoiGian = 0;

            // 2. Chuyên gia Khách quan: Hệ số loại công việc (Thi cuối kỳ, Giữa kỳ...)
            double diemLoaiTask = task.LayHeSoQuanTrong() * 100;

            // 3. Chuyên gia Tín chỉ: Môn càng nhiều tín chỉ càng quan trọng (Giả sử Max 4 tín chỉ)
            double diemTinChi = (monHoc.SoTinChi / 4.0) * 100;
            if (diemTinChi > 100) diemTinChi = 100;

            // 4. Chuyên gia Chủ quan: Độ khó do sinh viên tự nhận định (Thang 1-5)
            double diemDoKho = (task.DoKho / 5.0) * 100;

            // TỔNG HỢP (Arithmetic Mean có trọng số):
            // Giao quyền quyết định: Thời gian (40%), Tính chất sự kiện (30%), Tín chỉ (20%), Độ khó chủ quan (10%)
            double finalPriority = (diemThoiGian * 0.4) +
                                   (diemLoaiTask * 0.3) +
                                   (diemTinChi * 0.2) +
                                   (diemDoKho * 0.1);

            return Math.Round(finalPriority, 2); // Làm tròn 2 chữ số thập phân cho đẹp
        }
    }
}