# Sửa thuật toán Cân Bằng Tải: rải đều thật sự thay vì "least-loaded full-chunk"

> Plan ngày 2026-06-30 · nhánh `ui_rf` · tầng Service (logic nghiệp vụ)

## Context — vì sao sửa

Trên màn **Cân Bằng Tải** (cap = 2h/ngày = 120p), thuật toán cho ra lịch phân mảnh:

| Ngày | Tải | |
|---|---|---|
| Hôm nay | 120 (đầy) | K5 Phần 1 |
| Ngày mai | **45** (dư 75!) | K5 Phần 2 |
| 02/07 | 120 (đầy) | K4 Phần 1 |
| 03/07 | 30 | K4 Phần 2 |

"Ngày mai" còn trống 75p nhưng không task nào được ghép vào; task tiếp theo lại
nhảy sang ngày trống phía sau → lịch full/half/full/half xen kẽ, lãng phí cap.

**Đây là LỖI LOGIC, không phải chủ ý** (đã xác nhận với người dùng). Bằng chứng:
kết quả không khớp **cả hai** ý đồ có thể có — "dồn sớm" phải là 120/120/75, còn
"cân bằng đều" (315p / 7 ≈ 45p/ngày) thì lại càng sai. Người dùng chọn hướng sửa:
**cân bằng đều thật sự** (mỗi ngày một mức tải gần bằng nhau).

## Nguyên nhân gốc (đã truy vết)

`SmartStudyPlanner/Services/WorkloadServiceImpl.cs` — hàm `GenerateSchedule`, vòng
gán task (dòng 67–106). Hai chỗ kết hợp gây lỗi:

- **Dòng 77–79** chọn ngày theo `.Where(d => d.TotalMinutes < capacityMinutes).OrderBy(d => d.TotalMinutes)`
  → **ngày RỖNG (0p) luôn xếp trước ngày đã dùng dở (45p)**. Nên khi còn bất kỳ
  ngày trống nào trong cửa sổ, khoảng trống của "Ngày mai" KHÔNG bao giờ được lấp.
- **Dòng 93–94** `chunk = Math.Min(remaining, spaceLeft)` → đổ nguyên một khối tới
  120p vào ngày được chọn. Heuristic "least-loaded" vốn để cân bằng, nhưng với task
  **chia nhỏ được** + khối đầy-cap thì nó không cân bằng mà phân mảnh.

Truy vết tay tái hiện đúng ảnh: K5 (cần ~165p) → 120 hôm nay + 45 ngày mai; rồi K4
(cần ~150p) thấy ngày 02/07 (0p) "nhẹ tải hơn" ngày mai (45p) nên đổ 120 vào 02/07.

**Phạm vi lỗi:** chỉ 1 thuật toán duy nhất. `BalanceWorkloadStage` (pipeline) chỉ
gọi lại `IWorkloadService.GenerateSchedule` (cùng `WorkloadServiceImpl`). Sửa ở đây
là sửa cả hai lối vào (màn Cân Bằng Tải + pipeline).

## Đã xác minh

- **Không test nào chốt hành vi hiện tại** của `WorkloadServiceImpl.GenerateSchedule`
  — trong `SmartStudyPlanner.Tests` chỉ có 1 *stub* `GenerateSchedule` (PipelineStageTests.cs:45).
  Sửa thuật toán **không phá contract test nào**.
- `ScheduleDay`: `Date`, `DisplayName`, `TotalMinutes`, `Tasks` (`List<ScheduledTask>`),
  `HeaderText` (dẫn xuất). `ScheduledTask`: `TenTask`, `TenMon`, `SoPhut`.
- ViewModel `BuildSchedule` lọc bỏ ngày rỗng (`if (day.Tasks.Count > 0)`), nên các
  ngày trống dư ra trong danh sách 7 ngày sẽ tự ẩn — **không cần** đổi ViewModel/View.
- Phần thu thập + sắp task theo `DiemUuTien` (dòng 43–56) và dựng 7 ngày có tên
  "Hôm nay/Ngày mai/dd/MM/yyyy" (dòng 57–65) **giữ nguyên**.

## Thiết kế thuật toán mới (thay vòng dòng 67–106)

Ý tưởng: tính **mức tải mục tiêu mỗi ngày** rồi **lấp tuần tự từ ngày sớm → muộn**,
mỗi ngày chỉ nhận đến mục tiêu, chỉ chia task khi vượt ranh giới ngày. Vừa cân bằng
đều, vừa hạn chế phân mảnh, vừa tôn trọng ưu tiên (task ưu tiên cao xếp trước nên
rơi vào ngày sớm).

```
const int MinSession = 30;                  // phiên học tối thiểu — chống "xé vụn"
int total = Σ minutesNeeded (tất cả task);  // minutesNeeded = RawSuggested - ThoiGianDaHoc, bỏ ≤0
if (total == 0) return days;                // không có gì để xếp

int idealDays    = 7;                                  // cửa sổ tuần đang hiển thị
int targetPerDay = Clamp(CeilDiv(total, idealDays), MinSession, capacityMinutes);
//  - không thấp hơn MinSession  → tổng nhỏ không bị rải thành lát 5–10p
//  - không cao hơn cap          → không tràn
int numDays = Max(idealDays, CeilDiv(total, targetPerDay));  // quá tải thì kéo dài thêm ngày
```

Lấp tuần tự, **không quay lui** (con trỏ ngày chỉ tiến):

