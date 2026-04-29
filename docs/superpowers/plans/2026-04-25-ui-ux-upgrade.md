# UI/UX Upgrade — Dashboard & Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nâng cấp toàn diện UI/UX của SmartStudyPlanner — fix dark mode, thêm sidebar navigation, thêm stat cards, thay emoji bằng vector icons, và cải thiện DataGrid.

**Architecture:** Mọi thay đổi màu sắc đi qua hệ thống `DynamicResource` token trong `LightTheme.xaml` / `DarkTheme.xaml`; không hardcode hex trong XAML view. Sidebar được thêm trực tiếp vào `MainWindow.xaml` (Grid 2 cột), navigation logic giữ trong code-behind. DataGrid bỏ full-row coloring, chuyển sang badge trong cell template.

**Tech Stack:** WPF/.NET, XAML, C#, Segoe MDL2 Assets (Windows built-in icon font), LiveCharts2 (giữ nguyên)

---

## Trạng thái hiện tại — điểm xuất phát

| File | Vấn đề |
|------|--------|
| `LightTheme.xaml` | Chỉ 5 token — thiếu accent, danger, success, chart bg, sidebar |
| `DarkTheme.xaml` | Tương tự, chỉ 5 token |
| `DashboardPage.xaml` | ~15 màu hex hardcode, emoji làm icon, DataGrid `Foreground="Black"` vỡ dark mode |
| `MainWindow.xaml` | Chỉ có `<Frame>` — không có sidebar/navigation |

---

## File Map

| File | Loại | Thay đổi |
|------|------|---------|
| `SmartStudyPlanner/Themes/LightTheme.xaml` | Modify | Thêm 14 token màu mới |
| `SmartStudyPlanner/Themes/DarkTheme.xaml` | Modify | Thêm 14 token màu mới (dark variants) |
| `SmartStudyPlanner/App.xaml` | Modify | Thêm 3 static badge brushes + global Button style |
| `SmartStudyPlanner/Views/MainWindow.xaml` | Modify | Grid 2 cột: Sidebar (220px) + Frame |
| `SmartStudyPlanner/Views/MainWindow.xaml.cs` | Modify | Thêm nav item click handlers |
| `SmartStudyPlanner/Views/DashboardPage.xaml` | Modify | Fix hardcoded hex, thay emoji, stat cards, badge column |
| `SmartStudyPlanner/ViewModels/DashboardViewModel.cs` | Modify | Thêm 2 computed property cho stat cards |

---

## Task 1: Expand Color Token System

**Files:**
- Modify: `SmartStudyPlanner/Themes/LightTheme.xaml`
- Modify: `SmartStudyPlanner/Themes/DarkTheme.xaml`

- [ ] **Step 1: Mở LightTheme.xaml và thay toàn bộ nội dung**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Base surfaces -->
    <SolidColorBrush x:Key="AppBackground"        Color="#F4F6F7"/>
    <SolidColorBrush x:Key="CardBackground"        Color="#FFFFFF"/>
    <SolidColorBrush x:Key="StatCardBackground"    Color="#FFFFFF"/>
    <SolidColorBrush x:Key="ChartBackground"       Color="#F8FAFC"/>
    <SolidColorBrush x:Key="SurfaceHover"          Color="#F1F5F9"/>

    <!-- Text -->
    <SolidColorBrush x:Key="PrimaryText"           Color="#1E293B"/>
    <SolidColorBrush x:Key="SecondaryText"         Color="#64748B"/>

    <!-- Borders -->
    <SolidColorBrush x:Key="BorderColor"           Color="#E2E8F0"/>

    <!-- Accent (blue) -->
    <SolidColorBrush x:Key="AccentColor"           Color="#2563EB"/>
    <SolidColorBrush x:Key="AccentHover"           Color="#1D4ED8"/>
    <SolidColorBrush x:Key="AccentText"            Color="#2563EB"/>

    <!-- Semantic -->
    <SolidColorBrush x:Key="DangerColor"           Color="#DC2626"/>
    <SolidColorBrush x:Key="DangerBackground"      Color="#FEE2E2"/>
    <SolidColorBrush x:Key="WarningColor"          Color="#D97706"/>
    <SolidColorBrush x:Key="WarningBackground"     Color="#FEF3C7"/>
    <SolidColorBrush x:Key="SuccessColor"          Color="#16A34A"/>
    <SolidColorBrush x:Key="SuccessBackground"     Color="#DCFCE7"/>

    <!-- Sidebar (dark sidebar on light theme — intentional) -->
    <SolidColorBrush x:Key="SidebarBackground"     Color="#1E293B"/>
    <SolidColorBrush x:Key="SidebarBorder"         Color="#334155"/>
    <SolidColorBrush x:Key="SidebarText"           Color="#94A3B8"/>
    <SolidColorBrush x:Key="SidebarActiveBackground" Color="#334155"/>
    <SolidColorBrush x:Key="SidebarActiveText"     Color="#F1F5F9"/>
    <SolidColorBrush x:Key="SidebarIconColor"      Color="#64748B"/>
