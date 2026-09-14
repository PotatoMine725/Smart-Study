using System;
using System.Collections.Generic;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Fence.Policies;
using SmartStudyPlanner.Sync.Merge;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Fence.Policies
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1, Constraint S2/S3 (plan §10.4, §16.1 P-K-1..7). Policy-unit level: every
    /// ImpactSet is hand-constructed; no ImpactResolver/FenceRouter (Slice 2). P-K-4 and P-K-5 are the
    /// SB-3/OD-4 owner-ruling cells (2026-09-14).
    /// </summary>
    public class ConstraintOccupancyFencePolicyTests
    {
        private static readonly Guid T = new("f0000000-0000-0000-0000-0000000000a1");     // owning task for K
        private static readonly Guid N0 = new("f0000000-0000-0000-0000-0000000000b1");    // S2 occupant note
        private static readonly Guid NewNote = new("f0000000-0000-0000-0000-0000000000b2");
        private static readonly Guid T2 = new("f0000000-0000-0000-0000-0000000000a2");    // unrelated task
        private static readonly Guid T2Link = new("f0000000-0000-0000-0000-0000000000c1");

        private static readonly ConstraintOccupancyFencePolicy Policy = new();

        private static SyncConflictRecordRow MakeRow(Guid taskId, Guid? baseEntityId, Guid conflictId) => new()
        {
            ConflictId = conflictId,
            ScopeKey = $"{SyncEntityTypes.TaskNote}|MaTask={taskId:D}",
            Kind = ConflictKind.ConstraintConflict,
            EntityType = SyncEntityTypes.TaskNote,
            ConstraintKey = "MaTask",
            ConstraintValue = taskId.ToString("D"),
            LocalEntityId = N0,
            BaseEntityId = baseEntityId,
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "fp",
            Status = ConflictRecordStatus.Unresolved,
        };

        private static ProtectedContract ContractS2(Guid? conflictId = null) =>
            Policy.Derive(MakeRow(T, N0, conflictId ?? Guid.NewGuid()));

        private static ProtectedContract ContractS3(Guid taskId, Guid? conflictId = null) =>
            Policy.Derive(MakeRow(taskId, null, conflictId ?? Guid.NewGuid()));

        private static ImpactSet Impact(
            IReadOnlyList<ImpactScope>? scopes = null, IReadOnlyList<ImpactRow>? rows = null) =>
            new(rows ?? Array.Empty<ImpactRow>(), Array.Empty<ImpactEdge>(),
                scopes ?? Array.Empty<ImpactScope>(), Array.Empty<ImpactLifecycle>());

        [Fact] // P-K-1: S3, UpsertNoteAsync(T, "x") acquires the empty scope
        public void CreateNoteInEmptyScope_S3_IsBlocked_ScopeAcquired()
        {
            var contract = ContractS3(T);
            var impact = Impact(scopes: new[]
            {
                new ImpactScope(contract.ScopeKey, T, ScopeChange.Acquired, NewNote),
            });

            var result = Policy.Evaluate(contract, impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("CONS.ScopeAcquired", result.RuleId);
        }

        [Fact] // P-K-2: S2, delete T releases the occupied scope
        public void DeleteOwningTask_S2_IsBlocked_ScopeReleased_AtConstraintScope()
        {
            var contract = ContractS2();
            var impact = Impact(
                scopes: new[] { new ImpactScope(contract.ScopeKey, T, ScopeChange.Released, N0) },
                rows: new[] { new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T) });

            var result = Policy.Evaluate(contract, impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("CONS.ScopeReleased", result.RuleId);
            Assert.Equal(RoutingStage.ConstraintScope, result.Stage);
        }

        [Fact] // P-K-3: S2, delete grandparent MonHoc -- 2-level cascade still releases the scope
        public void DeleteGrandparent_S2_TwoLevelCascade_IsBlocked_ScopeReleased()
        {
            var contract = ContractS2();
            var impact = Impact(
                scopes: new[] { new ImpactScope(contract.ScopeKey, T, ScopeChange.Released, N0) },
                rows: new[]
                {
                    new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.CascadeTombstoned, WasLive: true, CausedByEntityId: T),
                });

            var result = Policy.Evaluate(contract, impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("CONS.ScopeReleased", result.RuleId);
        }

        [Fact] // P-K-4: S2, edit N0 content -- SB-3 owner ruling: Blocked, not Passed
        public void EditOccupantContent_S2_IsBlocked_OccupantContent_SB3Ruling()
        {
            var contract = ContractS2();
            var impact = Impact(scopes: new[]
            {
                new ImpactScope(contract.ScopeKey, T, ScopeChange.OccupantContentChanged, N0),
            });

            var result = Policy.Evaluate(contract, impact);

            Assert.Equal(FenceOutcome.Blocked, result.Outcome);
            Assert.Equal("CONS.OccupantContent", result.RuleId);
        }

        [Fact] // P-K-5: S3, empty scope; owning task tombstoned -- OD-4 owner ruling: Passed
        public void DeleteOwningTask_EmptyS3Scope_IsPassed_EmptyScopeParentTombstoned_OD4Ruling()
        {
            var contract = ContractS3(T);
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.StudyTask, T, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T),
            });

            var result = Policy.Evaluate(contract, impact);

            Assert.Equal(FenceOutcome.Passed, result.Outcome);
            Assert.Equal("CONS.EmptyScopeParentTombstoned", result.RuleId);
        }

        [Fact] // P-K-6 (router-level in the plan; exercised here per-contract): S3 at K1 and K2, reassign K1 -> K2
        public void ReassignAcrossTwoS3Scopes_BothContractsAreBlockedIndependently()
        {
            var t1 = new Guid("f0000000-0000-0000-0000-0000000000d1");
            var t2 = new Guid("f0000000-0000-0000-0000-0000000000d2");
            var note = new Guid("f0000000-0000-0000-0000-0000000000d3");

            var contractK1 = ContractS3(t1);
            var contractK2 = ContractS3(t2);

            var impact = Impact(scopes: new[]
            {
                new ImpactScope(contractK1.ScopeKey, t1, ScopeChange.Released, note),
                new ImpactScope(contractK2.ScopeKey, t2, ScopeChange.Acquired, note),
            });

            var resultK1 = Policy.Evaluate(contractK1, impact);
            var resultK2 = Policy.Evaluate(contractK2, impact);

            Assert.Equal(FenceOutcome.Blocked, resultK1.Outcome);
            Assert.Equal("CONS.ScopeReleased", resultK1.RuleId);
            Assert.Equal(FenceOutcome.Blocked, resultK2.Outcome);
            Assert.Equal("CONS.ScopeAcquired", resultK2.RuleId);
        }

        [Fact] // P-K-7: S2 on K(T); unrelated S3 on K(T2); delete T2's link -- fence passes for K(T)
        public void UnrelatedTaskLinkDeleted_IsNotApplicable_ForK()
        {
            var contract = ContractS2();
            var impact = Impact(rows: new[]
            {
                new ImpactRow(SyncEntityTypes.TaskReferenceLink, T2Link, RowEffect.Tombstoned, WasLive: true, CausedByEntityId: T2),
            });

            var result = Policy.Evaluate(contract, impact);

            Assert.Equal(FenceOutcome.NotApplicable, result.Outcome);
        }

        [Fact]
        public void Derive_S2_HasBasePresentForm_S3_HasBaseNullForm()
        {
            Assert.Equal(ConstraintForm.BasePresent, ContractS2().Form);
            Assert.Equal(ConstraintForm.BaseNull, ContractS3(T).Form);
        }
    }
}
