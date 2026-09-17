using System;
using SmartStudyPlanner.Data;

namespace SmartStudyPlanner.Services.Soe
{
    /// <summary>
    /// T3.7 (Epic 3, Card G) — ghi các hàng <see cref="OptimizerRunLogRow"/> (G2-6 report contract)
    /// xuống <see cref="AppDbContext"/> cho MỘT lần gọi <see cref="IScheduleOptimizer.Optimize"/>.
    /// KHÔNG phải một seam công khai đã được wire vào bất kỳ pipeline sản phẩm nào ở Card G này —
    /// giống cách <c>IConstraintValidator</c>/<c>IObjectiveEvaluator</c> tồn tại trước khi được
    /// tiêu thụ, seam này tồn tại và được tiêu thụ trong test harness (T3.4's corpus run) làm
    /// integration test thật, không phải một class bị bỏ hoang không caller nào.
    /// </summary>
    public interface IOptimizerRunLogWriter
    {
        /// <summary>Ghi mọi checkpoint của mọi pass trong <paramref name="report"/> thành các hàng
        /// riêng biệt, cùng gắn <paramref name="runId"/>.</summary>
        void Persist(Guid runId, OptimizeReport report);
    }

    /// <summary>
    /// Implementation DUY NHẤT của <see cref="IOptimizerRunLogWriter"/>. Nhận factory tạo
    /// <see cref="AppDbContext"/> mới mỗi lần gọi (cùng idiom với các repository Sqlite hiện có,
    /// vd. <c>SqliteWeightChangeLogRepository</c>) thay vì giữ một context sống lâu — an toàn với
    /// vòng đời ngắn hạn của SQLite connection trong test lẫn production.
    /// </summary>
    public sealed class OptimizerRunLogWriter : IOptimizerRunLogWriter
    {
        private readonly Func<AppDbContext> _ctxFactory;
        private readonly Func<DateTime> _clock;

        public OptimizerRunLogWriter(Func<AppDbContext> ctxFactory, Func<DateTime>? clock = null)
        {
            _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public void Persist(Guid runId, OptimizeReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var now = _clock();
            using var db = _ctxFactory();

            for (int passIdx = 0; passIdx < report.Passes.Count; passIdx++)
            {
                var pass = report.Passes[passIdx];
                foreach (var checkpoint in pass.Checkpoints)
                {
                    db.OptimizerRunLogs.Add(new OptimizerRunLogRow
                    {
                        Id = Guid.NewGuid(),
                        RunId = runId,
                        CreatedUtc = now,
                        Termination = report.Termination,
                        PassCount = report.PassCount,
                        PassIndex = passIdx + 1,
                        KStar = pass.KStar,
                        CheckpointIndex = checkpoint.Index,
                        ViolationCount = checkpoint.Eval.ViolationCount,
                        OverdueMinutes = checkpoint.Eval.OverdueMinutes,
                        Score = checkpoint.Eval.Score,
                        Reason = checkpoint.Reason,
                    });
                }
            }

            db.SaveChanges();
        }
    }
}
