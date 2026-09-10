using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-5) — the sync apply/orchestration layer. Composes the four merged
    /// infrastructure slices into one correct logical sync operation:
    /// <code>
    /// load base -> obtain Local/Remote -> T2.3 Merge() -> apply result OR stage conflict
    ///           -> baseline with POST-APPLY Rev -> commit atomically
    /// </code>
    ///
    /// <para><b>Transaction and context lifetime.</b> One logical sync operation = one transaction
    /// (D8-G) <i>and</i> one <see cref="AppDbContext"/>. The context is created from the caller's
    /// factory immediately before the operation and disposed immediately after it, on every path.
    /// <br/>
    /// DoR §12.1 as written gives the session one context for the whole run; the PR-2 residuals note
    /// (<c>docs/review/2026-09-09-t2.4-pr5-pr2-residuals.md</c>, R2 "OPEN") flags that this cannot
    /// coexist with "dispose the failed context" and asks PR-5 to have the owner ratify one shape. The
    /// PR-5 brief ratifies the per-operation shape explicitly — "It should own: one DbContext; one
    /// logical sync-operation transaction", "After a failure: context lifetime ends", and a
    /// <c>using var db = CreateDbContext()</c> inside the per-operation sketch. This class implements
    /// that. It makes R2 Case A and Case B <i>structurally</i> impossible rather than disciplined away:
    /// a dirty or stale-but-clean instance cannot survive into another operation because the object
    /// that held it no longer exists. Both cases were reproduced first, against the real seam, in
    /// <c>SyncApplyFailedSaveStateTests</c>.
    /// </para>
    ///
    /// <para><b>R1 (residuals note).</b> <see cref="AppDbContext.MarkSyncApplied"/> is keyed by
    /// reference identity and silently does nothing for an instance the ChangeTracker never visits, so
    /// marking an <c>AsNoTracking</c> instance writes nothing while reporting success. Every write in
    /// this session goes through <see cref="WriteAsync"/>, which resolves the <i>tracked</i> instance,
    /// asserts it is not <see cref="EntityState.Detached"/>, and only then marks — closing R1 by
    /// construction. The merge always reads through <c>AsNoTracking</c> (DoR §11.3) and those
    /// instances are never marked or written.
    /// </para>
    ///
    /// <para><b>Not in this PR.</b> Resolution orchestration (KeepLocal/KeepRemote/KeepBase/
    /// ManualMerge, the D8-H drift precondition, replay classification) is PR-6 / DoR §13. PR-5 ships
    /// only the shared drift primitive (<see cref="SyncBaseFingerprint"/>) that PR-6 consumes.
    /// </para>
    /// </summary>
    public sealed class SyncApplySession
    {
        private readonly Func<AppDbContext> _contextFactory;

        /// <param name="contextFactory">
        /// Must return a context configured exactly like the composition root's
        /// (<c>ServiceLocator</c>'s <c>ctxFactory</c>), so <c>DeviceIdProvider</c> is the persisted
        /// device identity. The session calls it once per logical operation and disposes the result.
        /// </param>
        public SyncApplySession(Func<AppDbContext> contextFactory)
            => _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

        /// <summary>
        /// Applies one peer's change set. Every operation is independent (D8-G): a rejection or a
        /// failure in one never rolls back another, and the run continues. Operations whose parent has
        /// not arrived yet are retried once at the end (DoR §12.5 step 3); anything still missing a
        /// parent is rejected, not orphaned.
        /// </summary>
        public async Task<SyncApplyReport> ApplyAsync(IncomingChangeSet changes, CancellationToken ct = default)
        {
            if (changes is null) throw new ArgumentNullException(nameof(changes));

            var plan = SyncApplyPlanner.Plan(changes);
            var results = new List<SyncApplyResult>(plan.Count);
            var deferred = new List<SyncOperation>();

            foreach (var op in plan)
            {
                var result = await ExecuteAsync(op, changes.PeerDeviceId, ct);
                if (result.Outcome == SyncApplyOutcome.Rejected && result.Reason == SyncApplyReason.MissingParent)
                {
                    deferred.Add(op);   // a parent may still arrive later in this same run
                    continue;
                }
                results.Add(result);
            }

            foreach (var op in deferred)
                results.Add(await ExecuteAsync(op, changes.PeerDeviceId, ct));

            return new SyncApplyReport(results);
        }

        // ================================================================= one operation = one tx

        private async Task<SyncApplyResult> ExecuteAsync(SyncOperation operation, string peerId, CancellationToken ct)
        {
            var db = _contextFactory();
            try
            {
                // `await using` is the load-bearing guarantee here: disposing an uncommitted
                // transaction rolls it back. Measured — deleting the two explicit RollbackAsync calls
                // below leaves every transaction test green, so they are defence in depth and a
                // statement of intent, not the mechanism. What IS discriminating is that ONE
                // transaction spans both saves (DoR §11.2): committing between them turns
                // SyncApplyTransactionTests red.
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var state = new OperationState(db, peerId, db.Clock(), db.DeviceIdProvider());
                    var result = await RouteAsync(state, operation, ct);

                    // A rejection wrote nothing by contract, but rolling back is what makes that a
                    // guarantee rather than a claim about every early-return path above.
                    if (result.Outcome == SyncApplyOutcome.Rejected) await tx.RollbackAsync(ct);
                    else await tx.CommitAsync(ct);

                    return result;
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
            catch (MergeContractViolationException)
            {
                // Fail closed: the pure core was handed input the model says cannot exist. Never a
                // conflict (DoR §4.1); isolated to this operation (D8-G).
                return SyncApplyResult.Rejected(operation, SyncApplyReason.ContractViolation);
            }
            catch (Exception ex)
            {
                return SyncApplyResult.Failed(operation, ex);
            }
            finally
            {
                // Context lifetime ends with the operation — success, rejection or failure alike.
                await db.DisposeAsync();
            }
        }

        private static Task<SyncApplyResult> RouteAsync(OperationState st, SyncOperation op, CancellationToken ct) =>
            op.Kind == SyncOperationKind.TaskNoteScope
                ? ApplyTaskNoteScopeAsync(st, op, ct)
                : ApplyEntityAsync(st, op, op.EntityId, op.Remote, StructuralScopeKeyOf(op.EntityType, op.EntityId), ct);

        // ================================================================= entity operation

        private static async Task<SyncApplyResult> ApplyEntityAsync(
            OperationState st, SyncOperation op, Guid entityId, EntitySnapshot remote,
            string? lockScopeKey, CancellationToken ct)
        {
            var entityType = op.EntityType;

            // ---- Base -------------------------------------------------------------------------
            var baseRow = await SyncBaseSnapshotStore.GetAsync(st.Db, st.PeerId, entityType, entityId, ct);
            if (!TryReadBaseline(baseRow, entityType, out var baseSnapshot))
                return SyncApplyResult.Rejected(op, SyncApplyReason.BaselineUnreadable);

            // ---- Local (AsNoTracking, DoR §11.3) ----------------------------------------------
            var localEntity = await LoadNoTrackingAsync(st.Db, entityType, entityId, ct);

            EntitySnapshot? result;
            IReadOnlyList<ConflictCandidate> candidates;
            bool isNoOp;

            if (localEntity is null)
            {
                // A baseline says this peer already had the row, so a missing local row means someone
                // hard-deleted a synced entity. Fail closed rather than silently re-creating it.
                if (baseSnapshot is not null)
                    return SyncApplyResult.Rejected(op, SyncApplyReason.LocalRowMissing);

                result = remote;                       // ordinary create of the incoming row
                candidates = Array.Empty<ConflictCandidate>();
                isNoOp = false;
            }
            else
            {
                var merged = ThreeWayMerge.Merge(
                    new EntityRef(entityType, entityId), baseSnapshot,
                    EntitySnapshotMapper.ToSnapshot(localEntity), remote);

                result = merged.Result;
                candidates = merged.Candidates;
                isNoOp = merged.IsNoOp;
            }

            // ---- D4 ConcurrentReparent (the pure core's only Unresolved output) ----------------
            var structural = candidates.FirstOrDefault(c => c.Kind == ConflictKind.StructuralConflict);
            if (structural is not null)
            {
                return await StageStructuralAsync(st, op, entityType, entityId, localEntity,
                                                  baseSnapshot, structural, ct);
            }

            // ---- D9-T6 / DoR §12.4 scope-lock --------------------------------------------------
            // Any write into a scope holding an Unresolved record would make live != Base, and v1 has
            // no rebase (D8-H). Checked before anything is staged or written.
            if (lockScopeKey is not null &&
                await SyncConflictRecordStore.FindUnresolvedByScopeKeyAsync(st.Db, lockScopeKey, ct) is not null)
            {
                return SyncApplyResult.Rejected(op, SyncApplyReason.ScopeHasUnresolvedConflict);
            }

            // ---- D9-T4 / DoR §12.2 parent handling ---------------------------------------------
            var autoEvidence = new List<ConflictCandidate>(candidates);

            if (result is not null && !result.Provenance.IsDeleted)
            {
                var parent = await InspectParentAsync(st.Db, entityType, result, ct);
                switch (parent.State)
                {
                    case ParentState.Missing:
                        // Deferred by apply ordering; rejected if still absent after the retry pass.
                        return SyncApplyResult.Rejected(op, SyncApplyReason.MissingParent);

                    case ParentState.Tombstoned when parent.Rule == ParentRule.Structural:
                        return await StageParentTombstonedAsync(st, op, entityType, entityId, localEntity,
                                                                baseSnapshot, result, ct);

                    case ParentState.Tombstoned when parent.Rule == ParentRule.FkOnlyChild:
                        // ACK A2-a: an FK-only child arriving live under a tombstoned task is cascade-
                        // tombstoned, not turned into a StructuralConflict — a StructuralConflict here
                        // would offer KeepLocal/KeepRemote, both of which re-create a live child under
                        // a dead parent. Provenance is the parent's (ACK A2-b).
                        if (localEntity is not null && baseSnapshot is not null)
                        {
                            autoEvidence.Add(TombstoneEvidence(
                                entityType, entityId, baseSnapshot,
                                EntitySnapshotMapper.ToSnapshot(localEntity),
                                WithTombstone(result, parent.Provenance!)));
                        }
                        result = WithTombstone(result, parent.Provenance!);
                        isNoOp = false;
                        break;

                    // ParentRule.NoFkChild (StudyLog): a live log under a tombstoned task is the
                    // current local model — analytics/ML read them — so it applies unchanged, with no
                    // evidence. ParentRule.None (HocKy): nothing to check.
                }
            }

            if (result is null)
                return SyncApplyResult.Rejected(op, SyncApplyReason.StructuralConflictWithoutBase);

            // ---- apply --------------------------------------------------------------------------
            var wasLive = localEntity is not null && !localEntity.IsDeleted;

            if (isNoOp)
            {
                // §11.1 rule 4: a no-op writes nothing and must not force Modified, so Rev is
                // untouched and a replay stays a replay. §11.2: the baseline still advances if it was
                // missing or behind, otherwise it is left alone.
                await StageAutoEvidenceAsync(st, autoEvidence, ct);
                if (baseRow is null || baseRow.Rev < localEntity!.Rev)
                {
                    await SyncBaseSnapshotStore.UpsertAsync(
                        st.Db, st.PeerId, entityType, entityId, localEntity!.Rev,
                        CanonicalJson.Write(EntitySnapshotMapper.ToSnapshot(localEntity)), st.NowUtc, ct);
                }
                await st.Db.SaveChangesAsync(ct);
                return SyncApplyResult.NoOp(op);
            }

            await StageAutoEvidenceAsync(st, autoEvidence, ct);
            await WriteAsync(st, entityType, entityId, result, ct);

            // Cascade only on the live -> dead transition: a row that was already tombstoned locally
            // has cascaded once already, and re-cascading would re-stamp children on every replay.
            if (result.Provenance.IsDeleted && wasLive)
                await CascadeTombstoneAsync(st, entityType, entityId, result.Provenance, ct);

            await st.Db.SaveChangesAsync(ct);          // save #1: Rev++ on every marked entry
            await UpsertBaselinesAsync(st, ct);        // POST-apply Rev, read off the tracked instances
            await st.Db.SaveChangesAsync(ct);          // save #2, same transaction (DoR §11.2)

            return SyncApplyResult.Applied(op);
        }

        // ================================================================= TaskNote uniqueness scope

        private static async Task<SyncApplyResult> ApplyTaskNoteScopeAsync(
            OperationState st, SyncOperation op, CancellationToken ct)
        {
            var scope = new ConstraintScope(SyncEntityTypes.TaskNote, "MaTask", op.ScopeValue.ToString("D"));
            var scopeKey = ConflictKeys.ScopeKey(ConflictKind.ConstraintConflict, SyncEntityTypes.TaskNote,
                                                 null, null, scope);

            // Base for a SCOPE is the baseline row that occupies it, which need not share the incoming
            // row's Id — so it is located by scope value, not by PK.
            var (baseId, baseSnapshot, baselineReadable) = await LoadTaskNoteBaselineInScopeAsync(st, op.ScopeValue, ct);
            if (!baselineReadable) return SyncApplyResult.Rejected(op, SyncApplyReason.BaselineUnreadable);

            // The UNIQUE index on TaskNotes(MaTask) is unfiltered, so a tombstoned note still occupies
            // the scope. It must be passed to the pure core (which then reports
            // ScopeOccupiedByTombstone) — hiding it would turn a fail-closed rejection into a raw
            // UNIQUE violation at SaveChanges, which D9 §19 forbids as a detection mechanism.
            var localNote = await st.Db.TaskNotes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.MaTask == op.ScopeValue, ct);

            var detection = ConstraintMerge.Detect(new ConstraintScopeInput(
                scope,
                baseId is { } bid && baseSnapshot is not null ? (bid, baseSnapshot) : null,
                localNote is null ? null : (localNote.Id, EntitySnapshotMapper.ToSnapshot(localNote)),
                (op.EntityId, op.Remote)));

            switch (detection.Outcome)
            {
                case ConstraintOutcome.NoAction:
                case ConstraintOutcome.LocalOnly:
                    return SyncApplyResult.NoOp(op);

                case ConstraintOutcome.ScopeOccupiedByTombstone:
                    return SyncApplyResult.Rejected(op, SyncApplyReason.ScopeOccupiedByTombstone);

                case ConstraintOutcome.OrdinaryMerge:
                case ConstraintOutcome.CreateRemote:
                    // Same row on both sides, or a scope only the remote occupies: an ordinary entity
                    // apply, still guarded by this scope's lock and by the FK parent rule.
                    return await ApplyEntityAsync(st, op, op.EntityId, op.Remote, scopeKey, ct);

                case ConstraintOutcome.ConstraintConflict:
                    return await StageConstraintAsync(st, op, detection.Candidate!, localNote!, baseSnapshot, ct);

                default:
                    throw new MergeContractViolationException($"Unhandled constraint outcome {detection.Outcome}.");
            }
        }

        // ================================================================= conflict staging paths

        /// <summary>
        /// D9-T1 shape S1 — a ConcurrentReparent conflict. The local row is rewritten to the WHOLE Base
        /// row (not just the FK), because "live = Base" read literally means no other field may be
        /// partially applied either; the record's full-row evidence is what lets PR-6 restore either
        /// side later.
        /// </summary>
        private static async Task<SyncApplyResult> StageStructuralAsync(
            OperationState st, SyncOperation op, string entityType, Guid entityId,
            ISyncMetadata? localEntity, EntitySnapshot? baseSnapshot, ConflictCandidate candidate,
            CancellationToken ct)
        {
            // With a null Base there is no row to hold live and no audited withdrawal mechanism for
            // anything but a TaskNote. Unreachable by construction — a ConcurrentReparent needs the
            // same entity Id on both sides, and two devices cannot independently mint one Guid — so
            // this fails closed instead of inventing a mechanism for it.
            if (baseSnapshot is null || localEntity is null)
                return SyncApplyResult.Rejected(op, SyncApplyReason.StructuralConflictWithoutBase);

            var row = ConflictStaging.ToRow(candidate, st.PeerId, st.NowUtc, st.DeviceId,
                                            localEntity.Rev, ConflictLocalWithdrawal.RewrittenToBase);

            var staged = await SyncConflictRecordStore.StageAsync(st.Db, row, ct);
            if (staged.Outcome == ConflictStagingOutcome.ScopeHasUnresolvedConflict)
                return SyncApplyResult.Rejected(op, SyncApplyReason.ScopeHasUnresolvedConflict);
            if (staged.Outcome == ConflictStagingOutcome.AlreadyStaged)
                return new SyncApplyResult(op, SyncApplyOutcome.ConflictAlreadyStaged, ConflictId: staged.Record.ConflictId);

            await WriteAsync(st, entityType, entityId, baseSnapshot, ct);
            await st.Db.SaveChangesAsync(ct);
            await UpsertBaselinesAsync(st, ct);        // (Rev_post, canonical(Base)) — DoR §11.2
            await st.Db.SaveChangesAsync(ct);

            return new SyncApplyResult(op, SyncApplyOutcome.ConflictStaged, ConflictId: row.ConflictId);
        }

        /// <summary>
        /// D9-T4 — the incoming child targets a tombstoned D4 parent. Never a live orphan.
        /// <para>
        /// When a local row exists and Base exists, this is S1 with <c>ParentTombstoned</c> as the
        /// reason: hold the local row at Base and stage full evidence.
        /// </para>
        /// <para>
        /// When there is NO local row (the incoming child is a pure create), the child is simply not
        /// materialised — which is exactly what DoR §12.2 says the Base-null case means — and the
        /// operation is rejected. No record is written, because the evidence model has no slot for
        /// "there is no competing local candidate": <c>ConflictCandidate.Local</c> and the schema's
        /// <c>LocalSnapshotJson</c> are both non-nullable, and filling them with the remote row would
        /// record evidence that never existed. This is reported as a semantic blocker rather than
        /// resolved locally. It loses nothing: the row stays on the peer and is re-offered every run,
        /// and once the peer receives our parent tombstone its own cascade turns the child into a
        /// tombstone that applies cleanly.
        /// </para>
        /// </summary>
        private static async Task<SyncApplyResult> StageParentTombstonedAsync(
            OperationState st, SyncOperation op, string entityType, Guid entityId,
            ISyncMetadata? localEntity, EntitySnapshot? baseSnapshot, EntitySnapshot remoteResult,
            CancellationToken ct)
        {
            if (localEntity is null || baseSnapshot is null)
                return SyncApplyResult.Rejected(op, SyncApplyReason.ParentTombstonedNoLocalRow);

            var candidate = new ConflictCandidate(
                ConflictKind.StructuralConflict, entityType, entityId,
                StructuralFieldOf(entityType), null, StructuralReason.ParentTombstoned,
                baseSnapshot, entityId,
                EntitySnapshotMapper.ToSnapshot(localEntity), entityId,
                remoteResult, entityId,
                null, null);

            return await StageStructuralAsync(st, op, entityType, entityId, localEntity, baseSnapshot, candidate, ct);
        }

        /// <summary>
        /// D5 / D9-T1 — a constraint conflict on a <c>TaskNote</c> uniqueness scope. Neither candidate
        /// becomes live. With a Base the local candidate is rewritten to it (S2); with a null Base the
        /// scope must end with no row at all, which is the audited M5 exception (S3).
        /// </summary>
        private static async Task<SyncApplyResult> StageConstraintAsync(
            OperationState st, SyncOperation op, ConflictCandidate candidate,
            TaskNote localNote, EntitySnapshot? baseSnapshot, CancellationToken ct)
        {
            var withdrawal = baseSnapshot is null
                ? ConflictLocalWithdrawal.HardDeleted
                : ConflictLocalWithdrawal.RewrittenToBase;

            var row = ConflictStaging.ToRow(candidate, st.PeerId, st.NowUtc, st.DeviceId,
                                            localNote.Rev, withdrawal);

            var staged = await SyncConflictRecordStore.StageAsync(st.Db, row, ct);
            if (staged.Outcome == ConflictStagingOutcome.ScopeHasUnresolvedConflict)
                return SyncApplyResult.Rejected(op, SyncApplyReason.ScopeHasUnresolvedConflict);
            if (staged.Outcome == ConflictStagingOutcome.AlreadyStaged)
                return new SyncApplyResult(op, SyncApplyOutcome.ConflictAlreadyStaged, ConflictId: staged.Record.ConflictId);

            if (baseSnapshot is null)
            {
                // S3. Evidence first, and committed-first: SaveChanges puts the record (with the full
                // local snapshot and its Rev) on the connection BEFORE the physical delete, which is
                // the only order under which ConflictStaging's precondition is checkable and the only
                // order in which the invariant holds at every observable instant. Both statements are
                // inside this operation's transaction and roll back together.
                await st.Db.SaveChangesAsync(ct);
                await ConflictStaging.HardDeleteWithdrawnTaskNoteAsync(st.Db, row, localNote.Id, ct);

                // No baseline row is written: Base was null, so none existed for this peer, and the
                // remote candidate was deliberately not materialised (DoR §11.2).
                return new SyncApplyResult(op, SyncApplyOutcome.ConflictStaged, ConflictId: row.ConflictId);
            }

            // S2 — rewrite the local candidate to the Base row, same Id.
            await WriteAsync(st, SyncEntityTypes.TaskNote, localNote.Id, baseSnapshot, ct);
            await st.Db.SaveChangesAsync(ct);
            await UpsertBaselinesAsync(st, ct);
            await st.Db.SaveChangesAsync(ct);

            return new SyncApplyResult(op, SyncApplyOutcome.ConflictStaged, ConflictId: row.ConflictId);
        }

        /// <summary>
        /// D9-T5 auto evidence (<c>AutoLww</c> / <c>AutoTombstone</c>) is written directly as Resolved
        /// and never changes the merge outcome. Deduped by ConflictKey inside the operation because
        /// <c>StageAsync</c>'s own check is a database query and cannot see rows added to the tracker
        /// earlier in this same unit of work.
        /// </summary>
        private static async Task StageAutoEvidenceAsync(
            OperationState st, IEnumerable<ConflictCandidate> candidates, CancellationToken ct)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.AutoResolution is null) continue;   // Unresolved kinds have their own paths

                var row = ConflictStaging.ToRow(candidate, st.PeerId, st.NowUtc, st.DeviceId,
                                                null, ConflictLocalWithdrawal.None);
                if (!st.StagedKeys.Add(row.ConflictKey)) continue;

                await SyncConflictRecordStore.StageAsync(st.Db, row, ct);
            }
        }

        // ================================================================= writes

        /// <summary>
        /// The single write path (R1). Resolves the TRACKED instance — never the <c>AsNoTracking</c>
        /// instance the merge read — writes the snapshot onto it, asserts it is attached, and marks it.
        /// <c>DbSet.Update(detachedPoco)</c> is deliberately never used anywhere in this session: that
        /// is the exact shape PR-A had to fix, where a detached POCO's <c>Rev = 0</c> and a fresh
        /// <c>CreatedAtUtc</c> overwrote the persisted row.
        /// </summary>
        private static async Task<ISyncMetadata> WriteAsync(
            OperationState st, string entityType, Guid entityId, EntitySnapshot snapshot, CancellationToken ct)
        {
            var tracked = await LoadTrackedAsync(st.Db, entityType, entityId, ct);
            if (tracked is null)
            {
                tracked = EntitySnapshotMapper.CreateEntity(entityType, entityId, snapshot);
                st.Db.Add(tracked);
            }
            else
            {
                EntitySnapshotMapper.ApplyTo(tracked, snapshot);
            }

            if (st.Db.Entry(tracked).State == EntityState.Detached)
            {
                throw new InvalidOperationException(
                    $"sync-apply tried to mark a detached {entityType} {entityId:D}; MarkSyncApplied would be a silent no-op.");
            }

            st.Db.MarkSyncApplied(tracked);
            st.Written.Add(tracked);
            return tracked;
        }

        /// <summary>
        /// DoR §12.3 (ACK A2-b) — cascade tombstones for the parent's LIVE local children, generated
        /// locally in the same transaction. Children are found by FK through raw DbSet queries, never
        /// through navigation <c>Include</c>: EF's ChangeTracker fixup only ever reaches children that
        /// are already loaded, which is the documented Epic-1 gap <c>TaskCascadeHelper</c> exists for.
        /// <para>
        /// The provenance written on every descendant is the ORIGINATING tombstone's, propagated
        /// unchanged down the whole cascade. That is what makes both peers write identical bytes for
        /// the same causal deletion; stamping local-now instead would be deterministic per device but
        /// divergent across devices.
        /// </para>
        /// <para>
        /// The cascade edges are exactly the ones the model already has — HocKy→MonHoc, MonHoc→StudyTask
        /// (<c>OnDelete(Cascade)</c>) and StudyTask→{TaskNote, TaskReferenceLink}
        /// (<c>TaskCascadeHelper</c>). <c>StudyLog</c> is deliberately absent: it has no FK and Epic-1's
        /// cascade never touched it, so logs survive their task's deletion.
        /// </para>
        /// </summary>
        private static async Task CascadeTombstoneAsync(
            OperationState st, string entityType, Guid parentId, Provenance tombstone, CancellationToken ct)
        {
            switch (entityType)
            {
                case SyncEntityTypes.HocKy:
                    foreach (var child in await st.Db.MonHocs.Where(m => m.MaHocKy == parentId && !m.IsDeleted).ToListAsync(ct))
                    {
                        await TombstoneCascadeChildAsync(st, SyncEntityTypes.MonHoc, child, tombstone, ct);
                        await CascadeTombstoneAsync(st, SyncEntityTypes.MonHoc, child.MaMonHoc, tombstone, ct);
                    }
                    break;

                case SyncEntityTypes.MonHoc:
                    foreach (var child in await st.Db.StudyTasks.Where(t => t.MaMonHoc == parentId && !t.IsDeleted).ToListAsync(ct))
                    {
                        await TombstoneCascadeChildAsync(st, SyncEntityTypes.StudyTask, child, tombstone, ct);
                        await CascadeTombstoneAsync(st, SyncEntityTypes.StudyTask, child.MaTask, tombstone, ct);
                    }
                    break;

                case SyncEntityTypes.StudyTask:
                    foreach (var note in await st.Db.TaskNotes.Where(n => n.MaTask == parentId && !n.IsDeleted).ToListAsync(ct))
                        await TombstoneCascadeChildAsync(st, SyncEntityTypes.TaskNote, note, tombstone, ct);

                    foreach (var link in await st.Db.TaskReferenceLinks.Where(l => l.MaTask == parentId && !l.IsDeleted).ToListAsync(ct))
                        await TombstoneCascadeChildAsync(st, SyncEntityTypes.TaskReferenceLink, link, tombstone, ct);
                    break;
            }
        }

        private static async Task TombstoneCascadeChildAsync(
            OperationState st, string entityType, ISyncMetadata child, Provenance tombstone, CancellationToken ct)
        {
            var before = EntitySnapshotMapper.ToSnapshot(child);
            var after = WithTombstone(before, tombstone);
            var childId = EntitySnapshotMapper.IdOf(child);

            // Evidence only when this peer has a baseline for the child AND the local row has moved
            // since — that is the "delete-vs-edit" case D9-T5 asks to record. Without a baseline there
            // is nothing to say the row was concurrently edited, and the row's content is preserved on
            // the tombstone anyway, so evidence would add noise rather than recoverability.
            var baseRow = await SyncBaseSnapshotStore.GetAsync(st.Db, st.PeerId, entityType, childId, ct);
            if (TryReadBaseline(baseRow, entityType, out var baseSnapshot) &&
                baseSnapshot is not null && !SnapshotFieldsEqual(baseSnapshot, before))
            {
                await StageAutoEvidenceAsync(st, new[] { TombstoneEvidence(entityType, childId, baseSnapshot, before, after) }, ct);
            }

            EntitySnapshotMapper.ApplyTo(child, after);

            if (st.Db.Entry(child).State == EntityState.Detached)
                throw new InvalidOperationException($"cascade child {entityType} {childId:D} is not tracked.");

            st.Db.MarkSyncApplied(child);
            st.Written.Add(child);
        }

        private static async Task UpsertBaselinesAsync(OperationState st, CancellationToken ct)
        {
            foreach (var entity in st.Written)
            {
                var entityType = EntitySnapshotMapper.EntityTypeOf(entity);
                var entityId = EntitySnapshotMapper.IdOf(entity);

                // Read Rev off the tracked instance AFTER save #1, so this is the POST-apply value.
                // Storing the pre-apply Rev would leave row.Rev > baseline.Rev and make
                // SyncChangeEnumerator re-emit the row to this peer on every run, forever.
                await SyncBaseSnapshotStore.UpsertAsync(
                    st.Db, st.PeerId, entityType, entityId, entity.Rev,
                    CanonicalJson.Write(EntitySnapshotMapper.ToSnapshot(entity)), st.NowUtc, ct);
            }
        }

        // ================================================================= parent inspection

        private enum ParentState { Live, Tombstoned, Missing, NotApplicable }

        private enum ParentRule
        {
            None,           // no parent reference (HocKy)
            Structural,     // D4 target: MonHoc.MaHocKy, StudyTask.MaMonHoc
            FkOnlyChild,    // FK, no reparent semantics: TaskNote.MaTask, TaskReferenceLink.MaTask
            NoFkChild,      // reference only, no FK: StudyLog.MaTask
        }

        private readonly record struct ParentInfo(ParentState State, ParentRule Rule, Provenance? Provenance);

        private static async Task<ParentInfo> InspectParentAsync(
            AppDbContext db, string entityType, EntitySnapshot snapshot, CancellationToken ct)
        {
            var (rule, field) = entityType switch
            {
                SyncEntityTypes.MonHoc => (ParentRule.Structural, "MaHocKy"),
                SyncEntityTypes.StudyTask => (ParentRule.Structural, "MaMonHoc"),
                SyncEntityTypes.TaskNote => (ParentRule.FkOnlyChild, "MaTask"),
                SyncEntityTypes.TaskReferenceLink => (ParentRule.FkOnlyChild, "MaTask"),
                SyncEntityTypes.StudyLog => (ParentRule.NoFkChild, "MaTask"),
                _ => (ParentRule.None, string.Empty),
            };

            if (rule == ParentRule.None) return new ParentInfo(ParentState.NotApplicable, rule, null);

            var parentId = snapshot.Fields[field] is GuidValue g
                ? g.Value
                : throw new MergeContractViolationException($"{entityType}.{field} is not a Guid value.");

            var parent = await LoadParentAsync(db, entityType, parentId, ct);

            if (parent is null) return new ParentInfo(ParentState.Missing, rule, null);

            var provenance = EntitySnapshotMapper.ToSnapshot(parent).Provenance;
            return new ParentInfo(parent.IsDeleted ? ParentState.Tombstoned : ParentState.Live, rule, provenance);
        }

        /// <summary>
        /// The parent row for a child's reference: a <c>HocKy</c> for MonHoc, a <c>MonHoc</c> for
        /// StudyTask, a <c>StudyTask</c> for TaskNote / TaskReferenceLink / StudyLog. Read-only and
        /// untracked — parent liveness is an input to the decision, never something this operation writes.
        /// </summary>
        private static async Task<ISyncMetadata?> LoadParentAsync(
            AppDbContext db, string childEntityType, Guid parentId, CancellationToken ct) => childEntityType switch
            {
                SyncEntityTypes.MonHoc => await db.HocKys.AsNoTracking().FirstOrDefaultAsync(h => h.MaHocKy == parentId, ct),
                SyncEntityTypes.StudyTask => await db.MonHocs.AsNoTracking().FirstOrDefaultAsync(m => m.MaMonHoc == parentId, ct),
                _ => await db.StudyTasks.AsNoTracking().FirstOrDefaultAsync(t => t.MaTask == parentId, ct),
            };

        // ================================================================= helpers

        private static bool TryReadBaseline(SyncBaseSnapshotRow? row, string entityType, out EntitySnapshot? snapshot)
        {
            snapshot = null;
            if (row is null) return true;                 // no baseline is a legitimate null Base (D6-B)
            if (row.SnapshotJson is null) return false;   // DoR §5.5 row 10: fail closed, never "treat as null Base"

            try
            {
                var parsed = CanonicalJson.Read(row.SnapshotJson);
                if (!StringComparer.Ordinal.Equals(parsed.EntityType, entityType)) return false;
                snapshot = parsed;
                return true;
            }
            catch (SnapshotContractException)
            {
                return false;
            }
            catch (System.Text.Json.JsonException)
            {
                // DoR §5.5 row 10 is "SnapshotJson is null OR fails any rule above", and a baseline row
                // that is not even well-formed JSON fails the first of them. PR-1's reader surfaces
                // that as JsonException from JsonDocument.Parse rather than as a contract exception;
                // classifying it here is the apply layer implementing row 10, not a reader change.
                return false;
            }
        }

        private static async Task<(Guid? Id, EntitySnapshot? Snapshot, bool Readable)> LoadTaskNoteBaselineInScopeAsync(
            OperationState st, Guid scopeValue, CancellationToken ct)
        {
            var rows = await SyncBaseSnapshotStore.GetAllForPeerAsync(st.Db, st.PeerId, SyncEntityTypes.TaskNote, ct);
            foreach (var row in rows.Values)
            {
                if (!TryReadBaseline(row, SyncEntityTypes.TaskNote, out var snapshot))
                    return (null, null, false);

                if (snapshot is not null && snapshot.Fields["MaTask"] is GuidValue g && g.Value == scopeValue)
                    return (row.EntityId, snapshot, true);
            }
            return (null, null, true);
        }

        private static EntitySnapshot WithTombstone(EntitySnapshot snapshot, Provenance tombstone) =>
            snapshot with
            {
                Provenance = new Provenance(tombstone.ModifiedAtUtc, tombstone.ModifiedByDeviceId, true,
                                            tombstone.DeletedAtUtc ?? tombstone.ModifiedAtUtc),
            };

        private static ConflictCandidate TombstoneEvidence(
            string entityType, Guid entityId, EntitySnapshot? baseSnapshot,
            EntitySnapshot local, EntitySnapshot tombstone) =>
            new(ConflictKind.TombstoneConflict, entityType, entityId, null, null, null,
                baseSnapshot, baseSnapshot is null ? null : entityId,
                local, entityId, tombstone, entityId,
                ResolutionKind.AutoTombstone, tombstone);

        private static bool SnapshotFieldsEqual(EntitySnapshot a, EntitySnapshot b)
        {
            if (a.Fields.Count != b.Fields.Count) return false;
            foreach (var kv in a.Fields)
            {
                if (!b.Fields.TryGetValue(kv.Key, out var other) || !kv.Value.Equals(other)) return false;
            }
            return true;
        }

        private static string? StructuralFieldOf(string entityType) => entityType switch
        {
            SyncEntityTypes.MonHoc => "MaHocKy",
            SyncEntityTypes.StudyTask => "MaMonHoc",
            _ => null,
        };

        /// <summary>
        /// The only scopes that can hold an <c>Unresolved</c> record are Structural and Constraint:
        /// Field and Tombstone evidence enters as Resolved (D9-T5), so no other entity type needs a
        /// lock lookup. The TaskNote constraint scope key is supplied by the scope operation instead.
        /// </summary>
        private static string? StructuralScopeKeyOf(string entityType, Guid entityId) =>
            StructuralFieldOf(entityType) is { } field
                ? ConflictKeys.ScopeKey(ConflictKind.StructuralConflict, entityType, entityId, field, null)
                : null;

        private static async Task<ISyncMetadata?> LoadNoTrackingAsync(
            AppDbContext db, string entityType, Guid id, CancellationToken ct) => entityType switch
            {
                SyncEntityTypes.HocKy => await db.HocKys.AsNoTracking().FirstOrDefaultAsync(h => h.MaHocKy == id, ct),
                SyncEntityTypes.MonHoc => await db.MonHocs.AsNoTracking().FirstOrDefaultAsync(m => m.MaMonHoc == id, ct),
                SyncEntityTypes.StudyTask => await db.StudyTasks.AsNoTracking().FirstOrDefaultAsync(t => t.MaTask == id, ct),
                SyncEntityTypes.StudyLog => await db.StudyLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct),
                SyncEntityTypes.TaskNote => await db.TaskNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct),
                SyncEntityTypes.TaskReferenceLink => await db.TaskReferenceLinks.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct),
                _ => throw new MergeContractViolationException($"Unknown entity type '{entityType}'."),
            };

        private static async Task<ISyncMetadata?> LoadTrackedAsync(
            AppDbContext db, string entityType, Guid id, CancellationToken ct) => entityType switch
            {
                SyncEntityTypes.HocKy => await db.HocKys.FirstOrDefaultAsync(h => h.MaHocKy == id, ct),
                SyncEntityTypes.MonHoc => await db.MonHocs.FirstOrDefaultAsync(m => m.MaMonHoc == id, ct),
                SyncEntityTypes.StudyTask => await db.StudyTasks.FirstOrDefaultAsync(t => t.MaTask == id, ct),
                SyncEntityTypes.StudyLog => await db.StudyLogs.FirstOrDefaultAsync(l => l.Id == id, ct),
                SyncEntityTypes.TaskNote => await db.TaskNotes.FirstOrDefaultAsync(n => n.Id == id, ct),
                SyncEntityTypes.TaskReferenceLink => await db.TaskReferenceLinks.FirstOrDefaultAsync(r => r.Id == id, ct),
                _ => throw new MergeContractViolationException($"Unknown entity type '{entityType}'."),
            };

        /// <summary>State for exactly one logical operation. Dies with the operation's context.</summary>
        private sealed class OperationState
        {
            public OperationState(AppDbContext db, string peerId, DateTime nowUtc, string deviceId)
            {
                Db = db;
                PeerId = peerId;
                NowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
                DeviceId = deviceId;
            }

            public AppDbContext Db { get; }
            public string PeerId { get; }
            public DateTime NowUtc { get; }
            public string DeviceId { get; }

            /// <summary>Every row written in this operation; each gets a POST-apply baseline row.</summary>
            public List<ISyncMetadata> Written { get; } = new();

            /// <summary>ConflictKeys already added to this unit of work (StageAsync's check is a DB query).</summary>
            public HashSet<string> StagedKeys { get; } = new(StringComparer.Ordinal);
        }
    }
}