</ResourceDictionary>
```

- [ ] **Step 2: Mở DarkTheme.xaml và thay toàn bộ nội dung**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Base surfaces -->
    <SolidColorBrush x:Key="AppBackground"        Color="#0F172A"/>
    <SolidColorBrush x:Key="CardBackground"        Color="#1E293B"/>
    <SolidColorBrush x:Key="StatCardBackground"    Color="#1E293B"/>
    <SolidColorBrush x:Key="ChartBackground"       Color="#1A2536"/>
    <SolidColorBrush x:Key="SurfaceHover"          Color="#334155"/>

    <!-- Text -->
    <SolidColorBrush x:Key="PrimaryText"           Color="#F1F5F9"/>
    <SolidColorBrush x:Key="SecondaryText"         Color="#94A3B8"/>

    <!-- Borders -->
    <SolidColorBrush x:Key="BorderColor"           Color="#334155"/>

    <!-- Accent (blue) -->
    <SolidColorBrush x:Key="AccentColor"           Color="#3B82F6"/>
    <SolidColorBrush x:Key="AccentHover"           Color="#2563EB"/>
    <SolidColorBrush x:Key="AccentText"            Color="#60A5FA"/>

    <!-- Semantic -->
    <SolidColorBrush x:Key="DangerColor"           Color="#F87171"/>
    <SolidColorBrush x:Key="DangerBackground"      Color="#1F0606"/>
    <SolidColorBrush x:Key="WarningColor"          Color="#FCD34D"/>
    <SolidColorBrush x:Key="WarningBackground"     Color="#1C0D00"/>
    <SolidColorBrush x:Key="SuccessColor"          Color="#4ADE80"/>
    <SolidColorBrush x:Key="SuccessBackground"     Color="#022C0D"/>

    <!-- Sidebar -->
    <SolidColorBrush x:Key="SidebarBackground"     Color="#020617"/>
    <SolidColorBrush x:Key="SidebarBorder"         Color="#1E293B"/>
    <SolidColorBrush x:Key="SidebarText"           Color="#475569"/>
    <SolidColorBrush x:Key="SidebarActiveBackground" Color="#1E293B"/>
    <SolidColorBrush x:Key="SidebarActiveText"     Color="#F8FAFC"/>
    <SolidColorBrush x:Key="SidebarIconColor"      Color="#475569"/>
</ResourceDictionary>
```

- [ ] **Step 3: Build để xác nhận không có lỗi token**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```
git add SmartStudyPlanner/Themes/LightTheme.xaml SmartStudyPlanner/Themes/DarkTheme.xaml
git commit -m "feat(ui): expand color token system — 22 semantic tokens per theme"
```

---

## Task 2: App.xaml — Static Badge Brushes + Global Button Style

**Files:**
- Modify: `SmartStudyPlanner/App.xaml`

> **Lý do:** Badge colors (red/yellow/green) cho DataGrid status là semantic — không đổi theo theme, nên dùng `StaticResource` trong `App.xaml`. DynamicResource không hoạt động bên trong `DataTrigger.Setter` của WPF Style.

- [ ] **Step 1: Đọc App.xaml hiện tại để nắm context**

Mở file `SmartStudyPlanner/App.xaml`. Tìm block `<Application.Resources>`.

- [ ] **Step 2: Thêm static badge brushes và global Button style vào `Application.Resources`**

Thêm trước `</Application.Resources>`:

