# Data Model & Pipeline

> Consolidated 2026-05-21 from `2026-05-07-db-scheme-data-pipeline.md`. Re-verified against source **2026-07-10** (branch `ui_rf`) — reflects schema after M6.1 + M7 + Slice 4 + M8 telemetry + **Epic 1 M1.1, M1.2, and M1.3** (M1.2 merged 2026-07-10, M1.2-R1 closed — [../review/2026-07-10-epic1-m1.2-r1-remediation-review.md](../review/2026-07-10-epic1-m1.2-r1-remediation-review.md); M1.3 §8 content authored same day, M1.3 merged 2026-07-11 — `a3a0a3d`). Per D-C, code is normative.

## 1. Database

- Engine: **SQLite**, single file `SmartStudyData.db` next to the binary.
- ORM: **EF Core** via `AppDbContext`.
- Bootstrap: `db.Database.EnsureCreated()` (`App.xaml.cs:28`) — **no EF migrations.** `EnsureCreated` is a no-op on an existing DB, so shipped DBs are patched at startup by idempotent seams:
  - `ALTER TABLE HocKys ADD COLUMN IsSeeded` wrapped in try/catch (`App.xaml.cs:31-39`);
  - `TelemetrySchema.EnsureTables(db)` — `CREATE TABLE IF NOT EXISTS` for the 3 telemetry tables (`Data/TelemetrySchema.cs`);
  - *(in-flight, M1.2)* `SyncSchema.EnsureColumns` generalizes this into a versioned upgrade seam with backup-before-upgrade + migration report.
