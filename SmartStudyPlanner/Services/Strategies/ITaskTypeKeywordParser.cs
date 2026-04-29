using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface ITaskTypeKeywordParser
    {
        LoaiCongViec Parse(string lowerInput, LoaiCongViec defaultValue);
    }

    public class DefaultTaskTypeKeywordParser : ITaskTypeKeywordParser
    {
        // Thứ tự rule = ưu tiên. Rule đầu tiên match sẽ thắng.
        private readonly IReadOnlyList<IKeywordRule<LoaiCongViec>> _rules = new IKeywordRule<LoaiCongViec>[]
        {
            new ContainsAnyRule<LoaiCongViec>(LoaiCongViec.ThiGiuaKy,
                "giữa kỳ", "giua ky", "gk"),
            new ContainsAnyRule<LoaiCongViec>(LoaiCongViec.ThiCuoiKy,
                "cuối kỳ", "cuoi ky", "ck"),
            new ContainsAnyRule<LoaiCongViec>(LoaiCongViec.DoAnCuoiKy,
                "đồ án", "do an", "project", "bài tập lớn", "btl"),
            new ContainsAnyRule<LoaiCongViec>(LoaiCongViec.KiemTraThuongXuyen,
                "kiểm tra", "test", "15p", "1 tiết"),
        };

        public LoaiCongViec Parse(string lowerInput, LoaiCongViec defaultValue)
        {
            foreach (var rule in _rules)
            {
                if (rule.TryMatch(lowerInput, out var loai))
                    return loai;
            }
            return defaultValue;
        }
    }
}
