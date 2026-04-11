using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface ITaskTypeWeightProvider
    {
        double GetWeight(LoaiCongViec loai);
    }

    public class DefaultTaskTypeWeightProvider : ITaskTypeWeightProvider
    {
        private readonly IReadOnlyDictionary<LoaiCongViec, double> _map =
            new Dictionary<LoaiCongViec, double>
            {
                [LoaiCongViec.ThiCuoiKy]          = 1.0,
                [LoaiCongViec.DoAnCuoiKy]         = 0.8,
                [LoaiCongViec.ThiGiuaKy]          = 0.6,
                [LoaiCongViec.KiemTraThuongXuyen] = 0.3,
                [LoaiCongViec.BaiTapVeNha]        = 0.1,
            };

        public double GetWeight(LoaiCongViec loai) =>
            _map.TryGetValue(loai, out var w) ? w : 0.1;
    }
}
