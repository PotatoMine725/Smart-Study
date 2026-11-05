using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Merge
{
    /// <summary>Per-field merge outcome (D9 §12.1 five cases + structural).</summary>
    public enum FieldDecision { Base, Local, Remote, Common, LwwLocal, LwwRemote, StructuralConflict }

    public sealed record FieldOutcome(string Field, FieldDecision Decision, FieldValue? Result);

    public enum ConflictKind { FieldConflict, StructuralConflict, ConstraintConflict, TombstoneConflict }

    /// <summary>
    /// <see cref="ParentTombstoned"/> is produced by the APPLY layer (D4 / D9-T4), never by the
    /// pure core -- the core does not examine parent existence.
    /// </summary>
    public enum StructuralReason { ConcurrentReparent, ParentTombstoned }

    public enum ResolutionKind { KeepLocal, KeepRemote, KeepBase, ManualMerge, AutoLww, AutoTombstone }

    /// <summary>e.g. ("TaskNote", "MaTask", "&lt;guid D-format&gt;").</summary>
    public sealed record ConstraintScope(string EntityType, string Key, string Value);

    /// <summary>
    /// Immutable conflict evidence (D6). No generic winner/loser field: the record carries
    /// Base/Local/Remote and, for the auto kinds only, the deterministic outcome (D9-T5).
    /// Persistence of this evidence is T2.4, not T2.3.
    /// </summary>
    public sealed record ConflictCandidate(
        ConflictKind Kind,
        string EntityType,
        Guid? EntityId,                  // Field / Structural / Tombstone
        string? FieldName,               // Field / Structural
        ConstraintScope? Scope,          // Constraint
        StructuralReason? Reason,
        EntitySnapshot? Base,            // may be null (D6-B)
        Guid? BaseEntityId,
        // Local may be absent, and ONLY for a StructuralConflict whose logical child scope holds no
        // local row at all -- a remote pure create under a tombstoned structural parent (D4/D9-T4
        // amendment, 2026-09-10). The two move together: snapshot and id are both present or both
        // absent, enforced by ConflictKeys.ConflictKey. Absence is never a placeholder: the remote
        // row must NOT be copied into this slot.
        EntitySnapshot? Local, Guid? LocalEntityId,
        EntitySnapshot Remote, Guid RemoteEntityId,
        ResolutionKind? AutoResolution,  // AutoLww / AutoTombstone => Resolved directly (D9-T5); null => Unresolved
        EntitySnapshot? AutoResult);     // the deterministic outcome for the auto kinds

    /// <summary>Result of merging ONE entity (same EntityId on both sides).</summary>
    public sealed record MergedEntity(
        EntityRef Ref,
        EntitySnapshot? Result,          // null only when D9-T1 says the scope has no live row (null Base)
        Provenance ResultProvenance,     // DoR §6.5
        IReadOnlyList<FieldOutcome> Outcomes,
        IReadOnlyList<ConflictCandidate> Candidates,
        bool IsNoOp);                    // Result equals Local on every field + provenance => apply must not write

    /// <summary>
    /// Input for one constraint scope (D5), built by the apply-layer loader and consumed by the
    /// pure core. Local is the LIVE local row in scope; a tombstoned occupant is signalled by a
    /// snapshot whose <see cref="Provenance.IsDeleted"/> is true (DoR §7.2 last row).
    /// </summary>
    public sealed record ConstraintScopeInput(
        ConstraintScope Scope,
        (Guid Id, EntitySnapshot Snap)? Base,
        (Guid Id, EntitySnapshot Snap)? Local,
        (Guid Id, EntitySnapshot Snap)? Remote);

    /// <summary>Outcome of constraint-scope detection (DoR §7.2 table).</summary>
    public enum ConstraintOutcome
    {
        NoAction,                  // neither side has a row in scope
        CreateRemote,              // remote-only row: ordinary create (parent check + scope-lock are apply-layer)
        LocalOnly,                 // local-only row: nothing to do
        OrdinaryMerge,             // same Id on both sides => three-way merge, not a constraint conflict
        ConstraintConflict,        // different Ids compete for the scope => D5, no auto winner
        ScopeOccupiedByTombstone   // fail closed; unreachable in v1 by construction (DoR §7.2)
    }

    public sealed record ConstraintDetection(
        ConstraintOutcome Outcome,
        ConflictCandidate? Candidate,
        Guid? LocalEntityId,
        Guid? RemoteEntityId);
}
