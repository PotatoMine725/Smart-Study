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
    public sealed class SqliteMonHocRepository : IMonHocRepository
    {
        private readonly Func<AppDbContext> _ctxFactory;

        public SqliteMonHocRepository(Func<AppDbContext> ctxFactory)
        {
            _ctxFactory = ctxFactory;
        }

        public async Task<MonHoc?> GetAsync(Guid maMonHoc, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.MonHocs
                .Include(m => m.DanhSachTask)
                .FirstOrDefaultAsync(m => m.MaMonHoc == maMonHoc, ct);
        }

        public async Task<List<MonHoc>> GetByHocKyAsync(Guid maHocKy, CancellationToken ct = default)
        {
            using var db = _ctxFactory();
            return await db.MonHocs
                .Where(m => m.MaHocKy == maHocKy)
                .Include(m => m.DanhSachTask)
                .ToListAsync(ct);
        }
    }
}
