using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Sync
{
    // Epic 2 / M2.1, T1.4 — per-peer last-synced base-snapshot store. Static class taking
    // AppDbContext db as first parameter, mirroring SyncStamper/SyncSchema/TelemetrySchema
    // (Data-layer sync infrastructure), NOT the Sqlite*Repository(factory) instance
    // convention used by domain ports.
    //
    // Epic 2 / T2.4 (PR-3) — transaction boundary. The write methods below stage their change
    // into the caller's AppDbContext and deliberately do NOT call SaveChanges, do NOT begin or
    // commit a transaction, and do NOT own the context's lifetime. D8-G ("one logical sync
    // operation = one transaction boundary") requires the baseline update to sit inside the
    // caller's transaction together with merge/apply and conflict staging, and DoR §11.2 puts
    // two SaveChangesAsync calls (apply, then baseline upsert) inside that one transaction.
    //
    // A self-saving store defeats both. It also flushes every OTHER dirty entity tracked by the
    // same context, and AppDbContext.SaveChanges runs SyncStamper over the whole ChangeTracker —
    // so an unrelated in-flight edit would be persisted AND stamped with local provenance
    // (Rev++/ModifiedAtUtc) by a call the caller only meant to record a baseline. Keeping the
    // save out of here means SyncStamper only ever runs at a save boundary the caller chose.
    //
    // The caller owns: DbContext, transaction, SaveChanges, commit/rollback.
    // This store owns: locating the baseline row, and setting its values.
    public static class SyncBaseSnapshotStore
    {
        public static Task<SyncBaseSnapshotRow?> GetAsync(
            AppDbContext db, string peerId, string entityType, Guid entityId,
            CancellationToken ct = default)
        {
            return db.SyncBaseSnapshots.FindAsync(new object[] { peerId, entityType, entityId }, ct).AsTask();
        }

        // Keyed by EntityId, scoped to one peer + one entity type — this IS the
        // watermark lookup table T2.2 reads.
        public static async Task<Dictionary<Guid, SyncBaseSnapshotRow>> GetAllForPeerAsync(
            AppDbContext db, string peerId, string entityType,
            CancellationToken ct = default)
        {
            var rows = await db.SyncBaseSnapshots
                .Where(r => r.PeerDeviceId == peerId && r.EntityType == entityType)
                .ToListAsync(ct);
            return rows.ToDictionary(r => r.EntityId);
        }

        /// <summary>
        /// Stages an upsert of one baseline row into <paramref name="db"/>. Finds the existing row
        /// by composite key; if present, overwrites Rev/SnapshotJson/SyncedAtUtc on that instance;
        /// if absent, adds a new one. Nothing is written to the database until the caller calls
        /// SaveChanges/SaveChangesAsync itself.
        ///
        /// FindAsync resolves the tracked instance first, so a row already tracked (including one
        /// still pending as Added from an earlier call in the same unit of work) is mutated in
        /// place rather than re-added — repeated upserts of the same key stage exactly one row.
        /// That is also why there is no detached DbSet.Update(row) overload here: it would attach a
        /// second instance of the same key and mark every column modified.
        ///
        /// <paramref name="rev"/> is supplied by the caller. This store neither computes nor infers
        /// baseline Rev (§11.1: Rev is a local-only counter owned by the apply layer).
        ///
        /// On failure nothing is saved and no transaction is touched; the caller remains
        /// responsible for rollback and for disposing/recreating the context.
        /// </summary>
        public static async Task UpsertAsync(
            AppDbContext db, string peerId, string entityType, Guid entityId,
            long rev, string? snapshotJson, DateTime syncedAtUtc,
            CancellationToken ct = default)
        {
            var existing = await db.SyncBaseSnapshots.FindAsync(new object[] { peerId, entityType, entityId }, ct);
            if (existing is null)
            {
                db.SyncBaseSnapshots.Add(new SyncBaseSnapshotRow
                {
                    PeerDeviceId = peerId,
                    EntityType = entityType,
                    EntityId = entityId,
                    Rev = rev,
                    SnapshotJson = snapshotJson,
                    SyncedAtUtc = syncedAtUtc,
                });
            }
            else
            {
                existing.Rev = rev;
                existing.SnapshotJson = snapshotJson;
                existing.SyncedAtUtc = syncedAtUtc;
            }
        }

        /// <summary>
        /// Stages removal of one baseline row. Same contract as <see cref="UpsertAsync"/>: the
        /// removal is only tracked, and takes effect when the caller saves. A missing row is a
        /// no-op.
        ///
        /// This is a real delete of a bookkeeping row, not a domain delete — SyncBaseSnapshotRow
        /// does not implement ISyncMetadata (see its doc comment), so it is never tombstoned or
        /// stamped and the Epic-1 no-hard-delete invariant does not apply to it.
        /// </summary>
        public static async Task RemoveAsync(
            AppDbContext db, string peerId, string entityType, Guid entityId,
            CancellationToken ct = default)
        {
            var existing = await db.SyncBaseSnapshots.FindAsync(new object[] { peerId, entityType, entityId }, ct);
            if (existing is null) return;

            db.SyncBaseSnapshots.Remove(existing);
        }
    }
}
