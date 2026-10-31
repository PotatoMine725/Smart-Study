using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// Owner ruling Q-1 on review #114 (2026-10-16): <c>XoaTask</c> deleting the task currently being edited
    /// leaves edit mode and clears the form — the same reset as a successful <c>ThemTask</c>. Before it, the
    /// form stayed bound to the deleted task and the next click re-ran the editor save on it: no live note,
    /// so a <c>Create</c> into the dead note's unfiltered <c>UNIQUE(MaTask)</c> scope (D-1, REPRODUCED in
    /// review #114 F-3 on base and head). The reset follows a SUCCESSFUL delete only: a failed or rejected
    /// delete leaves the task live, and the form keeps editing it (Q-1′).
    /// <para>
    /// The tests call <c>XoaTaskDaXacNhanAsync</c> — <c>XoaTask</c>'s body after its confirmation
    /// <c>MessageBox</c>, which cannot run in a test.
    /// </para>
    /// </summary>
    public class DeleteEditedTaskTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly Func<AppDbContext> _factory;

        public DeleteEditedTaskTests()
        {
            _conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(_conn)) { /* EnsureCreated */ }
            _factory = () => TestDb.Create(_conn);
        }

        public void Dispose() => _conn.Dispose();

        private async Task<(QuanLyTaskViewModel Vm, MonHoc MonHoc, StudyTask Task, StudyTask Other)> SeedTwoTasksWithNoteAsync()
        {
            var repo = new SqliteHocKyRepository(_factory);
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Bai dang sua", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = monHoc.MaMonHoc };
            var other = new StudyTask("Bai khac", DateTime.Today.AddDays(6), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = monHoc.MaMonHoc };
            monHoc.DanhSachTask.Add(task);
            monHoc.DanhSachTask.Add(other);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await repo.LuuHocKyAsync(hocKy);
            await new SqliteTaskEditorRepository(_factory).UpsertNoteAsync(task.MaTask, "ghi chu cu");

            var graph = (await repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKy.MaHocKy);
            var mon = graph.DanhSachMonHoc.Single(m => m.MaMonHoc == monHoc.MaMonHoc);
            var vm = new QuanLyTaskViewModel(graph, mon, repo, new SqliteTaskEditorRepository(_factory),
                new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(DateTime.Today)));
            return (vm, mon, mon.DanhSachTask.Single(t => t.MaTask == task.MaTask), mon.DanhSachTask.Single(t => t.MaTask == other.MaTask));
        }

        private async Task<List<StudyTask>> AllTasksAsync()
        {
            using var ctx = _factory();
            return await ctx.StudyTasks.AsNoTracking().ToListAsync();
        }

        private async Task<List<TaskNote>> AllNotesAsync()
        {
            using var ctx = _factory();
            return await ctx.TaskNotes.AsNoTracking().ToListAsync();
        }

        [Fact]
        public async Task EditThenDeleteTheEditedTask_LeavesEditMode_ClearsForm_NextSaveCreatesANewTask_NoUniqueError()
        {
            var (vm, _, task, other) = await SeedTwoTasksWithNoteAsync();
            await vm.SuaTaskCommand.ExecuteAsync(task);
            Assert.Equal("ghi chu cu", vm.NoteContent);
            vm.NewLinkUrl = "https://example.com/l";
            vm.AddLinkCommand.Execute(null);

            await vm.XoaTaskDaXacNhanAsync(task);

            // Same reset as a successful ThemTask.
            Assert.Equal("Thêm Deadline", vm.TextNutThem);
            Assert.Equal("#9B59B6", vm.MauNutThem);
            Assert.Equal(string.Empty, vm.TenTask);
            Assert.Equal(string.Empty, vm.DoKho);
            Assert.Null(vm.HanChot);
            Assert.Equal(0, vm.LoaiTaskIndex);
            Assert.Null(vm.NoteContent);
            Assert.Empty(vm.StudyLinks);

            // The user fills the form again and saves: a NEW task, its note under the new id.
            vm.TenTask = "Bai moi";
            vm.HanChot = DateTime.Today.AddDays(3);
            vm.DoKho = "1";
            vm.NoteContent = "ghi chu moi";
            await vm.ThemTaskCommand.ExecuteAsync(null);

            var tasks = await AllTasksAsync();
            Assert.True(tasks.Single(t => t.MaTask == task.MaTask).IsDeleted);
            var created = Assert.Single(tasks, t => !t.IsDeleted && t.MaTask != other.MaTask);
            Assert.Equal("Bai moi", created.TenTask);
            var notes = await AllNotesAsync();
            Assert.True(Assert.Single(notes, n => n.MaTask == task.MaTask).IsDeleted);   // cascade, untouched since
            Assert.Equal("ghi chu moi", Assert.Single(notes, n => n.MaTask == created.MaTask && !n.IsDeleted).Content);
            Assert.Equal("Thêm Deadline", vm.TextNutThem);
        }

        [Fact]
        public async Task DeleteAnotherTask_WhileEditing_StaysInEditMode_FormKept()
        {
            var (vm, _, task, other) = await SeedTwoTasksWithNoteAsync();
            await vm.SuaTaskCommand.ExecuteAsync(task);
            vm.TenTask = "Dang sua do";

            await vm.XoaTaskDaXacNhanAsync(other);

            Assert.Equal("Cập Nhật", vm.TextNutThem);
            Assert.Equal("Dang sua do", vm.TenTask);
            Assert.Equal("ghi chu cu", vm.NoteContent);

            await vm.ThemTaskCommand.ExecuteAsync(null);                             // still updates T
            var live = Assert.Single(await AllTasksAsync(), t => !t.IsDeleted);
            Assert.Equal((task.MaTask, "Dang sua do"), (live.MaTask, live.TenTask));
        }

        /// <summary>
        /// The reset follows the save: a fence-rejected delete (X-20's S1-CR held task) restores T into the
        /// graph (OD-7), T stays live, so the form keeps editing it.
        /// </summary>
        [Fact]
        public async Task DeleteTheEditedTask_Rejected_StaysInEditMode_FormKept()
        {
            using var fence = new FenceScenarioFixture();
            var (_, hocKy, monHocA, _, _, held) = await fence.StageS1CrAsync();
            var save = new LocalSaveDriver(fence.Fx);
            var graph = await save.LoadAsync(hocKy.MaHocKy);
            var mon = LocalSaveDriver.Mon(graph, monHocA.MaMonHoc);
            var vm = new QuanLyTaskViewModel(graph, mon, save.Repo, new SqliteTaskEditorRepository(fence.Fx.Factory),
                new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(new DateTime(2026, 9, 10))));
            var task = LocalSaveDriver.Task(graph, held.MaTask);
            await vm.SuaTaskCommand.ExecuteAsync(task);
            vm.TenTask = "Dang sua";

            await Assert.ThrowsAsync<MutationRejectedException>(() => vm.XoaTaskDaXacNhanAsync(task));

            Assert.Equal("Cập Nhật", vm.TextNutThem);
            Assert.Equal("Dang sua", vm.TenTask);
            Assert.True(LocalSaveDriver.Contains(graph, held.MaTask));
        }
    }
}
