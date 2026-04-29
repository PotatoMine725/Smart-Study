using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Contract cho Workload Balancer — tạo lịch học 7 ngày và quản lý capacity.
    /// Inject interface này thay vì gọi static WorkloadService trực tiếp.
    /// </summary>
    public interface IWorkloadService
    {
        /// <summary>Lấy số giờ/ngày người dùng đã cài đặt (mặc định 3.0h).</summary>
        double GetCapacity();

        /// <summary>Lưu số giờ/ngày.</summary>
        void SaveCapacity(double capacity);

        /// <summary>Chạy thuật toán Greedy Least-Load, trả về lịch 7 ngày.</summary>
        List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours);
    }
}
