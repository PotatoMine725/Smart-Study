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
    /// intent expands through <see cref="StructuralDependencyRegistry"/> cascade edges. A
    /// <see cref="MutationOperation.Reparent"/>/<see cref="MutationOperation.UpdateFields"/>/
    /// <see cref="MutationOperation.Create"/> intent never adds descendants.
    /// </para>
    /// <para>
    /// <b>Request-level, not per-intent (H-3).</b> The whole <see cref="MutationRequest"/> is one logical
    /// operation (D8-G), so the impact set must be self-consistent with the combined request rather than
    /// the concatenation of independently resolved intents. See <c>EffectiveParents</c> and
    /// <c>CascadeChildIdsAsync</c>.
    /// </para>
    /// <para>
    /// <b>CLOSED — the cascade predicate is LIVE-ONLY (M-3/A-1, owner ruling 2026-09-17).</b> The
    /// effective cascade predicate is <c>child.IsDeleted == false</c>, implemented by
    /// <see cref="LiveChildIdsAsync"/> and by the matching liveness check on the "moved in" leg of
    /// <c>CascadeChildIdsAsync</c>.
    /// <para>
    /// The ruling's principle: the ImpactSet models the SEMANTIC DOMAIN EFFECTS of the requested
    /// mutation, never implementation-only re-stamping/provenance writes. P0-a
    /// (<c>CascadePredicateProbeTests</c>) measured that the two production cascade implementations
    /// differ — the sync path (<c>SyncApplySession.CascadeTombstoneAsync</c>) is live-only, while the
    /// local path (<c>TaskCascadeHelper</c>, reached from <c>LuuHocKyAsync</c> and
    /// <c>SqliteStudyTaskRepository.DeleteAsync</c>) has no <c>IsDeleted</c> filter and re-stamps an
    /// already-tombstoned child. That re-stamp is an implementation-level <c>Rev</c>/provenance write,
    /// NOT a semantic lifecycle transition, so it does not belong in the impact set. An
    /// already-tombstoned child therefore produces no new <see cref="LifecycleEffect.Tombstone"/>, no
    /// <see cref="RowEffect.CascadeTombstoned"/> row, and no constraint-scope release.
    /// </para>
    /// <para>
    /// <b>D9-T1 is untouched by this.</b> The <c>UNIQUE(MaTask)</c> index on TaskNote stays unfiltered,
    /// so a tombstoned note still OCCUPIES its scope — the ruling says only that this mutation does not
    /// RELEASE it. Live occupant + <c>Tombstone(T)</c> =&gt; cascade reaches it =&gt; <c>Released(K)</c>
    /// =&gt; <c>CONS.ScopeReleased</c> may block. Already-dead occupant + <c>Tombstone(T)</c> =&gt; not an
    /// effective cascade target =&gt; no <c>Released(K)</c> =&gt; the OD-4 branch returns
    /// <c>CONS.EmptyScopeParentTombstoned</c>. "Scope remains occupied" and "scope does not exist" are
    /// different states and must not be conflated. Both legs are pinned by <c>ImpactCascadeLivenessTests</c>.
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
            var expanded = new HashSet<(string Type, Guid Id)>();

            // H-3: the request is resolved AS A UNIT. Every Reparent intent is collected up front, so a
            // cascade expanded later in the same request sees each row's post-request parent rather than
            // its stale DB FK.
            var effectiveParent = EffectiveParents(request);

            void AddRow(ImpactRow row)
            {
                var key = (row.EntityType, row.EntityId);
                if (rows.TryGetValue(key, out var existing) && Precedence(existing.Effect) >= Precedence(row.Effect)) return;
                rows[key] = row;
            }

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
                            isDirect: true, AddRow, edges, scopes, lifecycle, expanded, effectiveParent, ct);
                        break;

                    default:
                        throw new InvalidOperationException($"Unhandled MutationOperation {intent.Operation}.");
                }
            }

            // Every list is deduplicated and TOTALLY ordered (M-1): sorting on a partial key leaves ties
            // broken by insertion order, which would make two equivalent permutations of the same
            // request produce unequal -- but semantically identical -- ImpactSets.
            return new ImpactSet(
                rows.Values
                    .OrderBy(r => r.EntityType, StringComparer.Ordinal).ThenBy(r => r.EntityId).ToArray(),
                edges.Distinct()
                    .OrderBy(e => e.ChildType, StringComparer.Ordinal).ThenBy(e => e.ChildId)
                    .ThenBy(e => e.Field, StringComparer.Ordinal).ThenBy(e => e.ParentId).ThenBy(e => e.Change).ToArray(),
                scopes.Distinct()
                    .OrderBy(s => s.ScopeKey, StringComparer.Ordinal).ThenBy(s => s.OccupantId)
                    .ThenBy(s => s.ScopeValue).ThenBy(s => s.Change).ToArray(),
                lifecycle.Distinct()
                    .OrderBy(l => l.EntityType, StringComparer.Ordinal).ThenBy(l => l.EntityId)
                    .ThenBy(l => l.Effect).ToArray());
        }

        /// <summary>
        /// <b>RowEffect precedence (review finding M-1). Keep this next to <c>AddRow</c>.</b>
        /// <para>
        /// A row can be named by more than one intent in the same request, and by a cascade as well as
        /// directly. The effect kept for that row is the MAXIMUM of this total order, never the
        /// first-seen one -- so the result does not depend on the order intents happen to appear in the
        /// request, nor on dictionary/hash iteration order.
        /// </para>
        /// <para>
        /// The order is severity/terminality: an explicit <see cref="RowEffect.Tombstoned"/> outranks a
        /// <see cref="RowEffect.CascadeTombstoned"/> reached through an ancestor, so a DIRECT intent is
        /// never silently downgraded to a cascade effect merely because the cascade was expanded first.
        /// Both tombstone kinds outrank the non-terminal effects, because a row the request removes is
        /// removed however else the request also touches it.
        /// </para>
        /// <para>
        /// This ranking is observable: <c>RoutingStage.DirectSubject</c> vs
        /// <c>RoutingStage.CascadeReached</c> in every S1 policy's result is driven by it, and Stage is
        /// the first key of the router's deterministic aggregation.
        /// </para>
        /// </summary>
        private static int Precedence(RowEffect effect) => effect switch
        {
            RowEffect.Tombstoned => 5,
            RowEffect.CascadeTombstoned => 4,
            RowEffect.Reparented => 3,
            RowEffect.Created => 2,
            RowEffect.FieldsChanged => 1,
            _ => throw new InvalidOperationException($"Unranked RowEffect {effect}."),
        };

        /// <summary>
        /// The post-request parent of every row a <see cref="MutationOperation.Reparent"/> intent moves,
        /// keyed by (type, id) — review finding H-3.
        /// <para>
        /// Resolving each intent independently against live DB state lets one request report
        /// contradictory effects: <c>Reparent(T, M1 -&gt; M2)</c> + <c>Tombstone(M1)</c> would read T's
        /// stale <c>MaMonHoc = M1</c> and report T as both successfully moved out AND cascade-tombstoned
        /// under M1, together with T's own descendants and constraint scopes. The combined mutation does
        /// no such thing.
        /// </para>
        /// <para>
        /// One uniform rule replaces that, applied in <c>CascadeChildIdsAsync</c>: a row's parent for
        /// cascade purposes is its <see cref="RelationChange.After"/> when this request reparents it,
        /// and its DB FK otherwise. That excludes rows moved OUT of a tombstoned subtree and includes
        /// rows moved IN -- it is not a guard against one example. <c>After</c> is authoritative;
        /// <c>Before</c> is never consulted, since it is caller-supplied and may be stale.
        /// </para>
        /// <para>
        /// <see cref="MutationOperation.Create"/> is deliberately NOT part of this map. A created row is
        /// not in the database, so no cascade query can reach it; adding it would be inventing closure
        /// rather than modelling the actual cascade (INV-2).
        /// </para>
        /// </summary>
        private static IReadOnlyDictionary<(string Type, Guid Id), Guid?> EffectiveParents(MutationRequest request)
        {
            var map = new Dictionary<(string Type, Guid Id), Guid?>();

            foreach (var intent in request.Intents)
            {
                if (intent.Operation != MutationOperation.Reparent) continue;

                foreach (var rel in intent.Relations)
                {
                    if (StructuralDependencyRegistry.ParentTypeOf(intent.EntityType, rel.Field) is null) continue;
                    map[(intent.EntityType, intent.EntityId)] = rel.After;
                }
            }

            return map;
        }

        private static async Task ExpandTombstoneAsync(
            AppDbContext db, string entityType, Guid entityId, Guid causedBy, bool isDirect,
            Action<ImpactRow> addRow, List<ImpactEdge> edges, List<ImpactScope> scopes,
            List<ImpactLifecycle> lifecycle, HashSet<(string, Guid)> expanded,
            IReadOnlyDictionary<(string Type, Guid Id), Guid?> effectiveParent, CancellationToken ct)
        {
            var current = await ReadCurrentAsync(db, entityType, entityId, ct);
            var wasLive = current?.WasLive ?? true;

            // The row effect is recorded on EVERY visit and resolved by Precedence (M-1). Gating the
            // effect on "not yet visited" is what let a cascade seen first suppress a later direct
            // intent on the same row.
            addRow(new ImpactRow(entityType, entityId,
                isDirect ? RowEffect.Tombstoned : RowEffect.CascadeTombstoned, wasLive, causedBy));

            // Everything below is emitted once per row, so a revisit updates the effect and adds no
            // duplicate edge/scope/lifecycle entry and re-walks no descendants.
            if (!expanded.Add((entityType, entityId))) return;

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
                var children = await CascadeChildIdsAsync(db, cascadeEdge.ChildType, entityId, effectiveParent, ct);
                foreach (var childId in children)
                {
                    await ExpandTombstoneAsync(db, cascadeEdge.ChildType, childId, causedBy: entityId, isDirect: false,
                        addRow, edges, scopes, lifecycle, expanded, effectiveParent, ct);
                }
            }
        }

        /// <summary>
        /// The children of <paramref name="parentId"/> that THIS request's cascade actually reaches
        /// (H-3): the rows whose post-request parent is <paramref name="parentId"/>.
        /// <para>
        /// Starts from <see cref="LiveChildIdsAsync"/> — the live-only DB predicate ratified by M-3/A-1
        /// — then applies the request's own reparent intents: a row this request moves elsewhere is
        /// dropped, and a row it moves here is added. The "moved in" row is subjected to the SAME
        /// liveness predicate as the DB query (<c>!current.WasLive</c> below), so the two legs cannot
        /// drift apart.
        /// </para>
        /// <para>Result is sorted, so traversal order (and thus cascade causedBy attribution) does not
        /// depend on the database's row order or on dictionary iteration order.</para>
        /// </summary>
        private static async Task<IReadOnlyList<Guid>> CascadeChildIdsAsync(
            AppDbContext db, string childType, Guid parentId,
            IReadOnlyDictionary<(string Type, Guid Id), Guid?> effectiveParent, CancellationToken ct)
        {
            var children = new HashSet<Guid>();

            foreach (var id in await LiveChildIdsAsync(db, childType, parentId, ct))
            {
                // Moved out of this subtree by this same request => the combined mutation does not
                // cascade it, so reporting it as cascade-tombstoned would be a contradiction.
                if (effectiveParent.TryGetValue((childType, id), out var after) && after != parentId) continue;
                children.Add(id);
            }

            foreach (var moved in effectiveParent)
            {
                if (moved.Key.Type != childType || moved.Value != parentId) continue;
                if (children.Contains(moved.Key.Id)) continue;

                var current = await ReadCurrentAsync(db, childType, moved.Key.Id, ct);
                if (current is null || !current.WasLive) continue;
                children.Add(moved.Key.Id);
            }

            return children.OrderBy(id => id).ToArray();
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

        /// <summary>
        /// The EFFECTIVE cascade children of <paramref name="parentId"/>: <c>child.IsDeleted == false</c>
        /// (M-3/A-1, owner-ratified 2026-09-17). An already-tombstoned child is not a new lifecycle
        /// target, so the <c>!IsDeleted</c> clause in every arm below is load-bearing semantics, not an
        /// optimisation — removing it would model an implementation-level re-stamp as a domain effect
        /// and emit a false constraint-scope release (see <c>ImpactCascadeLivenessTests</c>).
        /// </summary>
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
