using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SmartStudyPlanner.ViewModels
{
    // Lớp phụ để chứa dữ liệu hiển thị lên UI
    public class ScheduledTask
    {
        public string TenTask { get; set; }
        public string TenMon { get; set; }
        public int SoPhut { get; set; }
        public string ThoiGianHienThi => $"{SoPhut} phút";
    }

    public class ScheduleDay
    {
        public DateTime Date { get; set; }
        public string DisplayName { get; set; }
        public int TotalMinutes { get; set; }
        public string HeaderText => $"{DisplayName} (Tổng: {TotalMinutes / 60}h {TotalMinutes % 60}p)";
        public ObservableCollection<ScheduledTask> Tasks { get; set; } = new ObservableCollection<ScheduledTask>();
    }

    public partial class WorkloadBalancerViewModel : ObservableObject
    {
        private HocKy _hocKy;

        // CAPACITY CỦA NGƯỜI DÙNG (Mặc định 3 tiếng)
        [ObservableProperty] private int capacityHours = 3;

        [ObservableProperty] private ObservableCollection<ScheduleDay> schedule = new ObservableCollection<ScheduleDay>();

        public WorkloadBalancerViewModel(HocKy hocKy)
        {
            _hocKy = hocKy;
            GenerateSchedule();
        }

        [RelayCommand]
        private void GenerateSchedule()
        {
            Schedule.Clear();
            int capacityMinutes = CapacityHours * 60; // Đổi ra phút

            // 1. Gom tất cả Task chưa làm
            var tatCaTask = new List<StudyTask>();
            var dictMonHoc = new Dictionary<StudyTask, MonHoc>();

            foreach (var mon in _hocKy.DanhSachMonHoc)
            {
                foreach (var task in mon.DanhSachTask.Where(t => t.TrangThai != "Hoàn thành"))
                {
                    task.DiemUuTien = DecisionEngine.CalculatePriority(task, mon);
                    tatCaTask.Add(task);
                    dictMonHoc[task] = mon;
                }
            }

            // 2. GREEDY + WEIGHTED: Ưu tiên Task điểm cao nhất (Khẩn cấp nhất) làm trước
            var sortedTasks = tatCaTask.OrderByDescending(t => t.DiemUuTien).ToList();

            // 3. Chuẩn bị "Các bình chứa nước" cho 7 ngày tới
            var days = new List<ScheduleDay>();
            for (int i = 0; i < 7; i++)
            {
                DateTime d = DateTime.Now.Date.AddDays(i);
                string name = i == 0 ? "Hôm nay" : (i == 1 ? "Ngày mai" : d.ToString("dd/MM/yyyy"));
                days.Add(new ScheduleDay { Date = d, DisplayName = name });
            }

            // 4. THUẬT TOÁN LEAST LOAD ĐỂ NHÉT TASK VÀO NGÀY
            foreach (var task in sortedTasks)
            {
                // Gọi hàm AI hiện tại để lấy số phút dự kiến
                int minutesNeeded = DecisionEngine.CalculateRawSuggestedMinutes(task) - task.ThoiGianDaHoc;
                if (minutesNeeded <= 0) continue;

                // Tìm ngày rảnh nhất (Least Load) mà khi nhét task này vào vẫn CHƯA VƯỢT Capacity
                var targetDay = days.Where(d => d.TotalMinutes + minutesNeeded <= capacityMinutes)
                                    .OrderBy(d => d.TotalMinutes)
                                    .FirstOrDefault();

                // Nếu ngày nào cũng đầy tràn Capacity -> Ép nhét vào ngày trống nhất hiện hành (Overload)
                if (targetDay == null)
                {
                    targetDay = days.OrderBy(d => d.TotalMinutes).First();
                }

                targetDay.Tasks.Add(new ScheduledTask
                {
                    TenTask = task.TenTask,
                    TenMon = dictMonHoc[task].TenMonHoc,
                    SoPhut = minutesNeeded
                });
                targetDay.TotalMinutes += minutesNeeded;
            }

            // Hiển thị ra UI những ngày có bài tập
            foreach (var d in days.Where(d => d.Tasks.Count > 0))
            {
                Schedule.Add(d);
            }
        }
    }
}