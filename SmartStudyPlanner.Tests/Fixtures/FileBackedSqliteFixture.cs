using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Tests.Fixtures
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 0 (plan §14, §16.3 P0-b). A real FILE-backed SQLite database so that two
    /// connections are two genuinely independent SQLite connections with their own lock state.
    /// <see cref="SyncApplyFixture"/> shares ONE in-memory connection between every context, so it can
    /// never observe lock contention -- the reason this fixture exists.
    /// <para>
    /// Each connection this fixture hands out uses <c>Pooling=False</c> (same as <c>DbBackup</c>), so
    /// closing/disposing it really releases the file handle and <see cref="Dispose"/> can delete the
    /// temp files on Windows.
    /// </para>
    /// <para>
    /// <b>Two knobs are explicit because they change the measurement, not just its speed.</b>
    /// <c>Default Timeout</c> is Microsoft.Data.Sqlite's busy-retry window -- "after how long" a blocked
    /// statement fails is a property of THIS setting, not of SQLite. Production's connection string
    /// (<c>AppDbContext.OnConfiguring</c>: <c>Data Source=...</c>) sets none, so production runs on the
    /// library default (30 s); tests here use small values so a blocked leg does not stall the suite.
    /// <b>Never pass 0</b>: Microsoft.Data.Sqlite treats a zero timeout as "retry forever" (ADO.NET
    /// convention), not "fail immediately" -- observed 2026-09-30 as a hung test host.
    /// <c>Journal mode</c>: no production code sets it, but EF Core's <c>EnsureCreated</c> switches a
    /// NEW SQLite file to WAL by itself -- OBSERVED 2026-09-30 (<c>PRAGMA journal_mode</c> read back
    /// <c>wal</c> on a fresh fixture file), which is also why the owner's dev database is WAL
    /// (docs/reports/2026-07-12-a1-wal-safe-backup.md). So the production-relevant mode is WAL and it is
    /// what this fixture produces by default; the rollback-journal (<c>delete</c>) mode is reachable only
    /// by forcing it and is a supplementary, non-production variant. Callers read the mode back with
    /// <see cref="ReadJournalMode"/> rather than trusting what they asked for.
    /// </para>
    /// </summary>
    internal sealed class FileBackedSqliteFixture : IDisposable
    {
        public string Path { get; }

        private readonly string _dir;

        /// <param name="rollbackJournal">
        /// <c>false</c> (default) = leave the mode <c>EnsureCreated</c> produced (WAL -- what production
        /// gets); <c>true</c> = persistently force <c>journal_mode=delete</c> (the SQLite classic mode; NOT
        /// what an EF-created production database is in).
        /// </param>
        public FileBackedSqliteFixture(bool rollbackJournal = false)
        {
            _dir = Directory.CreateTempSubdirectory("sqlite-lock-probe-").FullName;
            Path = System.IO.Path.Combine(_dir, "probe.db");

            using (var ctx = NewContext())
                ctx.Database.EnsureCreated();

            if (rollbackJournal)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=delete;";
                cmd.ExecuteNonQuery();
            }
        }

        private string ConnectionString(int timeoutSeconds) =>
            new SqliteConnectionStringBuilder
            {
                DataSource = Path,
                Pooling = false,
                DefaultTimeout = timeoutSeconds,
            }.ToString();

        /// <summary>A raw, already-open connection with its own lock state.</summary>
        public SqliteConnection OpenConnection(int timeoutSeconds = 2)
        {
            var conn = new SqliteConnection(ConnectionString(timeoutSeconds));
            conn.Open();
            return conn;
        }

        /// <summary>
        /// A context that owns its OWN connection (EF opens it lazily), unlike
        /// <c>SyncApplyFixture.NewContext</c>, which shares one. Deterministic stamping sources.
        /// </summary>
        public AppDbContext NewContext(int timeoutSeconds = 2)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(ConnectionString(timeoutSeconds))
                .Options;
            var ctx = new AppDbContext(options)
            {
                Clock = () => SyncApplyFixture.LocalNow,
                DeviceIdProvider = () => SyncApplyFixture.LocalDevice,
            };
            return ctx;
        }

        /// <summary>The journal mode the file is ACTUALLY in (read from SQLite, not assumed).</summary>
        public string ReadJournalMode()
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            return (string)cmd.ExecuteScalar()!;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, recursive: true); }
            catch (IOException) { /* best effort: temp dir, a leaked handle must not fail a measurement */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
