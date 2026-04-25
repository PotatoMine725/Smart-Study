namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>Mức độ rủi ro của một task.</summary>
    public enum RiskLevel
    {
        /// <summary>Điểm rủi ro &lt; 0.3 — task còn nhiều thời gian.</summary>
        Low,

        /// <summary>Điểm rủi ro 0.3 – 0.6 — cần chú ý.</summary>
        Medium,

        /// <summary>Điểm rủi ro 0.6 – 0.8 — nguy cơ cao.</summary>
        High,

        /// <summary>Điểm rủi ro &gt;= 0.8 — khẩn cấp, cần xử lý ngay.</summary>
        Critical
    }
}
