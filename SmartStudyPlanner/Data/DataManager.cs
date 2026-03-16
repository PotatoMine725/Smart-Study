using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    public static class DataManager
    {
        public static void LuuHocKy(HocKy hocKy)
        {
            if (hocKy == null) return;

            using (var db = new AppDbContext())
            {
                // 1. Tìm xem Học kỳ này đã có trong Database chưa (Nhớ phải lôi cả Môn và Task lên)
                var hocKyCu = db.HocKys
                    .Include(h => h.DanhSachMonHoc)
                    .ThenInclude(m => m.DanhSachTask)
                    .FirstOrDefault(h => h.MaHocKy == hocKy.MaHocKy);

                if (hocKyCu == null)
                {
                    // 2A. Nếu chưa có (Tạo mới ở màn hình Setup) -> Insert thẳng vào DB
                    db.HocKys.Add(hocKy);
                }
                else
                {
                    // 2B. Nếu đã có -> XÓA SẠCH CÁI CŨ ĐI
                    db.HocKys.Remove(hocKyCu);
                    db.SaveChanges(); // Chốt lệnh xóa để CSDL dọn dẹp chỗ trống

                    // ĐẮP NGUYÊN CÁI MỚI VÀO
                    db.HocKys.Add(hocKy);
                }

                // 3. Chốt lưu xuống file .db
                db.SaveChanges();
            }
        }

        public static HocKy DocHocKy()
        {
            using (var db = new AppDbContext())
            {
                // Truy vấn lôi toàn bộ cây dữ liệu lên
                return db.HocKys
                         .Include(hk => hk.DanhSachMonHoc)
                            .ThenInclude(mon => mon.DanhSachTask)
                         .FirstOrDefault();
            }
        }
    }
}