# Workload Balancer: Window → Page + cửa sổ chính mở Maximized

> Plan ngày 2026-06-30 · nhánh `ui_rf` · tiếp nối sau khi redesign Cân Bằng Tải đã ship (commit `1f6aa50`)

## Context

Sau khi gói redesign **Cân Bằng Tải** đã ship, màn hình này vẫn là một **cửa sổ độc lập**
(`WorkloadBalancerWindow : Window`) mở bằng `.Show()` từ sidebar — khác với Dashboard /
Analytics vốn là **`Page`** điều hướng trong `MainFrame`. Điều này tạo trải nghiệm rời rạc:
cửa sổ nổi riêng, có badge "Mở", không nằm trong luồng điều hướng thống nhất.

Mục tiêu:
1. **Chuyển Cân Bằng Tải từ `Window` → `Page`**, điều hướng trong `MainFrame` y như
   `DashboardPage` / `AnalyticsPage`.
2. **Cửa sổ chính (`MainWindow`) tự mở Maximized khi chạy** (giữ thanh tiêu đề + nút X;
   không borderless, để không phá luồng thu nhỏ xuống khay khi bấm X).

Khác với task trước ("chỉ tầng views"), task này **bắt buộc đụng code-behind + 1 sửa nhỏ
ViewModel** — đó chính là nội dung của việc chuyển đổi, không phải phát sinh ngoài ý muốn.

## Đã xác minh

- **Mẫu Page chuẩn** (`DashboardPage.xaml.cs`, `AnalyticsPage.xaml.cs`): `: Page`, có
  `public HocKy HocKy`, constructor `(HocKy)` set `DataContext`, `Page_Loaded` nạp dữ liệu.
  Điều hướng: `MainFrame.Navigate(new XxxPage(_currentHocKy))` + `SetActiveNav(NavXxx)`.
- **Lối vào duy nhất còn sống** của Workload là sidebar `NavWorkload_Click`
  (`MainWindow.xaml.cs:195`). `DashboardViewModel.MoWorkloadBalancerCommand` (dòng 355–362)
  là **dead code** — `grep` toàn repo không có XAML nào bind `MoWorkloadBalancerCommand`.
- **Bẫy 1 — binding sẽ vỡ âm thầm:** view dùng `RelativeSource AncestorType=Window` **6 lần**
  để lấy `DataContext.CapacityHours` (biểu đồ cột + meter + nhãn "đầy"). Khi là `Page` host
  trong `Frame`, ancestor `Window` là `MainWindow` (DataContext null) → binding rỗng, cột/meter
  biến mất. Phải đổi hết thành `AncestorType=Page`.
- **Bẫy 2 — MessageBox bật mỗi lần điều hướng:** `WorkloadBalancerViewModel` constructor gọi
  `GenerateSchedule()` (dòng 27), mà hàm này **bật `MessageBox`** (dòng 43). Là Window dialog
  thì chỉ 1 lần; là Page thì **mỗi lần bấm nav** đều dựng lại VM → popup modal. Phải tách
  MessageBox ra khỏi đường khởi tạo.
- **Bẫy 3 — khôi phục từ khay:** `MainWindow.xaml.cs:152` (`HienThiUngDung`) ép
  `WindowState = Normal`. Nếu chỉ đặt Maximized trong XAML, app mở maximized nhưng **bung về
  Normal mỗi lần mở lại từ khay** (luồng bấm X → thu nhỏ). Phải đổi dòng 152 → `Maximized`.
- **Tiền lệ fullscreen:** `FocusWindow.xaml` dùng `Maximized + WindowStyle=None` (borderless).
  Ta **không** dùng None cho MainWindow vì sẽ mất nút X mà `OnClosing` đang dựa vào.
- `WeightOptimizerWindow` **giữ nguyên** là Window — người dùng chỉ yêu cầu Workload.
- csproj SDK-style auto-glob: đổi tên / thêm file `.xaml`+`.cs` không cần sửa csproj.

## Phạm vi thay đổi

