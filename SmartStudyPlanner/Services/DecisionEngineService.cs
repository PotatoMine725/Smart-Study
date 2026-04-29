using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Instance-based implementation của IDecisionEngine.
    /// Nhận PriorityCalculator qua constructor — hoàn toàn testable và injectable.
    /// </summary>
    public class DecisionEngineService : IDecisionEngine
    {
        private readonly PriorityCalculator _calculator;
        private readonly IStudyTimePredictor _studyTimePredictor;
        private WeightConfig _config;

        public WeightConfig Config => _config;

        public DecisionEngineService(
            ITaskTypeWeightProvider taskTypeProvider,
            IClock clock,
            IStudyTimePredictor studyTimePredictor,
            WeightConfig? initialConfig = null)
        {
            _config = initialConfig ?? new WeightConfig();
            _studyTimePredictor = studyTimePredictor;

            _calculator = new PriorityCalculator(
                cfgAccessor: () => _config,
                rules: new IUrgencyRule[]
                {
                    new OverdueRule(),
                    new JustOverdueRule(),
                    new ImminentRule(),
                    new CompletedRule(),
                    new BeyondHorizonRule(),
                },
                components: new IPriorityComponent[]
                {
                    new TimeComponent(),
                    new TaskTypeComponent(taskTypeProvider),
                    new CreditComponent(),
                    new DifficultyComponent(),
                },
                clock: clock);
        }

        public double CalculatePriority(StudyTask task, MonHoc monHoc)
        {
            // Self-heal config nếu trọng số không hợp lệ (tổng ≠ 1.0)
            if (!_config.IsValid())
                _config = new WeightConfig();

            return _calculator.Calculate(task, monHoc);
        }

        public int CalculateRawSuggestedMinutes(StudyTask task)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh || task.DiemUuTien <= 0) return 0;

            double baseMinutes = (task.DiemUuTien / 100.0) * 120.0;
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;

            int totalMinutes = (int)(baseMinutes + difficultyBonus);
            return (int)Math.Round(totalMinutes / 15.0) * 15;
        }

        public string SuggestStudyTime(StudyTask task)
        {
            int totalMinutes = CalculateRawSuggestedMinutes(task);
            if (totalMinutes == 0) return "0 phút";

            int remainingMinutes = totalMinutes - task.ThoiGianDaHoc;

            if (remainingMinutes <= 0) return "Đã đạt mục tiêu 🎉";
            if (remainingMinutes < 60) return $"{remainingMinutes} phút";

            int hours = remainingMinutes / 60;
            int mins = remainingMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}p" : $"{hours}h";
        }

        public int PredictStudyMinutes(StudyTask task, MonHoc monHoc, out bool isMlPrediction)
        {
            var result = _studyTimePredictor.PredictAsync(task, monHoc).GetAwaiter().GetResult();
            isMlPrediction = result.IsMLPrediction;
            return result.Minutes;
        }
    }
}
