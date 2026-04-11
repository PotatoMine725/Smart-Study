using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;

namespace SmartStudyPlanner.Services.Strategies
{
    public class PriorityCalculator
    {
        private readonly Func<WeightConfig> _cfgAccessor;
        private readonly IReadOnlyList<IUrgencyRule> _rules;
        private readonly IReadOnlyList<IPriorityComponent> _components;
        private readonly IClock _clock;

        public PriorityCalculator(
            Func<WeightConfig> cfgAccessor,
            IReadOnlyList<IUrgencyRule> rules,
            IReadOnlyList<IPriorityComponent> components,
            IClock clock)
        {
            _cfgAccessor = cfgAccessor;
            _rules = rules;
            _components = components;
            _clock = clock;
        }

        public double Calculate(StudyTask task, MonHoc mon)
        {
            if (task == null || mon == null) return 0.0;

            var cfg = _cfgAccessor();
            if (!cfg.IsValid())
            {
                cfg = new WeightConfig();
            }

            double daysLeft = (task.HanChot.Date - _clock.Now.Date).TotalDays;

            foreach (var rule in _rules)
            {
                if (rule.TryApply(task, daysLeft, cfg, out var early))
                    return Math.Round(early, 2);
            }

            double total = 0;
            foreach (var c in _components)
            {
                total += c.Score(task, mon, cfg) * c.Weight(cfg);
            }

            return Math.Round(total, 2);
        }
    }
}
