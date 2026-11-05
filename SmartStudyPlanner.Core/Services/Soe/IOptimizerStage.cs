using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Một stage trong pass loop của <see cref="IScheduleOptimizer.Optimize"/> (G2-1,
    /// <c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>): một hàm thuần
    /// <c>schedule -&gt; schedule</c>, chạy KHÔNG điều kiện trong một pass (không có rollback
    /// giữa-run — đó là việc của checkpoint mechanism bên ngoài, không phải của stage).
    ///
    /// Ba ràng buộc áp dụng cho MỌI implementation của interface này (T3.9 DoR,
    /// <c>docs/plans/2026-08-06-t3.9-optimize-stage-list-design.md</c> §2/§3/§8):
    /// <list type="bullet">
    /// <item>KHÔNG đọc <see cref="SoeWeights"/> — theo D-G/D-J, chỉ checkpoint mechanism ngoài
    /// stage mới weight-aware; một stage đọc trọng số để tự quyết định move của nó là nhét
    /// objective-knowledge vào nửa "deterministic pipeline" của D-E, đúng hình dạng D-E cấm.</item>
    /// <item>ĐÚNG MỘT move mỗi lần gọi (§6 OQ2, owner ratified 2026-08-06) — không tự loop tới
    /// fixpoint bên trong trước khi trả kết quả. Đây là điều giữ cho phát biểu "số trạng thái
    /// materialize mỗi lần gọi Optimize là đúng <c>passes × N + 1</c>" (G2-1) không mơ hồ.</item>
    /// <item>KHÔNG BAO GIỜ tạo ngày mới (D2, T3.9 DoR §3) — chỉ allocator (T3.3) được mở ngày mới.</item>
    /// </list>
    /// </summary>
    public interface IOptimizerStage
    {
        /// <summary>
        /// Áp dụng rule của stage lên <paramref name="schedule"/> hiện tại, trả về lịch mới (hoặc
        /// chính <paramref name="schedule"/> nếu stage là no-op cho input này). Không side effect
        /// nào ngoài giá trị trả về — <paramref name="schedule"/> KHÔNG bị mutate.
        /// </summary>
        IReadOnlyList<ScheduledItem> Apply(IReadOnlyList<ScheduledItem> schedule);
    }
}
