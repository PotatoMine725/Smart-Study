# Smart Study Planner — Dependency Flows
## Spec · 2026-05-07

## 1. Purpose
Tài liệu này mô tả các dependency flow chính trong codebase: ai phụ thuộc vào ai, dữ liệu đi từ đâu tới đâu, và các điểm ghép nối quan trọng.

## 2. Top-level dependency direction
Luồng phụ thuộc được thiết kế chủ yếu theo chiều:

```text
Views → ViewModels → Services → Data / Models
```

Trong đó:
- `Views` không nên biết business logic nội bộ.
- `ViewModels` điều phối UI state và command.
- `Services` chứa nghiệp vụ cốt lõi.
- `Data` lo persistence.
- `Models` là data contract dùng chung.

## 3. Composition flow
### Startup flow
1. `App.xaml.cs` chạy `OnStartup()`.
2. Database local được tạo / kiểm tra.
3. `ServiceLocator.Configure()` đăng ký DI.
4. `IMLModelManager.InitializeAsync()` được warm up background.
5. UI tiếp tục mở dù ML chưa sẵn sàng.

### Why this matters
- Startup không bị block bởi ML.
- DB và DI là nền tảng trước khi UI dùng service.
- ML bị cô lập khỏi critical launch path.

## 4. Dashboard dependency flow
Dashboard là nơi nhiều service gặp nhau nhất.

### Flow chính
- `DashboardViewModel` lấy `HocKy` hiện tại.
- Nó gọi `IPipelineOrchestrator.Execute(...)` để tạo kết quả pipeline.
- Pipeline context chứa `CapacityHours`, `ReferenceTime`, `Semester`, `Settings`.
- `DashboardViewModel` vẫn có thể fallback qua `IDecisionEngine` và `IRiskAnalyzer` nếu pipeline không cung cấp đủ dữ liệu.
- `IStudyTelemetry` ghi event UX.
- `IWorkloadService` cung cấp capacity cho workload balancing.

### Dependencies in use
- repository để lưu state
- decision engine để chấm ưu tiên / estimate minutes
- risk analyzer để hiển thị mức rủi ro
- telemetry để ghi hành vi
- pipeline orchestrator để gom dữ liệu dashboard

## 5. Scheduling dependency flow
### Scheduling inputs
- semester (`HocKy`)
- subjects (`MonHoc`)
- tasks (`StudyTask`)
- capacity hours
- current time / clock
- priority rules

### Scheduling chain
- `WorkloadServiceImpl.GenerateSchedule(...)`
- gọi `IDecisionEngine.CalculatePriority(...)`
- gọi `IDecisionEngine.CalculateRawSuggestedMinutes(...)`
- tạo `ScheduleDay` và `ScheduledTask`
- chèn task vào ngày còn capacity

### Design consequence
Decision engine là nguồn ưu tiên chính, workload service là tầng phân phối lịch.

## 6. Pipeline dependency flow
Pipeline là chuỗi stage độc lập được orchestrator điều phối.

### Orchestrator dependencies
- danh sách `IPipelineStage`
- `PipelineContext`
- `PipelineStageResult`

### Stage flow
1. `ParseInputStage`
2. `PrioritizeStage`
3. `BalanceWorkloadStage`
4. `AssessRiskStage`
5. `AdaptStage`

### Properties
- stage order được xác định bằng `Order`
- stage có thể bị skip bởi policy
- lỗi stage được gom vào `context.Errors`
- orchestrator dừng sớm khi có failure thật

## 7. ML dependency flow
### ML lifecycle
- `IModelStorageProvider` trừu tượng hóa chỗ lưu model.
- `MLModelManager` load / train / retrain / persist model.
- `StudyTimePredictorService` dùng model để dự đoán.
- `DeviceHelper` hỗ trợ metadata local.

### Fallback rule
Nếu model không sẵn sàng hoặc confidence không đủ tốt, app dùng logic deterministic thay thế.

### Startup dependency note
ML chỉ là enhancement nên không được nằm trên đường sống còn của app.

## 8. Persistence dependency flow
### Data layer
- `AppDbContext` định nghĩa entity sets.
- `OnModelCreating()` cấu hình cascade relationships.
- `StudyRepository` làm việc với `AppDbContext`.
- ViewModel / service gọi repository, không chạm trực tiếp database logic phức tạp.

### Relationship flow
- `HocKy` → many `MonHoc`
- `MonHoc` → many `StudyTask`
- `StudyTask` → 1 `TaskNote`
- `StudyTask` → many `TaskReferenceLink`

## 9. UI dependency flow
### Main UI chain
- `MainWindow` host các page / navigation.
- `DashboardPage` khởi tạo `DashboardViewModel`.
- `FocusWindow` và `WorkloadBalancerWindow` dùng task / semester context.
- Pages khác tương tự lấy ViewModel và service thông qua DI / constructor.

### UI implications
- Một số ViewModel đang phụ thuộc `ServiceLocator`, nghĩa là composition vẫn partly static.
- Dù vậy, luồng dữ liệu vẫn đi từ service lên UI, không ngược lại.

## 10. Observability flow
- `IStudyTelemetry` là abstraction cho UX telemetry.
- `DebugStudyTelemetry` hiện chỉ log ra debug output.
- Các event như `dashboard_open`, `dashboard_click_save`, `focus_start` được ghi ở ViewModel layer.

## 11. Dependency risks
- `ServiceLocator` tạo coupling global nếu lạm dụng.
- Một số logic UI vẫn gắn chặt với model objects và service calls.
- `EnsureCreated` làm schema bootstrap đơn giản nhưng không phù hợp nếu migration complexity tăng.

## 12. Reading order for understanding dependencies
1. `App.xaml.cs`
2. `Services/ServiceLocator.cs`
3. `ViewModels/DashboardViewModel.cs`
4. `Services/Pipeline/PipelineOrchestrator.cs`
5. `Services/WorkloadServiceImpl.cs`
6. `Data/AppDbContext.cs`
