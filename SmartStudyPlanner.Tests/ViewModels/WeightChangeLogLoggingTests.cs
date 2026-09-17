using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner.Tests.ViewModels
{
    public class WeightChangeLogLoggingTests
    {
        private static WeightConfigSuggestion MakeSuggestion(double confidence = 0.87) => new()
        {
            Suggested = new WeightConfig { TimeWeight = 0.50, TaskTypeWeight = 0.25, CreditWeight = 0.15, DifficultyWeight = 0.10 },
            Confidence = confidence,
            Rationale = "test rationale",
        };

        private sealed class StubDecisionEngine : IDecisionEngine
        {
            public WeightConfig Config { get; } = new WeightConfig
            {
                TimeWeight = 0.40, TaskTypeWeight = 0.30, CreditWeight = 0.20, DifficultyWeight = 0.10
            };
            public WeightConfigSuggestion? SuggestionResult { get; set; }
            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(System.Threading.CancellationToken ct = default)
                => Task.FromResult(SuggestionResult);
            public double CalculatePriority(StudyTask t, MonHoc m) => 0;
            public int CalculateRawSuggestedMinutes(StudyTask t) => 0;
            public string SuggestStudyTime(StudyTask t) => string.Empty;
            public StudyTimePredictionResult PredictStudyMinutes(StudyTask t, MonHoc m) => new StudyTimePredictionResult(0, false, 0f);
        }

        private sealed class StubPolicy : IMlConfidencePolicy
        {
            public MlConfidenceDecision Decide(double c)
                => c >= 0.75 ? MlConfidenceDecision.AutoApply : MlConfidenceDecision.Reject;
        }

        private static async Task<(WeightOptimizerViewModel vm, FakeWeightChangeLogRepository logRepo)> BuildVmWithSuggestionLoaded(
            FakeStudyTaskRepository? taskRepo = null,
            FakeUserStatsRepository? statsRepo = null,
            FakeWeightChangeLogRepository? logRepo = null)
        {
            logRepo ??= new FakeWeightChangeLogRepository();
            statsRepo ??= new FakeUserStatsRepository();
            taskRepo ??= new FakeStudyTaskRepository();

            var engine = new StubDecisionEngine { SuggestionResult = MakeSuggestion() };
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), _ => { }, logRepo, statsRepo, taskRepo);
            await vm.LoadSuggestionCommand.ExecuteAsync(null);
            return (vm, logRepo);
        }

        [Fact]
        public async Task ApplySuggestion_LogsWeightChange_WithCorrectFields()
        {
            var (vm, logRepo) = await BuildVmWithSuggestionLoaded();

            vm.ApplySuggestionCommand.Execute(null);
            await Task.Delay(50);

            Assert.Single(logRepo.Added);
            var entry = logRepo.Added[0];
            Assert.Equal(0.40, entry.BeforeTime, precision: 3);
            Assert.Equal(0.30, entry.BeforeTaskType, precision: 3);
            Assert.Equal(0.50, entry.AfterTime, precision: 3);
            Assert.Equal(0.25, entry.AfterTaskType, precision: 3);
            Assert.Equal(0.87, entry.Confidence, precision: 3);
            Assert.Equal("test rationale", entry.Rationale);
            Assert.Null(entry.OutcomeMaturedUtc);
        }

        [Fact]
        public async Task ApplySuggestion_CohortContainsOnlyOpenTasks()
        {
            var openTask = new StudyTask("open task", DateTime.Today.AddDays(5), LoaiCongViec.BaiTapVeNha, 2);
            var doneTask = new StudyTask("done task", DateTime.Today.AddDays(3), LoaiCongViec.KiemTraThuongXuyen, 3);
            doneTask.TrangThai = StudyTaskStatus.HoanThanh;

            var taskRepo = new FakeStudyTaskRepository();
            taskRepo.Seed(new List<StudyTask> { openTask, doneTask });

            var (vm, logRepo) = await BuildVmWithSuggestionLoaded(taskRepo: taskRepo);

            vm.ApplySuggestionCommand.Execute(null);
            await Task.Delay(50);

            Assert.Single(logRepo.Added);
            var ids = JsonSerializer.Deserialize<List<Guid>>(logRepo.Added[0].CohortTaskIdsJson)!;
            Assert.Single(ids);
            Assert.Equal(openTask.MaTask, ids[0]);
        }

        [Fact]
        public async Task ApplySuggestion_SaveSucceeds_EvenWhenLogRepoThrows()
        {
            var logRepo = new FakeWeightChangeLogRepository { ShouldThrow = true };
            var (vm, _) = await BuildVmWithSuggestionLoaded(logRepo: logRepo);

            var ex = await Record.ExceptionAsync(() =>
            {
                vm.ApplySuggestionCommand.Execute(null);
                return Task.Delay(50);
            });

            Assert.Null(ex);
            Assert.False(string.IsNullOrEmpty(vm.ApplyStatus));
        }

        [Fact]
        public async Task ApplySuggestion_NullLogRepo_SaveSucceeds()
        {
            var engine = new StubDecisionEngine { SuggestionResult = MakeSuggestion() };
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), _ => { });
            await vm.LoadSuggestionCommand.ExecuteAsync(null);

            var ex = await Record.ExceptionAsync(() =>
            {
                vm.ApplySuggestionCommand.Execute(null);
                return Task.Delay(50);
            });

            Assert.Null(ex);
            Assert.False(string.IsNullOrEmpty(vm.ApplyStatus));
        }
    }
}
