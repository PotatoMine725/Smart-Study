using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.Analytics.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Telemetry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SmartStudyPlanner.ViewModels
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly HocKy _hocKy;
        private readonly IStudyLogRepository _studyLogRepository;
        private readonly IStudyAnalytics _analytics;
        private readonly IStudyTelemetry _telemetry;
        private readonly IStudyTimeTrainingDataSource _trainingDataSource;
        private readonly IMLModelManager? _mlModelManager;
        private List<StudyLog> _allLogs = new();

        public HocKy HocKy => _hocKy;

        [ObservableProperty] private ISeries[] weeklyChartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] weeklyChartXAxes = new[] { new Axis() };
        [ObservableProperty] private ISeries[] subjectChartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] subjectChartXAxes = new[] { new Axis() };
        [ObservableProperty] private int productivityValue;
        [ObservableProperty] private string productivityLabel = "Chưa có dữ liệu";
        [ObservableProperty] private ObservableCollection<SubjectInsight> subjectInsights = new();
        [ObservableProperty] private bool isRetraining;
        [ObservableProperty] private bool hasEnoughData;
        [ObservableProperty] private ObservableCollection<HeatCell> heatmapCells = new();
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool hasData;
        [ObservableProperty] private bool hasError;
        [ObservableProperty] private string emptyStateMessage = "Chưa có dữ liệu học tập.";
        [ObservableProperty] private int selectedRangeDays = 30;
        [ObservableProperty] private string selectedSubject = "Tất cả";
        [ObservableProperty] private ObservableCollection<int> rangeOptions = new() { 7, 30, 90 };
        [ObservableProperty] private ObservableCollection<string> subjectOptions = new() { "Tất cả" };
        [ObservableProperty] private string weeklyNarrative = string.Empty;
        [ObservableProperty] private string recommendedNextAction = string.Empty;

        public AnalyticsViewModel(HocKy hocKy)
            : this(hocKy, ServiceLocator.Get<IStudyLogRepository>(), ServiceLocator.Get<IStudyAnalytics>(), ServiceLocator.Get<IStudyTelemetry>(), ServiceLocator.Get<IStudyTimeTrainingDataSource>()) { }

        public AnalyticsViewModel(HocKy hocKy, IStudyLogRepository studyLogRepository, IStudyAnalytics analytics, IStudyTelemetry telemetry)
            : this(hocKy, studyLogRepository, analytics, telemetry, new NullStudyTimeTrainingDataSource()) { }

        public AnalyticsViewModel(HocKy hocKy, IStudyLogRepository studyLogRepository, IStudyAnalytics analytics, IStudyTelemetry telemetry, IStudyTimeTrainingDataSource trainingDataSource)
            : this(hocKy, studyLogRepository, analytics, telemetry, trainingDataSource, null) { }

        public AnalyticsViewModel(HocKy hocKy, IStudyLogRepository studyLogRepository, IStudyAnalytics analytics, IStudyTelemetry telemetry, IStudyTimeTrainingDataSource trainingDataSource, IMLModelManager? mlModelManager)
        {
            _hocKy = hocKy;
            _studyLogRepository = studyLogRepository;
            _analytics = analytics;
            _telemetry = telemetry;
            _trainingDataSource = trainingDataSource;
            _mlModelManager = mlModelManager;
        }

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;
                _allLogs = await _studyLogRepository.GetForHocKyAsync(_hocKy);
                HasEnoughData = _allLogs.Count >= 50;
                SubjectOptions = new ObservableCollection<string>(new[] { "Tất cả" }
                    .Concat(_hocKy.DanhSachMonHoc.Select(m => m.TenMonHoc).Distinct().OrderBy(x => x)));
                ApplyFilters();
                _telemetry.Track("analytics_open", new Dictionary<string, string> { ["semester"] = _hocKy.Ten });
            }
            catch
            {
                HasError = true;
                HasData = false;
                EmptyStateMessage = "Không thể tải analytics. Hãy thử lại.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedRangeDaysChanged(int value) => ApplyFilters();
        partial void OnSelectedSubjectChanged(string value) => ApplyFilters();

        private void ApplyFilters()
        {
            if (_allLogs.Count == 0)
            {
                HasData = false;
                EmptyStateMessage = "Bạn chưa có log học tập để phân tích.";
                return;
            }

            var from = DateTime.Today.AddDays(-Math.Max(1, SelectedRangeDays) + 1);
            var taskById = _hocKy.DanhSachMonHoc
                .SelectMany(m => m.DanhSachTask.Select(t => new { t.MaTask, Mon = m.TenMonHoc }))
                .ToDictionary(x => x.MaTask, x => x.Mon);
            var filtered = _allLogs
                .Where(l => l.NgayHoc.Date >= from)
                .Where(l => SelectedSubject == "Tất cả" || (taskById.TryGetValue(l.MaTask, out var mon) && mon == SelectedSubject))
                .ToList();

            HasData = filtered.Count > 0;
            EmptyStateMessage = HasData
                ? string.Empty
                : "Không có dữ liệu cho bộ lọc hiện tại.";
            if (!HasData) return;

            var weekly = _analytics.ComputeWeeklyMinutes(filtered, DateTime.Today);
            WeeklyChartSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = weekly.MinutesPerDay.ToArray(),
                    Name   = "Phút học",
                    Fill   = new SolidColorPaint(SKColors.CornflowerBlue)
                }
            };
            WeeklyChartXAxes = new[] { new Axis { Labels = weekly.DayLabels.ToArray(), LabelsRotation = 15 } };

            var insights = _analytics.ComputeSubjectInsights(_hocKy, filtered);
            SubjectInsights = new ObservableCollection<SubjectInsight>(insights);
            SubjectChartSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = insights.Select(x => Math.Round(x.CompletionRate * 100, 1)).ToArray(),
                    Name   = "Tỉ lệ hoàn thành (%)",
                    Fill   = new SolidColorPaint(SKColors.MediumSeaGreen)
                }
            };
            SubjectChartXAxes = new[] { new Axis { Labels = insights.Select(x => x.SubjectName).ToArray(), LabelsRotation = 15 } };

            int totalTasks    = insights.Sum(x => x.TotalTaskCount);
            int completedTasks = insights.Sum(x => x.CompletedTaskCount);
            double completionRate = totalTasks == 0 ? 0.0 : (double)completedTasks / totalTasks;
            int    streakDays     = StreakManager.GetCurrentStreak().StreakCount;
            double timeEfficiency = filtered.Count == 0 ? 0.0
                : filtered.Count(l => l.DaHoanThanh) / (double)filtered.Count;

            var score = _analytics.ComputeProductivityScore(completionRate, streakDays, timeEfficiency);
            ProductivityValue = score.Value;
            ProductivityLabel = score.Label;

            BuildHeatmap(filtered);
            BuildNarrative(filtered, insights);
            _telemetry.Track("analytics_filter_changed", new Dictionary<string, string>
            {
                ["range_days"] = SelectedRangeDays.ToString(),
                ["subject"] = SelectedSubject
            });
        }

        private void BuildNarrative(List<StudyLog> filtered, List<SubjectInsight> insights)
        {
            var current7 = filtered.Where(l => l.NgayHoc.Date >= DateTime.Today.AddDays(-6)).Sum(l => l.SoPhutHoc);
            var prev7 = filtered.Where(l => l.NgayHoc.Date >= DateTime.Today.AddDays(-13) && l.NgayHoc.Date < DateTime.Today.AddDays(-6)).Sum(l => l.SoPhutHoc);
            var delta = current7 - prev7;
            var trend = delta >= 0 ? $"tăng {delta} phút" : $"giảm {Math.Abs(delta)} phút";
            WeeklyNarrative = $"Tuần này bạn học {current7} phút, so với tuần trước {trend}.";

            var weakest = insights
                .Where(i => i.TotalTaskCount > 0)
                .OrderBy(i => i.CompletionRate)
                .ThenByDescending(i => i.TotalTaskCount)
                .FirstOrDefault();
            RecommendedNextAction = weakest == null
                ? "Tiếp tục duy trì nhịp học hiện tại."
                : $"Môn cần ưu tiên: {weakest.SubjectName}. Gợi ý: thêm 45-60 phút trong 3 ngày tới.";
        }

        private void BuildHeatmap(List<StudyLog> logs)
        {
            var byDate = logs
                .GroupBy(l => l.NgayHoc.Date)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.SoPhutHoc));

            // Align to the Monday 51 full weeks before the current week's Monday
            var today = DateTime.Today;
            int daysToMonday = ((int)today.DayOfWeek + 6) % 7; // 0=Mon,...,6=Sun
            var thisMonday = today.AddDays(-daysToMonday);
            var startDate = thisMonday.AddDays(-51 * 7);

            // UniformGrid Rows=7 is row-major: row 0 = all Mondays, row 1 = all Tuesdays, etc.
            var cells = new ObservableCollection<HeatCell>();
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 52; col++)
                {
                    var date = startDate.AddDays(col * 7 + row);
                    int minutes = byDate.TryGetValue(date, out int m) ? m : 0;
                    int level = minutes == 0 ? 0
                              : minutes <= 30  ? 1
                              : minutes <= 60  ? 2
                              : minutes <= 120 ? 3
                              : 4;
                    cells.Add(new HeatCell(date, minutes, level));
                }
            }
            HeatmapCells = cells;
        }

        [RelayCommand]
        private async Task RetrainModel()
        {
            if (IsRetraining || !HasEnoughData) return;
            IsRetraining = true;
            try
            {
                var predictor = _mlModelManager ?? ServiceLocator.Get<IMLModelManager>();
                var real = await _trainingDataSource.BuildAsync();
                var data = real.Count >= StudyTimeTrainingDataSource.MinRows
                    ? real
                    : SeedDataGenerator.Generate();
                await predictor.RetrainAsync(data);
            }
            finally
            {
                IsRetraining = false;
            }
        }

        private sealed class NullStudyTimeTrainingDataSource : IStudyTimeTrainingDataSource
        {
            public Task<System.Collections.Generic.IReadOnlyList<SmartStudyPlanner.Services.ML.Schema.StudyTimeInput>> BuildAsync(System.Threading.CancellationToken ct = default)
                => Task.FromResult<System.Collections.Generic.IReadOnlyList<SmartStudyPlanner.Services.ML.Schema.StudyTimeInput>>(System.Array.Empty<SmartStudyPlanner.Services.ML.Schema.StudyTimeInput>());
        }
    }
}
