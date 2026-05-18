using System;
using SmartStudyPlanner.Core.Parsing.Contracts;
using SmartStudyPlanner.Core.Parsing.Orchestrators;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Backward-compatible facade. Delegate vào <see cref="IParsingOrchestrator"/>.
    /// New consumers nên inject <c>IParsingOrchestrator</c> trực tiếp; static path còn lại
    /// chỉ phục vụ <c>QuanLyTaskViewModel</c> cho tới khi M8-A migrate.
    /// </summary>
    public static class SmartParser
    {
        private static readonly IParsingOrchestrator _default =
            new ParsingOrchestrator(new SystemClock());

        public static (string TenTask, DateTime HanChot, LoaiCongViec Loai, int DoKho) Parse(string input)
            => _default.Parse(input).ToLegacyTuple();
    }
}
