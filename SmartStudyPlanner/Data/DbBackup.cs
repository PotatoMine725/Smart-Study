using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SmartStudyPlanner.Data
{
    /// <summary>T1.8 risk mitigation: copy the live DB file before SyncSchema.EnsureColumns runs.</summary>
    public static class DbBackup
    {
        public static string? CreateBackup(string dbPath, DateTime utcNow)
        {
            if (!File.Exists(dbPath)) return null;

            // F5: in WAL mode, committed data may still sit in the -wal sidecar; a naive file copy
            // silently drops it, making the backup lossy exactly when it matters. Checkpoint into
            // the main file first so the copy is complete. TRUNCATE also resets the WAL. This is a
            // no-op on non-WAL databases. Pooling=False so the handle is released before File.Copy.
            // Deliberately no try/catch: backup is Epic 1's named top-risk mitigation -- fail loudly
            // at startup rather than upgrade against an incomplete backup (matches File.Copy below).
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cmd.ExecuteNonQuery();
            }

            var dir = Path.GetDirectoryName(dbPath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(dbPath);
            var ext = Path.GetExtension(dbPath);
            var backupPath = Path.Combine(dir, $"{name}.{utcNow:yyyyMMdd-HHmmss}.bak{ext}");

            File.Copy(dbPath, backupPath, overwrite: false);
            return backupPath;
        }
    }
}
