# M7 — Code Review
## Review · 2026-04-26

> **Scope reviewed:** Study Time Predictor ML stack, offline-first integration, dashboard indicator, analytics retrain hook, repository extensions, and startup DI wiring.
>
> **Verdict:** architecture tốt, blast radius được giữ hợp lý, fallback an toàn; tuy nhiên retrain flow vẫn cần theo dõi để bảo đảm nó lấy dữ liệu thực từ `StudyLog` thay vì chỉ seed data trong mọi trường hợp.

---

## 1. Executive Summary

M7 đã được triển khai theo đúng tinh thần của plan/spec và hiện đã được đồng bộ vào codebase/docs:

- tách rõ `MLModelManager`, `StudyTimePredictorService`, storage abstraction, và schema classes
- giữ nguyên formula fallback khi model chưa sẵn sàng hoặc confidence thấp
- tích hợp DI và startup warm-up không block UI
- thêm ML indicator trên dashboard và retrain entry point trên analytics

Đây là một triển khai **khá chặt và maintainable** cho giai đoạn MVP.

---

## 2. Strengths

### 2.1 Clean architecture
Các lớp trách nhiệm được chia khá tốt:

- `MLModelManager`
  - quản lý initialize / retrain / persist / load
- `StudyTimePredictorService`
  - build input, gọi model, quyết định fallback
- `IModelStorageProvider`
  - tách đường dẫn và filesystem khỏi logic train
- `DeviceHelper`
  - tách định danh thiết bị khỏi các service khác
- `SeedDataGenerator`
  - tách synthetic training data ra khỏi training pipeline

Điều này giúp M7 không làm bẩn domain layer hoặc UI layer.

### 2.2 Safe fallback behavior
Fallback được xử lý đúng hướng:

- model chưa ready → dùng formula cũ
- prediction confidence thấp → dùng formula cũ
- prediction thất bại → cũng fallback

Đây là lựa chọn rất phù hợp cho feature ML giai đoạn đầu.

### 2.3 Startup and UX safety
- app warm-up ML ở background
- không block UI khi mở app
- dashboard hiển thị `*` cho prediction từ AI
- tooltip giải thích rõ cho user
- analytics có nút `Tối ưu AI`

Các chi tiết này làm cho ML feature “visible but non-invasive”.

### 2.4 Blast radius controlled
M7 chạm vào đúng các điểm cần thiết:

- `StudyLog`
- `IStudyRepository` / `StudyRepository`
- DI container
- `DecisionEngineService`
- `DashboardViewModel`
- `AnalyticsViewModel`
- dashboard / analytics XAML

Không thấy dấu hiệu lôi ML vào logic domain quá sâu.

---

## 3. Risks / Watchouts

### 3.1 Retrain semantics still need scrutiny
Từ góc nhìn review, điểm cần theo dõi nhất là retrain.

Nếu command retrain vẫn đang dựa nhiều vào synthetic seed thay vì `StudyLog` thực, thì feature sẽ:

- đúng về mặt demo
- nhưng chưa phản ánh đầy đủ dữ liệu người dùng

Khuyến nghị:
- verify retrain path ưu tiên `StudyLog`
- seed data chỉ nên là bootstrap hoặc fallback

### 3.2 Sync prediction call path
`DecisionEngineService` gọi predictor theo kiểu sync wrapper. MVP này chấp nhận được, nhưng:

- có thể tạo overhead nếu call dày
- chưa tối ưu cho long-term async pipeline

Khuyến nghị:
- giữ cho MVP
- nhưng để lại follow-up cho async end-to-end nếu M7 mở rộng

### 3.3 Model persistence threshold needs validation
Ngưỡng persist model dựa trên R² là hợp lý, nhưng cần review theo thực tế:

- R² threshold hiện tại có thể hơi thấp hoặc hơi cao tùy data
- cần benchmark với data thật sau vài vòng sử dụng

### 3.4 StudyLog sync-ready fields must be consistently populated
`CreatedAtUtc`, `DeviceId`, `IsDeleted` đã xuất hiện trong schema, nhưng cần bảo đảm tất cả code path tạo log đều set đúng giá trị.

---

## 4. What is done well enough to ship

Nếu đánh giá theo tiêu chí MVP, các phần sau có thể xem là ship-ready:

- ML scaffold and service boundaries
- offline-first storage
- fallback formula path
- dashboard ML indicator
- analytics retrain hook
- DI registration and startup warm-up

---

## 5. Suggested follow-ups

### Non-blocking follow-ups
1. dùng `StudyLog` thật làm nguồn retrain chính
2. theo dõi performance của `PredictMinutes`
3. benchmark R² threshold bằng data thực
4. xác minh runtime binding cho `IsMLPrediction`
5. cân nhắc async prediction path ở phase sau

### Future hardening
1. cache prediction engine nếu cần
2. thêm telemetry tối thiểu cho retrain success/failure
3. bổ sung test cho load/save model round-trip nếu chưa có đầy đủ

---

## 6. Final Verdict

**Overall status:** Good MVP implementation.

**Quality judgment:**
- architecture: strong
- feature completeness: good for MVP
- production readiness: acceptable with a few follow-ups
- blast radius: controlled

**Recommendation:** merge/keep as current baseline, then iterate on real-log retraining and runtime verification in the next pass.

> **Skip note:** this review is archived and can be skipped for future implementation planning unless a new M7 follow-up is created.
