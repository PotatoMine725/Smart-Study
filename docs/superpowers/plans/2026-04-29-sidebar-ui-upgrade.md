# Sidebar UI Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add VS Code–style hover/active/inactive states to the left sidebar — 3px accent bar on active item, subtle hover background, and improved text contrast on inactive items.

**Architecture:** Single shared `SidebarNavButton` ControlTemplate (TargetType `ToggleButton`) in a new `SidebarStyles.xaml` resource dictionary. Active state driven by `IsChecked=True`; hover via `IsMouseOver` trigger. Both theme files gain two new color tokens and updated contrast values. Code-behind replaces manual `BackgroundProperty` and `SetResourceReference` calls with a single `IsChecked` toggle.

**Tech Stack:** WPF XAML, `ResourceDictionary`, `ToggleButton` ControlTemplate triggers, `DynamicResource`.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `SmartStudyPlanner/Themes/SidebarStyles.xaml` | ControlTemplate + `SidebarNavButton` style |
| **Modify** | `SmartStudyPlanner/App.xaml` | Merge `SidebarStyles.xaml` |
| **Modify** | `SmartStudyPlanner/Themes/LightTheme.xaml` | Add hover tokens, update contrast |
| **Modify** | `SmartStudyPlanner/Themes/DarkTheme.xaml` | Add hover tokens, update contrast |
| **Modify** | `SmartStudyPlanner/Views/MainWindow.xaml` | Button→ToggleButton, add Style, remove explicit Foreground |
| **Modify** | `SmartStudyPlanner/Views/MainWindow.xaml.cs` | IsChecked-based active state, add using |

---

## Task 1: Color tokens — LightTheme + DarkTheme

**Files:**
- Modify: `SmartStudyPlanner/Themes/LightTheme.xaml:30-36`
- Modify: `SmartStudyPlanner/Themes/DarkTheme.xaml:30-36`

- [ ] **Step 1: Update `LightTheme.xaml` sidebar section**

Replace the entire `<!-- Sidebar ... -->` block (lines 30–36) with:

```xml
    <!-- Sidebar (dark sidebar on light theme — intentional) -->
    <SolidColorBrush x:Key="SidebarBackground"       Color="#1E293B"/>
    <SolidColorBrush x:Key="SidebarBorder"           Color="#334155"/>
    <SolidColorBrush x:Key="SidebarText"             Color="#CBD5E1"/>
    <SolidColorBrush x:Key="SidebarActiveBackground" Color="#334155"/>
    <SolidColorBrush x:Key="SidebarActiveText"       Color="#F1F5F9"/>
    <SolidColorBrush x:Key="SidebarIconColor"        Color="#94A3B8"/>
    <SolidColorBrush x:Key="SidebarHoverBackground"  Color="#2D3F57"/>
    <SolidColorBrush x:Key="SidebarHoverText"        Color="#E2E8F0"/>
```

- [ ] **Step 2: Update `DarkTheme.xaml` sidebar section**

Replace the entire `<!-- Sidebar -->` block (lines 30–36) with:

```xml
    <!-- Sidebar -->
    <SolidColorBrush x:Key="SidebarBackground"       Color="#020617"/>
    <SolidColorBrush x:Key="SidebarBorder"           Color="#1E293B"/>
    <SolidColorBrush x:Key="SidebarText"             Color="#94A3B8"/>
    <SolidColorBrush x:Key="SidebarActiveBackground" Color="#1E293B"/>
    <SolidColorBrush x:Key="SidebarActiveText"       Color="#F8FAFC"/>
    <SolidColorBrush x:Key="SidebarIconColor"        Color="#64748B"/>
    <SolidColorBrush x:Key="SidebarHoverBackground"  Color="#0F172A"/>
    <SolidColorBrush x:Key="SidebarHoverText"        Color="#E2E8F0"/>
```

