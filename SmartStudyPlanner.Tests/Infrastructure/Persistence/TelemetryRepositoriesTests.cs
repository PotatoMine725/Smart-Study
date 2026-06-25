using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence
{
    public class TelemetryRepositoriesTests
    {
        private static (SqliteConnection conn, Func<SmartStudyPlanner.Data.AppDbContext> factory) NewDb()
        {
            var conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated — tạo cả 2 bảng telemetry */ }
            return (conn, () => TestDb.Create(conn));
        }

        // ── DifficultyLabelLogRepository ──────────────────────────────────────

        [Fact]
        public async Task DifficultyLabelLog_Add_RoundTrip()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteDifficultyLabelLogRepository(factory);

            var entry = new DifficultyLabelLog
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                InputText = "Ôn thi cuối kỳ",
                TaskType = LoaiCongViec.ThiCuoiKy,
                SuggestedDoKho = 4,
                FinalDoKho = 3,
                WasOverride = true,
                Source = "baseline",
                MaTask = null,
            };

            await repo.AddAsync(entry);

            using var verify = factory();
            var loaded = await verify.DifficultyLabelLogs.FindAsync(entry.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Ôn thi cuối kỳ", loaded!.InputText);
            Assert.Equal(LoaiCongViec.ThiCuoiKy, loaded.TaskType);
            Assert.Equal(4, loaded.SuggestedDoKho);
            Assert.Equal(3, loaded.FinalDoKho);
            Assert.True(loaded.WasOverride);
        }

        [Fact]
        public async Task DifficultyLabelLog_WasOverride_FalseWhenSuggestionAccepted()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteDifficultyLabelLogRepository(factory);

            var entry = new DifficultyLabelLog
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                InputText = "Bài tập về nhà",
                TaskType = LoaiCongViec.BaiTapVeNha,
                SuggestedDoKho = 2,
                FinalDoKho = 2,
                WasOverride = false,
                Source = "baseline",
            };

            await repo.AddAsync(entry);

            using var verify = factory();
            var loaded = await verify.DifficultyLabelLogs.FindAsync(entry.Id);
            Assert.NotNull(loaded);
            Assert.False(loaded!.WasOverride);
        }

        // ── WeightChangeLogRepository ─────────────────────────────────────────

        [Fact]
        public async Task WeightChangeLog_Add_RoundTrip()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteWeightChangeLogRepository(factory);

            var entry = BuildSampleLog(DateTime.UtcNow.AddDays(-1));
            await repo.AddAsync(entry);

            using var verify = factory();
            var loaded = await verify.WeightChangeLogs.FindAsync(entry.Id);
            Assert.NotNull(loaded);
            Assert.Equal(entry.Confidence, loaded!.Confidence);
            Assert.Equal(entry.BeforeTime, loaded.BeforeTime);
            Assert.Equal(entry.AfterTime, loaded.AfterTime);
            Assert.Equal("[\"abc\"]", loaded.CohortTaskIdsJson);
            Assert.Null(loaded.OutcomeMaturedUtc);
        }

        [Fact]
        public async Task WeightChangeLog_GetPendingMaturation_OnlyReturnsExpiredUnmatured()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteWeightChangeLogRepository(factory);

            var now = DateTime.UtcNow;
            var expired = BuildSampleLog(now.AddDays(-15)); // 15d ago — window=14 đã qua
            var recent = BuildSampleLog(now.AddDays(-5));   // 5d ago — chưa đủ 14d
            var alreadyMatured = BuildSampleLog(now.AddDays(-20));
            alreadyMatured.OutcomeMaturedUtc = now.AddDays(-6);

            await repo.AddAsync(expired);
            await repo.AddAsync(recent);
            await repo.AddAsync(alreadyMatured);

            var pending = await repo.GetPendingMaturationAsync(now);
            Assert.Single(pending);
            Assert.Equal(expired.Id, pending[0].Id);
        }

        [Fact]
        public async Task WeightChangeLog_UpdateOutcome_FillsFields_Idempotent()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var repo = new SqliteWeightChangeLogRepository(factory);

            var now = DateTime.UtcNow;
            var entry = BuildSampleLog(now.AddDays(-15));
            await repo.AddAsync(entry);

            entry.OutcomeMaturedUtc = now;
            entry.OutcomeMissRate = 0.12;
            entry.OutcomeAvgDelayDays = 1.5;
            entry.OutcomeCompletedInWindow = 3;
            await repo.UpdateOutcomeAsync(entry);

            // Chạy lần 2 — không có exception, kết quả không đổi (idempotent)
            await repo.UpdateOutcomeAsync(entry);

            using var verify = factory();
            var loaded = await verify.WeightChangeLogs.FindAsync(entry.Id);
            Assert.NotNull(loaded!.OutcomeMaturedUtc);
            Assert.Equal(0.12, loaded!.OutcomeMissRate);
            Assert.Equal(3, loaded.OutcomeCompletedInWindow);
        }

        private static WeightChangeLog BuildSampleLog(DateTime appliedUtc) => new()
        {
            Id = Guid.NewGuid(),
            AppliedUtc = appliedUtc,
            Confidence = 0.80,
            Rationale = "test",
            BeforeTime = 0.40, BeforeTaskType = 0.30, BeforeCredit = 0.20, BeforeDifficulty = 0.10,
            AfterTime = 0.35, AfterTaskType = 0.32, AfterCredit = 0.22, AfterDifficulty = 0.11,
            BaselineMissRate = 0.05,
            BaselineAvgDelayDays = 0.5,
            BaselineFocusStreak = 3,
            BaselineStudyMinutes30 = 600,
            BaselineTaskCount = 10,
            BaselineCompletedCount = 8,
            CohortTaskIdsJson = "[\"abc\"]",
            OutcomeWindowDays = 14,
        };
    }
}
