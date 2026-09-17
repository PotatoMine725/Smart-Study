using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — tests G and H from the PR-5 brief: the transaction boundary and the
    /// context lifetime that together discharge residual R2 from
    /// <c>docs/review/2026-09-09-t2.4-pr5-pr2-residuals.md</c>.
    /// <para>
    /// Both R2 cases were first REPRODUCED against the unmodified PR-2 seam in
    /// <c>SyncApplyFailedSaveStateTests</c> (Data): a failed save leaves an in-memory instance ahead of
    /// the database, either dirty (Case A) or clean-looking (Case B). The session's answer is structural
    /// rather than disciplinary — one context per logical operation, disposed on every path — and these
    /// tests assert the consequences a retry can actually observe.
    /// </para>
    /// </summary>
    public class SyncApplyTransactionTests : IDisposable
    {
        private readonly SyncApplyFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static StudyTask CloneTask(StudyTask src, Action<StudyTask>? edit = null)
        {
            var copy = new StudyTask
            {
                MaTask = src.MaTask,
                MaMonHoc = src.MaMonHoc,
                TenTask = src.TenTask,
                HanChot = src.HanChot,
                TrangThai = src.TrangThai,
                LoaiTask = src.LoaiTask,
                DoKho = src.DoKho,
                ThoiGianDaHoc = src.ThoiGianDaHoc,
                NgayHoanThanh = src.NgayHoanThanh,
            };
            edit?.Invoke(copy);
            return copy;
        }

        // ------------------------------------------------------------------ G. rollback

        /// <summary>
        /// G — when the baseline save (the second save inside the operation's transaction) fails, the
        /// whole operation rolls back: the domain write from the FIRST save is not committed either, and
        /// no baseline row survives. Without one transaction spanning both saves, the row would be
        /// committed with a bumped Rev and no matching baseline, and the enumerator would re-offer it to
        /// the peer forever.
        /// </summary>
        [Fact]
        public async Task G_WhenTheBaselineSaveFails_TheWholeOperationRollsBack()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task, rev: 1);

            var before = await _fx.ReadTaskAsync(task.MaTask);

            var remote = CloneTask(task, t => t.TenTask = "must not survive");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            FailingSaveDbContext? created = null;
            var session = new SyncApplySession(() => created = _fx.NewFailingContext(failOnSaveNumber: 2));

            var report = await session.ApplyAsync(SyncApplyFixture.From(remote));
            var result = Assert.Single(report.Results);

            Assert.Equal(SyncApplyOutcome.Failed, result.Outcome);
            Assert.Equal(SyncApplyReason.Exception, result.Reason);
            Assert.Contains("injected failure on save #2", result.Error!.Message);

            // The first save really did run — otherwise this test would prove nothing about rollback.
            Assert.Equal(2, created!.SaveAttempts);

            // ...and none of it is committed.
            var after = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(before!.TenTask, after!.TenTask);
            Assert.Equal(before.Rev, after.Rev);
            Assert.Equal(before.ModifiedByDeviceId, after.ModifiedByDeviceId);

            var baseline = await _fx.ReadBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.StudyTask, task.MaTask);
            Assert.Equal(1, baseline!.Rev);      // still the pre-apply baseline
        }

        /// <summary>
        /// G (context lifetime) — the failed operation's context is disposed, not handed on. This is the
        /// structural half of the R2 fix: a disposed context cannot carry a dirty or stale-but-clean
        /// instance into the next operation's transaction, which is how Case A commits the wrong
        /// provenance and how Case B hands a stale row to identity resolution.
        /// </summary>
        [Fact]
        public async Task G_FailedOperationContext_IsDisposed()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "x");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            FailingSaveDbContext? created = null;
            var session = new SyncApplySession(() => created = _fx.NewFailingContext(failOnSaveNumber: 2));
            await session.ApplyAsync(SyncApplyFixture.From(remote));

            Assert.True(created!.WasDisposed);
        }

        /// <summary>
        /// G (per-operation isolation) — each logical operation gets its OWN context, so a failure in one
        /// cannot touch another. Two independent operations, the first failing: the second still commits,
        /// and the two ran on different context instances.
        /// </summary>
        [Fact]
        public async Task G_EachOperationGetsItsOwnContext_SoAFailureCannotSpread()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var otherTask = new StudyTask("Other", new DateTime(2026, 3, 3), LoaiCongViec.BaiTapVeNha, 1)
            {
                MaMonHoc = task.MaMonHoc,
            };
            await _fx.AddLocalAsync(otherTask);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, otherTask);

            var remoteA = CloneTask(task, t => t.TenTask = "A changed");
            var remoteB = CloneTask(otherTask, t => t.TenTask = "B changed");
            SyncApplyFixture.Stamp(remoteA, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            SyncApplyFixture.Stamp(remoteB, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            // Only the FIRST context created fails; subsequent ones are healthy.
            var contexts = new System.Collections.Generic.List<AppDbContext>();
            var session = new SyncApplySession(() =>
            {
                AppDbContext ctx = contexts.Count == 0 ? _fx.NewFailingContext(2) : _fx.NewContext();
                contexts.Add(ctx);
                return ctx;
            });

            var report = await session.ApplyAsync(SyncApplyFixture.From(remoteA, remoteB));

            Assert.Equal(2, contexts.Count);
            Assert.NotSame(contexts[0], contexts[1]);

            Assert.Single(report.Failures);
            var committed = report.Results.Single(r => r.Committed);
            Assert.Equal(SyncApplyOutcome.Applied, committed.Outcome);

            // Whichever one was planned second survived; the other is untouched.
            var a = await _fx.ReadTaskAsync(task.MaTask);
            var b = await _fx.ReadTaskAsync(otherTask.MaTask);
            Assert.Equal(1, report.Results.Count(r => r.Outcome == SyncApplyOutcome.Failed));
            Assert.True(a!.TenTask == "A changed" ^ b!.TenTask == "B changed",
                        "exactly one of the two independent operations must have committed");
        }

        // ------------------------------------------------------------------ H. fresh-context retry

        /// <summary>
        /// H — after a failed operation, a retry on a FRESH context reloads authoritative database state
        /// and produces the correct final result: Rev advances exactly ONE step from the persisted value,
        /// and the provenance is the remote's.
        /// <para>
        /// The Rev assertion is the discriminating one. In the failed attempt the in-memory instance
        /// already reached Rev 2; had the session reused that context (or had the retry been rebuilt from
        /// surviving in-memory state) the row would land on Rev 3 — R2 Case A's "duplicate revision"
        /// exactly. Asserting only "the retry succeeded" would pass against that bug.
        /// </para>
        /// </summary>
        [Fact]
        public async Task H_RetryOnAFreshContext_ReloadsDbState_AndDoesNotDuplicateTheRevision()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.TenTask = "applied on retry");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            // --- attempt 1: fails on the baseline save
            FailingSaveDbContext? failed = null;
            var failing = new SyncApplySession(() => failed = _fx.NewFailingContext(failOnSaveNumber: 2));
            Assert.Equal(SyncApplyOutcome.Failed,
                         Assert.Single((await failing.ApplyAsync(SyncApplyFixture.From(remote))).Results).Outcome);
            Assert.True(failed!.WasDisposed);
            Assert.Equal(1, (await _fx.ReadTaskAsync(task.MaTask))!.Rev);   // DB still pre-apply

            // --- attempt 2: a brand-new session, a brand-new context, state reloaded from the database
            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            Assert.Equal(SyncApplyOutcome.Applied, Assert.Single(report.Results).Outcome);

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal("applied on retry", row!.TenTask);
            Assert.Equal(2, row.Rev);                                        // 1 -> 2, NOT 3
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);
            Assert.Equal(SyncApplyFixture.RemoteLater, DateTime.SpecifyKind(row.ModifiedAtUtc, DateTimeKind.Utc));

            var baseline = await _fx.ReadBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.StudyTask, task.MaTask);
            Assert.Equal(row.Rev, baseline!.Rev);

            // The retry converged: nothing is re-offered to the peer.
            using var ctx = _fx.NewContext();
            var changes = await SyncChangeEnumerator.GetChangesForPeerAsync(ctx, SyncApplyFixture.PeerDevice);
            Assert.DoesNotContain(changes.StudyTasks, c => c.EntityId == task.MaTask);
        }

        /// <summary>
        /// H (first-save failure) — the other failure shape: when the very first save fails, no Rev was
        /// ever persisted, and the retry still lands on exactly one increment. This is the Case A shape
        /// (throw before anything reaches the database) carried through to a real retry.
        /// </summary>
        [Fact]
        public async Task H_WhenTheFirstSaveFails_RetryStillProducesExactlyOneIncrement()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task, t => t.DoKho = 7);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var failing = new SyncApplySession(() => _fx.NewFailingContext(failOnSaveNumber: 1));
            Assert.Equal(SyncApplyOutcome.Failed,
                         Assert.Single((await failing.ApplyAsync(SyncApplyFixture.From(remote))).Results).Outcome);
            Assert.Equal(1, (await _fx.ReadTaskAsync(task.MaTask))!.Rev);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var row = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(7, row!.DoKho);
            Assert.Equal(2, row.Rev);
            Assert.Equal(SyncApplyFixture.PeerDevice, row.ModifiedByDeviceId);
        }

        /// <summary>
        /// A rejection must leave the database exactly as it was. Rolling back on the rejection path as
        /// well as the exception path is what makes that a guarantee rather than a property of whichever
        /// early return happened to fire.
        /// </summary>
        [Fact]
        public async Task RejectedOperation_CommitsNothing()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetRawBaselineAsync(SyncApplyFixture.PeerDevice, SyncEntityTypes.StudyTask, task.MaTask, 1, null);

            var before = await _fx.ReadTaskAsync(task.MaTask);
            var remote = CloneTask(task, t => t.TenTask = "nope");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var result = Assert.Single(report.Results);

            Assert.Equal(SyncApplyOutcome.Rejected, result.Outcome);
            Assert.Equal(SyncApplyReason.BaselineUnreadable, result.Reason);

            var after = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(before!.Rev, after!.Rev);
            Assert.Equal(before.TenTask, after.TenTask);
            Assert.Empty(await _fx.ReadConflictsAsync());
        }
    }
}
