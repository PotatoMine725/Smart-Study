using SmartStudyPlanner.Services;

namespace SmartStudyPlanner.Tests.TestDoubles
{
    // Store streak trong RAM để unit-test luật streak không chạm đĩa
    // (loại tranh ghi streak_data.json khi test chạy song song).
    public class InMemoryStreakStore : IStreakStore
    {
        private UserStreakData _data;

        public InMemoryStreakStore(UserStreakData? seed = null)
        {
            _data = seed ?? new UserStreakData();
        }

        public UserStreakData Load() => new UserStreakData
        {
            StreakCount = _data.StreakCount,
            LastStudyDate = _data.LastStudyDate
        };

        public void Save(UserStreakData data)
        {
            _data = new UserStreakData
            {
                StreakCount = data.StreakCount,
                LastStudyDate = data.LastStudyDate
            };
        }
    }
}
