using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Core.Parsing.Engines;
using SmartStudyPlanner.Core.Parsing.Models;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Parsing.Orchestrators
{
    /// <summary>
    /// Compose toàn bộ luồng parsing: time + task extraction (+ optional intent classifier).
    /// Khi không có <see cref="IIntentClassifier"/> → kết quả tương đương heuristic legacy.
    /// </summary>
    public sealed class ParsingOrchestrator : IParsingOrchestrator
    {
        private readonly ITimeParsingEngine _timeEngine;
        private readonly ITaskExtractionEngine _taskEngine;
        private readonly IIntentClassifier? _intentClassifier;
        private readonly IClock _clock;

        public ParsingOrchestrator(IClock clock, IIntentClassifier? intentClassifier = null)
            : this(new RuleBasedTimeParsingEngine(clock), new TaskExtractionEngine(), clock, intentClassifier)
        {
        }

        public ParsingOrchestrator(
            ITimeParsingEngine timeEngine,
            ITaskExtractionEngine taskEngine,
            IClock clock,
            IIntentClassifier? intentClassifier = null)
        {
            _timeEngine = timeEngine;
            _taskEngine = taskEngine;
            _clock = clock;
            _intentClassifier = intentClassifier;
        }

        public ParseResult Parse(string input)
        {
            string lowerInput = input.ToLower();

            LoaiCongViec loaiHeuristic = _taskEngine.ExtractType(lowerInput, LoaiCongViec.BaiTapVeNha);
            int doKhoHeuristic = _taskEngine.ExtractDifficulty(lowerInput, defaultValue: 3);
            var hanChot = _timeEngine.ParseDeadline(lowerInput, _clock.Now.AddDays(1));

            var prediction = _intentClassifier?.Classify(input);

            LoaiCongViec loai = prediction?.Loai ?? loaiHeuristic;
            int doKho = prediction?.DoKho ?? doKhoHeuristic;
            ParseSource source = prediction is null
                ? ParseSource.Heuristic
                : (prediction.Loai.HasValue || prediction.DoKho.HasValue
                    ? ParseSource.MlAugmented
                    : ParseSource.Heuristic);

            return new ParseResult
            {
                TenTask = input,
                HanChot = hanChot,
                Loai = loai,
                DoKho = doKho,
                Confidence = prediction?.Confidence,
                Source = source,
            };
        }
    }
}