- [ ] **Step 3: Build to verify no broken resources**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj -v quiet 2>&1 | tail -5
```

Expected: `0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/Themes/LightTheme.xaml SmartStudyPlanner/Themes/DarkTheme.xaml
git commit -m "feat(ui): add sidebar hover tokens + improve inactive text contrast in both themes"
```

---

## Task 2: Create `SidebarStyles.xaml` + merge in `App.xaml`

**Files:**
- Create: `SmartStudyPlanner/Themes/SidebarStyles.xaml`
- Modify: `SmartStudyPlanner/App.xaml:8-10`

- [ ] **Step 1: Create `SmartStudyPlanner/Themes/SidebarStyles.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style x:Key="SidebarNavButton" TargetType="ToggleButton">
        <Setter Property="Background"                 Value="Transparent"/>
        <Setter Property="Foreground"                 Value="{DynamicResource SidebarText}"/>
        <Setter Property="BorderThickness"            Value="0"/>
        <Setter Property="HorizontalContentAlignment" Value="Left"/>
        <Setter Property="Padding"                    Value="12,10"/>
        <Setter Property="Margin"                     Value="0,2"/>
        <Setter Property="Cursor"                     Value="Hand"/>
        <Setter Property="FontFamily"                 Value="Segoe UI"/>
        <Setter Property="FontSize"                   Value="13"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ToggleButton">
                    <Border x:Name="Root"
                            Background="{TemplateBinding Background}"
                            CornerRadius="6">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="3"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>

                            <!-- Left accent bar — visible only when IsChecked=True -->
                            <Border x:Name="AccentBar"
                                    Grid.Column="0"
                                    Background="{DynamicResource AccentColor}"
                                    CornerRadius="3,0,0,3"
                                    Visibility="Collapsed"/>

                            <ContentPresenter Grid.Column="1"
                                              Margin="{TemplateBinding Padding}"
                                              HorizontalAlignment="Left"
                                              VerticalAlignment="Center"/>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <!-- Hover: subtle darker bg + brighter text -->
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Root" Property="Background"
                                    Value="{DynamicResource SidebarHoverBackground}"/>
                            <Setter Property="Foreground"
                                    Value="{DynamicResource SidebarHoverText}"/>
                        </Trigger>
                        <!-- Active (IsChecked): active bg + accent bar + bright text
                             Listed after IsMouseOver so it wins when both are true -->
                        <Trigger Property="IsChecked" Value="True">
                            <Setter TargetName="Root" Property="Background"
                                    Value="{DynamicResource SidebarActiveBackground}"/>
                            <Setter TargetName="AccentBar" Property="Visibility" Value="Visible"/>
                            <Setter Property="Foreground"
                                    Value="{DynamicResource SidebarActiveText}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2: Merge `SidebarStyles.xaml` in `App.xaml`**

In `SmartStudyPlanner/App.xaml`, replace:

```xml
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/Themes/LightTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
```

With:

```xml
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/Themes/LightTheme.xaml"/>
                <ResourceDictionary Source="pack://application:,,,/Themes/SidebarStyles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
```

- [ ] **Step 3: Build to verify no broken resources**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj -v quiet 2>&1 | tail -5
```

Expected: `0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add SmartStudyPlanner/Themes/SidebarStyles.xaml SmartStudyPlanner/App.xaml
git commit -m "feat(ui): add SidebarNavButton ControlTemplate with hover + active accent bar"
```

---

## Task 3: Update `MainWindow.xaml` — Button → ToggleButton

**Files:**
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml`

The 6 sidebar controls (4 nav + 2 footer) each need:
1. `<Button` → `<ToggleButton`, closing tag likewise
2. `Style="{StaticResource SidebarNavButton}"` added
3. Remove `Background="..."`, `HorizontalContentAlignment="..."`, `Padding="..."`, `Margin="..."` attributes (all moved into the Style)
4. Remove `Foreground="{DynamicResource ...}"` from child `TextBlock` elements (inherit from ToggleButton)
5. `NavDashboard` gets `IsChecked="True"` (initial active item)

- [ ] **Step 1: Replace `NavDashboard` button**

Replace:

```xml
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
```

With:

```xml
                    <ToggleButton x:Name="NavDashboard" Click="NavDashboard_Click"
                                  IsChecked="True"
                                  Style="{StaticResource SidebarNavButton}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE80F;" FontFamily="Segoe MDL2 Assets" FontSize="16" Width="24"/>
                            <TextBlock Text="Dashboard" FontSize="13" FontWeight="Medium" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 2: Replace `NavMonHoc` button**

Replace:

```xml
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
```

With:

```xml
                    <ToggleButton x:Name="NavMonHoc" Click="NavMonHoc_Click"
                                  Style="{StaticResource SidebarNavButton}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE736;" FontFamily="Segoe MDL2 Assets" FontSize="16" Width="24"/>
                            <TextBlock Text="Môn Học &amp; Bài Tập" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 3: Replace `NavWorkload` button**

Replace:

```xml
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
```

With:

```xml
                    <ToggleButton x:Name="NavWorkload" Click="NavWorkload_Click"
                                  Style="{StaticResource SidebarNavButton}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE9CE;" FontFamily="Segoe MDL2 Assets" FontSize="16" Width="24"/>
                            <TextBlock Text="Cân Bằng Tải" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 4: Replace `NavAnalytics` button**

Replace:

```xml
                    <Button x:Name="NavAnalytics" Click="NavAnalytics_Click"
                            Background="Transparent" HorizontalContentAlignment="Left"
                            Padding="12,10" Margin="0,2">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE9D9;" FontFamily="Segoe MDL2 Assets" FontSize="16"
                                       Foreground="{DynamicResource SidebarIconColor}" Width="24"/>
                            <TextBlock Text="Analytics" FontSize="13"
                                       Foreground="{DynamicResource SidebarText}" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