```xml
<!-- Badge brushes — static, không đổi theo theme -->
<SolidColorBrush x:Key="BadgeDanger"  Color="#DC2626"/>
<SolidColorBrush x:Key="BadgeWarning" Color="#D97706"/>
<SolidColorBrush x:Key="BadgeSuccess" Color="#16A34A"/>
<SolidColorBrush x:Key="BadgeNeutral" Color="#64748B"/>

<!-- Global Button base style — xóa default Windows chrome -->
<Style TargetType="Button">
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Cursor"          Value="Hand"/>
    <Setter Property="FontFamily"      Value="Segoe UI"/>
    <Setter Property="FontSize"        Value="13"/>
    <Setter Property="Padding"         Value="14,8"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="border"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="6"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="border" Property="Opacity" Value="0.85"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="border" Property="Opacity" Value="0.70"/>
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="border" Property="Opacity" Value="0.38"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

- [ ] **Step 3: Build**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```
git add SmartStudyPlanner/App.xaml
git commit -m "feat(ui): add static badge brushes and global Button style with CornerRadius"
```

---

## Task 3: MainWindow — Sidebar Navigation

**Files:**
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml`
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml.cs`

> **Lý do:** MainWindow hiện tại chỉ là `<Frame>`. Cần thêm sidebar 220px bên trái chứa nav items. Action buttons (`Quản lý`, `Cân bằng tải`, `Lưu`, `Theme`) sẽ chuyển lên sidebar, không còn nằm trong Dashboard content.

- [ ] **Step 1: Thay toàn bộ MainWindow.xaml**

```xml
<Window x:Class="SmartStudyPlanner.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Smart Study Planner" Height="680" Width="1100"
        MinHeight="600" MinWidth="900"
        WindowStartupLocation="CenterScreen"
        Background="{DynamicResource AppBackground}">

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- ── Sidebar ── -->
        <Border Grid.Column="0"
                Background="{DynamicResource SidebarBackground}"
                BorderBrush="{DynamicResource SidebarBorder}"
                BorderThickness="0,0,1,0">
            <DockPanel>

                <!-- App title -->
                <Border DockPanel.Dock="Top" Padding="20,24,20,16"
                        BorderBrush="{DynamicResource SidebarBorder}" BorderThickness="0,0,0,1">
                    <StackPanel>
                        <TextBlock Text="&#xE82D;"
                                   FontFamily="Segoe MDL2 Assets" FontSize="22"
                                   Foreground="{DynamicResource AccentColor}"
                                   Margin="0,0,0,6"/>
                        <TextBlock Text="Smart Study" FontSize="16" FontWeight="SemiBold"
                                   Foreground="{DynamicResource SidebarActiveText}"/>
                        <TextBlock Text="Planner" FontSize="13"
                                   Foreground="{DynamicResource SidebarText}"/>
                    </StackPanel>
                </Border>

                <!-- Footer actions -->
                <StackPanel DockPanel.Dock="Bottom" Margin="12,0,12,16">
                    <Separator Background="{DynamicResource SidebarBorder}" Margin="0,0,0,8"/>

                    <Button x:Name="BtnLuu" Click="BtnLuu_Click"
                            Background="Transparent" HorizontalContentAlignment="Left"
                            Padding="12,10" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE74E;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                                       Foreground="{DynamicResource SidebarIconColor}" Width="24"/>
                            <TextBlock Text="Lưu Tiến Trình" FontSize="13"
                                       Foreground="{DynamicResource SidebarText}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>

                    <Button x:Name="BtnTheme" Click="BtnTheme_Click"
                            Background="Transparent" HorizontalContentAlignment="Left"
                            Padding="12,10" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock x:Name="ThemeIcon" Text="&#xE708;"
                                       FontFamily="Segoe MDL2 Assets" FontSize="16"
                                       Foreground="{DynamicResource SidebarIconColor}" Width="24"/>
                            <TextBlock Text="Giao Diện" FontSize="13"
                                       Foreground="{DynamicResource SidebarText}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </StackPanel>

                <!-- Nav items -->
                <StackPanel Margin="12,12,12,0">
                    <Button x:Name="NavDashboard" Click="NavDashboard_Click"
                            Background="{DynamicResource SidebarActiveBackground}"
                            HorizontalContentAlignment="Left"
                            Padding="12,10" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE80F;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                                       Foreground="{DynamicResource SidebarActiveText}" Width="24"/>
                            <TextBlock Text="Dashboard" FontSize="13" FontWeight="Medium"
                                       Foreground="{DynamicResource SidebarActiveText}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>

                    <Button x:Name="NavMonHoc" Click="NavMonHoc_Click"
                            Background="Transparent" HorizontalContentAlignment="Left"
                            Padding="12,10" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE736;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                                       Foreground="{DynamicResource SidebarIconColor}" Width="24"/>
                            <TextBlock Text="Môn Học &amp; Bài Tập" FontSize="13"
                                       Foreground="{DynamicResource SidebarText}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>

                    <Button x:Name="NavWorkload" Click="NavWorkload_Click"
                            Background="Transparent" HorizontalContentAlignment="Left"
                            Padding="12,10" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE9CE;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                                       Foreground="{DynamicResource SidebarIconColor}" Width="24"/>
                            <TextBlock Text="Cân Bằng Tải" FontSize="13"
                                       Foreground="{DynamicResource SidebarText}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </StackPanel>

            </DockPanel>
        </Border>

        <!-- ── Content Frame ── -->
        <Frame x:Name="MainFrame" Grid.Column="1"
               NavigationUIVisibility="Hidden"/>
    </Grid>
