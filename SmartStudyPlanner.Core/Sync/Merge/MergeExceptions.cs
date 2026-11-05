using System;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>Fail-closed reasons for SnapshotJson v1 (DoR §5.5 rows 1-9).</summary>
    public enum SnapshotContractReason
    {
        UnknownVersion,
        UnknownEntityType,
        MissingField,
        ExtraField,
        TypeMismatch,
        InvalidDateTime,
        MissingProvenance,
        TombstoneWithoutTimestamp,
        NonCanonical
    }

    /// <summary>
    /// SnapshotJson v1 contract violation. Every condition in DoR §5.5 fails closed with one of
    /// these; nothing is repaired, defaulted, or downgraded to "treat as null Base".
    /// (Row 10, BaselineUnreadable, is an apply-layer concern and is NOT in the pure core.)
    /// </summary>
    public sealed class SnapshotContractException : Exception
    {
        public SnapshotContractReason Reason { get; }

        public SnapshotContractException(SnapshotContractReason reason, string message)
            : base($"{reason}: {message}")
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// The pure core was handed input that the model says cannot exist (mismatched field set,
    /// non-UTC provenance, a differing copy-on-create value on the same Id, resurrection of a
    /// tombstone). DoR §4.1: contract violations are NEVER turned into conflicts.
    /// </summary>
    public sealed class MergeContractViolationException : Exception
    {
        public MergeContractViolationException(string message) : base(message) { }
    }
}
