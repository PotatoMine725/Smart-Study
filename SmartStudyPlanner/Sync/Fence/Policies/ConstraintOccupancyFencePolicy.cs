using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Fence.Policies
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 1, Constraint (S2/S3, incl. null-Base) (fence spec §4, plan §10.4). Protects
    /// a concrete constraint scope `K = TaskNote|MaTask=t`, not every TaskNote or every task descendant
    /// (spec §4 "Explicitly not protected"). SB-3 ruling (2026-09-14): editing the held S2 occupant's
    /// content is <see cref="FenceOutcome.Blocked"/>. OD-4 ruling (2026-09-14): an owning-task
    /// tombstone over an empty S3 scope is <see cref="FenceOutcome.Passed"/> -- this policy's
    /// contribution only; every other applicable policy on the task still evaluates independently
    /// (INV-4). See docs/specs/2026-09-14-policy-driven-mutation-routing-owner-rulings.md §2.1-2.2.
    /// </summary>
    internal sealed class ConstraintOccupancyFencePolicy : IConflictFencePolicy
    {
        public ConflictShape Shape => ConflictShape.ConstraintOccupancy;

        public ProtectedContract Derive(SyncConflictRecordRow unresolved)
        {
            if (unresolved is null) throw new ArgumentNullException(nameof(unresolved));

            var scopeValue = Guid.ParseExact(unresolved.ConstraintValue!, "D");
            var form = unresolved.BaseEntityId is not null ? ConstraintForm.BasePresent : ConstraintForm.BaseNull;

            var candidateIds = new List<Guid>(3);
            if (unresolved.BaseEntityId is { } b) candidateIds.Add(b);
            if (unresolved.LocalEntityId is { } l && !candidateIds.Contains(l)) candidateIds.Add(l);
            if (!candidateIds.Contains(unresolved.RemoteEntityId)) candidateIds.Add(unresolved.RemoteEntityId);

            return new ProtectedContract(
                unresolved.ConflictId,
                unresolved.ScopeKey,
                ConflictShape.ConstraintOccupancy,
                new ConstraintScopeSubject(unresolved.ScopeKey, scopeValue),
                Edge: null,
                new[] { form == ConstraintForm.BasePresent ? "CONS.OccupantUnchanged" : "CONS.ScopeEmpty" },
                form,
                candidateIds);
        }

        public PolicyResult Evaluate(ProtectedContract contract, ImpactSet impact)
        {
            if (contract is null) throw new ArgumentNullException(nameof(contract));
            if (impact is null) throw new ArgumentNullException(nameof(impact));

            var k = (ConstraintScopeSubject)contract.Subject;

            foreach (var scope in impact.Scopes)
            {
                if (scope.ScopeKey != k.ScopeKey) continue;

                return scope.Change switch
                {
                    ScopeChange.Acquired => Blocked("CONS.ScopeAcquired", $"Acquired {k.ScopeKey} by {scope.OccupantId}"),
                    ScopeChange.Released => Blocked("CONS.ScopeReleased", $"Released {k.ScopeKey} (was {scope.OccupantId})"),
                    ScopeChange.OccupantContentChanged => Blocked("CONS.OccupantContent",
                        $"OccupantContentChanged {k.ScopeKey} occupant {scope.OccupantId}"),
                    _ => throw new InvalidOperationException($"Unhandled ScopeChange {scope.Change}."),
                };
            }

            var tombstoned = false;
            foreach (var row in impact.Rows)
            {
                if (row.EntityType != SyncEntityTypes.StudyTask || row.EntityId != k.ScopeValue) continue;
                if (row.Effect is RowEffect.Tombstoned or RowEffect.CascadeTombstoned) tombstoned = true;
            }

            if (tombstoned)
            {
                return new PolicyResult(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                    FenceOutcome.Passed, RoutingStage.ConstraintScope, "CONS.EmptyScopeParentTombstoned",
                    $"owning task {k.ScopeValue} tombstoned; scope {k.ScopeKey} occupancy unchanged");
            }

            return new PolicyResult(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                FenceOutcome.NotApplicable, RoutingStage.ConstraintScope, "CONS.NotApplicable",
                $"impact never names scope {k.ScopeKey} or its owning task {k.ScopeValue}");

            PolicyResult Blocked(string ruleId, string evidence) =>
                new(contract.ConflictId, contract.ScopeKey, Shape, contract.Subject,
                    FenceOutcome.Blocked, RoutingStage.ConstraintScope, ruleId, evidence);
        }
    }
}
