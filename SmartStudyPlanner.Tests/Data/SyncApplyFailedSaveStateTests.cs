using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Epic 2 / T2.4 PR-5 — REPRODUCTION of the two R2 hazards recorded (as labelled inference, not
    /// measurement) in <c>docs/review/2026-09-09-t2.4-pr5-pr2-residuals.md</c>. That document's
    /// checklist requires PR-5 to measure both cases *before* designing a mitigation, so these tests
    /// exist to turn the inference into evidence and to pin down the exact reason
    /// <c>SyncApplySession</c> gives every logical operation its own <see cref="AppDbContext"/> and
    /// disposes it unconditionally.
    ///
    /// These are CHARACTERIZATION tests of the PR-2 seam, not guards on new behaviour: they assert
    /// that a failed save leaves in-memory state ahead of the database. The named production mutation
    /// that turns them RED is "snapshot and restore Rev inside
    /// SyncStamper.ApplyPreservingProvenance" — i.e. the in-memory rollback the residuals doc tells
    /// PR-5 *not* to add. Their value is that they fail loudly if someone adds it anyway, because the
    /// session's context-per-operation lifetime would then be paying for a hazard that no longer
    /// exists.
    ///
    /// Why the obvious assertion is deliberately NOT the point (residuals doc, R2 "Required tests"
    /// item 3): "nothing was committed" is trivially true in Case A — the throw happens inside
    /// SyncStamper.Apply, before anything reaches the database — and would pass against a completely
    /// broken implementation. The discriminating assertions are that the surviving in-memory instance
    /// is ahead of the persisted row, and that the *next* ordinary save on the same context commits
    /// that drift with LOCAL provenance, which is precisely the failure mode PR-2 exists to prevent.
    /// </summary>
    public class SyncApplyFailedSaveStateTests : IDisposable
    {
        private static readonly DateTime RemoteAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        private static readonly DateTime LocalAt = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
        private const string RemoteDevice = "REMOTE-DEVICE";
        private const string LocalDevice = "LOCAL-DEVICE";

        private readonly SqliteConnection _conn;

        public SyncApplyFailedSaveStateTests() => _conn = TestDb.OpenConnection();

        public void Dispose() => _conn.Dispose();

        private AppDbContext NewCtx()
        {
            var ctx = TestDb.Create(_conn);
            ctx.Clock = () => LocalAt;
            ctx.DeviceIdProvider = () => LocalDevice;
            return ctx;
        }

        private async Task<(Guid First, Guid Second)> SeedTwoHocKyAsync()
        {
            using var ctx = NewCtx();
            var a = new HocKy("HK A", new DateTime(2026, 1, 1));
            var b = new HocKy("HK B", new DateTime(2026, 1, 2));
            ctx.HocKys.Add(a);
            ctx.HocKys.Add(b);
            await ctx.SaveChangesAsync();
            return (a.MaHocKy, b.MaHocKy);
        }

        private async Task<long> DbRevAsync(Guid id)
        {
            using var read = NewCtx();
            var row = await read.HocKys.AsNoTracking().FirstAsync(h => h.MaHocKy == id);
            return row.Rev;
        }

        // ------------------------------------------------------------------ R2 Case A

        /// <summary>
        /// R2 Case A — a marked entry validated BEFORE an invalid marked entry has already taken its
        /// in-memory <c>Rev++</c> when <c>SyncStamper.Apply</c> throws. The intent map is cleared in
        /// AppDbContext's <c>finally</c>, so that still-Modified instance arrives at the next ordinary
        /// <c>SaveChanges</c> UNMARKED and is committed with a second Rev bump and local provenance.
        /// </summary>
        [Fact]
        public async Task CaseA_ThrowInsideApply_LeavesEarlierEntryAheadOfDb_AndNextOrdinarySaveCommitsLocalProvenance()
        {
            var (firstId, secondId) = await SeedTwoHocKyAsync();

            using var ctx = NewCtx();

            // Tracking order decides visitation order (ChangeTracker.Entries() enumerates the identity
            // map, which is insertion-ordered), so load the entry that must survive the throw first.
            var first = await ctx.HocKys.FirstAsync(h => h.MaHocKy == firstId);
            var second = await ctx.HocKys.FirstAsync(h => h.MaHocKy == secondId);

            Assert.Equal(1, await DbRevAsync(firstId));

            // Valid marked entry: carries the winning remote provenance the apply layer must preserve.
            first.Ten = "merged-from-remote";
            first.ModifiedAtUtc = RemoteAt;
            first.ModifiedByDeviceId = RemoteDevice;
            ctx.MarkSyncApplied(first);

            // Invalid marked entry: default ModifiedAtUtc is exactly what the seam fails closed on.
            second.Ten = "also-merged";
            second.ModifiedAtUtc = default;
            second.ModifiedByDeviceId = RemoteDevice;
            ctx.MarkSyncApplied(second);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
            Assert.Contains("incomplete provenance", thrown.Message);

            // MEASUREMENT 1 — the earlier entry really was mutated before the throw. If
            // ChangeTracker had visited the invalid entry first this would be 1, and the
            // reproduction would be inconclusive rather than silently vacuous.
            Assert.Equal(2, first.Rev);
            Assert.Equal(1, await DbRevAsync(firstId));   // database untouched: the throw precedes any I/O

            // MEASUREMENT 2 (the discriminating one) — the failed operation's dirty instance is
            // carried into the next save as an ordinary local edit.
            Assert.Equal(EntityState.Modified, ctx.Entry(first).State);
            await ctx.SaveChangesAsync();

            using var verify = NewCtx();
            var committed = await verify.HocKys.AsNoTracking().FirstAsync(h => h.MaHocKy == firstId);
            Assert.Equal(3, committed.Rev);                         // double bump: 1 -> 2 (failed) -> 3
            Assert.Equal(LocalDevice, committed.ModifiedByDeviceId); // remote provenance LOST
            Assert.Equal(LocalAt, committed.ModifiedAtUtc);
            Assert.Equal("merged-from-remote", committed.Ten);       // ...on content the merge decided
        }

        // ------------------------------------------------------------------ R2 Case B

        /// <summary>
        /// R2 Case B — DoR §11.2 puts two saves inside one transaction (apply, then baseline upsert).
        /// When the SECOND save fails and the transaction rolls back, the first save already ran with
        /// <c>acceptAllChangesOnSuccess</c>, so EF has marked those entities <c>Unchanged</c> while the
        /// database was rolled back. Nothing is dirty to signal the drift, and EF identity resolution
        /// hands the stale instance back to any later read on the same context.
        ///
        /// The deterministic lever for the second failure is
        /// <c>trg_SyncConflictRecords_ResolvedIsTerminal</c>: a staged mutation of a Resolved record
        /// aborts unconditionally at the database.
        /// </summary>
        [Fact]
        public async Task CaseB_SecondSaveFailsInsideTransaction_LeavesCleanInstanceAheadOfRolledBackDb()
        {
            var (id, _) = await SeedTwoHocKyAsync();

            Guid resolvedConflictId;
            using (var seed = NewCtx())
            {
                SyncConflictRecordSchema.EnsureTable(seed);   // EnsureCreated() never creates triggers
                var rec = ResolvedRecord();
                resolvedConflictId = rec.ConflictId;
                seed.SyncConflictRecords.Add(rec);
                await seed.SaveChangesAsync();
            }

            using var ctx = NewCtx();
            await using var tx = await ctx.Database.BeginTransactionAsync();

            // --- first save: the apply write, exactly as the session performs it
            var hocKy = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            hocKy.Ten = "merged-from-remote";
            hocKy.ModifiedAtUtc = RemoteAt;
            hocKy.ModifiedByDeviceId = RemoteDevice;
            ctx.MarkSyncApplied(hocKy);
            await ctx.SaveChangesAsync();

            Assert.Equal(2, hocKy.Rev);
            Assert.Equal(EntityState.Unchanged, ctx.Entry(hocKy).State);

            // --- second save inside the same transaction: fails at the database
            var resolved = await ctx.SyncConflictRecords.FirstAsync(r => r.ConflictId == resolvedConflictId);
            resolved.Status = ConflictRecordStatus.Unresolved;
            await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());

            await tx.RollbackAsync();

            // MEASUREMENT — after rollback the instance is CLEAN and still ahead of the database.
            Assert.Equal(EntityState.Unchanged, ctx.Entry(hocKy).State);
            Assert.Equal(2, hocKy.Rev);
            Assert.Equal(1, await DbRevAsync(id));
            Assert.Equal("HK A", (await FreshRowAsync(id)).Ten);   // content rolled back too

            // ...and identity resolution keeps handing that stale instance to this context, so a
            // "reload" on the failed context does not recover authoritative state.
            var reread = await ctx.HocKys.FirstAsync(h => h.MaHocKy == id);
            Assert.Same(hocKy, reread);
            Assert.Equal(2, reread.Rev);
            Assert.Equal("merged-from-remote", reread.Ten);
        }

        private async Task<HocKy> FreshRowAsync(Guid id)
        {
            using var read = NewCtx();
            return await read.HocKys.AsNoTracking().FirstAsync(h => h.MaHocKy == id);
        }

        private static SyncConflictRecordRow ResolvedRecord() => new()
        {
            ConflictId = Guid.NewGuid(),
            ConflictKey = "case-b-key",
            ScopeKey = "case-b-scope",
            Kind = SmartStudyPlanner.Sync.Merge.ConflictKind.FieldConflict,
            EntityType = SyncEntityTypes.HocKy,
            EntityId = Guid.NewGuid(),
            FieldName = "Ten",
            PeerDeviceId = RemoteDevice,
            LocalEntityId = Guid.NewGuid(),
            LocalSnapshotJson = "{}",
            LocalFingerprint = "local-fp",
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "remote-fp",
            Status = ConflictRecordStatus.Resolved,
            ResolutionKind = SmartStudyPlanner.Sync.Merge.ResolutionKind.AutoLww,
            CreatedAtUtc = LocalAt,
            CreatedByDeviceId = LocalDevice,
            ResolvedAtUtc = LocalAt,
            ResolvedByDeviceId = LocalDevice,
        };
    }
}
