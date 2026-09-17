using System;
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
    /// Epic 2 / M2.1, T2.2 — change enumeration per peer via the Rev watermark.
    /// Core predicate tested against StudyTask (one entity type is enough — the predicate
    /// is generic, see §6.4), plus one integration-style test exercising
    /// <see cref="SyncChangeEnumerator.GetChangesForPeerAsync"/> across all six entities.
    /// Uses the shared in-memory SQLite fixture pattern (TestDb.Create / SqliteConnection
    /// ":memory:") per this repo's convention.
    /// </summary>
    public class SyncChangeEnumeratorTests
    {
        private static (SqliteConnection conn, Func<AppDbContext> factory) NewDb()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated done */ }
            return (conn, () => TestDb.Create(conn));
        }

        [Fact]
        public async Task NewRow_NoPriorSnapshotForPeer_IsIncluded()
        {
            // Catches: enumerator failing to treat "no snapshot" as "changed".
            var (conn, factory) = NewDb();
            using var _ = conn;

            StudyTask task;
            using (var db = factory())
                task = await TestDb.SeedTaskAsync(db);

            using var verify = factory();
            var allTasks = await verify.StudyTasks.ToListAsync();
            var changes = await SyncChangeEnumerator.EnumerateChangesAsync(
                verify, SyncEntityTypes.StudyTask, allTasks, t => t.MaTask, "peerA");

            Assert.Contains(changes, c => c.EntityId == task.MaTask);
        }

        [Fact]
        public async Task ModifiedRow_RevAboveSnapshot_IsIncluded()
        {
            // Catches: enumerator not detecting a real Rev increase.
            var (conn, factory) = NewDb();
            using var _ = conn;

            StudyTask task;
            using (var db = factory())
                task = await TestDb.SeedTaskAsync(db);

            using (var db = factory())
            {
                var seeded = await db.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, task.MaTask,
                    rev: seeded.Rev, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                // Real SaveChangesAsync path -> SyncStamper bumps Rev for real, not by hand.
                var toModify = await db.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
                toModify.TenTask = "Modified";
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var allTasks = await verify.StudyTasks.ToListAsync();
            var changes = await SyncChangeEnumerator.EnumerateChangesAsync(
                verify, SyncEntityTypes.StudyTask, allTasks, t => t.MaTask, "peerA");

            Assert.Contains(changes, c => c.EntityId == task.MaTask);
        }

        [Fact]
        public async Task RowAtOrBelowWatermark_NotModifiedSinceSnapshot_IsExcluded()
        {
            // Catches: enumerator returning every row unconditionally (ignoring the
            // watermark entirely).
            var (conn, factory) = NewDb();
            using var _ = conn;

            StudyTask task;
            using (var db = factory())
                task = await TestDb.SeedTaskAsync(db);

            using (var db = factory())
            {
                // Derive the snapshot Rev from the row actually read back, not a literal —
                // this is the exact boundary row.Rev > snapshot.Rev must reject.
                var seeded = await db.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, task.MaTask,
                    rev: seeded.Rev, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var allTasks = await verify.StudyTasks.ToListAsync();
            var changes = await SyncChangeEnumerator.EnumerateChangesAsync(
                verify, SyncEntityTypes.StudyTask, allTasks, t => t.MaTask, "peerA");

            Assert.DoesNotContain(changes, c => c.EntityId == task.MaTask);
        }

        [Fact]
        public async Task TombstonedRow_IsIncludedWithIsDeletedTrue()
        {
            // Catches: an accidental tombstone filter (HasQueryFilter, or a stray
            // .Where(!IsDeleted)) creeping into the query path.
            var (conn, factory) = NewDb();
            using var _ = conn;

            StudyTask task;
            using (var db = factory())
                task = await TestDb.SeedTaskAsync(db);

            using (var db = factory())
            {
                var seeded = await db.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, task.MaTask,
                    rev: seeded.Rev, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using (var db = factory())
            {
                // Soft-delete via the real SaveChanges path so SyncStamper stamps
                // IsDeleted + bumps Rev — not by hand-setting IsDeleted.
                var toDelete = await db.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
                db.StudyTasks.Remove(toDelete);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            // Raw DbSet, not a repository — must still see the tombstoned row (§2.3).
            var allTasks = await verify.StudyTasks.ToListAsync();
            var changes = await SyncChangeEnumerator.EnumerateChangesAsync(
                verify, SyncEntityTypes.StudyTask, allTasks, t => t.MaTask, "peerA");

            var changed = Assert.Single(changes, c => c.EntityId == task.MaTask);
            Assert.True(changed.IsDeleted);
        }

        [Fact]
        public async Task PeerIsolation_SnapshottedForPeerA_StillNewForPeerB()
        {
            // Catches: a missing/wrong PeerDeviceId predicate collapsing all peers into
            // one watermark.
            var (conn, factory) = NewDb();
            using var _ = conn;

            StudyTask task;
            using (var db = factory())
                task = await TestDb.SeedTaskAsync(db);

            using (var db = factory())
            {
                // Modify + snapshot for peer A only, at the row's actual current Rev.
                var toModify = await db.StudyTasks.SingleAsync(t => t.MaTask == task.MaTask);
                toModify.TenTask = "Modified for A";
                await db.SaveChangesAsync();
                await SyncBaseSnapshotStore.UpsertAsync(
                    db, "peerA", SyncEntityTypes.StudyTask, task.MaTask,
                    rev: toModify.Rev, snapshotJson: null, syncedAtUtc: DateTime.UtcNow);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var allTasks = await verify.StudyTasks.ToListAsync();

            var changesForA = await SyncChangeEnumerator.EnumerateChangesAsync(
                verify, SyncEntityTypes.StudyTask, allTasks, t => t.MaTask, "peerA");
            var changesForB = await SyncChangeEnumerator.EnumerateChangesAsync(
                verify, SyncEntityTypes.StudyTask, allTasks, t => t.MaTask, "peerB");

            Assert.DoesNotContain(changesForA, c => c.EntityId == task.MaTask);
            Assert.Contains(changesForB, c => c.EntityId == task.MaTask);
        }

        [Fact]
        public async Task GetChangesForPeerAsync_AllSixEntityTypes_IncludingTombstoned()
        {
            // Catches: any one of the six DbSet wiring lines in GetChangesForPeerAsync
            // accidentally going through a filtered path instead of the raw DbSet.
            var (conn, factory) = NewDb();
            using var _ = conn;

            Guid hocKyId, monHocId, taskId, logId, noteId, linkId;

            using (var db = factory())
            {
                var task = await TestDb.SeedTaskAsync(db);
                hocKyId = (await db.HocKys.SingleAsync()).MaHocKy;
                monHocId = (await db.MonHocs.SingleAsync()).MaMonHoc;
                taskId = task.MaTask;

                var log = new StudyLog { MaTask = taskId, NgayHoc = DateTime.Today, SoPhutHoc = 30 };
                var note = new TaskNote { MaTask = taskId, Content = "note" };
                var link = new TaskReferenceLink { MaTask = taskId, Title = "t", Url = "http://x" };
                db.StudyLogs.Add(log);
                db.TaskNotes.Add(note);
                db.TaskReferenceLinks.Add(link);
                await db.SaveChangesAsync();
                logId = log.Id;
                noteId = note.Id;
                linkId = link.Id;

                // Tombstone one of the six (StudyLog) via the real SaveChanges path.
                // No snapshot at all exists for peer P for any of the six.
                var toDelete = await db.StudyLogs.SingleAsync(l => l.Id == logId);
                db.StudyLogs.Remove(toDelete);
                await db.SaveChangesAsync();
            }

            using var verify = factory();
            var changeSet = await SyncChangeEnumerator.GetChangesForPeerAsync(verify, "peerP");

            Assert.Contains(changeSet.HocKys, c => c.EntityId == hocKyId);
            Assert.Contains(changeSet.MonHocs, c => c.EntityId == monHocId);
            Assert.Contains(changeSet.StudyTasks, c => c.EntityId == taskId);
            var logChange = Assert.Single(changeSet.StudyLogs, c => c.EntityId == logId);
            Assert.True(logChange.IsDeleted);
            Assert.Contains(changeSet.TaskNotes, c => c.EntityId == noteId);
            Assert.Contains(changeSet.TaskReferenceLinks, c => c.EntityId == linkId);
        }
    }
}
