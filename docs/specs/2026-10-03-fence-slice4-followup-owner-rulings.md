# T2.4 Fence Slice 4 follow-ups — Owner Rulings: E-2, D-5 (Q-2), OD-5, Q-1 (recorded)

| | |
|---|---|
| **Date** | 2026-10-03 |
| **Status** | E-2: **CLOSED — (c)** · D-5: **CLOSED — V2 (membership-only restore)** · OD-5: **CLOSED — (a) guard test** · Q-1: **CLOSED — recorded here for findability** |
| **Closes** | E-2 (rulings 2026-09-14 §7; measured in Slice 4, PR #106); Q-2 (Slice 4 report §7.5), filed as defect **D-5**; OD-5 (fence plan §20); Q-1 (review PR #107, M-1) |
| **Amends** | OD-7 (2026-09-14 §2.4) — *scope extension only*: D-5 adds a narrower restoration for non-fence save failures. OD-7's fence-rejection behaviour is unchanged |
| **Does not amend** | D1–D9 frozen record; SB-3, OD-4, OD-2; M-3/A-1 live-only predicate; D8-H fingerprint definition; `ConflictResolver`; any `Sync/**` semantics |
| **Leaves open** | SB-2/OD-1 (Slice 6), OD-6, OD-8, G4; long-term direction for persisted time-dependent Derived values (see E-2 §note) |
| **Implemented by** | E-2: ticket `2026-10-03-e2-derived-no-stamp` (PR <PR>) · D-5: ticket `2026-10-03-d5-failed-save-restore` (PR <PR>) · OD-5: fence Slice 5 (PR <PR>) · Q-1: PR #106 |

## 1. E-2 — CLOSED: (c) Derived-only changes do not stamp

**Ruling.** When a local save modifies a synced entity and **every** modified property is classified **Derived**
(`MergeSurfaceRegistry`; today `DiemUuTien`, `MucDoCanhBao`, `IsSeeded`), `SyncStamper` persists the new values but
does **not** increment `Rev` and does **not** change `ModifiedAtUtc` / `ModifiedByDeviceId`. Any non-Derived property
in the same entry ⇒ normal stamping. Unclassified properties ⇒ normal stamping (fail toward today's behaviour).

**Why.** Measured in Slice 4: a Derived-only save on a row held at Base re-stamps provenance, drifting the D8-H
fingerprint and making the conflict record permanently unresolvable, with no intent for the fence to route.
`QuanLyTaskViewModel` recomputes `DiemUuTien` (deadline- and time-dependent) on open, so ordinary use triggers it.
Derived fields are already excluded from sync snapshots (`EntitySnapshotMapper`), so stamping them only produces
fingerprint drift and, once sync exists, needless change traffic.

**Rejected.** (a) accept drift — breaks SB-3's purpose. (b) writer skips Derived on held rows — puts conflict
knowledge into the persistence writer.

**Note (not a ruling).** Option (d) — stop persisting time-dependent Derived values and compute them on read — is the
root-cause direction. Recorded as a candidate, not scheduled.

## 2. D-5 (Q-2) — CLOSED: V2, membership-only restore on non-fence failures

**Defect.** A local semester save that fails for any reason other than a fence rejection leaves **unsaved additions**
in the caller's graph. `ThemTask`'s next click writes two rows; `ThemMon`'s unsaved MonHoc is written by a later
save. Pre-existing on `origin/dev` (probe in PR #106).

**Ruling.** On a failed local semester save **not** caused by `MutationRejectedException`: after rollback, the caller's
graph has every **unsaved addition removed** (a `MonHoc`/`StudyTask` whose id has no persisted live row), using a
fresh read of persisted state. **Scalar edits and pending deletions on persisted rows are kept** in memory, as today.
The original exception is rethrown; a failure of the repair itself travels with it (as in Slice 4) and never replaces it.

**Why V2 and not a full restore (V1).** V1 ("RAM = DB after any failure") would also discard pending edits on
transient errors (e.g. `SQLITE_BUSY` after the 30 s timeout). `MoFocusMode` adds a focus session's study time to
the in-memory task and then saves; V1 would erase that time on a transient failure. V2 removes exactly the state that
causes duplicates and nothing else. The fence-rejection path keeps OD-7's full restore (a rejected request must not
survive in memory; a transient failure is not a rejection).

**Edges recorded, not ruled.** A failed create of a brand-new semester (`hocKyCu == null`): the root `HocKy` is held by
`SetupViewModel` and cannot be "removed"; the implementer reports the behaviour. Create-mode form state that the
command clears before awaiting the save is covered by Q-1 (§4).

## 3. OD-5 — CLOSED: (a) guard test

`IStudyTaskRepository.AddAsync` / `UpdateAsync` / `DeleteAsync` have zero production callers (re-verified 2026-10-03:
production uses only `GetAllAsync`, in `OutcomeMaturationService` and `WeightOptimizerViewModel`) and bypass the fence.
**Ruling:** a source-scan guard test fails if production code calls them. No routing through the fence, no removal now.
Removal stays a tech-debt candidate. The re-stamp of a dead task by `DeleteAsync` (D-2 residual) is accepted while it
has no caller.

## 4. Q-1 — CLOSED (recorded here; ruled 2026-10-03 during the Slice 4 review)

OD-7's "restore the in-memory graph" also covers **VM state that the save command itself changes before awaiting the
save** (edit mode, form reset). Narrow: only that state. Implemented in PR #106 (`ThemTask`, `ThemMon`).

## 5. Consistency check

| Check | Result |
|---|---|
| Any frozen D1–D9 text changed? | No |
| OD-7 fence-rejection behaviour changed? | No — D-5 applies only to non-fence failures |
| Any `Sync/**` semantic changed? | No. E-2 changes `SyncStamper` (Data layer) only for Derived-only entries; the marked sync-apply path is untouched |
| Conflict with M-3/A-1 or D-2? | No |
| Anything authorised beyond the three tickets? | No |
