using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    // Class này ký hợp đồng với IStudyRepository
    public class StudyRepository : IStudyRepository
    {
        public async Task<HocKy> DocHocKyAsync()
        {
            using (var db = new AppDbContext())
            {
                // Dùng các hàm có chữ Async ở đuôi để luồng giao diện không bị đứng hình chờ đợi
                return await db.HocKys
                         .Include(hk => hk.DanhSachMonHoc)
                            .ThenInclude(mon => mon.DanhSachTask)
                         .FirstOrDefaultAsync();
            }
        }

        public async Task LuuHocKyAsync(HocKy hocKy)
        {
            if (hocKy == null) return;

            using (var db = new AppDbContext())
            {
                // 1. Kiểm tra xem Học kỳ này đã tồn tại trong CSDL chưa
                // (Dùng AsNoTracking() để DB chỉ nhìn lướt qua mà không "nhớ" nó, giúp tránh lỗi Tracking)
                var tonTai = await db.HocKys
                                     .AsNoTracking()
                                     .AnyAsync(h => h.MaHocKy == hocKy.MaHocKy);

                if (tonTai)
                {
                    // Đã có trong DB -> Tiến hành ghi đè (Update)
                    db.HocKys.Update(hocKy);
                }
                else
                {
                    // Chưa có trong DB -> Thêm mới hoàn toàn (Insert)
                    db.HocKys.Add(hocKy);
                }

                await db.SaveChangesAsync();
            }
        }

        public async Task<List<HocKy>> LayDanhSachHocKyAsync()
        {
            using (var db = new AppDbContext())
            {
                // Dùng ToListAsync() để lôi TOÀN BỘ học kỳ có trong DB lên
                return await db.HocKys
                         .Include(hk => hk.DanhSachMonHoc)
                            .ThenInclude(mon => mon.DanhSachTask)
                         .ToListAsync();
            }
        }
    }
}