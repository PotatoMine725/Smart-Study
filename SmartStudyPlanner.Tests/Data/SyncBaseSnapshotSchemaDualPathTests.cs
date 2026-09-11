using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Epic 2 / M2.1 (T1.4) — dual-path schema test for the new <c>SyncBaseSnapshots</c>
    /// table, same shape as <see cref="OptimizerRunLogSchemaTests"/> (M2.1's own gate):
    /// on a DB created BEFORE T1.4 (table missing), <c>EnsureCreated()</c> is a no-op, so
    /// the app must patch it via <see cref="SyncBaseSnapshotSchema.EnsureTable"/>
    /// (CREATE TABLE IF NOT EXISTS). Simulates pre-T1.4 = EnsureCreated full schema then
    /// DROP the table, then runs the exact production seam and round-trips directly
    /// through <see cref="AppDbContext"/>.
    /// </summary>
    public class SyncBaseSnapshotSchemaDualPathTests
    {
        private static int TableCount(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
            cmd.Parameters.AddWithValue("$n", table);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private sealed record ColumnInfo(string Name, string Type, bool NotNull);

        // SQLite's PRAGMA statements don't accept bound parameters for identifiers (only
        // SqliteParameter values can be bound — see TableCount above for that case), so the
        // table name can't be parameterized here. Hardcoded to the one table this class
        // tests, instead of taking a variable `table` argument, so there's no
        // string-interpolated-identifier pattern for a SAST scanner to (correctly, in
        // general, just not here) flag as an injection shape.
        private static List<ColumnInfo> ColumnInfos(SqliteConnection conn)
        {
            var result = new List<ColumnInfo>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(SyncBaseSnapshots)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                var name = reader.GetString(1);
                var type = reader.GetString(2);
                var notNull = reader.GetInt64(3) != 0;
                result.Add(new ColumnInfo(name, type, notNull));
            }
            return result;
        }

        [Fact]
        public void EnsureTable_OnPreEpic2Db_RecreatesTableAndRoundTrips()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;

            // 1) DB "đời mới": EnsureCreated dựng đủ schema, gồm cả SyncBaseSnapshots (đã
            //    đăng ký DbSet + HasKey trong AppDbContext.OnModelCreating).
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated */ }

            // 2) Hạ cấp thành DB pre-T1.4: bỏ bảng SyncBaseSnapshots như thể DB tạo trước M2.1.
            using (var downgrade = TestDb.Create(conn))
            {
                downgrade.Database.ExecuteSqlRaw("DROP TABLE SyncBaseSnapshots");
            }

            // 3) Sanity: bảng đã thiếu THẬT và EnsureCreated KHÔNG tự vá (no-op trên DB đã tồn tại).
            using (var afterEnsureCreated = TestDb.Create(conn)) { /* EnsureCreated lần 2 = no-op */ }
            Assert.Equal(0, TableCount(conn, "SyncBaseSnapshots"));

            // 4) Chạy đúng seam production → bảng phải xuất hiện trở lại.
            using (var migrate = TestDb.Create(conn))
            {
                SyncBaseSnapshotSchema.EnsureTable(migrate);
            }
            Assert.Equal(1, TableCount(conn, "SyncBaseSnapshots"));

            // 5) Round-trip trực tiếp qua SyncBaseSnapshotStore: bảng vừa vá phải khớp entity +
            //    insert path.
            var entityId = Guid.NewGuid();
            var syncedAt = new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc);
            using (var write = TestDb.Create(conn))
            {
                SyncBaseSnapshotStore.UpsertAsync(
                    write, "peerA", SyncEntityTypes.HocKy, entityId, rev: 4,
                    snapshotJson: "{}", syncedAtUtc: syncedAt).GetAwaiter().GetResult();
                // PR-3: the store only stages the row; this test owns the save.
                write.SaveChanges();
            }

            using var verify = TestDb.Create(conn);
            var row = SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.HocKy, entityId)
                .GetAwaiter().GetResult();

            Assert.NotNull(row);
            Assert.Equal(4, row!.Rev);
            Assert.Equal("{}", row.SnapshotJson);
            Assert.Equal(syncedAt, row.SyncedAtUtc);
        }

        [Fact]
        public void EnsureTable_WhenTableAlreadyExists_IsIdempotent()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated — bảng đã có sẵn */ }

            using var db = TestDb.Create(conn);
            SyncBaseSnapshotSchema.EnsureTable(db);
            SyncBaseSnapshotSchema.EnsureTable(db);

            Assert.Equal(1, TableCount(conn, "SyncBaseSnapshots"));
        }

        [Fact]
        public void EnsureTable_ColumnShape_MatchesEfGeneratedSchemaExactly()
        {
            // Drift-hazard check (§3.5): raw-SQL column defs and EF-model-generated column
            // defs must agree exactly, or a round-trip could silently store the wrong
            // affinity/nullability on one path but not the other.
            var connEf = TestDb.OpenConnection();
            using var _ef = connEf;
            using (var seed = TestDb.Create(connEf)) { /* EnsureCreated builds SyncBaseSnapshots from the EF model */ }
            var efColumns = ColumnInfos(connEf);

            var connRaw = TestDb.OpenConnection();
            using var _raw = connRaw;
            using (var db = TestDb.Create(connRaw))
            {
                db.Database.ExecuteSqlRaw("DROP TABLE SyncBaseSnapshots");
                SyncBaseSnapshotSchema.EnsureTable(db);
            }
            var rawColumns = ColumnInfos(connRaw);

            // Guard against a vacuous pass (e.g. both sides silently returning zero rows
            // because of a wrong table name or a PRAGMA quirk) — pin the expected column
            // count so an empty-vs-empty comparison can't slip through as "identical".
            Assert.Equal(6, efColumns.Count);
            Assert.Equal(efColumns.Count, rawColumns.Count);
            Assert.Equal(efColumns, rawColumns);
        }
    }
}
