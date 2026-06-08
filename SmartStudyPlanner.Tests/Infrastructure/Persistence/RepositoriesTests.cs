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
    /// <summary>
    /// Integration tests cho các repository abstraction trong Slice 4.
    /// Dùng in-memory SQLite (shared connection) — pattern giống TaskNotesTests.
    /// </summary>
    public class RepositoriesTests
    {
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            // Tạo schema lần đầu — các call sau dùng cùng connection nên data persist.
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated done */ }
            return (conn, () => TestDb.Create(conn));
        }

        [Fact]
        public async Task StudyTaskRepository_AddGetUpdateDelete_RoundTrip()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            Guid maMonHoc;
            using (var ctx = TestDb.Create(conn))
            {
                var seeded = await TestDb.SeedTaskAsync(ctx);
                maMonHoc = seeded.MaMonHoc;
            }

            var repo = new SqliteStudyTaskRepository(factory);
            var task = new StudyTask("T1", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = maMonHoc,
                MucDoCanhBao = "An toàn",
            };
            await repo.AddAsync(task);

            var loaded = await repo.GetAsync(task.MaTask);
            Assert.NotNull(loaded);
            Assert.Equal("T1", loaded!.TenTask);

            loaded.TenTask = "T1-updated";
            await repo.UpdateAsync(loaded);

            var reload = await repo.GetAsync(task.MaTask);
            Assert.Equal("T1-updated", reload!.TenTask);

            await repo.DeleteAsync(task.MaTask);
            Assert.Null(await repo.GetAsync(task.MaTask));
        }

        [Fact]
        public async Task StudyLogRepository_AddGetByTaskAndSince()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteStudyLogRepository(factory);

            var maTask = Guid.NewGuid();
            var baseTime = new DateTime(2026, 5, 1, 8, 0, 0);

            await repo.AddAsync(new StudyLog { MaTask = maTask, NgayHoc = baseTime, SoPhutHoc = 30, CreatedAtUtc = baseTime });
            await repo.AddAsync(new StudyLog { MaTask = maTask, NgayHoc = baseTime.AddDays(1), SoPhutHoc = 45, CreatedAtUtc = baseTime.AddDays(1) });
            await repo.AddAsync(new StudyLog { MaTask = Guid.NewGuid(), NgayHoc = baseTime, SoPhutHoc = 10, CreatedAtUtc = baseTime });

            var byTask = await repo.GetByTaskAsync(maTask);
            Assert.Equal(2, byTask.Count);

            var sinceDay2 = await repo.GetSinceAsync(baseTime.AddDays(1));
            Assert.Single(sinceDay2);
            Assert.Equal(45, sinceDay2[0].SoPhutHoc);
        }

        [Fact]
        public async Task MonHocRepository_GetByHocKy_TraVeKemTasks()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            using (var ctx = TestDb.Create(conn))
            {
                await TestDb.SeedTaskAsync(ctx);
            }

            var repo = new SqliteMonHocRepository(factory);

            using var probe = TestDb.Create(conn);
            var hocKyId = (await probe.HocKys.FirstAsync()).MaHocKy;

            var monHocs = await repo.GetByHocKyAsync(hocKyId);
            Assert.Single(monHocs);
            Assert.Single(monHocs[0].DanhSachTask);
        }

        [Fact]
        public async Task UserStatsRepository_TinhMissRateVaStreakDung()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var reference = new DateTime(2026, 5, 18);

            using (var ctx = TestDb.Create(conn))
            {
                var hocKy = new HocKy("HK", DateTime.Today);
                var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
                hocKy.DanhSachMonHoc.Add(monHoc);
                ctx.HocKys.Add(hocKy);
                await ctx.SaveChangesAsync();
                var maMonHoc = monHoc.MaMonHoc;
                // 2 task hoàn thành, 1 task overdue, 1 task tương lai
                ctx.StudyTasks.AddRange(
                    new StudyTask { MaMonHoc = maMonHoc, TenTask = "done-on-time", HanChot = reference.AddDays(-5),
                        TrangThai = StudyTaskStatus.HoanThanh, NgayHoanThanh = reference.AddDays(-6), MucDoCanhBao = "x" },
                    new StudyTask { MaMonHoc = maMonHoc, TenTask = "done-late", HanChot = reference.AddDays(-10),
                        TrangThai = StudyTaskStatus.HoanThanh, NgayHoanThanh = reference.AddDays(-8), MucDoCanhBao = "x" },
                    new StudyTask { MaMonHoc = maMonHoc, TenTask = "overdue", HanChot = reference.AddDays(-2),
                        TrangThai = StudyTaskStatus.ChuaLam, MucDoCanhBao = "x" },
                    new StudyTask { MaMonHoc = maMonHoc, TenTask = "future", HanChot = reference.AddDays(5),
                        TrangThai = StudyTaskStatus.ChuaLam, MucDoCanhBao = "x" }
                );
                // Streak: 3 ngày liên tiếp tới reference
                ctx.StudyLogs.AddRange(
                    new StudyLog { MaTask = Guid.NewGuid(), NgayHoc = reference, SoPhutHoc = 30, CreatedAtUtc = reference },
                    new StudyLog { MaTask = Guid.NewGuid(), NgayHoc = reference.AddDays(-1), SoPhutHoc = 45, CreatedAtUtc = reference.AddDays(-1) },
                    new StudyLog { MaTask = Guid.NewGuid(), NgayHoc = reference.AddDays(-2), SoPhutHoc = 20, CreatedAtUtc = reference.AddDays(-2) }
                    // gap day -3 → streak dừng ở 3
                );
                await ctx.SaveChangesAsync();
            }

            var repo = new SqliteUserStatsRepository(factory);
            var snap = await repo.GetSnapshotAsync(reference);

            Assert.Equal(4, snap.TotalTaskCount);
            Assert.Equal(2, snap.CompletedTaskCount);
            Assert.Equal(1, snap.OverdueTaskCount);
            Assert.Equal(0.25, snap.MissRate);
            // done-late: 2 ngày trễ; done-on-time hoàn thành trước hạn → không tính vào avg
            Assert.Equal(2.0, snap.AverageDelayDays);
            Assert.Equal(3, snap.FocusStreakDays);
            Assert.Equal(95, snap.TotalStudyMinutesLast30Days);
        }

        [Fact]
        public async Task HocKyRepository_LuuVaLayDanhSach_RoundTripVaOverwrite()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteHocKyRepository(factory);

            var hocKy = new HocKy("HK1", DateTime.Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            monHoc.DanhSachTask.Add(new StudyTask("T1", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
            });
            hocKy.DanhSachMonHoc.Add(monHoc);

            await repo.LuuHocKyAsync(hocKy);

            var loaded = await repo.LayDanhSachHocKyAsync();
            Assert.Single(loaded);
            Assert.Single(loaded[0].DanhSachMonHoc);
            Assert.Single(loaded[0].DanhSachMonHoc[0].DanhSachTask);
            Assert.Equal("T1", loaded[0].DanhSachMonHoc[0].DanhSachTask[0].TenTask);

            // Overwrite cùng MaHocKy: thêm task thứ 2 → không nhân đôi học kỳ, dữ liệu mới đầy đủ.
            hocKy.DanhSachMonHoc[0].DanhSachTask.Add(new StudyTask("T2", DateTime.Today.AddDays(5), LoaiCongViec.ThiGiuaKy, 3)
            {
                MaMonHoc = monHoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
            });
            await repo.LuuHocKyAsync(hocKy);

            var reloaded = await repo.LayDanhSachHocKyAsync();
            Assert.Single(reloaded); // không nhân đôi học kỳ
            Assert.Equal(2, reloaded[0].DanhSachMonHoc[0].DanhSachTask.Count);
        }

        [Fact]
        public async Task TaskEditorRepository_NoteUpsert_VaLinkCrud()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            Guid maTask;
            using (var ctx = TestDb.Create(conn))
            {
                var seeded = await TestDb.SeedTaskAsync(ctx);
                maTask = seeded.MaTask;
            }

            var repo = new SqliteTaskEditorRepository(factory);

            // Note: insert rồi update qua cùng entry point.
            await repo.UpsertNoteAsync(maTask, "ghi chú 1");
            await repo.UpsertNoteAsync(maTask, "ghi chú 2");
            var bundle = await repo.GetBundleAsync(maTask);
            Assert.NotNull(bundle);
            Assert.Equal("ghi chú 2", bundle!.Note!.Content);

            // Link CRUD.
            var link = new TaskReferenceLink { MaTask = maTask, Title = "Docs", Url = "https://x", SortOrder = 0 };
            await repo.AddLinkAsync(link);
            Assert.Single(await repo.GetLinksAsync(maTask));

            link.Title = "Docs v2";
            await repo.UpdateLinkAsync(link);
            var afterUpdate = await repo.GetLinksAsync(maTask);
            Assert.Equal("Docs v2", afterUpdate[0].Title);

            await repo.DeleteLinkAsync(link.Id);
            Assert.Empty(await repo.GetLinksAsync(maTask));
        }
    }
}
