using System;

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

        /// <summary>Sàn mềm áp cho mỗi trọng số TRƯỚC khi scale, tránh một trọng số bị đẩy về ~0.</summary>
        public const double MinWeight = 0.05;

        // Sai số 0.001 vì double arithmetic trong C# có thể lệch nhẹ
        public bool IsValid() =>
            Math.Abs(TimeWeight + TaskTypeWeight + CreditWeight + DifficultyWeight - 1.0) < 0.001;

        /// <summary>
        /// Clamp sàn (<see cref="MinWeight"/>) cho 4 trọng số rồi scale về tổng = 1.0. Mutate object này.
        /// Dùng làm guardrail sau khi WeightOptimizer điều chỉnh. Nếu tổng suy biến (&lt;= 0) thì reset default.
        /// </summary>
        public void Normalize()
        {
            TimeWeight = Math.Max(TimeWeight, MinWeight);
            TaskTypeWeight = Math.Max(TaskTypeWeight, MinWeight);
            CreditWeight = Math.Max(CreditWeight, MinWeight);
            DifficultyWeight = Math.Max(DifficultyWeight, MinWeight);

            double sum = TimeWeight + TaskTypeWeight + CreditWeight + DifficultyWeight;
            if (sum <= 0)
            {
                TimeWeight = 0.40; TaskTypeWeight = 0.30; CreditWeight = 0.20; DifficultyWeight = 0.10;
                return;
            }

            TimeWeight /= sum;
            TaskTypeWeight /= sum;
            CreditWeight /= sum;
            DifficultyWeight /= sum;
        }
    }
}