```
cursor = 0
foreach (task in sortedTasks)            // đã sort theo DiemUuTien giảm dần
    rem = minutesNeeded(task)
    pieces = []                          // (dayIndex, minutes) của riêng task này
    while rem > 0:
        bảo đảm days[cursor] tồn tại (tạo ngày dd/MM/yyyy nếu cursor >= days.Count)
        space = targetPerDay - days[cursor].TotalMinutes   // target ≤ cap nên luôn ≤ chỗ trống thật
        if space <= 0: cursor++; continue
        chunk = Min(rem, space)
        days[cursor].TotalMinutes += chunk
        pieces.Add((cursor, chunk)); rem -= chunk
        if days[cursor].TotalMinutes >= targetPerDay: cursor++   // ngày đạt mục tiêu → khoá, sang ngày sau
    // phát ScheduledTask: 1 mảnh/ngày; nếu task trải >1 ngày thì đánh "(Phần n)"
    foreach ((dayIndex, minutes), idx) in pieces:
        name = pieces.Count > 1 ? $"{task.TenTask} (Phần {idx+1})" : task.TenTask
        days[dayIndex].Tasks.Add(new ScheduledTask { TenTask=name, TenMon=..., SoPhut=minutes })
```

Tiện ích: `CeilDiv(a,b) = (a + b - 1) / b`. `MinSession` để là **hằng số có tên**
(có thể nâng thành tham số/cấu hình sau).

### Kiểm chứng bằng tay (đúng số trong ảnh)

`total = 315`, `target = ceil(315/7) = 45`, `numDays = 7`:

| Ngày | Tải | Nội dung |
|---|---|---|
| Hôm nay | 45 | K5 (Phần 1) 45 |
| Ngày mai | 45 | K5 (Phần 2) 45 |
| 02/07 | 45 | K5 (Phần 3) 45 |
| 03/07 | 45 | K5 (Phần 4) 30 · K4 (Phần 1) 15 |
| 04/07 | 45 | K4 (Phần 2) 45 |
| 05/07 | 45 | K4 (Phần 3) 45 |
| 06/07 | 45 | K4 (Phần 4) 45 |

→ **7 ngày đều 45p**, không còn ngày đầy/ngày hụt xen kẽ. Đúng yêu cầu "cân bằng đều".

### Các ca biên đã tính tới

- **Tổng nhỏ** (vd 1 task 30p): `target = max(ceil(30/7), 30) = 30`, `numDays = 1`
  → dồn 30p vào hôm nay, **không** xé thành lát 5p × 6 ngày (nhờ `MinSession`).
- **Quá tải** (total > 7×cap): `target = cap`, `numDays = ceil(total/cap) > 7`
  → lấp đầy cap mỗi ngày rồi **kéo dài thêm ngày** (giữ hành vi mở rộng cũ).
- **Ngày dư cuối** có thể < target (phần còn lại) — chấp nhận được.

## Đánh đổi cần người dùng biết (không chặn)

- **Cân bằng đều ⇒ chia nhỏ task nhiều hơn**: task dài sẽ thành nhiều "(Phần n)" rải
  qua nhiều ngày (K5 thành 4 phần ở ví dụ trên). Đây là bản chất của "rải đều", không
  phải lỗi. `MinSession=30` đặt sàn để mảnh không quá vụn; có thể chỉnh sau.
- **Tải dồn về đầu tuần khi quá tải**: chỉ khi total > 7×cap mới lấp đầy cap; bình
  thường mọi ngày ở mức target < cap.

## Phạm vi thay đổi

| File | Hành động |
|---|---|
| `SmartStudyPlanner/Services/WorkloadServiceImpl.cs` | Thay **vòng gán task (dòng 67–106)** bằng thuật toán target-fill ở trên. Giữ nguyên thu thập/sort task (43–56), dựng 7 ngày (57–65), `return days` (108). Thêm helper `CeilDiv` private + hằng `MinSession`. |

**Không đụng:** `ScheduleModels.cs`, `IWorkloadService` (chữ ký giữ nguyên),
`BalanceWorkloadStage.cs`, `WorkloadBalancerViewModel.cs`, View/Page, converters.

## Verification

1. `rtk dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj` — build sạch.
2. `rtk dotnet test SmartStudyPlanner.Tests` — xanh (không test nào chốt hành vi cũ).
3. Chạy app → màn **Cân Bằng Tải**, đặt cap = 2h, dữ liệu như ảnh:
   - Biểu đồ cột **đều nhau** quanh vạch nét đứt, **không** còn cột đầy xen cột hụt.
   - "Ngày mai" không còn bỏ trống 75p; mọi ngày có tải ≈ target.
4. Đổi cap (1h, 4h) và đổi số lượng task để soát ca **tổng nhỏ** (1 task ngắn → dồn
   1 ngày, không xé vụn) và ca **quá tải** (nhiều task → lấp đầy cap rồi kéo dài ngày).
5. (Khuyến nghị) Thêm test mới cho `WorkloadServiceImpl.GenerateSchedule`: dựng
   `HocKy` 2 task tổng 315p, cap 2h, assert mọi `ScheduleDay.TotalMinutes` lệch nhau
   ≤ 1 phiên và **không** ngày nào < target khi vẫn còn task chưa xếp.
6. `gitnexus_detect_changes()` trước commit: chỉ `WorkloadServiceImpl.GenerateSchedule`
   (và flow của nó) bị ảnh hưởng.
