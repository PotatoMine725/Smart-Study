using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using SmartStudyPlanner.Services.Soe;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>One item of the G2-5 three-arm partition report.</summary>
    public sealed class SoeT34ArmReportItem
    {
        [JsonPropertyOrder(0)] public string Id { get; set; } = string.Empty;
        [JsonPropertyOrder(1)] public string Category { get; set; } = string.Empty;

        /// <summary>1 = S strictly more feasible than B (no quality floor); 2 = identical
        /// feasibility (quality is a reported finding, not blocking); 3 = S strictly LESS feasible
        /// than B (hard fail, no waiver).</summary>
        [JsonPropertyOrder(2)] public int Arm { get; set; }

        // S = Optimize(NewAllocator(I)), harness boundary (">=") -- same unit as B below.
        [JsonPropertyOrder(3)] public int S_HarnessViolationChunks { get; set; }
        [JsonPropertyOrder(4)] public int S_HarnessOverdueMinutes { get; set; }

        // B = frozen T3.6b baseline (pre-T3.3 least-loaded allocator), harness boundary (">=").
        [JsonPropertyOrder(5)] public int B_HarnessViolationChunks { get; set; }
        [JsonPropertyOrder(6)] public int B_HarnessOverdueMinutes { get; set; }

        // S under the PRODUCTION unit/boundary (task-level, ">") -- reported for transparency,
        // NOT used for arm classification (see class doc comment for why).
        [JsonPropertyOrder(7)] public int S_ProductionViolatingTaskCount { get; set; }
        [JsonPropertyOrder(8)] public int S_ProductionOverdueMinutes { get; set; }

        [JsonPropertyOrder(9)] public double ScoreS { get; set; }

        /// <summary>Always null -- Score(B) is not computable from the frozen artifact. See
        /// <see cref="SoeT34ArmAggregate.ScoreBNote"/>.</summary>
        [JsonPropertyOrder(10)] public double? ScoreB { get; set; }
    }

    public sealed class SoeT34ArmAggregate
    {
        [JsonPropertyOrder(0)] public int ItemCount { get; set; }
        [JsonPropertyOrder(1)] public int Arm1Count { get; set; }
        [JsonPropertyOrder(2)] public int Arm2Count { get; set; }
        [JsonPropertyOrder(3)] public int Arm3Count { get; set; }
        [JsonPropertyOrder(4)] public bool ScoreBComputable { get; set; }
        [JsonPropertyOrder(5)] public string ScoreBNote { get; set; } = string.Empty;
    }

    public sealed class SoeT34CorpusReportArtifact
    {
        [JsonPropertyOrder(0)] public string ArtifactKind { get; set; } = "soe-t34-t37-corpus-report";
        [JsonPropertyOrder(1)]
        public string GeneratedFor { get; set; } =
            "Epic 3 / T3.4-T3.7 (Card G) -- G2-5 three-arm partition of S = Optimize(NewAllocator(I)) " +
            "against B = T3.6b frozen baseline, on the T3.6 corpus.";
        [JsonPropertyOrder(2)] public string HeadSha { get; set; } = string.Empty;
        [JsonPropertyOrder(3)]
        public string Methodology { get; set; } =
            "Arm classification compares S and B on the HARNESS boundary ('>=', on-deadline-day counts as " +
            "violation) for BOTH sides, because B's frozen record only stores the harness-boundary pair " +
            "(DHViolationChunks/OverdueMinutes) -- comparing S's production-boundary pair against B's " +
            "harness-boundary pair would be a unit mismatch that could manufacture a false arm-3. S's " +
            "production-unit pair is reported alongside for transparency, not used for the partition. " +
            "CRITICALLY, the classification key is OverdueMinutes ALONE, not (chunk-count, minutes) -- an " +
            "empirical finding during this card's own review: 29 items initially classified as arm 3 " +
            "using chunk-count-then-minutes turned out to be 24 pure chunking artifacts (S and B have " +
            "IDENTICAL OverdueMinutes but a different chunk split of the same overdue work -- e.g. " +
            "ODUE-001: S splits one already-overdue 60-minute task into two chunks across two days, B's " +
            "old least-loaded allocator happened to fit the same 60 minutes on one day as a single " +
            "chunk) and only 5 were a genuine minutes delta (4 worse, 1 better). Using chunk-count as a " +
            "cross-ALLOCATOR feasibility key is unsound for the same reason ConstraintValidationResult " +
            "itself distinguishes ViolatingTaskCount from chunk-level Violations.Count: how many pieces " +
            "an allocator happens to split a task into is an implementation detail, not a feasibility " +
            "fact. A true task-level violating-count for B is not computable (the frozen artifact stores " +
            "no per-item list, only the chunk-level aggregate), so OverdueMinutes -- itself invariant to " +
            "chunking, since it sums SoPhut across however many chunks a task was split into -- is the " +
            "only sound comparison key available. See SoeOptimizeCorpusItemResult's doc comment " +
            "(SoeOptimizeCorpusMetrics.cs) for the separate boundary-disagreement ('>=' vs '>') finding.";
        [JsonPropertyOrder(4)] public SoeT34ArmAggregate Aggregate { get; set; } = new();
        [JsonPropertyOrder(5)] public List<SoeT34ArmReportItem> Items { get; set; } = new();
    }

    /// <summary>
    /// T3.4 part (d) + T3.7 (Epic 3, Card G) — G2-5's three-arm partition (master plan success
    /// metric #8) over the T3.6 corpus, comparing <c>S = Optimize(NewAllocator(I))</c> (current
    /// allocator + real <see cref="ScheduleOptimizer"/>) against <c>B</c> (the frozen T3.6b
    /// baseline). Also the T3.7 integration point: this is the natural, real caller of
    /// <see cref="OptimizerRunLogWriter"/> per the execution plan's own instruction ("don't build
    /// T3.7's writer in isolation with no real caller").
    /// </summary>
    public class SoeT34CorpusReportTests
    {
        private static readonly DateTime FixedNow = new(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
        private readonly ITestOutputHelper _output;

        public SoeT34CorpusReportTests(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };

        // Excluded from the frozen-comparison, same reasoning as SoeBaselineCaptureTests: a
        // "captured at commit X" fact, not an invariant that must track current HEAD.
        private static readonly string[] ExcludedTopLevelFields = { "HeadSha" };

        [Fact]
        public void G25_ThreeArmPartition_ProducesReportArtifact_AndPersistsOptimizerRunLog()
        {
            var results = SoeOptimizeCorpusMetrics.Results;
            var baseline = SoeT34BaselineArtifact.LoadFrozen();
            var evaluator = new ObjectiveEvaluator();

            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { /* EnsureCreated */ }

            var writer = new OptimizerRunLogWriter(() => TestDb.Create(conn), () => FixedNow);

            var items = new List<SoeT34ArmReportItem>();
            int arm1 = 0, arm2 = 0, arm3 = 0;
            var arm3Ids = new List<string>();

            foreach (var r in results)
            {
                // T3.7 integration: persist THIS scenario's real OptimizeReport, tagged with a fresh
                // run id -- one Optimize() call = one run.
                writer.Persist(Guid.NewGuid(), r.Report);

                if (!baseline.SchedulesById.TryGetValue(r.Id, out var b))
                {
                    throw new InvalidOperationException(
                        $"Frozen baseline thiếu item '{r.Id}' -- corpus đã đổi shape kể từ Card A? " +
                        "Không thể partition item này.");
                }

                // Classification key: OverdueMinutes ALONE -- see Methodology string above for why
                // chunk-count is excluded (a cross-allocator chunking artifact, empirically confirmed
                // during this card's own review; not a feasibility signal).
                int minutesCmp = r.HarnessBoundaryPairOptimized.Minutes.CompareTo(b.OverdueMinutes);
                int arm = minutesCmp < 0 ? 1 : (minutesCmp == 0 ? 2 : 3);
                if (arm == 1) arm1++;
                else if (arm == 2) arm2++;
                else { arm3++; arm3Ids.Add(r.Id); }

                double scoreS = evaluator.Evaluate(r.OptimizedItems, SoeOptimizeCorpusMetrics.DefaultWeights).Total;

                items.Add(new SoeT34ArmReportItem
                {
                    Id = r.Id,
                    Category = r.Category,
                    Arm = arm,
                    S_HarnessViolationChunks = r.HarnessBoundaryPairOptimized.Chunks,
                    S_HarnessOverdueMinutes = r.HarnessBoundaryPairOptimized.Minutes,
                    B_HarnessViolationChunks = b.DHViolationChunks,
                    B_HarnessOverdueMinutes = b.OverdueMinutes,
                    S_ProductionViolatingTaskCount = r.OptimizedValidation.ViolatingTaskCount,
                    S_ProductionOverdueMinutes = r.OptimizedValidation.TotalOverdueMinutes,
                    ScoreS = scoreS,
                    ScoreB = null,
                });
            }

            _output.WriteLine($"G2-5 three-arm partition over {results.Count} corpus items:");
            _output.WriteLine($"  Arm 1 (S strictly more feasible than B, no quality floor): {arm1}");
            _output.WriteLine($"  Arm 2 (identical feasibility, quality is a reported finding): {arm2}");
            _output.WriteLine($"  Arm 3 (S strictly LESS feasible than B -- hard fail, no waiver): {arm3}"
                + (arm3 > 0 ? " -- " + string.Join(",", arm3Ids) : string.Empty));

            if (arm3 > 0)
            {
                _output.WriteLine(
                    $"[FINDING] G2-5 arm 3 (hard fail per master-plan success metric #8's disposition " +
                    $"table, owner ruling required at CP-4/M3.2 ship gate) hit for {arm3} item(s): " +
                    $"{string.Join(",", arm3Ids)}. On these, S = Optimize(NewAllocator(I)) is strictly " +
                    "LESS feasible (more OverdueMinutes) than B = the pre-T3.3 least-loaded baseline. " +
                    "Root cause TRACED (not inferred) on MIX-024, the most dramatic case (B=0 overdue " +
                    "minutes, S=180): its 7 tasks range priority 13.70..98.63; the allocator's priority-" +
                    "descending processing order places the LOWEST-priority task ('Thi cuối kỳ Tiếng " +
                    "Anh #0595', priority=13.70, HanChot offset=5 -- the EARLIEST deadline of all 7 " +
                    "tasks) dead last, by which point five higher-priority, later-deadline tasks (offsets " +
                    "7..21) have already consumed every day from offset 0 through 4. Its own two chunks " +
                    "land at offset 5 (60 min, exactly on its deadline day) and offset 6 (120 min, " +
                    "clearly overdue) -- 180 overdue minutes total, matching S exactly. This is the same " +
                    "root cause as the 220 residual (all-pairwise) deadline inversions reported in " +
                    "Inversion_Allocator_TotalAcrossCorpus_ComparedAgainstFrozenBaseline: A1 (execution " +
                    "plan §3.4) keeps priority as the task-ordering key and uses HanChot only to choose " +
                    "WHICH day a task's OWN chunk lands on, never to reorder tasks against each other -- " +
                    "so a high-priority, far-deadline task can consume all pre-deadline capacity ahead " +
                    "of a lower-priority, near-deadline one, even on inputs the EDF cumulative-demand " +
                    "test (DesignedFeasible) says are feasible under an optimal deadline-first schedule. " +
                    "LoadRebalanceStage (N=1) cannot repair this: it targets LoadBalance, not deadline " +
                    "feasibility, and G2-2's admissibility gate only prevents the Optimize seam from " +
                    "WORSENING what the allocator already produced -- it cannot repair an allocator-level " +
                    "regression. Not hard-asserted to zero: Card G is forbidden from touching " +
                    "WorkloadServiceImpl's placement logic, and this is a real architectural consequence " +
                    "of A1, not a measurement artifact (unlike the 24 chunk-count false positives " +
                    "filtered out above). PINNED below instead (characterization assertion) so this test " +
                    "goes red -- loudly, not silently -- the moment the count or membership changes, " +
                    "rather than relying on anyone reading passing-test output.");
            }

            // Characterization assertion (Card F precedent: "expected casualty, pinned"), not a
            // green-by-omission log line -- if the allocator's behavior on this corpus ever changes
            // (regresses further OR is fixed), this MUST go red and force a review, not pass silently.
            // Both G2-5's own "must be named and dispositioned" (arm 2) and "no waiver" (arm 3)
            // language rule out a breach passing unnoticed -- an ambient WriteLine on an otherwise-
            // green test is exactly that.
            Assert.Equal(4, arm3);
            Assert.Equal(
                new[] { "MIX-016", "MIX-019", "MIX-024", "MIX-047" },
                arm3Ids.OrderBy(x => x, StringComparer.Ordinal).ToArray());

            // T3.7 verification: total persisted rows must equal total checkpoints across the corpus.
            long expectedRows = results.Sum(r => (long)r.Report.Passes.Sum(p => p.Checkpoints.Count));
            using (var verify = TestDb.Create(conn))
            {
                long actualRows = verify.OptimizerRunLogs.Count();
                _output.WriteLine($"T3.7 OptimizerRunLog: {actualRows} row(s) persisted across {results.Count} runs (expected {expectedRows}).");
                Assert.Equal(expectedRows, actualRows);
            }

            var artifact = new SoeT34CorpusReportArtifact
            {
                HeadSha = RepoLocator.GetHeadSha(RepoLocator.FindRepoRoot()),
                Aggregate = new SoeT34ArmAggregate
                {
                    ItemCount = results.Count,
                    Arm1Count = arm1,
                    Arm2Count = arm2,
                    Arm3Count = arm3,
                    ScoreBComputable = false,
                    ScoreBNote =
                        "Frozen baseline artifact (docs/reports/data/2026-08-05-soe-t36-baseline.json) " +
                        "predates IObjectiveEvaluator (T3.2) and stores only aggregated per-schedule " +
                        "proxies (LoadBalanceVariance/StdDev, FragmentedTaskCount, " +
                        "SameSubjectAdjacentCount/SubjectSwitchCount) -- not the ScheduledItem list " +
                        "ObjectiveEvaluator.Evaluate requires, and not a Total score. Score(B) is " +
                        "genuinely not computable from the committed artifact without re-running the " +
                        "retired pre-T3.3 placement algorithm, which would violate PD-3/R8's frozen-" +
                        "baseline discipline. Consequently metric #8's quality half (G2-5's arm-2 rule, " +
                        "Score(S) >= Score(B) - eps) is UNEVALUATED, not passed, for every arm-2 item -- " +
                        $"{arm2} of {results.Count} corpus items ({100.0 * arm2 / results.Count:F0}%) -- " +
                        "not worked around by resurrecting the retired allocator. Score(S) IS reported " +
                        "per item below (directly computable). Separately: arm classification itself " +
                        "uses OverdueMinutes alone (see Methodology), which cannot detect 'same total " +
                        "overdue minutes spread across more distinct tasks' as a feasibility regression, " +
                        "because B's frozen record has no task-level violating-count to compare against " +
                        "(S_ProductionViolatingTaskCount has no B counterpart) -- an unavoidable artifact-" +
                        "granularity limit, not a defect in this test.",
                },
                Items = items,
            };

            WriteOrVerifyArtifact(artifact);
        }

        private void WriteOrVerifyArtifact(SoeT34CorpusReportArtifact artifact)
        {
            string repoRoot = RepoLocator.FindRepoRoot();
            string outPath = Path.Combine(repoRoot, "docs", "reports", "data", "2026-08-07-soe-t34-corpus-report.json");
            string freshJson = JsonSerializer.Serialize(artifact, JsonOptions);

            if (!File.Exists(outPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllText(outPath, freshJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                _output.WriteLine($"[bootstrap] wrote new artifact at '{outPath}'.");
                return;
            }

            var committedNode = JsonNode.Parse(File.ReadAllText(outPath))!;
            var freshNode = JsonNode.Parse(freshJson)!;

            var committedCanon = committedNode.AsObject();
            var freshCanon = freshNode.AsObject();
            foreach (var field in ExcludedTopLevelFields)
            {
                committedCanon.Remove(field);
                freshCanon.Remove(field);
            }

            bool structurallyEqual = JsonNode.DeepEquals(committedCanon, freshCanon);
            Assert.True(structurallyEqual,
                $"Số đo report mới tính KHÁC với artifact đã commit tại '{outPath}' (đã loại trừ HeadSha). " +
                "Report này deterministic (corpus + Optimize() đều deterministic, đã pin ở " +
                "SoeT34InvariantTests.Optimize_CorpusRun_IsDeterministic_AcrossThreeRepeatedRuns) -- một " +
                "lệch ở đây là dấu hiệu thật, không phải nhiễu, xem lại diff trước khi tự tay cập nhật file.");
        }
    }
}
