using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Models.Telemetry;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// Sync-metadata stamping seam (M1.1) + Deleted-state cascade-tombstone (M1.2, G1),
    /// driven through the real AppDbContext + real ISyncMetadata entities (HocKy, MonHoc) —
    /// closes the M1.1 review's R2 finding (no parallel test DbContext/entity anymore).
    /// </summary>
    public class SyncMetadataStampingTests : IDisposable
    {
        private readonly SqliteConnection _conn;

        public SyncMetadataStampingTests()
        {
            _conn = TestDb.OpenConnection();
        }

        public void Dispose() => _conn.Dispose();

        private AppDbContext NewCtx() => TestDb.Create(_conn);

        [Fact]
        public async Task Add_StampsRevOneAndMetadata()
        {
            using var ctx = NewCtx();
            ctx.Clock = () => new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);

            var hocKy = new HocKy("HK", DateTime.Today);
            ctx.HocKys.Add(hocKy);
            await ctx.SaveChangesAsync();

            Assert.Equal(1, hocKy.Rev);
            Assert.Equal(new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc), hocKy.ModifiedAtUtc);
            Assert.False(string.IsNullOrEmpty(hocKy.ModifiedByDeviceId));
        }

        [Fact]
        public async Task Modify_IncrementsRevAndUpdatesStamp()
        {
            using var ctx = NewCtx();
            ctx.Clock = () => new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);

            var hocKy = new HocKy("HK", DateTime.Today);
            ctx.HocKys.Add(hocKy);
            await ctx.SaveChangesAsync();
            Assert.Equal(1, hocKy.Rev);

            ctx.Clock = () => new DateTime(2026, 7, 3, 11, 0, 0, DateTimeKind.Utc);
            hocKy.Ten = "HK2";
            await ctx.SaveChangesAsync();

            Assert.Equal(2, hocKy.Rev);
            Assert.Equal(new DateTime(2026, 7, 3, 11, 0, 0, DateTimeKind.Utc), hocKy.ModifiedAtUtc);
        }

        [Fact]
        public async Task Add_MultipleEntities_EachStampedIndependently()
        {
            using var ctx = NewCtx();
            var h1 = new HocKy("HK1", DateTime.Today);
            var h2 = new HocKy("HK2", DateTime.Today);
            ctx.HocKys.AddRange(h1, h2);
            await ctx.SaveChangesAsync();

            Assert.Equal(1, h1.Rev);
            Assert.Equal(1, h2.Rev);
        }

        [Fact]
        public async Task MixedSave_OnlyStampsSyncMetadataEntities()
        {
            using var ctx = NewCtx();
            var hocKy = new HocKy("HK", DateTime.Today);
            ctx.HocKys.Add(hocKy);
            ctx.DifficultyLabelLogs.Add(new DifficultyLabelLog { Id = Guid.NewGuid() });
            await ctx.SaveChangesAsync();

            Assert.Equal(1, hocKy.Rev);
        }

        [Fact]
        public async Task Delete_TombstonesInsteadOfHardDelete()
        {
            var hocKyId = Guid.Empty;
            using (var ctx = NewCtx())
            {
                var hocKy = new HocKy("HK", DateTime.Today);
                ctx.HocKys.Add(hocKy);
                await ctx.SaveChangesAsync();
                hocKyId = hocKy.MaHocKy;
            }

            using (var ctx2 = NewCtx())
            {
                var tracked = await ctx2.HocKys.FirstAsync(h => h.MaHocKy == hocKyId);
                ctx2.HocKys.Remove(tracked);
                await ctx2.SaveChangesAsync();
            }

            using var ctx3 = NewCtx();
            var stillThere = await ctx3.HocKys.FirstOrDefaultAsync(h => h.MaHocKy == hocKyId);
            Assert.NotNull(stillThere);
            Assert.True(stillThere!.IsDeleted);
            Assert.NotNull(stillThere.DeletedAtUtc);
            Assert.Equal(2, stillThere.Rev);
        }

        [Fact]
        public async Task Delete_CascadesTombstoneToLoadedChildren()
        {
            Guid hocKyId, monHocId;
            using (var ctx = NewCtx())
            {
                var hocKy = new HocKy("HK", DateTime.Today);
                var monHoc = new MonHoc("MH", 3) { MaHocKy = hocKy.MaHocKy };
                hocKy.DanhSachMonHoc.Add(monHoc);
                ctx.HocKys.Add(hocKy);
                await ctx.SaveChangesAsync();
                hocKyId = hocKy.MaHocKy;
                monHocId = monHoc.MaMonHoc;
            }

            using (var ctx2 = NewCtx())
            {
                var tracked = await ctx2.HocKys.Include(h => h.DanhSachMonHoc).FirstAsync(h => h.MaHocKy == hocKyId);
                ctx2.HocKys.Remove(tracked);
                await ctx2.SaveChangesAsync();
            }

            using var ctx3 = NewCtx();
            var monStillThere = await ctx3.MonHocs.FirstOrDefaultAsync(m => m.MaMonHoc == monHocId);
            Assert.NotNull(monStillThere);
            Assert.True(monStillThere!.IsDeleted);
        }
    }
}
