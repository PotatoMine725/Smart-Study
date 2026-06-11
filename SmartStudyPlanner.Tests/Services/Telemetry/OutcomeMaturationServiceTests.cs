using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services.Telemetry;
using SmartStudyPlanner.Tests.TestDoubles;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Telemetry
{
    public class OutcomeMaturationServiceTests
    {
        private static readonly DateTime ApplyTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime NowAfterWindow = ApplyTime.AddDays(15); // 15d > 14d window
        private static readonly DateTime NowBeforeWindow = ApplyTime.AddDays(10); // 10d < 14d window

        private static WeightChangeLog MakeLog(IEnumerable<Guid>? cohort = null) => new()
        {
            Id = Guid.NewGuid(),
            AppliedUtc = ApplyTime,
            OutcomeWindowDays = 14,
            CohortTaskIdsJson = JsonSerializer.Serialize(cohort ?? Array.Empty<Guid>()),
        };

        private static OutcomeMaturationService Build(
            FakeWeightChangeLogRepository logRepo,
            FakeStudyTaskRepository taskRepo)
            => new(logRepo, taskRepo);

        // ── core maturation ──────────────────────────────────────────────────

        [Fact]
        public async Task MatureAsync_FillsOutcome_ForExpiredLog()
        {
            var openTask = new StudyTask("open", ApplyTime.AddDays(5).ToLocalTime(), LoaiCongViec.BaiTapVeNha, 2);
            // Not completed → counted as missed

            var log = MakeLog(new[] { openTask.MaTask });
            var logRepo = new FakeWeightChangeLogRepository();
            logRepo.Seed(new[] { log });
            var taskRepo = new FakeStudyTaskRepository();
            taskRepo.Seed(new[] { openTask });

            var svc = Build(logRepo, taskRepo);
            int count = await svc.MatureAsync(NowAfterWindow);

            Assert.Equal(1, count);
            Assert.NotNull(log.OutcomeMaturedUtc);
            Assert.Equal(NowAfterWindow, log.OutcomeMaturedUtc!.Value);
            Assert.Equal(0, log.OutcomeCompletedInWindow);
            Assert.Equal(1.0, log.OutcomeMissRate);
        }

        [Fact]
        public async Task MatureAsync_Skips_LogNotYetExpired()
        {
            var log = MakeLog();
            var logRepo = new FakeWeightChangeLogRepository();
            logRepo.Seed(new[] { log });

            var svc = Build(logRepo, new FakeStudyTaskRepository());
            int count = await svc.MatureAsync(NowBeforeWindow);

            Assert.Equal(0, count);
            Assert.Null(log.OutcomeMaturedUtc);
        }

        [Fact]
        public async Task MatureAsync_Idempotent_SecondCallDoesNothing()
        {
            var log = MakeLog();
            var logRepo = new FakeWeightChangeLogRepository();
            logRepo.Seed(new[] { log });

            var svc = Build(logRepo, new FakeStudyTaskRepository());
            await svc.MatureAsync(NowAfterWindow);
            int secondCount = await svc.MatureAsync(NowAfterWindow);

            Assert.Equal(0, secondCount);
        }

        // ── cohort filtering ──────────────────────────────────────────────────

        [Fact]
        public async Task MatureAsync_OnlyCountsCohortTasks_NotAllTasks()
        {
            var cohortTask = new StudyTask("cohort", ApplyTime.AddDays(5).ToLocalTime(), LoaiCongViec.BaiTapVeNha, 2);
            var outsideTask = new StudyTask("outside", ApplyTime.AddDays(5).ToLocalTime(), LoaiCongViec.BaiTapVeNha, 2);
            // Both uncompleted — only cohortTask should count

            var log = MakeLog(new[] { cohortTask.MaTask }); // outsideTask NOT in cohort
            var logRepo = new FakeWeightChangeLogRepository();
            logRepo.Seed(new[] { log });
            var taskRepo = new FakeStudyTaskRepository();
            taskRepo.Seed(new[] { cohortTask, outsideTask });

            var svc = Build(logRepo, taskRepo);
            await svc.MatureAsync(NowAfterWindow);

            // MissRate based on 1 cohort task, not 2 total tasks
            Assert.Equal(1.0, log.OutcomeMissRate); // 1 missed / 1 cohort
        }

        [Fact]
        public async Task MatureAsync_CompletedInWindow_CountedCorrectly()
        {
            var completionTime = ApplyTime.AddDays(8).ToLocalTime(); // within 14d window
            var task = new StudyTask("done", ApplyTime.AddDays(12).ToLocalTime(), LoaiCongViec.ThiCuoiKy, 4);
            task.TrangThai = StudyTaskStatus.HoanThanh;
            task.NgayHoanThanh = completionTime;

            var log = MakeLog(new[] { task.MaTask });
            var logRepo = new FakeWeightChangeLogRepository();
            logRepo.Seed(new[] { log });
            var taskRepo = new FakeStudyTaskRepository();
            taskRepo.Seed(new[] { task });

            var svc = Build(logRepo, taskRepo);
            await svc.MatureAsync(NowAfterWindow);

            Assert.Equal(1, log.OutcomeCompletedInWindow);
            Assert.Equal(0.0, log.OutcomeMissRate); // 0 missed / 1 cohort
        }

        [Fact]
        public async Task MatureAsync_EmptyCohort_ZeroMissRate()
        {
            var log = MakeLog(Array.Empty<Guid>());
            var logRepo = new FakeWeightChangeLogRepository();
            logRepo.Seed(new[] { log });

            var svc = Build(logRepo, new FakeStudyTaskRepository());
            await svc.MatureAsync(NowAfterWindow);

            Assert.NotNull(log.OutcomeMaturedUtc);
            Assert.Equal(0.0, log.OutcomeMissRate);
            Assert.Equal(0, log.OutcomeCompletedInWindow);
        }
    }
}
