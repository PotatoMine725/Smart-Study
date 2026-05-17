# Smart Study Planner — Usecase System Flows
## Spec · 2026-05-07

## 1. Mục đích
Tài liệu này mô tả luồng hoạt động của hệ thống theo từng usecase tiêu biểu. Trọng tâm là: người dùng thao tác trên UI → ViewModel nào nhận event → class/service nào được gọi → dữ liệu đi đâu → hệ thống trả về gì.

## 2. Nguyên tắc đọc tài liệu
Mỗi usecase dưới đây được mô tả theo khung:
- **User action**: người dùng làm gì trên UI
- **Entry point**: View / ViewModel nhận thao tác
- **Service chain**: các class được gọi tiếp theo
- **Output**: UI / data / side effects
- **Fallback / notes**: điều gì xảy ra nếu thiếu dữ liệu hoặc lỗi

## 3. UC-01 — Mở dashboard tổng quan
### User action
Người dùng mở `DashboardPage`.

### Entry point
`DashboardViewModel` được khởi tạo với `HocKy` hiện tại.

### Service chain
1. Constructor resolve:
   - `IStudyRepository`
   - `IDecisionEngine`
   - `IWorkloadService`
   - `IRiskAnalyzer`
   - `IPipelineOrchestrator`
   - `IStudyTelemetry`
2. `LoadDuLieuDashboard()` chạy ngay khi ViewModel tạo.
3. `IStudyTelemetry.Track("dashboard_open", ...)` ghi event.
4. `IPipelineOrchestrator.Execute(new PipelineContext { ... })`
   - pipeline stages có thể gồm `ParseInputStage`, `PrioritizeStage`, `BalanceWorkloadStage`, `AssessRiskStage`, `AdaptStage`
5. `BuildDashboardSummary(pipelineResult)` tổng hợp dữ liệu hiển thị.
6. Trong loop task:
   - `IDecisionEngine.CalculateRawSuggestedMinutes(task)`
   - `IRiskAnalyzer.Assess(task, mon)` khi pipeline không cung cấp đủ risk
   - `IDecisionEngine.PredictStudyMinutes(task, mon, out isMl)` để lấy gợi ý thời gian
7. `ApplySummary`, `ApplyCharts`, `ApplySchedule`, `ApplyAdaptations`, `ApplyStreak`
8. `RaiseNotification(topTasks)` nếu có task khẩn cấp.

### Output
- `ThongKe`
- `Top5Task`
- chart series cho trạng thái / môn học / thời gian
- `LichHocHomNay`
- `AdaptationItems`
- `ChuoiStreak`
- toast notification nếu có cảnh báo

### Fallback / notes
- Nếu dashboard không có dữ liệu thì hiển thị empty state.
- Nếu pipeline fail, ViewModel vẫn có thể fallback sang risk analyzer / decision engine cục bộ.

## 4. UC-02 — Người dùng nhập task mới bằng form
### User action
Người dùng điền `TenTask`, `HanChot`, `LoaiTaskIndex`, `DoKho`, thêm note hoặc link nếu cần, rồi bấm nút thêm/cập nhật.

### Entry point
`QuanLyTaskViewModel.ThemTask()`.

### Service chain
1. Validate input:
   - `TenTask` không rỗng
   - `HanChot` phải có giá trị
2. Parse `DoKho` sang số, clamp về 1..5.
3. Convert `LoaiTaskIndex` sang `LoaiCongViec`.
4. Nếu đang tạo mới:
   - `new StudyTask(TenTask, HanChot.Value, loaiTask, doKhoInt)`
   - add vào `MonHocHienTai.DanhSachTask`
   - `IStudyTelemetry.Track("task_add")`
5. Nếu đang sửa:
   - cập nhật `_taskDangSua.TenTask`, `HanChot`, `LoaiTask`, `DoKho`
   - `IStudyTelemetry.Track("task_update")`
6. `TinhDiemVaSapXep()` gọi `IDecisionEngine.CalculatePriority(task, MonHocHienTai)` cho từng task.
7. `OnRefreshGrid?.Invoke()` để view refresh.
8. `await _repository.LuuHocKyAsync(HocKyHienTai)` persist toàn bộ semester state.
9. Nếu có note hoặc link:
   - `await _repository.UpsertTaskNoteAsync(taskId, NoteContent)`
   - `await _repository.GetTaskReferenceLinksAsync(taskId)`
   - `await _repository.DeleteTaskReferenceLinkAsync(...)` cho link bị xóa
   - `await _repository.UpdateTaskReferenceLinkAsync(model)` hoặc `AddTaskReferenceLinkAsync(model)`
10. Reset form fields.

### Output
- task mới / task đã sửa trong `MonHocHienTai.DanhSachTask`
- database được cập nhật
- notes / links được đồng bộ
- form được reset về trạng thái sạch

