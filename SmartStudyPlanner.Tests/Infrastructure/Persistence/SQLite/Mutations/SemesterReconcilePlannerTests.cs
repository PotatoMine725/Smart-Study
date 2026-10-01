using System;
using System.Linq;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 3: the planner's output for each save topology, with no database.
    /// "Old" is a detached copy standing in for the graph the executor loads; "incoming" is the
    /// caller's graph. The end-to-end effect of each plan is pinned by
    /// <see cref="SemesterSaveRegressionSnapshotTests"/>.
    /// </summary>
    public class SemesterReconcilePlannerTests
    {
        private static MonHoc Subject(HocKy hocKy, string ten, Guid? id = null)
        {
            var mon = new MonHoc(ten, 3) { MaHocKy = hocKy.MaHocKy };
            if (id.HasValue) mon.MaMonHoc = id.Value;
            hocKy.DanhSachMonHoc.Add(mon);
            return mon;
        }

        private static StudyTask Task(MonHoc mon, string ten, Guid? id = null, bool stampFk = true)
        {
            var task = new StudyTask(ten, new DateTime(2026, 2, 1), LoaiCongViec.BaiTapVeNha, 2);
            if (id.HasValue) task.MaTask = id.Value;
            if (stampFk) task.MaMonHoc = mon.MaMonHoc;
            mon.DanhSachTask.Add(task);
            return task;
        }

        // Same ids, separate objects: what a fresh load of the caller's last save looks like.
        private static HocKy Clone(HocKy source)
        {
            var copy = new HocKy(source.Ten, source.NgayBatDau) { MaHocKy = source.MaHocKy };
            foreach (var mon in source.DanhSachMonHoc)
            {
                var monCopy = Subject(copy, mon.TenMonHoc, mon.MaMonHoc);
                foreach (var task in mon.DanhSachTask)
                    Task(monCopy, task.TenTask, task.MaTask).MaMonHoc = task.MaMonHoc;
            }
            return copy;
        }

        private static void AssertNoStructuralChange(SemesterReconcilePlan plan)
        {
            Assert.Empty(plan.FkHeals);
            Assert.Empty(plan.TaskReparents);
            Assert.Empty(plan.MonHocDeletes);
            Assert.Empty(plan.MonHocAdds);
            Assert.Empty(plan.TaskDeletes);
            Assert.Empty(plan.ValidationErrors);
        }

        [Fact]
        public void NoOldGraph_IsCreate_AndNothingElse()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            Task(Subject(incoming, "Toan"), "T1", stampFk: false);

            var plan = SemesterReconcilePlanner.Plan(null, incoming);

            Assert.True(plan.IsCreate);
            AssertNoStructuralChange(plan);
            Assert.Empty(plan.MonHocUpdates);
            Assert.Empty(plan.TaskUpserts);
        }

        [Fact]
        public void UnchangedGraph_OnlyUpdatesAndNonNewUpserts()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var old = Clone(incoming);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.False(plan.IsCreate);
            AssertNoStructuralChange(plan);
            Assert.Equal(new[] { toan.MaMonHoc }, plan.MonHocUpdates);
            Assert.Equal(new[] { new TaskUpsert(t1.MaTask, toan.MaMonHoc, false) }, plan.TaskUpserts);
        }

        [Fact]
        public void EditedTaskFields_AreNotAStructuralIntent()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var old = Clone(incoming);
            t1.TenTask = "T1-edited";

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            AssertNoStructuralChange(plan);
            Assert.Equal(new[] { new TaskUpsert(t1.MaTask, toan.MaMonHoc, false) }, plan.TaskUpserts);
        }

        [Fact]
        public void NewMonHocWithTasks_IsAnAdd_AndItsTasksAreNewUpserts()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var old = Clone(incoming);
            var ly = Subject(incoming, "Ly");
            var l1 = Task(ly, "L1");
            var l2 = Task(ly, "L2");

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(new[] { ly.MaMonHoc }, plan.MonHocAdds);
            Assert.Equal(new[] { toan.MaMonHoc }, plan.MonHocUpdates);
            Assert.Equal(new[]
            {
                new TaskUpsert(t1.MaTask, toan.MaMonHoc, false),
                new TaskUpsert(l1.MaTask, ly.MaMonHoc, true),
                new TaskUpsert(l2.MaTask, ly.MaMonHoc, true),
            }, plan.TaskUpserts);
            Assert.Empty(plan.MonHocDeletes);
            Assert.Empty(plan.TaskDeletes);
            Assert.Empty(plan.ValidationErrors);
        }

        [Fact]
        public void NewTaskUnderExistingMonHoc_IsANewUpsert()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            Task(toan, "T1");
            var old = Clone(incoming);
            var t2 = Task(toan, "T2");

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            AssertNoStructuralChange(plan);
            Assert.Equal(new TaskUpsert(t2.MaTask, toan.MaMonHoc, true), plan.TaskUpserts.Last());
        }

        [Fact]
        public void TaskAbsentFromIncoming_IsATaskDelete()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var t2 = Task(toan, "T2");
            var old = Clone(incoming);
            toan.DanhSachTask.Remove(t1);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(new[] { t1.MaTask }, plan.TaskDeletes);
            Assert.Equal(new[] { new TaskUpsert(t2.MaTask, toan.MaMonHoc, false) }, plan.TaskUpserts);
            Assert.Empty(plan.MonHocDeletes);
            Assert.Empty(plan.TaskReparents);
        }

        [Fact]
        public void MonHocAbsentFromIncoming_IsAMonHocDelete_AndEachOfItsTasksATaskDelete()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var t2 = Task(toan, "T2");
            var t3 = Task(toan, "T3");
            var ly = Subject(incoming, "Ly");
            var s1 = Task(ly, "S1");
            var old = Clone(incoming);
            incoming.DanhSachMonHoc.Remove(toan);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(new[] { toan.MaMonHoc }, plan.MonHocDeletes);
            Assert.Equal(new[] { t1.MaTask, t2.MaTask, t3.MaTask }, plan.TaskDeletes);
            Assert.Equal(new[] { ly.MaMonHoc }, plan.MonHocUpdates);
            Assert.Equal(new[] { new TaskUpsert(s1.MaTask, ly.MaMonHoc, false) }, plan.TaskUpserts);
            Assert.Empty(plan.TaskReparents);
        }

        [Fact]
        public void TaskMovedBetweenMonHocs_IsAReparent_NotADeleteAndAdd()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var ly = Subject(incoming, "Ly");
            var old = Clone(incoming);
            toan.DanhSachTask.Remove(t1);
            t1.MaMonHoc = ly.MaMonHoc;
            ly.DanhSachTask.Add(t1);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(new[] { new TaskReparent(t1.MaTask, toan.MaMonHoc, ly.MaMonHoc) }, plan.TaskReparents);
            Assert.Equal(new[] { new TaskUpsert(t1.MaTask, ly.MaMonHoc, false) }, plan.TaskUpserts);
            Assert.Empty(plan.TaskDeletes);
            Assert.Empty(plan.MonHocDeletes);
        }

        // The shape LayDanhSachHocKyAsync's dedup hands back: the losing clone is gone from the
        // graph and its task sits under the representative.
        [Fact]
        public void CloneMerge_LosingCloneIsDeleted_AndItsTaskReparentedNotDeleted()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toanA = Subject(incoming, "Toán");
            var ta = Task(toanA, "TA");
            var toanB = Subject(incoming, "toán ");
            var tb = Task(toanB, "TB");
            var old = Clone(incoming);
            incoming.DanhSachMonHoc.Remove(toanB);
            tb.MaMonHoc = toanA.MaMonHoc;
            toanA.DanhSachTask.Add(tb);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(new[] { toanB.MaMonHoc }, plan.MonHocDeletes);
            Assert.Equal(new[] { new TaskReparent(tb.MaTask, toanB.MaMonHoc, toanA.MaMonHoc) }, plan.TaskReparents);
            Assert.Empty(plan.TaskDeletes);
            Assert.Equal(new[]
            {
                new TaskUpsert(ta.MaTask, toanA.MaMonHoc, false),
                new TaskUpsert(tb.MaTask, toanA.MaMonHoc, false),
            }, plan.TaskUpserts);
        }

        [Fact]
        public void TaskWithEmptyFk_GetsAHeal_IsDiffedOnItsNavigationOwner_AndIsNotMutated()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            Task(toan, "T1");
            var old = Clone(incoming);
            var heal = Task(toan, "Heal", stampFk: false);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            var fkHeal = Assert.Single(plan.FkHeals);
            Assert.Same(heal, fkHeal.Task);
            Assert.Equal(toan.MaMonHoc, fkHeal.Owner);
            Assert.Equal(new TaskUpsert(heal.MaTask, toan.MaMonHoc, true), plan.TaskUpserts.Last());
            Assert.Empty(plan.ValidationErrors);
            Assert.Equal(Guid.Empty, heal.MaMonHoc); // pure: writing the FK is the writer's job
        }

        [Fact]
        public void TaskPointingToUnknownMonHoc_IsATaskUpsertStageError_AndStaysInTheUpsertList()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var old = Clone(incoming);
            var bad = Task(toan, "Bad");
            bad.MaMonHoc = Guid.NewGuid();
            var after = Task(toan, "After");

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            var error = Assert.Single(plan.ValidationErrors);
            Assert.Equal(PlanValidationKind.UnknownMonHoc, error.Kind);
            Assert.Equal(PlanStage.TaskUpsert, error.Stage);
            Assert.Equal(bad.MaTask, error.MaTask);
            Assert.Equal(bad.MaMonHoc, error.MaMonHoc);
            Assert.Equal(
                $"Reconcile: task 'Bad' ({bad.MaTask}) references MonHoc {bad.MaMonHoc} not present in HocKy {old.MaHocKy}.",
                error.Message);
            // The writer walks this list and fails when it reaches the bad task, so the tasks before
            // it are still written to the tracker first -- the pre-extraction position of the throw.
            Assert.Equal(new[] { t1.MaTask, bad.MaTask, after.MaTask }, plan.TaskUpserts.Select(u => u.MaTask));
        }

        [Fact]
        public void DuplicateMaMonHoc_IsABeforeFkHealError_AndThePlanCarriesNoHeals()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            Task(toan, "T1");
            var old = Clone(incoming);
            Task(toan, "Heal", stampFk: false);
            Subject(incoming, "ToanDup", toan.MaMonHoc);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            var error = Assert.Single(plan.ValidationErrors);
            Assert.Equal(PlanValidationKind.DuplicateMonHocId, error.Kind);
            Assert.Equal(PlanStage.BeforeFkHeal, error.Stage);
            Assert.Equal(toan.MaMonHoc, error.MaMonHoc);
            Assert.Empty(plan.FkHeals);
            Assert.Empty(plan.TaskUpserts);
        }

        [Fact]
        public void DuplicateMaTask_IsAnAfterFkHealError_AndThePlanStillCarriesTheHeals()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            var t1 = Task(toan, "T1");
            var ly = Subject(incoming, "Ly");
            var old = Clone(incoming);
            var heal = Task(toan, "Heal", stampFk: false);
            Task(ly, "T1Dup", t1.MaTask);

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            var error = Assert.Single(plan.ValidationErrors);
            Assert.Equal(PlanValidationKind.DuplicateTaskId, error.Kind);
            Assert.Equal(PlanStage.AfterFkHeal, error.Stage);
            Assert.Equal(t1.MaTask, error.MaTask);
            Assert.Same(heal, Assert.Single(plan.FkHeals).Task);
            Assert.Empty(plan.TaskUpserts);
            Assert.Empty(plan.TaskDeletes);
        }

        // The planner itself has no IsDeleted filter: tombstoned rows handed to it in the old graph
        // are planned as deletes again. Since D-2 (PR #105) the executor loads the old graph
        // live-only, so this input no longer reaches the planner from a real save -- see
        // SemesterSaveRegressionSnapshotTests.NoChangeSave_OverAlreadyTombstonedRows_WritesNothing.
        [Fact]
        public void TombstonedRowsInOldGraph_ArePlannedAsDeletesAgain_Observed()
        {
            var incoming = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = Subject(incoming, "Toan");
            Task(toan, "T1");
            var old = Clone(incoming);
            var deadTask = Task(old.DanhSachMonHoc[0], "T2");
            deadTask.IsDeleted = true;
            var deadMon = Subject(old, "Ly");
            deadMon.IsDeleted = true;

            var plan = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(new[] { deadMon.MaMonHoc }, plan.MonHocDeletes);
            Assert.Equal(new[] { deadTask.MaTask }, plan.TaskDeletes);
        }
    }
}
