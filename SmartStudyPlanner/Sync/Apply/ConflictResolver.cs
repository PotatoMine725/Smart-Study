using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartStudyPlanner.Data;
using SmartStudyPlanner.Models;
using SmartStudyPlanner.Sync.Merge;

namespace SmartStudyPlanner.Sync.Apply
{
    /// <summary>
    /// Epic 2 / T2.4 (PR-6, DoR §13; owner rulings B-1..B-4 and the E-3 acknowledgement,
    /// <c>docs/specs/T2.4-PR6-ConflictResolver-Rulings-2026-09-11.md</c>) — human resolution of an
    /// <c>Unresolved</c> <see cref="SyncConflictRecordRow"/>.
    /// <para>
    /// <b>Context lifetime (B-1).</b> Each <see cref="ResolveAsync"/> call owns exactly one
    /// <see cref="AppDbContext"/>, built from the composition-root factory and disposed on every
    /// exit path. A failed call never reuses its context; retry is a new <see cref="ResolveAsync"/>
    /// call, which builds a fresh context and re-reads the record. This is a PR-6-specific rule — it
    /// does not widen PR-5's PR-5-only context-lifetime amendment.
    /// </para>
    /// <para>
    /// <b>R1 / ACK A2-c.</b> The resolver never calls <see cref="AppDbContext.MarkSyncApplied"/>: DoR
    /// §13.4 (frozen) says the result is written as an ORDINARY local write, stamped by
    /// <see cref="SyncStamper"/> — a human decision made here, now, that should win LWW against a
    /// stale concurrent edit. The candidate's original authorship stays in the immutable evidence
    /// columns, untouched (enforced by <c>trg_SyncConflictRecords_EvidenceImmutable</c>).
    /// </para>
    /// <para>
    /// <b>Not in this PR.</b> No baseline write (DoR §13.5), no cascade, no force/rebase/reopen, no
    /// UI, no second physical-delete site. See the owner rulings file and
    /// <c>docs/review/2026-09-11-t2.4-pr6-conflict-resolver-dor.md</c> §14 for the full scope fence.
    /// </para>
    /// </summary>
    public sealed class ConflictResolver
    {
        private readonly Func<AppDbContext> _contextFactory;

        /// <param name="contextFactory">
        /// Must return a context configured exactly like the composition root's, so
        /// <c>DeviceIdProvider</c> is the persisted device identity. Called once per
        /// <see cref="ResolveAsync"/> call; the result is disposed before the call returns.
        /// </param>
        public ConflictResolver(Func<AppDbContext> contextFactory)
            => _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

        public async Task<ResolutionOutcome> ResolveAsync(Guid conflictId, ResolutionRequest request, CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var db = _contextFactory();
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var outcome = await ResolveInTransactionAsync(db, conflictId, request, ct);

                    // NoOpReplay and every Rejected reason wrote nothing by contract; rolling back
                    // makes that a guarantee rather than a claim about every early-return path above.
                    if (outcome.Kind == ResolutionOutcomeKind.Applied)
                        await tx.CommitAsync(ct);
                    else
                        await tx.RollbackAsync(ct);

                    return outcome;
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            }
            catch (MergeContractViolationException)
            {
                // Fail closed: the request or the record shape is something the model says cannot
                // exist. Never a crash, never silently accepted.
                return ResolutionOutcome.Rejected(ResolutionRejectReason.ContractViolation);
            }
            catch (Exception ex)
            {
                return ResolutionOutcome.Failed(ex);
            }
            finally
            {
                // B-1: context lifetime ends with the call, on every path.
                await db.DisposeAsync();
            }
        }

        // ================================================================= classification (DoR §7.2)

