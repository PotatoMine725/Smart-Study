namespace SmartStudyPlanner.Core.Risk.Models
{
    public class RiskAssessment
    {
        public Guid TaskId { get; init; }
        public double Score { get; init; }
        public RiskLevel Level { get; init; }
        public double DeadlineUrgencyScore { get; init; }
        public double ProgressGapScore { get; init; }
        public double PerformanceDropScore { get; init; }

        public string DisplayLabel => Level switch
        {
            RiskLevel.Critical => "⚠️ Khẩn cấp",
            RiskLevel.High => "🔴 Nguy cơ cao",
            RiskLevel.Medium => "🟡 Chú ý",
            RiskLevel.Low => "🟢 An toàn",
            _ => "Không xác định"
        };

        public static RiskLevel FromScore(double score) => score switch
        {
            >= 0.8 => RiskLevel.Critical,
            >= 0.6 => RiskLevel.High,
            >= 0.3 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }
}
