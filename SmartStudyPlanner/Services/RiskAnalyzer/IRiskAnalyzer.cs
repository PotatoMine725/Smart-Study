using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.RiskAnalyzer
{
    /// <summary>
    /// Contract cho Risk Analyzer Engine.
    /// Inject interface này vào ViewModel thay vì tạo trực tiếp.
    /// </summary>
    public interface IRiskAnalyzer
    {
        RiskAssessment Assess(StudyTask task, MonHoc mon);
    }
}
