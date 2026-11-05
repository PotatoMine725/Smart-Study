using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Fence
{
    // Epic 2 / T2.4 Slice 1 (fence spec §4, §9). Shape-specific protected subject/reference frame,
    // derived purely from an unresolved ConflictRecord's columns and immutable evidence.

    public enum ConflictShape
    {
        Unsupported = 0,
        ConcurrentReparent,
        ParentTombstoned,
        AbsentLocalParentTombstoned,
        ConstraintOccupancy
    }

    public enum ConstraintForm { BasePresent, BaseNull }

    public abstract record ProtectedSubject;

    /// <summary>S1-CR, S1-PT: the conflicted entity itself.</summary>
    public sealed record EntitySubject(string EntityType, Guid EntityId) : ProtectedSubject;

    /// <summary>AL-PT: a logical identity that must remain absent from live state.</summary>
    public sealed record AbsentIdentitySubject(string EntityType, Guid EntityId) : ProtectedSubject;

    /// <summary>Constraint: a concrete constraint scope (e.g. TaskNote|MaTask=t).</summary>
    public sealed record ConstraintScopeSubject(string ScopeKey, Guid ScopeValue) : ProtectedSubject;

    /// <summary>
    /// The structural reference frame. HeldParentId = Base parent currently held live (S1), or the
    /// tombstoned Remote parent the absent candidate points at (AL-PT).
    /// </summary>
    public sealed record ProtectedEdge(string ChildType, Guid ChildId, string Field, Guid? HeldParentId);

    public sealed record ProtectedContract(
        Guid ConflictId,
        string ScopeKey,
        ConflictShape Shape,
        ProtectedSubject Subject,
        ProtectedEdge? Edge,
        IReadOnlyList<string> Predicates,
        ConstraintForm? Form,
        IReadOnlyList<Guid> CandidateIds);
}
