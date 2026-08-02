using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Instance-based implementation của IWorkloadService.
    /// Inject IDecisionEngine và IClock qua constructor — không còn phụ thuộc vào static class.
    /// </summary>
    public class WorkloadServiceImpl : IWorkloadService
    {
        private static readonly string FilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capacity.txt");

        private const double DefaultCapacityHours = 3.0;

        /// <summary>
        /// Sàn sức học. Lấy đúng theo Minimum của slider ở WorkloadBalancerPage.xaml:68 —
        /// đây là mức nhỏ nhất mà UI thừa nhận, không phải con số bịa ra.
        /// Nó cũng chặn luôn lỗi treo: capacityHours &lt; 1/60 làm (int)(h*60) = 0 và
        /// GenerateSchedule lặp vô hạn (WP-4 §3.1). Kẹp ở đây vì file là đường vào duy nhất
        /// không tin được — WorkloadBalancerViewModel đọc GetCapacity() rồi gọi thẳng
        /// GenerateSchedule ngay trong constructor, trước khi slider kịp kẹp giá trị.
        /// </summary>
        private const double MinCapacityHours = 1.0;

        private readonly IDecisionEngine _decisionEngine;
        private readonly IClock _clock;

        public WorkloadServiceImpl(IDecisionEngine decisionEngine, IClock clock)
        {
            _decisionEngine = decisionEngine;
            _clock = clock;
        }

        public double GetCapacity()
        {
            if (!File.Exists(FilePath)) return DefaultCapacityHours;

            string raw = File.ReadAllText(FilePath).Trim();

            // NumberStyles.Float (KHÔNG kèm AllowThousands) là phần quan trọng nhất ở đây.
            // Overload double.TryParse(s, out v) ngầm bật AllowThousands, nên nó fail HỞ chứ
            // không fail đóng: "4,5" trên en-US ra 45, "4.5" trên vi-VN cũng ra 45 — sức học
            // gấp 10 lần mà không có lấy một thông báo. Với Float thì cả hai trường hợp lệch
            // culture đều parse fail và rơi về default, đúng như mong đợi.
            //
            // Invariant trước (định dạng SaveCapacity ghi ra), rồi mới tới culture hiện tại
            // để file cũ trên máy vi-VN ("4,5") vẫn đọc được — lần mở trang workload kế tiếp
            // sẽ SaveCapacity đè lại thành invariant, nên nhánh fallback này là đường di cư
            // một chiều, chỉ cần sống sót đúng một lần mở app.
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double val)
                && !double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out val))
            {
                return DefaultCapacityHours;
            }

            return Math.Max(val, MinCapacityHours);
        }

        public void SaveCapacity(double capacity)
        {
            File.WriteAllText(FilePath, capacity.ToString(CultureInfo.InvariantCulture));
        }

        public List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours)
        {
            int capacityMinutes = (int)(capacityHours * 60);
            var tatCaTask = new List<StudyTask>();
            var dictMonHoc = new Dictionary<StudyTask, MonHoc>();

            foreach (var mon in hocKy.DanhSachMonHoc)
            {
                foreach (var task in mon.DanhSachTask.Where(t => t.TrangThai != StudyTaskStatus.HoanThanh))
                {
                    task.DiemUuTien = _decisionEngine.CalculatePriority(task, mon);
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
                int minutesNeeded = _decisionEngine.CalculateRawSuggestedMinutes(task) - task.ThoiGianDaHoc;
                if (minutesNeeded <= 0) continue;

                int remainingMinutes = minutesNeeded;
                int part = 1;

                while (remainingMinutes > 0)
                {
                    var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes)
                                       .OrderBy(d => d.TotalMinutes)
                                       .FirstOrDefault();

                    if (targetDay == null)
                    {
                        int nextOffset = days.Count;
                        DateTime newDate = today.AddDays(nextOffset);
                        targetDay = new ScheduleDay
                        {
                            Date = newDate,
                            DisplayName = newDate.ToString("dd/MM/yyyy")
                        };
                        days.Add(targetDay);
                    }

                    int spaceLeft = capacityMinutes - targetDay.TotalMinutes;
                    int chunk = Math.Min(remainingMinutes, spaceLeft);

                    targetDay.Tasks.Add(new ScheduledTask
                    {
                        TenTask = (minutesNeeded > spaceLeft || part > 1) ? $"{task.TenTask} (Phần {part})" : task.TenTask,
                        TenMon = dictMonHoc[task].TenMonHoc,
                        SoPhut = chunk
                    });
                    targetDay.TotalMinutes += chunk;
                    remainingMinutes -= chunk;
                    part++;
                }
            }

            return days;
        }
    }
}
