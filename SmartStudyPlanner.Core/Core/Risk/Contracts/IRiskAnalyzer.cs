using SmartStudyPlanner.Core.Risk.Models;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Risk.Contracts
{
    public interface IRiskAnalyzer
    {
        RiskAssessment Assess(StudyTask task, MonHoc mon);
    }
}
