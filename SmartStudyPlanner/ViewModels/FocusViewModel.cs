using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SmartStudyPlanner.ViewModels
{
    public partial class FocusViewModel : ObservableObject
    {
        private DispatcherTimer _timer;
        private int _thoiGianConLai;
        private bool _dangHoc = true;

        // BIẾN MỚI: Đếm tổng số giây THỰC TẾ đã ngồi học
        private int _tongGiayDaHoc = 0;

        private readonly IStudyLogRepository _studyLogRepository;
        private readonly IStudyTelemetry _telemetry;
        private readonly IStudyTimeOutcomeLogRepository _outcomeLogRepo;
        private readonly IStreakManager _streak;

        public TaskDashboardItem TaskHienTai { get; set; }

        [ObservableProperty] private string tieuDeTask;
        [ObservableProperty] private string thoiGianHienThi;
        [ObservableProperty] private string trangThaiText;
        [ObservableProperty] private string mauTrangThai;
        [ObservableProperty] private string tienDoText;

        public Action OnKetThuc { get; set; }

        // Production: resolve streak thật (disk-backed) qua ServiceLocator.
        public FocusViewModel(TaskDashboardItem task)
            : this(task, ServiceLocator.Get<IStudyLogRepository>(), ServiceLocator.Get<IStudyTelemetry>(), ServiceLocator.Get<IStudyTimeOutcomeLogRepository>(), ServiceLocator.Get<IStreakManager>()) { }
        // Các ctor test-facing default về NullStreakManager để không chạm đĩa (mirror NullStudyTelemetry).
        public FocusViewModel(TaskDashboardItem task, IStudyLogRepository studyLogRepository)
            : this(task, studyLogRepository, new NullStudyTelemetry(), new NullStudyTimeOutcomeLogRepository(), new NullStreakManager()) { }
        public FocusViewModel(TaskDashboardItem task, IStudyLogRepository studyLogRepository, IStudyTelemetry telemetry)
            : this(task, studyLogRepository, telemetry, new NullStudyTimeOutcomeLogRepository(), new NullStreakManager()) { }
        public FocusViewModel(TaskDashboardItem task, IStudyLogRepository studyLogRepository, IStudyTelemetry telemetry, IStudyTimeOutcomeLogRepository outcomeLogRepo)
            : this(task, studyLogRepository, telemetry, outcomeLogRepo, new NullStreakManager()) { }

        public FocusViewModel(TaskDashboardItem task, IStudyLogRepository studyLogRepository, IStudyTelemetry telemetry, IStudyTimeOutcomeLogRepository outcomeLogRepo, IStreakManager streak)
        {
            TaskHienTai = task;
            _studyLogRepository = studyLogRepository;
            _telemetry = telemetry;
            _outcomeLogRepo = outcomeLogRepo;
            _streak = streak;
            TieuDeTask = $"Đang Focus: {task.TenTask} ({task.TenMonHoc})";

            ThietLapPomodoro(true);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        public void SimulateStudySeconds(int seconds) => _tongGiayDaHoc += seconds;

        private void ThietLapPomodoro(bool laHoc)
        {
            _dangHoc = laHoc;
            _thoiGianConLai = laHoc ? 25 * 60 : 5 * 60;
            TrangThaiText = laHoc ? "THỜI GIAN TẬP TRUNG" : "GIẢI LAO";
            MauTrangThai = laHoc ? "#E74C3C" : "#2ECC71";
            CapNhatGiaoDienThoiGian();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _thoiGianConLai--;

            // NẾU ĐANG LÀ CHẾ ĐỘ HỌC THÌ CỘNG DỒN THỜI GIAN ĐÃ NGỒI
            if (_dangHoc)
            {
                _tongGiayDaHoc++;

                // HIỂN THỊ TIẾN ĐỘ THỰC TẾ
                int tongPhutHienTai = TaskHienTai.TaskGoc.ThoiGianDaHoc + (_tongGiayDaHoc / 60);
                TienDoText = $"Tiến độ: Đã học {tongPhutHienTai} phút";
            }

            CapNhatGiaoDienThoiGian();

            if (_thoiGianConLai <= 0)
            {
                _timer.Stop();
                System.Media.SystemSounds.Exclamation.Play();
                ThietLapPomodoro(!_dangHoc);
                _timer.Start();
            }
        }

        private void CapNhatGiaoDienThoiGian()
        {
            int phut = _thoiGianConLai / 60;
            int giay = _thoiGianConLai % 60;
            ThoiGianHienThi = $"{phut:D2}:{giay:D2}";
        }

        // --- CÁC HÀM XỬ LÝ KẾT THÚC VÀ LƯU DỮ LIỆU ---
        // A6: awaited (not fire-and-forget) so a write failure surfaces via the calling command's
        // ExecutionTask instead of being silently discarded.
        private async Task LuuThoiGianThucTe(bool daHoanThanh)
        {
            int phutDaHoc = _tongGiayDaHoc / 60;
            if (phutDaHoc > 0)
            {
                // T1: capture pre-increment value before += so StudiedMinutesSoFar matches
                // the feature the predictor saw at prediction time (task.ThoiGianDaHoc before this session).
                int studiedSoFar = TaskHienTai.TaskGoc.ThoiGianDaHoc;
                TaskHienTai.TaskGoc.ThoiGianDaHoc += phutDaHoc;
                _streak.UpdateStreak();

                await _outcomeLogRepo.AddAsync(new StudyTimeOutcomeLog
                {
                    Id                  = Guid.NewGuid(),
                    CreatedUtc          = DateTime.UtcNow,
                    MaTask              = TaskHienTai.TaskGoc.MaTask,
                    TaskType            = (int)TaskHienTai.TaskGoc.LoaiTask,
                    Difficulty          = TaskHienTai.TaskGoc.DoKho,
                    Credits             = (float)(TaskHienTai.MonHocGoc?.SoTinChi ?? 0),
                    DaysLeft            = (float)(TaskHienTai.TaskGoc.HanChot - DateTime.Today).TotalDays,
                    StudiedMinutesSoFar = studiedSoFar,
                    ActualMinutes       = phutDaHoc,
                    PredictedMinutes    = null,
                    WasMlPrediction     = TaskHienTai.IsMLPrediction,
                    Confidence          = null,
                });
            }

            // DeviceId stamped at the write site (A6 scope) — StudyLog doesn't implement
            // ISyncMetadata until M1.2's T1.1, so this is independent of the SyncStamper seam.
            await _studyLogRepository.AddAsync(new StudyLog
            {
                MaTask       = TaskHienTai.TaskGoc.MaTask,
                NgayHoc      = DateTime.Today,
                SoPhutHoc    = phutDaHoc,
                SoPhutDuKien = 0,
                DaHoanThanh  = daHoanThanh,
                DeviceId     = DeviceHelper.GetId(),
            });
        }

        [RelayCommand] private void BatDau() => _timer.Start();
        [RelayCommand] private void TamDung() => _timer.Stop();

        [RelayCommand(FlowExceptionsToTaskScheduler = true)]
        private async Task HoanThanh()
        {
            _timer.Stop();
            await LuuThoiGianThucTe(true);
            _telemetry.Track("focus_complete", new System.Collections.Generic.Dictionary<string, string> { ["task"] = TaskHienTai.TenTask });
            TaskHienTai.TaskGoc.NgayHoanThanh = DateTime.Today;
            TaskHienTai.TaskGoc.TrangThai = StudyTaskStatus.HoanThanh;
            OnKetThuc?.Invoke();
        }

        [RelayCommand(FlowExceptionsToTaskScheduler = true)]
        private async Task ThoatKhanCap()
        {
            _timer.Stop();
            await LuuThoiGianThucTe(false);
            _telemetry.Track("focus_abort", new System.Collections.Generic.Dictionary<string, string> { ["task"] = TaskHienTai.TenTask });
            OnKetThuc?.Invoke();
        }

        private sealed class NullStudyTelemetry : IStudyTelemetry
        {
            public void Track(string eventName, IDictionary<string, string>? properties = null) { }
        }

        // No-op streak: dùng cho ctor test-facing để không ghi streak_data.json.
        private sealed class NullStreakManager : IStreakManager
        {
            public UserStreakData GetCurrentStreak() => new UserStreakData();
            public void UpdateStreak() { }
        }

        private sealed class NullStudyTimeOutcomeLogRepository : IStudyTimeOutcomeLogRepository
        {
            public static readonly NullStudyTimeOutcomeLogRepository Instance = new();
            public Task AddAsync(StudyTimeOutcomeLog entry, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<StudyTimeOutcomeLog>> GetAllAsync(System.Threading.CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(Array.Empty<StudyTimeOutcomeLog>());
            public Task<IReadOnlyList<StudyTimeOutcomeLog>> GetSinceAsync(DateTime since, System.Threading.CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(Array.Empty<StudyTimeOutcomeLog>());
            public Task<int> CountAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(0);
        }
    }
}
