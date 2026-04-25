using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Contract cho Decision Engine — tính điểm ưu tiên và gợi ý thời gian học.
    /// Inject interface này vào ViewModel thay vì gọi static DecisionEngine trực tiếp.
    /// </summary>
    public interface IDecisionEngine
    {
        /// <summary>Cấu hình trọng số hiện tại.</summary>
        WeightConfig Config { get; }

        /// <summary>Tính điểm ưu tiên cho task trong ngữ cảnh môn học.</summary>
        double CalculatePriority(StudyTask task, MonHoc monHoc);

        /// <summary>Số phút học thô (chưa trừ thời gian đã học) — dùng vẽ biểu đồ.</summary>
        int CalculateRawSuggestedMinutes(StudyTask task);

        /// <summary>Chuỗi gợi ý thời gian còn lại (đã trừ ThoiGianDaHoc) cho DataGrid.</summary>
        string SuggestStudyTime(StudyTask task);
    }
}
