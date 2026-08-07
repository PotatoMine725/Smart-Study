using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services.Soe;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// T3.7 (Epic 3, Card G) — dual-path schema cho bảng <c>OptimizerRunLogs</c>, cùng khuôn với
    /// <c>TelemetrySchemaDualPathTests</c> (M8's gate #4): trên một DB tạo TRƯỚC T3.7 (thiếu bảng),
    /// <c>EnsureCreated()</c> là no-op, nên app phải vá bằng
    /// <see cref="TelemetrySchema.EnsureOptimizerRunLogTable"/> (CREATE TABLE IF NOT EXISTS). Mô
    /// phỏng pre-T3.7 = EnsureCreated full schema rồi DROP bảng, sau đó chạy đúng seam production
    /// và round-trip trực tiếp qua <see cref="AppDbContext"/> (không có repository riêng cho bảng
    /// này — Card G chỉ yêu cầu writer, không yêu cầu full repository).
    /// </summary>
    public class OptimizerRunLogSchemaTests
    {
        private static int TableCount(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
            cmd.Parameters.AddWithValue("$n", table);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        [Fact]
        public void EnsureOptimizerRunLogTable_OnPreT37Db_RecreatesTableAndRoundTrips()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;

            // 1) DB "đời mới": EnsureCreated dựng đủ schema, gồm cả OptimizerRunLogs (đã đăng ký
            //    DbSet + HasKey trong AppDbContext.OnModelCreating).
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated */ }

            // 2) Hạ cấp thành DB pre-T3.7: bỏ bảng OptimizerRunLogs như thể DB tạo trước Card G.
            using (var downgrade = TestDb.Create(conn))
            {
                downgrade.Database.ExecuteSqlRaw("DROP TABLE OptimizerRunLogs");
            }

            // 3) Sanity: bảng đã thiếu THẬT và EnsureCreated KHÔNG tự vá (no-op trên DB đã tồn tại).
            using (var afterEnsureCreated = TestDb.Create(conn)) { /* EnsureCreated lần 2 = no-op */ }
            Assert.Equal(0, TableCount(conn, "OptimizerRunLogs"));

            // 4) Chạy đúng seam production → bảng phải xuất hiện trở lại.
            using (var migrate = TestDb.Create(conn))
            {
                TelemetrySchema.EnsureOptimizerRunLogTable(migrate);
            }
            Assert.Equal(1, TableCount(conn, "OptimizerRunLogs"));

            // 5) Round-trip trực tiếp qua AppDbContext: bảng vừa vá phải khớp entity + insert path,
            //    bao gồm CẢ Reason = null (C₀) LẪN Reason có giá trị (C_k thường), trên CÙNG một
            //    round-trip -- nếu cột INTEGER NULL không khớp CLR nullable enum, một trong hai sẽ
            //    lệch giá trị đọc lại chứ không ném exception (SQLite loose typing).
            var runId = Guid.NewGuid();
            var rowC0 = new OptimizerRunLogRow
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                CreatedUtc = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc),
                Termination = OptimizerTermination.Terminated_FixedPoint,
                PassCount = 1,
                PassIndex = 1,
                KStar = 1,
                CheckpointIndex = 0,
                ViolationCount = 2,
                OverdueMinutes = 120,
                Score = -0.5,
                Reason = null,
            };
            var rowC1 = new OptimizerRunLogRow
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                CreatedUtc = rowC0.CreatedUtc,
                Termination = OptimizerTermination.Terminated_FixedPoint,
                PassCount = 1,
                PassIndex = 1,
                KStar = 1,
                CheckpointIndex = 1,
                ViolationCount = 1,
                OverdueMinutes = 60,
                Score = 0.25,
                Reason = OptimizerCheckpointReason.Selected,
            };

            using (var write = TestDb.Create(conn))
            {
                write.OptimizerRunLogs.Add(rowC0);
                write.OptimizerRunLogs.Add(rowC1);
                write.SaveChanges();
            }

            using var verify = TestDb.Create(conn);
            var readC0 = verify.OptimizerRunLogs.Single(r => r.Id == rowC0.Id);
            var readC1 = verify.OptimizerRunLogs.Single(r => r.Id == rowC1.Id);

            Assert.Null(readC0.Reason);
            Assert.Equal(OptimizerCheckpointReason.Selected, readC1.Reason);
            Assert.Equal(runId, readC0.RunId);
            Assert.Equal(runId, readC1.RunId);
            Assert.Equal(2, readC0.ViolationCount);
            Assert.Equal(120, readC0.OverdueMinutes);
            Assert.Equal(-0.5, readC0.Score);
            Assert.Equal(OptimizerTermination.Terminated_FixedPoint, readC0.Termination);
        }

        [Fact]
        public void EnsureOptimizerRunLogTable_WhenTableAlreadyExists_IsIdempotent()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated — bảng đã có sẵn */ }

            using var db = TestDb.Create(conn);
            TelemetrySchema.EnsureOptimizerRunLogTable(db);
            TelemetrySchema.EnsureOptimizerRunLogTable(db);

            Assert.Equal(1, TableCount(conn, "OptimizerRunLogs"));
        }
    }
}
