using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests;
using Xunit;

namespace SmartStudyPlanner.Tests.DevTools
{
    [Trait("Category", "Seed")]
    public class DbSeedTests : IDisposable
    {
        private readonly SqliteConnection _conn;

        public DbSeedTests()
        {
            _conn = TestDb.OpenConnection();
        }

        public void Dispose() => _conn.Dispose();

        [Fact]
        public async Task Seed_180StudyLogs_ForMlPipelineVerification()
        {
            await using var db = TestDb.Create(_conn);

            // Step B: create seed HocKy
            var hocKy = new HocKy("Học Kỳ Dev Seed", DateTime.Today.AddDays(-90)) { IsSeeded = true };
            db.HocKys.Add(hocKy);
            await db.SaveChangesAsync();

            // Step C: create 2 MonHoc — one light (2 credits), one heavy (4 credits)
            var monNhe  = new MonHoc("Toán Rời Rạc", 2)          { MaHocKy = hocKy.MaHocKy };
            var monNang = new MonHoc("Lập Trình Nâng Cao", 4)    { MaHocKy = hocKy.MaHocKy };
            db.MonHocs.AddRange(monNhe, monNang);
            await db.SaveChangesAsync();

            // Step D: create 10 StudyTasks — 5 per MonHoc, DoKho spread 1–5
            var tasks = new List<StudyTask>();
            var loaiValues = (LoaiCongViec[])Enum.GetValues(typeof(LoaiCongViec));

            foreach (var (mon, baseDoKho) in new[] { (monNhe, 1), (monNang, 3) })
            {
                for (int i = 0; i < 5; i++)
                {
                    int doKho = Math.Min(baseDoKho + i, 5);
                    tasks.Add(new StudyTask(
                        tenTask: $"Task {mon.TenMonHoc} K{doKho}",
                        hanChot: DateTime.Today.AddDays(30),
                        loaiTask: loaiValues[i % loaiValues.Length],
                        doKho: doKho)
                    {
                        MaMonHoc = mon.MaMonHoc,
                        MucDoCanhBao = "An toàn",
                    });
                }
            }
            db.StudyTasks.AddRange(tasks);
            await db.SaveChangesAsync();

            // Step E: generate 180 StudyLogs across 3 groups
            var rng = new Random(42);   // fixed seed → reproducible

            var tasksLight  = tasks.Where(t => t.DoKho <= 2).ToList();
            var tasksMedium = tasks.Where(t => t.DoKho == 3).ToList();
            var tasksHeavy  = tasks.Where(t => t.DoKho >= 4).ToList();

            var logs = new List<StudyLog>();

            void AddGroup(List<StudyTask> groupTasks, int count, float minMin, float maxMin)
            {
                for (int i = 0; i < count; i++)
                {
                    float noise   = 1f + (rng.NextSingle() - 0.5f) * 0.3f;      // ±15%
                    int soPhut    = (int)Math.Max(10,
                        (minMin + rng.NextSingle() * (maxMin - minMin)) * noise);
                    var task      = groupTasks[i % groupTasks.Count];
                    int daysAgo   = rng.Next(1, 61);

                    logs.Add(new StudyLog
                    {
                        MaTask        = task.MaTask,
                        NgayHoc       = DateTime.Today.AddDays(-daysAgo),
                        SoPhutHoc     = soPhut,
                        SoPhutDuKien  = Math.Max(10, soPhut + rng.Next(-10, 10)),
                        DaHoanThanh   = true,
                        CreatedAtUtc  = DateTime.UtcNow.AddDays(-daysAgo),
                        DeviceId      = "desktop-seed-dev",
                        IsDeleted     = false,
                    });
                }
            }

            AddGroup(tasksLight,  60, 20f,  60f);   // 20–60 min (light)
            AddGroup(tasksMedium, 60, 60f,  120f);  // 60–120 min (medium)
            AddGroup(tasksHeavy,  60, 120f, 240f);  // 120–240 min (heavy)

            db.StudyLogs.AddRange(logs);
            await db.SaveChangesAsync();

            // Step F: verify counts
            var logCount  = await db.StudyLogs.CountAsync();
            var taskCount = await db.StudyTasks.CountAsync();

            Assert.True(logCount  >= 180, $"Expected >= 180 StudyLogs, got {logCount}");
            Assert.True(taskCount >= 10,  $"Expected >= 10 StudyTasks, got {taskCount}");
        }
    }
}
