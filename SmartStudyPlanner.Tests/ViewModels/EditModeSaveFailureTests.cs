using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4, review M-1 / owner ruling Q-1 (2026-10-03): the edit-mode save
    /// commands (<c>ThemTask</c>, <c>ThemMon</c>) leave edit mode only after the save succeeds. Here the
    /// save fails for a reason that is NOT the fence (one forced exception, nothing written), so no
    /// graph restore runs; the next click must still update the row being edited -- never create a
    /// second one -- and the note/link follow-up must target that row.
    /// <para>
    /// Self-contained on purpose (real SQLite repository, no fence fixtures), so the same file can be
    /// run against <c>origin/dev</c> to show whether the failure mode pre-dates Slice 4.
    /// </para>
    /// The two <c>*_CreateMode_*</c> tests are CHARACTERIZATIONS: they pin what a failed CREATE does
    /// today. The Q-1 ruling covers edit-mode state only; create mode is an open owner question.
    /// </summary>
    public class EditModeSaveFailureTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly Func<AppDbContext> _factory;

        public EditModeSaveFailureTests()
        {
            _conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(_conn)) { /* EnsureCreated */ }
            _factory = () => TestDb.Create(_conn);
        }

        public void Dispose() => _conn.Dispose();

        /// <summary>Fails the next save with a forced exception before anything is written, then delegates.</summary>
        private sealed class FailNextSaveRepository : IHocKyRepository
        {
            private readonly IHocKyRepository _inner;
            public bool FailNext = true;
            public FailNextSaveRepository(IHocKyRepository inner) => _inner = inner;

            public Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default) => _inner.LayDanhSachHocKyAsync(ct);

            public Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
            {
                if (!FailNext) return _inner.LuuHocKyAsync(hocKy, ct);
                FailNext = false;
                throw new InvalidOperationException("forced save failure");
            }
        }

        /// <summary>Records the task id every note/link follow-up write targets.</summary>
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

        private async Task<(HocKy HocKy, MonHoc MonHoc, StudyTask Task)> SeedAndReloadAsync(SqliteHocKyRepository repo)
        {
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Bai tap goc", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = monHoc.MaMonHoc };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await repo.LuuHocKyAsync(hocKy);

            var graph = (await repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKy.MaHocKy);
            var mon = graph.DanhSachMonHoc.Single(m => m.MaMonHoc == monHoc.MaMonHoc);
            return (graph, mon, mon.DanhSachTask.Single(t => t.MaTask == task.MaTask));
        }

        private QuanLyTaskViewModel TaskVm(HocKy hocKy, MonHoc monHoc, IHocKyRepository repo, ITaskEditorRepository editor) =>
            new(hocKy, monHoc, repo, editor, new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(DateTime.Today)));

        private async Task<List<StudyTask>> LiveTasksAsync(Guid maMonHoc)
        {
            using var ctx = _factory();
            return await ctx.StudyTasks.AsNoTracking().Where(t => t.MaMonHoc == maMonHoc && !t.IsDeleted).ToListAsync();
        }

        private async Task<List<MonHoc>> LiveMonHocsAsync(Guid maHocKy)
        {
            using var ctx = _factory();
            return await ctx.MonHocs.AsNoTracking().Where(m => m.MaHocKy == maHocKy && !m.IsDeleted).ToListAsync();
        }

        [Fact]
        public async Task ThemTask_EditMode_SaveFails_NextClickUpdatesSameTask_NoDuplicate_FollowUpsTargetIt()
        {
            var real = new SqliteHocKyRepository(_factory);
            var (hocKy, monHoc, task) = await SeedAndReloadAsync(real);
            var repo = new FailNextSaveRepository(real);
            var editor = new RecordingTaskEditorRepository();
            var vm = TaskVm(hocKy, monHoc, repo, editor);

            await vm.SuaTaskCommand.ExecuteAsync(task);
            vm.TenTask = "Bai tap da sua";
            vm.NoteContent = "ghi chu";
            vm.NewLinkUrl = "https://example.com/doc";
            vm.AddLinkCommand.Execute(null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ThemTaskCommand.ExecuteAsync(null));

            Assert.Equal("Cập Nhật", vm.TextNutThem);                       // still editing T
            Assert.Empty(editor.NoteTargets);
            Assert.Empty(editor.LinkTargets);
            Assert.Equal("Bai tap goc", Assert.Single(await LiveTasksAsync(monHoc.MaMonHoc)).TenTask);

            await vm.ThemTaskCommand.ExecuteAsync(null);                       // the user clicks again

            var live = Assert.Single(await LiveTasksAsync(monHoc.MaMonHoc));   // no duplicate row
            Assert.Equal(task.MaTask, live.MaTask);
            Assert.Equal("Bai tap da sua", live.TenTask);
            Assert.Single(monHoc.DanhSachTask);
            Assert.Equal(new[] { task.MaTask }, editor.NoteTargets);
            Assert.Equal(new[] { task.MaTask }, editor.LinkTargets);
            Assert.Equal("Thêm Deadline", vm.TextNutThem);                    // edit mode left after success
        }

        [Fact]
        public async Task ThemMon_EditMode_SaveFails_NextClickSavesTheEdit_NoDuplicate_NoMisleadingMessage()
        {
            var real = new SqliteHocKyRepository(_factory);
            var (hocKy, monHoc, _) = await SeedAndReloadAsync(real);
            var repo = new FailNextSaveRepository(real);
            var thongBao = new List<string>();
            var vm = new QuanLyMonHocViewModel(hocKy, repo) { OnThongBao = thongBao.Add };

            vm.SuaMonCommand.Execute(monHoc);
            vm.TenMon = "Toán cao cấp";
            vm.SoTinChi = "4";

            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ThemMonCommand.ExecuteAsync(null));

            Assert.Equal("Cập Nhật", vm.TextNutThem);
            Assert.Equal("Toán", Assert.Single(await LiveMonHocsAsync(hocKy.MaHocKy)).TenMonHoc);

            await vm.ThemMonCommand.ExecuteAsync(null);

            var live = Assert.Single(await LiveMonHocsAsync(hocKy.MaHocKy));
            Assert.Equal(monHoc.MaMonHoc, live.MaMonHoc);
            Assert.Equal("Toán cao cấp", live.TenMonHoc);                    // the retry saved the edit ...
            Assert.Equal(4, live.SoTinChi);
            Assert.Empty(thongBao);                                           // ... instead of "đã tồn tại"
            Assert.Single(hocKy.DanhSachMonHoc);
            Assert.Equal("Thêm Môn", vm.TextNutThem);
        }

        /// <summary>
        /// CHARACTERIZATION (create mode, outside the Q-1 ruling): the new task is added to the shared
        /// collection before the save, a failed save leaves it there, and the next click adds a second
        /// one -- both are then written. Pins status quo; not a desired semantic.
        /// </summary>
        [Fact]
        public async Task ThemTask_CreateMode_SaveFails_NextClick_PersistsTwoRows_Characterization()
        {
            var real = new SqliteHocKyRepository(_factory);
            var (hocKy, monHoc, _) = await SeedAndReloadAsync(real);
            var repo = new FailNextSaveRepository(real);
            var vm = TaskVm(hocKy, monHoc, repo, new RecordingTaskEditorRepository());
            vm.TenTask = "Bai moi";
            vm.HanChot = DateTime.Today.AddDays(2);
            vm.DoKho = "1";

            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ThemTaskCommand.ExecuteAsync(null));
            await vm.ThemTaskCommand.ExecuteAsync(null);

            Assert.Equal(2, (await LiveTasksAsync(monHoc.MaMonHoc)).Count(t => t.TenTask == "Bai moi"));
        }

        /// <summary>
        /// CHARACTERIZATION (create mode, outside the Q-1 ruling): the new MonHoc stays in the shared
        /// collection after the failed save, so the next click is stopped by the duplicate-name guard
        /// ("đã tồn tại") and nothing is written -- the unsaved MonHoc rides along with whatever save
        /// comes next. Pins status quo; not a desired semantic.
        /// </summary>
        [Fact]
        public async Task ThemMon_CreateMode_SaveFails_NextClick_IsStoppedByNameGuard_Characterization()
        {
            var real = new SqliteHocKyRepository(_factory);
            var (hocKy, _, _) = await SeedAndReloadAsync(real);
            var repo = new FailNextSaveRepository(real);
            var thongBao = new List<string>();
            var vm = new QuanLyMonHocViewModel(hocKy, repo) { OnThongBao = thongBao.Add, TenMon = "Lý", SoTinChi = "2" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ThemMonCommand.ExecuteAsync(null));
            await vm.ThemMonCommand.ExecuteAsync(null);

            Assert.Contains("đã tồn tại", Assert.Single(thongBao));
            Assert.DoesNotContain(await LiveMonHocsAsync(hocKy.MaHocKy), m => m.TenMonHoc == "Lý");
            Assert.Single(hocKy.DanhSachMonHoc, m => m.TenMonHoc == "Lý");     // unsaved, still in memory
        }
    }
}
