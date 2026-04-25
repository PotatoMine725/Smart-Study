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

        // Sai số 0.001 vì double arithmetic trong C# có thể lệch nhẹ
        public bool IsValid() =>
            Math.Abs(TimeWeight + TaskTypeWeight + CreditWeight + DifficultyWeight - 1.0) < 0.001;
    }
}
