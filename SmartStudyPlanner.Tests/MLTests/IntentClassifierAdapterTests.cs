using System;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    /// <summary>Slice 6 — confidence policy + adapter gating unit tests.</summary>
    public class IntentClassifierAdapterTests
    {
        // ---- DefaultMlConfidencePolicy boundaries ----

        [Theory]
        [InlineData(0.59, MlConfidenceDecision.Reject)]
        [InlineData(0.60, MlConfidenceDecision.Review)]
        [InlineData(0.74, MlConfidenceDecision.Review)]
        [InlineData(0.75, MlConfidenceDecision.AutoApply)]
        [InlineData(1.00, MlConfidenceDecision.AutoApply)]
        [InlineData(0.00, MlConfidenceDecision.Reject)]
        public void Policy_DecidesAtThresholds(double conf, MlConfidenceDecision expected)
        {
            Assert.Equal(expected, new DefaultMlConfidencePolicy().Decide(conf));
        }

        // ---- IntentClassifierAdapter gating ----

        [Fact]
        public void Adapter_PassesPrediction_WhenConfidenceAtOrAboveThreshold()
        {
            var pred = new IntentPrediction { Loai = LoaiCongViec.ThiGiuaKy, Confidence = 0.60 };
            var sut = new IntentClassifierAdapter(new StubService(pred), new DefaultMlConfidencePolicy());

            var result = sut.Classify("thi giữa kỳ mai");

            Assert.Same(pred, result);
        }

        [Fact]
        public void Adapter_DropsPrediction_WhenConfidenceBelowThreshold()
        {
            var pred = new IntentPrediction { Loai = LoaiCongViec.ThiCuoiKy, Confidence = 0.59 };
            var sut = new IntentClassifierAdapter(new StubService(pred), new DefaultMlConfidencePolicy());

            Assert.Null(sut.Classify("mơ hồ"));
        }

        [Fact]
        public void Adapter_ReturnsNull_WhenServiceReturnsNull()
        {
            var sut = new IntentClassifierAdapter(new StubService(null), new DefaultMlConfidencePolicy());

            Assert.Null(sut.Classify("bất kỳ"));
        }

        [Fact]
        public void Adapter_ReturnsNull_WhenServiceThrows()
        {
            var sut = new IntentClassifierAdapter(new ThrowingService(), new DefaultMlConfidencePolicy());

            // Offline-first: a classifier failure must never bubble up and break parsing.
            Assert.Null(sut.Classify("bất kỳ"));
        }

        private sealed class StubService : IIntentClassifierService
        {
            private readonly IntentPrediction? _pred;
            public StubService(IntentPrediction? pred) => _pred = pred;
            public bool IsModelLoaded => true;
            public IntentPrediction? Predict(string rawInput) => _pred;
        }

        private sealed class ThrowingService : IIntentClassifierService
        {
            public bool IsModelLoaded => true;
            public IntentPrediction? Predict(string rawInput) => throw new InvalidOperationException("boom");
        }
    }
}
