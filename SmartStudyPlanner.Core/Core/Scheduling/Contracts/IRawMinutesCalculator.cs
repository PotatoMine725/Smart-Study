using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Scheduling.Contracts
{
    public interface IRawMinutesCalculator
    {
        int Calculate(StudyTask task);
    }
}
