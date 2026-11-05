namespace SmartStudyPlanner.Sync.Fence
{
    // Epic 2 / T2.4 Slice 1 (fence spec §4, §8.3; plan §10, INV-6/INV-8). A policy is pure: it never
    // takes an AppDbContext (no I/O) and never takes a MutationOrigin (no local bypass). The purity
    // fence (mission §8) enforces this by source scan, not just by signature.
    internal interface IConflictFencePolicy
    {
        ConflictShape Shape { get; }

        /// <summary>Pure. Reads only the row's persisted columns and immutable evidence JSON.</summary>
        ProtectedContract Derive(SyncConflictRecordRow unresolved);

        /// <summary>Pure. No AppDbContext, no MutationOrigin.</summary>
        PolicyResult Evaluate(ProtectedContract contract, ImpactSet impact);
    }
}
