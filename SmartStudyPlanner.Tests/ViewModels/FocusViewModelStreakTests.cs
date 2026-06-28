using System;
using System.Collections.Generic;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    // Khóa cứng mảnh ghép wiring mà không test nào khác bảo vệ: HoanThanh phải gọi
    // IStreakManager.UpdateStreak(). Dùng spy qua ctor 5-arg, không chạm đĩa.
    public class FocusViewModelStreakTests
    {
        private static FocusViewModel BuildVm(IStreakManager streak)
        {
            var task = new StudyTask("T1", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);
            var item = new TaskDashboardItem { TaskGoc = task, TenTask = "T1", TenMonHoc = "Toán" };
            return new FocusViewModel(item, new FakeStudyLogRepository(), new NullStudyTelemetryForStreakTest(),
                new NullStudyTimeOutcomeLogRepositoryForStreakTest(), streak);
        }

        [Fact]
        public void HoanThanh_GoiUpdateStreak_KhiCoThoiGianHoc()
        {
            var spy = new SpyStreakManager();
            var vm = BuildVm(spy);

            vm.SimulateStudySeconds(120); // 2 phút > 0 -> kích hoạt nhánh cập nhật streak
            vm.HoanThanhCommand.Execute(null);

            Assert.Equal(1, spy.UpdateStreakCalls);
        }

        [Fact]
        public void HoanThanh_KhongGoiUpdateStreak_KhiChuaHocPhutNao()
        {
            var spy = new SpyStreakManager();
            var vm = BuildVm(spy);

            vm.HoanThanhCommand.Execute(null); // 0 phút -> không cập nhật streak

            Assert.Equal(0, spy.UpdateStreakCalls);
        }

        private sealed class SpyStreakManager : IStreakManager
        {
            public int UpdateStreakCalls { get; private set; }
            public UserStreakData GetCurrentStreak() => new UserStreakData();
            public void UpdateStreak() => UpdateStreakCalls++;
        }

        private sealed class NullStudyTelemetryForStreakTest : IStudyTelemetry
        {
            public void Track(string eventName, IDictionary<string, string>? properties = null) { }
        }

        private sealed class NullStudyTimeOutcomeLogRepositoryForStreakTest : IStudyTimeOutcomeLogRepository
        {
            public System.Threading.Tasks.Task AddAsync(StudyTimeOutcomeLog entry, System.Threading.CancellationToken ct = default)
                => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task<IReadOnlyList<StudyTimeOutcomeLog>> GetAllAsync(System.Threading.CancellationToken ct = default)
                => System.Threading.Tasks.Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(Array.Empty<StudyTimeOutcomeLog>());
            public System.Threading.Tasks.Task<IReadOnlyList<StudyTimeOutcomeLog>> GetSinceAsync(DateTime since, System.Threading.CancellationToken ct = default)
                => System.Threading.Tasks.Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(Array.Empty<StudyTimeOutcomeLog>());
            public System.Threading.Tasks.Task<int> CountAsync(System.Threading.CancellationToken ct = default)
                => System.Threading.Tasks.Task.FromResult(0);
        }
    }
}
