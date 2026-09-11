using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-4) — the persistent ConflictRecord staging boundary (DoR §8, D6/D7/D8,
    /// D9-T1..T6). Every schema/lifecycle/idempotency invariant here is enforced at the database
    /// layer (SyncConflictRecordSchema's triggers and the two unique indexes); these tests exercise
    /// the real seam (SyncConflictRecordSchema.EnsureTable, called via TestDb.Create's EnsureCreated
    /// path) against real SQLite, not a mocked DbContext.
    ///
    /// Mutation-testing convention (repo standard, no automated mutation tool — F21): for each
    /// invariant below, the accompanying PR report records what happened when the corresponding
    /// trigger/index was temporarily removed from SyncConflictRecordSchema.EnsureTable and the test
    /// re-run (observed RED), then reverted (back to GREEN).
    /// </summary>
    public class SyncConflictRecordStoreTests
    {
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated: table + both EF indexes */ }
            using (var db = TestDb.Create(conn)) SyncConflictRecordSchema.EnsureTable(db); // + triggers
            return (conn, () => TestDb.Create(conn));
        }

        private static void Exec(SqliteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        // EF Core's SQLite provider stores Guid as TEXT in UPPERCASE ("9F471D26-..."), not
        // Guid.ToString("D")'s lowercase — SQLite TEXT comparison is case-sensitive, so a raw SQL
        // literal built from the lowercase default would match zero rows and silently no-op instead
        // of exercising the trigger under test.
        private static string SqlGuid(Guid g) => g.ToString("D").ToUpperInvariant();

        private static readonly DateTime T0 = new(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc);

        /// <summary>A fully-populated, schema-valid Unresolved ConstraintConflict candidate.</summary>
        private static SyncConflictRecordRow MinimalRow(string conflictKey, string scopeKey) => new()
        {
            ConflictId = Guid.NewGuid(),
            ConflictKey = conflictKey,
            ScopeKey = scopeKey,
            Kind = ConflictKind.ConstraintConflict,
            EntityType = SyncEntityTypes.TaskNote,
            ConstraintKey = "MaTask",
            ConstraintValue = Guid.NewGuid().ToString("D"),
            PeerDeviceId = "peerA",
            SnapshotVersion = CanonicalJson.Version,
            BaseEntityId = null,
            BaseSnapshotJson = null,
            BaseFingerprint = null,
            LocalEntityId = Guid.NewGuid(),
            LocalSnapshotJson = "{\"local\":true}",
            LocalFingerprint = "fp-local-" + Guid.NewGuid().ToString("N"),
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{\"remote\":true}",
            RemoteFingerprint = "fp-remote-" + Guid.NewGuid().ToString("N"),
            Status = ConflictRecordStatus.Unresolved,
            CreatedAtUtc = T0,
            CreatedByDeviceId = "peerA",
        };

        // ---------------------------------------------------------------------------------------
        // A — insert a valid Unresolved ConflictRecord
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task A_InsertValidUnresolvedRecord_Persists()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-A", "scope-A");

            using (var db = factory())
            {
                var outcome = await SyncConflictRecordStore.StageAsync(db, row);
                Assert.Equal(ConflictStagingOutcome.Staged, outcome.Outcome);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var persisted = await SyncConflictRecordStore.GetAsync(verify, row.ConflictId);
            Assert.NotNull(persisted);
            Assert.Equal("ck-A", persisted!.ConflictKey);
            Assert.Equal(ConflictRecordStatus.Unresolved, persisted.Status);
            Assert.Equal(ConflictKind.ConstraintConflict, persisted.Kind);
        }

        // ---------------------------------------------------------------------------------------
        // B — duplicate ConflictKey is rejected (unique index, DB-level, bypassing the store's
        // pre-check on purpose to prove the DB constraint itself is what actually blocks it).
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task B_InsertDuplicateConflictKey_ViaRawAdd_IsRejectedByDatabase()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using (var db = factory())
            {
                db.SyncConflictRecords.Add(MinimalRow("ck-B-dup", "scope-B-1"));
                await db.SaveChangesAsync();
            }

            using var db2 = factory();
            db2.SyncConflictRecords.Add(MinimalRow("ck-B-dup", "scope-B-2")); // same key, different scope/id
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
            Assert.Contains("UNIQUE constraint failed", ex.InnerException?.Message ?? ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // C — a second Unresolved record for the SAME ScopeKey is rejected (D9-T6), even with a
        // different ConflictKey — the partial unique index, not the plain ConflictKey index.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task C_InsertSecondUnresolvedSameScope_IsRejectedByDatabase()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using (var db = factory())
            {
                db.SyncConflictRecords.Add(MinimalRow("ck-C-1", "scope-C"));
                await db.SaveChangesAsync();
            }

            using var db2 = factory();
            db2.SyncConflictRecords.Add(MinimalRow("ck-C-2", "scope-C")); // different key, same scope
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
            Assert.Contains("UNIQUE constraint failed", ex.InnerException?.Message ?? ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // D — same shape as C, phrased at the store's typed level: StageAsync must reject a
        // different candidate for a scope that already has an Unresolved record, without ever
        // touching the tracker (no exception needed for the common/expected case).
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task D_StageAsync_DifferentConflictKeySameUnresolvedScope_ReturnsScopeHasUnresolvedConflict()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using (var db = factory())
            {
                var first = await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-D-1", "scope-D"));
                Assert.Equal(ConflictStagingOutcome.Staged, first.Outcome);
                await db.SaveChangesAsync();
            }

            using var db2 = factory();
            var second = await SyncConflictRecordStore.StageAsync(db2, MinimalRow("ck-D-2", "scope-D"));
            Assert.Equal(ConflictStagingOutcome.ScopeHasUnresolvedConflict, second.Outcome);
            Assert.Equal("ck-D-1", second.Record.ConflictKey); // hands back the blocking record
            Assert.Empty(db2.ChangeTracker.Entries<SyncConflictRecordRow>()); // nothing was staged
        }

        // ---------------------------------------------------------------------------------------
        // E — once the first record in a scope becomes Resolved, a NEW Unresolved record (a
        // different, later conflict in the same logical scope) is allowed — this is the DoR §8.4
        // rule verbatim, not an inference: the partial index only covers Status = 0.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task E_AfterFirstRecordResolved_NewUnresolvedRecordInSameScope_Succeeds()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var first = MinimalRow("ck-E-1", "scope-E");

            using (var db = factory())
            {
                db.SyncConflictRecords.Add(first);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                var resolved = await SyncConflictRecordStore.MarkResolvedAsync(
                    db, first.ConflictId, ResolutionKind.KeepBase,
                    resultEntityId: null, resultSnapshotJson: null, resultFingerprint: "null",
                    resolvedAtUtc: T0.AddMinutes(5), resolvedByDeviceId: "peerA");
                Assert.NotNull(resolved);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                var outcome = await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-E-2", "scope-E"));
                Assert.Equal(ConflictStagingOutcome.Staged, outcome.Outcome);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var second = await SyncConflictRecordStore.FindUnresolvedByScopeKeyAsync(verify, "scope-E");
            Assert.NotNull(second);
            Assert.Equal("ck-E-2", second!.ConflictKey);
        }

        // ---------------------------------------------------------------------------------------
        // F / G — original evidence AND classification/identity are immutable after insert (D7-F).
        // Proved at the DB layer with a raw UPDATE attempt, independent of any application code path.
        // ---------------------------------------------------------------------------------------
        [Theory]
        [InlineData("LocalSnapshotJson", "'{\"tampered\":true}'")]
        [InlineData("LocalFingerprint", "'tampered-fp'")]
        [InlineData("BaseFingerprint", "'tampered-base-fp'")]
        [InlineData("RemoteSnapshotJson", "'{\"tampered\":true}'")]
        [InlineData("ConflictKey", "'tampered-key'")]
        [InlineData("ScopeKey", "'tampered-scope'")]
        [InlineData("Kind", "99")]
        [InlineData("EntityType", "'Tampered'")]
        [InlineData("PeerDeviceId", "'tampered-peer'")]
        [InlineData("CreatedAtUtc", "'2099-01-01T00:00:00.0000000Z'")]
        [InlineData("LocalRowRev", "999")]
        [InlineData("LocalWithdrawal", "2")]
        public void F_G_EvidenceAndClassificationColumns_CannotBeMutatedAfterInsert(string column, string sqlLiteral)
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-FG-" + column, "scope-FG-" + column);
            using (var db = factory()) { db.SyncConflictRecords.Add(row); db.SaveChanges(); }

            var ex = Assert.Throws<SqliteException>(() => Exec(conn,
                $"UPDATE SyncConflictRecords SET {column} = {sqlLiteral} WHERE ConflictId = '{SqlGuid(row.ConflictId)}'"));
            Assert.Contains("original evidence is immutable", ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // H — Unresolved -> Resolved succeeds, through the store + a real SaveChanges, RETURNING
        // interplay included (DoR risk note: the 3-column probe must be repeated on the real table).
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task H_UnresolvedToResolved_Succeeds()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-H", "scope-H");
            using (var db = factory()) { db.SyncConflictRecords.Add(row); await db.SaveChangesAsync(); }

            using (var db = factory())
            {
                var resolved = await SyncConflictRecordStore.MarkResolvedAsync(
                    db, row.ConflictId, ResolutionKind.KeepLocal,
                    resultEntityId: row.LocalEntityId, resultSnapshotJson: row.LocalSnapshotJson,
                    resultFingerprint: row.LocalFingerprint, resolvedAtUtc: T0.AddHours(1), resolvedByDeviceId: "peerA");
                Assert.NotNull(resolved);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var persisted = await SyncConflictRecordStore.GetAsync(verify, row.ConflictId);
            Assert.Equal(ConflictRecordStatus.Resolved, persisted!.Status);
            Assert.Equal(ResolutionKind.KeepLocal, persisted.ResolutionKind);
            Assert.Equal(row.LocalEntityId, persisted.ResultEntityId);
            Assert.Equal(T0.AddHours(1), persisted.ResolvedAtUtc);
        }

        // ---------------------------------------------------------------------------------------
        // I — Resolved -> Unresolved fails (D7-D: no reopen). Proved both at the raw-SQL layer and
        // through the ordinary EF SaveChanges path a caller would actually use.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task I_ResolvedToUnresolved_RawSql_Aborts()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-I-raw", "scope-I-raw");
            using (var db = factory())
            {
                db.SyncConflictRecords.Add(row);
                await db.SaveChangesAsync();
                await SyncConflictRecordStore.MarkResolvedAsync(
                    db, row.ConflictId, ResolutionKind.KeepBase, null, null, "null", T0, "peerA");
                await db.SaveChangesAsync();
            }

            var ex = Assert.Throws<SqliteException>(() => Exec(conn,
                $"UPDATE SyncConflictRecords SET Status = 0 WHERE ConflictId = '{SqlGuid(row.ConflictId)}'"));
            Assert.Contains("Resolved is terminal", ex.Message);
        }

        [Fact]
        public async Task I_ResolvedToUnresolved_ViaEfSaveChanges_Aborts()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-I-ef", "scope-I-ef");
            using (var db = factory())
            {
                db.SyncConflictRecords.Add(row);
                await db.SaveChangesAsync();
                await SyncConflictRecordStore.MarkResolvedAsync(
                    db, row.ConflictId, ResolutionKind.KeepBase, null, null, "null", T0, "peerA");
                await db.SaveChangesAsync();
            }

            using var db2 = factory();
            var loaded = await db2.SyncConflictRecords.SingleAsync(r => r.ConflictId == row.ConflictId);
            loaded.Status = ConflictRecordStatus.Unresolved; // attempt to reopen
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
            Assert.Contains("Resolved is terminal", ex.InnerException?.Message ?? ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // J — a Resolved -> Resolved mutation that changes immutable evidence still fails (the
        // evidence trigger is unconditional; the terminal-status trigger only widens the set of
        // columns that abort once Status = 1, it does not narrow it).
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task J_ResolvedRecord_EvidenceMutation_StillAborts()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-J", "scope-J");
            using (var db = factory())
            {
                db.SyncConflictRecords.Add(row);
                await db.SaveChangesAsync();
                await SyncConflictRecordStore.MarkResolvedAsync(
                    db, row.ConflictId, ResolutionKind.KeepBase, null, null, "null", T0, "peerA");
                await db.SaveChangesAsync();
            }

            var ex = Assert.Throws<SqliteException>(() => Exec(conn,
                $"UPDATE SyncConflictRecords SET LocalSnapshotJson = '{{\"tampered\":true}}' WHERE ConflictId = '{SqlGuid(row.ConflictId)}'"));
            Assert.Contains("original evidence is immutable", ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // K — delete behaviour matches the frozen contract: never deleted in v1.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task K_RawDelete_Aborts()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-K", "scope-K");
            using (var db = factory()) { db.SyncConflictRecords.Add(row); await db.SaveChangesAsync(); }

            var ex = Assert.Throws<SqliteException>(() => Exec(conn,
                $"DELETE FROM SyncConflictRecords WHERE ConflictId = '{SqlGuid(row.ConflictId)}'"));
            Assert.Contains("never deleted in v1", ex.Message);
        }

        [Fact]
        public async Task K_EfRemove_AlsoAbortsAtSaveChanges()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-K-ef", "scope-K-ef");
            using (var db = factory()) { db.SyncConflictRecords.Add(row); await db.SaveChangesAsync(); }

            using var db2 = factory();
            var loaded = await db2.SyncConflictRecords.SingleAsync(r => r.ConflictId == row.ConflictId);
            db2.SyncConflictRecords.Remove(loaded);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
            Assert.Contains("never deleted in v1", ex.InnerException?.Message ?? ex.Message);
        }

        // Audit-fence style structural check (same idiom as DoR §17 T-5's grep for "DELETE FROM"):
        // the store's public surface must expose no Remove/Delete method at all, so a future PR-5/
        // PR-6 author cannot even call one by mistake — the DB trigger is the last line of defence,
        // this is the first.
        [Fact]
        public void K_Store_ExposesNoRemoveOrDeleteMethod()
        {
            var publicMethodNames = typeof(SyncConflictRecordStore)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(m => m.Name);

            Assert.DoesNotContain(publicMethodNames, n =>
                n.Contains("Remove", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        }

        // ---------------------------------------------------------------------------------------
        // L — staging-level idempotency (D5-G/D7-C): the SAME ConflictKey staged twice is a no-op
        // the second time. (Resolution-REPLAY idempotency — NoOpReplay for a matching re-request
        // against an already-Resolved record — is PR-6's ConflictResolver orchestration, not this
        // store; see MarkResolvedAsync's own doc comment for why that boundary is deliberate.)
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task L_StageAsync_SameConflictKeyTwice_SecondCallIsNoOp()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using (var db = factory())
            {
                var first = await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-L", "scope-L"));
                Assert.Equal(ConflictStagingOutcome.Staged, first.Outcome);
                await db.SaveChangesAsync();
            }

            using var db2 = factory();
            var second = await SyncConflictRecordStore.StageAsync(db2, MinimalRow("ck-L", "scope-L-retry"));
            Assert.Equal(ConflictStagingOutcome.AlreadyStaged, second.Outcome);
            Assert.Empty(db2.ChangeTracker.Entries<SyncConflictRecordRow>());

            using var verify = factory();
            Assert.Equal(1, await verify.SyncConflictRecords.CountAsync());
        }

        // ---------------------------------------------------------------------------------------
        // L2 — D4/D9-T4 replay coverage via the ConflictKey/AlreadyStaged mechanism specifically,
        // independent of the D9-T6 ScopeKey lock (test L above proves AlreadyStaged generically with
        // an arbitrary string key; this proves it for a REAL replayed candidate through the actual
        // deterministic hash and the actual production row-builder).
        //
        // Why this needs its own seam: SyncApplyParentHandlingTests.
        // M_CreateUnderTombstonedStructuralParent_ReplayStagesNoSecondRecord is the existing
        // end-to-end replay test for this exact D4/D9-T4 shape (a pure create under a tombstoned
        // structural parent), but its own doc comment records that for an entity-scoped
        // StructuralConflict the apply session's ScopeKey pre-check is checked BEFORE parent
        // inspection and intercepts every replay first -- so that test observes
        // ScopeHasUnresolvedConflict, never AlreadyStaged, and cannot exercise ConflictKey dedup on
        // its own. StageAsync's own body checks ConflictKey BEFORE ScopeKey (see above), so calling
        // it directly -- the narrowest legitimate seam, bypassing only the apply session's ordering,
        // not any production invariant -- is what isolates mechanism #2. No production code changes.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task L2_ReplayedD4D9T4Candidate_IsRecognizedByConflictKey_IndependentlyOfScopeKey()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            var remoteId = Guid.NewGuid();

            // Two INDEPENDENTLY built candidates for the exact D4/D9-T4 shape (same as
            // SyncApplyParentHandlingTests.M_CreateUnderTombstonedStructuralParent_...): a pure create
            // under a tombstoned MonHoc parent, so Local is legally absent (StructuralConflict only).
            // Same logical content, separate object graphs -- this is what "replay recomputes the
            // candidate fresh from the re-received payload" actually looks like, not the same object
            // reused twice.
            var remote1 = MergeTestData.Snap(SyncEntityTypes.MonHoc, MergeTestData.Live(500, "peer-device"));
            var remote2 = MergeTestData.Snap(SyncEntityTypes.MonHoc, MergeTestData.Live(500, "peer-device"));
            Assert.NotSame(remote1, remote2);

            static ConflictCandidate Candidate(Guid remoteId, EntitySnapshot remote) => new(
                ConflictKind.StructuralConflict, SyncEntityTypes.MonHoc, remoteId, "MaHocKy", null,
                StructuralReason.ParentTombstoned,
                null, null,   // Base, BaseEntityId -- a pure create has no baseline
                null, null,   // Local, LocalEntityId -- legally absent for a StructuralConflict
                remote, remoteId,
                null, null);  // not an auto-resolved kind

            var candidate1 = Candidate(remoteId, remote1);
            var candidate2 = Candidate(remoteId, remote2);

            var key1 = ConflictKeys.ConflictKey(candidate1);
            var key2 = ConflictKeys.ConflictKey(candidate2);
            Assert.Equal(key1, key2);   // determinism: same logical state -> same hash, independent objects

            // Same production row-builder the apply session itself uses (ConflictStaging.ToRow).
            var row1 = ConflictStaging.ToRow(candidate1, "peerA", T0, "peerA",
                                              localRowRev: null, withdrawal: ConflictLocalWithdrawal.None);
            var row2 = ConflictStaging.ToRow(candidate2, "peerA", T0.AddMinutes(5), "peerA",
                                              localRowRev: null, withdrawal: ConflictLocalWithdrawal.None);
            Assert.Equal(row1.ConflictKey, row2.ConflictKey);
            Assert.NotEqual(row1.ConflictId, row2.ConflictId);   // each ToRow call mints a fresh id (D7-A)

            using (var db = factory())
            {
                var staged = await SyncConflictRecordStore.StageAsync(db, row1);
                Assert.Equal(ConflictStagingOutcome.Staged, staged.Outcome);
                await db.SaveChangesAsync();
            }

            var originalRemoteSnapshotJson = row1.RemoteSnapshotJson;

            // 1. an existing Unresolved StructuralConflictRecord exists.
            using (var check = factory())
            {
                var persisted = await SyncConflictRecordStore.GetAsync(check, row1.ConflictId);
                Assert.NotNull(persisted);
                Assert.Equal(ConflictKind.StructuralConflict, persisted!.Kind);
                Assert.Equal(ConflictRecordStatus.Unresolved, persisted.Status);
            }

            // 2/3. the replay reaches StageAsync directly and is recognized through ConflictKey.
            using var db2 = factory();
            var replayed = await SyncConflictRecordStore.StageAsync(db2, row2);

            Assert.Equal(ConflictStagingOutcome.AlreadyStaged, replayed.Outcome);
            Assert.Equal(row1.ConflictId, replayed.Record.ConflictId);     // the ORIGINAL record, not row2's
            Assert.Equal(row1.ConflictKey, replayed.Record.ConflictKey);

            // 4. no second record was added to the tracker, or ever persisted.
            Assert.Empty(db2.ChangeTracker.Entries<SyncConflictRecordRow>());
            using (var verify = factory())
                Assert.Equal(1, await verify.SyncConflictRecords.CountAsync());

            // 5/6. existing evidence and status are untouched by the replay.
            using (var verify = factory())
            {
                var persisted = await SyncConflictRecordStore.GetAsync(verify, row1.ConflictId);
                Assert.Equal(originalRemoteSnapshotJson, persisted!.RemoteSnapshotJson);
                Assert.Equal(ConflictRecordStatus.Unresolved, persisted.Status);
            }

            // 7. no child/live structural row was ever materialized. This test deliberately never
            // touches a domain table -- that is what makes it a store-level COMPLEMENT to (not a
            // replacement for) SyncApplyParentHandlingTests.M_CreateUnderTombstonedStructuralParent_
            // ReplayStagesNoSecondRecord, which proves the same "no child materializes" property
            // through the real apply path, where the ScopeKey lock -- not ConflictKey dedup -- is what
            // actually intercepts that end-to-end replay.
            using (var verify = factory())
                Assert.Equal(0, await verify.MonHocs.CountAsync());
        }

        // ---------------------------------------------------------------------------------------
        // M — invalid / contract violations fail closed: a required evidence field left null is
        // rejected by the database, never silently coerced to something else.
        //
        // The D4/D9-T4 amendment (2026-09-10) made the three local-candidate columns nullable, so the
        // enforcement moved from three NOT NULL constraints to the strictly narrower
        // CK_SyncConflictRecords_LocalCandidate. M keeps asserting the SAME property it always did —
        // a ConstraintConflict missing its local evidence cannot reach the table — and M2/M3 below pin
        // the two halves of the narrower rule the amendment introduced.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task M_MissingRequiredLocalSnapshot_FailsClosed()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-M", "scope-M");

            // All three absent, which is the direct analogue of the pre-amendment "NOT NULL" property:
            // absence is legal ONLY for a StructuralConflict, and this is a ConstraintConflict. Nulling
            // just one column instead would be caught by the present-or-absent-together clause (M3's
            // job) and would leave this test unable to fail when the kind restriction is removed.
            row.LocalEntityId = null;
            row.LocalSnapshotJson = null;
            row.LocalFingerprint = null;

            using var db = factory();
            db.SyncConflictRecords.Add(row);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("CHECK constraint failed", ex.InnerException?.Message ?? ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // M2 — D4/D9-T4 amendment: a StructuralConflict with NO local candidate is legal, and round
        // trips with all three columns absent. This is the case PR-5 could not represent before.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task M2_StructuralConflictWithNoLocalCandidate_RoundTrips()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-M2", "scope-M2");
            row.Kind = ConflictKind.StructuralConflict;
            row.EntityType = SyncEntityTypes.MonHoc;
            row.EntityId = Guid.NewGuid();
            row.FieldName = "MaHocKy";
            row.ConstraintKey = null;
            row.ConstraintValue = null;
            row.StructuralReason = StructuralReason.ParentTombstoned;
            row.LocalEntityId = null;
            row.LocalSnapshotJson = null;
            row.LocalFingerprint = null;

            using (var db = factory())
            {
                db.SyncConflictRecords.Add(row);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var persisted = await SyncConflictRecordStore.GetAsync(verify, row.ConflictId);
            Assert.NotNull(persisted);
            Assert.Null(persisted!.LocalEntityId);
            Assert.Null(persisted.LocalSnapshotJson);
            Assert.Null(persisted.LocalFingerprint);
            Assert.Equal("{\"remote\":true}", persisted.RemoteSnapshotJson);
        }

        // ---------------------------------------------------------------------------------------
        // M3 — the amendment's "present or absent TOGETHER" half. Half-absent local evidence is the
        // fabricated-placeholder shape in disguise (an id claiming a row whose snapshot is missing),
        // so it must fail closed even for a StructuralConflict, where absence itself is legal.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task M3_HalfAbsentLocalCandidate_FailsClosedEvenForAStructuralConflict()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-M3", "scope-M3");
            row.Kind = ConflictKind.StructuralConflict;
            row.StructuralReason = StructuralReason.ParentTombstoned;
            row.LocalEntityId = Guid.NewGuid();   // an id with no snapshot behind it
            row.LocalSnapshotJson = null;
            row.LocalFingerprint = null;

            using var db = factory();
            db.SyncConflictRecords.Add(row);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("CHECK constraint failed", ex.InnerException?.Message ?? ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // N — null BaseSnapshotJson is supported where Base is absent (D6-B: concurrent create).
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task N_NullBase_RoundTrips()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-N", "scope-N");
            row.BaseEntityId = null;
            row.BaseSnapshotJson = null;
            row.BaseFingerprint = null;

            using (var db = factory()) { db.SyncConflictRecords.Add(row); await db.SaveChangesAsync(); }

            using var verify = factory();
            var persisted = await SyncConflictRecordStore.GetAsync(verify, row.ConflictId);
            Assert.Null(persisted!.BaseEntityId);
            Assert.Null(persisted.BaseSnapshotJson);
            Assert.Null(persisted.BaseFingerprint);
        }

        // ---------------------------------------------------------------------------------------
        // O — D9-T1/M5 audit evidence for the Base=null TaskNote hard-delete exception: LocalRowRev
        // and LocalWithdrawal must persist and, once inserted, are immutable evidence (F/G already
        // proves the immutability half generically; this proves the specific S3 shape round-trips).
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task O_S3HardDeleteAuditEvidence_PersistsAndIsImmutable()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-O", "scope-O");
            row.BaseEntityId = null;
            row.BaseSnapshotJson = null;
            row.BaseFingerprint = null;
            row.LocalRowRev = 7;
            row.LocalWithdrawal = ConflictLocalWithdrawal.HardDeleted;

            using (var db = factory()) { db.SyncConflictRecords.Add(row); await db.SaveChangesAsync(); }

            using (var verify = factory())
            {
                var persisted = await SyncConflictRecordStore.GetAsync(verify, row.ConflictId);
                Assert.Equal(7, persisted!.LocalRowRev);
                Assert.Equal(ConflictLocalWithdrawal.HardDeleted, persisted.LocalWithdrawal);
            }

            var ex = Assert.Throws<SqliteException>(() => Exec(conn,
                $"UPDATE SyncConflictRecords SET LocalRowRev = 99 WHERE ConflictId = '{SqlGuid(row.ConflictId)}'"));
            Assert.Contains("original evidence is immutable", ex.Message);
        }

        // ---------------------------------------------------------------------------------------
        // P — transaction composability: no store method calls SaveChanges/commits on its own.
        // ---------------------------------------------------------------------------------------
        [Fact]
        public async Task P_StageAsync_WithoutCallerSave_DoesNotPersist()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using (var db = factory())
            {
                await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-P1", "scope-P1"));
                // No caller SaveChanges.
            }

            using var verify = factory();
            Assert.Null(await SyncConflictRecordStore.FindByConflictKeyAsync(verify, "ck-P1"));
        }

        [Fact]
        public async Task P_MarkResolvedAsync_WithoutCallerSave_DoesNotPersist()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var row = MinimalRow("ck-P2", "scope-P2");
            using (var db = factory()) { db.SyncConflictRecords.Add(row); await db.SaveChangesAsync(); }

            using (var db = factory())
            {
                await SyncConflictRecordStore.MarkResolvedAsync(
                    db, row.ConflictId, ResolutionKind.KeepBase, null, null, "null", T0, "peerA");
                // No caller SaveChanges.
            }

            using var verify = factory();
            var persisted = await SyncConflictRecordStore.GetAsync(verify, row.ConflictId);
            Assert.Equal(ConflictRecordStatus.Unresolved, persisted!.Status);
        }

        [Fact]
        public async Task P_Stage_RollbackDiscards_CommitPersists()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-P3", "scope-P3"));
                await db.SaveChangesAsync();
                await tx.RollbackAsync();
            }
            using (var verify = factory())
                Assert.Null(await SyncConflictRecordStore.FindByConflictKeyAsync(verify, "ck-P3"));

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-P4", "scope-P4"));
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            using (var verify = factory())
                Assert.NotNull(await SyncConflictRecordStore.FindByConflictKeyAsync(verify, "ck-P4"));
        }

        // Composability with an unrelated domain write in the SAME transaction — the shape PR-5's
        // apply session actually needs (merge/apply result + conflict staging, one commit).
        [Fact]
        public async Task P_Stage_AndDomainChange_CommitTogetherAtomically()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            Guid hocKyId;
            using (var db = factory())
            {
                var hocKy = new HocKy("HK Baseline", new DateTime(2026, 9, 1));
                db.HocKys.Add(hocKy);
                await db.SaveChangesAsync();
                hocKyId = hocKy.MaHocKy;
            }

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                var hocKy = await db.HocKys.SingleAsync(h => h.MaHocKy == hocKyId);
                hocKy.Ten = "Renamed alongside a staged conflict";
                await db.SaveChangesAsync();

                await SyncConflictRecordStore.StageAsync(db, MinimalRow("ck-P5", "scope-P5"));
                await db.SaveChangesAsync();

                await tx.CommitAsync();
            }

            using var verify = factory();
            Assert.Equal("Renamed alongside a staged conflict",
                (await verify.HocKys.SingleAsync(h => h.MaHocKy == hocKyId)).Ten);
            Assert.NotNull(await SyncConflictRecordStore.FindByConflictKeyAsync(verify, "ck-P5"));
        }
    }
}
