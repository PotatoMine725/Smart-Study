# Analytics (Step 2) — Diagnosis of the owner's re-closure findings

**Date:** 2026-07-20
**Trigger:** [`2026-07-20-fix-plan-observation.md`](2026-07-20-fix-plan-observation.md) — owner's runbook re-run. Steps 0/1/3 passed, Step 4 signed off; **Step 2 (Analytics/heatmap) surfaced anomalies and the owner held the release** pending investigation.
**Method:** static source trace of the Analytics subsystem (ViewModel + View + service + the sole `StudyLog` writer). No agent team dispatched — the cluster root-caused from one `AnalyticsViewModel` read.
**Confidence:** HIGH — the primary root cause is structural/provable from the binding, and the data-provenance thread is now **confirmed by the owner** (see §2).

> **Update 2026-07-20:** owner chose "settle it — plan the fix" over an agent team. Fix implemented per [`../plans/2026-07-20-analytics-stale-render-fix.md`](../plans/2026-07-20-analytics-stale-render-fix.md): **Part 1 (ViewModel reset) verified by a RED→GREEN discriminating test, full suite 337 pass.** Part 2 (XAML panel-hiding) is compile-checked only — WPF bindings resolve at runtime, so its visibility toggle is confirmed by the owner's Step 2 re-run, not by the build.

> **Framing that matters for B4:** every finding below is in the **Analytics subsystem, which R1/R2 never touched**. This is **not a reopen regression** — the reopen's P0 (FK crash) and P0-adj (crash visibility) passed in Steps 0/1/3. The release hold is a *separate, pre-existing* Analytics display bug, not a failure of the fix.

---

## 1. Primary root cause — stale-render on the no-data branch (explains findings C, D, and the no-data half of A)

**One structural defect explains "the graph still renders even when it says «Không có dữ liệu»", the order-dependence, and «does not re-render on every subject».**

`AnalyticsViewModel.ApplyFilters()` sets `HasData` from the filtered logs, then **returns early on the empty branch without resetting any chart output**:

- `AnalyticsViewModel.cs:129` — `HasData = filtered.Count > 0;`
- `AnalyticsViewModel.cs:130-132` — sets `EmptyStateMessage = "Không có dữ liệu cho bộ lọc hiện tại."`
- `AnalyticsViewModel.cs:133` — **`if (!HasData) return;`** ← leaves `WeeklyChartSeries`, `SubjectChartSeries`, `SubjectInsights`, `HeatmapCells`, `WeeklyNarrative`, `RecommendedNextAction`, `ProductivityValue/Label` holding the **previous** filter's values.

Meanwhile the view **never collapses the chart panels** — only the banner toggles:

- `AnalyticsPage.xaml:122-134` — empty-state banner: a **live** `DataTrigger` on `HasData=False` (correct).
- `AnalyticsPage.xaml:150 / 160 / 187 / 221` — weekly chart, subject chart, heatmap, details grid: **no `Visibility` binding to `HasData`** → always visible.

**Net effect:** selecting a subject with no logs in range shows the "no data" banner *on top of* the prior filter's charts. Because the leftover is whatever the last **data-bearing** selection rendered, the screen looks non-deterministic and order-dependent — exactly the owner's "different orders in options creates different graph rendering… probably copied from previous options."

**The discriminator that proves stale-render over "subject A really has data":** the banner is driven by `HasData`, which is recomputed fresh at line 129 *before* the early return. Banner-visible ⟹ `HasData=false` ⟹ filtered empty for "A" ⟹ line 133 skips every rebuild ⟹ charts are stale. The page loads "Tất cả" first (`AnalyticsViewModel.cs:50` default `SelectedSubject = "Tất cả"`), renders it, then freezes it when the owner switches to "A".

**Severity:** display-correctness bug (not cosmetic). It showed one subject's data under a *different* subject the owner had explicitly selected, and it actively misled acceptance testing. **Fix is small** (below).

## 2. The "1 phút" value — real focus-session data, shown under the wrong filter

The weekly bar of `1` and the hero narrative "Tuần này bạn học 1 phút" are **genuine data**, surfaced under subject "A" only because of the §1 stale-render.

- The **only** production `StudyLog` writer is the Pomodoro/focus timer: `FocusViewModel.cs:151` writes `NgayHoc = DateTime.Today`, `SoPhutHoc = phutDaHoc`.
- The hero narrative + weekly chart are hardwired to the **last 7 days ending today** (`AnalyticsViewModel.cs:182`; `StudyAnalyticsService.ComputeWeeklyMinutes(..., DateTime.Today)` at `StudyAnalyticsService.cs:17-22`), so any recent focus session always lands on the "today" column and recurs across days.
- A task being marked "Đã xong" does **not** zero study minutes — completion and logged minutes are independent. So "1 phút" is consistent with a ~1-minute focus session, not a bug in the number itself.

**CONFIRMED by the owner (2026-07-20):** a focus session was run on a task in subject **A1** (>1 min, floored to `1` by whole-minute integer division at `FocusViewModel.cs:123`), while the dropdown was on **A**; click order was first→last then random. That is the exact stale-render sequence — "A" has no logs, so the page stayed frozen on the prior "Tất cả" render, which included the A1 session. The "1 phút" value itself is genuine, not a bug.

## 3. Secondary quirks — product *decisions*, not stale-render (these are the QA 2.4 items)

Independent of §1; visible even when a subject *does* have data:

