# Evidence record — DFD-9a end-to-end check, run 2026-08-27

> **This is an evidence record, not a report.** Per [`README.md`](README.md) it is exempt from the
> Scope / Verification / Decisions sections, and its wording must not be tidied by anyone else.
>
> **Who wrote what.** §1 and §2 are machine output and file metadata, captured by Claude directly
> from the build and the database — reproducible by re-running the commands. **§3, §4 and §5 are the
> owner's to write**, in the owner's own words. The blanks are deliberate; a blank that reads as a
> pass is worse than an empty one.
>
> Runbook: [`../plans/2026-08-26-dfd9a-instrumentation-runbook.md`](../plans/2026-08-26-dfd9a-instrumentation-runbook.md)
> Defect record: [`../plans/2026-08-26-prediction-instrumentation-defect.md`](../plans/2026-08-26-prediction-instrumentation-defect.md) §9.4

---

## 1. Provenance — what was actually run

| Item | Value | Checked how |
|---|---|---|
| Executable | `SmartStudyPlanner/bin/Debug/net10.0-windows10.0.19041.0/SmartStudyPlanner.exe` | |
| Exe size / mtime | 178 176 bytes, **2026-08-27 15:57:57** | `Get-Item` |
| Fix commit in tree | `bb5d241` (2026-08-26), `FocusViewModel.cs` | `git log -1` |
| Exe newer than fix? | **Yes** — 08-27 15:57 > 08-26 | runbook §1.2 |
| Database | `…/net10.0-windows10.0.19041.0/SmartStudyData.db` | printed by the read script |
| Model file | `%APPDATA%\SmartStudyPlanner\models\study_time.zip` — **present**, 12 607 bytes, 2026-06-27 | runbook §1.4 |
| Backup taken | `SmartStudyData.db.pre-dfd9a-20260827-1626.bak` | runbook §1.3 |

---

## 2. Raw read-command output

Command: `python tools/qa/read_outcome_logs.py`

### 2.1 Baseline, before any scenario

```text
FILE : D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db
MTIME: 2026-07-26 20:44:36.097919
ROWS : 2
CreatedUtc               Actual  Predicted  WasML  Confidence
2026-07-15 11:36:31.39      1.0       NULL      0        NULL
2026-07-20 13:28:20.53      1.0       NULL      1        NULL
```

### 2.2 After Scenario 1 and Scenario 2

Captured 2026-08-27 17:51 from the same database, after both sessions:

```text
FILE : D:\Code\C#\SmartStudyPlanner\SmartStudyPlanner\bin\Debug\net10.0-windows10.0.19041.0\SmartStudyData.db
MTIME: 2026-08-27 17:51:37.427884
ROWS : 4
CreatedUtc               Actual  Predicted  WasML  Confidence
2026-07-15 11:36:31.39      1.0       NULL      0        NULL
2026-07-20 13:28:20.53      1.0       NULL      1        NULL
2026-08-27 10:24:35.24     16.0        0.0      0         0.0
2026-08-27 10:38:29.93      2.0        0.0      0         0.0
```

*(`CreatedUtc` is UTC; local time was +7, so the two sessions ended 17:24 and 17:38 local.)*

**Owner: paste your own two terminal outputs here if they differ from the above in any way.**

<!-- paste run-1 (after Scenario 1) and run-2 (after Scenario 2) outputs here -->

### 2.3 Full column dump of the two new rows

```text
Id | CreatedUtc | MaTask | TaskType | Difficulty | Credits | DaysLeft | StudiedMinutesSoFar | ActualMinutes | PredictedMinutes | WasMlPrediction | Confidence
7C6FD64F-… | 2026-08-27 10:24:35.24 | 533482D5-… | 3 | 5.0 | 4.0 | -77.0 | 1.0 | 16.0 | 0.0 | 0 | 0.0
B2403F0D-… | 2026-08-27 10:38:29.93 | 6E9F9920-… | 4 | 5.0 | 4.0 | -77.0 | 0.0 |  2.0 | 0.0 | 0 | 0.0
```

Both tasks: `HanChot = 2026-06-11`, i.e. **77 days past deadline**, `DiemUuTien = 0.0`.

### 2.4 Note on the database mtime

