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
        [ObservableProperty] private double capacityHours = 3;

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

            int capacityMinutes = (int)(CapacityHours * 60);

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

            var sortedTasks = tatCaTask.OrderByDescending(t => t.DiemUuTien).ToList();

            var days = new List<ScheduleDay>();
            for (int i = 0; i < 7; i++)
            {
                DateTime d = DateTime.Now.Date.AddDays(i);
                string name = i == 0 ? "Hôm nay" : (i == 1 ? "Ngày mai" : d.ToString("dd/MM/yyyy"));
                days.Add(new ScheduleDay { Date = d, DisplayName = name });
            }

            // 4. THUẬT TOÁN LEAST LOAD + AUTO-SPLITTING (TỰ ĐỘNG CHIA NHỎ)
            foreach (var task in sortedTasks)
            {
                int minutesNeeded = DecisionEngine.CalculateRawSuggestedMinutes(task) - task.ThoiGianDaHoc;
                if (minutesNeeded <= 0) continue;

                int remainingMinutes = minutesNeeded;
                int part = 1; // Biến đếm xem bài tập bị chia làm mấy phần

                while (remainingMinutes > 0)
                {
                    // Tìm ngày rảnh nhất mà VẪN CÒN TRỐNG so với Capacity
                    var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes)
                                        .OrderBy(d => d.TotalMinutes)
                                        .FirstOrDefault();

                    // Nếu 7 ngày tới đều đã bị full Capacity -> Bắt buộc nhét phần còn lại vào ngày rảnh nhất
                    if (targetDay == null)
                    {
                        targetDay = days.OrderBy(d => d.TotalMinutes).First();

                        targetDay.Tasks.Add(new ScheduledTask
                        {
                            TenTask = part > 1 ? $"{task.TenTask} (Phần {part})" : task.TenTask,
                            TenMon = dictMonHoc[task].TenMonHoc,
                            SoPhut = remainingMinutes
                        });
                        targetDay.TotalMinutes += remainingMinutes;
                        break; // Nhét xong rồi thì thoát vòng lặp
                    }

                    // Tính xem ngày này còn chứa được bao nhiêu phút
                    int spaceLeft = capacityMinutes - targetDay.TotalMinutes;

                    // Chỉ cắt đúng phần vừa vặn với chỗ trống
                    int chunk = Math.Min(remainingMinutes, spaceLeft);

                    targetDay.Tasks.Add(new ScheduledTask
                    {
                        // Tự động nối thêm chữ (Phần 1, Phần 2...) nếu bài tập bị chặt ra
                        TenTask = (minutesNeeded > spaceLeft || part > 1) ? $"{task.TenTask} (Phần {part})" : task.TenTask,
                        TenMon = dictMonHoc[task].TenMonHoc,
                        SoPhut = chunk
                    });

                    targetDay.TotalMinutes += chunk;
                    remainingMinutes -= chunk;
                    part++;
                }
            }

            foreach (var d in days.Where(d => d.Tasks.Count > 0))
            {
                Schedule.Add(d);
            }

            System.Windows.MessageBox.Show($"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!", "Workload Balancer");
        }
    }
}