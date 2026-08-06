namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Bộ ba đánh giá của một checkpoint trong pass loop của <see cref="IScheduleOptimizer"/>
    /// (G2-2, <c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>): cặp feasibility
    /// theo D-H (<see cref="ViolationCount"/> so trước, <see cref="OverdueMinutes"/> so sau) +
    /// <see cref="Score"/> chất lượng theo D-J. <see cref="OverdueMinutes"/> là <c>long</c> —
    /// đúng chữ ký <c>Eval</c> mà G2-2 quy định nguyên văn, dù
    /// <see cref="ConstraintValidationResult.TotalOverdueMinutes"/> hiện là <c>int</c> (đã clamp
    /// ở Card D) — widen ở đây, không thu hẹp lại seam D đã ship.
    /// </summary>
    public readonly record struct Eval(int ViolationCount, long OverdueMinutes, double Score);
}
