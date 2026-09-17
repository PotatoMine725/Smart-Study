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
    /// read back as NULL. TaskNotes' pre-Epic-1 UpdatedAtUtc is reconciled into ModifiedAtUtc and
    /// then dropped -- the EF model no longer has that property, so leaving the NOT NULL column in
    /// place made every TaskNote insert fail on an upgraded DB (PR-B).
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
        /// representative column on one table is sufficient for the ADD-COLUMN half.
        /// The leftover legacy TaskNotes.UpdatedAtUtc is checked separately because it survives
        /// on databases whose ADD-COLUMN half already ran: those DBs pass the HocKys.Rev check
        /// and would otherwise never re-enter the repair path (nor the DbBackup gate).</summary>
        public static bool NeedsUpgrade(AppDbContext db) =>
            !ColumnExists(db, "HocKys", "Rev") || ColumnExists(db, "TaskNotes", "UpdatedAtUtc");

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
            // WHERE ModifiedAtUtc IS NULL is not a new rule: while NeedsUpgrade only probed
            // HocKys.Rev this method was reachable only with ModifiedAtUtc freshly added and NULL
            // on every row, so guarded and unguarded were the same statement. NeedsUpgrade now
            // also fires on already-upgraded DBs, where an unguarded UPDATE would revert a note
            // edited after that upgrade to its frozen legacy stamp -- and ModifiedAtUtc is the
            // LWW key, so that would be silent data loss, not a cosmetic regression.
            if (ColumnExists(db, "TaskNotes", "UpdatedAtUtc"))
            {
                db.Database.ExecuteSqlRaw(
                    "UPDATE TaskNotes SET ModifiedAtUtc = UpdatedAtUtc WHERE ModifiedAtUtc IS NULL");
            }

            // Backfill any row left without a stamp (freshly added column, or TaskNotes rows
            // that had no UpdatedAtUtc to reconcile from) so ISyncMetadata's non-nullable
            // ModifiedAtUtc/ModifiedByDeviceId are never read back as NULL. The two columns are
            // backfilled independently -- a row already reconciled from UpdatedAtUtc has a
            // non-null ModifiedAtUtc but still needs ModifiedByDeviceId stamped.
            var now = db.Clock();
            // Lấy qua seam của context, đối xứng với db.Clock() ở trên: backfill phải dùng
            // đúng danh tính mà mọi write sau này dùng, nếu không row cũ và row mới của
            // cùng một máy sẽ mang hai device id khác nhau. KHÔNG tự dựng DeviceIdentity ở
            // đây — EnsureColumns bị 6 test gọi trực tiếp, và default không-I/O của context
            // là thứ giữ cho chúng không ghi device-id.txt vào profile thật của người chạy.
            var deviceId = db.DeviceIdProvider();
            foreach (var table in SyncTables)
            {
                db.Database.ExecuteSqlRaw(
                    $"UPDATE {table} SET ModifiedAtUtc = {{0}} WHERE ModifiedAtUtc IS NULL", now);
                db.Database.ExecuteSqlRaw(
                    $"UPDATE {table} SET ModifiedByDeviceId = {{0}} WHERE ModifiedByDeviceId IS NULL", deviceId);
            }

            // Only now, once every row has been reconciled and stamped, is the legacy column dead
            // and safe to remove. It has to go: the EF model dropped UpdatedAtUtc in M1.2, so EF
            // omits it from every INSERT while SQLite still declares it TEXT NOT NULL with no
            // default -- every TaskNote insert on an upgraded DB fails with
            // "NOT NULL constraint failed: TaskNotes.UpdatedAtUtc". DROP COLUMN needs SQLite
            // >= 3.35.0 and the bundled engine is 3.49.1; UpdatedAtUtc backs no index, constraint,
            // trigger or view, so the drop is metadata-only and leaves every row intact. The
            // caller (AppStartup) has already taken a DbBackup, because NeedsUpgrade above reports
            // true for exactly this state.
            if (ColumnExists(db, "TaskNotes", "UpdatedAtUtc"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE TaskNotes DROP COLUMN UpdatedAtUtc");
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
