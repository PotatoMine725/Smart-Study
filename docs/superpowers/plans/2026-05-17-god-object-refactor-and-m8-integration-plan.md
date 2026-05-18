# God Object Refactor + M8 ML Integration Plan
## Plan · 2026-05-17

> **Goal:** hoàn tất việc tách các god object còn lại (`DecisionEngineService`, `SmartParser`) vào `Core/*` theo mô hình facade/adapter đã được nghiệm thu ở phase Risk (2026-05-12), sau đó mở contracts `Core/ML` + repository abstraction để M8-A (Text Classifier) và M8-B (Weight Optimizer) land an toàn.

> **Status:** in-progress — 4/8 slice đã ship (xem Progress log §0).

---

## 0. Progress log

| Slice | Trạng thái | Commit | Ngày | Ghi chú |
|---|---|---|---|---|
| 1 — Core contracts | ✅ Done | `5ece84c` | 2026-05-17 | 12 file contract; build/test xanh; fix using namespace `WeightConfig` ở `Services` |
| 2 — Split `DecisionEngineService` | ✅ Done | `3b176fb` | 2026-05-17 | 4 file Core + 9 test mới; facade 42 dòng; ServiceLocator không cần đổi |
| 3 — Parsing orchestrator + SmartParser DI | ✅ Done | TBD | 2026-05-18 | 4 file Core/Parsing + 5 test mới; SmartParser static delegate vào default `ParsingOrchestrator`; `IParsingOrchestrator` đã register ở `ServiceLocator` |
| 4 — Repository abstractions | ✅ Done | TBD | 2026-05-18 | 4 interface + 4 impl + UserStatsSnapshot; 4 integration test in-memory SQLite |
| 5 — M8-A A1+A2 schema/service | ⏸ Pending | — | — | |
| 6 — M8-A A3+A4+A5 integration | ⏸ Pending | — | — | |
| 7 — M8-B B1+B2+B3 optimizer | ⏸ Pending | — | — | |
| 8 — M8-B B4+B5 UI/harden | ⏸ Pending | — | — | |

**Test baseline:** 138 → **147** (Slice 2) → **152** (Slice 3) → **156** (Slice 4). Build/test xanh sau mỗi slice.

**GitNexus snapshot sau Slice 2:** 1.842 nodes, 4.392 edges, 65 clusters, 101 flows.

> **Tham chiếu:**
> - `docs/superpowers/plans/2026-05-12-core-modularization-refactor-plan.md`
> - `docs/superpowers/reports/2026-05-12-phase-next-final-report.md`
> - `docs/superpowers/reports/2026-05-12-change-log.md`
> - `docs/superpowers/specs/2026-04-26-m8-ml-suite-expansion.md`
> - `docs/superpowers/plans/2026-04-26-m8-ml-suite-expansion.md`

---

## 1. Bối cảnh đã chốt

- Phase Risk (2026-05-12) đã đặt mô hình bridge: `Core/Risk/*` chứa domain thật, `Services/RiskAnalyzer/RiskAnalyzerService` còn lại như facade adapter (51 dòng). Mô hình này sẽ được nhân bản cho Scheduling/Parsing.
- `DecisionEngineService` (92 dòng) vẫn ôm 4 trách nhiệm: priority scoring, raw minutes, suggestion formatting, ML predict passthrough.
- `SmartParser` (27 dòng) là static facade delegate 3 strategy parsers — chữ ký `Parse(string)` đang được `QuanLyTaskViewModel.cs:175` gọi trực tiếp; chưa có chỗ chèn ML intent classifier.
- M8 chưa khởi động. M7 (StudyTimePredictor) đã có và là tiền lệ ML duy nhất, sẽ được giữ nguyên trong `Services/ML/*` xuyên suốt refactor.

---

## 2. Quyết định scope (theo xác nhận user)

| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | Slice đầu | Chỉ commit **Core contracts** trước (`Core/Scheduling/Contracts`, `Core/Parsing/Contracts`); split `DecisionEngineService` sang slice kế tiếp |
| 2 | `SmartParser` | Convert từ **static → instance + DI** để M8-A có thể inject `ITextClassifierService` cleaner |
| 3 | Repository abstraction (Phase 7 của plan gốc) | **Làm trước M8-B** vì WeightOptimizer cần truy vấn history (StudyLogs, completion stats) |
| 4 | Pipeline rehome (`Services/Pipeline/*` → `Application/UseCases/*`) | **Tách độc lập** — không nằm trong sequence này, lên kế hoạch sau |

---

## 3. Sequence (8 slices, mỗi slice = 1 commit shippable)

