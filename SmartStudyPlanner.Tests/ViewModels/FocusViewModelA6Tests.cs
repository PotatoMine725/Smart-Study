using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    // A6: the StudyLog write used to be fire-and-forget (`_ = repo.AddAsync(...)`), so a failure
    // was silently discarded. This locks the fix: the write is awaited, and a failure is caught,
    // logged via telemetry ("autosave_failed") and surfaced to the user (R5) instead of vanishing
    // or being left as an unobserved ExecutionTask fault.
    public class FocusViewModelA6Tests
    {
        private sealed class FakeStudyTelemetry : IStudyTelemetry
        {
            public List<(string EventName, IDictionary<string, string>? Properties)> Events { get; } = new();
            public void Track(string eventName, IDictionary<string, string>? properties = null)
                => Events.Add((eventName, properties));
        }

        private static (FocusViewModel vm, FakeStudyLogRepository repo, FakeStudyTelemetry telemetry) BuildVm(bool repoShouldThrow)
        {
            var task = new StudyTask("T1", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
            };
            var item = new TaskDashboardItem { TaskGoc = task, TenTask = "T1", TenMonHoc = "Toán" };
            var repo = new FakeStudyLogRepository { ShouldThrow = repoShouldThrow };
            var telemetry = new FakeStudyTelemetry();
            var vm = new FocusViewModel(item, repo, telemetry);
            return (vm, repo, telemetry);
        }

        [Fact]
        public async System.Threading.Tasks.Task HoanThanh_RepoThrows_TracksAutosaveFailedAndNotifiesUser()
        {
            var (vm, _, telemetry) = BuildVm(repoShouldThrow: true);
            vm.SimulateStudySeconds(60);
            string? notice = null;
            vm.NotifyUser = message => notice = message;
            var ketThucCalled = false;
            vm.OnKetThuc = () => ketThucCalled = true;

            vm.HoanThanhCommand.Execute(null);
            var execTask = ((IAsyncRelayCommand)vm.HoanThanhCommand).ExecutionTask;
            Assert.NotNull(execTask);
            await execTask!; // must NOT fault: the failure is caught, not left unobserved

            Assert.Contains(telemetry.Events, e => e.EventName == "autosave_failed");
            Assert.False(string.IsNullOrEmpty(notice));
            // Save failed — must not mark the task complete, or close the view, on top of a failed persist.
            Assert.Null(vm.TaskHienTai.TaskGoc.NgayHoanThanh);
            Assert.False(ketThucCalled);
        }

        [Fact]
        public async System.Threading.Tasks.Task ThoatKhanCap_RepoThrows_TracksAutosaveFailedAndNotifiesUser()
        {
            var (vm, _, telemetry) = BuildVm(repoShouldThrow: true);
            vm.SimulateStudySeconds(60);
            string? notice = null;
            vm.NotifyUser = message => notice = message;
            var ketThucCalled = false;
            vm.OnKetThuc = () => ketThucCalled = true;

            vm.ThoatKhanCapCommand.Execute(null);
            var execTask = ((IAsyncRelayCommand)vm.ThoatKhanCapCommand).ExecutionTask;
            Assert.NotNull(execTask);
            await execTask!; // must NOT fault: the failure is caught, not left unobserved

            Assert.Contains(telemetry.Events, e => e.EventName == "autosave_failed");
            // "The user hit emergency exit" is tracked regardless of save outcome — a separate
            // fact from the save failure, so it must not be suppressed by a failed autosave.
            Assert.Contains(telemetry.Events, e => e.EventName == "focus_abort");
            Assert.False(string.IsNullOrEmpty(notice));
            // Emergency exit must always close the focus-lock window, even on a failed save.
            Assert.True(ketThucCalled);
        }

        [Fact]
        public async System.Threading.Tasks.Task HoanThanh_NewStudyLog_HasDeviceIdStamped()
        {
            var (vm, repo, _) = BuildVm(repoShouldThrow: false);
            vm.SimulateStudySeconds(60);

            vm.HoanThanhCommand.Execute(null);
            await System.Threading.Tasks.Task.Yield();

            var log = Assert.Single(repo.AddedLogs);
            Assert.False(string.IsNullOrEmpty(log.DeviceId));
        }
    }
}
