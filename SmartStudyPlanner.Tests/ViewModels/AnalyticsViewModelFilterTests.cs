using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Services.Analytics;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.ViewModels
{
    /// <summary>
    /// Epic 1 reopen / Step 2 — guards the stale-render fix: switching to a filter with
    /// no logs must clear every chart output, not leave the previous filter's data on screen
    /// under the "Không có dữ liệu" banner.
    /// </summary>
    public class AnalyticsViewModelFilterTests
    {
        [Fact]
        public async Task SwitchingToSubjectWithNoLogs_ClearsStaleCharts()
        {
            // Arrange: subject "A" has a task with a study log today; subject "B" has none.
            var hocKy = new HocKy("HK", DateTime.Today);

            var monA = new MonHoc("A", 3);
            var taskA = new StudyTask("Bài A", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 2);
            monA.DanhSachTask.Add(taskA);
            hocKy.DanhSachMonHoc.Add(monA);

            var monB = new MonHoc("B", 3);
            monB.DanhSachTask.Add(new StudyTask("Bài B", DateTime.Today.AddDays(7), LoaiCongViec.BaiTapVeNha, 2));
            hocKy.DanhSachMonHoc.Add(monB);

            var repo = new SeededStudyLogRepository(new List<StudyLog>
            {
                new StudyLog { MaTask = taskA.MaTask, NgayHoc = DateTime.Today, SoPhutHoc = 10 }
            });

            var vm = new AnalyticsViewModel(hocKy, repo, new StudyAnalyticsService(), new NullStudyTelemetry());

            // Act 1: default filter "Tất cả" renders subject A's data.
            await vm.LoadAsync();

            // Precondition — the data-bearing filter actually populated the charts.
            Assert.True(vm.HasData);
            Assert.NotEmpty(vm.WeeklyChartSeries);
            Assert.NotEmpty(vm.HeatmapCells);
            Assert.NotEqual(string.Empty, vm.WeeklyNarrative);

            // Act 2: switch to subject "B", which has no logs in range.
            vm.SelectedSubject = "B";

            // Assert: the empty state is honored AND no stale chart data survives.
            Assert.False(vm.HasData);
            Assert.Equal("Không có dữ liệu cho bộ lọc hiện tại.", vm.EmptyStateMessage);
            Assert.Empty(vm.WeeklyChartSeries);
            Assert.Empty(vm.SubjectChartSeries);
            Assert.Empty(vm.SubjectInsights);
            Assert.Empty(vm.HeatmapCells);
            Assert.Equal(string.Empty, vm.WeeklyNarrative);
            Assert.Equal(string.Empty, vm.RecommendedNextAction);
            Assert.Equal(0, vm.ProductivityValue);
        }

        // Single-use doubles (test-structure convention: one-off stubs inline).

        private sealed class SeededStudyLogRepository : IStudyLogRepository
        {
            private readonly List<StudyLog> _logs;
            public SeededStudyLogRepository(List<StudyLog> logs) => _logs = logs;

            public Task<List<StudyLog>> GetForHocKyAsync(HocKy hocKy, CancellationToken ct = default)
                => Task.FromResult(_logs);

            public Task AddAsync(StudyLog log, CancellationToken ct = default) => Task.CompletedTask;
            public Task<List<StudyLog>> GetByTaskAsync(Guid maTask, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetSinceAsync(DateTime since, CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
            public Task<List<StudyLog>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<StudyLog>());
        }

        private sealed class NullStudyTelemetry : SmartStudyPlanner.Services.Telemetry.IStudyTelemetry
        {
            public void Track(string eventName, IDictionary<string, string>? properties = null) { }
        }
    }
}
