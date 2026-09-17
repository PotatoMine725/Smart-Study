using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Epic 2 / T2.4 PR-2 — sync-apply seam (DoR §10, tests X-1..X-6). Covers the marked path
    /// (provenance preserved, Rev++), per-entry isolation from unmarked local edits in the same
    /// SaveChanges, fail-closed validation, intent-map cleanup, both save APIs, and the legacy
    /// 3-argument SyncStamper.Apply overload.
    ///
    /// Local provenance is asserted *by value* against sentinel Clock/DeviceIdProvider rather than
    /// by counting delegate invocations: AppDbContext evaluates DeviceIdProvider() eagerly in the
    /// argument list, so an invocation count cannot distinguish "consulted for this entry" from
    /// "consulted for the batch" and would fail against a correct implementation.
    /// </summary>
    public class SyncApplySeamTests : IDisposable
    {
        private static readonly DateTime RemoteAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        private static readonly DateTime RemoteDeletedAt = new(2026, 3, 4, 5, 6, 8, DateTimeKind.Utc);
        private static readonly DateTime LocalAt = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
        private const string RemoteDevice = "REMOTE-DEVICE";
        private const string LocalDevice = "LOCAL-DEVICE";

        // A SECOND local identity, used where the seeded row already carries the first one. Without
        // it, "provenance preserved" and "provenance re-stamped locally" produce identical bytes on
        // a locally-seeded row, and the mixed-batch test cannot tell a per-entry marker apart from a
        // batch-wide one (verified: with a single local identity, a batch-wide mutation stays green).
        private static readonly DateTime LocalAt2 = new(2026, 8, 15, 18, 30, 0, DateTimeKind.Utc);
        private const string LocalDevice2 = "LOCAL-DEVICE-2";

        private readonly SqliteConnection _conn;

        public SyncApplySeamTests() => _conn = TestDb.OpenConnection();

        public void Dispose() => _conn.Dispose();

        /// <summary>Context whose local stamping sources are sentinels, so any local stamp is visible.</summary>
        private AppDbContext NewCtx()
        {
            var ctx = TestDb.Create(_conn);
            ctx.Clock = () => LocalAt;
            ctx.DeviceIdProvider = () => LocalDevice;
            return ctx;
        }

        private async Task<Guid> SeedHocKyAsync()
        {
            using var ctx = NewCtx();
            var hocKy = new HocKy("HK", DateTime.Today);
            ctx.HocKys.Add(hocKy);
            await ctx.SaveChangesAsync();
            return hocKy.MaHocKy;
        }

        private static void SetRemoteProvenance(ISyncMetadata meta)
        {
            meta.ModifiedAtUtc = RemoteAt;
            meta.ModifiedByDeviceId = RemoteDevice;
        }

        // ---------------------------------------------------------------- B. marked sync behavior

        /// <summary>X-1 — marked Added keeps remote provenance and still gets the local Rev.</summary>
        [Fact]
        public async Task MarkedAdded_PreservesProvenance_AndIncrementsRev()
        {
            using var ctx = NewCtx();
            var hocKy = new HocKy("HK remote", DateTime.Today);
            SetRemoteProvenance(hocKy);

            ctx.HocKys.Add(hocKy);
            ctx.MarkSyncApplied(hocKy);
            await ctx.SaveChangesAsync();

            Assert.Equal(RemoteAt, hocKy.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, hocKy.ModifiedByDeviceId);
            Assert.Equal(1, hocKy.Rev);
            Assert.False(hocKy.IsDeleted);
            Assert.Null(hocKy.DeletedAtUtc);

            // and it really is what landed in the DB, not just the in-memory instance
            using var verify = NewCtx();
            var stored = await verify.HocKys.AsNoTracking().FirstAsync(h => h.MaHocKy == hocKy.MaHocKy);
            Assert.Equal(RemoteAt, stored.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, stored.ModifiedByDeviceId);
        }

        /// <summary>
        /// X-1 — marked Modified keeps remote provenance and increments Rev exactly once.
        /// This is the exact scenario C-4 characterizes as destructive on the unmarked path.
        /// </summary>
        [Fact]
        public async Task MarkedModified_PreservesProvenance_AndIncrementsRevExactlyOnce()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            Assert.Equal(1, tracked.Rev);

            tracked.Ten = "HK merged";
            SetRemoteProvenance(tracked);
            ctx.MarkSyncApplied(tracked);
            await ctx.SaveChangesAsync();

            Assert.Equal(RemoteAt, tracked.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, tracked.ModifiedByDeviceId);
            Assert.Equal(2, tracked.Rev);
        }

        /// <summary>X-1 — a marked live row keeps IsDeleted = false; the seam never invents a tombstone.</summary>
        [Fact]
        public async Task MarkedLiveEntity_KeepsIsDeletedFalse()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            tracked.Ten = "HK still live";
            SetRemoteProvenance(tracked);
            ctx.MarkSyncApplied(tracked);
            await ctx.SaveChangesAsync();

            Assert.False(tracked.IsDeleted);
            Assert.Null(tracked.DeletedAtUtc);
        }

        /// <summary>
        /// X-1 — a winning remote tombstone, applied the DoR §10.2 way: IsDeleted/DeletedAtUtc set
        /// explicitly and the entry marked, never Remove(). All four provenance fields survive.
        /// </summary>
        [Fact]
        public async Task MarkedTombstone_PreservesTombstoneAndProvenance_AndIncrementsRev()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            tracked.IsDeleted = true;
            tracked.DeletedAtUtc = RemoteDeletedAt;
            SetRemoteProvenance(tracked);
            ctx.MarkSyncApplied(tracked);
            await ctx.SaveChangesAsync();

            Assert.True(tracked.IsDeleted);
            Assert.Equal(RemoteDeletedAt, tracked.DeletedAtUtc);
            Assert.Equal(RemoteAt, tracked.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, tracked.ModifiedByDeviceId);
            Assert.Equal(2, tracked.Rev);

            using var verify = NewCtx();
            var stored = await verify.HocKys.AsNoTracking().FirstAsync(h => h.MaHocKy == id);
            Assert.True(stored.IsDeleted);
            Assert.Equal(RemoteDeletedAt, stored.DeletedAtUtc);
            Assert.Equal(RemoteAt, stored.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, stored.ModifiedByDeviceId);
        }

        /// <summary>
        /// X-1 — the marked entry's provenance comes from neither the local Clock nor the local
        /// DeviceIdProvider. Both are sentinels here and neither value may appear on the row.
        /// </summary>
        [Fact]
        public async Task MarkedEntry_DoesNotTakeProvenanceFromLocalClockOrDeviceProvider()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            tracked.Ten = "HK merged";
            SetRemoteProvenance(tracked);
            ctx.MarkSyncApplied(tracked);
            await ctx.SaveChangesAsync();

            Assert.NotEqual(LocalAt, tracked.ModifiedAtUtc);
            Assert.NotEqual(LocalDevice, tracked.ModifiedByDeviceId);
        }

        /// <summary>
        /// §11.1 rule 4 — an idempotent replay leaves the entity Unchanged; a marked Unchanged
        /// entry writes nothing and must not bump Rev (the session must not force Modified).
        /// </summary>
        [Fact]
        public async Task MarkedUnchangedEntity_IsNoOp_AndLeavesRevAlone()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            ctx.MarkSyncApplied(tracked);
            await ctx.SaveChangesAsync();

            Assert.Equal(1, tracked.Rev);
        }

        // ------------------------------------------------------------- C. mixed-batch isolation

        /// <summary>
        /// X-4 — the decisive per-entry test: one marked and one ordinary local edit in the SAME
        /// SaveChanges. A batch-wide suppression (or a swapped Clock/DeviceIdProvider) would strip
        /// B's local provenance; a batch-wide stamp would destroy A's remote provenance.
        /// </summary>
        [Fact]
        public async Task MixedSave_MarkedKeepsRemoteProvenance_UnmarkedGetsLocalStamp()
        {
            var idA = await SeedHocKyAsync();
            var idB = await SeedHocKyAsync();

            using var ctx = NewCtx();
            // distinct save-time local identity: B was seeded under LocalAt/LocalDevice, so only a
            // genuine re-stamp can move it to LocalAt2/LocalDevice2 — leaving B's provenance in
            // place (a batch-wide preserve) is now visible.
            ctx.Clock = () => LocalAt2;
            ctx.DeviceIdProvider = () => LocalDevice2;

            var a = await ctx.HocKys.FirstAsync(h => h.MaHocKy == idA);
            var b = await ctx.HocKys.FirstAsync(h => h.MaHocKy == idB);
            Assert.Equal(LocalAt, b.ModifiedAtUtc);          // precondition: B starts on identity 1

            a.Ten = "A from remote";
            SetRemoteProvenance(a);
            ctx.MarkSyncApplied(a);

            b.Ten = "B edited locally";

            await ctx.SaveChangesAsync();

            Assert.Equal(RemoteAt, a.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, a.ModifiedByDeviceId);
            Assert.Equal(2, a.Rev);

            Assert.Equal(LocalAt2, b.ModifiedAtUtc);
            Assert.Equal(LocalDevice2, b.ModifiedByDeviceId);
            Assert.Equal(2, b.Rev);
        }

        // ------------------------------------------------------------------ D. failure / cleanup

        [Theory]
        [InlineData(true, false, false)]   // ModifiedAtUtc left at default
        [InlineData(false, true, false)]   // ModifiedByDeviceId empty
        [InlineData(false, false, true)]   // IsDeleted without DeletedAtUtc
        public async Task MarkedEntryWithInvalidProvenance_FailsClosed(
            bool defaultTimestamp, bool emptyDevice, bool tombstoneWithoutDeletedAt)
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            SetRemoteProvenance(tracked);
            tracked.Ten = "HK invalid";

            if (defaultTimestamp) tracked.ModifiedAtUtc = default;
            if (emptyDevice) tracked.ModifiedByDeviceId = "";
            if (tombstoneWithoutDeletedAt)
            {
                tracked.IsDeleted = true;
                tracked.DeletedAtUtc = null;
            }

            ctx.MarkSyncApplied(tracked);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
            Assert.Contains("incomplete provenance", ex.Message);
            Assert.Equal(1, tracked.Rev);   // fails before the local Rev++
        }

        /// <summary>
        /// X-2 — Remove() on a marked entity is forbidden: SyncStamper must not be taught to
        /// reinterpret a Deleted entry as a valid remote tombstone (DoR §10.2).
        /// </summary>
        [Fact]
        public async Task MarkedDeletedEntry_FailsClosed()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            SetRemoteProvenance(tracked);
            ctx.HocKys.Remove(tracked);
            ctx.MarkSyncApplied(tracked);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
            Assert.Contains("Remove() is not allowed", ex.Message);
        }

        /// <summary>X-5 — a failed save must not leak sync intent into the next save on the same context.</summary>
        [Fact]
        public async Task FailedSave_ClearsIntent_SoNextOrdinarySaveStampsLocally()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            tracked.Ten = "HK invalid";
            tracked.ModifiedAtUtc = default;
            tracked.ModifiedByDeviceId = "";
            ctx.MarkSyncApplied(tracked);

            await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());

            // same instance, same context, no re-mark: must now take the ordinary local path
            tracked.Ten = "HK edited locally";
            await ctx.SaveChangesAsync();

            Assert.Equal(LocalAt, tracked.ModifiedAtUtc);
            Assert.Equal(LocalDevice, tracked.ModifiedByDeviceId);
            Assert.Equal(2, tracked.Rev);
        }

        /// <summary>X-5 — intent is scoped to one save; it does not survive a successful one either.</summary>
        [Fact]
        public async Task SuccessfulMarkedSave_ClearsIntent_SoNextOrdinarySaveStampsLocally()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            tracked.Ten = "HK from remote";
            SetRemoteProvenance(tracked);
            ctx.MarkSyncApplied(tracked);
            await ctx.SaveChangesAsync();
            Assert.Equal(RemoteAt, tracked.ModifiedAtUtc);

            tracked.Ten = "HK edited locally afterwards";
            await ctx.SaveChangesAsync();

            Assert.Equal(LocalAt, tracked.ModifiedAtUtc);
            Assert.Equal(LocalDevice, tracked.ModifiedByDeviceId);
            Assert.Equal(3, tracked.Rev);
        }

        // ---------------------------------------------------------------------- E. both save APIs

        /// <summary>X-3 — the synchronous SaveChanges() goes through the identical seam.</summary>
        [Fact]
        public async Task SyncSaveChanges_MarkedEntry_PreservesProvenance_AndIncrementsRev()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = ctx.HocKys.First(h => h.MaHocKy == id);
            tracked.Ten = "HK from remote";
            SetRemoteProvenance(tracked);
            ctx.MarkSyncApplied(tracked);
            ctx.SaveChanges();

            Assert.Equal(RemoteAt, tracked.ModifiedAtUtc);
            Assert.Equal(RemoteDevice, tracked.ModifiedByDeviceId);
            Assert.Equal(2, tracked.Rev);
            await Task.CompletedTask;
        }

        /// <summary>X-3/X-5 — sync path also fails closed and also clears intent in its finally.</summary>
        [Fact]
        public async Task SyncSaveChanges_FailedMarkedSave_ClearsIntent()
        {
            var id = await SeedHocKyAsync();

            using var ctx = NewCtx();
            var tracked = ctx.HocKys.First(h => h.MaHocKy == id);
            tracked.Ten = "HK invalid";
            tracked.ModifiedAtUtc = default;
            tracked.ModifiedByDeviceId = "";
            ctx.MarkSyncApplied(tracked);

            Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());

            tracked.Ten = "HK edited locally";
            ctx.SaveChanges();

            Assert.Equal(LocalAt, tracked.ModifiedAtUtc);
            Assert.Equal(LocalDevice, tracked.ModifiedByDeviceId);
            await Task.CompletedTask;
        }

        // ----------------------------------------------------------- F. backward compatibility

        /// <summary>
        /// X-6 — the legacy 3-argument overload, exercised DIRECTLY. After PR-2 AppDbContext calls
        /// the 4-argument overload, so the existing stamping tests no longer reach the 3-arg one;
        /// without this test the "byte-identical legacy path" claim would be unproven.
        /// </summary>
        [Fact]
        public async Task LegacyThreeArgOverload_StampsLocally_AndTombstonesDeleted()
        {
            var idToModify = await SeedHocKyAsync();
            var idToDelete = await SeedHocKyAsync();

            using var ctx = TestDb.Create(_conn);

            // all three states left PENDING, so the direct Apply call is what acts on them
            var added = new HocKy("HK added", DateTime.Today);
            ctx.HocKys.Add(added);

            var modified = await ctx.HocKys.FirstAsync(h => h.MaHocKy == idToModify);
            modified.Ten = "HK modified";

            var toDelete = await ctx.HocKys.FirstAsync(h => h.MaHocKy == idToDelete);
            ctx.HocKys.Remove(toDelete);

            var stampAt = new DateTime(2026, 9, 9, 9, 9, 9, DateTimeKind.Utc);
            SyncStamper.Apply(ctx.ChangeTracker, () => stampAt, "LEGACY-DEVICE");

            Assert.Equal(stampAt, added.ModifiedAtUtc);
            Assert.Equal("LEGACY-DEVICE", added.ModifiedByDeviceId);
            Assert.Equal(1, added.Rev);
            Assert.Equal(2, modified.Rev);

            Assert.Equal(stampAt, modified.ModifiedAtUtc);
            Assert.Equal("LEGACY-DEVICE", modified.ModifiedByDeviceId);

            Assert.True(toDelete.IsDeleted);
            Assert.Equal(stampAt, toDelete.DeletedAtUtc);
            Assert.Equal(stampAt, toDelete.ModifiedAtUtc);
            Assert.Equal("LEGACY-DEVICE", toDelete.ModifiedByDeviceId);
            Assert.Equal(EntityState.Modified, ctx.Entry(toDelete).State);
        }
    }
}
