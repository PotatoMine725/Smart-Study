using System;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// D-4 (owner 2026-10-03): a save in which no property value changed, made from a caller graph
    /// that has already been saved once. The writer copies the graph onto freshly loaded rows
    /// (<c>CopySyncSafeValues</c>); after a first save the graph still carries the pre-save
    /// <c>Rev</c>/<c>ModifiedAtUtc</c>, so the copy flags those properties modified even though the
    /// restore puts the persisted values back. <c>HocKyRepository_ResaveWithNoChanges_*</c> reloads a
    /// fresh graph between saves and so never exercises this pattern.
    /// <para>
    /// OBSERVED on origin/dev 3c49924 (commit 8387f03): the no-change save restamped the row, and on
    /// an S1-CR held row moved its live fingerprint a second time after E-2. Ruled as the vacuous case
    /// of E-2 (c) (rulings §1, clarified 2026-10-03): a save in which no value changed is not stamped.
    /// </para>
    /// <para>
    /// The second save runs on a second local identity (<see cref="Later"/>, <see cref="OtherDevice"/>)
    /// so a restamp of any of the three stamp columns is visible on its own.
    /// </para>
    /// </summary>
    public class LongLivedGraphNoChangeSaveTests : IDisposable
    {
        private static readonly DateTime Later = SyncApplyFixture.LocalNow.AddHours(1);
        private const string OtherDevice = "OTHER-LOCAL";

        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;
        private readonly SqliteHocKyRepository _laterRepo;
        private readonly ITestOutputHelper _out;

        public LongLivedGraphNoChangeSaveTests(ITestOutputHelper output)
        {
            _out = output;
            _save = new LocalSaveDriver(_fence.Fx);
            _laterRepo = new SqliteHocKyRepository(() => _fence.Fx.NewContext(Later, OtherDevice));
        }

        public void Dispose() => _fence.Dispose();

        private async Task<(long Rev, DateTime ModifiedAtUtc, string ModifiedBy)> ReadAsync(Guid taskId, string label)
        {
            var t = (await _fence.Fx.ReadTaskAsync(taskId))!;
            _out.WriteLine($"{label}: rev={t.Rev} mod={t.ModifiedAtUtc:O} by={t.ModifiedByDeviceId}");
            return (t.Rev, t.ModifiedAtUtc, t.ModifiedByDeviceId);
        }

        private async Task<string> LiveFingerprintAsync(Guid taskId) =>
            SyncBaseFingerprint.Of(IncomingChanges.Of((await _fence.Fx.ReadTaskAsync(taskId))!).Snapshot);

        [Fact]
        public async Task D4_NoChangeSave_FromPreviouslySavedGraph_IsNotStamped()
        {
            var (hocKy, _, task) = await _fence.Fx.SeedTreeAsync();

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).TenTask = "edited once";
            await _save.SaveAsync(graph);                                           // real Merge change: stamped
            var afterFirst = await ReadAsync(task.MaTask, "after first save");

            await _laterRepo.LuuHocKyAsync(graph);                                  // same graph, nothing changed
            var afterSecond = await ReadAsync(task.MaTask, "after no-change save");

            Assert.Equal(SyncApplyFixture.LocalNow, afterFirst.ModifiedAtUtc);
            Assert.Equal(afterFirst, afterSecond);                                  // Rev, ModifiedAtUtc, ModifiedBy unchanged
        }

        /// <summary>
        /// The held-row form. A graph loaded after staging is stale on the held row only if an earlier
        /// save from it stamped the row, and the only fence-passing edit on a held row is a Derived-only
        /// one (E-2) — so on origin/dev this drift compounded E-2. With E-2 (c) the Derived-only save
        /// is not stamped, the graph stays current, and neither save moves the held row.
        /// (The vacuous rule on its own is pinned by the test above; here the graph never goes stale.)
        /// </summary>
        [Fact]
        public async Task D4_NoChangeSave_AfterDerivedOnlySave_OnHeldRow_NoDrift()
        {
            var (record, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var staged = await ReadAsync(task.MaTask, "staged");
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).DiemUuTien += 7;
            await _save.SaveAsync(graph);                                           // Derived-only (E-2)
            var afterFirst = await ReadAsync(task.MaTask, "after Derived-only save");
            var fpAfterFirst = await LiveFingerprintAsync(task.MaTask);

            await _laterRepo.LuuHocKyAsync(graph);                                  // same graph, nothing changed
            var afterSecond = await ReadAsync(task.MaTask, "after no-change save");

            Assert.Equal(staged, afterFirst);                                       // E-2 (c): not stamped
            Assert.Equal(staged, afterSecond);                                      // vacuous case: not stamped
            Assert.Equal(fpAfterFirst, await LiveFingerprintAsync(task.MaTask));
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
        }

        /// <summary>
        /// E-2's purpose end to end (rulings §1 "Why": a drifted Base makes the record permanently
        /// unresolvable). The tests above stop at the D8-H fingerprint; this one resolves. A held row
        /// gets a Derived-only save and then a no-change save from the same graph, and KeepBase must
        /// still apply. The outcome is asserted first, with no fingerprint check before it, so a
        /// regression shows as the resolver's own <c>Rejected</c>. Review PR #110 F-3 / probe P6:
        /// on origin/dev 3c49924 (before PR #109) this resolve returns Rejected.
        /// </summary>
        [Fact]
        public async Task HeldRow_DerivedOnlyThenNoChangeSave_ThenResolveKeepBase_IsApplied()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).DiemUuTien += 7;
            LocalSaveDriver.Task(graph, task.MaTask).MucDoCanhBao = "Khẩn cấp";
            await _save.SaveAsync(graph);                                           // Derived-only (E-2)
            await _laterRepo.LuuHocKyAsync(graph);                                  // same graph, nothing changed

            var outcome = await new ConflictResolver(_fence.Fx.Factory)
                .ResolveAsync(record.ConflictId, new ResolutionRequest(ResolutionKind.KeepBase, null, null));
            _out.WriteLine($"outcome={outcome.Kind}");

            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);
            var live = (await _fence.Fx.ReadTaskAsync(task.MaTask))!;
            Assert.False(live.IsDeleted);
            Assert.Equal(monHocA.MaMonHoc, live.MaMonHoc);
            Assert.Equal(ConflictRecordStatus.Resolved, await _save.StatusOfAsync(record));
        }
    }
}
