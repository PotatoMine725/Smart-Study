using SmartStudyPlanner.Core.Scheduling.Engines;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Core.Scheduling
{
    public class RawMinutesCalculatorTests
    {
        private readonly RawMinutesCalculator _sut = new();

        [Fact]
        public void Calculate_TaskHoanThanh_TraVe0()
        {
            var task = new StudyTask { TrangThai = StudyTaskStatus.HoanThanh, DiemUuTien = 80, DoKho = 4 };
            Assert.Equal(0, _sut.Calculate(task));
        }

        [Fact]
        public void Calculate_DiemUuTienBangHoacNho0_TraVe0()
        {
            var task = new StudyTask { TrangThai = StudyTaskStatus.ChuaLam, DiemUuTien = 0, DoKho = 5 };
            Assert.Equal(0, _sut.Calculate(task));
        }

        [Fact]
        public void Calculate_LuonLamTronVeBoiSo15()
        {
            // 50/100 * 120 = 60, 3/5 * 60 = 36 → 96 → round/15*15 = 105
            var task = new StudyTask { TrangThai = StudyTaskStatus.ChuaLam, DiemUuTien = 50, DoKho = 3 };
            int result = _sut.Calculate(task);
            Assert.Equal(0, result % 15);
        }

        [Fact]
        public void Calculate_DiemUuTienVaDoKhoCao_TraVeGiaTriHopLy()
        {
            // 100/100*120 + 5/5*60 = 180 → 180
            var task = new StudyTask { TrangThai = StudyTaskStatus.ChuaLam, DiemUuTien = 100, DoKho = 5 };
            Assert.Equal(180, _sut.Calculate(task));
        }
    }
}
