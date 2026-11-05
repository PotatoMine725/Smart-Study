using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync
{
    /// <summary>Outcome of <see cref="SyncConflictRecordStore.StageAsync"/> (DoR §8.4).</summary>
    public enum ConflictStagingOutcome
    {
        /// <summary>No record existed for this ConflictKey or ScopeKey; the candidate was added to
        /// the tracker (not yet saved).</summary>
        Staged,

        /// <summary>A record with this exact ConflictKey already exists (any status) — the evidence
        /// is already stored. Retry-safe no-op (D5-G, D7-C): nothing was added to the tracker.</summary>
        AlreadyStaged,

        /// <summary>A different ConflictKey exists as Unresolved for this ScopeKey (D9-T6). Nothing
        /// was added to the tracker; the caller's sync operation must be rejected.</summary>
        ScopeHasUnresolvedConflict,
    }

    public sealed record ConflictStagingResult(ConflictStagingOutcome Outcome, SyncConflictRecordRow Record);

    // Epic 2 / T2.4 (PR-4) — persistent staging boundary for conflict evidence (D6/D8). Static class
    // taking AppDbContext db as first parameter, same idiom as SyncBaseSnapshotStore/SyncStamper/
    // SyncSchema (Data/Sync-layer infrastructure), not the Sqlite*Repository(factory) instance
    // convention used by domain ports.
    //
    // Transaction boundary (same reasoning as SyncBaseSnapshotStore, PR-3): every method here only
    // stages a change into the caller's AppDbContext. None of them call SaveChanges, none open or
    // commit a transaction, none own the context's lifetime. D8-C requires conflict staging and the
    // live-state decision to be atomic, and D9 §18 puts persist-conflict-OR-apply-result inside ONE
    // transaction the caller (PR-5's apply session) owns. A self-saving store would defeat that the
    // same way SyncBaseSnapshotStore.SetAsync used to (F-g/PR-3): it would flush every OTHER dirty
    // entity tracked by the same context through SyncStamper at a moment the caller did not choose.
    //
    // The caller owns: DbContext, transaction, SaveChanges, commit/rollback, and the actual merge/
    // resolution DECISIONS. This store owns: locating rows, checking the two staging invariants
    // (D7-C dedup, D9-T6 scope-lock) before Add, and writing resolution fields onto an already-loaded
    // tracked row. It does not decide what a resolution's Result should be (that is PR-6's
    // ConflictResolver, DoR §13) and it does not decide when a Structural/Constraint conflict is
    // detected or what its evidence contains (that is T2.3's pure core / PR-5's apply layer).
    public static class SyncConflictRecordStore
    {
        public static Task<SyncConflictRecordRow?> GetAsync(
            AppDbContext db, Guid conflictId, CancellationToken ct = default)
        {
            return db.SyncConflictRecords.FindAsync(new object[] { conflictId }, ct).AsTask();
        }

        public static Task<SyncConflictRecordRow?> FindByConflictKeyAsync(
            AppDbContext db, string conflictKey, CancellationToken ct = default)
        {
            // A plain LINQ query, not FindAsync: ConflictKey is not the primary key, so this always
            // hits the database rather than resolving a same-unit-of-work Added instance from the
            // local tracker first. StageAsync below relies on this — see its own comment for why
            // that is the correct, honestly-documented behaviour rather than a bug.
            return db.SyncConflictRecords.FirstOrDefaultAsync(r => r.ConflictKey == conflictKey, ct);
        }

        public static Task<SyncConflictRecordRow?> FindUnresolvedByScopeKeyAsync(
            AppDbContext db, string scopeKey, CancellationToken ct = default)
        {
            return db.SyncConflictRecords.FirstOrDefaultAsync(
                r => r.ScopeKey == scopeKey && r.Status == ConflictRecordStatus.Unresolved, ct);
        }

        /// <summary>
        /// Stages <paramref name="candidate"/> as a new ConflictRecord, applying the two staging
        /// invariants (DoR §8.4) as a typed pre-check before touching the tracker:
        /// <list type="number">
        /// <item>a record with the same <see cref="SyncConflictRecordRow.ConflictKey"/> already exists
        /// (any status) => <see cref="ConflictStagingOutcome.AlreadyStaged"/>, no-op (D5-G/D7-C);</item>
        /// <item>otherwise an <c>Unresolved</c> record already occupies this
        /// <see cref="SyncConflictRecordRow.ScopeKey"/> => <see cref="ConflictStagingOutcome.ScopeHasUnresolvedConflict"/>,
        /// no-op (D9-T6);</item>
        /// <item>otherwise the candidate is added to the tracker (not saved) and
        /// <see cref="ConflictStagingOutcome.Staged"/> is returned.</item>
        /// </list>
        /// This pre-check is DELIBERATELY not the last line of defence — it is a convenience that
        /// turns the common case into a typed result instead of a caught exception. The partial
        /// unique index on <c>(ScopeKey) WHERE Status = 0</c> and the unique index on
        /// <c>ConflictKey</c> (see <c>SyncConflictRecordSchema</c>) are the actual, unconditional
        /// enforcement: a caller that raced past this check (e.g. two concurrent staging attempts on
        /// separate contexts) still gets a hard failure at <c>SaveChanges</c>, never a silently
        /// accepted duplicate. Callers that want retry-safety within a single database MUST NOT rely
        /// on this pre-check alone across process/context boundaries.
        /// <para>
        /// <paramref name="candidate"/>.<see cref="SyncConflictRecordRow.ConflictId"/> is used only
        /// when the outcome is <see cref="ConflictStagingOutcome.Staged"/> (D7-A: minted once per new
        /// ConflictKey). On <see cref="ConflictStagingOutcome.AlreadyStaged"/> the EXISTING record is
        /// returned instead, and the caller's candidate id/evidence is discarded — evidence is
        /// immutable once staged (D7-F), so a retry's freshly-built candidate can never overwrite it.
        /// </para>
        /// </summary>
        public static async Task<ConflictStagingResult> StageAsync(
            AppDbContext db, SyncConflictRecordRow candidate, CancellationToken ct = default)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));

            var existingByKey = await db.SyncConflictRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ConflictKey == candidate.ConflictKey, ct);
            if (existingByKey is not null)
                return new ConflictStagingResult(ConflictStagingOutcome.AlreadyStaged, existingByKey);

            var unresolvedInScope = await db.SyncConflictRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ScopeKey == candidate.ScopeKey && r.Status == ConflictRecordStatus.Unresolved, ct);
            if (unresolvedInScope is not null)
                return new ConflictStagingResult(ConflictStagingOutcome.ScopeHasUnresolvedConflict, unresolvedInScope);

            db.SyncConflictRecords.Add(candidate);
            return new ConflictStagingResult(ConflictStagingOutcome.Staged, candidate);
        }

        /// <summary>
        /// Stages the resolution fields onto an already-persisted record: loads it (tracked),
        /// overwrites <see cref="SyncConflictRecordRow.Status"/> to <see cref="ConflictRecordStatus.Resolved"/>
        /// plus the resolution/result columns, and returns the tracked instance. Does NOT call
        /// SaveChanges. Returns null if no record exists for <paramref name="conflictId"/>.
        /// <para>
        /// This is a field-setter only — the same shape as <c>SyncBaseSnapshotStore.UpsertAsync</c>.
        /// It does NOT decide whether the resolution is valid: it does not re-check live-state drift
        /// (D8-H), it does not reject a replay against an Auto* record (D9-T5), and it does not
        /// re-resolve an already-Resolved record — that orchestration is <c>ConflictResolver</c>
        /// (PR-6, DoR §13), which this store deliberately does not own. What DOES stop an invalid
        /// call here from corrupting anything is <c>trg_SyncConflictRecords_ResolvedIsTerminal</c>:
        /// calling this again on an already-Resolved row stages an in-memory mutation that
        /// unconditionally aborts at <c>SaveChanges</c> (proved by
        /// <c>SyncConflictRecordStoreTests</c>), so an orchestration bug fails closed here rather than
        /// silently overwriting evidence.
        /// </para>
        /// </summary>
        public static async Task<SyncConflictRecordRow?> MarkResolvedAsync(
            AppDbContext db, Guid conflictId,
            ResolutionKind resolutionKind, Guid? resultEntityId, string? resultSnapshotJson, string? resultFingerprint,
            DateTime resolvedAtUtc, string resolvedByDeviceId,
            CancellationToken ct = default)
        {
            var row = await db.SyncConflictRecords.FindAsync(new object[] { conflictId }, ct);
            if (row is null) return null;

            row.Status = ConflictRecordStatus.Resolved;
            row.ResolutionKind = resolutionKind;
            row.ResultEntityId = resultEntityId;
            row.ResultSnapshotJson = resultSnapshotJson;
            row.ResultFingerprint = resultFingerprint;
            row.ResolvedAtUtc = resolvedAtUtc;
            row.ResolvedByDeviceId = resolvedByDeviceId;
            return row;
        }

        // No Remove/Delete method exists here on purpose (mission SCOPE / DoR §8.1: "records are
        // never deleted in v1"). trg_SyncConflictRecords_NoDelete is the unconditional backstop;
        // SyncConflictRecordStoreTests asserts by reflection that this type exposes none.
    }
}
