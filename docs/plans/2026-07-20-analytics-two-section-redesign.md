# Design plan — Analytics page, two-section restructure

**Date:** 2026-07-20
**Status:** design brief (owner will deliver to a design tool). Post-release feature — Epic 1 is
released (`docs/plans/2026-07-11-epic-1-closure-gate.md` B4 re-decision), so feature work is unblocked.
**Origin:** owner request 2026-07-20, on top of the Step-2 findings in
[`../reports/2026-07-20-analytics-step2-diagnosis.md`](../reports/2026-07-20-analytics-step2-diagnosis.md).

---

## 1. Why this change

Today the subject dropdown **silently governs some charts but not others** — the weekly-minutes chart
and heatmap follow it, but the completion chart and details table always show *all* subjects. There is
no visual signal for which is which, so selecting a subject and seeing an unchanged chart reads as
broken. This confused the owner during acceptance testing.

**Fix the confusion by making the filter's reach visible:** split Analytics into two clearly-labelled
sections — a **Focused** section that follows the subject filter, and an **Overall** section for
inherently cross-subject views that intentionally ignore it.

**Guiding UX principle:** *a filter must either apply to everything on screen, or the UI must make it
obvious which region it governs.* Sectioning is how we make it obvious.

---

## 2. Section model

### Section 1 — "Focused" (follows the subject filter)
Everything here answers *"how is THIS subject going?"* and re-renders whenever the subject changes.
- Weekly study-minutes trend for the selected subject.
- The selected subject's completion / progress (the completion chart, now **filtered to one subject** —
  owner decision 2026-07-20).
- The **MÔN HỌC dropdown lives at the top of this section**, not in the global header — so it visibly
  "owns" what it controls.

### Section 2 — "Overall / Toàn học kỳ" (ignores the subject filter)
Everything here is cross-subject or holistic and would be meaningless filtered to one subject:
- **Heatmap** — "am I studying consistently?" is about the whole person; per-subject it goes sparse
  and misleading. Uses **all** logs (range-filtered, not subject-filtered).
- **"Thống kê theo môn học"** comparison table — its entire value is comparing subjects; filtered to
  one subject it's a single pointless row. Always all subjects.
- **Productivity score** + **"Câu chuyện tuần này"** narrative — read as a "whole-you this week"
  summary. (Placement is a soft call; overall is the natural home. Flag for the designer to confirm.)

