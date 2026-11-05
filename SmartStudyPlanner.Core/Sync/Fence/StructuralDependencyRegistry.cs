using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStudyPlanner.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §5.1, plan §7.2). The explicit, hand-maintained source of
    /// known structural relationships -- the cascade edges exist today only as code, spread across
    /// three places (<c>AppDbContext.OnModelCreating</c>, <c>TaskCascadeHelper</c>,
    /// <c>SyncApplySession.CascadeTombstoneAsync</c>). This registry reuses <see cref="Merge.FieldClass"/>
    /// from <see cref="Merge.MergeSurfaceRegistry"/> rather than inventing a second field taxonomy.
    /// <para>
    /// A malformed/incomplete configuration fails closed at load time (mission §11): the static
    /// constructor validates every edge names a known <see cref="SyncEntityTypes"/> pair before the
    /// registry is usable, so an editing mistake here cannot silently produce an allow result.
    /// </para>
    /// </summary>
    public static class StructuralDependencyRegistry
    {
        /// <summary>
        /// One structural or cascade-only parent/child edge.
        /// <para>
        /// The concerns below are DELIBERATELY independent (owner ruling 2026-09-17, review finding
        /// H-1/A-2). None is derived from another, and none may be computed from another:
        /// </para>
        /// <list type="bullet">
        /// <item><b>Merge classification</b> is not here at all -- it lives in
        ///       <see cref="Merge.MergeSurfaceRegistry"/> and answers "how does 3-way merge treat this
        ///       field" (CopyOnCreate, ChildField, ConstraintScope, ...).</item>
        /// <item><b>Structural dependency</b> is membership in <see cref="All"/> -- it answers "is this
        ///       parent/child relation part of the known topology". Every edge below is one.</item>
        /// <item><b><see cref="FenceRoutable"/></b> answers "may a mutation through this relation enter
        ///       the Slice-2 fence". Structural membership does NOT imply it, and merge classification
        ///       never implies it.</item>
        /// </list>
        /// <see cref="CascadesOnTombstone"/> is a fourth, separate question ("does tombstoning the
        /// parent tombstone this child"). It happens to agree with <see cref="FenceRoutable"/> on
        /// today's five edges, which is a coincidence of the current domain rather than a rule -- the
        /// two are stored as independent columns and neither accessor reads the other's flag.
        /// </summary>
        public sealed record StructuralEdge(
            string ParentType, string ChildType, string ChildField, bool CascadesOnTombstone, bool FenceRoutable);

        // Known dependency graph (fence spec §5.1):
        //   HocKy -> MonHoc -> StudyTask -> { TaskNote, TaskReferenceLink }
        // StudyLog is registered for completeness (plan §7.2 table) so the topology is complete, but it
        // neither cascades nor routes:
        //   - no cascade: it has no FK at all (CopyOnCreate, MergeSurfaceRegistry.cs) and Epic-1's
        //     cascade never reached it (FACT,
        //     SyncApplyParentHandlingTests.N_StudyLogUnderTombstonedTask_AppliesLiveWithNoEvidence);
        //   - no fence route: owner ruling 2026-09-17 (H-1/A-2 CASE B) keeps StudyLog writes outside the
        //     Slice-2 fence-routing surface, preserving the plan's existing decision.
        private static readonly IReadOnlyList<StructuralEdge> Edges = new[]
        {
            new StructuralEdge(SyncEntityTypes.HocKy, SyncEntityTypes.MonHoc, "MaHocKy", CascadesOnTombstone: true, FenceRoutable: true),
            new StructuralEdge(SyncEntityTypes.MonHoc, SyncEntityTypes.StudyTask, "MaMonHoc", CascadesOnTombstone: true, FenceRoutable: true),
            new StructuralEdge(SyncEntityTypes.StudyTask, SyncEntityTypes.TaskNote, "MaTask", CascadesOnTombstone: true, FenceRoutable: true),
            new StructuralEdge(SyncEntityTypes.StudyTask, SyncEntityTypes.TaskReferenceLink, "MaTask", CascadesOnTombstone: true, FenceRoutable: true),
            new StructuralEdge(SyncEntityTypes.StudyTask, SyncEntityTypes.StudyLog, "MaTask", CascadesOnTombstone: false, FenceRoutable: false),
        };

        private static readonly IReadOnlySet<string> KnownEntityTypes;

        static StructuralDependencyRegistry()
        {
            var known = new HashSet<string>(StringComparer.Ordinal) { SyncEntityTypes.HocKy };
            foreach (var edge in Edges)
            {
                if (string.IsNullOrEmpty(edge.ParentType) || string.IsNullOrEmpty(edge.ChildType) || string.IsNullOrEmpty(edge.ChildField))
                {
                    throw new InvalidOperationException(
                        $"StructuralDependencyRegistry has an incomplete edge {edge.ParentType}->{edge.ChildType}.");
                }

                known.Add(edge.ParentType);
                known.Add(edge.ChildType);
            }

            var expected = new[]
            {
                SyncEntityTypes.HocKy, SyncEntityTypes.MonHoc, SyncEntityTypes.StudyTask,
                SyncEntityTypes.StudyLog, SyncEntityTypes.TaskNote, SyncEntityTypes.TaskReferenceLink,
            };
            foreach (var type in expected)
            {
                if (!known.Contains(type))
                {
                    throw new InvalidOperationException(
                        $"StructuralDependencyRegistry is missing every edge that names '{type}'.");
                }
            }

            KnownEntityTypes = known;
        }

        public static IReadOnlyList<StructuralEdge> All => Edges;

        /// <summary>True when <paramref name="entityType"/> is a registered structural parent or child
        /// (mission §6 RouteKnown). Fails closed for anything not in the registry.</summary>
        public static bool IsKnownEntityType(string entityType) =>
            !string.IsNullOrEmpty(entityType) && KnownEntityTypes.Contains(entityType);

        /// <summary>
        /// The D4 structural-parent field for a type that carries one. Null for every other type --
        /// HocKy has no structural parent of its own, and TaskNote/TaskReferenceLink/StudyLog are
        /// cascade-only children, never a <see cref="ConflictShape.ConcurrentReparent"/> or
        /// <see cref="ConflictShape.ParentTombstoned"/> subject (<see cref="ConflictShapeClassifier"/>
        /// only ever classifies MonHoc/StudyTask into those shapes).
        /// </summary>
        public static string? StructuralFieldOf(string entityType) => entityType switch
        {
            SyncEntityTypes.MonHoc => "MaHocKy",
            SyncEntityTypes.StudyTask => "MaMonHoc",
            _ => null,
        };

        /// <summary>Every edge that cascade-tombstones live children when <paramref name="parentType"/>
        /// is tombstoned (fence spec §5.1-§5.3: the actual cascade, not an invented closure).</summary>
        public static IReadOnlyList<StructuralEdge> CascadeChildrenOf(string parentType) =>
            Edges.Where(e => e.ParentType == parentType && e.CascadesOnTombstone).ToArray();

        // ------------------------------------------------------------------ fence-route authority
        //
        // Owner ruling 2026-09-17 (review finding H-1/A-2 clarification). THREE separate concerns; no
        // one of them is a fallback for any other:
        //
        //   1. MergeSurfaceRegistry          -> merge-field semantics (CopyOnCreate, ChildField,
        //                                       ConstraintScope, Structural, ...). Never consulted here.
        //   2. StructuralDependencyRegistry  -> structural dependency TOPOLOGY. Membership in `Edges`
        //      (IsKnownStructuralDependency)     means the relation is structurally KNOWN. It does NOT
        //                                       mean it is fence-routable.
        //   3. FenceRoutable on an edge      -> fence-route ELIGIBILITY, declared explicitly per edge.
        //      (IsRegisteredStructuralRoute)     This, and only this, answers RouteKnown.
        //
        // Therefore:  structural dependency != automatically fence-routable
        //             merge-known           != automatically fence-routable
        //
        // The load-bearing cases (owner ruling CASE A / CASE B):
        //   - TaskReferenceLink.MaTask is CopyOnCreate for merge (D9-T3), IS a known structural
        //     dependency, AND is explicitly fence-routable => RouteKnown. Its merge classification must
        //     NOT be changed to make routing work.
        //   - StudyLog.MaTask is CopyOnCreate for merge and IS a known structural dependency, but is
        //     explicitly NOT fence-routable => RouteUnknown.
        //
        // The correct way to change any one answer is to edit that one authority, never another.

        /// <summary>
        /// True when the (<paramref name="childType"/>, <paramref name="childField"/>) tuple is part of
        /// the known structural dependency TOPOLOGY -- concern 2 above. This is deliberately NOT the
        /// routability answer: a relation can be structurally known and still not fence-routable
        /// (<c>StudyLog.MaTask</c>). Use <see cref="IsRegisteredStructuralRoute"/> for routing.
        /// </summary>
        public static bool IsKnownStructuralDependency(string childType, string childField) =>
            !string.IsNullOrEmpty(childType) && !string.IsNullOrEmpty(childField) &&
            Edges.Any(e => e.ChildType == childType && e.ChildField == childField);

        /// <summary>
        /// True when <paramref name="childField"/> on <paramref name="childType"/> is EXPLICITLY
        /// registered as a fence-routable mutation relation -- concern 3 above, the sole authority for
        /// <c>RouteKnown</c>. Neither <see cref="Merge.MergeSurfaceRegistry"/> membership nor bare
        /// structural-topology membership is a fallback: the edge must carry
        /// <see cref="StructuralEdge.FenceRoutable"/>. Fails closed for anything else.
        /// </summary>
        public static bool IsRegisteredStructuralRoute(string childType, string childField) =>
            !string.IsNullOrEmpty(childType) && !string.IsNullOrEmpty(childField) &&
            Edges.Any(e => e.FenceRoutable && e.ChildType == childType && e.ChildField == childField);

        /// <summary>
        /// True when <paramref name="entityType"/> takes part in at least one EXPLICITLY fence-routable
        /// edge, as either endpoint -- the type-level half of <c>RouteKnown</c>. Distinct from
        /// <see cref="IsKnownEntityType"/>, which answers the topology question and stays true for
        /// <c>StudyLog</c>: StudyLog appears only on a non-routable edge, so every StudyLog intent is
        /// route-unknown even when it names no relation at all (owner ruling CASE B).
        /// </summary>
        public static bool IsFenceRoutableEntityType(string entityType) =>
            !string.IsNullOrEmpty(entityType) &&
            Edges.Any(e => e.FenceRoutable && (e.ParentType == entityType || e.ChildType == entityType));

        /// <summary>
        /// The registered parent type at the other end of the <paramref name="childType"/>/
        /// <paramref name="childField"/> edge, or null when the tuple is not a registered route. The
        /// (ChildType, ChildField) pair is unique across the registry, so the answer is unambiguous.
        /// </summary>
        public static string? ParentTypeOf(string childType, string childField) =>
            Edges.FirstOrDefault(e => e.ChildType == childType && e.ChildField == childField)?.ParentType;
    }
}