        private static async Task<ResolutionOutcome> ResolveInTransactionAsync(
            AppDbContext db, Guid conflictId, ResolutionRequest request, CancellationToken ct)
        {
            // ---- step 0: request shape ----------------------------------------------------------
            if (request.Kind is not (ResolutionKind.KeepLocal or ResolutionKind.KeepRemote
                                   or ResolutionKind.KeepBase or ResolutionKind.ManualMerge))
                return ResolutionOutcome.Rejected(ResolutionRejectReason.InvalidRequest);

            if (request.Kind == ResolutionKind.ManualMerge)
            {
                if (request.ManualResult is null || request.ManualResultEntityId is null)
                    return ResolutionOutcome.Rejected(ResolutionRejectReason.InvalidRequest);
            }
            else if (request.ManualResult is not null || request.ManualResultEntityId is not null)
            {
                return ResolutionOutcome.Rejected(ResolutionRejectReason.InvalidRequest);
            }

            // ---- step 1: ConflictId is the addressing identity (D7-A) ----------------------------
            var rec = await SyncConflictRecordStore.GetAsync(db, conflictId, ct);
            if (rec is null) return ResolutionOutcome.Rejected(ResolutionRejectReason.UnknownConflict);

            // ---- step 2: auto-resolution can never be re-resolved (D9-T5) -----------------------
            if (rec.ResolutionKind is ResolutionKind.AutoLww or ResolutionKind.AutoTombstone)
                return ResolutionOutcome.Rejected(ResolutionRejectReason.NotResolvable);

            // ---- step 3: resolved -> replay classification, before drift (D7-C/D7-D) -------------
            if (rec.Status == ConflictRecordStatus.Resolved)
            {
                if (!TryDeriveResult(rec, request, out var replay))
                    return ResolutionOutcome.Rejected(ResolutionRejectReason.InvalidRequest);

                var isSameResolution = rec.ResolutionKind == request.Kind
                                     && rec.ResultEntityId == replay.EntityId
                                     && StringComparer.Ordinal.Equals(Norm(rec.ResultFingerprint), Norm(replay.Fingerprint));

                return isSameResolution
                    ? ResolutionOutcome.NoOpReplay()
                    : ResolutionOutcome.Rejected(ResolutionRejectReason.MismatchedReplay);
            }

            // ---- step 4: only Structural/Constraint can ever be Unresolved (defensive) ------------
            if (rec.Kind is not (ConflictKind.StructuralConflict or ConflictKind.ConstraintConflict))
                return ResolutionOutcome.Rejected(ResolutionRejectReason.ContractViolation);

            // ---- step 5: kind applicability -- AL-PT has no local candidate (B-4) -----------------
            if (request.Kind == ResolutionKind.KeepLocal && rec.LocalEntityId is null)
                return ResolutionOutcome.Rejected(ResolutionRejectReason.KindNotApplicable);

            // ---- step 6: live-state drift gate (D8-H) --------------------------------------------
            var (liveId, live) = await LoadLiveScopeAsync(db, rec, ct);
            if (!SyncBaseFingerprint.Matches(rec.BaseFingerprint, live))
                return ResolutionOutcome.Rejected(ResolutionRejectReason.LiveStateDrift);

            // ---- step 7: derive + validate the selected result -----------------------------------
            if (!TryDeriveResult(rec, request, out var derived))
                return ResolutionOutcome.Rejected(ResolutionRejectReason.InvalidRequest);

            if (request.Kind == ResolutionKind.ManualMerge)
            {
                var manualReject = await ValidateManualMergeAsync(db, rec, derived, ct);
                if (manualReject is not null) return ResolutionOutcome.Rejected(manualReject.Value);
            }

            // ---- step 8: result checks (parent / occupancy / scope-lock) -------------------------
            var liveFingerprint = SyncBaseFingerprint.Of(live);
            var writesNothing = derived.EntityId == liveId
                              && StringComparer.Ordinal.Equals(Norm(derived.Fingerprint), liveFingerprint);

            // B-2: reject if the result would have a tombstoned/missing parent. The ruling's exempt
            // subcase (i) names KeepBase specifically -- it is NOT "any kind whose write happens to be
            // empty". Those are not equivalent: a record whose Local was never locally edited (the
            // test-M shape -- see SyncApplyParentHandlingTests.M_UpdateUnderTombstonedStructuralParent)
            // has Local byte-identical to Base, so a content-only exemption would also excuse KeepLocal
            // from the check the owner's ruling says must reject it there. Keying literally on Kind ==
            // KeepBase keeps every other kind checked regardless of whether its write happens to be a
            // no-op.
            if (!(request.Kind == ResolutionKind.KeepBase && writesNothing))
            {
                var parentReject = await CheckResultParentAsync(db, rec.EntityType, derived.Snapshot!, ct);
                if (parentReject is not null) return ResolutionOutcome.Rejected(parentReject.Value);
            }

            // E-8: a Constraint result targeting a different Id than whatever currently occupies
            // the scope (live or tombstoned -- TaskNotes(MaTask) is an unfiltered UNIQUE index)
            // cannot be written without vacating that occupant first, and PR-6 has no withdrawal
            // mechanism of its own (M5 is staging-time only).
            if (rec.Kind == ConflictKind.ConstraintConflict && liveId is not null && liveId != derived.EntityId)
                return ResolutionOutcome.Rejected(ResolutionRejectReason.ScopeOccupied);

            // D9-T6, this record excluded -- structurally redundant given the partial unique index
            // (this record IS the current Unresolved occupant of its own ScopeKey), kept because
            // DoR §13.2 states it as an explicit step.
            var scopeLock = await SyncConflictRecordStore.FindUnresolvedByScopeKeyAsync(db, rec.ScopeKey, ct);
            if (scopeLock is not null && scopeLock.ConflictId != rec.ConflictId)
                return ResolutionOutcome.Rejected(ResolutionRejectReason.ScopeHasUnresolvedConflict);

            // ---- step 9: write (unmarked) + resolve + one save -----------------------------------
            if (!writesNothing)
                await WriteResultAsync(db, rec, derived, ct);

            await SyncConflictRecordStore.MarkResolvedAsync(
                db, rec.ConflictId, request.Kind, derived.EntityId, derived.SnapshotJson, derived.Fingerprint,
                db.Clock(), db.DeviceIdProvider(), ct);

            await db.SaveChangesAsync(ct);
            return ResolutionOutcome.Applied();
        }

