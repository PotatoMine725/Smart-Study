using System;
using System.IO;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    public class LocalModelStorageTests
    {
        [Fact]
        public void CreatesModelDirectory()
        {
            var provider = new LocalModelStorageProvider();
            Assert.True(Directory.Exists(provider.BaseDirectory));
        }
    }
}
