# Smart Study Planner — Async Workflow
## Spec · 2026-05-07

## 1. Purpose
Tài liệu này mô tả các luồng bất đồng bộ hiện có trong app, đặc biệt là nơi app chủ động không block UI.

## 2. Overall async posture
Ứng dụng không phải async-heavy toàn cục, nhưng có một số điểm async quan trọng để giữ UI phản hồi tốt:
- startup warm-up cho ML
- repository save/load ở một số command
- retrain / model lifecycle
- task / dashboard refresh sau dialog

## 3. Startup async workflow
Trong `App.xaml.cs`:
1. DB được khởi tạo đồng bộ lúc startup.
2. DI container được build đồng bộ.
3. ML model manager được warm up bằng `Task.Run(async () => ...)`.
4. Exception của warm-up bị nuốt để app không fail startup.

### Design intent
- không để ML kéo chậm startup
- ML là enhancement, nên có thể sẵn sàng muộn
- app vẫn mở dù model hỏng / thiếu

## 4. ML async workflow
### InitializeAsync
`MLModelManager.InitializeAsync()`:
- dùng `SemaphoreSlim` để serialize lifecycle operations
- load model / meta nếu có
- nếu model không hợp lệ thì retrain từ seed data

### RetrainAsync
- nhận dataset input
- lock gate
- training model trên thread pool bằng `Task.Run`
- serialize model/meta ra file tạm
- copy atomically sang file cuối
- xóa file tạm sau khi hoàn tất

### Async properties
- tránh race condition khi có nhiều request lifecycle
- save model không block UI thread
- fallback khi confidence / quality không đạt vẫn giữ app chạy

## 5. Dashboard async-adjacent behavior
Dashboard hiện chủ yếu là synchronous trong ViewModel, nhưng có một số hành vi async ở command:
- lưu dữ liệu có `await _repository.LuuHocKyAsync(...)`
- sau focus mode hoặc thao tác workload, dashboard reload lại

### UX effect
Người dùng thấy thao tác lưu vẫn giữ UI responsive hơn so với blocking hoàn toàn.

## 6. Command-level async workflow
### Save command
- telemetry track event
- await save repository
- show confirmation dialog

### Focus mode command
- mở dialog focus
- sau khi đóng dialog, await save repository
- reload dashboard

### Other flows
Một số command khác vẫn sync vì chủ yếu chỉ mở window hoặc đổi theme.

## 7. Telemetry workflow
Telemetry hiện tại là debug-only, không có network async.
- `Track(...)` ghi vào `Debug.WriteLine`
- gọi đồng bộ nhưng chi phí nhỏ
- an toàn để dùng trong command flow

## 8. What is not async yet
- pipeline execution hiện chủ yếu sync
- workload generation sync
- dashboard load sync
- analytics refresh chưa có background refresh riêng

Điều này phù hợp với design hiện tại vì dữ liệu app local và nhỏ.

## 9. Async safety rules
- Không để ML warm-up chặn startup.
- Không để retrain block UI thread.
- Dùng gate khi thao tác ML model lifecycle.
- Ưu tiên fail-soft: nếu async task lỗi, UI vẫn còn dùng được.

## 10. Reading order
1. `App.xaml.cs`
2. `Services/ML/MLModelManager.cs`
3. `ViewModels/DashboardViewModel.cs`
4. `Data/StudyRepository.cs`
