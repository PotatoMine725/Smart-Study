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
    /// Epic 2 / T2.4 Slice 2 (fence spec §3.1, §5; plan §7.3-§7.4). Derives the explicit impact set
    /// <c>I(m) = Rows(m) ∪ Edges(m) ∪ ConstraintScopes(m) ∪ LifecycleEffects(m)</c> for a
    /// <see cref="MutationRequest"/>. Read-only: every query is <c>AsNoTracking</c>, and nothing here
    /// mutates the tracker or persistent state (INV-8).
    /// <para>
    /// <b>Actual cascade, not invented closure (INV-2).</b> Only a <see cref="MutationOperation.Tombstone"/>
    /// intent expands through <see cref="StructuralDependencyRegistry"/> cascade edges, and only through
    /// LIVE children -- a row already tombstoned before this request is a pre-existing, out-of-scope
    /// condition (plan §7.4/D-2), not part of this mutation's actual write set. A
    /// <see cref="MutationOperation.Reparent"/>/<see cref="MutationOperation.UpdateFields"/>/
    /// <see cref="MutationOperation.Create"/> intent never adds descendants.
    /// </para>
    /// </summary>
    internal static class ImpactResolver
    {
        public static async Task<ImpactSet> ResolveAsync(AppDbContext db, MutationRequest request, CancellationToken ct = default)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));
            if (request is null) throw new ArgumentNullException(nameof(request));

            var rows = new Dictionary<(string Type, Guid Id), ImpactRow>();
            var edges = new List<ImpactEdge>();
            var scopes = new List<ImpactScope>();
            var lifecycle = new List<ImpactLifecycle>();
            var visitedForCascade = new HashSet<(string Type, Guid Id)>();

            void AddRow(ImpactRow row)
            {
                var key = (row.EntityType, row.EntityId);
                if (rows.TryGetValue(key, out var existing) && Rank(existing.Effect) >= Rank(row.Effect)) return;
                rows[key] = row;
            }

            static int Rank(RowEffect effect) => effect == RowEffect.CascadeTombstoned ? 0 : 1;

            foreach (var intent in request.Intents)
            {
                switch (intent.Operation)
                {
                    case MutationOperation.Create:
                        AddRow(new ImpactRow(intent.EntityType, intent.EntityId, RowEffect.Created, WasLive: true, intent.EntityId));
                        lifecycle.Add(new ImpactLifecycle(intent.EntityType, intent.EntityId, LifecycleEffect.Create));
                        foreach (var rel in intent.Relations)
                        {
                            if (rel.After is not { } after) continue;
                            edges.Add(new ImpactEdge(intent.EntityType, intent.EntityId, rel.Field, after, EdgeChange.Added));
                            if (IsConstraintScopeField(intent.EntityType, rel.Field))
                            {
                                scopes.Add(new ImpactScope(
                                    ScopeKeyFor(intent.EntityType, rel.Field, after), after, ScopeChange.Acquired, intent.EntityId));
                            }
                        }
                        break;

                    case MutationOperation.UpdateFields:
                        AddRow(new ImpactRow(intent.EntityType, intent.EntityId, RowEffect.FieldsChanged, WasLive: true, intent.EntityId));
                        if (intent.EntityType == SyncEntityTypes.TaskNote && intent.ChangedFields.Count > 0)
                        {
                            var current = await ReadCurrentAsync(db, intent.EntityType, intent.EntityId, ct);
                            if (current is { ParentId: { } owningTask })
                            {
                                scopes.Add(new ImpactScope(
                                    ScopeKeyFor(SyncEntityTypes.TaskNote, "MaTask", owningTask), owningTask,
                                    ScopeChange.OccupantContentChanged, intent.EntityId));
                            }
                        }
                        break;

                    case MutationOperation.Reparent:
                        AddRow(new ImpactRow(intent.EntityType, intent.EntityId, RowEffect.Reparented, WasLive: true, intent.EntityId));
                        foreach (var rel in intent.Relations)
                        {
                            if (rel.Before is { } before)
                                edges.Add(new ImpactEdge(intent.EntityType, intent.EntityId, rel.Field, before, EdgeChange.Removed));
                            if (rel.After is { } after)
                                edges.Add(new ImpactEdge(intent.EntityType, intent.EntityId, rel.Field, after, EdgeChange.Added));

                            if (IsConstraintScopeField(intent.EntityType, rel.Field))
                            {
                                if (rel.Before is { } oldScope)
                                    scopes.Add(new ImpactScope(ScopeKeyFor(intent.EntityType, rel.Field, oldScope), oldScope, ScopeChange.Released, intent.EntityId));
                                if (rel.After is { } newScope)
                                    scopes.Add(new ImpactScope(ScopeKeyFor(intent.EntityType, rel.Field, newScope), newScope, ScopeChange.Acquired, intent.EntityId));
                            }
                        }
                        break;

                    case MutationOperation.Tombstone:
                        await ExpandTombstoneAsync(db, intent.EntityType, intent.EntityId, causedBy: intent.EntityId,
                            isDirect: true, AddRow, edges, scopes, lifecycle, visitedForCascade, ct);
                        break;

                    default:
                        throw new InvalidOperationException($"Unhandled MutationOperation {intent.Operation}.");
                }
            }

            return new ImpactSet(
                rows.Values.OrderBy(r => r.EntityType, StringComparer.Ordinal).ThenBy(r => r.EntityId).ToArray(),
                edges.OrderBy(e => e.ChildType, StringComparer.Ordinal).ThenBy(e => e.ChildId).ToArray(),
                scopes.OrderBy(s => s.ScopeKey, StringComparer.Ordinal).ThenBy(s => s.OccupantId).ToArray(),
                lifecycle.OrderBy(l => l.EntityType, StringComparer.Ordinal).ThenBy(l => l.EntityId).ToArray());
        }

        private static async Task ExpandTombstoneAsync(
            AppDbContext db, string entityType, Guid entityId, Guid causedBy, bool isDirect,
            Action<ImpactRow> addRow, List<ImpactEdge> edges, List<ImpactScope> scopes,
            List<ImpactLifecycle> lifecycle, HashSet<(string, Guid)> visited, CancellationToken ct)
        {
            if (!visited.Add((entityType, entityId))) return;

            var current = await ReadCurrentAsync(db, entityType, entityId, ct);
            var wasLive = current?.WasLive ?? true;

            addRow(new ImpactRow(entityType, entityId,
                isDirect ? RowEffect.Tombstoned : RowEffect.CascadeTombstoned, wasLive, causedBy));
            lifecycle.Add(new ImpactLifecycle(entityType, entityId, LifecycleEffect.Tombstone));

            var parentEdge = StructuralDependencyRegistry.All.FirstOrDefault(e => e.ChildType == entityType);
            if (parentEdge is not null && current?.ParentId is { } parentId)
            {
                edges.Add(new ImpactEdge(entityType, entityId, parentEdge.ChildField, parentId, EdgeChange.Removed));
                if (IsConstraintScopeField(entityType, parentEdge.ChildField))
                {
                    scopes.Add(new ImpactScope(
                        ScopeKeyFor(entityType, parentEdge.ChildField, parentId), parentId, ScopeChange.Released, entityId));
                }
            }

            foreach (var cascadeEdge in StructuralDependencyRegistry.CascadeChildrenOf(entityType))
            {
                var liveChildren = await LiveChildIdsAsync(db, cascadeEdge.ChildType, entityId, ct);
                foreach (var childId in liveChildren)
                {
                    await ExpandTombstoneAsync(db, cascadeEdge.ChildType, childId, causedBy: entityId, isDirect: false,
                        addRow, edges, scopes, lifecycle, visited, ct);
                }
            }
        }

        private static bool IsConstraintScopeField(string entityType, string field) =>
            entityType == SyncEntityTypes.TaskNote && field == "MaTask";

        private static string ScopeKeyFor(string entityType, string field, Guid scopeValue) =>
            entityType == SyncEntityTypes.TaskNote && field == "MaTask"
                ? ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null,
                    new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", scopeValue.ToString("D")))
                : throw new InvalidOperationException($"{entityType}.{field} is not a known constraint-scope field.");

        private sealed record CurrentRowState(bool WasLive, Guid? ParentId);

        private static async Task<CurrentRowState?> ReadCurrentAsync(
            AppDbContext db, string entityType, Guid entityId, CancellationToken ct)
        {
            switch (entityType)
            {
                case SyncEntityTypes.HocKy:
                {
                    var row = await db.HocKys.AsNoTracking()
                        .Where(h => h.MaHocKy == entityId).Select(h => new { h.IsDeleted }).FirstOrDefaultAsync(ct);
                    return row is null ? null : new CurrentRowState(!row.IsDeleted, null);
                }
                case SyncEntityTypes.MonHoc:
                {
                    var row = await db.MonHocs.AsNoTracking()
                        .Where(m => m.MaMonHoc == entityId).Select(m => new { m.IsDeleted, m.MaHocKy }).FirstOrDefaultAsync(ct);
                    return row is null ? null : new CurrentRowState(!row.IsDeleted, row.MaHocKy);
                }
                case SyncEntityTypes.StudyTask:
                {
                    var row = await db.StudyTasks.AsNoTracking()
                        .Where(t => t.MaTask == entityId).Select(t => new { t.IsDeleted, t.MaMonHoc }).FirstOrDefaultAsync(ct);
                    return row is null ? null : new CurrentRowState(!row.IsDeleted, row.MaMonHoc);
                }
                case SyncEntityTypes.TaskNote:
                {
                    var row = await db.TaskNotes.AsNoTracking()
                        .Where(n => n.Id == entityId).Select(n => new { n.IsDeleted, n.MaTask }).FirstOrDefaultAsync(ct);
                    return row is null ? null : new CurrentRowState(!row.IsDeleted, row.MaTask);
                }
                case SyncEntityTypes.TaskReferenceLink:
                {
                    var row = await db.TaskReferenceLinks.AsNoTracking()
                        .Where(l => l.Id == entityId).Select(l => new { l.IsDeleted, l.MaTask }).FirstOrDefaultAsync(ct);
                    return row is null ? null : new CurrentRowState(!row.IsDeleted, row.MaTask);
                }
                default:
                    return null; // unregistered type: no current-state read (RouteKnown gate rejects before this runs)
            }
        }

        private static async Task<IReadOnlyList<Guid>> LiveChildIdsAsync(
            AppDbContext db, string childType, Guid parentId, CancellationToken ct)
        {
            switch (childType)
            {
                case SyncEntityTypes.MonHoc:
                    return await db.MonHocs.AsNoTracking()
                        .Where(m => !m.IsDeleted && m.MaHocKy == parentId).Select(m => m.MaMonHoc).ToListAsync(ct);
                case SyncEntityTypes.StudyTask:
                    return await db.StudyTasks.AsNoTracking()
                        .Where(t => !t.IsDeleted && t.MaMonHoc == parentId).Select(t => t.MaTask).ToListAsync(ct);
                case SyncEntityTypes.TaskNote:
                    return await db.TaskNotes.AsNoTracking()
                        .Where(n => !n.IsDeleted && n.MaTask == parentId).Select(n => n.Id).ToListAsync(ct);
                case SyncEntityTypes.TaskReferenceLink:
                    return await db.TaskReferenceLinks.AsNoTracking()
                        .Where(l => !l.IsDeleted && l.MaTask == parentId).Select(l => l.Id).ToListAsync(ct);
                default:
                    return Array.Empty<Guid>();
            }
        }
    }
}
