using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync;
using SmartStudyPlanner.Sync.Apply;
using SmartStudyPlanner.Sync.Merge;
using SmartStudyPlanner.Tests.Fixtures;
using Xunit;

namespace SmartStudyPlanner.Tests.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — tests J, K, P, Q, R, S, T, U from the PR-5 brief: the D9-T1 staging
    /// mechanism (including the audited M5 hard delete), auto-resolved evidence, the D9-T6 scope lock,
    /// replay behaviour and the Base-drift primitive.
    /// </summary>
    public class SyncApplyConflictStagingTests : IDisposable
    {
        private readonly SyncApplyFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private static StudyTask CloneTask(StudyTask src, Action<StudyTask>? edit = null)
        {
            var copy = new StudyTask
            {
                MaTask = src.MaTask,
                MaMonHoc = src.MaMonHoc,
                TenTask = src.TenTask,
                HanChot = src.HanChot,
                TrangThai = src.TrangThai,
                LoaiTask = src.LoaiTask,
                DoKho = src.DoKho,
                ThoiGianDaHoc = src.ThoiGianDaHoc,
                NgayHoanThanh = src.NgayHoanThanh,
            };
            edit?.Invoke(copy);
            return copy;
        }

        // ------------------------------------------------------------------ J. null-Base TaskNote (M5)

        /// <summary>
        /// J — the concrete D9-T1 S3 scenario: Base = null, Local = N1, Remote = N2, same MaTask,
        /// different Ids. The unresolved conflict means the scope must have NO live row, so the withdrawn
        /// local candidate is physically removed through the single audited M5 exception — evidence
        /// first, same transaction. Neither candidate is live afterwards, and the uniqueness scope is
        /// free, which is what keeps all four PR-6 resolutions materialisable.
        /// </summary>
        [Fact]
        public async Task J_NullBaseTaskNoteCollision_StagesEvidence_AndHardDeletesTheWithdrawnCandidate()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            var n1 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "ghi chú cục bộ" };
            await _fx.AddLocalAsync(n1);

            var n2 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "ghi chú từ peer" };
            SyncApplyFixture.Stamp(n2, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(n2));
            var result = Assert.Single(report.Results);
            Assert.Equal(SyncApplyOutcome.ConflictStaged, result.Outcome);

            // --- evidence
            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictKind.ConstraintConflict, record.Kind);
            Assert.Equal(ConflictRecordStatus.Unresolved, record.Status);
            Assert.Equal(SyncEntityTypes.TaskNote, record.EntityType);
            Assert.Null(record.EntityId);                                  // scope-addressed, not entity-addressed
            Assert.Equal("MaTask", record.ConstraintKey);
            Assert.Equal(task.MaTask.ToString("D"), record.ConstraintValue);
            Assert.Null(record.BaseSnapshotJson);
            Assert.Null(record.BaseFingerprint);                           // "no live row in scope"
            Assert.Equal(n1.Id, record.LocalEntityId);
            Assert.Equal(n2.Id, record.RemoteEntityId);
            Assert.Contains("ghi chú cục bộ", record.LocalSnapshotJson);    // the withdrawn row is recoverable
            Assert.Contains("ghi chú từ peer", record.RemoteSnapshotJson);
            Assert.Equal(ConflictLocalWithdrawal.HardDeleted, record.LocalWithdrawal);
            Assert.Equal(1, record.LocalRowRev);                           // Rev continuity for PR-6 KeepLocal

            // --- live state: the scope has no row at all
            Assert.Equal(0, await _fx.CountNotesInScopeAsync(task.MaTask));
            Assert.Null(await _fx.ReadNoteAsync(n1.Id));
            Assert.Null(await _fx.ReadNoteAsync(n2.Id));

            // --- and the UNIQUE index really is free again (this is what M1/M2 could not achieve)
            var later = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "sau khi resolve" };
            await _fx.AddLocalAsync(later);
            Assert.NotNull(await _fx.ReadNoteAsync(later.Id));
        }

        /// <summary>
        /// J (atomicity) — the staging write and the live-state withdrawal are one transaction (D8-C).
        /// Proved by poisoning the save: an Unresolved record already occupying this ScopeKey with a
        /// different ConflictKey makes the operation reject, and N1 must still be present afterwards.
        /// </summary>
        [Fact]
        public async Task J_WhenStagingIsRejected_TheLocalCandidateIsNotDeleted()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var n1 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "still here" };
            await _fx.AddLocalAsync(n1);

            var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", task.MaTask.ToString("D"));
            var scopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null, scope);

            using (var ctx = _fx.NewContext())
            {
                ctx.SyncConflictRecords.Add(new SyncConflictRecordRow
                {
                    ConflictId = Guid.NewGuid(),
                    ConflictKey = "a-different-key",
                    ScopeKey = scopeKey,
                    Kind = ConflictKind.ConstraintConflict,
                    EntityType = SyncEntityTypes.TaskNote,
                    ConstraintKey = "MaTask",
                    ConstraintValue = task.MaTask.ToString("D"),
                    PeerDeviceId = SyncApplyFixture.PeerDevice,
                    LocalEntityId = Guid.NewGuid(),
                    LocalSnapshotJson = "{}",
                    LocalFingerprint = "x",
                    RemoteEntityId = Guid.NewGuid(),
                    RemoteSnapshotJson = "{}",
                    RemoteFingerprint = "y",
                    Status = ConflictRecordStatus.Unresolved,
                    CreatedAtUtc = SyncApplyFixture.LocalNow,
                    CreatedByDeviceId = SyncApplyFixture.LocalDevice,
                });
                await ctx.SaveChangesAsync();
            }

            var n2 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "from peer" };
            SyncApplyFixture.Stamp(n2, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(n2));
            var result = Assert.Single(report.Results);

            Assert.Equal(SyncApplyOutcome.Rejected, result.Outcome);
            Assert.Equal(SyncApplyReason.ScopeHasUnresolvedConflict, result.Reason);
            Assert.NotNull(await _fx.ReadNoteAsync(n1.Id));                 // never touched
            Assert.Single(await _fx.ReadConflictsAsync());                   // no second record
        }

        // ------------------------------------------------------------------ K. constraint staging, Base != null

        /// <summary>
        /// K — a constraint conflict WITH a Base: the local candidate is rewritten to the Base row rather
        /// than hard-deleted, so the audited exception stays scoped to the one case that needs it.
        /// </summary>
        [Fact]
        public async Task K_ConstraintConflictWithBase_RewritesTheLocalCandidateToBase()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();

            var n1 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "base content" };
            await _fx.AddLocalAsync(n1);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, n1);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.TaskNotes.FirstAsync(n => n.Id == n1.Id);
                live.Content = "locally edited";
                await ctx.SaveChangesAsync();
            }

            var n2 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "competing remote note" };
            SyncApplyFixture.Stamp(n2, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(n2));
            Assert.Equal(SyncApplyOutcome.ConflictStaged, Assert.Single(report.Results).Outcome);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictLocalWithdrawal.RewrittenToBase, record.LocalWithdrawal);
            Assert.NotNull(record.BaseFingerprint);

            var inScope = await _fx.ReadNoteInScopeAsync(task.MaTask);
            Assert.NotNull(inScope);
            Assert.Equal(n1.Id, inScope!.Id);                     // the Base row, same Id
            Assert.Equal("base content", inScope.Content);        // held at Base
            Assert.Null(await _fx.ReadNoteAsync(n2.Id));         // remote candidate never materialised
        }

        // ------------------------------------------------------------------ P. record shape

        /// <summary>
        /// P — the persisted record carries the staging metadata PR-6 and any UI need, and identity is
        /// split the way D7-A requires: <c>ConflictId</c> addresses the record, <c>ConflictKey</c> is the
        /// logical identity a retry collapses on, and the two are never interchanged.
        /// </summary>
        [Fact]
        public async Task P_StagedRecord_CarriesScopeIdentityAndStagingProvenance()
        {
            var (hocKy, _, task) = await _fx.SeedTreeAsync();
            var monHocB = new MonHoc("MH B", 2) { MaHocKy = hocKy.MaHocKy };
            var monHocC = new MonHoc("MH C", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(monHocB);
            await _fx.AddLocalAsync(monHocC);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = monHocB.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task, t => t.MaMonHoc = monHocC.MaMonHoc);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var result = Assert.Single(report.Results);
            var record = Assert.Single(await _fx.ReadConflictsAsync());

            Assert.Equal(record.ConflictId, result.ConflictId);
            Assert.NotEqual(Guid.Empty, record.ConflictId);
            Assert.NotEmpty(record.ConflictKey);
            Assert.NotEqual(record.ConflictKey, record.ConflictId.ToString());
            Assert.Equal($"{SyncEntityTypes.StudyTask}|{task.MaTask:D}|MaMonHoc", record.ScopeKey);
            Assert.Equal(SyncApplyFixture.PeerDevice, record.PeerDeviceId);
            Assert.Equal(CanonicalJson.Version, record.SnapshotVersion);
            Assert.Equal(SyncApplyFixture.LocalNow, DateTime.SpecifyKind(record.CreatedAtUtc, DateTimeKind.Utc));
            Assert.Equal(SyncApplyFixture.LocalDevice, record.CreatedByDeviceId);
            Assert.Null(record.ResolutionKind);
            Assert.Null(record.ResolvedAtUtc);
        }

        // ------------------------------------------------------------------ Q. AutoLww evidence

        /// <summary>
        /// Q — D9-T5: a same-field concurrent change is auto-resolved by LWW, and the decision is
        /// recorded as <c>FieldConflict</c> evidence written DIRECTLY as Resolved. The evidence never
        /// changes the outcome; it explains it.
        /// </summary>
        [Fact]
        public async Task Q_SameFieldConcurrentChange_AppliesLwwWinner_AndRecordsAutoLwwAsResolved()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            // Local edit carries an EARLIER timestamp than the remote, so the remote wins and the row
            // visibly changes — a local winner would be a no-op and prove less.
            using (var ctx = _fx.NewContext(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.TenTask = "local name";
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task, t => t.TenTask = "remote name");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            Assert.Equal(SyncApplyOutcome.Applied, Assert.Single(report.Results).Outcome);

            Assert.Equal("remote name", (await _fx.ReadTaskAsync(task.MaTask))!.TenTask);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictKind.FieldConflict, record.Kind);
            Assert.Equal("TenTask", record.FieldName);
            Assert.Equal(ConflictRecordStatus.Resolved, record.Status);          // never re-resolvable
            Assert.Equal(ResolutionKind.AutoLww, record.ResolutionKind);
            Assert.NotNull(record.ResultSnapshotJson);
            Assert.Contains("remote name", record.ResultSnapshotJson!);
            Assert.Equal(SyncApplyFixture.LocalNow, DateTime.SpecifyKind(record.ResolvedAtUtc!.Value, DateTimeKind.Utc));
        }

        /// <summary>
        /// Q (local winner) — when the local side wins the LWW the merged result already equals the live
        /// row, so nothing is written (Rev untouched) but the evidence is still recorded.
        /// </summary>
        [Fact]
        public async Task Q_WhenLocalWinsLww_NothingIsWritten_ButEvidenceIsStillRecorded()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.TenTask = "local wins";
                await ctx.SaveChangesAsync();
            }
            var before = await _fx.ReadTaskAsync(task.MaTask);

            var remote = CloneTask(task, t => t.TenTask = "remote loses");
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            Assert.Equal(SyncApplyOutcome.NoOp, Assert.Single(report.Results).Outcome);

            var after = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal("local wins", after!.TenTask);
            Assert.Equal(before!.Rev, after.Rev);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ResolutionKind.AutoLww, record.ResolutionKind);
            Assert.Contains("local wins", record.ResultSnapshotJson!);
        }

        // ------------------------------------------------------------------ R. AutoTombstone evidence

        /// <summary>
        /// R — delete-vs-edit: the tombstone wins and the losing local edit is preserved as
        /// <c>TombstoneConflict</c> evidence with <c>AutoTombstone</c>, written directly as Resolved.
        /// </summary>
        [Fact]
        public async Task R_DeleteVersusEdit_RecordsAutoTombstoneAsResolved()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.TenTask = "edited, about to lose";
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice, isDeleted: true);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            Assert.True((await _fx.ReadTaskAsync(task.MaTask))!.IsDeleted);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictKind.TombstoneConflict, record.Kind);
            Assert.Equal(ConflictRecordStatus.Resolved, record.Status);
            Assert.Equal(ResolutionKind.AutoTombstone, record.ResolutionKind);
            Assert.Contains("edited, about to lose", record.LocalSnapshotJson);   // the losing edit is recoverable
            Assert.Equal($"{SyncEntityTypes.StudyTask}|{task.MaTask:D}|*", record.ScopeKey);
        }

        /// <summary>
        /// R (no competing claim) — a tombstone over a local row that never moved from Base is not a
        /// conflict, it just loses the row. Emitting evidence here would fill the table with noise.
        /// </summary>
        [Fact]
        public async Task R_TombstoneOverAnUntouchedRow_RecordsNoEvidence()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            var remote = CloneTask(task);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice, isDeleted: true);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            Assert.True((await _fx.ReadTaskAsync(task.MaTask))!.IsDeleted);
            Assert.Empty(await _fx.ReadConflictsAsync());
        }

        // ------------------------------------------------------------------ S/T. scope lock and replay

        /// <summary>
        /// S — D9-T6: once a scope holds an Unresolved record, a DIFFERENT candidate in that scope
        /// rejects the whole operation instead of creating a second unresolved record. Any other
        /// outcome would make live != Base while unresolved, and v1 has no rebase (D8-H).
        /// </summary>
        [Fact]
        public async Task S_DifferentCandidateInALockedScope_RejectsTheOperation()
        {
            var (hocKy, monHocA, task) = await _fx.SeedTreeAsync();
            var b = new MonHoc("B", 2) { MaHocKy = hocKy.MaHocKy };
            var c = new MonHoc("C", 2) { MaHocKy = hocKy.MaHocKy };
            var d = new MonHoc("D", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(b);
            await _fx.AddLocalAsync(c);
            await _fx.AddLocalAsync(d);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = b.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var first = CloneTask(task, t => t.MaMonHoc = c.MaMonHoc);
            SyncApplyFixture.Stamp(first, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            Assert.Equal(SyncApplyOutcome.ConflictStaged,
                         Assert.Single((await _fx.Session().ApplyAsync(SyncApplyFixture.From(first))).Results).Outcome);

            var stateAfterStaging = await _fx.ReadTaskAsync(task.MaTask);

            // A different reparent target in the same scope.
            var second = CloneTask(task, t => t.MaMonHoc = d.MaMonHoc);
            SyncApplyFixture.Stamp(second, new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc), SyncApplyFixture.PeerDevice);

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(second));
            var result = Assert.Single(report.Results);

            Assert.Equal(SyncApplyOutcome.Rejected, result.Outcome);
            Assert.Equal(SyncApplyReason.ScopeHasUnresolvedConflict, result.Reason);
            Assert.Single(await _fx.ReadConflictsAsync());               // still exactly one record

            var now = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(monHocA.MaMonHoc, now!.MaMonHoc);               // still held at Base
            Assert.Equal(stateAfterStaging!.Rev, now.Rev);               // and nothing was re-written
        }

        /// <summary>
        /// T — replaying the SAME staging operation is safe: no second record, no change to live state,
        /// no Rev movement.
        /// <para>
        /// The outcome is the scope-lock rejection rather than an "AlreadyStaged" no-op, and that is a
        /// consequence of the withdrawal, not a gap: holding the row at Base made Local equal Base, so
        /// the replay's merge no longer produces a conflict candidate at all and there is no
        /// <c>ConflictKey</c> left to match the stored one against. Per D9-T6 and DoR §12.4 a scope
        /// holding an Unresolved record rejects every write until it is resolved, which is exactly what
        /// happens here — so "idempotent" means "provably no second effect", which is what is asserted.
        /// </para>
        /// </summary>
        [Fact]
        public async Task T_ReplayingTheSameStagingOperation_HasNoSecondEffect()
        {
            var (hocKy, monHocA, task) = await _fx.SeedTreeAsync();
            var b = new MonHoc("B", 2) { MaHocKy = hocKy.MaHocKy };
            var c = new MonHoc("C", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(b);
            await _fx.AddLocalAsync(c);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = b.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task, t => t.MaMonHoc = c.MaMonHoc);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);

            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));
            var afterFirst = await _fx.ReadTaskAsync(task.MaTask);
            var recordAfterFirst = Assert.Single(await _fx.ReadConflictsAsync());

            var report = await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            Assert.Equal(SyncApplyOutcome.Rejected, Assert.Single(report.Results).Outcome);
            var recordAfterSecond = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(recordAfterFirst.ConflictId, recordAfterSecond.ConflictId);
            Assert.Equal(recordAfterFirst.ConflictKey, recordAfterSecond.ConflictKey);

            var afterSecond = await _fx.ReadTaskAsync(task.MaTask);
            Assert.Equal(monHocA.MaMonHoc, afterSecond!.MaMonHoc);
            Assert.Equal(afterFirst!.Rev, afterSecond.Rev);
            Assert.Equal(afterFirst.ModifiedAtUtc, afterSecond.ModifiedAtUtc);
        }

        /// <summary>
        /// S (unconditional enforcement) — the application pre-check exists to make rejection a typed
        /// outcome, not to be the only guard. The partial unique index refuses a second Unresolved row
        /// in one scope even when the check is bypassed entirely.
        /// </summary>
        [Fact]
        public async Task S_DatabaseRefusesASecondUnresolvedRecordInTheSameScope()
        {
            using var ctx = _fx.NewContext();
            ctx.SyncConflictRecords.Add(Row("key-1", "one-scope", ConflictRecordStatus.Unresolved));
            await ctx.SaveChangesAsync();

            ctx.SyncConflictRecords.Add(Row("key-2", "one-scope", ConflictRecordStatus.Unresolved));
            await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        }

        private static SyncConflictRecordRow Row(string key, string scope, ConflictRecordStatus status) => new()
        {
            ConflictId = Guid.NewGuid(),
            ConflictKey = key,
            ScopeKey = scope,
            Kind = ConflictKind.StructuralConflict,
            EntityType = SyncEntityTypes.StudyTask,
            EntityId = Guid.NewGuid(),
            FieldName = "MaMonHoc",
            PeerDeviceId = SyncApplyFixture.PeerDevice,
            LocalEntityId = Guid.NewGuid(),
            LocalSnapshotJson = "{}",
            LocalFingerprint = "l",
            RemoteEntityId = Guid.NewGuid(),
            RemoteSnapshotJson = "{}",
            RemoteFingerprint = "r",
            Status = status,
            CreatedAtUtc = SyncApplyFixture.LocalNow,
            CreatedByDeviceId = SyncApplyFixture.LocalDevice,
        };

        // ------------------------------------------------------------------ U. Base drift primitive

        /// <summary>
        /// U — the shared Base-drift primitive (D8-H) over a really-persisted record: right after
        /// staging the live row still matches the stored <c>BaseFingerprint</c>; once anything moves the
        /// live row, it does not. PR-6's resolver turns a false here into
        /// <c>Rejected(LiveStateDrift)</c> — no force, no automatic rebase.
        /// <para>
        /// There is deliberately no apply-time drift gate: on the apply path Base and Local are supposed
        /// to differ, which is what a three-way merge is for.
        /// </para>
        /// </summary>
        [Fact]
        public async Task U_StoredBaseFingerprint_DetectsLiveStateDrift()
        {
            var (hocKy, _, task) = await _fx.SeedTreeAsync();
            var b = new MonHoc("B", 2) { MaHocKy = hocKy.MaHocKy };
            var c = new MonHoc("C", 2) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(b);
            await _fx.AddLocalAsync(c);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = b.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var remote = CloneTask(task, t => t.MaMonHoc = c.MaMonHoc);
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.NotNull(record.BaseFingerprint);

            var atStaging = IncomingChanges.Of((await _fx.ReadTaskAsync(task.MaTask))!).Snapshot;
            Assert.True(SyncBaseFingerprint.Matches(record.BaseFingerprint, atStaging));

            // Someone edits the row while the conflict is still unresolved.
            using (var ctx = _fx.NewContext(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.DoKho = 9;
                await ctx.SaveChangesAsync();
            }

            var drifted = IncomingChanges.Of((await _fx.ReadTaskAsync(task.MaTask))!).Snapshot;
            Assert.False(SyncBaseFingerprint.Matches(record.BaseFingerprint, drifted));

            // And "no live row in scope" is representable without ambiguity.
            Assert.True(SyncBaseFingerprint.Matches(null, null));
            Assert.False(SyncBaseFingerprint.Matches(null, drifted));
        }
    }
}
