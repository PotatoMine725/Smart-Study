using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Soe;
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
            => GenerateScheduleWithIdentity(hocKy, capacityHours).Days;

        /// <summary>
        /// T3.8 (Epic 3, Card C) — cùng thuật toán với <see cref="GenerateSchedule"/> (không đổi
        /// hành vi phân bổ so với <see cref="GenerateSchedule"/>: sort ưu tiên, earliest-feasible
        /// placement (T3.3, CP-3 2026-08-05), mở ngày tràn — characterization suite
        /// <c>WorkloadServiceScheduleTests</c> chốt việc này), nhưng còn trả về danh sách
        /// <see cref="ScheduledItem"/> — mang <c>MaTask</c> + <c>HanChot</c> mà
        /// <c>List&lt;ScheduleDay&gt;</c> không có chỗ chứa.
        ///
        /// <c>internal</c> (InternalsVisibleTo SmartStudyPlanner.Tests, AssemblyInfo.cs:4) vì đây
        /// chưa phải seam công khai — <c>IWorkloadService</c> giữ nguyên chỉ một member
        /// <c>GenerateSchedule</c>. Đây là chỗ Card D (<c>IConstraintValidator</c>) / Card E
        /// (<c>IObjectiveEvaluator</c>) sẽ nối vào tiếp, thay vì đọc ngược từ
        /// <c>List&lt;ScheduleDay&gt;</c> đã mất identity.
        /// </summary>
        internal (List<ScheduleDay> Days, List<ScheduledItem> Items) GenerateScheduleWithIdentity(
            HocKy hocKy, double capacityHours)
        {
            int capacityMinutes = ClampCapacityMinutes(capacityHours);
            var tatCaTask = new List<StudyTask>();
            var dictMonHoc = new Dictionary<StudyTask, MonHoc>();

            // T3.3 (Epic 3, Card F, CP-2 2026-08-05): điểm ưu tiên được tính vào một cấu trúc
            // cục bộ thay vì ghi thẳng vào task.DiemUuTien -- GenerateSchedule không còn tác
            // dụng phụ lên model của caller nữa. PrioritizeStage và
            // QuanLyTaskViewModel.CapNhatDiem đã tự chấm lại DiemUuTien độc lập ở chỗ khác; UI
            // Workload Balancer không bind DiemUuTien -- xác nhận bằng grep trước khi bỏ dòng
            // ghi đè này, không phải suy đoán.
            var diemUuTienTheoTask = new Dictionary<StudyTask, double>();

            foreach (var mon in hocKy.DanhSachMonHoc)
            {
                foreach (var task in mon.DanhSachTask.Where(t => t.TrangThai != StudyTaskStatus.HoanThanh))
                {
                    diemUuTienTheoTask[task] = _decisionEngine.CalculatePriority(task, mon);
                    tatCaTask.Add(task);
                    dictMonHoc[task] = mon;
                }
            }

            var sortedTasks = tatCaTask.OrderByDescending(t => diemUuTienTheoTask[t]).ToList();
            var days = new List<ScheduleDay>();
            var scheduledItems = new List<ScheduledItem>();

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
                    // T3.3 (Epic 3, Card F, CP-3 2026-08-05): earliest-feasible thay least-loaded
                    // (OrderBy(d => d.TotalMinutes)). Ưu tiên ngày SỚM NHẤT còn chỗ mà không vượt
                    // HanChot của task; nếu không ngày nào trong hạn còn chỗ, rơi về ngày sớm nhất
                    // còn chỗ bất kể hạn chót (KHÔNG từ chối xếp -- đó là việc của
                    // IConstraintValidator ở một bước validate sau, không phải allocator này).
                    // Deadline chỉ chi phối CHỌN NGÀY, chưa bao giờ chi phối có xếp hay không.
                    //
                    // ĐÃ CHỨNG MINH BẰNG ĐẠI SỐ (2026-08-06 review round, không chỉ thử nghiệm),
                    // rồi xác nhận lại bằng nhiều tổ hợp deadline/priority xen kẽ (kể cả deadline
                    // trong quá khứ): nhánh lọc theo HanChot ở trên KHÔNG BAO GIỜ đổi ngày được
                    // chọn, với BẤT KỲ input nào. Gọi E = "ngày sớm nhất (theo Date) còn chỗ trong
                    // days hiện tại" (bỏ qua hạn chót). Vì capacity một ngày chỉ TĂNG (không có gì
                    // làm rỗng lại một ngày đã lấp) và days được duyệt theo Date tăng dần:
                    //   - Nếu E &lt;= HanChot: E tự thoả điều kiện lọc, nên tier-1 trả về đúng E
                    //     (E là min của toàn tập, cũng là min của bất kỳ tập con nào chứa E).
                    //   - Nếu E &gt; HanChot: mọi ngày &lt;= HanChot chắc chắn đã đầy (vì E là ngày
                    //     sớm nhất CÒN CHỖ), nên tier-1 rỗng, rơi về tier-2 (bỏ qua hạn) -- vẫn ra E.
                    // Hai nhánh trên tính LUÔN ra E, không có input nào lọt qua kẽ hở. Vì vậy nhánh
                    // này KHÔNG THỂ có mutation check: không có mutation nào của điều kiện lọc HanChot
                    // làm thay đổi output trên bất kỳ input nào -- đây là dạng "provably inert" mạnh
                    // nhất, không phải "chưa test tới". Nó trở thành có tác dụng thật khi có cơ chế
                    // làm ngày "mất chỗ" không đơn điệu (vd. reorder/reject của Optimize() seam,
                    // T3.9, chưa xây ở card này) hoặc khi IConstraintValidator từ chối một placement
                    // và allocator phải thử lại. Giữ nhánh này đúng như ratify (CP-3) thay vì rút
                    // gọn thành "luôn chọn E" -- nó là phần "deadline-aware" mà card này được giao
                    // xây, và trở thành sống khi input/cơ chế phía trên đổi, dù hôm nay chưa có.
                    DateTime hanChotDate = task.HanChot.Date;

                    var targetDay = days.Where(d => d.TotalMinutes < capacityMinutes && d.Date <= hanChotDate)
                                       .OrderBy(d => d.Date)
                                       .FirstOrDefault()
                                   ?? days.Where(d => d.TotalMinutes < capacityMinutes)
                                       .OrderBy(d => d.Date)
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

                    string tenHienThi = (minutesNeeded > spaceLeft || part > 1)
                        ? $"{task.TenTask} (Phần {part})"
                        : task.TenTask;

                    // Xây ScheduledItem (mang identity) TRƯỚC, rồi chiếu sang ScheduledTask —
                    // đúng thứ tự "build internal representation, then project" của T3.8, chỉ làm
                    // theo từng chunk thay vì một lượt riêng sau khi vòng lặp kết thúc: vòng lặp
                    // đóng ngày mới dựa trên trạng thái TotalMinutes tích luỹ (greedy, có thứ tự),
                    // nên tách "quyết định xếp vào đâu" ra khỏi "ghi lại đã xếp gì" thành hai lượt
                    // độc lập sẽ phải mô phỏng lại đúng thuật toán này — rủi ro lệch hành vi không
                    // cần thiết cho một seam chỉ cần tồn tại, chưa cần được tiêu thụ ở card này.
                    var item = new ScheduledItem(
                        MaTask: task.MaTask,
                        HanChot: task.HanChot,
                        TenTaskGoc: task.TenTask,
                        TenHienThi: tenHienThi,
                        TenMon: dictMonHoc[task].TenMonHoc,
                        Date: targetDay.Date,
                        SoPhut: chunk);
                    scheduledItems.Add(item);

                    targetDay.Tasks.Add(new ScheduledTask
                    {
                        TenTask = item.TenHienThi,
                        TenMon = item.TenMon,
                        SoPhut = item.SoPhut
                    });
                    targetDay.TotalMinutes += chunk;
                    remainingMinutes -= chunk;
                    part++;
                }
            }

            return (days, scheduledItems);
        }
    }
}
