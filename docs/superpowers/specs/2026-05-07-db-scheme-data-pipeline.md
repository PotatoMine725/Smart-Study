# Smart Study Planner — DB Scheme & Data Pipeline
## Spec · 2026-05-07

## 1. Purpose
Tài liệu này mô tả schema dữ liệu local hiện tại và cách dữ liệu đi qua app từ persistence tới planning, analytics và UI.

## 2. Database architecture
Database hiện tại là local SQLite, được EF Core quản lý qua `AppDbContext`.

### Connection behavior
- Nếu `DbContextOptions` chưa được cấu hình, app dùng file `SmartStudyData.db` trong thư mục chạy.
- Database được tạo bằng `EnsureCreated()` khi app khởi động.
- Có chế độ dev reset bằng biến môi trường `DEV_RESET_DB=1`.

### Implication
- Schema bootstrap đơn giản.
- Phù hợp local-first/offline-first.
- Chưa phải migration-centric setup.

## 3. Entity scheme
### 3.1 `HocKy`
Semester container.
- chứa danh sách `MonHoc`
- là gốc của nhiều luồng planning

### 3.2 `MonHoc`
Subject / course.
- thuộc về một `HocKy`
- chứa danh sách `StudyTask`

### 3.3 `StudyTask`
Task chính của hệ thống.
- là đơn vị input cho scheduling, decision engine, risk analyzer, dashboard
- có trạng thái, deadline, thời gian đã học, điểm ưu tiên

### 3.4 `StudyLog`
Study session record.
- dùng cho analytics, streak, và tương lai là training data cho ML

### 3.5 `TaskNote`
- 1-1 với `StudyTask`
- lưu note theo task
- cascade delete theo task

### 3.6 `TaskReferenceLink`
- 1-n với `StudyTask`
- lưu liên kết ngoài / tài liệu tham khảo
- cascade delete theo task

## 4. Relationship model
Trong `OnModelCreating()`:
- `HocKy` → many `MonHoc` with cascade delete
- `MonHoc` → many `StudyTask` with cascade delete
- `TaskNote` → unique index trên `MaTask`, 1-1 với `StudyTask`
- `TaskReferenceLink` → many-to-one với `StudyTask`

## 5. Data pipeline overview
Dữ liệu từ DB đi qua các lớp như sau:

```text
SQLite
  → AppDbContext
    → StudyRepository
      → Services (decision / workload / analytics / risk / pipeline)
        → ViewModels
          → Views
```

## 6. Operational data flows
### 6.1 Load path
- repository đọc `HocKy` và entity con
- ViewModel lấy semester hiện tại
- planning services tính priority / schedule / risk
- dashboard và analytics render kết quả

### 6.2 Save path
- user edit task / log / note / reference
- ViewModel hoặc command gọi repository
- repository persist thay đổi vào SQLite
- dashboard reload dữ liệu để phản ánh trạng thái mới

### 6.3 Scheduler path
- tasks chưa hoàn thành được lọc ra
- decision engine gán priority
- workload service chia task vào `ScheduleDay`
- pipeline có thể bổ sung risk / adaptation context

### 6.4 Analytics path
- study logs và task history được tổng hợp
- `StudyAnalyticsService` tạo report / insight / productivity score
- `AnalyticsViewModel` biến dữ liệu thành chart / summary

### 6.5 ML path
- local model đọc dữ liệu seed hoặc retrain data
- prediction service dùng input từ task / subject state
- kết quả dự đoán được dùng cho UI suggestion và planning hints

## 7. Data lifecycle rules
- Dữ liệu offline là nguồn chuẩn.
- Xóa học kỳ sẽ cascade sang môn và task con.
- Xóa môn học sẽ cascade task con.
- Note và reference link đi theo task.
- Model ML và metadata được lưu riêng trên filesystem, không nằm chung schema với SQLite.

## 8. Data-to-feature mapping
### Dashboard inputs
- số môn học
- số task mở
- priority score
- risk score
- predicted minutes
- schedule hôm nay
- streak summary

### Analytics inputs
- study logs
- task completion state
- per-subject totals
- elapsed study minutes

### Workload inputs
- pending tasks
- current capacity
- deadline pressure
- historical progress

## 9. Future-ready considerations
- Schema hiện tại rất phù hợp cho local-first.
- Nếu có cloud later, các entity này sẽ là base contract tốt cho sync.
- Tuy nhiên, hiện chưa có explicit sync metadata trong SQLite ngoài lớp ML metadata filesystem.

## 10. Reading order
1. `Data/AppDbContext.cs`
2. `Models/HocKy.cs`
3. `Models/MonHoc.cs`
4. `Models/StudyTask.cs`
5. `Models/StudyLog.cs`
6. `Models/TaskNote.cs`
7. `Models/TaskReferenceLink.cs`
8. `Data/StudyRepository.cs`
