using System;
using System.IO;
using System.Text.Json;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    public class UserStreakData
    {
        public int StreakCount { get; set; } = 0;
        public DateTime LastStudyDate { get; set; } = DateTime.MinValue;
    }

    public static class StreakManager
    {
        // File lưu trữ chuỗi ngày sẽ nằm cạnh file database .db
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "streak_data.json");

        private static IClock _clock = new SystemClock();

        private static UserStreakData Load()
        {
            if (!File.Exists(FilePath)) return new UserStreakData();
            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<UserStreakData>(json) ?? new UserStreakData();
            }
            catch { return new UserStreakData(); }
        }

        private static void Save(UserStreakData data)
        {
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(FilePath, json);
        }

        public static UserStreakData GetCurrentStreak()
        {
            var data = Load();

            // LỜI NGUYỀN CỦA STREAK: Nếu hôm nay mà cách ngày học cuối cùng LỚN HƠN 1 NGÀY -> Mất chuỗi!
            if (data.StreakCount > 0 && (_clock.Now.Date - data.LastStudyDate.Date).TotalDays > 1)
            {
                data.StreakCount = 0;
                Save(data);
            }
            return data;
        }

        public static void UpdateStreak()
        {
            var data = Load();
            var today = _clock.Now.Date;

            if (data.LastStudyDate.Date == today) return; // Hôm nay đã được cộng chuỗi rồi thì thôi

            // Nếu học đúng hẹn (cách 1 ngày) thì cộng dồn, nếu bỏ bê lâu quá thì bắt đầu lại = 1
            if ((today - data.LastStudyDate.Date).TotalDays == 1)
                data.StreakCount++;
            else
                data.StreakCount = 1;

            data.LastStudyDate = today;
            Save(data);
        }
    }
}