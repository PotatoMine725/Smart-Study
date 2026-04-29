# Consolidated Change Report
## 2026-04-26

> **Scope:** dev reset strategy, DB persistence fix, theme toggle hook fix, M7/M8 documentation alignment, and Semester end-date auto/manual behavior.

---

## 1. What was changed

### 1.1 Database startup behavior
- Switched the dev bootstrap away from always deleting the database on startup.
- Added opt-in dev reset behavior via `DEV_RESET_DB=1`.
- Default behavior now preserves the SQLite database across app restarts.

### 1.2 Workload capacity persistence
- Confirmed that workload capacity remains stored separately in `capacity.txt`.
- This setting continues to survive restarts even when database state is reset manually.

### 1.3 Theme toggle hook
- Fixed the dark mode toggle so it is invoked directly from `ThemeManager`.
- The button now works from every page instead of depending on Dashboard-specific context.

### 1.4 Semester end-date behavior
- Default semester end date now auto-suggests `NgayBatDau + 150 days`.
- Users can override the end date manually.
- Added a restore-default flow to re-enable auto mode.

### 1.5 ML lifecycle and docs alignment
- M7 remains implemented as an offline-first study-time predictor.
- M8 spec and plan were added for the text classifier + weight optimizer suite.
- Confidence threshold for M8-B is hard-coded and documented.
- Clean-reset docs were updated to reflect opt-in reset behavior instead of always-on reset.

---

## 2. Files updated in code

- `SmartStudyPlanner/App.xaml.cs`
  - startup DB bootstrap now preserves the SQLite DB by default
  - `EnsureDeleted()` is only used when `DEV_RESET_DB=1` is explicitly enabled
  - `EnsureCreated()` still recreates the schema when needed
- `SmartStudyPlanner/Views/MainWindow.xaml.cs`
  - theme toggle now calls `ThemeManager.ToggleTheme()` directly, so it works from every page
- `SmartStudyPlanner/Models/HocKy.cs`
  - semester end date now defaults to `NgayBatDau.AddDays(150)`
  - added internal auto/manual state for the end date
- `SmartStudyPlanner/ViewModels/SetupViewModel.cs`
  - added editable `NgayKetThuc` binding
  - added auto/manual synchronization logic
  - added restore-default command for the 150-day baseline
- `SmartStudyPlanner/Views/SetupPage.xaml`
  - exposed the end-date field and the restore-default action in the UI

---

## 3. Files updated in docs

- `docs/implementation_plan.md`
  - updated roadmap to reflect M7 completion, M8 planning, opt-in dev reset, and semester-end-date work
- `docs/superpowers/plans/2026-04-26-dev-reset-clean-slate.md`
  - converted clean reset from always-on to opt-in
- `docs/superpowers/specs/2026-04-26-dev-reset-clean-slate.md`
  - documented the dev-only clean slate strategy
- `docs/superpowers/specs/2026-04-26-m8-ml-suite-expansion.md`
  - added M8-A/M8-B scope and the fixed confidence thresholds
- `docs/superpowers/plans/2026-04-26-m8-ml-suite-expansion.md`
  - split implementation into A/B tracks and documented the confidence thresholds
- `docs/superpowers/reviews/2026-04-26-m7-code-review.md`
  - archived the M7 review result
- `docs/superpowers/plans/2026-04-26-semester-end-date-editable-150-days.md`
  - documented the auto/manual end-date workflow with a 150-day default
- `README.md`
  - added status notes for dev reset, theme hook fix, and semester end-date behavior

---

## 4. Current operational policy

- **Normal dev startup:** keep DB data
- **Clean reset:** only when `DEV_RESET_DB=1`
- **ML artifacts:** retrain on the current dev baseline when reset is used
- **Theme toggle:** available globally from the shell UI
- **Semester end date:** auto-suggests 150 days but can be overridden manually

---

## 5. Outcome

The project now has a more stable dev baseline:
- data survives restarts by default
- reset remains available when intentionally requested
- theme switching works globally
- semester dates are more user-friendly
- ML docs are aligned with the current plan state