This mirrors dashboards students already know: GitHub's contribution graph (all activity, never
per-repo), fitness apps ("this workout" vs "overall trends"), banking ("this account" vs "all
accounts").

---

## 3. Current → new mapping

| Current element (AnalyticsPage.xaml) | New home | Filter behavior |
|---|---|---|
| MÔN HỌC dropdown (global header) | Section 1 header | *is* the section-1 control |
| KHOẢNG THỜI GIAN dropdown (global header) | stays global (top) | applies to **both** sections |
| Weekly minutes chart "SỐ PHÚT HỌC — 7 NGÀY QUA" | Section 1 | follows subject |
| Completion chart "TỈ LỆ HOÀN THÀNH THEO MÔN" | Section 1, **reworked to the selected subject** | follows subject |
| Heatmap "HOẠT ĐỘNG HỌC TẬP — 52 TUẦN QUA" | Section 2 | ignores subject |
| Details table "CHI TIẾT THEO MÔN HỌC" | Section 2 | ignores subject (comparison) |
| Productivity score + narrative hero | Section 2 (designer to confirm) | overall |

---

## 4. Layout (one scrolling page — not tabs)

```
┌ Analytics ───────────────────────────────  [Khoảng thời gian ▾] ┐  ← range = global lens
│
│  ┌ SECTION 1 · FOCUSED ──────────────  Môn học: [ A ▾] ───────┐
│  │  "Đang xem: A"                                              │
│  │  ┌ Weekly minutes (this subject) ┐  ┌ This subject's ─────┐ │
│  │  │  bar chart                    │  │ completion / progress│ │
│  │  └───────────────────────────────┘  └─────────────────────┘ │
│  └────────────────────────────────────────────────────────────┘
│
│  ┌ SECTION 2 · TOÀN HỌC KỲ (tất cả môn) ──────────────────────┐
│  │  Productivity score + "Câu chuyện tuần này"                 │
│  │  Heatmap (study consistency, all subjects)                  │
│  │  Thống kê theo môn học (compare all subjects)               │
│  └────────────────────────────────────────────────────────────┘
└──────────────────────────────────────────────────────────────────┘
```

Keep it a single scrolling page with strong visual grouping (section headers + divider/card cluster).
Students glance at analytics; don't make them navigate tabs.

---

## 5. States to design (all four)

1. **Single subject with data** — Section 1 shows that subject; Section 2 unchanged.
2. **Subject = "Tất cả"** — Section 1 shows the all-subjects aggregate trend/progress; Section 2 stays
   comparison + habit. Frame them as complementary ("how much / trend" vs "compare + consistency") so
   they don't feel redundant.
3. **Selected subject has no logs in range** — Section 1 shows a clean empty state (the reason this
   whole investigation started: no stale charts). Section 2 still renders (it's overall).
4. **Loading** — existing "Đang tải dữ liệu analytics..." pattern.

---

## 6. Hard requirements

- **NO CLIPPING (must-fix, carried from the owner's Step-2 re-run).** The current charts clip their
  x-axis labels (dates / subject names) and bars — fixed `Height="220"` in a `StackPanel` leaves no
  room for rotated labels, and the narrow completion-chart column makes it worse. The redesign **must**
  give charts responsive sizing (min-height with room to grow, adequate bottom padding for rotated
  date/subject labels) and be validated at the app's default window size. No axis label or bar may be
  cut off in any of the four states above.
- **Preserve the MVVM contract.** Keep binding to an `AnalyticsViewModel`; the view stays declarative
  (no code-behind logic). See §7 for the data the VM must now expose.
- **Reuse the existing design system** (`AnalyticsStyles.xaml`): `AnCard`, `AnPanel`, `AnEyebrow`,
  `AnPageTitle`, `AnSectionTitle`, `AnFieldLabel`, `AnDataGrid`, `AnGhostButton`, heat legend colors.
  Don't invent a parallel visual language.
- **Theme-aware** — honor the existing `DynamicResource` light/dark tokens.

---

## 7. Data-contract implications (for whoever implements the design)

The split needs the ViewModel to expose **two** data scopes where today it exposes one:

- **Section 1 (subject-scoped):** weekly minutes + completion for the *selected* subject only. The
  completion currently comes from `StudyAnalyticsService.ComputeSubjectInsights`, which iterates **all**
  subjects (`StudyAnalyticsService.cs:33`) — it must gain a subject-scoped path (or the VM filters its
  result to the selection).
- **Section 2 (overall):** the comparison table (all subjects, as today) **and** a heatmap built from
  **all** logs — today `BuildHeatmap` uses the subject-*filtered* set, which must change to overall for
  the Overall section.
- Net: the VM will grow a small number of new bound properties (subject-scoped vs overall) rather than
  reusing one filtered set for everything. The existing stale-render guard (`ResetAnalyticsOutputs`)
  applies to the Section-1 subject-scoped outputs.

This is a note for implementation feasibility, not a prescription for the visual design.

---

## 8. Out of scope (separate decisions)
- **Range selector vs. trend chart:** the weekly chart is hardwired to the last 7 days regardless of
  range (`StudyAnalyticsService.cs:17`). Decide separately whether the range should drive the trend or
  the label should just say "7 ngày qua". Not required for the split.

## 9. Acceptance criteria
- Selecting a subject visibly updates **only** Section 1; Section 2 is unchanged.
- The subject dropdown sits with Section 1; the range dropdown is global and affects both.
- All four states in §5 render correctly, with **zero clipped labels or bars** at default window size.
- Empty Section-1 state shows no stale data (regression guard from the stale-render fix holds).
- Visual language matches the existing `AnalyticsStyles.xaml` tokens; light + dark both correct.

## 10. Decisions made (ADR-style)
### Split by "filter reach", not by chart type
- **Why:** the confusion was never "too many charts" — it was not knowing which charts the subject
  filter controls. Grouping by *what the filter governs* addresses the actual mental-model gap.
- **What for:** a student can trust that Section 1 = "this subject" and Section 2 = "everything", with
  no silent, invisible filtering.
- **Experience:** matches how mainstream dashboards already teach users to read "focused vs overview".

### Completion chart follows the subject filter (owner call)
- **Why:** owner preference 2026-07-20; a chart that ignores an explicit selection reads as broken.
- **What for:** Section 1 becomes fully subject-consistent. The all-subjects comparison survives as the
  Section-2 table, which is where cross-subject comparison actually belongs.

### Clipping folded here, not hotfixed
- **Why:** the page is being re-laid-out; a blind height/margin patch (unverifiable headless) would be
  redone and would cost an extra owner re-run to confirm.
- **What for:** fixing the *class* of problem as an acceptance criterion prevents the new layout from
  reintroducing it.