| File | Hành động |
|---|---|
| `Views/WorkloadBalancerWindow.xaml` → `Views/WorkloadBalancerPage.xaml` | Đổi tên file; root `<Window>`→`<Page>`; `x:Class`→`WorkloadBalancerPage`; bỏ thuộc tính Window-only; `Window.Resources`→`Page.Resources`; 6× `AncestorType=Window`→`AncestorType=Page` |
| `Views/WorkloadBalancerWindow.xaml.cs` → `Views/WorkloadBalancerPage.xaml.cs` | Đổi tên; class `: Page`; thêm `public HocKy HocKy`; (tùy) `Page_Loaded` |
| `Views/MainWindow.xaml.cs` | `NavWorkload_Click` → điều hướng Page; bỏ field `_workloadWindow` + logic badge; dòng 152 `Normal`→`Maximized` |
| `Views/MainWindow.xaml` | `WindowState="Maximized"`; bỏ `WorkloadOpenBadge` |
| `ViewModels/WorkloadBalancerViewModel.cs` | Tách MessageBox khỏi đường khởi tạo (xem dưới) |
| `ViewModels/DashboardViewModel.cs` | Xóa dead command `MoWorkloadBalancer` (dòng 355–362) + `using` thừa nếu có |

**Không đụng:** `ScheduleModels.cs`, `IWorkloadService`/service, `WorkloadConverters.cs`,
`WorkloadStyles.xaml`, `SubjectPalette.xaml`, `App.xaml`, `WeightOptimizerWindow`.

## Chi tiết các bước

### 1. Đổi tên symbol C# bằng gitnexus (không find-replace)
`gitnexus_rename` cho `WorkloadBalancerWindow` → `WorkloadBalancerPage`. Nó cập nhật symbol C#
+ tham chiếu (`MainWindow._workloadWindow`, dead command trong `DashboardViewModel`). **Không**
tự lật `<Window>`→`<Page>`, `x:Class`, hay đổi tên file `.xaml` — các phần đó làm tay (bước 2).
Chạy `gitnexus_impact` trước khi đổi để báo blast radius.

### 2. View: WorkloadBalancerPage.xaml
- Đổi tên cả 2 file `.xaml` + `.xaml.cs` sang `WorkloadBalancerPage`.
- Root `<Window …>` → `<Page …>`; `x:Class="SmartStudyPlanner.Views.WorkloadBalancerPage"`.
- **Bỏ** thuộc tính chỉ-Window: `Title`, `Height`, `Width`, `MinWidth`, `MinHeight`, `Icon`,
  `WindowStartupLocation`. Giữ `Background="{DynamicResource AppBackground}"` (Page có Background).
- `<Window.Resources>`→`<Page.Resources>` (giữ nguyên merge `WorkloadStyles.xaml` + 4 converter).
- **Đổi cả 6 chỗ** `RelativeSource={RelativeSource AncestorType=Window}` →
  `AncestorType=Page` (3 trong biểu đồ cột, 3 trong meter/nhãn "ĐÃ ĐẠT MỨC TỐI ĐA").
- `</Window>` → `</Page>`.

### 3. Code-behind: WorkloadBalancerPage.xaml.cs
Theo đúng mẫu `AnalyticsPage`:
```csharp
public partial class WorkloadBalancerPage : Page
{
    public HocKy HocKy { get; }
    public WorkloadBalancerPage(HocKy hocKy)
    {
        InitializeComponent();
        HocKy = hocKy;
        this.DataContext = new WorkloadBalancerViewModel(hocKy);
    }
}
```
(Namespace giữ `SmartStudyPlanner.Views`; bỏ `using System.Windows` nếu thừa, thêm
`System.Windows.Controls` cho `Page`.)

