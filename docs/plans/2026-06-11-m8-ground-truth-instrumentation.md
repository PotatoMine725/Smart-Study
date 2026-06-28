# Plan — M8 Ground-Truth Instrumentation (DoKho + WeightConfig→Outcome)

## Goal

Dựng tầng telemetry/ground-truth bền vững để **hai nhiệm vụ ML của M8 có dữ liệu thật trước khi train**: (1) bắt nhãn `DoKho` mỗi lần user chốt task, (2) log `WeightConfig đổi → outcome` theo cohort. Đồng thời thay default-3 cứng của DoKho bằng baseline prior-theo-TaskType (vẫn deterministic), và verify Slice 8 UI đã có. Ship xong khi: 2 bảng SQLite tạo được trên cả DB mới lẫn DB cũ, đường tạo task ghi `DifficultyLabelLog`, đường apply weight ghi `WeightChangeLog` + mature được outcome sau 14 ngày, build sạch và test ≥ baseline 191. **Không train ML trong plan này** (hoãn, gated theo volume + class-balance).

## Status

`draft` — 2026-06-11. Chưa code. Chờ approve.

## Context — vì sao làm

Hai nhiệm vụ ban đầu ("implement ML cho DoKho thuộc M8-A" và "đưa M8-B lên ML thật") **hội tụ về cùng một gốc: thiếu ground-truth trung thực.**

Bằng chứng khảo sát:
- **M8-B**: repo chưa hề log lịch sử `weight-change → outcome`. `WeightOptimizer` hiện là rule thuần (`WeightRuleEngine`), comment tự xác nhận "chưa log lịch sử weight→outcome". Không có nhãn thật để train.
- **M8-A DoKho**: nhãn `Difficulty` trong `Services/ML/TextClassifier/seed_intents.csv` (698 dòng) **lệch nặng** — `3 = 378 (54%)`, `4 = 208`, `5 = 81`, `2 = 30`, **`1 = đúng 1 dòng`** — và phân bố bám theo `TaskType`. Train ML difficulty ngay chỉ học lại tương quan `TaskType` + bias mặc định 3, **không dự đoán nổi lớp 1–2**. Lặp đúng lỗi "nhãn không trung thực" mà M8-B đang tránh.

→ Theo `docs/specs/system_roadmap.md` (§10 fallback, §11 telemetry, §13 anti-overengineering) và `docs/specs/ML_Heuristic_design.md`: **rule-based là lựa chọn trung thực khi chưa có data.** Việc đúng bây giờ là **instrument logging để tích lũy ground-truth + ship baseline trung thực; train ML để sau.**

**Phát hiện thêm:** Slice 8 (UI review/apply M8-B) **thực ra đã tồn tại** — `Views/WeightOptimizerWindow.xaml` (15.8K) + code-behind, `MainWindow` đã wire `NavWeightOptimizer`, `WeightOptimizerViewModel` đầy đủ gating/apply + tests. Roadmap doc đang stale. Slice 8 ở đây = **verify + hook logging**, KHÔNG build lại.

## Quyết định thiết kế (đã chốt với user)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | **DoKho: hoãn ML**, ship baseline keyword + prior-theo-TaskType | Nhãn hiện default-heavy + bám TaskType → train là dishonest. Baseline deterministic + explainable, đúng spec §10. |
| D2 | **Phạm vi = logging + baseline + verify Slice 8** | Apply path là nơi hook weight-change log; Slice 8 đã có nên chỉ verify. |
| D3 | **Outcome M8-B đo theo cohort, window 14 ngày** | Cohort = task đang mở tại lúc apply → tránh confound do task mới sinh sau apply. |
| D4 | **Rule `WeightRuleEngine` vẫn là backbone kể cả sau khi có data** | Tín hiệu outcome là quan sát yếu (1 user, không counterfactual). ML sau này chỉ thay phần đề xuất; `IsValid()`/`Normalize()` last-line. |
| D5 | **DoKho ground-truth = nhãn user commit**, KHÔNG dùng study-minutes | Tránh feedback loop với `StudyTimePredictor` (vốn dùng `task.DoKho` làm feature). |

