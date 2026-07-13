using System;
using System.IO;
using Microsoft.Data.Sqlite;
using SmartStudyPlanner.Data;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>T1.8 risk mitigation: back up the live DB file before EnsureColumns runs.</summary>
    public class DbBackupTests : IDisposable
    {
        private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            SqliteConnection.ClearAllPools(); // release any pooled file handles before cleanup
            Directory.Delete(_tempDir, recursive: true);
        }

        // Real (not fake-text) SQLite fixture: fake-text fixtures are exactly why F5 escaped --
        // the checkpoint pragma throws on non-DB files and clean text never carries a live WAL.
        private static void CreateSqliteDbWithMarker(string dbPath, string markerValue)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Marker (Value TEXT NOT NULL); INSERT INTO Marker (Value) VALUES ($v);";
            cmd.Parameters.AddWithValue("$v", markerValue);
            cmd.ExecuteNonQuery();
        }

        private static string ReadFirstMarker(string dbPath)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Marker LIMIT 1;";
            return (string)cmd.ExecuteScalar()!;
        }

        [Fact]
        public void CreateBackup_CopiesDbFileWithTimestampedName()
        {
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            CreateSqliteDbWithMarker(dbPath, "marker-content");

            var backupPath = DbBackup.CreateBackup(dbPath, new DateTime(2026, 7, 5, 14, 30, 0));

            Assert.True(File.Exists(backupPath));
            Assert.Equal("marker-content", ReadFirstMarker(backupPath!));
            Assert.Contains("20260705-143000", backupPath);
            Assert.NotEqual(dbPath, backupPath);
        }

        [Fact]
        public void CreateBackup_WhenSourceMissing_ReturnsNullAndDoesNotThrow()
        {
            var dbPath = Path.Combine(_tempDir, "DoesNotExist.db");

            var backupPath = DbBackup.CreateBackup(dbPath, DateTime.UtcNow);

            Assert.Null(backupPath);
        }

        /// <summary>
        /// F5 discriminating test: in WAL mode, committed rows can still live in the -wal sidecar.
        /// A naive File.Copy of only the main .db silently drops them, so the "backup" is lossy
        /// exactly when it matters. CreateBackup must checkpoint first so committed rows survive.
        /// On the unmodified File.Copy-only baseline this FAILS because the rows are ABSENT from the
        /// backup (the table exists but is empty); with the checkpoint it PASSES.
        /// </summary>
        [Fact]
        public void CreateBackup_WithPendingWalPages_IncludesCommittedRowsInBackup()
        {
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");

            // Keep this writer OPEN across the backup call: closing it would auto-checkpoint on
            // dispose and hide the live-WAL scenario we need to reproduce.
            var writer = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            try
            {
                writer.Open();
                void Exec(string sql)
                {
                    using var cmd = writer.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }

                Exec("PRAGMA journal_mode=WAL;");
                Exec("CREATE TABLE Marker (Value TEXT NOT NULL);");
                // Flush the schema into the main .db so the backup HAS the table -- this makes the
                // baseline failure "rows absent", not "no such table" (a setup artifact).
                Exec("PRAGMA wal_checkpoint(TRUNCATE);");
                // Disable autocheckpoint, then commit rows that stay ONLY in the -wal sidecar.
                Exec("PRAGMA wal_autocheckpoint=0;");
                Exec("INSERT INTO Marker (Value) VALUES ('wal-row-1');");
                Exec("INSERT INTO Marker (Value) VALUES ('wal-row-2');");

                // Precondition: the live-WAL state M1.2's clean fixtures never had.
                var walPath = dbPath + "-wal";
                Assert.True(File.Exists(walPath), "precondition: -wal sidecar must exist");
                Assert.True(new FileInfo(walPath).Length > 0,
                    "precondition: -wal sidecar must be non-empty (committed rows still pending)");

                var backupPath = DbBackup.CreateBackup(dbPath, new DateTime(2026, 7, 12, 9, 0, 0));

                Assert.NotNull(backupPath);
                using var verify = new SqliteConnection($"Data Source={backupPath};Pooling=False");
                verify.Open();
                using var read = verify.CreateCommand();
                read.CommandText = "SELECT COUNT(*) FROM Marker;";
                var count = (long)read.ExecuteScalar()!;
                Assert.Equal(2L, count); // committed-but-un-checkpointed rows must survive into the backup
            }
            finally
            {
                writer.Dispose();
            }
        }
    }
}
