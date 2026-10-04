using System;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — <b>E-2</b>, measured 2026-10-03 (OBSERVED: the row was re-stamped
    /// and drifted), then ruled (c) in <c>docs/specs/2026-10-03-fence-slice4-followup-owner-rulings.md</c>
    /// §1: a save whose only changed values are Derived persists them without stamping. The tests below
    /// pin the ruled behaviour.
    /// <para>
    /// Question: on a StudyTask held live at Base by an Unresolved record, does a save that changes
    /// ONLY Derived fields (<c>DiemUuTien</c>, <c>MucDoCanhBao</c> — <c>MergeSurfaceRegistry</c>
    /// <c>FieldClass.Derived</c>) still write the row, and so drift it away from the record's
    /// <c>BaseFingerprint</c>? The planner emits no intent for a Derived-only change (by construction,
    /// <see cref="SemesterReconcilePlannerIntentTests.DerivedOnlyChange_EmitsNoIntent"/>), so the
    /// fence has nothing to route and the save passes.
    /// </para>
    /// </summary>
    public class DerivedOnlySaveDriftObservationTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;
        private readonly ITestOutputHelper _out;

        public DerivedOnlySaveDriftObservationTests(ITestOutputHelper output)
        {
            _out = output;
            _save = new LocalSaveDriver(_fence.Fx);
        }

        public void Dispose() => _fence.Dispose();

        private sealed record Observation(long Rev, DateTime ModifiedAtUtc, string ModifiedBy, double DiemUuTien, string MucDoCanhBao, bool BaseMatches);

        private async Task<Observation> ObserveAsync(SyncConflictRecordRow record, Guid taskId, string label)
        {
            var t = (await _fence.Fx.ReadTaskAsync(taskId))!;
            var o = new Observation(t.Rev, t.ModifiedAtUtc, t.ModifiedByDeviceId, t.DiemUuTien, t.MucDoCanhBao,
                await _save.BaseStillMatchesAsync(record, taskId));
            _out.WriteLine($"{label}: {o}");
            return o;
        }

        /// <summary>Control: the identical load-and-save with no change at all writes nothing.</summary>
        [Fact]
        public async Task E2_Control_NoChangeSave_OnHeldRow_NoDrift()
        {
            var (record, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var before = await ObserveAsync(record, task.MaTask, "before");

            await _save.SaveAsync(await _save.LoadAsync(hocKy.MaHocKy));

            var after = await ObserveAsync(record, task.MaTask, "after");
            Assert.Equal(before, after);
            Assert.True(after.BaseMatches);
        }

        [Fact]
        public async Task E2_DiemUuTienOnlySave_OnHeldRow_NotStamped_NoDrift()
        {
            var (record, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var before = await ObserveAsync(record, task.MaTask, "before");
            Assert.True(before.BaseMatches);

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).DiemUuTien = before.DiemUuTien + 42.5;
            await _save.SaveAsync(graph);                                           // passes: no intent to route

            var after = await ObserveAsync(record, task.MaTask, "after");
            Assert.Equal(before.DiemUuTien + 42.5, after.DiemUuTien);              // the Derived value was written
            Assert.Equal(before.Rev, after.Rev);                                   // E-2 (c): not stamped
            Assert.Equal(before.ModifiedAtUtc, after.ModifiedAtUtc);
            Assert.Equal(before.ModifiedBy, after.ModifiedBy);
            Assert.True(after.BaseMatches);                                        // no Base drift on the held row
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
        }

        [Fact]
        public async Task E2_MucDoCanhBaoOnlySave_OnHeldRow_NotStamped_NoDrift()
        {
            var (record, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var before = await ObserveAsync(record, task.MaTask, "before");

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).MucDoCanhBao = before.MucDoCanhBao == "Khẩn cấp" ? "An toàn" : "Khẩn cấp";
            await _save.SaveAsync(graph);

            var after = await ObserveAsync(record, task.MaTask, "after");
            Assert.NotEqual(before.MucDoCanhBao, after.MucDoCanhBao);
            Assert.Equal(before.Rev, after.Rev);
            Assert.Equal(before.ModifiedAtUtc, after.ModifiedAtUtc);
            Assert.True(after.BaseMatches);
        }

        /// <summary>A priority engine with a fixed answer, so the VM's recompute is deterministic.</summary>
        private sealed class FixedPriorityEngine : IDecisionEngine
        {
            private readonly double _priority;
            public FixedPriorityEngine(double priority) => _priority = priority;
            public WeightConfig Config { get; } = new();
            public double CalculatePriority(StudyTask task, MonHoc monHoc) => _priority;
            public int CalculateRawSuggestedMinutes(StudyTask task) => 0;
            public string SuggestStudyTime(StudyTask task) => "0 phút";
            public StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc) => new(0, false, 0f);
            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default) => Task.FromResult<WeightConfigSuggestion?>(null);
        }

        /// <summary>
        /// The production path the E-2 question is about: <c>QuanLyTaskViewModel</c>'s constructor
        /// (<c>TinhDiemVaSapXep</c>) recomputes <c>DiemUuTien</c>/<c>MucDoCanhBao</c> for every task of
        /// the MonHoc it opens, on the SHARED graph. Any later save from that page then writes those
        /// Derived columns. Here the engine is a fixed stub (82 ⇒ "Khẩn cấp"); whether the real engine's
        /// value differs from the stored one on a given day depends on the clock and the deadline
        /// (INFERENCE — not measured with the real engine). Under E-2 (c) the values land, unstamped.
        /// </summary>
        [Fact]
        public async Task E2_QuanLyTaskViewModelOpen_ThenAnySave_DoesNotDriftHeldRow()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var before = await ObserveAsync(record, task.MaTask, "before");
            var graph = await _save.LoadAsync(hocKy.MaHocKy);

            _ = new QuanLyTaskViewModel(graph, LocalSaveDriver.Mon(graph, monHocA.MaMonHoc), _save.Repo,
                new FakeTaskEditorRepository(), new FixedPriorityEngine(82),
                new ParsingOrchestrator(new FakeClock(new DateTime(2026, 9, 10))));

            var held = LocalSaveDriver.Task(graph, task.MaTask);
            Assert.Equal(82, held.DiemUuTien);                                     // recomputed in memory by opening the page
            Assert.Equal("Khẩn cấp", held.MucDoCanhBao);

            await _save.SaveAsync(graph);                                          // e.g. a sibling's HoanThanhTask/XoaTask

            var after = await ObserveAsync(record, task.MaTask, "after");
            Assert.Equal(82, after.DiemUuTien);
            Assert.Equal(before.Rev, after.Rev);
            Assert.Equal(before.ModifiedAtUtc, after.ModifiedAtUtc);
            Assert.True(after.BaseMatches);
        }
    }
}
