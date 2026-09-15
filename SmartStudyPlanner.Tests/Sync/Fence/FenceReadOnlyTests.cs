using System;
using System.Linq;
using System.Threading.Tasks;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §8.3; mission §19; plan §16.2 X-8). Proves the fence is
    /// completely read-only against a real SQLite database: zero saves, zero pending tracker changes,
    /// and byte-identical table snapshots before/after, over both a <c>Blocked</c> and a
    /// <c>FencePassed</c> scenario.
    /// <para>
    /// Discriminating on purpose (mission's own trap, restated here): asserting only
    /// <c>SaveCount == 0</c> would still pass a resolver/selector that forgot one <c>AsNoTracking()</c>
    /// call -- a tracked-but-unsaved read leaves pending entries in the <see cref="Microsoft.EntityFrameworkCore.ChangeTracker"/>
    /// without ever calling <c>SaveChanges</c>. The <c>ChangeTracker.Entries()</c> assertion is what
    /// actually catches that mutant; dropping any <c>AsNoTracking()</c> in <see cref="ImpactResolver"/>,
    /// <see cref="ConflictDependencySelector"/>, or a policy's <c>Derive</c> turns it RED.
    /// </para>
    /// </summary>
    public class FenceReadOnlyTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static MutationRequest Req(params MutationIntent[] intents) =>
            new(MutationOrigin.LocalApplication, intents);

        private static MutationIntent Tombstone(string entityType, Guid entityId) =>
            new(MutationOperation.Tombstone, entityType, entityId, Array.Empty<RelationChange>(), Array.Empty<string>());

        [Fact]
        public async Task BlockedScenario_ZeroSaves_EmptyChangeTracker_TablesUnchanged()
        {
            var (record, _, task) = await _fx.StageS1PtAsync();
            var before = await _fx.SnapshotAllTablesAsync();

            using var ctx = _fx.NewSaveCountingContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(Tombstone(SyncEntityTypes.StudyTask, task.MaTask)));

            Assert.False(decision.FencePassed); // sanity: this really did exercise the selector+policy path
            Assert.Contains(decision.Results, r => r.ConflictId == record.ConflictId && r.Outcome == FenceOutcome.Blocked);

            Assert.Equal(0, ctx.SaveCount);
            Assert.Empty(ctx.ChangeTracker.Entries());

            var after = await _fx.SnapshotAllTablesAsync();
            Assert.Equal(before, after);
        }

        [Fact]
        public async Task FencePassedScenario_ZeroSaves_EmptyChangeTracker_TablesUnchanged()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync(); // no unresolved records at all
            var before = await _fx.SnapshotAllTablesAsync();

            using var ctx = _fx.NewSaveCountingContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(Tombstone(SyncEntityTypes.StudyTask, task.MaTask)));

            Assert.True(decision.FencePassed);

            Assert.Equal(0, ctx.SaveCount);
            Assert.Empty(ctx.ChangeTracker.Entries());

            var after = await _fx.SnapshotAllTablesAsync();
            Assert.Equal(before, after);
        }

        [Fact]
        public async Task ImpactResolver_Alone_LeavesTheChangeTrackerEmpty()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();

            using var ctx = _fx.NewSaveCountingContext();
            await ImpactResolver.ResolveAsync(ctx, Req(Tombstone(SyncEntityTypes.StudyTask, task.MaTask)));

            Assert.Equal(0, ctx.SaveCount);
            Assert.Empty(ctx.ChangeTracker.Entries());
        }

        [Fact]
        public async Task ConflictDependencySelector_Alone_LeavesTheChangeTrackerEmpty()
        {
            var (_, _, _, _, _, task) = await _fx.StageS1CrAsync();
            var impact = new ImpactSet(
                new[] { new ImpactRow(SyncEntityTypes.StudyTask, task.MaTask, RowEffect.Tombstoned, true, task.MaTask) },
                Array.Empty<ImpactEdge>(), Array.Empty<ImpactScope>(), Array.Empty<ImpactLifecycle>());

            using var ctx = _fx.NewSaveCountingContext();
            await ConflictDependencySelector.SelectAsync(ctx, impact);

            Assert.Equal(0, ctx.SaveCount);
            Assert.Empty(ctx.ChangeTracker.Entries());
        }
    }
}
