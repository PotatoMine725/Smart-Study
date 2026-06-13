# M8 Ground-Truth Instrumentation — Verification Report

**Date:** 2026-06-13
**Agent:** Claude (Opus 4.8) via Claude Code
**Plan verified:** [`docs/plans/2026-06-11-m8-ground-truth-instrumentation.md`](../plans/2026-06-11-m8-ground-truth-instrumentation.md)

## Scope

Audit that the six slices of the M8 Ground-Truth Instrumentation plan — telemetry
persistence (Slice 0), M8-A difficulty ground-truth (Slices 1A/1B), and M8-B
WeightConfig→outcome cohort capture (Slices 2A/2B/2C) — shipped as specified, and
that the plan's acceptance gates hold. Covers commits `7f07e26` (Slice 0) through
`3f2fa50` (Slice 2C). Two gaps surfaced during the audit were remediated in the
same session (see §3).

## Findings

### 1. Per-slice verification — all six shipped

| Slice | Commit | Status | Evidence |
|---|---|---|---|
| 0 — telemetry persistence | `7f07e26` | ✅ | `DifficultyLabelLog` + `WeightChangeLog` carry every field the plan lists; 2 DbSets + `OnModelCreating` keys; 2 repo interfaces + SQLite impls (`Func<AppDbContext>` factory); ServiceLocator registers both. **Schema TRAP handled:** `CREATE TABLE IF NOT EXISTS` for both tables at startup (idempotent, safe on pre-existing DBs), mirroring the `IsSeeded` ALTER pattern. |
| 1A — TaskType difficulty prior | `670fc19` | ✅ (intent completed this session) | `PriorForTaskType` mapping exact: DoAnCuoiKy/ThiCuoiKy=4, ThiGiuaKy/KiemTraThuongXuyen=3, BaiTapVeNha=2, default 3. Keyword rules still win. **Gap at audit: prior was wired only into the telemetry baseline, not the live parse default — closed in `feat(m8a)`, see §3.** |
| 1B — capture DoKho on save | `36d6889` | ✅ | `QuanLyTaskViewModel.LogDifficultyLabelAsync` fire-and-forget after save; null-safe + try/catch; `FinalDoKho` = user value, `Source = "manual"`, `WasOverride = FinalDoKho != prior`. |
| 2A — log weight + cohort | `891ce24` | ✅ | `ApplySuggestion` snapshots the before-config prior to mutation → fire-and-forget log; cohort = open tasks (`TrangThai != HoanThanh`); baseline from `GetSnapshotAsync`; the Keep path does not log. |
| 2B — outcome maturation (14d) | `9cfd026` | ✅ | 14-day + idempotency gate in `GetPendingMaturationAsync` (`OutcomeMaturedUtc == null && AppliedUtc.AddDays(OutcomeWindowDays) <= now`); reads cohort from JSON; measures completion at `AppliedUtc + 14d`; background `Task.Run` in `OnStartup`. |
| 2C — verify UI + docs | `3f2fa50` | ✅ | `WeightOptimizerWindow` present; `DefaultMlConfidencePolicy` thresholds ≥0.75 auto / ≥0.60 review / <0.60 reject; CHANGELOG/ROADMAP updated. |

### 2. Acceptance gates

- **Build clean:** ✅ 0 errors (97 warnings, all pre-existing: NU1904 `System.Drawing.Common`, CS8618 nullable).
- **Tests green:** ⚠️→✅ At audit start: 236/238, 2 failing. Both failures were a pre-existing date-fragility in `DecisionEngineTests` (frozen `FakeClock(2026-04-11)` vs task deadlines built from real `DateTime.Now`), **unrelated to M8** — confirmed by `git log` (last touched at `b697eca`, before all M8 commits) and by the failures being assertion flips, not compile/exception errors. Fixed this session (§3). Now **242/242**.
- **Dual-path schema safety:** ✅ verified by code inspection — the `CREATE TABLE IF NOT EXISTS` path is correct and column types match the entities. Not runtime-smoked against an actual pre-M8 `.db` file (see Follow-ups).

### 3. Remediation performed this session

| Fix | Commit | Detail |
|---|---|---|
| Close Slice 1A wiring gap | `feat(m8a): wire TaskType difficulty prior into live parse path` | `ParsingOrchestrator.Parse` now passes `PriorForTaskType(loaiHeuristic)` as the difficulty default instead of a flat `3`, so the value shown/saved when no difficulty keyword is present reflects the task-type prior. Keyword rules still win; ML override semantics unchanged. +4 orchestrator tests (TDD red→green). |
| Fix date-fragile tests | `test(decision-engine): anchor priority deadlines to frozen FakeClock` | Extracted the frozen instant as `FixedNow`; the two ordering tests now build deadlines relative to it. Date-insensitive tests left on `DateTime.Now`. |

## Verification

- Build + full suite (RTK-wrapped `dotnet`):
  - `rtk dotnet build SmartStudyPlanner.slnx` → **0 errors**.
  - `rtk dotnet test SmartStudyPlanner.slnx --no-build` → **242 passed, 0 failed**.
- TDD evidence for the 1A fix: the new `Parse_KhongCoTuKhoaDoKho_DungPriorTheoLoai` cases failed with `Actual: 3` before the orchestrator change and pass after.
- GitNexus `impact` was run before each edit; `detect_changes` before each commit. The 1A change flags HIGH on aggregate because `Parse` is a hub in the `PhanTichNhapNhanh` flow — expected, and covered by the 6 existing parser guard tests (byte-equal / legacy) which remain green.

## Follow-ups (non-blocking)

1. **Same-class date latent:** `CalculatePriority_TaskTrongVung31Den60Ngay_LonHon0` still uses `DateTime.Now.AddDays(45)` (green only because score > 0 holds at any future gap). Anchor it to `FixedNow` if a stricter window assertion is ever added.
2. **Schema dual-path not runtime-smoked:** the migration is verified by inspection only. A one-off manual smoke (launch against a copied pre-M8 `.db`, confirm both tables appear and a save/apply writes a row) would close gate #4 empirically.
3. **1B telemetry semantics:** `DifficultyLabelLog.SuggestedDoKho` / `WasOverride` compare against the *type prior* only, not the full keyword-aware parser suggestion. `FinalDoKho` + `InputText` remain the true label, so training data is unaffected; revisit only if a richer "suggestion vs final" signal is wanted.
4. **Distill:** per `docs/reports/README.md`, fold the 1A-wiring completion into `CHANGELOG.md` and the date-fragility lesson into `docs/knowledge/` when convenient; this report can then be pruned.
