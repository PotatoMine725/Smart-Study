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
                // SỨC MẠNH THẬT SỰ CỦA EF CORE:
                // Lệnh Update() thông minh này sẽ tự động duyệt qua toàn bộ Học Kỳ, Môn, Task.
                // Cái nào mới -> tự phát hiện và INSERT. Cái nào cũ -> tự UPDATE.
                // Ta KHÔNG CẦN phải Xóa-rồi-thêm như DataManager cũ nữa!
                db.HocKys.Update(hocKy);

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