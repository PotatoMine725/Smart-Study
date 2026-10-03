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
    /// D-2 (fence plan §4.2, §19 R-9): a local save must not write to rows that are already tombstoned.
    /// The ruled cascade predicate is <c>child.IsDeleted == false</c> (Slice-2 owner rulings §1, M-3/A-1),
    /// and a tombstoned TaskNote keeps occupying <c>UNIQUE(MaTask)</c> (§1.2, D9-T1).
    /// <para>
    /// Every save here runs under its own device id and clock value, so a re-stamp of
    /// <c>Rev</c>, <c>ModifiedAtUtc</c>, <c>DeletedAtUtc</c> or <c>ModifiedByDeviceId</c> cannot hide
    /// behind an equal value written by an earlier save.
    /// </para>
    /// </summary>
    public class TombstoneRestampTests : IDisposable
    {
        private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly SqliteConnection _conn;
        private DateTime _now = Epoch;
        private string _device = "dev-0";

        public TombstoneRestampTests()
        {
            _conn = TestDb.OpenConnection();
            using (TestDb.Create(_conn)) { /* EnsureCreated */ }
        }

        public void Dispose() => _conn.Dispose();

        private AppDbContext NewContext()
        {
            var ctx = TestDb.Create(_conn);
            var now = _now;
            var device = _device;
            ctx.Clock = () => now;
            ctx.DeviceIdProvider = () => device;
            return ctx;
        }

        private void At(int minute, string device)
        {
            _now = Epoch.AddMinutes(minute);
            _device = device;
        }

        private SqliteHocKyRepository HocKyRepo() => new(NewContext);

        private static StudyTask NewTask(MonHoc mon, string ten)
        {
            var task = new StudyTask(ten, new DateTime(2026, 2, 1), LoaiCongViec.BaiTapVeNha, 2)
            {
                MaMonHoc = mon.MaMonHoc,
                MucDoCanhBao = "An toàn",
            };
            mon.DanhSachTask.Add(task);
            return task;
        }

        private static MonHoc NewMon(HocKy hk, string ten)
        {
            var mon = new MonHoc(ten, 3) { MaHocKy = hk.MaHocKy };
            hk.DanhSachMonHoc.Add(mon);
            return mon;
        }

        private async Task<(Guid NoteId, Guid LinkId)> AddNoteAndLinkAsync(StudyTask task)
        {
            using var ctx = NewContext();
            var note = new TaskNote { MaTask = task.MaTask, Content = "note(" + task.TenTask + ")" };
            var link = new TaskReferenceLink { MaTask = task.MaTask, Title = "link(" + task.TenTask + ")", Url = "https://example.test/" + task.TenTask };
            ctx.TaskNotes.Add(note);
            ctx.TaskReferenceLinks.Add(link);
            await ctx.SaveChangesAsync();
            return (note.Id, link.Id);
        }

        private async Task TombstoneNoteAndLinkDirectlyAsync(Guid noteId, Guid linkId)
        {
            using var ctx = NewContext();
            ctx.TaskNotes.Remove(await ctx.TaskNotes.FirstAsync(n => n.Id == noteId));
            ctx.TaskReferenceLinks.Remove(await ctx.TaskReferenceLinks.FirstAsync(l => l.Id == linkId));
            await ctx.SaveChangesAsync();
        }

        private sealed record Stamp(bool IsDeleted, long Rev, DateTime ModifiedAtUtc, DateTime? DeletedAtUtc, string ModifiedByDeviceId);

        private static Stamp Of(ISyncMetadata m) => new(m.IsDeleted, m.Rev, m.ModifiedAtUtc, m.DeletedAtUtc, m.ModifiedByDeviceId);

        private async Task<Dictionary<Guid, Stamp>> StampsAsync()
        {
            using var ctx = NewContext();
            var all = new Dictionary<Guid, Stamp>();
            foreach (var r in await ctx.HocKys.AsNoTracking().ToListAsync()) all[r.MaHocKy] = Of(r);
            foreach (var r in await ctx.MonHocs.AsNoTracking().ToListAsync()) all[r.MaMonHoc] = Of(r);
            foreach (var r in await ctx.StudyTasks.AsNoTracking().ToListAsync()) all[r.MaTask] = Of(r);
            foreach (var r in await ctx.TaskNotes.AsNoTracking().ToListAsync()) all[r.Id] = Of(r);
            foreach (var r in await ctx.TaskReferenceLinks.AsNoTracking().ToListAsync()) all[r.Id] = Of(r);
            return all;
        }

        private void AssertTombstonedAt(Stamp s, int minute, string device)
        {
            Assert.True(s.IsDeleted);
            Assert.Equal(Epoch.AddMinutes(minute), s.DeletedAtUtc);
            Assert.Equal(Epoch.AddMinutes(minute), s.ModifiedAtUtc);
            Assert.Equal(device, s.ModifiedByDeviceId);
        }

        // ---------------------------------------------------------------------------------------

        [Fact]
        public async Task NoChangeSave_LeavesTombstonesByteIdentical()
        {
            var hk = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = NewMon(hk, "Toan");
            NewTask(toan, "T1");
            var t2 = NewTask(toan, "T2");
            var ly = NewMon(hk, "Ly");
            var l1 = NewTask(ly, "L1");

            At(1, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);
            At(2, "dev-A");
            await AddNoteAndLinkAsync(t2);
            await AddNoteAndLinkAsync(l1);

            // Task-level (T2 + note/link) and MonHoc-level (Ly -> L1 + note/link) tombstones.
            toan.DanhSachTask.Remove(t2);
            hk.DanhSachMonHoc.Remove(ly);
            At(3, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);

            var before = await StampsAsync();
            Assert.Equal(7, before.Values.Count(s => s.IsDeleted)); // precondition: Ly, L1, T2, 2 notes, 2 links

            At(4, "dev-B");
            await HocKyRepo().LuuHocKyAsync(hk); // nothing changed in the caller's graph
            var after = await StampsAsync();

            Assert.Equal(before, after);
        }

        [Fact]
        public async Task DeleteLiveMonHoc_TombstonesLiveChildren_LeavesDeadChildrenUntouched()
        {
            var hk = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = NewMon(hk, "Toan");
            var t1 = NewTask(toan, "T1");   // live, live note/link       -> must be tombstoned
            var t2 = NewTask(toan, "T2");   // already dead, + note/link  -> must stay byte-identical
            var t3 = NewTask(toan, "T3");   // live, but DEAD note/link   -> task tombstoned, children untouched
            var ly = NewMon(hk, "Ly");
            var s1 = NewTask(ly, "S1");     // sibling control

            At(1, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);
            At(2, "dev-A");
            var (n1, k1) = await AddNoteAndLinkAsync(t1);
            var (n2, k2) = await AddNoteAndLinkAsync(t2);
            var (n3, k3) = await AddNoteAndLinkAsync(t3);
            var (ns, ks) = await AddNoteAndLinkAsync(s1);

            toan.DanhSachTask.Remove(t2);
            At(3, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);
            At(4, "dev-A");
            await TombstoneNoteAndLinkDirectlyAsync(n3, k3);

            var before = await StampsAsync();
            Assert.True(before[t2.MaTask].IsDeleted && before[n2].IsDeleted && before[k2].IsDeleted);
            Assert.True(before[n3].IsDeleted && before[k3].IsDeleted && !before[t3.MaTask].IsDeleted);

            hk.DanhSachMonHoc.Remove(toan);
            At(5, "dev-B");
            await HocKyRepo().LuuHocKyAsync(hk);
            var after = await StampsAsync();

            // Every LIVE row under the deleted MonHoc is tombstoned by this save.
            foreach (var id in new[] { toan.MaMonHoc, t1.MaTask, n1, k1, t3.MaTask })
                AssertTombstonedAt(after[id], 5, "dev-B");

            // Rows that were already dead are not written again.
            foreach (var id in new[] { t2.MaTask, n2, k2, n3, k3 })
                Assert.Equal(before[id], after[id]);

            // Sibling MonHoc untouched.
            foreach (var id in new[] { ly.MaMonHoc, s1.MaTask, ns, ks })
                Assert.Equal(before[id], after[id]);
        }

        [Fact]
        public async Task D9T1_DeadNoteStillOccupiesUniqueMaTask_AfterSave()
        {
            var hk = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = NewMon(hk, "Toan");
            var t1 = NewTask(toan, "T1");
            var t2 = NewTask(toan, "T2");

            At(1, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);
            At(2, "dev-A");
            var (n2, _) = await AddNoteAndLinkAsync(t2);
            toan.DanhSachTask.Remove(t2);
            At(3, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);
            At(4, "dev-B");
            await HocKyRepo().LuuHocKyAsync(hk);

            using (var ctx = NewContext())
            {
                var dead = await ctx.TaskNotes.AsNoTracking().SingleAsync(n => n.Id == n2);
                Assert.True(dead.IsDeleted);
                Assert.Equal(t2.MaTask, dead.MaTask);
            }

            // The dead note still occupies its scope: a second note at T2 is rejected by the database.
            using (var clash = NewContext())
            {
                clash.TaskNotes.Add(new TaskNote { MaTask = t2.MaTask, Content = "intruder" });
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => clash.SaveChangesAsync());
                Assert.Contains("UNIQUE constraint failed: TaskNotes.MaTask", ex.InnerException?.Message);
            }

            // Control: the same insert at a task with no note succeeds, so the rejection above is the
            // occupancy and not something else wrong with the inserted row.
            using (var ok = NewContext())
            {
                ok.TaskNotes.Add(new TaskNote { MaTask = t1.MaTask, Content = "first note" });
                await ok.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task StudyTaskRepositoryDeleteAsync_SkipsAlreadyDeadNote_TombstonesLiveLink()
        {
            var hk = new HocKy("HK1", new DateTime(2026, 1, 5));
            var toan = NewMon(hk, "Toan");
            var t1 = NewTask(toan, "T1");

            At(1, "dev-A");
            await HocKyRepo().LuuHocKyAsync(hk);
            At(2, "dev-A");
            var (n1, _) = await AddNoteAndLinkAsync(t1);
            Guid liveLink;
            using (var ctx = NewContext())
            {
                var extra = new TaskReferenceLink { MaTask = t1.MaTask, Title = "live", Url = "https://example.test/live" };
                ctx.TaskReferenceLinks.Add(extra);
                await ctx.SaveChangesAsync();
                liveLink = extra.Id;
            }
            At(3, "dev-A");
            using (var ctx = NewContext())
            {
                ctx.TaskNotes.Remove(await ctx.TaskNotes.FirstAsync(n => n.Id == n1));
                await ctx.SaveChangesAsync();
            }

            var before = await StampsAsync();

            At(4, "dev-B");
            await new SqliteStudyTaskRepository(NewContext).DeleteAsync(t1.MaTask);
            var after = await StampsAsync();

            Assert.Equal(before[n1], after[n1]);
            AssertTombstonedAt(after[liveLink], 4, "dev-B");
            AssertTombstonedAt(after[t1.MaTask], 4, "dev-B");
        }
    }
}
