using System;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    public class DifficultyLabelLoggingTests
    {
        private static (SmartStudyPlanner.ViewModels.QuanLyTaskViewModel vm, FakeDifficultyLabelLogRepository logRepo) BuildVm(LoaiCongViec? loai = null)
        {
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            hocKy.DanhSachMonHoc.Add(monHoc);
            var logRepo = new FakeDifficultyLabelLogRepository();
            var vm = new QuanLyTaskViewModel(
                hocKy, monHoc,
                new FakeHocKyRepository(),
                new FakeTaskEditorRepository(),
                new FakeDecisionEngine(),
                new SmartStudyPlanner.Services.Telemetry.DebugStudyTelemetry(),
                new ParsingOrchestrator(new FakeClock(DateTime.Today)),
                logRepo);
            return (vm, logRepo);
        }

        [Fact]
        public async Task ThemTask_LogsDifficultyLabel_WithCorrectFields()
        {
            var (vm, logRepo) = BuildVm();
            vm.TenTask = "Ôn thi cuối kỳ";
            vm.HanChot = DateTime.Today.AddDays(7);
            vm.LoaiTaskIndex = (int)LoaiCongViec.ThiCuoiKy;
            vm.DoKho = "4"; // matches prior → no override

            await vm.ThemTaskCommand.ExecuteAsync(null);

            // Give fire-and-forget a moment to complete
            await Task.Delay(50);

            Assert.Single(logRepo.Added);
            var entry = logRepo.Added[0];
            Assert.Equal("Ôn thi cuối kỳ", entry.InputText);
            Assert.Equal(LoaiCongViec.ThiCuoiKy, entry.TaskType);
            Assert.Equal(4, entry.SuggestedDoKho);
            Assert.Equal(4, entry.FinalDoKho);
            Assert.False(entry.WasOverride);
            Assert.Equal("manual", entry.Source);
        }

        [Fact]
        public async Task ThemTask_WasOverride_True_WhenUserChangesFromPrior()
        {
            var (vm, logRepo) = BuildVm();
            vm.TenTask = "Bài tập về nhà";
            vm.HanChot = DateTime.Today.AddDays(3);
            vm.LoaiTaskIndex = (int)LoaiCongViec.BaiTapVeNha;
            vm.DoKho = "5"; // prior=2, user sets 5 → override

            await vm.ThemTaskCommand.ExecuteAsync(null);
            await Task.Delay(50);

            Assert.Single(logRepo.Added);
            var entry = logRepo.Added[0];
            Assert.Equal(2, entry.SuggestedDoKho); // prior for BaiTapVeNha
            Assert.Equal(5, entry.FinalDoKho);
            Assert.True(entry.WasOverride);
        }

        [Fact]
        public async Task ThemTask_SaveSucceeds_EvenWhenLogRepoThrows()
        {
            var (vm, logRepo) = BuildVm();
            logRepo.ShouldThrow = true;
            vm.TenTask = "Task sẽ lưu được dù log lỗi";
            vm.HanChot = DateTime.Today.AddDays(5);
            vm.LoaiTaskIndex = (int)LoaiCongViec.KiemTraThuongXuyen;
            vm.DoKho = "3";

            // Should not throw — try/catch in LogDifficultyLabelAsync swallows it
            var ex = await Record.ExceptionAsync(() => vm.ThemTaskCommand.ExecuteAsync(null));
            Assert.Null(ex);
        }

        [Fact]
        public async Task ThemTask_NullLogRepo_SaveSucceeds()
        {
            // Ctor without difficultyLogRepo (default null) — should not throw
            var hocKy = new HocKy("HK", DateTime.Today);
            var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
            hocKy.DanhSachMonHoc.Add(monHoc);
            var vm = new QuanLyTaskViewModel(
                hocKy, monHoc,
                new FakeHocKyRepository(),
                new FakeTaskEditorRepository(),
                new FakeDecisionEngine(),
                new SmartStudyPlanner.Services.Telemetry.DebugStudyTelemetry(),
                new ParsingOrchestrator(new FakeClock(DateTime.Today)));
            vm.TenTask = "Task without log";
            vm.HanChot = DateTime.Today.AddDays(2);
            vm.DoKho = "3";

            var ex = await Record.ExceptionAsync(() => vm.ThemTaskCommand.ExecuteAsync(null));
            Assert.Null(ex);
        }
    }
}
