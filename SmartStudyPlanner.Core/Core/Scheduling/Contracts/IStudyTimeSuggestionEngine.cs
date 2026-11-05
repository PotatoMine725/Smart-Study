using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Scheduling.Contracts
{
    public interface IStudyTimeSuggestionEngine
    {
        string Suggest(StudyTask task);
    }
}
