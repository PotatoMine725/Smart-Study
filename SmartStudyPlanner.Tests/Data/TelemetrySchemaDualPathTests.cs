using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Gate #4 — dual-path schema. Trên một DB tạo TRƯỚC M8 (thiếu 2 bảng telemetry),
    /// <c>EnsureCreated()</c> là no-op, nên app phải vá bằng <see cref="TelemetrySchema.EnsureTables"/>
    /// (CREATE TABLE IF NOT EXISTS). Mô phỏng pre-M8 = EnsureCreated full schema rồi DROP 2 bảng,
    /// sau đó chạy đúng seam production và round-trip qua repo thật để chứng minh bảng vá ra
    /// tương thích với entity/insert path. Đây là phần plan tự gọi là "TRAP", trước đây chỉ
    /// verify bằng đọc code — test này smoke nó empirically.
    /// </summary>
    public class TelemetrySchemaDualPathTests
    {
        private static int TableCount(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
            cmd.Parameters.AddWithValue("$n", table);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        [Fact]
        public async Task EnsureTables_OnPreM8Db_RecreatesTablesAndRoundTrips()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;

            // 1) DB "đời mới": EnsureCreated dựng đủ schema, gồm cả 2 bảng telemetry.
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated */ }

            // 2) Hạ cấp thành DB pre-M8: bỏ 2 bảng telemetry như thể DB tạo trước M8.
            using (var downgrade = TestDb.Create(conn))
            {
                downgrade.Database.ExecuteSqlRaw("DROP TABLE DifficultyLabelLogs");
                downgrade.Database.ExecuteSqlRaw("DROP TABLE WeightChangeLogs");
            }

            // 3) Sanity: bảng đã thiếu THẬT và EnsureCreated KHÔNG tự vá (no-op trên DB đã tồn tại).
            //    Nếu assert này sai thì kịch bản pre-M8 không được tái hiện và test sẽ vô nghĩa.
            using (var afterEnsureCreated = TestDb.Create(conn)) { /* EnsureCreated lần 2 = no-op */ }
            Assert.Equal(0, TableCount(conn, "DifficultyLabelLogs"));
            Assert.Equal(0, TableCount(conn, "WeightChangeLogs"));

            // 4) Chạy đúng seam production → hai bảng phải xuất hiện trở lại.
            using (var migrate = TestDb.Create(conn))
            {
                TelemetrySchema.EnsureTables(migrate);
            }
            Assert.Equal(1, TableCount(conn, "DifficultyLabelLogs"));
            Assert.Equal(1, TableCount(conn, "WeightChangeLogs"));

            // 5) Round-trip qua repo thật: bảng vừa vá phải khớp entity + insert path.
            var diffRepo = new SqliteDifficultyLabelLogRepository(() => TestDb.Create(conn));
            var diff = new DifficultyLabelLog
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                InputText = "Ôn thi cuối kỳ",
                TaskType = LoaiCongViec.ThiCuoiKy,
                SuggestedDoKho = 4,
                FinalDoKho = 4,
                WasOverride = false,
                Source = "baseline",
                MaTask = null,
            };
            await diffRepo.AddAsync(diff);

            var weightRepo = new SqliteWeightChangeLogRepository(() => TestDb.Create(conn));
            var weight = new WeightChangeLog
            {
                Id = Guid.NewGuid(),
                AppliedUtc = DateTime.UtcNow,
                Confidence = 0.80,
                Rationale = "smoke",
                BeforeTime = 0.40, BeforeTaskType = 0.30, BeforeCredit = 0.20, BeforeDifficulty = 0.10,
                AfterTime = 0.35, AfterTaskType = 0.32, AfterCredit = 0.22, AfterDifficulty = 0.11,
                BaselineMissRate = 0.05,
                BaselineAvgDelayDays = 0.5,
                BaselineFocusStreak = 3,
                BaselineStudyMinutes30 = 600,
                BaselineTaskCount = 10,
                BaselineCompletedCount = 8,
                CohortTaskIdsJson = "[\"t1\"]",
                OutcomeWindowDays = 14,
            };
            await weightRepo.AddAsync(weight);

            using var verify = TestDb.Create(conn);
            Assert.NotNull(await verify.DifficultyLabelLogs.FindAsync(diff.Id));
            Assert.NotNull(await verify.WeightChangeLogs.FindAsync(weight.Id));
        }

        [Fact]
        public void EnsureTables_WhenTablesAlreadyExist_IsIdempotent()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated — bảng đã có sẵn */ }

            // Bảng đã tồn tại (đường EnsureCreated) → gọi seam nhiều lần phải là no-op, không ném.
            using var db = TestDb.Create(conn);
            TelemetrySchema.EnsureTables(db);
            TelemetrySchema.EnsureTables(db);

            Assert.Equal(1, TableCount(conn, "DifficultyLabelLogs"));
            Assert.Equal(1, TableCount(conn, "WeightChangeLogs"));
            Assert.Equal(1, TableCount(conn, "StudyTimeOutcomeLogs"));
        }

        // M8-C gate — mirror "M8 gate #4" for StudyTimeOutcomeLogs.
        // Simulates a DB that existed before M8-C (has M8 tables but not StudyTimeOutcomeLogs).
        [Fact]
        public async Task EnsureTables_OnPreM8CDb_CreatesOutcomeLogsAndRoundTrips()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;

            // 1) Full schema via EnsureCreated (includes StudyTimeOutcomeLogs now).
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated */ }

            // 2) Simulate pre-M8C DB: drop only StudyTimeOutcomeLogs.
            using (var downgrade = TestDb.Create(conn))
            {
                downgrade.Database.ExecuteSqlRaw("DROP TABLE StudyTimeOutcomeLogs");
            }

            // 3) Sanity: table is gone and EnsureCreated is a no-op on existing DB.
            using (var afterEnsureCreated = TestDb.Create(conn)) { }
            Assert.Equal(0, TableCount(conn, "StudyTimeOutcomeLogs"));

            // 4) Production seam → table must reappear.
            using (var migrate = TestDb.Create(conn))
            {
                TelemetrySchema.EnsureTables(migrate);
            }
            Assert.Equal(1, TableCount(conn, "StudyTimeOutcomeLogs"));

            // 5) Round-trip via real repo: patched table must accept insert.
            var repo = new SqliteStudyTimeOutcomeLogRepository(() => TestDb.Create(conn));
            var entry = new StudyTimeOutcomeLog
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                MaTask = null,
                TaskType = (int)LoaiCongViec.BaiTapVeNha,
                Difficulty = 3f,
                Credits = 3f,
                DaysLeft = 5f,
                StudiedMinutesSoFar = 0f,
                ActualMinutes = 45f,
                PredictedMinutes = null,
                WasMlPrediction = false,
                Confidence = null,
            };
            await repo.AddAsync(entry);

            using var verify = TestDb.Create(conn);
            Assert.NotNull(await verify.StudyTimeOutcomeLogs.FindAsync(entry.Id));
        }
    }
}
