using System;

namespace SmartStudyPlanner.Services.Strategies
{
    // Abstraction cho thời gian hiện tại để test được deterministic.
    // Why: DateTime.Now là một static global, không mock được, khiến
    // PriorityCalculator + SmartParser không thể viết unit test ổn định.
    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
