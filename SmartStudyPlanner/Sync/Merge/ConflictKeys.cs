using System;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>
    /// ScopeKey / CandidateFingerprint / ConflictKey exactly as ratified in DoR §5.3.
    /// No <c>Rev</c>, no GUID randomness, no clock: both peers must compute the same key from
    /// mirrored Local/Remote input (D5-G, D7-B).
    /// </summary>
    public static class ConflictKeys
    {
        private const string KeyPrefix = "ck1|";

        public static string ScopeKey(ConflictKind kind, string entityType, Guid? entityId,
                                      string? fieldName, ConstraintScope? scope)
        {
            if (kind == ConflictKind.ConstraintConflict)
            {
                if (scope is null)
                    throw new MergeContractViolationException("A ConstraintConflict needs a ConstraintScope.");
                return scope.EntityType + "|" + scope.Key + "=" + scope.Value;
            }

            if (entityId is null)
                throw new MergeContractViolationException($"A {kind} needs an EntityId.");

            // A tombstone conflict is scoped to the whole row, so it uses the "*" field slot.
            var field = kind == ConflictKind.TombstoneConflict
                ? "*"
                : fieldName ?? throw new MergeContractViolationException($"A {kind} needs a FieldName.");

            return entityType + "|" + entityId.Value.ToString("D") + "|" + field;
        }

        /// <summary>Constraint candidates differ by Id, so the Id is part of the candidate identity.</summary>
        public static string CandidateFingerprint(EntitySnapshot snapshot, Guid entityId) =>
            CanonicalJson.Fingerprint(snapshot) + "@" + entityId.ToString("D");

        public static string ScopeKeyOf(ConflictCandidate candidate) =>
            ScopeKey(candidate.Kind, candidate.EntityType, candidate.EntityId, candidate.FieldName, candidate.Scope);

        public static string ConflictKey(ConflictCandidate candidate)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));

            var left = CandidateFingerprint(candidate.Local, candidate.LocalEntityId);
            var right = CandidateFingerprint(candidate.Remote, candidate.RemoteEntityId);

            // min/max by ordinal order makes the key symmetric: a peer that sees Local and Remote
            // the other way round computes the same key. Both operands are ASCII (hex + '@' + a
            // D-format GUID), so ordinal string order is unambiguous here.
            string lo, hi;
            if (string.CompareOrdinal(left, right) <= 0) { lo = left; hi = right; }
            else { lo = right; hi = left; }

            var payload = KeyPrefix
                        + candidate.Kind + "|"
                        + ScopeKeyOf(candidate) + "|"
                        + CanonicalJson.Fingerprint(candidate.Base) + "|"
                        + lo + "|"
                        + hi;

            return CanonicalJson.Sha256Hex(payload);
        }
    }
}
