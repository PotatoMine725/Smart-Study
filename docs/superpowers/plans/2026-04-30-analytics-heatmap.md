# Analytics Heatmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GitHub-style contribution heatmap to `AnalyticsPage` — a 52-column × 7-row grid of small squares showing study activity over the past 364 days, colored by intensity (minutes studied per day).

**Architecture:** A new `HeatCell` record holds `Date`, `TotalMinutes`, and `Level` (0–4). `AnalyticsViewModel` aggregates `_allLogs` by `NgayHoc` into 364 `HeatCell` entries (aligned to the Monday of 52 weeks ago). A new `HeatLevelToBrushConverter` maps level → SKColor brush. The XAML section uses a pure WPF `ItemsControl` with `UniformGrid(Rows=7, Columns=52)` — no extra library needed.

**Tech Stack:** WPF XAML, `IValueConverter`, `UniformGrid`, `ObservableCollection`, `StudyLog.NgayHoc` / `SoPhutHoc`.

---

## Intensity levels

| Level | Minutes/day | Color (light/dark theme) |
|-------|------------|--------------------------|
| 0 | 0 (no activity) | `#EBEDF0` / `#161B22` |
| 1 | 1 – 30 | `#9BE9A8` / `#0E4429` |
| 2 | 31 – 60 | `#40C463` / `#006D32` |
| 3 | 61 – 120 | `#30A14E` / `#26A641` |
| 4 | > 120 | `#216E39` / `#39D353` |

> Colors are fixed GitHub-green palette. Theme detection via `Application.Current.Resources.MergedDictionaries` (check for "DarkTheme" in Source).

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `SmartStudyPlanner/Models/HeatCell.cs` | Record: Date, TotalMinutes, Level |
| **Create** | `SmartStudyPlanner/Converters/HeatLevelToBrushConverter.cs` | `IValueConverter`: Level (int) → `SolidColorBrush` |
| **Modify** | `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs` | Add `HeatmapCells` property + `BuildHeatmap()` called in `LoadAsync()` |
| **Modify** | `SmartStudyPlanner/Views/AnalyticsPage.xaml` | Add heatmap section between Weekly Chart and Subject Chart |

---

## Task 1: `HeatCell` record

**File:** Create `SmartStudyPlanner/Models/HeatCell.cs`

- [ ] **Step 1: Create `HeatCell.cs`**

```csharp
namespace SmartStudyPlanner.Models
{
    public record HeatCell(DateTime Date, int TotalMinutes, int Level)
    {
        public string Tooltip => TotalMinutes == 0
            ? $"{Date:dd/MM/yyyy} — Không có dữ liệu"
            : $"{Date:dd/MM/yyyy} — {TotalMinutes} phút";
    }
}
```

---

## Task 2: `HeatLevelToBrushConverter`

**File:** Create `SmartStudyPlanner/Converters/HeatLevelToBrushConverter.cs`

- [ ] **Step 1: Create converter**

```csharp
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartStudyPlanner.Converters
{
    public class HeatLevelToBrushConverter : IValueConverter
    {
        // Light-mode palette (GitHub green)
        private static readonly string[] LightColors = { "#EBEDF0", "#9BE9A8", "#40C463", "#30A14E", "#216E39" };
        // Dark-mode palette
        private static readonly string[] DarkColors  = { "#161B22", "#0E4429", "#006D32", "#26A641", "#39D353" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int level = value is int l ? Math.Clamp(l, 0, 4) : 0;
            bool isDark = Application.Current.Resources.MergedDictionaries
                .Any(d => d.Source?.OriginalString.Contains("DarkTheme") == true);
            string hex = isDark ? DarkColors[level] : LightColors[level];
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
```

---

## Task 3: Extend `AnalyticsViewModel`

**File:** `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs`

- [ ] **Step 1: Add `using` for `HeatCell`**

Add to the usings block (already has `SmartStudyPlanner.Models`):
```csharp
using System.Collections.ObjectModel;  // already present
```
No new using needed — `HeatCell` is in `SmartStudyPlanner.Models`.

- [ ] **Step 2: Add observable property**

After `[ObservableProperty] private bool hasEnoughData;` add:
```csharp
[ObservableProperty] private ObservableCollection<HeatCell> heatmapCells = new();
```

- [ ] **Step 3: Add `BuildHeatmap()` method**