### Fallback / notes
- Nếu thiếu tên task hoặc deadline, hiện message box và dừng.
- Quick input parser không chạm note/link, chỉ điền core fields.

## 5. UC-03 — Quick input parser
### User action
Người dùng dán mô tả tự nhiên vào ô nhập nhanh và bấm parse.

### Entry point
`QuanLyTaskViewModel.PhanTichNhapNhanh()`.

### Service chain
1. Kiểm tra input rỗng.
2. `SmartParser.Parse(VanBanNhapNhanh)`.
3. Gán kết quả vào:
   - `TenTask`
   - `HanChot`
   - `LoaiTaskIndex`
   - `DoKho`
4. Cập nhật hint và text nút lưu.
5. Clear `VanBanNhapNhanh`.

### Output
- form được điền sẵn core fields
- người dùng tiếp tục kiểm tra và bổ sung note/link

### Fallback / notes
- Parser chỉ điền field lõi, không tự động tạo note hoặc link.
- Luồng này được thiết kế để giảm sai sót thay vì tự động hóa hoàn toàn.

## 6. UC-04 — Sửa task hiện có
### User action
Người dùng chọn một task rồi bấm sửa.

### Entry point
`QuanLyTaskViewModel.SuaTask(taskCanSua)`.

### Service chain
1. Lưu reference task đang sửa vào `_taskDangSua` và `_editingTaskId`.
2. `IStudyTelemetry.Track("task_click_edit", ...)`.
3. Copy dữ liệu task ra form:
   - `TenTask`
   - `HanChot`
   - `LoaiTaskIndex`
   - `DoKho`
4. Đổi button text sang chế độ cập nhật.
5. `await _repository.GetTaskEditorBundleAsync(taskCanSua.MaTask)`
   - lấy note và link hiện có
6. Bind `NoteContent` và `StudyLinks` từ bundle.

### Output
- form chuyển sang chế độ edit
- note/link cũ được load lên UI
- task id được giữ để update đúng bản ghi

### Fallback / notes
- Nếu bundle không có note/link thì UI vẫn cho edit task bình thường.

## 7. UC-05 — Xóa task
### User action
Người dùng bấm xóa trên một task.

### Entry point
`QuanLyTaskViewModel.XoaTask(taskCanXoa)`.

### Service chain
1. Confirm bằng `MessageBox.YesNo`.
2. Nếu đồng ý:
   - remove task khỏi `MonHocHienTai.DanhSachTask`
   - `await _repository.LuuHocKyAsync(HocKyHienTai)`
   - cập nhật `HasData`

### Output
- task bị loại khỏi UI và database sau save

### Fallback / notes
- Nếu user hủy confirm thì không có thay đổi.
- Cascade delete ở DB đảm bảo note/link đi theo task nếu entity bị xóa ở persistence layer.

## 8. UC-06 — Hoàn thành task
### User action
Người dùng đánh dấu một task là hoàn thành.

### Entry point
`QuanLyTaskViewModel.HoanThanhTask(taskDaXong)`.

### Service chain
1. Kiểm tra task hợp lệ và chưa hoàn thành.
2. Set `taskDaXong.TrangThai = StudyTaskStatus.HoanThanh`.
3. `TinhDiemVaSapXep()` để cập nhật priority / warning.
4. `OnRefreshGrid?.Invoke()`.
5. `await _repository.LuuHocKyAsync(HocKyHienTai)`.

### Output
- task chuyển sang completed
- danh sách task được sắp xếp lại
- dữ liệu được persist

### Fallback / notes
- Task đã completed thì không được mark lại.

## 9. UC-07 — Vào focus mode
### User action
Người dùng mở một task trong `FocusWindow`.

### Entry point
`DashboardViewModel.MoFocusMode(taskDuocChon)`.

### Service chain
1. `IStudyTelemetry.Track("focus_start", ...)`.
2. Tạo `new Views.FocusWindow(taskDuocChon)`.
3. `ShowDialog()` để user học theo phiên Pomodoro.
4. Sau khi đóng dialog:
   - `await _repository.LuuHocKyAsync(_hocKyHienTai)`
   - `LoadDuLieuDashboard()` để refresh dashboard.

### Bên trong `FocusViewModel`
1. Khởi tạo timer 1 giây bằng `DispatcherTimer`.
2. `ThietLapPomodoro(true)` đặt phiên học 25 phút.
3. Mỗi tick:
   - giảm `_thoiGianConLai`
   - nếu đang học thì tăng `_tongGiayDaHoc`
   - cập nhật `TienDoText`
4. Khi đủ thời gian:
   - chuyển sang nghỉ hoặc quay lại học
5. Khi hoàn thành:
   - `LuuThoiGianThucTe(true)`
   - `IStudyTelemetry.Track("focus_complete", ...)`
   - set task trạng thái hoàn thành
   - `OnKetThuc?.Invoke()`
