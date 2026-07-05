using System;
using System.IO;

namespace SmartStudyPlanner.Data
{
    /// <summary>T1.8 risk mitigation: copy the live DB file before SyncSchema.EnsureColumns runs.</summary>
    public static class DbBackup
    {
        public static string? CreateBackup(string dbPath, DateTime utcNow)
        {
            if (!File.Exists(dbPath)) return null;

            var dir = Path.GetDirectoryName(dbPath) ?? ".";
            var name = Path.GetFileNameWithoutExtension(dbPath);
            var ext = Path.GetExtension(dbPath);
            var backupPath = Path.Combine(dir, $"{name}.{utcNow:yyyyMMdd-HHmmss}.bak{ext}");

            File.Copy(dbPath, backupPath, overwrite: false);
            return backupPath;
        }
    }
}
