using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Port cho MonHoc aggregate.
    /// </summary>
    public interface IMonHocRepository
    {
        Task<MonHoc?> GetAsync(Guid maMonHoc, CancellationToken ct = default);
        Task<List<MonHoc>> GetByHocKyAsync(Guid maHocKy, CancellationToken ct = default);
    }
}
