using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Soe;
using SmartStudyPlanner.Services.Strategies;
using SmartStudyPlanner.Tests.TestDoubles;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// Metric của MỘT schedule (một scenario chạy qua GenerateSchedule đúng một lần). Property
    /// order được ghim rõ bằng JsonPropertyOrder để output JSON deterministic không phụ thuộc
    /// vào thứ tự reflection trả về property -- một phần của yêu cầu "byte-identical qua 3 lần
    /// chạy".
    /// </summary>
    public sealed class ScheduleRecord
    {
        [JsonPropertyOrder(0)] public string Id { get; set; } = string.Empty;
        [JsonPropertyOrder(1)] public string Category { get; set; } = string.Empty;
        [JsonPropertyOrder(2)] public bool DesignedFeasible { get; set; }
        [JsonPropertyOrder(3)] public double CapacityHours { get; set; }
        [JsonPropertyOrder(4)] public int TaskCount { get; set; }
        [JsonPropertyOrder(5)] public int DaysProduced { get; set; }
        [JsonPropertyOrder(6)] public int TotalMinutesScheduled { get; set; }

        // D-H: violations(output) đo trên chính input này -- xem MethodologyNote.
        [JsonPropertyOrder(7)] public int DHViolationChunks { get; set; }
        [JsonPropertyOrder(8)] public int OverdueMinutes { get; set; }

        // Deadline-inversion, hai định nghĩa con -- xem MethodologyNote.
        [JsonPropertyOrder(9)] public int SelfInversions { get; set; }
        [JsonPropertyOrder(10)] public int PairwiseInversions { get; set; }

        // Objective inputs thô cho T3.2 tương lai -- KHÔNG phải điểm tổng hợp.
        [JsonPropertyOrder(11)] public double LoadBalanceVariance { get; set; }
        [JsonPropertyOrder(12)] public double LoadBalanceStdDev { get; set; }
        [JsonPropertyOrder(13)] public int DaysTouched { get; set; }
        [JsonPropertyOrder(14)] public int FragmentedTaskCount { get; set; }
        [JsonPropertyOrder(15)] public int TotalFragmentChunks { get; set; }
        [JsonPropertyOrder(16)] public int SameSubjectAdjacentCount { get; set; }
        [JsonPropertyOrder(17)] public int SubjectSwitchCount { get; set; }

        [JsonPropertyOrder(18)] public bool FeasibleButImprovable { get; set; }
    }

    public sealed class MethodologyNote
    {
        [JsonPropertyOrder(0)]
        public string ASixCrutchCaveat { get; set; } =
            "Deadlines/base names in this artifact come from a corpus-owned name->deadline side " +
            "table (execution plan A6/PD-7), NOT from a production IConstraintValidator -- T3.1 does " +
            "not exist yet. This is a measurement crutch confined to this test harness. T3.4 must " +
            "re-measure this same corpus through the real validator once it exists; the two numbers " +
            "must agree (acceptance criterion 10).";

        [JsonPropertyOrder(1)]
        public string DeadlineDayDefinition { get; set; } =
            "deadlineDay(task) = floor((task.HanChot.Date - scenario.Today).TotalDays). Day 0 is " +
            "today; negative values mean the task's deadline is already in the past (overdue) at " +
            "scenario construction time.";

        [JsonPropertyOrder(2)]
        public string DesignedFeasibilityDefinition { get; set; } =
            "A scenario is labeled 'designed feasible' using the standard EDF cumulative-demand " +
            "test for a single preemptible resource: sort distinct deadlineDay values ascending; for " +
            "every prefix, the running sum of MinutesNeeded for tasks whose deadlineDay is in that " +
            "prefix must not exceed capacityMinutes(after clamp) * max(deadlineDay, 0). This is a " +
            "necessary-and-sufficient condition for this scheduling model (any task can be split " +
            "across any days). It is a DESIGN-TIME label for corpus construction, independent of " +
            "what the (deadline-blind) allocator actually does -- the whole point of the corpus is " +
            "that these two can and do diverge.";

        [JsonPropertyOrder(3)]
        public string DeadlineInversionSelfMiss { get; set; } =
            "Self-miss inversion: a scheduled chunk of task T is placed on a day with offset >= " +
            "deadlineDay(T) (i.e. on or after T's own deadline day), AND at least one day with " +
            "offset < deadlineDay(T) ends the run with TotalMinutes < capacityMinutes (i.e. some " +
            "earlier day had unused capacity that could have absorbed this chunk instead). Counted " +
            "once per offending chunk.";

        [JsonPropertyOrder(4)]
        public string DeadlineInversionPairwise { get; set; } =
            "Pairwise inversion: for two distinct scheduled tasks Ti, Tj in the same schedule with " +
            "deadlineDay(Ti) < deadlineDay(Tj) (Ti due strictly earlier), both 'individually " +
            "placeable' (MinutesNeeded(T) <= capacityMinutes * max(deadlineDay(T), 0), i.e. neither " +
            "is a priori infeasible alone -- this is a simplification that ignores competition for " +
            "capacity between the two tasks, documented here rather than hidden), and " +
            "firstDay(Tj) < firstDay(Ti) where firstDay is the earliest day offset any chunk of the " +
            "task was placed on. Counted once per offending unordered pair.";

        [JsonPropertyOrder(5)]
        public string DHViolationDefinition { get; set; } =
            "D-H violation (chunk-level): a scheduled chunk placed on a day with offset >= " +
            "deadlineDay(its task). OverdueMinutes is the sum of SoPhut over exactly those chunks. " +
            "This is 'violations(input)' for D-H's invariant (violations(output) <= violations(input)) " +
            "measured against the CURRENT allocator's own single run -- there is no prior 'input' " +
            "schedule here, the allocator's output is being characterized as a baseline for T3.3 to " +
            "improve on.";

        [JsonPropertyOrder(6)]
        public string ObjectiveInputsNote { get; set; } =
            "Raw measurements only, no synthesized score -- T3.2 (IObjectiveEvaluator, w1..w5) does " +
            "not exist yet. LoadBalanceVariance/StdDev = population variance/stddev of TotalMinutes " +
            "across all produced ScheduleDay entries. DaysTouched = count of days with >=1 task. " +
            "FragmentedTaskCount/TotalFragmentChunks = count of base tasks split into >1 '(Phần n)' " +
            "chunk, and the total number of chunks beyond the first per such task (FragmentationPenalty " +
            "proxy). SameSubjectAdjacentCount/SubjectSwitchCount = count of consecutive same-day " +
            "chunk pairs sharing vs. not sharing TenMon (ContextContinuity proxy).";

        [JsonPropertyOrder(7)]
        public string DeterminismNote { get; set; } =
            "Every field in this artifact is byte-identical across repeated runs under the fixed " +
            "seed (SoeCorpusGenerator.Seed), EXCEPT CaptureProvenance.CapturedAtUtc/TotalRuntimeMs/ " +
            "AvgRuntimeMsPerSchedule, which are wall-clock capture metadata and vary by construction. " +
            "That block is clearly separated so the deterministic core (methodology + aggregate + " +
            "per-schedule records) can be diffed independently.";

        [JsonPropertyOrder(8)]
        public string RegenerationProcedure { get; set; } =
            "This artifact is a FROZEN pre-T3.3 baseline (execution plan PD-3/R8): a baseline " +
            "captured after T3.3 reworks the allocator is worthless. Routine `dotnet test` runs " +
            "(SoeBaselineCaptureTests.CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing) " +
            "verify freshly-computed metrics against this committed file and fail loudly on drift; " +
            "they never overwrite it. To intentionally regenerate (e.g. after a reviewed corpus or " +
            "methodology change made BEFORE T3.3 lands), set the environment variable " +
            "SOE_BASELINE_REGENERATE=1 and re-run that test, then review the resulting diff before " +
            "committing. Do not regenerate after T3.3 has changed the allocator -- capture a new, " +
            "separately-named artifact instead if a post-rework comparison is ever needed.";
    }

    public sealed class CategoryCount
    {
        [JsonPropertyOrder(0)] public string Category { get; set; } = string.Empty;
        [JsonPropertyOrder(1)] public int Count { get; set; }
    }

    public sealed class AggregateStats
    {
        [JsonPropertyOrder(0)] public int ScheduleCount { get; set; }
        [JsonPropertyOrder(1)] public int InfeasibleDesignedCount { get; set; }
        [JsonPropertyOrder(2)] public double InfeasibleDesignedPct { get; set; }
        [JsonPropertyOrder(3)] public int TotalSelfInversions { get; set; }
        [JsonPropertyOrder(4)] public int TotalPairwiseInversions { get; set; }
        [JsonPropertyOrder(5)] public int TotalDeadlineInversions { get; set; }
        [JsonPropertyOrder(6)] public int TotalDHViolationChunks { get; set; }
        [JsonPropertyOrder(7)] public int TotalOverdueMinutes { get; set; }
        [JsonPropertyOrder(8)] public int FeasibleButImprovableCount { get; set; }
        [JsonPropertyOrder(9)] public List<string> FeasibleButImprovableExampleIds { get; set; } = new();
        [JsonPropertyOrder(10)] public List<CategoryCount> ScheduleCountByCategory { get; set; } = new();
    }

    public sealed class CaptureProvenance
    {
        [JsonPropertyOrder(0)]
        public string Note { get; set; } =
            "CapturedAtUtc and *RuntimeMs below vary run-to-run by nature of wall-clock measurement. " +
            "This is the ONLY part of the artifact excluded from the byte-identical-across-3-runs claim.";
        [JsonPropertyOrder(1)] public string CapturedAtUtc { get; set; } = string.Empty;
        [JsonPropertyOrder(2)] public double TotalRuntimeMs { get; set; }
        [JsonPropertyOrder(3)] public double AvgRuntimeMsPerSchedule { get; set; }
    }

    public sealed class BaselineArtifact
    {
        [JsonPropertyOrder(0)] public string ArtifactKind { get; set; } = "soe-baseline-capture";
        [JsonPropertyOrder(1)]
        public string GeneratedFor { get; set; } =
            "Epic 3 / T3.6a-T3.6b (Card A) -- current (unmodified) WorkloadServiceImpl.GenerateSchedule allocator baseline";
        [JsonPropertyOrder(2)] public string HeadSha { get; set; } = string.Empty;
        [JsonPropertyOrder(3)] public int Seed { get; set; }
        [JsonPropertyOrder(4)] public string TodayReference { get; set; } = string.Empty;
        [JsonPropertyOrder(5)] public MethodologyNote Methodology { get; set; } = new();
        [JsonPropertyOrder(6)] public AggregateStats Aggregate { get; set; } = new();
        [JsonPropertyOrder(7)] public CaptureProvenance CaptureProvenance { get; set; } = new();
        [JsonPropertyOrder(8)] public List<ScheduleRecord> Schedules { get; set; } = new();
    }

    /// <summary>Trả điểm ưu tiên/số phút theo bảng tra corpus, cùng idiom với
    /// WorkloadServiceScheduleTests.StubDecisionEngine.</summary>
    internal sealed class SoeStubDecisionEngine : IDecisionEngine
    {
        public Dictionary<string, double> Priorities { get; } = new();
        public Dictionary<string, int> Minutes { get; } = new();
        public WeightConfig Config { get; } = new WeightConfig();

        public double CalculatePriority(StudyTask task, MonHoc monHoc) => Priorities.GetValueOrDefault(task.TenTask, 0);
        public int CalculateRawSuggestedMinutes(StudyTask task) => Minutes.GetValueOrDefault(task.TenTask, 0);
        public string SuggestStudyTime(StudyTask task) => string.Empty;

        public StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc)
            => new StudyTimePredictionResult(CalculateRawSuggestedMinutes(task), false, 0f);

        public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
            => Task.FromResult<WeightConfigSuggestion?>(null);
    }

    /// <summary>Tìm repo root (thư mục chứa SmartStudyPlanner.slnx) từ bin output, và đọc HEAD SHA
    /// qua `git rev-parse HEAD` chạy trực tiếp -- không có precedent nào trong test project gọi
    /// git, nên viết mới, tối giản có chủ đích.</summary>
    internal static class RepoLocator
    {
        public static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartStudyPlanner.slnx")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new InvalidOperationException(
                    $"Không tìm được repo root (SmartStudyPlanner.slnx) đi lên từ '{AppContext.BaseDirectory}'.");
            }

            return dir.FullName;
        }

        public static string GetHeadSha(string repoRoot)
        {
            var psi = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Không khởi động được `git rev-parse HEAD`.");

            string stdout = proc.StandardOutput.ReadToEnd().Trim();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                throw new InvalidOperationException(
                    $"`git rev-parse HEAD` thất bại (exit {proc.ExitCode}): {stderr}");
            }

            return stdout;
        }
    }

    /// <summary>
    /// T3.6b — tính metric baseline cho MỘT scenario bằng cách chạy WorkloadServiceImpl.GenerateSchedule
    /// thật (không sửa production) rồi đo lại theo các định nghĩa trong MethodologyNote.
    /// </summary>
    public static class SoeScheduleMetrics
    {
        private static readonly Regex PhanSuffixRegex = new(@" \(Phần \d+\)$", RegexOptions.Compiled);

        public static string StripPhanSuffix(string tenTask)
        {
            var m = PhanSuffixRegex.Match(tenTask);
            return m.Success ? tenTask.Substring(0, m.Index) : tenTask;
        }

        /// <summary>
        /// Tự-kiểm PD-7 bắt buộc: given tên đã lên lịch (đã hoặc chưa strip "(Phần n)"), phải
        /// resolve về ĐÚNG MỘT task trong corpus. Ném lỗi ồn ào (không nuốt lặng lẽ) nếu 0 hoặc
        /// >1 khớp -- đây là cái A6 side table phải tự bảo vệ trước khi được tin dùng để đo
        /// deadline-inversion.
        /// </summary>
        public static SoeTaskDef ResolveExactlyOne(string scheduledTenTask, IReadOnlyList<SoeTaskDef> corpusTasks, string scenarioId)
        {
            string stripped = StripPhanSuffix(scheduledTenTask);
            var matches = corpusTasks.Where(t => t.TenTask == stripped).ToList();

            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"A6 self-check FAILED for scenario '{scenarioId}': scheduled item '{scheduledTenTask}' " +
                    $"(stripped -> '{stripped}') resolved to {matches.Count} corpus tasks; expected exactly 1.");
            }

            return matches[0];
        }

        public static ScheduleRecord RunScenario(SoeScenario scenario)
        {
            var engine = new SoeStubDecisionEngine();
            var hocKy = new HocKy($"HK-{scenario.Id}", scenario.Today);
            var monHocBySubject = new Dictionary<string, MonHoc>();

            foreach (var t in scenario.Tasks)
            {
                if (!monHocBySubject.TryGetValue(t.MonHoc, out var mon))
                {
                    mon = new MonHoc(t.MonHoc, 3) { MaHocKy = hocKy.MaHocKy };
                    monHocBySubject[t.MonHoc] = mon;
                    hocKy.DanhSachMonHoc.Add(mon);
                }

                var studyTask = new StudyTask(t.TenTask, t.HanChot, t.LoaiTask, t.DoKho);
                mon.DanhSachTask.Add(studyTask);
                engine.Priorities[t.TenTask] = t.Priority;
                engine.Minutes[t.TenTask] = t.MinutesNeeded;
            }

            IClock clock = new FakeClock(scenario.Today);
            var sut = new WorkloadServiceImpl(engine, clock);
            var days = sut.GenerateSchedule(hocKy, scenario.CapacityHours);

            return ComputeRecord(scenario, days);
        }

        /// <summary>
        /// T3.4 (Card G) — biến thể của <see cref="RunScenario"/> gọi
        /// <c>GenerateScheduleWithIdentity</c> (T3.8) ĐÚNG MỘT lần, trả về CẢ <c>Days</c>
        /// (cho các phép đo dựa trên A6 name-resolution hiện có) LẪN <c>Items</c> (cho
        /// <c>IConstraintValidator</c>/<c>IScheduleOptimizer</c> — chúng cần identity thật, không
        /// qua name-stripping). Gọi allocator MỘT lần duy nhất là chủ đích: nếu gọi hai lần riêng
        /// (một cho Days, một cho Items) sẽ không còn gì đảm bảo hai kết quả mô tả CÙNG một lịch —
        /// điều acceptance criterion 10 (A6 cross-check) cần giữ đúng.
        ///
        /// <b>MaTask được gán XÁC ĐỊNH (deterministic), KHÔNG dùng Guid.NewGuid() mặc định của
        /// <c>StudyTask</c>'s constructor:</b> <c>LoadRebalanceStage</c> hoà giải tie-break trên
        /// D_max bằng <c>.ThenBy(idx => schedule[idx].MaTask)</c> — nếu MaTask đổi ngẫu nhiên mỗi
        /// lần corpus chạy, tie-break có thể chọn item khác nhau giữa hai lần chạy CÙNG một input,
        /// phá byte-identical-across-3-runs (criterion 6). MaTask ở đây suy ra XÁC ĐỊNH từ
        /// <c>TenTask</c> (MD5 -&gt; Guid) — an toàn vì corpus generator tự đảm bảo tên task base
        /// DUY NHẤT trên toàn corpus (PD-7 self-check, <c>SoeCorpusGenerator.Generate</c>).
        /// </summary>
        public static (ScheduleRecord Record, List<ScheduleDay> Days, IReadOnlyList<ScheduledItem> Items)
            RunScenarioWithIdentity(SoeScenario scenario)
        {
            var engine = new SoeStubDecisionEngine();
            var hocKy = new HocKy($"HK-{scenario.Id}", scenario.Today);
            var monHocBySubject = new Dictionary<string, MonHoc>();

            foreach (var t in scenario.Tasks)
            {
                if (!monHocBySubject.TryGetValue(t.MonHoc, out var mon))
                {
                    mon = new MonHoc(t.MonHoc, 3) { MaHocKy = hocKy.MaHocKy };
                    monHocBySubject[t.MonHoc] = mon;
                    hocKy.DanhSachMonHoc.Add(mon);
                }

                var studyTask = new StudyTask(t.TenTask, t.HanChot, t.LoaiTask, t.DoKho)
                {
                    MaTask = DeterministicGuid(t.TenTask),
                };
                mon.DanhSachTask.Add(studyTask);
                engine.Priorities[t.TenTask] = t.Priority;
                engine.Minutes[t.TenTask] = t.MinutesNeeded;
            }

            IClock clock = new FakeClock(scenario.Today);
            var sut = new WorkloadServiceImpl(engine, clock);
            var (days, items) = sut.GenerateScheduleWithIdentity(hocKy, scenario.CapacityHours);

            var record = ComputeRecord(scenario, days);
            return (record, days, items);
        }

        /// <summary>Guid xác định (deterministic) suy từ MD5(tenTask) — chỉ dùng trong harness đo
        /// lường T3.4, KHÔNG phải một tiện ích production. An toàn không đụng độ trên corpus vì
        /// tên task base đã được đảm bảo DUY NHẤT (PD-7 self-check).</summary>
        private static Guid DeterministicGuid(string tenTask)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(tenTask));
            return new Guid(hash);
        }

        /// <summary>internal (thay vì private) từ T3.4 (Card G) — <c>SoeOptimizeCorpusMetrics</c>
        /// tái dùng đúng logic đo này trên schedule lấy từ <c>GenerateScheduleWithIdentity</c>, tránh
        /// nhân đôi định nghĩa DHViolationChunks/inversion đã pin ở đây.</summary>
        internal static ScheduleRecord ComputeRecord(SoeScenario scenario, List<ScheduleDay> days)
        {
            int capMin = CapacityMath.ClampCapacityMinutes(scenario.CapacityHours);
            var byName = scenario.Tasks.ToDictionary(t => t.TenTask);

            var dayOffsets = days
                .Select(d => (Offset: (d.Date - scenario.Today).Days, d.TotalMinutes))
                .ToList();

            int dHViolationChunks = 0;
            int overdueMinutes = 0;
            int selfInversions = 0;
            var firstDayByTask = new Dictionary<string, int>();

            foreach (var day in days)
            {
                int dayOffset = (day.Date - scenario.Today).Days;

                foreach (var st in day.Tasks)
                {
                    var resolved = ResolveExactlyOne(st.TenTask, scenario.Tasks, scenario.Id);
                    string baseName = resolved.TenTask;
                    int deadlineDay = (resolved.HanChot.Date - scenario.Today).Days;

                    if (!firstDayByTask.TryGetValue(baseName, out int cur) || dayOffset < cur)
                    {
                        firstDayByTask[baseName] = dayOffset;
                    }

                    if (dayOffset >= deadlineDay)
                    {
                        dHViolationChunks++;
                        overdueMinutes += st.SoPhut;

                        bool earlierSlack = dayOffsets.Any(d => d.Offset < deadlineDay && d.TotalMinutes < capMin);
                        if (earlierSlack) selfInversions++;
                    }
                }
            }

            int pairwiseInversions = ComputePairwiseInversions(firstDayByTask, byName, capMin, scenario.Today);

            double mean = days.Count > 0 ? days.Average(d => d.TotalMinutes) : 0;
            double variance = days.Count > 0 ? days.Average(d => Math.Pow(d.TotalMinutes - mean, 2)) : 0;
            double stddev = Math.Sqrt(variance);
            int daysTouched = days.Count(d => d.Tasks.Count > 0);

            var chunkGroups = days.SelectMany(d => d.Tasks)
                .GroupBy(t => StripPhanSuffix(t.TenTask))
                .ToList();
            int fragmentedTaskCount = chunkGroups.Count(g => g.Count() > 1);
            int totalFragmentChunks = chunkGroups.Where(g => g.Count() > 1).Sum(g => g.Count() - 1);

            int sameSubjectAdjacent = 0;
            int subjectSwitch = 0;
            foreach (var day in days)
            {
                for (int i = 1; i < day.Tasks.Count; i++)
                {
                    if (day.Tasks[i].TenMon == day.Tasks[i - 1].TenMon) sameSubjectAdjacent++;
                    else subjectSwitch++;
                }
            }

            bool feasibleButImprovable = overdueMinutes == 0 && (variance > 0 || fragmentedTaskCount > 0);

            return new ScheduleRecord
            {
                Id = scenario.Id,
                Category = scenario.Category,
                DesignedFeasible = scenario.DesignedFeasible,
                CapacityHours = scenario.CapacityHours,
                TaskCount = scenario.Tasks.Count,
                DaysProduced = days.Count,
                TotalMinutesScheduled = days.Sum(d => d.TotalMinutes),
                DHViolationChunks = dHViolationChunks,
                OverdueMinutes = overdueMinutes,
                SelfInversions = selfInversions,
                PairwiseInversions = pairwiseInversions,
                LoadBalanceVariance = Math.Round(variance, 4),
                LoadBalanceStdDev = Math.Round(stddev, 4),
                DaysTouched = daysTouched,
                FragmentedTaskCount = fragmentedTaskCount,
                TotalFragmentChunks = totalFragmentChunks,
                SameSubjectAdjacentCount = sameSubjectAdjacent,
                SubjectSwitchCount = subjectSwitch,
                FeasibleButImprovable = feasibleButImprovable,
            };
        }

        /// <summary>
        /// Đếm số cặp task (không thứ tự) vi phạm "pairwise inversion" -- xem
        /// MethodologyNote.DeadlineInversionPairwise để có định nghĩa đầy đủ. Mỗi cặp chỉ được
        /// xét đúng một lần (vòng lặp a &lt; b), tránh đếm trùng hai chiều.
        /// </summary>
        private static int ComputePairwiseInversions(
            Dictionary<string, int> firstDayByTask, Dictionary<string, SoeTaskDef> byName, int capMin, DateTime today)
        {
            var infos = firstDayByTask
                .Select(kv =>
                {
                    var def = byName[kv.Key];
                    int deadlineDay = (def.HanChot.Date - today.Date).Days;
                    bool placeable = def.MinutesNeeded <= (long)capMin * Math.Max(deadlineDay, 0);
                    return (Name: kv.Key, DeadlineDay: deadlineDay, FirstDay: kv.Value, Placeable: placeable);
                })
                .ToList();

            int pairwiseInversions = 0;
            for (int a = 0; a < infos.Count; a++)
            {
                for (int b = a + 1; b < infos.Count; b++)
                {
                    var x = infos[a];
                    var y = infos[b];
                    if (x.DeadlineDay == y.DeadlineDay) continue;

                    var earlier = x.DeadlineDay < y.DeadlineDay ? x : y;
                    var later = x.DeadlineDay < y.DeadlineDay ? y : x;

                    if (earlier.Placeable && later.Placeable && later.FirstDay < earlier.FirstDay)
                    {
                        pairwiseInversions++;
                    }
                }
            }

            return pairwiseInversions;
        }
    }
}
