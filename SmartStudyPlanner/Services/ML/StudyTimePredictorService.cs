using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML.Schema;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services.ML
{
    public class StudyTimePredictorService : IStudyTimePredictor
    {
        private readonly IMLModelManager _modelManager;

        public bool IsReady => _modelManager.IsReady;

        public StudyTimePredictorService(IMLModelManager modelManager)
        {
            _modelManager = modelManager;
        }

        public Task<StudyTimePredictionResult> PredictAsync(StudyTask task, MonHoc monHoc, CancellationToken ct = default)
        {
            if (!IsReady)
                return Task.FromResult(Fallback(task));

            var input = new Schema.StudyTimeInput
            {
                TaskType = task.LoaiTask.ToString(),
                Difficulty = task.DoKho,
                Credits = monHoc.SoTinChi,
                DaysLeft = task.HanChot > DateTime.Today
                    ? (float)(task.HanChot - DateTime.Today).TotalDays
                    : 7f,
                StudiedMinutesSoFar = task.ThoiGianDaHoc,
            };

            int predicted = _modelManager.PredictMinutes(input);
            if (predicted < 0)
                return Task.FromResult(Fallback(task));

            int formula = ComputeFormulaMinutes(task);
            float confidence = 1f - Math.Clamp(
                Math.Abs(predicted - formula) / (float)Math.Max(formula, 1),
                0f, 1f);

            if (confidence >= 0.6f)
                return Task.FromResult(new StudyTimePredictionResult(predicted, true, confidence));
            else
                return Task.FromResult(new StudyTimePredictionResult(formula, false, confidence));
        }

        /// <summary>Computes the formula-based estimate (same logic as Fallback, returns 0 for terminal tasks).</summary>
        private static int ComputeFormulaMinutes(StudyTask task)
        {
            if (task.TrangThai == StudyTaskStatus.HoanThanh || task.DiemUuTien <= 0)
                return 0;

            double baseMinutes = (task.DiemUuTien / 100.0) * 120.0;
            double difficultyBonus = (task.DoKho / 5.0) * 60.0;
            return (int)Math.Round((baseMinutes + difficultyBonus) / 15.0) * 15;
        }

        private static StudyTimePredictionResult Fallback(StudyTask task)
        {
            int minutes = ComputeFormulaMinutes(task);
            return new StudyTimePredictionResult(minutes, false, 0f);
        }
    }
}
