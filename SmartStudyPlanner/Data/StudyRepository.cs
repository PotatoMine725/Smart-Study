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
                // BẢO MẬT 1: Bật Transaction. Nếu đang lưu mà cúp điện hoặc crash app, 
                // Database sẽ tự động hoàn tác (Rollback), dữ liệu cũ không bao giờ bị mất!
                using (var transaction = await db.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Kéo toàn bộ rễ của Học kỳ cũ lên
                        var hocKyCu = await db.HocKys
                            .Include(h => h.DanhSachMonHoc)
                            .ThenInclude(m => m.DanhSachTask)
                            .FirstOrDefaultAsync(h => h.MaHocKy == hocKy.MaHocKy);

                        // 2. Nếu có cũ -> Xóa sạch bách khỏi CSDL
                        if (hocKyCu != null)
                        {
                            db.HocKys.Remove(hocKyCu);
                            await db.SaveChangesAsync();
                        }

                        // BẢO MẬT 2: CHÌA KHÓA DIỆT LỖI TRACKING!
                        // Xóa sạch trí nhớ của EF Core để nó quên đi cái hocKyCu vừa bị xóa,
                        // dọn đường sạch sẽ để đón hocKy mới vào mà không bị "đụng" ID.
                        db.ChangeTracker.Clear();

                        // 3. Đắp nguyên cái ba-lô dữ liệu mới vào
                        db.HocKys.Add(hocKy);
                        await db.SaveChangesAsync();

                        // 4. Chốt giao dịch, ghi thẳng vào ổ cứng
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        // Gặp biến -> Quay xe!
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
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

        public async Task AddStudyLogAsync(StudyLog log)
        {
            using var db = new AppDbContext();
            db.StudyLogs.Add(log);
            await db.SaveChangesAsync();
        }

        public async Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy)
        {
            using var db = new AppDbContext();
            var taskIds = hocKy.DanhSachMonHoc
                .SelectMany(m => m.DanhSachTask)
                .Select(t => t.MaTask)
                .ToHashSet();
            return await db.StudyLogs
                .Where(l => taskIds.Contains(l.MaTask))
                .OrderBy(l => l.NgayHoc)
                .ToListAsync();
        }
    }
}