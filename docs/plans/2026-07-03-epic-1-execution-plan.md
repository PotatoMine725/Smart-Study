# Epic 1 — Sync-Ready Data Model: Execution Plan

> Scope: **Epic 1 only** (per [`2026-07-03-master-plan.md`](2026-07-03-master-plan.md)). Epics 2/3/4 are
> **not touched**. Architecture decisions D-A…D-J stay frozen; this plan is execution-only.

## Context

Epic 1 is the foundation milestone (D-B: *sync-ready data model first*) and the highest-blast-radius
epic — it touches every synced entity, the cascade rules, the repository layer, and the schema-upgrade
path for existing alpha-tester DBs. Its goal is to make every entity **mergeable** (D-I metadata +
tombstones + identity semantics) and to close the two persistence prerequisites (A6 awaited write, B3
single write path). No sync transport, no merge engine, no snapshot store — those are later epics.

Exploration verified the codebase against the plan's baseline assumptions and found four things that
**sharpen** the work (none expand scope):

1. **B3 is already consolidated.** 9 repositories, each with a single `SaveChangesAsync` via a
   `Func<AppDbContext>` factory (`Services/ServiceLocator.cs`). So T1.6 is **not** "consolidate repos" —
   it is **"establish the single stamping seam"** that the plan's phrase *"one write path to instrument"*
   actually points at. That seam does not exist yet.
2. **The spine is one interception point.** An override of `AppDbContext.SaveChangesAsync()` that walks
   `ChangeTracker.Entries()` collapses **T1.1 (stamp Rev/ModifiedAtUtc/ModifiedByDeviceId) + T1.2
   (Deleted→tombstone + G1 cascade) + T1.5 (DeviceId)** into a single place. All six entities write via
   `DbSet` Add/Update/Remove, so all route through it; `ExecuteSqlRaw` (seed / schema seams) correctly
   bypasses it.
3. **Partial metadata already exists.** `StudyLog` carries `CreatedAtUtc` / `DeviceId` / `IsDeleted` and
   is already soft-deleted by convention; `TaskNote` has `UpdatedAtUtc`; `TaskReferenceLink` has
   `CreatedAtUtc`. Reconcile these rather than duplicate them.
4. **T1.8's mechanism is evidence-decided.** No EF migrations exist. Two working precedents do:
   `App.xaml.cs:33` (`ALTER TABLE … ADD COLUMN` + `SqliteException` catch) and the **testable**
   `Data/TelemetrySchema.cs` (`CREATE TABLE IF NOT EXISTS` seam with `TelemetrySchemaDualPathTests`).
   The D-I/tombstone columns are pure `ADD COLUMN` — extend the testable seam, not more untestable inline
   ALTERs, and not EF migrations.

Intended outcome: every synced entity carries `Rev, ModifiedAtUtc, ModifiedByDeviceId, IsDeleted,
DeletedAtUtc`; no hard deletes remain; existing DBs upgrade in place losslessly; A6 closed. Build + tests
green; `data-model.md` §§3/8 and roadmap A.3 updated after code lands.

## Decisions locked (before code)

| # | Decision | Choice | Rationale |
|---|---|---|---|
| **G1** | Soft-delete cascade policy | **Cascade-tombstone** | Soft-deleting a parent tombstones all live descendants in the same transaction — preserves today's hard-cascade UX; lives in the SaveChanges override (EF's `ON DELETE CASCADE` no longer fires once deletes become `UPDATE IsDeleted=1`). |
| **T1.8** | Schema-upgrade mechanism | **Extend the testable schema seam** (new `SyncSchema.EnsureColumns`, sibling of `TelemetrySchema`) with idempotent `ALTER TABLE ADD COLUMN` | No migrations infra; two precedents; columns are pure additive; keeps upgrade path unit-testable (dual-path). Not EF migrations, not inline App.xaml.cs ALTERs. |
| **Fields** | Metadata shape | `ISyncMetadata { long Rev; DateTime ModifiedAtUtc; string ModifiedByDeviceId; bool IsDeleted; DateTime? DeletedAtUtc }` | One interface the seam stamps. Keep `CreatedAtUtc` **distinct** from `ModifiedAtUtc` (creation ≠ last-write). Reconcile `TaskNote.UpdatedAtUtc` → `ModifiedAtUtc`. Reuse `StudyLog.IsDeleted`. |
| **Device id** | Stamp source | Reuse **`Services/ML/DeviceHelper.GetId()`** (static, already the canonical provider per `lessons-learned.md:76`) | `AppDbContext` is parameterless-constructed, so the seam reads the static id (cached). No new device-identity concept. |
| **T1.3** | Identity-semantics scope | **Bounded to the observed `MonHoc` semantic-duplicate class** (`data-model.md:121`, commit `946799b` preview) | Same subject as multiple rows with different Guids, patched read-side (`.Distinct()`/`GroupBy`). No new "clone semester" feature. |

