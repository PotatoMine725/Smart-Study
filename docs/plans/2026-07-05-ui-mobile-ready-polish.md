# UI/UX Improvement Plan — Fidelity Closure + Mobile-Ready Polish

> Date: 2026-07-05 · Branch: `ui_rf` · Status: PROPOSED
> Commits split per concern. GitNexus impact analysis required before any C#/VM edit.

## Context

The app was redesigned (branch `ui_rf`) from 4 concepts in `legacy\` (Dashboard, Analytics,
Subjects & Tasks, Workload Balancer — each with HTML mockup + DESIGN_SPEC.md + reference XAML).
The shipped product is ~80% faithful. This plan closes the fidelity gap, pays down cross-cutting
UX debt, and makes the app "mobile-ready" in both confirmed senses:

- **Responsive + touch-friendly WPF now** — app runs native full-screen on desktop (keeps
  `WindowState=Maximized` default) but adapts when windowed or on smaller devices.
- **Portable design-token/component system** — a mobile companion port (MAUI/Avalonia, tied to
  LAN-sync D-I) is near-term; tokens and icon geometries must be copy-portable.

Constraints: lightweight (no new NuGet packages), 289 unit tests stay green. No UI epic exists
in the master plan — this plan fills that hole (D4 registers it).

## Verified findings

Fidelity gaps that survived file-level verification:
- **Dashboard**: DonutChart fixed 160×160 (`Controls\DonutChart.xaml`); KPI 22px vs spec 27px;
  Top-5 DataGrid fixed columns (90/32); magic margins 26/24/22. (Adaptation-strip trigger is
  correct — no work.)
- **Analytics**: hero card verified faithful (boxed "Nên làm tiếp" + HasEnoughData wiring OK);
  remaining: 52-col heatmap unreadable at narrow widths; hero doesn't stack when narrow.
- **Subjects**: spec's `MaxWidth=900 + HorizontalAlignment=Left` dropped → unbounded stretch;
  form doesn't stack.
- **Tasks**: `WsMeterTrack` style exists in StudyWorkspaceStyles.xaml but is **never used**;
  DataGrid `MaxHeight=420` + fixed cols 130/130/70; no Enter-to-add/tab order; verify WsPill
  mapping (pill currently binds DiemUuTien→PriorityBrush; spec also badges MucDoCanhBao).
- **Workload (largest gap)**: capacity presets 6h/8h/10h **entirely missing**; slider
  `Minimum=1 Maximum=8` so 10h not representable; 7-col day grid overflows <~900px; no min-size
  guardrails since Window→Page conversion; slider lacks labels/AutomationProperties.

Cross-cutting debt:
- **BUG**: `BadgeDanger/BadgeWarning/BadgeSuccess` are static brushes in `App.xaml` (lines ~19-21)
  → wrong contrast in dark mode.
- No hover/pressed states on most interactive elements; no page transitions; no
  AutomationProperties; touch targets <44px; MDL2 icon font (16 glyph uses, **MainWindow.xaml
  only**) — not portable, and the legacy specs themselves ban icon fonts.
- Stale surfaces: SetupPage (4 hardcoded hex, pre-redesign look), FocusWindow (forced
  Maximized+Topmost), WeightOptimizerWindow (480×520 default, resizable but no MinWidth/Height).

## Decisions (confirmed with owner)

| # | Decision |
|---|----------|
| 1 | Mobile-ready = responsive WPF now AND portable tokens for near-term MAUI/Avalonia port |
| 2 | Scope = fidelity closure first, then UX debt everywhere |
| 3 | Desktop is full-screen-first; **lower MainWindow MinWidth 900→680** so windowed/small-device layouts are real; keep startup Maximized |
| 4 | FocusWindow: normal resizable window + "Ghim trên cùng" pin toggle, **default OFF** |
| 5 | A11y label language: **Vietnamese** (matches UI) |
| 6 | Badge tokens stay **distinct** from Danger/Warning/SuccessColor (higher saturation); revisit aliasing in the manifest |
| 7 | Workload slider ceiling: **10** (per legacy spec 0–10h); confirm no VM clamp via gitnexus_impact on CapacityHours before edit |

## Phase order: Foundations → Fidelity → Interaction → Portability

Foundations go first because 3 of 5 fidelity fixes (donut scaling, Subjects stacking, Workload
day-strip collapse) are responsive behaviors — building them without a breakpoint mechanism
means writing them twice; and fidelity fixes replace magic numbers, which need `DesignTokens.xaml`
to exist or Phase 2 adds new debt.

---

## Phase 1 — Foundations (F1–F3, ~1 day)

### F1. Responsive breakpoint mechanism (create)
- `SmartStudyPlanner\Behaviors\Breakpoint.cs` — `enum Breakpoint { Narrow, Medium, Wide }` +
  pure `BreakpointResolver.Resolve(width, narrowMax, mediumMax)` (unit-testable, no UI).
- `SmartStudyPlanner\Behaviors\ResponsiveLayout.cs` — attached props: `Track` (bool, hooks
  SizeChanged), read-only `Breakpoint`, optional `NarrowMaxWidth`/`MediumMaxWidth`
  (defaults **720 / 1100**).
- Consumption pattern (pure XAML, per page):

```xml
<Page b:ResponsiveLayout.Track="True">
  <DataTrigger Binding="{Binding Path=(b:ResponsiveLayout.Breakpoint),
                RelativeSource={RelativeSource AncestorType=Page}}" Value="Narrow">
    <Setter Property="Grid.Column" Value="0"/> <Setter Property="Grid.Row" Value="1"/>
  </DataTrigger>
