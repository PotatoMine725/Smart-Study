using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Services.Soe;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.9 -- per-seam unit tests cho <see cref="OptimizerComparator"/> (G2-2) và
    /// <see cref="ScheduleOptimizer"/>/<see cref="IScheduleOptimizer"/> (G2-1/G2-4/G2-6). Loop và
    /// comparator được implement TỔNG QUÁT cho N stage (không hardcode N=1) -- phần lớn test ở
    /// đây dùng fake <see cref="IOptimizerStage"/>/<see cref="IConstraintValidator"/>/
    /// <see cref="IObjectiveEvaluator"/> đã "script" sẵn Eval mong muốn cho từng state, để engineer
    /// chính xác trajectory cần kiểm (bao gồm cả <c>Superseded_ByLaterCheckpoint</c> -- KHÔNG thể
    /// đạt được trong một pass với N=1 thật, vì cần k=2 trở lên trong CÙNG một pass để có "một
    /// checkpoint SAU" -- xem <see cref="RunPassAndSelect_AllFiveReasonCodesInOnePass"/> dùng N=5
    /// synthetic). Case dùng <see cref="LoadRebalanceStage"/> thật (N=1) kiểm phần còn lại: loop
    /// termination thực tế và <c>passes × N + 1</c> trên chính stage list đã ratify.
    /// </summary>
    public sealed class ScheduleOptimizerTests
    {
        private static readonly DateTime Day0 = new(2026, 8, 5);
        private static readonly SoeWeights UnitWeights = new(1, 1, 1, 1, 1);

        // =====================================================================================
        // OptimizerComparator (G2-2) -- trực tiếp trên Eval, không qua ScheduleOptimizer.
        // =====================================================================================

        [Fact]
        public void CompareFeasibility_ViolationCountDiffers_DecidesRegardlessOfOverdueMinutes()
        {
            var fewerViolationsHugeOverdue = new Eval(1, 100_000, 0);
            var moreViolationsTinyOverdue = new Eval(2, 1, 0);

            Assert.True(OptimizerComparator.CompareFeasibility(fewerViolationsHugeOverdue, moreViolationsTinyOverdue) < 0);
        }

        [Fact]
        public void CompareFeasibility_SameViolationCount_FallsBackToOverdueMinutes()
        {
            var lessOverdue = new Eval(1, 10, 0);
            var moreOverdue = new Eval(1, 20, 0);

            Assert.True(OptimizerComparator.CompareFeasibility(lessOverdue, moreOverdue) < 0);
        }

        [Fact]
        public void IsAdmissible_LessFeasibleThanPassEntry_IsFalse_RegardlessOfScore()
        {
            var passEntry = new Eval(0, 0, 0);
            var worseFeasibility = new Eval(1, 0, 1_000_000); // điểm khổng lồ không cứu được

            Assert.False(OptimizerComparator.IsAdmissible(worseFeasibility, passEntry));
        }

        [Fact]
        public void IsAdmissible_EqualOrBetterFeasibilityThanPassEntry_IsTrue()
        {
            var passEntry = new Eval(1, 50, 0);
            var sameFeasibility = new Eval(1, 50, -999);
            var betterFeasibility = new Eval(0, 0, -999);

            Assert.True(OptimizerComparator.IsAdmissible(sameFeasibility, passEntry));
            Assert.True(OptimizerComparator.IsAdmissible(betterFeasibility, passEntry));
        }

        [Fact]
        public void IsBetterThan_BetterFeasibility_IsTrueRegardlessOfScore()
        {
            var better = new Eval(0, 0, -1_000_000);
            var incumbent = new Eval(1, 0, 1_000_000);

            Assert.True(OptimizerComparator.IsBetterThan(better, incumbent));
        }

        [Fact]
        public void IsBetterThan_SameFeasibility_ScoreBarelyAboveEpsilon_IsTrue()
        {
            var incumbent = new Eval(0, 0, 10.0);
            double eps = OptimizerComparator.RelEps * Math.Max(1.0, Math.Abs(incumbent.Score));
            var justAbove = new Eval(0, 0, 10.0 + eps * 1.5);

            Assert.True(OptimizerComparator.IsBetterThan(justAbove, incumbent));
        }

        [Fact]
        public void IsBetterThan_SameFeasibility_ScoreWithinEpsilon_IsFalse()
        {
            var incumbent = new Eval(0, 0, 10.0);
            double eps = OptimizerComparator.RelEps * Math.Max(1.0, Math.Abs(incumbent.Score));
            var withinNoise = new Eval(0, 0, 10.0 + eps * 0.5);

            Assert.False(OptimizerComparator.IsBetterThan(withinNoise, incumbent));
        }

        [Fact]
        public void IsBetterThan_SameFeasibility_LowerScore_IsFalse()
        {
            var incumbent = new Eval(0, 0, 10.0);
            var lower = new Eval(0, 0, 5.0);

            Assert.False(OptimizerComparator.IsBetterThan(lower, incumbent));
        }

        // =====================================================================================
        // ScheduleOptimizer -- G2-6 reason codes, dùng fake stage/validator/evaluator "scripted".
        // Mỗi "state" là một schedule 1-item mà SoPhut mã hoá state id -- fake validator/evaluator
        // đọc id đó để trả Eval đã script sẵn, decoupled hoàn toàn khỏi domain thật.
        // =====================================================================================

        [Fact]
        public void RunPassAndSelect_AllFiveReasonCodesInOnePass()
        {
            // 5 stage tổng hợp, mỗi stage ép trạng thái sang một id cố định -- script Eval để tạo
            // đúng cả 5 reason code (G2-6) trong MỘT pass:
            //   C0 (id0): score=0, khả thi.
            //   C1 (id1): score=10, khả thi -> đánh bại C0 -> Selected (tạm).
            //   C2 (id2): score=10+5e-9 (trong epsilon của C1) -> Rejected_NoImprovement.
            //   C3 (id3): infeasible (1 violation) dù score "hấp dẫn" -> Rejected_Infeasible.
            //   C4 (id4): score=15, khả thi -> đánh bại C1 -> C1 bị Superseded, C4 = Selected (tạm).
            //   C5 (id5): score=12, khả thi, thấp hơn C4 quá epsilon -> Rejected_LowerScore.
            // k* cuối = 4.
            var validatorScript = new Dictionary<int, (int violations, int overdue)>
            {
                [0] = (0, 0),
                [1] = (0, 0),
                [2] = (0, 0),
                [3] = (1, 999),
                [4] = (0, 0),
                [5] = (0, 0),
            };
            var evaluatorScript = new Dictionary<int, double>
            {
                [0] = 0.0,
                [1] = 10.0,
                [2] = 10.0 + 5e-9,
                [3] = 12345.0, // irrelevant -- infeasible, không bao giờ được so sánh điểm
                [4] = 15.0,
                [5] = 12.0,
            };

            var stages = new IOptimizerStage[]
            {
                new MapToStateStage(1),
                new MapToStateStage(2),
                new MapToStateStage(3),
                new MapToStateStage(4),
                new MapToStateStage(5),
            };
            var optimizer = new ScheduleOptimizer(
                stages,
                new ScriptedValidator(validatorScript),
                new ScriptedEvaluator(evaluatorScript),
                UnitWeights);

            var (finalSchedule, report) = optimizer.Optimize(new[] { StateItem(0) });

            var pass1 = report.Passes[0];
            Assert.Equal(4, pass1.KStar);
            Assert.Equal(6, pass1.Checkpoints.Count); // C0..C5

            Assert.Null(pass1.Checkpoints[0].Reason); // C0 không mang code
            Assert.Equal(OptimizerCheckpointReason.Superseded_ByLaterCheckpoint, pass1.Checkpoints[1].Reason);
            Assert.Equal(OptimizerCheckpointReason.Rejected_NoImprovement, pass1.Checkpoints[2].Reason);
            Assert.Equal(OptimizerCheckpointReason.Rejected_Infeasible, pass1.Checkpoints[3].Reason);
            Assert.Equal(OptimizerCheckpointReason.Selected, pass1.Checkpoints[4].Reason);
            Assert.Equal(OptimizerCheckpointReason.Rejected_LowerScore, pass1.Checkpoints[5].Reason);

            // Pass 2 chạy trên trạng thái đã commit (id=4, score=15) -- không stage nào (đều ép
            // cứng về id 1..5) đánh bại nó -> k*=0 -> Terminated_FixedPoint, dừng ở pass 2.
            Assert.Equal(OptimizerTermination.Terminated_FixedPoint, report.Termination);
            Assert.Equal(2, report.PassCount);
            Assert.Equal(0, report.Passes[1].KStar);
            Assert.Equal(4, finalSchedule[0].SoPhut); // state id 4 được commit
        }

        [Fact]
        public void Optimize_PassLimitReached_ReportsTerminatedPassLimit_AfterExactlyMaxPasses()
        {
            // Stage luôn cải thiện (id tăng dần, score = id) -- không bao giờ đạt fixed point
            // trong 4 pass -- phải chạy đủ MaxPasses rồi dừng, KHÔNG phải cycle guard.
            var optimizer = new ScheduleOptimizer(
                new IOptimizerStage[] { new IncrementStage() },
                new AlwaysFeasibleValidator(),
                new IdentityScoreEvaluator(),
                UnitWeights);

            var (finalSchedule, report) = optimizer.Optimize(new[] { StateItem(0) });

            Assert.Equal(OptimizerTermination.Terminated_PassLimit, report.Termination);
            Assert.Equal(ScheduleOptimizer.MaxPasses, report.PassCount);
            Assert.Equal(4, report.Passes.Count);
            Assert.All(report.Passes, p => Assert.Equal(1, p.KStar)); // mỗi pass đều commit stage duy nhất
            Assert.Equal(ScheduleOptimizer.MaxPasses, finalSchedule[0].SoPhut); // id tăng đúng 1/pass
        }

        [Fact]
        public void Optimize_StatesMaterialized_EqualsPassesTimesNPlusOne_ForMultiStageList()
        {
            // Tái dùng kịch bản 5-reason-code ở trên (N=5) nhưng bọc mỗi stage bằng bộ đếm --
            // xác nhận đúng "passes × N + 1" (G2-1's bright line) trên MỘT stage list N>1, chứng
            // minh loop machinery KHÔNG hardcode N=1 (N=1 chỉ là cấu hình đăng ký, không phải một
            // giới hạn của chính ScheduleOptimizer).
            var validatorScript = new Dictionary<int, (int violations, int overdue)>
            {
                [0] = (0, 0), [1] = (0, 0), [2] = (0, 0), [3] = (1, 999), [4] = (0, 0), [5] = (0, 0),
            };
            var evaluatorScript = new Dictionary<int, double>
            {
                [0] = 0.0, [1] = 10.0, [2] = 10.0 + 5e-9, [3] = 12345.0, [4] = 15.0, [5] = 12.0,
            };

            var counting = new List<CountingStage>();
            var stages = new IOptimizerStage[5];
            for (int i = 0; i < 5; i++)
            {
                var c = new CountingStage(new MapToStateStage(i + 1));
                counting.Add(c);
                stages[i] = c;
            }

            var optimizer = new ScheduleOptimizer(
                stages,
                new ScriptedValidator(validatorScript),
                new ScriptedEvaluator(evaluatorScript),
                UnitWeights);

            var (_, report) = optimizer.Optimize(new[] { StateItem(0) });

            int n = stages.Length;
            int totalApplyCalls = counting.Sum(c => c.CallCount);
            int totalMaterializedStates = totalApplyCalls + 1; // +1 = trạng thái đầu vào gốc, chưa từng qua Apply

            Assert.Equal(report.PassCount * n, totalApplyCalls);
            Assert.Equal(report.PassCount * n + 1, totalMaterializedStates);
        }

        // =====================================================================================
        // Tích hợp với LoadRebalanceStage thật (N=1) -- stage list đã ratify (T3.9 DoR §8).
        // =====================================================================================

        [Fact]
        public void Optimize_RealLoadRebalanceStage_SingleUsedDay_TerminatesFixedPointImmediately()
        {
            var t1 = Guid.NewGuid();
            var schedule = new List<ScheduledItem>
            {
                new(t1, Day0.AddDays(30), "A", "A", "Toán", Day0, 60),
                new(t1, Day0.AddDays(30), "A", "A (Phần 2)", "Toán", Day0, 40),
            };

            var optimizer = new ScheduleOptimizer(
                new IOptimizerStage[] { new LoadRebalanceStage() },
                new ConstraintValidator(),
                new ObjectiveEvaluator(),
                UnitWeights);

            var (finalSchedule, report) = optimizer.Optimize(schedule);

            Assert.Equal(OptimizerTermination.Terminated_FixedPoint, report.Termination);
            Assert.Equal(1, report.PassCount);
            Assert.Equal(0, report.Passes[0].KStar);
            // <=1 ngày dùng -> LoadRebalanceStage no-op -> C1 hoà tuyệt đối với C0.
            Assert.Equal(OptimizerCheckpointReason.Rejected_NoImprovement, report.Passes[0].Checkpoints[1].Reason);
            Assert.Equal(schedule, finalSchedule);
        }

        [Fact]
        public void Optimize_RealLoadRebalanceStage_ImbalancedFeasibleSchedule_CommitsAnImprovingMove()
        {
            var t1 = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);

            // D_max = Day0 (150+50=200), D_min = Day1 (20). gap=180 -> item 150 eligible ->
            // pass 1 dời nó (LoadBalance cải thiện) -> k*=1. Optimize() KHÔNG dừng lại sau một
            // pass -- vòng lặp ngoài (G2-4) tiếp tục cho tới fixed point: pass 2 xét lại trên
            // {Day0=50, Day1=170}, thấy gap=120, dời item C=20 (không phải A=150, quá khổ so với
            // gap mới) -> {Day0=70, Day1=150}; pass 3 xét lại, gap=80, A=150 trên D_max không
            // còn eligible -> no-op -> k*=0 -> Terminated_FixedPoint. Test này pin ĐÚNG hành vi
            // hội tụ nhiều-pass đó, không chỉ pass đầu tiên.
            var schedule = new List<ScheduledItem>
            {
                new(t1, farHanChot, "A", "A", "Toán", Day0, 150),
                new(t1, farHanChot, "B", "B", "Toán", Day0, 50),
                new(t1, farHanChot, "C", "C", "Toán", Day0.AddDays(1), 20),
            };

            var optimizer = new ScheduleOptimizer(
                new IOptimizerStage[] { new LoadRebalanceStage() },
                new ConstraintValidator(),
                new ObjectiveEvaluator(),
                UnitWeights);

            var (finalSchedule, report) = optimizer.Optimize(schedule);

            // Pass 1 commit đúng move đầu tiên đã lập luận thủ công ở trên.
            Assert.Equal(1, report.Passes[0].KStar);
            Assert.Equal(OptimizerCheckpointReason.Selected, report.Passes[0].Checkpoints[1].Reason);

            // Hội tụ sau 3 pass, dừng bằng fixed point (không phải pass limit).
            Assert.Equal(OptimizerTermination.Terminated_FixedPoint, report.Termination);
            Assert.Equal(3, report.PassCount);
            Assert.Equal(70, finalSchedule.Where(i => i.Date == Day0).Sum(i => i.SoPhut));
            Assert.Equal(150, finalSchedule.Where(i => i.Date == Day0.AddDays(1)).Sum(i => i.SoPhut));
        }

        // =====================================================================================
        // Determinism: cùng input -> output byte-identical qua nhiều lần gọi (mức unit, độc lập
        // với determinism metric cấp corpus của master plan).
        // =====================================================================================

        [Fact]
        public void Optimize_SameInput_ProducesIdenticalOutputAcrossRepeatedCalls()
        {
            var t1 = Guid.NewGuid();
            var t2 = Guid.NewGuid();
            var farHanChot = Day0.AddDays(60);
            var schedule = new List<ScheduledItem>
            {
                new(t1, farHanChot, "A", "A", "Toán", Day0, 150),
                new(t1, farHanChot, "B", "B", "Toán", Day0, 50),
                new(t2, farHanChot, "C", "C", "Vật Lý", Day0.AddDays(1), 20),
                new(t2, farHanChot.AddDays(5), "D", "D", "Hóa Học", Day0.AddDays(2), 90),
            };

            var optimizer = new ScheduleOptimizer(
                new IOptimizerStage[] { new LoadRebalanceStage() },
                new ConstraintValidator(),
                new ObjectiveEvaluator(),
                UnitWeights);

            var (result1, report1) = optimizer.Optimize(schedule);
            var (result2, _) = optimizer.Optimize(schedule);
            var (result3, _) = optimizer.Optimize(schedule);

            Assert.Equal(result1, result2);
            Assert.Equal(result1, result3);
            Assert.Equal(report1.Termination, optimizer.Optimize(schedule).Report.Termination);
            Assert.Equal(report1.PassCount, optimizer.Optimize(schedule).Report.PassCount);
        }

        // =====================================================================================
        // Fakes -- decoupled hoàn toàn khỏi domain thật, chỉ phục vụ engineer trajectory cho test.
        // =====================================================================================

        private static ScheduledItem StateItem(int id) =>
            new(Guid.Empty, DateTime.MaxValue, "S", "S", "X", new DateTime(2026, 1, 1), id);

        /// <summary>Ép trạng thái sang một id cố định, bỏ qua input -- mô phỏng một stage tổng
        /// hợp cho mục đích test loop machinery, không phải một LoadRebalanceStage thật.</summary>
        private sealed class MapToStateStage : IOptimizerStage
        {
            private readonly int _toStateId;
            public MapToStateStage(int toStateId) => _toStateId = toStateId;
            public IReadOnlyList<ScheduledItem> Apply(IReadOnlyList<ScheduledItem> schedule) => new[] { StateItem(_toStateId) };
        }

        /// <summary>Tăng state id thêm 1 mỗi lần gọi -- mô phỏng một chuỗi cải thiện vô hạn (dùng
        /// để kiểm Terminated_PassLimit, không phải để mô phỏng LoadRebalanceStage).</summary>
        private sealed class IncrementStage : IOptimizerStage
        {
            public IReadOnlyList<ScheduledItem> Apply(IReadOnlyList<ScheduledItem> schedule) => new[] { StateItem(schedule[0].SoPhut + 1) };
        }

        /// <summary>Bọc đếm số lần Apply được gọi -- phục vụ assertion "passes × N + 1".</summary>
        private sealed class CountingStage : IOptimizerStage
        {
            private readonly IOptimizerStage _inner;
            public CountingStage(IOptimizerStage inner) => _inner = inner;
            public int CallCount { get; private set; }
            public IReadOnlyList<ScheduledItem> Apply(IReadOnlyList<ScheduledItem> schedule)
            {
                CallCount++;
                return _inner.Apply(schedule);
            }
        }

        private sealed class ScriptedValidator : IConstraintValidator
        {
            private readonly IReadOnlyDictionary<int, (int violations, int overdue)> _script;
            public ScriptedValidator(IReadOnlyDictionary<int, (int violations, int overdue)> script) => _script = script;

            public ConstraintValidationResult Validate(IReadOnlyList<ScheduledItem> items)
            {
                var (violations, overdue) = _script[items[0].SoPhut];
                return new ConstraintValidationResult(violations == 0, violations, overdue, Array.Empty<DeadlineViolation>());
            }
        }

        private sealed class ScriptedEvaluator : IObjectiveEvaluator
        {
            private readonly IReadOnlyDictionary<int, double> _script;
            public ScriptedEvaluator(IReadOnlyDictionary<int, double> script) => _script = script;

            public ObjectiveScore Evaluate(IReadOnlyList<ScheduledItem> schedule, SoeWeights weights)
                => new(0, 0, 0, 0, 0, _script[schedule[0].SoPhut]);
        }

        private sealed class AlwaysFeasibleValidator : IConstraintValidator
        {
            public ConstraintValidationResult Validate(IReadOnlyList<ScheduledItem> items)
                => new(true, 0, 0, Array.Empty<DeadlineViolation>());
        }

        /// <summary>Score = state id trực tiếp -- dùng với <see cref="IncrementStage"/> để tạo một
        /// chuỗi cải thiện luôn strictly-better, không bao giờ đạt fixed point trong 4 pass.</summary>
        private sealed class IdentityScoreEvaluator : IObjectiveEvaluator
        {
            public ObjectiveScore Evaluate(IReadOnlyList<ScheduledItem> schedule, SoeWeights weights)
                => new(0, 0, 0, 0, 0, schedule[0].SoPhut);
        }
    }
}
