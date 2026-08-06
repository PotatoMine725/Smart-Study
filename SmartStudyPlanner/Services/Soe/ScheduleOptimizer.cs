using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// Implementation DUY NHẤT của <see cref="IScheduleOptimizer"/> cho T3.9. Implement G2-1
    /// (run-all/commit-best-prefix), G2-2 (comparator, qua <see cref="OptimizerComparator"/>),
    /// G2-3 (epsilon), G2-4 (vòng lặp fixed-point ngoài), G2-6 (report contract) ĐÚNG NHƯ ratified
    /// -- KHÔNG redesign bất kỳ phần nào trong số này (xem
    /// <c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>).
    ///
    /// Nhận danh sách stage qua constructor (<see cref="IReadOnlyList{T}"/> của
    /// <see cref="IOptimizerStage"/>) -- vòng lặp/comparator ở đây KHÔNG hardcode N. N=1
    /// (<see cref="LoadRebalanceStage"/> DUY NHẤT) là quyết định của T3.9 DoR
    /// (<c>docs/plans/2026-08-06-t3.9-optimize-stage-list-design.md</c> §8), nằm ở CHỖ ĐĂNG KÝ
    /// stage list (composition root), không nằm ở class này.
    ///
    /// Hai seam độc lập <see cref="IConstraintValidator"/>/<see cref="IObjectiveEvaluator"/> được
    /// tiêm riêng biệt và KHÔNG BAO GIỜ được gọi lẫn nhau ở đây (D-J) -- class này chỉ là
    /// checkpoint mechanism kết hợp output của cả hai qua <see cref="Eval"/>/
    /// <see cref="OptimizerComparator"/>, đúng vai trò G2-2 gán cho nó.
    ///
    /// D-E core (không global search): mỗi pass đánh giá đúng <c>N+1</c> trạng thái (C₀..C_N, C₀
    /// là trạng thái vào-pass tái sử dụng, KHÔNG tính lại) -- tổng cả lần gọi Optimize là đúng
    /// <c>passes × N + 1</c> trạng thái (G2-1's bright line), vì C₀ của pass sau (nếu có) chính là
    /// trạng thái đã commit của pass trước, một trong các C_k đã materialize ở pass đó, không phải
    /// một trạng thái mới. Branching factor 1: mỗi checkpoint chỉ có ĐÚNG MỘT đầu vào, ĐÚNG MỘT
    /// đầu ra (chữ ký <see cref="IOptimizerStage.Apply"/>) -- không gì được sinh ra chỉ để bị loại.
    /// </summary>
    public sealed class ScheduleOptimizer : IScheduleOptimizer
    {
        /// <summary>G2-4: "MaxPasses = 4 is a runtime bound, not a cycle guard" -- implementation
        /// constant T3.3 owns; không phải một quyết định được mở lại ở đây.</summary>
        public const int MaxPasses = 4;

        private readonly IReadOnlyList<IOptimizerStage> _stages;
        private readonly IConstraintValidator _validator;
        private readonly IObjectiveEvaluator _evaluator;
        private readonly SoeWeights _weights;

        public ScheduleOptimizer(
            IReadOnlyList<IOptimizerStage> stages,
            IConstraintValidator validator,
            IObjectiveEvaluator evaluator,
            SoeWeights weights)
        {
            _stages = stages ?? throw new ArgumentNullException(nameof(stages));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _weights = weights ?? throw new ArgumentNullException(nameof(weights));
        }

        public (IReadOnlyList<ScheduledItem> Schedule, OptimizeReport Report) Optimize(IReadOnlyList<ScheduledItem> schedule)
        {
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            var state = schedule;
            var passes = new List<OptimizerPassReport>();

            for (int pass = 1; pass <= MaxPasses; pass++)
            {
                var (committed, kStar, passReport) = RunPassAndSelect(state);
                passes.Add(passReport);

                if (kStar == 0)
                {
                    // k* = 0: pass không đổi gì -- pass kế tiếp chắc chắn lặp y hệt (G2-4),
                    // dừng ngay, không chạy một pass đã biết trước là vô ích.
                    return (state, new OptimizeReport(OptimizerTermination.Terminated_FixedPoint, pass, passes));
                }

                state = committed;

                if (pass == MaxPasses)
                {
                    return (state, new OptimizeReport(OptimizerTermination.Terminated_PassLimit, pass, passes));
                }
            }

            // Không thể chạm tới: vòng for luôn return ở kStar==0 hoặc ở pass==MaxPasses.
            throw new InvalidOperationException("ScheduleOptimizer.Optimize: unreachable -- vòng lặp phải return trong for.");
        }

        /// <summary>
        /// Chạy TOÀN BỘ stage list một lượt trên <paramref name="entry"/> (không rollback giữa
        /// chừng -- G2-1), materialize đúng <c>N</c> checkpoint mới (C₁..C_N; C₀ là chính
        /// <paramref name="entry"/>, tái sử dụng), rồi chọn k* theo G2-2/G2-6.
        /// </summary>
        private (IReadOnlyList<ScheduledItem> Committed, int KStar, OptimizerPassReport Report) RunPassAndSelect(
            IReadOnlyList<ScheduledItem> entry)
        {
            int n = _stages.Count;
            var states = new IReadOnlyList<ScheduledItem>[n + 1];
            var evals = new Eval[n + 1];

            states[0] = entry;
            evals[0] = EvaluateState(entry);

            for (int k = 1; k <= n; k++)
            {
                states[k] = _stages[k - 1].Apply(states[k - 1]);
                evals[k] = EvaluateState(states[k]);
            }

            // Selection loop của G2-2, mở rộng để gán reason code (G2-6) tại chỗ. "best" ở đây
            // luôn là incumbent thật sự tại thời điểm xét k (không backtrack) -- đúng vì
            // IsBetterThan xây một chuỗi cải thiện đơn điệu ngặt, nên so k với incumbent-tại-thời-
            // điểm-k tương đương so k với k* cuối cùng (transitivity).
            var reasons = new OptimizerCheckpointReason?[n + 1]; // reasons[0] luôn null (C₀ không mang code)
            int best = 0;

            for (int k = 1; k <= n; k++)
            {
                if (!OptimizerComparator.IsAdmissible(evals[k], evals[0]))
                {
                    reasons[k] = OptimizerCheckpointReason.Rejected_Infeasible;
                    continue;
                }

                if (OptimizerComparator.IsBetterThan(evals[k], evals[best]))
                {
                    // C₀ không bao giờ mang reason code (kể cả khi bị đánh bại) -- chỉ demote
                    // incumbent cũ nếu nó là một checkpoint thật (k >= 1).
                    if (best != 0) reasons[best] = OptimizerCheckpointReason.Superseded_ByLaterCheckpoint;
                    reasons[k] = OptimizerCheckpointReason.Selected; // tạm thời -- có thể bị demote sau
                    best = k;
                }
                else
                {
                    bool tie = OptimizerComparator.CompareFeasibility(evals[k], evals[best]) == 0
                        && Math.Abs(evals[k].Score - evals[best].Score)
                           <= OptimizerComparator.RelEps * Math.Max(1.0, Math.Abs(evals[best].Score));

                    reasons[k] = tie
                        ? OptimizerCheckpointReason.Rejected_NoImprovement
                        : OptimizerCheckpointReason.Rejected_LowerScore;
                }
            }

            var checkpoints = new List<OptimizerCheckpoint>(n + 1);
            for (int k = 0; k <= n; k++)
            {
                checkpoints.Add(new OptimizerCheckpoint(k, evals[k], reasons[k]));
            }

            var passReport = new OptimizerPassReport(best, checkpoints);
            return (states[best], best, passReport);
        }

        private Eval EvaluateState(IReadOnlyList<ScheduledItem> state)
        {
            var validation = _validator.Validate(state);
            var score = _evaluator.Evaluate(state, _weights);
            return new Eval(validation.ViolatingTaskCount, validation.TotalOverdueMinutes, score.Total);
        }
    }
}
