using System;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-6, DoR §13.1) — a resolution request for one <c>ConflictId</c>. Only
    /// <see cref="ResolutionKind.KeepLocal"/>, <see cref="ResolutionKind.KeepRemote"/>,
    /// <see cref="ResolutionKind.KeepBase"/> and <see cref="ResolutionKind.ManualMerge"/> are valid
    /// here — the auto kinds are staged directly as <c>Resolved</c> (D9-T5) and can never be
    /// requested. <see cref="ManualResult"/>/<see cref="ManualResultEntityId"/> are required together
    /// for <see cref="ResolutionKind.ManualMerge"/> and forbidden for every other kind.
    /// </summary>
    public sealed record ResolutionRequest(ResolutionKind Kind, EntitySnapshot? ManualResult, Guid? ManualResultEntityId);

    public enum ResolutionOutcomeKind { Applied, NoOpReplay, Rejected, Failed }

    /// <summary>Engineering names (DoR §13, E-10) — none of these change semantic meaning.</summary>
    public enum ResolutionRejectReason
    {
        None = 0,
        InvalidRequest,
        UnknownConflict,
        NotResolvable,
        MismatchedReplay,
        LiveStateDrift,
        KindNotApplicable,
        ManualTombstoneNotSupported,
        ResultParentTombstoned,
        ResultParentMissing,
        ResultIdInUse,
        ResultOutOfScope,
        ScopeOccupied,
        ScopeHasUnresolvedConflict,
        ContractViolation,
    }

    /// <summary>Outcome of one <see cref="ConflictResolver.ResolveAsync"/> call (DoR §13.1).</summary>
    public sealed record ResolutionOutcome(
        ResolutionOutcomeKind Kind,
        ResolutionRejectReason Reason = ResolutionRejectReason.None,
        Exception? Error = null)
    {
        public static ResolutionOutcome Applied() => new(ResolutionOutcomeKind.Applied);
        public static ResolutionOutcome NoOpReplay() => new(ResolutionOutcomeKind.NoOpReplay);
        public static ResolutionOutcome Rejected(ResolutionRejectReason reason) => new(ResolutionOutcomeKind.Rejected, reason);
        public static ResolutionOutcome Failed(Exception ex) => new(ResolutionOutcomeKind.Failed, ResolutionRejectReason.None, ex);
    }
}
