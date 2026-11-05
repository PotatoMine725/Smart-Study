using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Scheduling.Contracts
{
    public interface IPriorityEvaluator
    {
        double Evaluate(StudyTask task, MonHoc monHoc);
    }
}
