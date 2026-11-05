using System;
using System.Linq;

namespace SmartStudyPlanner.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (plan §15.1). Carries the <see cref="FenceDecision"/> that rejected a
    /// mutation. Defined here to the extent Slice 2's contract requires; throwing it at the gate
    /// (<c>RouteKnown ∧ FencePassed</c> failing) and handling it (rollback, graph restoration,
    /// surfacing per OD-7) belong to the executor built in Slice 4 -- this slice is read-only and
    /// never throws it itself (mission §23: "reject != retry"; no automatic retry is in scope here).
    /// </summary>
    public sealed class MutationRejectedException : Exception
    {
        public FenceDecision Decision { get; }

        public MutationRejectedException(FenceDecision decision)
            : base(BuildMessage(decision))
        {
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
        }

        private static string BuildMessage(FenceDecision decision)
        {
            if (decision is null) throw new ArgumentNullException(nameof(decision));

            if (!decision.RouteKnown)
                return "Mutation rejected: RouteKnown is false (an intent names an unregistered entity type or field).";

            var blocking = decision.Results
                .Where(r => r.Outcome is FenceOutcome.Blocked or FenceOutcome.Unsupported)
                .Select(r => $"{r.RuleId} ({r.Outcome}, conflict {r.ConflictId})");

            return "Mutation rejected by the structural conflict fence: " + string.Join("; ", blocking);
        }
    }
}
