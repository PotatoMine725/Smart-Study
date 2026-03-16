using System.Linq;
using Microsoft.EntityFrameworkCore; // Thư viện cực kỳ quan trọng để dùng .Include()
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    public static class DataManager
    {
        // HÀM 1: LƯU DỮ LIỆU BẰNG SQLITE
        public static void LuuHocKy(HocKy hocKy)
        {
            if (hocKy == null) return;

            using (var db = new AppDbContext())
            {
                // Phép màu của EF Core: 
                // Lệnh Update này sẽ tự động phân tích cả cái "ba lô" HocKy.
                // Cái nào mới -> Nó lệnh cho CSDL Insert.
                // Cái nào bị đổi tên -> Nó lệnh cho CSDL Update.
                db.HocKys.Update(hocKy);

                // Chốt lưu xuống file .db
                db.SaveChanges();
            }
        }

        // HÀM 2: ĐỌC DỮ LIỆU TỪ SQLITE
        public static HocKy DocHocKy()
        {
            using (var db = new AppDbContext())
            {
                // Truy vấn bằng LINQ thay vì đọc file Text:
                // Lấy Học Kỳ đầu tiên tìm thấy trong Database
                // .Include: Tự động móc nối để kéo luôn các Môn Học của học kỳ đó ra
                // .ThenInclude: Từ Môn Học kéo luôn các Bài Tập bên trong ra
                return db.HocKys
                         .Include(hk => hk.DanhSachMonHoc)
                            .ThenInclude(mon => mon.DanhSachTask)
                         .FirstOrDefault();
            }
        }
    }
}