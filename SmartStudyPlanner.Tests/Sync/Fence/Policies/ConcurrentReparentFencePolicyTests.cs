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
    /// Epic 2 / T2.4 Slice 1, S1-CR (plan §10.1, §16.1 P-CR-1..6). Policy-unit level: every ImpactSet
    /// is hand-constructed; no ImpactResolver/FenceRouter (Slice 2). P-CR-6 is the SB-3 owner-ruling
    /// cell (2026-09-14): a non-structural field edit on the held row is Blocked, not Passed.
    /// </summary>
    public class ConcurrentReparentFencePolicyTests
    {
        private static readonly Guid ConflictIdG = new("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid T = new("c0000000-0000-0000-0000-0000000000a1");   // E, the protected StudyTask
        private static readonly Guid M = new("c0000000-0000-0000-0000-0000000000b1");   // Base parent (MonHoc)
        private static readonly Guid M2 = new("c0000000-0000-0000-0000-0000000000b2");  // reparent target
        private static readonly Guid T2 = new("c0000000-0000-0000-0000-0000000000a2");  // unrelated sibling
        private static readonly Guid LinkId = new("c0000000-0000-0000-0000-0000000000c1");

        private static readonly ConcurrentReparentFencePolicy Policy = new();

        private static SyncConflictRecordRow MakeRow() => new()
        {
            ConflictId = ConflictIdG,
            ScopeKey = $"{SyncEntityTypes.StudyTask}|{T:D}|MaMonHoc",
            Kind = ConflictKind.StructuralConflict,
            EntityType = SyncEntityTypes.StudyTask,
            EntityId = T,
            FieldName = "MaMonHoc",
            StructuralReason = StructuralReason.ConcurrentReparent,
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

        [Fact] // P-CR-1: reparent T
        public void Reparent_E_IsBlocked_EdgeReplaced()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.Reparented, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1CR.EdgeReplaced", result.RuleId);
            Assert.Equal(RoutingStage.DirectSubject, result.Stage);
        }

        [Fact] // P-CR-2: delete T
        public void Tombstone_E_IsBlocked_SubjectRemoved()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1CR.SubjectRemoved", result.RuleId);
            Assert.Equal(RoutingStage.DirectSubject, result.Stage);
        }

        [Fact] // P-CR-3: delete M, cascade reaches T
        public void CascadeTombstone_E_IsBlocked_SubjectRemoved_AtCascadeReached()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.CascadeTombstoned, WasLive: true, CausedByEntityId: M),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1CR.SubjectRemoved", result.RuleId);
            Assert.Equal(RoutingStage.CascadeReached, result.Stage);
        }

        [Fact] // P-CR-4: delete T2 (unrelated sibling) -- impact never names E
        public void DeleteUnrelatedSibling_IsNotApplicable()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T2, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T2),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact] // P-CR-5: add link under T -- relevant (names E as parent endpoint) but not violated
        public void ChildEdgeOnly_IsPassed_NotNotApplicable()
        {
            var impact = Impact(edges: new[]
            {
                new ImpactEdge(SyncEntityTypes.TaskReferenceLink, LinkId, "MaTask", T, EdgeChange.Added),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Passed, result.Outcome);
            Assert.Equal("S1CR.ChildEdgeOnly", result.RuleId);
        }

        [Fact] // P-CR-6: edit T.TenTask -- SB-3 owner ruling: Blocked, not Passed
        public void NonStructuralFieldEdit_OnHeldRow_IsBlocked_SB3Ruling()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.FieldsChanged, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("S1CR.NonStructuralFields", result.RuleId);
            Assert.Equal(RoutingStage.DirectSubject, result.Stage);
        }

        [Fact]
        public void ImpactNeverNamesE_IsNotApplicable()
        {
            var result = Policy.Evaluate(Contract(), ImpactSet.Empty);
            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact]
        public void Derive_ReadsHeldParentFromBaseSnapshot_M()
        {
            var contract = Contract();
            var subject = Assert.IsType<EntitySubject>(contract.Subject);

            Assert.Equal(SyncEntityTypes.StudyTask, subject.EntityType);
            Assert.Equal(T, subject.EntityId);
            Assert.Equal(ConflictShape.ConcurrentReparent, contract.Shape);
            Assert.NotNull(contract.Edge);
            Assert.Equal(M, contract.Edge!.HeldParentId);
        }
    }
}
