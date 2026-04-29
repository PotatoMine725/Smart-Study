using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.Analytics.Models;
using SmartStudyPlanner.Services.ML;
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
        private readonly IStudyRepository _repository;
        private readonly IStudyAnalytics _analytics;
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

        public AnalyticsViewModel(HocKy hocKy)
            : this(hocKy, ServiceLocator.Get<IStudyRepository>(), ServiceLocator.Get<IStudyAnalytics>()) { }

        public AnalyticsViewModel(HocKy hocKy, IStudyRepository repository, IStudyAnalytics analytics)
        {
            _hocKy = hocKy;
            _repository = repository;
            _analytics = analytics;
        }

        public async Task LoadAsync()
        {
            _allLogs = await _repository.GetStudyLogsAsync(_hocKy);
            HasEnoughData = _allLogs.Count >= 50;

            var weekly = _analytics.ComputeWeeklyMinutes(_allLogs, DateTime.Today);
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

            var insights = _analytics.ComputeSubjectInsights(_hocKy, _allLogs);
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
            double timeEfficiency = _allLogs.Count == 0 ? 0.0
                : _allLogs.Count(l => l.DaHoanThanh) / (double)_allLogs.Count;

            var score = _analytics.ComputeProductivityScore(completionRate, streakDays, timeEfficiency);
            ProductivityValue = score.Value;
            ProductivityLabel = score.Label;
        }

        [RelayCommand]
        private async Task RetrainModel()
        {
            if (IsRetraining || !HasEnoughData) return;
            IsRetraining = true;
            try
            {
                var predictor = ServiceLocator.Get<IMLModelManager>();
                var data = SeedDataGenerator.Generate();
                await predictor.RetrainAsync(data);
            }
            finally
            {
                IsRetraining = false;
            }
        }
    }
}
