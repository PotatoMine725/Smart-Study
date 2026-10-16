using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Infrastructure.Persistence.Repositories;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Mutations;
using SmartStudyPlanner.Infrastructure.Persistence.SQLite.Repositories;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Fence;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Infrastructure.Persistence.SQLite.Mutations
{
    /// <summary>
    /// Epic 2 / T2.4 fence Slice 5 — the plan §16.1 rows reachable through <c>ITaskEditorRepository</c>
    /// (P-K-1, P-K-4, P-K-7, P-CR-5, P-PT-5), run end to end on real SQLite: staged conflict record, real
    /// editor call. A Blocked call leaves every table byte-identical and carries the rule id; a Passed
    /// call persists, and its decision still carries the relevant-but-Passed entry (T-18: record ≠
    /// subject ≠ contract).
    /// <para>
    /// The E2 rows (owner ruling 2026-10-16) drive <see cref="ITaskEditorRepository.SaveEditorAsync"/>:
    /// one note + links change is one fence decision, so a conflict that reaches ANY part of it rejects
    /// all of it — the "3rd of 5 calls" partial state of Phase 0 §3b cannot occur.
    /// </para>
    /// Passed decisions are read from <see cref="LocalTaskEditorExecutor"/> directly (the repository
    /// discards them); Blocked ones from the exception the public port throws.
    /// </summary>
    public class LocalTaskEditorFenceTests : IDisposable
    {
        private readonly FenceScenarioFixture _fence = new();

        public void Dispose() => _fence.Dispose();

        private SyncApplyFixture Fx => _fence.Fx;

        private ITaskEditorRepository Repo => new SqliteTaskEditorRepository(Fx.Factory);

        private LocalTaskEditorExecutor Executor => new(Fx.Factory);

        private static TaskReferenceLink NewLink(Guid taskId, string title = "new link", int sortOrder = 0) =>
            new() { Id = Guid.NewGuid(), MaTask = taskId, Title = title, Url = "https://example.test/" + title.Replace(' ', '-'), SortOrder = sortOrder };

        private async Task<TaskReferenceLink> AddLiveLinkAsync(Guid taskId, string title = "link", int sortOrder = 0)
        {
            var link = NewLink(taskId, title, sortOrder);
            await Fx.AddLocalAsync(link);
            return link;
        }

        private async Task<List<TaskNote>> NotesOfAsync(Guid taskId)
        {
            using var ctx = Fx.NewContext();
            return await ctx.TaskNotes.AsNoTracking().Where(n => n.MaTask == taskId).ToListAsync();
        }

        private async Task<List<TaskReferenceLink>> LinksOfAsync(Guid taskId)
        {
            using var ctx = Fx.NewContext();
            return await ctx.TaskReferenceLinks.AsNoTracking().Where(l => l.MaTask == taskId).ToListAsync();
        }

        private async Task<MutationRejectedException> AssertBlockedAndUnchangedAsync(Func<Task> call)
        {
            var before = await _fence.SnapshotAllTablesAsync();
            var ex = await Assert.ThrowsAsync<MutationRejectedException>(call);
            Assert.Equal(before, await _fence.SnapshotAllTablesAsync());
            return ex;
        }

        private static void AssertRule(MutationRejectedException ex, string ruleId, RoutingStage stage, Guid conflictId)
        {
            var blocking = LocalSaveDriver.SingleBlocking(ex);
            Assert.Equal(ruleId, blocking.RuleId);
            Assert.Equal(stage, blocking.Stage);
            Assert.Equal(conflictId, blocking.ConflictId);
            Assert.Contains(ruleId, ex.Message, StringComparison.Ordinal);
            Assert.Contains(conflictId.ToString(), ex.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// S1-PT with a LIVE Base parent (same recipe as <c>LocalSemesterSaveFenceTests</c>): the remote
        /// moved T to a MonHoc tombstoned locally; the apply session holds T live at Base.
        /// </summary>
        private async Task<(SyncConflictRecordRow Record, StudyTask Task)> StageS1PtLiveBaseParentAsync()
        {
            var (hocKy, monHoc, task) = await Fx.SeedTreeAsync();
            var deadTarget = new MonHoc("MH dead target", 2) { MaHocKy = hocKy.MaHocKy };
            await Fx.AddLocalAsync(deadTarget);
            await Fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = Fx.NewContext(SyncApplyFixture.LocalNow, "DELETER-DEVICE"))
            {
                ctx.MonHocs.Remove(await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == deadTarget.MaMonHoc));
                await ctx.SaveChangesAsync();
            }

            var remote = new StudyTask
            {
                MaTask = task.MaTask, MaMonHoc = deadTarget.MaMonHoc, TenTask = task.TenTask, HanChot = task.HanChot,
                TrangThai = task.TrangThai, LoaiTask = task.LoaiTask, DoKho = task.DoKho,
                ThoiGianDaHoc = task.ThoiGianDaHoc, NgayHoanThanh = task.NgayHoanThanh,
            };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await Fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = Assert.Single(await Fx.ReadConflictsAsync());
            Assert.Equal(StructuralReason.ParentTombstoned, record.StructuralReason);
            var live = (await Fx.ReadTaskAsync(task.MaTask))!;
            Assert.False(live.IsDeleted);
            Assert.Equal(monHoc.MaMonHoc, live.MaMonHoc);
            return (record, task);
        }

        // ================================================================== P-K (constraint scope K(T))

        [Fact]
        public async Task PK1_UpsertNote_NoLiveNote_UnderS3_IsBlocked_ScopeAcquired_NoNoteInScope()
        {
            var (record, task) = await _fence.StageS3Async();
            Assert.DoesNotContain(await NotesOfAsync(task.MaTask), n => !n.IsDeleted);

            var ex = await AssertBlockedAndUnchangedAsync(() => Repo.UpsertNoteAsync(task.MaTask, "x"));

            AssertRule(ex, "CONS.ScopeAcquired", RoutingStage.ConstraintScope, record.ConflictId);
            Assert.DoesNotContain(await NotesOfAsync(task.MaTask), n => !n.IsDeleted);
        }

        [Fact]
        public async Task PK4_EditOccupantNoteContent_UnderS2_IsBlocked_OccupantContent_ContentUnchanged()
        {
            var (record, task, occupant) = await _fence.SeedS2RecordAsync();

            var ex = await AssertBlockedAndUnchangedAsync(() => Repo.UpsertNoteAsync(task.MaTask, "edited while held"));

            AssertRule(ex, "CONS.OccupantContent", RoutingStage.ConstraintScope, record.ConflictId);
            var note = Assert.Single(await NotesOfAsync(task.MaTask));
            Assert.Equal(occupant.Id, note.Id);
            Assert.Equal("base content", note.Content);
        }

        [Fact]
        public async Task PK7_DeleteLinkOfT2_WithS2OnT_AndUnrelatedS3OnT2_IsFencePassed_AndPersisted()
        {
            var (s2, t, _) = await _fence.SeedS2RecordAsync();
            var (s3, t2) = await _fence.StageS3Async();
            Assert.NotEqual(t.MaTask, t2.MaTask);
            var link = await AddLiveLinkAsync(t2.MaTask);

            var decision = await Executor.DeleteLinkAsync(link.Id);

            Assert.True(decision.RouteKnown);
            Assert.True(decision.FencePassed);
            Assert.DoesNotContain(decision.Results, r => r.ConflictId == s2.ConflictId && r.Outcome != FenceOutcome.NotApplicable);
            Assert.True(Assert.Single(await LinksOfAsync(t2.MaTask)).IsDeleted);
            _ = s3;
        }

        // ================================================================== P-CR-5 / P-PT-5 (child edge only)

        [Fact]
        public async Task PCR5_AddLinkUnderHeldTask_IsPassed_ChildEdgeOnly_RecordSubjectContractDistinct()
        {
            var (record, _, _, _, _, task) = await _fence.StageS1CrAsync();
            var link = NewLink(task.MaTask);

            var decision = await Executor.AddLinkAsync(link);

            Assert.True(decision.FencePassed);
            var passed = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Passed, passed.Outcome);                   // relevant, not NotApplicable
            Assert.Equal("S1CR.ChildEdgeOnly", passed.RuleId);
            Assert.Equal(RoutingStage.CascadeReached, passed.Stage);
            Assert.Equal(ConflictShape.ConcurrentReparent, passed.Shape);
            Assert.Equal(record.ScopeKey, passed.ScopeKey);                       // the record ...
            var subject = Assert.IsType<EntitySubject>(passed.Subject);           // ... protects T ...
            Assert.Equal(task.MaTask, subject.EntityId);
            Assert.NotEqual(link.Id, subject.EntityId);                           // ... not the mutated link
            Assert.NotEqual(record.ConflictId, subject.EntityId);
            Assert.False(Assert.Single(await LinksOfAsync(task.MaTask)).IsDeleted);
        }

        [Fact]
        public async Task PPT5_AddLinkUnderPtHeldTask_IsPassed_S1PT_ChildEdgeOnly()
        {
            var (record, task) = await StageS1PtLiveBaseParentAsync();
            var link = NewLink(task.MaTask);

            var decision = await Executor.AddLinkAsync(link);

            Assert.True(decision.FencePassed);
            var passed = Assert.Single(decision.Results, r => r.ConflictId == record.ConflictId);
            Assert.Equal(FenceOutcome.Passed, passed.Outcome);
            Assert.Equal("S1PT.ChildEdgeOnly", passed.RuleId);
            Assert.Equal(ConflictShape.ParentTombstoned, passed.Shape);
            Assert.Equal(task.MaTask, Assert.IsType<EntitySubject>(passed.Subject).EntityId);
            Assert.False(Assert.Single(await LinksOfAsync(task.MaTask)).IsDeleted);
        }

        // ================================================================== the four port methods, one each

        [Fact]
        public async Task AddLink_UnderUnsupportedRecordOnTask_IsRejected_NothingWritten()
        {
            var (record, task) = await _fence.SeedUnsupportedRecordAsync();

            var ex = await AssertBlockedAndUnchangedAsync(() => Repo.AddLinkAsync(NewLink(task.MaTask)));

            var blocking = LocalSaveDriver.SingleBlocking(ex);
            Assert.Equal(FenceOutcome.Unsupported, blocking.Outcome);
            Assert.Equal(record.ConflictId, blocking.ConflictId);
            Assert.Empty(await LinksOfAsync(task.MaTask));
        }

        [Fact]
        public async Task DeleteLink_UnderUnsupportedRecordOnTask_IsRejected_LinkStaysLive()
        {
            var (record, task) = await _fence.SeedUnsupportedRecordAsync();
            var link = await AddLiveLinkAsync(task.MaTask);

            var ex = await AssertBlockedAndUnchangedAsync(() => Repo.DeleteLinkAsync(link.Id));

            Assert.Equal(record.ConflictId, LocalSaveDriver.SingleBlocking(ex).ConflictId);
            Assert.False(Assert.Single(await LinksOfAsync(task.MaTask)).IsDeleted);
        }

        [Fact]
        public async Task UpdateLink_UnderRecordOnTheLinkItself_IsRejected_FieldsUnchanged()
        {
            // A record whose subject IS the link (Unsupported shape, constructed like the fixture's).
            var (_, _, task) = await Fx.SeedTreeAsync();
            var link = await AddLiveLinkAsync(task.MaTask, "original");
            var record = await SeedUnsupportedRecordOnLinkAsync(link.Id);
            var edit = new TaskReferenceLink { Id = link.Id, MaTask = task.MaTask, Title = "edited", Url = link.Url, SortOrder = link.SortOrder };

            var ex = await AssertBlockedAndUnchangedAsync(() => Repo.UpdateLinkAsync(edit));

            Assert.Equal(record.ConflictId, LocalSaveDriver.SingleBlocking(ex).ConflictId);
            Assert.Equal("original", Assert.Single(await LinksOfAsync(task.MaTask)).Title);
        }

        private async Task<SyncConflictRecordRow> SeedUnsupportedRecordOnLinkAsync(Guid linkId)
        {
            var row = new SyncConflictRecordRow
            {
                ConflictId = Guid.NewGuid(),
                ConflictKey = "unsupported-link-seed-" + Guid.NewGuid().ToString("N"),
                ScopeKey = ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, SyncEntityTypes.TaskReferenceLink, linkId, "MaTask", null),
                Kind = ConflictKind.StructuralConflict,
                EntityType = SyncEntityTypes.TaskReferenceLink,
                EntityId = linkId,
                FieldName = "MaTask",
                StructuralReason = (StructuralReason)99,
                PeerDeviceId = SyncApplyFixture.PeerDevice,
                BaseEntityId = linkId,
                BaseSnapshotJson = "{}",
                BaseFingerprint = "seed-base-fp",
                LocalEntityId = linkId,
                LocalSnapshotJson = "{}",
                LocalFingerprint = "seed-local-fp",
                LocalRowRev = 1,
                RemoteEntityId = Guid.NewGuid(),
                RemoteSnapshotJson = "{}",
                RemoteFingerprint = "seed-remote-fp",
                Status = ConflictRecordStatus.Unresolved,
                CreatedAtUtc = SyncApplyFixture.LocalNow,
                CreatedByDeviceId = SyncApplyFixture.LocalDevice,
            };
            using var ctx = Fx.NewContext();
            ctx.SyncConflictRecords.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        // ================================================================== E2: SaveEditorAsync is one decision

        /// <summary>
        /// Phase 0 §3b's "3rd of 5" case: an Unsupported record on T reaches the link add and the link
        /// delete but not the note edit or the link update. Before E2 the note edit and the update
        /// committed and the rest did not; now the one request is rejected whole.
        /// </summary>
        [Fact]
        public async Task SaveEditor_RecordReachingOnlyPartOfTheChange_RejectsAllOfIt_TablesIdentical()
        {
            var (record, task) = await _fence.SeedUnsupportedRecordAsync();
            await Fx.AddLocalAsync(new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "old note" });
            var kept = await AddLiveLinkAsync(task.MaTask, "kept", 0);
            var removed = await AddLiveLinkAsync(task.MaTask, "removed", 1);
            var incoming = new[]
            {
                new TaskReferenceLink { Id = kept.Id, MaTask = task.MaTask, Title = "kept, edited", Url = kept.Url, SortOrder = 0 },
                NewLink(task.MaTask, "added", 1),
            };

            var ex = await AssertBlockedAndUnchangedAsync(() => Repo.SaveEditorAsync(task.MaTask, "new note", incoming));

            Assert.Equal(record.ConflictId, LocalSaveDriver.SingleBlocking(ex).ConflictId);
            Assert.Equal("old note", Assert.Single(await NotesOfAsync(task.MaTask)).Content);
            var links = await LinksOfAsync(task.MaTask);
            Assert.Equal(2, links.Count);
            Assert.Equal("kept", links.Single(l => l.Id == kept.Id).Title);
            Assert.False(links.Single(l => l.Id == removed.Id).IsDeleted);
        }

        /// <summary>
        /// Owner ruling 2026-10-16 (Q2): with no live note, a links-only edit (null content) creates no
        /// note, so it does not acquire K(T) and S3 does not block it -- the user changed no note. The
        /// note rows are compared before/after, since the S3 staging may leave a non-live row in scope.
        /// </summary>
        [Fact]
        public async Task SaveEditor_LinksOnlyEdit_NoLiveNote_UnderS3_IsPassed_NoNoteCreated()
        {
            var (_, task) = await _fence.StageS3Async();
            var notesBefore = (await NotesOfAsync(task.MaTask)).Select(n => (n.Id, n.IsDeleted, n.Content, n.Rev)).ToList();
            var link = NewLink(task.MaTask);

            var decision = await Executor.SaveEditorAsync(task.MaTask, null, new[] { link });

            Assert.True(decision.RouteKnown && decision.FencePassed);
            Assert.Equal(notesBefore, (await NotesOfAsync(task.MaTask)).Select(n => (n.Id, n.IsDeleted, n.Content, n.Rev)).ToList());
            Assert.False(Assert.Single(await LinksOfAsync(task.MaTask)).IsDeleted);
        }

        [Fact]
        public async Task SaveEditor_UnderS1CrOnTask_LinkAddAndDelete_ArePassed_AndPersistTogether()
        {
            var (record, _, _, _, _, task) = await _fence.StageS1CrAsync();
            await Fx.AddLocalAsync(new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "note" });
            var removed = await AddLiveLinkAsync(task.MaTask, "removed");
            var added = NewLink(task.MaTask, "added");

            var decision = await Executor.SaveEditorAsync(task.MaTask, "note", new[] { added });

            Assert.True(decision.FencePassed);
            Assert.Contains(decision.Results, r => r.ConflictId == record.ConflictId && r.RuleId == "S1CR.ChildEdgeOnly");
            var links = await LinksOfAsync(task.MaTask);
            Assert.True(links.Single(l => l.Id == removed.Id).IsDeleted);
            Assert.False(links.Single(l => l.Id == added.Id).IsDeleted);
        }
    }
}