### 4. ViewModel: tách MessageBox khỏi khởi tạo (giữ tên command)
`[RelayCommand]` phải vẫn là method `GenerateSchedule` để `GenerateScheduleCommand` không đổi
(nút "XẾP LỊCH LẠI" đang bind nó). Tách lõi ra helper có cờ thông báo:
```csharp
public WorkloadBalancerViewModel(HocKy hocKy, IWorkloadService workloadService)
{
    _hocKy = hocKy;
    _workloadService = workloadService;
    CapacityHours = _workloadService.GetCapacity();
    BuildSchedule(notify: false);   // khởi tạo: KHÔNG popup
}

[RelayCommand]
private void GenerateSchedule() => BuildSchedule(notify: true);  // người dùng bấm: có popup

private void BuildSchedule(bool notify)
{
    _workloadService.SaveCapacity(CapacityHours);
    var generatedList = _workloadService.GenerateSchedule(_hocKy, CapacityHours);
    Schedule.Clear();
    foreach (var day in generatedList)
        if (day.Tasks.Count > 0) Schedule.Add(day);
    if (notify)
        System.Windows.MessageBox.Show(
            $"Thuật toán đã xếp lại lịch thành công với giới hạn:\n{CapacityHours} giờ/ngày!",
            "Workload Balancer");
}
```

### 5. MainWindow điều hướng (code-behind + XAML)
- `NavWorkload_Click`: thay khối mở-window + badge bằng mẫu nav chuẩn:
  ```csharp
  private void NavWorkload_Click(object sender, RoutedEventArgs e)
  {
      if (_currentHocKy == null) return;
      _telemetry.Track("click_nav_workload");
      SetActiveNav(NavWorkload);
      MainFrame.Navigate(new WorkloadBalancerPage(_currentHocKy));
  }
  ```
  (`NavWorkload` đã có sẵn trong danh sách `SetActiveNav`.)
- Xóa field `private WorkloadBalancerWindow? _workloadWindow;` (dòng 27) và mọi tham chiếu
  `WorkloadOpenBadge`.
- `MainWindow.xaml`: xóa `<Border x:Name="WorkloadOpenBadge" …>` trong nút NavWorkload.

### 6. Cửa sổ chính mở Maximized + giữ Maximized khi khôi phục từ khay
- `MainWindow.xaml`: thêm `WindowState="Maximized"` (giữ `WindowStartupLocation`, chrome mặc
  định, **không** `WindowStyle=None`).
- `MainWindow.xaml.cs:152` trong `HienThiUngDung`: `this.WindowState = WindowState.Maximized;`

### 7. Dọn dead code
Xóa command `MoWorkloadBalancer` (`DashboardViewModel.cs:355–362`) — không UI nào bind, và nó
là thứ duy nhất còn giữ tham chiếu tới type Window cũ. Bỏ `using` thừa nếu phát sinh.

## Verification (runtime)

1. `rtk dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj` — build sạch, 0 XAML parse error,
   không còn tham chiếu `WorkloadBalancerWindow`.
2. Chạy app → **cửa sổ mở Maximized** ngay, vẫn thấy thanh tiêu đề + nút X.
3. Bấm nút X → thu nhỏ xuống khay (luồng cũ còn nguyên) → double-click icon khay → **mở lại
   vẫn Maximized** (xác nhận fix dòng 152).
4. Bấm **Cân Bằng Tải** ở sidebar: trang hiện trong khung nội dung, nav được tô active.
   Bấm qua lại 2–3 lần → **không** popup MessageBox lặp (xác nhận fix MessageBox).
5. Trên trang: **biểu đồ cột + thanh meter render đúng** (xác nhận fix `AncestorType=Page`);
   cột chạm vạch nét đứt → đỏ + nhãn "ĐÃ ĐẠT MỨC TỐI ĐA"; chip task đúng màu môn.
6. Bấm **XẾP LỊCH LẠI** → popup xác nhận hiện (đường lệnh vẫn báo); biểu đồ/thẻ cập nhật.
7. Điều hướng Dashboard → Workload → Analytics → Workload: dữ liệu vẫn đúng.
8. Toggle **Dark ↔ Light** khi đang ở trang Workload: mọi bề mặt/chip/thanh tải đúng cả 2 theme.
9. `gitnexus_detect_changes()` trước commit: chỉ các symbol/flow dự kiến bị ảnh hưởng.
