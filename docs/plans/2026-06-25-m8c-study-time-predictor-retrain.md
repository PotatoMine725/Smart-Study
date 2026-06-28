# Plan — M8-C Study-Time Predictor Retrain (telemetry → RetrainAsync)

## Goal
Nối **telemetry thật** vào vòng retrain của study-time predictor (`MLModelManager`),
để model thoát trạng thái `SeedOnly` và học từ dữ liệu học thật của user — **không
dựng model mới**, chỉ feed model regression đang có. Ship khi:
- Mỗi session học (Focus) ghi 1 dòng ground-truth snapshot vào bảng telemetry mới.
- Nút *Retrain* trong Analytics train trên dữ liệu thật (real-data-first), fallback
  seed khi dưới ngưỡng; sau train `ModelMeta.SeedOnly = false`, `ModelVersion` tăng.
- `dotnet build` clean, `dotnet test` ≥ baseline xanh hiện tại, `gitnexus_detect_changes`
  chỉ chạm đúng symbol dự kiến.

## Status
`draft` — 2026-06-25. Chưa code. Chờ approve.

## Context — vì sao làm
Plan M8 (`2026-06-11-m8-ground-truth-instrumentation.md`) đã dựng pattern telemetry
snapshot (`DifficultyLabelLog`, `WeightChangeLog`) nhưng **cố ý hoãn việc train ML**
("Không train ML trong plan này → hoãn Phase Training tương lai"). Đây chính là phase
đó, cho **study-time predictor** (model thứ 2 trong 2 model cho phép theo
`docs/specs/ML_Heuristic_design.md`).

Hiện trạng đã verify:
- `MLModelManager.RetrainAsync(IReadOnlyList<StudyTimeInput>)` đã sẵn (gate R²≥0.45,
  atomic swap, set `SeedOnly=false`), nhưng caller production duy nhất
  (`AnalyticsViewModel.RetrainModel`, dòng 210-225) **hardcode `SeedDataGenerator.Generate()`**.
- Ground truth có thật: mỗi session ghi `StudyLog.SoPhutHoc` (phút thực) tại
  `FocusViewModel.LuuThoiGianThucTe` (dòng 97-114) — gọi từ cả `HoanThanh` và `ThoatKhanCap`.
- **Khoảng trống**: chưa có lớp transform feature → `StudyTimeInput[]` → RetrainAsync.

Quyết định (đã chốt với user): dùng **bảng telemetry snapshot riêng** (M8-consistent),
**trigger manual** qua nút Retrain sẵn có.

## Quyết định thiết kế (đã chốt với user)
| # | Quyết định | Lý do |
|---|---|---|
| D1 | Bảng telemetry riêng `StudyTimeOutcomeLog`, **không** reuse `StudyLogs`, **không** nhồi cột ML vào `StudyLog` | Point-in-time chính xác (feature chụp tại lúc học, không drift khi user sửa DoKho/deadline); sống sót khi task bị xoá (denormalized); giữ telemetry ML tách khỏi domain model — đúng pattern M8 (`DifficultyLabelLog`/`WeightChangeLog`) và §5.1 isolation rule |
| D2 | Capture tại 1 chokepoint `FocusViewModel.LuuThoiGianThucTe` | Cả `HoanThanh` + `ThoatKhanCap` đều đi qua đây; ghi 1 chỗ, không nhân đôi |
| D3 | Trigger **manual** (nút Retrain Analytics), real-data-first + seed fallback | Đơn giản, deterministic, testable; không đụng `App.xaml.cs` lifecycle |
| D4 | Training chỉ cần feature + Label; `PredictedMinutes`/`WasMlPrediction`/`Confidence` lưu cho **eval**, `Confidence` nullable (thread-through sau) | Tách concern train vs đo prediction-error; không over-couple FocusViewModel |
| D5 | Giữ nguyên mọi guardrail hiện có + thêm volume gate (`MinRows`) | R²≥0.45 + confidence≥0.6 + formula fallback đã đúng §6; volume gate chống overfit trên ít dữ liệu thật |

### Nguyên tắc xuyên suốt (non-negotiable — `ML_Heuristic_design.md`)
- **ML vẫn advisory**: `StudyTimeOutcomeLog` + retrain **TUYỆT ĐỐI không** feed vào
  `CalculatePriority`/PriorityScore. Lõi heuristic (rules+components) bất biến.
- **≤ 2 ML model**: plan này **không thêm model**, chỉ feed regressor study-time đang có →
  vẫn trong giới hạn §5/§10.
- Giữ confidence-gate + formula fallback trong `StudyTimePredictorService` (KHÔNG sửa).
- Local-first, không thêm dependency nặng (ML.NET + FastTree đã tham chiếu).
- Không chạm contract `IDecisionEngine` / path tính ưu tiên.

