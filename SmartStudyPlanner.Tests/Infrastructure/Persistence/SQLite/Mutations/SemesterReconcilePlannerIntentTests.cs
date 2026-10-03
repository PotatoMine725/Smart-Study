using System;
using System.Collections.Generic;
using System.Linq;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Fence;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 4: the <see cref="MutationRequest"/> the planner emits for each save
    /// topology (plan §6 intent table), with no database. Old and incoming are two separate object
    /// graphs with the same ids, the way the executor's freshly loaded graph and the caller's graph
    /// are. The Slice-3 lists stay pinned by <see cref="SemesterReconcilePlannerTests"/>, which this
    /// file does not touch.
    /// </summary>
    public class SemesterReconcilePlannerIntentTests
    {
        private static readonly Guid H = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid M1 = Guid.Parse("20000000-0000-0000-0000-000000000001");
        private static readonly Guid M2 = Guid.Parse("20000000-0000-0000-0000-000000000002");
        private static readonly Guid T1 = Guid.Parse("30000000-0000-0000-0000-000000000001");
        private static readonly Guid T2 = Guid.Parse("30000000-0000-0000-0000-000000000002");
        private static readonly Guid T3 = Guid.Parse("30000000-0000-0000-0000-000000000003");

        /// <summary>H -> { M1 -> { T1, T2 }, M2 -> { T3 } }, fresh objects on every call.</summary>
        private static HocKy Graph()
        {
            var h = new HocKy("HK", new DateTime(2026, 1, 5)) { MaHocKy = H };
            var m1 = new MonHoc("MH 1", 3) { MaMonHoc = M1, MaHocKy = H };
            var m2 = new MonHoc("MH 2", 2) { MaMonHoc = M2, MaHocKy = H };
            m1.DanhSachTask.Add(NewTask(T1, M1, "T1"));
            m1.DanhSachTask.Add(NewTask(T2, M1, "T2"));
            m2.DanhSachTask.Add(NewTask(T3, M2, "T3"));
            h.DanhSachMonHoc.Add(m1);
            h.DanhSachMonHoc.Add(m2);
            return h;
        }

        private static StudyTask NewTask(Guid id, Guid owner, string ten) =>
            new(ten, new DateTime(2026, 2, 1), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaTask = id,
                MaMonHoc = owner,
                DiemUuTien = 10,
                MucDoCanhBao = "An toàn",
            };

        private static MonHoc Mon(HocKy h, Guid id) => h.DanhSachMonHoc.Single(m => m.MaMonHoc == id);

        private static StudyTask Task(HocKy h, Guid id) =>
            h.DanhSachMonHoc.SelectMany(m => m.DanhSachTask).Single(t => t.MaTask == id);

        private static IReadOnlyList<MutationIntent> Intents(HocKy? old, HocKy incoming)
        {
            var request = SemesterReconcilePlanner.Plan(old, incoming).Request;
            Assert.Equal(MutationOrigin.LocalApplication, request.Origin);
            return request.Intents;
        }

        private static string Show(MutationIntent i) =>
            $"{i.Operation} {i.EntityType} {i.EntityId} [{string.Join(",", i.Relations.Select(r => $"{r.Field}:{r.Before}->{r.After}"))}] " +
            $"({string.Join(",", i.ChangedFields)})";

        private static void AssertIntents(IReadOnlyList<MutationIntent> actual, params MutationIntent[] expected) =>
            Assert.Equal(
                expected.Select(Show).OrderBy(s => s, StringComparer.Ordinal),
                actual.Select(Show).OrderBy(s => s, StringComparer.Ordinal));

        private static MutationIntent I(MutationOperation op, string type, Guid id, RelationChange[]? rel = null, string[]? fields = null) =>
            new(op, type, id, rel ?? Array.Empty<RelationChange>(), fields ?? Array.Empty<string>());

        [Fact]
        public void NoChange_EmitsAnEmptyRequest_N9()
        {
            Assert.Empty(Intents(Graph(), Graph()));
        }

        [Fact]
        public void DerivedOnlyChange_EmitsNoIntent()
        {
            var incoming = Graph();
            Task(incoming, T1).DiemUuTien = 99.5;
            Task(incoming, T1).MucDoCanhBao = "Khẩn cấp";
            incoming.IsSeeded = true;

            Assert.Empty(Intents(Graph(), incoming));
        }

        [Fact]
        public void NotMappedHocKyChange_EmitsNoIntent()
        {
            var incoming = Graph();
            incoming.NgayKetThuc = new DateTime(2026, 6, 30);
            incoming.IsNgayKetThucAuto = false;

            Assert.Empty(Intents(Graph(), incoming));
        }

        [Fact]
        public void CreateBranch_CreatesHocKyEveryMonHocAndEveryTask_UnderTheirNavigationOwner()
        {
            var incoming = Graph();
            Mon(incoming, M2).MaHocKy = Guid.Empty;   // a `new MonHoc` carries no FK until EF fixup
            Task(incoming, T3).MaMonHoc = Guid.Empty;  // nor does a task added only to the collection

            AssertIntents(Intents(null, incoming),
                I(MutationOperation.Create, SyncEntityTypes.HocKy, H),
                I(MutationOperation.Create, SyncEntityTypes.MonHoc, M1, new[] { new RelationChange("MaHocKy", null, H) }),
                I(MutationOperation.Create, SyncEntityTypes.MonHoc, M2, new[] { new RelationChange("MaHocKy", null, H) }),
                I(MutationOperation.Create, SyncEntityTypes.StudyTask, T1, new[] { new RelationChange("MaMonHoc", null, M1) }),
                I(MutationOperation.Create, SyncEntityTypes.StudyTask, T2, new[] { new RelationChange("MaMonHoc", null, M1) }),
                I(MutationOperation.Create, SyncEntityTypes.StudyTask, T3, new[] { new RelationChange("MaMonHoc", null, M2) }));
        }

        [Fact]
        public void HocKyMergeFieldEdit_IsUpdateFieldsOnHocKy()
        {
            var incoming = Graph();
            incoming.Ten = "HK renamed";
            incoming.NgayBatDau = new DateTime(2026, 1, 12);

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.UpdateFields, SyncEntityTypes.HocKy, H, fields: new[] { "NgayBatDau", "Ten" }));
        }

        [Fact]
        public void MonHocDelete_IsOneTombstone_ItsTasksAreLeftToTheResolversCascade()
        {
            var incoming = Graph();
            incoming.DanhSachMonHoc.Remove(Mon(incoming, M1));

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Tombstone, SyncEntityTypes.MonHoc, M1));
        }

        [Fact]
        public void MonHocAdd_IsCreateUnderTheSavedHocKy_ItsTasksAreCreates()
        {
            var incoming = Graph();
            var m3 = new MonHoc("MH 3", 4);                       // MaHocKy = Guid.Empty, as ThemMon builds it
            var t4 = new StudyTask("T4", new DateTime(2026, 3, 1), LoaiCongViec.BaiTapVeNha, 1);  // FK unstamped
            m3.DanhSachTask.Add(t4);
            incoming.DanhSachMonHoc.Add(m3);

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Create, SyncEntityTypes.MonHoc, m3.MaMonHoc, new[] { new RelationChange("MaHocKy", null, H) }),
                I(MutationOperation.Create, SyncEntityTypes.StudyTask, t4.MaTask, new[] { new RelationChange("MaMonHoc", null, m3.MaMonHoc) }));
        }

        [Fact]
        public void MonHocMergeFieldEdit_IsUpdateFieldsOnThatMonHocOnly()
        {
            var incoming = Graph();
            Mon(incoming, M2).TenMonHoc = "MH 2 renamed";
            Mon(incoming, M2).SoTinChi = 4;

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.UpdateFields, SyncEntityTypes.MonHoc, M2, fields: new[] { "SoTinChi", "TenMonHoc" }));
        }

        [Fact]
        public void MonHocStructuralFkChange_IsReparentOfMonHoc()
        {
            var other = Guid.Parse("10000000-0000-0000-0000-000000000002");
            var incoming = Graph();
            Mon(incoming, M2).MaHocKy = other;

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Reparent, SyncEntityTypes.MonHoc, M2, new[] { new RelationChange("MaHocKy", H, other) }));
        }

        [Fact]
        public void TaskMove_IsReparentOfStudyTask()
        {
            var incoming = Graph();
            var t2 = Task(incoming, T2);
            Mon(incoming, M1).DanhSachTask.Remove(t2);
            t2.MaMonHoc = M2;
            Mon(incoming, M2).DanhSachTask.Add(t2);

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Reparent, SyncEntityTypes.StudyTask, T2, new[] { new RelationChange("MaMonHoc", M1, M2) }));
        }

        [Fact]
        public void TaskDelete_IsTombstoneOfStudyTask()
        {
            var incoming = Graph();
            Mon(incoming, M1).DanhSachTask.Remove(Task(incoming, T1));

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Tombstone, SyncEntityTypes.StudyTask, T1));
        }

        [Fact]
        public void TaskAdd_UnderExistingMonHoc_IsCreateWithNavigationOwner()
        {
            var incoming = Graph();
            var t4 = new StudyTask("T4", new DateTime(2026, 3, 1), LoaiCongViec.BaiTapVeNha, 1);  // FK unstamped -> heal
            Mon(incoming, M2).DanhSachTask.Add(t4);

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Create, SyncEntityTypes.StudyTask, t4.MaTask, new[] { new RelationChange("MaMonHoc", null, M2) }));
        }

        [Fact]
        public void TaskMergeFieldEdit_IsUpdateFieldsWithOnlyTheChangedMergeFields()
        {
            var incoming = Graph();
            var t3 = Task(incoming, T3);
            t3.TenTask = "T3 renamed";
            t3.TrangThai = "Hoàn thành";
            t3.NgayHoanThanh = new DateTime(2026, 2, 3);
            t3.DiemUuTien = 77;                                   // Derived: must not appear

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.UpdateFields, SyncEntityTypes.StudyTask, T3, fields: new[] { "NgayHoanThanh", "TenTask", "TrangThai" }));
        }

        [Fact]
        public void TaskMovedAndEdited_IsReparentPlusUpdateFields()
        {
            var incoming = Graph();
            var t2 = Task(incoming, T2);
            Mon(incoming, M1).DanhSachTask.Remove(t2);
            t2.MaMonHoc = M2;
            t2.DoKho = 5;
            Mon(incoming, M2).DanhSachTask.Add(t2);

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Reparent, SyncEntityTypes.StudyTask, T2, new[] { new RelationChange("MaMonHoc", M1, M2) }),
                I(MutationOperation.UpdateFields, SyncEntityTypes.StudyTask, T2, fields: new[] { "DoKho" }));
        }

        [Fact]
        public void TaskOutOfMonHocDeletedInSameSave_IsReparent_AndTheDeletedMonHocATombstone()
        {
            var incoming = Graph();
            var t1 = Task(incoming, T1);
            var m1 = Mon(incoming, M1);
            m1.DanhSachTask.Remove(t1);
            t1.MaMonHoc = M2;
            Mon(incoming, M2).DanhSachTask.Add(t1);
            incoming.DanhSachMonHoc.Remove(m1);                    // T2 goes with M1

            AssertIntents(Intents(Graph(), incoming),
                I(MutationOperation.Tombstone, SyncEntityTypes.MonHoc, M1),
                I(MutationOperation.Reparent, SyncEntityTypes.StudyTask, T1, new[] { new RelationChange("MaMonHoc", M1, M2) }));
        }

        [Fact]
        public void TaskReferencingUnknownMonHoc_StillEmitsItsCreate_TheValidationErrorStaysForTheWriter()
        {
            var unknown = Guid.Parse("20000000-0000-0000-0000-0000000000ff");
            var incoming = Graph();
            var t4 = new StudyTask("T4", new DateTime(2026, 3, 1), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = unknown };
            Mon(incoming, M2).DanhSachTask.Add(t4);

            var plan = SemesterReconcilePlanner.Plan(Graph(), incoming);

            Assert.Single(plan.ValidationErrors, e => e.Kind == PlanValidationKind.UnknownMonHoc);
            AssertIntents(plan.Request.Intents,
                I(MutationOperation.Create, SyncEntityTypes.StudyTask, t4.MaTask, new[] { new RelationChange("MaMonHoc", null, unknown) }));
        }

        [Fact]
        public void EarlyReturnValidationError_EmitsAnEmptyRequest()
        {
            var incoming = Graph();
            incoming.DanhSachMonHoc.Add(new MonHoc("dup", 1) { MaMonHoc = M1, MaHocKy = H });
            Mon(incoming, M2).DanhSachTask.Remove(Task(incoming, T3));   // a real change, still not emitted

            var plan = SemesterReconcilePlanner.Plan(Graph(), incoming);

            Assert.NotEmpty(plan.ValidationErrors);
            Assert.Empty(plan.Request.Intents);
        }

        [Fact]
        public void Planning_DoesNotMutateEitherGraph()
        {
            var old = Graph();
            var incoming = Graph();
            var t4 = new StudyTask("T4", new DateTime(2026, 3, 1), LoaiCongViec.BaiTapVeNha, 1);
            Mon(incoming, M2).DanhSachTask.Add(t4);
            incoming.Ten = "renamed";

            _ = SemesterReconcilePlanner.Plan(old, incoming);

            Assert.Equal(Guid.Empty, t4.MaMonHoc);
            Assert.Equal("HK", old.Ten);
        }
    }
}
