using System;
using System.Collections.Generic;

namespace SmartStudyPlanner.Services.Strategies
{
    public interface IDeadlineKeywordParser
    {
        DateTime Parse(string lowerInput, DateTime defaultValue);
    }

    // Parser deadline có 2 phase, giữ đúng hành vi bản gốc SmartParser:
    //   Phase 1: relative date ("hôm nay" / "mai" / "mốt" / "tuần sau") — first match wins
    //   Phase 2: day-of-week ("thứ 2" ... "chủ nhật") — override phase 1 nếu có match
    // "tuần sau" kết hợp với "thứ X" = bump thêm 7 ngày
    public class DefaultDeadlineKeywordParser : IDeadlineKeywordParser
    {
        private readonly IClock _clock;

        public DefaultDeadlineKeywordParser(IClock clock)
        {
            _clock = clock;
        }

        public DateTime Parse(string lowerInput, DateTime defaultValue)
        {
            var now = _clock.Now;
            var result = defaultValue;

            var relativeRules = new IKeywordRule<DateTime>[]
            {
                new ContainsAnyRule<DateTime>(() => now,             "hôm nay", "nay", "tối nay"),
                new ContainsAnyRule<DateTime>(() => now.AddDays(1),  "ngày mai", "mai"),
                new ContainsAnyRule<DateTime>(() => now.AddDays(2),  "ngày mốt", "mốt"),
                new ContainsAnyRule<DateTime>(() => now.AddDays(7),  "tuần sau", "tuan sau"),
            };

            foreach (var rule in relativeRules)
            {
                if (rule.TryMatch(lowerInput, out var d))
                {
                    result = d;
                    break;
                }
            }

            bool laTuanSau = lowerInput.Contains("tuần sau");

            var dayRules = new (DayOfWeek day, string[] keywords)[]
            {
                (DayOfWeek.Monday,    new[] { "thứ 2", "t2" }),
                (DayOfWeek.Tuesday,   new[] { "thứ 3", "t3" }),
                (DayOfWeek.Wednesday, new[] { "thứ 4", "t4" }),
                (DayOfWeek.Thursday,  new[] { "thứ 5", "t5" }),
                (DayOfWeek.Friday,    new[] { "thứ 6", "t6" }),
                (DayOfWeek.Saturday,  new[] { "thứ 7", "t7" }),
                (DayOfWeek.Sunday,    new[] { "chủ nhật", "cn" }),
            };

            foreach (var (day, keywords) in dayRules)
            {
                foreach (var k in keywords)
                {
                    if (lowerInput.Contains(k))
                    {
                        result = LayNgayCuaThu(day, laTuanSau, now);
                        return result;
                    }
                }
            }

            return result;
        }

        private static DateTime LayNgayCuaThu(DayOfWeek thuCanTim, bool laTuanSau, DateTime homNay)
        {
            int daysUntil = ((int)thuCanTim - (int)homNay.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7; // Nếu hnay thứ 3 mà bảo "thứ 3" thì là thứ 3 tuần sau

            DateTime ngayTimDuoc = homNay.AddDays(daysUntil);
            if (laTuanSau) ngayTimDuoc = ngayTimDuoc.AddDays(7);

            return ngayTimDuoc;
        }
    }
}
