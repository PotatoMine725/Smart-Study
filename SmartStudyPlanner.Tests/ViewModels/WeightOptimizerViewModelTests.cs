using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.ViewModels;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner.Tests.ViewModels
{
    public class WeightOptimizerViewModelTests
    {
        // ── inline stubs ──────────────────────────────────────────────────

        private sealed class StubDecisionEngine : IDecisionEngine
        {
            public WeightConfig Config { get; } = new();
            public WeightConfigSuggestion? SuggestionResult { get; set; }

            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult(SuggestionResult);

            public double CalculatePriority(StudyTask t, MonHoc m) => 0;
            public int CalculateRawSuggestedMinutes(StudyTask t) => 0;
            public string SuggestStudyTime(StudyTask t) => string.Empty;
            public StudyTimePredictionResult PredictStudyMinutes(StudyTask t, MonHoc m) => new StudyTimePredictionResult(0, false, 0f);
        }

        private sealed class StubPolicy : IMlConfidencePolicy
        {
            private readonly double _autoApply;
            private readonly double _review;

            public StubPolicy(double autoApply = 0.75, double review = 0.60)
            {
                _autoApply = autoApply;
                _review = review;
            }

            public MlConfidenceDecision Decide(double confidence)
            {
                if (confidence >= _autoApply) return MlConfidenceDecision.AutoApply;
                if (confidence >= _review) return MlConfidenceDecision.Review;
                return MlConfidenceDecision.Reject;
            }
        }

        private static WeightConfigSuggestion MakeSuggestion(double confidence) => new()
        {
            Suggested = new WeightConfig { TimeWeight = 0.50, TaskTypeWeight = 0.25, CreditWeight = 0.15, DifficultyWeight = 0.10 },
            Confidence = confidence,
            Rationale = "Test rationale"
        };

        // ── tests ────────────────────────────────────────────────────────

        [Fact]
        public async Task LoadSuggestion_WhenDecisionReject_HasSuggestionFalse()
        {
            var engine = new StubDecisionEngine { SuggestionResult = MakeSuggestion(0.40) };
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), _ => { });

            await vm.LoadSuggestionCommand.ExecuteAsync(null);

            Assert.False(vm.HasSuggestion);
            Assert.Equal(MlConfidenceDecision.Reject, vm.Decision);
            Assert.False(vm.IsHighConfidence);
            Assert.False(string.IsNullOrWhiteSpace(vm.StatusMessage));
        }

        [Fact]
        public async Task LoadSuggestion_WhenDecisionReview_HasSuggestionTrue_IsHighConfidenceFalse()
        {
            var engine = new StubDecisionEngine { SuggestionResult = MakeSuggestion(0.68) };
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), _ => { });

            await vm.LoadSuggestionCommand.ExecuteAsync(null);

            Assert.True(vm.HasSuggestion);
            Assert.Equal(MlConfidenceDecision.Review, vm.Decision);
            Assert.False(vm.IsHighConfidence);
        }

        [Fact]
        public async Task LoadSuggestion_WhenDecisionAutoApply_HasSuggestionTrue_IsHighConfidenceTrue()
        {
            var engine = new StubDecisionEngine { SuggestionResult = MakeSuggestion(0.87) };
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), _ => { });

            await vm.LoadSuggestionCommand.ExecuteAsync(null);

            Assert.True(vm.HasSuggestion);
            Assert.Equal(MlConfidenceDecision.AutoApply, vm.Decision);
            Assert.True(vm.IsHighConfidence);
        }

        [Fact]
        public async Task ApplySuggestion_MutatesDecisionEngineConfig()
        {
            var engine = new StubDecisionEngine { SuggestionResult = MakeSuggestion(0.87) };
            WeightConfig? savedConfig = null;
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), cfg => savedConfig = cfg);
            await vm.LoadSuggestionCommand.ExecuteAsync(null);

            vm.ApplySuggestionCommand.Execute(null);

            Assert.Equal(0.50, engine.Config.TimeWeight, precision: 3);
            Assert.Equal(0.25, engine.Config.TaskTypeWeight, precision: 3);
            Assert.NotNull(savedConfig);
            Assert.False(string.IsNullOrEmpty(vm.ApplyStatus));
        }

        [Fact]
        public async Task LoadSuggestion_WhenNullResult_HasSuggestionFalse()
        {
            var engine = new StubDecisionEngine { SuggestionResult = null };
            var vm = new WeightOptimizerViewModel(engine, new StubPolicy(), _ => { });

            await vm.LoadSuggestionCommand.ExecuteAsync(null);

            Assert.False(vm.HasSuggestion);
            Assert.Equal(MlConfidenceDecision.Reject, vm.Decision);
        }
    }
}
