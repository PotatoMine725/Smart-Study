using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    public class FocusViewModelOutcomeLogTests
    {
        private static (FocusViewModel vm, SpyStudyTimeOutcomeLogRepository spy) BuildVm(
            StudyTask? task = null,
            MonHoc? monHoc = null,
            bool isMLPrediction = false)
        {
            var monHocGoc = monHoc ?? new MonHoc("MH Test", 3) { MaHocKy = Guid.NewGuid() };
            var studyTask = task ?? new StudyTask("T1", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHocGoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
                ThoiGianDaHoc = 10,
            };
            var dashItem = new TaskDashboardItem
            {
                TenMonHoc = monHocGoc.TenMonHoc,
                TenTask = studyTask.TenTask,
                HanChot = studyTask.HanChot,
                TaskGoc = studyTask,
                MonHocGoc = monHocGoc,
                IsMLPrediction = isMLPrediction,
            };

            var spy = new SpyStudyTimeOutcomeLogRepository();
            var studyLogRepo = new CapturingStudyLogRepository();
            var vm = new FocusViewModel(dashItem, studyLogRepo, new NullStudyTelemetryForTest(), spy);
            return (vm, spy);
        }

        [Fact]
        public async Task OneSession_WritesExactlyOneOutcomeRow()
        {
            var (vm, spy) = BuildVm();
            vm.SimulateStudySeconds(120); // 2 minutes
            vm.HoanThanhCommand.Execute(null);

            // Fire-and-forget Task completes before assert since spy is synchronous
            await Task.Yield();
            Assert.Single(spy.Entries);
        }

        [Fact]
        public async Task OutcomeRow_MappingIsCorrect()
        {
            var monHoc = new MonHoc("Toán", 4) { MaHocKy = Guid.NewGuid() };
            var task = new StudyTask("Bài tập", DateTime.Today.AddDays(7), LoaiCongViec.ThiCuoiKy, 3)
            {
                MaMonHoc = monHoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
                ThoiGianDaHoc = 20,
            };
            var (vm, spy) = BuildVm(task: task, monHoc: monHoc, isMLPrediction: true);
            vm.SimulateStudySeconds(180); // 3 minutes

            vm.HoanThanhCommand.Execute(null);
            await Task.Yield();

            var row = Assert.Single(spy.Entries);
            Assert.Equal((int)LoaiCongViec.ThiCuoiKy, row.TaskType);
            Assert.Equal(3f, row.Difficulty);
            Assert.Equal(4f, row.Credits);
            Assert.Equal(20f, row.StudiedMinutesSoFar); // pre-increment (T1)
            Assert.Equal(3f, row.ActualMinutes);         // 180s / 60
            Assert.True(row.WasMlPrediction);
            Assert.Null(row.PredictedMinutes);
            Assert.Null(row.Confidence);
        }

        [Fact]
        public async Task StudiedMinutesSoFar_IsPreIncrementValue()
        {
            var task = new StudyTask("T", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
                ThoiGianDaHoc = 30, // pre-session value
            };
            var (vm, spy) = BuildVm(task: task);
            vm.SimulateStudySeconds(60); // 1 minute session

            vm.ThoatKhanCapCommand.Execute(null);
            await Task.Yield();

            var row = Assert.Single(spy.Entries);
            Assert.Equal(30f, row.StudiedMinutesSoFar); // must NOT include the 1 min just studied
            Assert.Equal(1f, row.ActualMinutes);
        }

        [Fact]
        public async Task ZeroMinuteSession_WritesNoOutcomeRow()
        {
            var (vm, spy) = BuildVm();
            // No SimulateStudySeconds — 0 seconds => 0 minutes

            vm.HoanThanhCommand.Execute(null);
            await Task.Yield();

            Assert.Empty(spy.Entries);
        }

        [Fact]
        public async Task NullMonHocGoc_DoesNotThrow_DefaultsCreditsToZero()
        {
            var task = new StudyTask("T", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 2)
            {
                MucDoCanhBao = "An toàn",
            };
            var dashItem = new TaskDashboardItem
            {
                TenTask = task.TenTask,
                TenMonHoc = "?",
                HanChot = task.HanChot,
                TaskGoc = task,
                MonHocGoc = null, // guard T4
            };
            var spy = new SpyStudyTimeOutcomeLogRepository();
            var vm = new FocusViewModel(dashItem, new CapturingStudyLogRepository(), new NullStudyTelemetryForTest(), spy);

            vm.SimulateStudySeconds(60);
            vm.HoanThanhCommand.Execute(null);
            await Task.Yield();

            var row = Assert.Single(spy.Entries);
            Assert.Equal(0f, row.Credits);
        }

        // StudyLog.DeviceId là thiết bị TẠO log, tách biệt với ModifiedByDeviceId mà
        // SyncStamper đóng dấu. Sau WP-3.2 nó phải chảy qua seam chứ không gọi thẳng
        // DeviceHelper, nếu không một row sẽ mang hai danh tính khác nhau khi user đổi
        // hostname — thứ Epic 2 không hoà giải được.
        [Fact]
        public async Task StudyLog_DeviceId_LayTuProviderDuocInject()
        {
            var monHoc = new MonHoc("MH Test", 3) { MaHocKy = Guid.NewGuid() };
            var task = new StudyTask("T1", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = monHoc.MaMonHoc,
                MucDoCanhBao = "An toàn",
            };
            var dashItem = new TaskDashboardItem
            {
                TenMonHoc = monHoc.TenMonHoc,
                TenTask = task.TenTask,
                HanChot = task.HanChot,
                TaskGoc = task,
                MonHocGoc = monHoc,
            };

            var studyLogRepo = new CapturingStudyLogRepository();
            var vm = new FocusViewModel(
                dashItem, studyLogRepo, new NullStudyTelemetryForTest(),
                new SpyStudyTimeOutcomeLogRepository(), new NullStreakManagerForTest(),
                () => "desktop-injected");

            vm.SimulateStudySeconds(60);
            vm.HoanThanhCommand.Execute(null);
            await Task.Yield();

            var log = Assert.Single(studyLogRepo.Logs);
            Assert.Equal("desktop-injected", log.DeviceId);
        }

        // ---- test doubles ----

        private sealed class SpyStudyTimeOutcomeLogRepository : IStudyTimeOutcomeLogRepository
        {
            public List<StudyTimeOutcomeLog> Entries { get; } = new();
            public Task AddAsync(StudyTimeOutcomeLog entry, CancellationToken ct = default)
            {
                Entries.Add(entry);
                return Task.CompletedTask;
            }
            public Task<IReadOnlyList<StudyTimeOutcomeLog>> GetAllAsync(CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(Entries);
            public Task<IReadOnlyList<StudyTimeOutcomeLog>> GetSinceAsync(DateTime since, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(Entries);
            public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(Entries.Count);
        }

        private sealed class CapturingStudyLogRepository : IStudyLogRepository
        {
            public List<StudyLog> Logs { get; } = new();
            public Task AddAsync(StudyLog log, CancellationToken ct = default)
            {
                Logs.Add(log);
                return Task.CompletedTask;
            }
            public Task<List<StudyLog>> GetByTaskAsync(Guid maTask, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetSinceAsync(DateTime since, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetForHocKyAsync(HocKy hocKy, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
        }

        private sealed class NullStudyTelemetryForTest : IStudyTelemetry
        {
            public void Track(string eventName, IDictionary<string, string>? properties = null) { }
        }

        // FocusViewModel có sẵn NullStreakManager nhưng nó là private nested — ctor 6 tham số
        // cần một cái nhìn thấy được từ đây. Không ghi streak_data.json.
        private sealed class NullStreakManagerForTest : IStreakManager
        {
            public UserStreakData GetCurrentStreak() => new UserStreakData();
            public void UpdateStreak() { }
        }
    }
}
