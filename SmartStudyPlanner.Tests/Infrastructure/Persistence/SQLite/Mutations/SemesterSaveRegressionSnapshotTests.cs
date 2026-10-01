using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 3 regression oracle. Pinned against the UNREFACTORED
    /// <c>SqliteHocKyRepository.LuuHocKyAsync</c> and kept unchanged across the planner/writer/executor
    /// extraction. Every test is a bare before/after snapshot diff over all six synced tables (rows, Rev, IsDeleted, DeletedAtUtc, ModifiedAtUtc, FKs) plus, where the save mutates
    /// the caller's graph, the caller-visible state afterwards.
    ///
    /// Everything asserted here is OBSERVED behaviour, not desired behaviour. Tests whose name ends in
    /// <c>_Observed</c> pin something a reader could mistake for a contract (repository instances leaking into the caller's graph, a half-refilled graph after a failed
    /// save). They are not to be "fixed" in Slice 3; Slice 4 / OD-7 owns them. (The re-stamping of
    /// already-tombstoned rows was pinned here too until the D-2 defect fix flipped that row.)
    /// </summary>
    public class SemesterSaveRegressionSnapshotTests
    {
        // ---- harness -------------------------------------------------------------------------

        private sealed class Harness : IDisposable
        {
            private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            private readonly SqliteConnection _conn;
            private readonly Dictionary<Guid, string> _labels = new();
            private int _tick;

            public SqliteHocKyRepository Repo { get; }

            public Harness()
            {
                _conn = TestDb.OpenConnection();
                using (TestDb.Create(_conn)) { /* EnsureCreated */ }
                Repo = new SqliteHocKyRepository(NewContext);
            }

            // One clock value per save ("t1", "t2", ...) so a re-stamp shows up as a changed tick.
            public AppDbContext NewContext()
            {
                var ctx = TestDb.Create(_conn);
                ctx.Clock = () => Epoch.AddMinutes(_tick);
                ctx.DeviceIdProvider = () => "dev-test";
                return ctx;
            }

            public HocKy Semester(string ten)
            {
                var hocKy = new HocKy(ten, new DateTime(2026, 1, 5));
                _labels[hocKy.MaHocKy] = ten;
                return hocKy;
            }

            public MonHoc Subject(HocKy hocKy, string label, string? ten = null)
            {
                var mon = new MonHoc(ten ?? label, 3) { MaHocKy = hocKy.MaHocKy };
                _labels[mon.MaMonHoc] = label;
                hocKy.DanhSachMonHoc.Add(mon);
                return mon;
            }

            public StudyTask Task(MonHoc mon, string ten, bool stampFk = true)
            {
                var task = NewTask(ten);
                if (stampFk) task.MaMonHoc = mon.MaMonHoc;
                _labels[task.MaTask] = ten;
                mon.DanhSachTask.Add(task);
                return task;
            }

            public static StudyTask NewTask(string ten) =>
                new StudyTask(ten, new DateTime(2026, 2, 1), LoaiCongViec.BaiTapVeNha, 2)
                {
                    MucDoCanhBao = "An toàn",
                };

            public void Label(Guid id, string label) => _labels[id] = label;

            public Task SaveAsync(HocKy hocKy)
            {
                _tick++;
                return Repo.LuuHocKyAsync(hocKy);
            }

            public async Task AddNoteAndLinksAsync(StudyTask task, params string[] linkTitles)
            {
                _tick++;
                using var ctx = NewContext();
                ctx.TaskNotes.Add(new TaskNote { MaTask = task.MaTask, Content = "note(" + task.TenTask + ")" });
                foreach (var title in linkTitles)
                    ctx.TaskReferenceLinks.Add(new TaskReferenceLink { MaTask = task.MaTask, Title = title, Url = "https://example.test/" + title });
                await ctx.SaveChangesAsync();
            }

            // StudyLog is the sixth synced table. The save never touches it; seeding one under a task
            // that is about to be deleted pins that (it stays live -- OBSERVED, not desired).
            public async Task AddStudyLogAsync(StudyTask task)
            {
                _tick++;
                using var ctx = NewContext();
                ctx.StudyLogs.Add(new StudyLog { MaTask = task.MaTask, NgayHoc = new DateTime(2026, 1, 10), SoPhutHoc = 25, GhiChu = "log(" + task.TenTask + ")" });
                await ctx.SaveChangesAsync();
            }

            public async Task<SortedDictionary<string, string>> SnapshotAsync()
            {
                var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
                using var ctx = NewContext();

                foreach (var h in await ctx.HocKys.IgnoreQueryFilters().AsNoTracking().ToListAsync())
                    rows.Add("HocKy/" + Name(h.MaHocKy, h.Ten), Row(h, "-", h.Ten));
                foreach (var m in await ctx.MonHocs.IgnoreQueryFilters().AsNoTracking().ToListAsync())
                    rows.Add("MonHoc/" + Name(m.MaMonHoc, m.TenMonHoc), Row(m, Name(m.MaHocKy, "?"), m.TenMonHoc));
                foreach (var t in await ctx.StudyTasks.IgnoreQueryFilters().AsNoTracking().ToListAsync())
                    rows.Add("StudyTask/" + Name(t.MaTask, t.TenTask), Row(t, Name(t.MaMonHoc, "?"), t.TenTask));
                foreach (var n in await ctx.TaskNotes.IgnoreQueryFilters().AsNoTracking().ToListAsync())
                    rows.Add("TaskNote/" + n.Content, Row(n, Name(n.MaTask, "?"), n.Content ?? ""));
                foreach (var l in await ctx.TaskReferenceLinks.IgnoreQueryFilters().AsNoTracking().ToListAsync())
                    rows.Add("TaskLink/" + l.Title, Row(l, Name(l.MaTask, "?"), l.Title));
                foreach (var g in await ctx.StudyLogs.IgnoreQueryFilters().AsNoTracking().ToListAsync())
                    rows.Add("StudyLog/" + g.GhiChu, Row(g, Name(g.MaTask, "?"), g.GhiChu ?? ""));

                return rows;
            }

            private string Name(Guid id, string fallback) => _labels.TryGetValue(id, out var label) ? label : fallback;

            private static string Row(ISyncMetadata meta, string fk, string name)
            {
                string state = meta.IsDeleted
                    ? "Deleted@" + Tick(meta.DeletedAtUtc)
                    : meta.DeletedAtUtc is null ? "Live" : "Live(DeletedAt=" + Tick(meta.DeletedAtUtc) + ")";
                return $"Rev={meta.Rev} {state} Mod={Tick(meta.ModifiedAtUtc)} FK={fk} Name={name}";
            }

            private static string Tick(DateTime? utc) =>
                utc is null ? "null" : "t" + (int)Math.Round((utc.Value - Epoch).TotalMinutes);

            public void Dispose() => _conn.Dispose();
        }

        private static string Live(int rev, int mod, string fk, string name) => $"Rev={rev} Live Mod=t{mod} FK={fk} Name={name}";
        private static string Dead(int rev, int at, string fk, string name) => $"Rev={rev} Deleted@t{at} Mod=t{at} FK={fk} Name={name}";
        private static string Added(string key, string row) => $"+ {key}: {row}";
        private static string Changed(string key, string from, string to) => $"~ {key}: {from} => {to}";

        private static List<string> Diff(SortedDictionary<string, string> before, SortedDictionary<string, string> after)
        {
            var lines = new List<string>();
            foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool had = before.TryGetValue(key, out var oldRow);
                bool has = after.TryGetValue(key, out var newRow);
                if (!had) lines.Add(Added(key, newRow!));
                else if (!has) lines.Add($"- {key}: {oldRow}");
                else if (oldRow != newRow) lines.Add(Changed(key, oldRow!, newRow!));
            }
            return lines;
        }

        private static void AssertDiff(SortedDictionary<string, string> before, SortedDictionary<string, string> after, params string[] expected)
            => Assert.Equal(expected.OrderBy(l => l.Substring(2), StringComparer.Ordinal).ToArray(), Diff(before, after).ToArray());

        // ---- create / add --------------------------------------------------------------------

        [Fact]
        public async Task CreateSemester_AttachesWholeIncomingGraph()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            var t2 = h.Task(toan, "T2", stampFk: false);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Added("HocKy/HK1", Live(1, 1, "-", "HK1")),
                Added("MonHoc/Toan", Live(1, 1, "HK1", "Toan")),
                Added("StudyTask/T1", Live(1, 1, "Toan", "T1")),
                Added("StudyTask/T2", Live(1, 1, "Toan", "T2")));

            // Caller-visible: the incoming objects themselves were tracked and stamped, and EF's own
            // fixup (not the explicit heal, which the create branch never reaches) filled the empty FK.
            Assert.Equal(1, hk.Rev);
            Assert.Equal(1, toan.Rev);
            Assert.Equal(1, t1.Rev);
            Assert.Equal(toan.MaMonHoc, t2.MaMonHoc);
            Assert.Equal(new[] { t1, t2 }, toan.DanhSachTask);
        }

        [Fact]
        public async Task AddMonHocWithTasks_ToExistingSemester()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            h.Task(toan, "T1");
            await h.SaveAsync(hk);

            var ly = h.Subject(hk, "Ly");
            var l1 = h.Task(ly, "L1");
            var l2 = h.Task(ly, "L2");
            var collectionBeforeSave = ly.DanhSachTask;

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Added("MonHoc/Ly", Live(1, 2, "HK1", "Ly")),
                Added("StudyTask/L1", Live(1, 2, "Ly", "L1")),
                Added("StudyTask/L2", Live(1, 2, "Ly", "L2")));

            // OBSERVED caller-visible effect: the new MonHoc's task collection is REPLACED by a fresh
            // instance and refilled with the caller's own task objects, in incoming order.
            Assert.NotSame(collectionBeforeSave, ly.DanhSachTask);
            Assert.Equal(2, ly.DanhSachTask.Count);
            Assert.Same(l1, ly.DanhSachTask[0]);
            Assert.Same(l2, ly.DanhSachTask[1]);
            Assert.Equal(1, ly.Rev);
            Assert.Equal(1, l1.Rev);
            Assert.Equal(1, l2.Rev);
        }

        [Fact]
        public async Task AddTask_ToExistingMonHoc()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            await h.SaveAsync(hk);

            var t2 = h.Task(toan, "T2");
            var collectionBeforeSave = toan.DanhSachTask;

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after, Added("StudyTask/T2", Live(1, 2, "Toan", "T2")));

            // The owner is the repository's tracked MonHoc, so the caller's collection is untouched;
            // only the new task object itself is tracked and stamped.
            Assert.Same(collectionBeforeSave, toan.DanhSachTask);
            Assert.Equal(new[] { t1, t2 }, toan.DanhSachTask);
            Assert.Equal(1, t2.Rev);
        }

        // ---- edit / no-op --------------------------------------------------------------------

        [Fact]
        public async Task EditTaskFields_BumpsOnlyThatRow()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            h.Task(toan, "T2");
            await h.SaveAsync(hk);

            t1.TenTask = "T1-edited";

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Changed("StudyTask/T1", Live(1, 1, "Toan", "T1"), Live(2, 2, "Toan", "T1-edited")));

            // OBSERVED: the caller's object does not receive the new Rev -- it stays stale.
            Assert.Equal(1, t1.Rev);
        }

        [Fact]
        public async Task NoChangeSave_OverLiveRows_WritesNothing()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            h.Task(toan, "T1");
            await h.SaveAsync(hk);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after);
        }

        [Fact]
        public async Task NoChangeSave_OverAlreadyTombstonedRows_WritesNothing()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            h.Task(toan, "T1");
            var t2 = h.Task(toan, "T2");
            var ly = h.Subject(hk, "Ly");
            h.Task(ly, "L1");
            await h.SaveAsync(hk);                       // t1
            await h.AddNoteAndLinksAsync(t2, "LinkT2");  // t2

            toan.DanhSachTask.Remove(t2);
            hk.DanhSachMonHoc.Remove(ly);
            await h.SaveAsync(hk);                       // t3: T2 (+note, link), Ly, L1 tombstoned

            var before = await h.SnapshotAsync();
            Assert.Equal(Dead(2, 3, "Toan", "T2"), before["StudyTask/T2"]);
            Assert.Equal(Dead(2, 3, "HK1", "Ly"), before["MonHoc/Ly"]);

            await h.SaveAsync(hk);                       // t4: nothing changed in the caller's graph
            var after = await h.SnapshotAsync();

            // INTENTIONAL FLIP (D-2, ticket Prompt/2026-09-30-d2-tombstone-restamp-fix.md). Until D-2 this
            // row pinned the OBSERVED re-stamp churn (plan P0-a / R-9): the old graph was loaded without
            // an IsDeleted filter, so rows tombstoned by an earlier save were re-removed and re-stamped
            // on every save. The executor now loads the old graph live-only, so a no-change save over
            // tombstones writes nothing. This is the only Slice-3 oracle row D-2 changes.
            AssertDiff(before, after);
        }

        // ---- delete --------------------------------------------------------------------------

        [Fact]
        public async Task DeleteTask_WithNoteAndLinks_XoaTask()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            var t2 = h.Task(toan, "T2");
            await h.SaveAsync(hk);                              // t1
            await h.AddNoteAndLinksAsync(t1, "A1", "A2");       // t2
            await h.AddNoteAndLinksAsync(t2, "B1");             // t3 (control: survives)
            await h.AddStudyLogAsync(t1);                       // t4

            toan.DanhSachTask.Remove(t1);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);                              // t5
            var after = await h.SnapshotAsync();

            // OBSERVED, NOT DESIRED: the deleted task's StudyLog is not in the diff -- it stays live.
            Assert.Equal(Live(1, 4, "T1", "log(T1)"), after["StudyLog/log(T1)"]);

            AssertDiff(before, after,
                Changed("StudyTask/T1", Live(1, 1, "Toan", "T1"), Dead(2, 5, "Toan", "T1")),
                Changed("TaskNote/note(T1)", Live(1, 2, "T1", "note(T1)"), Dead(2, 5, "T1", "note(T1)")),
                Changed("TaskLink/A1", Live(1, 2, "T1", "A1"), Dead(2, 5, "T1", "A1")),
                Changed("TaskLink/A2", Live(1, 2, "T1", "A2"), Dead(2, 5, "T1", "A2")));
        }

        [Fact]
        public async Task DeleteMonHoc_WithThreeTasksNotesAndLinks_XoaMon()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            var t2 = h.Task(toan, "T2");
            h.Task(toan, "T3");
            var ly = h.Subject(hk, "Ly");
            var s1 = h.Task(ly, "S1");
            await h.SaveAsync(hk);                              // t1
            await h.AddNoteAndLinksAsync(t1, "A1", "A2");       // t2
            await h.AddNoteAndLinksAsync(t2);                   // t3
            await h.AddNoteAndLinksAsync(s1, "S1Link");         // t4 (control: sibling survives)
            await h.AddStudyLogAsync(t2);                       // t5

            hk.DanhSachMonHoc.Remove(toan);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);                              // t6
            var after = await h.SnapshotAsync();

            // OBSERVED, NOT DESIRED: the StudyLog of a task under the deleted MonHoc stays live.
            Assert.Equal(Live(1, 5, "T2", "log(T2)"), after["StudyLog/log(T2)"]);

            AssertDiff(before, after,
                Changed("MonHoc/Toan", Live(1, 1, "HK1", "Toan"), Dead(2, 6, "HK1", "Toan")),
                Changed("StudyTask/T1", Live(1, 1, "Toan", "T1"), Dead(2, 6, "Toan", "T1")),
                Changed("StudyTask/T2", Live(1, 1, "Toan", "T2"), Dead(2, 6, "Toan", "T2")),
                Changed("StudyTask/T3", Live(1, 1, "Toan", "T3"), Dead(2, 6, "Toan", "T3")),
                Changed("TaskNote/note(T1)", Live(1, 2, "T1", "note(T1)"), Dead(2, 6, "T1", "note(T1)")),
                Changed("TaskLink/A1", Live(1, 2, "T1", "A1"), Dead(2, 6, "T1", "A1")),
                Changed("TaskLink/A2", Live(1, 2, "T1", "A2"), Dead(2, 6, "T1", "A2")),
                Changed("TaskNote/note(T2)", Live(1, 3, "T2", "note(T2)"), Dead(2, 6, "T2", "note(T2)")));
        }

        // ---- reparent ------------------------------------------------------------------------

        [Fact]
        public async Task ReparentTask_BetweenExistingMonHocs()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            h.Task(toan, "T2");
            var ly = h.Subject(hk, "Ly");
            await h.SaveAsync(hk);

            toan.DanhSachTask.Remove(t1);
            t1.MaMonHoc = ly.MaMonHoc;
            ly.DanhSachTask.Add(t1);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Changed("StudyTask/T1", Live(1, 1, "Toan", "T1"), Live(2, 2, "Ly", "T1")));
        }

        [Fact]
        public async Task ReparentTask_OutOfMonHocDeletedInTheSameSave_SurvivorIsNotCascaded()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            h.Task(toan, "T2");
            var ly = h.Subject(hk, "Ly");
            await h.SaveAsync(hk);

            toan.DanhSachTask.Remove(t1);
            t1.MaMonHoc = ly.MaMonHoc;
            ly.DanhSachTask.Add(t1);
            hk.DanhSachMonHoc.Remove(toan);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Changed("MonHoc/Toan", Live(1, 1, "HK1", "Toan"), Dead(2, 2, "HK1", "Toan")),
                Changed("StudyTask/T1", Live(1, 1, "Toan", "T1"), Live(2, 2, "Ly", "T1")),
                Changed("StudyTask/T2", Live(1, 1, "Toan", "T2"), Dead(2, 2, "Toan", "T2")));
        }

        [Fact]
        public async Task ReparentTask_IntoNewMonHoc_CallerGraphHoldsRepositoryInstance_Observed()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            await h.SaveAsync(hk);

            var hoa = h.Subject(hk, "Hoa");
            toan.DanhSachTask.Remove(t1);
            t1.MaMonHoc = hoa.MaMonHoc;
            hoa.DanhSachTask.Add(t1);
            var n1 = h.Task(hoa, "N1");
            var collectionBeforeSave = hoa.DanhSachTask;

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Added("MonHoc/Hoa", Live(1, 2, "HK1", "Hoa")),
                Added("StudyTask/N1", Live(1, 2, "Hoa", "N1")),
                Changed("StudyTask/T1", Live(1, 1, "Toan", "T1"), Live(2, 2, "Hoa", "T1")));

            // OBSERVED, NOT DESIRED (Phase 0 effect 4; Slice 4 / OD-7 owns it). The caller's new MonHoc
            // gets a replaced collection, and the reparented task in it is the REPOSITORY's tracked
            // instance (loaded from the DB by a context that is now disposed), not the caller's object.
            Assert.NotSame(collectionBeforeSave, hoa.DanhSachTask);
            Assert.Equal(2, hoa.DanhSachTask.Count);
            var reparented = hoa.DanhSachTask.Single(t => t.MaTask == t1.MaTask);
            Assert.NotSame(t1, reparented);
            Assert.Equal(2, reparented.Rev);
            Assert.Equal(1, t1.Rev);
            Assert.Same(n1, hoa.DanhSachTask.Single(t => t.MaTask == n1.MaTask));
            Assert.Equal(new[] { t1.MaTask, n1.MaTask }, hoa.DanhSachTask.Select(t => t.MaTask));
        }

        [Fact]
        public async Task CloneMergeSave_AfterLayDanhSachDedup()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toanA = h.Subject(hk, "ToanA", ten: "Toán");
            var ta = h.Task(toanA, "TA");
            var toanB = h.Subject(hk, "ToanB", ten: "toán ");
            var tb = h.Task(toanB, "TB");
            await h.SaveAsync(hk);

            var loaded = (await h.Repo.LayDanhSachHocKyAsync()).Single();
            Assert.Single(loaded.DanhSachMonHoc);

            // Which clone the dedup keeps follows the DB's row order (random Guid keys), so the test
            // reads the survivor off the loaded graph instead of assuming one.
            bool aWins = loaded.DanhSachMonHoc[0].MaMonHoc == toanA.MaMonHoc;
            var (winner, loser) = aWins ? ("ToanA", "ToanB") : ("ToanB", "ToanA");
            var loserTen = aWins ? toanB.TenMonHoc : toanA.TenMonHoc;
            var moved = aWins ? "TB" : "TA";

            var before = await h.SnapshotAsync();
            await h.SaveAsync(loaded);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after,
                Changed("MonHoc/" + loser, Live(1, 1, "HK1", loserTen), Dead(2, 2, "HK1", loserTen)),
                Changed("StudyTask/" + moved, Live(1, 1, loser, moved), Live(2, 2, winner, moved)));

            Assert.Single(loaded.DanhSachMonHoc);
            Assert.Equal(
                new[] { ta.MaTask, tb.MaTask }.OrderBy(id => id),
                loaded.DanhSachMonHoc[0].DanhSachTask.Select(t => t.MaTask).OrderBy(id => id));
        }

        // ---- FK heal -------------------------------------------------------------------------

        [Fact]
        public async Task TaskWithEmptyFk_IsHealedToItsNavigationOwner()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            h.Task(toan, "T1");
            await h.SaveAsync(hk);

            var heal = h.Task(toan, "Heal", stampFk: false);
            Assert.Equal(Guid.Empty, heal.MaMonHoc);

            var before = await h.SnapshotAsync();
            await h.SaveAsync(hk);
            var after = await h.SnapshotAsync();

            AssertDiff(before, after, Added("StudyTask/Heal", Live(1, 2, "Toan", "Heal")));

            // Caller-visible: the FK is written onto the caller's own object.
            Assert.Equal(toan.MaMonHoc, heal.MaMonHoc);
        }

        // ---- failure paths -------------------------------------------------------------------

        [Fact]
        public async Task TaskPointingToUnknownMonHoc_Throws_DbUnchanged_CallerGraphHalfRefilled_Observed()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            await h.SaveAsync(hk);

            // Incoming order of tasks: N1 (new MonHoc Hoa), T1, Heal, Bad (throws), N3 (new MonHoc Sinh).
            var hoa = h.Subject(hk, "Hoa");
            hk.DanhSachMonHoc.Move(hk.DanhSachMonHoc.IndexOf(hoa), 0);
            var n1 = h.Task(hoa, "N1");
            var heal = h.Task(toan, "Heal", stampFk: false);
            var bad = h.Task(toan, "Bad");
            bad.MaMonHoc = Guid.NewGuid();
            var sinh = h.Subject(hk, "Sinh");
            var n3 = h.Task(sinh, "N3");
            var hoaCollection = hoa.DanhSachTask;
            var sinhCollection = sinh.DanhSachTask;
            var toanCollection = toan.DanhSachTask;

            var before = await h.SnapshotAsync();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => h.SaveAsync(hk));
            var after = await h.SnapshotAsync();

            Assert.Contains(bad.MaTask.ToString(), ex.Message);
            Assert.Contains(bad.MaMonHoc.ToString(), ex.Message);
            AssertDiff(before, after); // rolled back: no table changed

            // OBSERVED, NOT DESIRED (Phase 0 effect 7; Slice 4 / OD-7 owns it). The throw happens late,
            // so the caller's graph keeps every mutation made before it:
            //  - the heal was applied;
            Assert.Equal(toan.MaMonHoc, heal.MaMonHoc);
            //  - both new MonHocs had their task collection replaced ...
            Assert.NotSame(hoaCollection, hoa.DanhSachTask);
            Assert.NotSame(sinhCollection, sinh.DanhSachTask);
            //  - ... and only the tasks iterated before the failing one were put back: Hoa is refilled,
            //    Sinh is left EMPTY (N3 is gone from the caller's graph).
            Assert.Equal(new[] { n1 }, hoa.DanhSachTask);
            Assert.Empty(sinh.DanhSachTask);
            Assert.DoesNotContain(n3, hk.DanhSachMonHoc.SelectMany(m => m.DanhSachTask));
            //  - the existing MonHoc's caller collection is untouched, the bad task still in it;
            Assert.Same(toanCollection, toan.DanhSachTask);
            Assert.Equal(new[] { t1, heal, bad }, toan.DanhSachTask);
            //  - nothing was stamped, because SaveChanges never ran.
            Assert.Equal(0, n1.Rev);
            Assert.Equal(0, hoa.Rev);
            Assert.Equal(new[] { hoa, toan, sinh }, hk.DanhSachMonHoc);
        }

        [Fact]
        public async Task DuplicateMaMonHoc_ThrowsArgumentException_BeforeHeal_DbUnchanged()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            h.Task(toan, "T1");
            await h.SaveAsync(hk);

            var heal = h.Task(toan, "Heal", stampFk: false);
            var duplicate = new MonHoc("ToanDup", 3) { MaMonHoc = toan.MaMonHoc, MaHocKy = hk.MaHocKy };
            hk.DanhSachMonHoc.Add(duplicate);

            var before = await h.SnapshotAsync();
            await Assert.ThrowsAsync<ArgumentException>(() => h.SaveAsync(hk));
            var after = await h.SnapshotAsync();

            AssertDiff(before, after);
            // OBSERVED: the duplicate is detected BEFORE the FK heal, so the caller's task is not healed.
            Assert.Equal(Guid.Empty, heal.MaMonHoc);
        }

        [Fact]
        public async Task DuplicateMaTask_ThrowsArgumentException_AfterHeal_DbUnchanged()
        {
            using var h = new Harness();
            var hk = h.Semester("HK1");
            var toan = h.Subject(hk, "Toan");
            var t1 = h.Task(toan, "T1");
            var ly = h.Subject(hk, "Ly");
            await h.SaveAsync(hk);

            var heal = h.Task(toan, "Heal", stampFk: false);
            var duplicate = Harness.NewTask("T1Dup");
            duplicate.MaTask = t1.MaTask;
            duplicate.MaMonHoc = ly.MaMonHoc;
            ly.DanhSachTask.Add(duplicate);

            var before = await h.SnapshotAsync();
            await Assert.ThrowsAsync<ArgumentException>(() => h.SaveAsync(hk));
            var after = await h.SnapshotAsync();

            AssertDiff(before, after);
            // OBSERVED: the duplicate is detected AFTER the FK heal, so the caller's task IS healed.
            Assert.Equal(toan.MaMonHoc, heal.MaMonHoc);
        }
    }
}
