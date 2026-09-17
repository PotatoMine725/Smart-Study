using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// G2-6: mã lý do cho một checkpoint <c>C₁…C_N</c> của một pass (KHÔNG áp dụng cho
    /// <c>C₀</c> — xem doc comment của <see cref="OptimizerCheckpoint.Reason"/>). Đúng MỘT mã cho
    /// mỗi checkpoint, theo cách được xây (by construction), thoả metric explainability của
    /// master plan ("100% of rejected candidates carry a reason code").
    /// </summary>
    public enum OptimizerCheckpointReason
    {
        /// <summary>Checkpoint này trở thành trạng thái commit của pass (k = k*).</summary>
        Selected,

        /// <summary>Cặp feasibility kém hơn trạng thái vào-pass. Không bao giờ selectable, dù
        /// điểm số cao đến đâu (D-H/D-J).</summary>
        Rejected_Infeasible,

        /// <summary>Khả thi (admissible); điểm số thấp hơn incumbent một cách rõ ràng (quá
        /// epsilon).</summary>
        Rejected_LowerScore,

        /// <summary>Khả thi (admissible); điểm số nằm trong epsilon của incumbent — hoà, và hoà
        /// thì giữ k thấp hơn.</summary>
        Rejected_NoImprovement,

        /// <summary>Khả thi và tốt hơn trạng thái vào-pass, nhưng một checkpoint SAU đó còn tốt
        /// hơn. Đây là bằng chứng "all-or-nothing veto" đã biến mất — một cải thiện đã ĐO ĐƯỢC,
        /// bị một cái tốt hơn đánh bại, chứ KHÔNG bị veto (G2-1).</summary>
        Superseded_ByLaterCheckpoint,
    }

    /// <summary>Lý do dừng vòng lặp ngoài (G2-4).</summary>
    public enum OptimizerTermination
    {
        /// <summary>Một pass commit k* = 0 (không đổi gì) -- pass kế tiếp chắc chắn lặp lại y hệt,
        /// nên vòng lặp dừng ngay, không tốn một pass đã biết trước là vô ích.</summary>
        Terminated_FixedPoint,

        /// <summary>Chạm <c>MaxPasses</c> mà chưa đạt fixed point -- một giới hạn runtime (ngân
        /// sách &lt; 2s p95 của master plan), KHÔNG phải cycle guard (chuỗi trạng thái commit đã
        /// được chứng minh đơn điệu ngặt, không thể quay vòng -- G2-4).</summary>
        Terminated_PassLimit,
    }

    /// <summary>
    /// Một checkpoint trong trajectory của một pass.
    /// </summary>
    /// <param name="Index">k: 0 = trạng thái vào-pass (C₀), 1..N = checkpoint sau stage thứ k.</param>
    /// <param name="Eval">Cặp feasibility + Score tại checkpoint này.</param>
    /// <param name="Reason">Đúng MỘT trong <see cref="OptimizerCheckpointReason"/> cho
    /// <c>C₁…C_N</c>; <c>null</c> cho <c>C₀</c> -- C₀ là trạng thái vào-pass mà mọi candidate khác
    /// so sánh với, không phải một candidate được đánh giá (G2-6: "Every checkpoint C₁ … C_N of
    /// every pass carries exactly one code" -- câu này không nhắc C₀).</param>
    public sealed record OptimizerCheckpoint(int Index, Eval Eval, OptimizerCheckpointReason? Reason);

    /// <summary>Báo cáo của một pass (G2-1/G2-6).</summary>
    /// <param name="KStar">k* đã chọn của pass này; 0 nghĩa là pass không commit gì.</param>
    /// <param name="Checkpoints">C₀..C_N theo đúng thứ tự Index.</param>
    public sealed record OptimizerPassReport(int KStar, IReadOnlyList<OptimizerCheckpoint> Checkpoints);

    /// <summary>Báo cáo của một lần gọi <see cref="IScheduleOptimizer.Optimize"/> (G2-4/G2-6).</summary>
    /// <param name="Termination"><see cref="OptimizerTermination.Terminated_FixedPoint"/> hoặc
    /// <see cref="OptimizerTermination.Terminated_PassLimit"/>.</param>
    /// <param name="PassCount">Số pass đã thực sự chạy.</param>
    /// <param name="Passes">Báo cáo từng pass, đúng thứ tự đã chạy.</param>
    public sealed record OptimizeReport(
        OptimizerTermination Termination,
        int PassCount,
        IReadOnlyList<OptimizerPassReport> Passes);
}
