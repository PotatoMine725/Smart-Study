using SmartStudyPlanner.Core.Scheduling.Contracts;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Core.Scheduling.Evaluators
{
    /// <summary>
    /// Thin wrapper quanh <see cref="PriorityCalculator"/> để expose qua Core contract.
    /// Không tự sửa WeightConfig — caller (SchedulingOrchestrator) chịu trách nhiệm self-heal.
    /// </summary>
    public sealed class PriorityEvaluator : IPriorityEvaluator
    {
        private readonly PriorityCalculator _calculator;

        public PriorityEvaluator(PriorityCalculator calculator)
        {
            _calculator = calculator;
        }

        public double Evaluate(StudyTask task, MonHoc monHoc) => _calculator.Calculate(task, monHoc);
    }
}
