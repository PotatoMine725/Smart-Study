using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SmartStudyPlanner.Services.Soe;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.4 (Epic 3, Card G) — D-H invariant + inversion property suite trên corpus T3.6
    /// (<see cref="SoeCorpusGenerator.Generate"/>, qua <see cref="SoeOptimizeCorpusMetrics"/>).
    ///
    /// Per G2 note D5 (<c>docs/plans/2026-08-04-g2-optimization-pass-semantics.md</c>): D-H trên
    /// seam <c>Optimize</c> và inversion property trên allocator là HAI test riêng biệt, KHÔNG gộp
    /// -- gộp sẽ ngầm gán công của allocator (T3.3) cho bảo chứng cấu trúc của G2. Class này giữ
    /// đúng ranh giới đó: <see cref="DH_Optimize_ViolationsNeverWorsen_AcrossFullCorpus"/> (a) và
    /// <see cref="Inversion_Allocator_TotalAcrossCorpus_ComparedAgainstFrozenBaseline"/> (b) là hai
    /// method độc lập, không method nào gọi method kia.
    /// </summary>
    public class SoeT34InvariantTests
    {
        private readonly ITestOutputHelper _output;
        public SoeT34InvariantTests(ITestOutputHelper output) => _output = output;

        // =====================================================================================
        // (a) D-H trên seam Optimize -- structural, "should be near-impossible to fail" (G2 note).
        // =====================================================================================

        [Fact]
        public void DH_Optimize_ViolationsNeverWorsen_AcrossFullCorpus()
        {
            var results = SoeOptimizeCorpusMetrics.Results;
            Assert.True(results.Count >= 200, $"corpus có {results.Count} item, cần >=200.");

            int breaches = 0;
            var breachIds = new List<string>();

            foreach (var r in results)
            {
                // Eval theo ĐÚNG thứ tự so sánh D-H (ViolatingTaskCount trước, TotalOverdueMinutes
                // sau) -- dùng chính OptimizerComparator sản xuất, KHÔNG tự viết lại so sánh.
                var before = new Eval(r.AllocatorValidation.ViolatingTaskCount, r.AllocatorValidation.TotalOverdueMinutes, 0);
                var after = new Eval(r.OptimizedValidation.ViolatingTaskCount, r.OptimizedValidation.TotalOverdueMinutes, 0);

                if (OptimizerComparator.CompareFeasibility(after, before) > 0)
                {
                    breaches++;
                    breachIds.Add(r.Id);
                }
            }

            _output.WriteLine($"D-H (Optimize seam): {results.Count} corpus item, {breaches} breach(es).");
            Assert.True(breaches == 0,
                $"D-H breach trên seam Optimize tại {breaches} item: {string.Join(",", breachIds.Take(10))}. " +
                "Theo G2 note D5, test này gần như không thể đỏ trừ khi OptimizerComparator bị sửa -- " +
                "một breach ở đây là tín hiệu về comparator, không phải về allocator.");
        }

        [Fact]
        public void Optimize_BranchingFactor_StatesEqualsPassesTimesNPlusOne_AcrossFullCorpus()
        {
            // G2-1's bright line ("passes x N + 1"), đã được unit-test trên fixture tổng hợp N=5
            // VÀ trên LoadRebalanceStage thật N=1 đơn lẻ (ScheduleOptimizerTests). Ở đây kiểm lại
            // trên TOÀN corpus thật (230 input đa dạng) để không chỉ dựa vào vài fixture tay --
            // dùng CountingStage bọc LoadRebalanceStage, tách khỏi Results (cache) vì cần đếm
            // Apply() riêng cho mỗi item.
            var scenarios = SoeCorpusGenerator.Generate();
            var validator = new ConstraintValidator();
            var evaluator = new ObjectiveEvaluator();

            long totalApplyCalls = 0;
            long totalPassCount = 0;

            foreach (var scenario in scenarios)
            {
                var (_, _, items) = SoeScheduleMetrics.RunScenarioWithIdentity(scenario);
                var counting = new CountingStage(new LoadRebalanceStage());
                var optimizer = new ScheduleOptimizer(
                    new IOptimizerStage[] { counting }, validator, evaluator, SoeOptimizeCorpusMetrics.DefaultWeights);

                var (_, report) = optimizer.Optimize(items);
                totalApplyCalls += counting.CallCount;
                totalPassCount += report.PassCount;
            }

            _output.WriteLine($"Branching factor: {totalPassCount} pass tổng cộng, {totalApplyCalls} Apply() call (N=1).");
            // Tổng Apply() call phải bằng đúng passCount * N (N=1) trên MỌI item cộng dồn.
            Assert.Equal(totalPassCount, totalApplyCalls);
        }

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

        // =====================================================================================
        // (b) Inversion property trên ALLOCATOR (không qua Optimize) -- criterion 1/5.
        // =====================================================================================

        [Fact]
        public void Inversion_Allocator_TotalAcrossCorpus_ComparedAgainstFrozenBaseline()
        {
            var results = SoeOptimizeCorpusMetrics.Results;

            var feasibleSubset = results.Where(r => r.DesignedFeasible).ToList();
            var infeasibleSubset = results.Where(r => !r.DesignedFeasible).ToList();

            int totalSelf = results.Sum(r => r.HarnessRecord.SelfInversions);
            int totalPairwise = results.Sum(r => r.HarnessRecord.PairwiseInversions);
            int totalInversions = totalSelf + totalPairwise;

            int feasibleSelf = feasibleSubset.Sum(r => r.HarnessRecord.SelfInversions);
            int feasiblePairwise = feasibleSubset.Sum(r => r.HarnessRecord.PairwiseInversions);
            int feasibleTotal = feasibleSelf + feasiblePairwise;

            int infeasibleSelf = infeasibleSubset.Sum(r => r.HarnessRecord.SelfInversions);
            int infeasiblePairwise = infeasibleSubset.Sum(r => r.HarnessRecord.PairwiseInversions);

            var baseline = SoeT34BaselineArtifact.LoadFrozen();

            _output.WriteLine($"Current allocator (earliest-feasible, post-T3.3) -- corpus of {results.Count}:");
            _output.WriteLine($"  Total inversions (self+pairwise): {totalInversions} (self={totalSelf}, pairwise={totalPairwise})");
            _output.WriteLine($"  Designed-feasible subset ({feasibleSubset.Count} items): {feasibleTotal} (self={feasibleSelf}, pairwise={feasiblePairwise})");
            _output.WriteLine($"  Designed-infeasible subset ({infeasibleSubset.Count} items): {infeasibleSelf + infeasiblePairwise} (self={infeasibleSelf}, pairwise={infeasiblePairwise})");
            _output.WriteLine($"  Frozen baseline (pre-T3.3, least-loaded) TotalDeadlineInversions: {baseline.Aggregate.TotalDeadlineInversions}");

            Assert.True(baseline.Aggregate.TotalDeadlineInversions > 0,
                "criterion 5 requires baseline > 0 -- xem SoeBaselineCaptureTests, đã pin điều này ở Card A.");

            // Positive, structural result: SelfInversions == 0 across the ENTIRE corpus (both
            // subsets) -- earliest-feasible placement (T3.3) fills days chronologically, so a chunk
            // landing on/after its own deadline day implies NO earlier day had slack; that is
            // exactly the self-miss inversion's own definition (MethodologyNote.DeadlineInversion
            // SelfMiss), so the class is eliminated by construction, not by luck. Pinned so a future
            // allocator change that reintroduces self-miss inversions goes red here.
            Assert.Equal(0, totalSelf);

            if (feasibleTotal > 0)
            {
                var offending = feasibleSubset.Where(r => r.HarnessRecord.SelfInversions + r.HarnessRecord.PairwiseInversions > 0)
                    .Select(r => r.Id).ToList();
                _output.WriteLine(
                    $"[FINDING] Criterion 5 ('0 deadline inversions... against a baseline > 0') is NOT " +
                    $"met in full: {feasibleTotal} PAIRWISE inversion(s) (self-miss is 0, see above) " +
                    $"remain on the designed-feasible subset alone ({feasibleSubset.Count} items, " +
                    $"{offending.Count} of them affected) -- a ~{100.0 * (baseline.Aggregate.TotalDeadlineInversions - totalInversions) / baseline.Aggregate.TotalDeadlineInversions:F0}% " +
                    $"reduction from baseline ({baseline.Aggregate.TotalDeadlineInversions} -> {totalInversions} " +
                    "corpus-wide), not the elimination the criterion's wording asserts. Root cause: A1 " +
                    "(execution plan §3.4) keeps priority, not deadline, as the task-ORDERING key -- a " +
                    "high-priority far-deadline task can still be placed ahead of a lower-priority near-" +
                    "deadline task. This is the SAME mechanism as the 4 genuine G2-5 arm-3 items in " +
                    "SoeT34CorpusReportTests (traced there on MIX-024) -- one architectural root cause " +
                    "(A1), two symptoms (residual pairwise inversions here, feasibility regressions " +
                    "there), one owner decision needed (whether A1 should be revisited, out of Card G's " +
                    "authority to change). Not tuned to zero here; reported as a finding, per this " +
                    "repo's discipline.");
            }

            // Criterion 5's "0 vs baseline > 0" chỉ có nghĩa trên tập THIẾT KẾ KHẢ THI -- tập
            // infeasible-by-design tất yếu còn vi phạm/inversion dù allocator có tốt cỡ nào (EDF
            // cumulative-demand đã vượt capacity từ đầu). KHÔNG assert 0 trên toàn corpus. Không
            // pin feasibleTotal == 0 (khác totalSelf ở trên): đây LÀ điểm findings này tồn tại để
            // báo cáo, pin về 0 sẽ tự mâu thuẫn với chính finding vừa nêu.
        }

        // =====================================================================================
        // (c) A6 cross-check -- criterion 10.
        // =====================================================================================

        [Fact]
        public void A6CrossCheck_NameResolutionPath_AgreesWithProductionValidator_UnderSameBoundary()
        {
            var results = SoeOptimizeCorpusMetrics.Results;

            // Đây là assertion THẬT của criterion 10: đường đi A6 (name-resolution qua Days) và
            // đường đi production (IConstraintValidator trực tiếp trên Items/identity thật) phải ra
            // CÙNG một số -- MIỄN LÀ giữ boundary CỐ ĐỊNH (cả hai đều dùng ">" -- production's
            // UniformDeadlinePolicy). Điều này chứng minh A6's crutch (name-stripping + resolve)
            // TƯƠNG ĐƯƠNG với identity thật (MaTask/HanChot trên ScheduledItem), KHÔNG lẫn với bất
            // đồng boundary riêng (đó là phần dưới, KHÔNG assert bằng).
            long totalNameResolvedChunks = 0, totalNameResolvedMinutes = 0;
            long totalProductionChunks = 0, totalProductionMinutes = 0;

            foreach (var r in results)
            {
                totalNameResolvedChunks += r.NameResolvedProductionBoundaryPair.Chunks;
                totalNameResolvedMinutes += r.NameResolvedProductionBoundaryPair.Minutes;
                totalProductionChunks += r.AllocatorValidation.Violations.Count;
                totalProductionMinutes += r.AllocatorValidation.TotalOverdueMinutes;
            }

            _output.WriteLine($"A6 cross-check (boundary held constant at production '>'):");
            _output.WriteLine($"  Name-resolved (harness identity path): {totalNameResolvedChunks} chunks, {totalNameResolvedMinutes} min");
            _output.WriteLine($"  Production validator (real identity):  {totalProductionChunks} chunks, {totalProductionMinutes} min");

            Assert.Equal(totalProductionChunks, totalNameResolvedChunks);
            Assert.Equal(totalProductionMinutes, totalNameResolvedMinutes);

            // ---- Named finding, NOT asserted equal: the harness's ORIGINAL boundary (">=") vs
            // production boundary (">") genuinely disagree on chunks placed EXACTLY on the deadline
            // day. Both HarnessRecord.DHViolationChunks (>=) and AllocatorValidation (>) are computed
            // on the SAME schedule (single GenerateScheduleWithIdentity call per scenario), so the
            // delta below is attributable ENTIRELY to the boundary choice, not to any allocator or
            // identity-resolution difference (already proven equal above).
            long totalHarnessBoundaryChunks = results.Sum(r => (long)r.HarnessBoundaryPairAllocator.Chunks);
            long totalHarnessBoundaryMinutes = results.Sum(r => (long)r.HarnessBoundaryPairAllocator.Minutes);

            // Self-check: ComputeHarnessBoundaryPair (direct on Items) must agree with
            // HarnessRecord.DHViolationChunks (name-resolution path, same ">=" boundary) -- both are
            // "harness boundary", just two different code paths (direct-field vs resolve-by-name).
            long totalHarnessRecordChunks = results.Sum(r => (long)r.HarnessRecord.DHViolationChunks);
            long totalHarnessRecordMinutes = results.Sum(r => (long)r.HarnessRecord.OverdueMinutes);
            Assert.Equal(totalHarnessRecordChunks, totalHarnessBoundaryChunks);
            Assert.Equal(totalHarnessRecordMinutes, totalHarnessBoundaryMinutes);

            long boundaryDeltaChunks = totalHarnessBoundaryChunks - totalProductionChunks;
            long boundaryDeltaMinutes = totalHarnessBoundaryMinutes - totalProductionMinutes;

            _output.WriteLine(
                $"[FINDING] Harness boundary ('>=', on-deadline-day counts as violation) vs production " +
                $"boundary ('>', on-deadline-day is OK): delta = {boundaryDeltaChunks} chunk(s), " +
                $"{boundaryDeltaMinutes} minute(s), attributable entirely to chunks placed exactly on " +
                "the deadline day (identity-resolution equivalence already proven above). This is a " +
                "genuine, previously-undocumented disagreement between the A6 measurement crutch " +
                "(SoeScheduleMetrics.ComputeRecord, Card A, predates T3.1) and the ratified production " +
                "predicate (UniformDeadlinePolicy, T3.1, date-only '>'). Not fixed here: editing " +
                "SoeScheduleMetrics's boundary would silently change what the frozen baseline artifact's " +
                "numbers mean (its own regression guard is [Fact(Skip=...)]). Named as a finding for the " +
                "closing report.");

            // task-vs-chunk reconciliation (the plan's explicitly-named issue): report both counts,
            // don't conflate.
            long totalTaskLevel = results.Sum(r => (long)r.AllocatorValidation.ViolatingTaskCount);
            _output.WriteLine(
                $"[Reconciliation] chunk-level (harness DHViolationChunks / validator Violations.Count) " +
                $"vs task-level (validator ViolatingTaskCount, distinct MaTask): chunk-level(production)=" +
                $"{totalProductionChunks}, task-level(production)={totalTaskLevel}. These are DIFFERENT " +
                "numbers by design (ConstraintValidationResult's own doc comment) -- not compared for " +
                "equality, reported side by side.");
        }

        // =====================================================================================
        // Criterion 6 -- determinism, 3 repeated full-corpus runs.
        // =====================================================================================

        [Fact]
        public void Optimize_CorpusRun_IsDeterministic_AcrossThreeRepeatedRuns()
        {
            var run1 = SoeOptimizeCorpusMetrics.RunFresh();
            var run2 = SoeOptimizeCorpusMetrics.RunFresh();
            var run3 = SoeOptimizeCorpusMetrics.RunFresh();

            Assert.Equal(run1.Count, run2.Count);
            Assert.Equal(run1.Count, run3.Count);

            int mismatches = 0;
            for (int i = 0; i < run1.Count; i++)
            {
                bool same12 = run1[i].OptimizedItems.SequenceEqual(run2[i].OptimizedItems)
                    && run1[i].Report.Termination == run2[i].Report.Termination
                    && run1[i].Report.PassCount == run2[i].Report.PassCount;
                bool same13 = run1[i].OptimizedItems.SequenceEqual(run3[i].OptimizedItems)
                    && run1[i].Report.Termination == run3[i].Report.Termination
                    && run1[i].Report.PassCount == run3[i].Report.PassCount;

                if (!same12 || !same13) mismatches++;
            }

            _output.WriteLine($"Determinism: {run1.Count} corpus items x 3 runs, {mismatches} mismatch(es).");
            Assert.Equal(0, mismatches);
        }

        // =====================================================================================
        // Criterion 7 -- explainability: 100% of rejected candidates carry a reason code.
        // =====================================================================================

        [Fact]
        public void Explainability_AllNonC0CheckpointsCarryReasonCode_AcrossFullCorpus()
        {
            var results = SoeOptimizeCorpusMetrics.Results;

            int totalC0 = 0, totalNonC0 = 0, missingOnNonC0 = 0, reasonOnC0 = 0;
            foreach (var r in results)
            {
                foreach (var pass in r.Report.Passes)
                {
                    foreach (var cp in pass.Checkpoints)
                    {
                        if (cp.Index == 0)
                        {
                            totalC0++;
                            if (cp.Reason != null) reasonOnC0++;
                        }
                        else
                        {
                            totalNonC0++;
                            if (cp.Reason == null) missingOnNonC0++;
                        }
                    }
                }
            }

            _output.WriteLine($"Explainability: {totalC0} C0 checkpoints (reason must be null), " +
                $"{totalNonC0} non-C0 checkpoints (reason must be non-null), {missingOnNonC0} missing.");
            Assert.Equal(0, missingOnNonC0);
            Assert.Equal(0, reasonOnC0);
        }

        // =====================================================================================
        // Criterion 9 -- runtime, reported (provisional metric, not gated here).
        // =====================================================================================

        [Fact]
        public void Runtime_OptimizeCall_LatencyAcrossCorpus_Reported()
        {
            var scenarios = SoeCorpusGenerator.Generate();
            var validator = new ConstraintValidator();
            var evaluator = new ObjectiveEvaluator();

            var latenciesMs = new List<double>(scenarios.Count);
            foreach (var scenario in scenarios)
            {
                var (_, _, items) = SoeScheduleMetrics.RunScenarioWithIdentity(scenario);
                var optimizer = new ScheduleOptimizer(
                    new IOptimizerStage[] { new LoadRebalanceStage() }, validator, evaluator, SoeOptimizeCorpusMetrics.DefaultWeights);

                var sw = Stopwatch.StartNew();
                optimizer.Optimize(items);
                sw.Stop();
                latenciesMs.Add(sw.Elapsed.TotalMilliseconds);
            }

            latenciesMs.Sort();
            double p95 = latenciesMs[(int)Math.Min(latenciesMs.Count - 1, Math.Ceiling(0.95 * latenciesMs.Count) - 1)];
            double mean = latenciesMs.Average();
            double max = latenciesMs[^1];

            _output.WriteLine($"Runtime per Optimize() call over {latenciesMs.Count} corpus items: " +
                $"mean={mean:F4}ms, p95={p95:F4}ms, max={max:F4}ms (master plan budget: <2000ms p95, provisional).");

            Assert.True(p95 < 2000.0, $"p95 latency {p95:F4}ms exceeds the provisional 2s budget.");
        }
    }
}
