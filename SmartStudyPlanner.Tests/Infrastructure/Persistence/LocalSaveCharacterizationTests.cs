using System;
using System.Linq;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence
{
    /// <summary>
    /// Epic 2 / T2.4 — <b>P0-c measurement</b> (plan §16.3 P0-c): the LOCAL-origin leg of W-2 / F-2
    /// (docs/review/2026-09-13-w2-f2-semantic-analysis.md §2.3/§2.5). Characterization only — these tests
    /// pin what the current code OBSERVED-ly does on 2026-09-30 and assert no desired semantic.
    /// <para>
    /// <b>Consumer:</b> Slice 4 (plan §21). This is the <b>P-CR-3 RED baseline</b>: once the fence is wired
    /// into the local save executor, the delete of the Base parent must become <c>Blocked</c> and
    /// <see cref="P0c_LocalDeleteOfBaseParent_ObservedBehaviour"/> is flipped in that PR.
    /// </para>
    /// <para>
    /// The delete goes through the same public entry points the UI uses (<c>QuanLyMonHocViewModel.XoaMon</c>:
    /// load the graph, drop the MonHoc from the in-memory graph, <c>LuuHocKyAsync</c> the whole HocKy),
    /// not through <c>TaskCascadeHelper</c> or any Slice-3-internal symbol, so the test stays valid across
    /// the <c>LuuHocKyAsync</c> extraction happening in parallel.
    /// </para>
    /// <para>
    /// Difference from W-2 Appendix A (recorded, not corrected): the appendix also baselines the Base
    /// parent <c>A</c> for the peer and the sync path does a per-peer baseline advance; the local path
    /// consults no baselines, and <see cref="FenceScenarioFixture.StageS1CrAsync"/> baselines only T.
    /// </para>
    /// </summary>
    public class LocalSaveCharacterizationTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();
        private readonly ITestOutputHelper _out;

        public LocalSaveCharacterizationTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => _fence.Dispose();

        private sealed record Observation(
            bool TaskIsDeleted, Guid TaskMaMonHoc, long TaskRev, string TaskModifiedBy,
            ConflictRecordStatus RecordStatus, bool BaseFingerprintMatches, int RecordCount);

        private async Task<Observation> ObserveAsync(Guid taskId, Guid recordId)
        {
            var task = (await _fence.Fx.ReadTaskAsync(taskId))!;
            var records = await _fence.Fx.ReadConflictsAsync();
            var record = records.Single(r => r.ConflictId == recordId);
            var snapshot = IncomingChanges.Of(task).Snapshot;
            var o = new Observation(task.IsDeleted, task.MaMonHoc, task.Rev, task.ModifiedByDeviceId,
                record.Status, SyncBaseFingerprint.Matches(record.BaseFingerprint, snapshot), records.Count);
            _out.WriteLine(o.ToString());
            return o;
        }

        /// <summary>XoaMon-equivalent: load the graph as the UI does, drop one MonHoc, re-save the HocKy.</summary>
        private async Task DeleteMonHocViaLocalSaveAsync(Guid hocKyId, Guid monHocId)
        {
            var repo = new SqliteHocKyRepository(_fence.Fx.Factory);
            var loaded = (await repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKyId);
            var victim = loaded.DanhSachMonHoc.Single(m => m.MaMonHoc == monHocId);
            loaded.DanhSachMonHoc.Remove(victim);
            await repo.LuuHocKyAsync(loaded);
        }

        /// <summary>
        /// P0-c — <b>OBSERVED 2026-09-30</b>: with an Unresolved S1-CR record held live at Base on task T
        /// (Base parent A, local candidate B, remote candidate C), a local <c>XoaMon(A)</c> save
        /// tombstones T through the local cascade. The record stays <c>Unresolved</c> and
        /// <c>SyncBaseFingerprint.Matches(record.BaseFingerprint, snapshot(T))</c> flips from
        /// <c>true</c> to <c>false</c> — the D9-T1 "live = Base while Unresolved" invariant is broken,
        /// nothing consulted the conflict record, and no new conflict row was written. This matches the
        /// INFERENCE in W-2 §2.5 ("parent tombstoned by a local UI delete: same end state"); it is now
        /// MEASURED for the local path. Not a desired semantic: P-CR-3 (Slice 4) makes this Blocked.
        /// <para>
        /// Discriminator: <see cref="P0c_Control_LocalDeleteOfLocalCandidateParent_LeavesBaseHeld"/> runs
        /// the identical staging and the identical save shape with a different MonHoc removed and gets
        /// the opposite result on every assertion.
        /// </para>
        /// </summary>
        [Fact]
        public async Task P0c_LocalDeleteOfBaseParent_ObservedBehaviour()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();

            // Preconditions (S0): T live, held at Base parent A, fingerprint matches, record Unresolved.
            var before = await ObserveAsync(task.MaTask, record.ConflictId);
            Assert.False(before.TaskIsDeleted);
            Assert.Equal(monHocA.MaMonHoc, before.TaskMaMonHoc);
            Assert.True(before.BaseFingerprintMatches);
            Assert.Equal(ConflictRecordStatus.Unresolved, before.RecordStatus);
            Assert.Equal(1, before.RecordCount);

            await DeleteMonHocViaLocalSaveAsync(hocKy.MaHocKy, monHocA.MaMonHoc);

            var after = await ObserveAsync(task.MaTask, record.ConflictId);
            Assert.True((await _fence.Fx.ReadMonHocAsync(monHocA.MaMonHoc))!.IsDeleted); // the delete itself landed
            Assert.True(after.TaskIsDeleted);                                            // T tombstoned (cascade)
            Assert.Equal(monHocA.MaMonHoc, after.TaskMaMonHoc);                          // still points at A
            Assert.True(after.TaskRev > before.TaskRev);
            Assert.Equal(SyncApplyFixture.LocalDevice, after.TaskModifiedBy);            // same device as before staging -> NOT discriminating alone; the Rev bump above is
            Assert.Equal(ConflictRecordStatus.Unresolved, after.RecordStatus);           // record untouched
            Assert.False(after.BaseFingerprintMatches);                                  // D9-T1 no longer holds
            Assert.Equal(1, after.RecordCount);                                          // nothing was staged or resolved
        }

        /// <summary>
        /// P0-c control — <b>OBSERVED 2026-09-30</b>: same staging, same <c>XoaMon</c> save shape, but the
        /// removed MonHoc is B (the LOCAL candidate parent, which T no longer points at after staging held
        /// it at Base). T is untouched, still live at A, the fingerprint still matches. W-2 §2.5 predicts
        /// exactly this ("Local or Remote candidate's parent tombstoned: No crossing"). It proves the
        /// P0-c observation is caused by deleting the BASE parent and not by "any XoaMon on this HocKy".
        /// </summary>
        [Fact]
        public async Task P0c_Control_LocalDeleteOfLocalCandidateParent_LeavesBaseHeld()
        {
            var (record, hocKy, monHocA, monHocB, _, task) = await _fence.StageS1CrAsync();
            var before = await ObserveAsync(task.MaTask, record.ConflictId);
            Assert.True(before.BaseFingerprintMatches);

            await DeleteMonHocViaLocalSaveAsync(hocKy.MaHocKy, monHocB.MaMonHoc);

            var after = await ObserveAsync(task.MaTask, record.ConflictId);
            Assert.True((await _fence.Fx.ReadMonHocAsync(monHocB.MaMonHoc))!.IsDeleted); // the delete itself landed
            Assert.False(after.TaskIsDeleted);
            Assert.Equal(monHocA.MaMonHoc, after.TaskMaMonHoc);
            Assert.True(after.BaseFingerprintMatches);
            Assert.Equal(ConflictRecordStatus.Unresolved, after.RecordStatus);
            Assert.Equal(1, after.RecordCount);
        }
    }
}
