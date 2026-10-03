using System;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
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
    /// The second save runs on a later clock (<see cref="Later"/>) so a restamp of
    /// <c>ModifiedAtUtc</c> is visible: with the fixture's fixed clock it would rewrite the same value.
    /// </para>
    /// </summary>
    [Trait("Kind", "Characterization")]
    public class LongLivedGraphNoChangeSaveTests : IDisposable
    {
        // CHARACTERIZATION — OBSERVED on origin/dev 3c49924, before the E-2/D-4 stamper change
        private static readonly DateTime Later = SyncApplyFixture.LocalNow.AddHours(1);

        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;
        private readonly SqliteHocKyRepository _laterRepo;
        private readonly ITestOutputHelper _out;

        public LongLivedGraphNoChangeSaveTests(ITestOutputHelper output)
        {
            _out = output;
            _save = new LocalSaveDriver(_fence.Fx);
            _laterRepo = new SqliteHocKyRepository(() => _fence.Fx.NewContext(Later));
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
        public async Task D4_NoChangeSave_FromPreviouslySavedGraph_Observed()
        {
            var (hocKy, _, task) = await _fence.Fx.SeedTreeAsync();

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).TenTask = "edited once";
            await _save.SaveAsync(graph);                                           // real Merge change: stamped
            var afterFirst = await ReadAsync(task.MaTask, "after first save");

            await _laterRepo.LuuHocKyAsync(graph);                                  // same graph, nothing changed
            var afterSecond = await ReadAsync(task.MaTask, "after no-change save");

            Assert.Equal(afterFirst.Rev + 1, afterSecond.Rev);                     // OBSERVED: restamped
            Assert.Equal(Later, afterSecond.ModifiedAtUtc);
        }

        /// <summary>
        /// The held-row form. S1-CR resets the live row to Base, so a graph loaded before staging
        /// differs from the live row in content and its save is not a no-change save. A graph loaded
        /// after staging is stale on the held row only if an earlier save from it stamped the row, and
        /// the only fence-passing edit on a held row is a Derived-only one (E-2). So on origin/dev the
        /// held-row drift from a no-change save is a second drift that compounds E-2.
        /// </summary>
        [Fact]
        public async Task D4_NoChangeSave_AfterDerivedOnlySave_OnHeldRow_Observed()
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

            Assert.Equal(staged.Rev + 1, afterFirst.Rev);                          // OBSERVED: E-2 stamp
            Assert.Equal(afterFirst.Rev + 1, afterSecond.Rev);                     // OBSERVED: restamped again
            Assert.Equal(Later, afterSecond.ModifiedAtUtc);                        // provenance moved again
            Assert.NotEqual(fpAfterFirst, await LiveFingerprintAsync(task.MaTask));  // the no-change save itself drifts
            Assert.False(await _save.BaseStillMatchesAsync(record, task.MaTask));
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
        }
    }
}
