using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Services.ML;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.ML
{
    public class StudyTimeTrainingDataSourceTests
    {
        private static StubOutcomeLogRepository MakeRepo(int count, LoaiCongViec taskType = LoaiCongViec.BaiTapVeNha)
        {
            var entries = Enumerable.Range(0, count).Select(i => new StudyTimeOutcomeLog
            {
                Id                  = Guid.NewGuid(),
                CreatedUtc          = DateTime.UtcNow,
                TaskType            = (int)taskType,
                Difficulty          = 2f,
                Credits             = 3f,
                DaysLeft            = 5f,
                StudiedMinutesSoFar = (float)i,
                ActualMinutes       = 45f + i,
                WasMlPrediction     = false,
            }).ToList();
            return new StubOutcomeLogRepository(entries);
        }

        [Fact]
        public async Task BelowMinRows_ReturnsEmpty()
        {
            var src = new StudyTimeTrainingDataSource(MakeRepo(StudyTimeTrainingDataSource.MinRows - 1));
            var result = await src.BuildAsync();
            Assert.Empty(result);
        }

        [Fact]
        public async Task AtMinRows_ReturnsFullSet()
        {
            var src = new StudyTimeTrainingDataSource(MakeRepo(StudyTimeTrainingDataSource.MinRows));
            var result = await src.BuildAsync();
            Assert.Equal(StudyTimeTrainingDataSource.MinRows, result.Count);
        }

        [Fact]
        public async Task Mapping_LabelEqualsActualMinutes()
        {
            var src = new StudyTimeTrainingDataSource(MakeRepo(StudyTimeTrainingDataSource.MinRows));
            var result = await src.BuildAsync();
            Assert.All(result, r => Assert.True(r.Label >= 45f));
        }

        [Fact]
        public async Task Mapping_TaskTypeStringMatchesEnum()
        {
            var src = new StudyTimeTrainingDataSource(MakeRepo(StudyTimeTrainingDataSource.MinRows, LoaiCongViec.ThiCuoiKy));
            var result = await src.BuildAsync();
            Assert.All(result, r => Assert.Equal(LoaiCongViec.ThiCuoiKy.ToString(), r.TaskType));
        }

        [Fact]
        public async Task Mapping_FeatureFieldsRoundTrip()
        {
            var src = new StudyTimeTrainingDataSource(MakeRepo(StudyTimeTrainingDataSource.MinRows));
            var result = await src.BuildAsync();
            var first = result[0];
            Assert.Equal(2f, first.Difficulty);
            Assert.Equal(3f, first.Credits);
            Assert.Equal(5f, first.DaysLeft);
        }

        private sealed class StubOutcomeLogRepository : IStudyTimeOutcomeLogRepository
        {
            private readonly List<StudyTimeOutcomeLog> _rows;
            public StubOutcomeLogRepository(List<StudyTimeOutcomeLog> rows) => _rows = rows;
            public Task AddAsync(StudyTimeOutcomeLog entry, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<StudyTimeOutcomeLog>> GetAllAsync(CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(_rows);
            public Task<IReadOnlyList<StudyTimeOutcomeLog>> GetSinceAsync(DateTime since, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<StudyTimeOutcomeLog>>(_rows);
            public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_rows.Count);
        }
    }
}
