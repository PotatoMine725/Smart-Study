using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface IDifficultyKeywordParser
    {
        int Parse(string lowerInput, int defaultValue);
    }

    public class DefaultDifficultyKeywordParser : IDifficultyKeywordParser
    {
        private readonly IReadOnlyList<IKeywordRule<int>> _rules = new IKeywordRule<int>[]
        {
            new ContainsAnyRule<int>(5, "khó", "kho", "căng", "chết"),
            new ContainsAnyRule<int>(1, "dễ", "de", "chill", "nhàn", "ez"),
        };

        public int Parse(string lowerInput, int defaultValue)
        {
            foreach (var rule in _rules)
            {
                if (rule.TryMatch(lowerInput, out var doKho))
                    return doKho;
            }
            return defaultValue;
        }
    }
}
