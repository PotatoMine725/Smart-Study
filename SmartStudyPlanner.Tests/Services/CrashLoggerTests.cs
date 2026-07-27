using System;
using System.IO;
using System.Threading.Tasks;
using SmartStudyPlanner.Services;
using Xunit;

namespace SmartStudyPlanner.Tests.Services
{
    public class CrashLoggerTests : IDisposable
    {
        private readonly string _origPath;
        private readonly string _tempPath;

        public CrashLoggerTests()
        {
            _origPath = CrashLogger.LogPath;
            _tempPath = Path.Combine(Path.GetTempPath(), $"crashlog-test-{Guid.NewGuid():N}.log");
            CrashLogger.LogPath = _tempPath;
        }

        public void Dispose()
        {
            CrashLogger.LogPath = _origPath;
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Fact]
        public void Log_WritesContextAndException()
        {
            CrashLogger.Log("unit-test", new InvalidOperationException("boom"));

            var content = File.ReadAllText(_tempPath);
            Assert.Contains("unit-test", content);
            Assert.Contains("boom", content);
        }

        [Fact]
        public async Task Observe_FaultedTask_LandsInCrashLog()
        {
            await CrashLogger.Observe(
                Task.FromException(new InvalidOperationException("bang")), "observe-test");

            var content = File.ReadAllText(_tempPath);
            Assert.Contains("observe-test", content);
            Assert.Contains("bang", content);
        }

        [Fact]
        public async Task Observe_SuccessfulTask_WritesNothing()
        {
            await CrashLogger.Observe(Task.CompletedTask, "no-fault");

            Assert.False(File.Exists(_tempPath));
        }
    }
}
