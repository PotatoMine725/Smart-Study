using System;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    // A6: the StudyLog write used to be fire-and-forget (`_ = repo.AddAsync(...)`), so a failure
    // was silently discarded. This locks the fix: the write is awaited and its failure is
    // observable via the command's ExecutionTask instead of vanishing.
    public class FocusViewModelA6Tests
    {
        private static (FocusViewModel vm, FakeStudyLogRepository repo) BuildVm()
        {
            var task = new StudyTask("T1", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
            };
            var item = new TaskDashboardItem { TaskGoc = task, TenTask = "T1", TenMonHoc = "Toán" };
            var repo = new FakeStudyLogRepository { ShouldThrow = true };
            var vm = new FocusViewModel(item, repo);
            return (vm, repo);
        }

        [Fact]
        public async System.Threading.Tasks.Task HoanThanh_RepoThrows_FaultIsObservableViaExecutionTask()
        {
            var (vm, _) = BuildVm();
            vm.SimulateStudySeconds(60);

            vm.HoanThanhCommand.Execute(null);
            await System.Threading.Tasks.Task.Yield();

            var execTask = ((IAsyncRelayCommand)vm.HoanThanhCommand).ExecutionTask;
            Assert.NotNull(execTask);
            Assert.True(execTask!.IsFaulted, "write failure must surface via ExecutionTask, not be swallowed");
        }

        [Fact]
        public async System.Threading.Tasks.Task ThoatKhanCap_RepoThrows_FaultIsObservableViaExecutionTask()
        {
            var (vm, _) = BuildVm();
            vm.SimulateStudySeconds(60);

            vm.ThoatKhanCapCommand.Execute(null);
            await System.Threading.Tasks.Task.Yield();

            var execTask = ((IAsyncRelayCommand)vm.ThoatKhanCapCommand).ExecutionTask;
            Assert.NotNull(execTask);
            Assert.True(execTask!.IsFaulted, "write failure must surface via ExecutionTask, not be swallowed");
        }

        [Fact]
        public async System.Threading.Tasks.Task HoanThanh_NewStudyLog_HasDeviceIdStamped()
        {
            var task = new StudyTask("T1", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
            };
            var item = new TaskDashboardItem { TaskGoc = task, TenTask = "T1", TenMonHoc = "Toán" };
            var repo = new FakeStudyLogRepository();
            var vm = new FocusViewModel(item, repo);
            vm.SimulateStudySeconds(60);

            vm.HoanThanhCommand.Execute(null);
            await System.Threading.Tasks.Task.Yield();

            var log = Assert.Single(repo.AddedLogs);
            Assert.False(string.IsNullOrEmpty(log.DeviceId));
        }
    }
}
