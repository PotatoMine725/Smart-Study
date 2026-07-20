# Fix plan — Analytics stale-render on the no-data filter branch

**Date:** 2026-07-20
**Diagnosis:** [`../reports/2026-07-20-analytics-step2-diagnosis.md`](../reports/2026-07-20-analytics-step2-diagnosis.md)
**Owner approval:** granted (plan-mode approval, 2026-07-20).

## Context

During the Epic 1 reopen re-closure re-run (owner runbook, Step 2), the owner found the Analytics page showed the "Không có dữ liệu cho bộ lọc hiện tại" banner **while the charts below still rendered data**, that charts "did not re-render on every subject", and that "different option orders create different rendering (copied from previous options)". Static trace root-caused the whole cluster to **one structural defect**:

`AnalyticsViewModel.ApplyFilters()` returns early on the no-data branch (`AnalyticsViewModel.cs:133` — `if (!HasData) return;`) **without resetting any chart output**, and `AnalyticsPage.xaml` **never hides the chart panels** when `HasData` is false (only the banner toggles, `:127`). So a filter with no logs shows the empty-state banner on top of the *previous* filter's stale charts. This is a display-correctness bug (it showed one subject's data under a different, explicitly-selected subject and misled acceptance testing), **pre-existing and untouched by the reopen (R1/R2)** — not a regression.

Intended outcome: selecting a filter with no data shows *only* the empty state; switching between data-bearing filters always re-renders.

**Provenance confirmed by the owner (2026-07-20):** the "1 phút" was a real focus session on a task in subject **A1**, viewed while the dropdown was on **A** (a different subject); click order was first→last then random. Exactly the stale-render signature: "A" has no logs → `HasData=false` → the page stays frozen on the prior "Tất cả" render, which included the A1 session. The "session was >1 min but showed 1" is **expected whole-minute flooring** (`FocusViewModel.cs:123`, `phutDaHoc = _tongGiayDaHoc / 60`), not a defect.

**Out of scope (owner agreed to defer as decisions, QA 2.4):**
- Subject-completion chart + details table iterate all subjects regardless of the subject filter (`StudyAnalyticsService.cs:33`).
- The trend chart is hardwired to the last 7 days, so the range selector barely affects it (`StudyAnalyticsService.cs:17`).
- Focus minutes floor partial minutes to whole (`FocusViewModel.cs:123`) — benign; a `Math.Round` tweak would be a *separate* commit if ever wanted.

## The fix (two cheap layers)

**Part 1 — ViewModel (the correctness fix).** `SmartStudyPlanner/ViewModels/AnalyticsViewModel.cs`
- Add a private `ResetAnalyticsOutputs()` that clears every filter-driven output to its empty/default:
  `WeeklyChartSeries = Array.Empty<ISeries>()`, `WeeklyChartXAxes = new[]{ new Axis() }`,
  `SubjectChartSeries = Array.Empty<ISeries>()`, `SubjectChartXAxes = new[]{ new Axis() }`,
  `SubjectInsights = new()`, `HeatmapCells = new()`,
  `WeeklyNarrative = string.Empty`, `RecommendedNextAction = string.Empty`,
  `ProductivityValue = 0`, `ProductivityLabel = "Chưa có dữ liệu"`.
- Call it in **both** no-data early-return branches: `_allLogs.Count == 0` (`:113-118`) and `!HasData` (`:133`), before returning. The has-data path (`:135` onward) is unchanged.

**Part 2 — View (defense-in-depth + clean empty state).** `SmartStudyPlanner/Views/AnalyticsPage.xaml`
- Collapse the data-dependent panels when `HasData` is false, using the file's own idiom (a `Style` with `DataTrigger Binding="{Binding HasData}" Value="False"` → `Visibility=Collapsed`, like the empty-state banner at `:110-134`). No new converter.
- Panels gated: BAND A grid (weekly + subject charts, `:137`), HEATMAP border (`:166`), DETAILS table border (`:215`). Narrative hero + productivity card stay visible (they show reset placeholders).

## Test first (TDD)

New file `SmartStudyPlanner.Tests/ViewModels/AnalyticsViewModelFilterTests.cs`. Test `SwitchingToSubjectWithNoLogs_ClearsStaleCharts`:
1. `HocKy` with subject "A" (task `T`) and subject "B" (task, no logs).
2. Inline `IStudyLogRepository` stub: `GetForHocKyAsync` → one `StudyLog { MaTask = T, NgayHoc = Today, SoPhutHoc = 10 }`.
3. `new AnalyticsViewModel(hocKy, stubRepo, new StudyAnalyticsService(), new NullTelemetry())` (4-arg ctor).
4. `await LoadAsync()` → "Tất cả" renders; assert `HasData` true + charts non-empty (precondition).
5. `SelectedSubject = "B"` → `ApplyFilters` → no logs.
6. Assert: `HasData==false`, banner message set, **and** `WeeklyChartSeries`/`SubjectChartSeries`/`SubjectInsights`/`HeatmapCells` empty, `WeeklyNarrative==""`, `ProductivityValue==0` (RED before Part 1, GREEN after).

## Acceptance gates
1. New test REDs before Part 1, GREENs after.
2. `rtk dotnet test` → **337** (336 + 1).
3. `rtk dotnet build SmartStudyPlanner.slnx` → 0 errors, warnings at **96** baseline.
4. **(Owner — required to verify Part 2)** Re-run Step 2: no-data subject → banner only, no chart frames; data-bearing subjects → re-render each time. Part 1's data-correctness is unit-verified, but the panel-hiding `DataTrigger`s are **compile-checked only** — WPF bindings resolve at runtime, so a misbound trigger compiles clean and silently no-ops. This launch is the only thing that confirms they fire. (Worst case if a trigger is wrong: empty chart frames beside the banner instead of hidden — cosmetic, not a data regression, because Part 1 already emptied them.)

## Impact / risk
`gitnexus_impact` rates `ApplyFilters` **HIGH** by centrality (`LoadAsync`, `OnSelectedSubjectChanged`, `OnSelectedRangeDaysChanged`, `Page_Loaded`). Behavioral risk **low** — only adds clearing on the no-data branch; has-data path unchanged. Re-run `gitnexus_impact` + `gitnexus_detect_changes()` before commit to confirm scope = `AnalyticsViewModel` + `AnalyticsPage.xaml` + new test.

## Execution / dispatch
Single-concern (1 VM method, 1 XAML, 1 test) — **no parallel dispatch**; inline TDD. One commit, no `Co-Authored-By` trailer.

## Decisions still needing the owner (not implemented here)
- Subject-completion chart / details table: follow the subject filter, or always show all subjects?
- Range selector vs. trend chart: make range drive the trend, or keep "last 7 days" fixed and relabel?

## After the fix
Record the B4 re-decision: **reopen fix accepted (P0/P0-adj passed Steps 0/1/3); release deferred on this separate Analytics bug, now fixed.** Step 5's global-handler backlog (2 non-WPF handler live-fire tests) — low priority, not a blocker.

## Decisions made (ADR-style)
### Fix at the source (ViewModel reset) *and* add the View guard, not one or the other
- **Why:** the ViewModel reset is the real correctness fix (no stale data can exist); the View guard is defense-in-depth so a future skipped rebuild can never resurface stale charts, and it makes the empty state clean (no empty axes next to the banner).
- **What for:** correctness that survives future edits to `ApplyFilters`, plus a tidy empty state.
- **Experience:** binding chart-panel visibility to the same `HasData` flag the banner already uses keeps one source of truth for "is there data" instead of two subtly-different notions.
