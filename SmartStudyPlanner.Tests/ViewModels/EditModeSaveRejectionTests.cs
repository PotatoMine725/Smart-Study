using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4, review M-1 / owner ruling Q-1 (2026-10-03): OD-7's "restore the
    /// in-memory graph" also covers the VM state a save command changes before awaiting the save --
    /// for <c>ThemTask</c> and <c>ThemMon</c>, edit mode. After the fence rejects an edit of a held row,
    /// the VM is still editing that row: the next click re-submits the edit to it (and is rejected
    /// again while the record stays unresolved), never takes the create branch, and no note/link
    /// follow-up is written for the rejected save.
    /// </summary>
    public class EditModeSaveRejectionTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();
        private readonly LocalSaveDriver _save;

        public EditModeSaveRejectionTests() => _save = new LocalSaveDriver(_fence.Fx);

        public void Dispose() => _fence.Dispose();

        private sealed class RecordingTaskEditorRepository : ITaskEditorRepository
        {
            public List<Guid> NoteTargets { get; } = new();
            public List<Guid> LinkTargets { get; } = new();

            public Task<TaskEditorBundle?> GetBundleAsync(Guid taskId, CancellationToken ct = default) => Task.FromResult<TaskEditorBundle?>(null);
            public Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default) { NoteTargets.Add(taskId); return Task.CompletedTask; }
            public Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default) => Task.FromResult(new List<TaskReferenceLink>());
            public Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default) { LinkTargets.Add(link.MaTask); return Task.CompletedTask; }
            public Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default) { LinkTargets.Add(link.MaTask); return Task.CompletedTask; }
            public Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default) => Task.CompletedTask;
        }

        private async Task<int> LiveTaskCountAsync(Guid maHocKy)
        {
            using var ctx = _fence.Fx.NewContext();
            return await ctx.StudyTasks.CountAsync(t => !t.IsDeleted && ctx.MonHocs.Any(m => m.MaMonHoc == t.MaMonHoc && m.MaHocKy == maHocKy));
        }

        private async Task<int> LiveMonHocCountAsync(Guid maHocKy)
        {
            using var ctx = _fence.Fx.NewContext();
            return await ctx.MonHocs.CountAsync(m => !m.IsDeleted && m.MaHocKy == maHocKy);
        }

        [Fact]
        public async Task ThemTask_EditOfHeldTask_Rejected_StaysInEditMode_NextClickIsNoDuplicate_NoFollowUps()
        {
            var (record, hocKy, monHocA, _, _, task) = await _fence.StageS1CrAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var mon = LocalSaveDriver.Mon(graph, monHocA.MaMonHoc);
            var editor = new RecordingTaskEditorRepository();
            var vm = new QuanLyTaskViewModel(graph, mon, _save.Repo, editor, new FakeDecisionEngine(),
                new ParsingOrchestrator(new FakeClock(new DateTime(2026, 9, 10))));
            var tasksBefore = await LiveTaskCountAsync(hocKy.MaHocKy);

            await vm.SuaTaskCommand.ExecuteAsync(LocalSaveDriver.Task(graph, task.MaTask));
            vm.TenTask = "edited while held";
            vm.NoteContent = "note for the held task";
            vm.NewLinkUrl = "https://example.com/held";
            vm.AddLinkCommand.Execute(null);

            var first = await Assert.ThrowsAsync<MutationRejectedException>(() => vm.ThemTaskCommand.ExecuteAsync(null));
            Assert.Equal("S1CR.NonStructuralFields", LocalSaveDriver.SingleBlocking(first).RuleId);
            Assert.Equal("Cập Nhật", vm.TextNutThem);                              // still editing T
            await _save.AssertGraphMatchesPersistedAsync(graph);                    // R2 restored the model graph

            var second = await Assert.ThrowsAsync<MutationRejectedException>(() => vm.ThemTaskCommand.ExecuteAsync(null));
            Assert.Equal("S1CR.NonStructuralFields", LocalSaveDriver.SingleBlocking(second).RuleId); // an edit of T, not a Create

            Assert.Equal(tasksBefore, await LiveTaskCountAsync(hocKy.MaHocKy));     // no duplicate row
            Assert.Single(mon.DanhSachTask, t => t.MaTask == task.MaTask);
            Assert.DoesNotContain(mon.DanhSachTask, t => t.TenTask == "edited while held" && t.MaTask != task.MaTask);
            Assert.Empty(editor.NoteTargets);                                       // no follow-up for a rejected save
            Assert.Empty(editor.LinkTargets);
            Assert.True(await _save.BaseStillMatchesAsync(record, task.MaTask));
            Assert.Equal("Cập Nhật", vm.TextNutThem);
        }

        /// <summary>
        /// A MonHoc carries a Structural field (<c>MaHocKy</c>) too, so the S1-CR recipe works one level up:
        /// both sides reparent the same MonHoc to different HocKys, and the MonHoc is held live at Base.
        /// </summary>
        private async Task<(SyncConflictRecordRow Record, HocKy HocKy, MonHoc MonHoc)> StageS1CrOnMonHocAsync()
        {
            var fx = _fence.Fx;
            var (hocKy, monHoc, _) = await fx.SeedTreeAsync();
            var hocKyB = new HocKy("HK B", hocKy.NgayBatDau);
            var hocKyC = new HocKy("HK C", hocKy.NgayBatDau);
            await fx.AddLocalAsync(hocKyB);
            await fx.AddLocalAsync(hocKyC);
            await fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, monHoc);

            using (var ctx = fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == monHoc.MaMonHoc);
                live.MaHocKy = hocKyB.MaHocKy;
                await ctx.SaveChangesAsync();
            }

            var before = (await fx.ReadConflictsAsync()).Select(r => r.ConflictId).ToHashSet();
            var remote = new MonHoc { MaMonHoc = monHoc.MaMonHoc, MaHocKy = hocKyC.MaHocKy, TenMonHoc = monHoc.TenMonHoc, SoTinChi = monHoc.SoTinChi };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = Assert.Single(await fx.ReadConflictsAsync(), r => !before.Contains(r.ConflictId));
            Assert.Equal(ConflictKind.StructuralConflict, record.Kind);
            Assert.Equal(StructuralReason.ConcurrentReparent, record.StructuralReason);
            Assert.Equal(SyncEntityTypes.MonHoc, record.EntityType);
            Assert.Equal(monHoc.MaMonHoc, record.EntityId);
            Assert.Equal(hocKy.MaHocKy, (await fx.ReadMonHocAsync(monHoc.MaMonHoc))!.MaHocKy); // held live at Base
            return (record, hocKy, monHoc);
        }

        [Fact]
        public async Task ThemMon_RenameOfHeldMonHoc_Rejected_StaysInEditMode_NextClickIsNoDuplicate()
        {
            var (_, hocKy, monHoc) = await StageS1CrOnMonHocAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var thongBao = new List<string>();
            var vm = new QuanLyMonHocViewModel(graph, _save.Repo) { OnThongBao = thongBao.Add };
            var monsBefore = await LiveMonHocCountAsync(hocKy.MaHocKy);

            vm.SuaMonCommand.Execute(LocalSaveDriver.Mon(graph, monHoc.MaMonHoc));
            vm.TenMon = "renamed while held";                                      // a NAME change: SoTinChi alone is stopped by the name guard

            var first = await Assert.ThrowsAsync<MutationRejectedException>(() => vm.ThemMonCommand.ExecuteAsync(null));
            Assert.Equal("S1CR.NonStructuralFields", LocalSaveDriver.SingleBlocking(first).RuleId);
            Assert.Equal("Cập Nhật", vm.TextNutThem);
            await _save.AssertGraphMatchesPersistedAsync(graph);

            var second = await Assert.ThrowsAsync<MutationRejectedException>(() => vm.ThemMonCommand.ExecuteAsync(null));
            Assert.Equal("S1CR.NonStructuralFields", LocalSaveDriver.SingleBlocking(second).RuleId);

            Assert.Equal(monsBefore, await LiveMonHocCountAsync(hocKy.MaHocKy));   // no duplicate MonHoc
            Assert.DoesNotContain(graph.DanhSachMonHoc, m => m.TenMonHoc == "renamed while held" && m.MaMonHoc != monHoc.MaMonHoc);
            Assert.Empty(thongBao);
            Assert.Equal("Cập Nhật", vm.TextNutThem);
        }
    }
}
