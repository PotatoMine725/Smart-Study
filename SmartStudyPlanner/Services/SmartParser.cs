using SmartStudyPlanner.Models;
using System;

namespace SmartStudyPlanner.Services
{
    public static class SmartParser
    {
        // Hàm này nhận vào chuỗi và trả ra 1 bộ 4 thông tin: Tên, Hạn chót, Loại, Độ khó
        public static (string TenTask, DateTime HanChot, LoaiCongViec Loai, int DoKho) Parse(string input)
        {
            string lowerInput = input.ToLower();

            // 1. GÁN GIÁ TRỊ MẶC ĐỊNH THÔNG MINH (Nếu không đoán được thì dùng cái này)
            string tenTask = input; // Giữ nguyên chuỗi gốc làm tên
            DateTime hanChot = DateTime.Now.AddDays(1); // Mặc định là ngày mai
            LoaiCongViec loai = LoaiCongViec.BaiTapVeNha; // Mặc định là bài tập
            int doKho = 3; // Mặc định độ khó trung bình

            // 2. BẮT TỪ KHÓA TÌM LOẠI TASK (Chấp nhận viết tắt)
            if (lowerInput.Contains("giữa kỳ") || lowerInput.Contains("giua ky") || lowerInput.Contains("gk"))
                loai = LoaiCongViec.ThiGiuaKy;
            else if (lowerInput.Contains("cuối kỳ") || lowerInput.Contains("cuoi ky") || lowerInput.Contains("ck"))
                loai = LoaiCongViec.ThiCuoiKy;
            else if (lowerInput.Contains("đồ án") || lowerInput.Contains("do an") || lowerInput.Contains("project") || lowerInput.Contains("bài tập lớn") || lowerInput.Contains("btl"))
                loai = LoaiCongViec.DoAnCuoiKy;
            else if (lowerInput.Contains("kiểm tra") || lowerInput.Contains("test") || lowerInput.Contains("15p") || lowerInput.Contains("1 tiết"))
                loai = LoaiCongViec.KiemTraThuongXuyen;

            // 3. BẮT TỪ KHÓA TÌM ĐỘ KHÓ
            if (lowerInput.Contains("khó") || lowerInput.Contains("kho") || lowerInput.Contains("căng") || lowerInput.Contains("chết"))
                doKho = 5;
            else if (lowerInput.Contains("dễ") || lowerInput.Contains("de") || lowerInput.Contains("chill") || lowerInput.Contains("nhàn") || lowerInput.Contains("ez"))
                doKho = 1;

            // 4. BẮT TỪ KHÓA TÌM NGÀY (Suy luận tương đối)
            if (lowerInput.Contains("hôm nay") || lowerInput.Contains("nay") || lowerInput.Contains("tối nay"))
                hanChot = DateTime.Now;
            else if (lowerInput.Contains("ngày mai") || lowerInput.Contains("mai"))
                hanChot = DateTime.Now.AddDays(1);
            else if (lowerInput.Contains("ngày mốt") || lowerInput.Contains("mốt"))
                hanChot = DateTime.Now.AddDays(2);
            else if (lowerInput.Contains("tuần sau") || lowerInput.Contains("tuan sau"))
                hanChot = DateTime.Now.AddDays(7);

            // Xử lý "Thứ X tuần sau" (Ví dụ: "thứ 6 tuần sau kiểm tra")
            if (lowerInput.Contains("thứ 2") || lowerInput.Contains("t2")) hanChot = LayNgayCuaThu(DayOfWeek.Monday, lowerInput.Contains("tuần sau"));
            else if (lowerInput.Contains("thứ 3") || lowerInput.Contains("t3")) hanChot = LayNgayCuaThu(DayOfWeek.Tuesday, lowerInput.Contains("tuần sau"));
            else if (lowerInput.Contains("thứ 4") || lowerInput.Contains("t4")) hanChot = LayNgayCuaThu(DayOfWeek.Wednesday, lowerInput.Contains("tuần sau"));
            else if (lowerInput.Contains("thứ 5") || lowerInput.Contains("t5")) hanChot = LayNgayCuaThu(DayOfWeek.Thursday, lowerInput.Contains("tuần sau"));
            else if (lowerInput.Contains("thứ 6") || lowerInput.Contains("t6")) hanChot = LayNgayCuaThu(DayOfWeek.Friday, lowerInput.Contains("tuần sau"));
            else if (lowerInput.Contains("thứ 7") || lowerInput.Contains("t7")) hanChot = LayNgayCuaThu(DayOfWeek.Saturday, lowerInput.Contains("tuần sau"));
            else if (lowerInput.Contains("chủ nhật") || lowerInput.Contains("cn")) hanChot = LayNgayCuaThu(DayOfWeek.Sunday, lowerInput.Contains("tuần sau"));

            return (tenTask, hanChot, loai, doKho);
        }

        // Hàm phụ trợ tính ngày cho các Thứ trong tuần
        private static DateTime LayNgayCuaThu(DayOfWeek thuCanTim, bool laTuanSau)
        {
            DateTime homNay = DateTime.Now;
            int daysUntil = ((int)thuCanTim - (int)homNay.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7; // Nếu hnay thứ 3 mà bảo "thứ 3" thì là thứ 3 tuần sau

            DateTime ngayTimDuoc = homNay.AddDays(daysUntil);
            if (laTuanSau) ngayTimDuoc = ngayTimDuoc.AddDays(7);

            return ngayTimDuoc;
        }
    }
}