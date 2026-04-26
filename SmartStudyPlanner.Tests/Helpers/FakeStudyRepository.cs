using System.Collections.Generic;
using System.Threading.Tasks;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Tests.Helpers
{
    internal class FakeStudyRepository : IStudyRepository
    {
        public List<StudyLog> AddedLogs { get; } = new();

        public Task<HocKy> DocHocKyAsync() => Task.FromResult<HocKy>(null);
        public Task<List<HocKy>> LayDanhSachHocKyAsync() => Task.FromResult(new List<HocKy>());
        public Task LuuHocKyAsync(HocKy hocKy) => Task.CompletedTask;

        public Task AddStudyLogAsync(StudyLog log)
        {
            AddedLogs.Add(log);
            return Task.CompletedTask;
        }

        public Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy) => Task.FromResult(new List<StudyLog>());
    }
}
