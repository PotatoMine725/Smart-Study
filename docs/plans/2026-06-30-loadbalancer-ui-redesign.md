# Redesign giao diện Cân Bằng Tải (Workload Balancer)

> Plan ngày 2026-06-30 · nhánh `ui_rf` · tầng views

## Context

Màn hình **Workload Balancer** hiện tại (`Views/WorkloadBalancerWindow.xaml`) là một hộp
thoại 500×650 chật chội: tiêu đề có emoji `⚖️`, màu hard-code (`#8E44AD`, `#2980B9`,
`#27AE60`…) không theo theme, danh sách ngày chỉ là text thô, **không** trực quan hoá được
khái niệm "cân bằng tải". Trong `LoadBalancer_redesign/` đã có sẵn một **gói redesign hoàn
chỉnh** (XAML view mới + 3 converter + style dictionary + DESIGN_SPEC) đồng bộ hệ màu /
no-icon / Dark–Light với Dashboard & Analytics đã ship.

Công việc này là **tích hợp gói redesign đã có** vào app, hoạt động ở **tầng views**:
thay file view, thêm style + converter trình bày. **Không** sửa ViewModel / Model / Service,
**không** sửa code-behind của cửa sổ. Kết quả: bố cục 3 lớp (điều khiển sức học → biểu đồ
phân bổ tải → lịch chi tiết dạng thẻ), giữ nguyên 100% binding.

## Đã xác minh (không phải bàn lại)

- **Binding khớp 100%** với code hiện có:
  - `WorkloadBalancerViewModel`: `CapacityHours` (double), `Schedule`
    (`ObservableCollection<ScheduleDay>`), `GenerateScheduleCommand` (sinh từ `[RelayCommand] GenerateSchedule`).
  - `ScheduleDay`: `DisplayName`, `TotalMinutes`, `HeaderText`, `Tasks`.
  - `ScheduledTask`: `TenTask`, `TenMon`, `SoPhut`, `ThoiGianHienThi`.
- **Code-behind không cần đụng**: `WorkloadBalancerWindow.xaml.cs` nhận `HocKy` và set
  `DataContext`; XAML mới giữ nguyên `x:Class="SmartStudyPlanner.Views.WorkloadBalancerWindow"`.
- `SubjectToBrushConverter` đã có sẵn (`Converters/DashboardConverters.cs`), nhận `string`
  (TenMon) → màu môn. Tái dùng, không khai lại.
- **Mọi theme key dùng đều tồn tại ở cả Light & Dark**: `StatCardBackground`,
  `SurfaceHover`, `SuccessColor` (LightTheme + DarkTheme), `SeverityUrgent` (SubjectPalette,
  theme-independent). `AppBackground`/`CardBackground`/`BorderColor`/`PrimaryText`/
  `SecondaryText` là chuẩn, Dashboard/Analytics đã dùng.
- **csproj SDK-style (`UseWPF=true`)**: auto-glob file `.xaml`→Page và `.cs`→Compile. Thêm
  file mới vào `Themes/` và `Converters/` **không cần** sửa csproj.
- `SubjectPalette.xaml` **đã có** trong app và đã merge trong `App.xaml` → **không copy đè**
  bản trong gói redesign.
- Khác biệt "biểu đồ xếp chồng theo môn" (HTML) vs "1 cột/ngày" (XAML): DESIGN_SPEC đã
  chốt 1 cột/ngày để bám binding; HTML chỉ là preview trình duyệt (người dùng đã xác nhận).

## Phạm vi thay đổi (tầng views)

| File | Hành động |
|---|---|
| `SmartStudyPlanner/Views/WorkloadBalancerWindow.xaml` | **Thay** bằng bản redesign (có chỉnh `Window.Resources`, xem dưới) |
| `SmartStudyPlanner/Themes/WorkloadStyles.xaml` | **Thêm mới** (copy từ gói) — style `Wb*` + `PlanAccent` |
| `SmartStudyPlanner/Converters/WorkloadConverters.cs` | **Thêm mới** (copy từ gói) — 3 converter tải/ngày |

**Ngoại lệ duy nhất ngoài XAML thuần:** `WorkloadConverters.cs` là file C# mới. Đây là
*converter trình bày* (`IMultiValueConverter` bắt buộc phải là code, không thể viết trong
XAML), cùng pattern với `DashboardConverters.cs` đã có — **không** phải code-behind của
view, **không** đụng logic nghiệp vụ.

