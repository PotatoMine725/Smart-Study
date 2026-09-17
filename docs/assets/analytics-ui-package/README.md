# Analytics two-section redesign — implementation package

Approved design: see `Analytics Redesign Proposal.dc.html` (interactive mockup, still
in this project) for the visual reference — subject-identity colors, light/dark theme
toggle, and the empty/loading states are all live-previewable there.

## Contents

- `Views/AnalyticsPage.xaml` — drop-in replacement for
  `SmartStudyPlanner/Views/AnalyticsPage.xaml`. Uses only existing style resources
  (`AnCard`, `AnPanel`, `AnSectionTick`, etc. from `Themes/AnalyticsStyles.xaml` —
  unchanged) and existing converters (`HeatLevelToBrushConverter`,
  `SubjectToBrushConverter`).
- `Controls/RadialProgressRing.xaml` + `.xaml.cs` — new UserControl, the completion
  percentage ring (WPF has no conic-gradient brush, so this draws two arcs the same
  way `DonutChart` already does). Copy into `SmartStudyPlanner/Controls/`.
- `DATA-CONTRACT.md` — the exact `AnalyticsViewModel.cs` changes this XAML depends on:
  4 new properties, a scoped `ApplyFilters()`, a new `BuildFocusedCompletion()`, and a
  new `BuildOverallSection()` that decouples Section 2 (heatmap/table/narrative/score)
  from both filters so it always reflects the whole semester.

## What changed from the current page

- Môn học selector moved from the global header into Section 1's own header (it's the
  filter that section owns); Khoảng thời gian stays global (both sections' Section-1
  content it drives; Section 2 is always all-time).
- Completion switched from a per-subject bar chart to a single percentage ring —
  correct now that Section 1 shows one subject (or an all-subjects aggregate) at a time
  instead of comparing many.
- Narrative + productivity score moved to the top of Section 2 (they're semester-wide,
  not filter-scoped).
- Each Section 1 chart (weekly, completion) shows its own empty message instead of one
  blanket page-level empty state; Section 2 always renders.
- Selected subject gets a consistent identity color (dropdown border, weekly bars, ring)
  reusing the existing `SubjectToBrushConverter` palette — also shown as a dot next to
  each row in Section 2's details table so the two sections cross-reference visually.
