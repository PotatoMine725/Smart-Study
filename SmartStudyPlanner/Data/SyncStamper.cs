using System;
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
        {
            foreach (var entry in tracker.Entries().ToList())
            {
                if (entry.Entity is not ISyncMetadata meta) continue;

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
    }
}
