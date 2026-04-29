# Dev Reset — Clean Slate Strategy
## Plan · 2026-04-26

> **Status:** partial
>
> **Scope:** the clean-slate path is now opt-in via `DEV_RESET_DB=1` instead of always-on startup deletion.
>
> **Policy:** keep DB persistence by default so semesters/tasks survive restarts; use the reset path only when intentionally testing a fresh baseline.

---

## Delivery strategy

### Default runtime behavior
- app startup should preserve local SQLite data across restarts
- `EnsureCreated()` may still be used to create the schema if the DB file is missing
- `EnsureDeleted()` must only run when a developer explicitly enables reset mode

### Reset trigger
- environment variable: `DEV_RESET_DB=1`
- when enabled, startup may delete the local DB before recreating schema
- this keeps the reset capability available without destroying normal dev data on every launch

---

## Why this change matters

The previous always-on clean slate behavior caused the app to lose semester data on every restart, while settings stored outside SQLite (like workload capacity) remained intact. That mismatch was confusing and made the app feel inconsistent.

With the new policy:
- database-backed data persists by default
- file-backed settings remain as designed
- reset is still available for clean testing when needed

---

## Acceptance criteria

- restart app → previously created semester data is still available
- workload capacity setting remains persisted separately
- clean reset is still possible when `DEV_RESET_DB=1` is set
- no data loss occurs during normal dev launches
