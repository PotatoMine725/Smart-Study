# Smart Study Planner — Project Architecture
## Spec · 2026-05-07

## 1. Purpose
Tài liệu này mô tả kiến trúc hiện tại của `SmartStudyPlanner` dựa trên codebase thực tế, để làm chuẩn tham chiếu cho phân tích, mở rộng và refactor sau này.

## 2. Architecture summary
`SmartStudyPlanner` là một ứng dụng desktop WPF theo hướng local-first / offline-first. Luồng tổng quát là:

```text
Views
  → ViewModels
    → Services
      → Data / ML / Pipeline / Analytics / Risk
        → SQLite + local filesystem
```

Kiến trúc hiện tại được tổ chức theo các lớp rõ ràng:

- `Views`: giao diện WPF, cửa sổ và page.
- `ViewModels`: state, command, điều phối UI logic.
- `Services`: nghiệp vụ, pipeline, ML, analytics, risk, telemetry.
- `Data`: EF Core `DbContext` và repository.
- `Models`: entity và DTO dùng chung.

## 3. Core architectural principles
- UI không chứa business logic nặng.
- Business logic được tách khỏi view để test độc lập.
- Local data là nguồn dữ liệu mặc định.
- ML là phần tăng cường, không được phép chặn ứng dụng.
- Pipeline và services được register qua DI để giảm phụ thuộc cứng.

## 4. Main runtime composition
`App.xaml.cs` là điểm khởi tạo chính:

1. Mở database local bằng EF Core + SQLite.
2. Tạo schema nếu chưa có.
3. Build service container qua `ServiceLocator`.
4. Warm up ML model manager trong background.
5. Cho phép app tiếp tục chạy dù ML fail.

`ServiceLocator` đóng vai trò composition root tạm thời cho WPF app và đăng ký toàn bộ service quan trọng.

## 5. Major subsystems
### 5.1 Presentation layer
Các màn hình chính gồm:
- `MainWindow`
- `DashboardPage`
- `QuanLyMonHocPage`
- `QuanLyTaskPage`
- `AnalyticsPage`
- `SetupPage`
- `FocusWindow`
- `WorkloadBalancerWindow`

### 5.2 Planning and decision layer
Subsystem này tạo thứ tự ưu tiên và lịch học:
- `DecisionEngineService`
- `PriorityCalculator`
- `WeightConfig`
- `WorkloadServiceImpl`
- `IPipelineOrchestrator` / `PipelineOrchestrator`
- pipeline stages trong `Services/Pipeline/Stages`

### 5.3 Risk layer
Subsystem đánh giá mức rủi ro theo task / môn học:
- `RiskAnalyzerService`
- `RiskAssessment`
- `IRiskAnalyzer`
- `IRiskComponent`

### 5.4 Analytics layer
Subsystem tổng hợp tiến độ và hành vi học tập:
- `StudyAnalyticsService`
- `WeeklyReport`
- `SubjectInsight`
- `ProductivityScore`

### 5.5 ML layer
Subsystem ML chạy local để dự đoán thời gian học:
- `MLModelManager`
- `StudyTimePredictorService`
- `LocalModelStorageProvider`
- `SeedDataGenerator`
- schema trong `Services/ML/Schema`

### 5.6 Persistence layer
- `AppDbContext`
- `StudyRepository`
- `IStudyRepository`

## 6. Dependency boundaries
Các dependency quan trọng đi theo chiều một chiều:

- `Views` chỉ gọi `ViewModels`.
- `ViewModels` chỉ gọi `Services` và repository abstractions.
- `Services` không phụ thuộc UI.
- `Data` chỉ lo persistence.
- `Models` được dùng xuyên lớp nhưng không nên chứa UI logic.

## 7. Architectural strengths
- Tách lớp khá rõ.
- Có DI container trung tâm.
- Có pipeline stage-based thay vì một khối monolith.
- Có fallback an toàn khi ML không sẵn sàng.
- Có hướng offline-first rõ ràng.

## 8. Architectural constraints
- App hiện vẫn là Windows desktop app.
- `ServiceLocator` vẫn là composition root tạm thời, chưa phải DI setup hoàn chỉnh kiểu host builder.
- Một số ViewModel vẫn resolve service trực tiếp qua static locator.
- Database schema được tạo bằng `EnsureCreated`, nên migration story chưa phải trọng tâm hiện tại.

## 9. Recommended reading order
Khi đọc codebase theo kiến trúc, nên đi theo thứ tự:
1. `App.xaml.cs`
2. `Services/ServiceLocator.cs`
3. `Data/AppDbContext.cs`
4. `Services/Pipeline/PipelineOrchestrator.cs`
5. `Services/DecisionEngineService.cs`
6. `ViewModels/DashboardViewModel.cs`