```

With:

```xml
                    <ToggleButton x:Name="NavAnalytics" Click="NavAnalytics_Click"
                                  Style="{StaticResource SidebarNavButton}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE9D9;" FontFamily="Segoe MDL2 Assets" FontSize="16" Width="24"/>
                            <TextBlock Text="Analytics" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 5: Replace `BtnLuu` footer button**

Replace:

```xml
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
```

With:

```xml
                    <ToggleButton x:Name="BtnLuu" Click="BtnLuu_Click"
                                  Style="{StaticResource SidebarNavButton}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE74E;" FontFamily="Segoe MDL2 Assets" FontSize="16" Width="24"/>
                            <TextBlock Text="Lưu Tiến Trình" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 6: Replace `BtnTheme` footer button**

Replace:

```xml
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
```

With:

```xml
                    <ToggleButton x:Name="BtnTheme" Click="BtnTheme_Click"
                                  Style="{StaticResource SidebarNavButton}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock x:Name="ThemeIcon" Text="&#xE708;"
                                       FontFamily="Segoe MDL2 Assets" FontSize="16" Width="24"/>
                            <TextBlock Text="Giao Diện" FontSize="13" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 7: Build to verify XAML compiles**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj -v quiet 2>&1 | tail -5
```

Expected: `0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add SmartStudyPlanner/Views/MainWindow.xaml
git commit -m "feat(ui): convert sidebar buttons to ToggleButton with SidebarNavButton style"
```

---

## Task 4: Update `MainWindow.xaml.cs` — IsChecked-based active state

**Files:**
- Modify: `SmartStudyPlanner/Views/MainWindow.xaml.cs`

- [ ] **Step 1: Add `using System.Windows.Controls.Primitives;`**

At the top of `MainWindow.xaml.cs`, after the existing `using System.Windows.Controls;` line, add:

```csharp
using System.Windows.Controls.Primitives;
```

- [ ] **Step 2: Replace `SetActiveNav` method (lines 159–174)**

Replace the entire `SetActiveNav` method:

```csharp
private void SetActiveNav(System.Windows.Controls.Button active)
{
    foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload, NavAnalytics })
    {
        btn.ClearValue(BackgroundProperty);
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
```

With:

```csharp
private void SetActiveNav(ToggleButton active)
{
    foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload, NavAnalytics })
        btn.IsChecked = false;
    active.IsChecked = true;
}
```

- [ ] **Step 3: Update `NavDashboard_Click`, `NavMonHoc_Click`, `NavAnalytics_Click` call signatures**

These calls are already correct (passing the button by name). No change needed — they call `SetActiveNav(NavDashboard)` etc. which now resolves to `ToggleButton` since the field type changed.

Update `NavWorkload_Click` to prevent it staying checked (it opens a window, not a page):

Replace:

```csharp
        private void NavWorkload_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHocKy == null) return;
            if (_workloadWindow == null || !_workloadWindow.IsLoaded)
            {
                _workloadWindow = new WorkloadBalancerWindow(_currentHocKy);
                _workloadWindow.Show();
            }
            else
                _workloadWindow.Activate();
        }
```

With:

```csharp
        private void NavWorkload_Click(object sender, RoutedEventArgs e)
        {
            NavWorkload.IsChecked = false;   // opens a window, not a nav page — don't show as active
            if (_currentHocKy == null) return;
            if (_workloadWindow == null || !_workloadWindow.IsLoaded)
            {
                _workloadWindow = new WorkloadBalancerWindow(_currentHocKy);
                _workloadWindow.Show();
            }
            else
                _workloadWindow.Activate();
        }
```

- [ ] **Step 4: Build + run to visually verify**

```bash
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj -v quiet 2>&1 | tail -5
```

Expected: `0 Error(s)`

Launch the app and verify:
- Dashboard nav item shows blue accent bar on left + bright text
- Hovering any sidebar button shows darker background
- Clicking a nav item moves the accent bar to the new item
- Clicking "Cân Bằng Tải" opens the window but leaves the previous nav item still active
- Footer buttons (Lưu, Giao Diện) show hover effect but never get an accent bar
- Switching theme (Giao Diện button) applies correct sidebar colors in both Light and Dark

- [ ] **Step 5: Commit**

```bash
git add SmartStudyPlanner/Views/MainWindow.xaml.cs
git commit -m "feat(ui): replace SetActiveNav Background logic with ToggleButton.IsChecked"
```

---

## Quick Reference

```bash
# Build only
dotnet build SmartStudyPlanner/SmartStudyPlanner.csproj

# Run app
dotnet run --project SmartStudyPlanner/SmartStudyPlanner.csproj
```
