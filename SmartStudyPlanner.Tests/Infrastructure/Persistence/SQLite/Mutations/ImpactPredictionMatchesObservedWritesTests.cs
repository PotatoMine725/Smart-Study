using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4 — X-16, the plan §7.4 oracle that makes "actual cascade, not invented
    /// closure" (INV-2) provable on the local path. With NO conflict records: snapshot every domain row,
    /// compute <see cref="ImpactResolver"/>'s output for the request the planner emits for this save,
    /// run the real save, diff.
    /// <para>
    /// <b>Interpretation, stated (§7.4 says "rows whose live→dead state or FK changed equals the
    /// predicted Rows with WasLive = true", which does not literally cover creates):</b> the
    /// per-effect mapping compared is
    /// Tombstoned / CascadeTombstoned ↔ live→dead;
    /// Reparented ↔ structural FK changed;
    /// Created ↔ row that did not exist before;
    /// FieldsChanged is excluded on both sides (a field edit changes neither liveness nor an edge).
    /// P0-a/D-2 decided the "already-dead rows re-stamped" question: they are not re-stamped any more,
    /// so nothing is excluded from the observed side.
    /// </para>
    /// Mutation proof (§7.4): delete the TaskNote branch of the resolver's cascade expansion ⇒ the
    /// task-delete and MonHoc-delete topologies go RED.
    /// </summary>
    public class ImpactPredictionMatchesObservedWritesTests : IDisposable
    {
        private readonly SyncApplyFixture _fx = new();
        private readonly LocalSaveDriver _save;
        private readonly ITestOutputHelper _out;

        public ImpactPredictionMatchesObservedWritesTests(ITestOutputHelper output)
        {
            _out = output;
            _save = new LocalSaveDriver(_fx);
        }

        public void Dispose() => _fx.Dispose();

        private sealed record RowState(bool IsDeleted, Guid? ParentId);

        private async Task<Dictionary<string, RowState>> SnapshotAsync()
        {
            using var ctx = _fx.NewContext();
            var rows = new Dictionary<string, RowState>(StringComparer.Ordinal);
            foreach (var r in await ctx.HocKys.AsNoTracking().ToListAsync()) rows[$"{SyncEntityTypes.HocKy}:{r.MaHocKy:D}"] = new(r.IsDeleted, null);
            foreach (var r in await ctx.MonHocs.AsNoTracking().ToListAsync()) rows[$"{SyncEntityTypes.MonHoc}:{r.MaMonHoc:D}"] = new(r.IsDeleted, r.MaHocKy);
            foreach (var r in await ctx.StudyTasks.AsNoTracking().ToListAsync()) rows[$"{SyncEntityTypes.StudyTask}:{r.MaTask:D}"] = new(r.IsDeleted, r.MaMonHoc);
            foreach (var r in await ctx.TaskNotes.AsNoTracking().ToListAsync()) rows[$"{SyncEntityTypes.TaskNote}:{r.Id:D}"] = new(r.IsDeleted, r.MaTask);
            foreach (var r in await ctx.TaskReferenceLinks.AsNoTracking().ToListAsync()) rows[$"{SyncEntityTypes.TaskReferenceLink}:{r.Id:D}"] = new(r.IsDeleted, r.MaTask);
            return rows;
        }

        private static SortedSet<string> Observed(Dictionary<string, RowState> before, Dictionary<string, RowState> after)
        {
            var changes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (key, now) in after)
            {
                if (!before.TryGetValue(key, out var was)) { changes.Add($"{key}:Created"); continue; }
                if (!was.IsDeleted && now.IsDeleted) changes.Add($"{key}:Tombstoned");
                if (was.ParentId != now.ParentId) changes.Add($"{key}:Reparented");
            }
            return changes;
        }

        private static SortedSet<string> Predicted(ImpactSet impact)
        {
            var changes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var row in impact.Rows.Where(r => r.WasLive))
            {
                var category = row.Effect switch
                {
                    RowEffect.Tombstoned or RowEffect.CascadeTombstoned => "Tombstoned",
                    RowEffect.Reparented => "Reparented",
                    RowEffect.Created => "Created",
                    _ => null,
                };
                if (category is not null) changes.Add($"{row.EntityType}:{row.EntityId:D}:{category}");
            }
            return changes;
        }

        /// <summary>Predict (no records ⇒ the save will pass), save, diff, compare.</summary>
        private async Task AssertOracleAsync(HocKy graph, int expectedChangeCount)
        {
            var before = await SnapshotAsync();

            ImpactSet impact;
            using (var db = _fx.NewContext())
            {
                var old = await LocalSemesterSaveExecutor.LoadLiveGraphAsync(db, graph.MaHocKy, default);
                impact = await ImpactResolver.ResolveAsync(db, SemesterReconcilePlanner.Plan(old, graph).Request);
            }

            await _save.SaveAsync(graph);

            var observed = Observed(before, await SnapshotAsync());
            var predicted = Predicted(impact);
            _out.WriteLine("predicted:\n  " + string.Join("\n  ", predicted));
            _out.WriteLine("observed:\n  " + string.Join("\n  ", observed));

            Assert.Equal(observed, predicted);
            Assert.Equal(expectedChangeCount, observed.Count);                   // non-vacuous: the save did something
        }

        private async Task<StudyTask> AddTaskWithChildrenAsync(Guid monHocId, string ten)
        {
            var task = new StudyTask(ten, new DateTime(2026, 2, 9), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHocId };
            await _fx.AddLocalAsync(task);
            await _fx.AddLocalAsync(new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "note " + ten });
            await _fx.AddLocalAsync(new TaskReferenceLink { MaTask = task.MaTask, Title = "a", Url = "https://example.test/a" });
            await _fx.AddLocalAsync(new TaskReferenceLink { MaTask = task.MaTask, Title = "b", Url = "https://example.test/b" });
            return task;
        }

        [Fact]
        public async Task TaskDelete_WithNoteAndLinks()
        {
            var (hocKy, monHoc, _) = await _fx.SeedTreeAsync();
            var victim = await AddTaskWithChildrenAsync(monHoc.MaMonHoc, "victim");
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveTask(graph, victim.MaTask);

            await AssertOracleAsync(graph, expectedChangeCount: 4);              // task + note + 2 links
        }

        [Fact]
        public async Task MonHocDelete_WithThreeTasksNotesAndLinks()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            var doomed = new MonHoc("doomed", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(doomed);
            for (var i = 0; i < 3; i++) await AddTaskWithChildrenAsync(doomed.MaMonHoc, "t" + i);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.RemoveMon(graph, doomed.MaMonHoc);

            await AssertOracleAsync(graph, expectedChangeCount: 1 + 3 * 4);
        }

        [Fact]
        public async Task Reparent_BetweenExistingMonHocs()
        {
            var (hocKy, _, task) = await _fx.SeedTreeAsync();
            var target = new MonHoc("target", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(target);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.MoveTask(graph, task.MaTask, target.MaMonHoc);

            await AssertOracleAsync(graph, expectedChangeCount: 1);
        }

        [Fact]
        public async Task ReparentOutOfMonHocDeletedInTheSameSave_SurvivorIsNotCascaded()
        {
            var (hocKy, doomedMon, survivor) = await _fx.SeedTreeAsync();
            var goner = await AddTaskWithChildrenAsync(doomedMon.MaMonHoc, "goner");
            var target = new MonHoc("target", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(target);
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            LocalSaveDriver.MoveTask(graph, survivor.MaTask, target.MaMonHoc);
            LocalSaveDriver.RemoveMon(graph, doomedMon.MaMonHoc);

            await AssertOracleAsync(graph, expectedChangeCount: 1 + 1 + 4);      // reparent + MonHoc + goner subtree
        }

        [Fact]
        public async Task CloneMergeSave_AfterLayDanhSachDedup()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            var first = new MonHoc("Toán", 3) { MaHocKy = hocKy.MaHocKy };
            var second = new MonHoc("TOÁN", 3) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(first);
            await _fx.AddLocalAsync(second);
            await AddTaskWithChildrenAsync(first.MaMonHoc, "under first");
            await AddTaskWithChildrenAsync(second.MaMonHoc, "under second");
            var graph = await _save.LoadAsync(hocKy.MaHocKy);                     // dedup folds one clone into the other

            await AssertOracleAsync(graph, expectedChangeCount: 2);              // losing clone tombstoned + its task reparented
        }

        [Fact]
        public async Task Create_NewSemesterWithSubjectsAndTasks()
        {
            var graph = new HocKy("new", new DateTime(2026, 9, 1));
            for (var m = 0; m < 2; m++)
            {
                var mon = new MonHoc("mon " + m, 3);
                for (var t = 0; t < 2; t++) mon.DanhSachTask.Add(new StudyTask($"t{m}{t}", new DateTime(2026, 10, 1), LoaiCongViec.BaiTapVeNha, 1));
                graph.DanhSachMonHoc.Add(mon);
            }

            await AssertOracleAsync(graph, expectedChangeCount: 1 + 2 + 4);
        }

        [Fact]
        public async Task Create_SubjectWithTasksInExistingSemester()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var mon = new MonHoc("added", 2);
            mon.DanhSachTask.Add(new StudyTask("a", new DateTime(2026, 10, 1), LoaiCongViec.BaiTapVeNha, 1));
            mon.DanhSachTask.Add(new StudyTask("b", new DateTime(2026, 10, 2), LoaiCongViec.BaiTapVeNha, 1));
            graph.DanhSachMonHoc.Add(mon);

            await AssertOracleAsync(graph, expectedChangeCount: 3);
        }
    }
}
