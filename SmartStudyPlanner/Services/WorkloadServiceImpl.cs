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

        private const int MinCapacityMinutes = (int)(MinCapacityHours * 60);

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

            // NumberStyles.Float vẫn nhận "NaN"/"Infinity", và "1e400" tràn thành +∞ mà vẫn
            // trả true. Math.Max(NaN, x) = NaN chứ không chọn toán hạng kia, nên riêng cái sàn
            // KHÔNG chặn được: NaN lọt xuống thành (int)(NaN*60) = 0 — đúng lại lỗi treo mà
            // sàn sinh ra để chặn — còn +∞ cast ra int.MinValue làm spaceLeft âm và
            // remainingMinutes TĂNG mỗi vòng lặp. Không hữu hạn thì coi như rác.
            if (!double.IsFinite(val)) return DefaultCapacityHours;

            return Math.Max(val, MinCapacityHours);
        }

        public void SaveCapacity(double capacity)
        {
            File.WriteAllText(FilePath, capacity.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Kẹp capacityHours về số phút dùng được. Đây là bất biến RIÊNG của vòng lặp phân bổ
        /// bên dưới, nên nó phải được bảo vệ tại chỗ chứ không đi mượn GetCapacity: hai đường
        /// vào hiện tại (GetCapacity đã kẹp sàn, slider Minimum="1") đều sạch, nhưng đó là
        /// tính chất của caller, không phải của method này.
        ///
        /// capacityMinutes &lt; 1 làm vòng while không bao giờ kết thúc: spaceLeft &lt;= 0 =&gt;
        /// chunk &lt;= 0 =&gt; remainingMinutes không giảm. Ba cách rơi vào đó:
        ///   - NaN: mọi phép so sánh với NaN đều false, nên !(x &gt;= sàn) bắt được, còn
        ///     (x &lt; sàn) thì KHÔNG — đây đúng là lỗ đã lọt qua Math.Max ở GetCapacity.
        ///   - 0 hoặc âm: (int)(h*60) ra 0 hoặc âm.
        ///   - +∞ / tràn: cast double-&gt;int khi ngoài dải là undefined, thực tế ra int.MinValue,
        ///     làm spaceLeft âm và remainingMinutes TĂNG mỗi vòng.
        /// </summary>
        private static int ClampCapacityMinutes(double capacityHours)
        {
            if (!(capacityHours >= MinCapacityHours)) return MinCapacityMinutes;

            double minutes = capacityHours * 60;
            return minutes >= int.MaxValue ? int.MaxValue : (int)minutes;
        }

        public List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours)
        {
            int capacityMinutes = ClampCapacityMinutes(capacityHours);
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
