# UX Quality Gate Checklist (Phase F)

## Visual parity
- [ ] Dashboard, Analytics, QuanLyTask, Focus render correctly in Light theme
- [ ] Dashboard, Analytics, QuanLyTask, Focus render correctly in Dark theme
- [ ] No text with low contrast against background
- [ ] No hardcoded row/background warning colors in primary DataGrid flows

## Navigation and context
- [ ] Sidebar active state matches current page in `MainFrame`
- [ ] `Workload` popup open indicator appears/disappears correctly
- [ ] Current semester context label updates after navigation
- [ ] Primary journeys reachable in <= 2 clicks from Dashboard

## State handling
- [ ] `IsLoading` state appears while data loads
- [ ] `HasData = false` displays meaningful empty-state messages
- [ ] Error state (`HasError`) has user-facing guidance
- [ ] Filters producing empty result do not break chart/table bindings

## Task notes and links
- [ ] Quick parser only fills core task fields (not notes/links)
- [ ] URL validation blocks invalid/non-http(s) links
- [ ] Link list shows host preview and supports open/copy/remove
- [ ] Saving/Editing a task keeps notes/links stable

## Telemetry sanity
- [ ] `dashboard_open`, `analytics_open`, and nav click events are emitted
- [ ] Focus start/complete/abort events are emitted
- [ ] Analytics filter change event includes `range_days` and `subject`

## Regression checks
- [ ] Add/Edit/Delete task flow still works
- [ ] Complete task + focus flow still works
- [ ] Analytics charts and heatmap still render with real data
- [ ] App startup and theme toggle do not regress

