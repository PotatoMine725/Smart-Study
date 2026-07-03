using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using Xunit;

namespace SmartStudyPlanner.Tests.Data
{
    /// <summary>
    /// M1.1 stamping seam, tested against a test-only entity + test DbContext subclass —
    /// no production entity implements ISyncMetadata until M1.2's T1.1, so this keeps the
    /// milestone boundary clean instead of pulling entity-field work forward.
    /// </summary>
    public class SyncMetadataStampingTests
    {
        private sealed class TestSyncEntity : ISyncMetadata
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public string? Name { get; set; }
            public long Rev { get; set; }
            public DateTime ModifiedAtUtc { get; set; }
            public string ModifiedByDeviceId { get; set; } = string.Empty;
            public bool IsDeleted { get; set; }
            public DateTime? DeletedAtUtc { get; set; }
        }

        private sealed class PlainEntity
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public string? Name { get; set; }
        }

        private sealed class StampingTestDbContext : DbContext
        {
            public DbSet<TestSyncEntity> SyncedEntities => Set<TestSyncEntity>();
            public DbSet<PlainEntity> PlainEntities => Set<PlainEntity>();
            public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;
            public string DeviceId { get; set; } = "test-device";

            public StampingTestDbContext(DbContextOptions options) : base(options) { }

            public override int SaveChanges(bool acceptAllChangesOnSuccess)
            {
                SyncStamper.Apply(ChangeTracker, Clock, DeviceId);
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }

            public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, System.Threading.CancellationToken ct = default)
            {
                SyncStamper.Apply(ChangeTracker, Clock, DeviceId);
                return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            }
        }

        private static (SqliteConnection conn, StampingTestDbContext ctx) NewContext()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            var opts = new DbContextOptionsBuilder<StampingTestDbContext>().UseSqlite(conn).Options;
            var ctx = new StampingTestDbContext(opts);
            ctx.Database.EnsureCreated();
            return (conn, ctx);
        }

        [Fact]
        public async Task Add_StampsRevOneAndMetadata()
        {
            var (conn, ctx) = NewContext();
            using var _c = conn;
            using var _ctx = ctx;
            ctx.Clock = () => new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);
            ctx.DeviceId = "desktop-abc123";

            var entity = new TestSyncEntity { Name = "A" };
            ctx.SyncedEntities.Add(entity);
            await ctx.SaveChangesAsync();

            Assert.Equal(1, entity.Rev);
            Assert.Equal(new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc), entity.ModifiedAtUtc);
            Assert.Equal("desktop-abc123", entity.ModifiedByDeviceId);
        }

        [Fact]
        public async Task Modify_IncrementsRevAndUpdatesStamp()
        {
            var (conn, ctx) = NewContext();
            using var _c = conn;
            using var _ctx = ctx;
            ctx.Clock = () => new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);

            var entity = new TestSyncEntity { Name = "A" };
            ctx.SyncedEntities.Add(entity);
            await ctx.SaveChangesAsync();
            Assert.Equal(1, entity.Rev);

            ctx.Clock = () => new DateTime(2026, 7, 3, 11, 0, 0, DateTimeKind.Utc);
            entity.Name = "B";
            await ctx.SaveChangesAsync();

            Assert.Equal(2, entity.Rev);
            Assert.Equal(new DateTime(2026, 7, 3, 11, 0, 0, DateTimeKind.Utc), entity.ModifiedAtUtc);
        }

        [Fact]
        public async Task Add_MultipleEntities_EachStampedIndependently()
        {
            var (conn, ctx) = NewContext();
            using var _c = conn;
            using var _ctx = ctx;

            var e1 = new TestSyncEntity { Name = "A" };
            var e2 = new TestSyncEntity { Name = "B" };
            ctx.SyncedEntities.AddRange(e1, e2);
            await ctx.SaveChangesAsync();

            Assert.Equal(1, e1.Rev);
            Assert.Equal(1, e2.Rev);
        }

        [Fact]
        public async Task MixedSave_OnlyStampsSyncMetadataEntities()
        {
            var (conn, ctx) = NewContext();
            using var _c = conn;
            using var _ctx = ctx;

            var synced = new TestSyncEntity { Name = "A" };
            var plain = new PlainEntity { Name = "B" };
            ctx.SyncedEntities.Add(synced);
            ctx.PlainEntities.Add(plain);
            await ctx.SaveChangesAsync();

            Assert.Equal(1, synced.Rev);
        }
    }
}
