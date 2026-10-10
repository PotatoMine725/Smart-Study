using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// D-5 (rulings 2026-10-03 §2, V2): a local semester save that fails for any reason other than a
    /// fence rejection removes every UNSAVED ADDITION (a MonHoc/StudyTask with no persisted live row,
    /// per a fresh read) from the caller's graph, and keeps scalar edits and pending deletions. The
    /// original exception is rethrown; a failure of the repair rides along in <c>Exception.Data</c>.
    /// Shape K (owner ruling 2026-10-10): an unsaved MonHoc that holds a persisted task is kept, with
    /// its pre-call collection, minus unsaved tasks.
    /// <para>
    /// Failures are injected INSIDE the executor (at BEGIN, or at SaveChanges after the writer ran),
    /// through the context factory -- a repository decorator that throws before the executor would
    /// never reach the repair.
    /// </para>
    /// </summary>
    public class LocalSemesterSaveFailedSaveRepairTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly ContextScript _script;
        private readonly SqliteHocKyRepository _repo;

        public LocalSemesterSaveFailedSaveRepairTests()
        {
            _conn = TestDb.OpenConnection();
            using (TestDb.Create(_conn)) { /* EnsureCreated */ }
            _script = new ContextScript(_conn);
            _repo = new SqliteHocKyRepository(_script.Create);
        }

        public void Dispose() => _conn.Dispose();

        /// <summary>Hands out the queued contexts first, then ordinary ones; counts every context created.</summary>
        private sealed class ContextScript
        {
            private readonly SqliteConnection _conn;
            private readonly Queue<Func<AppDbContext>> _next = new();

            public ContextScript(SqliteConnection conn) => _conn = conn;

            public int Created { get; private set; }

            private DbContextOptions<AppDbContext> Options(params IInterceptor[] interceptors) =>
                new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).AddInterceptors(interceptors).Options;

            public void FailNextSave() => _next.Enqueue(() => new FailingSaveDbContext(Options(), failOnSaveNumber: 1));

            public void FailNextBegin() => _next.Enqueue(() => new AppDbContext(Options(new BusyAtBeginInterceptor())));

            public void ThenRun(Func<AppDbContext> step) => _next.Enqueue(step);

            public AppDbContext Normal() => TestDb.Create(_conn);

            public AppDbContext Create()
            {
                Created++;
                return _next.Count > 0 ? _next.Dequeue()() : Normal();
            }
        }

        /// <summary>BEGIN is where SQLITE_BUSY surfaces after the busy timeout (P0-b): the write lock is taken there.</summary>
        private sealed class BusyAtBeginInterceptor : DbTransactionInterceptor
        {
            public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
                DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
                CancellationToken cancellationToken = default) =>
                throw new SqliteException("database is locked (injected at BEGIN)", 5);
        }

        private async Task<(HocKy Graph, MonHoc Toan, StudyTask T1, StudyTask T2)> SeedAndReloadAsync()
        {
            var hocKy = new HocKy("HK", new DateTime(2026, 1, 5));
            var toan = new MonHoc("Toan", 3) { MaHocKy = hocKy.MaHocKy };
            var t1 = new StudyTask("T1", new DateTime(2026, 2, 2), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = toan.MaMonHoc };
            var t2 = new StudyTask("T2", new DateTime(2026, 2, 3), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = toan.MaMonHoc };
            toan.DanhSachTask.Add(t1);
            toan.DanhSachTask.Add(t2);
            hocKy.DanhSachMonHoc.Add(toan);
            await _repo.LuuHocKyAsync(hocKy);

            var graph = (await _repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKy.MaHocKy);
            var mon = graph.DanhSachMonHoc.Single();
            return (graph, mon, mon.DanhSachTask.Single(t => t.MaTask == t1.MaTask), mon.DanhSachTask.Single(t => t.MaTask == t2.MaTask));
        }

        private static StudyTask NewTask(MonHoc owner, string ten)
        {
            var task = new StudyTask(ten, new DateTime(2026, 3, 3), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = owner.MaMonHoc };
            owner.DanhSachTask.Add(task);
            return task;
        }

        private static MonHoc NewMon(HocKy graph, string ten)
        {
            var mon = new MonHoc(ten, 2) { MaHocKy = graph.MaHocKy };
            graph.DanhSachMonHoc.Add(mon);
            return mon;
        }

        private async Task<List<StudyTask>> LiveTasksAsync()
        {
            using var ctx = _script.Normal();
            return await ctx.StudyTasks.AsNoTracking().Where(t => !t.IsDeleted).ToListAsync();
        }

        private async Task<List<MonHoc>> LiveMonHocsAsync()
        {
            using var ctx = _script.Normal();
            return await ctx.MonHocs.AsNoTracking().Where(m => !m.IsDeleted).ToListAsync();
        }

        private static IEnumerable<StudyTask> AllTasks(HocKy graph) => graph.DanhSachMonHoc.SelectMany(m => m.DanhSachTask);

        /// <summary>
        /// The ruled split, after the writer ran (failure at SaveChanges): unsaved additions at both
        /// levels are gone, while a scalar edit and a pending deletion on persisted rows stay in memory
        /// and are written by the next successful save. Mutants: no repair ⇒ the additions are written
        /// later; V1 (full restore) ⇒ the edit and the deletion are lost.
        /// </summary>
        [Fact]
        public async Task SaveChangesFails_UnsavedAdditionsRemoved_EditAndPendingDeletionKept_NextSaveWritesThem()
        {
            var (graph, toan, t1, t2) = await SeedAndReloadAsync();
            var toanCollection = toan.DanhSachTask;
            t1.TenTask = "T1 edited";
            toan.DanhSachTask.Remove(t2);
            var n1 = NewTask(toan, "N1");
            var hoa = NewMon(graph, "Hoa");
            var n2 = NewTask(hoa, "N2");

            _script.FailNextSave();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.LuuHocKyAsync(graph));
            Assert.StartsWith("injected failure", ex.Message, StringComparison.Ordinal);
            Assert.Null(LocalSaveRejection.RepairFailureOf(ex));

            Assert.Equal(new[] { toan }, graph.DanhSachMonHoc);
            Assert.Same(toanCollection, toan.DanhSachTask);
            Assert.Equal(new[] { t1 }, toan.DanhSachTask);
            Assert.Equal("T1 edited", t1.TenTask);
            Assert.DoesNotContain(AllTasks(graph), t => t.MaTask == n1.MaTask || t.MaTask == n2.MaTask);
            Assert.Equal(new[] { "T1", "T2" }, (await LiveTasksAsync()).Select(t => t.TenTask).OrderBy(s => s));

            await _repo.LuuHocKyAsync(graph);

            var live = Assert.Single(await LiveTasksAsync());
            Assert.Equal(t1.MaTask, live.MaTask);
            Assert.Equal("T1 edited", live.TenTask);
            Assert.Equal(toan.MaMonHoc, Assert.Single(await LiveMonHocsAsync()).MaMonHoc);
        }

        /// <summary>
        /// The MoFocusMode shape: FocusViewModel adds the session's minutes to the graph's own task, then
        /// the dashboard saves. A transient failure must not erase that time (the reason V2 was chosen).
        /// </summary>
        [Fact]
        public async Task FocusMinutes_SurviveATransientFailure_AndArePersistedByTheNextSave()
        {
            var (graph, _, t1, _) = await SeedAndReloadAsync();
            t1.ThoiGianDaHoc += 25;

            _script.FailNextSave();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.LuuHocKyAsync(graph));

            Assert.Equal(25, t1.ThoiGianDaHoc);
            Assert.Contains(t1, AllTasks(graph));

            await _repo.LuuHocKyAsync(graph);

            Assert.Equal(25, (await LiveTasksAsync()).Single(t => t.MaTask == t1.MaTask).ThoiGianDaHoc);
        }

        /// <summary>
        /// The ruling's motivating transient case: SQLITE_BUSY surfaces at BEGIN, which sits OUTSIDE the
        /// transaction's try. The repair must cover it too. Mutant: repair only in the rollback catch ⇒ RED.
        /// </summary>
        [Fact]
        public async Task BusyAtBegin_TheRepairStillRuns()
        {
            var (graph, toan, t1, _) = await SeedAndReloadAsync();
            var n1 = NewTask(toan, "N1");
            t1.ThoiGianDaHoc += 10;

            _script.FailNextBegin();
            var ex = await Assert.ThrowsAsync<SqliteException>(() => _repo.LuuHocKyAsync(graph));
            Assert.Equal(5, ex.SqliteErrorCode);

            Assert.Equal(new[] { toan }, graph.DanhSachMonHoc);            // persisted rows stay ...
            Assert.Contains(t1, toan.DanhSachTask);
            Assert.DoesNotContain(n1, toan.DanhSachTask);                  // ... the addition goes
            Assert.Equal(10, t1.ThoiGianDaHoc);
        }

        /// <summary>
        /// The repair's own failure (here: the fresh read cannot get a context) never replaces the save's
        /// exception; it is attached under <see cref="LocalSaveRejection.RepairFailureDataKey"/>.
        /// Mutant: no try/catch around the repair ⇒ the repair's exception surfaces instead ⇒ RED.
        /// </summary>
        [Fact]
        public async Task RepairFailure_TheOriginalExceptionSurfaces_WithTheRepairErrorAttached()
        {
            var (graph, toan, _, _) = await SeedAndReloadAsync();
            NewTask(toan, "N1");

            _script.FailNextSave();
            _script.ThenRun(() => throw new TimeoutException("fresh read failed"));
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.LuuHocKyAsync(graph));

            Assert.StartsWith("injected failure", ex.Message, StringComparison.Ordinal);
            var repairFailure = Assert.IsType<TimeoutException>(LocalSaveRejection.RepairFailureOf(ex));
            Assert.Equal("fresh read failed", repairFailure.Message);
            Assert.Same(repairFailure, ex.Data[LocalSaveRejection.RepairFailureDataKey]);
        }

        /// <summary>
        /// Shape K (owner ruling 2026-10-10, appended to rulings §2): a persisted task moved into an
        /// unsaved MonHoc. Dropping the MonHoc would leave the task in no collection, and the next save
        /// would tombstone a persisted row. Instead the MonHoc is kept with its PRE-CALL collection (the
        /// writer had swapped in a new one holding the repository's instance of the task), minus its
        /// unsaved tasks. Mutant: no K ⇒ the task is deleted by the next save ⇒ RED.
        /// </summary>
        [Fact]
        public async Task PersistedTaskMovedIntoUnsavedMonHoc_ThatMonHocIsKept_WithItsOwnCollection_NothingDeletedLater()
        {
            var (graph, toan, t1, t2) = await SeedAndReloadAsync();
            var hoa = NewMon(graph, "Hoa");
            var hoaCollection = hoa.DanhSachTask;
            toan.DanhSachTask.Remove(t1);
            t1.MaMonHoc = hoa.MaMonHoc;
            hoa.DanhSachTask.Add(t1);
            var n1 = NewTask(hoa, "N1");

            _script.FailNextSave();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.LuuHocKyAsync(graph));

            Assert.Equal(new[] { toan, hoa }, graph.DanhSachMonHoc);
            Assert.Same(hoaCollection, hoa.DanhSachTask);
            Assert.Equal(new[] { t1 }, hoa.DanhSachTask);                 // the caller's own instance
            Assert.Equal(hoa.MaMonHoc, t1.MaMonHoc);                       // the move (an FK edit) is kept
            Assert.Equal(new[] { t2 }, toan.DanhSachTask);
            Assert.DoesNotContain(AllTasks(graph), t => t.MaTask == n1.MaTask);

            await _repo.LuuHocKyAsync(graph);

            var live = await LiveTasksAsync();
            Assert.Equal(hoa.MaMonHoc, live.Single(t => t.MaTask == t1.MaTask).MaMonHoc);
            Assert.DoesNotContain(live, t => t.MaTask == n1.MaTask);
            Assert.Contains(await LiveMonHocsAsync(), m => m.MaMonHoc == hoa.MaMonHoc);
        }

        /// <summary>
        /// "Persisted" is decided by the FRESH read, not by the failed transaction's old graph: here the
        /// same MonHoc + task were committed by another save between the failure and the repair, so they
        /// are no longer additions and stay -- and the MonHoc gets its pre-call collection back (the
        /// writer had replaced it), not a half-built one. Mutant: decide from the old graph, or restore
        /// collections only for shape K ⇒ RED.
        /// </summary>
        [Fact]
        public async Task RowsCommittedElsewhereBeforeTheRepair_AreKept_WithTheCallersCollection()
        {
            var (graph, _, _, _) = await SeedAndReloadAsync();
            var hoa = NewMon(graph, "Hoa");
            var hoaCollection = hoa.DanhSachTask;
            var n1 = NewTask(hoa, "N1");

            _script.FailNextSave();
            _script.ThenRun(() =>
            {
                using (var other = _script.Normal())
                {
                    var hoaCopy = new MonHoc("Hoa", 2) { MaMonHoc = hoa.MaMonHoc, MaHocKy = graph.MaHocKy };
                    other.MonHocs.Add(hoaCopy);
                    other.StudyTasks.Add(new StudyTask("N1", n1.HanChot, n1.LoaiTask, n1.DoKho) { MaTask = n1.MaTask, MaMonHoc = hoa.MaMonHoc });
                    other.SaveChanges();
                }
                return _script.Normal();
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.LuuHocKyAsync(graph));

            Assert.Contains(hoa, graph.DanhSachMonHoc);
            Assert.Same(hoaCollection, hoa.DanhSachTask);
            Assert.Equal(new[] { n1 }, hoa.DanhSachTask);
        }

        /// <summary>
        /// Edge recorded, not ruled (rulings §2): a failed create of a semester with no persisted row.
        /// Every MonHoc is an unsaved addition and is removed; the root HocKy stays (it cannot be removed
        /// from a caller). In production this is a no-op: SetupViewModel.TaoHocKy saves a fresh HocKy
        /// with no MonHoc, held in a local that a failure discards. Pinned as OBSERVED.
        /// </summary>
        [Fact]
        public async Task NewSemesterCreateFails_EveryMonHocRemoved_RootKept_Observed()
        {
            var hocKy = new HocKy("HK new", new DateTime(2026, 1, 5));
            var mon = NewMon(hocKy, "Toan");
            NewTask(mon, "N1");

            _script.FailNextSave();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.LuuHocKyAsync(hocKy));

            Assert.Empty(hocKy.DanhSachMonHoc);
            Assert.Equal("HK new", hocKy.Ten);

            await _repo.LuuHocKyAsync(hocKy);
            Assert.Empty(await LiveMonHocsAsync());
            Assert.Empty(await LiveTasksAsync());
        }

        /// <summary>
        /// The fence-rejection path is OD-7's (full restore, Slice 4), not D-5's: on a rejection the
        /// repair does not run -- no second context is opened for a fresh read, and nothing is attached.
        /// (After a successful OD-7 restore a V2 pass would remove nothing, so graph-shape tests cannot
        /// tell the two apart; the context count can.) Mutant: repair on the fence path too ⇒ RED.
        /// </summary>
        [Fact]
        public async Task FenceRejection_DoesNotRunTheRepair()
        {
            using var fence = new FenceScenarioFixture();
            var (_, hocKy, _, _, _, task) = await fence.StageS1CrAsync();
            var graph = await new LocalSaveDriver(fence.Fx).LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            var contexts = 0;
            var repo = new SqliteHocKyRepository(() => { contexts++; return fence.Fx.NewContext(); });
            var ex = await Assert.ThrowsAsync<MutationRejectedException>(() => repo.LuuHocKyAsync(graph));

            Assert.Equal(1, contexts);
            Assert.False(ex.Data.Contains(LocalSaveRejection.RepairFailureDataKey));
        }
    }
}