- Dev reset: set `DEV_RESET_DB=1` to wipe and recreate. A dev-seed semester is marked `IsSeeded=1` at startup and **excluded from all reads** (`SqliteHocKyRepository.cs:27`).
- Connection: `AppDbContext.OnConfiguring` uses `Data Source=<BaseDirectory>\SmartStudyData.db` if no `DbContextOptions` was passed (constructor overload retained for testability).
- **Single stamping seam (M1.1):** `AppDbContext` overrides `SaveChanges(bool)` / `SaveChangesAsync(bool, ct)` — the two overloads every repository write routes through — to call `SyncStamper.Apply(ChangeTracker, Clock, DeviceHelper.GetId())` before the base save (`Data/AppDbContext.cs:93-103`). `Clock` is a settable `Func<DateTime>` (default `DateTime.UtcNow`) for deterministic tests. At HEAD this is a **behavioral no-op** because no production entity implements `ISyncMetadata` yet (that lands with M1.2's T1.1).

## 2. Entities

### Academic structure

| Entity | Purpose | Key relationships / notes |
|---|---|---|
| `HocKy` | Semester container | 1→N `MonHoc`. `NgayKetThuc` + `IsNgayKetThucAuto` are `[NotMapped]` (default `NgayBatDau + 150 days`). `IsSeeded` (mapped) marks the dev-seed row, filtered out of every read |
| `MonHoc` | Subject / course | belongs to `HocKy`; 1→N `StudyTask`; has `SoTinChi` (credits) |
| `StudyTask` | The atomic unit of work | belongs to `MonHoc`; `TenTask`, `HanChot`, `LoaiTask` (`LoaiCongViec`), `DoKho` (1–5), `TrangThai` (**string** constants `StudyTaskStatus.ChuaLam`/`HoanThanh`), `ThoiGianDaHoc`, `DiemUuTien`, `MucDoCanhBao`, `NgayHoanThanh` |
| `StudyLog` | One study session | 1 per task per session; `CreatedAtUtc`/`DeviceId`/`IsDeleted` (M7), plus the full D-I metadata block (`Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId`/`DeletedAtUtc`) since **Epic 1/M1.2**. `DeviceId` is populated at the write site (`ViewModels/FocusViewModel.cs`) and the write is awaited, not fire-and-forget, since **M1.1** — closing [lessons-learned L2](lessons-learned.md) / finding A6 |
| `TaskNote` | Freeform note | **1-1** with `StudyTask`, unique index on `MaTask`; cascade delete; `ModifiedAtUtc` is seam-owned (reconciled from the former `UpdatedAtUtc` in **M1.2**) |
| `TaskReferenceLink` | External link | **1-N** with `StudyTask`; cascade delete; `Title`, `Url`, `Category`, `SortOrder` |

### Sync metadata contract (M1.1)

`Models/ISyncMetadata.cs` — `Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`. Declared and enforced by `SyncStamper` (`Rev++` + stamps on every `Added`/`Modified` entry), but **no production entity implements it at HEAD** — M1.2 (in-flight) implements it on all six entities.

### Telemetry / ground-truth entities (M8) — `Models/Telemetry/`

Standalone tables, **no FK** to domain tables (`MaTask` is a nullable reference only); created by `TelemetrySchema.EnsureTables`:

| Entity | Written by | Purpose |
|---|---|---|
| `DifficultyLabelLog` | `QuanLyTaskViewModel` on task save | suggested vs. final `DoKho` (+ `WasOverride`) — ground truth for difficulty heuristics |
| `StudyTimeOutcomeLog` | `FocusViewModel.LuuThoiGianThucTe` | features at study time (`TaskType`, `Difficulty`, `Credits`, `DaysLeft`, `StudiedMinutesSoFar`) + `ActualMinutes` label — **the predictor's real training data** |
| `WeightChangeLog` | `WeightOptimizerViewModel.ApplySuggestion` | before/after weights, confidence, rationale, baseline `UserStatsSnapshot` fields, open-task cohort (JSON); outcome columns (`OutcomeMissRate`, …) filled by `OutcomeMaturationService` after the 14-day window |

### Pipeline / display structures

`ScheduleDay`, `ScheduledTask`, `TaskDashboardItem` (display includes `IsMLPrediction`), `TaskEditorBundle` (task + note + links for atomic load/save), `AdaptationSuggestion`, `HeatCell`, `DashboardChartModels` (`StatusSegment`, `SubjectTimeProgress`, `SubjectWorkload` — native dashboard charts).

### ML structures

`StudyTimeInput` (6 features: `TaskType`, `Difficulty`, `Credits`, `DaysLeft`, `StudiedMinutesSoFar`, `Label`), `StudyTimeOutput` (single `Score` = predicted minutes), `ModelMeta` (`LastRetrainedAt`, `LogsUsedCount`, `ModelVersion`, `SeedOnly`, `DeviceId`, `ModelHash`).

### Pipeline structures

`PipelineContext`, `PipelineStageResult`, `PipelineExecutionResult`.

## 3. Cascade rules (`OnModelCreating`)

- `HocKy` → `MonHoc`: cascade delete.
- `MonHoc` → `StudyTask`: cascade delete.
- `StudyTask` → `TaskNote`: unique index on `MaTask`, cascade delete.
- `StudyTask` → `TaskReferenceLink`: cascade delete.
- Telemetry tables: standalone keys, **no cascade** (no FK).

> **Sync note (Epic 1 / M1.2, G1 closed):** these `OnDelete(DeleteBehavior.Cascade)`
> configs are kept, but are no longer **hard** deletes at the SQL level. `SyncStamper` (in
> `AppDbContext.SaveChanges`) intercepts every `Deleted`-state entry — including the ones EF's own
> cascade fixup marks on loaded children — and converts it to a soft tombstone (`IsDeleted = true`,
> `DeletedAtUtc` stamped) instead of letting a real `DELETE` reach the DB. The cascade config's role
> shifted from "produce a SQL cascade" to "drive EF's in-memory fixup so cascade-tombstoning works."
> FK-only children (`TaskNote`/`TaskReferenceLink`, no navigation property) are unreachable by that
> fixup, so every `StudyTask`-removal path hand-cascades them via `TaskCascadeHelper` (M1.2-R1).
> See [`2026-07-03-g1-soft-delete-cascade.md`](../plans/2026-07-03-g1-soft-delete-cascade.md).

## 4. Data lifecycle rules

- Local data is the canonical source of truth.
- Deleting a semester tombstones dependent subjects + tasks (+ their notes + links) — see §3.
  (No UI path deletes a whole semester today; deletion is exercised via subject/task deletes below.)
- Deleting a subject tombstones its tasks (+ their notes + links).
- Deleting a task tombstones its notes + links.
- Notes and reference links follow the task.
- Subject/task deletes are expressed as **absence**: the UI drops the item from the in-memory
  `HocKy` graph and re-saves the whole semester. `SqliteHocKyRepository.LuuHocKyAsync` reconciles
  the new graph against the DB by Guid (update existing rows in place, add new rows, remove rows
  absent from the new graph) rather than a blanket remove-then-recreate — the latter would collide
  on the primary key of every unchanged row now that deletes are soft (the row never actually
  leaves the table).
- **Task FK integrity in the reconcile (Epic 1 reopen R1).** The Guid-diff above keys each task on
  its `MaMonHoc` owner FK, so that FK must be correct before the diff runs. Two guards ensure it:
  (1) `QuanLyTaskViewModel.ThemTask` stamps `MaMonHoc = MonHocHienTai.MaMonHoc` at creation
  (`QuanLyTaskViewModel.cs:192-194`), no longer relying on EF graph fixup to fill it; (2) the
  reconcile first **heals** any task that entered the graph through a navigation collection with
  `MaMonHoc == Guid.Empty` by adopting its navigation parent's id
  (`SqliteHocKyRepository.cs:118-121` — the same semantics EF fixup gave the pre-M1.2 save), then
  **fails loud** if a task references a `MonHoc` not present in the `HocKy`, throwing
  `InvalidOperationException("Reconcile: task '…' references MonHoc … not present …")`
  (`SqliteHocKyRepository.cs:191-195`) instead of silently dropping or mis-parenting it. This closed
  the B4 crash where an unstamped FK reached the diff.
  > **Latent, deferred:** `StudyTask.MucDoCanhBao` has the same constructor-stamping shape as the
  > fixed `MaMonHoc` gap but has **not** had a call-site survey (a known-unknown); tracked in
  > [system_roadmap §A.4](../specs/system_roadmap.md), not addressed in this cycle.
- Filesystem-only state (not in SQLite):
  - ML model artifacts + `ModelMeta` — `%AppData%\SmartStudyPlanner\models\`;
  - weight configuration — `%LocalAppData%\SmartStudyPlanner\weight_config.json` (`WeightConfigStore`);
  - streak — `streak_data.json` next to the binary (`JsonFileStreakStore`).

## 5. Repository layer

The legacy wide `Data/IStudyRepository` + `StudyRepository` pair is **fully retired** (repository-split plan `2026-06-02-split-studyrepository.md`, archived 2026-07-07 → `legacy/Archived plans/`; consolidation finished as Epic 1 T1.6 — zero references remain in production). All access goes through **nine narrow repositories** in `Infrastructure/Persistence/Repositories/` with SQLite implementations in `.../SQLite/Repositories/`, each constructed with a `Func<AppDbContext>` factory (in-memory SQLite testable):

| Interface | Surface |
|---|---|
| `IHocKyRepository` | `LayDanhSachHocKyAsync` (filters `IsSeeded`, **dedups cloned `MonHoc`** by name and merges their tasks — commit `946799b`) + `LuuHocKyAsync` (since **M1.2**: transactional Guid-diff reconcile of the semester graph — update-in-place / add / remove-by-absence — replacing the former remove-then-recreate, which would PK-collide once deletes became soft tombstones) |
| `IStudyTaskRepository` | CRUD on `StudyTask` (`DeleteAsync` has zero production callers; cascade-tombstones note + links via `TaskCascadeHelper` — M1.2-R1) |
| `IStudyLogRepository` | query by task / semester / since-timestamp; add |
| `IMonHocRepository` | by `HocKy` |
| `IUserStatsRepository` | aggregates `UserStatsSnapshot` (`MissRate`, `AverageDelayDays`, `FocusStreakDays`, `TotalStudyMinutesLast30Days`, …) — feature source for the M8-B Weight Optimizer |
| `ITaskEditorRepository` | `TaskEditorBundle` atomic load/save (note + links) |
| `IDifficultyLabelLogRepository` | append/read `DifficultyLabelLog` |
| `IWeightChangeLogRepository` | append `WeightChangeLog`, `GetPendingMaturationAsync`, `UpdateOutcomeAsync` |
| `IStudyTimeOutcomeLogRepository` | append/read `StudyTimeOutcomeLog` (+ `CountAsync`) |

Consumers: `DashboardViewModel`/`QuanLyMonHocViewModel`/`QuanLyTaskViewModel`/`SetupViewModel` use `IHocKyRepository`; `FocusViewModel` uses `IStudyLogRepository` + `IStudyTimeOutcomeLogRepository`; `AnalyticsViewModel` uses `IStudyLogRepository` + `IStudyTimeTrainingDataSource`; `MainWindow`'s background deadline check uses `IHocKyRepository`.

## 6. Pipeline (data flow)

```text
SQLite
  → AppDbContext (SaveChanges* → SyncStamper stamping seam)
    → 9× Sqlite*Repository (Func<AppDbContext> factory)
      → Services (decision / workload / analytics / risk / pipeline / ML / maturation)
        → ViewModels
          → Views (native dashboard charts, LiveCharts analytics, focus, workload page)
```

### Load path
`IHocKyRepository.LayDanhSachHocKyAsync` reads `HocKy` + descendants (seed-filtered, clone-dedup'd) → ViewModel scopes to current semester → planning services compute priority / schedule / risk → dashboard + analytics render.

### Save path
User edit → ViewModel command → `IHocKyRepository.LuuHocKyAsync` (transaction: Guid-diff reconcile of the semester graph — update-in-place / add / remove-by-absence → commit; rollback on failure) → dashboard reload reflects new state. Every `SaveChanges` passes through the stamping seam, which tombstones removed rows instead of hard-deleting them.

### Scheduler path
`WorkloadServiceImpl.GenerateSchedule(hocKy, capacityHours)` filters unfinished tasks → asks `IDecisionEngine.CalculatePriority` per task → packs into `ScheduleDay` while respecting capacity (least-loaded-day placement — deadline-blind; the known SOE violation source, fixed in Epic 3).

### Analytics path
Logs + task history → `StudyAnalyticsService` (`ComputeWeeklyMinutes` + `ComputeSubjectInsights` + `ComputeProductivityScore`) → bound onto `AnalyticsViewModel.WeeklyChartSeries` / `SubjectChartSeries` / `SubjectInsights` / `ProductivityValue`+`ProductivityLabel` + `HeatmapCells`.

### ML path (real-data-first since commit `2f0e51e`)
Every completed/aborted focus session appends a `StudyTimeOutcomeLog` row. On user-triggered retrain (`AnalyticsViewModel.RetrainModel`, enabled at ≥ 50 study logs): `StudyTimeTrainingDataSource.BuildAsync()` projects outcome logs into `StudyTimeInput` (requires ≥ `MinRows = 50`, else empty) → falls back to `SeedDataGenerator.Generate()` → `MLModelManager.RetrainAsync` trains FastTree, keeps the new model only if test-split R² ≥ 0.45 (atomic temp-file swap). `StudyTimePredictorService.PredictAsync` returns `(Minutes, IsMlPrediction, Confidence)` — ML wins when agreement-confidence ≥ 0.6, else the deterministic formula.

### Weight-feedback path (M8-B, Slice 8 — shipped)
`WeightOptimizerWindow` → `IDecisionEngine.SuggestWeightConfigAsync()` → user applies → `WeightConfig.Normalize()` + `WeightConfigStore.Save` + fire-and-forget `WeightChangeLog` (baseline stats + open-task cohort) → `OutcomeMaturationService` fills outcome columns at a later startup once the 14-day window elapses.

## 7. Data → feature mapping

| Surface | Inputs |
|---|---|
| Dashboard | Subject count, open tasks, priority score, risk score, predicted minutes, today's schedule, streak |
| Analytics | Study logs, completion state, per-subject totals, elapsed minutes, heatmap (`StudyLog.NgayHoc` → 5-level GitHub-green) |
| Workload balancer | Pending tasks, current capacity, deadline pressure, history |
| Weight optimizer | `UserStatsSnapshot` aggregates + current `WeightConfig` |
| Retrain | `StudyTimeOutcomeLog` rows (seed fallback) |

## 8. Future-proofing & sync-readiness

> Target (per [../plans/2026-07-01-architecture-direction-decisions.md](../plans/2026-07-01-architecture-direction-decisions.md), D-A;
> mechanics frozen in [../plans/2026-07-02-architecture-freeze-decisions.md](../plans/2026-07-02-architecture-freeze-decisions.md), D-I):
> **multi-device, two-way LAN sync** — not cloud. Executing as **Epic 1** of the
> [master plan](../plans/2026-07-03-master-plan.md) via the
> [Epic 1 execution plan](../plans/2026-07-03-epic-1-execution-plan.md).

**Already in place**
- **Stable global identity** — every entity uses a `Guid` primary key (`HocKy.MaHocKy`,
  `MonHoc.MaMonHoc`, `StudyTask.MaTask`, `StudyLog.Id`, telemetry `Id`), addressable across devices.
- **Single write path + stamping seam (M1.1)** — all writes funnel through `AppDbContext.SaveChanges*`
  → `SyncStamper`; verified: no `ExecuteUpdate`/`ExecuteDelete`/raw-SQL bypass on synced entities.
- **D-I metadata block on every synced entity (Epic 1 / M1.2, T1.1) — done.** `HocKy`, `MonHoc`,
  `StudyTask`, `StudyLog`, `TaskNote`, `TaskReferenceLink` all implement `ISyncMetadata` (`Rev`,
  `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`), stamped by the single
  `SyncStamper` seam in `AppDbContext.SaveChanges`. `Rev` is a local monotonic per-entity counter —
  **never compared across devices** (see [lessons-learned L6](lessons-learned.md)).
- **Tombstones on every synced entity (Epic 1 / M1.2, T1.2 — G1 closed) — done.** Deletes are
  soft (`IsDeleted` + `DeletedAtUtc`), including cascade-tombstone to live descendants across all
  removal paths (M1.2-R1); see §3/§4. Existing pre-Epic-1 databases upgrade in place via
  `Data/SyncSchema.EnsureColumns` + backup + migration report (T1.8).
- **Read paths filter tombstoned rows** — every repository read query excludes `IsDeleted` rows
  (mirrors the pattern `SqliteStudyLogRepository` already had for `StudyLog`).
- **A6 closed (M1.1)** — the `StudyLog` write is awaited, `DeviceId` populated at the write site,
  failures user-visible.
- `IUserStatsRepository` aggregates were designed with M8-B's `WeightOptimizerInput` in mind.
- **`MonHoc` identity semantics, not just IDs (Epic 1 / M1.3) — done, bounded to `MonHoc`.**
  Guids stop key collisions, not *semantic* duplicates (two rows meaning the same subject).
  `Models/MonHocIdentity.Normalize` (NFC → trim → collapse whitespace → invariant-culture
  lowercase; diacritics preserved — "Toán" == "toán " but "Toán" != "Toan") is the single
  definition every dedup/prevent-at-source site routes through: the 4 read-side dedups
  (`SqliteHocKyRepository.LayDanhSachHocKyAsync`, `StudyAnalyticsService.ComputeSubjectInsights`,
  `AdaptStage.Execute`, `AnalyticsViewModel`'s subject filter) and add-time prevent-at-source in
  `QuanLyMonHocViewModel.ThemMon`. This is the alpha stopgap for the semantic-duplicate class —
  true cross-device identity-merge is Epic 2. Widening the dedup key surfaced a pre-existing M1.2
  gap in `LuuHocKyAsync`'s task reconcile (task reconcile was scoped per-`MonHoc`-parent, so a
  task moved between merged clones collided with itself in one `SaveChanges`); fixed by scoping
  the task diff to the whole `HocKy` instead — see the M1.3 report for the full trace.

**Still required before two-way sync (sequenced per D-B)**
1. **Merge engine — decided ([D-I](../plans/2026-07-02-architecture-freeze-decisions.md)), not yet
   built:** field-level merge is powered by a **last-synced base snapshot per peer** (3-way diff),
   not per-field version columns. `Rev`/`ModifiedAtUtc`/`ModifiedByDeviceId` (above) are the inputs
   this merge will read — the merge logic itself, the base-snapshot store (T1.4), and change
   enumeration are Epic 2 (M2.1).
2. **Conflict policy — decided (D-F + D-I):** field-level merge by default; a field changed on both sides
   relative to the base is a concurrent same-field edit → **LWW** with tie-break `ModifiedAtUtc` →
   `ModifiedByDeviceId` (lexicographic). **Delete-vs-edit: tombstone wins.** The losing side of every
   conflict is preserved in a **conflict record** (edit history is out of v1 scope). **No HLC.**
   Still open: tombstone retention length / purge authority (default until Epic 2's G4 closes: never
   purge — a single-device alpha never purges).

## 9. Reading order

1. `Data/AppDbContext.cs` → `Data/SyncStamper.cs` → `Models/ISyncMetadata.cs`
2. `Models/HocKy.cs` → `Models/MonHoc.cs` → `Models/StudyTask.cs` → `Models/StudyLog.cs`
3. `Models/TaskNote.cs` → `Models/TaskReferenceLink.cs` → `Models/TaskEditorBundle.cs`
4. `Models/Telemetry/*.cs` → `Data/TelemetrySchema.cs`
5. `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs`
6. `Infrastructure/Persistence/Repositories/UserStatsSnapshot.cs` → `.../SqliteUserStatsRepository.cs`
7. `Services/ML/StudyTimeTrainingDataSource.cs` → `Services/Telemetry/OutcomeMaturationService.cs`
