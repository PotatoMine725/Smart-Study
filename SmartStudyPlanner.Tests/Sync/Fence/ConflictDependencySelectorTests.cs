using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §3.3, §7.1 step 2; plan §8). <see cref="ConflictDependencySelector"/>
    /// selects unresolved records reached by an <see cref="ImpactSet"/>. Selection is not violation
    /// (INV-3) -- these tests only assert WHICH records are returned, never whether they would go on
    /// to <c>Blocked</c>/<c>Passed</c> (that is <c>FenceRouterTests</c>' concern).
    /// </summary>
    public class ConflictDependencySelectorTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static ImpactSet RowImpact(string entityType, Guid entityId, RowEffect effect = RowEffect.FieldsChanged) =>
            new(new[] { new ImpactRow(entityType, entityId, effect, WasLive: true, entityId) },
                Array.Empty<ImpactEdge>(), Array.Empty<ImpactScope>(), Array.Empty<ImpactLifecycle>());

        [Fact]
        public async Task SelectsAnUnresolvedS1CrRecord_ByScopeKey()
        {
            var (record, _, _, _, _, task) = await _fx.StageS1CrAsync();

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, RowImpact(SyncEntityTypes.StudyTask, task.MaTask));

            Assert.Contains(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task SelectsAnUnresolvedS1PtRecord_ByScopeKey()
        {
            var (record, _, task) = await _fx.StageS1PtAsync();

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, RowImpact(SyncEntityTypes.StudyTask, task.MaTask));

            Assert.Contains(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task DoesNotSelect_AnUnrelatedRecord()
        {
            var (record, _, _, _, _, task) = await _fx.StageS1CrAsync();
            var unrelatedId = Guid.NewGuid();

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, RowImpact(SyncEntityTypes.StudyTask, unrelatedId));

            Assert.DoesNotContain(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task DoesNotSelect_AResolvedRecord()
        {
            var (record, _, _, _, _, task) = await _fx.StageS1CrAsync();

            using (var ctx = _fx.Fx.NewContext())
            {
                var tracked = await ctx.SyncConflictRecords.FindAsync(record.ConflictId);
                tracked!.Status = ConflictRecordStatus.Resolved;
                tracked.ResolvedAtUtc = SyncApplyFixture.LocalNow;
                tracked.ResolvedByDeviceId = SyncApplyFixture.LocalDevice;
                await ctx.SaveChangesAsync();
            }

            using var readCtx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(readCtx, RowImpact(SyncEntityTypes.StudyTask, task.MaTask));

            Assert.DoesNotContain(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task IdentityPredicate_SelectsARecord_WhoseScopeKeyFormatTheSelectorDoesNotOtherwiseReconstruct()
        {
            // A record with a garbled/unrecognised ScopeKey (a future field, a corrupted row) must still
            // be caught by EntityId alone (mission §8 fail-closed net) -- proving the identity predicate
            // does real work, not merely restating the ScopeKey predicate.
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();
            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "garbled-" + Guid.NewGuid().ToString("N"),
                ScopeKey = "not-a-recognised-scope-key-format",
                Kind = ConflictKind.StructuralConflict,
                EntityType = SyncEntityTypes.StudyTask,
                EntityId = task.MaTask,
                FieldName = "MaMonHoc",
                StructuralReason = StructuralReason.ConcurrentReparent,
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = task.MaTask,
                BaseSnapshotJson = "{}",
                BaseFingerprint = "fp",
                LocalEntityId = task.MaTask,
                LocalSnapshotJson = "{}",
                LocalFingerprint = "fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "fp2",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };
            using (var seedCtx = _fx.Fx.NewContext())
            {
                seedCtx.SyncConflictRecords.Add(row);
                await seedCtx.SaveChangesAsync();
            }

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, RowImpact(SyncEntityTypes.StudyTask, task.MaTask));

            Assert.Contains(selected, r => r.ConflictId == row.ConflictId);
        }

        [Fact]
        public async Task ConstraintScope_SelectsAnS2Record_ByScopeKeyOnImpactScope()
        {
            var (record, task, occupant) = await _fx.SeedS2RecordAsync();

            var impact = new ImpactSet(
                Array.Empty<ImpactRow>(), Array.Empty<ImpactEdge>(),
                new[] { new ImpactScope(record.ScopeKey, task.MaTask, ScopeChange.OccupantContentChanged, occupant.Id) },
                Array.Empty<ImpactLifecycle>());

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, impact);

            Assert.Contains(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task ConstraintScope_SelectsAnS3Record_ByOwningTaskIdInImpactRows()
        {
            var (record, task) = await _fx.StageS3Async();

            using var ctx = _fx.Fx.NewContext();
            // The owning StudyTask appears in impact -- e.g. its own tombstone -- which must still reach
            // the TaskNote-scope record through the constraint-value fallback (mission §8).
            var selected = await ConflictDependencySelector.SelectAsync(ctx,
                RowImpact(SyncEntityTypes.StudyTask, task.MaTask, RowEffect.Tombstoned));

            Assert.Contains(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task DeduplicatesByConflictId_WhenBothPredicatesMatchTheSameRecord()
        {
            var (record, _, task) = await _fx.StageS1PtAsync();

            var impact = new ImpactSet(
                new[] { new ImpactRow(SyncEntityTypes.StudyTask, task.MaTask, RowEffect.FieldsChanged, true, task.MaTask) },
                new[] { new ImpactEdge(SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", Guid.NewGuid(), EdgeChange.Removed) },
                Array.Empty<ImpactScope>(), Array.Empty<ImpactLifecycle>());

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, impact);

            Assert.Single(selected, r => r.ConflictId == record.ConflictId);
        }

        [Fact]
        public async Task EmptyImpact_SelectsNothing()
        {
            await _fx.StageS1CrAsync();

            using var ctx = _fx.Fx.NewContext();
            var selected = await ConflictDependencySelector.SelectAsync(ctx, ImpactSet.Empty);

            Assert.Empty(selected);
        }
    }
}