### ⚠️ Callout TRAP — schema migration trên DB cũ
`db.Database.EnsureCreated()` **KHÔNG thêm bảng mới vào DB đã tồn tại**. Hai bảng log phải tạo bằng `CREATE TABLE IF NOT EXISTS` raw SQL lúc startup, **mirror đúng pattern `IsSeeded ALTER TABLE`** ở `App.xaml.cs:29-42` (try/catch `SqliteException`). Quên việc này → app crash hoặc insert fail âm thầm trên máy đã có DB.

### Nguyên tắc xuyên suốt (non-negotiable)
- ML/telemetry **chỉ advisory**, không tự mutate planner core. Decision/Planner/Balancer/Risk giữ nguyên.
- Offline-first: chỉ SQLite + file local, không network.
- Mọi đường ghi log **bọc try/catch** → logging không bao giờ chặn save task / apply weight (enhancement, không phải critical path).
- Test mirror prod namespace 1:1 (doubles→`TestDoubles/`, fixtures→`Fixtures/`).
- Một phase = một hoặc vài commit theo concern; confirm từng bước.

## Pre-edit checklist (chạy trước khi sửa mỗi symbol)

- `gitnexus_impact(target: "WeightOptimizerViewModel", direction: "upstream")` trước khi hook `ApplySuggestion` — báo HIGH/CRITICAL nếu có.
- `gitnexus_impact(target: "AppDbContext", direction: "upstream")` trước khi thêm DbSet — blast radius lớn (toàn repo).
- `gitnexus_impact(target: "ServiceLocator", direction: "upstream")` trước khi thêm đăng ký.
- `gitnexus_impact(target: "DefaultDifficultyKeywordParser", direction: "upstream")` trước khi đổi fallback.
- Trước mỗi commit: `gitnexus_detect_changes()` xác nhận blast radius khớp scope slice.

---

## Slice list

### Slice 0 (Commit 1) — `feat(telemetry): add DifficultyLabelLog + WeightChangeLog persistence`

Hai entity log + 2 repo + tạo bảng an toàn trên DB cũ. **Chưa hook vào UI** — chỉ nền tảng + round-trip test.

**File map**
- `Models/Telemetry/DifficultyLabelLog.cs` (mới)
- `Models/Telemetry/WeightChangeLog.cs` (mới)
- `Data/AppDbContext.cs` — +2 `DbSet`, cấu hình key trong `OnModelCreating` (mirror `TaskNote`)
- `App.xaml.cs` — +`CREATE TABLE IF NOT EXISTS` cho 2 bảng (mirror block `IsSeeded`)
- `Infrastructure/Persistence/Repositories/IDifficultyLabelLogRepository.cs` (mới)
- `Infrastructure/Persistence/Repositories/IWeightChangeLogRepository.cs` (mới)
- `Infrastructure/Persistence/SQLite/Repositories/SqliteDifficultyLabelLogRepository.cs` (mới, `Func<AppDbContext>` factory — mirror `SqliteUserStatsRepository`)
- `Infrastructure/Persistence/SQLite/Repositories/SqliteWeightChangeLogRepository.cs` (mới)
- `Services/ServiceLocator.cs` — +2 đăng ký (cạnh `IUserStatsRepository`)
- `SmartStudyPlanner.Tests/Infrastructure/Persistence/...` — round-trip test cả 2 repo (dùng `Fixtures/TestDb`)

**`DifficultyLabelLog`** — ground-truth DoKho:
`Id (Guid PK)`, `CreatedUtc`, `InputText (string)`, `TaskType (LoaiCongViec)`, `SuggestedDoKho (int?)`, `FinalDoKho (int)`, `WasOverride (bool)`, `Source (string)`, `MaTask (Guid?)`.

