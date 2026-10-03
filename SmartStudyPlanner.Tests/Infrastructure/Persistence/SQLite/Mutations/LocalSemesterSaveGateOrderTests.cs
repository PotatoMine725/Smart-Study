using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — gate order inside <see cref="LocalSemesterSaveExecutor"/> (plan §5,
    /// §16.2 X-10/X-11, §17 N-6/N-9). "Pass is not authorize": a passed fence still leaves business
    /// validation and persistence free to fail, and both must roll back. A non-passed decision must
    /// never reach the writer.
    /// </summary>
    public class LocalSemesterSaveGateOrderTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;

        public LocalSemesterSaveGateOrderTests() => _save = new LocalSaveDriver(_fence.Fx);

        public void Dispose() => _fence.Dispose();

        private SyncApplyFixture Fx => _fence.Fx;

        private async Task<FenceDecision> DecisionForAsync(HocKy graph)
        {
            using var db = Fx.NewContext();
            var old = await LocalSemesterSaveExecutor.LoadLiveGraphAsync(db, graph.MaHocKy, default);
            return await FenceRouter.EvaluateAsync(db, SemesterReconcilePlanner.Plan(old, graph).Request);
        }

        /// <summary>
        /// X-10 — no records; a task names a MonHoc that is not in the HocKy. The fence passes (shown by
        /// evaluating the same request), the writer's late validation still throws with the Slice-3
        /// message, and nothing is persisted.
        /// </summary>
        [Fact]
        public async Task X10_PassedFence_ThenBusinessValidationFails_NothingPersisted()
        {
            var (hocKy, monHoc, _) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var unknown = Guid.NewGuid();
            var stray = new StudyTask("stray", new DateTime(2026, 3, 3), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = unknown };
            LocalSaveDriver.Mon(graph, monHoc.MaMonHoc).DanhSachTask.Add(stray);

            var decision = await DecisionForAsync(graph);
            Assert.True(decision.RouteKnown);
            Assert.True(decision.FencePassed);

            var before = await _fence.SnapshotAllTablesAsync();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _save.SaveAsync(graph));

            Assert.Equal(SemesterReconcilePlanner.UnknownMonHocMessage("stray", stray.MaTask, unknown, hocKy.MaHocKy), ex.Message);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
        }

        /// <summary>X-11 — no records; the single SaveChanges fails. Rolled back, tables identical, context disposed.</summary>
        [Fact]
        public async Task X11_PassedFence_ThenPersistenceFails_RolledBack_ContextDisposed()
        {
            var (hocKy, monHoc, _) = await Fx.SeedTreeAsync();
            var sibling = new StudyTask("T2", new DateTime(2026, 2, 9), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHoc.MaMonHoc };
            await Fx.AddLocalAsync(sibling);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, sibling.MaTask);

            FailingSaveDbContext? failing = null;
            var repo = new SqliteHocKyRepository(() => failing = Fx.NewFailingContext(failOnSaveNumber: 1));
            var before = await _fence.SnapshotAllTablesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repo.LuuHocKyAsync(graph));

            Assert.StartsWith("injected failure", ex.Message, StringComparison.Ordinal);
            Assert.Equal(1, failing!.SaveAttempts);
            Assert.True(failing.WasDisposed);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
        }

        /// <summary>
        /// N-6 — the executor's gate step, called with a Blocked decision, throws before the writer:
        /// the tracker holds no Added/Modified/Deleted entry and the caller's FK-heal candidate is not
        /// healed. This is the SAME step <c>ExecuteAsync</c> runs (not a parallel copy), so the mutant
        /// "writer before fence" placed there turns this test RED.
        /// </summary>
        [Fact]
        public async Task N6_BlockedDecision_NeverReachesTheWriter()
        {
            var (hocKy, monHoc, task) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, task.MaTask);
            var unstamped = new StudyTask("heal me", new DateTime(2026, 3, 3), LoaiCongViec.BaiTapVeNha, 1);
            LocalSaveDriver.Mon(graph, monHoc.MaMonHoc).DanhSachTask.Add(unstamped);

            var blocked = new FenceDecision(new[]
            {
                new PolicyResult(Guid.NewGuid(), "scope", ConflictShape.ConcurrentReparent,
                    new EntitySubject(SyncEntityTypes.StudyTask, task.MaTask), FenceOutcome.Blocked,
                    RoutingStage.DirectSubject, "S1CR.SubjectRemoved", "constructed"),
            }, RouteKnown: true);

            using var db = Fx.NewContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var old = await LocalSemesterSaveExecutor.LoadLiveGraphAsync(db, hocKy.MaHocKy, default);
            var plan = SemesterReconcilePlanner.Plan(old, graph);

            await Assert.ThrowsAsync<MutationRejectedException>(
                () => LocalSemesterSaveExecutor.ApplyIfPassedAsync(db, old, graph, plan, blocked, default));

            Assert.DoesNotContain(db.ChangeTracker.Entries(),
                e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
            Assert.Equal(Guid.Empty, unstamped.MaMonHoc);
        }

        [Fact]
        public async Task N6_RouteUnknownDecision_NeverReachesTheWriter()
        {
            var (hocKy, _, task) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, task.MaTask);
            var unknownRoute = new FenceDecision(Array.Empty<PolicyResult>(), RouteKnown: false);

            using var db = Fx.NewContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var old = await LocalSemesterSaveExecutor.LoadLiveGraphAsync(db, hocKy.MaHocKy, default);
            var plan = SemesterReconcilePlanner.Plan(old, graph);

            var ex = await Assert.ThrowsAsync<MutationRejectedException>(
                () => LocalSemesterSaveExecutor.ApplyIfPassedAsync(db, old, graph, plan, unknownRoute, default));

            Assert.Contains("RouteKnown is false", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(db.ChangeTracker.Entries(),
                e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        }

        /// <summary>N-9 — a no-change save is an empty request, passes vacuously and writes nothing.</summary>
        [Fact]
        public async Task N9_NoChangeSave_IsAnEmptyRequest_AndANoOp()
        {
            var (hocKy, _, _) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);

            using (var db = Fx.NewContext())
            {
                var plan = SemesterReconcilePlanner.Plan(await LocalSemesterSaveExecutor.LoadLiveGraphAsync(db, hocKy.MaHocKy, default), graph);
                Assert.Empty(plan.Request.Intents);
            }

            var before = await _fence.SnapshotAllTablesAsync();
            await _save.SaveAsync(graph);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
        }

        /// <summary>N-9 on a held row: a no-change save over a semester with an Unresolved record passes.</summary>
        [Fact]
        public async Task N9_NoChangeSave_OverHeldRow_PassesAndWritesNothing()
        {
            var (record, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);

            var before = await _fence.SnapshotAllTablesAsync();
            await _save.SaveAsync(graph);

            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
        }
    }
}
