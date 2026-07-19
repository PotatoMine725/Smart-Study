using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    // Reopen 2026-07: the B4 crash lived in the gap between test fixtures (which always
    // stamped MaMonHoc by hand) and the real ThemTask path (which never did). This class
    // drives the ViewModel command against the real SQLite repository — fixture bias is
    // the failure mode it exists to prevent. Do NOT set MaMonHoc manually in these tests.
    public class QuanLyTaskViewModelTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly Func<AppDbContext> _factory;

        public QuanLyTaskViewModelTests()
        {
            _conn = TestDb.OpenConnection();
            using (var seed = TestDb.Create(_conn)) { /* EnsureCreated */ }
            _factory = () => TestDb.Create(_conn);
        }

        public void Dispose() => _conn.Dispose();

        [Fact]
        public async Task ThemTask_NewTask_PersistsWithOwnerSubjectFk()
        {
            var repo = new SqliteHocKyRepository(_factory);
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            hocKy.DanhSachMonHoc.Add(monHoc);
            await repo.LuuHocKyAsync(hocKy); // semester already exists → reconcile path on next save

            var vm = new QuanLyTaskViewModel(hocKy, monHoc, repo, new FakeTaskEditorRepository(),
                new FakeDecisionEngine(), new ParsingOrchestrator(new FakeClock(DateTime.Today)))
            {
                TenTask = "Bai tap 1",
                HanChot = DateTime.Today.AddDays(3),
                DoKho = "2",
            };

            await vm.ThemTaskCommand.ExecuteAsync(null);

            using var ctx = _factory();
            var saved = await ctx.StudyTasks.SingleAsync(t => t.TenTask == "Bai tap 1");
            Assert.Equal(monHoc.MaMonHoc, saved.MaMonHoc);
        }
    }
}
