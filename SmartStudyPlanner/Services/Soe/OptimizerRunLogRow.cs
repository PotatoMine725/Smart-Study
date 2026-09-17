using System;
using System.ComponentModel.DataAnnotations;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// T3.7 (Epic 3, Card G) — một hàng telemetry PHẲNG (không FK, cùng lý do M8's
    /// <c>DifficultyLabelLog</c>/<c>WeightChangeLog</c>/<c>StudyTimeOutcomeLog</c> đứng độc lập:
    /// đây là log để phân tích/audit sau này, không phải một quan hệ nghiệp vụ cần ràng buộc toàn
    /// vẹn tham chiếu) — ghi lại ĐÚNG MỘT checkpoint của MỘT pass của MỘT lần gọi
    /// <see cref="IScheduleOptimizer.Optimize"/> (G2-6's report contract,
    /// <c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c> §"G2-6").
    ///
    /// <see cref="RunId"/> nhóm mọi hàng của CÙNG một lần gọi <c>Optimize</c> lại với nhau (không
    /// phải khoá chính — <see cref="Id"/> mới là khoá chính, một hàng/checkpoint). Số hàng của một
    /// <see cref="RunId"/> = tổng <c>Checkpoints.Count</c> trên mọi pass của report đó
    /// (<c>Σ(N+1)</c> qua các pass đã chạy).
    ///
    /// <see cref="Termination"/>/<see cref="PassCount"/> lặp lại GIỐNG NHAU trên mọi hàng cùng
    /// <see cref="RunId"/> (chúng là thuộc tính CẤP LỜI GỌI, không phải cấp checkpoint) — denormalize
    /// có chủ đích, đúng tinh thần "hàng phẳng" đã nêu ở trên: một reader (T3.5/G3's tuning data,
    /// DP-2) không cần JOIN gì để biết lần gọi đó dừng vì đâu.
    /// </summary>
    public sealed class OptimizerRunLogRow
    {
        [Key] public Guid Id { get; set; }

        /// <summary>Tag một lần gọi <see cref="IScheduleOptimizer.Optimize"/> — mọi checkpoint của
        /// mọi pass thuộc CÙNG lần gọi đó chia sẻ đúng một giá trị này.</summary>
        public Guid RunId { get; set; }

        public DateTime CreatedUtc { get; set; }

        /// <summary>Cấp lời gọi (G2-4) — lặp lại trên mọi hàng cùng <see cref="RunId"/>.</summary>
        public OptimizerTermination Termination { get; set; }

        /// <summary>Cấp lời gọi — số pass ĐÃ THỰC SỰ chạy, lặp lại trên mọi hàng cùng
        /// <see cref="RunId"/>.</summary>
        public int PassCount { get; set; }

        /// <summary>Cấp pass — 1-based, đúng thứ tự pass đã chạy trong <c>OptimizeReport.Passes</c>.</summary>
        public int PassIndex { get; set; }

        /// <summary>Cấp pass — k* đã chọn CỦA PASS NÀY, lặp lại trên mọi checkpoint cùng pass.</summary>
        public int KStar { get; set; }

        /// <summary>Cấp checkpoint — 0 = C₀ (trạng thái vào-pass), 1..N = sau stage thứ đó
        /// (<c>OptimizerCheckpoint.Index</c>).</summary>
        public int CheckpointIndex { get; set; }

        public int ViolationCount { get; set; }

        public long OverdueMinutes { get; set; }

        public double Score { get; set; }

        /// <summary>Nullable — <c>null</c> ĐÚNG cho C₀ (trạng thái vào-pass không phải một candidate
        /// được đánh giá, xem doc comment <c>OptimizerCheckpoint.Reason</c>), một trong
        /// <see cref="OptimizerCheckpointReason"/> cho C₁..C_N.</summary>
        public OptimizerCheckpointReason? Reason { get; set; }
    }
}
