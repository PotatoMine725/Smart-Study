using SmartStudyPlanner.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services.ML
{
    public interface IStudyTimePredictor
    {
        bool IsReady { get; }
        Task<StudyTimePredictionResult> PredictAsync(StudyTask task, MonHoc monHoc, CancellationToken ct = default);
    }

    public sealed record StudyTimePredictionResult(int Minutes, bool IsMLPrediction, float Confidence);
}
