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

        /// <summary>
        /// Stands in for a candidate that does not exist (D4/D9-T4 amendment 2026-09-10: a
        /// StructuralConflict whose logical child scope holds no local row). Deliberately NOT
        /// <c>fp(null)@00000000-...</c>: a real candidate's fingerprint is always
        /// "64 hex chars + '@' + a D-format GUID", so a one-character sentinel can never collide with
        /// one, not even with a row whose Id happens to be <see cref="Guid.Empty"/>. ASCII, so the
        /// ordinal ordering in <see cref="ConflictKey"/> stays unambiguous.
        /// </summary>
        public const string AbsentCandidate = "-";

        /// <summary>Constraint candidates differ by Id, so the Id is part of the candidate identity.</summary>
        public static string CandidateFingerprint(EntitySnapshot? snapshot, Guid? entityId) =>
            snapshot is null || entityId is null
                ? AbsentCandidate
                : CanonicalJson.Fingerprint(snapshot) + "@" + entityId.Value.ToString("D");

        public static string ScopeKeyOf(ConflictCandidate candidate) =>
            ScopeKey(candidate.Kind, candidate.EntityType, candidate.EntityId, candidate.FieldName, candidate.Scope);

        public static string ConflictKey(ConflictCandidate candidate)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));

            // D4/D9-T4 amendment (2026-09-10). Absence is legal for a StructuralConflict only, and the
            // snapshot and the id must agree about it -- half-absent evidence would be unreadable for
            // PR-6 and is exactly the "fabricated placeholder" shape the amendment forbids.
            if ((candidate.Local is null) != (candidate.LocalEntityId is null))
                throw new MergeContractViolationException(
                    "A candidate's local snapshot and LocalEntityId must be present or absent together.");

            if (candidate.Local is null && candidate.Kind != ConflictKind.StructuralConflict)
                throw new MergeContractViolationException(
                    $"A {candidate.Kind} needs a local candidate; only a StructuralConflict may have none.");

            var left = CandidateFingerprint(candidate.Local, candidate.LocalEntityId);
            var right = CandidateFingerprint(candidate.Remote, candidate.RemoteEntityId);

            // min/max by ordinal order makes the key symmetric: a peer that sees Local and Remote
            // the other way round computes the same key. Both operands are ASCII (hex + '@' + a
            // D-format GUID, or AbsentCandidate), so ordinal string order is unambiguous here.
            //
            // For an absent local candidate the mirroring rationale (D5-G/D7-B) genuinely does not
            // apply: that case is asymmetric by construction -- one peer has the row and the other has
            // a tombstoned parent and no row, so the two sides never see mirrored input. What is
            // preserved, and what the replay path actually needs, is DETERMINISM: the same input
            // always yields the same key, so a re-offered create cannot mint a second record.
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
