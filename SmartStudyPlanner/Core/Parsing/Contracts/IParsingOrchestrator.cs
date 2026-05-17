using SmartStudyPlanner.Core.Parsing.Models;

namespace SmartStudyPlanner.Core.Parsing.Contracts
{
    /// <summary>
    /// Compose-port cho luồng parsing: time + task extraction (+ optional intent classifier).
    /// Slice 3 sẽ implement. Bảo toàn khả năng trả về tuple legacy qua <see cref="ParseResult.ToLegacyTuple"/>.
    /// </summary>
    public interface IParsingOrchestrator
    {
        ParseResult Parse(string input);
    }
}
