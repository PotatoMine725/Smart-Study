using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — maps a pure-core <see cref="ConflictCandidate"/> onto PR-4's persistent
    /// <see cref="SyncConflictRecordRow"/>, and owns the one audited Epic-1 hard-delete exception
    /// (DoR §9.2, mechanism M5, owner-approved as ACK A1).
    /// <para>
    /// This type makes no merge decisions and opens no transaction. It stages rows into the caller's
    /// context; <see cref="SyncApplySession"/> owns the DbContext, the transaction and the saves.
    /// </para>
    /// </summary>
    internal static class ConflictStaging
    {
        /// <summary>
        /// Builds the persistent row for a candidate. Auto kinds (D9-T5) are written directly as
        /// <see cref="ConflictRecordStatus.Resolved"/> with their deterministic result and are never
        /// re-resolvable; everything else enters as <see cref="ConflictRecordStatus.Unresolved"/>.
        /// <para>
        /// <c>BaseFingerprint</c> is left null when Base is null — the schema's own contract is
        /// "null ⇔ no live row in scope", and PR-6's drift check (DoR §13.2) reads it as
        /// <c>rec.BaseFingerprint ?? fp(null)</c>, so a null column and the literal fingerprint of a
        /// null snapshot mean the same thing without either being ambiguous.
        /// </para>
        /// </summary>
        public static SyncConflictRecordRow ToRow(
            ConflictCandidate candidate, string peerDeviceId,
            DateTime nowUtc, string deviceId,
            long? localRowRev, ConflictLocalWithdrawal withdrawal)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));

            var auto = candidate.AutoResolution;

            return new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),                       // D7-A: minted once per new ConflictKey
                ConflictKey = ConflictKeys.ConflictKey(candidate),
                ScopeKey = ConflictKeys.ScopeKeyOf(candidate),

                Kind = candidate.Kind,
                EntityType = candidate.EntityType,
                EntityId = candidate.EntityId,
                FieldName = candidate.FieldName,
                ConstraintKey = candidate.Scope?.Key,
                ConstraintValue = candidate.Scope?.Value,
                StructuralReason = candidate.Reason,

                PeerDeviceId = peerDeviceId,
                SnapshotVersion = CanonicalJson.Version,

                BaseEntityId = candidate.BaseEntityId,
                BaseSnapshotJson = candidate.Base is null ? null : CanonicalJson.Write(candidate.Base),
                BaseFingerprint = candidate.Base is null ? null : CanonicalJson.Fingerprint(candidate.Base),

                // D4/D9-T4 amendment: absent together when a StructuralConflict has no competing local
                // candidate. Same idiom as Base above, and deliberately NOT a fallback to the remote
                // row -- the amendment forbids representing absence with a synthetic local snapshot.
                // ConflictKeys.ConflictKey (called for ConflictKey above) is the single place that
                // validates this, and it throws for any other kind, so no guard is duplicated here.
                LocalEntityId = candidate.LocalEntityId,
                LocalSnapshotJson = candidate.Local is null ? null : CanonicalJson.Write(candidate.Local),
                LocalFingerprint = candidate.Local is null ? null : CanonicalJson.Fingerprint(candidate.Local),
                LocalRowRev = localRowRev,
                LocalWithdrawal = withdrawal,

                RemoteEntityId = candidate.RemoteEntityId,
                RemoteSnapshotJson = CanonicalJson.Write(candidate.Remote),
                RemoteFingerprint = CanonicalJson.Fingerprint(candidate.Remote),

                Status = auto is null ? ConflictRecordStatus.Unresolved : ConflictRecordStatus.Resolved,
                ResolutionKind = auto,
                ResultEntityId = auto is null ? null : candidate.EntityId ?? candidate.LocalEntityId,
                ResultSnapshotJson = candidate.AutoResult is null ? null : CanonicalJson.Write(candidate.AutoResult),
                ResultFingerprint = candidate.AutoResult is null ? null : CanonicalJson.Fingerprint(candidate.AutoResult),

                CreatedAtUtc = nowUtc,
                CreatedByDeviceId = deviceId,
                ResolvedAtUtc = auto is null ? null : nowUtc,
                ResolvedByDeviceId = auto is null ? null : deviceId,
            };
        }

        // ---------------------------------------------------------------- M5, the audited exception

        /// <summary>
        /// DoR §9.2 mechanism M5, owner-approved (ACK A1). The ONLY production site allowed to issue a
        /// physical DELETE against a synced domain table, and only ever against one
        /// <see cref="TaskNote"/> row: the withdrawn local candidate of a Base-null constraint conflict
        /// (D9-T1 shape S3), where "live = Base" means the uniqueness scope must end with no row at all
        /// and no other mechanism can produce that without leaking a domain tombstone into the sync
        /// stream (M1/M2) or adding a third row state plus a schema change on a user table (M3).
        /// <para>
        /// <b>Evidence-first, and evidence-first in the committed sense.</b> The caller must have
        /// already <c>Add</c>ed the ConflictRecord carrying this row's full snapshot and
        /// <c>LocalRowRev</c>, and must have saved it, before this runs: raw SQL executes immediately
        /// against the connection while an EF <c>Add</c> only materialises at SaveChanges, so
        /// "Add ⇒ DELETE ⇒ SaveChanges" would put the physical delete on the wire BEFORE the evidence.
        /// Both orders are inside the one transaction and both roll back together, so a crash loses
        /// neither — but only this order makes the precondition below checkable at delete time, and
        /// only this order keeps the invariant true at every instant the connection is observable.
        /// </para>
        /// <para>
        /// Preconditions are enforced, not documented: the record must be persisted, Unresolved, carry
        /// <see cref="ConflictLocalWithdrawal.HardDeleted"/>, name this exact row, and the row must not
        /// be tracked (the loader reads with <c>AsNoTracking</c>, so a tracked instance means someone
        /// staged an EF write for the row this method is about to remove underneath it).
        /// </para>
        /// <para>
        /// Raw SQL is deliberate (D9 §20 "audited exception", not a silent shortcut): teaching
        /// <c>SyncStamper</c> a "real delete" mode would widen the HIGH-impact stamping seam for the
        /// sake of one row class. <c>SyncApplyAuditFenceTests</c> asserts this is the only such site.
        /// </para>
        /// </summary>
        public static async Task HardDeleteWithdrawnTaskNoteAsync(
            AppDbContext db, SyncConflictRecordRow record, Guid taskNoteId, CancellationToken ct = default)
        {
            if (db is null) throw new ArgumentNullException(nameof(db));
            if (record is null) throw new ArgumentNullException(nameof(record));

            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("M5 hard delete must run inside the apply session's transaction.");

            if (!StringComparer.Ordinal.Equals(record.EntityType, SyncEntityTypes.TaskNote))
                throw new InvalidOperationException("M5 hard delete is scoped to TaskNote rows only.");

            if (record.Kind != ConflictKind.ConstraintConflict)
                throw new InvalidOperationException("M5 hard delete only withdraws a constraint-conflict candidate.");

            if (record.LocalWithdrawal != ConflictLocalWithdrawal.HardDeleted)
                throw new InvalidOperationException("M5 hard delete requires LocalWithdrawal = HardDeleted on the record.");

            if (record.Status != ConflictRecordStatus.Unresolved)
                throw new InvalidOperationException("M5 hard delete only applies while the conflict is Unresolved.");

            if (record.LocalEntityId != taskNoteId)
                throw new InvalidOperationException("M5 hard delete target does not match the record's LocalEntityId.");

            if (string.IsNullOrEmpty(record.LocalSnapshotJson) || record.LocalRowRev is null)
                throw new InvalidOperationException("M5 hard delete requires the withdrawn row's snapshot and Rev on the record.");

            if (db.Entry(record).State != EntityState.Unchanged)
                throw new InvalidOperationException("M5 hard delete requires the evidence row to be saved first (evidence-first).");

            foreach (var tracked in db.ChangeTracker.Entries<TaskNote>())
            {
                if (tracked.Entity.Id == taskNoteId)
                    throw new InvalidOperationException("M5 hard delete target must not be tracked by the apply context.");
            }

            // The single audited physical delete. Parameterised, never string-concatenated.
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM TaskNotes WHERE Id = {taskNoteId}", ct);
        }
    }

    /// <summary>
    /// Epic 2 / T2.4 — the shared Base-drift primitive (D8-H). Kept here in PR-5 because staging is
    /// what writes <see cref="SyncConflictRecordRow.BaseFingerprint"/>, and kept deliberately small:
    /// PR-6's <c>ConflictResolver</c> is its consumer, where DoR §13.2 places the actual
    /// "reject the resolution on drift" precondition.
    /// <para>
    /// There is NO apply-time drift gate. On the apply path Base comes from the baseline row and Local
    /// from the live row, and those are *supposed* to differ — that is what a three-way merge is for.
    /// Adding a gate here would be inventing policy the frozen record does not contain.
    /// </para>
    /// </summary>
    public static class SyncBaseFingerprint
    {
        /// <summary>Fingerprint of a snapshot, or of "no row in scope" when null (PR-1's <c>fp(null)</c>).</summary>
        public static string Of(EntitySnapshot? snapshot) => CanonicalJson.Fingerprint(snapshot);

        /// <summary>
        /// True when the live state of a conflict's scope still matches the Base the conflict was
        /// staged against. A stored null column means "no live row in scope", which matches a null
        /// <paramref name="currentLive"/>. No force and no automatic rebase exist in v1 (D8-H):
        /// callers reject on false.
        /// </summary>
        public static bool Matches(string? storedBaseFingerprint, EntitySnapshot? currentLive) =>
            StringComparer.Ordinal.Equals(storedBaseFingerprint ?? Of(null), Of(currentLive));
    }
}