### ⚠️ Callout TRAP
- **T1 — StudiedMinutesSoFar off-by-this-session**: tại `LuuThoiGianThucTe`, dòng 102
  đã `TaskGoc.ThoiGianDaHoc += phutDaHoc` **trước** khi ghi log (dòng 106). Phải chụp
  `StudiedMinutesSoFar` **trước** lệnh `+=` (hoặc `ThoiGianDaHoc - phutDaHoc`), nếu không
  feature sẽ cộng dư đúng số phút của chính session này.
- **T2 — Schema dual-path**: DB tạo trước bảng mới sẽ KHÔNG được `EnsureCreated()` thêm bảng.
  Bắt buộc `CREATE TABLE IF NOT EXISTS` trong `TelemetrySchema.EnsureTables` (giống lý do
  2 bảng M8) + smoke test dual-path trên DB cũ (mirror "M8 gate #4").
- **T3 — FocusViewModel ctor layering**: ctor có chuỗi overload + `NullStudyTelemetry`
  cho testability. Thêm repo theo **đúng pattern null-object/optional**, không phá chuỗi
  ctor hay test hiện có.
- **T4 — Credits nguồn**: lấy `Credits` từ `TaskHienTai.MonHocGoc.SoTinChi` — guard null
  (verify `MonHocGoc` được populate trên path Focus; nếu null → bỏ qua ghi log hoặc default an toàn).
- **T5 — Chỉ ghi khi `phutDaHoc > 0`**: bỏ session rỗng (tránh row Label=0 gây nhiễu train).
- **T6 — Không FK-cascade**: outcome log là snapshot denormalized; không xoá theo task.

## Pre-edit checklist (chạy trước khi sửa mỗi symbol)
- `gitnexus_impact({target:"LuuThoiGianThucTe", direction:"upstream"})` — báo blast radius (HoanThanh/ThoatKhanCap).
- `gitnexus_impact({target:"RetrainModel", direction:"upstream"})`.
- `gitnexus_impact({target:"EnsureTables", direction:"upstream"})`.
- `gitnexus_impact({target:"AppDbContext", direction:"upstream"})` trước khi thêm DbSet.
- `RetrainAsync` chỉ đọc/được gọi — không sửa; xác nhận impact để chắc.
- `gitnexus_detect_changes()` **trước mỗi commit**; cảnh báo user nếu HIGH/CRITICAL.

---

## Slice list

### Slice 0 (Commit 1) — `feat(telemetry): add StudyTimeOutcomeLog entity + repo + schema`
Tạo hạ tầng telemetry, chưa ghi/đọc gì.

**File map**
- `Models/Telemetry/StudyTimeOutcomeLog.cs` (mới) — entity, mirror `Models/Telemetry/DifficultyLabelLog.cs`.
- `Infrastructure/Persistence/Repositories/IStudyTimeOutcomeLogRepository.cs` (mới) — `AddAsync`, `GetAllAsync`/`GetSinceAsync`, `CountAsync` (mirror `IDifficultyLabelLogRepository`).
- `Infrastructure/Persistence/SQLite/Repositories/SqliteStudyTimeOutcomeLogRepository.cs` (mới) — mirror `SqliteDifficultyLabelLogRepository`, dùng `ctxFactory`, respect soft-delete nếu có.
- `Data/TelemetrySchema.cs` (sửa) — thêm `CREATE TABLE IF NOT EXISTS StudyTimeOutcomeLogs (...)` (xem schema dưới).
- `Data/AppDbContext.cs` (sửa) — `DbSet<StudyTimeOutcomeLog> StudyTimeOutcomeLogs`.
- `Services/ServiceLocator.cs` (sửa) — đăng ký `IStudyTimeOutcomeLogRepository` (cạnh dòng 46/48).

**Schema `StudyTimeOutcomeLogs`** (cột)
- `Id TEXT PK`, `CreatedUtc TEXT`, `MaTask TEXT NULL`
- Features @ study time: `TaskType INTEGER`, `Difficulty REAL`, `Credits REAL`, `DaysLeft REAL`, `StudiedMinutesSoFar REAL`
- Label/eval: `ActualMinutes REAL` (= SoPhutHoc), `PredictedMinutes REAL`, `WasMlPrediction INTEGER`, `Confidence REAL NULL`

**Exit criteria**: build clean; `dotnet test` ≥ baseline; `EnsureTables` idempotent — smoke test tạo bảng trên cả DB mới (EnsureCreated) và DB pre-M8C (raw SQL), mirror test schema dual-path đã có.

---

### Slice 1 (Commit 2) — `feat(telemetry): capture study-time ground truth on session complete`
Ghi snapshot tại chokepoint Focus.

