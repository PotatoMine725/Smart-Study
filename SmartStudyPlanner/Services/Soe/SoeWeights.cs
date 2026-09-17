using System;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Trọng số w1..w5 cho công thức mục tiêu (objective) của Study Optimization Engine, đóng
    /// băng bởi D-G (<c>docs/plans/2026-07-02-architecture-freeze-decisions.md</c>, dòng 28-55):
    ///
    /// <c>Score = w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty
    /// + w5·FragmentationPenalty</c>
    ///
    /// Đây là một vector trọng số HOÀN TOÀN TÁCH BIỆT khỏi <see cref="WeightConfig"/> (4 trọng số
    /// cho <c>PriorityScore</c> của Decision Engine — TimeWeight/TaskTypeWeight/CreditWeight/
    /// DifficultyWeight). Hai vector phục vụ hai giai đoạn khác nhau: <c>WeightConfig</c> định
    /// hướng "task nào ưu tiên trước" (Decision Engine); <see cref="SoeWeights"/> định hướng
    /// "trong các lịch KHẢ THI, lịch nào chất lượng hơn" (SOE, T3.2, chỉ chạy sau khi
    /// <c>IConstraintValidator</c>/T3.1 đã lọc feasibility — xem D-J). Không có lý do kiến trúc
    /// nào để hợp nhất hai vector này, và làm vậy sẽ xoá đúng ranh giới mà D-G/D-J vừa vẽ ra.
    ///
    /// KHÔNG ràng buộc tổng = 1.0 (khác <see cref="WeightConfig.IsValid"/>/<see cref="WeightConfig.Normalize"/>)
    /// — quyết định có chủ đích, xem lý do trong seam-decision doc
    /// (<c>docs/plans/2026-08-05-t32-objective-evaluator-seam-decisions.md</c>): các số hạng ở
    /// đây không đồng nhất (ba số hạng "reward" + hai số hạng "penalty" đã tự mang dấu âm — xem
    /// <see cref="ObjectiveScore"/>), tổ hợp tuyến tính chỉ phục vụ XẾP HẠNG tương đối giữa các
    /// candidate feasible (D-J), không phải một xác suất/tỷ lệ trộn cần chuẩn hoá về [0,1]. Việc
    /// chọn giá trị w1..w5 cụ thể, cơ chế governance, và có nên ràng buộc gì thêm là việc của một
    /// gate tương lai (T3.5/GATE G3) — kiểu này chỉ định hình shape của vector, không tune nó.
    ///
    /// Chỉ ràng buộc đúng một điều tại đây: mọi trọng số phải không âm. Trọng số âm sẽ lật ngược
    /// ngầm chiều ngữ nghĩa của một số hạng (vd: w1 âm sẽ "thưởng" cho việc mất cân bằng tải) —
    /// đó là một cái bẫy (footgun), không phải một đòn bẩy tuning hợp lệ; nếu governance tương lai
    /// muốn trọng số có dấu, đó là quyết định của họ để nới lỏng ràng buộc này, không phải mặc
    /// định hôm nay.
    /// </summary>
    /// <param name="LoadBalanceWeight">w1 — trọng số cho <see cref="ObjectiveScore.LoadBalance"/>.</param>
    /// <param name="ContextContinuityWeight">w2 — trọng số cho <see cref="ObjectiveScore.ContextContinuity"/>.</param>
    /// <param name="SessionQualityWeight">w3 — trọng số cho <see cref="ObjectiveScore.SessionQuality"/>.</param>
    /// <param name="FatiguePenaltyWeight">w4 — trọng số cho <see cref="ObjectiveScore.FatiguePenalty"/>.</param>
    /// <param name="FragmentationPenaltyWeight">w5 — trọng số cho <see cref="ObjectiveScore.FragmentationPenalty"/>.</param>
    public sealed record SoeWeights(
        double LoadBalanceWeight = 1.0,
        double ContextContinuityWeight = 1.0,
        double SessionQualityWeight = 1.0,
        double FatiguePenaltyWeight = 1.0,
        double FragmentationPenaltyWeight = 1.0)
    {
        /// <summary>
        /// Đúng một ràng buộc: không trọng số nào âm. Không kiểm tra tổng = 1.0 (xem lý do ở XML
        /// doc của type) — <see cref="WeightConfig.IsValid"/> KHÔNG phải tiền lệ áp dụng ở đây.
        /// </summary>
        public bool IsValid() =>
            LoadBalanceWeight >= 0 &&
            ContextContinuityWeight >= 0 &&
            SessionQualityWeight >= 0 &&
            FatiguePenaltyWeight >= 0 &&
            FragmentationPenaltyWeight >= 0;
    }
}
