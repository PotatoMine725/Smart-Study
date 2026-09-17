using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using SmartStudyPlanner.Models;
using Xunit;
using Xunit.Abstractions;

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
    ///     khối CaptureProvenance wall-clock VÀ top-level HeadSha -- cả hai đều là "sự thật tại
    ///     thời điểm capture", không phải bất biến phải luôn khớp HEAD hiện tại; xem
    ///     ExcludedFromFrozenComparison) và FAIL to nếu phần còn lại (230 schedule record, toàn
    ///     bộ metric, khối methodology) lệch -- KHÔNG bao giờ ghi đè.
    ///   - Để chủ ý tái tạo lại artifact (ví dụ: corpus/methodology đổi một cách có review, TRƯỚC
    ///     khi T3.3 chạm vào allocator), set biến môi trường SOE_BASELINE_REGENERATE=1 rồi chạy
    ///     lại đúng test này, sau đó tự xem lại diff trước khi commit.
    /// </summary>
    public class SoeBaselineCaptureTests
    {
        private const string RegenerateEnvVar = "SOE_BASELINE_REGENERATE";

        private readonly ITestOutputHelper _output;

        public SoeBaselineCaptureTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            // CapacityHours giờ phủ luôn NaN/NegativeInfinity (capacity-edge category) -- mặc định
            // System.Text.Json ném NotSupportedException khi serialize các giá trị double này.
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        // Top-level field KHÔNG được đưa vào so sánh chặt: HeadSha ghi lại "allocator tại commit
        // nào được đo lúc capture" -- một sự kiện lịch sử đóng băng, KHÔNG phải bất biến phải
        // luôn bằng HEAD hiện tại. Coi nó như CaptureProvenance: nếu bị đưa vào so sánh chặt, MỌI
        // commit sau lần capture (kể cả không liên quan) sẽ làm HEAD trôi khỏi giá trị đã đóng
        // băng và khiến test đỏ vĩnh viễn -- đúng lỗi đã xảy ra ở 8d938b9.
        private static readonly string[] ExcludedTopLevelFields = { "HeadSha" };

        // 3 field wall-clock trong CaptureProvenance (xem MethodologyNote.DeterminismNote) --
        // cùng lý do loại trừ như HeadSha ở trên.
        private static readonly string[] ExcludedCaptureProvenanceFields =
            { "CapturedAtUtc", "TotalRuntimeMs", "AvgRuntimeMsPerSchedule" };

        /// <summary>
        /// So sánh CẤU TRÚC (JsonNode, không phải text thô) sau khi loại bỏ các field được miễn
        /// trừ ở trên -- bền hơn so-sánh-text trước những thay đổi định dạng không cố ý, miễn là
        /// JsonPropertyOrder vẫn ghim thứ tự property lúc serialize (đã ghim, xem các DTO trong
        /// SoeBaselineMetrics.cs).
        /// </summary>
        private static JsonNode CanonicalizeForComparison(JsonNode root)
        {
            var obj = root.AsObject();
            foreach (var field in ExcludedTopLevelFields)
            {
                obj.Remove(field);
            }
            if (obj["CaptureProvenance"] is JsonObject provenance)
            {
                foreach (var field in ExcludedCaptureProvenanceFields)
                {
                    provenance.Remove(field);
                }
            }
            return obj;
        }

        // T3.3 (Epic 3, Card F, 2026-08-06 review round) đổi placement rule của
        // WorkloadServiceImpl (least-loaded -> earliest-feasible), nên code hiện tại KHÔNG thể
        // tái tạo baseline đóng băng TRƯỚC T3.3 mà test dưới đây so sánh vào -- đây là hệ quả dự
        // kiến, không phải bug (xem doc-comment class ở trên, đoạn "QUAN TRỌNG (PD-3/R8)").
        // Artifact (docs/reports/data/2026-08-05-soe-t36-baseline.json) CHỦ Ý KHÔNG được tái tạo
        // lại: nó vẫn là mốc "before" bất biến cho T3.4/Card G đo delta, không còn vai trò
        // regression-guard cho riêng phép so sánh này nữa. Skip có chủ đích thay vì để đỏ vĩnh
        // viễn -- một suite đỏ sẵn phá tín hiệu "suite xanh" mà các card sau (đặc biệt T3.4) cần
        // để chứng minh acceptance criteria trên một lần chạy sạch. Các bất biến về
        // corpus/methodology (không phụ thuộc allocator cụ thể nào) được tách ra
        // CorpusMethodology_KhongThoaiHoa_LuonChayDuLieuMoi ngay dưới đây để KHÔNG "chết theo"
        // cùng test bị Skip -- chúng vẫn chạy mỗi lần suite chạy.
        [Fact(Skip =
            "T3.3 (2026-08-06) đổi allocator (least-loaded -> earliest-feasible) nên không thể " +
            "tái tạo baseline đóng băng trước T3.3 -- hệ quả dự kiến, xem PD-3/R8 ở doc-comment " +
            "class. Artifact vẫn là mốc 'before' bất biến cho T3.4/Card G, KHÔNG được tái tạo. " +
            "Corpus/methodology sanity đã tách sang CorpusMethodology_KhongThoaiHoa_LuonChayDuLieuMoi.")]
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
            var committedNode = JsonNode.Parse(File.ReadAllText(outPath))!;
            var freshNode = JsonNode.Parse(freshJson)!;

            string committedHeadSha = committedNode["HeadSha"]?.GetValue<string>() ?? "?";
            if (!string.Equals(committedHeadSha, headSha, StringComparison.Ordinal))
            {
                // Thông tin, KHÔNG fail: HeadSha ghi lại commit lúc capture, không phải bất biến
                // phải bám theo HEAD hiện tại (xem ExcludedTopLevelFields).
                _output.WriteLine(
                    $"[info] artifact HeadSha='{committedHeadSha}' (đo tại thời điểm capture) khác " +
                    $"HEAD hiện tại='{headSha}'. Đây là điều BÌNH THƯỜNG khi có commit mới sau lần " +
                    "capture -- không phải dấu hiệu allocator/corpus lệch.");
            }

            var committedCanon = CanonicalizeForComparison(committedNode);
            var freshCanon = CanonicalizeForComparison(freshNode);

            bool structurallyEqual = JsonNode.DeepEquals(committedCanon, freshCanon);

            Assert.True(structurallyEqual,
                $"Số đo baseline mới tính KHÁC với artifact đã commit tại '{outPath}' (đã loại trừ " +
                "HeadSha + CaptureProvenance wall-clock, xem ExcludedTopLevelFields/" +
                "ExcludedCaptureProvenanceFields). Artifact này là baseline ĐÓNG BĂNG trước T3.3 " +
                "(execution plan PD-3/R8) -- test chỉ xác minh, không tự ghi đè. Nếu lệch này là " +
                "một thay đổi corpus/methodology có chủ đích và đã review (KHÔNG phải vì T3.3 đã " +
                "sửa allocator -- nếu vậy thì artifact này hết tác dụng, đừng tái tạo lại nó), set " +
                $"{RegenerateEnvVar}=1 rồi chạy lại đúng test này để tái tạo có chủ đích, sau đó " +
                "tự xem lại diff trước khi commit.");
        }

        /// <summary>
        /// T3.3 (Epic 3, Card F, 2026-08-06 review round 3): tách khỏi
        /// <see cref="CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing"/> (giờ bị
        /// <c>Skip</c>) vì <c>Skip</c> vô tình bỏ luôn phần "artifact TỒN TẠI và parse được" mà
        /// chính tên method đó hứa ("...OrBootstrapsIfMissing") -- nếu file bị xoá hay hỏng sau
        /// khi test kia bị Skip, cả suite vẫn xanh và Card G (T3.4) sẽ âm thầm mất mốc "before"
        /// mà không ai flag. Test này CHỦ Ý chỉ kiểm tra CẤU TRÚC TĨNH của artifact đã commit
        /// (tồn tại, JSON hợp lệ, đủ field top-level, đủ số schedule record) -- KHÔNG so sánh với
        /// số đo từ allocator hiện tại (đó chính xác là phần đã đóng băng/off-limits, thuộc về
        /// test bị Skip). Không đọc file nào khác ngoài artifact đã commit -- không chạy corpus,
        /// không gọi allocator.
        /// </summary>
        [Fact]
        public void BaselineArtifact_TonTaiVaCoCauTrucHopLe()
        {
            string repoRoot = RepoLocator.FindRepoRoot();
            string outPath = Path.Combine(repoRoot, "docs", "reports", "data", "2026-08-05-soe-t36-baseline.json");

            Assert.True(File.Exists(outPath),
                $"Baseline artifact không tồn tại tại '{outPath}' -- T3.4/Card G sẽ mất mốc " +
                "'before' để đo delta mà không có bất kỳ tín hiệu nào cảnh báo, vì test so sánh " +
                "(CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing) đang bị Skip.");

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(File.ReadAllText(outPath));
            }
            catch (JsonException ex)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Baseline artifact tại '{outPath}' không parse được thành JSON hợp lệ: {ex.Message}");
            }

            Assert.NotNull(node);
            var obj = node!.AsObject();

            // Shape top-level tối thiểu (khớp BaselineArtifact trong SoeBaselineMetrics.cs) --
            // không kiểm tra GIÁ TRỊ (đó là việc bị đóng băng), chỉ kiểm tra SHAPE còn nguyên.
            foreach (var field in new[] { "HeadSha", "Seed", "TodayReference", "Aggregate", "Schedules" })
            {
                Assert.True(obj.ContainsKey(field), $"Baseline artifact thiếu field top-level '{field}'.");
            }

            var schedules = obj["Schedules"]!.AsArray();
            Assert.True(schedules.Count >= 200,
                $"Baseline artifact chỉ có {schedules.Count} schedule record, cần >=200 -- artifact " +
                "có vẻ đã bị cắt/hỏng, không phải file capture đầy đủ.");

            var aggregate = obj["Aggregate"]!.AsObject();
            Assert.True(aggregate.ContainsKey("ScheduleCount"), "Aggregate thiếu field 'ScheduleCount'.");
            Assert.Equal(schedules.Count, aggregate["ScheduleCount"]!.GetValue<int>());
        }

        /// <summary>
        /// T3.3 (Epic 3, Card F, 2026-08-06 review round): tách khỏi
        /// <see cref="CaptureBaseline_VerifiesFrozenArtifact_OrBootstrapsIfMissing"/> (giờ bị
        /// <c>Skip</c> vì so sánh với baseline ĐÓNG BĂNG trước T3.3, xem lý do ở đó). Các bất
        /// biến ở đây KHÔNG phụ thuộc allocator cụ thể nào (chỉ về corpus generator + methodology
        /// đo lường), nên phải tiếp tục chạy mỗi lần suite chạy chứ không được "chết theo" cùng
        /// test bị Skip -- nếu không, một corpus generator hỏng (thoái hoá về tầm thường, xem
        /// execution plan §1.6/§1.7) sẽ không còn ai bắt được.
        /// </summary>
        [Fact]
        public void CorpusMethodology_KhongThoaiHoa_LuonChayDuLieuMoi()
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

            var records = new List<ScheduleRecord>(scenarios.Count);
            foreach (var scenario in scenarios)
            {
                // Cùng lý do như bản gốc: đi qua SoeScheduleMetrics.ResolveExactlyOne cho từng
                // item -- chỗ tự-kiểm PD-7 thật sự thực thi.
                records.Add(SoeScheduleMetrics.RunScenario(scenario));
            }

            int totalInversions = records.Sum(r => r.SelfInversions) + records.Sum(r => r.PairwiseInversions);
            int feasibleButImprovableCount = records.Count(r => r.FeasibleButImprovable);

            Assert.True(totalInversions > 0,
                "baseline inversion count = 0 -- corpus vẫn thoái hoá (execution plan §1.6/§1.7): " +
                "không có bằng chứng để đo cải thiện.");
            Assert.True(feasibleButImprovableCount > 0,
                "không có schedule nào feasible-but-improvable -- objective delta sẽ không có gì để đo.");
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
