using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Contract cho Workload Balancer — xếp lịch học theo ngày và quản lý capacity.
    /// Inject interface này thay vì gọi static WorkloadService trực tiếp.
    /// </summary>
    public interface IWorkloadService
    {
        /// <summary>Lấy số giờ/ngày người dùng đã cài đặt (mặc định 3.0h).</summary>
        double GetCapacity();

        /// <summary>Lưu số giờ/ngày.</summary>
        void SaveCapacity(double capacity);

        /// <summary>Xếp lịch theo ngày sớm nhất còn chỗ (T3.3), trả về các ngày có bài.</summary>
        List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours);
    }
}
