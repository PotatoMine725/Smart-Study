using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// T3.9 (Epic 3): seam <c>Optimize(schedule) -&gt; (schedule, report)</c> mà GATE G2
    /// (<c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>) đặc tả pass-loop mechanics
    /// -- "run-all, commit-best-prefix" (G2-1), comparator lexicographic (G2-2/G2-3), vòng lặp
    /// fixed-point ngoài (G2-4), report contract (G2-6). Chữ ký GIỮ NGUYÊN như ratified -- nhận và
    /// chỉ nhận một <see cref="ScheduledItem"/> list, không tham số khác (D2, T3.9 DoR §3: một
    /// stage/seam cần capacity context bên ngoài sẽ ép đổi chữ ký này, điều G2 chưa cho phép).
    ///
    /// Đây là một seam CHƯA được tiêu thụ ở đâu trong production code (giống
    /// <see cref="IConstraintValidator"/>/<see cref="IObjectiveEvaluator"/> trước khi T3.9 land) --
    /// wiring output của allocator qua <c>Optimize()</c> vào <c>WorkloadServiceImpl</c> là việc của
    /// T3.4/Card G, KHÔNG phải T3.9.
    /// </summary>
    public interface IScheduleOptimizer
    {
        /// <summary>
        /// Chạy vòng lặp pass (G2-4) trên <paramref name="schedule"/> đầu vào, trả về lịch đã
        /// commit (feasibility &lt;= đầu vào, theo transitivity của D-H -- G2-2) cùng báo cáo đầy
        /// đủ theo G2-6.
        /// </summary>
        (IReadOnlyList<ScheduledItem> Schedule, OptimizeReport Report) Optimize(IReadOnlyList<ScheduledItem> schedule);
    }
}
