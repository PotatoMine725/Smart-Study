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

    // Seam lưu trữ streak. Tách ra để loại tranh ghi file khi test chạy song song
    // và để unit-test luật streak không cần chạm đĩa.
    public interface IStreakStore
    {
        UserStreakData Load();
        void Save(UserStreakData data);
    }

    // Logic streak (đọc/cập nhật chuỗi ngày). Instance + injectable thay cho static cũ.
    public interface IStreakManager
    {
        UserStreakData GetCurrentStreak();
        void UpdateStreak();
    }

    // Production store: ghi JSON cạnh file database .db. Giữ nguyên byte/schema bản static cũ
    // (JsonSerializer.Serialize default options, guard File.Exists, try/catch -> data rỗng).
    public class JsonFileStreakStore : IStreakStore
    {
        private readonly string _filePath;

        public JsonFileStreakStore(string filePath)
        {
            _filePath = filePath;
        }

        public UserStreakData Load()
        {
            if (!File.Exists(_filePath)) return new UserStreakData();
            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<UserStreakData>(json) ?? new UserStreakData();
            }
            catch { return new UserStreakData(); }
        }

        public void Save(UserStreakData data)
        {
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(_filePath, json);
        }
    }

    public class StreakManager : IStreakManager
    {
        private readonly IStreakStore _store;
        private readonly IClock _clock;

        public StreakManager(IStreakStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        public UserStreakData GetCurrentStreak()
        {
            var data = _store.Load();

            // LỜI NGUYỀN CỦA STREAK: Nếu hôm nay mà cách ngày học cuối cùng LỚN HƠN 1 NGÀY -> Mất chuỗi!
            if (data.StreakCount > 0 && (_clock.Now.Date - data.LastStudyDate.Date).TotalDays > 1)
            {
                data.StreakCount = 0;
                _store.Save(data);
            }
            return data;
        }

        public void UpdateStreak()
        {
            var data = _store.Load();
            var today = _clock.Now.Date;

            if (data.LastStudyDate.Date == today) return; // Hôm nay đã được cộng chuỗi rồi thì thôi

            // Nếu học đúng hẹn (cách 1 ngày) thì cộng dồn, nếu bỏ bê lâu quá thì bắt đầu lại = 1
            if ((today - data.LastStudyDate.Date).TotalDays == 1)
                data.StreakCount++;
            else
                data.StreakCount = 1;

            data.LastStudyDate = today;
            _store.Save(data);
        }
    }
}
