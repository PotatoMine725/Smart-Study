using System;

namespace SmartStudyPlanner.Sync.Fence
{
    // Epic 2 / T2.4 Slice 1 (fence spec §3.4, plan §10). FenceRouter/FenceDecision aggregation is
    // Slice 2 -- these are the per-policy result types only.

    public enum FenceOutcome { NotApplicable, Passed, Blocked, Unsupported }

    public enum RoutingStage { DirectSubject = 0, CascadeReached = 1, ConstraintScope = 2 }

    public sealed record PolicyResult(
        Guid ConflictId,
        string ScopeKey,
        ConflictShape Shape,
        ProtectedSubject Subject,
        FenceOutcome Outcome,
        RoutingStage Stage,
        string RuleId,
        string Evidence);
}