**`WeightChangeLog`** — ground-truth M8-B (cohort-based):
- PK + `AppliedUtc`, `Confidence (double)`, `Rationale (string)`.
- Before: `BeforeTime/BeforeTaskType/BeforeCredit/BeforeDifficulty`. After: `AfterTime/AfterTaskType/AfterCredit/AfterDifficulty`.
- Snapshot baseline "trước": `BaselineMissRate`, `BaselineAvgDelayDays`, `BaselineFocusStreak`, `BaselineStudyMinutes30`, `BaselineTaskCount`, `BaselineCompletedCount`.
- Cohort: `CohortTaskIdsJson (string)` — list `Guid` task đang mở tại `AppliedUtc`.
- Outcome (nullable, fill khi mature): `OutcomeWindowDays (=14)`, `OutcomeMaturedUtc (DateTime?)`, `OutcomeMissRate (double?)`, `OutcomeAvgDelayDays (double?)`, `OutcomeCompletedInWindow (int?)`.

**Exit criteria**: build clean; test ≥ 191 + 2 test mới; mở DB cũ (đã có) không crash, cả 2 bảng tồn tại sau startup.

---

### Slice 1A (Commit 2) — `feat(m8a): difficulty baseline = TaskType prior fallback`

Thay default-3 cứng bằng prior-theo-TaskType. Thuần deterministic, unit-test trực tiếp.

**File map**
- `Services/Strategies/IDifficultyKeywordParser.cs` (`DefaultDifficultyKeywordParser`) — khi không match keyword `khó`/`dễ`, fallback theo prior TaskType thay vì default cố định.
- Test: bảng giá trị prior mong đợi.

**Prior mapping (rút từ phân bố thực đo được):**
`DoAnCuoiKy → 4`, `ThiCuoiKy → 4`, `ThiGiuaKy → 3`, `KiemTraThuongXuyen → 3`, `BaiTapVeNha → 2`.

`TextClassifierService.Predict` giữ `DoKho = null` (KHÔNG đưa difficulty vào model ML). `IntentPrediction.DoKho` đã có field nếu muốn surface prior.

**Exit criteria**: build clean; test ≥ baseline + test prior mapping; keyword `khó`/`dễ` vẫn thắng prior.

---

### Slice 1B (Commit 3) — `feat(m8a): capture difficulty ground-truth on task save`

Hook bắt nhãn tại đường lưu task.

**File map**
- `ViewModels/QuanLyTaskViewModel.cs` — khi user lưu task: ghi `DifficultyLabelLog` (`InputText`, `SuggestedDoKho` = giá trị parser/baseline đề xuất, `FinalDoKho` = giá trị user chốt, `WasOverride = suggested != final`, `TaskType`, `MaTask`). Inject `IDifficultyLabelLogRepository` (giữ default ctor resolve từ `ServiceLocator`). Bọc try/catch.
- Test (mirror ns): override detection đúng; save task vẫn chạy khi repo logging ném lỗi (nuốt exception).

**Exit criteria**: build clean; test ≥ baseline + test mới; smoke: tạo task đổi DoKho khác đề xuất → DB có dòng `WasOverride = 1`.

---

### Slice 2A (Commit 4) — `feat(m8b): log weight change + cohort on apply`

Đường apply ghi before/after + cohort + snapshot "trước".

**File map**
- `ViewModels/WeightOptimizerViewModel.cs` — tại `ApplySuggestion()` (≈line 77), trước `_onSave(cfg)`: ghi `WeightChangeLog` = before-config, after-config, `Confidence`, `Rationale`, snapshot hiện tại (qua `IUserStatsRepository.GetSnapshotAsync`), cohort = `Id` các task đang mở (`TrangThai != HoanThanh`) tại `AppliedUtc`. Inject `IWeightChangeLogRepository` + `IUserStatsRepository` + `IClock` (giữ default ctor resolve `ServiceLocator`). Bọc try/catch.
- Test: Apply ghi log với cohort + snapshot đúng; Keep KHÔNG ghi.

**Exit criteria**: build clean; test ≥ baseline + test mới; smoke: Apply → DB có `WeightChangeLog`, `OutcomeMaturedUtc` null.

