# Semester End Date — Auto-synced Editable End Date
## Plan · 2026-04-26

> **Goal:** let users enter only `Ngày bắt đầu`, auto-suggest `Ngày kết thúc` as `+150 ngày`, then allow manual override later.
>
> **Policy:**
> - default semester duration is 150 days
> - the app auto-fills `Ngày kết thúc` while the field is in auto mode
> - once user edits `Ngày kết thúc`, the field becomes manual mode and should no longer be overwritten by changes to `Ngày bắt đầu`
> - user can restore the default auto calculation at any time

---

## 1. UX behavior

### Auto mode
- On new semester creation, `Ngày kết thúc = Ngày bắt đầu + 150 ngày`.
- If user changes `Ngày bắt đầu` while still in auto mode, `Ngày kết thúc` updates automatically.

### Manual override mode
- If user edits `Ngày kết thúc` directly, the field switches to manual mode.
- Subsequent changes to `Ngày bắt đầu` must not overwrite `Ngày kết thúc`.

### Restore default
- Provide a `Tự động tính lại` / `Khôi phục mặc định` action.
- This returns the editor to auto mode and re-applies `+150 ngày`.

---

## 2. Implementation scope

### Files to modify
- `SmartStudyPlanner/Models/HocKy.cs`
- `SmartStudyPlanner/ViewModels/SetupViewModel.cs`
- `SmartStudyPlanner/Views/SetupPage.xaml`

### Acceptance criteria
- New semester defaults to `NgayBatDau + 150 days`.
- User can manually edit the end date.
- User can restore auto calculation.
- UI clearly indicates whether the end date is auto-generated or manually overridden.

---

## 3. Blast radius

### High
- Setup page / viewmodel
- `HocKy` constructor default behavior

### Medium
- semester creation flow

### Low
- dashboard, analytics, ML modules

---

## 4. Recommended implementation order
1. Update `HocKy` default end-date calculation to 150 days.
2. Add auto/manual end-date handling to `SetupViewModel`.
3. Expose editable end-date UI in `SetupPage.xaml`.
4. Add restore-default action.
5. Verify new semester creation and continue-existing-semester flow remain intact.
