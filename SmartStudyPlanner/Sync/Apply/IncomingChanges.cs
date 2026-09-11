using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — the only integration hook PR-5 adds: turns what T2.2's
    /// <see cref="SyncChangeEnumerator"/> produced on the SENDING device into apply input on this one.
    /// <para>
    /// The conversion to <see cref="EntitySnapshot"/> happens here, at the boundary, for a concrete
    /// reason: <see cref="ChangedEntity{T}"/> carries the sending context's TRACKED instance
    /// (<c>SyncChangeEnumerator</c> loads whole DbSets tracked), and attaching another context's
    /// tracked entity to the apply session would hand EF two identity maps over one object. A snapshot
    /// is inert, is exactly what the pure core consumes, and is the same shape a future T2.1 transport
    /// would deserialize into — so M2.1 ("merge engine, no network") and M2.2 share one entry point.
    /// </para>
    /// <para>
    /// <c>Rev</c> is deliberately dropped: it is a local counter on the sender and has no meaning here
    /// (D9 §21). <c>ChangedEntity.IsDeleted</c> is likewise not re-read — the tombstone state travels
    /// inside the snapshot's provenance block, so there is only one representation of it.
    /// </para>
    /// </summary>
    public static class IncomingChanges
    {
        /// <summary>
        /// Flattens a peer's change set into apply input. Order does not matter: the session plans its
        /// own dependency-safe order (DoR §12.5).
        /// </summary>
        public static IncomingChangeSet FromPeerChangeSet(PeerChangeSet changes, string peerDeviceId)
        {
            if (changes is null) throw new ArgumentNullException(nameof(changes));
            if (string.IsNullOrWhiteSpace(peerDeviceId)) throw new ArgumentException("Peer device id is required.", nameof(peerDeviceId));

            var list = new List<IncomingChange>();
            Add(list, SyncEntityTypes.HocKy, changes.HocKys);
            Add(list, SyncEntityTypes.MonHoc, changes.MonHocs);
            Add(list, SyncEntityTypes.StudyTask, changes.StudyTasks);
            Add(list, SyncEntityTypes.StudyLog, changes.StudyLogs);
            Add(list, SyncEntityTypes.TaskNote, changes.TaskNotes);
            Add(list, SyncEntityTypes.TaskReferenceLink, changes.TaskReferenceLinks);
            return new IncomingChangeSet(peerDeviceId, list);
        }

        /// <summary>One row, for a harness that builds input without a whole change set.</summary>
        public static IncomingChange Of(ISyncMetadata entity) => new(
            EntitySnapshotMapper.EntityTypeOf(entity),
            EntitySnapshotMapper.IdOf(entity),
            EntitySnapshotMapper.ToSnapshot(entity));

        private static void Add<T>(List<IncomingChange> target, string entityType, IEnumerable<ChangedEntity<T>> rows)
            where T : class, ISyncMetadata
        {
            foreach (var row in rows)
                target.Add(new IncomingChange(entityType, row.EntityId, EntitySnapshotMapper.ToSnapshot(row.Entity)));
        }
    }
}
