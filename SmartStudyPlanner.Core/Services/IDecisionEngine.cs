using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;

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

        /// <summary>
        /// Trả về kết quả dự đoán phút học (Minutes + IsMLPrediction + Confidence) với ngữ cảnh môn học.
        /// Confidence đi kèm để write-site telemetry ghi lại được cái ĐÃ dự đoán, không chỉ việc CÓ dự đoán.
        /// </summary>
        StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc);

        /// <summary>
        /// M8-B: đề xuất WeightConfig dựa trên thống kê người dùng (read-only, không tự apply).
        /// Trả null nếu WeightOptimizer không khả dụng.
        /// </summary>
        Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default);
    }
}
