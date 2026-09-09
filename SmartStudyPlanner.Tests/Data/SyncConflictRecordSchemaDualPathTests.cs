using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-4, DoR §8) — dual-path schema test for the new
    /// <c>SyncConflictRecords</c> table, same shape as
    /// <see cref="SyncBaseSnapshotSchemaDualPathTests"/>: on a DB created before PR-4 (table
    /// missing), <c>EnsureCreated()</c> is a no-op on an existing DB, so the app must patch it via
    /// <see cref="SyncConflictRecordSchema.EnsureTable"/>. Unlike that precedent, this table also
    /// needs three triggers that <c>EnsureCreated()</c> never creates on ANY database — so
    /// <see cref="SyncConflictRecordSchema.EnsureTable"/> must run unconditionally, including on a
    /// database where EF already built the table from the model.
    /// </summary>
    public class SyncConflictRecordSchemaDualPathTests
    {
        private static int TableCount(SqliteConnection conn, string table)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n";
            cmd.Parameters.AddWithValue("$n", table);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static int TriggerCount(SqliteConnection conn, string trigger)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name=$n";
            cmd.Parameters.AddWithValue("$n", trigger);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private sealed record ColumnInfo(string Name, string Type, bool NotNull);

        // Hardcoded table name — PRAGMA doesn't accept bound parameters for identifiers (only
        // SqliteParameter values, used above for the sqlite_master lookups), so this stays a fixed
        // single-table helper rather than a string-interpolated-identifier shape a SAST scanner would
        // (correctly, in general, just not here) flag as an injection pattern. Same reasoning as
        // SyncBaseSnapshotSchemaDualPathTests.ColumnInfos.
        private static List<ColumnInfo> ColumnInfos(SqliteConnection conn)
        {
            var result = new List<ColumnInfo>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(SyncConflictRecords)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(1);
                var type = reader.GetString(2);
                var notNull = reader.GetInt64(3) != 0;
                result.Add(new ColumnInfo(name, type, notNull));
            }
            return result;
        }

        private static SyncConflictRecordRow MinimalRow(string conflictKey, string scopeKey) => new()
        {
            ConflictId = Guid.NewGuid(),
            ConflictKey = conflictKey,
            ScopeKey = scopeKey,
            Kind = ConflictKind.ConstraintConflict,
            EntityType = SyncEntityTypes.TaskNote,
            PeerDeviceId = "peerA",
            SnapshotVersion = CanonicalJson.Version,
            LocalEntityId = Guid.NewGuid(),
            LocalSnapshotJson = "{\"local\":true}",
            LocalFingerprint = "fp-local",
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{\"remote\":true}",
            RemoteFingerprint = "fp-remote",
            CreatedAtUtc = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc),
            CreatedByDeviceId = "peerA",
        };

        [Fact]
        public void EnsureTable_OnPreEpic2Db_RecreatesTableIndexesTriggers_AndRoundTrips()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;

            // 1) "New-era" DB: EnsureCreated builds full schema, including SyncConflictRecords
            //    (registered DbSet + OnModelCreating) — but no triggers; EF has no trigger concept.
            using (var seed = TestDb.Create(conn)) { }
            Assert.Equal(1, TableCount(conn, "SyncConflictRecords"));
            Assert.Equal(0, TriggerCount(conn, "trg_SyncConflictRecords_EvidenceImmutable"));

            // 2) Downgrade to pre-PR-4: drop the table as if the DB predates this feature.
            using (var downgrade = TestDb.Create(conn))
                downgrade.Database.ExecuteSqlRaw("DROP TABLE SyncConflictRecords");

            // 3) Sanity: the table is really missing, and EnsureCreated (run again) does not
            //    self-heal an existing database.
            using (var afterEnsureCreated = TestDb.Create(conn)) { }
            Assert.Equal(0, TableCount(conn, "SyncConflictRecords"));

            // 4) Production seam: table, both indexes and all three triggers must appear.
            using (var migrate = TestDb.Create(conn))
                SyncConflictRecordSchema.EnsureTable(migrate);

            Assert.Equal(1, TableCount(conn, "SyncConflictRecords"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_EvidenceImmutable"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_ResolvedIsTerminal"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_NoDelete"));

            // 5) Round-trip directly through the EF-mapped DbSet on the just-patched table.
            var row = MinimalRow("ck-roundtrip", "scope-roundtrip");
            using (var write = TestDb.Create(conn))
            {
                write.SyncConflictRecords.Add(row);
                write.SaveChanges();
            }

            using var verify = TestDb.Create(conn);
            var persisted = verify.SyncConflictRecords.Find(row.ConflictId);
            Assert.NotNull(persisted);
            Assert.Equal("ck-roundtrip", persisted!.ConflictKey);
            Assert.Equal("scope-roundtrip", persisted.ScopeKey);
            Assert.Equal(ConflictRecordStatus.Unresolved, persisted.Status);
        }

        // The case EnsureTable exists FOR beyond the "missing table" path: on a database where EF's
        // own EnsureCreated() already built SyncConflictRecords (a genuinely fresh DB, no upgrade in
        // sight), the triggers are STILL missing, because EnsureCreated() never creates triggers.
        // AppStartup.EnsureDatabaseReady calls EnsureTable unconditionally for exactly this reason.
        [Fact]
        public void EnsureTable_OnAFreshEnsureCreatedDb_StillPatchesInTheTriggers()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { }
            Assert.Equal(1, TableCount(conn, "SyncConflictRecords"));
            Assert.Equal(0, TriggerCount(conn, "trg_SyncConflictRecords_NoDelete"));

            using (var db = TestDb.Create(conn))
                SyncConflictRecordSchema.EnsureTable(db);

            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_NoDelete"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_ResolvedIsTerminal"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_EvidenceImmutable"));
        }

        [Fact]
        public void EnsureTable_WhenAlreadyPatched_IsIdempotent()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { }

            using var db = TestDb.Create(conn);
            SyncConflictRecordSchema.EnsureTable(db);
            SyncConflictRecordSchema.EnsureTable(db);

            Assert.Equal(1, TableCount(conn, "SyncConflictRecords"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_EvidenceImmutable"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_ResolvedIsTerminal"));
            Assert.Equal(1, TriggerCount(conn, "trg_SyncConflictRecords_NoDelete"));
        }

        [Fact]
        public void EnsureTable_ColumnShape_MatchesEfGeneratedSchemaExactly()
        {
            // Drift-hazard check: raw-SQL column defs and EF-model-generated column defs must agree
            // exactly, column-for-column and order-for-order, or a round-trip could silently store
            // the wrong affinity/nullability on one path but not the other.
            var connEf = TestDb.OpenConnection();
            using var _ef = connEf;
            using (var seed = TestDb.Create(connEf)) { }
            var efColumns = ColumnInfos(connEf);

            var connRaw = TestDb.OpenConnection();
            using var _raw = connRaw;
            using (var db = TestDb.Create(connRaw))
            {
                db.Database.ExecuteSqlRaw("DROP TABLE SyncConflictRecords");
                SyncConflictRecordSchema.EnsureTable(db);
            }
            var rawColumns = ColumnInfos(connRaw);

            // Guard against a vacuous pass (both sides silently empty because of a wrong table name)
            // — pin the expected column count so empty-vs-empty can't slip through as "identical".
            Assert.Equal(32, efColumns.Count);
            Assert.Equal(efColumns.Count, rawColumns.Count);
            Assert.Equal(efColumns, rawColumns);
        }
    }
}
