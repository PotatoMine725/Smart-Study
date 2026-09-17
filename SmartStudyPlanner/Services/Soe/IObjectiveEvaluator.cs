using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// T3.2 (Epic 3, Card E). Đo CHẤT LƯỢNG (quality only) của một lịch đã lên — không quyết định
    /// tính khả thi (đó là <c>IConstraintValidator</c>, T3.1/Card D — một seam độc lập mà kiểu này
    /// không tham chiếu tới, theo D-J). Chỉ xếp hạng các candidate mà validator ĐÃ chấp nhận.
    ///
    /// Không có tham số nào liên quan hạn chót của task nguồn ở đây, và sẽ không bao giờ có — D-G
    /// (<c>docs/plans/2026-07-02-architecture-freeze-decisions.md</c>, dòng 28-55) khoá khái niệm
    /// độ khẩn cấp theo hạn chót thuộc riêng về <c>PriorityCalculator</c> của Decision Engine; nó
    /// re-enter SOE duy nhất như một HARD CONSTRAINT ở Card D, không bao giờ như một số hạng
    /// objective ở đây. Xem <c>ObjectiveEvaluatorTests</c> (test project, hai case cuối file) —
    /// assertion này được kiểm bằng reflection + quét source, không chỉ nêu trong comment.
    /// </summary>
    public interface IObjectiveEvaluator
    {
        /// <summary>
        /// Tính điểm chất lượng cho một lịch, biểu diễn bằng danh sách chunk đã lên
        /// (<see cref="ScheduledItem"/>). Không có giả định thứ tự phần tử trong <paramref name="schedule"/>
        /// — mọi số hạng được tính bằng cách group theo <see cref="ScheduledItem.Date"/> hoặc
        /// <see cref="ScheduledItem.MaTask"/>, không bao giờ theo vị trí danh sách (đúng bài học
        /// từ vụ false positional-contract bug ở <c>WorkloadServiceIdentityTests</c>).
        /// </summary>
        /// <param name="schedule">Danh sách chunk đã lên lịch — có thể rỗng (lịch trống, trả điểm
        /// trung tính/vacuous cho mọi số hạng, xem implementation).</param>
        /// <param name="weights">Vector trọng số w1..w5, xem <see cref="SoeWeights"/>.</param>
        ObjectiveScore Evaluate(IReadOnlyList<ScheduledItem> schedule, SoeWeights weights);
    }
}
