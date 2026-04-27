using System.Threading.Tasks;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.MLTests
{
    public class StudyTimePredictorTests
    {
        [Fact]
        public async Task PredictAsync_FallsBack_WhenModelNotReady()
        {
            var manager = new StubModelManager { Ready = false };
            var predictor = new StudyTimePredictorService(manager);
            var task = new StudyTask("A", System.DateTime.Today.AddDays(1), LoaiCongViec.BaiTapVeNha, 3) { DiemUuTien = 50 };
            var mon = new MonHoc("M", 3);

            var result = await predictor.PredictAsync(task, mon);

            Assert.False(result.IsMLPrediction);
            Assert.True(result.Minutes >= 0);
        }

        private sealed class StubModelManager : IMLModelManager
        {
            public bool Ready { get; set; }
            public bool IsReady => Ready;
            public Task InitializeAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
            public Task RetrainAsync(System.Collections.Generic.IReadOnlyList<Services.ML.Schema.StudyTimeInput> data, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
            public Task<float> EvaluateR2Async(System.Threading.CancellationToken ct = default) => Task.FromResult(0.5f);
            public int PredictMinutes(Services.ML.Schema.StudyTimeInput input) => -1;
        }
    }
}
