using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Tests.Fixtures;
using SmartStudyPlanner.ViewModels;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence
{
    /// <summary>
    /// PR-A (DoR §14.1, OPEN-8): <see cref="SqliteTaskEditorRepository.UpdateLinkAsync"/> used to be
    /// handed a *detached* POCO built by <see cref="TaskReferenceLinkItemVm.ToModel"/>, which carries
    /// Rev = 0, empty sync provenance and CreatedAtUtc = UtcNow. <c>DbSet.Update()</c> marks every
    /// property Modified, so SaveChanges rewrote CreatedAtUtc (creation provenance, immutable per
    /// D9-T3) and reset the Rev watermark that T2.2 change-enumeration depends on.
    ///
    /// These tests pin the *local save* semantics of the update path. They assert nothing about
    /// merge/sync semantics (T2.3/T2.4) and do not exercise SyncStamper's contract beyond the
    /// pre-existing "one local write ⇒ Rev++" rule already covered by SyncMetadataStampingTests.
    /// </summary>
    public class TaskReferenceLinkUpdateTests : IDisposable
    {
        // Distinctly old so a rewrite of CreatedAtUtc is unambiguous evidence, not a millisecond delta.
        private static readonly DateTime C0 = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T0 = new(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T1 = new(2026, 7, 3, 11, 0, 0, DateTimeKind.Utc);

        private readonly SqliteConnection _conn;
        private DateTime _now = T0;

        public TaskReferenceLinkUpdateTests()
        {
            _conn = TestDb.OpenConnection();
            using var seed = TestDb.Create(_conn); // EnsureCreated
        }

        public void Dispose() => _conn.Dispose();

        /// <summary>Context factory with an injected clock — same seam as SyncMetadataStampingTests.</summary>
        private Func<AppDbContext> Factory() => () =>
        {
            var ctx = TestDb.Create(_conn);
            ctx.Clock = () => _now;
            return ctx;
        };

        private async Task<Guid> SeedTaskAsync()
        {
            using var ctx = Factory()();
            var task = await TestDb.SeedTaskAsync(ctx);
            return task.MaTask;
        }

        private async Task<TaskReferenceLink> SeedLinkAsync(SqliteTaskEditorRepository repo, Guid maTask)
        {
            await repo.AddLinkAsync(new TaskReferenceLink
            {
                MaTask = maTask,
                Title = "Docs",
                Url = "https://example.test/v1",
                Category = "ref",
                SortOrder = 0,
                CreatedAtUtc = C0,
            });
            var links = await repo.GetLinksAsync(maTask);
            return Assert.Single(links);
        }

        /// <summary>
        /// The exact VM round-trip from QuanLyTaskViewModel.LuuTask: FromModel → edit → ToModel →
        /// UpdateLinkAsync. RED on the unmodified tree (Rev collapses to 1, CreatedAtUtc rewritten to T1).
        /// </summary>
        [Fact]
        public async Task UpdateLinkAsync_ViaViewModelPath_IncrementsRevAndPreservesCreatedAtUtc()
        {
            var maTask = await SeedTaskAsync();
            var repo = new SqliteTaskEditorRepository(Factory());

            var inserted = await SeedLinkAsync(repo, maTask);
            Assert.Equal(1, inserted.Rev);
            Assert.Equal(C0, inserted.CreatedAtUtc);

            _now = T1;
            var vm = TaskReferenceLinkItemVm.FromModel(inserted);
            vm.Title = "Docs v2";
            await repo.UpdateLinkAsync(vm.ToModel());

            var after = Assert.Single(await repo.GetLinksAsync(maTask));
            Assert.Equal("Docs v2", after.Title);
            Assert.Equal(2, after.Rev);                       // continues from the existing watermark
            Assert.Equal(C0, after.CreatedAtUtc);             // creation provenance immutable (D9-T3)
            Assert.Equal(T1, after.ModifiedAtUtc);            // legitimate local modification stamped
            Assert.Equal(inserted.Id, after.Id);
            Assert.False(after.IsDeleted);
        }

        /// <summary>Every editable field on the VM surface must actually reach the row.</summary>
        [Fact]
        public async Task UpdateLinkAsync_UpdatesAllEditableFields()
        {
            var maTask = await SeedTaskAsync();
            var repo = new SqliteTaskEditorRepository(Factory());
            var inserted = await SeedLinkAsync(repo, maTask);

            _now = T1;
            await repo.UpdateLinkAsync(new TaskReferenceLink
            {
                Id = inserted.Id,
                MaTask = maTask,
                Title = "Docs v2",
                Url = "https://example.test/v2",
                Category = "cheatsheet",
                SortOrder = 7,
                CreatedAtUtc = DateTime.UtcNow, // detached POCO lies about creation time — must be ignored
            });

            var after = Assert.Single(await repo.GetLinksAsync(maTask));
            Assert.Equal("Docs v2", after.Title);
            Assert.Equal("https://example.test/v2", after.Url);
            Assert.Equal("cheatsheet", after.Category);
            Assert.Equal(7, after.SortOrder);
            Assert.Equal(C0, after.CreatedAtUtc);
        }

        /// <summary>
        /// A detached POCO carrying the wrong owner must not reparent the row. RED on the unmodified
        /// tree: DbSet.Update marks MaTask Modified and the link silently moves to the other task.
        /// </summary>
        [Fact]
        public async Task UpdateLinkAsync_DoesNotReassignMaTask()
        {
            var maTask = await SeedTaskAsync();
            var otherTask = await SeedTaskAsync();
            Assert.NotEqual(maTask, otherTask);

            var repo = new SqliteTaskEditorRepository(Factory());
            var inserted = await SeedLinkAsync(repo, maTask);

            _now = T1;
            await repo.UpdateLinkAsync(new TaskReferenceLink
            {
                Id = inserted.Id,
                MaTask = otherTask,
                Title = "Docs v2",
                Url = inserted.Url,
                Category = inserted.Category,
                SortOrder = inserted.SortOrder,
            });

            var after = Assert.Single(await repo.GetLinksAsync(maTask));
            Assert.Equal(maTask, after.MaTask);
            Assert.Equal("Docs v2", after.Title);
            Assert.Empty(await repo.GetLinksAsync(otherTask));
        }

        /// <summary>Silent no-op would hide bugs (DoR §14.1) — an unknown Id throws.</summary>
        [Fact]
        public async Task UpdateLinkAsync_UnknownId_Throws()
        {
            var maTask = await SeedTaskAsync();
            var repo = new SqliteTaskEditorRepository(Factory());

            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateLinkAsync(
                new TaskReferenceLink { Id = Guid.NewGuid(), MaTask = maTask, Title = "ghost" }));
        }

        /// <summary>
        /// A tombstoned row is not an update target — resurrecting one is explicitly out of scope
        /// (DoR §19), so the update must be rejected rather than quietly rewriting dead content.
        /// </summary>
        [Fact]
        public async Task UpdateLinkAsync_TombstonedLink_Throws()
        {
            var maTask = await SeedTaskAsync();
            var repo = new SqliteTaskEditorRepository(Factory());
            var inserted = await SeedLinkAsync(repo, maTask);

            await repo.DeleteLinkAsync(inserted.Id);
            Assert.Empty(await repo.GetLinksAsync(maTask));

            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateLinkAsync(
                new TaskReferenceLink { Id = inserted.Id, MaTask = maTask, Title = "resurrected" }));

            using var read = Factory()();
            var row = await read.TaskReferenceLinks.SingleAsync(l => l.Id == inserted.Id);
            Assert.True(row.IsDeleted);
            Assert.Equal("Docs", row.Title);
        }

        /// <summary>Characterization: the insert path is untouched by PR-A.</summary>
        [Fact]
        public async Task AddLinkAsync_NewLink_StampsRevOneAndKeepsSuppliedCreatedAtUtc()
        {
            var maTask = await SeedTaskAsync();
            var repo = new SqliteTaskEditorRepository(Factory());

            var link = new TaskReferenceLink
            {
                MaTask = maTask,
                Title = "Docs",
                Url = "https://example.test/v1",
                SortOrder = 0,
                CreatedAtUtc = C0,
            };
            await repo.AddLinkAsync(link);

            var after = Assert.Single(await repo.GetLinksAsync(maTask));
            Assert.Equal(1, after.Rev);
            Assert.Equal(C0, after.CreatedAtUtc);
            Assert.Equal(T0, after.ModifiedAtUtc);
            Assert.False(string.IsNullOrEmpty(after.ModifiedByDeviceId));
            Assert.False(after.IsDeleted);
        }

        /// <summary>
        /// Characterization: a brand-new VM (not loaded from the DB) still inserts through
        /// AddLinkAsync with ToModel's CreatedAtUtc = UtcNow — PR-A leaves ToModel alone.
        /// </summary>
        [Fact]
        public async Task AddLinkAsync_FromNewViewModel_InsertsWithToModelCreatedAtUtc()
        {
            var maTask = await SeedTaskAsync();
            var repo = new SqliteTaskEditorRepository(Factory());

            var before = DateTime.UtcNow;
            var vm = new TaskReferenceLinkItemVm { MaTask = maTask, Title = "New", Url = "https://example.test/new" };
            await repo.AddLinkAsync(vm.ToModel());
            var afterUtc = DateTime.UtcNow;

            var row = Assert.Single(await repo.GetLinksAsync(maTask));
            Assert.Equal(vm.Id, row.Id);
            Assert.Equal(1, row.Rev);
            Assert.InRange(row.CreatedAtUtc, before.AddSeconds(-1), afterUtc.AddSeconds(1));
        }
    }
}