Add private method before `RetrainModel()`:
```csharp
private void BuildHeatmap(List<StudyLog> logs)
{
    // Group logs by date (date only, strip time)
    var byDate = logs
        .GroupBy(l => l.NgayHoc.Date)
        .ToDictionary(g => g.Key, g => g.Sum(l => l.SoPhutHoc));

    // Start from the Monday of the week that is 51 full weeks ago
    var today = DateTime.Today;
    var startDate = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday - 7 * 51);
    if (startDate.DayOfWeek != DayOfWeek.Monday)
        startDate = startDate.AddDays(-(((int)startDate.DayOfWeek + 6) % 7));

    var cells = new ObservableCollection<HeatCell>();
    // Fill column-major (Monday first per column) for UniformGrid Rows=7
    for (int col = 0; col < 52; col++)
    {
        for (int row = 0; row < 7; row++)
        {
            var date = startDate.AddDays(col * 7 + row);
            int minutes = byDate.TryGetValue(date, out int m) ? m : 0;
            int level = minutes == 0 ? 0
                      : minutes <= 30  ? 1
                      : minutes <= 60  ? 2
                      : minutes <= 120 ? 3
                      : 4;
            cells.Add(new HeatCell(date, minutes, level));
        }
    }
    HeatmapCells = cells;
}
```

- [ ] **Step 4: Call `BuildHeatmap()` in `LoadAsync()`**

At the end of `LoadAsync()`, before the closing brace, add:
```csharp
BuildHeatmap(_allLogs);
```

---

## Task 4: Heatmap XAML section in `AnalyticsPage.xaml`

**File:** `SmartStudyPlanner/Views/AnalyticsPage.xaml`

- [ ] **Step 1: Add converter namespace + resource**

Add xmlns to `<Page>` opening tag:
```xml
xmlns:conv="clr-namespace:SmartStudyPlanner.Converters"
```

Add inside `<Page.Resources>`:
```xml
<conv:HeatLevelToBrushConverter x:Key="HeatBrush"/>
```

- [ ] **Step 2: Add heatmap section**

Insert after the Weekly Chart `</Border>` (after line ~70) and before the Subject Completion section:

```xml
<!-- Study Activity Heatmap -->
<TextBlock Text="Hoạt động học tập (52 tuần qua)" Style="{StaticResource SectionHeader}"/>
<Border Background="{DynamicResource StatCardBackground}" CornerRadius="10"
        Padding="12" BorderBrush="{DynamicResource BorderColor}" BorderThickness="1"
        Margin="0,0,0,4">
    <StackPanel>

        <!-- Day-of-week labels + grid -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="24"/>   <!-- day labels -->
                <ColumnDefinition Width="*"/>    <!-- cells -->
            </Grid.ColumnDefinitions>

            <!-- Day labels: Mon / Wed / Fri -->
            <StackPanel Grid.Column="0" VerticalAlignment="Top" Margin="0,2,0,0">
                <TextBlock Text="T2" FontSize="9" Foreground="{DynamicResource SecondaryText}"
                           Height="14" Margin="0,0,0,0"/>
                <TextBlock Height="14"/>
                <TextBlock Text="T4" FontSize="9" Foreground="{DynamicResource SecondaryText}"
                           Height="14"/>
                <TextBlock Height="14"/>
                <TextBlock Text="T6" FontSize="9" Foreground="{DynamicResource SecondaryText}"
                           Height="14"/>
                <TextBlock Height="14"/>
                <TextBlock Height="14"/>
            </StackPanel>

            <!-- 52 × 7 cell grid -->
            <ItemsControl Grid.Column="1"
                          ItemsSource="{Binding HeatmapCells}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <UniformGrid Rows="7" Columns="52"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Width="12" Height="12" Margin="1"
                                CornerRadius="2"
                                Background="{Binding Level, Converter={StaticResource HeatBrush}}"
                                ToolTip="{Binding Tooltip}"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </Grid>

        <!-- Legend -->
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,6,4,0">
            <TextBlock Text="Ít" FontSize="10" Foreground="{DynamicResource SecondaryText}"
                       VerticalAlignment="Center" Margin="0,0,4,0"/>
            <Border Width="12" Height="12" CornerRadius="2" Margin="1">
                <Border.Style>
                    <Style TargetType="Border">
                        <Setter Property="Background" Value="#EBEDF0"/>
                    </Style>
                </Border.Style>
            </Border>
            <Border Width="12" Height="12" CornerRadius="2" Background="#9BE9A8" Margin="1"/>
            <Border Width="12" Height="12" CornerRadius="2" Background="#40C463" Margin="1"/>
            <Border Width="12" Height="12" CornerRadius="2" Background="#30A14E" Margin="1"/>
            <Border Width="12" Height="12" CornerRadius="2" Background="#216E39" Margin="1"/>
            <TextBlock Text="Nhiều" FontSize="10" Foreground="{DynamicResource SecondaryText}"
                       VerticalAlignment="Center" Margin="4,0,0,0"/>
        </StackPanel>

    </StackPanel>
</Border>
```

---

## Verification checklist

- [ ] `dotnet build` → 0 errors
- [ ] Open Analytics page → heatmap renders 52×7 grid
- [ ] Hover over a cell → tooltip shows date + minutes
- [ ] Switch to Dark theme → cells use dark palette
- [ ] Day with study data shows non-gray color
- [ ] Empty days show level-0 color (no crash on missing dates)
