using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.Analytics.Models;
using System;
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

        public HocKy HocKy => _hocKy;

        [ObservableProperty] private ISeries[] weeklyChartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] weeklyChartXAxes = new[] { new Axis() };
        [ObservableProperty] private ISeries[] subjectChartSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] subjectChartXAxes = new[] { new Axis() };
        [ObservableProperty] private int productivityValue;
        [ObservableProperty] private string productivityLabel = "Chưa có dữ liệu";
        [ObservableProperty] private ObservableCollection<SubjectInsight> subjectInsights = new();

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
            var logs = await _repository.GetStudyLogsAsync(_hocKy);

            var weekly = _analytics.ComputeWeeklyMinutes(logs, DateTime.Today);
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

            var insights = _analytics.ComputeSubjectInsights(_hocKy, logs);
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
            double timeEfficiency = logs.Count == 0 ? 0.0
                : logs.Count(l => l.DaHoanThanh) / (double)logs.Count;

            var score = _analytics.ComputeProductivityScore(completionRate, streakDays, timeEfficiency);
            ProductivityValue = score.Value;
            ProductivityLabel = score.Label;
        }
    }
}
