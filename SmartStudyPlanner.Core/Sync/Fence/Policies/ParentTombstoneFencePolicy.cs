using System;

namespace SmartStudyPlanner.Sync.Fence.Policies
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1, S1-PT (fence spec §4, plan §10.2). Protects the staged entity `E` in the
    /// frame of its required structural parent `P`. Deleting `E` always crosses the contract (spec
    /// §3.2 names "E live" as part of the frame). SB-3 ruling (2026-09-14): a non-structural field
    /// edit on the held row is <see cref="FenceOutcome.Blocked"/> -- see
    /// docs/specs/2026-09-14-policy-driven-mutation-routing-owner-rulings.md §2.1.
    /// Explicitly not protected: E's siblings, E's parent's other children, E's descendants (spec §4).
    /// </summary>
    internal sealed class ParentTombstoneFencePolicy : IConflictFencePolicy
    {
        public ConflictShape Shape => ConflictShape.ParentTombstoned;

        public ProtectedContract Derive(SyncConflictRecordRow unresolved)
        {
            if (unresolved is null) throw new ArgumentNullException(nameof(unresolved));

            var entityId = unresolved.EntityId!.Value;
            var field = unresolved.FieldName!;
            var heldParentId = SnapshotField.ReadGuid(unresolved.BaseSnapshotJson, field);

            return new ProtectedContract(
                unresolved.ConflictId,
                unresolved.ScopeKey,
                ConflictShape.ParentTombstoned,
                new EntitySubject(unresolved.EntityType, entityId),
                new ProtectedEdge(unresolved.EntityType, entityId, field, heldParentId),
                new[] { "S1PT.SubjectPresent", "S1PT.EdgeUnchanged" },
                Form: null,
                CandidateIds: SnapshotField.CandidateIds(unresolved));
        }

        public PolicyResult Evaluate(ProtectedContract contract, ImpactSet impact)
        {
            if (contract is null) throw new ArgumentNullException(nameof(contract));
            if (impact is null) throw new ArgumentNullException(nameof(impact));

            var e = (EntitySubject)contract.Subject;

            foreach (var row in impact.Rows)
            {
                if (row.EntityType != e.EntityType || row.EntityId != e.EntityId) continue;

                return row.Effect switch
                {
                    RowEffect.Reparented => Blocked(contract, RoutingStage.DirectSubject,
                        "S1PT.ParentFrameChanged", $"Reparent {e.EntityType} {e.EntityId}"),
                    RowEffect.Tombstoned => Blocked(contract, RoutingStage.DirectSubject,
                        "S1PT.SubjectRemoved", $"Tombstone {e.EntityType} {e.EntityId}"),
                    RowEffect.CascadeTombstoned => Blocked(contract, RoutingStage.CascadeReached,
                        "S1PT.SubjectRemoved", $"CascadeTombstone {e.EntityType} {e.EntityId} via {row.CausedByEntityId}"),
                    RowEffect.Created => Blocked(contract, RoutingStage.DirectSubject,
                        "S1PT.SubjectReplaced", $"Create {e.EntityType} {e.EntityId}"),
                    RowEffect.FieldsChanged => Blocked(contract, RoutingStage.DirectSubject,
                        "S1PT.NonStructuralFields", $"UpdateFields {e.EntityType} {e.EntityId}"),
                    _ => throw new InvalidOperationException($"Unhandled RowEffect {row.Effect}."),
                };
            }

            foreach (var edge in impact.Edges)
            {
                if (edge.ParentId != e.EntityId) continue;

                return new PolicyResult(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                    FenceOutcome.Passed, RoutingStage.CascadeReached, "S1PT.ChildEdgeOnly",
                    $"{edge.Change} child edge {edge.ChildType} {edge.ChildId}.{edge.Field} under {e.EntityType} {e.EntityId}");
            }

            return NotApplicable(contract);
        }

        private static PolicyResult Blocked(ProtectedContract c, RoutingStage stage, string ruleId, string evidence) =>
            new(c.ConflictId, c.ScopeKey, c.Shape, c.Subject, FenceOutcome.Blocked, stage, ruleId, evidence);

        private static PolicyResult NotApplicable(ProtectedContract c) =>
            new(c.ConflictId, c.ScopeKey, c.Shape, c.Subject, FenceOutcome.NotApplicable,
                RoutingStage.DirectSubject, "S1PT.NotApplicable", "impact never names E");
    }
}
