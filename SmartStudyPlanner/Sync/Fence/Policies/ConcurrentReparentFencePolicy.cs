using System;
using System.Collections.Generic;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Fence.Policies
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1, S1-CR (fence spec §4, plan §10.1). Protects the conflicted entity `E`
    /// and its contested D4 structural edge. SB-3 ruling (2026-09-14): a non-structural field edit on
    /// the held row is <see cref="FenceOutcome.Blocked"/>, not Passed -- see
    /// docs/specs/2026-09-14-policy-driven-mutation-routing-owner-rulings.md §2.1.
    /// </summary>
    internal sealed class ConcurrentReparentFencePolicy : IConflictFencePolicy
    {
        public ConflictShape Shape => ConflictShape.ConcurrentReparent;

        public ProtectedContract Derive(SyncConflictRecordRow unresolved)
        {
            if (unresolved is null) throw new ArgumentNullException(nameof(unresolved));

            var entityId = unresolved.EntityId!.Value;
            var field = unresolved.FieldName!;
            var heldParentId = SnapshotField.ReadGuid(unresolved.BaseSnapshotJson, field);

            return new ProtectedContract(
                unresolved.ConflictId,
                unresolved.ScopeKey,
                ConflictShape.ConcurrentReparent,
                new EntitySubject(unresolved.EntityType, entityId),
                new ProtectedEdge(unresolved.EntityType, entityId, field, heldParentId),
                new[] { "S1CR.SubjectPresent", "S1CR.EdgeUnchanged" },
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
                        "S1CR.EdgeReplaced", $"Reparent {e.EntityType} {e.EntityId}"),
                    RowEffect.Tombstoned => Blocked(contract, RoutingStage.DirectSubject,
                        "S1CR.SubjectRemoved", $"Tombstone {e.EntityType} {e.EntityId}"),
                    RowEffect.CascadeTombstoned => Blocked(contract, RoutingStage.CascadeReached,
                        "S1CR.SubjectRemoved", $"CascadeTombstone {e.EntityType} {e.EntityId} via {row.CausedByEntityId}"),
                    RowEffect.Created => Blocked(contract, RoutingStage.DirectSubject,
                        "S1CR.SubjectReplaced", $"Create {e.EntityType} {e.EntityId}"),
                    RowEffect.FieldsChanged => Blocked(contract, RoutingStage.DirectSubject,
                        "S1CR.NonStructuralFields", $"UpdateFields {e.EntityType} {e.EntityId}"),
                    _ => throw new InvalidOperationException($"Unhandled RowEffect {row.Effect}."),
                };
            }

            foreach (var edge in impact.Edges)
            {
                if (edge.ParentId != e.EntityId) continue;

                return new PolicyResult(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                    FenceOutcome.Passed, RoutingStage.CascadeReached, "S1CR.ChildEdgeOnly",
                    $"{edge.Change} child edge {edge.ChildType} {edge.ChildId}.{edge.Field} under {e.EntityType} {e.EntityId}");
            }

            return NotApplicable(contract);
        }

        private static PolicyResult Blocked(ProtectedContract c, RoutingStage stage, string ruleId, string evidence) =>
            new(c.ConflictId, c.ScopeKey, c.Shape, c.Subject, FenceOutcome.Blocked, stage, ruleId, evidence);

        private static PolicyResult NotApplicable(ProtectedContract c) =>
            new(c.ConflictId, c.ScopeKey, c.Shape, c.Subject, FenceOutcome.NotApplicable,
                RoutingStage.DirectSubject, "S1CR.NotApplicable", "impact never names E");
    }

    /// <summary>Small shared reader for the two S1 policies and AL-PT; pure, no I/O beyond the string it is given.</summary>
    internal static class SnapshotField
    {
        public static Guid? ReadGuid(string? snapshotJson, string field)
        {
            if (snapshotJson is null) return null;
            var snapshot = CanonicalJson.Read(snapshotJson);
            return snapshot.Fields.TryGetValue(field, out var value) && value is GuidValue g ? g.Value : null;
        }

        public static IReadOnlyList<Guid> CandidateIds(SyncConflictRecordRow row)
        {
            var ids = new List<Guid>(3);
            if (row.BaseEntityId is { } b) ids.Add(b);
            if (row.LocalEntityId is { } l && !ids.Contains(l)) ids.Add(l);
            if (!ids.Contains(row.RemoteEntityId)) ids.Add(row.RemoteEntityId);
            return ids;
        }
    }
}