**Không đụng:** `WorkloadBalancerWindow.xaml.cs`, `WorkloadBalancerViewModel.cs`,
`ScheduleModels.cs`, services, `SubjectPalette.xaml`, **và cả `App.xaml`** (xem dưới).

## Khác biệt so với DESIGN_SPEC: giữ style ở phạm vi cửa sổ, không sửa App.xaml

DESIGN_SPEC đề xuất merge `WorkloadStyles.xaml` trong `App.xaml`. Để bám sát yêu cầu
"hoạt động ở tầng views" và vì `PlanAccent` + các style `Wb*` **chỉ** dùng cho cửa sổ này,
ta merge `WorkloadStyles.xaml` ngay trong `Window.Resources` của view thay vì App.xaml.
Cách này gọn hơn và giữ thay đổi nằm trong đúng file view.

Cụ thể, sửa block `Window.Resources` của bản redesign từ:

```xml
<Window.Resources>
    <conv:SubjectToBrushConverter x:Key="SubjBrush"/>
    <conv:LoadToLengthConverter x:Key="LoadLen"/>
    <conv:LoadToBrushConverter x:Key="LoadBrush"/>
    <conv:FullDayToVisibilityConverter x:Key="FullVis"/>
</Window.Resources>
```

thành:

```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/Themes/WorkloadStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        <conv:SubjectToBrushConverter x:Key="SubjBrush"/>
        <conv:LoadToLengthConverter x:Key="LoadLen"/>
        <conv:LoadToBrushConverter x:Key="LoadBrush"/>
        <conv:FullDayToVisibilityConverter x:Key="FullVis"/>
    </ResourceDictionary>
</Window.Resources>
```

`{StaticResource PlanAccent}` / `{StaticResource WbEyebrow}`… resolve được từ
`Window.Resources` (đã merge); `BasedOn="{StaticResource {x:Type Button}}"` và các
`{DynamicResource ...}` theme key vẫn resolve lên `App.Resources`.

> Nếu vì lý do nào đó style merge ở cửa sổ không resolve khi build (StaticResource trong
> dictionary được merged), fallback là cách của SPEC: thêm `WorkloadStyles.xaml` vào
> `App.xaml` (sau `SubjectPalette.xaml`). Chỉ dùng khi phát sinh vấn đề.

## Các bước thực hiện

1. Tạo `SmartStudyPlanner/Converters/WorkloadConverters.cs` = nội dung file trong gói
   (`LoadBalancer_redesign/xaml/Converters/WorkloadConverters.cs`), nguyên xi.
2. Tạo `SmartStudyPlanner/Themes/WorkloadStyles.xaml` = nội dung file trong gói
   (`LoadBalancer_redesign/xaml/Themes/WorkloadStyles.xaml`), nguyên xi.
3. Thay nội dung `SmartStudyPlanner/Views/WorkloadBalancerWindow.xaml` bằng bản redesign
   (`LoadBalancer_redesign/xaml/Views/WorkloadBalancerWindow.xaml`), **chỉ sửa block
   `Window.Resources`** như trên để tự merge `WorkloadStyles.xaml`.

## Verification (runtime, không chỉ static)

Lỗi còn sót lại chỉ có thể là *cosmetic* (một theme key thiếu ở 1 theme → brush rỗng), nên
phải chạy thật:

1. `rtk dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj` — build sạch, 0 XAML parse error.
2. Chạy app, mở cửa sổ **Workload Balancer** (từ MainWindow, theo flow hiện có cần một `HocKy`).
3. Kiểm tra mắt thường:
   - Biểu đồ cột scale đúng theo đường nét đứt "sức học"; ngày chạm vạch → cột đỏ
     (`SeverityUrgent`) + nhãn "ĐÃ ĐẠT MỨC TỐI ĐA".
   - Chip task có màu theo môn (`SubjectToBrushConverter`).
   - Slider 1–8 đổi `CapacityHours`; nút "XẾP LỊCH LẠI" chạy `GenerateScheduleCommand`
     (hộp thoại xác nhận hiện ra như cũ) và biểu đồ/thẻ cập nhật lại.
4. **Toggle Dark ↔ Light**: mọi bề mặt, tick section, chip, thanh tải render đúng ở cả hai
   theme (đây là điểm dễ lọt key thiếu).
