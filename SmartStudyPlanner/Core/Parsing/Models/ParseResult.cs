using System;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Parsing.Models
{
    /// <summary>
    /// Kết quả parse chuẩn của Core/Parsing. Có sẵn helper để convert sang tuple legacy
    /// (TenTask, HanChot, Loai, DoKho) đang được Services.SmartParser.Parse trả về.
    /// </summary>
    public sealed class ParseResult
    {
        public string TenTask { get; init; } = string.Empty;
        public DateTime HanChot { get; init; }
        public LoaiCongViec Loai { get; init; }
        public int DoKho { get; init; }

        /// <summary>Confidence từ ML (null = pure heuristic).</summary>
        public double? Confidence { get; init; }

        /// <summary>Nguồn quyết định cuối cùng cho mỗi field — phục vụ debug/UX preview.</summary>
        public ParseSource Source { get; init; } = ParseSource.Heuristic;

        public (string TenTask, DateTime HanChot, LoaiCongViec Loai, int DoKho) ToLegacyTuple()
            => (TenTask, HanChot, Loai, DoKho);
    }

    public enum ParseSource
    {
        Heuristic = 0,
        MlAugmented = 1,
        MlOverridden = 2,
    }
}
