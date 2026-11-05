using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SmartStudyPlanner.Data
{
    public sealed record TableSnapshot(string Table, int RowCount, string Checksum);

    /// <summary>
    /// T1.8 evidence for the "lossless upgrade" acceptance criterion: a per-table row-count +
    /// content checksum, captured before and after <see cref="SyncSchema.EnsureColumns"/> runs.
    /// Row count is the primary lossless-data signal. The checksum only compares meaningfully
    /// across a schema change when restricted to the columns common to both snapshots (the new
    /// D-I columns are expected to differ pre/post -- that's the migration, not corruption) --
    /// callers doing a before/after upgrade comparison should pass the pre-upgrade column list.
    /// </summary>
    public static class MigrationReporter
    {
        private static readonly string[] SyncTables =
        {
            "HocKys", "MonHocs", "StudyTasks", "StudyLogs", "TaskNotes", "TaskReferenceLinks"
        };

        private const string FieldSeparator = "|";
        private const string RowSeparator = "\n";
        private const string NullMarker = "<null>";

        public static IReadOnlyList<TableSnapshot> Capture(AppDbContext db) =>
            SyncTables.Select(t => CaptureTable(db, t)).ToList();

        public static TableSnapshot CaptureTable(AppDbContext db, string table, IReadOnlyList<string>? columns = null)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) connection.Open();
            try
            {
                var columnList = columns is { Count: > 0 } ? string.Join(", ", columns) : "*";
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT {columnList} FROM {table} ORDER BY rowid";
                using var reader = cmd.ExecuteReader();

                int rowCount = 0;
                var sb = new StringBuilder();
                while (reader.Read())
                {
                    rowCount++;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        sb.Append(reader.IsDBNull(i) ? NullMarker : Convert.ToString(reader.GetValue(i)));
                        sb.Append(FieldSeparator);
                    }
                    sb.Append(RowSeparator);
                }

                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
                return new TableSnapshot(table, rowCount, hash);
            }
            finally
            {
                if (shouldClose) connection.Close();
            }
        }
    }
}
