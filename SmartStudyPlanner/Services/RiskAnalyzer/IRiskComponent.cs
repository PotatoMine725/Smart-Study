using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>
    /// Strategy interface cho một thành phần rủi ro.
    /// Score() trả về giá trị trong [0.0, 1.0].
    /// </summary>
    public interface IRiskComponent
    {
        double Score(StudyTask task, MonHoc mon, IClock clock);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Thành phần 1: Deadline Urgency  (trọng số 0.5 trong công thức tổng)
    // Công thức: DeadlineUrgency = 1 / (DaysLeft + 1)
    // ────────────────────────────────────────────────────────────────────────────
    public class DeadlineUrgencyRisk : IRiskComponent
    {
        public double Score(StudyTask task, MonHoc mon, IClock clock)
        {
            if (task.TrangThai == "Hoàn thành") return 0.0;

            double daysLeft = (task.HanChot.Date - clock.Now.Date).TotalDays;
            if (daysLeft < 0) return 1.0;          // Đã quá hạn → cực kỳ nguy hiểm

            return 1.0 / (daysLeft + 1);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Thành phần 2: Progress Gap  (trọng số 0.3)
    // Công thức: ProgressGap = 1 - ProgressPercent
    // ProgressPercent = ThoiGianDaHoc / SuggestedMinutes (capped at 1)
    // ────────────────────────────────────────────────────────────────────────────
    public class ProgressGapRisk : IRiskComponent
    {
        private readonly IDecisionEngine _engine;

        public ProgressGapRisk(IDecisionEngine engine) => _engine = engine;

        public double Score(StudyTask task, MonHoc mon, IClock clock)
        {
            if (task.TrangThai == "Hoàn thành") return 0.0;

            int suggested = _engine.CalculateRawSuggestedMinutes(task);
            if (suggested <= 0) return 0.0;

            double progress = System.Math.Min(1.0, task.ThoiGianDaHoc / (double)suggested);
            return 1.0 - progress;
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Thành phần 3: Performance Drop  (trọng số 0.2)
    // MVP: dùng DoKho làm proxy cho độ khó tiềm năng (normalize về [0,1]).
    // Khi có dữ liệu điểm số thực tế (v2) sẽ thay thế.
    // ────────────────────────────────────────────────────────────────────────────
    public class PerformanceDropRisk : IRiskComponent
    {
        private const int MaxDifficulty = 5;

        public double Score(StudyTask task, MonHoc mon, IClock clock)
        {
            if (task.TrangThai == "Hoàn thành") return 0.0;

            int clamped = System.Math.Max(1, System.Math.Min(MaxDifficulty, task.DoKho));
            return (clamped - 1) / (double)(MaxDifficulty - 1); // [0, 1]
        }
    }
}
