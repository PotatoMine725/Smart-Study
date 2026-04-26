namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>Kết quả đánh giá rủi ro của một task.</summary>
    public class RiskAssessment
    {
        /// <summary>Id của task được đánh giá — dùng để tra cứu kết quả pipeline.</summary>
        public Guid TaskId { get; init; }

        /// <summary>Điểm rủi ro tổng hợp [0.0, 1.0].</summary>
        public double Score { get; init; }

        /// <summary>Mức độ rủi ro được phân loại.</summary>
        public RiskLevel Level { get; init; }

        /// <summary>Điểm thành phần Deadline Urgency (trọng số 0.5).</summary>
        public double DeadlineUrgencyScore { get; init; }

        /// <summary>Điểm thành phần Progress Gap (trọng số 0.3).</summary>
        public double ProgressGapScore { get; init; }

        /// <summary>Điểm thành phần Performance Drop (trọng số 0.2).</summary>
        public double PerformanceDropScore { get; init; }

        /// <summary>Nhãn hiển thị cho UI.</summary>
        public string DisplayLabel => Level switch
        {
            RiskLevel.Critical => "⚠️ Khẩn cấp",
            RiskLevel.High     => "🔴 Nguy cơ cao",
            RiskLevel.Medium   => "🟡 Chú ý",
            RiskLevel.Low      => "🟢 An toàn",
            _                  => "Không xác định"
        };

        public static RiskLevel FromScore(double score) => score switch
        {
            >= 0.8 => RiskLevel.Critical,
            >= 0.6 => RiskLevel.High,
            >= 0.3 => RiskLevel.Medium,
            _      => RiskLevel.Low
        };
    }
}
