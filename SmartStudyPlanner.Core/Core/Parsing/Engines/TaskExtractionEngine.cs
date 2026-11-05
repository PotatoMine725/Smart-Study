using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Parsing.Engines
{
    /// <summary>
    /// Compose TaskType + Difficulty keyword parsers vào một engine duy nhất.
    /// </summary>
    public sealed class TaskExtractionEngine : ITaskExtractionEngine
    {
        private readonly ITaskTypeKeywordParser _taskTypeParser;
        private readonly IDifficultyKeywordParser _difficultyParser;

        public TaskExtractionEngine()
            : this(new DefaultTaskTypeKeywordParser(), new DefaultDifficultyKeywordParser())
        {
        }

        public TaskExtractionEngine(ITaskTypeKeywordParser taskTypeParser, IDifficultyKeywordParser difficultyParser)
        {
            _taskTypeParser = taskTypeParser;
            _difficultyParser = difficultyParser;
        }

        public LoaiCongViec ExtractType(string lowerInput, LoaiCongViec defaultValue)
            => _taskTypeParser.Parse(lowerInput, defaultValue);

        public int ExtractDifficulty(string lowerInput, int defaultValue)
            => _difficultyParser.Parse(lowerInput, defaultValue);
    }
}
