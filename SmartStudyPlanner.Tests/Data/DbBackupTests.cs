using System;
using System.IO;
using SmartStudyPlanner.Data;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>T1.8 risk mitigation: back up the live DB file before EnsureColumns runs.</summary>
    public class DbBackupTests : IDisposable
    {
        private readonly string _tempDir = Directory.CreateTempSubdirectory().FullName;

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public void CreateBackup_CopiesDbFileWithTimestampedName()
        {
            var dbPath = Path.Combine(_tempDir, "SmartStudyData.db");
            File.WriteAllText(dbPath, "fake-db-content");

            var backupPath = DbBackup.CreateBackup(dbPath, new DateTime(2026, 7, 5, 14, 30, 0));

            Assert.True(File.Exists(backupPath));
            Assert.Equal("fake-db-content", File.ReadAllText(backupPath));
            Assert.Contains("20260705-143000", backupPath);
            Assert.NotEqual(dbPath, backupPath);
        }

        [Fact]
        public void CreateBackup_WhenSourceMissing_ReturnsNullAndDoesNotThrow()
        {
            var dbPath = Path.Combine(_tempDir, "DoesNotExist.db");

            var backupPath = DbBackup.CreateBackup(dbPath, DateTime.UtcNow);

            Assert.Null(backupPath);
        }
    }
}
