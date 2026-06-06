using System;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Services;

namespace SmartStudyPlanner.Services.ML.WeightOptimizer
{
    /// <summary>
    /// Rule-based MVP cho M8-B: ánh xạ <see cref="UserStatsSnapshot"/> → đề xuất <see cref="WeightConfig"/>.
    /// Hàm THUẦN, deterministic, không I/O — unit-test trực tiếp bằng snapshot dựng tay.
    /// Không có model ML: dự án chưa log lịch sử weight→outcome nên không có ground-truth thật.
    /// </summary>
    public static class WeightRuleEngine
    {
        /// <summary>Không bao giờ dịch quá 15% tuyệt đối về phía TimeWeight trong một lần đề xuất.</summary>
        public const double MaxShift = 0.15;

        public static WeightConfigSuggestion Compute(WeightConfig current, UserStatsSnapshot stats)
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(stats);

            // --- Tín hiệu "đang trễ" → ưu tiên thời gian (urgency). ---
            double missRate = Clamp01(stats.MissRate);
            double delayNorm = Clamp01(stats.AverageDelayDays / 7.0);   // trễ >=7 ngày => max
            double pressure = Clamp01(0.6 * missRate + 0.4 * delayNorm);

            // Chuỗi tập trung dài = kỷ luật tốt → giảm bớt mức nudge (tối đa giảm 50%).
            double streakRelief = Clamp01(stats.FocusStreakDays / 14.0);
            double effective = pressure * (1.0 - 0.5 * streakRelief);
            double shift = MaxShift * effective;

            var suggested = Clone(current);
            double others = current.TaskTypeWeight + current.CreditWeight + current.DifficultyWeight;
            if (shift > 0 && others > 0)
            {
                // Lấy 'shift' từ 3 trọng số còn lại theo tỷ lệ kích thước hiện tại, dồn vào Time.
                suggested.TimeWeight = current.TimeWeight + shift;
                suggested.TaskTypeWeight = current.TaskTypeWeight - shift * (current.TaskTypeWeight / others);
                suggested.CreditWeight = current.CreditWeight - shift * (current.CreditWeight / others);
                suggested.DifficultyWeight = current.DifficultyWeight - shift * (current.DifficultyWeight / others);
            }

            // Guardrail: clamp sàn + scale về tổng 1.0; last-line nếu vẫn lệch thì giữ current.
            suggested.Normalize();
            if (!suggested.IsValid())
                suggested = Clone(current);

            return new WeightConfigSuggestion
            {
                Suggested = suggested,
                Confidence = ComputeConfidence(stats),
                Rationale = BuildRationale(shift, missRate, stats),
            };
        }

        /// <summary>
        /// Confidence = độ ĐỦ dữ liệu (không phải R²). Dữ liệu càng nhiều → càng tự tin.
        /// Khớp ngưỡng policy 0.60/0.75: ~15 task + ~450 phút/30 ngày ≈ 0.75.
        /// </summary>
        public static double ComputeConfidence(UserStatsSnapshot stats)
        {
            double taskConf = Clamp01(stats.TotalTaskCount / 20.0);
            double activityConf = Clamp01(stats.TotalStudyMinutesLast30Days / 600.0);
            double confidence = 0.5 * taskConf + 0.5 * activityConf;

            // Dữ liệu quá thưa → ép xuống dưới ngưỡng review để Slice 8 không surface.
            if (stats.TotalTaskCount < 5)
                confidence = Math.Min(confidence, 0.40);

            return Clamp01(confidence);
        }

        private static string BuildRationale(double shift, double missRate, UserStatsSnapshot stats)
        {
            if (shift < 0.005)
                return "Lịch học ổn định — giữ nguyên trọng số.";

            return $"MissRate {missRate:P0}, trễ TB {stats.AverageDelayDays:F1} ngày → +Time {shift:F2}.";
        }

        private static WeightConfig Clone(WeightConfig c) => new()
        {
            TimeWeight = c.TimeWeight,
            TaskTypeWeight = c.TaskTypeWeight,
            CreditWeight = c.CreditWeight,
            DifficultyWeight = c.DifficultyWeight,
            MaxCredits = c.MaxCredits,
            MaxDifficulty = c.MaxDifficulty,
            HorizonDays = c.HorizonDays,
        };

        private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
    }
}
