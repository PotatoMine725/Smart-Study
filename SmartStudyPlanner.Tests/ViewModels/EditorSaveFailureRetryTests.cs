using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// Epic 2 / T2.4 fence Slice 5, owner rulings 2026-10-16: E2 (the note + links change is one
    /// transaction) and R-a + Q-1′ = whole command (<c>ThemTask</c> leaves edit mode and clears the form
    /// only after BOTH the task save and the editor save succeed; on any failure it stays in edit mode
    /// with the form as typed — no reload, no catch in the VM).
    /// <para>
    /// Phase 0 OBSERVED the failure mode this closes: an editor failure mid-command left edit mode, so
    /// the next click created a second task and then hit <c>UNIQUE constraint failed:
    /// TaskReferenceLinks.Id</c>. Here the failure is injected INSIDE the editor's SaveChanges, on the
    /// save that carries the new link — after the note and the link edit in the old call sequence.
    /// </para>
    /// </summary>
    public class EditorSaveFailureRetryTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly Func<AppDbContext> _factory;
        private Guid? _failSaveAddingLinkId;

        public EditorSaveFailureRetryTests()
        {
            _conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(_conn)) { /* EnsureCreated */ }
            _factory = () => TestDb.Create(_conn);
        }

        public void Dispose() => _conn.Dispose();

        /// <summary>Throws, once armed, on the SaveChanges that would insert the armed link id.</summary>
        private sealed class FailWhenAddingLinkDbContext : AppDbContext
        {
            private readonly EditorSaveFailureRetryTests _owner;

            public FailWhenAddingLinkDbContext(DbContextOptions<AppDbContext> options, EditorSaveFailureRetryTests owner)
                : base(options) => _owner = owner;

            public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            {
                if (_owner._failSaveAddingLinkId is { } id
                    && ChangeTracker.Entries<TaskReferenceLink>().Any(e => e.State == EntityState.Added && e.Entity.Id == id))
                {
                    _owner._failSaveAddingLinkId = null;
                    throw new InvalidOperationException("injected editor save failure");
                }
                return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
        }

        private AppDbContext EditorContext()
        {
            var ctx = new FailWhenAddingLinkDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options, this);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private async Task<(HocKy HocKy, MonHoc MonHoc, StudyTask Task, TaskReferenceLink Link)> SeedAndReloadAsync(SqliteHocKyRepository repo)
        {
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Bai tap goc", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2) { MaMonHoc = monHoc.MaMonHoc };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            await repo.LuuHocKyAsync(hocKy);

            var link = new TaskReferenceLink { Id = Guid.NewGuid(), MaTask = task.MaTask, Title = "L1", Url = "https://example.com/l1", SortOrder = 0 };
            using (var ctx = _factory())
            {
                ctx.TaskReferenceLinks.Add(link);
                await ctx.SaveChangesAsync();
            }

            var graph = (await repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKy.MaHocKy);
            var mon = graph.DanhSachMonHoc.Single(m => m.MaMonHoc == monHoc.MaMonHoc);
            return (graph, mon, mon.DanhSachTask.Single(t => t.MaTask == task.MaTask), link);
        }

        private static QuanLyTaskViewModel TaskVm(HocKy hocKy, MonHoc monHoc, SqliteHocKyRepository repo, SqliteTaskEditorRepository editor) =>
            new(hocKy, monHoc, repo, editor, new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(DateTime.Today)));

        private async Task<List<StudyTask>> LiveTasksAsync(Guid maMonHoc)
        {
            using var ctx = _factory();
            return await ctx.StudyTasks.AsNoTracking().Where(t => t.MaMonHoc == maMonHoc && !t.IsDeleted).ToListAsync();
        }

        private async Task<(List<TaskNote> Notes, List<TaskReferenceLink> Links)> EditorRowsAsync()
        {
            using var ctx = _factory();
            return (await ctx.TaskNotes.AsNoTracking().ToListAsync(), await ctx.TaskReferenceLinks.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task ThemTask_EditMode_EditorSaveFailsMidCommand_StaysInEditMode_FormKept_NextClickUpdatesSameTask()
        {
            var hocKyRepo = new SqliteHocKyRepository(_factory);
            var (hocKy, monHoc, task, l1) = await SeedAndReloadAsync(hocKyRepo);
            var vm = TaskVm(hocKy, monHoc, hocKyRepo, new SqliteTaskEditorRepository(EditorContext));

            await vm.SuaTaskCommand.ExecuteAsync(task);
            vm.TenTask = "Da sua";
            vm.NoteContent = "ghi chu";
            vm.StudyLinks.Single(l => l.Id == l1.Id).Title = "L1 da sua";
            vm.NewLinkUrl = "https://example.com/l2";
            vm.AddLinkCommand.Execute(null);
            var l2Id = vm.StudyLinks.Single(l => l.Id != l1.Id).Id;

            _failSaveAddingLinkId = l2Id;
            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ThemTaskCommand.ExecuteAsync(null));

            // VM: still editing T, the form exactly as typed.
            Assert.Equal("Cập Nhật", vm.TextNutThem);
            Assert.Equal("Da sua", vm.TenTask);
            Assert.Equal("ghi chu", vm.NoteContent);
            Assert.Equal(2, vm.StudyLinks.Count);
            Assert.Equal("L1 da sua", vm.StudyLinks.Single(l => l.Id == l1.Id).Title);
            // DB: the task save committed (it is its own, already-fenced operation); NOTHING of the editor did.
            Assert.Equal("Da sua", Assert.Single(await LiveTasksAsync(monHoc.MaMonHoc)).TenTask);
            var (notes, links) = await EditorRowsAsync();
            Assert.Empty(notes);
            Assert.Equal("L1", Assert.Single(links).Title);

            await vm.ThemTaskCommand.ExecuteAsync(null);                           // the user clicks again

            var live = Assert.Single(await LiveTasksAsync(monHoc.MaMonHoc));        // no duplicate task
            Assert.Equal(task.MaTask, live.MaTask);
            Assert.Single(monHoc.DanhSachTask);
            (notes, links) = await EditorRowsAsync();
            var note = Assert.Single(notes);
            Assert.Equal(task.MaTask, note.MaTask);
            Assert.Equal("ghi chu", note.Content);
            Assert.Equal(2, links.Count);                                           // no UNIQUE error, no extra row
            Assert.All(links, l => Assert.Equal(task.MaTask, l.MaTask));
            Assert.Equal("L1 da sua", links.Single(l => l.Id == l1.Id).Title);
            Assert.False(links.Single(l => l.Id == l2Id).IsDeleted);
            // Whole command succeeded: edit mode left, form cleared.
            Assert.Equal("Thêm Deadline", vm.TextNutThem);
            Assert.Equal(string.Empty, vm.TenTask);
            Assert.Null(vm.NoteContent);
            Assert.Empty(vm.StudyLinks);
        }

        /// <summary>
        /// Owner ruling 2026-10-16, extension of ruling 2 to create mode: once the new task's save
        /// succeeded, <c>ThemTask</c> treats that task as the one being edited until the whole command
        /// succeeds, so a failed editor save is retried against it instead of creating a second task.
        /// (A REJECTION in create mode needs a record on a brand-new task id's scope — not reachable;
        /// the failure path covers the same VM state.)
        /// </summary>
        [Fact]
        public async Task ThemTask_CreateMode_EditorSaveFails_SwitchesToEditingTheNewTask_NextClickUpdatesIt_NoDuplicate()
        {
            var hocKyRepo = new SqliteHocKyRepository(_factory);
            var (hocKy, monHoc, original, _) = await SeedAndReloadAsync(hocKyRepo);
            var vm = TaskVm(hocKy, monHoc, hocKyRepo, new SqliteTaskEditorRepository(EditorContext));
            vm.TenTask = "Bai moi";
            vm.HanChot = DateTime.Today.AddDays(2);
            vm.DoKho = "1";
            vm.NoteContent = "ghi chu moi";
            vm.NewLinkUrl = "https://example.com/moi";
            vm.AddLinkCommand.Execute(null);
            var linkId = Assert.Single(vm.StudyLinks).Id;

            _failSaveAddingLinkId = linkId;
            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.ThemTaskCommand.ExecuteAsync(null));

            var created = Assert.Single(await LiveTasksAsync(monHoc.MaMonHoc), t => t.MaTask != original.MaTask);
            Assert.Equal("Bai moi", created.TenTask);                               // the task save committed
            Assert.Equal("Cập Nhật", vm.TextNutThem);                               // now editing the new task
            Assert.Equal("Bai moi", vm.TenTask);
            Assert.Equal("ghi chu moi", vm.NoteContent);
            Assert.Single(vm.StudyLinks);
            var (notes, links) = await EditorRowsAsync();
            Assert.Empty(notes);
            Assert.DoesNotContain(links, l => l.Id == linkId);

            await vm.ThemTaskCommand.ExecuteAsync(null);                           // the user clicks again

            var live = await LiveTasksAsync(monHoc.MaMonHoc);
            Assert.Equal(2, live.Count);                                            // original + ONE new task
            Assert.Equal(created.MaTask, Assert.Single(live, t => t.TenTask == "Bai moi").MaTask);
            Assert.Single(monHoc.DanhSachTask, t => t.TenTask == "Bai moi");
            (notes, links) = await EditorRowsAsync();
            Assert.Equal("ghi chu moi", Assert.Single(notes, n => n.MaTask == created.MaTask).Content);
            Assert.Equal(created.MaTask, links.Single(l => l.Id == linkId).MaTask);
            Assert.Equal("Thêm Deadline", vm.TextNutThem);
            Assert.Equal(string.Empty, vm.TenTask);
            Assert.Empty(vm.StudyLinks);
        }

        /// <summary>
        /// The rejection variant (S2 on K(T): the task rename passes the fence, the note edit is the held
        /// occupant's content — SB-3 Blocked). R-a: the VM stays in edit mode with the rejected edit still
        /// in the form; the next click re-submits it to the same task and is rejected again while the
        /// record stays unresolved. Never a second task.
        /// </summary>
        [Fact]
        public async Task ThemTask_EditMode_EditorSaveRejected_StaysInEditMode_NextClickRejectedAgain_NoDuplicate()
        {
            using var fence = new FenceScenarioFixture();
            var (record, task, occupant) = await fence.SeedS2RecordAsync();
            var save = new LocalSaveDriver(fence.Fx);
            var hocKyId = await HocKyOfAsync(fence, task.MaTask);
            var graph = await save.LoadAsync(hocKyId);
            var mon = graph.DanhSachMonHoc.Single(m => m.DanhSachTask.Any(t => t.MaTask == task.MaTask));
            var vm = new QuanLyTaskViewModel(graph, mon, save.Repo, new SqliteTaskEditorRepository(fence.Fx.Factory),
                new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(new DateTime(2026, 9, 10))));

            await vm.SuaTaskCommand.ExecuteAsync(LocalSaveDriver.Task(graph, task.MaTask));
            Assert.Equal("base content", vm.NoteContent);
            vm.TenTask = "renamed";
            vm.NoteContent = "edited while held";
            vm.NewLinkUrl = "https://example.com/new";
            vm.AddLinkCommand.Execute(null);

            var first = await Assert.ThrowsAsync<MutationRejectedException>(() => vm.ThemTaskCommand.ExecuteAsync(null));
            Assert.Equal("CONS.OccupantContent", LocalSaveDriver.SingleBlocking(first).RuleId);
            Assert.Equal(record.ConflictId, LocalSaveDriver.SingleBlocking(first).ConflictId);
            Assert.Equal("Cập Nhật", vm.TextNutThem);
            Assert.Equal("edited while held", vm.NoteContent);                       // R-a: kept, no reload
            Assert.Single(vm.StudyLinks);

            var second = await Assert.ThrowsAsync<MutationRejectedException>(() => vm.ThemTaskCommand.ExecuteAsync(null));
            Assert.Equal("CONS.OccupantContent", LocalSaveDriver.SingleBlocking(second).RuleId);

            using var ctx = fence.Fx.NewContext();
            Assert.Single(await ctx.StudyTasks.AsNoTracking().Where(t => t.MaMonHoc == mon.MaMonHoc && !t.IsDeleted).ToListAsync());
            Assert.Equal("renamed", (await ctx.StudyTasks.AsNoTracking().SingleAsync(t => t.MaTask == task.MaTask)).TenTask);
            Assert.Equal("base content", (await ctx.TaskNotes.AsNoTracking().SingleAsync(n => n.Id == occupant.Id)).Content);
            Assert.Empty(await ctx.TaskReferenceLinks.AsNoTracking().Where(l => l.MaTask == task.MaTask).ToListAsync());
            Assert.Single(mon.DanhSachTask);
            Assert.Equal("Cập Nhật", vm.TextNutThem);
        }

        private static async Task<Guid> HocKyOfAsync(FenceScenarioFixture fence, Guid taskId)
        {
            using var ctx = fence.Fx.NewContext();
            var maMonHoc = (await ctx.StudyTasks.AsNoTracking().SingleAsync(t => t.MaTask == taskId)).MaMonHoc;
            return (await ctx.MonHocs.AsNoTracking().SingleAsync(m => m.MaMonHoc == maMonHoc)).MaHocKy;
        }
    }
}
