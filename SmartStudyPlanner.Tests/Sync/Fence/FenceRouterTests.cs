using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    /// Epic 2 / T2.4 Slice 2 (fence spec §7; plan §11.3, §16.2, §17). Router-level scenario and
    /// negative-case coverage: overlapping conflicts (X-1/X-2), deterministic ordering (X-3), origin
    /// equivalence (X-5), unknown shape/route (X-6/X-7), and the fail-closed negative cases N-1..N-4,
    /// N-8, N-9. Read-only proofs live separately in <c>FenceReadOnlyTests</c>; source-level guards
    /// (X-4, X-9, N-10) live in <c>FenceSourceFenceTests</c>.
    /// </summary>
    public class FenceRouterTests : IDisposable
    {
        private readonly FenceScenarioFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static MutationRequest Req(MutationOrigin origin = MutationOrigin.LocalApplication, params MutationIntent[] intents) =>
            new(origin, intents);

        private static MutationIntent Tombstone(string entityType, Guid entityId) =>
            new(MutationOperation.Tombstone, entityType, entityId, Array.Empty<RelationChange>(), Array.Empty<string>());

        private static (string Stage, string ScopeKey, string RuleId, FenceOutcome Outcome)[] Project(FenceDecision decision) =>
            decision.Results.Select(r => (r.Stage.ToString(), r.ScopeKey, r.RuleId, r.Outcome)).ToArray();

        // ---- seeding helpers: constructed records that reference a LIVE entity's real snapshot,
        // so policy.Derive() succeeds through real canonical JSON (EntitySnapshotMapper is the same
        // mapper SyncApplySession itself uses).

        private static async Task<SyncConflictRecordRow> SeedStructuralAsync(
            AppDbContext ctx, StructuralReason reason, string entityType, ISyncMetadata liveEntity, Guid entityId, string field,
            Guid? conflictId = null)
        {
            var snapshot = EntitySnapshotMapper.ToSnapshot(liveEntity);
            var json = CanonicalJson.Write(snapshot);

            var row = new SyncConflictRecordRow
            {
                ConflictId = conflictId ?? Guid.NewGuid(),
                ConflictKey = $"seed-{reason}-{entityId:N}-" + Guid.NewGuid().ToString("N"),
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

        private static async Task<SyncConflictRecordRow> SeedConstraintAsync(
            AppDbContext ctx, TaskNote occupant, Guid owningTaskId, Guid? conflictId = null)
        {
            var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", owningTaskId.ToString("D"));
            var row = new SyncConflictRecordRow
            {
                ConflictId = conflictId ?? Guid.NewGuid(),
                ConflictKey = "seed-cons-" + Guid.NewGuid().ToString("N"),
                ScopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null, scope),
                Kind = ConflictKind.ConstraintConflict,
                EntityType = SyncEntityTypes.TaskNote,
                ConstraintKey = "MaTask",
                ConstraintValue = owningTaskId.ToString("D"),
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = occupant.Id,
                BaseSnapshotJson = "{}",
                BaseFingerprint = "seed-base-fp",
                LocalEntityId = occupant.Id,
                LocalSnapshotJson = "{}",
                LocalFingerprint = "seed-base-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "seed-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };
            ctx.SyncConflictRecords.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        /// <summary>Builds A(HocKy) -> T(MonHoc)[S1-PT] -> N(StudyTask)[S1-CR] -> note[Constraint],
        /// with A/T/N/note all still LIVE (mission §11.1: the topology is exercised at router level
        /// with a constructed request -- HocKy has no local delete entry point).</summary>
        private async Task<(HocKy A, MonHoc T, StudyTask N, TaskNote Note)> BuildOverlapTopologyAsync()
        {
            var a = new HocKy("A", DateTime.Today);
            var t = new MonHoc("T", 3) { MaHocKy = a.MaHocKy };
            var n = new StudyTask("N", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = t.MaMonHoc };
            t.DanhSachTask.Add(n);
            a.DanhSachMonHoc.Add(t);

            using (var ctx = _fx.Fx.NewContext())
            {
                ctx.HocKys.Add(a);
                await ctx.SaveChangesAsync();
            }

            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = n.MaTask, Content = "occupant" };
            await _fx.Fx.AddLocalAsync(note);

            using (var ctx = _fx.Fx.NewContext())
            {
                await SeedStructuralAsync(ctx, StructuralReason.ParentTombstoned, SyncEntityTypes.MonHoc, t, t.MaMonHoc, "MaHocKy");
            }
            using (var ctx = _fx.Fx.NewContext())
            {
                await SeedStructuralAsync(ctx, StructuralReason.ConcurrentReparent, SyncEntityTypes.StudyTask, n, n.MaTask, "MaMonHoc");
            }
            using (var ctx = _fx.Fx.NewContext())
            {
                await SeedConstraintAsync(ctx, note, n.MaTask);
            }

            return (a, t, n, note);
        }

        // ------------------------------------------------------------------ X-1 / X-2 overlap

        [Fact]
        public async Task X1_DeleteAncestor_EvaluatesAllThreeOverlappingConflicts_NoEarlyExit()
        {
            var (a, t, n, note) = await BuildOverlapTopologyAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.HocKy, a.MaHocKy)));

            Assert.False(decision.FencePassed);
            Assert.Equal(3, decision.Results.Count);
            Assert.All(decision.Results, r => Assert.Equal(FenceOutcome.Blocked, r.Outcome));

            Assert.Contains(decision.Results, r => r.Shape == ConflictShape.ParentTombstoned && r.RuleId == "S1PT.SubjectRemoved" && r.Stage == RoutingStage.CascadeReached);
            Assert.Contains(decision.Results, r => r.Shape == ConflictShape.ConcurrentReparent && r.RuleId == "S1CR.SubjectRemoved" && r.Stage == RoutingStage.CascadeReached);
            Assert.Contains(decision.Results, r => r.Shape == ConflictShape.ConstraintOccupancy && r.RuleId == "CONS.ScopeReleased" && r.Stage == RoutingStage.ConstraintScope);
        }

        /// <summary>
        /// Regression for the specific trap this scenario exists to catch: if the ImpactResolver failed
        /// to emit <c>ImpactScope(K, Released)</c> for the live note reached by the cascade, the
        /// Constraint policy would silently fall through to its OD-4 empty-scope branch and return
        /// <c>Passed</c> instead of <c>Blocked</c> -- the aggregate would still read "not FencePassed"
        /// (2 Blocked + 1 Passed), so a looser assertion would not catch it. This asserts the exact
        /// outcome and rule id for that one result.
        /// </summary>
        [Fact]
        public async Task X1_ConstraintResult_IsBlockedScopeReleased_NotThePassedEmptyScopeBranch()
        {
            var (a, _, _, _) = await BuildOverlapTopologyAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.HocKy, a.MaHocKy)));

            var consResult = Assert.Single(decision.Results, r => r.Shape == ConflictShape.ConstraintOccupancy);
            Assert.Equal(FenceOutcome.Blocked, consResult.Outcome);
            Assert.Equal("CONS.ScopeReleased", consResult.RuleId);
        }

        [Fact]
        public async Task X2_DeleteMonHocDirectly_StillEvaluatesAllThreeConflicts()
        {
            var (_, t, n, note) = await BuildOverlapTopologyAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.MonHoc, t.MaMonHoc)));

            Assert.Equal(3, decision.Results.Count);
            Assert.All(decision.Results, r => Assert.Equal(FenceOutcome.Blocked, r.Outcome));
            Assert.Contains(decision.Results, r => r.Shape == ConflictShape.ParentTombstoned && r.Stage == RoutingStage.DirectSubject);
            Assert.Contains(decision.Results, r => r.Shape == ConflictShape.ConcurrentReparent && r.Stage == RoutingStage.CascadeReached);
            Assert.Contains(decision.Results, r => r.Shape == ConflictShape.ConstraintOccupancy && r.RuleId == "CONS.ScopeReleased");
        }

        /// <summary>Mutant this must catch: a router that <c>break</c>s on the first Blocked result.</summary>
        [Fact]
        public async Task X1_NeverStopsAfterFirstBlocked_AllThreeAreIndependentlyReported()
        {
            var (a, t, n, note) = await BuildOverlapTopologyAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.HocKy, a.MaHocKy)));

            var distinctShapes = decision.Results.Select(r => r.Shape).Distinct().ToArray();
            Assert.Equal(3, distinctShapes.Length);
        }

        // ------------------------------------------------------------------ X-3 determinism

        /// <summary>
        /// Two independently built fixtures stage the same topology in a DIFFERENT insertion order
        /// (the identity of each <c>SyncConflictRecordRow</c> is fresh per fixture, so full
        /// <see cref="PolicyResult"/> equality is not meaningful across them -- <c>ConflictId</c> is
        /// minted per staging). What must match is the ordered projection actually used for reporting.
        /// </summary>
        [Fact]
        public async Task X3_ResultOrder_IsDeterministic_AcrossDifferentInsertionOrders()
        {
            using var fxOne = new FenceScenarioFixture();
            using var fxTwo = new FenceScenarioFixture();

            // Fixed, SHARED ids across both fixtures: the only variable between the two runs is the
            // database INSERTION ORDER of the three conflict records, not the entity identities (which
            // would otherwise make the ScopeKey strings themselves incomparable across fixtures).
            var aId = Guid.NewGuid();
            var tId = Guid.NewGuid();
            var nId = Guid.NewGuid();
            var noteId = Guid.NewGuid();

            var one = await BuildOverlapTopologyInAsync(fxOne, constraintFirst: false, aId, tId, nId, noteId);
            var two = await BuildOverlapTopologyInAsync(fxTwo, constraintFirst: true, aId, tId, nId, noteId);

            using var ctxOne = fxOne.Fx.NewContext();
            var decisionOne = await FenceRouter.EvaluateAsync(ctxOne, Req(intents: Tombstone(SyncEntityTypes.HocKy, one.A.MaHocKy)));

            using var ctxTwo = fxTwo.Fx.NewContext();
            var decisionTwo = await FenceRouter.EvaluateAsync(ctxTwo, Req(intents: Tombstone(SyncEntityTypes.HocKy, two.A.MaHocKy)));

            Assert.Equal(Project(decisionOne), Project(decisionTwo));
        }

        /// <summary>
        /// The discriminating leg for the "sort omitted" mutant. <see cref="ConflictDependencySelector"/>
        /// already returns records ordered by <c>ConflictId</c> for its OWN determinism (mission §12), so
        /// a naive cross-fixture comparison with random ids can coincidentally match even when
        /// <see cref="FenceRouter"/>'s own <c>(Stage, ScopeKey, ConflictId)</c> sort is missing (two
        /// same-stage records have only a 50% chance of landing in the "wrong" relative order by luck).
        /// This test removes the luck: ConflictIds are chosen so that raw-ConflictId order is the EXACT
        /// REVERSE of correct Stage order (Constraint's ConflictId sorts first, but its
        /// <see cref="RoutingStage.ConstraintScope"/> must sort LAST). Mutant: drop the
        /// <c>.OrderBy(r =&gt; r.Stage)...</c> chain in <c>FenceRouter.Aggregate</c>.
        /// </summary>
        [Fact]
        public async Task X3_ResultOrder_SortsByStageAndScopeKey_NotByRawConflictIdFromTheSelector()
        {
            var a = new HocKy("A", DateTime.Today);
            var t = new MonHoc("T", 3) { MaHocKy = a.MaHocKy };
            var n = new StudyTask("N", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = t.MaMonHoc };
            t.DanhSachTask.Add(n);
            a.DanhSachMonHoc.Add(t);
            using (var ctx = _fx.Fx.NewContext())
            {
                ctx.HocKys.Add(a);
                await ctx.SaveChangesAsync();
            }
            var note = new TaskNote { Id = Guid.NewGuid(), MaTask = n.MaTask, Content = "occupant" };
            await _fx.Fx.AddLocalAsync(note);

            // Deliberately adversarial ConflictIds: ascending ConflictId order is Constraint, S1CR, S1PT --
            // the exact reverse of the correct Stage-first order (both CascadeReached before ConstraintScope).
            var constraintId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var crId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var ptId = Guid.Parse("00000000-0000-0000-0000-000000000003");

            using (var ctx = _fx.Fx.NewContext())
                await SeedConstraintAsync(ctx, note, n.MaTask, constraintId);
            using (var ctx = _fx.Fx.NewContext())
                await SeedStructuralAsync(ctx, StructuralReason.ConcurrentReparent, SyncEntityTypes.StudyTask, n, n.MaTask, "MaMonHoc", crId);
            using (var ctx = _fx.Fx.NewContext())
                await SeedStructuralAsync(ctx, StructuralReason.ParentTombstoned, SyncEntityTypes.MonHoc, t, t.MaMonHoc, "MaHocKy", ptId);

            using var evalCtx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(evalCtx, Req(intents: Tombstone(SyncEntityTypes.HocKy, a.MaHocKy)));

            Assert.Equal(3, decision.Results.Count);
            Assert.All(decision.Results.Take(2), r => Assert.Equal(RoutingStage.CascadeReached, r.Stage));
            Assert.Equal(RoutingStage.ConstraintScope, decision.Results[2].Stage);
            Assert.Equal(constraintId, decision.Results[2].ConflictId);
        }

        /// <summary>Second determinism leg: repeated evaluation on the SAME fixture is stable.</summary>
        [Fact]
        public async Task X3_ResultOrder_IsStable_AcrossRepeatedEvaluation()
        {
            var (a, _, _, _) = await BuildOverlapTopologyAsync();
            var request = Req(intents: Tombstone(SyncEntityTypes.HocKy, a.MaHocKy));

            using var ctx1 = _fx.Fx.NewContext();
            var first = await FenceRouter.EvaluateAsync(ctx1, request);
            using var ctx2 = _fx.Fx.NewContext();
            var second = await FenceRouter.EvaluateAsync(ctx2, request);

            Assert.Equal(Project(first), Project(second));
        }

        private async Task<(HocKy A, MonHoc T, StudyTask N, TaskNote Note)> BuildOverlapTopologyInAsync(
            FenceScenarioFixture fx, bool constraintFirst, Guid? aId = null, Guid? tId = null, Guid? nId = null, Guid? noteId = null)
        {
            var a = new HocKy("A", DateTime.Today);
            if (aId is { } fixedA) a.MaHocKy = fixedA;
            var t = new MonHoc("T", 3) { MaHocKy = a.MaHocKy };
            if (tId is { } fixedT) t.MaMonHoc = fixedT;
            var n = new StudyTask("N", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = t.MaMonHoc };
            if (nId is { } fixedN) n.MaTask = fixedN;
            t.DanhSachTask.Add(n);
            a.DanhSachMonHoc.Add(t);

            using (var ctx = fx.Fx.NewContext())
            {
                ctx.HocKys.Add(a);
                await ctx.SaveChangesAsync();
            }

            var note = new TaskNote { Id = noteId ?? Guid.NewGuid(), MaTask = n.MaTask, Content = "occupant" };
            await fx.Fx.AddLocalAsync(note);

            if (constraintFirst)
            {
                using (var ctx = fx.Fx.NewContext()) await SeedConstraintAsync(ctx, note, n.MaTask);
                using (var ctx = fx.Fx.NewContext()) await SeedStructuralAsync(ctx, StructuralReason.ConcurrentReparent, SyncEntityTypes.StudyTask, n, n.MaTask, "MaMonHoc");
                using (var ctx = fx.Fx.NewContext()) await SeedStructuralAsync(ctx, StructuralReason.ParentTombstoned, SyncEntityTypes.MonHoc, t, t.MaMonHoc, "MaHocKy");
            }
            else
            {
                using (var ctx = fx.Fx.NewContext()) await SeedStructuralAsync(ctx, StructuralReason.ParentTombstoned, SyncEntityTypes.MonHoc, t, t.MaMonHoc, "MaHocKy");
                using (var ctx = fx.Fx.NewContext()) await SeedStructuralAsync(ctx, StructuralReason.ConcurrentReparent, SyncEntityTypes.StudyTask, n, n.MaTask, "MaMonHoc");
                using (var ctx = fx.Fx.NewContext()) await SeedConstraintAsync(ctx, note, n.MaTask);
            }

            return (a, t, n, note);
        }

        // ------------------------------------------------------------------ X-5 origin equivalence

        [Fact]
        public async Task X5_LocalAndSyncApplyOrigin_ProduceIdenticalResults()
        {
            var (record, _, task) = await _fx.StageS1PtAsync();
            var intent = Tombstone(SyncEntityTypes.StudyTask, task.MaTask);

            using var ctxLocal = _fx.Fx.NewContext();
            var local = await FenceRouter.EvaluateAsync(ctxLocal, Req(MutationOrigin.LocalApplication, intent));

            using var ctxSync = _fx.Fx.NewContext();
            var sync = await FenceRouter.EvaluateAsync(ctxSync, Req(MutationOrigin.SyncApply, intent));

            Assert.Equal(Project(local), Project(sync));
        }

        // ------------------------------------------------------------------ X-6 / N-1 unknown shape

        [Fact]
        public async Task X6_UnknownShape_ProducesUnsupported_AndRejectsTheFence()
        {
            var (record, task) = await _fx.SeedUnsupportedRecordAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.StudyTask, task.MaTask)));

            Assert.False(decision.FencePassed);
            var result = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Unsupported, result.Outcome);
            Assert.Contains(record.ConflictId.ToString(), result.Evidence);
        }

        // ------------------------------------------------------------------ X-7 / N-2 unknown route

        [Fact]
        public async Task X7_UnregisteredEntityType_RouteKnownIsFalse_AndRejectsTheFence()
        {
            var intent = new MutationIntent(MutationOperation.UpdateFields, "Foo", Guid.NewGuid(),
                Array.Empty<RelationChange>(), new[] { "Bar" });

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: intent));

            Assert.False(decision.RouteKnown);
            Assert.False(decision.FencePassed);
            Assert.Empty(decision.Results);
        }

        [Fact]
        public async Task X7_UnregisteredMergeField_RouteKnownIsFalse()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();
            // "TenTask" is a Merge field, not Structural/ConstraintScope -- naming it in Relations is
            // not a shape this fence understands as a structural/constraint relation change.
            var intent = new MutationIntent(MutationOperation.Reparent, SyncEntityTypes.StudyTask, task.MaTask,
                new[] { new RelationChange("TenTask", null, null) }, Array.Empty<string>());

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: intent));

            Assert.False(decision.RouteKnown);
        }

        // ------------------------------------------------------------------ N-3 unreadable evidence

        [Fact]
        public async Task N3_UnreadableEvidence_ProducesUnsupported_WithEvidenceUnreadableRuleId_NeverThrows()
        {
            var (record, task) = await _fx.SeedUnreadableEvidenceRecordAsync();

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.StudyTask, task.MaTask)));

            var result = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Unsupported, result.Outcome);
            Assert.Equal("S1PT.EvidenceUnreadable", result.RuleId);
        }

        // ------------------------------------------------------------------ N-4 DB exception propagates

        [Fact]
        public async Task N4_UnderlyingDatabaseException_Propagates_IsNotConvertedToBlockedOrUnsupported()
        {
            var (_, _, task) = await _fx.Fx.SeedTreeAsync();
            var ctx = _fx.Fx.NewContext();
            await ctx.DisposeAsync(); // force every subsequent query on this context to throw

            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.StudyTask, task.MaTask))));
        }

        // ------------------------------------------------------------------ N-8 dedup at router level

        [Fact]
        public async Task N8_RouterNeverReportsTheSameConflictTwice()
        {
            var (record, _, task) = await _fx.StageS1PtAsync();

            // An intent that touches the entity both directly (row) and via a relation change (edge) --
            // both selector predicates fire for the same underlying record.
            var intent = new MutationIntent(MutationOperation.Reparent, SyncEntityTypes.StudyTask, task.MaTask,
                new[] { new RelationChange("MaMonHoc", Guid.NewGuid(), Guid.NewGuid()) }, Array.Empty<string>());

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: intent));

            var count = decision.Results.Count(r => r.ConflictId == record.ConflictId);
            Assert.Equal(1, count);
        }

        // ------------------------------------------------------------------ N-9 empty request

        [Fact]
        public async Task N9_EmptyRequest_IsFencePassed_WithNoResults()
        {
            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req());

            Assert.True(decision.RouteKnown);
            Assert.Empty(decision.Results);
            Assert.True(decision.FencePassed);
        }

        // ------------------------------------------------------------------ passing / unrelated cases

        [Fact]
        public async Task UnrelatedSibling_IsNotBlockedByAnUnresolvedConflictOnAnotherEntity()
        {
            var (_, _, _, _, _, task) = await _fx.StageS1CrAsync();

            var (_, monHoc, _) = await _fx.Fx.SeedTreeAsync();
            var sibling = new StudyTask("Sibling", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHoc.MaMonHoc };
            await _fx.Fx.AddLocalAsync(sibling);

            using var ctx = _fx.Fx.NewContext();
            var decision = await FenceRouter.EvaluateAsync(ctx, Req(intents: Tombstone(SyncEntityTypes.StudyTask, sibling.MaTask)));

            Assert.True(decision.FencePassed);
        }
    }
}
