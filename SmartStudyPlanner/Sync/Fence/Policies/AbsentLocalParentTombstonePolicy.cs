using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Fence.Policies
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1, AL-PT (fence spec §4.2, plan §10.3). Protects the absence of logical
    /// identity `E` in live state. No ordinary create/update path may materialize `E` -- "absent, so
    /// create is harmless" is never a valid inference (spec §4.2). Blocking never depends on which
    /// parent a materialization proposes for `E` -- the identity itself is protected, not a specific edge.
    /// </summary>
    internal sealed class AbsentLocalParentTombstonePolicy : IConflictFencePolicy
    {
        public ConflictShape Shape => ConflictShape.AbsentLocalParentTombstoned;

        public ProtectedContract Derive(SyncConflictRecordRow unresolved)
        {
            if (unresolved is null) throw new ArgumentNullException(nameof(unresolved));

            var entityId = unresolved.EntityId!.Value;
            var field = unresolved.FieldName!;
            // AL-PT has no Base row; the frame is the tombstoned Remote parent the absent candidate points at.
            var heldParentId = SnapshotField.ReadGuid(unresolved.RemoteSnapshotJson, field);

            var candidateIds = new List<Guid> { unresolved.RemoteEntityId };
            if (!candidateIds.Contains(entityId)) candidateIds.Add(entityId);

            return new ProtectedContract(
                unresolved.ConflictId,
                unresolved.ScopeKey,
                ConflictShape.AbsentLocalParentTombstoned,
                new AbsentIdentitySubject(unresolved.EntityType, entityId),
                new ProtectedEdge(unresolved.EntityType, entityId, field, heldParentId),
                new[] { "ALPT.RemainsAbsent" },
                Form: null,
                CandidateIds: candidateIds);
        }

        public PolicyResult Evaluate(ProtectedContract contract, ImpactSet impact)
        {
            if (contract is null) throw new ArgumentNullException(nameof(contract));
            if (impact is null) throw new ArgumentNullException(nameof(impact));

            var e = (AbsentIdentitySubject)contract.Subject;

            foreach (var row in impact.Rows)
            {
                if (row.EntityType != e.EntityType || row.EntityId != e.EntityId) continue;

                return row.Effect == RowEffect.Created
                    ? Blocked("ALPT.SameIdentityMaterialization", $"Create {e.EntityType} {e.EntityId}")
                    : Blocked("ALPT.AbsentIdentityTargeted", $"{row.Effect} {e.EntityType} {e.EntityId}");
            }

            var parentId = contract.Edge?.HeldParentId;
            if (parentId is { } p)
            {
                var expectedParentType = ExpectedParentType(contract.Edge!.ChildType, contract.Edge!.Field);
                foreach (var row in impact.Rows)
                {
                    if (row.EntityType != expectedParentType || row.EntityId != p) continue;

                    return new PolicyResult(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                        FenceOutcome.Passed, RoutingStage.CascadeReached, "ALPT.FrameUnchanged",
                        $"impact reaches tombstoned parent {p}, not the absent identity {e.EntityId}");
                }
            }

            return new PolicyResult(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                FenceOutcome.NotApplicable, RoutingStage.DirectSubject, "ALPT.NotApplicable",
                "impact never names the absent identity or its tombstoned parent frame");

            PolicyResult Blocked(string ruleId, string evidence) =>
                new(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                    FenceOutcome.Blocked, RoutingStage.DirectSubject, ruleId, evidence);
        }

        // Same two structural fields ConflictShapeClassifier.IsKnownStructuralField recognizes --
        // AL-PT is only ever derived from one of these, so the pair is always known by the time
        // Evaluate runs.
        private static string ExpectedParentType(string childType, string field) => (childType, field) switch
        {
            (var t, "MaMonHoc") when t == SyncEntityTypes.StudyTask => SyncEntityTypes.MonHoc,
            (var t, "MaHocKy") when t == SyncEntityTypes.MonHoc => SyncEntityTypes.HocKy,
            _ => throw new InvalidOperationException($"Unhandled structural field {childType}.{field}."),
        };
    }
}
