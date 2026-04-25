using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Static facade giữ nguyên để tương thích ngược với các call sites cũ.
    /// ScheduledTask và ScheduleDay đã chuyển sang SmartStudyPlanner.Models.
    /// Logic thực tế đã chuyển sang WorkloadServiceImpl (instance-based, injectable).
    /// Không thêm logic mới vào đây — hãy thêm vào IWorkloadService / WorkloadServiceImpl.
    /// </summary>
    public static class WorkloadService
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capacity.txt");
        private static IClock _clock = new SystemClock();

        public static double GetCapacity()
        {
            if (File.Exists(FilePath) && double.TryParse(File.ReadAllText(FilePath), out double val)) return val;
            return 3.0;
        }

        public static void SaveCapacity(double capacity)
        {
            File.WriteAllText(FilePath, capacity.ToString());
        }

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

            DateTime today = _clock.Now.Date;
            for (int i = 0; i < 7; i++)
            {
                DateTime d = today.AddDays(i);
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