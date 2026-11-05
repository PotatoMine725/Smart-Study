using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Sync
{
    // Epic 2 / M2.1, T2.2 — change enumeration per peer via the Rev watermark. Depends on
    // T1.4's SyncBaseSnapshotStore for the per-(peer, row) baseline; Rev is a per-row edit
    // counter (never compared across devices), so the predicate below compares row.Rev
    // against THIS peer's own last-synced snapshot for THAT row, never a single scalar.
    public static class SyncChangeEnumerator
    {
        // Generic core predicate. allRows must come from the raw DbSet (db.HocKys,
        // db.StudyTasks, ...), never from a Sqlite*Repository — repositories filter out
        // tombstoned rows, which would silently hide deletions from the enumeration.
        public static async Task<List<ChangedEntity<T>>> EnumerateChangesAsync<T>(
            AppDbContext db, string entityType, IEnumerable<T> allRows,
            Func<T, Guid> idSelector, string peerId, CancellationToken ct = default)
            where T : class, ISyncMetadata
        {
            var baseline = await SyncBaseSnapshotStore.GetAllForPeerAsync(db, peerId, entityType, ct);
            var result = new List<ChangedEntity<T>>();
            foreach (var row in allRows)
            {
                var id = idSelector(row);
                if (!baseline.TryGetValue(id, out var snapshot) || row.Rev > snapshot.Rev)
                    result.Add(new ChangedEntity<T>(id, row, row.Rev, row.IsDeleted));
            }
            return result;
        }

        // Wires the generic predicate across all six synced entities for one peer.
        public static async Task<PeerChangeSet> GetChangesForPeerAsync(
            AppDbContext db, string peerId, CancellationToken ct = default)
        {
            return new PeerChangeSet(
                await EnumerateChangesAsync(db, SyncEntityTypes.HocKy,
                    await db.HocKys.ToListAsync(ct), h => h.MaHocKy, peerId, ct),
                await EnumerateChangesAsync(db, SyncEntityTypes.MonHoc,
                    await db.MonHocs.ToListAsync(ct), m => m.MaMonHoc, peerId, ct),
                await EnumerateChangesAsync(db, SyncEntityTypes.StudyTask,
                    await db.StudyTasks.ToListAsync(ct), t => t.MaTask, peerId, ct),
                await EnumerateChangesAsync(db, SyncEntityTypes.StudyLog,
                    await db.StudyLogs.ToListAsync(ct), l => l.Id, peerId, ct),
                await EnumerateChangesAsync(db, SyncEntityTypes.TaskNote,
                    await db.TaskNotes.ToListAsync(ct), n => n.Id, peerId, ct),
                await EnumerateChangesAsync(db, SyncEntityTypes.TaskReferenceLink,
                    await db.TaskReferenceLinks.ToListAsync(ct), l => l.Id, peerId, ct));
        }
    }
}
