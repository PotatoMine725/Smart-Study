# Plan: Analytics Page UI Redesign

## Context

Trang Analytics hiện tại dùng layout cũ: icon Segoe MDL2, header căn giữa, card filter nằm ngang, narrative và productivity score tách riêng hai card. Thiếu hierarchy typography và design language nhất quán với Dashboard đã redesign.

Thư mục `Analytics_Redesign/` chứa toàn bộ tài liệu sẵn: XAML mới + styles + design spec. Nhiệm vụ là tích hợp các file đó vào project — không viết XAML mới, không thay đổi ViewModel hay code-behind.

## Phạm vi thay đổi

| File | Hành động |
|------|-----------|
| `SmartStudyPlanner/Themes/AnalyticsStyles.xaml` | **Tạo mới** — copy từ `Analytics_Redesign/xaml/Themes/AnalyticsStyles.xaml` |
| `SmartStudyPlanner/App.xaml` | **Sửa** — thêm 1 dòng ResourceDictionary |
| `SmartStudyPlanner/Views/AnalyticsPage.xaml` | **Thay toàn bộ** — copy từ `Analytics_Redesign/xaml/Views/AnalyticsPage.xaml` |

## Không thay đổi

- `SubjectPalette.xaml` — đã có đầy đủ `SubjectColor1-6` + `SeverityUrgent/Warn/Safe/Done/High` (từ Dashboard redesign)
- `AnalyticsPage.xaml.cs` — code-behind giữ nguyên hoàn toàn
- `AnalyticsViewModel.cs` — tất cả 14 binding được giữ nguyên trong XAML mới
- Toàn bộ services, models, converters — không đụng đến

## Bước thực hiện

### 1. Tạo `Themes/AnalyticsStyles.xaml`

Copy nguyên văn nội dung từ `Analytics_Redesign/xaml/Themes/AnalyticsStyles.xaml` vào `SmartStudyPlanner/Themes/AnalyticsStyles.xaml`.

File định nghĩa các style keys: `AnCard`, `AnPanel`, `AnEyebrow`, `AnPageTitle`, `AnSubText`, `AnSectionTick`, `AnSectionTitle`, `AnSectionSub`, `AnFieldLabel`, `AnValueLarge`, `AnValueSmall`, `AnSolidButton`, `AnGhostButton`, `AnDataGridHeader`, `AnDataGridRow`, `AnDataGridCell`, `AnDataGrid`.

Tất cả màu dùng `{DynamicResource ...}` → follow theme tự động. SDK-style csproj tự include XAML — không cần đăng ký `.csproj`.

### 2. Cập nhật `App.xaml`

Thêm sau dòng `DashboardStyles.xaml`:

```xml
<ResourceDictionary Source="pack://application:,,,/Themes/AnalyticsStyles.xaml"/>
```

`SubjectPalette.xaml` đã có sẵn ở dòng 12 — bỏ qua.

### 3. Thay `Views/AnalyticsPage.xaml`

Copy nguyên văn nội dung từ `Analytics_Redesign/xaml/Views/AnalyticsPage.xaml` vào `SmartStudyPlanner/Views/AnalyticsPage.xaml`.

Layout mới (so với hiện tại):
- **Header** — eyebrow "PHÂN TÍCH HỌC TẬP" + title 24px + subtitle, filters xếp phải
- **Narrative Hero** — 2-column card: story (trái) + productivity score (phải, số lớn 46px)
- **Band A** — 2-column 7*/5*: weekly chart + subject completion chart (giữ LiveCharts bindings)
- **Heatmap** — giữ nguyên 7×52 UniformGrid + `HeatLevelToBrushConverter`
- **DataGrid** — style `AnDataGrid`, cùng columns và bindings cũ

`AppEmptyStateCard` (loading/empty states) đã tồn tại trong `CommonStyles.xaml:55`.

## Verification

1. `dotnet build` — build clean, không XAML compile error
2. Chạy app → click Analytics → kiểm tra:
   - Header typography (eyebrow + title + subtitle)
   - Narrative hero card: WeeklyNarrative + ProductivityValue
   - 2 chart panel cạnh nhau
   - Heatmap 52 tuần render đúng màu
   - DataGrid hiển thị SubjectInsights
3. Thử đổi filter → data cập nhật bình thường
