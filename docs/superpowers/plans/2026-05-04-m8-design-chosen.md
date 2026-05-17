# M8 — Design Decision and Implementation Plan
## Plan · 2026-05-04

> **Context:** This document chốt design cho M8 sau khi rà soát codebase, docs gốc, và trạng thái local/remote qua GitHub CLI.
>
> **Decision:** M8 sẽ triển khai theo thứ tự **M8-A Text Classifier trước**, sau đó mới đến **M8-B Weight Optimizer**.
>
> **Reasoning:**
> - `SmartParser` hiện vẫn là heuristic-only, là điểm vào rõ ràng và ít rủi ro nhất.
> - Text Classifier tạo ra nền tảng ML/schema/import chung cho cả M8.
> - Weight Optimizer tác động trực tiếp tới `WeightConfig`, nên cần đợi pipeline và pattern fallback của M8-A ổn định trước.
> - Offline-first vẫn là mặc định; không phụ thuộc cloud trong M8.

## 1. Current code status

### Local repo state
- Branch hiện tại: `dev`
- Remote tracking: `origin/dev`
- Working tree đang **dirty** với nhiều thay đổi đang mở ở UI, view-model, service, theme, telemetry, và tài liệu.
- Có một số file mới chưa track và một file markdown bị xoá (`DecisionEngine_Review.md`).
- Không có dấu hiệu repo bị detached HEAD hoặc thiếu remote.

### GitHub context
- `gh auth status` xác nhận đã đăng nhập GitHub bằng account `PotatoMine725`.
- `gh pr status` cho thấy branch hiện tại đang gắn với PR `#40 Dev` và checks đang ở trạng thái `1/2`.

### Codebase observations
- `SmartParser` còn là facade heuristic, chưa có ML classifier.
- `MLModelManager` đã có pattern load/train/save offline-first, nên M8 nên bám theo kiểu lifecycle này.
- `DecisionEngineService` và `WeightConfig` đã có fallback contract rõ ràng, phù hợp để làm nền cho optimizer.
- Tài liệu gốc của M8 đã mô tả rõ hai sub-model và các guardrails về confidence.

## 2. Chosen M8 design

### 2.1 M8-A — Text Classifier
**Mục tiêu:** mở rộng `SmartParser` bằng classifier ML để trích xuất:
- `TaskName`
- `TaskType`
- `Difficulty`
- `DeadlineHint`

**Ràng buộc:**
- `DeadlineHint` vẫn phải được resolve bởi engine deadline hiện có.
- Nếu ML không sẵn sàng hoặc confidence thấp, parser phải rơi về heuristic cũ một cách deterministic.
- Không làm `SmartParser` phụ thuộc hard vào artifact ML.

**Ưu tiên triển khai:** cao nhất.

### 2.2 M8-B — Weight Optimizer
**Mục tiêu:** sinh ra một `WeightConfig` thay thế, kèm confidence và explanation.

**Policy confidence chốt:**
- `confidence >= 0.75` → auto-suggest, cho phép apply nhanh nhưng vẫn cho user review
- `0.60 <= confidence < 0.75` → chỉ suggest, bắt buộc user review trước khi áp dụng
- `confidence < 0.60` → không auto-suggest, giữ current config

**Guardrail chốt:**
- Không được silently mutate cấu hình khi confidence thấp.
- Config hiện tại vẫn là fallback an toàn cuối cùng.
- Không phụ thuộc mạng.

## 3. Implementation plan

### Phase 1 — M8-A foundation
1. Define schema classes cho text classifier.
2. Add CSV importer + validation cho schema version và required columns.
3. Implement classifier service/lifecycle theo offline-first model.
4. Wire classifier vào `SmartParser` bằng merge deterministic với heuristic parser.
5. Add tests cho fallback, confidence thấp, và import validation.

### Phase 2 — M8-B foundation
1. Define optimizer input/output schema.
2. Add CSV importer + validation cho weight dataset.
3. Implement optimizer service và lifecycle.
4. Add config suggestion contract để tách đề xuất khỏi áp dụng.
5. Add tests cho suggestion generation, confidence gating, và explicit apply/ignore.

### Phase 3 — DI, UI, hardening
1. Register services trong `ServiceLocator`/startup.
2. Add review/apply flow cho weight suggestion.
3. Update parser preview UX nếu cần.
4. Verify app vẫn chạy với/không có model files.
5. Update docs trạng thái và acceptance criteria.

## 4. Acceptance criteria
- SmartParser dùng Text Classifier để cải thiện parse.
- Deadline hint vẫn do engine hiện có xử lý.
- Weight Optimizer sinh được full `WeightConfig` suggestion.
- Low-confidence suggestion phải qua review.
- CSV datasets được validate rõ ràng.
- Offline-first vẫn giữ nguyên.
- Fallback behavior vẫn deterministic và an toàn.

## 5. Delivery note

M8 nên được triển khai theo hướng “ML cải thiện trải nghiệm, không thay thế quyền kiểm soát của người dùng”.

**Kết luận:** chốt design M8 = **parser-first**, rồi mới **optimizer-second**.