## The architectural spine (build this first, everything falls out of it)

**Single stamping seam** — override `SaveChanges()` / `SaveChangesAsync()` in `Data/AppDbContext.cs`:

```
foreach entry in ChangeTracker.Entries() where Entity is ISyncMetadata:
  if State == Added or Modified:
      Rev++ ; ModifiedAtUtc = clock.UtcNow ; ModifiedByDeviceId = DeviceHelper.GetId()
  if State == Deleted:                          // tombstone, not DELETE
      State = Modified ; IsDeleted = true ; DeletedAtUtc = clock.UtcNow ; Rev++
      // G1 cascade-tombstone: mark live descendants Deleted too (they re-enter this loop)
```

- Add a small ambient clock seam on the context (default `DateTime.UtcNow`; overridable in tests) so
  `ModifiedAtUtc` is deterministic under test — mirrors the existing `FakeClock` convention.
- `Rev` is a local monotonic per-entity counter — **never compared across devices** (L6).

## Execution — milestones (strictly sequential; same substrate, do not parallelize the core)

### M1.1 — Single write path + stamping seam
- **Baseline first (T-metric):** add a task-save timing test in
  `SmartStudyPlanner.Tests/Infrastructure/Persistence/RepositoriesTests.cs` (Stopwatch around N ×
  `AddAsync`/`UpdateAsync`) and record p95 **before** stamping lands. This is the success-metric baseline.
- **T1.6 (reframed) — stamping seam:** new `Models/ISyncMetadata.cs`; override in `Data/AppDbContext.cs`.
  Confirm all six entities' writes route through `DbSet` Add/Update/Remove (they do). `gitnexus_impact`
  on `AppDbContext.SaveChangesAsync` before editing.
- **T1.5 — close A6:** await the two fire-and-forget writes at `ViewModels/FocusViewModel.cs:138-145`
  (drop the `_ =`); make `LuuThoiGianThucTe` async and propagate through callers `HoanThanh()` (line 263)
  and `ThoatKhanCap()` (line 274); surface failures instead of swallowing. `DeviceId`/`ModifiedByDeviceId`
  now stamped automatically by the seam.
- **Re-measure:** p95 task-save ≤ **1.2×** baseline (success metric).

### M1.2 — Schema upgrade + D-I metadata + tombstones  [G1 closes here]
- **T1.1 — entity fields:** implement `ISyncMetadata` on all six (`HocKy`, `MonHoc`, `StudyTask`,
  `StudyLog`, `TaskNote`, `TaskReferenceLink`). Reconcile existing fields (see Decisions). `gitnexus_impact`
  per entity; surface HIGH/CRITICAL first.
- **T1.8 — upgrade seam:** new `Data/SyncSchema.EnsureColumns(db)` doing idempotent
  `ALTER TABLE <t> ADD COLUMN …` for the missing columns per entity (StudyLog only needs
  `Rev, ModifiedAtUtc, ModifiedByDeviceId, DeletedAtUtc`). Wire into `App.xaml.cs` after `EnsureCreated()`,
  replacing the untestable inline IsSeeded ALTER pattern's approach for the new columns.
  - **Backup-before-upgrade:** copy `SmartStudyData.db` → timestamped backup before applying (risk mitigation).
  - **Migration report:** small utility capturing pre/post row counts + per-table checksums (the evidence
    for the "lossless upgrade" acceptance criterion).
- **T1.2 — tombstones + G1 cascade:** the seam (M1.1) already flips Deleted→tombstone and cascade-tombstones
  descendants. Update read paths: extend the `!IsDeleted` filter (already in `SqliteStudyLogRepository`) to
  every repo's read queries. Relax/annotate the now-inert `OnDelete(Cascade)` config in
  `AppDbContext.cs:43-70` (deletes no longer reach the DB as DELETEs).
- **G1 decision note:** `docs/plans/2026-07-03-g1-soft-delete-cascade.md` (cascade-tombstone).

### M1.3 — Identity semantics (bounded)
- **T1.3:** define `MonHoc` natural-key identity = `(MaHocKy, normalized TenMonHoc)`; add equality / a shared
  dedup helper; centralize the scattered read-side dedup (`ViewModels/AnalyticsViewModel.cs` SubjectOptions
  `.Distinct()`, `Services/Pipeline/Stages/AdaptStage.cs` GroupBy, `Services/Analytics/StudyAnalyticsService.cs`
  GroupBy) behind it; prevent-at-source in `ViewModels/QuanLyMonHocViewModel.cs:100`. **No** hard DB unique
  constraint in the alpha (would break existing duplicate data) — consolidate, don't reject.

