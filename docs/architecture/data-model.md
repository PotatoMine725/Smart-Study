# Data Model & Pipeline

> Consolidated 2026-05-21 from `2026-05-07-db-scheme-data-pipeline.md`. Re-verified against source **2026-07-07 at commit `3c96978`** (branch `ui_rf`) — reflects schema after M6.1 + M7 + Slice 4 + M8 telemetry + **Epic 1 M1.1**. Per D-C, code is normative; Epic 1 **M1.2 is implemented but NOT merged** (worktree, refine-before-accept — [../review/2026-07-06-epic1-m1.2-review.md](../review/2026-07-06-epic1-m1.2-review.md)); this file describes `ui_rf` HEAD, with M1.2 deltas called out as *in-flight*.

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
| `StudyLog` | One study session | 1 per task per session; sync-ready fields `CreatedAtUtc`, `DeviceId`, `IsDeleted` (M7). Since **M1.1**, `DeviceId` **is populated at the write site** (`ViewModels/FocusViewModel.cs:151-159`) and the write is awaited, not fire-and-forget — closing [lessons-learned L2](lessons-learned.md) / finding A6 |
| `TaskNote` | Freeform note | **1-1** with `StudyTask`, unique index on `MaTask`; cascade delete; carries `UpdatedAtUtc` (M1.2 reconciles it into `ModifiedAtUtc`) |
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

> **Sync note:** at HEAD these are still **hard** deletes. Under the two-way LAN-sync target (§8) a hard delete cannot propagate to other devices. **M1.2 (in-flight)** replaces them: `SyncStamper` converts `Deleted` → tombstone (`IsDeleted`/`DeletedAtUtc`/`Rev++`) with G1 cascade-tombstone of descendants, and the `OnDelete(Cascade)` config becomes fixup-only. One refine remains before merge (M1.2-R1: `SqliteStudyTaskRepository.DeleteAsync` — a dead-in-production API — skips the child cascade).

## 4. Data lifecycle rules

- Local data is the canonical source of truth.
- Deleting a semester removes dependent subjects + tasks (+ their notes + links).
- Deleting a subject removes its tasks; notes and reference links follow the task.
- Filesystem-only state (not in SQLite):
  - ML model artifacts + `ModelMeta` — `%AppData%\SmartStudyPlanner\models\`;
  - weight configuration — `%LocalAppData%\SmartStudyPlanner\weight_config.json` (`WeightConfigStore`);
  - streak — `streak_data.json` next to the binary (`JsonFileStreakStore`).

## 5. Repository layer

The legacy wide `Data/IStudyRepository` + `StudyRepository` pair is **fully retired** (repository-split plan `2026-06-02-split-studyrepository.md`, archived 2026-07-07 → `legacy/Archived plans/`; consolidation finished as Epic 1 T1.6 — zero references remain in production). All access goes through **nine narrow repositories** in `Infrastructure/Persistence/Repositories/` with SQLite implementations in `.../SQLite/Repositories/`, each constructed with a `Func<AppDbContext>` factory (in-memory SQLite testable):

| Interface | Surface |
|---|---|
| `IHocKyRepository` | `LayDanhSachHocKyAsync` (filters `IsSeeded`, **dedups cloned `MonHoc`** by name and merges their tasks — commit `946799b`) + `LuuHocKyAsync` (transactional remove-then-recreate of the semester graph; M1.2 rewrites this into a Guid-diff reconcile) |
| `IStudyTaskRepository` | CRUD on `StudyTask` (`DeleteAsync` currently has zero production callers) |
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
User edit → ViewModel command → `IHocKyRepository.LuuHocKyAsync` (transaction: remove old graph → `ChangeTracker.Clear()` → re-add new graph → commit; rollback on failure) → dashboard reload reflects new state. Every `SaveChanges` passes through the stamping seam.

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

**Already in place (at `ui_rf` HEAD)**
- **Stable global identity** — every entity uses a `Guid` primary key, addressable across devices.
- **Single write path + stamping seam (M1.1, merged)** — all writes funnel through `AppDbContext.SaveChanges*` → `SyncStamper`; verified: no `ExecuteUpdate`/`ExecuteDelete`/raw-SQL bypass on synced entities.
- **`ISyncMetadata` contract** declared (`Rev`, `ModifiedAtUtc`, `ModifiedByDeviceId`, `IsDeleted`, `DeletedAtUtc`).
- **A6 closed (M1.1)** — the `StudyLog` write is awaited, `DeviceId` populated at the write site, failures user-visible.
- Partial change/tombstone metadata on `StudyLog` (M7 fields), `IUserStatsRepository` aggregates designed for M8-B.

**In-flight (M1.2 — implemented in worktree, refine-before-accept as of 2026-07-06)**
- `ISyncMetadata` on all six entities (`TaskNote.UpdatedAtUtc` → `ModifiedAtUtc` reconcile).
- Delete → tombstone everywhere + **G1 cascade-tombstone** (one blocker: M1.2-R1, `DeleteAsync` child cascade).
- `!IsDeleted` read-path filters on every UI-facing read.
- `SyncSchema.EnsureColumns` upgrade seam + `DbBackup` + `MigrationReporter`; `LuuHocKyAsync` rewritten to Guid-diff reconcile (tombstones break delete-by-recreate).

**Still required before two-way sync (sequenced per D-B)**
1. **Identity semantics, not just IDs** (M1.3) — Guids stop key collisions, not *semantic* duplicates; the dedup-cloned-`MonHoc` fix (commit `946799b`) is the preview.
2. **Change tracking — decided ([D-I](../plans/2026-07-02-architecture-freeze-decisions.md)):** `Rev` is a monotonic per-entity counter, local to each device — **never compared across devices** (see [lessons-learned L6](lessons-learned.md)); field-level merge is powered by a **last-synced base snapshot per peer** (3-way diff), not per-field version columns (Epic 2 / T1.4).
3. **Conflict policy — decided (D-F + D-I):** field-level merge by default; concurrent same-field edit → **LWW** with tie-break `ModifiedAtUtc` → `ModifiedByDeviceId` (lexicographic). **Delete-vs-edit: tombstone wins.** The losing side of every conflict is preserved in a **conflict record** (edit history out of v1 scope). **No HLC.** Still open: tombstone retention length / purge authority.

## 9. Reading order

1. `Data/AppDbContext.cs` → `Data/SyncStamper.cs` → `Models/ISyncMetadata.cs`
2. `Models/HocKy.cs` → `Models/MonHoc.cs` → `Models/StudyTask.cs` → `Models/StudyLog.cs`
3. `Models/TaskNote.cs` → `Models/TaskReferenceLink.cs` → `Models/TaskEditorBundle.cs`
4. `Models/Telemetry/*.cs` → `Data/TelemetrySchema.cs`
5. `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs`
6. `Infrastructure/Persistence/Repositories/UserStatsSnapshot.cs` → `.../SqliteUserStatsRepository.cs`
7. `Services/ML/StudyTimeTrainingDataSource.cs` → `Services/Telemetry/OutcomeMaturationService.cs`
