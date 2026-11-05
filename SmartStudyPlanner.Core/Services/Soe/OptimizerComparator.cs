using System;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Comparator của G2-2 (<c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>) —
    /// gần như nguyên văn từ decision note, KHÔNG redesign, chỉ implement. Ba hàm:
    /// <see cref="CompareFeasibility"/> (D-H: đếm vi phạm trước, tổng phút trễ sau — số nguyên
    /// chính xác, không epsilon), <see cref="IsAdmissible"/> (D-H/D-J: một checkpoint kém khả thi
    /// hơn trạng thái vào-pass KHÔNG BAO GIỜ được chọn, dù điểm số cao đến đâu — "no score can
    /// purchase a violation"), <see cref="IsBetterThan"/> (so sánh lexicographic duy nhất: khả
    /// thi trước, chất lượng sau — epsilon tương đối theo G2-3).
    /// </summary>
    public static class OptimizerComparator
    {
        /// <summary>
        /// G2-3's epsilon: "1e-9 is a floating-point noise guard, not a policy dial" — tồn tại để
        /// hai trạng thái chỉ khác nhau vì thứ tự cộng dồn (floating-point) không bị đọc thành
        /// một cải thiện, thứ có thể khiến vòng lặp ngoài chạy mãi không tiến triển.
        /// </summary>
        public const double RelEps = 1e-9;

        /// <summary>Thứ tự D-H: số vi phạm trước, tổng phút trễ hạn sau.</summary>
        public static int CompareFeasibility(in Eval x, in Eval y)
            => x.ViolationCount != y.ViolationCount
                ? x.ViolationCount.CompareTo(y.ViolationCount)
                : x.OverdueMinutes.CompareTo(y.OverdueMinutes);

        /// <summary>
        /// D-H + D-J: một checkpoint kém khả thi hơn trạng thái vào-pass KHÔNG BAO GIỜ được chọn,
        /// dù điểm số cao đến đâu.
        /// </summary>
        public static bool IsAdmissible(in Eval c, in Eval passEntry)
            => CompareFeasibility(c, passEntry) <= 0;

        /// <summary>
        /// So sánh accept DUY NHẤT. Lexicographic: khả thi trước, rồi chất lượng — xem
        /// <see cref="RelEps"/> cho epsilon.
        /// </summary>
        public static bool IsBetterThan(in Eval c, in Eval incumbent)
        {
            int f = CompareFeasibility(c, incumbent);
            if (f != 0) return f < 0;
            return c.Score > incumbent.Score + RelEps * Math.Max(1.0, Math.Abs(incumbent.Score));
        }
    }
}
