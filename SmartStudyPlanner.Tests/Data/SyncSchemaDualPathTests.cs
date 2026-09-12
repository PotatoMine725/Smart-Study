using System;
using System.Linq;
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

        private static bool IndexExists(SqliteConnection conn, string table, string index)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA index_list({table})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(reader.GetOrdinal("name")), index, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>PR-B rests on ALTER TABLE ... DROP COLUMN, which SQLite only grew in 3.35.0.
        /// The bundled engine version is a property of the Microsoft.Data.Sqlite package, not of
        /// this repo, so it is asserted rather than assumed: a package downgrade past that floor
        /// would otherwise surface as a failed upgrade on a user's database, not in CI.</summary>
        [Fact]
        public void BundledSqlite_SupportsDropColumn()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sqlite_version()";
            var parts = Convert.ToString(cmd.ExecuteScalar())!.Split('.');
            var version = new Version(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));

            Assert.True(version >= new Version(3, 35, 0), $"DROP COLUMN needs SQLite >= 3.35.0, bundled: {version}");
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
            // Pre-Epic-1 EnsureCreated also emitted the 1-1 unique index; keeping it here means
            // the upgrade seam is exercised against a TaskNotes that carries an index, which is
            // what makes SQLite's DROP COLUMN safety claim meaningful (PR-B).
            Exec(conn, "CREATE UNIQUE INDEX IX_TaskNotes_MaTask ON TaskNotes (MaTask)");
            Exec(conn, @"CREATE TABLE TaskReferenceLinks (
                Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Title TEXT NOT NULL DEFAULT '',
                Url TEXT NOT NULL DEFAULT '', Category TEXT NULL, SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAtUtc TEXT NOT NULL DEFAULT '')");
        }

        /// <summary>
        /// PR-B / DoR F-e: the state a developer's (and every alpha-tester's) DB is actually in
        /// today -- the D-I columns were already patched on by an earlier launch, but the legacy
        /// pre-Epic-1 <c>UpdatedAtUtc TEXT NOT NULL</c> is still there, because
        /// <c>EnsureColumns</c> only ever read it. Built by downgrading the current shape (same
        /// pattern as <see cref="MigrationReporterTests"/>), keeping the real FK and unique index
        /// so the repair is proven against the production table shape, not a stripped one.
        /// </summary>
        private static void CreateUpgradedSchemaStillCarryingLegacyColumn(SqliteConnection conn)
        {
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated -> current shape */ }
            Exec(conn, "DROP TABLE TaskNotes");
            Exec(conn, @"CREATE TABLE TaskNotes (
                Id TEXT NOT NULL PRIMARY KEY, MaTask TEXT NOT NULL, Content TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                Rev INTEGER NOT NULL DEFAULT 0, ModifiedAtUtc TEXT NULL,
                ModifiedByDeviceId TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAtUtc TEXT NULL,
                FOREIGN KEY (MaTask) REFERENCES StudyTasks (MaTask) ON DELETE CASCADE)");
            Exec(conn, "CREATE UNIQUE INDEX IX_TaskNotes_MaTask ON TaskNotes (MaTask)");
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

        /// <summary>
        /// PR-B RED #1. The test above seeds notes with raw SQL only, so it never noticed that
        /// the legacy <c>UpdatedAtUtc TEXT NOT NULL</c> column survives the upgrade: the EF model
        /// has no such property, so every EF INSERT omits it and SQLite rejects the row with
        /// "NOT NULL constraint failed: TaskNotes.UpdatedAtUtc". That is a live product bug on
        /// every upgraded DB (new-note path), not just a sync blocker.
        /// </summary>
        [Fact]
        public async Task EnsureColumns_OnPreEpic1TaskNotesTable_EfInsertAfterUpgrade_Succeeds()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreatePreEpic1Schema(conn);

            using (var migrate = TestDb.Create(conn))
            {
                SyncSchema.EnsureColumns(migrate);
            }

            // The insert comes first on purpose: it is the defect itself, so an unfixed tree fails
            // here with the production error rather than on a schema assertion about the cause.
            using (var write = TestDb.Create(conn))
            {
                write.TaskNotes.Add(new TaskNote { MaTask = Guid.NewGuid(), Content = "ghi chú mới" });
                await write.SaveChangesAsync();
            }

            Assert.False(ColumnExists(conn, "TaskNotes", "UpdatedAtUtc")); // dead column removed

            using var verify = TestDb.Create(conn);
            var note = await verify.TaskNotes.SingleAsync();
            Assert.Equal("ghi chú mới", note.Content);
            Assert.Equal(1, note.Rev); // stamped by the normal seam, so the row is a real sync row
        }

        /// <summary>
        /// PR-B RED #2 (DoR F-e). <c>NeedsUpgrade</c> only probed <c>HocKys.Rev</c>, so a DB that
        /// already took the D-I upgrade could never re-enter the repair path -- which is exactly
        /// the state every already-upgraded DB is stuck in. It must now report an upgrade is due
        /// while the legacy column is still present, so <c>AppStartup</c>'s backup gate fires.
        /// </summary>
        [Fact]
        public void NeedsUpgrade_OnUpgradedDbStillCarryingLegacyColumn_ReturnsTrue()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreateUpgradedSchemaStillCarryingLegacyColumn(conn);

            using var db = TestDb.Create(conn);
            Assert.True(ColumnExists(conn, "HocKys", "Rev"));            // D-I upgrade already ran
            Assert.True(ColumnExists(conn, "TaskNotes", "UpdatedAtUtc")); // but the dead column stayed
            Assert.True(SyncSchema.NeedsUpgrade(db));
        }

        /// <summary>
        /// PR-B RED #3. Re-entering <c>EnsureColumns</c> on an already-upgraded DB is a state the
        /// old <c>NeedsUpgrade</c> made unreachable, and the reconcile UPDATE was unconditional --
        /// harmless while <c>ModifiedAtUtc</c> was always NULL, destructive now: it would revert a
        /// note edited after the upgrade back to its frozen legacy stamp. <c>ModifiedAtUtc</c> is
        /// the LWW key, so that is a sync-correctness regression, not cosmetics. The reconcile
        /// must still fill rows that never got one.
        /// </summary>
        [Fact]
        public async Task EnsureColumns_OnUpgradedDbWithLegacyColumn_DropsItWithoutClobberingModifiedAtUtc()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            CreateUpgradedSchemaStillCarryingLegacyColumn(conn);

            using (var seedCtx = TestDb.Create(conn))
            {
                await TestDb.SeedTaskAsync(seedCtx);
                await TestDb.SeedTaskAsync(seedCtx);
            }

            var legacyStamp = new DateTime(2026, 6, 1, 9, 30, 0, DateTimeKind.Utc);
            var postUpgradeStamp = new DateTime(2026, 8, 20, 14, 0, 0, DateTimeKind.Utc);

            // Row A: edited through the app AFTER the D-I upgrade -> ModifiedAtUtc is authoritative.
            // Row B: never reconciled (ModifiedAtUtc still NULL) -> must be filled from UpdatedAtUtc.
            // MaTask is taken from StudyTasks by sub-select rather than re-serialised here, so the
            // FK is satisfied byte-for-byte whatever encoding EF chose for the Guid.
            using (var seed = conn.CreateCommand())
            {
                seed.CommandText = @"
                    INSERT INTO TaskNotes (Id, MaTask, Content, UpdatedAtUtc, Rev, ModifiedAtUtc, ModifiedByDeviceId, IsDeleted)
                    SELECT $idA, MaTask, 'đã sửa sau khi nâng cấp', $legacy, 3, $post, 'device-a', 0
                    FROM StudyTasks ORDER BY rowid LIMIT 1;
                    INSERT INTO TaskNotes (Id, MaTask, Content, UpdatedAtUtc, Rev, ModifiedAtUtc, ModifiedByDeviceId, IsDeleted)
                    SELECT $idB, MaTask, 'chưa từng được reconcile', $legacy, 0, NULL, NULL, 0
                    FROM StudyTasks ORDER BY rowid LIMIT 1 OFFSET 1;";
                seed.Parameters.AddWithValue("$idA", Guid.NewGuid().ToString());
                seed.Parameters.AddWithValue("$idB", Guid.NewGuid().ToString());
                seed.Parameters.AddWithValue("$legacy", legacyStamp.ToString("o"));
                seed.Parameters.AddWithValue("$post", postUpgradeStamp.ToString("o"));
                seed.ExecuteNonQuery();
            }

            using (var migrate = TestDb.Create(conn))
            {
                SyncSchema.EnsureColumns(migrate);
            }

            Assert.False(ColumnExists(conn, "TaskNotes", "UpdatedAtUtc"));

            using var verify = TestDb.Create(conn);
            var notes = await verify.TaskNotes.ToListAsync();
            Assert.Equal(2, notes.Count); // no row lost to the repair

            var rowA = notes.Single(n => n.Content == "đã sửa sau khi nâng cấp");
            Assert.Equal(postUpgradeStamp, rowA.ModifiedAtUtc); // NOT reverted to legacyStamp
            Assert.Equal(3, rowA.Rev);
            Assert.Equal("device-a", rowA.ModifiedByDeviceId);

            var rowB = notes.Single(n => n.Content == "chưa từng được reconcile");
            Assert.Equal(legacyStamp, rowB.ModifiedAtUtc); // backfill semantics preserved

            // The drop is a metadata-only ALTER, not a table rebuild: the 1-1 unique index must
            // still exist AND still bite. A rebuild that quietly lost it would leave every later
            // TaskNote merge without the constraint T2.4 reasons about.
            Assert.True(IndexExists(conn, "TaskNotes", "IX_TaskNotes_MaTask"));
            var duplicate = Assert.Throws<SqliteException>(() => Exec(conn, @"
                INSERT INTO TaskNotes (Id, MaTask, Content, Rev, ModifiedAtUtc, ModifiedByDeviceId, IsDeleted)
                SELECT '99999999-9999-9999-9999-999999999999', MaTask, 'trùng MaTask', 0, '2026-09-08', 'd', 0
                FROM TaskNotes ORDER BY rowid LIMIT 1"));
            Assert.Contains("UNIQUE constraint failed", duplicate.Message);
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
