# Dashboard Redesign — layout mission-control + chart native XAML

## Goal
Thay trang Dashboard sang layout **mission-control** (header band → bảng deadline
full-width → 2 dải phân tích → dải gợi ý), palette mở rộng (mỗi môn 1 màu + dải
severity), **bỏ icon**, khung cố định chống "UI bloat", Dark/Light qua `DynamicResource`.
Chart vẽ **native XAML** (Rectangle/Border/Path) thay LiveCharts → đổi màu theo theme,
VM thuần data. Nguồn thiết kế: `Dashboard Redesign/` (DESIGN_SPEC + demo HTML + gói 4 file).

Ship xong = mở Dashboard thấy layout mới, chart native đúng tỉ lệ/màu, toggle Dark/Light
đổi màu cả chart, không crash; Analytics không bị ảnh hưởng.

## Status
`draft` — chờ duyệt.

## Quyết định đã chốt
- **Phạm vi:** CHỈ trang Dashboard. Không đụng sidebar/MainWindow/trang khác.
- **Chart:** native XAML, **bỏ LiveCharts khỏi Dashboard**. VM phát data thuần (số + nhãn
  + key), View lo render + màu.
- **KHÔNG thêm NuGet chart mới.** LiveCharts/SkiaSharp **ở lại** (Analytics còn dùng:
  `AnalyticsViewModel` + `AnalyticsPage.xaml`) → không gỡ NuGet.

### Lợi ích thật (không phải "nhẹ hơn" — Skia vẫn ship vì Analytics)
1. **Theme-reactive:** paint LiveCharts set cứng 1 lần trong VM, không đổi khi toggle
   Dark/Light. Chart native dùng `DynamicResource` → đổi theme là đổi màu (đúng spec).
2. **VM UI-agnostic** → migration sau (vd Avalonia) rẻ.
3. **Khớp mockup HTML** pixel-perfect; sửa luôn lỗi cột khối lượng 1-màu-xanh.

### Đã kiểm chứng (2 Explore agent + đọc trực tiếp)
- Binding 100% khớp VM + `TaskDashboardItem`/`ScheduledTask`/`AdaptationSuggestion`;
  `MucDoCanhBao` khớp DataTrigger; emoji đã có `StripLeadingGlyphConverter`.
- 15 theme token đủ trong Light + Dark; `ThemeManager` swap theo source-string.
- 5 converter gói không trùng tên (0 collision); `AppEmptyStateCard` + implicit `Button` tồn tại.
- csproj SDK-style, default items on → file mới tự include.
- `DashboardSummary` đã có đủ số liệu thô → chỉ reshape, không sửa logic thu thập.
- **Defect gói:** `DashboardPage.xaml:136` dùng `{StaticResource {x:Type TextBlock}}` →
  không resolve (app không có implicit TextBlock style) → crash. Phải sửa.

---

## Slice list

### Slice 1 — Foundation: converters + palette + styles
Additive, app cũ vẫn build/chạy.
- TẠO `Converters/DashboardConverters.cs` (copy từ gói; namespace `SmartStudyPlanner.Converters`).
- TẠO `Themes/SubjectPalette.xaml`, `Themes/DashboardStyles.xaml` (copy từ gói).
- SỬA `App.xaml`: append **sau** `CommonStyles.xaml` (giữ `LightTheme.xaml` index 0), pack URI:
  ```xml
  <ResourceDictionary Source="pack://application:,,,/Themes/SubjectPalette.xaml"/>
  <ResourceDictionary Source="pack://application:,,,/Themes/DashboardStyles.xaml"/>
  ```
- **Exit:** `dotnet build` xanh; app chạy, Dashboard cũ không đổi.

### Slice 2 — DonutChart UserControl + RatioToLengthConverter
Additive (chưa dùng), build được.
- TẠO `Controls/DonutChart.xaml(.cs)` — donut **stroked-arc** native. Control tự lo toàn bộ
  toán arc: nhận `Segments` ({Key,Count}), tự tính tổng → fraction → geometry → số ở tâm.
  - Mỗi segment = `Path` + 1 `ArcSegment` trên đường tròn tâm: `Fill=null`,
    `Stroke=<brush theo Key>`, `StrokeThickness=<độ dày vòng>`. `θ→(cx+R·cosθ, cy+R·sinθ)`,
    bắt đầu −90°, `IsLargeArc = sweep>180°`, clockwise. Màu theo Key resolve từ `SubjectPalette`
    (`SeverityUrgent/Warn/Safe/Done`) qua `FindResource`.
  - **4 edge case BẮT BUỘC:** (1) tổng=0 → vòng rỗng, không chia 0; (2) 1 segment=100% →
    `ArcSegment` 360° suy biến → dùng `Ellipse` stroked; (3) count=0 → bỏ qua (không cộng góc,
    không sliver); (4) làm tròn → segment cuối **đóng vòng**, không tin `Σfraction==1.0`.
