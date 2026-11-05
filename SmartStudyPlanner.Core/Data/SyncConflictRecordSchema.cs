using System;
using System.Data;
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
    /// <c>SyncConflictRecords</c> was a brand-new, EMPTY table on every existing DB when PR-4 shipped —
    /// no column was added to a table that already carries user data, so (same reasoning as
    /// <see cref="SyncBaseSnapshotSchema"/>'s own doc comment) this method does NOT call
    /// <c>DbBackup.CreateBackup</c>. Idempotent throughout (<c>IF NOT EXISTS</c> on the table and
    /// both indexes, on every trigger, and a one-shot guard on the rebuild below), safe to call on
    /// every startup. Rollback if needed: <c>DROP TRIGGER</c> the three triggers, then
    /// <c>DROP TABLE SyncConflictRecords</c> — no other table is touched.
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
    /// <para>
    /// <b>D4/D9-T4 amendment (2026-09-10).</b> The three local-candidate columns are nullable, because
    /// a StructuralConflict may legitimately have no competing local candidate (a remote pure create
    /// under a tombstoned structural parent). <c>CK_SyncConflictRecords_LocalCandidate</c> replaces the
    /// three <c>NOT NULL</c> constraints with a strictly narrower rule rather than simply dropping
    /// them: the three columns are present or absent TOGETHER, and absence is legal only for
    /// <c>Kind = 1</c> (<c>StructuralConflict</c>). Every other kind still fails closed at the database
    /// layer, which is the property the original <c>NOT NULL</c> columns were there for.
    /// </para>
    /// </summary>
    // Every identifier interpolated into ExecuteSqlRaw below comes from a const in this file, never
    // from external input -- SQL identifiers can't be parameterized, so raw SQL is required. Same
    // reasoning (and same suppression) as SyncSchema.
