using Microsoft.EntityFrameworkCore;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-4, DoR §8) — runtime schema migration for the persistent
    /// <c>SyncConflictRecords</c> table, the staging boundary for T2.4 conflict evidence (D6/D8).
    /// <para>
    /// <see cref="AppDbContext"/>.EnsureCreated() DOES create this table on a genuinely fresh DB
    /// (it is registered as a DbSet + <c>OnModelCreating</c> entity, same as
    /// <see cref="SyncBaseSnapshotSchema"/>'s table), but it never creates triggers, on ANY DB —
    /// new or pre-existing. <see cref="EnsureTable"/> is therefore called unconditionally on every
    /// startup, unlike <see cref="SyncSchema.EnsureColumns"/> which only runs when
    /// <see cref="SyncSchema.NeedsUpgrade"/> is true: even a brand-new DB needs this seam to get its
    /// triggers, only the <c>CREATE TABLE</c>/<c>CREATE INDEX</c> statements are no-ops there
    /// (<c>IF NOT EXISTS</c>).
    /// </para>
    /// <para>
    /// <c>SyncConflictRecords</c> is a brand-new, EMPTY table on every existing DB — no column is
    /// added to a table that already carries user data, so (same reasoning as
    /// <see cref="SyncBaseSnapshotSchema"/>'s own doc comment) this method does NOT call
    /// <c>DbBackup.CreateBackup</c>. Idempotent throughout (<c>IF NOT EXISTS</c> on the table and
    /// both indexes, on every trigger), safe to call on every startup. Rollback if needed:
    /// <c>DROP TRIGGER</c> the three triggers, then <c>DROP TABLE SyncConflictRecords</c> — no other
    /// table is touched.
    /// </para>
    /// <para>
    /// The two indexes enforce D7-A (<c>ConflictKey</c> uniqueness) and D9-T6 (at most one
    /// <c>Unresolved</c> record per logical <c>ScopeKey</c> — a partial unique index scoped to
    /// <c>Status = 0</c>, so a <c>Resolved</c> record never blocks a later, different conflict in the
    /// same scope). The three triggers enforce D7-F (original evidence immutable after insert) and
    /// D7-D (Resolved is terminal — no reopen, no further mutation of the resolution columns) and the
    /// "no delete in v1" rule, all at the database layer per the DoR's own preference: application
    /// code may supplement these but must never be the only thing standing between a caller bug and a
    /// broken invariant. Verified compatible with EF Core's <c>RETURNING</c>-based updates (DoR §8.3,
    /// probe F-m(d)) — an ordinary EF update of the allowed Status/Resolution columns on an
    /// <c>Unresolved</c> row still succeeds; only violations abort.
    /// </para>
    /// </summary>
    public static class SyncConflictRecordSchema
    {
        public static void EnsureTable(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS SyncConflictRecords (
                    ConflictId            TEXT    NOT NULL PRIMARY KEY,
                    ConflictKey           TEXT    NOT NULL,
                    ScopeKey              TEXT    NOT NULL,
                    Kind                  INTEGER NOT NULL,
                    EntityType            TEXT    NOT NULL,
                    EntityId              TEXT    NULL,
                    FieldName             TEXT    NULL,
                    ConstraintKey         TEXT    NULL,
                    ConstraintValue       TEXT    NULL,
                    StructuralReason      INTEGER NULL,
                    PeerDeviceId          TEXT    NOT NULL,
                    SnapshotVersion       INTEGER NOT NULL,
                    BaseEntityId          TEXT    NULL,
                    BaseSnapshotJson      TEXT    NULL,
                    BaseFingerprint       TEXT    NULL,
                    LocalEntityId         TEXT    NOT NULL,
                    LocalSnapshotJson     TEXT    NOT NULL,
                    LocalFingerprint      TEXT    NOT NULL,
                    LocalRowRev           INTEGER NULL,
                    LocalWithdrawal       INTEGER NOT NULL DEFAULT 0,
                    RemoteEntityId        TEXT    NOT NULL,
                    RemoteSnapshotJson    TEXT    NOT NULL,
                    RemoteFingerprint     TEXT    NOT NULL,
                    Status                INTEGER NOT NULL,
                    ResolutionKind        INTEGER NULL,
                    ResultEntityId        TEXT    NULL,
                    ResultSnapshotJson    TEXT    NULL,
                    ResultFingerprint     TEXT    NULL,
                    CreatedAtUtc          TEXT    NOT NULL,
                    CreatedByDeviceId     TEXT    NOT NULL,
                    ResolvedAtUtc         TEXT    NULL,
                    ResolvedByDeviceId    TEXT    NULL
                )");

            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SyncConflictRecords_ConflictKey
                    ON SyncConflictRecords(ConflictKey)");

            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SyncConflictRecords_OneUnresolvedPerScope
                    ON SyncConflictRecords(ScopeKey) WHERE Status = 0");

            db.Database.ExecuteSqlRaw(@"
                CREATE TRIGGER IF NOT EXISTS trg_SyncConflictRecords_EvidenceImmutable
                BEFORE UPDATE OF ConflictKey, ScopeKey, Kind, EntityType, EntityId, FieldName, ConstraintKey, ConstraintValue,
                                 StructuralReason, PeerDeviceId, SnapshotVersion, BaseEntityId, BaseSnapshotJson, BaseFingerprint,
                                 LocalEntityId, LocalSnapshotJson, LocalFingerprint, LocalRowRev, LocalWithdrawal,
                                 RemoteEntityId, RemoteSnapshotJson, RemoteFingerprint, CreatedAtUtc, CreatedByDeviceId
                ON SyncConflictRecords FOR EACH ROW
                BEGIN SELECT RAISE(ABORT, 'SyncConflictRecords: original evidence is immutable (D7-F)'); END");

            db.Database.ExecuteSqlRaw(@"
                CREATE TRIGGER IF NOT EXISTS trg_SyncConflictRecords_ResolvedIsTerminal
                BEFORE UPDATE OF Status, ResolutionKind, ResultEntityId, ResultSnapshotJson, ResultFingerprint, ResolvedAtUtc, ResolvedByDeviceId
                ON SyncConflictRecords FOR EACH ROW WHEN OLD.Status = 1
                BEGIN SELECT RAISE(ABORT, 'SyncConflictRecords: Resolved is terminal (D7-D)'); END");

            db.Database.ExecuteSqlRaw(@"
                CREATE TRIGGER IF NOT EXISTS trg_SyncConflictRecords_NoDelete
                BEFORE DELETE ON SyncConflictRecords FOR EACH ROW
                BEGIN SELECT RAISE(ABORT, 'SyncConflictRecords: records are never deleted in v1'); END");
        }
    }
}
