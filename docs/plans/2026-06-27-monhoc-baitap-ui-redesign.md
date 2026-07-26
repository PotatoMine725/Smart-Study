# Refactor UI — Trang "Môn học & Bài tập" (view-layer only)

## Context

Hai trang `QuanLyMonHocPage` và `QuanLyTaskPage` hiện dùng layout cũ (DataGrid trần,
emoji icon, không ăn theo hệ màu Dashboard/Analytics đã redesign). Gói thiết kế trong
`MonHoc_BaiTap_Redesign/` cung cấp bản XAML mới: trang Môn học → **thẻ môn** viền màu
nhận diện; trang Bài tập → **Smart Add hero** + bảng deadline có meter/pill, đồng bộ
ngôn ngữ thị giác với Dashboard & Analytics (no-icon, Dark/Light qua `DynamicResource`).

Mục tiêu: áp dụng layout mới **chỉ ở tầng view**, **giữ nguyên binding/command**
và **không động vào code-behind, ViewModel, Model**. File HTML trong gói chỉ là preview
(workspace master–detail hợp nhất, có shell trình duyệt) → **KHÔNG dùng** cho dự án;
ta giữ 2 trang riêng + luồng điều hướng cũ.

## Impact Analysis (blast radius: LOW)

Refactor thuần XAML, **không sửa symbol C#** nên graph code (index theo C#) không phải
nguồn chính xác — phân tích coupling trực tiếp:

| Thành phần | Tác động | Ghi chú |
|---|---|---|
| `Views/QuanLyMonHocPage.xaml` | **Thay nội dung** | DataGrid → ItemsControl thẻ môn |
| `Views/QuanLyTaskPage.xaml` | **Thay nội dung** | Thêm Smart Add hero, meter/pill |
| `App.xaml` | **+1 dòng** merge dictionary | Thêm `StudyWorkspaceStyles.xaml` |
| `Themes/StudyWorkspaceStyles.xaml` | **TẠO MỚI** | Style card/pill/datagrid/nút/input |
| `Views/*.xaml.cs` (2 file) | **KHÔNG đổi** | Ràng buộc — xem dưới |
| `ViewModels/*`, `Models/*` | **KHÔNG đổi** | Mọi binding tái dùng |
| `Themes/SubjectPalette.xaml`, `Converters/DashboardConverters.cs` | **Đã có sẵn** | Không cần copy lại |

**Ràng buộc compile-time (named elements trong code-behind):**
- `QuanLyTaskPage.xaml.cs:22` gọi `dgDanhSachTask.Items.Refresh()` → bản redesign **đã giữ**
  `x:Name="dgDanhSachTask"`.
- `QuanLyMonHocPage.xaml.cs:27` gọi `dgDanhSachMon.Items.Refresh()` → bản redesign dùng
  `ItemsControl` chưa đặt tên → đã **thêm `x:Name="dgDanhSachMon"`** cho `ItemsControl`
  (`ItemsControl.Items.Refresh()` hợp lệ khi có `ItemsSource`) → code-behind không đổi.

**Đã verify tồn tại:**
- Converters `SubjectToBrushConverter`/`PriorityToBrushConverter`/`ScoreToWidthConverter`
  (namespace `SmartStudyPlanner.Converters`).
- `SubjectPalette.xaml` đã merge trong `App.xaml`; `SeverityDone` (#7888A6) có sẵn.
- `AppEmptyStateCard` + theme keys (`StatCardBackground`, `ChartBackground`, `SurfaceHover`,
  `AccentColor`, `WarningColor`, `DangerColor`, `SuccessColor`, `BorderColor`, `PrimaryText`,
  `SecondaryText`, `AppBackground`) có trong cả Light & Dark.
- `MucDoCanhBao` có trên `StudyTask` / `QuanLyTaskViewModel`.
- Pattern `BasedOn="{StaticResource {x:Type Button}}"` đã chứng minh an toàn (CommonStyles/
  DashboardStyles/AnalyticsStyles ship cùng pattern) → không `XamlParseException` lúc khởi động.

**Thay đổi nội dung cột (không mất dữ liệu):** redesign bỏ cột `Trạng Thái`, gộp trạng thái
hoàn thành vào **pill Mức độ**. Task xong vẫn nằm trong `DanhSachTask`; `TinhDiemVaSapXep` set
`MucDoCanhBao = "Đã xong"` khi `TrangThai == HoanThanh`.

Rủi ro: **THẤP**. Coupling compile-time duy nhất là 2 `x:Name`, đều được bảo toàn.

## Implementation (đã thực hiện)

1. **Tạo `SmartStudyPlanner/Themes/StudyWorkspaceStyles.xaml`** — copy từ gói redesign.
2. **`App.xaml`** — thêm `StudyWorkspaceStyles.xaml` ngay sau `SubjectPalette.xaml`.
3. **`Views/QuanLyMonHocPage.xaml`** — bản redesign + `x:Name="dgDanhSachMon"`.
4. **`Views/QuanLyTaskPage.xaml`** — bản redesign + pill "Đã xong" → nền xám (thuần view):
   pill `Border` dùng `Style BasedOn WsPill`, Setter mặc định
   `Background = {Binding DiemUuTien, Converter=PriorityBrush}`, DataTrigger
   `MucDoCanhBao == "Đã xong"` → `Background = {StaticResource SeverityDone}`.
5. **KHÔNG sửa** code-behind / ViewModel / Model.

## Verification

1. **Build**: `dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj` — phải xanh; xác nhận
   `dgDanhSachMon`/`dgDanhSachTask` resolve.
2. **Trang Môn học**: thẻ môn viền + swatch màu ổn định; Thêm/Sửa/Xóa; nút **Tasks** điều hướng
   (`XemTaskCommand` → `OnNavigateToTask`); sửa môn xong bảng refresh.
3. **Trang Bài tập**: Smart Add (`PhanTichNhapNhanhCommand`); meter Điểm ƯT; pill Mức độ theo
   `DiemUuTien`, task đã xong → pill xám "Đã xong"; Xong/Sửa/Xóa refresh; empty state khi
   `HasData=False`; Ghi chú + Liên kết hoạt động.
4. **Đổi theme** Light ↔ Dark → cả 2 trang đổi màu bề mặt đúng.