        // ================================================================= result derivation

        /// <summary>
        /// The selected/requested result, pre-stamp (E-1): KeepLocal/KeepRemote/KeepBase read the
        /// record's own already-canonical evidence columns directly (no recomputation), so a replay
        /// can always recompute the same identity even though the live row's provenance will differ
        /// once it is locally stamped (A2-c). <see cref="SnapshotJson"/>/<see cref="Fingerprint"/>
        /// are kept nullable (never normalised) so they can be written straight into
        /// <c>ResultSnapshotJson</c>/<c>ResultFingerprint</c> with the same "null ⇔ no result"
        /// convention <see cref="SyncConflictRecordRow.BaseFingerprint"/> already uses (D7-G).
        /// </summary>
        private readonly record struct DerivedResult(Guid? EntityId, EntitySnapshot? Snapshot, string? SnapshotJson, string? Fingerprint);

        private static bool TryDeriveResult(SyncConflictRecordRow rec, ResolutionRequest request, out DerivedResult derived)
        {
            try
            {
                derived = request.Kind switch
                {
                    ResolutionKind.KeepLocal => FromStored(rec.LocalEntityId, rec.LocalSnapshotJson, rec.LocalFingerprint),
                    ResolutionKind.KeepRemote => FromStored(rec.RemoteEntityId, rec.RemoteSnapshotJson, rec.RemoteFingerprint),
                    ResolutionKind.KeepBase => FromStored(rec.BaseEntityId, rec.BaseSnapshotJson, rec.BaseFingerprint),
                    ResolutionKind.ManualMerge => new DerivedResult(
                        request.ManualResultEntityId, request.ManualResult,
                        CanonicalJson.Write(request.ManualResult!), CanonicalJson.Fingerprint(request.ManualResult)),
                    _ => default,
                };
                return true;
            }
            catch (SnapshotContractException)
            {
                derived = default;
                return false;
            }
            catch (MergeContractViolationException)
            {
                derived = default;
                return false;
            }
        }

        private static DerivedResult FromStored(Guid? entityId, string? json, string? fingerprint) =>
            new(entityId, json is null ? null : CanonicalJson.Read(json), json, fingerprint);

        /// <summary>Normalises a nullable stored fingerprint to PR-1's "null" sentinel for comparison only.</summary>
        private static string Norm(string? fingerprint) => fingerprint ?? CanonicalJson.Fingerprint(null);

        // ================================================================= ManualMerge validation

        private static async Task<ResolutionRejectReason?> ValidateManualMergeAsync(
            AppDbContext db, SyncConflictRecordRow rec, DerivedResult derived, CancellationToken ct)
        {
            var snapshot = derived.Snapshot!;

            if (!StringComparer.Ordinal.Equals(snapshot.EntityType, rec.EntityType))
                return ResolutionRejectReason.ResultOutOfScope;

            if (snapshot.Provenance.IsDeleted)
                return ResolutionRejectReason.ManualTombstoneNotSupported;   // B-3, every entity type, no exception

            if (rec.Kind == ConflictKind.StructuralConflict)
            {
                // E-3: entity-scoped Structural records collapse Local/Remote/Base to one Id
                // (the same entity being reparented on both sides) -- a fresh Guid would leave the
                // scope's own row untouched at Base and create an unrelated second entity.
                return derived.EntityId == rec.EntityId ? null : ResolutionRejectReason.ResultOutOfScope;
            }

            // ConstraintConflict (TaskNote scope).
            var maTask = ((GuidValue)snapshot.Fields["MaTask"]).Value;
            if (!StringComparer.Ordinal.Equals(maTask.ToString("D"), rec.ConstraintValue))
                return ResolutionRejectReason.ResultOutOfScope;

            var isKnownId = derived.EntityId == rec.LocalEntityId
                          || derived.EntityId == rec.RemoteEntityId
                          || derived.EntityId == rec.BaseEntityId;
            if (!isKnownId)
            {
                // A fresh Guid is legitimate only for a scope that genuinely creates a new entity
                // (DoR §13.3) -- checked against the target table, not merely against the three
                // evidence Ids, so a genuine PK collision still fails closed.
                var existing = await LoadNoTrackingAsync(db, rec.EntityType, derived.EntityId!.Value, ct);
                if (existing is not null) return ResolutionRejectReason.ResultIdInUse;
            }

            return null;
        }

