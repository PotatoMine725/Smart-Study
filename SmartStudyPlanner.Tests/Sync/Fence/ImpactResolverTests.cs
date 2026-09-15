using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §3.1, §5; plan §7.3-§7.4). <see cref="ImpactResolver"/>
    /// derives <c>I(m) = Rows ∪ Edges ∪ ConstraintScopes ∪ LifecycleEffects</c> for a
    /// <see cref="MutationRequest"/>, expanding a <see cref="MutationOperation.Tombstone"/> only
    /// through the ACTUAL live cascade (INV-2) -- never an invented closure.
    /// </summary>
    public class ImpactResolverTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static MutationRequest Req(params MutationIntent[] intents) =>
            new(MutationOrigin.LocalApplication, intents);

        [Fact]
        public async Task Create_MonHoc_ProducesRowLifecycleAndAddedEdge()
        {
            var (hocKy, _, _) = await _fx.Fx.SeedTreeAsync();
            var newMonHocId = Guid.NewGuid();

            var request = Req(new MutationIntent(MutationOperation.Create, SyncEntityTypes.MonHoc, newMonHocId,
                new[] { new RelationChange("MaHocKy", null, hocKy.MaHocKy) }, Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            var row = Assert.Single(impact.Rows);
            Assert.Equal(SyncEntityTypes.MonHoc, row.EntityType);
            Assert.Equal(newMonHocId, row.EntityId);
            Assert.Equal(RowEffect.Created, row.Effect);

            Assert.Single(impact.Lifecycle, l => l.EntityType == SyncEntityTypes.MonHoc && l.EntityId == newMonHocId && l.Effect == LifecycleEffect.Create);
            Assert.Single(impact.Edges, e => e.ChildType == SyncEntityTypes.MonHoc && e.ChildId == newMonHocId &&
                                              e.Field == "MaHocKy" && e.ParentId == hocKy.MaHocKy && e.Change == EdgeChange.Added);
            Assert.Empty(impact.Scopes);
        }

        [Fact]
        public async Task Create_TaskNote_AcquiresItsConstraintScope()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();
            var newNoteId = Guid.NewGuid();

            var request = Req(new MutationIntent(MutationOperation.Create, SyncEntityTypes.TaskNote, newNoteId,
                new[] { new RelationChange("MaTask", null, task.MaTask) }, Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            var scope = Assert.Single(impact.Scopes);
            Assert.Equal(ScopeChange.Acquired, scope.Change);
            Assert.Equal(task.MaTask, scope.ScopeValue);
            Assert.Equal(newNoteId, scope.OccupantId);
        }

        [Fact]
        public async Task UpdateFields_OrdinaryScalar_ProducesFieldsChangedRowOnly()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();

            var request = Req(new MutationIntent(MutationOperation.UpdateFields, SyncEntityTypes.StudyTask, task.MaTask,
                Array.Empty<RelationChange>(), new[] { "TenTask" }));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            var row = Assert.Single(impact.Rows);
            Assert.Equal(RowEffect.FieldsChanged, row.Effect);
            Assert.Empty(impact.Edges);
            Assert.Empty(impact.Scopes);
        }

        [Fact]
        public async Task UpdateFields_TaskNoteContent_ProducesOccupantContentChangedForItsOwningTaskScope()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "x" };
            await _fx.Fx.AddLocalAsync(note);

            var request = Req(new MutationIntent(MutationOperation.UpdateFields, SyncEntityTypes.TaskNote, note.Id,
                Array.Empty<RelationChange>(), new[] { "Content" }));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            var scope = Assert.Single(impact.Scopes);
            Assert.Equal(ScopeChange.OccupantContentChanged, scope.Change);
            Assert.Equal(task.MaTask, scope.ScopeValue);
            Assert.Equal(note.Id, scope.OccupantId);
        }

        [Fact]
        public async Task Reparent_StudyTask_ProducesReparentedRowAndBothEdges()
        {
            var (hocKy, monHocA, task) = await _fx.Fx.SeedTreeAsync();
            var monHocB = new MonHoc("MH B", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.Fx.AddLocalAsync(monHocB);

            var request = Req(new MutationIntent(MutationOperation.Reparent, SyncEntityTypes.StudyTask, task.MaTask,
                new[] { new RelationChange("MaMonHoc", monHocA.MaMonHoc, monHocB.MaMonHoc) }, Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            var row = Assert.Single(impact.Rows);
            Assert.Equal(RowEffect.Reparented, row.Effect);

            Assert.Equal(2, impact.Edges.Count);
            Assert.Contains(impact.Edges, e => e.ParentId == monHocA.MaMonHoc && e.Change == EdgeChange.Removed);
            Assert.Contains(impact.Edges, e => e.ParentId == monHocB.MaMonHoc && e.Change == EdgeChange.Added);
        }

        [Fact]
        public async Task Tombstone_StudyTask_CascadesToLiveNoteAndLink_WithReleasedScope()
        {
            var (_, monHoc, task) = await _fx.Fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "x" };
            await _fx.Fx.AddLocalAsync(note);
            var link = new TaskReferenceLink { Id = Guid.NewGuid(), MaTask = task.MaTask, Title = "t", Url = "u", SortOrder = 0, CreatedAtUtc = SyncApplyFixture.LocalEarlier };
            await _fx.Fx.AddLocalAsync(link);

            var request = Req(new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.StudyTask, task.MaTask,
                Array.Empty<RelationChange>(), Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            Assert.Equal(3, impact.Rows.Count);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task.MaTask && r.Effect == RowEffect.Tombstoned);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.TaskNote && r.EntityId == note.Id && r.Effect == RowEffect.CascadeTombstoned && r.CausedByEntityId == task.MaTask);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.TaskReferenceLink && r.EntityId == link.Id && r.Effect == RowEffect.CascadeTombstoned && r.CausedByEntityId == task.MaTask);

            Assert.Contains(impact.Edges, e => e.ChildType == SyncEntityTypes.StudyTask && e.ChildId == task.MaTask && e.ParentId == monHoc.MaMonHoc && e.Change == EdgeChange.Removed);
            Assert.Contains(impact.Edges, e => e.ChildType == SyncEntityTypes.TaskNote && e.ChildId == note.Id && e.ParentId == task.MaTask && e.Change == EdgeChange.Removed);
            Assert.Contains(impact.Edges, e => e.ChildType == SyncEntityTypes.TaskReferenceLink && e.ChildId == link.Id && e.ParentId == task.MaTask && e.Change == EdgeChange.Removed);

            var scope = Assert.Single(impact.Scopes);
            Assert.Equal(ScopeChange.Released, scope.Change);
            Assert.Equal(task.MaTask, scope.ScopeValue);
            Assert.Equal(note.Id, scope.OccupantId);

            Assert.Equal(3, impact.Lifecycle.Count(l => l.Effect == LifecycleEffect.Tombstone));
        }

        [Fact]
        public async Task Tombstone_StudyTaskWithEmptyNoteScope_ProducesNoScopeEvent()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync(); // no TaskNote seeded

            var request = Req(new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.StudyTask, task.MaTask,
                Array.Empty<RelationChange>(), Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            Assert.Empty(impact.Scopes);
        }

        /// <summary>
        /// P-CR-3/P-PT-3 style: deleting MonHoc must reach EVERY live StudyTask under it, not just one.
        /// Mutant: "ImpactResolver skips cascade recursion" -- deleting the recursion call turns this RED.
        /// </summary>
        [Fact]
        public async Task Tombstone_MonHoc_CascadesToEveryLiveStudyTaskUnderIt()
        {
            var (hocKy, monHoc, task1) = await _fx.Fx.SeedTreeAsync();
            var task2 = new StudyTask("Task 2", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHoc.MaMonHoc };
            await _fx.Fx.AddLocalAsync(task2);

            var request = Req(new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.MonHoc, monHoc.MaMonHoc,
                Array.Empty<RelationChange>(), Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task1.MaTask && r.Effect == RowEffect.CascadeTombstoned);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task2.MaTask && r.Effect == RowEffect.CascadeTombstoned);
        }

        /// <summary>
        /// A live-only cascade predicate (mission §21 engineering decision, recorded in the PR body):
        /// an already-tombstoned sibling under the same parent must NOT reappear in the impact set.
        /// </summary>
        [Fact]
        public async Task Tombstone_MonHoc_DoesNotReVisitAnAlreadyTombstonedStudyTask()
        {
            var (_, monHoc, task1) = await _fx.Fx.SeedTreeAsync();
            var task2 = new StudyTask("Task 2", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHoc.MaMonHoc };
            await _fx.Fx.AddLocalAsync(task2);

            using (var ctx = _fx.Fx.NewContext())
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task2.MaTask);
                ctx.StudyTasks.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var request = Req(new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.MonHoc, monHoc.MaMonHoc,
                Array.Empty<RelationChange>(), Array.Empty<string>()));

            using (var ctx = _fx.Fx.NewContext())
            {
                var impact = await ImpactResolver.ResolveAsync(ctx, request);
                Assert.DoesNotContain(impact.Rows, r => r.EntityId == task2.MaTask);
                Assert.Contains(impact.Rows, r => r.EntityId == task1.MaTask);
            }
        }

        /// <summary>Three-level cascade: HocKy -> MonHoc -> StudyTask -> TaskNote, matching the fence
        /// spec §5 matrix row "Delete/tombstone HocKy".</summary>
        [Fact]
        public async Task Tombstone_HocKy_CascadesThroughTheWholeTree()
        {
            var (hocKy, monHoc, task) = await _fx.Fx.SeedTreeAsync();
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "x" };
            await _fx.Fx.AddLocalAsync(note);

            var request = Req(new MutationIntent(MutationOperation.Tombstone, SyncEntityTypes.HocKy, hocKy.MaHocKy,
                Array.Empty<RelationChange>(), Array.Empty<string>()));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.HocKy && r.EntityId == hocKy.MaHocKy && r.Effect == RowEffect.Tombstoned);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.MonHoc && r.EntityId == monHoc.MaMonHoc && r.Effect == RowEffect.CascadeTombstoned);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task.MaTask && r.Effect == RowEffect.CascadeTombstoned);
            Assert.Contains(impact.Rows, r => r.EntityType == SyncEntityTypes.TaskNote && r.EntityId == note.Id && r.Effect == RowEffect.CascadeTombstoned);

            var scope = Assert.Single(impact.Scopes);
            Assert.Equal(ScopeChange.Released, scope.Change);
        }

        [Fact]
        public async Task EmptyRequest_ProducesEmptyImpact()
        {
            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, Req());

            Assert.Empty(impact.Rows);
            Assert.Empty(impact.Edges);
            Assert.Empty(impact.Scopes);
            Assert.Empty(impact.Lifecycle);
        }

        [Fact]
        public async Task Rows_AreOrderedDeterministically_ByEntityTypeThenId()
        {
            var (hocKy, monHoc, task1) = await _fx.Fx.SeedTreeAsync();
            var task2 = new StudyTask("Task 2", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHoc.MaMonHoc };
            await _fx.Fx.AddLocalAsync(task2);

            var request = Req(
                new MutationIntent(MutationOperation.UpdateFields, SyncEntityTypes.StudyTask, task2.MaTask, Array.Empty<RelationChange>(), new[] { "TenTask" }),
                new MutationIntent(MutationOperation.UpdateFields, SyncEntityTypes.StudyTask, task1.MaTask, Array.Empty<RelationChange>(), new[] { "TenTask" }),
                new MutationIntent(MutationOperation.UpdateFields, SyncEntityTypes.MonHoc, monHoc.MaMonHoc, Array.Empty<RelationChange>(), new[] { "TenMonHoc" }));

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, request);

            var ids = impact.Rows.Select(r => (r.EntityType, r.EntityId)).ToArray();
            var expected = ids.OrderBy(x => x.EntityType, StringComparer.Ordinal).ThenBy(x => x.EntityId).ToArray();
            Assert.Equal(expected, ids);
        }
    }
}
