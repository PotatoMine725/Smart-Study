using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Single stamping seam (Epic 1 / D-I): walks the ChangeTracker before SaveChanges commits
    /// and stamps every ISyncMetadata entity. Added/Modified get Rev/ModifiedAtUtc/ModifiedByDeviceId.
    /// Deleted (M1.2, G1) is converted to a tombstone — State flips to Modified, IsDeleted/DeletedAtUtc
    /// are set — so no real DELETE ever reaches the DB. Cascade to children relies on AppDbContext's
    /// OnDelete(Cascade) config already having resolved them to Deleted state via EF's own in-memory
    /// fixup (only reaches children that are loaded/tracked — see docs/plans/2026-07-03-g1-soft-delete-cascade.md).
    /// Entries() is snapshotted to a list first so flipping a parent's State back to Modified can't
    /// disturb the fixup that already ran for its children.
    /// </summary>
    public static class SyncStamper
    {
        public static void Apply(ChangeTracker tracker, Func<DateTime> utcNow, string deviceId)
            => Apply(tracker, utcNow, deviceId, null);

        /// <summary>
        /// Epic 2 / T2.4 (PR-2, DoR §10) apply seam. <paramref name="intents"/> carries a per-entry
        /// sync-apply marker keyed by entity reference; an entry present in it keeps the provenance
        /// the merge/apply layer already wrote (ModifiedAtUtc / ModifiedByDeviceId / IsDeleted /
        /// DeletedAtUtc) and only gets the local <c>Rev++</c>. Every other entry — including an
        /// unmarked ISyncMetadata entry in the very same SaveChanges — goes down the local path
        /// above, unchanged. Passing <c>null</c> (or an empty map) is byte-identical to the
        /// pre-PR-2 behaviour, which is what the 3-arg overload does.
        ///
        /// The marker is deliberately per-entry rather than a batch flag or a swapped
        /// Clock/DeviceIdProvider: a batch-wide switch would silently strip local provenance from
        /// unrelated edits that happen to share the unit of work (D9 §20, no batch-wide override).
        /// A marked entry may not be <see cref="EntityState.Deleted"/> — a winning remote tombstone
        /// is applied by setting IsDeleted/DeletedAtUtc explicitly, never via Remove(), so the
        /// stamper never has to guess whether a delete is local or remote (DoR §10.2).
        /// </summary>
        public static void Apply(ChangeTracker tracker, Func<DateTime> utcNow, string deviceId,
                                 IReadOnlyDictionary<object, SyncApplyIntent>? intents)
        {
            foreach (var entry in tracker.Entries().ToList())
            {
                if (entry.Entity is not ISyncMetadata meta) continue;

                if (intents is not null && intents.ContainsKey(entry.Entity))
                {
                    ApplyPreservingProvenance(entry, meta);
                    continue;
                }

                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    meta.Rev++;
                    meta.ModifiedAtUtc = utcNow();
                    meta.ModifiedByDeviceId = deviceId;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    meta.Rev++;
                    meta.IsDeleted = true;
                    meta.DeletedAtUtc = utcNow();
                    meta.ModifiedAtUtc = utcNow();
                    meta.ModifiedByDeviceId = deviceId;
                }
            }
        }

        /// <summary>
        /// Marked path (DoR §10.1). Fails closed on anything the apply layer should have settled
        /// before marking; deliberately does NOT repair incomplete provenance — a silently patched
        /// row would converge to different bytes on the two peers. Rev stays a local-only counter
        /// (§11.1): incremented here, never read from or copied out of the remote snapshot.
        /// An Unchanged marked entry is a no-op (§11.1 rule 4: a no-op merge writes nothing and
        /// must not force Modified), so replaying an already-applied change leaves Rev alone.
        /// </summary>
        private static void ApplyPreservingProvenance(EntityEntry entry, ISyncMetadata meta)
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException(
                    "sync-apply must set IsDeleted explicitly; Remove() is not allowed on a marked entry");

            if (entry.State is not (EntityState.Added or EntityState.Modified)) return;

            if (meta.ModifiedAtUtc == default
                || string.IsNullOrEmpty(meta.ModifiedByDeviceId)
                || (meta.IsDeleted && meta.DeletedAtUtc is null))
                throw new InvalidOperationException("sync-apply entry carries incomplete provenance");

            meta.Rev++;
        }
    }
}