        // ================================================================= parent check (B-2, §11)

        private static async Task<ResolutionRejectReason?> CheckResultParentAsync(
            AppDbContext db, string entityType, EntitySnapshot result, CancellationToken ct)
        {
            var (field, parentType) = entityType switch
            {
                SyncEntityTypes.MonHoc => ("MaHocKy", SyncEntityTypes.HocKy),
                SyncEntityTypes.StudyTask => ("MaMonHoc", SyncEntityTypes.MonHoc),
                SyncEntityTypes.TaskNote => ("MaTask", SyncEntityTypes.StudyTask),
                _ => ((string?)null, (string?)null),
            };
            if (field is null) return null;

            var parentId = ((GuidValue)result.Fields[field]).Value;
            var parent = await LoadNoTrackingAsync(db, parentType!, parentId, ct);
            if (parent is null) return ResolutionRejectReason.ResultParentMissing;
            return parent.IsDeleted ? ResolutionRejectReason.ResultParentTombstoned : null;
        }

        // ================================================================= write (R1, no marking)

        private static async Task WriteResultAsync(
            AppDbContext db, SyncConflictRecordRow rec, DerivedResult derived, CancellationToken ct)
        {
            var entityId = derived.EntityId!.Value;
            var tracked = await LoadTrackedAsync(db, rec.EntityType, entityId, ct);

            if (tracked is null)
            {
                tracked = EntitySnapshotMapper.CreateEntity(rec.EntityType, entityId, derived.Snapshot!);

                // E-2: re-inserting the withdrawn local identity (KeepLocal, or a ManualMerge that
                // chooses it) restores Rev continuity from what M5 recorded before the hard delete,
                // so the ordinary stamper's Added-path Rev++ (SyncStamper.cs) yields LocalRowRev+1
                // instead of resetting to 1 and sitting at/below a peer's existing baseline for it.
                if (rec.LocalRowRev is { } seed && entityId == rec.LocalEntityId)
                    tracked.Rev = seed;

                db.Add(tracked);
            }
            else
            {
                EntitySnapshotMapper.ApplyTo(tracked, derived.Snapshot!);
            }

            // No MarkSyncApplied: the ordinary ISyncMetadata stamping path applies (A2-c). Never
            // this method's job to guard against a detached instance -- LoadTrackedAsync always
            // resolves a tracked row or none, and CreateEntity's result is Add()ed immediately.
        }

        // ================================================================= scope / drift loading

        private static async Task<(Guid? Id, EntitySnapshot? Snapshot)> LoadLiveScopeAsync(
            AppDbContext db, SyncConflictRecordRow rec, CancellationToken ct)
        {
            if (rec.Kind == ConflictKind.StructuralConflict)
            {
                var id = rec.EntityId!.Value;
                var entity = await LoadNoTrackingAsync(db, rec.EntityType, id, ct);
                return entity is null ? ((Guid?)null, null) : (id, EntitySnapshotMapper.ToSnapshot(entity));
            }

            // ConstraintConflict: scoped by the MaTask value, not by any specific entity Id -- the
            // occupant of the scope may not share the incoming candidates' Ids at all (S3).
            var scopeValue = Guid.Parse(rec.ConstraintValue!);
            var note = await db.TaskNotes.AsNoTracking().FirstOrDefaultAsync(n => n.MaTask == scopeValue, ct);
            return note is null ? ((Guid?)null, null) : (note.Id, EntitySnapshotMapper.ToSnapshot(note));
        }

        // ================================================================= entity load helpers
        // Duplicated from SyncApplySession (E-7, DoR §15.2: "owner may prefer duplication to keep
        // SyncApplySession.cs untouched") rather than extracted -- SyncApplySession's own loaders
        // are private static and this keeps PR-6 additive over PR-5.

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
    }
}