</Page>
```

- Also: `MainWindow.xaml` MinWidth 900→680 (MinHeight stays 600).
- Tests: resolver boundary tests (719/720/1100) in the test project, mirroring prod namespace
  per test-structure convention.
- Acceptance: resizing across 720/1100 flips layouts live; zero binding errors in Output.

### F2. DesignTokens.xaml + badge-brush bugfix (create + fix)
- Create `SmartStudyPlanner\Themes\DesignTokens.xaml`, merged FIRST in `App.xaml`
  (theme-invariant scales only):
  - Spacing: `SpaceXS=4 S=8 M=12 L=16 XL=24 XXL=32`; Radius: `RadiusS=6 M=10 L=14`;
  - Type: `TypeCaption=12 Body=13 Subtitle=16 Title=20 Kpi=27 Display=34`;
  - `MinTouchTarget=44`; Motion durations `MotionFast=150ms Base=200ms Slow=250ms`.
- **Bugfix (own commit)**: delete static `BadgeDanger/Warning/Success` from `App.xaml`; define
  per-theme in `LightTheme.xaml` + `DarkTheme.xaml` (dark variants brightened, e.g.
  `#F87171/#FBBF24/#4ADE80`). Consumers use DynamicResource by key → zero churn.
- Acceptance: badges legible in dark mode; app renders identically otherwise.

### F3. Icons: MDL2 → Path geometries (small, confirmed scope: MainWindow only)
- Create `SmartStudyPlanner\Themes\Icons.xaml` (~8 `Geometry` keys, 24×24 grid).
- Modify `MainWindow.xaml` (+ `SidebarStyles.xaml` icon presenter if needed):
  `<Path Data="{StaticResource Icon.Dashboard}" Fill="{DynamicResource ...}" .../>`.
- Acceptance: no `Segoe MDL2`/`&#xE...;` left in repo XAML; icons recolor on theme switch.

## Phase 2 — Fidelity closure (A1–A5, XAML-only unless flagged)

### A1 Dashboard — `Views\DashboardPage.xaml`, `Themes\DashboardStyles.xaml`, `Controls\DonutChart.xaml`
- Donut: wrap in Viewbox (Min 140 / Max 220) — prefer zero-code; if arc math assumes 160px,
  run gitnexus_impact on DonutChart code-behind first. KPI → `TypeKpi` (27). Top-5 columns →
  Auto/star + MinWidth. Margins → spacing tokens. Narrow: KPI row 4→2×2.
- Acceptance: nothing clips at 800px; donut scales; KPI 27px both themes.

### A2 Analytics — `Views\AnalyticsPage.xaml`, `Themes\AnalyticsStyles.xaml`
- Heatmap: horizontal ScrollViewer `PanningMode=HorizontalOnly` at Narrow (touch-friendly);
  hero `*`/`Auto` columns stack vertically at Narrow.
- Acceptance: heatmap cells never <6px; hero stacks below 720.

### A3 Subjects — `Views\QuanLyMonHocPage.xaml`
- Restore `MaxWidth=900 + HorizontalAlignment=Left`; form 2-col→1-col at Narrow.

### A4 Tasks — `Views\QuanLyTaskPage.xaml` (+ converter in `Converters\` only if missing — flag)
- Wire unused `WsMeterTrack` meter per spec; drop `MaxHeight=420` for star-row layout; columns →
  Auto/MinWidth; verify/apply `WsPill` for MucDoCanhBao per spec; `KeyBinding Enter` → existing
  Add command; explicit TabIndex on form.