</Window>
```

- [ ] **Step 2: Cập nhật MainWindow.xaml.cs — thêm nav handlers**

Trong class `MainWindow`, tìm `public MainWindow()`. Ngay sau `InitializeComponent();` có thể có `MainFrame.Navigate(...)` — giữ nguyên. Thêm các method sau vào cuối class (trước dấu `}`):

```csharp
private void SetActiveNav(Button active)
{
    foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload })
    {
        btn.SetResourceReference(BackgroundProperty, "Transparent");
        // reset text colors
        var sp = btn.Content as StackPanel;
        if (sp == null) continue;
        foreach (var tb in sp.Children.OfType<TextBlock>())
            tb.SetResourceReference(TextBlock.ForegroundProperty, "SidebarText");
    }
    active.SetResourceReference(BackgroundProperty, "SidebarActiveBackground");
    var activeSp = active.Content as StackPanel;
    if (activeSp != null)
        foreach (var tb in activeSp.Children.OfType<TextBlock>())
            tb.SetResourceReference(TextBlock.ForegroundProperty, "SidebarActiveText");
}

private void NavDashboard_Click(object sender, RoutedEventArgs e)
{
    SetActiveNav(NavDashboard);
    MainFrame.Navigate(new DashboardPage());
}

private void NavMonHoc_Click(object sender, RoutedEventArgs e)
{
    SetActiveNav(NavMonHoc);
    MainFrame.Navigate(new QuanLyMonHocPage());
}

private void NavWorkload_Click(object sender, RoutedEventArgs e)
{
    SetActiveNav(NavWorkload);
    var win = new WorkloadBalancerWindow();
    win.Show();
}
```

Đồng thời, tìm handler `BtnLuu_Click` và `BtnTheme_Click` — nếu logic này hiện nằm trong `DashboardPage`, cần chuyển lên đây. Nếu chúng gọi `Command` trên `DashboardViewModel`, giữ Command logic và gọi qua ServiceLocator:

```csharp
private void BtnLuu_Click(object sender, RoutedEventArgs e)
{
    // Delegate sang DashboardViewModel nếu đang ở Dashboard page
    if (MainFrame.Content is DashboardPage dp &&
        dp.DataContext is DashboardViewModel vm)
        vm.LuuDuLieuCommand.Execute(null);
}

private void BtnTheme_Click(object sender, RoutedEventArgs e)
{
    if (MainFrame.Content is DashboardPage dp &&
        dp.DataContext is DashboardViewModel vm)
        vm.ToggleThemeCommand.Execute(null);
}
```

Thêm `using SmartStudyPlanner.ViewModels;` ở đầu file nếu chưa có.

- [ ] **Step 3: Build**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

Nếu lỗi `QuanLyMonHocPage` / `WorkloadBalancerWindow` không tìm thấy namespace: thêm `using SmartStudyPlanner;` hoặc kiểm tra namespace trong `.xaml.cs` của các page đó.

- [ ] **Step 4: Smoke test**

Chạy app. Kiểm tra:
- Sidebar hiển thị với 3 nav items
- Dashboard nav item có nền sáng hơn (active state)
- Click "Môn Học & Bài Tập" → navigate đúng page
- Click "Cân Bằng Tải" → mở WorkloadBalancerWindow
- Footer buttons "Lưu" và "Giao Diện" hiển thị dưới sidebar

- [ ] **Step 5: Commit**

```
git add SmartStudyPlanner/Views/MainWindow.xaml SmartStudyPlanner/Views/MainWindow.xaml.cs
git commit -m "feat(ui): add sidebar navigation to MainWindow — 3 nav items + footer actions"
```

---

## Task 4: Dashboard — Fix Hardcoded Colors (Dark Mode)

**Files:**
- Modify: `SmartStudyPlanner/Views/DashboardPage.xaml`

> **Lý do:** Hiện tại Dashboard có ~15 màu hex hardcode. Sau Task 1, các token mới đã có. Task này thay thế chúng để dark mode hoạt động đúng.

- [ ] **Step 1: Xóa 4 action buttons khỏi Dashboard**

Tìm block:
```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,20">
    <Button Content="📚 Quản lý Môn Học &amp; Bài Tập" .../>
    <Button Content="⚖️ Cân Bằng Tải" .../>
    <Button Content="💾 Lưu Tiến Trình" .../>
    <Button Content="🌙 Giao Diện" .../>