### Docs (DoD, after code lands)
- `docs/architecture/data-model.md` §3 (deletes → soft-delete) and §8 (items 1 & 2 done; item 3 metadata
  columns done, merge still Epic 2). Roadmap `system_roadmap.md` A.3 status.

## Test strategy (per convention: mirror prod ns; `Fixtures/`, `TestDoubles/`; xUnit)
- **Stamping seam** (unit, new `Tests/Data/SyncMetadataStampingTests.cs`): Add→Rev=1 + stamps set;
  Modified→Rev increments; Delete→`IsDeleted=true` + `DeletedAtUtc` set + descendants tombstoned (G1).
- **Upgrade** (new `Tests/Data/SyncSchemaDualPathTests.cs`, mirroring `TelemetrySchemaDualPathTests`):
  build an *old-shape* DB via raw `CREATE TABLE` (columns absent) → run `SyncSchema.EnsureColumns` →
  assert columns added, idempotent on re-run, round-trips through real repos. One fixture per alpha-tester
  DB shape; row-count + checksum assertions = the migration report.
- **A6** (ViewModels): failure in the awaited write surfaces (not swallowed).
- **Characterization:** the M1.1 timing test pins p95 pre/post.
- No property-testing library is present (no FsCheck) — Epic 1 uses fixtures/characterization; the property
  suites belong to Epics 2/3 and are out of scope here.

## Execution model — agent orchestration + superpowers

The alpha critical path (M1.1→M1.2→M1.3) is **sequential** — each step edits the same substrate, so I drive
the core seam/schema/tombstone work single-threaded and **confirm each milestone before the next**
(split-by-concern / confirm-each-step). Independent, parallel-safe pieces are delegated to subagents while the
core proceeds:

| Track | Delegated work | When |
|---|---|---|
| P1 | Draft the **G1 decision note** | During M1.1 |
| Infra | **Migration-report utility** + **task-save timing harness** (isolated test infra) | During M1.1 |
| P5 | **Docs sync** (`data-model.md` §§3/8, roadmap A.3) drafts | After each milestone lands |

**Superpowers used in the execution stage:**
- `superpowers:test-driven-development` — the stamping seam, tombstone/cascade logic, and upgrade seam are
  written test-first (failing test → implement → green).
- `superpowers:using-git-worktrees` — isolate Epic 1 on its own worktree/branch (high-blast-radius).
- `superpowers:subagent-driven-development` / `dispatching-parallel-agents` — the P1/infra/P5 tracks above.
- `superpowers:systematic-debugging` — if the seam or in-place upgrade misbehaves.
- `superpowers:verification-before-completion` + `requesting-code-review` — before claiming DoD / before merge.

**Commit sequence (split by concern):** (1) baseline timing harness → (2) `ISyncMetadata` + stamping seam +
A6 → (3) entity fields + `SyncSchema.EnsureColumns` + backup + migration report → (4) tombstones + G1 cascade
+ read-filters → (5) `MonHoc` identity → (6) docs. `gitnexus_detect_changes` before each commit.

## Verification (end-to-end)
1. `dotnet build SmartStudyPlanner.slnx` — clean.
2. `dotnet test SmartStudyPlanner.slnx --no-build` — green (incl. new stamping, upgrade, A6, timing tests).
3. **In-place upgrade:** point the app at a copied *pre-upgrade* `SmartStudyData.db`; confirm it upgrades,
   the migration report shows matching row counts + checksums, and the app runs.
4. **Metrics:** p95 task-save ≤ 1.2× baseline; 0 fire-and-forget writes remain (review sweep);
   100% of new `StudyLog` rows carry a device id.

## Definition of Done (Epic 1)
- `gitnexus_impact` before editing any symbol; HIGH/CRITICAL surfaced first. `gitnexus_detect_changes`
  before every commit.
- Build + tests green; acceptance-criteria tests present.
- Every synced entity carries the five metadata fields; no hard delete remains; A6 closed; existing DB
  upgrades losslessly (migration report is the evidence).
- G1 note merged; `data-model.md` §§3/8 + roadmap A.3 updated after code lands; success metrics reported
  in the closing note.

## Explicitly out of scope (other epics — untouched)
Sync transport/merge, base-snapshot store (T1.4→M2.1), conflict records, SOE tables, retention/purge
(G4/T2.6), the M3.0 corpus/baselines, and gates **G2/G3/G4** (front-loaded in the master plan but belonging
to Epics 2/3). Only **G1** is closed here.
