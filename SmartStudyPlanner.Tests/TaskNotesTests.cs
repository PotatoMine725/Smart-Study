using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Helpers;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests
{
    // ─── DB fixture helpers ───────────────────────────────────────────────────

    internal static class TestDb
    {
        /// <summary>Creates an in-memory SQLite context with all tables created.</summary>
        public static AppDbContext Create(SqliteConnection conn)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(conn)
                .Options;
            var ctx = new AppDbContext(opts);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        public static SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            return conn;
        }

        /// <summary>Seeds a minimal task tree (HocKy → MonHoc → StudyTask) and returns the task.</summary>
        public static async Task<StudyTask> SeedTaskAsync(AppDbContext ctx)
        {
            var hocKy = new HocKy("HK Test", DateTime.Today);
            var monHoc = new MonHoc("MH Test", 3) { MaHocKy = hocKy.MaHocKy };
            var task = new StudyTask("Task A", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
            };
            monHoc.DanhSachTask.Add(task);
            hocKy.DanhSachMonHoc.Add(monHoc);
            ctx.HocKys.Add(hocKy);
            await ctx.SaveChangesAsync();
            return task;
        }
    }

    // ─── Repository tests ─────────────────────────────────────────────────────

    public class TaskNotesRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _conn;

        public TaskNotesRepositoryTests()
        {
            _conn = TestDb.OpenConnection();
        }

        public void Dispose() => _conn.Dispose();

        private AppDbContext NewCtx() => TestDb.Create(_conn);

        // ── Upsert + load ──────────────────────────────────────────────────────

        [Fact]
        public async Task UpsertTaskNote_ThenLoad_ReturnsNote()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);

            var note = new TaskNote { MaTask = task.MaTask, Content = "Study this carefully" };
            ctx.TaskNotes.Add(note);
            await ctx.SaveChangesAsync();

            using var ctx2 = NewCtx();
            var loaded = await ctx2.TaskNotes.FirstOrDefaultAsync(n => n.MaTask == task.MaTask);
            Assert.NotNull(loaded);
            Assert.Equal("Study this carefully", loaded.Content);
        }

        [Fact]
        public async Task UpsertTaskNote_UpdateExisting_ContentChanges()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);
            ctx.TaskNotes.Add(new TaskNote { MaTask = task.MaTask, Content = "First" });
            await ctx.SaveChangesAsync();

            using var ctx2 = NewCtx();
            var note = await ctx2.TaskNotes.FirstAsync(n => n.MaTask == task.MaTask);
            note.Content = "Updated";
            await ctx2.SaveChangesAsync();

            using var ctx3 = NewCtx();
            var reloaded = await ctx3.TaskNotes.FirstAsync(n => n.MaTask == task.MaTask);
            Assert.Equal("Updated", reloaded.Content);
        }

        // ── Links ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task AddLinks_ThenLoadBundle_ReturnsAllLinksOrdered()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);

            ctx.TaskReferenceLinks.AddRange(
                new TaskReferenceLink { MaTask = task.MaTask, Title = "A", Url = "https://a.com", SortOrder = 0 },
                new TaskReferenceLink { MaTask = task.MaTask, Title = "B", Url = "https://b.com", SortOrder = 1 },
                new TaskReferenceLink { MaTask = task.MaTask, Title = "C", Url = "https://c.com", SortOrder = 2 }
            );
            await ctx.SaveChangesAsync();

            using var ctx2 = NewCtx();
            var links = await ctx2.TaskReferenceLinks
                .Where(l => l.MaTask == task.MaTask)
                .OrderBy(l => l.SortOrder)
                .ToListAsync();

            Assert.Equal(3, links.Count);
            Assert.Equal("A", links[0].Title);
            Assert.Equal("C", links[2].Title);
        }

        // ── Cascade delete ─────────────────────────────────────────────────────

        [Fact]
        public async Task DeleteTask_CascadesToNoteAndLinks()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);

            ctx.TaskNotes.Add(new TaskNote { MaTask = task.MaTask, Content = "note" });
            ctx.TaskReferenceLinks.Add(new TaskReferenceLink { MaTask = task.MaTask, Title = "L", Url = "https://l.com" });
            await ctx.SaveChangesAsync();

            // Delete via parent hierarchy (same way LuuHocKyAsync works)
            using var ctx2 = NewCtx();
            var hocKy = await ctx2.HocKys
                .Include(h => h.DanhSachMonHoc)
                .ThenInclude(m => m.DanhSachTask)
                .FirstAsync();
            ctx2.HocKys.Remove(hocKy);
            await ctx2.SaveChangesAsync();

            using var ctx3 = NewCtx();
            Assert.False(await ctx3.TaskNotes.AnyAsync(n => n.MaTask == task.MaTask));
            Assert.False(await ctx3.TaskReferenceLinks.AnyAsync(l => l.MaTask == task.MaTask));
        }

        // ── SaveBundle diff ────────────────────────────────────────────────────

        [Fact]
        public async Task SaveBundle_RemovesDeletedLinks()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);

            var linkA = new TaskReferenceLink { MaTask = task.MaTask, Title = "A", Url = "https://a.com", SortOrder = 0 };
            var linkB = new TaskReferenceLink { MaTask = task.MaTask, Title = "B", Url = "https://b.com", SortOrder = 1 };
            ctx.TaskReferenceLinks.AddRange(linkA, linkB);
            await ctx.SaveChangesAsync();

            // Reload and reconcile: keep only A
            using var ctx2 = NewCtx();
            var existing = await ctx2.TaskReferenceLinks.Where(l => l.MaTask == task.MaTask).ToListAsync();
            var keepA = existing.First(l => l.Title == "A");
            ctx2.TaskReferenceLinks.Remove(existing.First(l => l.Title == "B"));
            await ctx2.SaveChangesAsync();

            using var ctx3 = NewCtx();
            var remaining = await ctx3.TaskReferenceLinks.Where(l => l.MaTask == task.MaTask).ToListAsync();
            Assert.Single(remaining);
            Assert.Equal("A", remaining[0].Title);
        }

        // ── Empty note ─────────────────────────────────────────────────────────

        [Fact]
        public async Task UpsertTaskNote_NullContent_DoesNotThrow()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);
            ctx.TaskNotes.Add(new TaskNote { MaTask = task.MaTask, Content = null });
            var ex = await Record.ExceptionAsync(() => ctx.SaveChangesAsync());
            Assert.Null(ex);
        }

        // ── Zero links ─────────────────────────────────────────────────────────

        [Fact]
        public async Task GetLinks_NoLinksExist_ReturnsEmptyList()
        {
            using var ctx = NewCtx();
            var task = await TestDb.SeedTaskAsync(ctx);
            var links = await ctx.TaskReferenceLinks.Where(l => l.MaTask == task.MaTask).ToListAsync();
            Assert.Empty(links);
        }
    }

    // ─── ViewModel tests ──────────────────────────────────────────────────────

    public class TaskNotesViewModelTests
    {
        private static QuanLyTaskViewModel BuildVm()
        {
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            hocKy.DanhSachMonHoc.Add(monHoc);
            var repo = new FakeStudyRepository();
            var engine = new FakeDecisionEngine();
            return new QuanLyTaskViewModel(hocKy, monHoc, repo, engine);
        }

        [Fact]
        public void AddLinkCommand_EmptyUrl_DoesNotAdd()
        {
            var vm = BuildVm();
            vm.NewLinkUrl = "";
            vm.AddLinkCommand.Execute(null);
            Assert.Empty(vm.StudyLinks);
        }

        [Fact]
        public void AddLinkCommand_ValidUrl_AddsToCollection()
        {
            var vm = BuildVm();
            vm.NewLinkUrl = "https://example.com";
            vm.NewLinkTitle = "Example";
            vm.AddLinkCommand.Execute(null);
            Assert.Single(vm.StudyLinks);
            Assert.Equal("Example", vm.StudyLinks[0].Title);
            Assert.Equal("https://example.com", vm.StudyLinks[0].Url);
        }

        [Fact]
        public void AddLinkCommand_ClearsInputFields_AfterAdd()
        {
            var vm = BuildVm();
            vm.NewLinkUrl = "https://example.com";
            vm.NewLinkTitle = "Title";
            vm.AddLinkCommand.Execute(null);
            Assert.Equal(string.Empty, vm.NewLinkUrl);
            Assert.Equal(string.Empty, vm.NewLinkTitle);
        }

        [Fact]
        public void RemoveLinkCommand_RemovesItem()
        {
            var vm = BuildVm();
            vm.NewLinkUrl = "https://a.com";
            vm.AddLinkCommand.Execute(null);
            var item = vm.StudyLinks[0];
            vm.RemoveLinkCommand.Execute(item);
            Assert.Empty(vm.StudyLinks);
        }

        [Fact]
        public void ClearNoteCommand_SetsNoteContentNull()
        {
            var vm = BuildVm();
            vm.NoteContent = "some text";
            vm.ClearNoteCommand.Execute(null);
            Assert.Null(vm.NoteContent);
        }

        [Fact]
        public void PhanTichNhapNhanh_DoesNotModifyNoteOrLinks()
        {
            var vm = BuildVm();
            vm.NoteContent = "my note";
            vm.NewLinkUrl = "https://existing.com";
            vm.AddLinkCommand.Execute(null);

            vm.VanBanNhapNhanh = "nộp btl T6 tuần sau";
            vm.PhanTichNhapNhanhCommand.Execute(null);

            Assert.Equal("my note", vm.NoteContent);
            Assert.Single(vm.StudyLinks);
        }
    }

    // ─── Minimal fake engine for ViewModel tests ──────────────────────────────

    internal class FakeDecisionEngine : SmartStudyPlanner.Services.IDecisionEngine
    {
        public SmartStudyPlanner.Services.WeightConfig Config { get; } = new();
        public double CalculatePriority(StudyTask task, MonHoc monHoc) => 0;
        public int CalculateRawSuggestedMinutes(StudyTask task) => 0;
        public string SuggestStudyTime(StudyTask task) => "0 phút";
    }
}
