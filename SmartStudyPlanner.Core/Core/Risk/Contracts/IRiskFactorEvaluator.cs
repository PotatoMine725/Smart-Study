using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Risk.Contracts
{
    public interface IRiskFactorEvaluator
    {
        string Name { get; }
        double Evaluate(StudyTask task, MonHoc mon, IClock clock);
    }
}
