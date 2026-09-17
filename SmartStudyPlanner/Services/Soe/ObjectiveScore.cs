namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Kết quả của <see cref="IObjectiveEvaluator.Evaluate"/>: năm số hạng thô (trước khi nhân
    /// trọng số) cộng với <see cref="Total"/> đã tổ hợp tuyến tính theo <see cref="SoeWeights"/>.
    /// Giữ cả số hạng thô (không chỉ <see cref="Total"/>) để test có thể assert trực tiếp trên
    /// từng số hạng độc lập ("lịch tải đều thì LoadBalance cao") mà không cần mẹo cô lập trọng số,
    /// và để một caller tương lai (T3.3) giải thích "vì sao điểm thấp" theo từng thành phần —
    /// hợp với D-J: "explanations distinguish 'rejected: infeasible' from 'rejected: lower score'".
    ///
    /// Quy ước dấu (xem lý do đầy đủ trong seam-decision doc và XML doc của
    /// <see cref="SoeWeights"/>): ba số hạng "reward" (<see cref="LoadBalance"/>,
    /// <see cref="ContextContinuity"/>, <see cref="SessionQuality"/>) nằm trong [0, 1], 1 = tốt
    /// nhất. Hai số hạng "penalty" (<see cref="FatiguePenalty"/>, <see cref="FragmentationPenalty"/>)
    /// nằm trong [-1, 0], 0 = không có phạt, -1 = phạt tối đa — TỰ MANG DẤU ÂM, để công thức
    /// <c>Total = w1·LoadBalance + w2·ContextContinuity + w3·SessionQuality + w4·FatiguePenalty +
    /// w5·FragmentationPenalty</c> giữ đúng nguyên văn dấu "+" của D-G trong khi trọng số dương
    /// (w4, w5 &gt;= 0) vẫn kéo điểm xuống đúng như cái tên "penalty" ngụ ý.
    /// </summary>
    /// <param name="LoadBalance">[0,1], 1 = phân bổ SoPhut đều nhất giữa các ngày dùng.</param>
    /// <param name="ContextContinuity">[0,1], 1 = mỗi ngày tập trung vào ít môn nhất.</param>
    /// <param name="SessionQuality">[0,1], 1 = độ dài các chunk gần "vùng lý tưởng" nhất.</param>
    /// <param name="FatiguePenalty">[-1,0], 0 = không có chuỗi ngày tải nặng liên tiếp.</param>
    /// <param name="FragmentationPenalty">[-1,0], 0 = không task nào bị cắt thành nhiều chunk.</param>
    /// <param name="Total">Tổ hợp tuyến tính năm số hạng trên theo <see cref="SoeWeights"/> đã dùng.</param>
    public sealed record ObjectiveScore(
        double LoadBalance,
        double ContextContinuity,
        double SessionQuality,
        double FatiguePenalty,
        double FragmentationPenalty,
        double Total);
}
