using System;
using System.IO;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.ML
{
    public class LocalModelStorageTests : IDisposable
    {
        private readonly string _tempRoot =
            Path.Combine(Path.GetTempPath(), "ssp-modelstore-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void CreatesModelDirectory()
        {
            var provider = new LocalModelStorageProvider(_tempRoot);

            Assert.True(Directory.Exists(provider.BaseDirectory));
            Assert.Equal(_tempRoot, provider.BaseDirectory);
        }

        [Fact]
        public void DefaultBaseDirectory_TroToProfileAppData()
        {
            // Không construct provider mặc định ở đây — chỉ khẳng định hình dạng đường dẫn,
            // để test không bao giờ ghi vào profile thật của người chạy.
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SmartStudyPlanner", "models");

            Assert.Equal(expected, LocalModelStorageProvider.DefaultBaseDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