</StackPanel>
```

Xóa toàn bộ `<StackPanel>` này (4 buttons đã chuyển lên sidebar).

- [ ] **Step 2: Fix chart container backgrounds**

Tìm 2 `<Border Background="#F0F3F4" CornerRadius="8" Padding="10">` trong phần charts. Thay `Background="#F0F3F4"` thành `Background="{DynamicResource ChartBackground}"`.

- [ ] **Step 3: Fix DataGrid row Foreground**

Tìm trong `<DataGrid.RowStyle>`:
```xml
<Setter Property="Foreground" Value="Black" />
```
Thay thành:
```xml
<Setter Property="Foreground" Value="{DynamicResource PrimaryText}" />
<Setter Property="Background" Value="{DynamicResource CardBackground}" />
```

- [ ] **Step 4: Xóa DataTrigger row backgrounds**

Xóa 3 `<DataTrigger>` blocks trong `RowStyle` (chúng dùng màu hardcode `#FFCDD2`, `#FFF9C4`, `#C8E6C9`). Row coloring sẽ được thay bằng badge trong Task 5. Sau khi xóa, `RowStyle` chỉ còn 2 Setter (Foreground + Background).

- [ ] **Step 5: Fix hardcoded màu trong TextBlock section headers**

| Tìm | Thay bằng |
|-----|-----------|
| `Foreground="#E74C3C"` (TOP 5 header) | `Foreground="{DynamicResource DangerColor}"` |
| `Foreground="#27AE60"` (lịch hôm nay header) | `Foreground="{DynamicResource SuccessColor}"` |
| `Foreground="#2980B9"` (tiến độ thời gian header) | `Foreground="{DynamicResource AccentColor}"` |
| `Foreground="#E67E22"` (streak badge border + text) | `Foreground="{DynamicResource WarningColor}"` |
| `BorderBrush="#E67E22"` (streak badge) | `BorderBrush="{DynamicResource WarningColor}"` |
| `Background="#FFF3E0"` (streak badge bg) | `Background="{DynamicResource WarningBackground}"` |
| `Foreground="#16A085"` (cần học thêm column) | `Foreground="{DynamicResource SuccessColor}"` |
| `Foreground="Gray"` (đã học column) | `Foreground="{DynamicResource SecondaryText}"` |

- [ ] **Step 6: Fix button colors trong DataGrid cell template**

Tìm nút `🍅 HỌC NGAY` và `Đi tới ➔`:
```xml
<Button Content="🍅 HỌC NGAY" Background="#E74C3C" .../>
<Button Content="Đi tới ➔" Background="#3498DB" .../>
```
Thay thành:
```xml
<Button Content="HỌC NGAY" Background="{DynamicResource DangerColor}" Foreground="White"
        FontWeight="Bold" Cursor="Hand" Padding="10,4" Margin="0,0,5,0"
        Command="{Binding DataContext.MoFocusModeCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
        CommandParameter="{Binding}"/>
<Button Content="Đi tới" Background="{DynamicResource AccentColor}" Foreground="White"
        Padding="10,4" Cursor="Hand"
        Command="{Binding DataContext.DiToiTaskCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
        CommandParameter="{Binding}"/>
```

- [ ] **Step 7: Build**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Smoke test dark mode**

Chạy app. Bật dark mode. Kiểm tra:
- Charts không còn nền trắng cứng
- DataGrid text màu sáng (không còn Foreground="Black")
- Streak badge màu đúng theo theme
- Section headers màu đúng

- [ ] **Step 9: Commit**

```
git add SmartStudyPlanner/Views/DashboardPage.xaml
git commit -m "fix(ui): replace hardcoded hex colors with DynamicResource tokens — fix dark mode"
```

---

## Task 5: DataGrid — Status Badge Column

**Files:**
- Modify: `SmartStudyPlanner/Views/DashboardPage.xaml`

