using System;
using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// T1.8 — schema-upgrade seam for the D-I sync-metadata columns, sibling of
    /// <see cref="TelemetrySchema"/>. <c>EnsureCreated()</c> is a no-op on an existing DB, so an
    /// alpha-tester's pre-Epic-1 database needs its five ISyncMetadata columns patched onto each
    /// of the six synced tables idempotently (SQLite's ADD COLUMN has no IF NOT EXISTS, so each
    /// column is checked via PRAGMA table_info first). Existing rows are backfilled with a
    /// ModifiedAtUtc/ModifiedByDeviceId stamp so the non-nullable ISyncMetadata fields are never
    /// read back as NULL. TaskNotes' pre-Epic-1 UpdatedAtUtc is reconciled into ModifiedAtUtc.
    /// </summary>
    // table/column/sqlType interpolated into ExecuteSqlRaw below are always drawn from the
    // hardcoded SyncTables/call-site literals in this file, never from external input --
    // SQL identifiers (table/column names) can't be parameterized, so ExecuteSqlRaw is required.
#pragma warning disable EF1002
    public static class SyncSchema
    {
        private static readonly string[] SyncTables =
        {
            "HocKys", "MonHocs", "StudyTasks", "StudyLogs", "TaskNotes", "TaskReferenceLinks"
        };

        /// <summary>Cheap pre-check so callers (App startup) only back up the DB file when a
        /// real upgrade is about to happen -- all six tables migrate together, so checking one
        /// representative column on one table is sufficient.</summary>
        public static bool NeedsUpgrade(AppDbContext db) => !ColumnExists(db, "HocKys", "Rev");

        public static void EnsureColumns(AppDbContext db)
        {
            foreach (var table in SyncTables)
            {
                AddColumnIfMissing(db, table, "Rev", "INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing(db, table, "ModifiedAtUtc", "TEXT NULL");
                AddColumnIfMissing(db, table, "ModifiedByDeviceId", "TEXT NULL");
                AddColumnIfMissing(db, table, "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing(db, table, "DeletedAtUtc", "TEXT NULL");
            }

            // TaskNotes reconciled UpdatedAtUtc -> ModifiedAtUtc (same meaning, one name).
            if (ColumnExists(db, "TaskNotes", "UpdatedAtUtc"))
            {
                db.Database.ExecuteSqlRaw("UPDATE TaskNotes SET ModifiedAtUtc = UpdatedAtUtc");
            }

            // Backfill any row left without a stamp (freshly added column, or TaskNotes rows
            // that had no UpdatedAtUtc to reconcile from) so ISyncMetadata's non-nullable
            // ModifiedAtUtc/ModifiedByDeviceId are never read back as NULL. The two columns are
            // backfilled independently -- a row already reconciled from UpdatedAtUtc has a
            // non-null ModifiedAtUtc but still needs ModifiedByDeviceId stamped.
            var now = db.Clock();
            var deviceId = DeviceHelper.GetId();
            foreach (var table in SyncTables)
            {
                db.Database.ExecuteSqlRaw(
                    $"UPDATE {table} SET ModifiedAtUtc = {{0}} WHERE ModifiedAtUtc IS NULL", now);
                db.Database.ExecuteSqlRaw(
                    $"UPDATE {table} SET ModifiedByDeviceId = {{0}} WHERE ModifiedByDeviceId IS NULL", deviceId);
            }
        }

        private static void AddColumnIfMissing(AppDbContext db, string table, string column, string sqlType)
        {
            if (ColumnExists(db, table, column)) return;
            db.Database.ExecuteSqlRaw($"ALTER TABLE {table} ADD COLUMN {column} {sqlType}");
        }

        private static bool ColumnExists(AppDbContext db, string table, string column)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) connection.Open();
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({table})";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(reader.GetOrdinal("name")), column, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            finally
            {
                if (shouldClose) connection.Close();
            }
        }
    }
#pragma warning restore EF1002
}