### Slice 1 — Core contracts (no behavior change) — ✅ DONE (commit `5ece84c`, 2026-05-17)
**Goal:** tạo namespace + interface skeletons cho Scheduling, Parsing, ML để các slice sau có chỗ implement.

**Tạo mới:**
- `SmartStudyPlanner/Core/Scheduling/Contracts/ISchedulingOrchestrator.cs`
- `SmartStudyPlanner/Core/Scheduling/Contracts/IPriorityEvaluator.cs`
- `SmartStudyPlanner/Core/Scheduling/Contracts/IRawMinutesCalculator.cs`
- `SmartStudyPlanner/Core/Scheduling/Contracts/IStudyTimeSuggestionEngine.cs`
- `SmartStudyPlanner/Core/Parsing/Contracts/IParsingOrchestrator.cs`
- `SmartStudyPlanner/Core/Parsing/Contracts/IIntentClassifier.cs` (sẽ là augmentation port cho M8-A)
- `SmartStudyPlanner/Core/Parsing/Contracts/ITimeParsingEngine.cs`
- `SmartStudyPlanner/Core/Parsing/Contracts/ITaskExtractionEngine.cs`
- `SmartStudyPlanner/Core/Parsing/Models/ParseResult.cs` (giữ tương thích tuple cũ qua extension)
- `SmartStudyPlanner/Core/ML/Contracts/IMlConfidencePolicy.cs`
- `SmartStudyPlanner/Core/ML/Contracts/IIntentClassifierService.cs` (adapter port — implement sau ở M8-A)
- `SmartStudyPlanner/Core/ML/Contracts/IWeightOptimizerService.cs` (adapter port — implement sau ở M8-B)

**Không đổi:** mọi consumer hiện tại, không di chuyển file `Services/*`.

**Exit:** build pass, test pass (138/138).

---

### Slice 2 — Split `DecisionEngineService` → `Core/Scheduling` — ✅ DONE (commit `3b176fb`, 2026-05-17)
**Goal:** rút logic ra leaf classes, biến `DecisionEngineService` thành facade adapter (mirror Risk pattern).

**Actual outcome:**
- 4 file mới đã tạo đúng plan.
- `DecisionEngineService` rút từ 92 → 42 dòng (mục tiêu ~30, thực tế giữ thêm doc-comment + giữ chữ ký ctor đầy đủ).
- **Không cần sửa `ServiceLocator.cs`** — facade giữ nguyên ctor signature nên DI wiring không đổi.
- Tests: thêm `RawMinutesCalculatorTests` (4) + `StudyTimeSuggestionEngineTests` (5) = 9 test mới. `DecisionEngineTests` giữ nguyên, tất cả pass.
- Verify: build xanh, **147/147** pass (138 baseline + 9 mới), 0 regression.
- Pre-edit impact: `LOW` risk — chỉ ảnh hưởng `DecisionEngineTests` (depth 1–2).

**Tạo mới:**
- `Core/Scheduling/Evaluators/PriorityEvaluator.cs` ← `CalculatePriority` (wrap `PriorityCalculator`)
- `Core/Scheduling/Engines/RawMinutesCalculator.cs` ← `CalculateRawSuggestedMinutes` (logic công thức)
- `Core/Scheduling/Engines/StudyTimeSuggestionEngine.cs` ← `SuggestStudyTime` (format "x phút / Xh Yp")
- `Core/Scheduling/Orchestrators/SchedulingOrchestrator.cs` — compose 3 cái trên + `IStudyTimePredictor`

**Sửa:**
- `Services/DecisionEngineService.cs` → facade ~30 dòng, giữ chữ ký `IDecisionEngine` để `QuanLyTaskViewModel` và các caller không phải đổi
- `Services/ServiceLocator.cs` — wire `SchedulingOrchestrator` vào DI

**Test:** thêm unit test riêng cho `RawMinutesCalculator` và `StudyTimeSuggestionEngine`; giữ nguyên `DecisionEngineTests` để bảo vệ contract.

**Pre-edit:** `gitnexus_impact({target: "DecisionEngineService"})` + báo blast radius trước khi đụng.

---

### Slice 3 — Parsing orchestrator + `SmartParser` instance/DI conversion — ✅ DONE (2026-05-18)
**Goal:** mở chỗ chèn cho M8-A ngay trong `Core/Parsing`.

