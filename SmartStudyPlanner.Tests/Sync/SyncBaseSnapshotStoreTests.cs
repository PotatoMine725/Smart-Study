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
    /// Epic 2 / M2.1, T1.4 — per-peer last-synced base-snapshot store. Uses the shared
    /// in-memory SQLite fixture pattern (TestDb.Create / SqliteConnection ":memory:") per
    /// this repo's convention (see SoftDeleteReadPathTests.cs, RepositoriesTests.cs).
    /// </summary>
    public class SyncBaseSnapshotStoreTests
    {
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated done */ }
            return (conn, () => TestDb.Create(conn));
        }

        [Fact]
        public async Task SetThenGet_RoundTripsRevSnapshotJsonAndSyncedAtUtc()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();
            var syncedAt = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc);

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.HocKy, entityId, rev: 3,
                    snapshotJson: "{\"foo\":1}", syncedAtUtc: syncedAt);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.HocKy, entityId);

            Assert.NotNull(row);
            Assert.Equal(3, row!.Rev);
            Assert.Equal("{\"foo\":1}", row.SnapshotJson);
            Assert.Equal(syncedAt, row.SyncedAtUtc);
        }

        [Fact]
        public async Task SetTwiceForSameKey_OverwritesInPlace_DoesNotAppend()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, entityId, rev: 1,
                    snapshotJson: "first", syncedAtUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
                await db.SaveChangesAsync();
            }
            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, entityId, rev: 5,
                    snapshotJson: "second", syncedAtUtc: new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.StudyTask, entityId);

            Assert.NotNull(row);
            Assert.Equal(5, row!.Rev);
            Assert.Equal("second", row.SnapshotJson);

            // Count the raw table directly, not GetAllForPeerAsync's Dictionary<EntityId, _>
            // projection — a dictionary keyed by EntityId would collapse two rows into one
            // (or throw on a duplicate key) regardless of whether SetAsync actually upserted
            // or appended, so it can't distinguish the two. This assertion can.
            var rowCount = await verify.SyncBaseSnapshots.CountAsync(r =>
                r.PeerDeviceId == "peerA" && r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == entityId);
            Assert.Equal(1, rowCount);
        }

        [Fact]
        public async Task TwoPeersSnapshottingSameRow_AreIndependent()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.MonHoc, entityId, rev: 7,
                    snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var forA = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.MonHoc, entityId);
            var forB = await SyncBaseSnapshotStore.GetAsync(verify, "peerB", SyncEntityTypes.MonHoc, entityId);

            Assert.NotNull(forA);
            Assert.Equal(7, forA!.Rev);
            Assert.Null(forB);

            var allForB = await SyncBaseSnapshotStore.GetAllForPeerAsync(verify, "peerB", SyncEntityTypes.MonHoc);
            Assert.Empty(allForB);
        }

        [Fact]
        public async Task Get_ForKeyWithNoSnapshot_ReturnsNull()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;

            using var db = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(db, "peerA", SyncEntityTypes.StudyLog, Guid.NewGuid());

            Assert.Null(row);
        }

        [Fact]
        public async Task Delete_RemovesRow_SubsequentGetReturnsNull()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var entityId = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.TaskNote, entityId, rev: 2,
                    snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }
            using (var db = factory())
            {
                await SyncBaseSnapshotStore.RemoveAsync(db, "peerA", SyncEntityTypes.TaskNote, entityId);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var row = await SyncBaseSnapshotStore.GetAsync(verify, "peerA", SyncEntityTypes.TaskNote, entityId);
            Assert.Null(row);
        }

        [Fact]
        public async Task GetAllForPeerAsync_ReturnsExactlyThatPeerAndEntityType()
        {
            var (conn, factory) = NewDb();
            using var _ = conn;
            var idOne = Guid.NewGuid();
            var idTwo = Guid.NewGuid();
            var idOtherPeer = Guid.NewGuid();
            var idOtherType = Guid.NewGuid();

            using (var db = factory())
            {
                await SyncBaseSnapshotStore.UpsertAsync(db, "peerA", SyncEntityTypes.TaskReferenceLink, idOne, 1, null, DateTime.UtcNow);
                await SyncBaseSnapshotStore.UpsertAsync(db, "peerA", SyncEntityTypes.TaskReferenceLink, idTwo, 2, null, DateTime.UtcNow);
                // Second peer, same entity type — must be absent from peerA's result.
                await SyncBaseSnapshotStore.UpsertAsync(db, "peerB", SyncEntityTypes.TaskReferenceLink, idOtherPeer, 9, null, DateTime.UtcNow);
                // Same peer, different entity type — must also be absent from peerA's TaskReferenceLink result.
                await SyncBaseSnapshotStore.UpsertAsync(db, "peerA", SyncEntityTypes.TaskNote, idOtherType, 9, null, DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var result = await SyncBaseSnapshotStore.GetAllForPeerAsync(verify, "peerA", SyncEntityTypes.TaskReferenceLink);

            Assert.Equal(2, result.Count);
            Assert.Contains(idOne, result.Keys);
            Assert.Contains(idTwo, result.Keys);
            Assert.DoesNotContain(idOtherPeer, result.Keys);
            Assert.DoesNotContain(idOtherType, result.Keys);
        }

        [Fact]
        public void SyncBaseSnapshotRow_DoesNotImplementISyncMetadata()
        {
            // Load-bearing, not decorative — proves SyncStamper stays untouched by
            // construction (its `entry.Entity is not ISyncMetadata meta` skip clause),
            // not by accident. If this ever flips, SyncStamper would start
            // stamping/tombstoning sync bookkeeping rows as if they were user data.
            Assert.False(typeof(ISyncMetadata).IsAssignableFrom(typeof(SyncBaseSnapshotRow)));
        }
    }
}
