using Microsoft.EntityFrameworkCore;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Extracted from <c>App.OnStartup</c> so the exact production DB-bootstrap sequence
    /// (schema migrations, in commit order) is callable from an integration test against a real
    /// file-based <see cref="AppDbContext"/> that owns its own connection -- <c>OnStartup</c> is
    /// WPF lifecycle and can't be driven from a unit test directly (same reasoning as
    /// <see cref="TelemetrySchema"/>).
    /// </summary>
    public static class AppStartup
    {
        public static void EnsureDatabaseReady(AppDbContext db, string dbPath)
        {
            db.Database.EnsureCreated();

            // Runtime schema migration: thêm cột IsSeeded nếu DB cũ chưa có
            try
            {
                db.Database.ExecuteSqlRaw(
                    "ALTER TABLE HocKys ADD COLUMN IsSeeded INTEGER NOT NULL DEFAULT 0");
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // Column đã tồn tại — bỏ qua
            }

            // Đánh dấu bản ghi seed đã tồn tại trong DB
            db.Database.ExecuteSqlRaw(
                "UPDATE HocKys SET IsSeeded = 1 WHERE Ten = 'Học Kỳ Dev Seed'");

            // Runtime schema migration: tạo bảng telemetry nếu DB cũ chưa có
            // (EnsureCreated không thêm bảng mới vào DB đã tồn tại). Tách ra seam
            // Data/TelemetrySchema.cs để dual-path test gọi được cùng đường SQL.
            TelemetrySchema.EnsureTables(db);

            // Runtime schema migration: tạo bảng OptimizerRunLogs nếu DB cũ chưa có (T3.7, Epic 3
            // Card G — cùng đường patch-seam như hai dòng trên, tách method riêng có chủ đích, xem
            // doc comment của TelemetrySchema.EnsureOptimizerRunLogTable).
            TelemetrySchema.EnsureOptimizerRunLogTable(db);

            // Epic 1 / M1.2 (T1.8): patch the D-I sync-metadata columns onto an existing
            // (pre-Epic-1) DB. Back up the file first -- only when an upgrade is actually
            // about to run, so normal launches don't pile up backup files every time.
            if (SyncSchema.NeedsUpgrade(db))
            {
                DbBackup.CreateBackup(dbPath, db.Clock());
                SyncSchema.EnsureColumns(db);
            }
        }
    }
}