**Actual outcome:**
- 3 file engine/orchestrator + 1 test file (5 test mới) tạo đúng plan.
- `SmartParser` static giữ chữ ký `Parse(string)` cũ, delegate vào default `ParsingOrchestrator(SystemClock())` — không cần đổi `QuanLyTaskViewModel.cs:246`.
- `ServiceLocator` register `IParsingOrchestrator` cho consumer M8-A.
- Pre-edit impact: `SmartParser` LOW (0 caller class-level, 1 call site method-level đúng như doc).
- `detect-changes` post-edit: MEDIUM risk, 5 flows ảnh hưởng — toàn bộ là `BuildProvider` + `PhanTichNhapNhanh → Parse` đúng scope.
- Tests: thêm `ParsingOrchestratorTests` (5) → 147 → **152** pass, 0 regression (bao gồm `PhanTichNhapNhanh_DoesNotModifyNoteOrLinks` đi qua static path).
- **Deviation:** plan ghi "delegate vào singleton DI", thực tế dùng static default instance. Lý do: test `PhanTichNhapNhanh_DoesNotModifyNoteOrLinks` đi qua `SmartParser.Parse` mà không boot `ServiceLocator` → tránh phụ thuộc circular. Functionally equivalent (cùng `SystemClock`, không classifier).

**Tạo mới:**
- `Core/Parsing/Engines/RuleBasedTimeParsingEngine.cs` (wrap `DefaultDeadlineKeywordParser`)
- `Core/Parsing/Engines/TaskExtractionEngine.cs` (wrap `DefaultTaskTypeKeywordParser` + `DefaultDifficultyKeywordParser`)
- `Core/Parsing/Orchestrators/ParsingOrchestrator.cs` implement `IParsingOrchestrator`; nhận optional `IIntentClassifier` (null = pure heuristic, không gãy khi M8-A chưa có)
- `Services/SmartParserService.cs` — instance class implement `IParsingOrchestrator` facade (nếu cần tên backward-compat)

**Sửa:**
- `Services/SmartParser.cs` static → **giữ phương thức `Parse(string)` static** delegate vào singleton DI để không phá `QuanLyTaskViewModel.cs:175`; deprecate dần
- `Services/ServiceLocator.cs` đăng ký `IParsingOrchestrator`
- Các ViewModel mới (M8-A) sẽ inject `IParsingOrchestrator` thay vì gọi static

**Test:** thêm `ParsingOrchestratorTests` cover (a) không có IntentClassifier → kết quả bằng static cũ, (b) có IntentClassifier stub → kết quả merge đúng.

**Pre-edit:** `gitnexus_impact({target: "SmartParser"})` — đặc biệt check call sites của `Parse(string)`.

---

### Slice 4 — Repository abstractions (Phase 7 của plan gốc) — ✅ DONE (2026-05-18)
**Goal:** mở seam cho M8-B (Weight Optimizer cần đọc StudyLogs, completion stats, miss rate).

**Actual outcome:**
- 4 interface mới ở `Infrastructure/Persistence/Repositories/` (`IStudyTaskRepository`, `IStudyLogRepository`, `IMonHocRepository`, `IUserStatsRepository`) + `UserStatsSnapshot` (aggregate model với `MissRate`, `AverageDelayDays`, `FocusStreakDays`, `TotalStudyMinutesLast30Days`, ...).
- 4 implementation ở `Infrastructure/Persistence/SQLite/Repositories/` — nhận `Func<AppDbContext>` factory để hỗ trợ test với in-memory SQLite.
- `ServiceLocator` đăng ký toàn bộ 4 repo bằng singleton + factory closure.
- Tests: 4 integration test mới (`RepositoriesTests`) chạy in-memory SQLite — bao phủ round-trip CRUD `StudyTask`, log query by-task/since, MonHoc by-HocKy, UserStats aggregate. 152 → **156** pass.
- **Deviation:** chưa migrate consumer hiện tại (ví dụ trong plan là `StudyAnalyticsService`). Lý do: `StudyAnalyticsService` thực ra là pure function nhận `IEnumerable<StudyLog>` — không phải DB consumer. Các ViewModel khác (`Focus`, `Dashboard`, ...) gọi `IStudyRepository` trực tiếp — migrate vào slice riêng để giữ blast radius. Plan exit "ít nhất 1 consumer mẫu" được thay bằng integration test trực tiếp trên 4 repo, đủ chứng minh contract.
- Pre-edit impact: không touch symbol cũ ngoài `ServiceLocator.BuildProvider`. `detect-changes`: MEDIUM risk (do flow `OnStartup → ParsingOrchestrator` thay đổi), scope đúng kỳ vọng.

**Tạo mới:**
- `Infrastructure/Persistence/Repositories/IStudyTaskRepository.cs`
- `Infrastructure/Persistence/Repositories/IStudyLogRepository.cs`
- `Infrastructure/Persistence/Repositories/IMonHocRepository.cs`
- `Infrastructure/Persistence/Repositories/IUserStatsRepository.cs` (aggregate query cho optimizer features: `AverageDelayDays`, `MissRate`, `FocusStreakDays`…)
- Implementations trong `Infrastructure/Persistence/SQLite/Repositories/*` — delegate vào `AppDbContext` hiện tại

