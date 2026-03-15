using System;

namespace SmartStudyPlanner
{
    // Class này chỉ dùng để hiển thị dữ liệu lên màn hình Dashboard
    public class TaskDashboardItem
    {
        public string TenMonHoc { get; set; }
        public string TenTask { get; set; }
        public DateTime HanChot { get; set; }
        public double DiemUuTien { get; set; }
        public string MucDoCanhBao { get; set; }
    }
}