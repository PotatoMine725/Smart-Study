using System;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync
{
    // Epic 2 / T2.4 (PR-4, DoR §8). Lifecycle of a ConflictRecord: Unresolved -> Resolved, never
    // back (D7-D). AutoLww/AutoTombstone evidence is written directly as Resolved (D9-T5).
    public enum ConflictRecordStatus { Unresolved = 0, Resolved = 1 }

    // What happened to the LOCAL candidate while staging a Structural/Constraint conflict so that
    // live = Base while Unresolved (D9-T1). None: nothing needed withdrawing (D9-T1 shape S1/S2 --
    // the local row is rewritten to Base in place, not withdrawn). RewrittenToBase: same as None,
    // recorded explicitly for audit. HardDeleted: the DoR §9.2/M5 audited exception -- a Base=null
    // TaskNote candidate had to be physically removed because there is no live row for it to become.
    // This column is on the CONFLICT table, not on any domain table, and only ever describes the
    // withdrawal PR-5's ConflictStaging performed in the SAME transaction it wrote this record.
    public enum ConflictLocalWithdrawal { None = 0, RewrittenToBase = 1, HardDeleted = 2 }

    /// <summary>
    /// Persistent staging boundary for a conflict candidate (D6/D8). Standalone bookkeeping row --
    /// Guid PK, no FK to any domain table -- same idiom as <see cref="SyncBaseSnapshotRow"/> and the
    /// telemetry log rows. Deliberately NOT <see cref="ISyncMetadata"/>: a conflict record is not
    /// itself synced business data in v1 (D7-H / OPEN-2 is still open), so it must never be stamped
    /// or tombstoned by <c>SyncStamper</c>.
    /// <para>
    /// This type persists exactly what T2.3's pure core (<c>Sync.Merge.ConflictCandidate</c>) and the
    /// T2.4 apply/resolution layers decide -- it does not decide anything itself. <see cref="Kind"/>,
    /// <see cref="StructuralReason"/> and <see cref="ResolutionKind"/> reuse the enums already defined
    /// by the pure merge core (PR-1) instead of duplicating a second taxonomy.
    /// </para>
    /// <para>
    /// Column order below is authoritative and mirrors <c>Data/SyncConflictRecordSchema.EnsureTable</c>
    /// exactly (dual-path drift-hazard test asserts the two agree column-for-column, same convention
    /// as <c>SyncBaseSnapshotSchemaDualPathTests.EnsureTable_ColumnShape_MatchesEfGeneratedSchemaExactly</c>).
    /// Evidence/identity columns are immutable after insert and Resolved is terminal -- both enforced
    /// by database triggers (<c>Data/SyncConflictRecordSchema.EnsureTable</c>), not by this class.
    /// </para>
    /// </summary>
    public class SyncConflictRecordRow
    {
        // ---- Identity ------------------------------------------------------------------------
        public Guid ConflictId { get; set; }          // D7-A: record identity. Minted once per new ConflictKey.
        public string ConflictKey { get; set; } = string.Empty;   // D7-A/D7-B: logical/idempotency identity.
        public string ScopeKey { get; set; } = string.Empty;      // D9-T6: at most one Unresolved per ScopeKey.

        // ---- Classification --------------------------------------------------------------------
        public ConflictKind Kind { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid? EntityId { get; set; }            // Field / Structural / Tombstone
        public string? FieldName { get; set; }          // Field / Structural
        public string? ConstraintKey { get; set; }       // Constraint scope key, e.g. "MaTask"
        public string? ConstraintValue { get; set; }     // Constraint scope value
        public StructuralReason? StructuralReason { get; set; }

        // ---- Peer / source ---------------------------------------------------------------------
        public string PeerDeviceId { get; set; } = string.Empty;
        public int SnapshotVersion { get; set; } = CanonicalJson.Version;

        // ---- Immutable evidence -----------------------------------------------------------------
        public Guid? BaseEntityId { get; set; }
        public string? BaseSnapshotJson { get; set; }     // D6-B: Base may be null.
        public string? BaseFingerprint { get; set; }      // null <=> "no live row in scope" (fp(null) = "null" in PR-1)

        public Guid LocalEntityId { get; set; }
        public string LocalSnapshotJson { get; set; } = string.Empty;
        public string LocalFingerprint { get; set; } = string.Empty;
        public long? LocalRowRev { get; set; }             // D9-T1/M5: local candidate's Rev at staging time,
                                                              // needed to restore Rev continuity on KeepLocal.
        public ConflictLocalWithdrawal LocalWithdrawal { get; set; } = ConflictLocalWithdrawal.None;

        public Guid RemoteEntityId { get; set; }
        public string RemoteSnapshotJson { get; set; } = string.Empty;
        public string RemoteFingerprint { get; set; } = string.Empty;

        // ---- State -------------------------------------------------------------------------------
        public ConflictRecordStatus Status { get; set; } = ConflictRecordStatus.Unresolved;

        // ---- Resolution / result provenance -------------------------------------------------------
        public ResolutionKind? ResolutionKind { get; set; }
        public Guid? ResultEntityId { get; set; }
        public string? ResultSnapshotJson { get; set; }   // D7-G: KeepBase with null Base => null result.
        public string? ResultFingerprint { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public string CreatedByDeviceId { get; set; } = string.Empty;
        public DateTime? ResolvedAtUtc { get; set; }
        public string? ResolvedByDeviceId { get; set; }
    }
}
