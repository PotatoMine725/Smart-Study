using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Tests.Fixtures
{
    /// <summary>
    /// Shared real-SQLite fixture for the Epic 2 / T2.4 PR-5 apply-session tests. Persistence behaviour
    /// (transactions, triggers, partial unique indexes, identity resolution) is the thing under test, so
    /// none of it is mocked — the DoR's own instruction for this slice.
    /// <para>
    /// One in-memory connection is shared by every context the test and the session create, which is
    /// what makes a context-per-operation session observable at all. It also means a reader on another
    /// context would see the session's UNCOMMITTED rows while its transaction is open, so tests only
    /// read after the session call returns.
    /// </para>
    /// <para>
    /// The session's own local identity (<see cref="LocalDevice"/> / <see cref="LocalNow"/>) is kept
    /// deliberately distinct from the peer's (<see cref="PeerDevice"/>): if they matched, "provenance
    /// preserved from the remote" and "provenance re-stamped locally" would produce identical bytes and
    /// most assertions in this suite would pass against a broken implementation. Same trap the PR-2
    /// seam tests document in their header.
    /// </para>
    /// </summary>
    internal sealed class SyncApplyFixture : IDisposable
    {
        public const string LocalDevice = "LOCAL-DEVICE";
        public const string PeerDevice = "PEER-DEVICE";
        public const string OtherPeerDevice = "OTHER-PEER";

        /// <summary>The session's clock: baseline SyncedAtUtc and ConflictRecord.CreatedAtUtc.</summary>
        public static readonly DateTime LocalNow = new(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);

        /// <summary>Seed time for local rows — earlier than <see cref="RemoteLater"/> so LWW is decidable.</summary>
        public static readonly DateTime LocalEarlier = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

        public static readonly DateTime RemoteLater = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime RemoteEarlier = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

        private readonly SqliteConnection _conn;

        public SyncApplyFixture()
        {
            _conn = TestDb.OpenConnection();
            using var ctx = NewContext();
            SyncConflictRecordSchema.EnsureTable(ctx);   // EnsureCreated() never creates the triggers
        }

        public void Dispose() => _conn.Dispose();

        /// <summary>A context with deterministic local stamping sources.</summary>
        public AppDbContext NewContext(DateTime? nowUtc = null, string? deviceId = null)
        {
            var ctx = TestDb.Create(_conn);
            var now = nowUtc ?? LocalNow;
            var device = deviceId ?? LocalDevice;
            ctx.Clock = () => now;
            ctx.DeviceIdProvider = () => device;
            return ctx;
        }

        /// <summary>The factory the session uses: one fresh context per logical operation.</summary>
        public Func<AppDbContext> Factory => () => NewContext();

        public SyncApplySession Session() => new(Factory);

        /// <summary>
        /// A context that throws on its N-th save, bound to the same database. Used to reach the
        /// failure DoR §11.2 makes possible but nothing else can trigger from outside: the SECOND save
        /// inside one operation's transaction.
        /// </summary>
        public TestDoubles.FailingSaveDbContext NewFailingContext(int failOnSaveNumber)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
            var ctx = new TestDoubles.FailingSaveDbContext(options, failOnSaveNumber);
            ctx.Database.EnsureCreated();
            ctx.Clock = () => LocalNow;
            ctx.DeviceIdProvider = () => LocalDevice;
            return ctx;
        }

        // ------------------------------------------------------------------ seeding

        /// <summary>
        /// Seeds HocKy -> MonHoc -> StudyTask through the ordinary local write path, so every row
        /// carries local provenance (<see cref="LocalEarlier"/> / <see cref="LocalDevice"/>) and
        /// <c>Rev = 1</c>.
        /// </summary>
        public async Task<(HocKy HocKy, MonHoc MonHoc, StudyTask Task)> SeedTreeAsync(DateTime? at = null)
        {
            using var ctx = NewContext(at ?? LocalEarlier);
            var hocKy = new HocKy("HK", new DateTime(2026, 1, 5));
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Task", new DateTime(2026, 2, 2), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHoc.MaMonHoc,
            };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            ctx.HocKys.Add(hocKy);
            await ctx.SaveChangesAsync();
            return (hocKy, monHoc, task);
        }

        /// <summary>Adds one row through the ordinary local path and returns it, re-read and detached.</summary>
        public async Task AddLocalAsync(ISyncMetadata entity, DateTime? at = null)
        {
            using var ctx = NewContext(at ?? LocalEarlier);
            ctx.Add(entity);
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Records a baseline row for one peer from an entity's current state — i.e. "this peer last saw
        /// the row looking exactly like this". Written through the PR-3 staging API plus an explicit
        /// save, which is what the caller owning the transaction does.
        /// </summary>
        public async Task SetBaselineAsync(string peerId, ISyncMetadata entity, long? rev = null)
        {
            var change = IncomingChanges.Of(entity);
            using var ctx = NewContext();
            await SyncBaseSnapshotStore.UpsertAsync(
                ctx, peerId, change.EntityType, change.EntityId,
                rev ?? entity.Rev, CanonicalJson.Write(change.Snapshot), LocalNow);
            await ctx.SaveChangesAsync();
        }

        /// <summary>Writes a raw baseline row, for the fail-closed "unreadable SnapshotJson" case.</summary>
        public async Task SetRawBaselineAsync(string peerId, string entityType, Guid entityId, long rev, string? json)
        {
            using var ctx = NewContext();
            await SyncBaseSnapshotStore.UpsertAsync(ctx, peerId, entityType, entityId, rev, json, LocalNow);
            await ctx.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ reading back

        public async Task<HocKy?> ReadHocKyAsync(Guid id)
        {
            using var ctx = NewContext();
            return await ctx.HocKys.AsNoTracking().FirstOrDefaultAsync(h => h.MaHocKy == id);
        }

        public async Task<MonHoc?> ReadMonHocAsync(Guid id)
        {
            using var ctx = NewContext();
            return await ctx.MonHocs.AsNoTracking().FirstOrDefaultAsync(m => m.MaMonHoc == id);
        }

        public async Task<StudyTask?> ReadTaskAsync(Guid id)
        {
            using var ctx = NewContext();
            return await ctx.StudyTasks.AsNoTracking().FirstOrDefaultAsync(t => t.MaTask == id);
        }

        public async Task<TaskNote?> ReadNoteAsync(Guid id)
        {
            using var ctx = NewContext();
            return await ctx.TaskNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<TaskNote?> ReadNoteInScopeAsync(Guid maTask)
        {
            using var ctx = NewContext();
            return await ctx.TaskNotes.AsNoTracking().FirstOrDefaultAsync(n => n.MaTask == maTask);
        }

        public async Task<TaskReferenceLink?> ReadLinkAsync(Guid id)
        {
            using var ctx = NewContext();
            return await ctx.TaskReferenceLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<StudyLog?> ReadLogAsync(Guid id)
        {
            using var ctx = NewContext();
            return await ctx.StudyLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<SyncBaseSnapshotRow?> ReadBaselineAsync(string peerId, string entityType, Guid entityId)
        {
            using var ctx = NewContext();
            return await ctx.SyncBaseSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(r => r.PeerDeviceId == peerId && r.EntityType == entityType && r.EntityId == entityId);
        }

        public async Task<System.Collections.Generic.List<SyncConflictRecordRow>> ReadConflictsAsync()
        {
            using var ctx = NewContext();
            return await ctx.SyncConflictRecords.AsNoTracking().ToListAsync();
        }

        public async Task<int> CountNotesInScopeAsync(Guid maTask)
        {
            using var ctx = NewContext();
            return await ctx.TaskNotes.AsNoTracking().CountAsync(n => n.MaTask == maTask);
        }

        // ------------------------------------------------------------------ building remote input

        /// <summary>
        /// Builds the incoming change for a detached entity carrying the provenance the remote peer
        /// would have written. The entity is never attached to any context — it is a value used only to
        /// produce an <see cref="EntitySnapshot"/>, which is all the session accepts.
        /// </summary>
        public static IncomingChangeSet From(params ISyncMetadata[] remoteRows)
        {
            var changes = new System.Collections.Generic.List<IncomingChange>(remoteRows.Length);
            foreach (var row in remoteRows) changes.Add(IncomingChanges.Of(row));
            return new IncomingChangeSet(PeerDevice, changes);
        }

        public static void Stamp(ISyncMetadata entity, DateTime modifiedAtUtc, string deviceId,
                                 bool isDeleted = false, DateTime? deletedAtUtc = null)
        {
            entity.ModifiedAtUtc = modifiedAtUtc;
            entity.ModifiedByDeviceId = deviceId;
            entity.IsDeleted = isDeleted;
            entity.DeletedAtUtc = isDeleted ? deletedAtUtc ?? modifiedAtUtc : null;
        }
    }
}
