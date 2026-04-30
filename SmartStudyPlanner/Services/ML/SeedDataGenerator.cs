using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML.Schema;
using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Services.ML
{
    public static class SeedDataGenerator
    {
        private const int Seed = 42;

        public static List<StudyTimeInput> Generate()
        {
            var rng = new Random(Seed);
            var data = new List<StudyTimeInput>(180);

            AddGroup(data, rng, 60, 1.5f, 1.5f, 8f, 20f, 60f);
            AddGroup(data, rng, 60, 3f, 3f, 5f, 60f, 120f);
            AddGroup(data, rng, 60, 4.5f, 4.5f, 2f, 120f, 240f);

            return data;
        }

        private static void AddGroup(List<StudyTimeInput> data, Random rng, int count, float difficulty, float credits, float daysLeft, float minMinutes, float maxMinutes)
        {
            for (int i = 0; i < count; i++)
            {
                var label = (float)(minMinutes + rng.NextDouble() * (maxMinutes - minMinutes));
                var noise = 1f + ((float)rng.NextDouble() - 0.5f) * 0.3f;
                data.Add(new StudyTimeInput
                {
                    TaskType = difficulty >= 4 ? LoaiCongViec.ThiCuoiKy.ToString() : LoaiCongViec.BaiTapVeNha.ToString(),
                    Difficulty = difficulty,
                    Credits = credits,
                    DaysLeft = daysLeft,
                    StudiedMinutesSoFar = 0,
                    Label = label * noise
                });
            }
        }
    }
}
