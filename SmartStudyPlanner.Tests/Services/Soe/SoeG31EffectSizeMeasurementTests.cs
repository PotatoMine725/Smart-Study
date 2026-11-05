using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Soe;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// G3-1 Phase 0 (roadmap §A.4 G3-1, doc-sync report 2026-08-20 §2 follow-up M-1): the effect size of
    /// <c>Optimize(NewAllocator(I))</c> against <c>NewAllocator(I)</c> itself, the measurement the
    /// roadmap says "does not exist as a durable artifact". The T3.4 corpus report compares S with the
    /// frozen pre-T3.3 baseline B; it never reports S against S0, which is what wiring buys a user.
    /// <para>
    /// <b>MEASUREMENT ONLY</b>, same convention as <c>LocalSaveLatencyMeasurementTests</c>: it prints
    /// numbers and asserts nothing about them. No threshold, no "worth wiring" verdict. The single
    /// exception is <see cref="Instrument_DetectsKnownChange_AndKnownNoChange"/>, which asserts that
    /// the change-detector used below can go red (a null result from an instrument that cannot see a
    /// change is not evidence). No production code is touched; the allocator is reached through the
    /// existing <c>InternalsVisibleTo</c> (<c>GenerateScheduleWithIdentity</c>).
    /// </para>
    /// <para>
    /// Run: <c>dotnet test --filter "FullyQualifiedName~SoeG31EffectSize" --logger "console;verbosity=detailed"</c>.
    /// Output quoted in <c>docs/reports/2026-11-05-g3-1-soe-wiring-recon.md</c>.
    /// </para>
    /// </summary>
    [Trait("Kind", "Measurement")]
    public class SoeG31EffectSizeMeasurementTests
    {
        /// <summary>Seed for the generated family. Deliberately not the corpus seed (12345).</summary>
        private const int FamilySeed = 20261105;
        private static readonly DateTime FamilyToday = new DateTime(2026, 11, 2);

        private static readonly int[] FamilyTaskCounts = { 5, 15, 40, 80, 160 };
        private static readonly string[] FamilyDensities = { "tight", "medium", "loose" };
        private static readonly double[] FamilyCapacities = { 1.0, 3.0, 5.0, 8.0 };
        private const int FamilyReplicates = 2;

        private readonly ITestOutputHelper _out;

        public SoeG31EffectSizeMeasurementTests(ITestOutputHelper output) => _out = output;

        // =====================================================================================
        // Instrument check -- the only asserting test in this class.
        // =====================================================================================

        [Fact]
        public void Instrument_DetectsKnownChange_AndKnownNoChange()
        {
            var day0 = new DateTime(2026, 8, 3);
            var far = day0.AddDays(60);
            var t1 = Guid.NewGuid();

            // Same inputs as ScheduleOptimizerTests' two real-stage fixtures.
            var single = new List<ScheduledItem>
            {
                new(t1, far, "A", "A", "Toán", day0, 60),
                new(t1, far, "A", "A (Phần 2)", "Toán", day0, 40),
            };
            var imbalanced = new List<ScheduledItem>
            {
                new(t1, far, "A", "A", "Toán", day0, 150),
                new(t1, far, "B", "B", "Toán", day0, 50),
                new(t1, far, "C", "C", "Toán", day0.AddDays(1), 20),
            };

            var none = MeasureOne("ctl-single", "control", single, capMinutes: 240, today: day0);
            var some = MeasureOne("ctl-imbalanced", "control", imbalanced, capMinutes: 240, today: day0);

            Assert.False(none.Changed);
            Assert.Equal(0, none.ChunksMoved);
            Assert.False(none.Day0Changed);

            Assert.True(some.Changed);
            Assert.True(some.ChunksMoved > 0);
            Assert.True(some.ScoreOut > some.ScoreIn);
            Assert.True(some.SpreadOut < some.SpreadIn);
            Assert.True(some.MovesLater > 0);   // day0 -> day1 is a later move
            Assert.True(some.Day0Changed);      // the source day is day 0
            Assert.Equal(70, some.Day0MinutesOut);
        }

        // =====================================================================================
        // 3a -- effect size on the existing 230-item T3.6 corpus.
        // =====================================================================================

        [Fact]
        public void EffectSize_T36Corpus_OptimizeVersusAllocatorOutput()
        {
            var scenarios = SoeCorpusGenerator.Generate().ToDictionary(s => s.Id);
            var rows = new List<Measurement>();

            foreach (var r in SoeOptimizeCorpusMetrics.Results)
            {
                var sc = scenarios[r.Id];
                rows.Add(Measure(r.Id, r.Category, r.AllocatorItems, r.OptimizedItems, r.Report,
                    CapacityMath.ClampCapacityMinutes(sc.CapacityHours), sc.Today, runtimeMs: double.NaN));
            }

            _out.WriteLine("=== G3-1 effect size -- T3.6 corpus (SoeCorpusGenerator seed 12345), S0 = allocator, S = Optimize(S0) ===");
            WriteProbeLines(rows);
            WriteAggregate(rows);

            _out.WriteLine("");
            _out.WriteLine("Per category:");
            foreach (var g in rows.GroupBy(x => x.Family).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var list = g.ToList();
                _out.WriteLine(F($"  {g.Key,-14} n={list.Count,3} changed={list.Count(x => x.Changed),3} ") +
                                 F($"dScoreSum={list.Sum(x => x.ScoreOut - x.ScoreIn):+0.0000;-0.0000} ") +
                                 F($"day0Changed={list.Count(x => x.Day0Changed),3}"));
            }

            _out.WriteLine("");
            _out.WriteLine("Changed items only:");
            WriteTable(rows.Where(x => x.Changed));
        }

        // =====================================================================================
        // 3a -- effect size on a generated family (task count x deadline density x capacity).
        // =====================================================================================

        [Fact]
        public void EffectSize_GeneratedFamily_OptimizeVersusAllocatorOutput()
        {
            var rows = new List<Measurement>();
            var optimizer = NewOptimizer();

            foreach (var sc in GenerateFamily())
            {
                var (_, _, items) = SoeScheduleMetrics.RunScenarioWithIdentity(sc);
                var sw = Stopwatch.StartNew();
                var (opt, report) = optimizer.Optimize(items);
                sw.Stop();
                rows.Add(Measure(sc.Id, sc.Category, items, opt, report,
                    CapacityMath.ClampCapacityMinutes(sc.CapacityHours), sc.Today, sw.Elapsed.TotalMilliseconds));
            }

            _out.WriteLine(F($"=== G3-1 effect size -- generated family (seed {FamilySeed}, today {FamilyToday:yyyy-MM-dd}) ==="));
            _out.WriteLine(F($"tasks {{{string.Join(",", FamilyTaskCounts)}}} x density {{{string.Join(",", FamilyDensities)}}} ") +
                             F($"x capacity h {{{string.Join(",", FamilyCapacities)}}} x {FamilyReplicates} replicates"));
            WriteProbeLines(rows);
            WriteAggregate(rows);

            _out.WriteLine("");
            _out.WriteLine("By task count:");
            foreach (var g in rows.GroupBy(x => x.Tasks).OrderBy(g => g.Key))
                WriteSlice($"  tasks={g.Key,-4}", g.ToList());
            _out.WriteLine("By density:");
            foreach (var g in rows.GroupBy(x => x.Family.Split('/')[0]).OrderBy(g => g.Key, StringComparer.Ordinal))
                WriteSlice($"  {g.Key,-10}", g.ToList());
            _out.WriteLine("By capacity:");
            foreach (var g in rows.GroupBy(x => x.CapMinutes).OrderBy(g => g.Key))
                WriteSlice($"  cap={g.Key / 60.0,-4:0.#}h", g.ToList());

            _out.WriteLine("");
            _out.WriteLine("All scenarios:");
            WriteTable(rows);
        }

        // =====================================================================================
        // 3d / 3f -- latency on the UI-thread path and telemetry volume per call.
        // =====================================================================================

        [Fact]
        public void Latency_AllocatorPlusOptimize_AndRunLogPersist()
        {
            const int warmup = 3;
            const int samples = 15;
            var optimizer = NewOptimizer();
            var family = GenerateFamily().ToList();

            _out.WriteLine("=== G3-1 latency -- per call, ms (Stopwatch, Debug build, this machine) ===");
            _out.WriteLine("allocator = SoeScheduleMetrics.RunScenarioWithIdentity (includes building the HocKy fixture: an upper bound on GenerateScheduleWithIdentity)");
            _out.WriteLine("");
            _out.WriteLine("tasks  chunks(max)  alloc p50/p95/max      optimize p50/p95/max   alloc+opt p95");

            foreach (var g in family.GroupBy(s => s.Tasks.Count).OrderBy(g => g.Key))
            {
                var alloc = new List<double>();
                var opt = new List<double>();
                var both = new List<double>();
                int maxChunks = 0;

                foreach (var sc in g)
                {
                    for (int i = 0; i < warmup + samples; i++)
                    {
                        var sw = Stopwatch.StartNew();
                        var (_, _, items) = SoeScheduleMetrics.RunScenarioWithIdentity(sc);
                        double a = sw.Elapsed.TotalMilliseconds;
                        sw.Restart();
                        optimizer.Optimize(items);
                        double o = sw.Elapsed.TotalMilliseconds;
                        if (i < warmup) continue;
                        alloc.Add(a); opt.Add(o); both.Add(a + o);
                        maxChunks = Math.Max(maxChunks, items.Count);
                    }
                }

                _out.WriteLine(F($"{g.Key,5}  {maxChunks,11}  {P(alloc, 50),6:0.000}/{P(alloc, 95),6:0.000}/{alloc.Max(),7:0.000}  ") +
                                 F($"{P(opt, 50),6:0.000}/{P(opt, 95),6:0.000}/{opt.Max(),7:0.000}  {P(both, 95),8:0.000}"));
            }

            // Telemetry: rows per Optimize call, and what one Persist costs on a file-backed SQLite DB
            // (WAL, EnsureCreated -- what production runs on).
            var reports = family.Select(sc => optimizer.Optimize(SoeScheduleMetrics.RunScenarioWithIdentity(sc).Items).Report).ToList();
            var rowsPerCall = reports.Select(r => r.Passes.Sum(p => p.Checkpoints.Count)).ToList();

            using var fx = new FileBackedSqliteFixture();
            var writer = new OptimizerRunLogWriter(() => fx.NewContext(timeoutSeconds: 5));
            for (int i = 0; i < warmup; i++) writer.Persist(Guid.NewGuid(), reports[i]);

            var persistMs = new List<double>();
            foreach (var report in reports)
            {
                var sw = Stopwatch.StartNew();
                writer.Persist(Guid.NewGuid(), report);
                persistMs.Add(sw.Elapsed.TotalMilliseconds);
            }

            _out.WriteLine("");
            _out.WriteLine(F($"=== G3-1 telemetry -- OptimizerRunLogs rows per Optimize call (n={reports.Count} generated scenarios) ==="));
            foreach (var g in rowsPerCall.GroupBy(x => x).OrderBy(g => g.Key))
                _out.WriteLine(F($"  {g.Key} rows: {g.Count()} calls"));
            _out.WriteLine(F($"  Persist on file-backed SQLite: p50={P(persistMs, 50):0.000} p95={P(persistMs, 95):0.000} max={persistMs.Max():0.000} ms"));
        }

        // =====================================================================================
        // Measurement core.
        // =====================================================================================

        private sealed record Measurement(
            string Id, string Family, int Tasks, int CapMinutes, int ChunksIn, int DaysUsed,
            int ViolTasksIn, int ViolTasksOut, int OverdueIn, int OverdueOut,
            double ScoreIn, double ScoreOut, ObjectiveScore TermsIn, ObjectiveScore TermsOut,
            int Passes, OptimizerTermination Termination, string KStars,
            bool Changed, int ChunksMoved, int MinutesMoved, int MovesLater, int MovesEarlier,
            int SpreadIn, int SpreadOut, bool Day0Changed, int Day0MinutesIn, int Day0MinutesOut,
            int NewSameTaskSameDay, int NewPartOrderBreaks, int NewlyPastDeadline,
            int MaxDayOut, bool OverCapacity, int DaysEmptied, double RuntimeMs,
            int ViolChunksIn, int ViolChunksOut, IReadOnlyList<OptimizerCheckpointReason> Reasons);

        private static ScheduleOptimizer NewOptimizer() => new(
            new IOptimizerStage[] { new LoadRebalanceStage() },
            new ConstraintValidator(),
            new ObjectiveEvaluator(),
            SoeOptimizeCorpusMetrics.DefaultWeights);

        private static Measurement MeasureOne(string id, string family, IReadOnlyList<ScheduledItem> s0, int capMinutes, DateTime today)
        {
            var (s, report) = NewOptimizer().Optimize(s0);
            return Measure(id, family, s0, s, report, capMinutes, today, double.NaN);
        }

        private static Measurement Measure(
            string id, string family, IReadOnlyList<ScheduledItem> s0, IReadOnlyList<ScheduledItem> s,
            OptimizeReport report, int capMinutes, DateTime today, double runtimeMs)
        {
            if (s0.Count != s.Count)
                throw new InvalidOperationException($"{id}: Optimize changed the item count ({s0.Count} -> {s.Count}).");

            var validator = new ConstraintValidator();
            var evaluator = new ObjectiveEvaluator();
            var weights = SoeOptimizeCorpusMetrics.DefaultWeights;
            var vIn = validator.Validate(s0);
            var vOut = validator.Validate(s);
            var tIn = evaluator.Evaluate(s0, weights);
            var tOut = evaluator.Evaluate(s, weights);

            int moved = 0, minutesMoved = 0, later = 0, earlier = 0, newlyPast = 0;
            for (int i = 0; i < s0.Count; i++)
            {
                // The stage only rewrites Date (LoadRebalanceStage.cs:95), so index-wise comparison is exact.
                if (s0[i] with { Date = s[i].Date } != s[i])
                    throw new InvalidOperationException($"{id}: item {i} changed in a field other than Date.");
                if (s0[i].Date == s[i].Date) continue;
                moved++;
                minutesMoved += s0[i].SoPhut;
                if (s[i].Date > s0[i].Date) later++; else earlier++;
                if (s[i].Date.Date > s[i].HanChot.Date && !(s0[i].Date.Date > s0[i].HanChot.Date)) newlyPast++;
            }

            var daysIn = DayTotals(s0);
            var daysOut = DayTotals(s);
            int day0In = daysIn.GetValueOrDefault(today.Date);
            int day0Out = daysOut.GetValueOrDefault(today.Date);
            bool day0Changed = Enumerable.Range(0, s0.Count)
                .Any(i => (s0[i].Date == today.Date) != (s[i].Date == today.Date));

            return new Measurement(
                id, family, s0.Select(x => x.MaTask).Distinct().Count(), capMinutes, s0.Count, daysIn.Count,
                vIn.ViolatingTaskCount, vOut.ViolatingTaskCount, vIn.TotalOverdueMinutes, vOut.TotalOverdueMinutes,
                tIn.Total, tOut.Total, tIn, tOut,
                report.PassCount, report.Termination, string.Join("", report.Passes.Select(p => p.KStar)),
                moved > 0, moved, minutesMoved, later, earlier,
                Spread(daysIn), Spread(daysOut), day0Changed, day0In, day0Out,
                SameTaskSameDay(s) - SameTaskSameDay(s0), PartOrderBreaks(s0, s) - PartOrderBreaks(s0, s0), newlyPast,
                daysOut.Count == 0 ? 0 : daysOut.Values.Max(), daysOut.Values.Any(v => v > capMinutes),
                daysIn.Keys.Count(d => !daysOut.ContainsKey(d)), runtimeMs,
                vIn.Violations.Count, vOut.Violations.Count,
                report.Passes.SelectMany(p => p.Checkpoints).Where(c => c.Reason.HasValue).Select(c => c.Reason!.Value).ToList());
        }

        private static Dictionary<DateTime, int> DayTotals(IReadOnlyList<ScheduledItem> s)
            => s.GroupBy(i => i.Date.Date).ToDictionary(g => g.Key, g => g.Sum(i => i.SoPhut));

        private static int Spread(Dictionary<DateTime, int> days)
            => days.Count == 0 ? 0 : days.Values.Max() - days.Values.Min();

        /// <summary>(MaTask, Date) pairs holding more than one chunk -- after projection these show as
        /// two "(Phần n)" rows of the same task on the same day.</summary>
        private static int SameTaskSameDay(IReadOnlyList<ScheduledItem> s)
            => s.GroupBy(i => (i.MaTask, i.Date.Date)).Count(g => g.Count() > 1);

        /// <summary>Tasks whose chunks, taken in allocator emission order (= "(Phần n)" order), are no
        /// longer in non-decreasing date order in <paramref name="s"/>.</summary>
        private static int PartOrderBreaks(IReadOnlyList<ScheduledItem> s0, IReadOnlyList<ScheduledItem> s)
        {
            int breaks = 0;
            foreach (var g in Enumerable.Range(0, s0.Count).GroupBy(i => s0[i].MaTask))
            {
                var dates = g.Select(i => s[i].Date).ToList();
                for (int k = 1; k < dates.Count; k++)
                {
                    if (dates[k] < dates[k - 1]) { breaks++; break; }
                }
            }
            return breaks;
        }

        // =====================================================================================
        // Generated family.
        // =====================================================================================

        private static IEnumerable<SoeScenario> GenerateFamily()
        {
            var rng = new Random(FamilySeed);
            var types = (LoaiCongViec[])Enum.GetValues(typeof(LoaiCongViec));

            foreach (int taskCount in FamilyTaskCounts)
            foreach (string density in FamilyDensities)
            foreach (double cap in FamilyCapacities)
            for (int rep = 0; rep < FamilyReplicates; rep++)
            {
                string id = F($"G31-n{taskCount}-{density}-c{cap:0}-r{rep}");
                int subjects = taskCount <= 5 ? 2 : 8;
                var tasks = new List<SoeTaskDef>(taskCount);
                for (int j = 0; j < taskCount; j++)
                {
                    int dueOffset = density switch
                    {
                        "tight" => rng.Next(0, 4),
                        "medium" => rng.Next(0, 15),
                        _ => rng.Next(7, 46),
                    };
                    tasks.Add(new SoeTaskDef
                    {
                        // Names must be globally unique: RunScenarioWithIdentity derives MaTask from MD5(TenTask).
                        TenTask = F($"{id}-T{j:000}"),
                        MonHoc = F($"Mon{j % subjects}"),
                        LoaiTask = types[rng.Next(types.Length)],
                        DoKho = 1 + rng.Next(5),
                        HanChot = FamilyToday.AddDays(dueOffset),
                        MinutesNeeded = 15 + rng.Next(0, 226),
                        Priority = Math.Round(rng.NextDouble() * 100, 3),
                    });
                }

                yield return new SoeScenario
                {
                    Id = id,
                    Category = F($"{density}/c{cap:0}"),
                    DesignedFeasible = false, // not used by this harness
                    CapacityHours = cap,
                    Today = FamilyToday,
                    Tasks = tasks,
                };
            }
        }

        // =====================================================================================
        // Output.
        // =====================================================================================

        /// <summary>The exact line set the deleted 2026-08-20 probe printed (doc-sync report §2), so the
        /// two can be compared line for line.</summary>
        private void WriteProbeLines(List<Measurement> rows)
        {
            _out.WriteLine(F($"ITEMS                   : {rows.Count}"));
            _out.WriteLine(F($"SCHEDULE CHANGED BY OPT : {rows.Count(x => x.Changed)}"));
            _out.WriteLine(F($"VIOLATIONS  before/after: {rows.Sum(x => x.ViolTasksIn)} / {rows.Sum(x => x.ViolTasksOut)}   (ConstraintValidator.ViolatingTaskCount, production boundary)"));
            _out.WriteLine(F($"VIOL CHUNKS before/after: {rows.Sum(x => x.ViolChunksIn)} / {rows.Sum(x => x.ViolChunksOut)}   (ConstraintValidationResult.Violations.Count, chunk level)"));
            _out.WriteLine(F($"OVERDUE MIN before/after:{rows.Sum(x => x.OverdueIn)} / {rows.Sum(x => x.OverdueOut)}"));
            _out.WriteLine(F($"SCORE SUM   before/after: {rows.Sum(x => x.ScoreIn):0.0000} / {rows.Sum(x => x.ScoreOut):0.0000}"));
            _out.WriteLine(F($"SCORE improved/worse    : {rows.Count(x => x.ScoreOut > x.ScoreIn)} / {rows.Count(x => x.ScoreOut < x.ScoreIn)}"));
            _out.WriteLine("TERMINATION             : " + string.Join(" · ", rows.GroupBy(x => x.Termination).OrderBy(g => g.Key)
                .Select(g => F($"{g.Key}: {g.Count()}"))));
            _out.WriteLine("PASSES histogram        : " + string.Join(", ", rows.GroupBy(x => x.Passes).OrderBy(g => g.Key)
                .Select(g => F($"{g.Key}p={g.Count()}"))));
        }

        private void WriteAggregate(List<Measurement> rows)
        {
            var ch = rows.Where(x => x.Changed).ToList();
            double sumIn = rows.Sum(x => x.ScoreIn);
            _out.WriteLine("");
            _out.WriteLine(F($"Changed scenarios        : {ch.Count}/{rows.Count} ({100.0 * ch.Count / rows.Count:0.0}%)"));
            _out.WriteLine(F($"Objective delta (all)    : {rows.Sum(x => x.ScoreOut - x.ScoreIn):+0.0000;-0.0000} on sum {sumIn:0.0000} ({100.0 * rows.Sum(x => x.ScoreOut - x.ScoreIn) / sumIn:+0.00;-0.00}%)"));
            if (ch.Count > 0)
            {
                var d = ch.Select(x => x.ScoreOut - x.ScoreIn).OrderBy(v => v).ToList();
                var rel = ch.Select(x => (x.ScoreOut - x.ScoreIn) / Math.Max(1e-9, Math.Abs(x.ScoreIn))).OrderBy(v => v).ToList();
                _out.WriteLine(F($"Delta on changed (abs)   : min {d.First():0.0000}  median {P(d, 50):0.0000}  max {d.Last():0.0000}"));
                _out.WriteLine(F($"Delta on changed (rel)   : min {100 * rel.First():0.00}%  median {100 * P(rel, 50):0.00}%  max {100 * rel.Last():0.00}%"));
                _out.WriteLine(F($"Term deltas on changed   : LB {ch.Sum(x => x.TermsOut.LoadBalance - x.TermsIn.LoadBalance):+0.0000;-0.0000}  ") +
                                 F($"CC {ch.Sum(x => x.TermsOut.ContextContinuity - x.TermsIn.ContextContinuity):+0.0000;-0.0000}  ") +
                                 F($"SQ {ch.Sum(x => x.TermsOut.SessionQuality - x.TermsIn.SessionQuality):+0.0000;-0.0000}  ") +
                                 F($"FP {ch.Sum(x => x.TermsOut.FatiguePenalty - x.TermsIn.FatiguePenalty):+0.0000;-0.0000}  ") +
                                 F($"FR {ch.Sum(x => x.TermsOut.FragmentationPenalty - x.TermsIn.FragmentationPenalty):+0.0000;-0.0000}  (sums)"));
            }
            _out.WriteLine(F($"Chunks moved / minutes   : {rows.Sum(x => x.ChunksMoved)} of {rows.Sum(x => x.ChunksIn)} chunks; {rows.Sum(x => x.MinutesMoved)} min"));
            _out.WriteLine(F($"Move direction           : later {rows.Sum(x => x.MovesLater)} / earlier {rows.Sum(x => x.MovesEarlier)}"));
            _out.WriteLine(F($"Max-min spread (sum)     : {rows.Sum(x => x.SpreadIn)} -> {rows.Sum(x => x.SpreadOut)} min"));
            _out.WriteLine(F($"Day-0 (today) changed    : {rows.Count(x => x.Day0Changed)} scenarios; today minutes {rows.Sum(x => x.Day0MinutesIn)} -> {rows.Sum(x => x.Day0MinutesOut)}"));
            _out.WriteLine(F($"Projection hazards (new) : same-task-same-day {rows.Sum(x => x.NewSameTaskSameDay)}; '(Phần n)' order breaks {rows.Sum(x => x.NewPartOrderBreaks)}"));
            _out.WriteLine(F($"Newly past deadline      : {rows.Sum(x => x.NewlyPastDeadline)} chunks"));
            _out.WriteLine(F($"Over capacity after Opt  : {rows.Count(x => x.OverCapacity)} scenarios; days emptied {rows.Sum(x => x.DaysEmptied)}"));
            _out.WriteLine(F($"Violations changed       : {rows.Count(x => x.ViolTasksIn != x.ViolTasksOut || x.OverdueIn != x.OverdueOut)} scenarios"));
            _out.WriteLine("Reason codes (C1, all passes): " + string.Join(" · ", rows.SelectMany(x => x.Reasons).GroupBy(r => r).OrderBy(g => g.Key)
                .Select(g => F($"{g.Key}: {g.Count()}"))));
            _out.WriteLine("Final-pass reason, unchanged scenarios: " + string.Join(" · ", rows.Where(x => !x.Changed).GroupBy(x => x.Reasons.Last()).OrderBy(g => g.Key)
                .Select(g => F($"{g.Key}: {g.Count()}"))));
        }

        private void WriteSlice(string label, List<Measurement> g)
        {
            double sumIn = g.Sum(x => x.ScoreIn);
            _out.WriteLine(F($"{label} n={g.Count,3} changed={g.Count(x => x.Changed),3} ({100.0 * g.Count(x => x.Changed) / g.Count,5:0.0}%) ") +
                             F($"dScore={100.0 * g.Sum(x => x.ScoreOut - x.ScoreIn) / sumIn,6:+0.00;-0.00}% ") +
                             F($"moved={g.Sum(x => x.ChunksMoved),4}/{g.Sum(x => x.ChunksIn),5} later/earlier={g.Sum(x => x.MovesLater)}/{g.Sum(x => x.MovesEarlier)} ") +
                             F($"day0Chg={g.Count(x => x.Day0Changed),3} viol={g.Sum(x => x.ViolTasksIn)}->{g.Sum(x => x.ViolTasksOut)}") +
                             F($" unchangedBy[infeasible/noop]={g.Count(x => !x.Changed && x.Reasons.Last() == OptimizerCheckpointReason.Rejected_Infeasible)}/{g.Count(x => !x.Changed && x.Reasons.Last() == OptimizerCheckpointReason.Rejected_NoImprovement)}"));
        }

        private void WriteTable(IEnumerable<Measurement> rows)
        {
            _out.WriteLine("id                         chunks days  viol in>out  overdue in>out  score in>out        passes kStars term  moved min  later/earl spread in>out day0 in>out  sameDay partBrk ms");
            foreach (var x in rows)
            {
                _out.WriteLine(F($"{x.Id,-26} {x.ChunksIn,6} {x.DaysUsed,4}  {x.ViolTasksIn,4}>{x.ViolTasksOut,-4}  {x.OverdueIn,6}>{x.OverdueOut,-6}  ") +
                                 F($"{x.ScoreIn,8:0.0000}>{x.ScoreOut,-8:0.0000}  {x.Passes,6} {x.KStars,-6} {(x.Termination == OptimizerTermination.Terminated_FixedPoint ? "FP" : "PL"),-4} ") +
                                 F($"{x.ChunksMoved,5} {x.MinutesMoved,4}  {x.MovesLater,4}/{x.MovesEarlier,-4}  {x.SpreadIn,5}>{x.SpreadOut,-5}  ") +
                                 F($"{x.Day0MinutesIn,4}>{x.Day0MinutesOut,-4}  {x.NewSameTaskSameDay,7} {x.NewPartOrderBreaks,7} {(double.IsNaN(x.RuntimeMs) ? "-" : x.RuntimeMs.ToString("0.000", CultureInfo.InvariantCulture))}"));
            }
        }

        private static double P(List<double> values, int pct)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return double.NaN;
            int idx = (int)Math.Ceiling(pct / 100.0 * sorted.Count) - 1;
            return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
        }

        private static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
    }
}
