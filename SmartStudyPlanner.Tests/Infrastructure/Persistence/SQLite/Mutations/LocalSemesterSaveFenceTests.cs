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
    /// Epic 2 / T2.4 fence Slice 4 — plan §16.1 rows that are reachable through
    /// <c>LuuHocKyAsync</c>, run end to end on real SQLite: staged conflict record, VM-equivalent graph
    /// edit, real save. A Blocked row must leave every table byte-identical (rulings §5 item 1) and
    /// carry the rule id in the exception (item 3).
    /// <para>
    /// Not here, by construction: the editor rows (P-CR-5, P-PT-5, P-K-1, P-K-4, P-K-7 — Slice 5,
    /// <c>ITaskEditorRepository</c>), the router-only rows (P-AL-4, P-K-6 — Slice 2). P-PT rows use a
    /// local recipe with a LIVE Base parent (<see cref="StageS1PtLiveBaseParentAsync"/>), because the
    /// fixture's S1-PT recipe tombstones the Base parent, and a task under a dead MonHoc is never in the
    /// graph <c>LayDanhSachHocKyAsync</c> returns — no VM can address it.
    /// </para>
    /// </summary>
    public class LocalSemesterSaveFenceTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;

        public LocalSemesterSaveFenceTests() => _save = new LocalSaveDriver(_fence.Fx);

        public void Dispose() => _fence.Dispose();

        private SyncApplyFixture Fx => _fence.Fx;

        private async Task<StudyTask> AddTaskAsync(Guid monHocId, string ten = "sibling")
        {
            var task = new StudyTask(ten, new DateTime(2026, 2, 9), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHocId };
            await Fx.AddLocalAsync(task);
            return task;
        }

        private async Task AddNoteAndLinkAsync(Guid taskId)
        {
            await Fx.AddLocalAsync(new TaskNote { Id = Guid.NewGuid(), MaTask = taskId, Content = "note" });
            await Fx.AddLocalAsync(new TaskReferenceLink { MaTask = taskId, Title = "link", Url = "https://example.test/l" });
        }

        private async Task<(bool NoteLive, bool LinkLive)> ChildrenLiveAsync(Guid taskId)
        {
            using var ctx = Fx.NewContext();
            var note = await ctx.TaskNotes.AsNoTracking().SingleAsync(n => n.MaTask == taskId);
            var link = await ctx.TaskReferenceLinks.AsNoTracking().SingleAsync(l => l.MaTask == taskId);
            return (!note.IsDeleted, !link.IsDeleted);
        }

        /// <summary>
        /// S1-PT with a LIVE Base parent: the remote moved T to M2, which is tombstoned locally. The
        /// apply session holds T live at Base (under M) and stages StructuralConflict(ParentTombstoned)
        /// with Local and Base present (<c>SyncApplySession</c> parent handling, D9-T4).
        /// </summary>
        private async Task<(SyncConflictRecordRow Record, HocKy HocKy, MonHoc MonHoc, StudyTask Task)> StageS1PtLiveBaseParentAsync()
        {
            var (hocKy, monHoc, task) = await Fx.SeedTreeAsync();
            var deadTarget = new MonHoc("MH dead target", 2) { MaHocKy = hocKy.MaHocKy };
            await Fx.AddLocalAsync(deadTarget);
            await Fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = Fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE"))
            {
                ctx.MonHocs.Remove(await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == deadTarget.MaMonHoc));
                await ctx.SaveChangesAsync();
            }

            var remote = new StudyTask
            {
                MaTask = task.MaTask, MaMonHoc = deadTarget.MaMonHoc, TenTask = task.TenTask, HanChot = task.HanChot,
                TrangThai = task.TrangThai, LoaiTask = task.LoaiTask, DoKho = task.DoKho,
                ThoiGianDaHoc = task.ThoiGianDaHoc, NgayHoanThanh = task.NgayHoanThanh,
            };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await Fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = Assert.Single(await Fx.ReadConflictsAsync());
            Assert.Equal(StructuralReason.ParentTombstoned, record.StructuralReason);
            Assert.NotNull(record.LocalEntityId);
            var live = (await Fx.ReadTaskAsync(task.MaTask))!;
            Assert.False(live.IsDeleted);
            Assert.Equal(monHoc.MaMonHoc, live.MaMonHoc);                  // held at the live Base parent
            return (record, hocKy, monHoc, task);
        }

        private async Task<MutationRejectedException> AssertBlockedAndUnchangedAsync(HocKy graph)
        {
            var before = await _fence.SnapshotAllTablesAsync();
            var ex = await _save.SaveExpectingRejectionAsync(graph);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            return ex;
        }

        private static void AssertRule(MutationRejectedException ex, string ruleId, RoutingStage stage, Guid conflictId)
        {
            var blocking = LocalSaveDriver.SingleBlocking(ex);
            Assert.Equal(ruleId, blocking.RuleId);
            Assert.Equal(stage, blocking.Stage);
            Assert.Equal(conflictId, blocking.ConflictId);
            Assert.Contains(ruleId, ex.Message, StringComparison.Ordinal);
            Assert.Contains(conflictId.ToString(), ex.Message, StringComparison.Ordinal);
        }

        // ================================================================== S1-CR

        [Fact]
        public async Task PCR1_ReparentHeldTask_IsBlocked_EdgeReplaced()
        {
            var (record, hocKy, _, monHocB, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.MoveTask(graph, task.MaTask, monHocB.MaMonHoc);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1CR.EdgeReplaced", RoutingStage.DirectSubject, record.ConflictId);
        }

        [Fact]
        public async Task PCR2_DeleteHeldTask_IsBlocked_SubjectRemoved_TaskAndChildrenStayLive()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            await AddNoteAndLinkAsync(task.MaTask);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1CR.SubjectRemoved", RoutingStage.DirectSubject, record.ConflictId);
            var live = (await Fx.ReadTaskAsync(task.MaTask))!;
            Assert.False(live.IsDeleted);
            Assert.Equal(monHocA.MaMonHoc, live.MaMonHoc);
            Assert.Equal((true, true), await ChildrenLiveAsync(task.MaTask));
        }

        /// <summary>
        /// P-CR-3 — the slice's headline row and P0-c's flip target. Plan §12.2 names it
        /// <c>BlockedMonHocDelete_TaskStillUnderSameParent_NoRowChanged</c>.
        /// </summary>
        [Fact]
        public async Task PCR3_DeleteParentOfHeldTask_IsBlockedAtCascade_SiblingAlsoStaysLive()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var sibling = await AddTaskAsync(monHocA.MaMonHoc);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveMon(graph, monHocA.MaMonHoc);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1CR.SubjectRemoved", RoutingStage.CascadeReached, record.ConflictId);
            Assert.False((await Fx.ReadMonHocAsync(monHocA.MaMonHoc))!.IsDeleted);
            Assert.False((await Fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
            Assert.False((await Fx.ReadTaskAsync(sibling.MaTask))!.IsDeleted);       // atomic: nothing of the save landed
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
        }

        [Fact]
        public async Task PCR4_DeleteSiblingOfHeldTask_Passes_AndPersists_RecordUntouched()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var sibling = await AddTaskAsync(monHocA.MaMonHoc);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, sibling.MaTask);

            await _save.SaveAsync(graph);

            Assert.True((await Fx.ReadTaskAsync(sibling.MaTask))!.IsDeleted);
            Assert.False((await Fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
        }

        /// <summary>P-CR-6 — SB-3 ruling (Block), not a characterization.</summary>
        [Fact]
        public async Task PCR6_EditMergeFieldOfHeldTask_IsBlocked_NonStructuralFields_NoBaseDrift()
        {
            var (record, hocKy, _, _, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).TenTask = "edited while held";

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1CR.NonStructuralFields", RoutingStage.DirectSubject, record.ConflictId);
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
        }

        // ================================================================== S1-PT (live Base parent)

        [Fact]
        public async Task PPT1_ReparentHeldTask_IsBlocked_ParentFrameChanged()
        {
            var (record, hocKy, _, task) = await StageS1PtLiveBaseParentAsync();
            var other = new MonHoc("MH other", 1) { MaHocKy = hocKy.MaHocKy };
            await Fx.AddLocalAsync(other);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.MoveTask(graph, task.MaTask, other.MaMonHoc);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1PT.ParentFrameChanged", RoutingStage.DirectSubject, record.ConflictId);
        }

        [Fact]
        public async Task PPT2_DeleteHeldTask_IsBlocked_SubjectRemoved_TaskAndChildrenStayLive()
        {
            var (record, hocKy, monHoc, task) = await StageS1PtLiveBaseParentAsync();
            await AddNoteAndLinkAsync(task.MaTask);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1PT.SubjectRemoved", RoutingStage.DirectSubject, record.ConflictId);
            Assert.Equal(monHoc.MaMonHoc, (await Fx.ReadTaskAsync(task.MaTask))!.MaMonHoc);
            Assert.Equal((true, true), await ChildrenLiveAsync(task.MaTask));
        }

        [Fact]
        public async Task PPT3_DeleteParentOfHeldTask_IsBlockedAtCascade_SiblingAlsoStaysLive()
        {
            var (record, hocKy, monHoc, task) = await StageS1PtLiveBaseParentAsync();
            var sibling = await AddTaskAsync(monHoc.MaMonHoc);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveMon(graph, monHoc.MaMonHoc);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1PT.SubjectRemoved", RoutingStage.CascadeReached, record.ConflictId);
            Assert.False((await Fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
            Assert.False((await Fx.ReadTaskAsync(sibling.MaTask))!.IsDeleted);
        }

        [Fact]
        public async Task PPT4_DeleteSiblingOfHeldTask_Passes_AndPersists_RecordUntouched()
        {
            var (record, hocKy, monHoc, task) = await StageS1PtLiveBaseParentAsync();
            var sibling = await AddTaskAsync(monHoc.MaMonHoc);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, sibling.MaTask);

            await _save.SaveAsync(graph);

            Assert.True((await Fx.ReadTaskAsync(sibling.MaTask))!.IsDeleted);
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
        }

        /// <summary>
        /// P-PT-6 — SB-3 ruling (Block). Recipe deviation, stated: the plan's row uses the dead-Base-parent
        /// recipe, which is unreachable from a fresh load (see class header); the ruling under test, an
        /// UpdateFields on the held row of an S1-PT record, is the same.
        /// </summary>
        [Fact]
        public async Task PPT6_EditMergeFieldOfHeldTask_IsBlocked_NonStructuralFields()
        {
            var (record, hocKy, _, task) = await StageS1PtLiveBaseParentAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.Task(graph, task.MaTask).DoKho = 5;

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "S1PT.NonStructuralFields", RoutingStage.DirectSubject, record.ConflictId);
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
        }

        // ================================================================== AL-PT (absent MonHoc identity E)

        [Fact]
        public async Task PAL1_MaterializeAbsentIdentityUnderAnotherLiveHocKy_IsBlocked_NoRow()
        {
            var (record, _, absentId) = await _fence.StageAlPtAsync();
            var (otherHocKy, _, _) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(otherHocKy.MaHocKy);
            graph.DanhSachMonHoc.Add(new MonHoc("same identity", 3) { MaMonHoc = absentId });

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "ALPT.SameIdentityMaterialization", RoutingStage.DirectSubject, record.ConflictId);
            Assert.Null(await Fx.ReadMonHocAsync(absentId));
        }

        /// <summary>
        /// P-AL-2 — the create lands under the dead parent P itself: a save of P's graph (a VM still
        /// holding the semester that was tombstoned underneath it). Blocked before the writer runs.
        /// </summary>
        [Fact]
        public async Task PAL2_MaterializeAbsentIdentityUnderItsDeadParent_IsBlocked_NoRow()
        {
            var (record, deadHocKy, absentId) = await _fence.StageAlPtAsync();
            var graph = new HocKy(deadHocKy.Ten, deadHocKy.NgayBatDau) { MaHocKy = deadHocKy.MaHocKy };
            graph.DanhSachMonHoc.Add(new MonHoc("orphan?", 3) { MaMonHoc = absentId, MaHocKy = deadHocKy.MaHocKy });

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            Assert.Contains(ex.Decision.Results, r => r.RuleId == "ALPT.SameIdentityMaterialization" && r.ConflictId == record.ConflictId);
            Assert.Null(await Fx.ReadMonHocAsync(absentId));
        }

        [Fact]
        public async Task PAL3_DeleteUnrelatedTaskUnderLiveHocKy_Passes()
        {
            _ = await _fence.StageAlPtAsync();
            var (otherHocKy, otherMon, otherTask) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(otherHocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, otherTask.MaTask);

            await _save.SaveAsync(graph);

            Assert.True((await Fx.ReadTaskAsync(otherTask.MaTask))!.IsDeleted);
            Assert.False((await Fx.ReadMonHocAsync(otherMon.MaMonHoc))!.IsDeleted);
        }

        // ================================================================== Constraint (TaskNote per StudyTask)

        private async Task<Guid> HocKyOfTaskAsync(Guid taskId)
        {
            var task = (await Fx.ReadTaskAsync(taskId))!;
            return (await Fx.ReadMonHocAsync(task.MaMonHoc))!.MaHocKy;
        }

        [Fact]
        public async Task PK2_DeleteTaskOwningHeldNoteScope_IsBlocked_ScopeReleased()
        {
            var (record, task, occupant) = await _fence.SeedS2RecordAsync();
            var graph = await _save.LoadAsync(await HocKyOfTaskAsync(task.MaTask));
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "CONS.ScopeReleased", RoutingStage.ConstraintScope, record.ConflictId);
            Assert.False((await Fx.ReadNoteAsync(occupant.Id))!.IsDeleted);
        }

        [Fact]
        public async Task PK3_DeleteMonHocAboveHeldNoteScope_IsBlocked_NoteAndTaskStayLive()
        {
            var (record, task, occupant) = await _fence.SeedS2RecordAsync();
            var graph = await _save.LoadAsync(await HocKyOfTaskAsync(task.MaTask));
            LocalSaveDriver.RemoveMon(graph, task.MaMonHoc);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            AssertRule(ex, "CONS.ScopeReleased", RoutingStage.ConstraintScope, record.ConflictId);
            Assert.False((await Fx.ReadNoteAsync(occupant.Id))!.IsDeleted);
            Assert.False((await Fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
        }

        /// <summary>P-K-5 — OD-4 ruling (Pass): the owning task of an EMPTY S3 scope may be tombstoned.</summary>
        [Fact]
        public async Task PK5_DeleteTaskOwningEmptyS3Scope_Passes_EmptyScopeParentTombstoned()
        {
            var (record, task) = await _fence.StageS3Async();
            var graph = await _save.LoadAsync(await HocKyOfTaskAsync(task.MaTask));
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            await _save.SaveAsync(graph);

            Assert.True((await Fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
            Assert.Equal(ConflictRecordStatus.Unresolved, await _save.StatusOfAsync(record));
        }

        // ================================================================== fail-closed

        /// <summary>X-6 / N-1 — an unclassifiable record reached by the save rejects it as Unsupported.</summary>
        [Fact]
        public async Task X6_UnclassifiableRecordOnDeletedTask_IsRejected_Unsupported()
        {
            var (record, task) = await _fence.SeedUnsupportedRecordAsync();
            var graph = await _save.LoadAsync(await HocKyOfTaskAsync(task.MaTask));
            LocalSaveDriver.RemoveTask(graph, task.MaTask);

            var ex = await AssertBlockedAndUnchangedAsync(graph);

            var result = LocalSaveDriver.SingleBlocking(ex);
            Assert.Equal(FenceOutcome.Unsupported, result.Outcome);
            Assert.Equal("Unsupported.UnclassifiableRecord", result.RuleId);
            Assert.Equal(record.ConflictId, result.ConflictId);
        }

        // ================================================================== owner requirement 4 (2026-10-03)

        /// <summary>
        /// The two intents the planner adds beyond plan §6's table must be route-KNOWN, or every
        /// ordinary semester rename would fail closed. OBSERVED basis: StructuralDependencyRegistry's
        /// edge <c>HocKy -&gt; MonHoc via MaHocKy, FenceRoutable: true</c> makes HocKy a fence-routable
        /// type and <c>MonHoc.MaHocKy</c> a registered route.
        /// </summary>
        [Fact]
        public async Task Req4_UpdateFieldsHocKy_And_ReparentMonHoc_AreRouteKnown()
        {
            var (hocKy, monHoc, _) = await Fx.SeedTreeAsync();
            var other = Guid.NewGuid();

            using var db = Fx.NewContext();
            var rename = await FenceRouter.EvaluateAsync(db, new MutationRequest(MutationOrigin.LocalApplication, new[]
            {
                new MutationIntent(MutationOperation.UpdateFields, SyncEntityTypes.HocKy, hocKy.MaHocKy,
                    Array.Empty<RelationChange>(), new[] { "Ten" }),
            }));
            var move = await FenceRouter.EvaluateAsync(db, new MutationRequest(MutationOrigin.LocalApplication, new[]
            {
                new MutationIntent(MutationOperation.Reparent, SyncEntityTypes.MonHoc, monHoc.MaMonHoc,
                    new[] { new RelationChange("MaHocKy", hocKy.MaHocKy, other) }, Array.Empty<string>()),
            }));

            Assert.True(rename.RouteKnown);
            Assert.True(rename.FencePassed);
            Assert.True(move.RouteKnown);
            Assert.True(move.FencePassed);
        }

        [Fact]
        public async Task Req4_PlainSemesterRename_WithNoConflictRecords_Persists()
        {
            var (hocKy, _, _) = await Fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            graph.Ten = "HK renamed";

            // The planner really emits UpdateFields(HocKy) for this save -- otherwise this test would
            // pass vacuously against an empty request.
            using (var db = Fx.NewContext())
            {
                var plan = SemesterReconcilePlanner.Plan(await LocalSemesterSaveExecutor.LoadLiveGraphAsync(db, hocKy.MaHocKy, default), graph);
                Assert.Contains(plan.Request.Intents, i => i.Operation == MutationOperation.UpdateFields && i.EntityType == SyncEntityTypes.HocKy);
            }

            await _save.SaveAsync(graph);

            Assert.Equal("HK renamed", (await Fx.ReadHocKyAsync(hocKy.MaHocKy))!.Ten);
        }
    }
}
