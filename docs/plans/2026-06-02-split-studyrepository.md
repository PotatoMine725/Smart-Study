# Plan — Tách `Data/StudyRepository` thành repo theo aggregate

## Goal

Hoàn tất tầng persistence của god-object refactor: `Data/StudyRepository` (185 dòng, ôm 5 aggregate) được tách thành các repository một-trách-nhiệm, mọi consumer migrate sang seam `Infrastructure/Persistence`, và god-repository + `IStudyRepository` bị xóa. Ship xong khi không còn reference tới `IStudyRepository`/`StudyRepository` và test ≥ baseline.

## Status

`done` — 2026-06-02. **Slice A** (repo mới + DI + tests, 156→158) và **Slice B** (migrate 7 consumer + xóa god-repo + dead-code) đã ship. Build sạch, 158 test pass, 0 reference tới `IStudyRepository`/`StudyRepository`.

## Context — vì sao làm

Slices 1–4 đã thu nhỏ `DecisionEngineService` (92→32) và `SmartParser` (→20, facade), và **đã dựng sẵn seam** `Infrastructure/Persistence/Repositories/*` + `SQLite/Repositories/Sqlite*`. Nhưng seam đó **chưa migrate**: các repo nhỏ gần như dead-wired (chỉ DI + seam M8-B), còn toàn bộ ViewModel vẫn đi qua `IStudyRepository` cũ — ôm chung 3 nhóm aggregate:

1. **HocKy** — `DocHocKyAsync`, `LayDanhSachHocKyAsync`, `LuuHocKyAsync` (lưu cả cây + transaction).
2. **StudyLog** — `AddStudyLogAsync`, `GetStudyLogsAsync(hocKy)`, `GetStudyLogsSinceAsync`.
3. **M6.1 TaskEditor** — TaskNote + TaskReferenceLink + TaskEditorBundle (7 method).

## Quyết định thiết kế (đã chốt)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | **3-repo split**: `IHocKyRepository` (mới), `IStudyLogRepository` (đã có), `ITaskEditorRepository` (mới, gom M6.1) | `IStudyLogRepository` đã đủ method. M6.1 luôn đi cùng qua `TaskEditorBundle` → một repo, không tách lẻ Note/Link. |
| D2 | **Phased 2-commit** | Tách theo concern; blast radius nhỏ mỗi commit; dễ review/rollback. |
| D3 | **Bỏ 2 dead method** `DocHocKyAsync`, `GetStudyLogsSinceAsync` | Grep toàn repo: không caller nào (chỉ interface/impl/fake). `GetSinceAsync` mới vẫn giữ cho seam M8. |
| D4 | **Xóa fake mồ côi** `SmartStudyPlanner/Tests/Helpers/FakeStudyRepository.cs` | Lọt vào assembly production qua SDK glob, không ai tham chiếu, sẽ vỡ build khi xóa `IStudyRepository`. |

### ⚠️ Callout AN TOÀN DỮ LIỆU
`LuuHocKyAsync` dùng pattern *xóa cả cây cũ → `ChangeTracker.Clear()` → add cây mới*, **bọc trong transaction** (comment "BẢO MẬT 1/2"). Khi chuyển vào `SqliteHocKyRepository`: **copy nguyên văn thân method**, chỉ đổi `new AppDbContext()` → `_ctxFactory()`. **KHÔNG refactor thân transaction** — bug ở đây = mất dữ liệu.

## Pre-edit checklist

- `gitnexus_impact(StudyRepository, upstream)` → **risk: HIGH**, 33 impacted (22 direct). `CALLS` thật là các ViewModel; phần còn lại là `IMPORTS` cấp namespace `SmartStudyPlanner.Data` → false-positive cho việc xóa class. Đã báo HIGH cho user; mitigate bằng phased 2-commit + test guard.
- Trước mỗi commit: `gitnexus_detect_changes()` xác nhận blast radius khớp scope slice.
- Trước khi sửa `SchedulingOrchestrator`/`WeightConfig`: N/A (không đụng).

### Map method → consumer (đã verify)

| Method cũ | Repo đích | Consumer migrate |
|---|---|---|
| `LuuHocKyAsync` | `IHocKyRepository.LuuHocKyAsync` | Dashboard(×2), QuanLyMonHoc(×2), QuanLyTask(×3), Setup |
| `LayDanhSachHocKyAsync` | `IHocKyRepository.LayDanhSachHocKyAsync` | Setup, MainWindow.xaml.cs |
| `AddStudyLogAsync` | `IStudyLogRepository.AddAsync` | Focus |
| `GetStudyLogsAsync(hocKy)` | `IStudyLogRepository.GetForHocKyAsync` | Analytics |
| TaskEditor (bundle/note/link CRUD) | `ITaskEditorRepository.*` | QuanLyTask |
| `DocHocKyAsync`, `GetStudyLogsSinceAsync` | — bỏ (D3) | không ai |

