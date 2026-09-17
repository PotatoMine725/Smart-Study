using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Fence
{
    // Epic 2 / T2.4 Slice 1 (fence spec §6). Audit/diagnostics only -- never a policy input (INV-6).
    // IConflictFencePolicy.Evaluate takes an ImpactSet, never a MutationRequest or MutationOrigin.
    public enum MutationOrigin { LocalApplication = 0, SyncApply = 1 }

    public enum MutationOperation { Create, UpdateFields, Reparent, Tombstone }

    /// <summary>One structural (D4) or ConstraintScope (D5) relation, before AND after (spec §3.1).</summary>
    public sealed record RelationChange(string Field, Guid? Before, Guid? After);

    /// <summary>
    /// A declarative, side-effect-free mutation request (spec §3.1). Not an EF entity mutation.
    /// </summary>
    public sealed record MutationIntent(
        MutationOperation Operation,
        string EntityType,
        Guid EntityId,
        IReadOnlyList<RelationChange> Relations,
        IReadOnlyList<string> ChangedFields);

    /// <summary>One logical operation (D8-G): all intents persist atomically or none do.</summary>
    public sealed record MutationRequest(MutationOrigin Origin, IReadOnlyList<MutationIntent> Intents);
}
