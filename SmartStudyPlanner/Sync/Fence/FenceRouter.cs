using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §7, §3.4; plan §11.3). The aggregate outcome of one
    /// <see cref="FenceRouter.EvaluateAsync"/> call. <see cref="FencePassed"/> is necessary, never
    /// sufficient, authorization to persist (spec §3.4, INV-7) -- callers still owe business
    /// validation, persistence preconditions, and transaction/concurrency checks.
    /// </summary>
    public sealed record FenceDecision(IReadOnlyList<PolicyResult> Results, bool RouteKnown)
    {
        /// <summary>
        /// <c>RouteKnown ∧ ∀r ∈ Results: r.Outcome ∈ {NotApplicable, Passed}</c>. An empty
        /// <see cref="Results"/> list with <see cref="RouteKnown"/> true (an empty request, or a
        /// request whose impact reaches no unresolved record) passes vacuously (N-9).
        /// </summary>
        public bool FencePassed => RouteKnown && Results.All(r => r.Outcome is FenceOutcome.NotApplicable or FenceOutcome.Passed);
    }

    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §7.1, §8; plan §11.3). Read-only orchestration:
    /// impact resolution → conflict-dependency selection → shape classification → policy routing →
    /// deterministic aggregation. Never writes a domain row, a <c>ConflictRecord</c>, or calls
    /// <c>SaveChanges</c> (INV-8) -- see <c>FenceReadOnlyTests</c>/<c>FenceSourceFenceTests</c>.
    /// <para>
    /// <b>Every applicable policy is evaluated (INV-4).</b> There is no early return on the first
    /// <see cref="FenceOutcome.Blocked"/> result and no priority among <see cref="ConflictShape"/>
    /// values -- see <see cref="Aggregate"/>'s ordering, which is for deterministic discovery/reporting
    /// only (spec §7.1 step 8).
    /// </para>
    /// </summary>
    internal static class FenceRouter
    {
        public static async Task<FenceDecision> EvaluateAsync(
            AppDbContext db, MutationRequest request, CancellationToken ct = default)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));
            if (request is null) throw new ArgumentNullException(nameof(request));

            // RouteKnown gates BEFORE impact resolution: the resolver cannot compute impact for an
            // intent naming an unregistered EntityType/field, so this is a precondition check, not a
            // "stop after the first result" shortcut (mission §6.1 forbids only the latter).
            if (!RouteKnown(request))
                return new FenceDecision(Array.Empty<PolicyResult>(), RouteKnown: false);

            var impact = await ImpactResolver.ResolveAsync(db, request, ct);
            var selected = await ConflictDependencySelector.SelectAsync(db, impact, ct);

            var results = new List<PolicyResult>(selected.Count);
            foreach (var record in selected)
            {
                results.Add(EvaluateOne(record, impact));
            }

            return new FenceDecision(Aggregate(results), RouteKnown: true);
        }

        private static PolicyResult EvaluateOne(SyncConflictRecordRow record, ImpactSet impact)
        {
            var shape = ConflictShapeClassifier.Classify(record);

            if (shape == ConflictShape.Unsupported)
            {
                return new PolicyResult(record.ConflictId, record.ScopeKey, ConflictShape.Unsupported,
                    new EntitySubject(record.EntityType, record.EntityId ?? Guid.Empty),
                    FenceOutcome.Unsupported, RoutingStage.DirectSubject,
                    "Unsupported.UnclassifiableRecord",
                    $"record {record.ConflictId} (Kind={record.Kind}, EntityType={record.EntityType}, " +
                    $"StructuralReason={record.StructuralReason?.ToString() ?? "null"}) has no registered shape");
            }

            var policy = FencePolicyRegistry.TryGet(shape)
                ?? throw new InvalidOperationException($"No policy registered for classified shape {shape}."); // unreachable: FencePolicyRegistry asserts completeness at load time (Slice 1)

            ProtectedContract contract;
            try
            {
                contract = policy.Derive(record);
            }
            catch (Exception ex) when (ex is SnapshotContractException or JsonException)
            {
                // N-3: unreadable/malformed evidence fails closed to Unsupported. It must never
                // surface as a thrown exception past the router, and it must never be silently read
                // as "no violation" (spec §8.2: an evidence-read failure is not an authorization).
                return new PolicyResult(record.ConflictId, record.ScopeKey, shape,
                    new EntitySubject(record.EntityType, record.EntityId ?? Guid.Empty),
                    FenceOutcome.Unsupported, RoutingStage.DirectSubject,
                    $"{EvidencePrefix(shape)}.EvidenceUnreadable",
                    $"record {record.ConflictId}: {ex.GetType().Name}: {ex.Message}");
            }

            return policy.Evaluate(contract, impact);
        }

        private static IReadOnlyList<PolicyResult> Aggregate(List<PolicyResult> results) =>
            results
                .OrderBy(r => r.Stage)
                .ThenBy(r => r.ScopeKey, StringComparer.Ordinal)
                .ThenBy(r => r.ConflictId)
                .ToArray();

        private static string EvidencePrefix(ConflictShape shape) => shape switch
        {
            ConflictShape.ConcurrentReparent => "S1CR",
            ConflictShape.ParentTombstoned => "S1PT",
            ConflictShape.AbsentLocalParentTombstoned => "ALPT",
            ConflictShape.ConstraintOccupancy => "CONS",
            _ => "Unsupported",
        };

        /// <summary>
        /// Spec §6 / plan §6: every intent's <c>EntityType</c> must be a known structural
        /// parent/child, and every <see cref="RelationChange.Field"/> it names must be a registered
        /// structural child edge on that type. Vacuously true for an empty request (N-9). Otherwise
        /// <c>false</c> fails closed (N-2/X-7) before any DB read.
        /// <para>
        /// <b>H-1/A-2, owner ruling 2026-09-17.</b> Routability is EXPLICIT, and is asked of
        /// <see cref="StructuralDependencyRegistry.IsFenceRoutableEntityType"/> /
        /// <see cref="StructuralDependencyRegistry.IsRegisteredStructuralRoute"/> ONLY. Three concerns
        /// stay separate here, and neither of the other two is a fallback for this one:
        /// <list type="bullet">
        /// <item><see cref="MergeSurfaceRegistry"/> is the authority for merge-field semantics and is
        ///       deliberately not consulted. Consulting it used to reject
        ///       <c>Create(TaskReferenceLink, MaTask: null -&gt; task)</c> — an explicitly fence-routable
        ///       relation — merely because that field is <c>CopyOnCreate</c> for merge purposes (CASE A).
        ///       Changing the field's merge classification to satisfy the router is forbidden.</item>
        /// <item>Bare structural-topology membership is likewise not consulted:
        ///       <c>StudyLog.MaTask</c> is a known structural dependency yet is deliberately outside the
        ///       Slice-2 fence-routing surface, so it is route-UNKNOWN (CASE B).</item>
        /// </list>
        /// <para>
        /// The type gate is checked even for an intent that names no relation, so a bare
        /// <c>Tombstone(StudyLog, ...)</c> is route-unknown too, not merely a StudyLog FK write.
        /// </para>
        /// </summary>
        private static bool RouteKnown(MutationRequest request)
        {
            foreach (var intent in request.Intents)
            {
                if (!StructuralDependencyRegistry.IsFenceRoutableEntityType(intent.EntityType)) return false;

                foreach (var relation in intent.Relations)
                {
                    if (!StructuralDependencyRegistry.IsRegisteredStructuralRoute(intent.EntityType, relation.Field))
                        return false;
                }
            }

            return true;
        }
    }
}