- **Subject-completion chart + details table ignore the subject filter.** `StudyAnalyticsService.ComputeSubjectInsights` iterates `hocKy.DanhSachMonHoc` (`StudyAnalyticsService.cs:33`) — **all** subjects — regardless of `SelectedSubject`. So "TỈ LỆ HOÀN THÀNH THEO MÔN" and "CHI TIẾT THEO MÔN" always show every subject. This is why the owner saw "Lập Trình Nâng Cao" while filtered to "A".
- **Weekly chart is range-independent.** The range selector (7/30/90) changes which logs are *included*, but `ComputeWeeklyMinutes` always plots the last 7 calendar days, so 30→90 barely changes this chart. The section is literally titled "7 NGÀY QUA", so the label is honest — but a range selector next to a fixed-7-day chart is confusing.

These are **behavioural decisions the owner must make** (should the completion chart follow the subject filter? should the range selector drive the trend chart?), matching QA finding 2.4. They are *not* fixed by the §1 patch.

## 4. Heatmap re-render (finding A)

Two contributors: (a) the §1 early-return skips `BuildHeatmap` for no-data subjects, so it stays stale; (b) when it *does* rebuild it uses subject-filtered `filtered`, so it should change — but sparse/saturated data can make changes visually subtle. Primary cause is (a); fixing §1 fixes the "doesn't re-render on empty subject" symptom.

---

## 5. Recommended fix (small, self-contained — would be a separate plan)

Two lines of defense, both cheap:

1. **ViewModel:** on the `!HasData` branch, reset the outputs to empty before returning (`WeeklyChartSeries = Array.Empty<ISeries>()`, `SubjectChartSeries = Array.Empty<ISeries>()`, `SubjectInsights = new()`, `HeatmapCells = new()`, clear `WeeklyNarrative`/`RecommendedNextAction`, zero `ProductivityValue`). This makes the empty state *actually* empty.
2. **View (defense-in-depth):** bind the chart-panel `Visibility` to `HasData` so a future skipped rebuild can never resurface stale charts.

Item §3 (subject-filter semantics, range-vs-trend) is **decisions first, then a small follow-up**, not part of this patch.

## 6. Step 5 — global-handler residual risk (separate from Step 2)

The owner asked whether to open tech-debt for the residual risk that the three global exception handlers are review- and launch-verified but **not unit-live-fired**.

- **DispatcherUnhandledException** genuinely can't be driven headlessly (needs a running WPF `Application`) — the owner's real Steps 0/1/3 launch *is* the live-fire, and it behaved.
- **`AppDomain.UnhandledException`** and **`TaskScheduler.UnobservedTaskException`** are **not** WPF-specific and *can* be integration-tested without an `Application`. A thin test that raises a fault and asserts `crash.log` grew is feasible for 2 of the 3.
- `CrashLogger` behaviour is already unit-tested; the wiring is trivial and PM-reviewed.

**Recommendation:** log a **small backlog item** (2 non-WPF handler integration tests), do **not** block the epic on it. Residual risk is low. See [Decisions made](#decisions-made) D2.

---

## Decisions made (ADR-style)

### D1 — Did not dispatch the agent team the owner requested; surfaced the direct diagnosis instead
- **Why:** the entire Step 2 cluster root-caused from a single `AnalyticsViewModel` read with citable line numbers. A cold team would re-derive exactly these five lines — the "expensive path" that returns confirming what's already shown.
- **What for:** hand the owner the evidenced result plus the choice — treat it settled and plan the fix, or commission one focused verification pass — rather than pre-emptively burning spawns or silently skipping their instruction.
- **Experience:** when new information appears (I could root-cause it directly) that the requester lacked when they gave the instruction, surface it and re-offer the decision; don't execute the now-redundant instruction on autopilot, and don't override it unilaterally either.

### D2 — Treat the Analytics finding as a display-correctness bug, the global-handler gap as low-risk backlog
- **Why:** the stale-render displayed the wrong subject's data under a selected subject and misled acceptance testing — that clears the bar for "bug", not "polish". The handler gap is trivially-wired, partly untestable by nature, and already launch-exercised.
- **What for:** gives the owner a clean split — one small fix to land before release, one small optional test task to schedule whenever.
- **Experience:** "hard to unit-test" is not the same as "untested"; the owner's real launch is legitimate live-fire evidence for the one handler no harness can reach.

### D3 — Kept the release HOLD, but reframed the B4 rationale
- **Why:** honoring the owner's "do not release" is non-negotiable, but the *reason* matters for the record: the reopen fix (P0/P0-adj) passed; the hold is a separate pre-existing Analytics bug.
- **What for:** lets B4 read "reopen accepted; release deferred on a distinct Analytics fix" instead of "reopen failed" — which would misrepresent Steps 0/1/3.
- **Experience:** separate the subsystem under test from the subsystem that happened to break during the same session; conflating them corrupts the gate decision.

---

## Status

- Primary root cause (§1): **confirmed**, high confidence.
- Data provenance (§2): **confirmed** — A1 focus session (floored to 1 min) shown under "A" via stale render.
- Independent verification: owner chose to settle it directly (no team) and proceed to the fix.
- Fix (§5): **Part 1 implemented + test-verified** (RED→GREEN, 337 pass); **Part 2 (panel-hiding) compile-checked**, visual toggle pending the owner's Step 2 re-run.
- Step-5 backlog (§6): **recommendation only** — schedule when convenient, not a blocker.
