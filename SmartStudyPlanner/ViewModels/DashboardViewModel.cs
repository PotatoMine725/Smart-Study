using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SmartStudyPlanner.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IHocKyRepository _hocKyRepository;
        private readonly IDecisionEngine _decisionEngine;
        private readonly IWorkloadService _workloadService;
        private readonly IRiskAnalyzer _riskAnalyzer;
        private readonly IPipelineOrchestrator _pipelineOrchestrator;
        private readonly IStudyTelemetry _telemetry;
        private readonly IStreakManager _streak;
        private HocKy _hocKyHienTai;

        [ObservableProperty] private string tieuDe;
        [ObservableProperty] private string thongKe;
        [ObservableProperty] private ObservableCollection<TaskDashboardItem> top5Task = new();
        [ObservableProperty] private ObservableCollection<StatusSegment> trangThaiSegments = new();
        [ObservableProperty] private ObservableCollection<SubjectTimeProgress> tienDoThoiGian = new();
        [ObservableProperty] private double maxThoiGian = 1;
        [ObservableProperty] private ObservableCollection<SubjectWorkload> khoiLuongMonHoc = new();
        [ObservableProperty] private int maxKhoiLuong = 1;
        [ObservableProperty] private string chuoiStreak;
        [ObservableProperty] private ObservableCollection<ScheduledTask> lichHocHomNay = new();
        [ObservableProperty] private string tieuDeLichHomNay;
        [ObservableProperty] private ObservableCollection<AdaptationSuggestion> adaptationItems = new();
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool hasData;
        [ObservableProperty] private bool hasError;
        [ObservableProperty] private string emptyStateMessage = "Chưa có dữ liệu để hiển thị.";

        public int SoTaskHomNay => LichHocHomNay?.Count ?? 0;

        public bool HasAdaptations => AdaptationItems.Count > 0;

        public string TyLeHoanThanhText
        {
            get
            {
                var total = Top5Task?.Count ?? 0;
                if (total == 0) return "0%";
                var done = Top5Task!.Count(t => t.MucDoCanhBao == "An toàn");
                return $"{done * 100 / total}%";
            }
        }

        public Action<HocKy> OnNavigateToMonHoc { get; set; }
        public Action<HocKy, MonHoc> OnNavigateToTask { get; set; }

        public DashboardViewModel(HocKy hocKy)
            : this(hocKy,
                ServiceLocator.Get<IHocKyRepository>(),
                ServiceLocator.Get<IDecisionEngine>(),
                ServiceLocator.Get<IWorkloadService>(),
                ServiceLocator.Get<IRiskAnalyzer>(),
                ServiceLocator.Get<IPipelineOrchestrator>(),
                ServiceLocator.Get<IStudyTelemetry>(),
                ServiceLocator.Get<IStreakManager>())
        {
        }

        public DashboardViewModel(HocKy hocKy, IHocKyRepository hocKyRepository, IDecisionEngine decisionEngine, IWorkloadService workloadService, IRiskAnalyzer riskAnalyzer, IPipelineOrchestrator pipelineOrchestrator, IStudyTelemetry telemetry, IStreakManager streak)
        {
            _hocKyHienTai = hocKy;
            _hocKyRepository = hocKyRepository;
            _decisionEngine = decisionEngine;
            _workloadService = workloadService;
            _riskAnalyzer = riskAnalyzer;
            _pipelineOrchestrator = pipelineOrchestrator;
            _telemetry = telemetry;
            _streak = streak;
            LoadDuLieuDashboard();
        }

        private void ApplyAdaptations(IReadOnlyList<AdaptationSuggestion> adaptations)
        {
            AdaptationItems.Clear();
            foreach (var a in adaptations.Take(5)) AdaptationItems.Add(a);
        }

        public void LoadDuLieuDashboard()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                EmptyStateMessage = "Chưa có dữ liệu để hiển thị.";
                TieuDe = $"TỔNG QUAN - {_hocKyHienTai.Ten.ToUpper()}";
                _telemetry.Track("dashboard_open", new Dictionary<string, string> { ["semester"] = _hocKyHienTai.Ten });

                var pipelineResult = _pipelineOrchestrator.Execute(new PipelineContext
                {
                    Semester = _hocKyHienTai,
                    ReferenceTime = DateTimeOffset.Now,
                    Settings = new PipelineUserSettings
                    {
                        EnableRiskAssessment = true,
                        EnableAdaptation = true,
                        CapacityHours = _workloadService.GetCapacity()
                    }
                });

                var summary = BuildDashboardSummary(pipelineResult);
                ApplySummary(summary);
                ApplyChartData(summary);
                ApplySchedule(summary.ScheduleDay);
                ApplyAdaptations(pipelineResult.Adaptations);
                ApplyStreak();

                HasData = summary.TopTasks.Count > 0 || summary.ScheduleDay?.Tasks.Count > 0;
                if (!HasData)
                    EmptyStateMessage = "Bạn chưa có task hoạt động. Hãy thêm task mới ở màn Môn học & Bài tập.";
            }
            catch
            {
                HasError = true;
                HasData = false;
                EmptyStateMessage = "Không thể tải dữ liệu dashboard. Hãy thử mở lại trang.";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(SoTaskHomNay));
                OnPropertyChanged(nameof(TyLeHoanThanhText));
                OnPropertyChanged(nameof(HasAdaptations));
            }
        }

        private DashboardSummary BuildDashboardSummary(PipelineExecutionResult pipelineResult)
        {
            var todaySchedule = pipelineResult.Schedule.FirstOrDefault();
            var riskById = pipelineResult.RiskReport.ToDictionary(r => r.TaskId);

            int tongSoMon = _hocKyHienTai.DanhSachMonHoc.Count;
            var topTasks = new List<TaskDashboardItem>();
            var monLabels = new List<string>();
            var taskCounts = new List<int>();
            var expectedMinutes = new List<double>();
            var actualMinutes = new List<double>();
            int countKhanCap = 0, countChuY = 0, countAnToan = 0, countDaXong = 0;

            foreach (var mon in _hocKyHienTai.DanhSachMonHoc)
            {
                monLabels.Add(TruncateLabel(mon.TenMonHoc));

                int taskCount = 0;
                double expected = 0;
                double actual = 0;

                foreach (var task in mon.DanhSachTask)
                {
                    taskCount++;
                    expected += _decisionEngine.CalculateRawSuggestedMinutes(task);
                    actual += task.ThoiGianDaHoc;

                    var warningLevel = GetWarningLevel(task);
                    if (task.TrangThai == StudyTaskStatus.HoanThanh) countDaXong++;
                    else if (task.DiemUuTien >= 80) countKhanCap++;
                    else if (task.DiemUuTien >= 50) countChuY++;
                    else countAnToan++;

                    if (task.TrangThai != StudyTaskStatus.HoanThanh)
                    {
                        var risk = riskById.TryGetValue(task.MaTask, out var cached)
                            ? cached
                            : _riskAnalyzer.Assess(task, mon); // fallback: pipeline was skipped
                        bool isMl;
                        var predictedMinutes = _decisionEngine.PredictStudyMinutes(task, mon, out isMl);
                        topTasks.Add(new TaskDashboardItem
                        {
                            TenMonHoc = mon.TenMonHoc,
                            TenTask = task.TenTask,
                            HanChot = task.HanChot,
                            DiemUuTien = task.DiemUuTien,
                            MucDoCanhBao = warningLevel,
                            ThoiGianGoiY = isMl ? $"{predictedMinutes} phút" : _decisionEngine.SuggestStudyTime(task),
                            TaskGoc = task,
                            MonHocGoc = mon,
                            IsMLPrediction = isMl,
                            MucDoRuiRo = risk.DisplayLabel,
                            RiskScore = risk.Score
                        });
                    }
                }

                taskCounts.Add(taskCount);
                expectedMinutes.Add(expected);
                actualMinutes.Add(actual);
            }

            var top5 = topTasks.OrderByDescending(t => t.DiemUuTien).Take(5).ToList();
            return new DashboardSummary(
                tongSoMon,
                topTasks.Count,
                top5,
                monLabels,
                taskCounts,
                expectedMinutes,
                actualMinutes,
                countKhanCap,
                countChuY,
                countAnToan,
                countDaXong,
                todaySchedule);
        }

        private void ApplySummary(DashboardSummary summary)
        {
            ThongKe = $"Bạn đang quản lý {summary.TotalSubjects} môn học và có {summary.TotalOpenTasks} deadline chưa hoàn thành.";
            Top5Task.Clear();
            foreach (var item in summary.TopTasks) Top5Task.Add(item);
        }

        private void ApplyChartData(DashboardSummary summary)
        {
            // Status donut
            TrangThaiSegments.Clear();
            if (summary.UrgentCount    > 0) TrangThaiSegments.Add(new StatusSegment("Urgent", "Khẩn cấp", summary.UrgentCount));
            if (summary.AttentionCount > 0) TrangThaiSegments.Add(new StatusSegment("Warn",   "Chú ý",    summary.AttentionCount));
            if (summary.SafeCount      > 0) TrangThaiSegments.Add(new StatusSegment("Safe",   "An toàn",  summary.SafeCount));
            if (summary.CompletedCount > 0) TrangThaiSegments.Add(new StatusSegment("Done",   "Đã xong",  summary.CompletedCount));

            // Time progress bars - top 5 subjects by peak time
            TienDoThoiGian.Clear();
            var topTienDo = summary.SubjectLabels
                .Select((label, i) => new SubjectTimeProgress(label, summary.ExpectedMinutes[i], summary.ActualMinutes[i]))
                .OrderByDescending(x => Math.Max(x.Expected, x.Actual))
                .Take(5);
            foreach (var item in topTienDo) TienDoThoiGian.Add(item);
            MaxThoiGian = TienDoThoiGian.Count > 0
                ? TienDoThoiGian.Max(x => Math.Max(x.Expected, x.Actual))
                : 1;
            if (MaxThoiGian <= 0) MaxThoiGian = 1;

            // Workload bars - top 5 subjects by task count
            KhoiLuongMonHoc.Clear();
            var topKhoiLuong = summary.SubjectLabels
                .Select((label, i) => new SubjectWorkload(label, summary.TaskCounts[i]))
                .OrderByDescending(x => x.Count)
                .Take(5);
            foreach (var item in topKhoiLuong) KhoiLuongMonHoc.Add(item);
            MaxKhoiLuong = KhoiLuongMonHoc.Count > 0
                ? KhoiLuongMonHoc.Max(x => x.Count)
                : 1;
            if (MaxKhoiLuong <= 0) MaxKhoiLuong = 1;
        }

        private void ApplySchedule(ScheduleDay? todaySchedule)
        {
            LichHocHomNay.Clear();
            if (todaySchedule?.Tasks.Count > 0)
            {
                foreach (var task in todaySchedule.Tasks) LichHocHomNay.Add(task);
                TieuDeLichHomNay = $"🎯 KẾ HOẠCH HỌC TẬP HÔM NAY ({todaySchedule.TotalMinutes} phút)";
            }
            else
            {
                TieuDeLichHomNay = "🎯 KẾ HOẠCH HỌC TẬP HÔM NAY (Không có lịch học tự động hôm nay)";
            }
        }

        private void ApplyStreak()
        {
            var dataStreak = _streak.GetCurrentStreak();
            ChuoiStreak = $"🔥 {dataStreak.StreakCount} Ngày";
        }

        private static string GetWarningLevel(StudyTask task)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh) return "Đã xong";
            if (task.DiemUuTien >= 80) return "Khẩn cấp";
            if (task.DiemUuTien >= 50) return "Chú ý";
            return "An toàn";
        }

        private static string TruncateLabel(string label)
        {
            return label.Length > 15 ? label[..12] + "..." : label;
        }

        [RelayCommand]
        private void MoQuanLyMonHoc() => OnNavigateToMonHoc?.Invoke(_hocKyHienTai);

        [RelayCommand]
        private async Task LuuDuLieu()
        {
            _telemetry.Track("dashboard_click_save");
            await _hocKyRepository.LuuHocKyAsync(_hocKyHienTai);
            System.Windows.MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void DiToiTask(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon == null) return;
            _telemetry.Track("dashboard_click_goto_task", new Dictionary<string, string> { ["task"] = taskDuocChon.TenTask });
            MonHoc? monHocCanTim = _hocKyHienTai.DanhSachMonHoc.FirstOrDefault(m => m.TenMonHoc == taskDuocChon.TenMonHoc);
            if (monHocCanTim != null) OnNavigateToTask?.Invoke(_hocKyHienTai, monHocCanTim);
        }

        [RelayCommand]
        private async Task MoFocusMode(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon == null) return;
            _telemetry.Track("focus_start", new Dictionary<string, string> { ["task"] = taskDuocChon.TenTask });
            var focusWin = new Views.FocusWindow(taskDuocChon);
            focusWin.ShowDialog();
            await _hocKyRepository.LuuHocKyAsync(_hocKyHienTai);
            LoadDuLieuDashboard();
        }

        [RelayCommand]
        private void ToggleTheme() => Services.ThemeManager.ToggleTheme();

        private sealed record DashboardSummary(
            int TotalSubjects,
            int TotalOpenTasks,
            IReadOnlyList<TaskDashboardItem> TopTasks,
            IReadOnlyList<string> SubjectLabels,
            IReadOnlyList<int> TaskCounts,
            IReadOnlyList<double> ExpectedMinutes,
            IReadOnlyList<double> ActualMinutes,
            int UrgentCount,
            int AttentionCount,
            int SafeCount,
            int CompletedCount,
            ScheduleDay? ScheduleDay);
    }
}
