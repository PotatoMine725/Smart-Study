using System;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests
{
    public class AnalyticsServiceTests
    {
        [Fact]
        public void StudyLog_HasRequiredProperties_AndGeneratesId()
        {
            var log = new StudyLog
            {
                MaTask       = Guid.NewGuid(),
                NgayHoc      = DateTime.Today,
                SoPhutHoc    = 25,
                SoPhutDuKien = 30,
                DaHoanThanh  = true
            };
            Assert.Equal(25, log.SoPhutHoc);
            Assert.True(log.DaHoanThanh);
            Assert.NotEqual(Guid.Empty, log.Id);
        }

        [Fact]
        public void StudyTask_NgayHoanThanh_IsNullByDefault()
        {
            var task = new StudyTask("Test", DateTime.Today, LoaiCongViec.BaiTapVeNha, 1);
            Assert.Null(task.NgayHoanThanh);
        }
    }
}
