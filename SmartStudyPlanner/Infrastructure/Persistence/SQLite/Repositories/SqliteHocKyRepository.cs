using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories
{
    public sealed class SqliteHocKyRepository : IHocKyRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteHocKyRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            // Dùng ToListAsync() để lôi TOÀN BỘ học kỳ có trong DB lên
            return await db.HocKys
                     .Include(hk => hk.DanhSachMonHoc)
                        .ThenInclude(mon => mon.DanhSachTask)
                     .ToListAsync(ct);
        }

        public async Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
        {
            if (hocKy == null) return;

            // ⚠️ Thân transaction được copy NGUYÊN VĂN từ StudyRepository.LuuHocKyAsync
            //    (logic chống mất dữ liệu — "BẢO MẬT 1/2"). Chỉ đổi `new AppDbContext()` → `_ctxFactory()`.
            using (var db = _ctxFactory())
            {
                // BẢO MẬT 1: Bật Transaction. Nếu đang lưu mà cúp điện hoặc crash app,
                // Database sẽ tự động hoàn tác (Rollback), dữ liệu cũ không bao giờ bị mất!
                using (var transaction = await db.Database.BeginTransactionAsync(ct))
                {
                    try
                    {
                        // 1. Kéo toàn bộ rễ của Học kỳ cũ lên
                        var hocKyCu = await db.HocKys
                            .Include(h => h.DanhSachMonHoc)
                            .ThenInclude(m => m.DanhSachTask)
                            .FirstOrDefaultAsync(h => h.MaHocKy == hocKy.MaHocKy, ct);

                        // 2. Nếu có cũ -> Xóa sạch bách khỏi CSDL
                        if (hocKyCu != null)
                        {
                            db.HocKys.Remove(hocKyCu);
                            await db.SaveChangesAsync(ct);
                        }

                        // BẢO MẬT 2: CHÌA KHÓA DIỆT LỖI TRACKING!
                        // Xóa sạch trí nhớ của EF Core để nó quên đi cái hocKyCu vừa bị xóa,
                        // dọn đường sạch sẽ để đón hocKy mới vào mà không bị "đụng" ID.
                        db.ChangeTracker.Clear();

                        // 3. Đắp nguyên cái ba-lô dữ liệu mới vào
                        db.HocKys.Add(hocKy);
                        await db.SaveChangesAsync(ct);

                        // 4. Chốt giao dịch, ghi thẳng vào ổ cứng
                        await transaction.CommitAsync(ct);
                    }
                    catch (Exception)
                    {
                        // Gặp biến -> Quay xe!
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
                }
            }
        }
    }
}
