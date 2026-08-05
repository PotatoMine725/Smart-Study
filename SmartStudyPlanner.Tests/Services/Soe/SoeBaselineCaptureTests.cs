using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.6a/b harness: sinh corpus (<see cref="SoeCorpusGenerator"/>), chạy allocator hiện tại
    /// (<see cref="SmartStudyPlanner.Services.WorkloadServiceImpl.GenerateSchedule"/>, KHÔNG sửa)
    /// qua từng scenario, đo baseline theo <see cref="SoeScheduleMetrics"/>, và verify/bootstrap
    /// artifact JSON committed dưới <c>docs/reports/data/</c>.
    ///
    /// Đây LÀ nguồn baseline dùng cho T3.4's A6 cross-check (execution plan §3.6, criterion 10) --
    /// các assertion dưới đây giữ corpus không thoái hoá về mức tầm thường (0 bất khả thi,
    /// 0 inversion) mà chính execution plan §1.6/§1.7 cảnh báo.
    ///
    /// QUAN TRỌNG (PD-3/R8): artifact này là baseline ĐÓNG BĂNG đo TRƯỚC KHI T3.3 sửa allocator.
    /// Một lần `dotnet test` bình thường (CI, một agent Card khác chạy full suite để kiểm tra hồi
    /// quy...) KHÔNG được phép âm thầm ghi đè file này -- nếu chạy sau khi T3.3 đã đổi allocator,
    /// việc ghi đè sẽ xoá mất chính bằng chứng T3.3 phải được đo đối chiếu. Vì vậy:
    ///   - Nếu artifact CHƯA tồn tại (bootstrap lần đầu), test ghi nó ra.
    ///   - Nếu artifact ĐÃ tồn tại, test chỉ SO SÁNH số đo mới tính với file đã commit (bỏ qua
    ///     khối CaptureProvenance vốn luôn đổi theo wall-clock) và FAIL to nếu lệch -- KHÔNG bao
    ///     giờ ghi đè.
    ///   - Để chủ ý tái tạo lại artifact (ví dụ: corpus/methodology đổi một cách có review, TRƯỚC
    ///     khi T3.3 chạm vào allocator), set biến môi trường SOE_BASELINE_REGENERATE=1 rồi chạy
    ///     lại đúng test này, sau đó tự xem lại diff trước khi commit.
    /// </summary>
    public class SoeBaselineCaptureTests
    {
        private const string RegenerateEnvVar = "SOE_BASELINE_REGENERATE";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            // CapacityHours giờ phủ luôn NaN/NegativeInfinity (capacity-edge category) -- mặc định
            // System.Text.Json ném NotSupportedException khi serialize các giá trị double này.
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        // Khớp đúng 3 field wall-clock trong CaptureProvenance (xem MethodologyNote.DeterminismNote)
        // -- đây là phần DUY NHẤT được phép khác nhau giữa artifact đã commit và số đo mới.
        private static readonly Regex VolatileFieldRegex = new(
            "\"(CapturedAtUtc|TotalRuntimeMs|AvgRuntimeMsPerSchedule)\": [^,\r\n]+",
            RegexOptions.Compiled);

        private static string CanonicalizeForComparison(string json)
            => VolatileFieldRegex.Replace(json, m => $"\"{m.Groups[1].Value}\": \"<volatile>\"");

        [Fact]
        public void CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing()
        {
            var scenarios = SoeCorpusGenerator.Generate();

            Assert.True(scenarios.Count >= 200, $"corpus có {scenarios.Count} schedule, cần >=200.");

            var allTasks = scenarios.SelectMany(s => s.Tasks).ToList();
            var byNameGlobal = new Dictionary<string, SoeTaskDef>();
            foreach (var t in allTasks)
            {
                Assert.True(byNameGlobal.TryAdd(t.TenTask, t), $"trùng tên task base trên toàn corpus: '{t.TenTask}'.");
            }

            int infeasibleCount = scenarios.Count(s => !s.DesignedFeasible);
            double infeasiblePct = 100.0 * infeasibleCount / scenarios.Count;
            Assert.True(infeasiblePct >= 25.0, $"chỉ {infeasiblePct:F1}% bất khả thi, cần >=25%.");

            string repoRoot = RepoLocator.FindRepoRoot();
            string headSha = RepoLocator.GetHeadSha(repoRoot);

            var records = new List<ScheduleRecord>(scenarios.Count);
            var sw = Stopwatch.StartNew();
            foreach (var scenario in scenarios)
            {
                // Mỗi lần gọi RunScenario, bên trong đi qua SoeScheduleMetrics.ResolveExactlyOne
                // cho từng item đã lên lịch -- đây là chỗ tự-kiểm PD-7 thật sự thực thi, không
                // phải một assertion tách rời chạy sau.
                records.Add(SoeScheduleMetrics.RunScenario(scenario));
            }
            sw.Stop();

            int totalSelfInv = records.Sum(r => r.SelfInversions);
            int totalPairInv = records.Sum(r => r.PairwiseInversions);
            int totalInversions = totalSelfInv + totalPairInv;
            int totalViolationChunks = records.Sum(r => r.DHViolationChunks);
            int totalOverdueMinutes = records.Sum(r => r.OverdueMinutes);
            var feasibleButImprovableIds = records.Where(r => r.FeasibleButImprovable).Select(r => r.Id).ToList();

            Assert.True(totalInversions > 0,
                "baseline inversion count = 0 -- corpus vẫn thoái hoá (execution plan §1.6/§1.7): " +
                "không có bằng chứng để T3.3 đo cải thiện.");
            Assert.True(feasibleButImprovableIds.Count > 0,
                "không có schedule nào feasible-but-improvable -- T3.2's objective delta sẽ không có gì để đo.");

            var categoryCounts = scenarios
                .GroupBy(s => s.Category)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new CategoryCount { Category = g.Key, Count = g.Count() })
                .ToList();

            var artifact = new BaselineArtifact
            {
                HeadSha = headSha,
                Seed = SoeCorpusGenerator.Seed,
                TodayReference = SoeCorpusGenerator.Today.ToString("yyyy-MM-dd"),
                Aggregate = new AggregateStats
                {
                    ScheduleCount = scenarios.Count,
                    InfeasibleDesignedCount = infeasibleCount,
                    InfeasibleDesignedPct = Math.Round(infeasiblePct, 2),
                    TotalSelfInversions = totalSelfInv,
                    TotalPairwiseInversions = totalPairInv,
                    TotalDeadlineInversions = totalInversions,
                    TotalDHViolationChunks = totalViolationChunks,
                    TotalOverdueMinutes = totalOverdueMinutes,
                    FeasibleButImprovableCount = feasibleButImprovableIds.Count,
                    FeasibleButImprovableExampleIds = feasibleButImprovableIds.Take(10).ToList(),
                    ScheduleCountByCategory = categoryCounts,
                },
                CaptureProvenance = new CaptureProvenance
                {
                    CapturedAtUtc = DateTime.UtcNow.ToString("o"),
                    TotalRuntimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 3),
                    AvgRuntimeMsPerSchedule = Math.Round(sw.Elapsed.TotalMilliseconds / scenarios.Count, 5),
                },
                Schedules = records,
            };

            string freshJson = JsonSerializer.Serialize(artifact, JsonOptions);
            string outPath = Path.Combine(repoRoot, "docs", "reports", "data", "2026-08-05-soe-t36-baseline.json");
            bool forceRegenerate = Environment.GetEnvironmentVariable(RegenerateEnvVar) == "1";

            if (!File.Exists(outPath) || forceRegenerate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllText(outPath, freshJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return; // bootstrap hoặc regenerate có chủ đích -- xem class doc-comment
            }

            // Đường đi mặc định của `dotnet test`: artifact ĐÃ tồn tại và KHÔNG có opt-in --
            // đây là baseline đóng băng (PD-3/R8), chỉ xác minh, không bao giờ ghi đè.
            string committedJson = File.ReadAllText(outPath);
            string committedCanon = CanonicalizeForComparison(committedJson);
            string freshCanon = CanonicalizeForComparison(freshJson);

            Assert.True(committedCanon == freshCanon,
                $"Số đo baseline mới tính KHÁC với artifact đã commit tại '{outPath}'. " +
                "Artifact này là baseline ĐÓNG BĂNG trước T3.3 (execution plan PD-3/R8) -- test " +
                "chỉ xác minh, không tự ghi đè. Nếu lệch này là một thay đổi corpus/methodology " +
                "có chủ đích và đã review (KHÔNG phải vì T3.3 đã sửa allocator -- nếu vậy thì " +
                "artifact này hết tác dụng, đừng tái tạo lại nó), set " +
                $"{RegenerateEnvVar}=1 rồi chạy lại đúng test này để tái tạo có chủ đích, sau đó " +
                "tự xem lại diff trước khi commit.");
        }

        // ---- PD-7 self-check: chứng minh check THẬT SỰ bắt được ambiguity, không chỉ luôn xanh ----

        [Fact]
        public void NameResolution_ThrowsLoudly_WhenTwoCorpusTasksShareStrippedName()
        {
            var today = SoeCorpusGenerator.Today;
            var dup = new List<SoeTaskDef>
            {
                new SoeTaskDef { TenTask = "Trùng tên", MonHoc = "Toán", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 2, HanChot = today.AddDays(5), MinutesNeeded = 60, Priority = 50 },
                new SoeTaskDef { TenTask = "Trùng tên", MonHoc = "Lý", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 2, HanChot = today.AddDays(5), MinutesNeeded = 60, Priority = 40 },
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SoeScheduleMetrics.ResolveExactlyOne("Trùng tên", dup, "TEST-DUP"));

            Assert.Contains("resolved to 2", ex.Message);
        }

        [Fact]
        public void NameResolution_ThrowsLoudly_WhenNoCorpusTaskMatches()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                SoeScheduleMetrics.ResolveExactlyOne("Không tồn tại", new List<SoeTaskDef>(), "TEST-MISSING"));

            Assert.Contains("resolved to 0", ex.Message);
        }

        [Fact]
        public void NameResolution_StripsPhanSuffix_BeforeResolving()
        {
            var today = SoeCorpusGenerator.Today;
            var one = new List<SoeTaskDef>
            {
                new SoeTaskDef { TenTask = "Việc dài", MonHoc = "Toán", LoaiTask = LoaiCongViec.BaiTapVeNha, DoKho = 2, HanChot = today.AddDays(5), MinutesNeeded = 180, Priority = 50 },
            };

            var resolved = SoeScheduleMetrics.ResolveExactlyOne("Việc dài (Phần 2)", one, "TEST-STRIP");

            Assert.Equal("Việc dài", resolved.TenTask);
        }

        [Fact]
        public void CorpusGenerator_IsDeterministic_AcrossRepeatedCalls()
        {
            // Sanity nhanh cho seed cố định TRƯỚC KHI chạy hết artifact 3 lần (bash, ngoài test
            // runner) -- nếu generator tự nó không deterministic thì không cần chạy artifact test.
            var first = SoeCorpusGenerator.Generate();
            var second = SoeCorpusGenerator.Generate();

            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].Id, second[i].Id);
                Assert.Equal(first[i].DesignedFeasible, second[i].DesignedFeasible);
                Assert.Equal(first[i].Tasks.Count, second[i].Tasks.Count);
                for (int k = 0; k < first[i].Tasks.Count; k++)
                {
                    Assert.Equal(first[i].Tasks[k].TenTask, second[i].Tasks[k].TenTask);
                    Assert.Equal(first[i].Tasks[k].HanChot, second[i].Tasks[k].HanChot);
                    Assert.Equal(first[i].Tasks[k].MinutesNeeded, second[i].Tasks[k].MinutesNeeded);
                }
            }
        }
    }
}
