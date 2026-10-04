using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// E-2 (c) — <c>docs/specs/2026-10-03-fence-slice4-followup-owner-rulings.md</c> §1, with the
    /// owner's 2026-10-03 decisions D-2 (a property is modified when its value differs from the
    /// original by EF's value comparer) and D-4 (a save in which no value changed is the vacuous case
    /// and is not stamped either). A local entry whose only changed values are
    /// <c>FieldClass.Derived</c> persists them and keeps Rev / ModifiedAtUtc / ModifiedByDeviceId.
    /// <para>
    /// Rows are seeded at <see cref="SyncApplyFixture.LocalEarlier"/> by
    /// <see cref="SyncApplyFixture.LocalDevice"/>; the save under test runs on a SECOND local identity
    /// (<see cref="Later"/>, <see cref="OtherDevice"/>) so each of the three stamp columns can show a
    /// restamp on its own. With one identity a restamped ModifiedByDeviceId writes the same bytes.
    /// </para>
    /// </summary>
    public class SyncStamperDerivedOnlyTests : IDisposable
    {
        private static readonly DateTime Later = SyncApplyFixture.LocalNow.AddHours(1);
        private const string OtherDevice = "OTHER-LOCAL";

        private readonly SyncApplyFixture _fx = new();
        private readonly LocalSaveDriver _save;
        private readonly SqliteHocKyRepository _other;

        public SyncStamperDerivedOnlyTests()
        {
            _save = new LocalSaveDriver(_fx);
            _other = new SqliteHocKyRepository(() => _fx.NewContext(Later, OtherDevice));
        }

        public void Dispose() => _fx.Dispose();

        private static void AssertNotStamped(ISyncMetadata expected, ISyncMetadata actual)
        {
            Assert.Equal(expected.Rev, actual.Rev);
            Assert.Equal(expected.ModifiedAtUtc, actual.ModifiedAtUtc);
            Assert.Equal(expected.ModifiedByDeviceId, actual.ModifiedByDeviceId);
        }

        private static void AssertStampedByOther(long expectedRev, ISyncMetadata actual)
        {
            Assert.Equal(expectedRev, actual.Rev);
            Assert.Equal(Later, actual.ModifiedAtUtc);
            Assert.Equal(OtherDevice, actual.ModifiedByDeviceId);
        }

        // ---------------------------------------------------------------- Derived-only: not stamped

        [Fact]
        public async Task DerivedOnlyEdit_StudyTask_PersistsValues_WithoutStamping()
        {
            var (hocKy, _, task) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadTaskAsync(task.MaTask))!;

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var t = LocalSaveDriver.Task(graph, task.MaTask);
            t.DiemUuTien = before.DiemUuTien + 12.5;
            t.MucDoCanhBao = "Khẩn cấp";
            await _other.LuuHocKyAsync(graph);

            var after = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.Equal(before.DiemUuTien + 12.5, after.DiemUuTien);
            Assert.Equal("Khẩn cấp", after.MucDoCanhBao);
            AssertNotStamped(before, after);
        }

        [Fact]
        public async Task DerivedOnlyEdit_HocKyIsSeeded_PersistsValue_WithoutStamping()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!;
            Assert.False(before.IsSeeded);

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            graph.IsSeeded = true;
            await _other.LuuHocKyAsync(graph);

            var after = (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!;
            Assert.True(after.IsSeeded);
            AssertNotStamped(before, after);
        }

        // ---------------------------------------------------------------- everything else: stamped as before

        [Fact]
        public async Task MixedEdit_DerivedPlusMerge_IsStamped()
        {
            var (hocKy, _, task) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadTaskAsync(task.MaTask))!;

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var t = LocalSaveDriver.Task(graph, task.MaTask);
            t.DiemUuTien = before.DiemUuTien + 3;
            t.TenTask = "renamed";
            await _other.LuuHocKyAsync(graph);

            var after = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.Equal("renamed", after.TenTask);
            AssertStampedByOther(before.Rev + 1, after);
        }

        [Fact]
        public async Task AddedEntity_IsStamped()
        {
            var (_, monHoc, _) = await _fx.SeedTreeAsync();
            var added = new StudyTask("new", new DateTime(2026, 3, 3), LoaiCongViec.BaiTapVeNha, 1) { MaMonHoc = monHoc.MaMonHoc };

            using (var ctx = _fx.NewContext(Later, OtherDevice))
            {
                ctx.StudyTasks.Add(added);
                await ctx.SaveChangesAsync();
            }

            AssertStampedByOther(1, (await _fx.ReadTaskAsync(added.MaTask))!);
        }

        [Fact]
        public async Task LocalDelete_StillBecomesStampedTombstone()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadTaskAsync(task.MaTask))!;

            using (var ctx = _fx.NewContext(Later, OtherDevice))
            {
                ctx.StudyTasks.Remove(await ctx.StudyTasks.FirstAsync(x => x.MaTask == task.MaTask));
                await ctx.SaveChangesAsync();
            }

            var after = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.True(after.IsDeleted);
            Assert.Equal(Later, after.DeletedAtUtc);
            AssertStampedByOther(before.Rev + 1, after);
        }

        /// <summary>
        /// A Tombstone-class value change on the unmarked Modified path is a non-Derived change and
        /// stamps (rulings §1). <c>Remove()</c> above takes the Deleted branch and never reaches the
        /// Derived-only check; setting <c>IsDeleted</c> on a tracked row does. Review PR #110 F-2: the
        /// mutant "a Tombstone-class change is ignorable" survived the suite before this test.
        /// </summary>
        [Fact]
        public async Task LocalIsDeletedSet_OnTrackedRow_NotViaRemove_IsStamped()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadTaskAsync(task.MaTask))!;

            using (var ctx = _fx.NewContext(Later, OtherDevice))
            {
                var tracked = await ctx.StudyTasks.FirstAsync(x => x.MaTask == task.MaTask);
                tracked.IsDeleted = true;
                tracked.DeletedAtUtc = Later;
                await ctx.SaveChangesAsync();
            }

            var after = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.True(after.IsDeleted);
            Assert.Equal(Later, after.DeletedAtUtc);
            AssertStampedByOther(before.Rev + 1, after);
        }

        /// <summary>
        /// The marked sync-apply path is untouched: a marked entry whose only change is Derived still
        /// gets the local Rev++ and keeps the provenance it carries (DoR §10.1).
        /// </summary>
        [Fact]
        public async Task MarkedSyncApplyEntry_DerivedOnly_StillIncrementsRev_AndKeepsProvenance()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!;

            using (var ctx = _fx.NewContext(Later, OtherDevice))
            {
                var tracked = await ctx.HocKys.FirstAsync(h => h.MaHocKy == hocKy.MaHocKy);
                tracked.IsSeeded = true;
                ctx.MarkSyncApplied(tracked);
                await ctx.SaveChangesAsync();
            }

            var after = (await _fx.ReadHocKyAsync(hocKy.MaHocKy))!;
            Assert.True(after.IsSeeded);
            Assert.Equal(before.Rev + 1, after.Rev);
            Assert.Equal(before.ModifiedAtUtc, after.ModifiedAtUtc);
            Assert.Equal(before.ModifiedByDeviceId, after.ModifiedByDeviceId);
        }

        // ---------------------------------------------------------------- D-2: value comparison, not IsModified

        /// <summary>
        /// The caller graph keeps its pre-save Rev/ModifiedAtUtc after a stamped save, so copying it
        /// onto fresh rows again flags those columns modified with unchanged values. Only the values
        /// count: the second save changed nothing but a Derived field and must not be stamped. Under an
        /// IsModified-only rule the stale Rev counts as a non-Derived change and the row is stamped.
        /// </summary>
        [Fact]
        public async Task DerivedOnlyEdit_AfterAStampedSave_FromTheSameGraph_IsNotStamped()
        {
            var (hocKy, _, task) = await _fx.SeedTreeAsync();

            var graph = await _save.LoadAsync(hocKy.MaHocKy);
            var t = LocalSaveDriver.Task(graph, task.MaTask);
            t.TenTask = "first edit";
            await _save.SaveAsync(graph);                                           // stamped (LocalNow, LOCAL-DEVICE)
            var afterFirst = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.Equal(SyncApplyFixture.LocalNow, afterFirst.ModifiedAtUtc);

            t.DiemUuTien += 5;
            await _other.LuuHocKyAsync(graph);                                      // same graph, Derived-only

            var after = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.Equal(afterFirst.DiemUuTien + 5, after.DiemUuTien);
            AssertNotStamped(afterFirst, after);
        }

        /// <summary>
        /// Fail-safe for entries EF attached without reading the database (<c>DbSet.Update</c> on a
        /// detached instance): every property is flagged modified with Original == Current, so value
        /// comparison cannot see the real change. Such an entry must still be stamped, or the edit
        /// would never be enumerated for sync. <c>SqliteStudyTaskRepository.UpdateAsync</c> is the one
        /// such path (no production caller on origin/dev 3c49924).
        /// </summary>
        [Fact]
        public async Task DetachedUpdate_WithARealChange_IsStamped()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var before = (await _fx.ReadTaskAsync(task.MaTask))!;

            var detached = (await _fx.ReadTaskAsync(task.MaTask))!;
            detached.TenTask = "updated detached";
            await new SqliteStudyTaskRepository(() => _fx.NewContext(Later, OtherDevice)).UpdateAsync(detached);

            var after = (await _fx.ReadTaskAsync(task.MaTask))!;
            Assert.Equal("updated detached", after.TenTask);
            AssertStampedByOther(before.Rev + 1, after);
        }
    }
}