> **Lý do:** Thay full-row background coloring bằng badge nhỏ trong cột "Mức Độ" — đẹp hơn, readable hơn cả hai theme. Badge dùng `StaticResource` vì màu semantic không đổi theo theme.

- [ ] **Step 1: Thay DataGridTextColumn "Mức Độ" bằng DataGridTemplateColumn**

Tìm:
```xml
<DataGridTextColumn Header="Mức Độ" Binding="{Binding MucDoCanhBao}" Width="85" />
```

Thay bằng:

```xml
<DataGridTemplateColumn Header="Mức Độ" Width="100">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Border x:Name="BadgeBorder" CornerRadius="4" Padding="8,3"
                    HorizontalAlignment="Left" Margin="4,2" Background="{StaticResource BadgeNeutral}">
                <Border.Style>
                    <Style TargetType="Border">
                        <Setter Property="Background" Value="{StaticResource BadgeNeutral}"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding MucDoCanhBao}" Value="Khẩn cấp">
                                <Setter Property="Background" Value="{StaticResource BadgeDanger}"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding MucDoCanhBao}" Value="Chú ý">
                                <Setter Property="Background" Value="{StaticResource BadgeWarning}"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding MucDoCanhBao}" Value="An toàn">
                                <Setter Property="Background" Value="{StaticResource BadgeSuccess}"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Border.Style>
                <TextBlock Text="{Binding MucDoCanhBao}" Foreground="White"
                           FontSize="11" FontWeight="SemiBold"/>
            </Border>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 2: Build**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Smoke test**

Chạy app. Kiểm tra cột "Mức Độ":
- Task "Khẩn cấp" → badge đỏ
- Task "Chú ý" → badge vàng
- Task "An toàn" → badge xanh lá
- Badge hiển thị đúng trong cả light và dark mode

- [ ] **Step 4: Commit**

```
git add SmartStudyPlanner/Views/DashboardPage.xaml
git commit -m "feat(ui): replace full-row colors with status badge in DataGrid Mức Độ column"
```

---

## Task 6: Dashboard — Stat Cards Row

**Files:**
- Modify: `SmartStudyPlanner/Views/DashboardPage.xaml`
- Modify: `SmartStudyPlanner/ViewModels/DashboardViewModel.cs`

> **Lý do:** Thay `TextBlock ThongKe` (raw string) bằng 3 stat cards trực quan hơn: số task hôm nay, tỉ lệ hoàn thành, và thời gian học tích lũy.

- [ ] **Step 1: Thêm 3 computed properties vào DashboardViewModel**

Tìm class `DashboardViewModel`. Thêm sau các property hiện tại (trước hoặc sau `ThongKe`):

```csharp
public int SoTaskHomNay => LichHocHomNay?.Count ?? 0;

public string TyLeHoanThanhText
{
    get
    {
        var total = Top5Task?.Count ?? 0;
        if (total == 0) return "0%";
        var done = Top5Task!.Count(t => t.MucDoCanhBao == "An toàn");
        return $"{(done * 100 / total)}%";
    }
}
```

Sau đó trong `LoadDuLieuDashboard()` (hoặc bất kỳ method nào refresh data), thêm ở cuối:
```csharp
OnPropertyChanged(nameof(SoTaskHomNay));
OnPropertyChanged(nameof(TyLeHoanThanhText));
```

- [ ] **Step 2: Thay TextBlock ThongKe bằng 3 stat cards**

Tìm block:
```xml
<TextBlock Text="{Binding ThongKe}" FontSize="14" Foreground="{DynamicResource SecondaryText}" HorizontalAlignment="Center" Margin="0,0,0,20" />
```

Thay bằng:

```xml
<Grid Margin="0,0,0,20">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="16"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="16"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- Card 1: Task hôm nay -->
    <Border Grid.Column="0" Background="{DynamicResource StatCardBackground}"
            CornerRadius="10" Padding="16,14"
            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
        <StackPanel>
            <TextBlock Text="&#xE8A1;" FontFamily="Segoe MDL2 Assets" FontSize="20"
                       Foreground="{DynamicResource AccentColor}" Margin="0,0,0,6"/>
            <TextBlock Text="{Binding SoTaskHomNay}" FontSize="28" FontWeight="Bold"
                       Foreground="{DynamicResource PrimaryText}"/>
            <TextBlock Text="Task hôm nay" FontSize="12"
                       Foreground="{DynamicResource SecondaryText}"/>
        </StackPanel>
    </Border>

    <!-- Card 2: Tỉ lệ hoàn thành -->
    <Border Grid.Column="2" Background="{DynamicResource StatCardBackground}"
            CornerRadius="10" Padding="16,14"
            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
        <StackPanel>
            <TextBlock Text="&#xE930;" FontFamily="Segoe MDL2 Assets" FontSize="20"
                       Foreground="{DynamicResource SuccessColor}" Margin="0,0,0,6"/>
            <TextBlock Text="{Binding TyLeHoanThanhText}" FontSize="28" FontWeight="Bold"
                       Foreground="{DynamicResource PrimaryText}"/>
            <TextBlock Text="Hoàn thành (Top 5)" FontSize="12"
                       Foreground="{DynamicResource SecondaryText}"/>
        </StackPanel>
    </Border>

    <!-- Card 3: Streak -->
    <Border Grid.Column="4" Background="{DynamicResource StatCardBackground}"
            CornerRadius="10" Padding="16,14"
            BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
        <StackPanel>
            <TextBlock Text="&#xE734;" FontFamily="Segoe MDL2 Assets" FontSize="20"
                       Foreground="{DynamicResource WarningColor}" Margin="0,0,0,6"/>
            <TextBlock Text="{Binding ChuoiStreak}" FontSize="20" FontWeight="Bold"
                       Foreground="{DynamicResource PrimaryText}"/>
            <TextBlock Text="Streak duy trì" FontSize="12"
                       Foreground="{DynamicResource SecondaryText}"/>
        </StackPanel>
    </Border>
