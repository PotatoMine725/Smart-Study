using System;

namespace SmartStudyPlanner.Models
{
    // Class này chỉ dùng để hiển thị dữ liệu lên màn hình Dashboard
    public class TaskDashboardItem
    {
        public string TenMonHoc { get; set; }
        public string TenTask { get; set; }
        public DateTime HanChot { get; set; }
        public double DiemUuTien { get; set; }
        public string MucDoCanhBao { get; set; }

        public string ThoiGianGoiY { get; set; }
        public StudyTask TaskGoc { get; set; }
        public MonHoc MonHocGoc { get; set; }

        // M7 — ML prediction marker
        public bool IsMLPrediction { get; set; }

        // Risk Analyzer fields (Module 4)
        public string MucDoRuiRo { get; set; } = "—";
        public double RiskScore { get; set; } = 0.0;
    }
}