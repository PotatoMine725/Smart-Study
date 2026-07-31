using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence
{
    public class SoftDeleteReadPathTests
    {
        // Sao nguyên từ RepositoriesTests.cs:30-37 — helper ở đó là private static.
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated done */ }
            return (conn, () => TestDb.Create(conn));
        }

        [Fact]
        public async Task GetForHocKyAsync_BoQuaLogDaTombstone()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            var hocKy  = new HocKy("HK Soft", DateTime.Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task   = new StudyTask("Bài 1", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
                MaMonHoc = monHoc.MaMonHoc,
            };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await new SqliteHocKyRepository(factory).LuuHocKyAsync(hocKy);

            var logs = new SqliteStudyLogRepository(factory);
            await logs.AddAsync(new StudyLog { MaTask = task.MaTask, NgayHoc = DateTime.Today, SoPhutHoc = 60 });
            await logs.AddAsync(new StudyLog
            {
                MaTask = task.MaTask, NgayHoc = DateTime.Today, SoPhutHoc = 999,
                IsDeleted = true, DeletedAtUtc = DateTime.UtcNow,
            });

            var result = await logs.GetForHocKyAsync(hocKy);

            Assert.Single(result);
            Assert.Equal(60, result[0].SoPhutHoc);
            Assert.DoesNotContain(result, l => l.IsDeleted);
        }

        [Fact]
        public async Task GetSnapshotAsync_KhongDemTaskDaTombstone()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            var hocKy  = new HocKy("HK Stats", DateTime.Today);
            var monHoc = new MonHoc("Lý", 3) { MaHocKy = hocKy.MaHocKy };
            var alive = new StudyTask("Còn sống", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn", MaMonHoc = monHoc.MaMonHoc,
            };
            var dead = new StudyTask("Đã xoá", DateTime.Today.AddDays(-5), LoaiCongViec.ThiCuoiKy, 5)
            {
                MucDoCanhBao = "An toàn", MaMonHoc = monHoc.MaMonHoc,
                IsDeleted = true, DeletedAtUtc = DateTime.UtcNow,
            };
            monHoc.DanhSachTask.Add(alive);
            monHoc.DanhSachTask.Add(dead);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await new SqliteHocKyRepository(factory).LuuHocKyAsync(hocKy);

            var snapshot = await new SqliteUserStatsRepository(factory)
                .GetSnapshotAsync(DateTime.UtcNow);

            // Task đã tombstone không được đếm vào tổng, và cũng không được tính là quá hạn.
            Assert.Equal(1, snapshot.TotalTaskCount);
            Assert.Equal(0, snapshot.OverdueTaskCount);
        }
    }
}
