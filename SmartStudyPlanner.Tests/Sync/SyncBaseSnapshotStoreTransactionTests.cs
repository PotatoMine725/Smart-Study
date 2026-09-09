using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-3) — transaction boundary of the base-snapshot store. D8-G says one
    /// logical sync operation is one transaction boundary, and DoR §11.2 puts two
    /// SaveChangesAsync calls (apply, then baseline upsert) inside that single transaction.
    /// Both require the store to stage its write into the caller's context and never save or
    /// commit on its own. These tests pin exactly that: the caller owns the DbContext, the
    /// transaction, SaveChanges and commit/rollback; the store only locates and mutates the row.
    ///
    /// Harness note: NewDb() hands every context the SAME SqliteConnection, so a context created
    /// while a transaction is open would collide with that connection's pending local transaction.
    /// Every assertion below therefore reads through a context created AFTER the transaction has
    /// been committed or rolled back and disposed.
    /// </summary>
    public class SyncBaseSnapshotStoreTransactionTests
    {
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated done */ }
            return (conn, () => TestDb.Create(conn));
        }

        private static async Task<Guid> SeedHocKyAsync(Func<AppDbContext> factory)
        {
            using var db = factory();
            var hocKy = new HocKy("HK Baseline", new DateTime(2026, 9, 1));
            db.HocKys.Add(hocKy);
            await db.SaveChangesAsync();
            return hocKy.MaHocKy;
        }

        // ---------------------------------------------------------------------------------
        // The discriminating test, and the reason PR-3 exists. Against the pre-PR-3 store (whose
        // SetAsync called SaveChangesAsync itself) this goes RED with
        //     Expected: "HK Baseline"  Actual: "Mutated but not committed"
        // because that internal save flushes EVERY dirty tracked entity in the caller's context:
        // an unrelated HocKy the caller had not chosen to persist gets written AND stamped by
        // SyncStamper (Rev++ / ModifiedAtUtc), from a call that was only meant to record a
        // baseline. That is the exact cross-contamination D8-G and the PR-2 seam exist to prevent.
        // ---------------------------------------------------------------------------------
        [Fact]
        public async Task Upsert_DoesNotFlushOrStampUnrelatedTrackedEntity()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var hocKyId = await SeedHocKyAsync(factory);

            long revBefore;
            DateTime modifiedBefore;
            using (var db = factory())
            {
                var hocKy = await db.HocKys.SingleAsync(h => h.MaHocKy == hocKyId);
                revBefore = hocKy.Rev;
                modifiedBefore = hocKy.ModifiedAtUtc;

                // Caller has an in-flight, deliberately unsaved domain edit.
                hocKy.Ten = "Mutated but not committed";
                Assert.Equal(EntityState.Modified, db.Entry(hocKy).State);

                // Baseline write for a completely unrelated entity/peer.
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, Guid.NewGuid(),
                    rev: 5, snapshotJson: null, syncedAtUtc: new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc));
            }

            using var verify = factory();
            var persisted = await verify.HocKys.SingleAsync(h => h.MaHocKy == hocKyId);
            Assert.Equal("HK Baseline", persisted.Ten);
            Assert.Equal(revBefore, persisted.Rev);
            Assert.Equal(modifiedBefore, persisted.ModifiedAtUtc);
        }

        [Fact]
        public async Task Upsert_WithoutCallerSave_DoesNotPersistToASecondContext()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.HocKy, entityId,
                    rev: 1, snapshotJson: "{\"a\":1}", syncedAtUtc: DateTime.UtcNow);
                // No caller SaveChanges: nothing may reach the database.
            }

            using var verify = factory();
            Assert.Null(await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.HocKy, entityId));
        }

        [Fact]
        public async Task Upsert_StagedRow_IsReadableOnTheSameContextBeforeSave()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using var db = factory();
            await SyncBaseSnapshotStore.UpsertAsync(
                db, "peerA", SyncEntityTypes.HocKy, entityId,
                rev: 7, snapshotJson: "{\"staged\":true}", syncedAtUtc: DateTime.UtcNow);

            // Tracked and available to the caller inside the same unit of work.
            var staged = await SyncBaseSnapshotStore.GetAsync(db, "peerA", SyncEntityTypes.HocKy, entityId);
            Assert.NotNull(staged);
            Assert.Equal(7, staged!.Rev);
            Assert.Single(db.ChangeTracker.Entries<SyncBaseSnapshotRow>());
        }

        [Fact]
        public async Task Upsert_ThenCallerSaveChangesAsync_Persists()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();
            var syncedAt = new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc);

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.MonHoc, entityId,
                    rev: 4, snapshotJson: "{\"x\":1}", syncedAtUtc: syncedAt);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.MonHoc, entityId);
            Assert.NotNull(row);
            Assert.Equal(4, row!.Rev);
            Assert.Equal("{\"x\":1}", row.SnapshotJson);
            Assert.Equal(syncedAt, row.SyncedAtUtc);
        }

        [Fact]
        public async Task Upsert_ThenCallerSynchronousSaveChanges_Persists()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyLog, entityId,
                    rev: 2, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                db.SaveChanges();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.StudyLog, entityId);
            Assert.NotNull(row);
            Assert.Equal(2, row!.Rev);
        }

        [Fact]
        public async Task Upsert_NewRow_InsideCallerTransaction_CommitPersists()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.TaskNote, entityId,
                    rev: 3, snapshotJson: "{\"n\":1}", syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.TaskNote, entityId);
            Assert.NotNull(row);
            Assert.Equal(3, row!.Rev);
        }

        [Fact]
        public async Task Upsert_NewRow_CallerRollback_LeavesNoBaselineRow()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.TaskNote, entityId,
                    rev: 3, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
                await tx.RollbackAsync();
            }

            using var verify = factory();
            Assert.Null(await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.TaskNote, entityId));
        }

        [Fact]
        public async Task Upsert_ExistingRow_CallerRollback_LeavesPreviousBaselineUnchanged()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();
            var firstSyncedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.MonHoc, entityId,
                    rev: 1, snapshotJson: "{\"v\":1}", syncedAtUtc: firstSyncedAt);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.MonHoc, entityId,
                    rev: 2, snapshotJson: "{\"v\":2}", syncedAtUtc: firstSyncedAt.AddDays(1));
                await db.SaveChangesAsync();
                await tx.RollbackAsync();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.MonHoc, entityId);
            Assert.NotNull(row);
            Assert.Equal(1, row!.Rev);
            Assert.Equal("{\"v\":1}", row.SnapshotJson);
            Assert.Equal(firstSyncedAt, row.SyncedAtUtc);
        }

        // DoR §11.2: apply and baseline upsert are two saves inside ONE transaction; committing
        // must publish both, and both must be reachable only after that commit.
        [Fact]
        public async Task Upsert_AndDomainChange_CommitTogetherAtomically()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var hocKyId = await SeedHocKyAsync(factory);
            var entityId = Guid.NewGuid();

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                var hocKy = await db.HocKys.SingleAsync(h => h.MaHocKy == hocKyId);
                hocKy.Ten = "Renamed in the same transaction";
                await db.SaveChangesAsync();

                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.HocKy, entityId,
                    rev: 9, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();

                await tx.CommitAsync();
            }

            using var verify = factory();
            Assert.Equal("Renamed in the same transaction",
                (await verify.HocKys.SingleAsync(h => h.MaHocKy == hocKyId)).Ten);
            Assert.NotNull(await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.HocKy, entityId));
        }

        [Fact]
        public async Task Upsert_AndDomainChange_RollbackDiscardsBoth()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var hocKyId = await SeedHocKyAsync(factory);
            var entityId = Guid.NewGuid();

            using (var db = factory())
            using (var tx = await db.Database.BeginTransactionAsync())
            {
                var hocKy = await db.HocKys.SingleAsync(h => h.MaHocKy == hocKyId);
                hocKy.Ten = "Should not survive";
                await db.SaveChangesAsync();

                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.HocKy, entityId,
                    rev: 9, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();

                await tx.RollbackAsync();
            }

            using var verify = factory();
            Assert.Equal("HK Baseline", (await verify.HocKys.SingleAsync(h => h.MaHocKy == hocKyId)).Ten);
            Assert.Null(await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.HocKy, entityId));
        }

        [Fact]
        public async Task Upsert_AddsNewRow_WhenKeyAbsent()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using var db = factory();
            await SyncBaseSnapshotStore.UpsertAsync(
                db, "peerA", SyncEntityTypes.HocKy, entityId,
                rev: 1, snapshotJson: "{\"new\":true}", syncedAtUtc: DateTime.UtcNow);

            var entry = Assert.Single(db.ChangeTracker.Entries<SyncBaseSnapshotRow>());
            Assert.Equal(EntityState.Added, entry.State);
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.SyncBaseSnapshots.CountAsync());
        }

        [Fact]
        public async Task Upsert_UpdatesExistingRowInPlace_WhenKeyPresent()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.HocKy, entityId,
                    rev: 1, snapshotJson: "{\"v\":1}", syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.HocKy, entityId,
                    rev: 2, snapshotJson: "{\"v\":2}", syncedAtUtc: DateTime.UtcNow);

                var entry = Assert.Single(db.ChangeTracker.Entries<SyncBaseSnapshotRow>());
                Assert.Equal(EntityState.Modified, entry.State);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            Assert.Equal(1, await verify.SyncBaseSnapshots.CountAsync());
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.HocKy, entityId);
            Assert.Equal(2, row!.Rev);
            Assert.Equal("{\"v\":2}", row.SnapshotJson);
        }

        // Idempotency has to be proved on the tracker, not just on the final value: asserting only
        // the persisted row would still pass if the store had staged two inserts for one key.
        [Fact]
        public async Task Upsert_RepeatedForSameKeyBeforeSave_StagesExactlyOneRow()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();
            var syncedAt = new DateTime(2026, 9, 9, 6, 0, 0, DateTimeKind.Utc);

            using var db = factory();
            await SyncBaseSnapshotStore.UpsertAsync(
                db, "peerA", SyncEntityTypes.TaskReferenceLink, entityId,
                rev: 1, snapshotJson: "{\"v\":1}", syncedAtUtc: syncedAt);
            await SyncBaseSnapshotStore.UpsertAsync(
                db, "peerA", SyncEntityTypes.TaskReferenceLink, entityId,
                rev: 2, snapshotJson: "{\"v\":2}", syncedAtUtc: syncedAt);
            await SyncBaseSnapshotStore.UpsertAsync(
                db, "peerA", SyncEntityTypes.TaskReferenceLink, entityId,
                rev: 2, snapshotJson: "{\"v\":2}", syncedAtUtc: syncedAt);

            // FindAsync must resolve the still-pending Added entity from the local tracker.
            var entry = Assert.Single(db.ChangeTracker.Entries<SyncBaseSnapshotRow>());
            Assert.Equal(EntityState.Added, entry.State);

            await db.SaveChangesAsync();
            Assert.Equal(1, await db.SyncBaseSnapshots.CountAsync());
            var row = await SyncBaseSnapshotStore.GetAsync(db, "peerA", SyncEntityTypes.TaskReferenceLink, entityId);
            Assert.Equal(2, row!.Rev);
            Assert.Equal("{\"v\":2}", row.SnapshotJson);
        }

        [Fact]
        public async Task Delete_WithoutCallerSave_DoesNotPersistTheRemoval()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.TaskNote, entityId,
                    rev: 1, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.RemoveAsync(db, "peerA", SyncEntityTypes.TaskNote, entityId);
                // No caller SaveChanges: the removal must remain staged only.
            }

            using var verify = factory();
            Assert.NotNull(await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.TaskNote, entityId));
        }

        [Fact]
        public async Task Delete_ThenCallerSave_RemovesTheRow()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.TaskNote, entityId,
                    rev: 1, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.RemoveAsync(db, "peerA", SyncEntityTypes.TaskNote, entityId);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            Assert.Null(await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.TaskNote, entityId));
        }
    }
}