</Grid>
```

Đồng thời, tìm block streak badge cũ (trong `StackPanel Orientation="Horizontal"` đầu trang):
```xml
<Border Background="#FFF3E0" BorderBrush="#E67E22" ...>
    <TextBlock Text="{Binding ChuoiStreak}" .../>
</Border>
```
Có thể xóa badge này vì streak đã hiển thị trong stat card ở trên. Chỉ giữ `TextBlock TieuDe`.

- [ ] **Step 3: Build**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Smoke test**

Chạy app. Kiểm tra:
- 3 stat cards hiển thị đúng với icon, số, và label
- Cards có border và border radius
- Dark mode: cards dùng `StatCardBackground` đúng màu

- [ ] **Step 5: Commit**

```
git add SmartStudyPlanner/Views/DashboardPage.xaml SmartStudyPlanner/ViewModels/DashboardViewModel.cs
git commit -m "feat(ui): add stat cards row to Dashboard — tasks today, completion rate, streak"
```

---

## Task 7: Dashboard — Section Headers & Chart Container Polish

**Files:**
- Modify: `SmartStudyPlanner/Views/DashboardPage.xaml`

> **Lý do:** Section headers hiện dùng emoji prefix (`🔥`, `📈`). Thay bằng styled `TextBlock` với Segoe MDL2 icon + consistent typography.

- [ ] **Step 1: Tạo style helper cho section header**

Thêm vào `<Page.Resources>` (nếu chưa có thì thêm ngay sau `<Page ...>` opening tag, trước `<ScrollViewer>`):

```xml
<Page.Resources>
    <Style x:Key="SectionHeader" TargetType="TextBlock">
        <Setter Property="FontSize"     Value="14"/>
        <Setter Property="FontWeight"   Value="SemiBold"/>
        <Setter Property="Foreground"   Value="{DynamicResource PrimaryText}"/>
        <Setter Property="Margin"       Value="0,20,0,10"/>
    </Style>
</Page.Resources>
```

- [ ] **Step 2: Thay section header "TOP 5 DEADLINE"**

Tìm:
```xml
<TextBlock Text="🔥 TOP 5 DEADLINE KHẨN CẤP NHẤT" FontWeight="Bold" FontSize="16" Foreground="#E74C3C" Margin="0,0,0,10" />
```
Thay bằng:
```xml
<StackPanel Orientation="Horizontal" Margin="0,20,0,10">
    <TextBlock Text="&#xEA6C;" FontFamily="Segoe MDL2 Assets" FontSize="15"
               Foreground="{DynamicResource DangerColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
    <TextBlock Text="TOP 5 DEADLINE KHẨN CẤP NHẤT" Style="{StaticResource SectionHeader}"
               Foreground="{DynamicResource DangerColor}" Margin="0"/>
