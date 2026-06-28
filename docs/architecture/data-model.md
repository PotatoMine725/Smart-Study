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
| `StudyLog` | One study session | 1 per task per session; sync-ready fields `CreatedAtUtc`, `DeviceId`, `IsDeleted` added in M7 |
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

## 8. Future-proofing

- Schema is already a strong contract for future cloud sync (entities map cleanly).
- No explicit sync metadata in SQLite besides M7's `CreatedAtUtc` / `DeviceId` / `IsDeleted` on `StudyLog`.
- `IUserStatsRepository` aggregates were designed with M8-B's `WeightOptimizerInput` in mind.

## 9. Reading order

1. `Data/AppDbContext.cs`
2. `Models/HocKy.cs` → `Models/MonHoc.cs` → `Models/StudyTask.cs` → `Models/StudyLog.cs`
3. `Models/TaskNote.cs` → `Models/TaskReferenceLink.cs` → `Models/TaskEditorBundle.cs`
4. `Data/StudyRepository.cs`
5. `Infrastructure/Persistence/Repositories/UserStatsSnapshot.cs`
6. `Infrastructure/Persistence/SQLite/Repositories/SqliteUserStatsRepository.cs`