6. Khi thoát sớm:
   - `LuuThoiGianThucTe(false)`
   - `IStudyTelemetry.Track("focus_abort", ...)`
   - `OnKetThuc?.Invoke()`

### Output
- study time được cộng vào `TaskGoc.ThoiGianDaHoc`
- `StudyLog` được tạo async qua repository
- streak được update
- dashboard được refresh sau khi focus kết thúc

### Fallback / notes
- Focus mode là luồng có async save nhưng timer / UI là local, không phụ thuộc network.
- `LuuThoiGianThucTe` hiện fire-and-forget cho log, nên design ưu tiên responsiveness hơn confirm chặt chẽ.

## 10. UC-08 — Cân bằng workload / tạo lịch học
### User action
Người dùng mở workload balancer từ dashboard.

### Entry point
`DashboardViewModel.MoWorkloadBalancer()`.

### Service chain
1. Tạo `WorkloadBalancerWindow(_hocKyHienTai)`.
2. Gán owner và `ShowDialog()`.
3. Sau khi dialog đóng, gọi `LoadDuLieuDashboard()`.

### Trong luồng scheduling nền
- `IWorkloadService` lấy capacity.
- `IDecisionEngine` chấm ưu tiên và ước lượng phút.
- `PipelineOrchestrator` có thể chạy qua các stage để tạo lịch, risk và adaptations.
- Output cuối là `ScheduleDay`, `ScheduledTask`, `AdaptationSuggestion`.

### Output
- lịch học theo ngày
- dashboard cập nhật lại lịch hôm nay
- adaptation suggestions nếu có

### Fallback / notes
- Nếu pipeline skip stage nào đó, orchestrator vẫn trả về phần kết quả khả dụng.

## 11. UC-09 — Xem analytics
### User action
Người dùng mở `AnalyticsPage`.

### Entry point
`AnalyticsViewModel` và `IStudyAnalytics`.

### Service chain
1. Load data từ repository / semester context.
2. `StudyAnalyticsService` tính:
   - weekly minutes
   - subject insight
   - productivity score
3. ViewModel bind dữ liệu ra chart / summary.

### Output
- report thống kê
- chart / insight theo tuần và theo môn

### Fallback / notes
- Nếu dữ liệu ít, analytics vẫn trả về insight tối thiểu thay vì lỗi.

## 12. UC-10 — Thêm note và reference links
### User action
Người dùng nhập note, link tham khảo, hoặc xóa / mở / copy link.

### Entry point
`QuanLyTaskViewModel`.

### Service chain
- `AddLink()` validate URL bằng `Uri.TryCreate(...)`
- `TaskReferenceLinkItemVm` được thêm vào `StudyLinks`
- `RemoveLink(...)` xóa khỏi collection
- `OpenLink(...)` dùng `Process.Start(... UseShellExecute = true)`
- `CopyLink(...)` dùng `Clipboard.SetText(...)`
- khi lưu task:
  - `UpsertTaskNoteAsync(...)`
  - add/update/delete các `TaskReferenceLink`

### Output
- note và links được persist cùng task
- UI cho phép người dùng quản lý tài nguyên học tập ngay trong màn task

## 13. UC-11 — Chuyển theme
### User action
Người dùng bấm toggle theme.

### Entry point
`DashboardViewModel.ToggleTheme()`.

### Service chain
- gọi `Services.ThemeManager.ToggleTheme()`.

### Output
- giao diện chuyển theme
- không thay đổi dữ liệu nghiệp vụ

## 14. Các class xuất hiện thường xuyên trong luồng usecase
### UI / ViewModels
- `DashboardViewModel`
- `QuanLyTaskViewModel`
- `FocusViewModel`

### Services
- `IStudyRepository`
- `IDecisionEngine`
- `IWorkloadService`
- `IRiskAnalyzer`
- `IPipelineOrchestrator`
- `IStudyTelemetry`
- `StudyAnalyticsService`
- `MLModelManager`
- `StudyTimePredictorService`

### Data / models
- `AppDbContext`
- `HocKy`
- `MonHoc`
- `StudyTask`
- `StudyLog`
- `TaskNote`
- `TaskReferenceLink`
- `TaskDashboardItem`
- `ScheduledTask`
- `ScheduleDay`
- `AdaptationSuggestion`

## 15. Kết luận
Luồng hệ thống hiện tại xoay quanh một chu trình rõ ràng: người dùng nhập dữ liệu qua UI, ViewModel chuyển dữ liệu vào service layer, engine tính ưu tiên / lịch / risk / prediction, rồi kết quả lại được đẩy ngược ra dashboard, analytics và focus mode. Đây là lý do các usecase nên được đọc cùng với pipeline và dependency flow để hiểu đúng toàn bộ hành vi của app.
