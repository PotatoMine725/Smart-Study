using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// Một task trong corpus T3.6a. Đây CHÍNH LÀ "A6 side table" (execution plan §3.4, PD-7):
    /// ánh xạ tên task -&gt; hạn chót do corpus tự sở hữu, dùng để đo baseline TRƯỚC KHI
    /// <c>IConstraintValidator</c> (T3.1) tồn tại. Nó là một crutch đo lường, KHÔNG phải sự thật
    /// production -- T3.4 sẽ cross-check các con số đo được ở đây qua validator thật một khi nó
    /// tồn tại (acceptance criterion 10 của execution plan).
    /// </summary>
    public sealed class SoeTaskDef
    {
        public string TenTask { get; init; } = string.Empty;
        public string MonHoc { get; init; } = string.Empty;
        public LoaiCongViec LoaiTask { get; init; }
        public int DoKho { get; init; }
        public DateTime HanChot { get; init; }
        public int MinutesNeeded { get; init; }
        public double Priority { get; init; }
    }

    /// <summary>Một "schedule" trong corpus: một học kỳ đầu vào + một mức capacity, chạy qua
    /// <see cref="SmartStudyPlanner.Services.WorkloadServiceImpl.GenerateSchedule"/> đúng một lần.</summary>
    public sealed class SoeScenario
    {
        public string Id { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;

        /// <summary>Nhãn khả thi THIẾT KẾ theo <see cref="EdfFeasibility"/> -- KHÔNG phải kết quả
        /// thật của allocator (allocator không biết hạn chót). Dùng để đảm bảo tỉ lệ ≥25% bất khả
        /// thi của T3.6a và để phân biệt "feasible-but-improvable" ở T3.6b.</summary>
        public bool DesignedFeasible { get; init; }
        public double CapacityHours { get; init; }
        public DateTime Today { get; init; }
        public List<SoeTaskDef> Tasks { get; init; } = new();
    }

    /// <summary>Mirror thuần của <c>WorkloadServiceImpl.ClampCapacityMinutes</c> (method đó là
    /// private, harness không gọi được trực tiếp). Corpus cần con số CAPACITY SAU KHI KẸP để tính
    /// nhãn khả thi thiết kế cho khớp với capacity mà allocator thật sự dùng -- nếu không, chính
    /// các case 0.5h/0.01h (capacity edge bắt buộc của T3.6a) sẽ có nhãn sai ngay từ đầu.</summary>
    internal static class CapacityMath
    {
        private const double MinCapacityHours = 1.0;
        private const int MinCapacityMinutes = (int)(MinCapacityHours * 60);

        public static int ClampCapacityMinutes(double capacityHours)
        {
            if (!(capacityHours >= MinCapacityHours)) return MinCapacityMinutes;
            double minutes = capacityHours * 60;
            return minutes >= int.MaxValue ? int.MaxValue : (int)minutes;
        }
    }

    /// <summary>
    /// Điều kiện khả thi THIẾT KẾ cho corpus (không phải hành vi thật của allocator, cái đó
    /// deadline-blind). Vì mọi task được phép cắt lát tự do qua nhiều ngày -- đúng cách allocator
    /// hiện tại phân bổ -- đây là bài kiểm EDF chuẩn cho một tài nguyên đơn, preemptive: với mọi
    /// hạn chót D xuất hiện trong corpus, tổng khối lượng của các task có hạn chót &lt;= D không
    /// được vượt quá capacityPerDay * max(D,0) (số ngày [0, D) tính từ "today"). Với bài toán
    /// preemptive một tài nguyên này, điều kiện cumulative-demand-theo-từng-deadline là CẦN VÀ ĐỦ,
    /// không chỉ là cận dưới. Task đã trễ hạn (D &lt;= 0) luôn khiến vế phải = 0, nên bất kỳ khối
    /// lượng dương nào rơi vào nhóm đó cũng làm nhãn thành bất khả thi -- đúng ý nghĩa "đã trễ
    /// ngay từ đầu".
    /// </summary>
    internal static class EdfFeasibility
    {
        public static int DeadlineDay(DateTime hanChot, DateTime today) => (hanChot.Date - today.Date).Days;

        public static bool IsDesignedFeasible(IReadOnlyList<SoeTaskDef> tasks, double capacityHours, DateTime today)
        {
            int capMin = CapacityMath.ClampCapacityMinutes(capacityHours);
            long cumulative = 0;

            foreach (var g in tasks.GroupBy(t => DeadlineDay(t.HanChot, today)).OrderBy(g => g.Key))
            {
                cumulative += g.Sum(t => (long)t.MinutesNeeded);
                int daysAvailable = Math.Max(g.Key, 0);
                if (cumulative > (long)capMin * daysAvailable) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// T3.6a — corpus generator. Sinh ≥200 schedule, ≥25% bất khả thi, seed cố định
    /// (<see cref="Seed"/>), hạn chót đa dạng theo đúng danh sách bắt buộc của execution plan
    /// §3.6 (deadline inversions; ties; trong/ngoài cửa sổ 7 ngày; task quá hạn; trộn
    /// exam-vs-homework; học kỳ bất khả thi; capacity edge 0.5h/0.01h).
    ///
    /// Tên task base (TenTask trước khi allocator gắn "(Phần n)") được cấp phát từ một bộ đếm
    /// toàn cục tăng dần tuyệt đối, đảm bảo DUY NHẤT trên toàn corpus theo cấu trúc -- đây là vế
    /// "generator emits unique task base names" của yêu cầu tự-kiểm PD-7. <see cref="Generate"/>
    /// còn tự kiểm tra lại điều này bằng một HashSet trước khi trả về, để một thay đổi sau này lỡ
    /// phá vỡ bộ đếm sẽ bị bắt ngay tại nguồn, không phải rơi xuống harness.
    /// </summary>
    public static class SoeCorpusGenerator
    {
        public const int Seed = 12345;
        public static readonly DateTime Today = new DateTime(2026, 8, 3);

        private static readonly string[] Subjects =
        {
            "Toán", "Vật Lý", "Hóa Học", "Văn Học", "Tiếng Anh",
            "Lịch Sử", "Địa Lý", "Sinh Học", "Tin Học"
        };

        private static readonly LoaiCongViec[] AllTypes =
            (LoaiCongViec[])Enum.GetValues(typeof(LoaiCongViec));

        public static IReadOnlyList<SoeScenario> Generate()
        {
            var rng = new Random(Seed);
            int seq = 1;
            int loaiIdx = 0;

            var scenarios = new List<SoeScenario>();
            scenarios.AddRange(GenerateInversionScenarios(ref seq, ref loaiIdx));
            scenarios.AddRange(GenerateTieScenarios(ref seq, ref loaiIdx));
            scenarios.AddRange(GenerateOverdueScenarios(ref seq, ref loaiIdx));
            scenarios.AddRange(GenerateWindowBoundaryScenarios(ref seq, ref loaiIdx));
            scenarios.AddRange(GenerateInfeasibleScenarios(ref seq, ref loaiIdx));
            scenarios.AddRange(GenerateCapacityEdgeScenarios(ref seq, ref loaiIdx));
            scenarios.AddRange(GenerateFillerScenarios(ref seq, ref loaiIdx, rng));

            // Tự-kiểm PD-7 tại nguồn: bộ đếm seq lẽ ra không thể tạo trùng tên, nhưng khẳng định
            // lại ở đây bằng dữ liệu thật thay vì chỉ tin vào cấu trúc mã nguồn.
            var seen = new HashSet<string>();
            foreach (var t in scenarios.SelectMany(s => s.Tasks))
            {
                if (!seen.Add(t.TenTask))
                {
                    throw new InvalidOperationException(
                        $"SoeCorpusGenerator sinh trùng tên task base: '{t.TenTask}'. " +
                        "Đây là vi phạm bất biến PD-7 (unique base names) -- sửa bộ đếm seq trước khi dùng corpus này.");
                }
            }

            return scenarios;
        }

        private static SoeTaskDef NewTask(
            ref int seq, string subject, LoaiCongViec loai, int doKho,
            int deadlineOffsetDays, int minutesNeeded, double priority)
        {
            string tenTask = $"{TypeLabel(loai)} {subject} #{seq:D4}";
            seq++;
            return new SoeTaskDef
            {
                TenTask = tenTask,
                MonHoc = subject,
                LoaiTask = loai,
                DoKho = doKho,
                HanChot = Today.AddDays(deadlineOffsetDays),
                MinutesNeeded = Math.Max(minutesNeeded, 1),
                Priority = priority,
            };
        }

        private static string TypeLabel(LoaiCongViec loai) => loai switch
        {
            LoaiCongViec.BaiTapVeNha => "Bài tập",
            LoaiCongViec.KiemTraThuongXuyen => "Kiểm tra",
            LoaiCongViec.ThiGiuaKy => "Thi giữa kỳ",
            LoaiCongViec.DoAnCuoiKy => "Đồ án",
            LoaiCongViec.ThiCuoiKy => "Thi cuối kỳ",
            _ => "Việc",
        };

        private static SoeScenario BuildScenario(string id, string category, double capacityHours, List<SoeTaskDef> tasks)
        {
            bool feasible = EdfFeasibility.IsDesignedFeasible(tasks, capacityHours, Today);
            return new SoeScenario
            {
                Id = id,
                Category = category,
                DesignedFeasible = feasible,
                CapacityHours = capacityHours,
                Today = Today,
                Tasks = tasks,
            };
        }

        // ---- category: deadline inversion trap (40) ----
        // Task A: ưu tiên cao, hạn chót xa. Task B: ưu tiên thấp, hạn chót gần (trong cửa sổ,
        // không quá hạn) nhưng vẫn khả thi riêng lẻ. Allocator xếp theo DiemUuTien giảm dần và
        // không đọc HanChot (WorkloadServiceImpl.cs:118-141), nên A luôn được xếp trước B dù B
        // cần xong sớm hơn -- đây chính xác là cái bẫy T3.6a phải phơi ra.
        private static List<SoeScenario> GenerateInversionScenarios(ref int seq, ref int loaiIdx)
        {
            var result = new List<SoeScenario>();
            double[] capacityChoices = { 1.0, 1.5, 2.0 };
            int[] lateOffsets = { 10, 14, 18, 22 };
            int[] earlyOffsets = { 1, 2, 3, 4, 5, 6 };

            for (int i = 0; i < 40; i++)
            {
                double capacityHours = capacityChoices[i % capacityChoices.Length];
                int capMin = CapacityMath.ClampCapacityMinutes(capacityHours);
                string subject = Subjects[i % Subjects.Length];
                int lateOffset = lateOffsets[i % lateOffsets.Length];
                int earlyOffset = earlyOffsets[i % earlyOffsets.Length];

                var loaiA = AllTypes[loaiIdx++ % AllTypes.Length];
                var loaiB = AllTypes[loaiIdx++ % AllTypes.Length];

                int minutesA = 60 + (i % 4) * 30; // 60..150
                int minutesBRaw = 30 + (i % 3) * 30; // 30..90
                int minutesB = Math.Min(minutesBRaw, capMin * earlyOffset);
                if (minutesB < 15) minutesB = 15;

                var taskA = NewTask(ref seq, subject, loaiA, 3 + (i % 3), lateOffset, minutesA, priority: 90 - (i % 5));
                var taskB = NewTask(ref seq, subject, loaiB, 2 + (i % 3), earlyOffset, minutesB, priority: 30 + (i % 5));

                result.Add(BuildScenario($"INV-{i:D3}", "inversion", capacityHours, new List<SoeTaskDef> { taskA, taskB }));
            }

            return result;
        }

        // ---- category: ties (20) ----
        // Nhiều task chung đúng một hạn chót (và, ở nửa số case, chung cả điểm ưu tiên) --
        // phủ yêu cầu "ties" của execution plan §3.6.
        private static List<SoeScenario> GenerateTieScenarios(ref int seq, ref int loaiIdx)
        {
            var result = new List<SoeScenario>();

            for (int i = 0; i < 20; i++)
            {
                double capacityHours = (i % 2 == 0) ? 1.0 : 2.0;
                string subject = Subjects[(i + 3) % Subjects.Length];
                int deadlineOffset = 2 + (i % 6); // 2..7
                int taskCount = 2 + (i % 3); // 2..4
                bool tiePriorityToo = i % 2 == 0;
                double sharedPriority = 50 + (i % 10);

                var tasks = new List<SoeTaskDef>();
                for (int k = 0; k < taskCount; k++)
                {
                    var loai = AllTypes[loaiIdx++ % AllTypes.Length];
                    double pri = tiePriorityToo ? sharedPriority : 40 + k * 10 + (i % 5);
                    int minutes = 30 + k * 15;
                    tasks.Add(NewTask(ref seq, subject, loai, 1 + k, deadlineOffset, minutes, pri));
                }

                result.Add(BuildScenario($"TIE-{i:D3}", "tie", capacityHours, tasks));
            }

            return result;
        }

        // ---- category: overdue tasks (20) ----
        private static List<SoeScenario> GenerateOverdueScenarios(ref int seq, ref int loaiIdx)
        {
            var result = new List<SoeScenario>();
            int[] overdueOffsets = { -1, -2, -3, -5, -7, -10 };

            for (int i = 0; i < 20; i++)
            {
                double capacityHours = (i % 3 == 0) ? 1.0 : (i % 3 == 1) ? 1.5 : 2.0;
                string subject = Subjects[(i + 5) % Subjects.Length];
                int overdueOffset = overdueOffsets[i % overdueOffsets.Length];
                int normalOffset = 4 + (i % 8);

                var loai1 = AllTypes[loaiIdx++ % AllTypes.Length];
                var loai2 = AllTypes[loaiIdx++ % AllTypes.Length];

                int minutesOverdue = 45 + (i % 4) * 15;
                int minutesNormal = 60 + (i % 5) * 20;

                var t1 = NewTask(ref seq, subject, loai1, 2 + (i % 4), overdueOffset, minutesOverdue, priority: 40 + (i % 20));
                var t2 = NewTask(ref seq, subject, loai2, 2 + (i % 4), normalOffset, minutesNormal, priority: 60 + (i % 20));

                result.Add(BuildScenario($"ODUE-{i:D3}", "overdue", capacityHours, new List<SoeTaskDef> { t1, t2 }));
            }

            return result;
        }

        // ---- category: 7-day window boundary (20) ----
        // Quét offset qua ranh giới ngày 6/7 (cửa sổ ban đầu của allocator, WorkloadServiceImpl.cs
        // :121-126 tạo đúng 7 ngày đầu rồi mới mở thêm khi tràn).
        private static List<SoeScenario> GenerateWindowBoundaryScenarios(ref int seq, ref int loaiIdx)
        {
            var result = new List<SoeScenario>();
            int[] offsets = { 0, 3, 5, 6, 7, 8, 12, 20 };

            for (int i = 0; i < 20; i++)
            {
                double capacityHours = 1.0;
                string subject = Subjects[(i + 2) % Subjects.Length];
                int offsetIn = offsets[i % offsets.Length];

                var loai1 = AllTypes[loaiIdx++ % AllTypes.Length];
                var loai2 = AllTypes[loaiIdx++ % AllTypes.Length];

                int minutes1 = 40 + (i % 5) * 20;
                var t1 = NewTask(ref seq, subject, loai1, 1 + (i % 5), offsetIn, minutes1, priority: 50 + (i % 30));
                var t2 = NewTask(ref seq, subject, loai2, 1 + (i % 5), offsetIn + 3, 30, priority: 20 + (i % 30));

                result.Add(BuildScenario($"WIN-{i:D3}", "window-boundary", capacityHours, new List<SoeTaskDef> { t1, t2 }));
            }

            return result;
        }

        // ---- category: deliberately infeasible semesters (60) ----
        // Nhiều task dồn cùng một hạn chót rất gấp (1..4 ngày) với tổng khối lượng vượt xa
        // capacity*ngày -- bất khả thi CHẮC CHẮN theo EdfFeasibility, không phụ thuộc rng.
        private static List<SoeScenario> GenerateInfeasibleScenarios(ref int seq, ref int loaiIdx)
        {
            var result = new List<SoeScenario>();
            double[] capacityChoices = { 0.5, 1.0, 1.5 };

            for (int i = 0; i < 60; i++)
            {
                double capacityHours = capacityChoices[i % capacityChoices.Length];
                string subject = Subjects[i % Subjects.Length];
                int deadlineOffset = 1 + (i % 4); // 1..4 ngày -- rất gấp
                int taskCount = 3 + (i % 3); // 3..5 task cùng dồn về một hạn chót

                var tasks = new List<SoeTaskDef>();
                for (int k = 0; k < taskCount; k++)
                {
                    var loai = AllTypes[loaiIdx++ % AllTypes.Length];
                    int minutes = 120 + k * 40 + (i % 3) * 20;
                    tasks.Add(NewTask(ref seq, subject, loai, 3 + (k % 3), deadlineOffset, minutes, priority: 50 + k * 10));
                }

                result.Add(BuildScenario($"INF-{i:D3}", "infeasible", capacityHours, tasks));
            }

            return result;
        }

        // ---- category: capacity floor edge values (10) ----
        // capacityHours = 0.5h / 0.01h -- đúng giá trị đã pin bởi
        // GenerateSchedule_CapacityDuoiSan_BiKepVeMotGio_KhongTreo. Cả hai đều kẹp về 60 phút/ngày
        // qua ClampCapacityMinutes -- hành vi mong đợi, không phải bug.
        private static List<SoeScenario> GenerateCapacityEdgeScenarios(ref int seq, ref int loaiIdx)
        {
            var result = new List<SoeScenario>();
            double[] capacityChoices = { 0.5, 0.01 };

            for (int i = 0; i < 10; i++)
            {
                double capacityHours = capacityChoices[i % capacityChoices.Length];
                string subject = Subjects[(i + 6) % Subjects.Length];
                int deadlineOffset = 6 + (i % 10);
                int taskCount = 1 + (i % 3);

                var tasks = new List<SoeTaskDef>();
                for (int k = 0; k < taskCount; k++)
                {
                    var loai = AllTypes[loaiIdx++ % AllTypes.Length];
                    int minutes = 60 + k * 60; // 60 / 120 / 180 -- cùng bậc số với test pin
                    tasks.Add(NewTask(ref seq, subject, loai, 2 + k, deadlineOffset, minutes, priority: 55 + k * 5));
                }

                result.Add(BuildScenario($"CAP-{i:D3}", "capacity-edge", capacityHours, tasks));
            }

            return result;
        }

        // ---- category: seeded-random filler for variety + volume (60) ----
        private static List<SoeScenario> GenerateFillerScenarios(ref int seq, ref int loaiIdx, Random rng)
        {
            var result = new List<SoeScenario>();
            double[] capacityChoices = { 0.5, 1.0, 1.5, 2.0, 3.0, 4.0 };

            for (int i = 0; i < 60; i++)
            {
                double capacityHours = capacityChoices[rng.Next(capacityChoices.Length)];
                int subjectCount = 1 + rng.Next(3);
                var tasks = new List<SoeTaskDef>();

                for (int s = 0; s < subjectCount; s++)
                {
                    string subject = Subjects[rng.Next(Subjects.Length)];
                    int taskCount = 1 + rng.Next(4);

                    for (int k = 0; k < taskCount; k++)
                    {
                        var loai = AllTypes[loaiIdx++ % AllTypes.Length];
                        int doKho = 1 + rng.Next(5);
                        int deadlineOffset = -5 + rng.Next(31); // -5..25
                        int minutes = 20 + rng.Next(15) * 20; // 20..300
                        double priority = 1 + rng.NextDouble() * 99;
                        tasks.Add(NewTask(ref seq, subject, loai, doKho, deadlineOffset, minutes, priority));
                    }
                }

                result.Add(BuildScenario($"MIX-{i:D3}", "mixed-random", capacityHours, tasks));
            }

            return result;
        }
    }
}
