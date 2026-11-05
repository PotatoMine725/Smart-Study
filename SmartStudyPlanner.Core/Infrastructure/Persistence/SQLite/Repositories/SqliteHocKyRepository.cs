using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
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
                     .Where(hk => !hk.IsSeeded && !hk.IsDeleted)
                     .Include(hk => hk.DanhSachMonHoc.Where(mon => !mon.IsDeleted))
                        .ThenInclude(mon => mon.DanhSachTask.Where(t => !t.IsDeleted))
                     .ToListAsync(ct);

            // Khử trùng môn học clone: DB có thể chứa nhiều MonHoc cùng identity chuẩn hóa
            // (do seed/lưu lặp, hoặc chỉ khác hoa/thường/khoảng trắng) → giữ 1 bản đại diện
            // mỗi identity để UI + mọi consumer thấy danh sách sạch. Epic 1 / M1.3: key qua
            // MonHocIdentity.Normalize thay vì raw TenMonHoc — nhất quán pattern dedup toàn dự án.
            // GỘP task từ MỌI clone (SelectMany, distinct theo MaTask) vào bản đại diện trước
            // khi loại clone → KHÔNG mất task nằm ở clone không-đại-diện. Mỗi task gộp được
            // reparent (MaMonHoc = daiDien.MaMonHoc) để đồ thị trả về nhất quán FK-với-navigation —
            // LuuHocKyAsync's reconcile dựa vào đó để nhận diện đúng chủ sở hữu mới của task.
            // Lần LuuHocKyAsync kế tiếp ghi đè danh sách đã khử trùng → prune clone khỏi DB.
            foreach (var hocKy in danhSach)
            {
                var monDuyNhat = hocKy.DanhSachMonHoc
                    .GroupBy(mon => MonHocIdentity.Normalize(mon.TenMonHoc))
                    .Select(nhom =>
                    {
                        var daiDien = nhom.First();

                        var taskGop = nhom
                            .SelectMany(mon => mon.DanhSachTask)
                            .GroupBy(task => task.MaTask)
                            .Select(nhomTask => nhomTask.First())
                            .ToList();

                        foreach (var task in taskGop)
                            task.MaMonHoc = daiDien.MaMonHoc;

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

        // Epic 2 / T2.4 fence Slice 3 (OD-2 = L1): the reconcile lives in Mutations/ -- planner, writer,
        // executor. This port only delegates.
        public Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
            => new LocalSemesterSaveExecutor(_ctxFactory).ExecuteAsync(hocKy, ct);
    }
}
