using SmartStudyPlanner.Services;
using System.IO;

namespace SmartStudyPlanner.Tests.Persistence
{
    public class WeightConfigStoreTests : IDisposable
    {
        private readonly string _tmpPath = Path.Combine(Path.GetTempPath(), $"wcs_test_{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        [Fact]
        public void Load_WhenFileNotFound_ReturnsDefaults()
        {
            var cfg = WeightConfigStore.LoadFrom(_tmpPath);

            Assert.Equal(0.40, cfg.TimeWeight, precision: 6);
            Assert.Equal(0.30, cfg.TaskTypeWeight, precision: 6);
            Assert.Equal(0.20, cfg.CreditWeight, precision: 6);
            Assert.Equal(0.10, cfg.DifficultyWeight, precision: 6);
        }

        [Fact]
        public void Save_ThenLoad_RoundtripsAllFields()
        {
            var original = new WeightConfig
            {
                TimeWeight = 0.50,
                TaskTypeWeight = 0.25,
                CreditWeight = 0.15,
                DifficultyWeight = 0.10,
                MaxCredits = 5,
                MaxDifficulty = 3,
                HorizonDays = 45
            };

            WeightConfigStore.SaveTo(original, _tmpPath);
            var loaded = WeightConfigStore.LoadFrom(_tmpPath);

            Assert.Equal(original.TimeWeight, loaded.TimeWeight, precision: 6);
            Assert.Equal(original.TaskTypeWeight, loaded.TaskTypeWeight, precision: 6);
            Assert.Equal(original.CreditWeight, loaded.CreditWeight, precision: 6);
            Assert.Equal(original.DifficultyWeight, loaded.DifficultyWeight, precision: 6);
            Assert.Equal(original.MaxCredits, loaded.MaxCredits);
            Assert.Equal(original.MaxDifficulty, loaded.MaxDifficulty);
            Assert.Equal(original.HorizonDays, loaded.HorizonDays);
        }

        [Fact]
        public void Load_WhenFileCorrupted_ReturnsDefaults()
        {
            File.WriteAllText(_tmpPath, "not valid json }{");
            var cfg = WeightConfigStore.LoadFrom(_tmpPath);

            Assert.Equal(0.40, cfg.TimeWeight, precision: 6);
        }
    }
}
