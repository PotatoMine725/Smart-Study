using System;
using System.Collections.Generic;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services;
using SmartStudyPlanner.Services.Pipeline;
using SmartStudyPlanner.Services.Pipeline.Stages;
using SmartStudyPlanner.Services.RiskAnalyzer;
using SmartStudyPlanner.Services.Strategies;

namespace SmartStudyPlanner.Tests.Pipeline
{
    public class PipelineStageTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime Now { get; init; }
        }

        private sealed class StubDecisionEngine : IDecisionEngine
        {
            public WeightConfig Config { get; } = new();
            public double CalculatePriority(StudyTask task, MonHoc monHoc) => task.DoKho + monHoc.SoTinChi;
            public int CalculateRawSuggestedMinutes(StudyTask task) => 120;
            public string SuggestStudyTime(StudyTask task) => "2 giờ";
        }

        private sealed class StubRiskAnalyzer : IRiskAnalyzer
        {
            public RiskAssessment Assess(StudyTask task, MonHoc mon) => new()
            {
                Score = 0.7,
                Level = RiskLevel.High,
                DeadlineUrgencyScore = 0.5,
                ProgressGapScore = 0.2,
                PerformanceDropScore = 0.1
            };
        }

        private sealed class StubWorkloadService : IWorkloadService
        {
            public double GetCapacity() => 3.0;
            public void SaveCapacity(double capacity) { }
            public List<ScheduleDay> GenerateSchedule(HocKy hocKy, double capacityHours) => new()
            {
                new ScheduleDay { Date = DateTime.Today, DisplayName = "Hôm nay", TotalMinutes = 90 }
            };
        }

        [Fact]
        public void ParseInputStage_NormalizesInput()
        {
            var stage = new ParseInputStage();
            var ctx = new PipelineContext { RawInput = "  Thi cuối kỳ môn Toán  " };

            var result = stage.Execute(ctx);

            Assert.True(result.Success);
            Assert.Equal("Thi cuối kỳ môn Toán", ctx.ParsedInput!.NormalizedInput);
        }

        [Fact]
        public void PrioritizeStage_SortsByDecisionEngineScore()
        {
            var stage = new PrioritizeStage(new StubDecisionEngine());
            var semester = BuildSemester();
            var ctx = new PipelineContext { Semester = semester };

            var result = stage.Execute(ctx);

            Assert.True(result.Success);
            Assert.Single(ctx.PrioritizedTasks!);
            Assert.Equal("Task 1", ctx.PrioritizedTasks![0].TenTask);
        }

        [Fact]
        public void BalanceWorkloadStage_UsesWorkloadService()
        {
            var stage = new BalanceWorkloadStage(new StubWorkloadService());
            var ctx = new PipelineContext { Semester = BuildSemester(), Settings = new PipelineUserSettings { CapacityHours = 2.5 } };

            var result = stage.Execute(ctx);

            Assert.True(result.Success);
            Assert.Single(ctx.Schedule!);
            Assert.Equal(90, ctx.Schedule![0].TotalMinutes);
        }

        [Fact]
        public void AssessRiskStage_AssignsRiskReport()
        {
            var stage = new AssessRiskStage(new StubRiskAnalyzer());
            var ctx = new PipelineContext
            {
                Semester = BuildSemester(),
                Settings = new PipelineUserSettings { EnableRiskAssessment = true }
            };

            var result = stage.Execute(ctx);

            Assert.True(result.Success);
            Assert.Single(ctx.RiskReport!);
            Assert.Equal(RiskLevel.High, ctx.RiskReport![0].Level);
        }

        [Fact]
        public void AdaptStage_GeneratesSuggestionForLowProgress()
        {
            var semester = BuildSemester();
            semester.NgayBatDau = DateTime.Today.AddDays(-100);
            semester.DanhSachMonHoc[0].DanhSachTask.Clear();
            semester.DanhSachMonHoc[0].DanhSachTask.Add(new StudyTask("Pending 1", DateTime.Today.AddDays(2), LoaiCongViec.BaiTapVeNha, 1));
            semester.DanhSachMonHoc[0].DanhSachTask.Add(new StudyTask("Pending 2", DateTime.Today.AddDays(3), LoaiCongViec.BaiTapVeNha, 1));
            semester.DanhSachMonHoc[0].DanhSachTask.Add(new StudyTask("Pending 3", DateTime.Today.AddDays(4), LoaiCongViec.BaiTapVeNha, 1));

            var stage = new AdaptStage();
            var ctx = new PipelineContext
            {
                Semester = semester,
                ReferenceTime = DateTimeOffset.Now,
                Settings = new PipelineUserSettings { EnableAdaptation = true }
            };

            var result = stage.Execute(ctx);

            Assert.True(result.Success);
            Assert.NotEmpty(ctx.Adaptations!);
        }

        [Fact]
        public void AdaptStage_Uses_NgayKetThuc_WhenSet()
        {
            // 10-day semester; today is day 5 → expectedProgress = 50%
            // 0 tasks completed out of 4 → progress = 0% → below threshold → suggestion generated
            var start = new DateTime(2026, 1, 1);
            var hocKy = new HocKy("HK-Test", start);
            hocKy.NgayKetThuc = start.AddDays(10);

            var mon = new MonHoc("CNTT", 3) { MaHocKy = hocKy.MaHocKy };
            for (int i = 0; i < 4; i++)
                mon.DanhSachTask.Add(new StudyTask($"T{i}", start.AddDays(15), LoaiCongViec.BaiTapVeNha, 1));
            hocKy.DanhSachMonHoc.Add(mon);

            var ctx = new PipelineContext
            {
                Semester = hocKy,
                ReferenceTime = new DateTimeOffset(new DateTime(2026, 1, 6)) // day 5
            };
            var stage = new AdaptStage();

            var result = stage.Execute(ctx);

            Assert.True(result.Success);
            Assert.NotEmpty(ctx.Adaptations!);
            Assert.Contains(ctx.Adaptations!, a => a.RuleKey == "progress_below_expected");
        }

        [Fact]
        public void PipelineOrchestrator_RunsStagesInOrder()
        {
            var orchestrator = new PipelineOrchestrator(new IPipelineStage[]
            {
                new ParseInputStage(),
                new PrioritizeStage(new StubDecisionEngine()),
                new BalanceWorkloadStage(new StubWorkloadService()),
                new AssessRiskStage(new StubRiskAnalyzer()),
                new AdaptStage()
            });

            var ctx = new PipelineContext
            {
                Semester = BuildSemester(),
                RawInput = "  test  ",
                ReferenceTime = DateTimeOffset.Now,
                Settings = new PipelineUserSettings { EnableRiskAssessment = true, EnableAdaptation = true, CapacityHours = 2.5 }
            };

            var result = orchestrator.Execute(ctx);

            Assert.Equal(PipelineStatus.Completed, result.Status);
            Assert.Equal(5, result.StageResults.Count);
            Assert.NotNull(ctx.ParsedInput);
            Assert.NotEmpty(ctx.Schedule!);
            Assert.NotEmpty(ctx.RiskReport!);
        }

        [Fact]
        public void AssessRiskStage_SetsTaskId_OnEveryAssessment()
        {
            var start = new DateTime(2026, 1, 1);
            var hocKy = new HocKy("HK-Test", start);
            var mon = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var t1 = new StudyTask("Bài 1", start.AddDays(7), LoaiCongViec.BaiTapVeNha, 2);
            var t2 = new StudyTask("Bài 2", start.AddDays(3), LoaiCongViec.KiemTraThuongXuyen, 3);
            mon.DanhSachTask.Add(t1);
            mon.DanhSachTask.Add(t2);
            hocKy.DanhSachMonHoc.Add(mon);

            var riskAnalyzer = ServiceLocator.Get<IRiskAnalyzer>();
            var stage = new AssessRiskStage(riskAnalyzer);
            var ctx = new PipelineContext
            {
                Semester = hocKy,
                Settings = new PipelineUserSettings { EnableRiskAssessment = true },
                ReferenceTime = DateTimeOffset.Now
            };

            stage.Execute(ctx);

            Assert.NotNull(ctx.RiskReport);
            Assert.Equal(2, ctx.RiskReport!.Count);
            Assert.All(ctx.RiskReport, r => Assert.NotEqual(Guid.Empty, r.TaskId));
            Assert.Contains(ctx.RiskReport, r => r.TaskId == t1.MaTask);
            Assert.Contains(ctx.RiskReport, r => r.TaskId == t2.MaTask);
        }

        private static HocKy BuildSemester()
        {
            var semester = new HocKy("HK1", DateTime.Today.AddDays(-5));
            var mon = new MonHoc("Toán", 3);
            mon.DanhSachTask.Add(new StudyTask("Task 1", DateTime.Today.AddDays(3), LoaiCongViec.ThiCuoiKy, 4));
            semester.DanhSachMonHoc.Add(mon);
            return semester;
        }
    }
}
