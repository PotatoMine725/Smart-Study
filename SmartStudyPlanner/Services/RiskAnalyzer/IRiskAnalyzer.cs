using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>
    /// Contract cho Risk Analyzer Engine.
    /// Inject interface này vào ViewModel thay vì tạo trực tiếp.
    /// </summary>
    public interface IRiskAnalyzer
    {
        /// <summary>
        /// Đánh giá rủi ro của một task theo công thức:
        /// Risk = DeadlineUrgency * 0.5 + ProgressGap * 0.3 + PerformanceDrop * 0.2
        /// </summary>
        RiskAssessment Assess(StudyTask task, MonHoc mon);
    }
}
