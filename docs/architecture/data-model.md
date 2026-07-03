# Data Model & Pipeline

> Consolidated 2026-05-21 from `2026-05-07-db-scheme-data-pipeline.md`. Reflects current schema after M6.1 + M7 + Slice 4.

## 1. Database

- Engine: **SQLite**, single file `SmartStudyData.db` next to the binary.
- ORM: **EF Core** via `AppDbContext`.
- Bootstrap: `db.Database.Migrate()` (after the `NgayHoanThanh` bug switched it from `EnsureCreated`).
- Dev reset: set `DEV_RESET_DB=1` to wipe and recreate.
- Connection: `AppDbContext` configures `Data Source=SmartStudyData.db` if no `DbContextOptions` was passed (constructor overload added for testability in M6.1).

## 2. Entities

### Academic structure

| Entity | Purpose | Key relationships |
|---|---|---|
| `HocKy` | Semester container | 1→N `MonHoc`. `NgayKetThuc` is `[NotMapped]`, defaults to `NgayBatDau + 150 days` with auto/manual flag |
| `MonHoc` | Subject / course | belongs to `HocKy`; 1→N `StudyTask`; has `SoTinChi` (credits) |
| `StudyTask` | The atomic unit of work | belongs to `MonHoc`; has `TenTask`, `HanChot`, `LoaiCongViec`, `DoKho` (1–5), `TrangThai` (`StudyTaskStatus.ChuaLam`/`HoanThanh`), `ThoiGianDaHoc`, `DiemUuTien`, `NgayHoanThanh` |
| `StudyLog` | One study session | 1 per task per session; sync-ready fields `CreatedAtUtc`, `DeviceId`, `IsDeleted` added in M7 — **schema only**: the production write path never populates `DeviceId` (`ViewModels/FocusViewModel.cs:138`; see [lessons-learned L2](lessons-learned.md)) |
| `TaskNote` | Freeform note | **1-1** with `StudyTask`, unique index on `MaTask`; cascade delete |
| `TaskReferenceLink` | External link | **1-N** with `StudyTask`; cascade delete; `Title`, `Url`, `Category`, `SortOrder` |

### Pipeline / display structures

`ScheduleDay`, `ScheduledTask`, `TaskDashboardItem` (display includes `IsMLPrediction`), `TaskEditorBundle` (task + note + links for atomic load/save), `AdaptationSuggestion`.

### ML structures

`StudyTimeInput` (6 features: `TaskType`, `Difficulty`, `Credits`, `DaysLeft`, `StudiedMinutesSoFar`, `Label`), `StudyTimeOutput` (single `Score` = predicted minutes), `ModelMeta` (`LastRetrainedAt`, `LogsUsedCount`, `ModelVersion`, `SeedOnly`, `DeviceId`, `ModelHash`).

### Pipeline structures

`PipelineContext`, `PipelineStageResult`, `PipelineExecutionResult`.

## 3. Cascade rules (`OnModelCreating`)

- `HocKy` → `MonHoc`: cascade delete.
- `MonHoc` → `StudyTask`: cascade delete.
- `StudyTask` → `TaskNote`: unique index on `MaTask`, cascade delete.
- `StudyTask` → `TaskReferenceLink`: cascade delete.

> **Sync note:** these are **hard** deletes. Under the two-way LAN-sync target (§8) a hard delete
> cannot propagate to other devices; that path needs soft-delete + tombstones.

## 4. Data lifecycle rules

- Local data is the canonical source of truth.
- Deleting a semester removes dependent subjects + tasks (+ their notes + links).
- Deleting a subject removes its tasks.
- Notes and reference links follow the task.
- ML model and metadata live on the filesystem only — not in SQLite.

## 5. Repository abstractions

Two coexisting layers:

| Layer | Where | Purpose |
|---|---|---|
| Legacy | `Data/IStudyRepository` + `StudyRepository` | Wide surface; used by current ViewModels |
| New (Slice 4) | `Infrastructure/Persistence/Repositories/I*Repository` + `Infrastructure/Persistence/SQLite/Repositories/Sqlite*Repository` | Narrow per-aggregate; supports `Func<AppDbContext>` factory for in-memory SQLite tests |

New interfaces:
- `IStudyTaskRepository` — CRUD on `StudyTask`
- `IStudyLogRepository` — query by task / since timestamp
- `IMonHocRepository` — by `HocKy`
- `IUserStatsRepository` — aggregates `UserStatsSnapshot` (`MissRate`, `AverageDelayDays`, `FocusStreakDays`, `TotalStudyMinutesLast30Days`, ...) — designed as the feature source for M8-B Weight Optimizer.

Migration of consumers (`Focus`, `Dashboard` VMs) is intentionally deferred to a separate slice; only `StudyAnalyticsService` and 4 integration tests use the new layer today.

## 6. Pipeline (data flow)

