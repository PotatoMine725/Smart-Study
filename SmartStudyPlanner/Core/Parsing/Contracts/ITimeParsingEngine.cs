using System;

namespace SmartStudyPlanner.Core.Parsing.Contracts
{
    public interface ITimeParsingEngine
    {
        DateTime ParseDeadline(string lowerInput, DateTime defaultValue);
    }
}
