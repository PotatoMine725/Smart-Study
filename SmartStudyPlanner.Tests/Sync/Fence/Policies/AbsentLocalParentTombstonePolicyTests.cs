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
    /// Epic 2 / T2.4 Slice 1, AL-PT (plan §10.3, §16.1 P-AL-1..4). Policy-unit level: every ImpactSet
    /// is hand-constructed. AL-PT never depends on which parent a materialization proposes -- the
    /// identity itself is protected (P-AL-1's mutant: "policy only blocks when parent is P").
    /// </summary>
    public class AbsentLocalParentTombstonePolicyTests
    {
        private static readonly Guid ConflictIdG = new("e0000000-0000-0000-0000-000000000001");
        private static readonly Guid E = new("e0000000-0000-0000-0000-0000000000a1");   // absent identity
        private static readonly Guid P = new("e0000000-0000-0000-0000-0000000000b1");   // tombstoned Remote parent
        private static readonly Guid M2 = new("e0000000-0000-0000-0000-0000000000b2");  // a different, live parent
        private static readonly Guid Other = new("e0000000-0000-0000-0000-0000000000c1"); // unrelated sibling

        private static readonly AbsentLocalParentTombstonePolicy Policy = new();

        private static SyncConflictRecordRow MakeRow() => new()
        {
            ConflictId = ConflictIdG,
            ScopeKey = $"{SyncEntityTypes.StudyTask}|{E:D}|MaMonHoc",
            Kind = ConflictKind.StructuralConflict,
            EntityType = SyncEntityTypes.StudyTask,
            EntityId = E,
            FieldName = "MaMonHoc",
            StructuralReason = StructuralReason.ParentTombstoned,
            LocalEntityId = null,
            BaseEntityId = null,
            RemoteEntityId = E,
            RemoteSnapshotJson = CanonicalJson.Write(
                MergeTestData.Snap(SyncEntityTypes.StudyTask, MergeTestData.Live(), ("MaMonHoc", new GuidValue(P)))),
            RemoteFingerprint = "fp",
            Status = ConflictRecordStatus.Unresolved,
        };

        private static ProtectedContract Contract() => Policy.Derive(MakeRow());

        private static ImpactSet Impact(
            IReadOnlyList<ImpactRow>? rows = null, IReadOnlyList<ImpactLifecycle>? lifecycle = null) =>
            new(rows ?? Array.Empty<ImpactRow>(), Array.Empty<ImpactEdge>(),
                Array.Empty<ImpactScope>(), lifecycle ?? Array.Empty<ImpactLifecycle>());

        [Fact] // P-AL-1: create E under a DIFFERENT live parent (M2, not P) -- still blocked
        public void CreateE_UnderUnrelatedLiveParent_IsBlocked_SameIdentityMaterialization()
        {
            var impact = Impact(
                rows: new[] { new ImpactRow(SyncEntityTypes.StudyTask, E, RowEffect.Created, WasLive: false, CausedByEntityId: E) },
                lifecycle: new[] { new ImpactLifecycle(SyncEntityTypes.StudyTask, E, LifecycleEffect.Create) });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("ALPT.SameIdentityMaterialization", result.RuleId);
            _ = M2; // documents that the proposed parent is irrelevant to the block
        }

        [Fact] // P-AL-2: same create, this time under the tombstoned parent P -- still blocked
        public void CreateE_UnderTombstonedParentP_IsBlocked_SameIdentityMaterialization()
        {
            var impact = Impact(
                rows: new[] { new ImpactRow(SyncEntityTypes.StudyTask, E, RowEffect.Created, WasLive: false, CausedByEntityId: E) },
                lifecycle: new[] { new ImpactLifecycle(SyncEntityTypes.StudyTask, E, LifecycleEffect.Create) });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("ALPT.SameIdentityMaterialization", result.RuleId);
        }

        [Fact] // P-AL-3: delete an unrelated sibling under a live parent -- fence passes
        public void DeleteUnrelatedSibling_IsNotApplicable()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, Other, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: Other),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact] // P-AL-4 (router-level in the plan; exercised here at policy level): constructed UpdateFields(E)
        public void UpdateFieldsOnAbsentE_IsBlocked_AbsentIdentityTargeted()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, E, RowEffect.FieldsChanged, WasLive: false, CausedByEntityId: E),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("ALPT.AbsentIdentityTargeted", result.RuleId);
        }

        [Fact] // impact names only E's tombstoned parent P -- Passed, not a resurrection inference
        public void ImpactNamesOnlyTombstonedParentP_IsPassed_FrameUnchanged()
        {
            // E is a StudyTask via MaMonHoc, so P (its held parent) is a MonHoc -- the row's
            // EntityType must match the actual parent type for the guard to accept it.
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.MonHoc, P, RowEffect.FieldsChanged, WasLive: false, CausedByEntityId: P),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.Passed, result.Outcome);
            Assert.Equal("ALPT.FrameUnchanged", result.RuleId);
        }

        [Fact] // B.2 (2026-09-14 owner ruling): row shares P's Guid but is NOT a MonHoc row --
        // must not trigger the parent-frame branch; falls through to NotApplicable.
        public void RowSharesHeldParentIdButWrongEntityType_DoesNotTriggerFrameUnchanged()
        {
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.TaskReferenceLink, P, RowEffect.FieldsChanged, WasLive: true, CausedByEntityId: P),
            });

            var result = Policy.Evaluate(Contract(), impact);

            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
            Assert.Equal("ALPT.NotApplicable", result.RuleId);
        }

        [Fact]
        public void ImpactNeverNamesEOrP_IsNotApplicable()
        {
            var result = Policy.Evaluate(Contract(), ImpactSet.Empty);
            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact]
        public void Derive_SubjectIsAbsentIdentity_FrameIsTombstonedRemoteParent()
        {
            var contract = Contract();
            var subject = Assert.IsType<AbsentIdentitySubject>(contract.Subject);

            Assert.Equal(E, subject.EntityId);
            Assert.Equal(ConflictShape.AbsentLocalParentTombstoned, contract.Shape);
            Assert.NotNull(contract.Edge);
            Assert.Equal(P, contract.Edge!.HeldParentId);
        }
    }
}
