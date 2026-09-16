using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 — independent-review findings H-1/A-2, H-2, H-3, M-1, M-2 and M-4,
    /// closed under the owner decisions of 2026-09-16.
    /// <para>
    /// M-3/A-1 (the cascade predicate) is deliberately NOT addressed here: P0-a
    /// (<see cref="CascadePredicateProbeTests"/>) measured that the two executing paths disagree, so
    /// the semantic is unruled. Every fixture below therefore keeps its cascade children LIVE, so no
    /// test in this file depends on the unresolved live-only-vs-unfiltered choice.
    /// </para>
    /// </summary>
    public class FenceReviewFindingsTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        // ------------------------------------------------------------------ helpers

        private static MutationRequest Req(params MutationIntent[] intents) =>
            new(MutationOrigin.LocalApplication, intents);

        private static MutationIntent Tombstone(string entityType, Guid entityId) =>
            new(MutationOperation.Tombstone, entityType, entityId, Array.Empty<RelationChange>(), Array.Empty<string>());

        private static MutationIntent Reparent(string entityType, Guid entityId, string field, Guid? before, Guid? after) =>
            new(MutationOperation.Reparent, entityType, entityId,
                new[] { new RelationChange(field, before, after) }, Array.Empty<string>());

        private static MutationIntent Create(string entityType, Guid entityId, string field, Guid parent) =>
            new(MutationOperation.Create, entityType, entityId,
                new[] { new RelationChange(field, null, parent) }, Array.Empty<string>());

        private static MutationIntent UpdateFields(string entityType, Guid entityId, params string[] fields) =>
            new(MutationOperation.UpdateFields, entityType, entityId, Array.Empty<RelationChange>(), fields);

        private static async Task<SyncConflictRecordRow> SeedStructuralAsync(
            AppDbContext ctx, StructuralReason reason, string entityType, ISyncMetadata liveEntity,
            Guid entityId, string field)
        {
            var snapshot = EntitySnapshotMapper.ToSnapshot(liveEntity);
            var json = CanonicalJson.Write(snapshot);

            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = $"rf-{reason}-{entityId:N}-" + Guid.NewGuid().ToString("N"),
                ScopeKey = ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, entityType, entityId, field, null),
                Kind = ConflictKind.StructuralConflict,
                EntityType = entityType,
                EntityId = entityId,
                FieldName = field,
                StructuralReason = reason,
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = entityId,
                BaseSnapshotJson = json,
                BaseFingerprint = CanonicalJson.Fingerprint(snapshot),
                LocalEntityId = entityId,
                LocalSnapshotJson = json,
                LocalFingerprint = CanonicalJson.Fingerprint(snapshot),
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = json,
                RemoteFingerprint = "remote-" + Guid.NewGuid().ToString("N"),
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };
            ctx.SyncConflictRecords.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        /// <summary>
        /// A ConstraintConflict at K(owningTask). <paramref name="baseOccupant"/> null gives the S3
        /// (Base-absent) form. The local-candidate columns stay populated either way:
        /// <c>CK_SyncConflictRecords_LocalCandidate</c> permits an absent local candidate only for
        /// <c>Kind = StructuralConflict</c>, and the real S3 staging recipe
        /// (<c>FenceScenarioFixture.StageS3Async</c>) does record a withdrawn local candidate too.
        /// </summary>
        private static async Task<SyncConflictRecordRow> SeedConstraintAsync(AppDbContext ctx, Guid owningTaskId, Guid? baseOccupant)
        {
            var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", owningTaskId.ToString("D"));
            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "rf-cons-" + Guid.NewGuid().ToString("N"),
                ScopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null, scope),
                Kind = ConflictKind.ConstraintConflict,
                EntityType = SyncEntityTypes.TaskNote,
                ConstraintKey = "MaTask",
                ConstraintValue = owningTaskId.ToString("D"),
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = baseOccupant,
                BaseSnapshotJson = baseOccupant is null ? null : "{}",
                BaseFingerprint = baseOccupant is null ? null : "rf-base-fp",
                LocalEntityId = baseOccupant ?? Guid.NewGuid(),
                LocalSnapshotJson = "{}",
                LocalFingerprint = "rf-local-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "rf-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };
            ctx.SyncConflictRecords.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        // ==================================================================
        // H-1 / A-2 — merge-field classification and fence-route classification are separate concerns
        // ==================================================================

        /// <summary>
        /// H1-A. <c>TaskReferenceLink.MaTask</c> is <see cref="FieldClass.CopyOnCreate"/> for merge
        /// purposes AND a registered structural child edge, so the fence route is known. The owner
        /// ruling (2026-09-16) makes <see cref="StructuralDependencyRegistry"/> — not
        /// <see cref="MergeSurfaceRegistry"/> — the authority for routability.
        /// </summary>
        [Fact]
        public async Task H1A_CreateTaskReferenceLinkUnderTask_IsRouteKnown()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(
                ctx, Req(Create(SyncEntityTypes.TaskReferenceLink, Guid.NewGuid(), "MaTask", task.MaTask)));

            Assert.True(decision.RouteKnown,
                "TaskReferenceLink.MaTask is registered as a StudyTask -> TaskReferenceLink structural child edge");
        }

        /// <summary>
        /// H1-B. Merge classification alone never implies routability. Exhaustive over every field
        /// <see cref="MergeSurfaceRegistry"/> knows: a (type, field) pair is route-known iff
        /// <see cref="StructuralDependencyRegistry"/> registers exactly that child-edge tuple.
        /// </summary>
        [Fact]
        public void H1B_MergeClassificationAlone_DoesNotImplyRouteKnown()
        {
            var registered = StructuralDependencyRegistry.All
                .Select(e => (e.ChildType, e.ChildField))
                .ToHashSet();

            var leaked = new List<string>();
            var missed = new List<string>();

            foreach (var spec in MergeSurfaceRegistry.All)
            {
                var type = spec.EntityType;
                foreach (var field in spec.Fields)
                {
                    var isRoute = StructuralDependencyRegistry.IsRegisteredStructuralRoute(type, field.Name);
                    var isRegistered = registered.Contains((type, field.Name));

                    if (isRoute && !isRegistered) leaked.Add($"{type}.{field.Name} ({field.Class})");
                    if (!isRoute && isRegistered) missed.Add($"{type}.{field.Name} ({field.Class})");
                }
            }

            Assert.Equal(Array.Empty<string>(), leaked.ToArray());
            Assert.Equal(Array.Empty<string>(), missed.ToArray());

            // Registration is per (ChildType, ChildField) tuple, never per type: a registered child
            // type named with one of its NON-edge fields is still not a route.
            Assert.False(StructuralDependencyRegistry.IsRegisteredStructuralRoute(SyncEntityTypes.TaskReferenceLink, "Title"));
            Assert.False(StructuralDependencyRegistry.IsRegisteredStructuralRoute(SyncEntityTypes.TaskNote, "Content"));
            Assert.False(StructuralDependencyRegistry.IsRegisteredStructuralRoute(SyncEntityTypes.StudyTask, "MaTask"));
            Assert.False(StructuralDependencyRegistry.IsRegisteredStructuralRoute("Nope", "MaTask"));
        }

        /// <summary>
        /// H1-C. The converse direction of the ruling: registering a structural route must NOT change
        /// the field's merge classification. This is the guard against "fix the fence by editing
        /// MergeSurfaceRegistry", which the owner ruling forbids outright.
        /// </summary>
        [Fact]
        public void H1C_StructuralRouteRegistration_DoesNotAlterMergeClassification()
        {
            Assert.Equal(FieldClass.CopyOnCreate,
                MergeSurfaceRegistry.Get(SyncEntityTypes.TaskReferenceLink).Fields.Single(f => f.Name == "MaTask").Class);
            Assert.Equal(FieldClass.CopyOnCreate,
                MergeSurfaceRegistry.Get(SyncEntityTypes.StudyLog).Fields.Single(f => f.Name == "MaTask").Class);

            // ...while both are nevertheless registered fence routes.
            Assert.True(StructuralDependencyRegistry.IsRegisteredStructuralRoute(SyncEntityTypes.TaskReferenceLink, "MaTask"));
            Assert.True(StructuralDependencyRegistry.IsRegisteredStructuralRoute(SyncEntityTypes.StudyLog, "MaTask"));

            // The ConstraintScope/Structural fields keep their own classification too.
            Assert.Equal(FieldClass.ConstraintScope,
                MergeSurfaceRegistry.Get(SyncEntityTypes.TaskNote).Fields.Single(f => f.Name == "MaTask").Class);
            Assert.Equal(FieldClass.Structural,
                MergeSurfaceRegistry.Get(SyncEntityTypes.StudyTask).Fields.Single(f => f.Name == "MaMonHoc").Class);
        }

        /// <summary>
        /// H1-D. <c>RouteKnown</c> is only the first stage. The same route-known request passes or
        /// blocks purely on conflict selection + policy evaluation, so routability never authorizes
        /// anything by itself. Asserted in BOTH directions so neither leg can pass vacuously.
        /// </summary>
        [Fact]
        public async Task H1D_RouteKnownTrue_DoesNotImplyFencePassed()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();
            var link = Create(SyncEntityTypes.TaskReferenceLink, Guid.NewGuid(), "MaTask", task.MaTask);

            // (a) No unresolved record anywhere: route known AND fence passes.
            using (var ctx = _fx.Fx.NewContext())
            {
                var pass = await FenceRouter.EvaluateAsync(ctx, Req(link));
                Assert.True(pass.RouteKnown);
                Assert.True(pass.FencePassed);
            }

            // (b) Same route, now with an S1-CR record on the task and a tombstone of that task in the
            //     same request: still route known, but the fence blocks.
            using (var seed = _fx.Fx.NewContext())
            {
                var live = await seed.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                await SeedStructuralAsync(seed, StructuralReason.ConcurrentReparent,
                    SyncEntityTypes.StudyTask, live, task.MaTask, "MaMonHoc");
            }

            using (var ctx = _fx.Fx.NewContext())
            {
                var blocked = await FenceRouter.EvaluateAsync(
                    ctx, Req(link, Tombstone(SyncEntityTypes.StudyTask, task.MaTask)));

                Assert.True(blocked.RouteKnown);
                Assert.False(blocked.FencePassed);
                Assert.Contains(blocked.Results, r => r.RuleId == "S1CR.SubjectRemoved" && r.Outcome == FenceOutcome.Blocked);
            }
        }

        // ==================================================================
        // H-2 — a conflict reachable through an edge's PARENT endpoint must be selectable
        // ==================================================================

        /// <summary>
        /// H-2. A structural edge has two endpoints. <c>Create(TaskReferenceLink L, MaTask: null -> T)</c>
        /// puts T in the impact only as <see cref="ImpactEdge.ParentId"/> — T is in no row, no lifecycle
        /// effect and no scope. A conflict whose protected subject is T must still be selected.
        /// Asserted at selector level, where the result is discriminating: the parent-endpoint policies
        /// return <c>Passed</c>, so a router-level <c>FencePassed</c> assertion could not tell the two
        /// behaviours apart.
        /// </summary>
        [Fact]
        public async Task H2_ConflictOnEdgeParentEndpoint_IsSelected()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();

            SyncConflictRecordRow record;
            using (var seed = _fx.Fx.NewContext())
            {
                var live = await seed.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                record = await SeedStructuralAsync(seed, StructuralReason.ConcurrentReparent,
                    SyncEntityTypes.StudyTask, live, task.MaTask, "MaMonHoc");
            }

            var linkId = Guid.NewGuid();
            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(
                ctx, Req(Create(SyncEntityTypes.TaskReferenceLink, linkId, "MaTask", task.MaTask)));

            // Precondition: T really is present ONLY as an edge parent endpoint.
            Assert.DoesNotContain(impact.Rows, r => r.EntityId == task.MaTask);
            Assert.DoesNotContain(impact.Lifecycle, l => l.EntityId == task.MaTask);
            Assert.Empty(impact.Scopes);
            Assert.Contains(impact.Edges, e => e.ChildId == linkId && e.ParentId == task.MaTask);

            var selected = await ConflictDependencySelector.SelectAsync(ctx, impact);

            Assert.Contains(selected, r => r.ConflictId == record.ConflictId);
        }

        /// <summary>H-2, router level: the parent-endpoint record is actually evaluated and reported.</summary>
        [Fact]
        public async Task H2_ConflictOnEdgeParentEndpoint_IsEvaluatedByTheRouter()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();

            SyncConflictRecordRow record;
            using (var seed = _fx.Fx.NewContext())
            {
                var live = await seed.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                record = await SeedStructuralAsync(seed, StructuralReason.ConcurrentReparent,
                    SyncEntityTypes.StudyTask, live, task.MaTask, "MaMonHoc");
            }

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(
                ctx, Req(Create(SyncEntityTypes.TaskReferenceLink, Guid.NewGuid(), "MaTask", task.MaTask)));

            var result = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal("S1CR.ChildEdgeOnly", result.RuleId);
            Assert.Equal(FenceOutcome.Passed, result.Outcome);
            Assert.Equal(RoutingStage.CascadeReached, result.Stage);
        }

        /// <summary>
        /// H-2 regression guard: adding the parent endpoint must not disturb the pre-existing CHILD
        /// endpoint behaviour. The conflict here is on the child of the edge.
        /// </summary>
        [Fact]
        public async Task H2_ConflictOnEdgeChildEndpoint_StillSelected()
        {
            var (_, monHoc, task) = await _fx.Fx.SeedTreeAsync();

            SyncConflictRecordRow record;
            using (var seed = _fx.Fx.NewContext())
            {
                var live = await seed.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                record = await SeedStructuralAsync(seed, StructuralReason.ConcurrentReparent,
                    SyncEntityTypes.StudyTask, live, task.MaTask, "MaMonHoc");
            }

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(
                ctx, Req(Reparent(SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", monHoc.MaMonHoc, Guid.NewGuid())));

            var selected = await ConflictDependencySelector.SelectAsync(ctx, impact);
            Assert.Contains(selected, r => r.ConflictId == record.ConflictId);
        }

        // ==================================================================
        // H-3 — the impact set must be self-consistent with the request AS A UNIT
        // ==================================================================

        /// <summary>
        /// H-3. <c>Reparent(T, M1 -> M2)</c> + <c>Tombstone(M1)</c> in one request. T leaves M1's
        /// subtree, so the combined mutation does not cascade-tombstone T (nor T's own children, nor
        /// T's TaskNote constraint scope). Reporting T as both moved out and cascade-tombstoned is the
        /// contradiction this finding names.
        /// </summary>
        [Fact]
        public async Task H3_ReparentOutOfATombstonedSubtree_IsNotAlsoCascadeTombstoned()
        {
            var (hocKy, monHoc1, task) = await _fx.Fx.SeedTreeAsync();
            var monHoc2 = new MonHoc("MH 2", 3) { MaHocKy = hocKy.MaHocKy };
            await _fx.Fx.AddLocalAsync(monHoc2);

            // A LIVE note under T, so "T's descendants were not cascaded either" is observable.
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "survives" };
            await _fx.Fx.AddLocalAsync(note);

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, Req(
                Reparent(SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", monHoc1.MaMonHoc, monHoc2.MaMonHoc),
                Tombstone(SyncEntityTypes.MonHoc, monHoc1.MaMonHoc)));

            var taskRow = Assert.Single(impact.Rows, r => r.EntityId == task.MaTask);
            Assert.Equal(RowEffect.Reparented, taskRow.Effect);

            var monRow = Assert.Single(impact.Rows, r => r.EntityId == monHoc1.MaMonHoc);
            Assert.Equal(RowEffect.Tombstoned, monRow.Effect);

            // The moved-out row is not tombstoned by the combined request, so no Tombstone lifecycle
            // effect for it -- and its descendants are untouched.
            Assert.DoesNotContain(impact.Lifecycle, l => l.EntityId == task.MaTask);
            Assert.DoesNotContain(impact.Rows, r => r.EntityId == note.Id);
            Assert.DoesNotContain(impact.Lifecycle, l => l.EntityId == note.Id);
            Assert.DoesNotContain(impact.Scopes, s => s.ScopeValue == task.MaTask);

            Assert.Single(impact.Lifecycle, l => l.EntityId == monHoc1.MaMonHoc && l.Effect == LifecycleEffect.Tombstone);
        }

        /// <summary>
        /// H-3, the symmetric direction: a row reparented INTO a subtree that the same request
        /// tombstones IS reached by that cascade. One uniform effective-parent rule, not a special case
        /// for the "moved out" example.
        /// </summary>
        [Fact]
        public async Task H3_ReparentIntoATombstonedSubtree_IsCascadeTombstoned()
        {
            var (hocKy, monHoc1, task) = await _fx.Fx.SeedTreeAsync();
            var monHoc2 = new MonHoc("MH 2", 3) { MaHocKy = hocKy.MaHocKy };
            await _fx.Fx.AddLocalAsync(monHoc2);

            using var ctx = _fx.Fx.NewContext();
            var impact = await ImpactResolver.ResolveAsync(ctx, Req(
                Reparent(SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", monHoc1.MaMonHoc, monHoc2.MaMonHoc),
                Tombstone(SyncEntityTypes.MonHoc, monHoc2.MaMonHoc)));

            var taskRow = Assert.Single(impact.Rows, r => r.EntityId == task.MaTask);
            Assert.Equal(RowEffect.CascadeTombstoned, taskRow.Effect);
            Assert.Equal(monHoc2.MaMonHoc, taskRow.CausedByEntityId);
            Assert.Contains(impact.Lifecycle, l => l.EntityId == task.MaTask && l.Effect == LifecycleEffect.Tombstone);
        }

        /// <summary>
        /// H-3 / M-1. Equivalent compound requests expressed in different intent orders must produce
        /// equivalent ImpactSets -- element for element, in every list.
        /// </summary>
        [Fact]
        public async Task H3_CompoundRequest_IsOrderIndependent()
        {
            var (hocKy, monHoc1, task) = await _fx.Fx.SeedTreeAsync();
            var monHoc2 = new MonHoc("MH 2", 3) { MaHocKy = hocKy.MaHocKy };
            await _fx.Fx.AddLocalAsync(monHoc2);
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "survives" };
            await _fx.Fx.AddLocalAsync(note);

            var reparent = Reparent(SyncEntityTypes.StudyTask, task.MaTask, "MaMonHoc", monHoc1.MaMonHoc, monHoc2.MaMonHoc);
            var tombstone = Tombstone(SyncEntityTypes.MonHoc, monHoc1.MaMonHoc);

            using var ctx = _fx.Fx.NewContext();
            var forward = await ImpactResolver.ResolveAsync(ctx, Req(reparent, tombstone));
            var reverse = await ImpactResolver.ResolveAsync(ctx, Req(tombstone, reparent));

            Assert.Equal(forward.Rows, reverse.Rows);
            Assert.Equal(forward.Edges, reverse.Edges);
            Assert.Equal(forward.Scopes, reverse.Scopes);
            Assert.Equal(forward.Lifecycle, reverse.Lifecycle);
        }

        // ==================================================================
        // M-1 — RowEffect precedence is explicit and order-independent
        // ==================================================================

        /// <summary>
        /// M-1. <c>[Tombstone(parent), Tombstone(child)]</c> and <c>[Tombstone(child), Tombstone(parent)]</c>
        /// must give the child the same effect, and it must be the DIRECT one: a direct intent is never
        /// downgraded to <see cref="RowEffect.CascadeTombstoned"/> just because the cascade was expanded
        /// first.
        /// </summary>
        [Fact]
        public async Task M1_DirectTombstoneDominatesCascade_InEitherIntentOrder()
        {
            var (_, monHoc, task) = await _fx.Fx.SeedTreeAsync();

            var parent = Tombstone(SyncEntityTypes.MonHoc, monHoc.MaMonHoc);
            var child = Tombstone(SyncEntityTypes.StudyTask, task.MaTask);

            using var ctx = _fx.Fx.NewContext();
            var parentFirst = await ImpactResolver.ResolveAsync(ctx, Req(parent, child));
            var childFirst = await ImpactResolver.ResolveAsync(ctx, Req(child, parent));

            var a = Assert.Single(parentFirst.Rows, r => r.EntityId == task.MaTask);
            var b = Assert.Single(childFirst.Rows, r => r.EntityId == task.MaTask);

            Assert.Equal(RowEffect.Tombstoned, a.Effect);
            Assert.Equal(RowEffect.Tombstoned, b.Effect);
            Assert.Equal(a, b);

            Assert.Equal(parentFirst.Rows, childFirst.Rows);
            Assert.Equal(parentFirst.Edges, childFirst.Edges);
            Assert.Equal(parentFirst.Scopes, childFirst.Scopes);
            Assert.Equal(parentFirst.Lifecycle, childFirst.Lifecycle);
        }

        /// <summary>
        /// M-1, at the router level: an S1-CR record on the child must report the DIRECT rule
        /// (<c>S1CR.SubjectRemoved</c> at <see cref="RoutingStage.DirectSubject"/>) in both orders. The
        /// stage is part of the deterministic aggregation key, so an order-dependent effect would also
        /// make result ordering order-dependent.
        /// </summary>
        [Fact]
        public async Task M1_RouterStageForADirectlyTombstonedSubject_IsOrderIndependent()
        {
            var (_, monHoc, task) = await _fx.Fx.SeedTreeAsync();

            using (var seed = _fx.Fx.NewContext())
            {
                var live = await seed.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                await SeedStructuralAsync(seed, StructuralReason.ConcurrentReparent,
                    SyncEntityTypes.StudyTask, live, task.MaTask, "MaMonHoc");
            }

            var parent = Tombstone(SyncEntityTypes.MonHoc, monHoc.MaMonHoc);
            var child = Tombstone(SyncEntityTypes.StudyTask, task.MaTask);

            using var ctx = _fx.Fx.NewContext();
            var parentFirst = await FenceRouter.EvaluateAsync(ctx, Req(parent, child));
            var childFirst = await FenceRouter.EvaluateAsync(ctx, Req(child, parent));

            foreach (var decision in new[] { parentFirst, childFirst })
            {
                var r = Assert.Single(decision.Results);
                Assert.Equal(FenceOutcome.Blocked, r.Outcome);
                Assert.Equal("S1CR.SubjectRemoved", r.RuleId);
                Assert.Equal(RoutingStage.DirectSubject, r.Stage);
            }
        }

        // ==================================================================
        // M-2 — missing plan-required router-level coverage
        // ==================================================================

        /// <summary>
        /// P-AL-4 (plan §16.2). An AL-PT record protects the ABSENCE of a logical identity. A
        /// constructed <c>UpdateFields(E)</c> targets that identity and must be
        /// <see cref="FenceOutcome.Blocked"/> with <c>ALPT.AbsentIdentityTargeted</c> — routed at
        /// router level, not just asserted on the policy in isolation.
        /// </summary>
        [Fact]
        public async Task PAL4_UpdateFieldsOnTheAbsentIdentity_IsBlockedByTheRouter()
        {
            var (record, _, absentMonHocId) = await _fx.StageAlPtAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(
                ctx, Req(UpdateFields(SyncEntityTypes.MonHoc, absentMonHocId, "TenMonHoc")));

            Assert.True(decision.RouteKnown);
            Assert.False(decision.FencePassed);

            var result = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("ALPT.AbsentIdentityTargeted", result.RuleId);
            Assert.Equal(ConflictShape.AbsentLocalParentTombstoned, result.Shape);
        }

        /// <summary>
        /// P-K-6 (plan §16.2). Two same-shape ConstraintOccupancy records in DIFFERENT scopes, and one
        /// constructed reassignment across them. BOTH must survive selection and BOTH must block: the
        /// router never dedups or prioritises by <see cref="ConflictShape"/>. This doubles as the
        /// same-shape/different-scope routing proof.
        /// </summary>
        [Fact]
        public async Task PK6_ReassignAcrossTwoConstraintScopes_BlocksBoth()
        {
            var (_, _, task1) = await _fx.Fx.SeedTreeAsync();
            var (_, _, task2) = await _fx.Fx.SeedTreeAsync();

            SyncConflictRecordRow k1, k2;
            using (var seed = _fx.Fx.NewContext())
            {
                k1 = await SeedConstraintAsync(seed, task1.MaTask, baseOccupant: null);
                k2 = await SeedConstraintAsync(seed, task2.MaTask, baseOccupant: null);
            }

            Assert.NotEqual(k1.ScopeKey, k2.ScopeKey); // same shape, different scopes

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(
                Reparent(SyncEntityTypes.TaskNote, Guid.NewGuid(), "MaTask", task1.MaTask, task2.MaTask)));

            Assert.True(decision.RouteKnown);
            Assert.False(decision.FencePassed);

            var r1 = Assert.Single(decision.Results, r => r.ConflictId == k1.ConflictId);
            var r2 = Assert.Single(decision.Results, r => r.ConflictId == k2.ConflictId);

            Assert.Equal(FenceOutcome.Blocked, r1.Outcome);
            Assert.Equal(FenceOutcome.Blocked, r2.Outcome);
            Assert.Equal("CONS.ScopeReleased", r1.RuleId);
            Assert.Equal("CONS.ScopeAcquired", r2.RuleId);

            // Both are ConstraintOccupancy: the shape is NOT a priority or a dedup key.
            Assert.All(decision.Results, r => Assert.Equal(ConflictShape.ConstraintOccupancy, r.Shape));
            Assert.Equal(2, decision.Results.Count);
        }

        /// <summary>
        /// M-2 ordering: results are ordered by <c>(Stage, ScopeKey, ConflictId)</c> and by nothing
        /// else. Two same-shape records at different scopes and the same stage must come back in
        /// ScopeKey ordinal order regardless of their ConflictId or insertion order.
        /// </summary>
        [Fact]
        public async Task M2_ResultOrdering_IsStageThenScopeKeyThenConflictId()
        {
            var (_, _, task1) = await _fx.Fx.SeedTreeAsync();
            var (_, _, task2) = await _fx.Fx.SeedTreeAsync();

            using (var seed = _fx.Fx.NewContext())
            {
                await SeedConstraintAsync(seed, task1.MaTask, baseOccupant: null);
                await SeedConstraintAsync(seed, task2.MaTask, baseOccupant: null);
            }

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(
                Reparent(SyncEntityTypes.TaskNote, Guid.NewGuid(), "MaTask", task1.MaTask, task2.MaTask)));

            var expected = decision.Results
                .OrderBy(r => r.Stage)
                .ThenBy(r => r.ScopeKey, StringComparer.Ordinal)
                .ThenBy(r => r.ConflictId)
                .ToArray();

            Assert.Equal(expected, decision.Results.ToArray());
        }

        // ==================================================================
        // M-4 — a selector-side DB failure propagates
        // ==================================================================

        /// <summary>
        /// M-4. N-4 proves an <see cref="ImpactResolver"/> DB exception propagates; this proves the
        /// SELECTOR's read does too. <c>UpdateFields(MonHoc)</c> with no relations makes the resolver
        /// issue zero DB reads (it only reads for TaskNote), so dropping the conflict-record table can
        /// only fail inside <see cref="ConflictDependencySelector"/>. The router must not swallow it or
        /// turn it into Blocked/Passed/NotApplicable, and must not retry.
        /// </summary>
        [Fact]
        public async Task M4_SelectorDbFailure_PropagatesAndIsNotConvertedToAnOutcome()
        {
            var (_, monHoc, _) = await _fx.Fx.SeedTreeAsync();
            var request = Req(UpdateFields(SyncEntityTypes.MonHoc, monHoc.MaMonHoc, "TenMonHoc"));

            using var ctx = _fx.Fx.NewContext();

            // Control: the resolver alone survives -- it issues no DB read for this intent, so the
            // failure below is provably the selector's.
            await ImpactResolver.ResolveAsync(ctx, request);

            await ctx.Database.ExecuteSqlRawAsync("DROP TABLE SyncConflictRecords;");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => FenceRouter.EvaluateAsync(ctx, request));
            Assert.Contains("SyncConflictRecords", ex.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
