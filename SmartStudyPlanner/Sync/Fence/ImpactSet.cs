using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Sync.Fence
{
    // Epic 2 / T2.4 Slice 1 (fence spec §3.1, plan §7.1). Built by the (Slice-2) ImpactResolver;
    // constructed directly by hand in Slice-1 policy-unit tests. Pure data -- no behaviour.

    public enum RowEffect { Created, FieldsChanged, Reparented, Tombstoned, CascadeTombstoned }

    public sealed record ImpactRow(
        string EntityType, Guid EntityId, RowEffect Effect, bool WasLive, Guid CausedByEntityId);

    public enum EdgeChange { Removed, Added }

    public sealed record ImpactEdge(
        string ChildType, Guid ChildId, string Field, Guid ParentId, EdgeChange Change);

    public enum ScopeChange { Acquired, Released, OccupantContentChanged }

    public sealed record ImpactScope(
        string ScopeKey, Guid ScopeValue, ScopeChange Change, Guid OccupantId);

    public enum LifecycleEffect { Create, Tombstone }

    public sealed record ImpactLifecycle(string EntityType, Guid EntityId, LifecycleEffect Effect);

    public sealed record ImpactSet(
        IReadOnlyList<ImpactRow> Rows,
        IReadOnlyList<ImpactEdge> Edges,
        IReadOnlyList<ImpactScope> Scopes,
        IReadOnlyList<ImpactLifecycle> Lifecycle)
    {
        public static readonly ImpactSet Empty = new(
            Array.Empty<ImpactRow>(), Array.Empty<ImpactEdge>(),
            Array.Empty<ImpactScope>(), Array.Empty<ImpactLifecycle>());
    }
}
