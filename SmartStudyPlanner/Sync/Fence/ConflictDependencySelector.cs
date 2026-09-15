using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Fence
{
    /// <summary>
    /// Epic 2 / T2.4 Slice 2 (fence spec §3.3, §7.1 step 2; plan §8). Selects every unresolved
    /// <see cref="SyncConflictRecordRow"/> whose <see cref="SyncConflictRecordRow.ScopeKey"/> or
    /// identity may be reached by an <see cref="ImpactSet"/>. Read-only (<c>AsNoTracking</c>).
    /// <para>
    /// <b>Selection is not violation (INV-3).</b> This type says which records a policy MUST inspect.
    /// It never decides whether the impact actually crosses the record's protected contract -- that is
    /// <see cref="IConflictFencePolicy.Evaluate"/>'s job. A selected record can legitimately come back
    /// <see cref="FenceOutcome.Passed"/> or <see cref="FenceOutcome.NotApplicable"/>.
    /// </para>
    /// <para>
    /// Two predicates, deliberately overlapping: the ScopeKey predicate is the indexed, precise match
    /// for the two shapes this system already knows how to key (structural entity scope, constraint
    /// scope). The identity predicate is a fail-closed net over <see cref="SyncConflictRecordRow.EntityId"/>
    /// / <see cref="SyncConflictRecordRow.ConstraintValue"/> so a record whose ScopeKey format this code
    /// does not recognise (a future field, a corrupted row) is still selected -- it then classifies as
    /// <see cref="ConflictShape.Unsupported"/> and fails closed (INV-5) instead of being silently missed.
    /// </para>
    /// </summary>
    internal static class ConflictDependencySelector
    {
        public static async Task<IReadOnlyList<SyncConflictRecordRow>> SelectAsync(
            AppDbContext db, ImpactSet impact, CancellationToken ct = default)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));
            if (impact is null) throw new ArgumentNullException(nameof(impact));

            var scopeKeys = new HashSet<string>(StringComparer.Ordinal);
            var entityIds = new HashSet<Guid>();
            var constraintValues = new HashSet<string>(StringComparer.Ordinal);

            void AddStructuralSubject(string entityType, Guid entityId)
            {
                entityIds.Add(entityId);
                var field = StructuralDependencyRegistry.StructuralFieldOf(entityType);
                if (field is not null)
                    scopeKeys.Add(ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, entityType, entityId, field, null));

                if (entityType == SyncEntityTypes.StudyTask)
                    constraintValues.Add(entityId.ToString("D")); // a task named anywhere in impact is a candidate TaskNote-scope owner
            }

            foreach (var row in impact.Rows) AddStructuralSubject(row.EntityType, row.EntityId);
            foreach (var lc in impact.Lifecycle) AddStructuralSubject(lc.EntityType, lc.EntityId);
            foreach (var edge in impact.Edges) AddStructuralSubject(edge.ChildType, edge.ChildId);

            foreach (var scope in impact.Scopes)
            {
                scopeKeys.Add(scope.ScopeKey);
                constraintValues.Add(scope.ScopeValue.ToString("D"));
            }

            var scopeKeyArr = scopeKeys.ToArray();
            var entityIdArr = entityIds.ToArray();
            var constraintValueArr = constraintValues.ToArray();

            var candidates = await db.SyncConflictRecords.AsNoTracking()
                .Where(r => r.Status == ConflictRecordStatus.Unresolved &&
                            (scopeKeyArr.Contains(r.ScopeKey)
                             || (r.EntityId != null && entityIdArr.Contains(r.EntityId.Value))
                             || (r.ConstraintValue != null && constraintValueArr.Contains(r.ConstraintValue))))
                .ToListAsync(ct);

            return candidates
                .GroupBy(r => r.ConflictId)
                .Select(g => g.First())
                .OrderBy(r => r.ConflictId)
                .ToArray();
        }
    }
}
