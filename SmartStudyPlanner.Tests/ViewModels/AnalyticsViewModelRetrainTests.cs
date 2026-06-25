using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.Services.Analytics.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.Schema;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    public class AnalyticsViewModelRetrainTests
    {
        private static AnalyticsViewModel BuildVm(
            IStudyTimeTrainingDataSource trainingSource,
            SpyMLModelManager spy)
        {
            var hocKy = new HocKy("HK", DateTime.Today);
            var vm = new AnalyticsViewModel(
                hocKy,
                new NullStudyLogRepository(),
                new NullStudyAnalytics(),
                new NullStudyTelemetry(),
                trainingSource,
                spy);
            vm.HasEnoughData = true; // bypass LoadAsync gate for unit tests
            return vm;
        }

        private static List<StudyTimeInput> MakeRealRows(int count)
            => Enumerable.Range(0, count)
                .Select(_ => new StudyTimeInput { TaskType = "BaiTapVeNha", Difficulty = 2f, Credits = 3f, DaysLeft = 5f, Label = 45f })
                .ToList();

        [Fact]
        public async Task EnoughRealData_RetrainAsync_ReceivesRealRows()
        {
            var real = MakeRealRows(StudyTimeTrainingDataSource.MinRows);
            var spy = new SpyMLModelManager();
            var vm = BuildVm(new StubTrainingSource(real), spy);

            await vm.RetrainModelCommand.ExecuteAsync(null);

            Assert.Equal(StudyTimeTrainingDataSource.MinRows, spy.LastDataCount);
            Assert.Equal(real[0].TaskType, spy.LastData![0].TaskType);
        }

        [Fact]
        public async Task InsufficientRealData_RetrainAsync_ReceivesSeedRows()
        {
            var spy = new SpyMLModelManager();
            var vm = BuildVm(new StubTrainingSource(new List<StudyTimeInput>()), spy);

            await vm.RetrainModelCommand.ExecuteAsync(null);

            // Seed data has 180 rows (3 groups of 60)
            Assert.Equal(180, spy.LastDataCount);
        }

        [Fact]
        public async Task AfterRetrain_IsRetrainingReturnsFalse()
        {
            var spy = new SpyMLModelManager();
            var vm = BuildVm(new StubTrainingSource(new List<StudyTimeInput>()), spy);

            await vm.RetrainModelCommand.ExecuteAsync(null);

            Assert.False(vm.IsRetraining);
        }

        // ---- test doubles ----

        private sealed class StubTrainingSource : IStudyTimeTrainingDataSource
        {
            private readonly IReadOnlyList<StudyTimeInput> _rows;
            public StubTrainingSource(IReadOnlyList<StudyTimeInput> rows) => _rows = rows;
            public Task<IReadOnlyList<StudyTimeInput>> BuildAsync(CancellationToken ct = default)
                => Task.FromResult(_rows);
        }

        private sealed class SpyMLModelManager : IMLModelManager
        {
            public bool IsReady => true;
            public int? LastDataCount { get; private set; }
            public IReadOnlyList<StudyTimeInput>? LastData { get; private set; }

            public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task RetrainAsync(IReadOnlyList<StudyTimeInput> data, CancellationToken ct = default)
            {
                LastDataCount = data.Count;
                LastData = data;
                return Task.CompletedTask;
            }
            public Task<float> EvaluateR2Async(CancellationToken ct = default) => Task.FromResult(0.5f);
            public int PredictMinutes(StudyTimeInput input) => 60;
        }

        private sealed class NullStudyLogRepository : IStudyLogRepository
        {
            public Task AddAsync(StudyLog log, CancellationToken ct = default) => Task.CompletedTask;
            public Task<List<StudyLog>> GetByTaskAsync(Guid maTask, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetSinceAsync(DateTime since, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetForHocKyAsync(HocKy hocKy, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
        }

        private sealed class NullStudyAnalytics : IStudyAnalytics
        {
            public WeeklyReport ComputeWeeklyMinutes(IEnumerable<StudyLog> logs, DateTime referenceDate)
                => new() { DayLabels = new(), MinutesPerDay = new() };
            public List<SubjectInsight> ComputeSubjectInsights(HocKy hocKy, IEnumerable<StudyLog> logs) => new();
            public ProductivityScore ComputeProductivityScore(double completionRate, int streakDays, double timeEfficiency)
                => new() { Value = 0 };
        }

        private sealed class NullStudyTelemetry : IStudyTelemetry
        {
            public void Track(string eventName, IDictionary<string, string>? properties = null) { }
        }
    }
}
