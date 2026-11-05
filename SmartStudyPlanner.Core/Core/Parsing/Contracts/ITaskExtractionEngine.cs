using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Core.Parsing.Contracts
{
    public interface ITaskExtractionEngine
    {
        LoaiCongViec ExtractType(string lowerInput, LoaiCongViec defaultValue);
        int ExtractDifficulty(string lowerInput, int defaultValue);
    }
}