---

### Slice 2B (Commit 5) — `feat(m8b): cohort outcome maturation service`

Mature outcome sau 14 ngày trên đúng cohort (phần khó nhất).

**File map**
- `Services/Telemetry/IOutcomeMaturationService.cs` + `OutcomeMaturationService.cs` (mới) — quét `WeightChangeLog` có `OutcomeMaturedUtc == null` và `AppliedUtc <= now - 14d`; với mỗi dòng, đọc lại **đúng cohort** (từ `CohortTaskIdsJson`) và tính miss-rate/avg-delay **của riêng cohort** tại mốc `AppliedUtc + 14d`; fill outcome. Idempotent.
- `IWeightChangeLogRepository` (hoặc `IUserStatsRepository`) — +method tính outcome theo `List<Guid>` cohort (mỗi MaTask → trạng thái/độ trễ so với `HanChot`).
- `App.xaml.cs` — gọi mature **cơ hội** trong background lúc launch (mirror block warm-up `_ = Task.Run(...)`). WPF không có scheduler → mature opportunistic (launch + khi mở `WeightOptimizerWindow`).
- Test: maturation chỉ fill dòng đã quá 14 ngày; tính trên đúng cohort (không lẫn task mới); chạy 2 lần không đổi kết quả (idempotent).

**Exit criteria**: build clean; test ≥ baseline + test mới; test giả lập `AppliedUtc` lùi 15 ngày → outcome fill từ đúng cohort.

---

### Slice 2C (Commit 6) — `docs(m8b): verify Slice 8 UI + sync stale roadmap`

**File map (chủ yếu doc + verify, ít/không code):**
- Verify `Views/WeightOptimizerWindow.xaml` hiển thị current vs suggested + confidence + `Rationale` + Apply/Keep, gate qua `DefaultMlConfidencePolicy` (≥0.75 auto / 0.60–0.75 review / <0.60 drop). Bổ sung nếu thiếu.
- `docs/active/refactor-god-object.md` — đánh dấu Slice 8 done + thêm slice telemetry.
- `docs/active/m8-text-classifier.md`, `docs/active/m8-weight-optimizer.md` — cập nhật trạng thái baseline + logging.
- `docs/CHANGELOG.md` — ghi các slice đã ship.

**Exit criteria**: doc khớp thực tế code; UI smoke chạy được Apply/Keep.

---

## Acceptance gates (mỗi slice)

1. `rtk dotnet build SmartStudyPlanner.slnx` — clean, 0 warning mới.
2. `rtk dotnet test SmartStudyPlanner.slnx --no-build` — ≥ 191 + test mới, 0 fail.
3. `gitnexus_detect_changes()` trước commit — blast radius khớp scope slice.
4. Schema dual-path: xoá DB rồi mở lại (nhánh `EnsureCreated`) + mở DB cũ có sẵn (nhánh `CREATE TABLE IF NOT EXISTS`) → cả 2 bảng tạo được.
5. Khi mỗi slice ship: ghi `docs/CHANGELOG.md`.

## Out of scope (hoãn — Phase Training tương lai)

Để tránh over-engineer khi chưa có data (spec §13). Ghi exit-criteria thành plan tương lai, **KHÔNG code lúc này**:
- **DoKho model**: khi `DifficultyLabelLog` đủ mẫu **và cân bằng lớp** (đặc biệt lớp 1–2 hiện ~0) → train text→difficulty (tái dùng pattern `TextClassifierModelManager`, zip riêng), advisory + gate confidence; baseline keyword/prior vẫn fallback.
- **Weight optimizer model**: khi `WeightChangeLog` đủ dòng đã mature → train thay phần đề xuất của `WeightRuleEngine` (rule vẫn fallback; `IsValid()`/`Normalize()` last-line). Tín hiệu quan sát yếu → rule vẫn backbone (D4).
- Train pipeline, model retrain UI, model versioning cho 2 model mới.
