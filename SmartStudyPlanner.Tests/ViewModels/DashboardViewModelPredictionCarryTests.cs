using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using SmartStudyPlanner.Core.ML.Contracts;
using SmartStudyPlanner.Core.Risk.Contracts;
using SmartStudyPlanner.Core.Risk.Models;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.ML;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// DFD-9a — guards the hop that actually broke.
    ///
    /// The prediction and its confidence were computed correctly and then discarded twice on the
    /// way to the write site. <c>FocusViewModelOutcomeLogTests</c> only proves that FocusViewModel
    /// maps whatever <see cref="TaskDashboardItem"/> hands it; it cannot show that the dashboard
    /// puts anything there. Before this file existed, deleting the two assignments in
    /// <c>BuildDashboardSummary</c> left the whole suite green — verified by mutation on
    /// 2026-08-26, which is why the file exists.
    /// </summary>
    public class DashboardViewModelPredictionCarryTests
    {
        private const int PredictedMinutes = 73;
        private const float PredictedConfidence = 0.64f;

        [Fact]
        public void Dashboard_CarriesPredictionAndConfidence_OntoTaskItem()
        {
            var vm = BuildVm(PredictedMinutes, isMl: true, confidence: PredictedConfidence);

            var item = Assert.Single(vm.Top5Task);
            Assert.True(item.IsMLPrediction);
            Assert.Equal(PredictedMinutes, item.PredictedMinutes);
            Assert.Equal(PredictedConfidence, item.Confidence);
        }

        [Fact]
        public void Dashboard_CarriesConfidence_EvenWhenPredictionWasRejected()
        {
            // Confidence < 0.6: the predictor returns the formula estimate with IsMLPrediction
            // false. The number still has to reach the item, or the rejected population -- the one
            // a calibration study needs most -- is invisible in telemetry.
            var vm = BuildVm(minutes: 30, isMl: false, confidence: 0.41f);

            var item = Assert.Single(vm.Top5Task);
            Assert.False(item.IsMLPrediction);
            Assert.Equal(30, item.PredictedMinutes);
            Assert.Equal(0.41f, item.Confidence);
        }

        private static DashboardViewModel BuildVm(int minutes, bool isMl, float confidence)
        {
            var hocKy = new HocKy("HK Test", DateTime.Today);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            mon.DanhSachTask.Add(new StudyTask("BT1", DateTime.Today.AddDays(4), LoaiCongViec.BaiTapVeNha, 3)
            {
                MaMonHoc = mon.MaMonHoc,
                MucDoCanhBao = "An toàn",
            });
            hocKy.DanhSachMonHoc.Add(mon);

            return new DashboardViewModel(
                hocKy,
                new StubHocKyRepository(),
                new StubDecisionEngine(minutes, isMl, confidence),
                new StubWorkloadService(),
                new StubRiskAnalyzer(),
                new StubPipelineOrchestrator(),
                new StubTelemetry(),
                new StubStreakManager());
        }

        private sealed class StubDecisionEngine : IDecisionEngine
        {
            private readonly StudyTimePredictionResult _result;
            public StubDecisionEngine(int minutes, bool isMl, float confidence)
                => _result = new StudyTimePredictionResult(minutes, isMl, confidence);

            public WeightConfig Config { get; } = new WeightConfig();
            public double CalculatePriority(StudyTask task, MonHoc monHoc) => 42.0;
            public int CalculateRawSuggestedMinutes(StudyTask task) => 60;
            public string SuggestStudyTime(StudyTask task) => "60 phút";
            public StudyTimePredictionResult PredictStudyMinutes(StudyTask task, MonHoc monHoc) => _result;
            public Task<WeightConfigSuggestion?> SuggestWeightConfigAsync(CancellationToken ct = default)
                => Task.FromResult<WeightConfigSuggestion?>(null);
        }

        private sealed class StubPipelineOrchestrator : IPipelineOrchestrator
        {
            // Empty RiskReport on purpose: BuildDashboardSummary then falls back to IRiskAnalyzer,
            // which keeps this harness independent of the risk pipeline's own shape.
            public PipelineExecutionResult Execute(PipelineContext context)
                => new PipelineExecutionResult { Status = PipelineStatus.Completed };
        }

        private sealed class StubRiskAnalyzer : IRiskAnalyzer
        {
            public RiskAssessment Assess(StudyTask task, MonHoc mon)
                => new RiskAssessment { TaskId = task.MaTask, Score = 10.0, Level = RiskLevel.Low };
        }

        private sealed class StubWorkloadService : IWorkloadService
        {
            public double GetCapacity() => 3.0;
            public void SaveCapacity(double capacity) { }
            public List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours) => new();
        }

        private sealed class StubHocKyRepository : IHocKyRepository
        {
            public Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default)
                => Task.FromResult(new List<HocKy>());
            public Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class StubTelemetry : IStudyTelemetry
        {
            public void Track(string eventName, IDictionary<string, string>? properties = null) { }
        }

        private sealed class StubStreakManager : IStreakManager
        {
            public UserStreakData GetCurrentStreak() => new UserStreakData { StreakCount = 0 };
            public void UpdateStreak() { }
        }
    }
}
