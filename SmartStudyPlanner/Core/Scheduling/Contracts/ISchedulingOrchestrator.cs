using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;

namespace SmartStudyPlanner.Core.Scheduling.Contracts
{
    /// <summary>
    /// Compose-port cho toàn bộ luồng scheduling: priority -> raw minutes -> suggestion -> ML predict.
    /// Slice 2 sẽ implement; hiện chỉ khai báo để Services/DecisionEngineService có thể adapt sau.
    /// </summary>
    public interface ISchedulingOrchestrator
    {
        double CalculatePriority(StudyTask task, MonHoc monHoc);
        int CalculateRawSuggestedMinutes(StudyTask task);
        string SuggestStudyTime(StudyTask task);
        /// <summary>
        /// Dự đoán phút học kèm ngữ cảnh môn học. Trả nguyên <see cref="StudyTimePredictionResult"/>
        /// (Minutes + IsMLPrediction + Confidence) thay vì <c>int</c> + <c>out bool</c>: chữ ký cũ
        /// không có chỗ chứa Confidence nên đã âm thầm vứt nó đi, và đó chính là gốc của defect
        /// DFD-9a (StudyTimeOutcomeLog ghi PredictedMinutes/Confidence = null).
        /// </summary>
        StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc);

        /// <summary>
        /// M8-B: đề xuất WeightConfig dựa trên thống kê người dùng. READ-ONLY — KHÔNG mutate Config.
        /// Trả null nếu WeightOptimizer không được inject (offline/disabled).
        /// </summary>
        Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default);
    }
}
