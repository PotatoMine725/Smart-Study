using System;
using System.Collections.Generic;
using System.Linq;
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
            var danhSach = await db.HocKys
                     .Where(hk => !hk.IsSeeded)
                     .Include(hk => hk.DanhSachMonHoc)
                        .ThenInclude(mon => mon.DanhSachTask)
                     .ToListAsync(ct);

            // Khử trùng môn học clone: DB có thể chứa nhiều MonHoc cùng TenMonHoc
            // (do seed/lưu lặp) → giữ 1 bản đại diện mỗi tên để UI + mọi consumer
            // thấy danh sách sạch. Nhất quán pattern GroupBy(TenMonHoc) toàn dự án.
            // GỘP task từ MỌI clone (SelectMany, distinct theo MaTask) vào bản đại
            // diện trước khi loại clone → KHÔNG mất task nằm ở clone không-đại-diện.
            // Lần LuuHocKyAsync kế tiếp ghi đè danh sách đã khử trùng → prune clone khỏi DB.
            foreach (var hocKy in danhSach)
            {
                var monDuyNhat = hocKy.DanhSachMonHoc
                    .GroupBy(mon => mon.TenMonHoc)
                    .Select(nhom =>
                    {
                        var daiDien = nhom.First();

                        var taskGop = nhom
                            .SelectMany(mon => mon.DanhSachTask)
                            .GroupBy(task => task.MaTask)
                            .Select(nhomTask => nhomTask.First())
                            .ToList();

                        if (taskGop.Count != daiDien.DanhSachTask.Count)
                        {
                            daiDien.DanhSachTask.Clear();
                            foreach (var task in taskGop)
                                daiDien.DanhSachTask.Add(task);
                        }

                        return daiDien;
                    })
                    .ToList();

                if (monDuyNhat.Count != hocKy.DanhSachMonHoc.Count)
                {
                    hocKy.DanhSachMonHoc.Clear();
                    foreach (var mon in monDuyNhat)
                        hocKy.DanhSachMonHoc.Add(mon);
                }
            }

            return danhSach;
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
