using System;
using System.IO;
using System.Text.Json;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Tests.TestDoubles;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    // Unit-test luật streak qua InMemoryStreakStore + FakeClock — không chạm đĩa,
    // hết tranh ghi streak_data.json khi chạy song song.
    public class StreakManagerTests
    {
        private static readonly DateTime Today = new DateTime(2026, 6, 27);

        private static StreakManager Build(IStreakStore store)
            => new StreakManager(store, new FakeClock(Today.Year, Today.Month, Today.Day));

        [Fact]
        public void UpdateStreak_CongDon_KhiCachDungMotNgay()
        {
            var store = new InMemoryStreakStore(new UserStreakData
            {
                StreakCount = 3,
                LastStudyDate = Today.AddDays(-1)
            });
            var sut = Build(store);

            sut.UpdateStreak();

            var data = store.Load();
            Assert.Equal(4, data.StreakCount);
            Assert.Equal(Today, data.LastStudyDate.Date);
        }

        [Fact]
        public void UpdateStreak_ResetVeMot_KhiBoBeQuaMotNgay()
        {
            var store = new InMemoryStreakStore(new UserStreakData
            {
                StreakCount = 5,
                LastStudyDate = Today.AddDays(-3)
            });
            var sut = Build(store);

            sut.UpdateStreak();

            var data = store.Load();
            Assert.Equal(1, data.StreakCount);
            Assert.Equal(Today, data.LastStudyDate.Date);
        }

        [Fact]
        public void UpdateStreak_KhongCongLanHai_KhiDaHocTrongNgay()
        {
            var store = new InMemoryStreakStore(new UserStreakData
            {
                StreakCount = 3,
                LastStudyDate = Today
            });
            var sut = Build(store);

            sut.UpdateStreak();

            var data = store.Load();
            Assert.Equal(3, data.StreakCount);
        }

        [Fact]
        public void UpdateStreak_BatDauTuMot_KhiChuaCoLichSu()
        {
            var store = new InMemoryStreakStore(); // LastStudyDate = MinValue
            var sut = Build(store);

            sut.UpdateStreak();

            var data = store.Load();
            Assert.Equal(1, data.StreakCount);
            Assert.Equal(Today, data.LastStudyDate.Date);
        }

        [Fact]
        public void GetCurrentStreak_MatChuoi_KhiQuaHan()
        {
            var store = new InMemoryStreakStore(new UserStreakData
            {
                StreakCount = 5,
                LastStudyDate = Today.AddDays(-3)
            });
            var sut = Build(store);

            var result = sut.GetCurrentStreak();

            Assert.Equal(0, result.StreakCount);
            Assert.Equal(0, store.Load().StreakCount); // được lưu lại
        }

        [Fact]
        public void GetCurrentStreak_GiuChuoi_KhiConTrongHan()
        {
            var store = new InMemoryStreakStore(new UserStreakData
            {
                StreakCount = 7,
                LastStudyDate = Today.AddDays(-1)
            });
            var sut = Build(store);

            var result = sut.GetCurrentStreak();

            Assert.Equal(7, result.StreakCount);
        }

        // Chốt "byte output giống bản static cũ": JsonFileStreakStore round-trip + serialize default.
        [Fact]
        public void JsonFileStreakStore_RoundTrip_GiuNguyenGiaTriVaByte()
        {
            string path = Path.Combine(Path.GetTempPath(), $"streak_{Guid.NewGuid():N}.json");
            try
            {
                var store = new JsonFileStreakStore(path);
                var data = new UserStreakData { StreakCount = 9, LastStudyDate = Today };

                store.Save(data);

                // Byte trên đĩa khớp serialize default options (compact) như StreakManager static cũ.
                Assert.Equal(JsonSerializer.Serialize(data), File.ReadAllText(path));

                var loaded = store.Load();
                Assert.Equal(9, loaded.StreakCount);
                Assert.Equal(Today, loaded.LastStudyDate.Date);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void JsonFileStreakStore_Load_TraVeRong_KhiFileChuaTonTai()
        {
            string path = Path.Combine(Path.GetTempPath(), $"streak_{Guid.NewGuid():N}.json");
            var store = new JsonFileStreakStore(path);

            var data = store.Load();

            Assert.Equal(0, data.StreakCount);
            Assert.Equal(DateTime.MinValue, data.LastStudyDate);
        }
    }
}
