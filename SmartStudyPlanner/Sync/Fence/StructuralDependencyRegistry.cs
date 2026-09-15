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
        /// <summary>One structural or cascade-only parent/child edge.</summary>
        public sealed record StructuralEdge(
            string ParentType, string ChildType, string ChildField, bool CascadesOnTombstone);

        // Known dependency graph (fence spec §5.1):
        //   HocKy -> MonHoc -> StudyTask -> { TaskNote, TaskReferenceLink }
        // StudyLog is registered for completeness (plan §7.2 table) but does NOT cascade: it has no FK
        // at all (CopyOnCreate, MergeSurfaceRegistry.cs) and Epic-1's cascade never reached it (FACT,
        // SyncApplyParentHandlingTests.N_StudyLogUnderTombstonedTask_AppliesLiveWithNoEvidence).
        private static readonly IReadOnlyList<StructuralEdge> Edges = new[]
        {
            new StructuralEdge(SyncEntityTypes.HocKy, SyncEntityTypes.MonHoc, "MaHocKy", CascadesOnTombstone: true),
            new StructuralEdge(SyncEntityTypes.MonHoc, SyncEntityTypes.StudyTask, "MaMonHoc", CascadesOnTombstone: true),
            new StructuralEdge(SyncEntityTypes.StudyTask, SyncEntityTypes.TaskNote, "MaTask", CascadesOnTombstone: true),
            new StructuralEdge(SyncEntityTypes.StudyTask, SyncEntityTypes.TaskReferenceLink, "MaTask", CascadesOnTombstone: true),
            new StructuralEdge(SyncEntityTypes.StudyTask, SyncEntityTypes.StudyLog, "MaTask", CascadesOnTombstone: false),
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
    }
}
