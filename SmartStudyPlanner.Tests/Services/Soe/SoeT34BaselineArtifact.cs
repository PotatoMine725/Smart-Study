using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.4 (Card G) — đọc lại artifact đóng băng của Card A
    /// (<c>docs/reports/data/2026-08-05-soe-t36-baseline.json</c>, <c>B</c> trong ký hiệu G2-5) làm
    /// nguồn "before" cho phần (b) inversion delta và phần (d) three-arm partition. KHÔNG BAO GIỜ
    /// ghi/tái tạo file này (đó là việc bị cấm rõ ràng của <c>SoeBaselineCaptureTests</c>'s
    /// <c>[Fact(Skip=...)]</c> — Card G chỉ ĐỌC).
    /// </summary>
    public sealed class SoeT34BaselineArtifact
    {
        public AggregateStats Aggregate { get; init; } = new();
        public IReadOnlyDictionary<string, ScheduleRecord> SchedulesById { get; init; } =
            new Dictionary<string, ScheduleRecord>();

        private static readonly Lazy<SoeT34BaselineArtifact> _cached = new(LoadFromDisk);

        public static SoeT34BaselineArtifact LoadFrozen() => _cached.Value;

        private static SoeT34BaselineArtifact LoadFromDisk()
        {
            string repoRoot = RepoLocator.FindRepoRoot();
            string path = Path.Combine(repoRoot, "docs", "reports", "data", "2026-08-05-soe-t36-baseline.json");

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Frozen baseline artifact không tồn tại tại '{path}' -- T3.4/Card G mất mốc 'before' " +
                    "để đo delta. Xem SoeBaselineCaptureTests.BaselineArtifact_TonTaiVaCoCauTrucHopLe.");
            }

            var options = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            };

            string json = File.ReadAllText(path);
            var artifact = JsonSerializer.Deserialize<BaselineArtifact>(json, options)
                ?? throw new InvalidOperationException($"Không parse được artifact tại '{path}'.");

            return new SoeT34BaselineArtifact
            {
                Aggregate = artifact.Aggregate,
                SchedulesById = artifact.Schedules.ToDictionary(s => s.Id),
            };
        }
    }
}
