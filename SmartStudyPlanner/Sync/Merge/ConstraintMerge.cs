using System;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>
    /// D5 constraint-scope detection (DoR §7.2). Structural, never driven by catching a UNIQUE
    /// violation (D9 §19). Produces evidence only: neither candidate becomes live, because while
    /// the conflict is unresolved the live row equals Base (D9-T1). The scope-lock (D9-T6) and
    /// the parent checks (D9-T4) are apply-layer concerns and are deliberately absent here.
    /// </summary>
    public static class ConstraintMerge
    {
        public static ConstraintDetection Detect(ConstraintScopeInput input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (input.Scope is null) throw new MergeContractViolationException("ConstraintScopeInput.Scope is null.");

            var spec = MergeSurfaceRegistry.Get(input.Scope.EntityType);
            Require(spec, "Base", input.Base);
            Require(spec, "Local", input.Local);
            Require(spec, "Remote", input.Remote);

            var local = input.Local;
            var remote = input.Remote;

            if (local is null && remote is null) return new ConstraintDetection(ConstraintOutcome.NoAction, null, null, null);

            if (local is null)
                return new ConstraintDetection(ConstraintOutcome.CreateRemote, null, null, remote!.Value.Id);

            if (remote is null)
                return new ConstraintDetection(ConstraintOutcome.LocalOnly, null, local.Value.Id, null);

            // Same row on both sides: this is an ordinary three-way merge, not a constraint clash.
            if (local.Value.Id == remote.Value.Id)
                return new ConstraintDetection(ConstraintOutcome.OrdinaryMerge, null, local.Value.Id, remote.Value.Id);

            // The scope is held by a tombstone and the remote wants it for a different row. v1 has
            // no resurrection and no partial index, so this fails closed instead of relying on the
            // UNIQUE index to raise. Unreachable by construction today (DoR §7.2, last row).
            if (local.Value.Snap.Provenance.IsDeleted)
            {
                return new ConstraintDetection(ConstraintOutcome.ScopeOccupiedByTombstone, null,
                                               local.Value.Id, remote.Value.Id);
            }

            var candidate = new ConflictCandidate(
                ConflictKind.ConstraintConflict,
                input.Scope.EntityType,
                null,                       // scope-addressed, not entity-addressed
                null,
                input.Scope,
                null,
                input.Base?.Snap,
                input.Base?.Id,
                local.Value.Snap, local.Value.Id,
                remote.Value.Snap, remote.Value.Id,
                null,                       // no auto winner (D5)
                null);

            return new ConstraintDetection(ConstraintOutcome.ConstraintConflict, candidate,
                                           local.Value.Id, remote.Value.Id);
        }

        private static void Require(EntitySpec spec, string role, (Guid Id, EntitySnapshot Snap)? side)
        {
            if (side is null) return;

            if (!StringComparer.Ordinal.Equals(side.Value.Snap.EntityType, spec.EntityType))
            {
                throw new MergeContractViolationException(
                    $"{role} in scope {spec.EntityType} is a {side.Value.Snap.EntityType} snapshot.");
            }

            if (side.Value.Snap.Fields.Count != spec.SnapshotFields.Count)
            {
                throw new MergeContractViolationException(
                    $"{role} carries {side.Value.Snap.Fields.Count} fields; {spec.EntityType} declares {spec.SnapshotFields.Count}.");
            }
        }
    }
}
