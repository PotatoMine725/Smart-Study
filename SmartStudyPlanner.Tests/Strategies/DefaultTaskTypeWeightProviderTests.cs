using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;
using Xunit;

namespace SmartStudyPlanner.Tests.Strategies
{
    public class DefaultTaskTypeWeightProviderTests
    {
        private readonly DefaultTaskTypeWeightProvider _sut = new();

        [Theory]
        [InlineData(LoaiCongViec.ThiCuoiKy, 1.0)]
        [InlineData(LoaiCongViec.DoAnCuoiKy, 0.8)]
        [InlineData(LoaiCongViec.ThiGiuaKy, 0.6)]
        [InlineData(LoaiCongViec.KiemTraThuongXuyen, 0.3)]
        [InlineData(LoaiCongViec.BaiTapVeNha, 0.1)]
        public void GetWeight_KnownEnum_ReturnsExpected(LoaiCongViec loai, double expected)
        {
            Assert.Equal(expected, _sut.GetWeight(loai));
        }

        [Fact]
        public void GetWeight_UndefinedEnum_ReturnsFallback()
        {
            // enum cast giá trị không hợp lệ — provider phải fallback về 0.1
            var invalid = (LoaiCongViec)999;
            Assert.Equal(0.1, _sut.GetWeight(invalid));
        }
    }
}
