using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Port cho HocKy aggregate root (load/save toàn cây học kỳ).
    /// Thay thế các method học kỳ của <see cref="Data.IStudyRepository"/>.
    /// </summary>
    public interface IHocKyRepository
    {
        Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default);
        Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default);
    }
}
