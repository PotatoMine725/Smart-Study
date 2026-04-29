using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Toolkit.Uwp.Notifications;
using SkiaSharp;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.RiskAnalyzer;
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
        private readonly IStudyRepository _repository;
        private readonly IDecisionEngine _decisionEngine;
        private readonly IWorkloadService _workloadService;
        private readonly IRiskAnalyzer _riskAnalyzer;
        private readonly IPipelineOrchestrator _pipelineOrchestrator;
        private HocKy _hocKyHienTai;
        private static bool _daThongBao;

        [ObservableProperty] private string tieuDe;
        [ObservableProperty] private string thongKe;
        [ObservableProperty] private ObservableCollection<TaskDashboardItem> top5Task = new();
        [ObservableProperty] private ISeries[] bieuDoTrangThai;
        [ObservableProperty] private ISeries[] bieuDoMonHoc;
        [ObservableProperty] private Axis[] trucXMonHoc;
        [ObservableProperty] private ISeries[] bieuDoThoiGian;
        [ObservableProperty] private Axis[] trucXThoiGian;
        [ObservableProperty] private string chuoiStreak;
        [ObservableProperty] private ObservableCollection<ScheduledTask> lichHocHomNay = new();
        [ObservableProperty] private string tieuDeLichHomNay;
        [ObservableProperty] private ObservableCollection<AdaptationSuggestion> adaptationItems = new();

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
                ServiceLocator.Get<IStudyRepository>(),
                ServiceLocator.Get<IDecisionEngine>(),
                ServiceLocator.Get<IWorkloadService>(),
                ServiceLocator.Get<IRiskAnalyzer>(),
                ServiceLocator.Get<IPipelineOrchestrator>())
        {
        }

        public DashboardViewModel(HocKy hocKy, IStudyRepository repository, IDecisionEngine decisionEngine, IWorkloadService workloadService, IRiskAnalyzer riskAnalyzer, IPipelineOrchestrator pipelineOrchestrator)
        {
            _hocKyHienTai = hocKy;
            _repository = repository;
            _decisionEngine = decisionEngine;
            _workloadService = workloadService;
            _riskAnalyzer = riskAnalyzer;
            _pipelineOrchestrator = pipelineOrchestrator;
            LoadDuLieuDashboard();
        }

        private void ApplyAdaptations(IReadOnlyList<AdaptationSuggestion> adaptations)
        {
            AdaptationItems.Clear();
            foreach (var a in adaptations) AdaptationItems.Add(a);
        }

        public void LoadDuLieuDashboard()
        {
            TieuDe = $"TỔNG QUAN - {_hocKyHienTai.Ten.ToUpper()}";

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
            ApplyCharts(summary);
            ApplySchedule(summary.ScheduleDay);
            ApplyAdaptations(pipelineResult.Adaptations);
            ApplyStreak();
            RaiseNotification(summary.TopTasks);
            OnPropertyChanged(nameof(SoTaskHomNay));
            OnPropertyChanged(nameof(TyLeHoanThanhText));
            OnPropertyChanged(nameof(HasAdaptations));
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

        private void ApplyCharts(DashboardSummary summary)
        {
            BieuDoTrangThai = new ISeries[]
            {
                new PieSeries<int> { Values = new[] { summary.UrgentCount }, Name = "Khẩn cấp", Fill = new SolidColorPaint(SKColors.Crimson) },
                new PieSeries<int> { Values = new[] { summary.AttentionCount }, Name = "Chú ý", Fill = new SolidColorPaint(SKColors.Orange) },
                new PieSeries<int> { Values = new[] { summary.SafeCount }, Name = "An toàn", Fill = new SolidColorPaint(SKColors.MediumSeaGreen) },
                new PieSeries<int> { Values = new[] { summary.CompletedCount }, Name = "Đã xong", Fill = new SolidColorPaint(SKColors.Gray) }
            };

            BieuDoMonHoc = new ISeries[]
            {
                new ColumnSeries<int> { Values = summary.TaskCounts.ToArray(), Name = "Số bài tập", Fill = new SolidColorPaint(SKColors.CornflowerBlue) }
            };
            TrucXMonHoc = new[] { new Axis { Labels = summary.SubjectLabels.ToArray(), LabelsRotation = 15 } };

            BieuDoThoiGian = new ISeries[]
            {
                new ColumnSeries<double> { Values = summary.ExpectedMinutes.ToArray(), Name = "Kỳ vọng (phút)", Fill = new SolidColorPaint(SKColors.CornflowerBlue) },
                new ColumnSeries<double> { Values = summary.ActualMinutes.ToArray(), Name = "Thực tế đã học (phút)", Fill = new SolidColorPaint(SKColors.MediumSeaGreen) }
            };
            TrucXThoiGian = new[] { new Axis { Labels = summary.SubjectLabels.ToArray(), LabelsRotation = 15 } };
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
                TieuDeLichHomNay = "🎯 KẾ HOẠCH HỌC TẬP HÔM NAY (Tuyệt vời, bạn không có deadline nào!)";
            }
        }

        private void ApplyStreak()
        {
            var dataStreak = Services.StreakManager.GetCurrentStreak();
            ChuoiStreak = $"🔥 {dataStreak.StreakCount} Ngày";
        }

        private void RaiseNotification(IReadOnlyList<TaskDashboardItem> topTasks)
        {
            if (_daThongBao) return;

            int urgentCount = topTasks.Count(t => t.MucDoCanhBao == "Khẩn cấp");
            if (urgentCount > 0)
            {
                new ToastContentBuilder()
                    .AddText("🔥 CẢNH BÁO DEADLINE!")
                    .AddText($"Bạn đang có {urgentCount} bài tập KHẨN CẤP cần xử lý ngay lập tức!")
                    .AddText("Hãy kiểm tra Smart Study Planner để xem gợi ý lịch học.")
                    .AddAudio(new Uri("ms-winsoundevent:Notification.Default"))
                    .Show();
                _daThongBao = true;
            }
            else if (topTasks.Count > 0)
            {
                new ToastContentBuilder()
                    .AddText("✅ Mọi thứ đang trong tầm kiểm soát!")
                    .AddText($"Bạn có {topTasks.Count} bài tập, nhưng chưa có gì quá hạn.")
                    .Show();
                _daThongBao = true;
            }
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
            await _repository.LuuHocKyAsync(_hocKyHienTai);
            System.Windows.MessageBox.Show("Đã lưu tiến trình thành công!", "Save Game", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void DiToiTask(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon == null) return;
            MonHoc? monHocCanTim = _hocKyHienTai.DanhSachMonHoc.FirstOrDefault(m => m.TenMonHoc == taskDuocChon.TenMonHoc);
            if (monHocCanTim != null) OnNavigateToTask?.Invoke(_hocKyHienTai, monHocCanTim);
        }

        [RelayCommand]
        private async Task MoFocusMode(TaskDashboardItem taskDuocChon)
        {
            if (taskDuocChon == null) return;
            var focusWin = new Views.FocusWindow(taskDuocChon);
            focusWin.ShowDialog();
            await _repository.LuuHocKyAsync(_hocKyHienTai);
            LoadDuLieuDashboard();
        }

        [RelayCommand]
        private void MoWorkloadBalancer()
        {
            var win = new Views.WorkloadBalancerWindow(_hocKyHienTai);
            win.Owner = System.Windows.Application.Current.MainWindow;
            win.ShowDialog();
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
