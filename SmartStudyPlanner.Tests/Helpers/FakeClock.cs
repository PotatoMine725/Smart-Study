using System;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Tests.Helpers
{
    public class FakeClock : IClock
    {
        public DateTime Now { get; set; }

        public FakeClock(DateTime now)
        {
            Now = now;
        }

        public FakeClock(int year, int month, int day)
            : this(new DateTime(year, month, day, 9, 0, 0))
        {
        }
    }
}
