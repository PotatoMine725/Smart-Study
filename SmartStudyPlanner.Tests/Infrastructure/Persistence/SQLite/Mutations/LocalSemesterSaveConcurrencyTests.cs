using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — X-12 (plan §14, §16.2): pass is not authorize under concurrency.
    /// Two genuinely independent connections on a FILE-backed SQLite database
    /// (<see cref="FileBackedSqliteFixture"/>, WAL as <c>EnsureCreated</c> leaves it).
    /// <para>
    /// The interleave is forced, not raced: a command interceptor on the executor's context fires
    /// ONCE, right after the fence's conflict-record selector has read <c>SyncConflictRecords</c> (and
    /// found nothing, so the fence passes). At that instant a second connection tries to commit an
    /// Unresolved S1-CR record on the very task the save is deleting. With the fence evaluated inside
    /// the executor's own transaction (write lock from BEGIN, P0-b), that insert cannot commit until
    /// the save is over; it times out BUSY. The invariant asserted is the plan's: never both a
    /// committed Unresolved record on T and a committed tombstone of T.
    /// </para>
    /// <para>
    /// Mutant that must turn this RED: evaluate the fence on a separate, earlier context (no
    /// transaction) -- the second connection then commits between the fence read and the write, and
    /// the executor tombstones T anyway.
    /// </para>
    /// </summary>
    public class LocalSemesterSaveConcurrencyTests
    {
        private readonly ITestOutputHelper _out;

        public LocalSemesterSaveConcurrencyTests(ITestOutputHelper output) => _out = output;

        private sealed class AfterConflictSelectHook : DbCommandInterceptor
        {
            private readonly Func<Task> _onFirstSelect;
            private int _fired;

            public AfterConflictSelectHook(Func<Task> onFirstSelect) => _onFirstSelect = onFirstSelect;

            public bool Fired => _fired == 1;

            public override async ValueTask<DbDataReader> ReaderExecutedAsync(
                DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
            {
                if (command.CommandText.Contains("\"SyncConflictRecords\"", StringComparison.Ordinal)
                    && Interlocked.Exchange(ref _fired, 1) == 0)
                {
                    await _onFirstSelect();
                }
                return result;
            }
        }

        private static string ConnectionString(FileBackedSqliteFixture fx, int timeoutSeconds) =>
            new SqliteConnectionStringBuilder { DataSource = fx.Path, Pooling = false, DefaultTimeout = timeoutSeconds }.ToString();

        private static AppDbContext NewContext(FileBackedSqliteFixture fx, int timeoutSeconds, IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ConnectionString(fx, timeoutSeconds));
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new AppDbContext(builder.Options)
            {
                Clock = () => SyncApplyFixture.LocalNow,
                DeviceIdProvider = () => SyncApplyFixture.LocalDevice,
            };
        }

        private static SyncConflictRecordRow S1CrRecordOn(StudyTask task) => new()
        {
            ConflictId = Guid.NewGuid(),
            ConflictKey = "x12-" + Guid.NewGuid().ToString("N"),
            ScopeKey = ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", null),
            Kind = ConflictKind.StructuralConflict,
            EntityType = SyncEntityTypes.StudyTask,
            EntityId = task.MaTask,
            FieldName = "MaMonHoc",
            StructuralReason = StructuralReason.ConcurrentReparent,
            PeerDeviceId = SyncApplyFixture.PeerDevice,
            BaseEntityId = task.MaTask,
            BaseSnapshotJson = "{}",
            BaseFingerprint = "x12-base",
            LocalEntityId = task.MaTask,
            LocalSnapshotJson = "{}",
            LocalFingerprint = "x12-local",
            LocalRowRev = 1,
            RemoteEntityId = task.MaTask,
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "x12-remote",
            Status = ConflictRecordStatus.Unresolved,
            CreatedAtUtc = SyncApplyFixture.LocalNow,
            CreatedByDeviceId = SyncApplyFixture.PeerDevice,
        };

        [Fact]
        public async Task X12_RecordStagedOnTaskBetweenFenceReadAndWrite_NeverCommitsAlongsideItsTombstone()
        {
            using var fx = new FileBackedSqliteFixture();
            Assert.Equal("wal", fx.ReadJournalMode());

            var hocKy = new HocKy("HK", new DateTime(2026, 1, 5));
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("T", new DateTime(2026, 2, 2), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = monHoc.MaMonHoc };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            using (var seed = NewContext(fx, 2))
            {
                seed.HocKys.Add(hocKy);
                await seed.SaveChangesAsync();
            }

            Exception? secondLegFailure = null;
            var secondLegCommitted = false;
            var hook = new AfterConflictSelectHook(async () =>
            {
                try
                {
                    using var other = NewContext(fx, timeoutSeconds: 1);
                    other.SyncConflictRecords.Add(S1CrRecordOn(task));
                    await other.SaveChangesAsync();
                    secondLegCommitted = true;
                }
                catch (Exception ex)
                {
                    secondLegFailure = ex;
                }
            });

            var reader = new SqliteHocKyRepository(() => NewContext(fx, 2));
            var graph = (await reader.LayDanhSachHocKyAsync()).Single();
            graph.DanhSachMonHoc[0].DanhSachTask.Clear();                       // XoaTask(T)

            var saver = new SqliteHocKyRepository(() => NewContext(fx, 5, hook));
            Exception? saveFailure = null;
            try { await saver.LuuHocKyAsync(graph); }
            catch (Exception ex) { saveFailure = ex; }

            using var check = NewContext(fx, 2);
            var recordCommitted = await check.SyncConflictRecords.AsNoTracking()
                .AnyAsync(r => r.EntityId == task.MaTask && r.Status == ConflictRecordStatus.Unresolved);
            var taskTombstoned = (await check.StudyTasks.AsNoTracking().SingleAsync(t => t.MaTask == task.MaTask)).IsDeleted;

            SqliteException? busy = null;
            for (var e = secondLegFailure; e is not null && busy is null; e = e.InnerException) busy = e as SqliteException;

            _out.WriteLine($"hookFired={hook.Fired} secondLegCommitted={secondLegCommitted} " +
                           $"secondLegFailure={secondLegFailure?.GetType().Name}:{busy?.SqliteErrorCode} " +
                           $"saveFailure={saveFailure?.GetType().Name} recordCommitted={recordCommitted} taskTombstoned={taskTombstoned}");

            Assert.True(hook.Fired);                                              // the interleave really happened
            Assert.False(recordCommitted && taskTombstoned);                      // the invariant (plan §16.2 X-12)

            // What the correct gate order produces (OBSERVED shape, not a second invariant): the second
            // leg is refused BUSY by the save's write lock, and the save commits the tombstone.
            Assert.Null(saveFailure);
            Assert.True(taskTombstoned);
            Assert.False(secondLegCommitted);
            Assert.Equal(5, busy?.SqliteErrorCode);                               // SQLITE_BUSY
        }
    }
}
