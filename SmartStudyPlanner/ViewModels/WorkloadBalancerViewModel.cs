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
            // BƯỚC 1: XÓA SẠCH DANH SÁCH HIỆN TẠI THAY VÌ TẠO DANH SÁCH MỚI
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

            foreach (var task in sortedTasks)
            {
                int minutesNeeded = DecisionEngine.CalculateRawSuggestedMinutes(task) - task.ThoiGianDaHoc;
                if (minutesNeeded <= 0) continue;

                var targetDay = days.Where(d => d.TotalMinutes + minutesNeeded <= capacityMinutes)
                                    .OrderBy(d => d.TotalMinutes)
                                    .FirstOrDefault();

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

            // BƯỚC 2: BƠM DỮ LIỆU MỚI VÀO DANH SÁCH CŨ, UI SẼ LẬP TỨC CHỚP SÁNG VÀ VẼ LẠI
            foreach (var d in days.Where(d => d.Tasks.Count > 0))
            {
                Schedule.Add(d);
            }

            // BƯỚC 3: HIỆN THÔNG BÁO ĐỂ DEBUG (Để biết thanh Slider có truyền đúng số vào không)
            System.Windows.MessageBox.Show($"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!", "Workload Balancer");
        }
    }
}