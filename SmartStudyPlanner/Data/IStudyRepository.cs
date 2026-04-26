using System.Threading.Tasks;
using SmartStudyPlanner.Models;
using System.Collections.Generic;

namespace SmartStudyPlanner.Data
{
    // Interface này như một bản hợp đồng, quy định các hành động mà CSDL phải có
    public interface IStudyRepository
    {
        // Chữ Task và Async đại diện cho việc chạy ngầm (Bất đồng bộ)
        Task<HocKy> DocHocKyAsync();
        Task<List<HocKy>> LayDanhSachHocKyAsync();
        Task LuuHocKyAsync(HocKy hocKy);
        Task AddStudyLogAsync(StudyLog log);
        Task<List<StudyLog>> GetStudyLogsAsync(HocKy hocKy);

        // M6.1 — Task Notes & Study Links
        Task<TaskEditorBundle?> GetTaskEditorBundleAsync(Guid taskId);
        Task UpsertTaskNoteAsync(Guid taskId, string? content);
        Task<List<TaskReferenceLink>> GetTaskReferenceLinksAsync(Guid taskId);
        Task AddTaskReferenceLinkAsync(TaskReferenceLink link);
        Task UpdateTaskReferenceLinkAsync(TaskReferenceLink link);
        Task DeleteTaskReferenceLinkAsync(Guid linkId);
        Task SaveTaskEditorBundleAsync(TaskEditorBundle bundle);
    }
}