using System;
using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Core.Parsing.Models;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Tests.Helpers;
using Xunit;

namespace SmartStudyPlanner.Tests.Parsing
{
    public class ParsingOrchestratorTests
    {
        private static readonly DateTime Today = new(2026, 5, 18, 9, 0, 0);

        [Fact]
        public void Parse_KhongCoClassifier_TraVeHeuristicGiongLegacy()
        {
            var clock = new FakeClock(Today);
            var sut = new ParsingOrchestrator(clock);

            var result = sut.Parse("Ôn thi giữa kỳ mai cực khó");

            Assert.Equal(LoaiCongViec.ThiGiuaKy, result.Loai);
            Assert.Equal(Today.AddDays(1), result.HanChot);
            Assert.Equal(ParseSource.Heuristic, result.Source);
            Assert.Null(result.Confidence);
        }

        [Fact]
        public void Parse_MatchVoiLegacySmartParser()
        {
            var legacy = SmartParser.Parse("Làm bài tập về nhà thứ 5 dễ");

            var clock = new FakeClock(DateTime.Now);
            var sut = new ParsingOrchestrator(clock);
            var fresh = sut.Parse("Làm bài tập về nhà thứ 5 dễ");

            Assert.Equal(legacy.Loai, fresh.Loai);
            Assert.Equal(legacy.DoKho, fresh.DoKho);
            Assert.Equal(legacy.TenTask, fresh.TenTask);
        }

        [Fact]
        public void Parse_CoClassifier_OverrideLoaiVaDoKho()
        {
            var clock = new FakeClock(Today);
            var classifier = new StubIntentClassifier(new IntentPrediction
            {
                Loai = LoaiCongViec.DoAnCuoiKy,
                DoKho = 5,
                Confidence = 0.82,
            });
            var sut = new ParsingOrchestrator(clock, classifier);

            var result = sut.Parse("Làm bài tập về nhà mai");

            Assert.Equal(LoaiCongViec.DoAnCuoiKy, result.Loai);
            Assert.Equal(5, result.DoKho);
            Assert.Equal(0.82, result.Confidence);
            Assert.Equal(ParseSource.MlAugmented, result.Source);
            // Time engine vẫn chạy độc lập
            Assert.Equal(Today.AddDays(1), result.HanChot);
        }

        [Fact]
        public void Parse_ClassifierTraVeNull_FallbackHeuristic()
        {
            var clock = new FakeClock(Today);
            var classifier = new StubIntentClassifier(null);
            var sut = new ParsingOrchestrator(clock, classifier);

            var result = sut.Parse("Kiểm tra 15p mai");

            Assert.Equal(LoaiCongViec.KiemTraThuongXuyen, result.Loai);
            Assert.Equal(ParseSource.Heuristic, result.Source);
        }

        [Fact]
        public void Parse_ClassifierChiCoLoai_GiuDoKhoHeuristic()
        {
            var clock = new FakeClock(Today);
            var classifier = new StubIntentClassifier(new IntentPrediction
            {
                Loai = LoaiCongViec.ThiCuoiKy,
                DoKho = null,
                Confidence = 0.7,
            });
            var sut = new ParsingOrchestrator(clock, classifier);

            var result = sut.Parse("Đề khó cuối kỳ mai");

            Assert.Equal(LoaiCongViec.ThiCuoiKy, result.Loai);
            // "khó" → DoKho heuristic = 5
            Assert.Equal(5, result.DoKho);
            Assert.Equal(ParseSource.MlAugmented, result.Source);
        }

        private sealed class StubIntentClassifier : IIntentClassifier
        {
            private readonly IntentPrediction? _prediction;
            public StubIntentClassifier(IntentPrediction? prediction) => _prediction = prediction;
            public IntentPrediction? Classify(string rawInput) => _prediction;
        }
    }
}
