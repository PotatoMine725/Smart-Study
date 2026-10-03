using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — what happens around a rejection: OD-7's in-memory restoration
    /// (owner ruling 2026-10-03: R2, in place, identity-preserving, from the old graph loaded in the
    /// same transaction), the sticky-sibling case it exists for (X-20), no retry after resolution
    /// (X-13), no resurrection by save (X-15), and the two OBSERVED consequences the owner asked to be
    /// pinned (OQ-1 clone reappearance; OQ-3 create-branch restore, an engineering decision).
    /// </summary>
    public class LocalSemesterSaveRejectionLifecycleTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;

        public LocalSemesterSaveRejectionLifecycleTests() => _save = new LocalSaveDriver(_fence.Fx);

        public void Dispose() => _fence.Dispose();

        private SyncApplyFixture Fx => _fence.Fx;

        private async Task<StudyTask> AddTaskAsync(Guid monHocId, string ten = "sibling")
        {
            var task = new StudyTask(ten, new DateTime(2026, 2, 9), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHocId };
            await Fx.AddLocalAsync(task);
            return task;
        }

        /// <summary>
        /// X-20 — OD-7 ruling (2026-09-14). One in-memory graph, the way one VM session holds it: delete
        /// the held task T ⇒ rejected and surfaced; the graph is back to persisted state (T present
        /// again, the SAME MonHoc and sibling instances); then edit sibling T2 in that same graph ⇒ the
        /// save persists, T stays live and held, so no <c>Tombstone(T)</c> was resubmitted.
        /// Mutant: drop the restoration step ⇒ the second save re-derives <c>Tombstone(T)</c> and is
        /// rejected again (sticky rejection).
        /// </summary>
        [Fact]
        public async Task X20_RejectedDelete_RestoresGraph_ThenSiblingEditInSameGraphPersists()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var sibling = await AddTaskAsync(monHocA.MaMonHoc);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var monInstances = graph.DanhSachMonHoc.ToList();
            var monAInstance = LocalSaveDriver.Mon(graph, monHocA.MaMonHoc);
            var siblingInstance = LocalSaveDriver.Task(graph, sibling.MaTask);

            LocalSaveDriver.RemoveTask(graph, task.MaTask);
            var ex = await _save.SaveExpectingRejectionAsync(graph);

            Assert.Equal("S1CR.SubjectRemoved", LocalSaveDriver.SingleBlocking(ex).RuleId);
            Assert.True(LocalSaveDriver.Contains(graph, task.MaTask));
            Assert.Equal(monInstances, graph.DanhSachMonHoc.ToList());           // same instances, same order
            Assert.Same(monAInstance, LocalSaveDriver.Mon(graph, monHocA.MaMonHoc));
            Assert.Same(siblingInstance, LocalSaveDriver.Task(graph, sibling.MaTask));
            Assert.Contains(LocalSaveDriver.Task(graph, task.MaTask), monAInstance.DanhSachTask);
            await _save.AssertGraphMatchesPersistedAsync(graph);

            LocalSaveDriver.Task(graph, sibling.MaTask).TenTask = "sibling edited after the rejection";
            await _save.SaveAsync(graph);

            Assert.Equal("sibling edited after the rejection", (await Fx.ReadTaskAsync(sibling.MaTask))!.TenTask);
            Assert.False((await Fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
        }

        /// <summary>Restoration reverts a rejected scalar edit and keeps NotMapped fields (OD-7 + R2 design).</summary>
        [Fact]
        public async Task RejectedFieldEdit_RevertsMappedScalars_KeepsNotMappedFields()
        {
            var (_, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            graph.NgayKetThuc = new DateTime(2026, 6, 30);                       // [NotMapped], UI-owned
            graph.IsNgayKetThucAuto = false;
            graph.Ten = "renamed in the same rejected save";
            var held = LocalSaveDriver.Task(graph, task.MaTask);
            held.TenTask = "edited while held";

            _ = await _save.SaveExpectingRejectionAsync(graph);

            Assert.Same(held, LocalSaveDriver.Task(graph, task.MaTask));
            Assert.Equal(task.TenTask, held.TenTask);
            Assert.Equal(hocKy.Ten, graph.Ten);                                   // bundled edits are not kept (rulings §2.4)
            Assert.Equal(new DateTime(2026, 6, 30), graph.NgayKetThuc);
            Assert.False(graph.IsNgayKetThucAuto);
            await _save.AssertGraphMatchesPersistedAsync(graph);
        }

        /// <summary>Restoration drops an unsaved addition bundled into the rejected save.</summary>
        [Fact]
        public async Task RejectedSave_DropsUnsavedAdditions()
        {
            var (_, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var added = new StudyTask("unsaved", new DateTime(2026, 4, 4), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHocA.MaMonHoc };
            LocalSaveDriver.Mon(graph, monHocA.MaMonHoc).DanhSachTask.Add(added);
            graph.DanhSachMonHoc.Add(new MonHoc("unsaved subject", 2));
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            _ = await _save.SaveExpectingRejectionAsync(graph);

            Assert.False(LocalSaveDriver.Contains(graph, added.MaTask));
            Assert.DoesNotContain(graph.DanhSachMonHoc, m => m.TenMonHoc == "unsaved subject");
            await _save.AssertGraphMatchesPersistedAsync(graph);
        }

        /// <summary>
        /// Owner requirement 5 (2026-10-03): if the restoration itself throws, the user still gets the
        /// <see cref="MutationRejectedException"/> with its rule ids; the restore failure rides along in
        /// <c>Exception.Data</c> (the frozen Sync/Fence type has no inner-exception constructor). The
        /// failure is forced through a CollectionChanged subscriber -- a UI binding that throws -- so no
        /// production seam exists for it. The graph shape after a partial restore is not asserted.
        /// </summary>
        [Fact]
        public async Task RestoreFailure_StillSurfacesTheRejection_WithTheRestoreErrorAttached()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, task.MaTask);
            LocalSaveDriver.Mon(graph, monHocA.MaMonHoc).DanhSachTask.CollectionChanged +=
                (_, _) => throw new InvalidOperationException("forced restore failure");

            var before = await _fence.SnapshotAllTablesAsync();
            var ex = await _save.SaveExpectingRejectionAsync(graph);

            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            Assert.Contains("S1CR.SubjectRemoved", ex.Message, StringComparison.Ordinal);
            Assert.Contains(record.ConflictId.ToString(), ex.Message, StringComparison.Ordinal);
            var restoreFailure = Assert.IsType<InvalidOperationException>(LocalSaveRejection.RestoreFailureOf(ex));
            Assert.Equal("forced restore failure", restoreFailure.Message);
            Assert.Contains("Không khôi phục được", LocalSaveRejection.UserMessage(ex), StringComparison.Ordinal);
        }

        /// <summary>
        /// X-13 — no automatic retry after resolution. P-CR-3 is rejected, the record is then resolved
        /// KeepBase. Nothing replays the rejected MonHoc delete: the parent stays live with T under it,
        /// and the restored graph, saved again, is a no-op. (Plan §12.2's
        /// <c>BlockedDelete_ThenResolveKeepBase_ParentStillLive</c>.) Without restoration the next save
        /// would now PASS and delete A -- a deferred delete nobody asked for again.
        /// </summary>
        [Fact]
        public async Task X13_BlockedDelete_ThenResolveKeepBase_ParentStillLive_NothingReplayed()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var sibling = await AddTaskAsync(monHocA.MaMonHoc);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveMon(graph, monHocA.MaMonHoc);
            _ = await _save.SaveExpectingRejectionAsync(graph);

            var outcome = await new ConflictResolver(Fx.Factory)
                .ResolveAsync(record.ConflictId, new ResolutionRequest(ResolutionKind.KeepBase, null, null));
            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);

            Assert.False((await Fx.ReadMonHocAsync(monHocA.MaMonHoc))!.IsDeleted);
            var live = (await Fx.ReadTaskAsync(task.MaTask))!;
            Assert.False(live.IsDeleted);
            Assert.Equal(monHocA.MaMonHoc, live.MaMonHoc);
            Assert.False((await Fx.ReadTaskAsync(sibling.MaTask))!.IsDeleted);

            Assert.Contains(graph.DanhSachMonHoc, m => m.MaMonHoc == monHocA.MaMonHoc);
            var beforeResave = await _fence.SnapshotAllTablesAsync();
            await _save.SaveAsync(graph);
            Assert.Equal(beforeResave, await _fence.SnapshotAllTablesAsync());
        }

        /// <summary>
        /// X-15 — no resurrection by save. A stale graph still carrying a task that was tombstoned since
        /// it was loaded cannot bring the row back. CHARACTERIZATION of D-2 (2026-10-01): the live-only
        /// load no longer sees the dead id, so the planner treats it as new, the fence passes it (no
        /// record), and the writer's Add collides on the primary key -- the save fails loudly, rolls
        /// back, and the row stays dead with its Rev untouched.
        /// </summary>
        [Fact]
        [Trait("Kind", "Characterization")]
        public async Task X15_StaleGraphWithTombstonedTask_DoesNotResurrectIt()
        {
            // CHARACTERIZATION — pins status quo (D-2 / D-4); not evidence of a ruling
            var (hocKy, monHoc, _) = await Fx.SeedTreeAsync();
            var doomed = await AddTaskAsync(monHoc.MaMonHoc, "doomed");
            var stale = await _save.LoadAsync(hocKy.MaHocKy);
            var fresh = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(fresh, doomed.MaTask);
            await _save.SaveAsync(fresh);
            var dead = (await Fx.ReadTaskAsync(doomed.MaTask))!;
            Assert.True(dead.IsDeleted);

            var before = await _fence.SnapshotAllTablesAsync();
            await Assert.ThrowsAsync<DbUpdateException>(() => _save.SaveAsync(stale));

            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            var after = (await Fx.ReadTaskAsync(doomed.MaTask))!;
            Assert.True(after.IsDeleted);
            Assert.Equal(dead.Rev, after.Rev);
        }

        /// <summary>
        /// OQ-1 (owner ruling 2026-10-03: option (i), restore to the raw live rows = persisted state).
        /// <b>OBSERVED consequence, pinned on request:</b> after a rejection, a MonHoc clone that
        /// <c>LayDanhSachHocKyAsync</c> had folded into its representative reappears in the caller's
        /// graph with its own task moved back under it; the next load folds it away again. The
        /// blocking record sits on T under an unrelated MonHoc, so the clone pair itself is not what
        /// was rejected.
        /// </summary>
        [Fact]
        public async Task OQ1_Rejection_RestoresRawRows_FoldedCloneReappearsUntilNextLoad_Observed()
        {
            var (_, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var first = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var second = new MonHoc("  toán ", 3) { MaHocKy = hocKy.MaHocKy };
            await Fx.AddLocalAsync(first);
            await Fx.AddLocalAsync(second);
            var firstTask = await AddTaskAsync(first.MaMonHoc, "under first");
            var secondTask = await AddTaskAsync(second.MaMonHoc, "under second");

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var survivor = Assert.Single(graph.DanhSachMonHoc, m => m.MaMonHoc == first.MaMonHoc || m.MaMonHoc == second.MaMonHoc);
            var loserId = survivor.MaMonHoc == first.MaMonHoc ? second.MaMonHoc : first.MaMonHoc;
            var loserTaskId = loserId == first.MaMonHoc ? firstTask.MaTask : secondTask.MaTask;
            Assert.Equal(survivor.MaMonHoc, LocalSaveDriver.Task(graph, loserTaskId).MaMonHoc);   // folded by the dedup

            LocalSaveDriver.RemoveTask(graph, task.MaTask);
            _ = await _save.SaveExpectingRejectionAsync(graph);

            var loser = Assert.Single(graph.DanhSachMonHoc, m => m.MaMonHoc == loserId);
            var loserTask = LocalSaveDriver.Task(graph, loserTaskId);
            Assert.Equal(loserId, loserTask.MaMonHoc);
            Assert.Contains(loserTask, loser.DanhSachTask);
            Assert.DoesNotContain(loserTask, survivor.DanhSachTask);
            await _save.AssertGraphMatchesPersistedAsync(graph);

            var reloaded = await _save.LoadAsync(hocKy.MaHocKy);
            Assert.DoesNotContain(reloaded.DanhSachMonHoc, m => m.MaMonHoc == loserId);
        }

        /// <summary>
        /// OQ-3 — ENGINEERING DECISION (not ruled): a rejected save of a never-persisted HocKy restores
        /// it to "persisted state", which has no MonHoc at all, so its MonHoc list is emptied; its own
        /// scalars are left as the caller typed them (there is no row to copy from). Reachable only
        /// through an identity collision with an AL-PT record, as here.
        /// </summary>
        [Fact]
        public async Task OQ3_RejectedCreateOfNewHocKy_EmptiesItsMonHocList_NoRowWritten()
        {
            var (record, _, absentId) = await _fence.StageAlPtAsync();
            var brandNew = new HocKy("new semester", new DateTime(2026, 9, 1));
            brandNew.DanhSachMonHoc.Add(new MonHoc("collides", 3) { MaMonHoc = absentId });

            var before = await _fence.SnapshotAllTablesAsync();
            var ex = await _save.SaveExpectingRejectionAsync(brandNew);

            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            Assert.Contains(ex.Decision.Results, r => r.RuleId == "ALPT.SameIdentityMaterialization" && r.ConflictId == record.ConflictId);
            Assert.Null(await Fx.ReadHocKyAsync(brandNew.MaHocKy));
            Assert.Empty(brandNew.DanhSachMonHoc);
            Assert.Equal("new semester", brandNew.Ten);
        }
    }
}
