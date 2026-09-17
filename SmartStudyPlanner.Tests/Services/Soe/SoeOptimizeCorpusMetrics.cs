using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Soe;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.4 (Card G) — kết quả đầy đủ của MỘT corpus item chạy qua allocator (T3.3) rồi qua
    /// <see cref="IScheduleOptimizer.Optimize"/> (T3.9), cộng các cặp feasibility đo dưới BA cách
    /// khác nhau để phục vụ acceptance criterion 10 (A6 cross-check, xem
    /// <see cref="SoeOptimizeCorpusMetrics"/>'s doc comment cho lý do cần cả ba):
    /// <list type="bullet">
    /// <item><b>Harness boundary (&gt;=)</b> — định nghĩa gốc của <c>SoeScheduleMetrics.ComputeRecord</c>
    /// (Card A, trước khi <c>IConstraintValidator</c> tồn tại): một chunk vi phạm nếu
    /// <c>dayOffset &gt;= deadlineDay</c>, tức <c>item.Date.Date &gt;= item.HanChot.Date</c> — ĐÚNG
    /// ngày hạn chót cũng tính là vi phạm.</item>
    /// <item><b>Production boundary (&gt;)</b> — định nghĩa CHÍNH THỨC của <see cref="UniformDeadlinePolicy"/>
    /// (T3.1, ratified): <c>item.Date.Date &gt; item.HanChot.Date</c> — ĐÚNG ngày hạn chót KHÔNG
    /// tính là vi phạm (xem <c>ConstraintValidatorTests</c>, case "đúng ngày hạn chót -&gt; OK").</item>
    /// </list>
    /// Hai định nghĩa này KHÁC NHAU tại đúng ranh giới "chunk nằm ĐÚNG ngày hạn chót" — một khác
    /// biệt thật, không phải lỗi harness, được phát hiện và ghi lại tường minh ở đây (T3.4 finding,
    /// xem class doc comment).
    /// </summary>
    public sealed class SoeOptimizeCorpusItemResult
    {
        public string Id { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public bool DesignedFeasible { get; init; }

        /// <summary>S0 = NewAllocator(I) -- schedule TRƯỚC khi qua Optimize().</summary>
        public IReadOnlyList<ScheduledItem> AllocatorItems { get; init; } = Array.Empty<ScheduledItem>();

        /// <summary>S = Optimize(NewAllocator(I)) -- schedule SAU khi qua Optimize().</summary>
        public IReadOnlyList<ScheduledItem> OptimizedItems { get; init; } = Array.Empty<ScheduledItem>();

        public OptimizeReport Report { get; init; } = null!;

        /// <summary>Kết quả production validator (task-level ViolatingTaskCount + TotalOverdueMinutes,
        /// boundary "&gt;") trên <see cref="AllocatorItems"/> -- đây là C0 của pass 1 mà Optimize() tự
        /// tính lại bên trong, đo LẠI ở đây độc lập để D-H test không phụ thuộc vào việc đọc report.</summary>
        public ConstraintValidationResult AllocatorValidation { get; init; } = null!;

        /// <summary>Kết quả production validator trên <see cref="OptimizedItems"/> (schedule đã commit).</summary>
        public ConstraintValidationResult OptimizedValidation { get; init; } = null!;

        /// <summary>ScheduleRecord đo theo harness gốc (Card A, boundary "&gt;=", qua ResolveExactlyOne)
        /// trên <see cref="AllocatorItems"/>'s Days chiếu -- nguồn của SelfInversions/PairwiseInversions
        /// dùng cho phần (b) inversion property.</summary>
        public ScheduleRecord HarnessRecord { get; init; } = null!;

        /// <summary>Cặp (chunk vi phạm, tổng phút trễ) boundary "&gt;=" trên chính <see cref="AllocatorItems"/>
        /// (không qua name-resolution -- ScheduledItem đã mang HanChot thật). Phải bằng
        /// <see cref="HarnessRecord"/>.DHViolationChunks/OverdueMinutes -- một self-check nội bộ.</summary>
        public (int Chunks, int Minutes) HarnessBoundaryPairAllocator { get; init; }

        /// <summary>Cặp (chunk vi phạm, tổng phút trễ) boundary "&gt;=" trên <see cref="OptimizedItems"/>
        /// -- dùng để so với B (frozen baseline, cũng đo boundary "&gt;=") trong phần (d) three-arm
        /// partition, giữ đơn vị nhất quán hai phía.</summary>
        public (int Chunks, int Minutes) HarnessBoundaryPairOptimized { get; init; }

        /// <summary>Cặp (chunk vi phạm, tổng phút trễ) đo QUA con đường name-resolution CỦA HARNESS
        /// (ResolveExactlyOne trên Days) nhưng với boundary PRODUCTION ("&gt;") -- giữ nguyên đường đi
        /// name-stripping của A6 crutch, chỉ đổi boundary. Nếu số này khớp với
        /// <see cref="AllocatorValidation"/>.Violations.Count/TotalOverdueMinutes, đó là bằng chứng
        /// A6's name-resolution TƯƠNG ĐƯƠNG với identity thật -- đúng thứ criterion 10 cần chứng minh,
        /// tách biệt khỏi bất đồng boundary (một phát hiện riêng, không phải điều cần assert bằng).</summary>
        public (int Chunks, int Minutes) NameResolvedProductionBoundaryPair { get; init; }
    }

    /// <summary>
    /// T3.4 (Card G) — chạy TOÀN BỘ corpus (<see cref="SoeCorpusGenerator.Generate"/>) qua allocator
    /// hiện tại (T3.3, earliest-feasible) rồi qua <see cref="ScheduleOptimizer"/> thật (T3.9, N=1
    /// LoadRebalanceStage, trọng số mặc định <see cref="SoeWeights"/> -- G3/T3.5 chưa ratify) ĐÚNG
    /// MỘT lần cho toàn bộ test process (qua <see cref="Results"/>, lazy + cache), để (a)/(b)/(c)/(d)
    /// của T3.4 dùng chung một lần chạy thay vì mỗi test tự chạy lại corpus (~230 scenario).
    ///
    /// <b>Vì sao KHÔNG dùng <c>SoeScheduleMetrics.RunScenario</c> trực tiếp:</b> nó chỉ trả về
    /// <c>List&lt;ScheduleDay&gt;</c> (không có <c>ScheduledItem</c>/identity) và không gán MaTask xác
    /// định -- xem <see cref="SoeScheduleMetrics.RunScenarioWithIdentity"/>'s doc comment cho lý do
    /// determinism.
    /// </summary>
    public static class SoeOptimizeCorpusMetrics
    {
        public static readonly SoeWeights DefaultWeights = new(1.0, 1.0, 1.0, 1.0, 1.0);

        private static readonly Lazy<IReadOnlyList<SoeOptimizeCorpusItemResult>> _cached =
            new(() => RunFresh().ToList());

        /// <summary>Kết quả chạy corpus, cache trong suốt vòng đời process test -- dùng cho mọi test
        /// KHÔNG cần tự kiểm determinism (đã có test riêng cho việc đó, xem
        /// <c>SoeT34InvariantTests.Optimize_CorpusRun_IsDeterministic_AcrossThreeRepeatedRuns</c>).</summary>
        public static IReadOnlyList<SoeOptimizeCorpusItemResult> Results => _cached.Value;

        /// <summary>Chạy lại corpus TỪ ĐẦU, không qua cache -- dùng bởi test determinism (cần nhiều
        /// lần chạy ĐỘC LẬP, không phải đọc lại cùng một list đã cache).</summary>
        public static IReadOnlyList<SoeOptimizeCorpusItemResult> RunFresh()
        {
            var scenarios = SoeCorpusGenerator.Generate();
            var validator = new ConstraintValidator();
            var evaluator = new ObjectiveEvaluator();

            var results = new List<SoeOptimizeCorpusItemResult>(scenarios.Count);
            foreach (var scenario in scenarios)
            {
                var (record, days, items) = SoeScheduleMetrics.RunScenarioWithIdentity(scenario);

                var allocatorValidation = validator.Validate(items);

                var optimizer = new ScheduleOptimizer(
                    new IOptimizerStage[] { new LoadRebalanceStage() },
                    validator,
                    evaluator,
                    DefaultWeights);
                var (optimizedItems, report) = optimizer.Optimize(items);

                var optimizedValidation = validator.Validate(optimizedItems);

                results.Add(new SoeOptimizeCorpusItemResult
                {
                    Id = scenario.Id,
                    Category = scenario.Category,
                    DesignedFeasible = scenario.DesignedFeasible,
                    AllocatorItems = items,
                    OptimizedItems = optimizedItems,
                    Report = report,
                    AllocatorValidation = allocatorValidation,
                    OptimizedValidation = optimizedValidation,
                    HarnessRecord = record,
                    HarnessBoundaryPairAllocator = ComputeHarnessBoundaryPair(items),
                    HarnessBoundaryPairOptimized = ComputeHarnessBoundaryPair(optimizedItems),
                    NameResolvedProductionBoundaryPair = ComputeNameResolvedProductionBoundaryPair(scenario, days),
                });
            }

            return results;
        }

        /// <summary>Boundary "&gt;=" (định nghĩa harness gốc, Card A) tính TRỰC TIẾP trên
        /// <see cref="ScheduledItem"/> -- không cần name-resolution vì ScheduledItem đã mang HanChot
        /// thật (T3.8). Đây là phép đo "compute a compatible pair from the underlying per-item data"
        /// mà execution plan criterion 10 gợi ý khi cần đối chiếu qua identity thật.</summary>
        public static (int Chunks, int Minutes) ComputeHarnessBoundaryPair(IReadOnlyList<ScheduledItem> items)
        {
            int chunks = 0;
            long minutes = 0;
            foreach (var item in items)
            {
                if (item.Date.Date >= item.HanChot.Date)
                {
                    chunks++;
                    minutes += item.SoPhut;
                }
            }
            return (chunks, (int)Math.Min(minutes, int.MaxValue));
        }

        /// <summary>Đi qua đúng con đường A6 crutch (ResolveExactlyOne trên Days), nhưng đổi boundary
        /// từ "&gt;=" (harness gốc) sang "&gt;" (production, <see cref="UniformDeadlinePolicy"/>) --
        /// dùng để cô lập RIÊNG bất đồng boundary khỏi bất đồng name-resolution-vs-identity-thật.</summary>
        public static (int Chunks, int Minutes) ComputeNameResolvedProductionBoundaryPair(
            SoeScenario scenario, List<ScheduleDay> days)
        {
            int chunks = 0;
            long minutes = 0;
            foreach (var day in days)
            {
                foreach (var st in day.Tasks)
                {
                    var resolved = SoeScheduleMetrics.ResolveExactlyOne(st.TenTask, scenario.Tasks, scenario.Id);
                    if (day.Date.Date > resolved.HanChot.Date)
                    {
                        chunks++;
                        minutes += st.SoPhut;
                    }
                }
            }
            return (chunks, (int)Math.Min(minutes, int.MaxValue));
        }
    }
}
