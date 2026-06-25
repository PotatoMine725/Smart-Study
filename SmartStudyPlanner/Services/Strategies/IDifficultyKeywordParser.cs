using System.Collections.Generic;
using SmartStudyPlanner.Models;

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

        /// <summary>
        /// Prior difficulty by task type, derived from observed label distribution.
        /// Replaces hard-coded default-3 when no keyword is matched.
        /// </summary>
        public static int PriorForTaskType(LoaiCongViec taskType) => taskType switch
        {
            LoaiCongViec.DoAnCuoiKy         => 4,
            LoaiCongViec.ThiCuoiKy          => 4,
            LoaiCongViec.ThiGiuaKy          => 3,
            LoaiCongViec.KiemTraThuongXuyen => 3,
            LoaiCongViec.BaiTapVeNha        => 2,
            _                               => 3,
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
