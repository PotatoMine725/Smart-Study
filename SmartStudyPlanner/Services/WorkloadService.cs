using SmartStudyPlanner.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmartStudyPlanner.Services
{
    // Chuyển 2 class phụ trợ sang đây để dùng chung
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
        public List<ScheduledTask> Tasks { get; set; } = new List<ScheduledTask>();
    }

    public static class WorkloadService
    {
        // Lưu thiết lập Capacity vào file text nhỏ để không bị "trôi tuột"
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capacity.txt");

        public static double GetCapacity()
        {
            if (File.Exists(FilePath) && double.TryParse(File.ReadAllText(FilePath), out double val)) return val;
            return 3.0; // Mặc định 3 tiếng nếu chưa từng cài đặt
        }

        public static void SaveCapacity(double capacity)
        {
            File.WriteAllText(FilePath, capacity.ToString());
        }

        // Bưng nguyên cái thuật toán Least Load + Splitting từ ViewModel sang đây
        public static List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours)
        {
            int capacityMinutes = (int)(capacityHours * 60);
            var tatCaTask = new List<StudyTask>();
            var dictMonHoc = new Dictionary<StudyTask, MonHoc>();

            foreach (var mon in hocKy.DanhSachMonHoc)
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

                int remainingMinutes = minutesNeeded;
                int part = 1;

                while (remainingMinutes > 0)
                {
                    var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes).OrderBy(d => d.TotalMinutes).FirstOrDefault();

                    if (targetDay == null)
                    {
                        targetDay = days.OrderBy(d => d.TotalMinutes).First();
                        targetDay.Tasks.Add(new ScheduledTask { TenTask = part > 1 ? $"{task.TenTask} (Phần {part})" : task.TenTask, TenMon = dictMonHoc[task].TenMonHoc, SoPhut = remainingMinutes });
                        targetDay.TotalMinutes += remainingMinutes;
                        break;
                    }

                    int spaceLeft = capacityMinutes - targetDay.TotalMinutes;
                    int chunk = Math.Min(remainingMinutes, spaceLeft);

                    targetDay.Tasks.Add(new ScheduledTask { TenTask = (minutesNeeded > spaceLeft || part > 1) ? $"{task.TenTask} (Phần {part})" : task.TenTask, TenMon = dictMonHoc[task].TenMonHoc, SoPhut = chunk });
                    targetDay.TotalMinutes += chunk;
                    remainingMinutes -= chunk;
                    part++;
                }
            }
            return days;
        }
    }
}