using System;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    public static class SmartParser
    {
        private static readonly IClock _clock = new SystemClock();
        private static readonly ITaskTypeKeywordParser _taskTypeParser = new DefaultTaskTypeKeywordParser();
        private static readonly IDifficultyKeywordParser _difficultyParser = new DefaultDifficultyKeywordParser();
        private static readonly IDeadlineKeywordParser _deadlineParser = new DefaultDeadlineKeywordParser(_clock);

        // Facade giữ nguyên chữ ký cũ để không breaking QuanLyTaskViewModel.cs:175
        public static (string TenTask, DateTime HanChot, LoaiCongViec Loai, int DoKho) Parse(string input)
        {
            string lowerInput = input.ToLower();

            string tenTask = input;
            LoaiCongViec loai = _taskTypeParser.Parse(lowerInput, LoaiCongViec.BaiTapVeNha);
            int doKho = _difficultyParser.Parse(lowerInput, defaultValue: 3);
            DateTime hanChot = _deadlineParser.Parse(lowerInput, _clock.Now.AddDays(1));

            return (tenTask, hanChot, loai, doKho);
        }
    }
}
