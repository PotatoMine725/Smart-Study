using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4, Phase 4 (task card: "save latency on the reference semester
    /// fixture, before vs after the fence"). <b>MEASUREMENT ONLY</b>: it prints numbers and asserts
    /// nothing about them. There is no threshold and nothing is optimised on the strength of it.
    /// <para>
    /// Reference semester = 1 HocKy, 8 MonHoc x 10 StudyTask, on a FILE-backed SQLite database
    /// (WAL, what <c>EnsureCreated</c> produces and what production runs in) with no conflict records.
    /// That matches production today, where nothing can write a ConflictRecord. Three save shapes
    /// cover the callers the fence adds reads to: a no-change save (<c>MoFocusMode</c>), a one-field
    /// edit (<c>HoanThanhTask</c>) and a task delete (<c>XoaTask</c>).
    /// </para>
    /// <para>
    /// The same test ran on the baseline commit before the executor was touched, and again after the
    /// fence was wired. Both outputs are quoted in the Slice 4 report.
    /// </para>
    /// </summary>
    [Trait("Kind", "Measurement")]
    public class LocalSaveLatencyMeasurementTests
    {
        private const int Subjects = 8;
        private const int TasksPerSubject = 10;
        private const int Warmup = 5;
        private const int Samples = 30;

        private readonly ITestOutputHelper _out;

        public LocalSaveLatencyMeasurementTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public async Task SaveLatency_ReferenceSemester_NoChange_FieldEdit_Delete()
        {
            using var fx = new FileBackedSqliteFixture();
            var repo = new SqliteHocKyRepository(() => fx.NewContext(timeoutSeconds: 5));
            var hocKyId = await SeedReferenceSemesterAsync(repo);

            var graph = (await repo.LayDanhSachHocKyAsync()).Single(h => h.MaHocKy == hocKyId);

            var noChange = await MeasureAsync(Samples, _ => repo.LuuHocKyAsync(graph));

            var target = graph.DanhSachMonHoc[0].DanhSachTask[0];
            var fieldEdit = await MeasureAsync(Samples, i =>
            {
                target.TrangThai = i % 2 == 0 ? "Hoàn thành" : "Chưa làm";
                return repo.LuuHocKyAsync(graph);
            });

            // One distinct task per sample, taken from the subjects not used above.
            var victims = graph.DanhSachMonHoc.Skip(1).SelectMany(m => m.DanhSachTask.Select(t => (m, t))).ToList();
            var delete = await MeasureAsync(Samples, i =>
            {
                var (mon, task) = victims[i];
                mon.DanhSachTask.Remove(task);
                return repo.LuuHocKyAsync(graph);
            }, warmup: 0);

            Report("no-change save (MoFocusMode)", noChange);
            Report("one-field edit (HoanThanhTask)", fieldEdit);
            Report("task delete (XoaTask)", delete);
        }

        private static async Task<Guid> SeedReferenceSemesterAsync(SqliteHocKyRepository repo)
        {
            var hocKy = new HocKy("Reference semester", new DateTime(2026, 1, 5));
            for (var s = 0; s < Subjects; s++)
            {
                var mon = new MonHoc($"Subject {s}", 3) { MaHocKy = hocKy.MaHocKy };
                for (var t = 0; t < TasksPerSubject; t++)
                {
                    mon.DanhSachTask.Add(new StudyTask($"Task {s}.{t}", new DateTime(2026, 3, 1).AddDays(t), LoaiCongViec.BaiTapVeNha, 2)
                    {
                        MaMonHoc = mon.MaMonHoc,
                    });
                }
                hocKy.DanhSachMonHoc.Add(mon);
            }

            await repo.LuuHocKyAsync(hocKy);
            return hocKy.MaHocKy;
        }

        private static async Task<List<double>> MeasureAsync(int samples, Func<int, Task> save, int warmup = Warmup)
        {
            for (var i = 0; i < warmup; i++) await save(i);

            var timings = new List<double>(samples);
            for (var i = 0; i < samples; i++)
            {
                var sw = Stopwatch.StartNew();
                await save(i);
                sw.Stop();
                timings.Add(sw.Elapsed.TotalMilliseconds);
            }
            return timings;
        }

        private void Report(string label, List<double> timings)
        {
            var sorted = timings.OrderBy(t => t).ToList();
            double P(double q) => sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(q * sorted.Count))];
            _out.WriteLine($"{label}: n={sorted.Count} median={P(0.5):F2} ms p95={P(0.95):F2} ms mean={sorted.Average():F2} ms min={sorted[0]:F2} ms max={sorted[^1]:F2} ms");
        }
    }
}
