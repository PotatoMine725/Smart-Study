using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Tests.Fixtures
{
    /// <summary>Shared in-memory SQLite fixture helpers for repository/DB tests.</summary>
    internal static class TestDb
    {
        /// <summary>Creates an in-memory SQLite context with all tables created.</summary>
        public static AppDbContext Create(SqliteConnection conn)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(conn)
                .Options;
            var ctx = new AppDbContext(opts);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        public static SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            return conn;
        }

        /// <summary>Seeds a minimal task tree (HocKy → MonHoc → StudyTask) and returns the task.</summary>
        public static async Task<StudyTask> SeedTaskAsync(AppDbContext ctx)
        {
            var hocKy = new HocKy("HK Test", DateTime.Today);
            var monHoc = new MonHoc("MH Test", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Task A", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
            };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            ctx.HocKys.Add(hocKy);
            await ctx.SaveChangesAsync();
            return task;
        }
    }
}