The file's mtime (17:51) is later than the last outcome row (17:38 local). The application was opened
again after Scenario 2 and wrote something — **no new outcome row appeared**, and the row count is
still 4. If a third focus session was attempted and lasted under 60 seconds, that is the expected
result (`LuuThoiGianThucTe` guards on `_tongGiayDaHoc / 60 > 0`), not a failure.

---

## 3. What the person at the keyboard saw — **owner to complete**

### 3.1 Scenario 1 — emergency exit (`✖ Thoát khẩn cấp`)

- Time actually sat with the timer running: ____________
- *Tiến độ: Đã học N phút* observed ticking? ____________
- Any dialog, error, or anything unexpected: ____________

### 3.2 Scenario 2 — completion (`✅ Đã Xong`)

- Time actually sat with the timer running: ____________
- Task dropped off the dashboard's active list afterwards? ____________
- Any dialog, error, or anything unexpected: ____________

### 3.3 Anything else worth recording

____________

---

## 4. Pre-registered criteria — **owner to rule**

The criteria were fixed in runbook §4 before the run and must not be adjusted now. What the data
shows is filled in; the ruling is the owner's.

| # | Criterion | What the output shows | Owner's ruling |
|---|---|---|---|
| 1 | S1: exactly one new row, `PredictedMinutes` and `Confidence` both NOT NULL | 2 → 3 rows; new row `0.0` / `0.0` — non-null | |
| 2 | S2: exactly one further new row, same two columns non-null | 3 → 4 rows; new row `0.0` / `0.0` — non-null | |
| 3 | Pre-fix rows still read `NULL` in the same output | Both 2026-07 rows still `NULL` / `NULL` | |
| 4 | `ActualMinutes` matches the time actually sat | `16.0` and `2.0` — **only the owner can confirm these** | |

**Overall verdict (PASS / FAIL / NOT RUN):** ____________

---

## 5. Scenario 4 — which branch produced these rows

**Result: UNDETERMINED.** Not a failure; the row genuinely cannot answer it.

Both new rows read `WasMlPrediction = 0`, `Confidence = 0.0`, `PredictedMinutes = 0.0`. For a task
more than 3 days overdue this output is *arithmetically forced*, and two different code paths
produce it identically:

1. `OverdueRule` (`daysLeft < -3`) sets `DiemUuTien = 0`.
2. `ComputeFormulaMinutes` returns `0` when `DiemUuTien <= 0`, so `formula = 0`.
3. `confidence = 1 - clamp(|predicted - formula| / max(formula, 1), 0, 1)` collapses to
   `1 - clamp(predicted)` — **exactly `0`** for any ML prediction of 1 minute or more.
4. `0 < 0.6`, so the ML branch is rejected and logs `(0, false, 0f)`.
5. `Fallback()` — taken when no model is ready — returns `(0, false, 0f)` as well.

The two are byte-identical in the row. This is DFD-5 (row-level provenance), already declared out of
scope at runbook §1.5.

`[inference]` `MLModelManager.InitializeAsync` retrains from seed data and sets `IsReady = true` even
when no valid model is on disk, and §1.4 confirmed a model file is present — so the ML branch
probably *did* run. **Not verified against the production DI wiring, and unfalsifiable from this
row.** Do not record it as an observation.

**What the logged `0` is not:** it is not a dropped value. `PredictedMinutes` is `int?` and
`Confidence` is `float?` end to end; a value dropped in transit arrives as `NULL`, which is exactly
what the two pre-fix rows show. `NULL → 0.0` in the same output is the transition the check was
looking for. `StudiedMinutesSoFar` independently corroborates it — `1.0` on the first new row and
`0.0` on the second, each matching its own task's pre-session `ThoiGianDaHoc`, so a per-task value is
demonstrably flowing through that write block rather than a constant.

**What this run therefore does not show:** no row in it carries a *non-zero* prediction, because every
task in this database is past its deadline. Runbook §3.1 describes the optional extra session that
would produce one.

---

## 6. Follow-ups raised by this run — not part of the verdict

- The confidence expression can only ever return `0` or `1` when `formula = 0`. That is **F-1**
  (M8-A confidence-gate calibration), already deferred; runbook §1.5 puts it out of scope.
- Every task in the Debug database is overdue and every `DiemUuTien` is `0.0`. That is test-data
  staleness, not a defect — but it means the dashboard's top-5 ordering is currently all ties.
