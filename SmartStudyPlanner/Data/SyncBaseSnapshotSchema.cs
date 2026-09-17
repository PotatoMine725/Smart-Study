using Microsoft.EntityFrameworkCore;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Epic 2 / M2.1 (T1.4) — runtime schema migration for the new
    /// <c>SyncBaseSnapshots</c> table (per-peer last-synced base-snapshot store).
    /// <see cref="AppDbContext"/>.EnsureCreated() does NOT add new tables to a DB that
    /// already exists, so this table must be patched in manually at startup — same idiom
    /// as <see cref="TelemetrySchema.EnsureOptimizerRunLogTable"/>.
    /// <para>
    /// <c>SyncBaseSnapshots</c> is a brand-new, EMPTY table on every existing DB — no
    /// column is added to a table that already carries user data, so (same reasoning as
    /// <see cref="TelemetrySchema.EnsureOptimizerRunLogTable"/>'s own doc comment) this
    /// method does NOT call <c>DbBackup.CreateBackup</c>. Idempotent
    /// (<c>CREATE TABLE IF NOT EXISTS</c>), safe to call on every startup. Rollback if
    /// needed: <c>DROP TABLE SyncBaseSnapshots</c> — no other table is touched.
    /// </para>
    /// </summary>
    public static class SyncBaseSnapshotSchema
    {
        public static void EnsureTable(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS SyncBaseSnapshots (
                    PeerDeviceId TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    EntityId TEXT NOT NULL,
                    Rev INTEGER NOT NULL,
                    SnapshotJson TEXT NULL,
                    SyncedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (PeerDeviceId, EntityType, EntityId)
                )");
        }
    }
}