### A5 Workload — `Views\WorkloadBalancerPage.xaml`, `Themes\WorkloadStyles.xaml` (+ **VM touch, gitnexus first**)
- Slider range → 1–10 (gitnexus_impact on CapacityHours; VM has tests among the 289).
- Add preset buttons 6h/8h/10h (prefer `SetCapacityCommand` on VM — flag as C# touch).
- Day strip: Narrow → vertical stack via ItemsPanel-swap trigger; Medium → horizontal scroll.
- Task boxes get SubjectPalette brushes; slider tick labels + `AutomationProperties.Name`;
  restore min-size guardrails on key panels.

Phase-2 verification, every workstream: `rtk dotnet build` clean, `rtk dotnet test` 289+ green,
manual resize script 1600→1100→900→720→640 in both themes (appended to
`docs\ux_quality_gate_checklist.md`).

## Phase 3 — Interaction layer + stale surfaces (C1–C4)

- **C1 Hover/pressed + touch targets** — `CommonStyles.xaml`, `SidebarStyles.xaml`, page
  dictionaries: `IsMouseOver`/`IsPressed` triggers on existing hover brushes; DataGrid RowStyle
  hover (Dashboard, Tasks); `MinHeight={StaticResource MinTouchTarget}` on buttons/nav/rows.
- **C2 Page transitions** — `MainWindow.xaml.cs` (**code-behind, gitnexus first**): on
  `MainFrame.Navigated`, 180ms opacity 0→1 + TranslateY 8→0 on new content (<40 lines);
  respect `SystemParameters.ClientAreaAnimation` (reduced-motion).
- **C3 Keyboard + a11y sweep** — `AutomationProperties.Name` (Vietnamese) on icon-only and
  chart elements (donut gets total-label name); TabIndex + `FocusManager.FocusedElement` on
  Setup/Tasks/Subjects forms.
- **C4 Stale surfaces** — `SetupPage.xaml` (hex → tokens, card language); `FocusWindow.xaml`
  (Normal resizable + "Ghim trên cùng" pin toggle default OFF — check code-behind for Topmost
  logic, gitnexus if C#); `WeightOptimizerWindow.xaml` (add MinWidth=480/MinHeight=520, token
  sweep).

## Phase 4 — Consolidation + portability (D1–D4)

- **D1** Token sweeps, one commit per dictionary (Common → Sidebar → Dashboard → Analytics →
  StudyWorkspace → Workload); grep-verify zero hardcoded values per dictionary; never delete old
  keys until repo grep shows zero references.
- **D2** `docs\design\design-tokens.json` (W3C style: color.semantic incl. light/dark, space,
  radius, type, motion, icon path data) + `docs\design\design-tokens.md` (mapping rules WPF
  today / MAUI-Avalonia later). Hand-synced; generator = optional future work.
- **D3** Extend `docs\ux_quality_gate_checklist.md`: breakpoint matrix, 44px audit, tab-walk,
  narrator spot-check.
- **D4** Register UI epic + mobile-ready definition in the master-plan docs.

## Commit sequence (~17 commits on `ui_rf`)

1. feat(ui): Breakpoint resolver + ResponsiveLayout behavior (+tests) — *C#*
2. feat(ui): DesignTokens.xaml scales
3. fix(theme): badge brushes → per-theme dictionaries
4. feat(ui): MDL2 glyphs → Icons.xaml Path geometries
5. feat(dashboard): responsive donut + KPI scale + fluid Top-5
6. feat(analytics): responsive heatmap + hero stacking
7. feat(subjects): restore MaxWidth + narrow stacking
8. feat(tasks): meter bar + fluid grid + Enter-to-add
9. feat(workload): capacity presets + slider range — *C#/VM, gitnexus first*
10. feat(workload): day-strip collapse + subject brushes + min sizes
11. feat(ui): hover/pressed + 44px touch targets
12. feat(ui): page navigation transition — *code-behind, gitnexus first*
13. feat(a11y): AutomationProperties + focus/tab sweep
14. feat(ui): restyle SetupPage / FocusWindow / WeightOptimizer
15. refactor(theme): token sweep Common/Sidebar
16. refactor(theme): token sweep page dictionaries
17. docs(design): tokens manifest + quality-gate + master-plan epic

Only commits 1, 9, 12 (and possibly 8/14 if converters/Topmost logic need C#) touch C# —
run `gitnexus_impact` before each; `gitnexus_detect_changes` before every commit
(CLAUDE.md hard rule). All others are XAML/docs and cannot affect the 289 tests.

## Verification (end-to-end)

1. `rtk dotnet build` — zero errors/warnings introduced.
2. `rtk dotnet test` — 289+ green (new resolver tests added).
3. Manual matrix per the extended `docs\ux_quality_gate_checklist.md`: both themes × widths
   1600/1100/900/720/640 × all 5 pages + 3 windows — no clipping, no binding errors in Output,
   badges legible in dark mode, icons recolor, nav transition ≤200ms and disabled when OS
   animations off, tab-walk completes on all forms.
4. Workload: presets set 6/8/10h, slider reaches 10, day strip stacks at Narrow.
5. FocusWindow: opens Normal, un-pinned; pin toggle sets Topmost.
