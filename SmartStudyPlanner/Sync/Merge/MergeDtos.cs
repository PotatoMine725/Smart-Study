using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStudyPlanner.Sync.Merge
{
    // Epic 2 / T2.3 -- PURE merge core (Decision Record D1; DoR 2026-09-08 §4.1).
    //
    // PURITY CONTRACT for every type in SmartStudyPlanner.Sync.Merge:
    //   no DbContext, no EF Core, no AppDbContext, no Models/* entity, no repository,
    //   no SaveChanges, no SyncStamper, no SyncBaseSnapshotStore, no SyncChangeEnumerator,
    //   no network, no UI, no clock, no persistence.
    // Entities are converted to EntitySnapshot at the apply-layer boundary (T2.4); the pure
    // core never sees a tracked EF object. Enforced by MergeCorePurityTests.

    /// <summary>Identity of what is being merged. EntityType is a <see cref="SyncEntityTypes"/> string.</summary>
    public sealed record EntityRef(string EntityType, Guid EntityId);

    /// <summary>
    /// Row-level provenance as stored on the row. <c>Rev</c> is deliberately ABSENT (D9 §21):
    /// Rev is local-only and must never take part in merge ordering.
    /// </summary>
    public sealed record Provenance(
        DateTime ModifiedAtUtc,        // Kind == Utc, tick precision (DoR §5.4)
        string ModifiedByDeviceId,     // compared with StringComparer.Ordinal
        bool IsDeleted,
        DateTime? DeletedAtUtc);       // Kind == Utc or null

    /// <summary>One field value on the merge surface, already normalised (DoR §5.2). Closed hierarchy.</summary>
    public abstract record FieldValue;

    public sealed record StringValue(string? Value) : FieldValue;
    public sealed record Int32Value(int Value) : FieldValue;              // also enums (underlying int)
    public sealed record BoolValue(bool Value) : FieldValue;
    public sealed record GuidValue(Guid Value) : FieldValue;              // structural + copy-on-create refs
    public sealed record WallClockValue(DateTime? Value) : FieldValue;    // Kind Unspecified, no offset
    public sealed record UtcValue(DateTime? Value) : FieldValue;          // Kind Utc

    /// <summary>
    /// The unit the pure core reasons about. <see cref="Fields"/> holds exactly the registry's
    /// snapshot field set for <see cref="EntityType"/>; canonical order comes from the registry,
    /// never from this dictionary's enumeration order (DoR §5.1 rule 3, test E-2).
    /// </summary>
    public sealed record EntitySnapshot(
        string EntityType,
        Provenance Provenance,
        IReadOnlyDictionary<string, FieldValue> Fields)
    {
        // Hand-written equality: the positional record would compare Fields by REFERENCE, which
        // makes two snapshots built from equal-but-distinct dictionaries unequal. Merge results
        // are compared by value everywhere in this core, so value equality is the correct contract.
        public bool Equals(EntitySnapshot? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!StringComparer.Ordinal.Equals(EntityType, other.EntityType)) return false;
            if (!Provenance.Equals(other.Provenance)) return false;
            if (Fields.Count != other.Fields.Count) return false;
            foreach (var kv in Fields)
            {
                if (!other.Fields.TryGetValue(kv.Key, out var v)) return false;
                if (!Equals(kv.Value, v)) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            var h = new HashCode();
            h.Add(EntityType, StringComparer.Ordinal);
            h.Add(Provenance);
            // Order-independent field contribution so the hash matches the order-independent Equals.
            var acc = 0;
            foreach (var kv in Fields)
                acc ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(kv.Key), kv.Value.GetHashCode());
            h.Add(acc);
            return h.ToHashCode();
        }
    }
}