</StackPanel>
```

- [ ] **Step 3: Thay section header "LỊCH HỌC HÔM NAY"**

Tìm:
```xml
<TextBlock Text="{Binding TieuDeLichHomNay}" FontWeight="Bold" FontSize="16" Foreground="#27AE60" Margin="0,20,0,10" />
```
Thay bằng:
```xml
<StackPanel Orientation="Horizontal" Margin="0,20,0,10">
    <TextBlock Text="&#xE8BF;" FontFamily="Segoe MDL2 Assets" FontSize="15"
               Foreground="{DynamicResource SuccessColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
    <TextBlock Text="{Binding TieuDeLichHomNay}" Style="{StaticResource SectionHeader}"
               Foreground="{DynamicResource SuccessColor}" Margin="0"/>
</StackPanel>
```

- [ ] **Step 4: Thay section header "TIẾN ĐỘ THỜI GIAN HỌC"**

Tìm:
```xml
<TextBlock Text="📈 TIẾN ĐỘ THỜI GIAN HỌC" FontWeight="Bold" FontSize="16" Foreground="#2980B9" Margin="0,20,0,10" />
```
Thay bằng:
```xml
<StackPanel Orientation="Horizontal" Margin="0,20,0,10">
    <TextBlock Text="&#xE9D9;" FontFamily="Segoe MDL2 Assets" FontSize="15"
               Foreground="{DynamicResource AccentColor}" VerticalAlignment="Center" Margin="0,0,8,0"/>
    <TextBlock Text="TIẾN ĐỘ THỜI GIAN HỌC" Style="{StaticResource SectionHeader}"
               Foreground="{DynamicResource AccentColor}" Margin="0"/>
</StackPanel>
```

- [ ] **Step 5: Fix chart section labels ("Tỉ lệ hoàn thành", "Khối lượng bài tập")**

Tìm 2 TextBlock trong phần chart grid:
```xml
<TextBlock Text="Tỉ lệ hoàn thành" FontWeight="Bold" Foreground="{DynamicResource SecondaryText}" HorizontalAlignment="Center"/>
<TextBlock Text="Khối lượng bài tập" FontWeight="Bold" Foreground="{DynamicResource SecondaryText}" HorizontalAlignment="Center"/>
```
Thêm `FontSize="13"` vào cả 2. Không thay đổi gì khác.

- [ ] **Step 6: Thêm card elevation cho today schedule items**

Tìm `<Border Background="{DynamicResource CardBackground}" CornerRadius="5" Padding="10" Margin="0,0,0,5" ...>` trong `ItemsControl`.

Thay thành:
```xml
<Border Background="{DynamicResource CardBackground}" CornerRadius="8" Padding="12,10"
        Margin="0,0,0,6"
        BorderBrush="{DynamicResource BorderColor}" BorderThickness="1">
    <Border.Effect>
        <DropShadowEffect Color="#000000" Opacity="0.04" BlurRadius="8" ShadowDepth="1"/>
    </Border.Effect>
    ...
```
(Giữ nội dung Grid bên trong không thay đổi.)

- [ ] **Step 7: Build**

```
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Smoke test cuối**

Chạy app. Kiểm tra toàn bộ Dashboard:
1. Light mode: stat cards, charts, section headers, today schedule đều đúng màu
2. Dark mode: toggle theme → mọi thứ đổi màu đúng (không còn màu trắng cứng)
3. DataGrid: badge hiển thị đúng màu, không còn full-row background
4. Sidebar: nav hoạt động, active state rõ ràng
5. Không còn emoji nào trong section headers

- [ ] **Step 9: Commit**

```
git add SmartStudyPlanner/Views/DashboardPage.xaml
git commit -m "feat(ui): replace emoji section headers with Segoe MDL2 icons, polish chart containers"
```

---

## Tổng kết Thay đổi

| Task | Files | Thay đổi chính |
|------|-------|----------------|
| T1 | LightTheme, DarkTheme | 22 token → 36 token mỗi theme |
| T2 | App.xaml | Badge brushes + global Button style |
| T3 | MainWindow.xaml + .cs | Sidebar 220px, 3 nav items |
| T4 | DashboardPage.xaml | Xóa hardcode, fix dark mode |
| T5 | DashboardPage.xaml | Badge column thay full-row color |
| T6 | DashboardPage.xaml + DashboardViewModel | 3 stat cards |
| T7 | DashboardPage.xaml | Section headers + card polish |

**Thứ tự commit gợi ý:** T1 → T2 → T3 → T4 → T5 → T6 → T7 (mỗi task 1 commit độc lập, build pass trước khi sang task tiếp theo)
