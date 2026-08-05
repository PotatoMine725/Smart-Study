using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Implementation duy nhất của <see cref="IObjectiveEvaluator"/> cho T3.2. Tổ hợp tuyến tính
    /// năm số hạng độc lập theo đúng công thức D-G. Không đọc trường hạn chót của task nguồn trên
    /// <see cref="ScheduledItem"/> ở bất kỳ đâu trong file này — mọi số hạng chỉ dùng
    /// <see cref="ScheduledItem.Date"/>,
    /// <see cref="ScheduledItem.SoPhut"/>, <see cref="ScheduledItem.TenMon"/>,
    /// <see cref="ScheduledItem.MaTask"/>. Lý do vận hành từng số hạng: xem
    /// <c>docs/plans/2026-08-05-t32-objective-evaluator-seam-decisions.md</c>.
    /// </summary>
    public sealed class ObjectiveEvaluator : IObjectiveEvaluator
    {
        // SessionQuality: "vùng lý tưởng" hình thang cho độ dài một chunk, tính bằng phút.
        // Lý do chọn các mốc này: xem seam-decision doc, mục SessionQuality.
        private const double SessionRampUpEnd = 25.0;
        private const double SessionIdealEnd = 90.0;
        private const double SessionRampDownEnd = 180.0;

        public ObjectiveScore Evaluate(IReadOnlyList<ScheduledItem> schedule, SoeWeights weights)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            double loadBalance = ComputeLoadBalance(schedule);
            double contextContinuity = ComputeContextContinuity(schedule);
            double sessionQuality = ComputeSessionQuality(schedule);
            double fatiguePenalty = ComputeFatiguePenalty(schedule);
            double fragmentationPenalty = ComputeFragmentationPenalty(schedule);

            double total =
                weights.LoadBalanceWeight * loadBalance +
                weights.ContextContinuityWeight * contextContinuity +
                weights.SessionQualityWeight * sessionQuality +
                weights.FatiguePenaltyWeight * fatiguePenalty +
                weights.FragmentationPenaltyWeight * fragmentationPenalty;

            return new ObjectiveScore(
                LoadBalance: loadBalance,
                ContextContinuity: contextContinuity,
                SessionQuality: sessionQuality,
                FatiguePenalty: fatiguePenalty,
                FragmentationPenalty: fragmentationPenalty,
                Total: total);
        }

        /// <summary>
        /// [0,1], 1 = tải đều nhất. Group theo Date -> tổng SoPhut/ngày (chỉ ngày có >=1 chunk).
        /// Dùng hệ số biến thiên (CV = stddev/mean, population) thay vì stddev thô để không phụ
        /// thuộc đơn vị/độ lớn tổng số phút; nghịch đảo 1/(1+CV) cho khoảng (0,1], 1.0 khi CV=0
        /// (mọi ngày dùng bằng nhau), tiến về 0 khi CV tăng. &lt;=1 ngày dùng -> vacuously balanced (1.0).
        /// </summary>
        private static double ComputeLoadBalance(IReadOnlyList<ScheduledItem> schedule)
        {
            var perDay = schedule
                .GroupBy(i => i.Date)
                .Select(g => (double)g.Sum(i => i.SoPhut))
                .ToList();

            if (perDay.Count <= 1) return 1.0;

            double mean = perDay.Average();
            if (mean <= 0) return 1.0;

            double variance = perDay.Average(m => (m - mean) * (m - mean));
            double stddev = Math.Sqrt(variance);
            double cv = stddev / mean;

            return 1.0 / (1.0 + cv);
        }

        /// <summary>
        /// [0,1], 1 = mỗi ngày dùng chỉ chạm đúng một môn. ScheduledItem không mang thứ tự chunk
        /// trong-ngày (Date là khoá join hợp lệ duy nhất giữa Items/Days — xem doc của
        /// ScheduledItem/IObjectiveEvaluator), nên KHÔNG đo "subject switch giữa hai chunk liền kề"
        /// kiểu Card A (cần thứ tự ScheduleDay.Tasks) mà đo độ tập trung môn học trong từng ngày:
        /// 1 / (số môn phân biệt trong ngày đó), trung bình trên các ngày dùng. Hạn chế đã biết
        /// (không bắt continuity liên-ngày) — xem seam-decision doc.
        /// </summary>
        private static double ComputeContextContinuity(IReadOnlyList<ScheduledItem> schedule)
        {
            var byDay = schedule.GroupBy(i => i.Date).ToList();
            if (byDay.Count == 0) return 1.0;

            double sum = byDay.Sum(g => 1.0 / g.Select(i => i.TenMon).Distinct().Count());
            return sum / byDay.Count;
        }

        /// <summary>
        /// [0,1], 1 = mọi chunk nằm trong "vùng lý tưởng" [25, 90] phút. Hình thang: ramp-up
        /// tuyến tính 0->1 trên (0, 25], phẳng 1.0 trên [25, 90], ramp-down tuyến tính 1->0 trên
        /// [90, 180), 0 tại &gt;=180. Trung bình trên mọi chunk (KHÔNG group theo ngày — đây là tín
        /// hiệu mức-chunk, khác FatiguePenalty là tín hiệu mức-ngày, xem seam-decision doc để biết
        /// vì sao hai số hạng này không trùng lặp nhau). Lịch rỗng -> vacuously 1.0.
        /// </summary>
        private static double ComputeSessionQuality(IReadOnlyList<ScheduledItem> schedule)
        {
            if (schedule.Count == 0) return 1.0;

            return schedule.Average(i => SessionMembership(i.SoPhut));
        }

        private static double SessionMembership(int soPhut)
        {
            double x = soPhut;
            if (x <= 0) return 0.0;
            if (x <= SessionRampUpEnd) return x / SessionRampUpEnd;
            if (x <= SessionIdealEnd) return 1.0;
            if (x < SessionRampDownEnd) return (SessionRampDownEnd - x) / (SessionRampDownEnd - SessionIdealEnd);
            return 0.0;
        }

        /// <summary>
        /// [-1,0], 0 = tốt nhất (không có chuỗi ngày tải nặng liên tiếp thật sự liền kề lịch).
        /// Tự-tương-đối (KHÔNG cần capacity bên ngoài — xem seam-decision doc lý do): "nặng" =
        /// tổng SoPhut/ngày &gt; mức trung bình trên các ngày dùng của CHÍNH lịch này. Chỉ đếm các
        /// cặp ngày liên tiếp THEO LỊCH THẬT (Date[i+1] == Date[i].AddDays(1)) trong số các ngày
        /// dùng, đã sort theo Date -- một khoảng trống (ngày nghỉ) cắt đứt chuỗi, đúng ý nghĩa
        /// "không nghỉ giữa các ngày nặng". Không có cặp liền-kề-thật nào quan sát được ->
        /// vacuously 0 (không đủ dữ liệu để phạt một chuỗi không tồn tại).
        /// </summary>
        private static double ComputeFatiguePenalty(IReadOnlyList<ScheduledItem> schedule)
        {
            var perDay = schedule
                .GroupBy(i => i.Date)
                .Select(g => (Date: g.Key, Minutes: g.Sum(i => i.SoPhut)))
                .OrderBy(d => d.Date)
                .ToList();

            if (perDay.Count == 0) return 0.0;

            double mean = perDay.Average(d => d.Minutes);
            var heavy = perDay.Select(d => d.Minutes > mean).ToList();

            int consecutivePairs = 0;
            int heavyPairs = 0;
            for (int i = 1; i < perDay.Count; i++)
            {
                if (perDay[i].Date != perDay[i - 1].Date.AddDays(1)) continue;

                consecutivePairs++;
                if (heavy[i] && heavy[i - 1]) heavyPairs++;
            }

            if (consecutivePairs == 0) return 0.0;

            return -((double)heavyPairs / consecutivePairs);
        }

        /// <summary>
        /// [-1,0], 0 = không task nào bị cắt thành &gt;1 chunk. Group theo
        /// <see cref="ScheduledItem.MaTask"/> (khoá identity thật, không phải chuỗi tên) — mỗi task
        /// đóng góp (số chunk - 1) "chunk thừa"; tổng chunk thừa / tổng số chunk trong toàn lịch,
        /// đổi dấu. Bám sát trực tiếp cặp raw-metric FragmentedTaskCount/TotalFragmentChunks của
        /// Card A (SoeBaselineMetrics.cs) — đúng gợi ý trong nhiệm vụ. Lịch rỗng -> vacuously 0.
        /// </summary>
        private static double ComputeFragmentationPenalty(IReadOnlyList<ScheduledItem> schedule)
        {
            if (schedule.Count == 0) return 0.0;

            int extraChunks = schedule
                .GroupBy(i => i.MaTask)
                .Sum(g => g.Count() - 1);

            return -((double)extraChunks / schedule.Count);
        }
    }
}
