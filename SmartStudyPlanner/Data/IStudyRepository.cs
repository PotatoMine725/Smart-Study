using System.Threading.Tasks;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Data
{
    // Interface này như một bản hợp đồng, quy định các hành động mà CSDL phải có
    public interface IStudyRepository
    {
        // Chữ Task và Async đại diện cho việc chạy ngầm (Bất đồng bộ)
        Task<HocKy> DocHocKyAsync();
        Task LuuHocKyAsync(HocKy hocKy);
    }
}