using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Services.Soe;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Services.Soe
{
    /// <summary>
    /// T3.7 (Epic 3, Card G) — per-seam unit tests cho <see cref="OptimizerRunLogWriter"/>, trên một
    /// <see cref="OptimizeReport"/> tổng hợp (không qua allocator/optimizer thật — đó là việc của
    /// <c>SoeT34CorpusReportTests</c>' integration run). Kiểm ba điều writer PHẢI đúng: (1) đúng số
    /// hàng = tổng checkpoint qua mọi pass, (2) field cấp-lời-gọi/cấp-pass lặp lại đúng trên mọi hàng
    /// liên quan, (3) <c>Reason</c> null cho C₀, có giá trị cho C₁..C_N.
    /// </summary>
    public class OptimizerRunLogWriterTests
    {
        private static readonly DateTime FixedNow = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        private static OptimizeReport BuildTwoPassReport()
        {
            // Pass 1: C0 (violations=1)->C1 (violations=0, Selected) -- k*=1.
            var pass1 = new OptimizerPassReport(
                KStar: 1,
                Checkpoints: new List<OptimizerCheckpoint>
                {
                    new(0, new Eval(1, 30, -1.0), null),
                    new(1, new Eval(0, 0, 0.5), OptimizerCheckpointReason.Selected),
                });

            // Pass 2: C0 (= pass1's committed state) -> C1 không cải thiện -- k*=0.
            var pass2 = new OptimizerPassReport(
                KStar: 0,
                Checkpoints: new List<OptimizerCheckpoint>
                {
                    new(0, new Eval(0, 0, 0.5), null),
                    new(1, new Eval(0, 0, 0.5), OptimizerCheckpointReason.Rejected_NoImprovement),
                });

            return new OptimizeReport(
                Termination: OptimizerTermination.Terminated_FixedPoint,
                PassCount: 2,
                Passes: new List<OptimizerPassReport> { pass1, pass2 });
        }

        [Fact]
        public void Persist_TwoPassReport_WritesExactlyOneRowPerCheckpoint()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { }

            var writer = new OptimizerRunLogWriter(() => TestDb.Create(conn), () => FixedNow);
            var report = BuildTwoPassReport();
            var runId = Guid.NewGuid();

            writer.Persist(runId, report);

            using var verify = TestDb.Create(conn);
            var rows = verify.OptimizerRunLogs.Where(r => r.RunId == runId).OrderBy(r => r.PassIndex).ThenBy(r => r.CheckpointIndex).ToList();

            Assert.Equal(4, rows.Count); // pass1: C0,C1 ; pass2: C0,C1

            Assert.All(rows, r => Assert.Equal(OptimizerTermination.Terminated_FixedPoint, r.Termination));
            Assert.All(rows, r => Assert.Equal(2, r.PassCount));
            Assert.All(rows, r => Assert.Equal(FixedNow, r.CreatedUtc));

            var pass1Rows = rows.Where(r => r.PassIndex == 1).ToList();
            Assert.All(pass1Rows, r => Assert.Equal(1, r.KStar));
            var pass2Rows = rows.Where(r => r.PassIndex == 2).ToList();
            Assert.All(pass2Rows, r => Assert.Equal(0, r.KStar));

            var c0Rows = rows.Where(r => r.CheckpointIndex == 0).ToList();
            Assert.Equal(2, c0Rows.Count);
            Assert.All(c0Rows, r => Assert.Null(r.Reason));

            var pass1C1 = pass1Rows.Single(r => r.CheckpointIndex == 1);
            Assert.Equal(OptimizerCheckpointReason.Selected, pass1C1.Reason);
            Assert.Equal(0, pass1C1.ViolationCount);
            Assert.Equal(0, pass1C1.OverdueMinutes);
            Assert.Equal(0.5, pass1C1.Score);

            var pass2C1 = pass2Rows.Single(r => r.CheckpointIndex == 1);
            Assert.Equal(OptimizerCheckpointReason.Rejected_NoImprovement, pass2C1.Reason);
        }

        [Fact]
        public void Persist_TwoSeparateRuns_TagsRowsWithDistinctRunIds_NoCrossContamination()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { }

            var writer = new OptimizerRunLogWriter(() => TestDb.Create(conn), () => FixedNow);
            var report = BuildTwoPassReport();

            var runIdA = Guid.NewGuid();
            var runIdB = Guid.NewGuid();
            writer.Persist(runIdA, report);
            writer.Persist(runIdB, report);

            using var verify = TestDb.Create(conn);
            Assert.Equal(4, verify.OptimizerRunLogs.Count(r => r.RunId == runIdA));
            Assert.Equal(4, verify.OptimizerRunLogs.Count(r => r.RunId == runIdB));
            Assert.Equal(8, verify.OptimizerRunLogs.Count());
        }

        [Fact]
        public void Persist_NullReport_Throws()
        {
            var conn = TestDb.OpenConnection();
            using var _ = conn;
            using (var seed = TestDb.Create(conn)) { }

            var writer = new OptimizerRunLogWriter(() => TestDb.Create(conn), () => FixedNow);

            Assert.Throws<ArgumentNullException>(() => writer.Persist(Guid.NewGuid(), null!));
        }
    }
}
