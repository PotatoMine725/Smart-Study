using System;
using System.Collections.Generic;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Fence.Policies;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence.Policies
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1, S1-PT (plan §10.2, §16.1 P-PT-1..6). Policy-unit level: every ImpactSet
    /// is hand-constructed; no ImpactResolver/FenceRouter (Slice 2). P-PT-6 is the SB-3 owner-ruling
    /// cell (2026-09-14): a non-structural field edit on the held row is Blocked, not Passed.
    /// </summary>
    public class ParentTombstoneFencePolicyTests
    {
        private static readonly Guid ConflictIdG = new("d0000000-0000-0000-0000-000000000001");
        private static readonly Guid T = new("d0000000-0000-0000-0000-0000000000a1");   // E, the protected StudyTask
        private static readonly Guid M = new("d0000000-0000-0000-0000-0000000000b1");   // Base parent (MonHoc, live)
        private static readonly Guid T2 = new("d0000000-0000-0000-0000-0000000000a2");  // unrelated sibling
        private static readonly Guid LinkId = new("d0000000-0000-0000-0000-0000000000c1");

        private static readonly ParentTombstoneFencePolicy Policy = new();

        private static SyncConflictRecordRow MakeRow() => new()
        {
            ConflictId = ConflictIdG,
            ScopeKey = $"{SyncEntityTypes.StudyTask}|{T:D}|MaMonHoc",
            Kind = ConflictKind.StructuralConflict,
            EntityType = SyncEntityTypes.StudyTask,
            EntityId = T,
            FieldName = "MaMonHoc",
            StructuralReason = StructuralReason.ParentTombstoned,
            LocalEntityId = T,
            BaseEntityId = T,
            BaseSnapshotJson = CanonicalJson.Write(
                MergeTestData.Snap(SyncEntityTypes.StudyTask, MergeTestData.Live(), ("MaMonHoc", new GuidValue(M)))),
            RemoteEntityId = T,
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "fp",
            Status = ConflictRecordStatus.Unresolved,
        };

        private static ProtectedContract Contract() => Policy.Derive(MakeRow());

        private static ImpactSet Impact(
            IReadOnlyList<ImpactRow>? rows = null, IReadOnlyList<ImpactEdge>? edges = null) =>
            new(rows ?? Array.Empty<ImpactRow>(), edges ?? Array.Empty<ImpactEdge>(),
                Array.Empty<ImpactScope>(), Array.Empty<ImpactLifecycle>());

        [Fact] // P-PT-1: reparent T to M2
        public void Reparent_E_IsBlocked_ParentFrameChanged()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.Reparented, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1PT.ParentFrameChanged", result.RuleId);
            Assert.Equal(RoutingStage.DirectSubject, result.Stage);
        }

        [Fact] // P-PT-2: delete T -- Blocked @DirectSubject
        public void Tombstone_E_IsBlocked_SubjectRemoved_AtDirectSubject()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1PT.SubjectRemoved", result.RuleId);
            Assert.Equal(RoutingStage.DirectSubject, result.Stage);
        }

        [Fact] // P-PT-3: delete M, cascade reaches T -- Blocked @CascadeReached
        public void CascadeTombstone_E_IsBlocked_SubjectRemoved_AtCascadeReached()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.CascadeTombstoned, WasLive: true, CausedByEntityId: M),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1PT.SubjectRemoved", result.RuleId);
            Assert.Equal(RoutingStage.CascadeReached, result.Stage);
        }

        [Fact] // P-PT-4: delete T2 (unrelated sibling) -- impact never names E
        public void DeleteUnrelatedSibling_IsNotApplicable()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T2, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T2),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact] // P-PT-5: add link under T -- relevant but not violated
        public void ChildEdgeOnly_IsPassed_NotNotApplicable()
        {
            var impact = Impact(edges: new[]
            {
                new ImpactEdge(SyncEntityTypes.TaskReferenceLink, LinkId, "MaTask", T, EdgeChange.Added),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Passed, result.Outcome);
            Assert.Equal("S1PT.ChildEdgeOnly", result.RuleId);
        }

        [Fact] // P-PT-6: edit T.TenTask -- SB-3 owner ruling: Blocked, not Passed
        public void NonStructuralFieldEdit_OnHeldRow_IsBlocked_SB3Ruling()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.FieldsChanged, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1PT.NonStructuralFields", result.RuleId);
            Assert.Equal(RoutingStage.DirectSubject, result.Stage);
        }

        [Fact]
        public void ImpactNeverNamesE_IsNotApplicable()
        {
            var result = Policy.Evaluate(Contract(), ImpactSet.Empty);
            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact]
        public void Derive_UsesParentTombstonedShape()
        {
            var contract = Contract();
            Assert.Equal(ConflictShape.ParentTombstoned, contract.Shape);
            Assert.IsType<EntitySubject>(contract.Subject);
        }
    }
}
