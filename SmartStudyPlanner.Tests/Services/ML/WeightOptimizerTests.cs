using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Scheduling.Orchestrators;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.ML.WeightOptimizer;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.ML
{
    /// <summary>Slice 7 (M8-B) — rule-based WeightOptimizer: rule engine, Normalize, service + orchestrator seam.</summary>
    public class WeightOptimizerTests
    {
        private static UserStatsSnapshot Snapshot(
            double missRate = 0.0, double avgDelay = 0.0, int streak = 0,
            int taskCount = 20, int minutes30 = 600)
            => new()
            {
                ReferenceUtc = new DateTime(2026, 6, 6),
                TotalTaskCount = taskCount,
                CompletedTaskCount = taskCount,
                OverdueTaskCount = (int)(taskCount * missRate),
                MissRate = missRate,
                AverageDelayDays = avgDelay,
                FocusStreakDays = streak,
                TotalStudyMinutesLast30Days = minutes30,
            };

        // ---- WeightRuleEngine: rule behaviour ----

        [Fact]
        public void HighMissRate_IncreasesTimeWeight_AndStaysValid()
        {
            var current = new WeightConfig(); // Time 0.40
            var result = WeightRuleEngine.Compute(current, Snapshot(missRate: 0.8, avgDelay: 5));

            Assert.True(result.Suggested.TimeWeight > current.TimeWeight,
                $"expected Time > {current.TimeWeight}, got {result.Suggested.TimeWeight}");
            Assert.True(result.Suggested.IsValid(), "suggested weights must sum to 1.0");
        }

        [Fact]
        public void StableUser_KeepsWeightsNearCurrent()
        {
            var current = new WeightConfig();
            var result = WeightRuleEngine.Compute(current, Snapshot(missRate: 0.0, avgDelay: 0, streak: 14));

            Assert.Equal(current.TimeWeight, result.Suggested.TimeWeight, precision: 3);
            Assert.Contains("ổn định", result.Rationale);
        }

        [Fact]
        public void FocusStreak_DampensTheShift()
        {
            var current = new WeightConfig();
            var noStreak = WeightRuleEngine.Compute(current, Snapshot(missRate: 0.8, avgDelay: 5, streak: 0));
            var longStreak = WeightRuleEngine.Compute(current, Snapshot(missRate: 0.8, avgDelay: 5, streak: 14));

            Assert.True(longStreak.Suggested.TimeWeight < noStreak.Suggested.TimeWeight,
                "a long focus streak should reduce the urgency nudge");
        }

        [Fact]
        public void Compute_DoesNotMutateInput()
        {
            var current = new WeightConfig();
            double before = current.TimeWeight;

            _ = WeightRuleEngine.Compute(current, Snapshot(missRate: 0.9, avgDelay: 6));

            Assert.Equal(before, current.TimeWeight);
        }

        // ---- Confidence = data sufficiency ----

        [Fact]
        public void SparseData_ConfidenceBelowReviewThreshold()
        {
            var result = WeightRuleEngine.Compute(new WeightConfig(), Snapshot(taskCount: 3, minutes30: 0));
            Assert.True(result.Confidence < DefaultMlConfidencePolicy.ReviewThreshold,
                $"sparse data confidence {result.Confidence} should be < 0.60");
        }

        [Fact]
        public void RichData_ConfidenceAtOrAboveAutoApply()
        {
            var result = WeightRuleEngine.Compute(new WeightConfig(), Snapshot(taskCount: 20, minutes30: 600));
            Assert.True(result.Confidence >= DefaultMlConfidencePolicy.AutoApplyThreshold,
                $"rich data confidence {result.Confidence} should be >= 0.75");
        }

        // ---- WeightConfig.Normalize ----

        [Fact]
        public void Normalize_ScalesToSumOne()
        {
            var cfg = new WeightConfig { TimeWeight = 0.8, TaskTypeWeight = 0.8, CreditWeight = 0.8, DifficultyWeight = 0.8 };
            cfg.Normalize();

            Assert.True(cfg.IsValid());
            Assert.Equal(0.25, cfg.TimeWeight, precision: 3);
        }

        [Fact]
        public void Normalize_LiftsZeroWeightOffFloor()
        {
            var cfg = new WeightConfig { TimeWeight = 1.0, TaskTypeWeight = 0.0, CreditWeight = 0.0, DifficultyWeight = 0.0 };
            cfg.Normalize();

            Assert.True(cfg.IsValid());
            Assert.True(cfg.DifficultyWeight > 0.0, "a zeroed weight must be lifted off the floor");
        }

        // ---- WeightOptimizerService (async wrapper) ----

        [Fact]
        public async Task Service_FetchesSnapshot_AndReturnsSuggestion()
        {
            var sut = new WeightOptimizerService(new FakeStatsRepo(Snapshot(missRate: 0.5)), new FakeClock(new DateTime(2026, 6, 6)));

            var result = await sut.SuggestAsync(new WeightConfig());

            Assert.NotNull(result);
            Assert.True(result!.Suggested.IsValid());
        }

        [Fact]
        public async Task Service_ReturnsNull_WhenCurrentIsNull()
        {
            var sut = new WeightOptimizerService(new FakeStatsRepo(Snapshot()), new FakeClock(DateTime.Now));
            Assert.Null(await sut.SuggestAsync(null!));
        }

        // ---- Orchestrator seam (read-only) ----

        [Fact]
        public async Task Orchestrator_ReturnsNull_WhenOptimizerNotInjected()
        {
            var orchestrator = BuildOrchestrator(weightOptimizer: null);
            Assert.Null(await orchestrator.SuggestWeightConfigAsync());
        }

        [Fact]
        public async Task Orchestrator_Delegates_WithoutMutatingConfig()
        {
            var sentinel = new WeightConfigSuggestion { Suggested = new WeightConfig(), Confidence = 0.9 };
            var fake = new FakeWeightOptimizer(sentinel);
            var orchestrator = BuildOrchestrator(fake);
            double before = orchestrator.Config.TimeWeight;

            var result = await orchestrator.SuggestWeightConfigAsync();

            Assert.Same(sentinel, result);
            Assert.Same(orchestrator.Config, fake.LastCurrent); // suggested off the live config
            Assert.Equal(before, orchestrator.Config.TimeWeight); // never mutated
        }

        // ---- DI seam (mirrors ServiceLocator registrations) ----

        [Fact]
        public async Task DI_ResolvesDecisionEngine_AndInjectsOptimizerThroughSeam()
        {
            // Proves MS DI selects the 5-arg ctor and injects the optional registered IWeightOptimizerService.
            // The null-guard in the orchestrator would otherwise silently mask a dead wiring (always null).
            var services = new ServiceCollection();
            services.AddSingleton<IClock>(new FakeClock(new DateTime(2026, 6, 6)));
            services.AddSingleton<ITaskTypeWeightProvider, DefaultTaskTypeWeightProvider>();
            services.AddSingleton<IStudyTimePredictor, NullPredictor>();
            services.AddSingleton<WeightConfig>();
            services.AddSingleton<IUserStatsRepository>(new FakeStatsRepo(Snapshot(missRate: 0.5)));
            services.AddSingleton<IWeightOptimizerService>(sp =>
                new WeightOptimizerService(sp.GetRequiredService<IUserStatsRepository>(), sp.GetRequiredService<IClock>()));
            services.AddSingleton<IDecisionEngine, DecisionEngineService>();

            using var provider = services.BuildServiceProvider();
            var engine = provider.GetRequiredService<IDecisionEngine>();

            var suggestion = await engine.SuggestWeightConfigAsync();

            Assert.NotNull(suggestion);
        }

        // ---- helpers ----

        private static SchedulingOrchestrator BuildOrchestrator(IWeightOptimizerService? weightOptimizer)
            => new(new DefaultTaskTypeWeightProvider(), new FakeClock(DateTime.Now),
                   new NullPredictor(), initialConfig: null, weightOptimizer: weightOptimizer);

        private sealed class FakeStatsRepo : IUserStatsRepository
        {
            private readonly UserStatsSnapshot _snapshot;
            public FakeStatsRepo(UserStatsSnapshot snapshot) => _snapshot = snapshot;
            public Task<UserStatsSnapshot> GetSnapshotAsync(DateTime referenceUtc, CancellationToken ct = default)
                => Task.FromResult(_snapshot);
        }

        private sealed class FakeWeightOptimizer : IWeightOptimizerService
        {
            private readonly WeightConfigSuggestion _result;
            public WeightConfig? LastCurrent { get; private set; }
            public FakeWeightOptimizer(WeightConfigSuggestion result) => _result = result;
            public Task<WeightConfigSuggestion?> SuggestAsync(WeightConfig current, CancellationToken ct = default)
            {
                LastCurrent = current;
                return Task.FromResult<WeightConfigSuggestion?>(_result);
            }
        }

        private sealed class NullPredictor : IStudyTimePredictor
        {
            public bool IsReady => false;
            public Task<StudyTimePredictionResult> PredictAsync(StudyTask task, MonHoc monHoc, CancellationToken ct = default)
                => Task.FromResult(new StudyTimePredictionResult(0, false, 0f));
        }
    }
}