`QuanLyMonHocViewModel` (`:18`) và `SetupViewModel` (`:14`) đang `new StudyRepository()` trực tiếp → DI hóa qua `ServiceLocator.Get<IHocKyRepository>()`.

---

## Slice list

### Slice A (Commit 1) — `refactor(persistence): add HocKy + TaskEditor repositories (no consumer change)`

Tạo repo mới theo pattern Slice 4 (interface ở `Infrastructure/Persistence/Repositories/`, impl ở `…/SQLite/Repositories/`, ctor nhận `Func<AppDbContext> ctxFactory`). **Không đụng ViewModel.** `IStudyRepository` còn nguyên.

**File map**
- `Infrastructure/Persistence/Repositories/IHocKyRepository.cs` (mới)
- `Infrastructure/Persistence/SQLite/Repositories/SqliteHocKyRepository.cs` (mới)
- `Infrastructure/Persistence/Repositories/ITaskEditorRepository.cs` (mới)
- `Infrastructure/Persistence/SQLite/Repositories/SqliteTaskEditorRepository.cs` (mới)
- `Services/ServiceLocator.cs` (thêm 2 đăng ký)
- `SmartStudyPlanner.Tests/Infrastructure/RepositoriesTests.cs` (thêm 2 test)

**Skeleton — IHocKyRepository**
```csharp
namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>Port cho HocKy aggregate root (load/save toàn cây học kỳ).</summary>
    public interface IHocKyRepository
    {
        Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default);
        Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default);
    }
}
```

**Skeleton — SqliteHocKyRepository**
```csharp
public sealed class SqliteHocKyRepository : IHocKyRepository
{
    private readonly Func<AppDbContext> _ctxFactory;
    public SqliteHocKyRepository(Func<AppDbContext> ctxFactory) => _ctxFactory = ctxFactory;

    public async Task<List<HocKy>> LayDanhSachHocKyAsync(CancellationToken ct = default)
    {
        using var db = _ctxFactory();
        return await db.HocKys
            .Include(hk => hk.DanhSachMonHoc).ThenInclude(m => m.DanhSachTask)
            .ToListAsync(ct);
    }

    public async Task LuuHocKyAsync(HocKy hocKy, CancellationToken ct = default)
    {
        if (hocKy == null) return;
        using var db = _ctxFactory();
        // ⚠️ COPY NGUYÊN VĂN thân transaction từ StudyRepository.LuuHocKyAsync.
        //    Đổi new AppDbContext() → _ctxFactory(); dùng `db`. KHÔNG refactor.
        //    BeginTransaction → load cây cũ → Remove + SaveChanges → ChangeTracker.Clear()
        //    → Add cây mới → SaveChanges → Commit (catch → Rollback).
    }
}
```

**Skeleton — ITaskEditorRepository** (gom 6 method M6.1 đang dùng; xác nhận `SaveTaskEditorBundleAsync` có caller không lúc code — hiện QuanLyTask làm piecemeal)
```csharp
namespace SmartStudyPlanner.Infrastructure.Persistence.Repositories
{
    /// <summary>Port cho M6.1: TaskNote + TaskReferenceLink (cùng TaskEditorBundle).</summary>
    public interface ITaskEditorRepository
    {
        Task<TaskEditorBundle?> GetBundleAsync(Guid taskId, CancellationToken ct = default);
        Task UpsertNoteAsync(Guid taskId, string? content, CancellationToken ct = default);
        Task<List<TaskReferenceLink>> GetLinksAsync(Guid taskId, CancellationToken ct = default);
        Task AddLinkAsync(TaskReferenceLink link, CancellationToken ct = default);
        Task UpdateLinkAsync(TaskReferenceLink link, CancellationToken ct = default);
        Task DeleteLinkAsync(Guid linkId, CancellationToken ct = default);
    }
}
```
`SqliteTaskEditorRepository` — copy thân các method tương ứng từ `StudyRepository`, đổi `new AppDbContext()` → `_ctxFactory()`.

**DI — ServiceLocator.cs** (cạnh đăng ký Slice 4, dùng `ctxFactory` dòng 43)
```csharp
services.AddSingleton<IHocKyRepository>(_ => new SqliteHocKyRepository(ctxFactory));
services.AddSingleton<ITaskEditorRepository>(_ => new SqliteTaskEditorRepository(ctxFactory));
```

