using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Models;

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
        int PredictStudyMinutes(StudyTask task, MonHoc monHoc, out bool isMlPrediction);

        /// <summary>
        /// M8-B: đề xuất WeightConfig dựa trên thống kê người dùng. READ-ONLY — KHÔNG mutate Config.
        /// Trả null nếu WeightOptimizer không được inject (offline/disabled).
        /// </summary>
        Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default);
    }
}
