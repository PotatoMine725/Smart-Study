# Smart Study Planner — Usecase Analysis
## Spec · 2026-05-07

## 1. Mục đích
Tài liệu này phân tích các usecase chính mà hệ thống hiện tại hỗ trợ hoặc có thể hỗ trợ một cách tự nhiên từ codebase hiện có. Mục tiêu là làm rõ người dùng làm gì, hệ thống phản ứng ra sao, và usecase nào là lõi so với phụ trợ.

## 2. Phân loại usecase
### 2.1 Usecase lõi
Đây là các luồng trực tiếp tạo giá trị chính cho app:
- tạo / chỉnh sửa / xóa task học tập
- phân tích nhanh input người dùng
- đánh giá ưu tiên và rủi ro
- tạo lịch học và cân bằng tải
- vào chế độ focus để học theo phiên
- lưu tiến trình và ghi log học

### 2.2 Usecase phụ trợ
Các luồng này không phải “core planner” nhưng hỗ trợ trải nghiệm:
- xem dashboard tổng quan
- xem analytics
- thêm note và link tham khảo cho task
- chuyển theme
- nhận thông báo nhắc việc
- mở workload balancer

## 3. Nhóm usecase theo mục tiêu người dùng
### 3.1 Nhập và quản lý dữ liệu học tập
Người dùng nhập học kỳ, môn học, task, deadline, độ khó và ghi chú. Đây là nhóm usecase nền, vì mọi pipeline còn lại đều cần dữ liệu này.

### 3.2 Ưu tiên hóa và lập kế hoạch
Sau khi có task, hệ thống tính điểm ưu tiên, ước lượng thời gian học, sắp lịch theo capacity và cảnh báo rủi ro.

### 3.3 Theo dõi tiến trình học
Người dùng vào focus mode, học theo timer, hoàn thành task và tạo study log.

### 3.4 Quan sát và ra quyết định
Người dùng xem dashboard, analytics và các cảnh báo để biết nên học gì trước.

## 4. Danh sách usecase chính
### UC-01 — Xem dashboard tổng quan
Người dùng mở dashboard để thấy tình trạng học tập hiện tại.

### UC-02 — Thêm task mới
Người dùng nhập task mới bằng form trực tiếp hoặc quick input parser.

### UC-03 — Sửa task hiện có
Người dùng mở một task để cập nhật deadline, loại task, độ khó, note và link.

### UC-04 — Xóa task
Người dùng xóa một task không còn cần thiết.

### UC-05 — Đánh dấu hoàn thành
Người dùng mark task là completed.

### UC-06 — Vào focus mode
Người dùng mở một task trong chế độ tập trung để học theo phiên.

### UC-07 — Cân bằng workload / tạo lịch học
Người dùng mở workload balancer để hệ thống phân bổ task theo ngày.

### UC-08 — Xem analytics
Người dùng xem số phút học, tiến độ, productivity, insight theo môn.

### UC-09 — Thêm note và reference links
Người dùng bổ sung tài nguyên học tập cho task.

### UC-10 — Chuyển theme
Người dùng đổi giao diện sáng/tối.

## 5. Usecase thường gặp theo vai trò
### 5.1 Học sinh / người dùng cuối
- nhập task
- xem lịch học đề xuất
- tập trung học
- đánh dấu hoàn thành
- xem thống kê

### 5.2 Người dùng muốn kiểm tra nhanh
- mở dashboard
- nhìn top task
- nhìn cảnh báo khẩn cấp
- xem lịch hôm nay

### 5.3 Người dùng muốn chỉnh sửa dữ liệu
- mở màn quản lý môn / task
- sửa input
- thêm note / link
- lưu thay đổi

## 6. Các nhóm input chính mà hệ thống hiểu
- học kỳ
- môn học
- task title
- deadline
- loại task
- độ khó
- note
- reference link
- study log / phút học thực tế
- trạng thái hoàn thành

## 7. Các output chính mà hệ thống trả về
- task ưu tiên cao/thấp
- mức cảnh báo
- thời gian học đề xuất
- lịch học theo ngày
- risk report
- analytics insight
- progress saved
- study logs
- notifications

## 8. Các điểm quyết định nghiệp vụ nổi bật
- nếu task hoàn thành thì không đưa vào top task phải xử lý
- nếu priority cao hơn ngưỡng thì task được gắn khẩn cấp
- nếu ML chưa sẵn sàng thì fallback sang heuristic
- nếu pipeline không chạy được thì dashboard vẫn phải hiển thị phần còn lại
- nếu người dùng sửa xong task thì hệ thống phải persist ngay

## 9. Usecase có tính hệ thống cao
Các luồng sau ảnh hưởng nhiều subsystem cùng lúc:
- thêm task mới
- mở dashboard
- focus mode
- cân bằng workload
- hoàn thành task

Các usecase này là ứng viên chính để mô tả chi tiết trong tài liệu luồng hệ thống.

## 10. Kết luận
Codebase hiện tại xoay quanh một trục rõ ràng: dữ liệu học tập đi vào hệ thống, decision engine và pipeline biến nó thành ưu tiên / lịch / cảnh báo, sau đó UI hiển thị kết quả và người dùng tương tác ngược lại bằng focus mode, lưu tiến trình và chỉnh sửa task.