- THÊM vào `DashboardConverters.cs`: `RatioToLengthConverter : IMultiValueConverter`
  (values: value, max; param: track) → `track*clamp(value/max,0,1)`; **guard max=0 → 0** (tránh NaN).
- **Exit:** `dotnet build` xanh.

### Slice 3 — Swap Dashboard sang native (VM + Page, atomic)
VM và Page đổi cùng lúc để nhất quán.
- SỬA `ViewModels/DashboardViewModel.cs`:
  - Bỏ `using LiveChartsCore...`; xóa 5 prop (dòng 38-42): `bieuDoTrangThai`, `bieuDoMonHoc`,
    `trucXMonHoc`, `bieuDoThoiGian`, `trucXThoiGian`.
  - Thêm data thuần: `ObservableCollection<StatusSegment> TrangThaiSegments`
    ({Key∈{Urgent,Warn,Safe,Done}, Label, Count}); `ObservableCollection<SubjectTimeProgress>
    TienDoThoiGian` ({Subject, Expected, Actual}) + `double MaxThoiGian`;
    `ObservableCollection<SubjectWorkload> KhoiLuongMonHoc` ({Subject, Count}) + `int MaxKhoiLuong`.
  - Đổi `ApplyCharts` → `ApplyChartData`: nạp các collection từ `summary`. KHÔNG để Brush trong
    VM; KHÔNG pre-compute fraction (DonutChart tự tính).
  - Thêm 3 record/class public `StatusSegment`/`SubjectTimeProgress`/`SubjectWorkload`.
- THAY `Views/DashboardPage.xaml` (layout gói, 2 khối chart đổi sang native):
  - Giữ `x:Class`, `Loaded="Page_Loaded"`; bỏ `xmlns:lvc`. **Sửa defect dòng 136**.
  - Band A trái: `<ctl:DonutChart Segments="{Binding TrangThaiSegments}"/>` + legend ItemsControl.
  - Band A phải: ItemsControl grouped bars trên `TienDoThoiGian` (2 Rectangle/cột; height qua
    `RatioToLengthConverter` với `MaxThoiGian`) + nhãn trục X.
  - Band B trái: ItemsControl horizontal bars trên `KhoiLuongMonHoc` (width qua
    `RatioToLengthConverter` với `MaxKhoiLuong`; màu fill = `SubjectToBrushConverter`).
  - Band B phải (lịch) / Adaptation / KPI / bảng Top-5: theo gói.
  - **Nhãn trục X môn:** `SubjectLabels` là tên đầy đủ; truncate + ToolTip tên đầy đủ.
- GIỮ NGUYÊN `Views/DashboardPage.xaml.cs`.
- **Exit:** build + run; Dashboard layout mới, chart native đúng tỉ lệ/màu, toggle theme đổi màu
  cả chart, không crash; 4 edge case donut OK; Analytics vẫn chạy.

---

## Pre-edit checklist
- Slice 3 sửa symbol `DashboardViewModel.ApplyCharts` + 5 prop chart. Chạy
  `gitnexus_impact({target:"ApplyCharts", direction:"upstream"})` và xác nhận blast-radius.
- **Đánh giá sơ bộ (từ grep):** 5 prop chart chỉ được bind trong `DashboardPage.xaml`;
  Analytics dùng prop riêng. Blast-radius **giới hạn trong Dashboard** → **risk MEDIUM**
  (không CRITICAL/HIGH). Không có caller ngoài trang Dashboard.
- Chạy `gitnexus_detect_changes()` trước mỗi commit để xác nhận chỉ chạm symbol dự kiến.

## Acceptance gates
- `dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj` — 0 error/warning XAML, không còn `lvc:`/LiveCharts trong Dashboard.
- `dotnet test` — không hồi quy (lưu ý 2 test DecisionEngine date-fragile đã biết, không liên quan).
- `gitnexus_detect_changes()` — scope khớp slice.
- Run tay: 4 edge case donut + toggle Dark/Light đổi màu chart + Analytics còn chạy + đối chiếu `Dashboard Redesign.html`.

## Out of scope
- Sidebar/MainWindow/các trang khác (Analytics, Môn Học, Cân Bằng Tải, Trọng Số AI).
- Gỡ NuGet LiveCharts/SkiaSharp (Analytics còn dùng).
- Migration framework (Avalonia/Blazor Hybrid) — việc riêng sau này; bây giờ chỉ giữ VM thuần
  data, KHÔNG thêm lớp abstraction cho framework tương lai.
- Sửa logic thu thập dữ liệu / pipeline / ML.
