using System;
using System.Collections.Generic;
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
    /// Epic 2 / T2.4 (PR-6, owner rulings B-2, B-3, B-4, and the E-3 acknowledgement,
    /// <c>docs/specs/T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md</c>) — the result-parent
    /// liveness check (residual 12, generalised), ManualMerge tombstone rejection, KeepLocal on the
    /// D4/D9-T4 no-local-candidate shape, the entity-scoped Structural identity rule, and the
    /// Constraint-scope occupancy guard (E-8).
    /// </summary>
    public class ConflictResolverParentAndManualMergeTests : IDisposable
    {
        private const string ResolverDevice = "RESOLVER-DEVICE";
        private static readonly DateTime ResolverNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
        private const string ParentDeleterDevice = "DELETER-DEVICE";
        private static readonly DateTime ParentDeletedAt = new(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc);
        private const string ManualMergeDevice = "MANUAL-MERGE-INPUT";           // never the winning stamp (A2-c overwrites it)
        private static readonly DateTime ManualMergeNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly SyncApplyFixture _fx = new();

        public void Dispose() => _fx.Dispose();

        private ConflictResolver Resolver() => new(() => _fx.NewContext(ResolverNow, ResolverDevice));

        private static ResolutionRequest KeepLocal() => new(ResolutionKind.KeepLocal, null, null);
        private static ResolutionRequest KeepRemote() => new(ResolutionKind.KeepRemote, null, null);
        private static ResolutionRequest KeepBase() => new(ResolutionKind.KeepBase, null, null);
        private static ResolutionRequest ManualMerge(EntitySnapshot result, Guid? id) => new(ResolutionKind.ManualMerge, result, id);

        // ------------------------------------------------------------------ manual snapshot builders

        private static EntitySnapshot ManualMonHoc(Guid maHocKy, string ten, bool isDeleted = false) =>
            new(SyncEntityTypes.MonHoc,
                new Provenance(ManualMergeNow, ManualMergeDevice, isDeleted, isDeleted ? ManualMergeNow : null),
                new Dictionary<string, FieldValue>(StringComparer.Ordinal)
                {
                    ["MaHocKy"] = new GuidValue(maHocKy),
                    ["TenMonHoc"] = new StringValue(ten),
                    ["SoTinChi"] = new Int32Value(3),
                });

        private static EntitySnapshot ManualStudyTask(Guid maMonHoc, string tenTask, bool isDeleted = false) =>
            new(SyncEntityTypes.StudyTask,
                new Provenance(ManualMergeNow, ManualMergeDevice, isDeleted, isDeleted ? ManualMergeNow : null),
                new Dictionary<string, FieldValue>(StringComparer.Ordinal)
                {
                    ["MaMonHoc"] = new GuidValue(maMonHoc),
                    ["TenTask"] = new StringValue(tenTask),
                    ["HanChot"] = new WallClockValue(new DateTime(2026, 2, 2)),
                    ["TrangThai"] = new StringValue("Chưa làm"),
                    ["LoaiTask"] = new Int32Value(0),
                    ["DoKho"] = new Int32Value(2),
                    ["ThoiGianDaHoc"] = new Int32Value(0),
                    ["NgayHoanThanh"] = new WallClockValue(null),
                });

        private static EntitySnapshot ManualTaskNote(Guid maTask, string? content, bool isDeleted = false) =>
            new(SyncEntityTypes.TaskNote,
                new Provenance(ManualMergeNow, ManualMergeDevice, isDeleted, isDeleted ? ManualMergeNow : null),
                new Dictionary<string, FieldValue>(StringComparer.Ordinal)
                {
                    ["MaTask"] = new GuidValue(maTask),
                    ["Content"] = new StringValue(content),
                });

        // ------------------------------------------------------------------ shape construction

        private async Task<(Guid ConflictId, Guid TaskId, Guid MonHocAId, Guid MonHocBId, Guid MonHocCId)> StageConcurrentReparentAsync()
        {
            var (hocKy, monHocA, task) = await _fx.SeedTreeAsync();
            var monHocB = new MonHoc("MH B", 2) { MaHocKy = hocKy.MaHocKy };
            var monHocC = new MonHoc("MH C", 4) { MaHocKy = hocKy.MaHocKy };
            await _fx.AddLocalAsync(monHocB);
            await _fx.AddLocalAsync(monHocC);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == task.MaTask);
                live.MaMonHoc = monHocB.MaMonHoc;
                await ctx.SaveChangesAsync();
            }

            var remote = new StudyTask("Task", task.HanChot, task.LoaiTask, task.DoKho) { MaTask = task.MaTask, MaMonHoc = monHocC.MaMonHoc };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            // Filtered by EntityId, not Assert.Single(...): the fixture's connection is shared across
            // every helper call in a test, so a test combining more than one shape has more than one
            // row in the whole table by the time this runs.
            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task.MaTask);
            return (record.ConflictId, task.MaTask, monHocA.MaMonHoc, monHocB.MaMonHoc, monHocC.MaMonHoc);
        }

        private async Task<(Guid ConflictId, Guid MonHocId, Guid HocKyBId, Guid HocKyCId)> StageMonHocReparentAsync()
        {
            var (_, monHoc, _) = await _fx.SeedTreeAsync();
            var hocKyB = new HocKy("HK B", new DateTime(2026, 2, 1));
            var hocKyC = new HocKy("HK C", new DateTime(2026, 3, 1));
            await _fx.AddLocalAsync(hocKyB);
            await _fx.AddLocalAsync(hocKyC);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, monHoc);

            using (var ctx = _fx.NewContext(SyncApplyFixture.LocalNow))
            {
                var live = await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == monHoc.MaMonHoc);
                live.MaHocKy = hocKyB.MaHocKy;
                await ctx.SaveChangesAsync();
            }

            var remote = new MonHoc { MaMonHoc = monHoc.MaMonHoc, MaHocKy = hocKyC.MaHocKy, TenMonHoc = monHoc.TenMonHoc, SoTinChi = monHoc.SoTinChi };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.EntityType == SyncEntityTypes.MonHoc && r.EntityId == monHoc.MaMonHoc);
            return (record.ConflictId, monHoc.MaMonHoc, hocKyB.MaHocKy, hocKyC.MaHocKy);
        }

        private async Task<(Guid ConflictId, Guid NewMonHocId, Guid TombstonedHocKyId)> StageAbsentLocalStructuralAsync()
        {
            var (hocKy, _, _) = await _fx.SeedTreeAsync();
            using (var ctx = _fx.NewContext(ParentDeletedAt, ParentDeleterDevice))
            {
                var live = await ctx.HocKys.FirstAsync(h => h.MaHocKy == hocKy.MaHocKy);
                ctx.HocKys.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var newMonHocId = Guid.NewGuid();
            var remote = new MonHoc { MaMonHoc = newMonHocId, MaHocKy = hocKy.MaHocKy, TenMonHoc = "orphan?", SoTinChi = 3 };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.EntityType == SyncEntityTypes.MonHoc && r.EntityId == newMonHocId);
            return (record.ConflictId, newMonHocId, hocKy.MaHocKy);
        }

        private async Task<(Guid ConflictId, Guid TaskId, Guid N1Id, Guid N2Id)> StageNullBaseConstraintAsync()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var n1 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "local note" };
            await _fx.AddLocalAsync(n1);

            var n2 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "remote note" };
            SyncApplyFixture.Stamp(n2, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(n2));

            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.Kind == ConflictKind.ConstraintConflict && r.ConstraintValue == task.MaTask.ToString("D"));
            return (record.ConflictId, task.MaTask, n1.Id, n2.Id);
        }

        /// <summary>The exact "test-M shape": local and remote both reference the SAME, now-tombstoned parent.</summary>
        private async Task<(Guid ConflictId, Guid TaskId, Guid TombstonedMonHocId)> StageParentTombstonedSharedByBothSidesAsync()
        {
            var (_, monHoc, task) = await _fx.SeedTreeAsync();
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, task);

            using (var ctx = _fx.NewContext(ParentDeletedAt, ParentDeleterDevice))
            {
                var live = await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == monHoc.MaMonHoc);
                ctx.MonHocs.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var remote = new StudyTask("must not land live", task.HanChot, task.LoaiTask, task.DoKho)
            { MaTask = task.MaTask, MaMonHoc = monHoc.MaMonHoc };
            SyncApplyFixture.Stamp(remote, SyncApplyFixture.RemoteLater, SyncApplyFixture.PeerDevice);
            await _fx.Session().ApplyAsync(SyncApplyFixture.From(remote));

            var record = (await _fx.ReadConflictsAsync())
                .Single(r => r.EntityType == SyncEntityTypes.StudyTask && r.EntityId == task.MaTask);
            return (record.ConflictId, task.MaTask, monHoc.MaMonHoc);
        }

        // ================================================================= B-4: KeepLocal on AL-PT

        [Fact]
        public async Task ZN4_KeepLocal_OnAlPt_IsKindNotApplicable_NeverReinterpretedAsNullResult()
        {
            var (conflictId, newMonHocId, _) = await StageAbsentLocalStructuralAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, KeepLocal());

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.KindNotApplicable, outcome.Reason);

            var record = Assert.Single(await _fx.ReadConflictsAsync());
            Assert.Equal(ConflictRecordStatus.Unresolved, record.Status);
            Assert.Null(record.ResolutionKind);
            Assert.Null(await _fx.ReadMonHocAsync(newMonHocId));
        }

        // ================================================================= B-2 / residual 12

        /// <summary>
        /// Z-PT1 (generic form) — the result-parent check is general (§11), not keyed on
        /// <c>StructuralReason</c>: an ordinary S1-CR record whose Remote target is separately
        /// tombstoned AFTER staging rejects KeepRemote, while KeepLocal (whose target is still live)
        /// is accepted.
        /// </summary>
        [Fact]
        public async Task ZPT1_ConcurrentReparent_KeepRemote_RejectedWhenTargetTombstoned_KeepLocal_StillApplies()
        {
            var (conflictId, taskId, _, monHocBId, monHocCId) = await StageConcurrentReparentAsync();

            using (var ctx = _fx.NewContext(ParentDeletedAt, ParentDeleterDevice))
            {
                var liveMonHocC = await ctx.MonHocs.FirstAsync(m => m.MaMonHoc == monHocCId);
                ctx.MonHocs.Remove(liveMonHocC);
                await ctx.SaveChangesAsync();
            }

            var keepRemote = await Resolver().ResolveAsync(conflictId, KeepRemote());
            Assert.Equal(ResolutionOutcomeKind.Rejected, keepRemote.Kind);
            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned, keepRemote.Reason);

            var untouched = await _fx.ReadConflictsAsync();
            Assert.Equal(ConflictRecordStatus.Unresolved, Assert.Single(untouched).Status);

            var keepLocal = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Applied, keepLocal.Kind);
            Assert.Equal(monHocBId, (await _fx.ReadTaskAsync(taskId))!.MaMonHoc);
        }

        /// <summary>
        /// Z-PT1 (test-M shape) — Local and Remote reference the SAME tombstoned parent: both
        /// KeepLocal and KeepRemote reject, KeepBase is accepted because it writes nothing, and a
        /// ManualMerge reparenting to a live parent is accepted.
        /// </summary>
        [Fact]
        public async Task ZPT1_TestMShape_BothSidesRejected_KeepBaseAccepted_ManualMergeToLiveParentAccepted()
        {
            var (conflictId, taskId, tombstonedMonHocId) = await StageParentTombstonedSharedByBothSidesAsync();

            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned,
                         (await Resolver().ResolveAsync(conflictId, KeepLocal())).Reason);
            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned,
                         (await Resolver().ResolveAsync(conflictId, KeepRemote())).Reason);

            var before = await _fx.ReadTaskAsync(taskId);
            var keepBase = await Resolver().ResolveAsync(conflictId, KeepBase());
            Assert.Equal(ResolutionOutcomeKind.Applied, keepBase.Kind);
            var after = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(before!.Rev, after!.Rev);                       // no domain write (B-2 (i))
        }

        [Fact]
        public async Task ZPT1_TestMShape_ManualMergeToLiveParent_IsApplied()
        {
            var (conflictId, taskId, _) = await StageParentTombstonedSharedByBothSidesAsync();
            var (_, liveMonHoc, _) = await _fx.SeedTreeAsync();   // a second, unrelated, live MonHoc

            var manual = ManualStudyTask(liveMonHoc.MaMonHoc, "reparented manually");
            var outcome = await Resolver().ResolveAsync(conflictId, ManualMerge(manual, taskId));

            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);
            var row = await _fx.ReadTaskAsync(taskId);
            Assert.Equal(liveMonHoc.MaMonHoc, row!.MaMonHoc);
            Assert.Equal("reparented manually", row.TenTask);
        }

        [Fact]
        public async Task ZPT1_AlPt_KeepRemote_IsRejected_ResultParentTombstoned()
        {
            var (conflictId, newMonHocId, _) = await StageAbsentLocalStructuralAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, KeepRemote());

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned, outcome.Reason);
            Assert.Null(await _fx.ReadMonHocAsync(newMonHocId));
        }

        /// <summary>
        /// Z-PT2 (derived FK leg) — a StudyTask parent tombstoned AFTER an S3 TaskNote scope was
        /// staged rejects KeepLocal/KeepRemote (no live or tombstone note is materialised under it),
        /// while KeepBase(null) still frees the scope.
        /// </summary>
        [Fact]
        public async Task ZPT2_TaskNoteParentTombstonedAfterStaging_RejectsKeepLocalAndKeepRemote_KeepBaseStillApplies()
        {
            var (conflictId, taskId, n1Id, n2Id) = await StageNullBaseConstraintAsync();

            using (var ctx = _fx.NewContext(ParentDeletedAt, ParentDeleterDevice))
            {
                var live = await ctx.StudyTasks.FirstAsync(t => t.MaTask == taskId);
                ctx.StudyTasks.Remove(live);
                await ctx.SaveChangesAsync();
            }

            var keepLocal = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned, keepLocal.Reason);
            Assert.Equal(0, await _fx.CountNotesInScopeAsync(taskId));

            var keepRemote = await Resolver().ResolveAsync(conflictId, KeepRemote());
            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned, keepRemote.Reason);
            Assert.Equal(0, await _fx.CountNotesInScopeAsync(taskId));

            var keepBase = await Resolver().ResolveAsync(conflictId, KeepBase());
            Assert.Equal(ResolutionOutcomeKind.Applied, keepBase.Kind);
            Assert.Equal(0, await _fx.CountNotesInScopeAsync(taskId));
        }

        // Z-PT3 — AL-PT ManualMerge reparented to live / tombstoned / missing parent.

        [Fact]
        public async Task ZPT3_AlPt_ManualMerge_ReparentToLiveParent_IsApplied()
        {
            var (conflictId, newMonHocId, _) = await StageAbsentLocalStructuralAsync();
            var liveHocKy = new HocKy("HK live", new DateTime(2026, 4, 1));
            await _fx.AddLocalAsync(liveHocKy);

            var outcome = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualMonHoc(liveHocKy.MaHocKy, "reparented"), newMonHocId));

            Assert.Equal(ResolutionOutcomeKind.Applied, outcome.Kind);
            var row = await _fx.ReadMonHocAsync(newMonHocId);
            Assert.NotNull(row);
            Assert.Equal(liveHocKy.MaHocKy, row!.MaHocKy);
            Assert.Equal(1, row.Rev);
        }

        [Fact]
        public async Task ZPT3_AlPt_ManualMerge_ToTombstonedParent_IsRejected()
        {
            var (conflictId, newMonHocId, tombstonedHocKyId) = await StageAbsentLocalStructuralAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualMonHoc(tombstonedHocKyId, "still dead"), newMonHocId));

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.ResultParentTombstoned, outcome.Reason);
            Assert.Null(await _fx.ReadMonHocAsync(newMonHocId));
        }

        [Fact]
        public async Task ZPT3_AlPt_ManualMerge_ToMissingParent_IsRejected()
        {
            var (conflictId, newMonHocId, _) = await StageAbsentLocalStructuralAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualMonHoc(Guid.NewGuid(), "no such semester"), newMonHocId));

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.ResultParentMissing, outcome.Reason);
            Assert.Null(await _fx.ReadMonHocAsync(newMonHocId));
        }

        // ================================================================= B-3: ManualMerge tombstones

        [Fact]
        public async Task ZN5_ManualMergeTombstone_Rejected_ForStudyTask_TwinAccepted()
        {
            var (conflictId, taskId, _, monHocBId, _) = await StageConcurrentReparentAsync();

            var tombstone = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualStudyTask(monHocBId, "manual", isDeleted: true), taskId));
            Assert.Equal(ResolutionOutcomeKind.Rejected, tombstone.Kind);
            Assert.Equal(ResolutionRejectReason.ManualTombstoneNotSupported, tombstone.Reason);
            Assert.False((await _fx.ReadTaskAsync(taskId))!.IsDeleted);

            var applied = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualStudyTask(monHocBId, "manual"), taskId));
            Assert.Equal(ResolutionOutcomeKind.Applied, applied.Kind);
        }

        [Fact]
        public async Task ZN5_ManualMergeTombstone_Rejected_ForMonHoc_TwinAccepted()
        {
            var (conflictId, monHocId, hocKyBId, _) = await StageMonHocReparentAsync();

            var tombstone = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualMonHoc(hocKyBId, "manual", isDeleted: true), monHocId));
            Assert.Equal(ResolutionOutcomeKind.Rejected, tombstone.Kind);
            Assert.Equal(ResolutionRejectReason.ManualTombstoneNotSupported, tombstone.Reason);
            Assert.False((await _fx.ReadMonHocAsync(monHocId))!.IsDeleted);

            var applied = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualMonHoc(hocKyBId, "manual"), monHocId));
            Assert.Equal(ResolutionOutcomeKind.Applied, applied.Kind);
        }

        [Fact]
        public async Task ZN5_ManualMergeTombstone_Rejected_ForTaskNote_NoLeafException_TwinAccepted()
        {
            var (conflictId, taskId, n1Id, _) = await StageNullBaseConstraintAsync();

            var tombstone = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualTaskNote(taskId, "manual", isDeleted: true), n1Id));
            Assert.Equal(ResolutionOutcomeKind.Rejected, tombstone.Kind);
            Assert.Equal(ResolutionRejectReason.ManualTombstoneNotSupported, tombstone.Reason);   // no TaskNote exception (B-3)
            Assert.Null(await _fx.ReadNoteAsync(n1Id));

            var applied = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualTaskNote(taskId, "manual"), n1Id));
            Assert.Equal(ResolutionOutcomeKind.Applied, applied.Kind);
        }

        // ================================================================= E-3: entity-scoped identity

        [Fact]
        public async Task ZN3_ManualMerge_WrongEntityId_OnStructuralRecord_IsRejected_CorrectId_IsApplied()
        {
            var (conflictId, taskId, _, monHocBId, _) = await StageConcurrentReparentAsync();
            var manualResult = ManualStudyTask(monHocBId, "manual name");

            var wrong = await Resolver().ResolveAsync(conflictId, ManualMerge(manualResult, Guid.NewGuid()));
            Assert.Equal(ResolutionOutcomeKind.Rejected, wrong.Kind);
            Assert.Equal(ResolutionRejectReason.ResultOutOfScope, wrong.Reason);

            var correct = await Resolver().ResolveAsync(conflictId, ManualMerge(manualResult, taskId));
            Assert.Equal(ResolutionOutcomeKind.Applied, correct.Kind);

            var row = await _fx.ReadTaskAsync(taskId);
            Assert.Equal("manual name", row!.TenTask);
            Assert.Equal(monHocBId, row.MaMonHoc);
        }

        [Fact]
        public async Task ZN3_ManualMerge_WrongEntityType_IsRejected()
        {
            var (conflictId, taskId, _, _, _) = await StageConcurrentReparentAsync();

            var outcome = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualMonHoc(Guid.NewGuid(), "wrong type"), taskId));

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.ResultOutOfScope, outcome.Reason);
        }

        /// <summary>
        /// Z-N3 (Constraint id rules) — missing id (InvalidRequest), wrong MaTask (ResultOutOfScope), a
        /// colliding fresh id (ResultIdInUse) and a genuinely fresh id (Applied — a Constraint scope may
        /// legitimately mint a new entity, unlike a Structural scope; E-3 does not narrow this leg).
        /// </summary>
        [Fact]
        public async Task ZN3_ManualMerge_OnConstraintScope_IdRulesAndMaTaskScope()
        {
            var (conflictId, taskId, _, _) = await StageNullBaseConstraintAsync();

            var missingId = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualTaskNote(taskId, "x"), null));
            Assert.Equal(ResolutionRejectReason.InvalidRequest, missingId.Reason);

            var wrongScope = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualTaskNote(Guid.NewGuid(), "x"), Guid.NewGuid()));
            Assert.Equal(ResolutionRejectReason.ResultOutOfScope, wrongScope.Reason);

            var (_, _, unrelatedTask) = await _fx.SeedTreeAsync();
            var collidingId = Guid.NewGuid();
            using (var ctx = _fx.NewContext())
            {
                ctx.TaskNotes.Add(new TaskNote { Id = collidingId, MaTask = unrelatedTask.MaTask, Content = "unrelated" });
                await ctx.SaveChangesAsync();
            }
            var collision = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualTaskNote(taskId, "x"), collidingId));
            Assert.Equal(ResolutionRejectReason.ResultIdInUse, collision.Reason);

            var freshId = Guid.NewGuid();
            var applied = await Resolver().ResolveAsync(conflictId, ManualMerge(ManualTaskNote(taskId, "brand new"), freshId));
            Assert.Equal(ResolutionOutcomeKind.Applied, applied.Kind);
            var fresh = await _fx.ReadNoteAsync(freshId);
            Assert.NotNull(fresh);
            Assert.Equal(1, fresh!.Rev);
        }

        // ================================================================= E-8: scope occupancy

        /// <summary>
        /// E-8 — a Constraint record whose live scope is occupied by an entity DIFFERENT from the one a
        /// resolution would write cannot be applied: TaskNotes(MaTask) is an unfiltered UNIQUE index and
        /// PR-6 has no withdrawal mechanism (M5 is staging-time only). Built by direct row injection —
        /// the same technique <c>SyncApplyConflictStagingTests.J_WhenStagingIsRejected...</c> uses —
        /// because this S2 shape (Base present, Local shares Base's Id, Remote a different Id) is
        /// documented as unreachable through the real apply session in v1 (DoR §9, S2 row); the resolver
        /// must still not crash on a record shaped like it.
        /// </summary>
        [Fact]
        public async Task E8_KeepRemote_OnAScopeStillOccupiedByADifferentEntity_IsScopeOccupied()
        {
            var (_, _, task) = await _fx.SeedTreeAsync();
            var n0 = new TaskNote { Id = Guid.NewGuid(), MaTask = task.MaTask, Content = "base content" };
            await _fx.AddLocalAsync(n0);
            await _fx.SetBaselineAsync(SyncApplyFixture.PeerDevice, n0);

            var baseSnapshot = IncomingChanges.Of((await _fx.ReadNoteAsync(n0.Id))!).Snapshot;
            var remoteSnapshot = ManualTaskNote(task.MaTask, "competing remote");
            var n2Id = Guid.NewGuid();

            var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", task.MaTask.ToString("D"));
            var scopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote, null, null, scope);

            Guid conflictId;
            using (var ctx = _fx.NewContext())
            {
                var record = new SyncConflictRecordRow
                {
                    ConflictId = Guid.NewGuid(),
                    ConflictKey = "ck-e8-scope-occupied",
                    ScopeKey = scopeKey,
                    Kind = ConflictKind.ConstraintConflict,
                    EntityType = SyncEntityTypes.TaskNote,
                    ConstraintKey = "MaTask",
                    ConstraintValue = task.MaTask.ToString("D"),
                    PeerDeviceId = SyncApplyFixture.PeerDevice,
                    SnapshotVersion = CanonicalJson.Version,
                    BaseEntityId = n0.Id,
                    BaseSnapshotJson = CanonicalJson.Write(baseSnapshot!),
                    BaseFingerprint = CanonicalJson.Fingerprint(baseSnapshot),
                    LocalEntityId = n0.Id,
                    LocalSnapshotJson = CanonicalJson.Write(baseSnapshot!),
                    LocalFingerprint = CanonicalJson.Fingerprint(baseSnapshot),
                    LocalRowRev = n0.Rev,
                    RemoteEntityId = n2Id,
                    RemoteSnapshotJson = CanonicalJson.Write(remoteSnapshot),
                    RemoteFingerprint = CanonicalJson.Fingerprint(remoteSnapshot),
                    Status = ConflictRecordStatus.Unresolved,
                    CreatedAtUtc = SyncApplyFixture.LocalNow,
                    CreatedByDeviceId = SyncApplyFixture.LocalDevice,
                };
                ctx.SyncConflictRecords.Add(record);
                await ctx.SaveChangesAsync();
                conflictId = record.ConflictId;
            }

            var outcome = await Resolver().ResolveAsync(conflictId, KeepRemote());

            Assert.Equal(ResolutionOutcomeKind.Rejected, outcome.Kind);
            Assert.Equal(ResolutionRejectReason.ScopeOccupied, outcome.Reason);
            Assert.Null(await _fx.ReadNoteAsync(n2Id));
            Assert.NotNull(await _fx.ReadNoteAsync(n0.Id));

            // KeepLocal on the SAME record (same id as the current occupant) is not scope-occupied.
            var keepLocal = await Resolver().ResolveAsync(conflictId, KeepLocal());
            Assert.Equal(ResolutionOutcomeKind.Applied, keepLocal.Kind);
        }
    }
}