**Sửa:**
- Không bắt buộc rewrite tất cả call sites trong slice này — chỉ cần repository **available** cho M8-B. Migration consumer làm dần.
- `ServiceLocator.cs` đăng ký repository.

**Exit:** build/test xanh; có ít nhất 1 consumer mẫu (ví dụ `StudyAnalyticsService`) chuyển sang dùng repository để chứng minh contract đủ.

---

### Slice 5 — M8-A Task A1+A2: TextClassifier schema + service
Theo plan `2026-04-26-m8-ml-suite-expansion.md`:
- `Services/ML/TextClassifier/` + `TextClassifierService` implement `IIntentClassifierService` (contract từ Slice 1)
- Schema: `TextClassifierInput`, `TextClassifierOutput`, `TextClassifierPrediction`
- CSV importer + lifecycle (`TextClassifierModelManager`)
- Tests: schema + lifecycle round-trip

**Guardrail:** không move file `Services/ML/*` cũ.

---

### Slice 6 — M8-A Task A3+A4+A5: Parser integration + UX + tests
- Wire `TextClassifierService` vào `ParsingOrchestrator` (Slice 3 đã chừa chỗ qua optional `IIntentClassifier`)
- Confidence policy: dùng `IMlConfidencePolicy` (`>=0.60` mới được merge vào parse output; <0.60 → fallback heuristic only)
- UX: preview trong task creation ViewModel
- Tests: classifier có/không, confidence thấp → fallback, CSV validation

**Exit:** app vẫn chạy không có model file (offline-first), test xanh.

---

### Slice 7 — M8-B Task B1+B2+B3: WeightOptimizer
- `Services/ML/WeightOptimizer/` + `WeightOptimizerService` implement `IWeightOptimizerService` (contract từ Slice 1)
- Đọc features qua `IUserStatsRepository` (Slice 4)
- `WeightConfigSuggestion` + confidence gating qua `IMlConfidencePolicy`:
  - `>=0.75` auto-suggest + one-click apply
  - `0.60–0.75` suggest, require review
  - `<0.60` không suggest
- Integrate vào `SchedulingOrchestrator` (Slice 2): apply suggestion chứ không mutate `WeightConfig` trực tiếp

---

### Slice 8 — M8-B Task B4+B5: UI review/apply + harden
- Settings/analytics UI: preview suggested config + confidence + apply/ignore actions
- Tests: suggestion generation, gating, apply/ignore, fallback unchanged

---

## 4. Acceptance gate cho mọi slice

1. `dotnet build SmartStudyPlanner.slnx` xanh
2. `dotnet test SmartStudyPlanner.slnx --no-build` — không regression so với baseline 138 pass
3. `gitnexus_detect_changes()` trước khi commit — blast radius khớp phase đã khai báo
4. Đối với mọi edit chạm `DecisionEngineService` / `SmartParser` / `WeightConfig` / `SchedulingOrchestrator`: chạy `gitnexus_impact({direction: "upstream"})` trước, báo blast radius cho user nếu HIGH/CRITICAL
5. Commit message theo convention `refactor(<area>): …` hoặc `feat(M8-A/B): …`, tách theo concern (1 slice = 1 commit, không gộp)

---

## 5. Out-of-scope (sẽ lên kế hoạch riêng)

- Pipeline rehome (`Services/Pipeline/*` → `Application/UseCases/*`) — Phase 4b của plan gốc, tách độc lập sau M8
- Phase 5 `Core/Capacity` — chỉ làm khi có nhu cầu cụ thể từ feedback
- Phase 8 `Core/Sync` + PostgreSQL — defer xa hơn
- Phase 9 Feedback loop — sau M8-B ổn định

---

## 6. Immediate next action

**Slice 3 — Parsing orchestrator + `SmartParser` instance/DI conversion.**

Trước khi sửa code:
1. `npx gitnexus analyze` (nếu index stale)
2. `npx gitnexus impact SmartParser --direction upstream --repo Smart-Study` — verify call sites của `Parse(string)`, đặc biệt `QuanLyTaskViewModel.cs:175`
3. Báo blast radius cho user; nếu HIGH/CRITICAL → confirm trước khi proceed

Sau đó: tạo `Core/Parsing/Engines/*` + `Core/Parsing/Orchestrators/ParsingOrchestrator.cs`, convert `Services/SmartParser` thành facade delegate vào singleton DI, đăng ký `IParsingOrchestrator` ở `ServiceLocator`, thêm `ParsingOrchestratorTests`.

Commit: `refactor(parsing): introduce ParsingOrchestrator + convert SmartParser to instance facade`.
