using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    /// <summary>
    /// Single stamping seam (Epic 1 / D-I, M1.1 scope): walks the ChangeTracker before SaveChanges
    /// commits and stamps Rev/ModifiedAtUtc/ModifiedByDeviceId on every added/modified ISyncMetadata
    /// entity. Delete→tombstone + G1 cascade is M1.2 scope — G1 closes there, not here.
    /// </summary>
    public static class SyncStamper
    {
        public static void Apply(ChangeTracker tracker, Func<DateTime> utcNow, string deviceId)
        {
            foreach (var entry in tracker.Entries())
            {
                if (entry.Entity is not ISyncMetadata meta) continue;
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

                meta.Rev++;
                meta.ModifiedAtUtc = utcNow();
                meta.ModifiedByDeviceId = deviceId;
            }
        }
    }
}