**File map**
- `ViewModels/FocusViewModel.cs` (sửa)
  - Thêm `IStudyTimeOutcomeLogRepository` qua **chuỗi ctor null-object** sẵn có (T3).
  - Trong `LuuThoiGianThucTe(bool daHoanThanh)`: chụp `int studiedSoFar = TaskGoc.ThoiGianDaHoc;` **TRƯỚC** dòng `+= phutDaHoc` (T1); chỉ ghi khi `phutDaHoc > 0` (T5).
  - Map: `TaskType=TaskGoc.LoaiTask`, `Difficulty=TaskGoc.DoKho`, `Credits=TaskHienTai.MonHocGoc?.SoTinChi` (guard T4), `DaysLeft=(TaskGoc.HanChot - DateTime.Today).TotalDays`, `StudiedMinutesSoFar=studiedSoFar`, `ActualMinutes=phutDaHoc`, `PredictedMinutes`/`WasMlPrediction` từ `TaskHienTai` (TaskDashboardItem có `IsMLPrediction` + predicted minutes), `Confidence=null`.

**Exit criteria**: build clean; unit test: 1 session → đúng 1 outcome row, mapping đúng, `StudiedMinutesSoFar` = giá trị pre-increment, session 0 phút không ghi; `dotnet test` ≥ baseline + new tests.

---

### Slice 2 (Commit 3) — `feat(ml): project outcome logs into StudyTimeInput training set`
Lớp transform thuần, có volume gate.

**File map**
- `Services/ML/IStudyTimeTrainingDataSource.cs` (mới) — `Task<IReadOnlyList<StudyTimeInput>> BuildAsync(CancellationToken ct=default)`.
- `Services/ML/StudyTimeTrainingDataSource.cs` (mới) — đọc `IStudyTimeOutcomeLogRepository` → map row → `StudyTimeInput` (`Label = ActualMinutes`, `TaskType = ((LoaiCongViec)TaskType).ToString()`); hằng `MinRows` (vd 50); trả **empty** nếu < `MinRows`.
- `Services/ServiceLocator.cs` (sửa) — đăng ký.

**Exit criteria**: build clean; unit test: mapping row→input (Label, TaskType string đúng); < ngưỡng → empty; ≥ ngưỡng → đủ rows; `dotnet test` ≥ baseline + new tests.

---

### Slice 3 (Commit 4) — `feat(ml): retrain on real telemetry (real-data-first, seed fallback)`
Đấu nút Retrain vào dữ liệu thật.

**File map**
- `ViewModels/AnalyticsViewModel.cs` (sửa `RetrainModel`, dòng 210-225)
  - Lấy `IStudyTimeTrainingDataSource` → `var real = await src.BuildAsync();`
  - `var data = real.Count >= MinRows ? real : SeedDataGenerator.Generate();` → `await predictor.RetrainAsync(data);`
  - Đồng bộ gate `HasEnoughData` với ngưỡng real-row (hoặc giữ và thêm trạng thái "trained on N real rows vs seed").

**Exit criteria**: build clean; unit test: path đủ dữ liệu thật → `RetrainAsync` nhận real rows; path thiếu → fallback seed; `dotnet test` ≥ baseline + new tests.

---

## Acceptance gates
- `rtk dotnet build` — clean (0 error).
- `rtk dotnet test` — ≥ baseline xanh (chụp baseline trước Slice 0).
- `gitnexus_detect_changes()` mỗi commit — chỉ chạm symbol trong File map của slice đó.

## Verification (E2E sau khi ship)
1. Chạy app → mở Focus 1 task → học vài phút → `HoanThanh`. Query SQLite:
   `SELECT * FROM StudyTimeOutcomeLogs` → đúng 1 row, `ActualMinutes>0`,
   `StudiedMinutesSoFar` = tổng trước session (không cộng dư).
2. Lặp tới khi ≥ `MinRows` → bấm *Retrain* trong Analytics → kiểm
   `ModelMeta` (`SeedOnly=false`, `ModelVersion` +1, file model rewrite).
3. Dashboard: khi confidence đủ, study-time suggestion hiện cờ `IsMLPrediction`.
4. Smoke schema dual-path: chạy `EnsureTables` trên DB pre-M8C copy → bảng được tạo, data cũ nguyên.
5. Regression: priority ranking (`CalculatePriority`) không đổi — ML không rò vào lõi heuristic.

## Out of scope
- **Auto/background retrain** (app-start, sau N session): hoãn — D3 chọn manual.
- **Sửa `StudyLog.SoPhutDuKien=0`** & thread `Confidence` xuống `TaskDashboardItem`: hoãn — chỉ cần cho eval, không cho train (D4).
- **Model mới / model thứ 3**: cấm theo §5/§10.
- **TaskType classifier retrain**: thuộc `2026-06-16-m8a-textclassifier-retrain.md`, không đụng.
- **Completion report**: viết sau khi slices ship (theo `docs/reports/README.md`).
