using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// T1.8 — dual-path schema for the D-I sync-metadata columns. Mirrors
    /// <see cref="TelemetrySchemaDualPathTests"/>: build a pre-Epic-1 DB shape via raw
    /// CREATE TABLE (missing columns), run the production seam, and prove it's idempotent
    /// and compatible with the real entity/insert path.
    /// </summary>
    public class SyncSchemaDualPathTests
    {
        private static bool ColumnExists(SqliteConnection conn, string table, string column)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int ColumnCount(SqliteConnection conn, string table, string column)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private static void Exec(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        /// <summary>A real pre-Epic-1 alpha DB has all six synced tables (EnsureCreated is
        /// all-or-nothing across the whole model) -- just missing the new D-I columns.</summary>
        private static void CreatePreEpic1Schema(SqliteConnection conn)
        {
            Exec(conn, @"CREATE TABLE HocKys (
                MaHocKy TEXT NOT NULL PRIMARY KEY, Ten TEXT NULL, NgayBatDau TEXT NOT NULL,
                IsSeeded INTEGER NOT NULL DEFAULT 0)");
            Exec(conn, @"CREATE TABLE MonHocs (
                MaMonHoc TEXT NOT NULL PRIMARY KEY, MaHocKy TEXT NOT NULL, TenMonHoc TEXT NULL,
                SoTinChi INTEGER NOT NULL)");
            Exec(conn, @"CREATE TABLE StudyTasks (
                MaTask TEXT NOT NULL PRIMARY KEY, MaMonHoc TEXT NOT NULL, TenTask TEXT NULL,
                HanChot TEXT NOT NULL, TrangThai TEXT NULL, LoaiTask INTEGER NOT NULL,
                DiemUuTien REAL NOT NULL, MucDoCanhBao TEXT NULL, DoKho INTEGER NOT NULL,
                ThoiGianDaHoc INTEGER NOT NULL DEFAULT 0, NgayHoanThanh TEXT NULL)");
            Exec(conn, @"CREATE TABLE StudyLogs (
                Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, NgayHoc TEXT NOT NULL,
                SoPhutHoc INTEGER NOT NULL, SoPhutDuKien INTEGER NOT NULL, DaHoanThanh INTEGER NOT NULL,
                GhiChu TEXT NULL, CreatedAtUtc TEXT NOT NULL, DeviceId TEXT NOT NULL DEFAULT '',
                IsDeleted INTEGER NOT NULL DEFAULT 0)");
            Exec(conn, @"CREATE TABLE TaskNotes (
                Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Content TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL)");
            Exec(conn, @"CREATE TABLE TaskReferenceLinks (
                Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Title TEXT NOT NULL DEFAULT '',
                Url TEXT NOT NULL DEFAULT '', Category TEXT NULL, SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL DEFAULT '')");
        }

        [Fact]
        public async Task EnsureColumns_OnPreEpic1HocKysTable_AddsColumnsAndRoundTrips()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreatePreEpic1Schema(conn);

            Assert.False(ColumnExists(conn, "HocKys", "Rev"));

            using (var migrate = TestDb.Create(conn))
            {
                migrate.Clock = () => new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);
                SyncSchema.EnsureColumns(migrate);
            }

            Assert.True(ColumnExists(conn, "HocKys", "Rev"));
            Assert.True(ColumnExists(conn, "HocKys", "ModifiedAtUtc"));
            Assert.True(ColumnExists(conn, "HocKys", "ModifiedByDeviceId"));
            Assert.True(ColumnExists(conn, "HocKys", "IsDeleted"));
            Assert.True(ColumnExists(conn, "HocKys", "DeletedAtUtc"));

            // Round-trip: patched columns must be compatible with the real entity/insert path.
            using (var write = TestDb.Create(conn))
            {
                write.HocKys.Add(new HocKy("HK", DateTime.Today));
                await write.SaveChangesAsync();
            }
            using var verify = TestDb.Create(conn);
            var loaded = await verify.HocKys.FirstAsync();
            Assert.Equal(1, loaded.Rev);
        }

        [Fact]
        public async Task EnsureColumns_OnPreEpic1StudyLogsTable_SkipsExistingIsDeletedColumn()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            // Pre-Epic-1 shape: StudyLog already carried CreatedAtUtc/DeviceId/IsDeleted (A6 scope).
            CreatePreEpic1Schema(conn);

            using (var migrate = TestDb.Create(conn))
            {
                SyncSchema.EnsureColumns(migrate);
            }

            Assert.Equal(1, ColumnCount(conn, "StudyLogs", "IsDeleted")); // not duplicated
            Assert.True(ColumnExists(conn, "StudyLogs", "Rev"));
            Assert.True(ColumnExists(conn, "StudyLogs", "ModifiedAtUtc"));
            Assert.True(ColumnExists(conn, "StudyLogs", "ModifiedByDeviceId"));
            Assert.True(ColumnExists(conn, "StudyLogs", "DeletedAtUtc"));

            using var write = TestDb.Create(conn);
            write.StudyLogs.Add(new StudyLog { MaTask = Guid.NewGuid(), NgayHoc = DateTime.Today, SoPhutHoc = 10 });
            await write.SaveChangesAsync(); // must not throw (duplicate-column ALTER would have)
        }

        [Fact]
        public async Task EnsureColumns_OnPreEpic1TaskNotesTable_BackfillsModifiedAtUtcFromUpdatedAtUtc()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreatePreEpic1Schema(conn);

            var noteId = Guid.NewGuid();
            var oldStamp = new DateTime(2026, 6, 1, 9, 30, 0, DateTimeKind.Utc);
            using (var seed = conn.CreateCommand())
            {
                seed.CommandText = "INSERT INTO TaskNotes (Id, MaTask, Content, UpdatedAtUtc) VALUES ($id, $mt, 'note', $ts)";
                seed.Parameters.AddWithValue("$id", noteId.ToString());
                seed.Parameters.AddWithValue("$mt", Guid.NewGuid().ToString());
                seed.Parameters.AddWithValue("$ts", oldStamp.ToString("o"));
                seed.ExecuteNonQuery();
            }

            using (var migrate = TestDb.Create(conn))
            {
                SyncSchema.EnsureColumns(migrate);
            }

            using var verify = TestDb.Create(conn);
            var note = await verify.TaskNotes.FirstAsync();
            Assert.Equal(oldStamp, note.ModifiedAtUtc);
        }

        [Fact]
        public void EnsureColumns_WhenColumnsAlreadyExist_IsIdempotent()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated already has the current shape */ }

            using var db = TestDb.Create(conn);
            var ex = Record.Exception(() =>
            {
                SyncSchema.EnsureColumns(db);
                SyncSchema.EnsureColumns(db);
            });

            Assert.Null(ex);
            Assert.Equal(1, ColumnCount(conn, "HocKys", "Rev"));
        }

        [Fact]
        public void NeedsUpgrade_OnPreEpic1Db_ReturnsTrue()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreatePreEpic1Schema(conn);

            using var db = TestDb.Create(conn);
            Assert.True(SyncSchema.NeedsUpgrade(db));
        }

        [Fact]
        public void NeedsUpgrade_AfterEnsureColumns_ReturnsFalse()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreatePreEpic1Schema(conn);
            using (var migrate = TestDb.Create(conn))
            {
                SyncSchema.EnsureColumns(migrate);
            }

            using var db = TestDb.Create(conn);
            Assert.False(SyncSchema.NeedsUpgrade(db));
        }
    }
}
