using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>
    /// Pure three-way entity merge: D9 §12.1's five-case table with per-field independence
    /// (D9 §13), D4 structural handling (DoR §7.1), tombstone-wins (DoR §7.3) and the D9-T5
    /// auto-resolution evidence. Static and referentially transparent: no clock, no device id,
    /// no persistence, no mutable state, and the inputs are never modified.
    /// </summary>
    public static class ThreeWayMerge
    {
        private static readonly IReadOnlyList<FieldOutcome> NoOutcomes = Array.Empty<FieldOutcome>();
        private static readonly IReadOnlyList<ConflictCandidate> NoCandidates = Array.Empty<ConflictCandidate>();

        public static MergedEntity Merge(EntityRef entity, EntitySnapshot? baseSnapshot,
                                         EntitySnapshot local, EntitySnapshot remote)
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));
            if (local is null) throw new ArgumentNullException(nameof(local));
            if (remote is null) throw new ArgumentNullException(nameof(remote));

            var spec = MergeSurfaceRegistry.Get(entity.EntityType);
            Validate(spec, "Base", baseSnapshot);
            Validate(spec, "Local", local);
            Validate(spec, "Remote", remote);

            var tombstoned = MergeTombstones(entity, spec, baseSnapshot, local, remote);
            if (tombstoned is not null) return tombstoned;

            return MergeFields(entity, spec, baseSnapshot, local, remote);
        }

        // ---- DoR §7.3: evaluated BEFORE any field merge ------------------------------------

        private static MergedEntity? MergeTombstones(EntityRef entity, EntitySpec spec,
                                                     EntitySnapshot? baseSnapshot,
                                                     EntitySnapshot local, EntitySnapshot remote)
        {
            var baseDead = baseSnapshot?.Provenance.IsDeleted ?? false;
            var localDead = local.Provenance.IsDeleted;
            var remoteDead = remote.Provenance.IsDeleted;

            if (baseDead)
            {
                if (!localDead || !remoteDead)
                {
                    // No code path un-tombstones a row, so a live side over a dead Base is not a
                    // conflict — it is input the model says cannot exist. Fail closed (DoR §7.3).
                    throw new MergeContractViolationException(
                        $"{entity.EntityType} {entity.EntityId:D}: Base is tombstoned but a side is live; resurrection does not exist in this model.");
                }

                // dead / dead / dead: nothing to apply anywhere.
                return new MergedEntity(entity, local, local.Provenance, NoOutcomes, NoCandidates, true);
            }

            if (!localDead && !remoteDead) return null;   // ordinary field merge

            if (localDead && remoteDead)
            {
                // Both sides deleted independently: tombstone wins with a deterministic provenance.
                var winner = GreaterSide(local, remote);
                return new MergedEntity(entity, winner, winner.Provenance, NoOutcomes, NoCandidates,
                                        winner.Equals(local));
            }

            // Delete versus edit: the tombstone wins regardless of how much later the editor wrote.
            var dead = localDead ? local : remote;
            var live = localDead ? remote : local;

            var candidates = NoCandidates;
            if (baseSnapshot is null || FieldsDiffer(spec, live, baseSnapshot))
            {
                // Evidence only when the live side actually made a competing claim; an untouched
                // live row is not a conflict, it just loses the row (DoR §7.3 D-2/D-4).
                candidates = new[]
                {
                    new ConflictCandidate(
                        ConflictKind.TombstoneConflict, entity.EntityType, entity.EntityId,
                        null, null, null,
                        baseSnapshot, baseSnapshot is null ? null : entity.EntityId,
                        local, entity.EntityId, remote, entity.EntityId,
                        ResolutionKind.AutoTombstone, dead)
                };
            }

            return new MergedEntity(entity, dead, dead.Provenance, NoOutcomes, candidates, dead.Equals(local));
        }

        // ---- D9 §12.1 five cases, per field --------------------------------------------------

        private static MergedEntity MergeFields(EntityRef entity, EntitySpec spec,
                                                EntitySnapshot? baseSnapshot,
                                                EntitySnapshot local, EntitySnapshot remote)
        {
            var outcomes = new List<FieldOutcome>(spec.SnapshotFields.Count);
            var candidates = new List<ConflictCandidate>();
            var merged = new Dictionary<string, FieldValue>(StringComparer.Ordinal);

            var structuralConflict = false;
            var localChangedSomething = false;
            var remoteChangedSomething = false;

            foreach (var f in spec.SnapshotFields)
            {
                var localValue = local.Fields[f.Name];
                var remoteValue = remote.Fields[f.Name];
                var baseValue = baseSnapshot?.Fields[f.Name];

                // Creation provenance and the constraint-scope key are immutable on a given Id
                // (D9-T3 / D5): a difference is impossible by construction, so it is a contract
                // violation rather than something to merge.
                if (f.Class is FieldClass.CopyOnCreate or FieldClass.ConstraintScope)
                {
                    if (!localValue.Equals(remoteValue))
                    {
                        throw new MergeContractViolationException(
                            $"{spec.EntityType}.{f.Name} is immutable on {entity.EntityId:D} but Local and Remote differ.");
                    }

                    var immutableDecision = baseValue is not null && localValue.Equals(baseValue)
                        ? FieldDecision.Base
                        : FieldDecision.Common;
                    outcomes.Add(new FieldOutcome(f.Name, immutableDecision, localValue));
                    merged[f.Name] = localValue;
                    continue;
                }

                // A null Base means neither side can be shown to be unchanged, so both count as
                // changed and the field lands in case 4 or case 5.
                var localChanged = baseValue is null || !localValue.Equals(baseValue);
                var remoteChanged = baseValue is null || !remoteValue.Equals(baseValue);
                if (localChanged) localChangedSomething = true;
                if (remoteChanged) remoteChangedSomething = true;

                if (!localChanged && !remoteChanged)                        // case 1
                {
                    outcomes.Add(new FieldOutcome(f.Name, FieldDecision.Base, baseValue));
                    merged[f.Name] = baseValue!;
                }
                else if (localChanged && !remoteChanged)                    // case 2
                {
                    outcomes.Add(new FieldOutcome(f.Name, FieldDecision.Local, localValue));
                    merged[f.Name] = localValue;
                }
                else if (!localChanged && remoteChanged)                    // case 3
                {
                    outcomes.Add(new FieldOutcome(f.Name, FieldDecision.Remote, remoteValue));
                    merged[f.Name] = remoteValue;
                }
                else if (localValue.Equals(remoteValue))                    // case 4
                {
                    outcomes.Add(new FieldOutcome(f.Name, FieldDecision.Common, localValue));
                    merged[f.Name] = localValue;
                }
                else if (f.Class == FieldClass.Structural)                  // D4: never LWW
                {
                    structuralConflict = true;
                    outcomes.Add(new FieldOutcome(f.Name, FieldDecision.StructuralConflict, null));
                    candidates.Add(new ConflictCandidate(
                        ConflictKind.StructuralConflict, entity.EntityType, entity.EntityId,
                        f.Name, null, StructuralReason.ConcurrentReparent,
                        baseSnapshot, baseSnapshot is null ? null : entity.EntityId,
                        local, entity.EntityId, remote, entity.EntityId,
                        null, null));                                       // no auto-winner
                }
                else                                                        // case 5: LWW
                {
                    var side = Lww.Decide(local.Provenance, localValue, remote.Provenance, remoteValue);
                    var winningValue = side == LwwSide.Local ? localValue : remoteValue;

                    outcomes.Add(new FieldOutcome(f.Name,
                        side == LwwSide.Local ? FieldDecision.LwwLocal : FieldDecision.LwwRemote, winningValue));
                    merged[f.Name] = winningValue;

                    // D9-T5: the LWW decision is itself the evidence, written directly as Resolved.
                    candidates.Add(new ConflictCandidate(
                        ConflictKind.FieldConflict, entity.EntityType, entity.EntityId,
                        f.Name, null, null,
                        baseSnapshot, baseSnapshot is null ? null : entity.EntityId,
                        local, entity.EntityId, remote, entity.EntityId,
                        ResolutionKind.AutoLww, side == LwwSide.Local ? local : remote));
                }
            }

            if (structuralConflict)
            {
                // D9-T1: while a structural conflict is unresolved the WHOLE live row equals Base;
                // partially applying the other fields would make live != Base. A null Base means
                // the scope has no live row at all.
                var structuralOnly = new List<ConflictCandidate>();
                foreach (var c in candidates)
                    if (c.Kind == ConflictKind.StructuralConflict) structuralOnly.Add(c);

                return new MergedEntity(
                    entity,
                    baseSnapshot,
                    baseSnapshot?.Provenance ?? local.Provenance,
                    outcomes,
                    structuralOnly,
                    baseSnapshot is not null && baseSnapshot.Equals(local));
            }

            var rowUntouched = baseSnapshot is not null && AllBase(outcomes);
            var provenance = ResultProvenance(baseSnapshot, local, remote,
                                              rowUntouched, localChangedSomething, remoteChangedSomething);

            var result = new EntitySnapshot(entity.EntityType, provenance, merged);
            return new MergedEntity(entity, result, provenance, outcomes, candidates,
                                    rowUntouched || result.Equals(local));
        }

        // ---- DoR §6.5 ------------------------------------------------------------------------

        private static Provenance ResultProvenance(EntitySnapshot? baseSnapshot,
                                                   EntitySnapshot local, EntitySnapshot remote,
                                                   bool rowUntouched, bool localChanged, bool remoteChanged)
        {
            if (rowUntouched) return baseSnapshot!.Provenance;
            if (localChanged && !remoteChanged) return local.Provenance;
            if (!localChanged && remoteChanged) return remote.Provenance;

            // Both sides moved: the greater (Ticks, DeviceId) side, which is also the side that wins
            // every case-5 field in this row. Symmetric, so both peers converge on the same value.
            return Lww.CompareProvenance(local.Provenance, remote.Provenance) >= 0
                ? local.Provenance
                : remote.Provenance;
        }

        // ---- helpers -------------------------------------------------------------------------

        private static bool AllBase(List<FieldOutcome> outcomes)
        {
            foreach (var o in outcomes)
                if (o.Decision != FieldDecision.Base) return false;
            return true;
        }

        private static bool FieldsDiffer(EntitySpec spec, EntitySnapshot a, EntitySnapshot b)
        {
            foreach (var f in spec.SnapshotFields)
                if (!a.Fields[f.Name].Equals(b.Fields[f.Name])) return true;
            return false;
        }

        /// <summary>Deterministic, symmetric pick between two tombstones (DoR §7.3 row 2).</summary>
        private static EntitySnapshot GreaterSide(EntitySnapshot a, EntitySnapshot b)
        {
            var byProvenance = Lww.CompareProvenance(a.Provenance, b.Provenance);
            if (byProvenance != 0) return byProvenance > 0 ? a : b;

            return string.CompareOrdinal(CanonicalJson.Fingerprint(a), CanonicalJson.Fingerprint(b)) >= 0 ? a : b;
        }

        // ---- contract validation (never turned into conflicts, DoR §4.1) ---------------------

        private static void Validate(EntitySpec spec, string role, EntitySnapshot? snapshot)
        {
            if (snapshot is null) return;

            if (!StringComparer.Ordinal.Equals(snapshot.EntityType, spec.EntityType))
            {
                throw new MergeContractViolationException(
                    $"{role} is a {snapshot.EntityType} snapshot but the merge is for {spec.EntityType}.");
            }

            if (snapshot.Fields.Count != spec.SnapshotFields.Count)
            {
                throw new MergeContractViolationException(
                    $"{role} carries {snapshot.Fields.Count} fields; {spec.EntityType} declares {spec.SnapshotFields.Count}.");
            }

            foreach (var f in spec.SnapshotFields)
            {
                if (!snapshot.Fields.TryGetValue(f.Name, out var value))
                    throw new MergeContractViolationException($"{role} is missing {spec.EntityType}.{f.Name}.");

                if (!Matches(f, value))
                {
                    throw new MergeContractViolationException(
                        $"{role}.{f.Name} is {value.GetType().Name}, which does not match registry type {f.ValueType.Name}.");
                }
            }

            var p = snapshot.Provenance;
            if (p.ModifiedAtUtc.Kind != DateTimeKind.Utc)
                throw new MergeContractViolationException($"{role}.ModifiedAtUtc must be Kind=Utc, was {p.ModifiedAtUtc.Kind}.");
            if (string.IsNullOrEmpty(p.ModifiedByDeviceId))
                throw new MergeContractViolationException($"{role}.ModifiedByDeviceId is empty.");
            if (p.DeletedAtUtc is { } d && d.Kind != DateTimeKind.Utc)
                throw new MergeContractViolationException($"{role}.DeletedAtUtc must be Kind=Utc, was {d.Kind}.");
            if (p.IsDeleted && p.DeletedAtUtc is null)
                throw new MergeContractViolationException($"{role} is tombstoned without a DeletedAtUtc.");
        }

        private static bool Matches(FieldSpec f, FieldValue value)
        {
            if (f.ValueType == typeof(string)) return value is StringValue;
            if (f.ValueType == typeof(int)) return value is Int32Value;
            if (f.ValueType == typeof(bool)) return value is BoolValue;
            if (f.ValueType == typeof(Guid)) return value is GuidValue;
            if (f.ValueType == typeof(DateTime)) return f.IsUtc ? value is UtcValue : value is WallClockValue;
            return false;
        }
    }
}
