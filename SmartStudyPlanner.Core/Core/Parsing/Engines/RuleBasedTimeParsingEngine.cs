using System;
using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Parsing.Engines
{
    /// <summary>
    /// Wrap <see cref="DefaultDeadlineKeywordParser"/> để Core/Parsing không lộ chi tiết strategy.
    /// </summary>
    public sealed class RuleBasedTimeParsingEngine : ITimeParsingEngine
    {
        private readonly IDeadlineKeywordParser _deadlineParser;

        public RuleBasedTimeParsingEngine(IClock clock)
            : this(new DefaultDeadlineKeywordParser(clock))
        {
        }

        public RuleBasedTimeParsingEngine(IDeadlineKeywordParser deadlineParser)
        {
            _deadlineParser = deadlineParser;
        }

        public DateTime ParseDeadline(string lowerInput, DateTime defaultValue)
            => _deadlineParser.Parse(lowerInput, defaultValue);
    }
}
