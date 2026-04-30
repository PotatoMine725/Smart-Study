# Sidebar UI Upgrade
## Spec · 2026-04-29

> **Goal:** Add hover/selected/unselected visual states to the left sidebar, increase text contrast on inactive items, and introduce a VS Code–style left accent bar on the active nav item.

---

## 1. Scope

### In scope
- Left accent bar (3px, AccentColor) on active nav item
- Hover state: subtle darker background + brighter text/icon
- Inactive text contrast improvement (both themes)
- Shared `ToggleButton` ControlTemplate replacing inline Button styling
- Both Light and Dark themes updated

### Out of scope
- Sidebar width or layout changes
- Animation/transitions
- Tooltip or badge additions
- Any page content or routing logic

---

## 2. Visual States

| State | Background | Foreground | Accent bar |
|-------|-----------|------------|------------|
| Inactive | Transparent | `SidebarText` | Hidden |
| Hover | `SidebarHoverBackground` | `SidebarHoverText` | Hidden |
| Active | `SidebarActiveBackground` | `SidebarActiveText` | Visible (3px left) |

Footer buttons (Lưu Tiến Trình, Giao Diện) use the same style but are never set to `IsChecked=True` — they get hover treatment only.

---

## 3. Color Tokens

### 3.1 New tokens (both theme files)

| Token | Light | Dark |
|-------|-------|------|
| `SidebarHoverBackground` | `#2D3F57` | `#0F172A` |
| `SidebarHoverText` | `#E2E8F0` | `#E2E8F0` |

### 3.2 Updated tokens (contrast improvement)

| Token | Light before | Light after | Dark before | Dark after |
|-------|-------------|------------|------------|-----------|
| `SidebarText` | `#94A3B8` | `#CBD5E1` | `#475569` | `#94A3B8` |
| `SidebarIconColor` | `#64748B` | `#94A3B8` | `#475569` | `#64748B` |

Existing tokens unchanged: `SidebarBackground`, `SidebarBorder`, `SidebarActiveBackground`, `SidebarActiveText`, `AccentColor`.

---

## 4. Architecture

### 4.1 New file: `SmartStudyPlanner/Themes/SidebarStyles.xaml`

A `ResourceDictionary` containing one style: `SidebarNavButton` (TargetType `ToggleButton`).

The ControlTemplate structure:
```
Border (Root) — CornerRadius 6, Background via TemplateBinding
└── Grid (2 columns: 3px | *)
    ├── Border (AccentBar) — col 0, AccentColor, CornerRadius 3,0,0,3, Collapsed by default
    └── ContentPresenter — col 1, inherits Foreground from ToggleButton
```

Triggers inside `ControlTemplate.Triggers`:
- `IsMouseOver=True` → Root.Background = SidebarHoverBackground, Foreground = SidebarHoverText
- `IsChecked=True` → Root.Background = SidebarActiveBackground, AccentBar Visible, Foreground = SidebarActiveText

### 4.2 MainWindow.xaml changes

- All 6 nav/footer `<Button>` elements changed to `<ToggleButton>`
- `Style="{StaticResource SidebarNavButton}"` added to each
- Explicit `Foreground` removed from child `TextBlock` elements (inherit from ToggleButton)
- `Background` attribute removed (controlled by Style)
- `NavDashboard` initialized with `IsChecked="True"` (default active page)

### 4.3 App.xaml changes

Merge `SidebarStyles.xaml` into the `Application.Resources` merged dictionaries, after the theme dictionary.

### 4.4 MainWindow.xaml.cs changes

Replace current active-state logic (setting `Background` on buttons) with:

```csharp
private void SetActiveNav(ToggleButton active)
{
    foreach (var btn in new[] { NavDashboard, NavMonHoc, NavWorkload, NavAnalytics })
        btn.IsChecked = (btn == active);
}
```

Each nav click handler calls `SetActiveNav(NavXxx)` before navigating.

---

## 5. File Map

| Action | Path | Change |
|--------|------|--------|
| **Create** | `SmartStudyPlanner/Themes/SidebarStyles.xaml` | ControlTemplate + SidebarNavButton style |
| **Modify** | `SmartStudyPlanner/App.xaml` | Merge SidebarStyles.xaml |
| **Modify** | `SmartStudyPlanner/Themes/LightTheme.xaml` | Add 2 hover tokens, update SidebarText + SidebarIconColor |
| **Modify** | `SmartStudyPlanner/Themes/DarkTheme.xaml` | Add 2 hover tokens, update SidebarText + SidebarIconColor |
| **Modify** | `SmartStudyPlanner/Views/MainWindow.xaml` | Button→ToggleButton, add Style, remove explicit Foreground |
| **Modify** | `SmartStudyPlanner/Views/MainWindow.xaml.cs` | IsChecked-based active state |

---

## 6. Acceptance Criteria

- [ ] Active nav item shows 3px blue accent bar on the left
- [ ] Hovering any sidebar button shows `SidebarHoverBackground` + brighter text
- [ ] Hover does not persist after mouse leaves
- [ ] Only one nav item is active at a time
- [ ] Footer buttons (Lưu, Giao Diện) have hover effect but no accent bar
- [ ] Inactive text is visibly brighter than before in both themes
- [ ] App builds with 0 errors
- [ ] Switching themes at runtime still applies correct sidebar colors

---

## 7. Non-goals

- No animation/easing on state transitions
- No changes to page routing or navigation logic
- No changes to sidebar width (220px) or font sizes