#pragma warning disable EF1002
    public static class SyncConflictRecordSchema
    {
        private const string Table = "SyncConflictRecords";

        /// <summary>Scratch name for the table rebuild; never present outside <see cref="UpgradeLocalCandidateColumns"/>.</summary>
        private const string UpgradeTable = "SyncConflictRecords_upgrade";

        /// <summary>
        /// D4/D9-T4 amendment. <c>Kind = 1</c> is <c>ConflictKind.StructuralConflict</c> — the enum's
        /// ordinal, the same idiom the <c>Status = 0</c> partial-index filter already relies on.
        /// Written at the <c>Kind</c> level, not narrowed to <c>StructuralReason = 1</c>: the ruling
        /// says "A StructuralConflict MAY have no local competing candidate", so the schema permits
        /// exactly that and no less.
        /// </summary>
        private const string LocalCandidateCheck = "CK_SyncConflictRecords_LocalCandidate";

        private const string LocalCandidateCheckBody = @"
                        (LocalEntityId IS NULL) = (LocalSnapshotJson IS NULL)
                    AND (LocalEntityId IS NULL) = (LocalFingerprint IS NULL)
                    AND (LocalEntityId IS NOT NULL OR Kind = 1)";

        /// <summary>All 32 columns, in model order. Spelled out so the rebuild's copy can never
        /// silently depend on <c>SELECT *</c> column ordering.</summary>
        private const string AllColumns =
            "ConflictId, ConflictKey, ScopeKey, Kind, EntityType, EntityId, FieldName, ConstraintKey, " +
            "ConstraintValue, StructuralReason, PeerDeviceId, SnapshotVersion, BaseEntityId, " +
            "BaseSnapshotJson, BaseFingerprint, LocalEntityId, LocalSnapshotJson, LocalFingerprint, " +
            "LocalRowRev, LocalWithdrawal, RemoteEntityId, RemoteSnapshotJson, RemoteFingerprint, " +
            "Status, ResolutionKind, ResultEntityId, ResultSnapshotJson, ResultFingerprint, " +
            "CreatedAtUtc, CreatedByDeviceId, ResolvedAtUtc, ResolvedByDeviceId";

        private static string CreateTableSql(string table) => $@"
                CREATE TABLE IF NOT EXISTS {table} (
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
                    LocalEntityId         TEXT    NULL,
                    LocalSnapshotJson     TEXT    NULL,
                    LocalFingerprint      TEXT    NULL,
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
                    ResolvedByDeviceId    TEXT    NULL,
                    CONSTRAINT {LocalCandidateCheck} CHECK ({LocalCandidateCheckBody})
                )";

        public static void EnsureTable(AppDbContext db)
        {
            db.Database.ExecuteSqlRaw(CreateTableSql(Table));

            // Must run BEFORE the index/trigger block: the rebuild drops the old table, taking its
            // indexes and triggers with it, and the IF NOT EXISTS statements below then put them back.
            UpgradeLocalCandidateColumns(db);

            db.Database.ExecuteSqlRaw($@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SyncConflictRecords_ConflictKey
                    ON {Table}(ConflictKey)");

            db.Database.ExecuteSqlRaw($@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_SyncConflictRecords_OneUnresolvedPerScope
                    ON {Table}(ScopeKey) WHERE Status = 0");

            db.Database.ExecuteSqlRaw($@"
                CREATE TRIGGER IF NOT EXISTS trg_SyncConflictRecords_EvidenceImmutable
                BEFORE UPDATE OF ConflictKey, ScopeKey, Kind, EntityType, EntityId, FieldName, ConstraintKey, ConstraintValue,
                                 StructuralReason, PeerDeviceId, SnapshotVersion, BaseEntityId, BaseSnapshotJson, BaseFingerprint,
                                 LocalEntityId, LocalSnapshotJson, LocalFingerprint, LocalRowRev, LocalWithdrawal,
                                 RemoteEntityId, RemoteSnapshotJson, RemoteFingerprint, CreatedAtUtc, CreatedByDeviceId
                ON {Table} FOR EACH ROW
                BEGIN SELECT RAISE(ABORT, 'SyncConflictRecords: original evidence is immutable (D7-F)'); END");

            db.Database.ExecuteSqlRaw($@"
                CREATE TRIGGER IF NOT EXISTS trg_SyncConflictRecords_ResolvedIsTerminal
                BEFORE UPDATE OF Status, ResolutionKind, ResultEntityId, ResultSnapshotJson, ResultFingerprint, ResolvedAtUtc, ResolvedByDeviceId
                ON {Table} FOR EACH ROW WHEN OLD.Status = 1
                BEGIN SELECT RAISE(ABORT, 'SyncConflictRecords: Resolved is terminal (D7-D)'); END");

            db.Database.ExecuteSqlRaw($@"
                CREATE TRIGGER IF NOT EXISTS trg_SyncConflictRecords_NoDelete
                BEFORE DELETE ON {Table} FOR EACH ROW
                BEGIN SELECT RAISE(ABORT, 'SyncConflictRecords: records are never deleted in v1'); END");
        }

        // ------------------------------------------------------- D4/D9-T4 amendment (2026-09-10)

        /// <summary>
        /// Relaxes the three local-candidate columns from <c>NOT NULL</c> to the narrower
        /// <see cref="LocalCandidateCheck"/> rule on a database created before the amendment.
        /// <para>
        /// SQLite's <c>ALTER TABLE</c> can neither drop a <c>NOT NULL</c> constraint nor add a table
        /// CHECK, so this is the documented table rebuild. It runs inside ONE transaction — a failure
        /// between <c>DROP</c> and <c>RENAME</c> would otherwise leave the database with no evidence
        /// table at all — and copies all 32 columns explicitly. Every pre-amendment row satisfies the
        /// new CHECK by construction (all three columns were <c>NOT NULL</c>), so the copy cannot abort
        /// on legal legacy data.
        /// </para>
        /// <para>
        /// The three triggers are dropped first and recreated by <see cref="EnsureTable"/>'s
        /// <c>IF NOT EXISTS</c> block. This is defence in depth, and measured as such: removing the
        /// three <c>DROP TRIGGER</c> statements leaves every test GREEN (mutation 28), because SQLite
        /// really does fire no triggers for <c>DROP TABLE</c>'s implicit row removal. They stay because
        /// the alternative is a migration whose correctness depends on that detail of a dependency
        /// rather than on anything this file states — and the trigger in question,
        /// <c>trg_SyncConflictRecords_NoDelete</c>, exists precisely to abort row removal.
        /// </para>
        /// </summary>
        private static void UpgradeLocalCandidateColumns(AppDbContext db)
        {
            if (!NeedsLocalCandidateUpgrade(db)) return;

            // Nested transaction ownership is never created: if the caller already owns one, the
            // rebuild joins it and the caller's commit/rollback covers it.
            var ownsTransaction = db.Database.CurrentTransaction is null;
            var tx = ownsTransaction ? db.Database.BeginTransaction() : null;
            try
            {
                db.Database.ExecuteSqlRaw("DROP TRIGGER IF EXISTS trg_SyncConflictRecords_EvidenceImmutable");
                db.Database.ExecuteSqlRaw("DROP TRIGGER IF EXISTS trg_SyncConflictRecords_ResolvedIsTerminal");
                db.Database.ExecuteSqlRaw("DROP TRIGGER IF EXISTS trg_SyncConflictRecords_NoDelete");

                // A leftover scratch table can only come from a crash mid-rebuild; dropping it makes a
                // retry deterministic instead of silently reusing a half-built table.
                db.Database.ExecuteSqlRaw($"DROP TABLE IF EXISTS {UpgradeTable}");
                db.Database.ExecuteSqlRaw(CreateTableSql(UpgradeTable));
                db.Database.ExecuteSqlRaw(
                    $"INSERT INTO {UpgradeTable} ({AllColumns}) SELECT {AllColumns} FROM {Table}");
                db.Database.ExecuteSqlRaw($"DROP TABLE {Table}");
                db.Database.ExecuteSqlRaw($"ALTER TABLE {UpgradeTable} RENAME TO {Table}");

                tx?.Commit();
            }
            catch
            {
                tx?.Rollback();
                throw;
            }
            finally
            {
                tx?.Dispose();
            }
        }

        /// <summary>
        /// True when the table exists in its pre-amendment shape. Probes <c>sqlite_master</c> for the
        /// CHECK constraint's NAME rather than reading <c>PRAGMA table_info</c> nullability: one probe
        /// then covers both halves of the amendment (relaxed columns AND the constraint that replaces
        /// them), and it cannot report "already upgraded" for a table that has the nullability but not
        /// the rule. Both creation paths — EF's <c>EnsureCreated</c> via <c>OnModelCreating</c>, and
        /// <see cref="CreateTableSql"/> — emit the constraint under this exact name.
        /// </summary>
        private static bool NeedsLocalCandidateUpgrade(AppDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) connection.Open();
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    "SELECT COALESCE(sql, '') FROM sqlite_master WHERE type = 'table' AND name = $name";
                var p = cmd.CreateParameter();
                p.ParameterName = "$name";
                p.Value = Table;
                cmd.Parameters.Add(p);

                var sql = cmd.ExecuteScalar() as string;

                // No table at all => EnsureTable just created it in the current shape; nothing to do.
                return !string.IsNullOrEmpty(sql)
                    && !sql!.Contains(LocalCandidateCheck, StringComparison.Ordinal);
            }
            finally
            {
                if (shouldClose) connection.Close();
            }
        }
    }
#pragma warning restore EF1002
}
