using System;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    public class WeightConfig
    {
        public double TimeWeight { get; set; } = 0.40;
        public double TaskTypeWeight { get; set; } = 0.30;
        public double CreditWeight { get; set; } = 0.20;
        public double DifficultyWeight { get; set; } = 0.10;
        public int MaxCredits { get; set; } = 4;
        public int MaxDifficulty { get; set; } = 5;
        public int HorizonDays { get; set; } = 60;

        // BẢO MẬT 1: Kiểm tra tổng trọng số có bằng 1.0 (100%) hay không
        // Dùng sai số 0.001 vì phép cộng số thập phân (double) trong C# có thể bị lệch một chút
        public bool IsValid()
        {
            return Math.Abs(TimeWeight + TaskTypeWeight + CreditWeight + DifficultyWeight - 1.0) < 0.001;
        }
    }

    /// <summary>
    /// Static facade giữ nguyên để tương thích ngược với 7 call sites hiện tại.
    /// Mọi logic thực tế đã chuyển sang DecisionEngineService (instance-based, injectable).
    /// Không thêm logic mới vào đây — hãy thêm vào IDecisionEngine / DecisionEngineService.
    /// </summary>
    public static class DecisionEngine
    {
        // Config vẫn được giữ ở đây vì một số ViewModel đọc trực tiếp HorizonDays.
        // DecisionEngineService nhận config qua cfgAccessor lambda nên luôn đồng bộ.
        public static WeightConfig Config { get; set; } = new WeightConfig();

        private static readonly ITaskTypeWeightProvider _taskTypeProvider = new DefaultTaskTypeWeightProvider();
        private static readonly IClock _clock = new SystemClock();

        private static readonly PriorityCalculator _calculator = new PriorityCalculator(
            cfgAccessor: () => Config,
            rules: new IUrgencyRule[]
            {
                new OverdueRule(),
                new JustOverdueRule(),
                new ImminentRule(),
                new CompletedRule(),
                new BeyondHorizonRule(),
            },
            components: new IPriorityComponent[]
            {
                new TimeComponent(),
                new TaskTypeComponent(_taskTypeProvider),
                new CreditComponent(),
                new DifficultyComponent(),
            },
            clock: _clock);

        public static double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            // BẢO MẬT: Facade là nơi duy nhất chịu trách nhiệm self-heal static Config.
            // Cần mutate tại đây (không chỉ dùng bản local) vì các ViewModel khác đọc
            // DecisionEngine.Config.HorizonDays trực tiếp — nếu không persist fallback,
            // chỗ khác sẽ vẫn thấy config sai.
            if (!Config.IsValid())
            {
                Config = new WeightConfig();
            }

            return _calculator.Calculate(task, monHoc);
        }

        // HÀM MỚI 1: Trả về con số phút thô (int) để vẽ biểu đồ
        public static int CalculateRawSuggestedMinutes(StudyTask task)
        {
            if (task.TrangThai == "Hoàn thành" || task.DiemUuTien <= 0) return 0;

            double baseMinutes = (task.DiemUuTien / 100.0) * 120.0;
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;

            int totalMinutes = (int)(baseMinutes + difficultyBonus);
            return (int)Math.Round(totalMinutes / 15.0) * 15;
        }

        // HÀM MỚI 2: Dùng lại hàm trên để format ra chuỗi chữ cho DataGrid
        public static string SuggestStudyTime(StudyTask task)
        {
            int totalMinutes = CalculateRawSuggestedMinutes(task);
            if (totalMinutes == 0) return "0 phút";

            // Trừ đi thời gian người dùng đã cày cuốc
            int remainingMinutes = totalMinutes - task.ThoiGianDaHoc;

            // Nếu đã học đủ hoặc dư thời gian
            if (remainingMinutes <= 0) return "Đã đạt mục tiêu 🎉";

            if (remainingMinutes < 60) return $"{remainingMinutes} phút";

            int hours = remainingMinutes / 60;
            int mins = remainingMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}p" : $"{hours}h";
        }
    }
}

