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

        // Upsert. Find existing row by composite key; if present, overwrite Rev/
        // SnapshotJson/SyncedAtUtc in place; if absent, add new. Calls
        // db.SaveChangesAsync() itself (self-contained per call, matching how
        // Sqlite*Repository methods behave from a caller's perspective).
        public static async Task SetAsync(
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

            await db.SaveChangesAsync(ct);
        }

        public static async Task DeleteAsync(
            AppDbContext db, string peerId, string entityType, Guid entityId,
            CancellationToken ct = default)
        {
            var existing = await db.SyncBaseSnapshots.FindAsync(new object[] { peerId, entityType, entityId }, ct);
            if (existing is null) return;

            db.SyncBaseSnapshots.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
