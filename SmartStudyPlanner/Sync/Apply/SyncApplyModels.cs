using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// One incoming remote row, already at the snapshot boundary. The session never receives another
    /// context's tracked EF instance: a remote row arrives as an <see cref="EntitySnapshot"/>, which is
    /// exactly what the pure core consumes and what a future T2.1 transport would deserialize into.
    /// </summary>
    public sealed record IncomingChange(string EntityType, Guid EntityId, EntitySnapshot Snapshot);

    /// <summary>Everything one peer offers in one exchange (D8-G: many independent operations).</summary>
    public sealed record IncomingChangeSet(string PeerDeviceId, IReadOnlyList<IncomingChange> Changes);

    public enum SyncOperationKind
    {
        /// <summary>One entity addressed by its PK (create / update / tombstone).</summary>
        Entity,

        /// <summary>One <c>TaskNote</c> uniqueness scope (D5): the whole <c>MaTask</c> scope is the unit.</summary>
        TaskNoteScope,
    }

    /// <summary>
    /// One logical sync operation = one transaction (D8-G). <see cref="Phase"/> implements the DoR
    /// §12.5 ordering: parent tombstones first (top-down), then creates/updates (top-down), so a child
    /// is always evaluated against parents that are already committed.
    /// </summary>
    public sealed record SyncOperation(
        SyncOperationKind Kind,
        string EntityType,
        Guid EntityId,
        Guid ScopeValue,
        EntitySnapshot Remote,
        int Phase)
    {
        public string Describe() => Kind == SyncOperationKind.TaskNoteScope
            ? $"TaskNote scope MaTask={ScopeValue:D}"
            : $"{EntityType} {EntityId:D}";
    }

    public enum SyncApplyOutcome
    {
        /// <summary>The merged result was written (create / update / tombstone) and committed.</summary>
        Applied,

        /// <summary>The merge was a no-op; no domain row was written. The baseline may still advance.</summary>
        NoOp,

        /// <summary>An Unresolved conflict was staged; live state was left equal to Base (D9-T1).</summary>
        ConflictStaged,

        /// <summary>Evidence for this candidate was already stored (same ConflictKey). Retry-safe no-op.</summary>
        ConflictAlreadyStaged,

        /// <summary>Nothing was written and the transaction was rolled back. See <see cref="SyncApplyReason"/>.</summary>
        Rejected,

        /// <summary>An exception escaped the operation. The transaction was rolled back and the context disposed.</summary>
        Failed,
    }

    public enum SyncApplyReason
    {
        None = 0,

        /// <summary>D9-T6 / DoR §12.4: an Unresolved record already occupies this logical scope.</summary>
        ScopeHasUnresolvedConflict,

        /// <summary>DoR §12.2: a required parent row does not exist locally, even after the retry pass.</summary>
        MissingParent,

        /// <summary>
        /// D9-T4: the incoming child targets a tombstoned D4 parent and there is no local row to hold
        /// at Base, so the child is not materialised. See <c>SyncApplySession</c>'s remarks.
        /// </summary>
        ParentTombstonedNoLocalRow,

        /// <summary>DoR §5.5 row 10: the stored baseline row exists but its SnapshotJson is unusable.</summary>
        BaselineUnreadable,

        /// <summary>A baseline exists for this row but the row itself is gone — impossible without a hard delete.</summary>
        LocalRowMissing,

        /// <summary>DoR §7.2 last row: the uniqueness scope is held by a tombstone and a different live row wants it.</summary>
        ScopeOccupiedByTombstone,

        /// <summary>A ConcurrentReparent conflict with a null Base has no Base row to hold live. Unreachable by construction.</summary>
        StructuralConflictWithoutBase,

        /// <summary>The pure core or the boundary rejected the input as impossible (never turned into a conflict).</summary>
        ContractViolation,

        /// <summary>An unexpected exception escaped. <see cref="SyncApplyResult.Error"/> carries it.</summary>
        Exception,
    }

    /// <summary>Result of exactly one logical operation (one transaction).</summary>
    public sealed record SyncApplyResult(
        SyncOperation Operation,
        SyncApplyOutcome Outcome,
        SyncApplyReason Reason = SyncApplyReason.None,
        Guid? ConflictId = null,
        Exception? Error = null)
    {
        public bool Committed => Outcome is SyncApplyOutcome.Applied
                                          or SyncApplyOutcome.NoOp
                                          or SyncApplyOutcome.ConflictStaged
                                          or SyncApplyOutcome.ConflictAlreadyStaged;

        public static SyncApplyResult Applied(SyncOperation op) => new(op, SyncApplyOutcome.Applied);
        public static SyncApplyResult NoOp(SyncOperation op) => new(op, SyncApplyOutcome.NoOp);
        public static SyncApplyResult Rejected(SyncOperation op, SyncApplyReason reason) =>
            new(op, SyncApplyOutcome.Rejected, reason);
        public static SyncApplyResult Failed(SyncOperation op, Exception ex) =>
            new(op, SyncApplyOutcome.Failed, SyncApplyReason.Exception, null, ex);
    }

    /// <summary>Outcome of a whole run: one result per logical operation, in execution order.</summary>
    public sealed record SyncApplyReport(IReadOnlyList<SyncApplyResult> Results)
    {
        public bool AllCommitted => Results.All(r => r.Committed);
        public IEnumerable<SyncApplyResult> Rejections => Results.Where(r => r.Outcome == SyncApplyOutcome.Rejected);
        public IEnumerable<SyncApplyResult> Failures => Results.Where(r => r.Outcome == SyncApplyOutcome.Failed);
    }

    /// <summary>
    /// DoR §12.5 ordering. Phase 1 applies parent tombstones top-down so that §12.2's parent check in
    /// phase 2 evaluates against already-committed state; phase 2 applies everything else top-down.
    /// A <c>TaskNote</c> change is planned as a whole uniqueness scope (D5), never as a bare row.
    /// </summary>
    internal static class SyncApplyPlanner
    {
        private static readonly string[] TombstoneFirstOrder =
        {
            SyncEntityTypes.HocKy, SyncEntityTypes.MonHoc, SyncEntityTypes.StudyTask,
        };

        private static readonly string[] ApplyOrder =
        {
            SyncEntityTypes.HocKy, SyncEntityTypes.MonHoc, SyncEntityTypes.StudyTask,
            SyncEntityTypes.StudyLog, SyncEntityTypes.TaskNote, SyncEntityTypes.TaskReferenceLink,
        };

        public static IReadOnlyList<SyncOperation> Plan(IncomingChangeSet changes)
        {
            if (changes is null) throw new ArgumentNullException(nameof(changes));

            var ops = new List<SyncOperation>();

            // Phase 1 — parent tombstones, top-down. Only the three types that own children need to
            // land before their children are evaluated; a child tombstone has nothing depending on it
            // and rides along in phase 2 with the rest of its type.
            foreach (var type in TombstoneFirstOrder)
            {
                foreach (var c in Of(changes, type))
                    if (c.Snapshot.Provenance.IsDeleted)
                        ops.Add(Entity(c, 1));
            }

            // Phase 2 — everything not already planned, top-down.
            foreach (var type in ApplyOrder)
            {
                if (type == SyncEntityTypes.TaskNote)
                {
                    // One operation per uniqueness scope (D5/D6-D). The unfiltered UNIQUE index on
                    // TaskNotes(MaTask) means a peer can hold at most one row per MaTask, so grouping
                    // by scope value yields exactly one remote candidate per operation; a change set
                    // carrying two rows for one scope is malformed and is rejected here rather than
                    // silently half-applied.
                    foreach (var group in Of(changes, type).GroupBy(c => ScopeValueOf(c)))
                    {
                        var members = group.ToList();
                        if (members.Count > 1)
                        {
                            throw new MergeContractViolationException(
                                $"Change set offers {members.Count} TaskNote rows for MaTask {group.Key:D}; " +
                                "UNIQUE(MaTask) makes that impossible on the sending peer.");
                        }

                        var c = members[0];
                        ops.Add(new SyncOperation(SyncOperationKind.TaskNoteScope, type, c.EntityId,
                                                  group.Key, c.Snapshot, 2));
                    }
                    continue;
                }

                foreach (var c in Of(changes, type))
                    if (!(c.Snapshot.Provenance.IsDeleted && TombstoneFirstOrder.Contains(type)))
                        ops.Add(Entity(c, 2));
            }

            return ops;
        }

        private static IEnumerable<IncomingChange> Of(IncomingChangeSet set, string entityType) =>
            set.Changes.Where(c => StringComparer.Ordinal.Equals(c.EntityType, entityType));

        private static SyncOperation Entity(IncomingChange c, int phase) =>
            new(SyncOperationKind.Entity, c.EntityType, c.EntityId, Guid.Empty, c.Snapshot, phase);

        private static Guid ScopeValueOf(IncomingChange c) =>
            c.Snapshot.Fields.TryGetValue("MaTask", out var v) && v is GuidValue g
                ? g.Value
                : throw new MergeContractViolationException("TaskNote snapshot has no MaTask scope value.");
    }
}