```text
SQLite
  → AppDbContext
    → Repository (legacy + new)
      → Services (decision / workload / analytics / risk / pipeline / ML)
        → ViewModels
          → Views (charts, grids, focus, dashboard)
```

### Load path
Repository reads `HocKy` + descendants → ViewModel scopes to current semester → planning services compute priority / schedule / risk → dashboard + analytics render.

### Save path
User edit → ViewModel command → repository persist → dashboard reload reflects new state.

### Scheduler path
`WorkloadServiceImpl.GenerateSchedule(hocKy, capacityHours)` filters unfinished tasks → asks `IDecisionEngine.CalculatePriority` per task → packs into `ScheduleDay` while respecting capacity.

### Analytics path
Logs + task history → `StudyAnalyticsService` (`ComputeWeeklyMinutes` + `ComputeSubjectInsights` + `ComputeProductivityScore`) → bound onto `AnalyticsViewModel.WeeklyChartSeries` / `SubjectChartSeries` / `SubjectInsights` / `ProductivityValue`+`ProductivityLabel` + `HeatmapCells`.

### ML path
Seed (180 rows from `SeedDataGenerator.Generate(180)`) or `StudyLog` history → `MLModelManager` trains FastTree → `StudyTimePredictorService.Predict(input, formulaFallback)` returns `(int Minutes, bool IsMLPrediction)`. If confidence < 0.6, formula wins.

## 7. Data → feature mapping

| Surface | Inputs |
|---|---|
| Dashboard | Subject count, open tasks, priority score, risk score, predicted minutes, today's schedule, streak |
| Analytics | Study logs, completion state, per-subject totals, elapsed minutes |
| Workload balancer | Pending tasks, current capacity, deadline pressure, history |
| Heatmap | `StudyLog.NgayHoc` (date) grouped → minute sums → 5-level GitHub-green |

## 8. Future-proofing & sync-readiness

> Target (per [../plans/2026-07-01-architecture-direction-decisions.md](../plans/2026-07-01-architecture-direction-decisions.md), D-A;
> mechanics frozen in [../plans/2026-07-02-architecture-freeze-decisions.md](../plans/2026-07-02-architecture-freeze-decisions.md), D-I):
> **multi-device, two-way LAN sync** — not cloud. Aspirational; describes what exists vs. what is still needed.

**Already in place**
- **Stable global identity** — every entity uses a `Guid` primary key (`HocKy.MaHocKy`,
  `MonHoc.MaMonHoc`, `StudyTask.MaTask`, `StudyLog.Id`, telemetry `Id`), addressable across devices.
- **Partial change/tombstone metadata on `StudyLog` only** — M7's `CreatedAtUtc` / `DeviceId` / `IsDeleted`.
- `IUserStatsRepository` aggregates were designed with M8-B's `WeightOptimizerInput` in mind.

**Still required before two-way sync (sequenced first — D-B)**
1. **Identity semantics, not just IDs** — Guids stop key collisions, not *semantic* duplicates
   (two rows meaning the same subject). The dedup-cloned-`MonHoc` fix (commit `946799b`) is the preview.
2. **Tombstones on every synced entity** — today's deletes are hard cascade deletes (§3); extend to
   soft-delete with `IsDeleted` + `DeletedAtUtc` per **D-I**.
3. **Change tracking — decided ([D-I](../plans/2026-07-02-architecture-freeze-decisions.md)):** every
   synced entity carries `Rev` (monotonic per-entity counter, local to each device — **never compared
   across devices**; see [lessons-learned L6](lessons-learned.md)), `ModifiedAtUtc`, `ModifiedByDeviceId`;
   field-level merge is powered by a **last-synced base snapshot per peer** (3-way diff), not per-field
   version columns.
4. **Conflict policy — decided (D-F + D-I):** field-level merge by default; a field changed on both sides
   relative to the base is a concurrent same-field edit → **LWW** with tie-break `ModifiedAtUtc` →
   `ModifiedByDeviceId` (lexicographic). **Delete-vs-edit: tombstone wins.** The losing side of every
   conflict is preserved in a **conflict record** (edit history is out of v1 scope). **No HLC.**
   Still open: tombstone retention length / purge authority, and the soft-delete cascade policy for
   parents with live children.

## 9. Reading order

1. `Data/AppDbContext.cs`
2. `Models/HocKy.cs` → `Models/MonHoc.cs` → `Models/StudyTask.cs` → `Models/StudyLog.cs`
3. `Models/TaskNote.cs` → `Models/TaskReferenceLink.cs` → `Models/TaskEditorBundle.cs`
4. `Data/StudyRepository.cs`
5. `Infrastructure/Persistence/Repositories/UserStatsSnapshot.cs`
6. `Infrastructure/Persistence/SQLite/Repositories/SqliteUserStatsRepository.cs`
