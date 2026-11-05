using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Models
{
    /// <summary>Một task đã được lên lịch (chỉ dùng để hiển thị, không lưu DB).</summary>
    public class ScheduledTask
    {
        public string TenTask { get; set; } = string.Empty;
        public string TenMon { get; set; } = string.Empty;
        public int SoPhut { get; set; }
        public string ThoiGianHienThi => $"{SoPhut} phút";
    }

    /// <summary>Kế hoạch học tập của 1 ngày.</summary>
    public class ScheduleDay
    {
        public DateTime Date { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int TotalMinutes { get; set; }
        public string HeaderText => $"{DisplayName} (Tổng: {TotalMinutes / 60}h {TotalMinutes % 60}p)";
        public List<ScheduledTask> Tasks { get; set; } = new List<ScheduledTask>();
    }
}