**Tests** (pattern `NewDb()` + `TestDb.Create` + `TestDb.SeedTaskAsync`)
- `HocKyRepository_LuuVaLayDanhSach_RoundTrip` — lưu HocKy có Mon+Task, đọc lại đủ cây; **lưu lại lần 2 (overwrite) không nhân đôi / không mất data** (guard transaction).
- `TaskEditorRepository_NoteUpsert_VaLinkCrud` — upsert note (insert→update), add/update/delete link, bundle khớp.

**Exit criteria**: build clean; test ≥ baseline (156) + 2 test mới; `IStudyRepository` chưa đổi.

---

### Slice B (Commit 2) — `refactor(persistence): migrate consumers off IStudyRepository, remove god-repo`

**B1 — Migrate consumer** (mỗi file: đổi field/ctor-param sang repo đúng, đổi tên method theo bảng map, sửa cả ctor DI-default lẫn ctor test-injectable):
- `DashboardViewModel` (`:321,:341`) → `IHocKyRepository.LuuHocKyAsync`.
- `QuanLyMonHocViewModel` (`:18,:85,:112`) → bỏ `new StudyRepository()`, inject `IHocKyRepository`.
- `SetupViewModel` (`:14,:47,:87`) → `IHocKyRepository` (`LayDanhSach`, `Luu`).
- `MainWindow.xaml.cs` (`:95,:98`) → `Get<IHocKyRepository>()`.
- `FocusViewModel` (`:106`) → `IStudyLogRepository.AddAsync` (giữ `_ = ...`).
- `AnalyticsViewModel` (`:70`) → `IStudyLogRepository.GetForHocKyAsync(_hocKy)`.
- `QuanLyTaskViewModel` → **2 dependency**: `IHocKyRepository` (`:121,:135,:202`) + `ITaskEditorRepository` (`:157,:210,:216,:219,:224,:226`); cập nhật cả 2 overload ctor.

**B2 — Test doubles**:
- Thay `SmartStudyPlanner.Tests/Helpers/FakeStudyRepository.cs` bằng: `FakeHocKyRepository`, `FakeTaskEditorRepository`, và `FakeStudyLogRepository` (giữ `AddedLogs` cho `AnalyticsServiceTests:99` `FocusViewModel_WritesStudyLog_OnHoanThanh`).
- Cập nhật `AnalyticsServiceTests.cs:99` + `TaskNotesTests.cs:218` sang fake mới.

**B3 — Xóa legacy** (D3+D4):
- Xóa `Data/StudyRepository.cs` + `Data/IStudyRepository.cs`.
- Gỡ `AddSingleton<IStudyRepository, StudyRepository>()` (ServiceLocator `:39`). Xét gỡ `AddSingleton<AppDbContext>()` (`:38`) nếu grep xác nhận thừa — line-item riêng, không bắt buộc.
- Xóa file mồ côi `SmartStudyPlanner/Tests/Helpers/FakeStudyRepository.cs`; quét `SmartStudyPlanner/Tests/` (Pipeline/, Helpers/) tìm file khác phụ thuộc `IStudyRepository`.
- 2 dead method không port (đã bỏ theo D3).

**Exit criteria**: build clean; test ≥ baseline; grep `IStudyRepository|StudyRepository` (trừ bin/obj) = rỗng.

---

## Acceptance gates (mỗi slice)

1. `dotnet build SmartStudyPlanner.slnx` — clean, 0 warning mới.
2. `dotnet test SmartStudyPlanner.slnx --no-build` — ≥ 156 + test mới, 0 fail.
3. `gitnexus_detect_changes()` trước commit — blast radius khớp scope (A: Infrastructure + ServiceLocator + Tests; B: ViewModels + Views + Tests + Data xóa).
4. Sau Slice B: `grep -rn "IStudyRepository\|StudyRepository" --include=*.cs .` (trừ bin/obj) → rỗng.
5. Smoke data-loss: app → tạo/sửa task → lưu → restart → data còn nguyên (hoặc dựa vào test overwrite round-trip).
6. Khi mỗi slice ship: ghi vào `docs/CHANGELOG.md`.

## Out of scope

- Tách ViewModel lớn (DashboardViewModel 371 dòng…) — tầng UI, ngoài god-object plan.
- Dọn tổng thể smell "thư mục `SmartStudyPlanner/Tests/` compile vào assembly production" (chỉ xóa file mồ côi chặn build).
- Rehome `Services/Pipeline/*`, `Core/Capacity`, `Core/Sync` (đã liệt kê ở `active/refactor-god-object.md`).
